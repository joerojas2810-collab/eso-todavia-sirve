// [ETSV] Integración — interacción FPS con objetos desmontables Y estaciones
// (solo jugador local). Se agrega en runtime al avatar local (ETSVBootstrap);
// NO modifica PlayerInteraction ni PlayerMovement de la base: REUTILIZA su
// raycast (InteractRay/InteractRange) y su acción "Interact" (E, Hold ~0.4 s).
//
// El cliente SOLO solicita (RPCs al host); nunca decide resultados:
//   - mantener E sobre una fijación accesible  → StartWork + ticks de progreso
//   - soltar E                                 → StopWork (progreso guardado)
//   - mantener E sobre componente Liberado     → ExtractRpc
//   - mantener E sobre componente con herramienta incorrecta → DamageRpc (golpe)
//   - mantener E sobre una ESTACIÓN            → StationUseRpc (balanza/trituradora/
//     residuos/almacenes/contenedores: el host valida y ejecuta la economía)
//   - piezas sueltas (PickupItem)              → las maneja PlayerInteraction upstream
// Herramientas: teclas 1/2/3 (GameCatalog.Tools[i].Key), selección viaja al host.

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Etsv.Domain;

namespace Etsv.Integration
{
    public class DisassemblyPlayerController : MonoBehaviour
    {
        public static DisassemblyPlayerController Current { get; private set; }

        // ---- info pública para el HUD (derivada del estado sincronizado, nunca autoritativa) ----
        public bool HasTarget { get; private set; }
        public string ObjectName { get; private set; }
        public string CompName { get; private set; }
        public string StateLabel { get; private set; }
        public string Hint { get; private set; }
        public string FastenerLabel { get; private set; }
        public string RequiredToolLabel { get; private set; }
        public double Progress01 { get; private set; }
        public int Quality { get; private set; }
        public int ToolIdx { get; private set; }
        public bool IsWorking { get; private set; }

        /// <summary>Pieza que este jugador transporta (físico HeldBy = cliente local).</summary>
        public DisassemblyPieceView CarriedPiece { get; private set; }

        private const float TickInterval = 0.1f;  // 10 Hz de solicitudes de trabajo
        private const byte StrikeDamage = 12;     // paridad con prototype/tests/disassembly.test.mjs

        private PlayerMovement _movement;
        private InputAction _interact;
        private InputAction _tool1, _tool2, _tool3;

        private DisassemblyObjectView _view;
        private int _targetComp = -1, _targetFastener = -1; // -1 = cuerpo del componente
        private StationRef _station;                        // estación apuntada
        private float _tickAccum;
        private RaycastHit[] _hitsBuf = new RaycastHit[16];

        private void Awake()
        {
            Current = this;
            _movement = GetComponent<PlayerMovement>();

            var pi = GetComponent<PlayerInput>();
            if (pi != null && pi.actions != null) _interact = pi.actions["Interact"];

            _tool1 = NewKeyAction("Tool1", GameCatalog.Tools.Length > 0 ? GameCatalog.Tools[0].Key : "1");
            _tool2 = NewKeyAction("Tool2", GameCatalog.Tools.Length > 1 ? GameCatalog.Tools[1].Key : "2");
            _tool3 = NewKeyAction("Tool3", GameCatalog.Tools.Length > 2 ? GameCatalog.Tools[2].Key : "3");
            ToolIdx = GameCatalog.Tools.Length - 1; // arranca con "manos"
        }

        private static InputAction NewKeyAction(string name, string key)
        {
            var a = new InputAction(name, InputActionType.Button);
            a.AddBinding("<Keyboard>/digit" + key);
            a.Enable();
            return a;
        }

        private void OnEnable()
        {
            if (_interact != null)
            {
                _interact.performed += OnInteractPerformed;
                _interact.canceled += OnInteractCanceled;
            }
        }

        private void OnDisable()
        {
            if (_interact != null)
            {
                _interact.performed -= OnInteractPerformed;
                _interact.canceled -= OnInteractCanceled;
            }
            StopWorking();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            _tool1?.Dispose(); _tool2?.Dispose(); _tool3?.Dispose();
        }

        private void Update()
        {
            HandleToolKeys();
            RefreshCarried();
            RefreshTarget();

            if (IsWorking)
            {
                if (_view == null || _targetFastener < 0) { StopWorking(); return; }
                _tickAccum += Time.deltaTime;
                while (_tickAccum >= TickInterval)
                {
                    _tickAccum -= TickInterval;
                    _view.WorkTickRpc((byte)_targetComp, (byte)_targetFastener, TickInterval);
                }
            }
        }

        private void HandleToolKeys()
        {
            int idx = -1;
            if (_tool1 != null && _tool1.WasPressedThisFrame()) idx = 0;
            else if (_tool2 != null && _tool2.WasPressedThisFrame()) idx = 1;
            else if (_tool3 != null && _tool3.WasPressedThisFrame()) idx = 2;
            if (idx < 0 || idx == ToolIdx || idx >= GameCatalog.Tools.Length) return;

            ToolIdx = idx;
            StopWorking();
            if (ETSVSessionView.Instance != null)
                ETSVSessionView.Instance.SelectToolRpc((byte)idx);
        }

        /// <summary>Pieza en manos del jugador LOCAL (HeldBy es legible por todos).</summary>
        private void RefreshCarried()
        {
            CarriedPiece = null;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null) return;
            ulong me = nm.LocalClient.ClientId;
            foreach (var kv in DisassemblyPieceView.Registry)
            {
                var pv = kv.Value;
                if (pv == null) continue;
                var pickup = pv.GetComponent<PickupItem>();
                if (pickup != null && pickup.HeldBy.Value == me) { CarriedPiece = pv; return; }
            }
        }

        // ------------------------------------------------------------------ targeting
        private void RefreshTarget()
        {
            HasTarget = false;
            _view = null;
            _targetComp = -1;
            _targetFastener = -1;
            _station = null;
            ObjectName = CompName = StateLabel = Hint = FastenerLabel = RequiredToolLabel = null;
            Progress01 = 0; Quality = 100;

            if (_movement == null) return;
            int n = Physics.RaycastNonAlloc(_movement.InteractRay, _hitsBuf, _movement.InteractRange);
            if (n == 0) return;
            System.Array.Sort(_hitsBuf, 0, n, HitDistanceComparer.Instance);

            DisassemblyObjectView view = null;
            int comp = -1, fast = -1;
            float compDist = float.MaxValue, stationDist = float.MaxValue;
            StationRef station = null;

            for (int i = 0; i < n; i++)
            {
                var col = _hitsBuf[i].collider;
                if (col == null) continue;
                if (col.GetComponent<PickupItem>() != null) return; // pieza suelta → upstream

                var fr = col.GetComponent<FastenerRef>();
                if (fr != null && fast < 0)
                {
                    var v = fr.GetComponentInParent<DisassemblyObjectView>();
                    if (v != null) { view = v; comp = fr.CompIdx; fast = fr.FastenerIdx; break; } // la fijación manda
                }
                var sr = col.GetComponent<StationRef>();
                if (sr != null && station == null) { station = sr; stationDist = _hitsBuf[i].distance; continue; }
                var cr = col.GetComponent<ComponentRef>();
                if (cr != null && comp < 0)
                {
                    var v2 = cr.GetComponentInParent<DisassemblyObjectView>();
                    if (v2 != null) { view = v2; comp = cr.CompIdx; compDist = _hitsBuf[i].distance; }
                }
            }

            // Estación vs componente: gana el más cercano.
            if (station != null && (comp < 0 || stationDist <= compDist))
            {
                _station = station;
                HasTarget = true;
                BuildStationHud(station);
                return;
            }
            if (view == null || comp < 0) return;

            _view = view; _targetComp = comp; _targetFastener = fast;
            HasTarget = true;
            BuildHudInfo(view, comp, fast);
        }

        // ------------------------------------------------------------------ HUD de objeto desmontable
        private void BuildHudInfo(DisassemblyObjectView view, int comp, int fast)
        {
            var bp = view.Blueprint;
            if (bp == null) return;
            ObjectName = bp.Name;
            var def = bp.Components[comp];
            CompName = def.Name;

            CompSync s;
            if (!view.TryGetComp(comp, out s)) return;
            var state = (CompState)s.State;
            Quality = s.Quality;
            StateLabel = StateName(state);

            if (fast >= 0 && def.Fasteners != null && fast < def.Fasteners.Length)
            {
                var ft = GameCatalog.FastenerTypes[def.Fasteners[fast].Type];
                ulong field = (s.FastenerBits >> (fast * 8)) & 0xFF;
                bool removed = (field & 0x80) != 0;
                int q = (int)(field & 0x7F);
                Progress01 = removed ? 1.0 : q / 127.0;
                FastenerLabel = "Fijación " + (fast + 1) + "/" + def.Fasteners.Length + " — " + ft.Name;
                RequiredToolLabel = ft.Tool == CurrentToolId() ? "✔ " + ft.Tool : "Necesitás: " + ToolName(ft.Tool);
                if (removed) Hint = "Fijación retirada";
                else if (state != CompState.Accesible) Hint = HintForState(state, bp, def);
                else if (ft.Tool != CurrentToolId()) Hint = "Equipá " + ToolName(ft.Tool) + " (tecla " + ToolKeyOf(ft.Tool) + ")";
                else Hint = IsWorking ? "Trabajando… (" + ft.Verb.ToLowerInvariant() + ")" : "Mantené E para " + ft.Verb.ToLowerInvariant();
            }
            else
            {
                FastenerLabel = null;
                RequiredToolLabel = null;
                Hint = HintForState(state, bp, def);
            }
        }

        // ------------------------------------------------------------------ HUD de estación
        private void BuildStationHud(StationRef sr)
        {
            var def = StationsCatalog.ById(sr.StationId);
            if (def == null) return;
            ObjectName = def.Name;
            var session = ETSVSessionView.Instance;

            string carried = CarriedPiece != null ? CarriedPiece.DefId : null;
            int carriedQ = CarriedPiece != null ? CarriedPiece.Quality.Value : 100;

            switch (def.Kind)
            {
                case StationKind.Balanza:
                    CompName = "Venta de piezas";
                    if (carried != null)
                    {
                        int v = EstimateValue(carried, carriedQ);
                        StateLabel = IsSealedDef(carried) ? "Sellado: no vendible" : "Lista para vender";
                        Hint = IsSealedDef(carried)
                            ? "El módulo sellado no se vende (residuo especial)"
                            : "Mantené E para vender por $" + v;
                    }
                    else { StateLabel = "Sin pieza en las manos"; Hint = "Levantá una pieza (E) y volvé"; }
                    break;

                case StationKind.Trituradora:
                    CompName = "Procesamiento (E=0.9, PROV-01)";
                    if (carried != null)
                    {
                        StateLabel = IsSealedDef(carried) ? "Sellado: incompatible" : "Lista para procesar";
                        Hint = IsSealedDef(carried)
                            ? "El módulo sellado no entra en maquinaria común"
                            : "Mantené E para procesar → " + RecipePreview(carried, carriedQ);
                    }
                    else { StateLabel = "Sin pieza en las manos"; Hint = "Levantá un componente y volvé"; }
                    break;

                case StationKind.ResiduosEspeciales:
                    CompName = "Gestión con coste (GDD §6.2: −$5/u)";
                    if (carried != null && IsSealedDef(carried))
                    {
                        int cost = SpecialCost(carried);
                        int money = session != null ? session.Money.Value : 0;
                        StateLabel = money >= cost ? "Entrega disponible" : "Fondos insuficientes";
                        Hint = "Mantené E para entregar (coste $" + cost + ")";
                    }
                    else { StateLabel = "Solo residuos especiales"; Hint = "Traé el módulo eléctrico sellado"; }
                    break;

                case StationKind.Estanteria:
                case StationKind.Pale:
                case StationKind.Suelo:
                {
                    int zoneIdx = def.Kind == StationKind.Estanteria ? 0 : def.Kind == StationKind.Pale ? 1 : 2;
                    byte used = 0; int cap = 8;
                    if (session != null && zoneIdx < session.ZoneCounts.Count) used = session.ZoneCounts[zoneIdx];
                    var zone = EconomyData.StorageZones[def.ZoneId];
                    cap = zone.Capacity;
                    CompName = "Almacén " + string.Join("/", zone.Accepts) + " · " + used + "/" + cap;
                    if (CarriedPiece != null) { StateLabel = "Almacenar"; Hint = "Mantené E para guardar " + carried; }
                    else { StateLabel = used > 0 ? "Retirar disponible" : "Vacía"; Hint = "Mantené E para retirar la última pieza"; }
                    break;
                }

                case StationKind.ContenedorMaterial:
                {
                    int matIdx = System.Array.IndexOf(EconomyData.MatIds, def.MatId);
                    ushort units = 0;
                    if (session != null && matIdx >= 0 && matIdx < session.MaterialUnits.Count)
                        units = session.MaterialUnits[matIdx];
                    var m = EconomyData.Materials[def.MatId];
                    CompName = m.Name + " · " + units + "/120 u (PROV-05)";
                    if (units > 0 && m.Price > 0)
                    {
                        StateLabel = "Vender todo";
                        Hint = "Mantené E: " + units + " u × $" + m.Price + " = $" + (units * m.Price);
                    }
                    else if (m.Price <= 0) { StateLabel = "No vendible"; Hint = "Los residuos especiales se gestionan en su contenedor"; }
                    else { StateLabel = "Vacío"; Hint = "Procesá componentes que rindan " + m.Name; }
                    break;
                }
            }
            Quality = carriedQ;
        }

        private static int EstimateValue(string defId, int quality)
        {
            int vbase = VBaseOf(defId);
            return JsMath.Round(vbase * (quality / 100.0) * EconomyData.CommercialMod);
        }

        /// <summary>VBase por definición (componente de blueprint o PART_DEF).</summary>
        public static int VBaseOf(string defId)
        {
            if (defId == null) return 0;
            foreach (var bp in CatalogIndex.Blueprints)
            {
                var cd = GameCatalog.CompById(bp, defId);
                if (cd != null) return cd.VBase;
            }
            PartDef pd;
            if (EconomyData.PartDefs.TryGetValue(defId, out pd)) return pd.VBase;
            return 0;
        }

        private static bool IsSealedDef(string defId)
        {
            foreach (var bp in CatalogIndex.Blueprints)
            {
                var cd = GameCatalog.CompById(bp, defId);
                if (cd != null) return cd.Sealed;
            }
            return false;
        }

        private static int SpecialCost(string defId)
        {
            foreach (var bp in CatalogIndex.Blueprints)
            {
                var cd = GameCatalog.CompById(bp, defId);
                if (cd != null && cd.Recipe != null)
                    foreach (var r in cd.Recipe)
                        if (r.Mat == "MAT-08")
                            return (int)(r.Base * System.Math.Abs(EconomyData.Materials["MAT-08"].Price));
            }
            return 0;
        }

        private string RecipePreview(string defId, int quality)
        {
            RecipeEntry[] recipe = null;
            foreach (var bp in CatalogIndex.Blueprints)
            {
                var cd = GameCatalog.CompById(bp, defId);
                if (cd != null) { recipe = cd.Recipe; break; }
            }
            if (recipe == null || recipe.Length == 0) return "sin receta";
            var parts = new List<string>();
            double P = quality / 100.0;
            foreach (var r in recipe)
            {
                if (r.Mat != null)
                {
                    var m = EconomyData.Materials[r.Mat];
                    parts.Add(m.Name + " ~" + (r.Base * 0.9 * P).ToString("0.#") + "u"); // E=0.9 PROV-01
                }
                if (r.Part != null)
                {
                    PartDef pd;
                    string nm = EconomyData.PartDefs.TryGetValue(r.Part, out pd) ? pd.Name : r.Part;
                    parts.Add(nm + " ×" + (int)r.Base);
                }
            }
            return string.Join(", ", parts.ToArray());
        }

        // ------------------------------------------------------------------ pistas de componente
        private string HintForState(CompState state, ObjectBlueprint bp, ComponentDef def)
        {
            switch (state)
            {
                case CompState.Liberado:
                    return "Mantené E para extraer";
                case CompState.Bloqueado:
                    if (def.BlockedBy != null && def.BlockedBy.Length > 0)
                    {
                        var names = new List<string>();
                        foreach (var b in def.BlockedBy)
                        {
                            var cd = GameCatalog.CompById(bp, b);
                            names.Add(cd != null ? cd.Name : b);
                        }
                        return "Bloqueado por: " + string.Join(", ", names.ToArray());
                    }
                    return "Bloqueado";
                case CompState.Accesible:
                    if (WrongToolForWholeComp(def)) return "Mantené E: golpear con " + ToolName(CurrentToolId()) + " lo va a dañar";
                    return def.Fasteners != null && def.Fasteners.Length > 0
                        ? "Apuntá a una fijación (esferas rojas)"
                        : "Mantené E para extraer";
                default:
                    return null;
            }
        }

        private bool WrongToolForWholeComp(ComponentDef def)
        {
            string tool = CurrentToolId();
            if (def.Fasteners == null || def.Fasteners.Length == 0) return false;
            foreach (var f in def.Fasteners)
            {
                var ft = GameCatalog.FastenerTypes[f.Type];
                if (ft.Tool == tool) return false;
            }
            return true;
        }

        private static string StateName(CompState s)
        {
            switch (s)
            {
                case CompState.Bloqueado: return "Bloqueado";
                case CompState.Accesible: return "Accesible";
                case CompState.Liberado: return "Liberado";
                case CompState.Extraido: return "Extraído";
                case CompState.Procesado: return "Procesado";
                default: return "Vendido";
            }
        }

        private string CurrentToolId()
        {
            return ToolIdx >= 0 && ToolIdx < GameCatalog.Tools.Length ? GameCatalog.Tools[ToolIdx].Id : "manos";
        }

        private static string ToolName(string toolId)
        {
            var t = GameCatalog.ToolById(toolId);
            return t != null ? t.Name : toolId;
        }

        private static string ToolKeyOf(string toolId)
        {
            var t = GameCatalog.ToolById(toolId);
            return t != null ? t.Key : "?";
        }

        // ------------------------------------------------------------------ acciones (solicitudes al host)
        private void OnInteractPerformed(InputAction.CallbackContext _)
        {
            if (_station != null)
            {
                if (ETSVSessionView.Instance != null)
                    ETSVSessionView.Instance.StationUseRpc((byte)_station.StationId);
                return;
            }
            if (_view == null || _targetComp < 0) return;

            CompSync s;
            if (!_view.TryGetComp(_targetComp, out s)) return;
            var state = (CompState)s.State;

            if (_targetFastener >= 0)
            {
                if (state != CompState.Accesible) return; // el host también lo valida
                _view.WorkStartRpc((byte)_targetComp, (byte)_targetFastener);
                IsWorking = true;
                _tickAccum = 0f;
                return;
            }

            if (state == CompState.Liberado)
            {
                _view.ExtractRpc((byte)_targetComp);
                return;
            }

            if ((state == CompState.Accesible || state == CompState.Bloqueado)
                && _view.Blueprint != null
                && WrongToolForWholeComp(_view.Blueprint.Components[_targetComp]))
            {
                _view.DamageRpc((byte)_targetComp, StrikeDamage); // golpe con herramienta incorrecta
            }
        }

        private void OnInteractCanceled(InputAction.CallbackContext _) { StopWorking(); }

        private void StopWorking()
        {
            if (!IsWorking) return;
            IsWorking = false;
            _tickAccum = 0f;
            if (_view != null && _targetComp >= 0 && _targetFastener >= 0)
                _view.WorkStopRpc((byte)_targetComp, (byte)_targetFastener);
        }

        private class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }
    }
}
