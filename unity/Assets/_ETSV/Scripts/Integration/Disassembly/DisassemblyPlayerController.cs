// [ETSV] Integración — interacción FPS con objetos desmontables (solo jugador local).
// Se agrega en runtime al avatar local (ETSVBootstrap); NO modifica PlayerInteraction
// ni PlayerMovement de la base: REUTILIZA su raycast (InteractRay/InteractRange) y
// su acción de input "Interact" (E / gamepad buttonNorth, Hold ~0.4 s — el mismo
// gesto que el pickup upstream).
//
// El cliente SOLO solicita (RPCs al host); nunca decide resultados:
//   - mantener E sobre una fijación accesible  → StartWork + ticks de progreso
//   - soltar E                                 → StopWork (el progreso queda guardado)
//   - mantener E sobre componente Liberado     → ExtractRpc
//   - mantener E sobre componente con herramienta incorrecta → DamageRpc (golpe,
//     paridad con el prototipo JS: el daño viene de acciones destructivas)
//   - piezas sueltas (PickupItem)              → las maneja PlayerInteraction upstream
// Herramientas: teclas 1/2/3 (llave/destornillador/manos, GameCatalog.Tools[i].Key),
// la selección viaja al host (registro autoritativo de DisassemblySessionHost).

using System.Collections.Generic;
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

        private const float TickInterval = 0.1f;  // 10 Hz de solicitudes de trabajo
        private const byte StrikeDamage = 12;     // paridad con prototype/tests/disassembly.test.mjs

        private PlayerMovement _movement;
        private InputAction _interact;
        private InputAction _tool1, _tool2, _tool3;

        private DisassemblyObjectView _view;
        private int _targetComp = -1, _targetFastener = -1; // -1 = cuerpo del componente
        private float _tickAccum;
        private RaycastHit[] _hitsBuf = new RaycastHit[16];

        private void Awake()
        {
            Current = this;
            _movement = GetComponent<PlayerMovement>();

            var pi = GetComponent<PlayerInput>();
            if (pi != null && pi.actions != null) _interact = pi.actions["Interact"];

            // Selección de herramienta: bindings inline (mismo patrón que la rotación
            // de items upstream, que no toca el asset compartido).
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
            RefreshTarget();

            if (IsWorking)
            {
                // Sin target (el jugador apartó la mira o el estado cambió) ⇒ interrumpir.
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
            StopWorking(); // cambiar de herramienta interrumpe el trabajo en curso
            if (ETSVSessionView.Instance != null)
                ETSVSessionView.Instance.SelectToolRpc((byte)idx);
        }

        // ------------------------------------------------------------------ targeting
        private void RefreshTarget()
        {
            HasTarget = false;
            _view = null;
            _targetComp = -1;
            _targetFastener = -1;
            ObjectName = CompName = StateLabel = Hint = FastenerLabel = RequiredToolLabel = null;
            Progress01 = 0; Quality = 100;

            if (_movement == null) return;
            int n = Physics.RaycastNonAlloc(_movement.InteractRay, _hitsBuf, _movement.InteractRange);
            if (n == 0) return;
            System.Array.Sort(_hitsBuf, 0, n, HitDistanceComparer.Instance);

            // Prioridad: fijación más cercana; si no, componente; PickupItem se ignora
            // (lo maneja el sistema upstream, sin doble manejo).
            DisassemblyObjectView view = null;
            int comp = -1, fast = -1;
            for (int i = 0; i < n; i++)
            {
                var col = _hitsBuf[i].collider;
                if (col == null) continue;
                if (col.GetComponent<PickupItem>() != null) return; // pieza suelta → upstream

                var fr = col.GetComponent<FastenerRef>();
                if (fr != null && fast < 0)
                {
                    view = fr.GetComponentInParent<DisassemblyObjectView>();
                    comp = fr.CompIdx; fast = fr.FastenerIdx;
                    if (view != null) break; // la fijación manda
                }
                var cr = col.GetComponent<ComponentRef>();
                if (cr != null && comp < 0)
                {
                    var v2 = cr.GetComponentInParent<DisassemblyObjectView>();
                    if (v2 != null) { view = v2; comp = cr.CompIdx; }
                }
            }
            if (view == null || comp < 0) return;

            _view = view; _targetComp = comp; _targetFastener = fast;
            HasTarget = true;
            BuildHudInfo(view, comp, fast);
        }

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
                    // Golpe solo si la herramienta equipada no sirve para NINGUNA fijación.
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
                if (ft.Tool == tool) return false; // alguna fijación sirve con esta herramienta
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
            if (_view == null || _targetComp < 0) return;

            CompSync s;
            if (!_view.TryGetComp(_targetComp, out s)) return;
            var state = (CompState)s.State;

            if (_targetFastener >= 0)
            {
                if (state != CompState.Accesible) return; // el host también lo valida
                var r = _view; byte c = (byte)_targetComp, f = (byte)_targetFastener;
                r.WorkStartRpc(c, f);
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
                && WrongToolForWholeComp(_view.Blueprint.Components[_targetComp]))
            {
                _view.DamageRpc((byte)_targetComp, StrikeDamage); // golpe con herramienta incorrecta
            }
        }

        private void OnInteractCanceled(InputAction.CallbackContext _) => StopWorking();

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
