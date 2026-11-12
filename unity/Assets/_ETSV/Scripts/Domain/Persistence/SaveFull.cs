// [ETSV] Dominio puro — FORMATO COMPLETO DE GUARDADO v4 (Instrucción 08).
// Fuente de verdad: GDD 5.0 §16 (datos persistentes, guardado seguro,
// restauración y versionado con migraciones explícitas).
//
// Capas separadas por diseño (§7 de la instrucción):
//   - Serialización:     SaveSerializer.SerializeFull / DeserializeFull (acá).
//   - Validación:        SaveValidator.ValidateFull (acá).
//   - Migración:         SaveMigrations.Migrate3To4 (acá, explícita y versionada).
//   - Reconstrucción lógica:  SessionPersistence.RestoreInto (capa Sim).
//   - Reconstrucción física:  ETSVBootstrap/PieceSpawner (capa Unity).
//   - Sincronización de red:  ETSVSessionView (NVs/NetworkLists existentes).
//
// POLÍTICA DETERMINISTA DE TRANSPORTE (GDD §16.4: "las interacciones manuales
// incompletas pueden cancelarse sin penalización"): una pieza carried/scale al
// guardar se persiste como world con la pose del portador (CarriedBy conserva
// la trazabilidad). Al cargar, la pieza aparece en el suelo: no se pierde, no
// se duplica, nadie reaparece transportando. Esta política reemplaza a PROV-11
// en el proyecto Unity; el prototipo JS conserva su comportamiento congelado y
// SaveSerializer.Serialize (v3) queda intacto para la paridad.

using System.Collections.Generic;

namespace Etsv.Domain
{
    // ------------------------------------------------------------------ DTOs v4

    /// <summary>Pose física de una pieza no terminal (mundo). Yaw en grados.</summary>
    public class PiecePose
    {
        public string Uid;
        public double X, Y, Z;
        public double Yaw;
        /// <summary>playerTag ("c{clientId}") que la transportaba al guardar, si
        /// corresponde (trazabilidad; la pieza se restaura en el suelo).</summary>
        public string CarriedBy;
    }

    public class YardFastener
    {
        public bool Removed;
        public double Progress;   // 0..1 — precisión completa (NO cuantizada como CompSync)
    }

    public class YardComponent
    {
        public string Id;             // id del componente en el blueprint
        public int State;             // (int)CompState
        public int Quality;           // 0..100 (daño incluido)
        public string TerminalTag;    // balanza | trituradora | residuo_especial | null
        public List<YardFastener> Fasteners;
    }

    /// <summary>Objeto del patio: instancia de blueprint con su desmontaje persistido
    /// (GDD §16.1 "Objetos: instancias, componentes y estados") y pose física.</summary>
    public class YardObject
    {
        public string DomainId;       // "microondas_3"
        public string BlueprintId;    // "microondas"
        /// <summary>Huella del catálogo al crearse (GDD §16.5: las instancias
        /// conservan una versión compatible o se migran con reglas explícitas).</summary>
        public int BpFingerprint;
        public double X, Y, Z;
        public double Yaw;
        public List<YardComponent> Components;
    }

    /// <summary>Contadores de identidad: impiden colisiones de ids al seguir jugando.</summary>
    public class SaveCounters
    {
        public int NextInstance;      // siguiente número de instancia de objeto ("_N")
    }

    // Estructuras EXTENSIBLES reservadas (§2: preparar sin implementar contratos
    // ni progresión narrativa — GDD §16.1 Historia/Contratos/Máquinas/Automatización).
    public class ContractSlot
    {
        public string Id;
        public string State;          // reservado: estados del sistema de contratos
        public string Note;
    }

    public class NamedFlag
    {
        public string Key;
        public string Value;
    }

    /// <summary>Progreso económico/tecnológico disponible (GDD §16.1 Empresa/Herramientas).</summary>
    public class ProgressData
    {
        public List<string> ToolsOwned;        // herramientas adquiridas (ids del catálogo)
        public List<string> Upgrades;          // mejoras compradas (ids futuros)
        public List<ContractSlot> Contracts;   // RESERVADO: siempre vacío por ahora
        public List<NamedFlag> NarrativeFlags; // RESERVADO: siempre vacío por ahora
    }

    /// <summary>Entrada de la captura completa (lo aporta la capa Sim/Unity).</summary>
    public class SaveExtras
    {
        public string SaveId;
        public string SavedAtUtc;
        public List<YardObject> Yard;
        public List<PiecePose> PiecePoses;
        public SaveCounters Counters;
        public ProgressData Progress;
    }

    /// <summary>Salida de la restauración lógica completa.</summary>
    public class FullRestore
    {
        public GameStateData Gs;
        public string SaveId;
        public string SavedAtUtc;
        public List<YardObject> Yard;
        public List<PiecePose> PiecePoses;
        public SaveCounters Counters;
        public ProgressData Progress;
    }

    // ------------------------------------------------------------------ huella de catálogo
    public static class SaveFormat
    {
        /// <summary>Huella estable del blueprint (GDD §16.5): si el catálogo cambia
        /// (componentes, fijaciones, recetas), la huella cambia y la carga exige una
        /// migración explícita en vez de reinterpretar datos viejos.</summary>
        public static int BlueprintFingerprint(ObjectBlueprint bp)
        {
            if (bp == null) return 0;
            var sb = new System.Text.StringBuilder();
            sb.Append(bp.Id).Append(';');
            foreach (var c in bp.Components)
            {
                sb.Append(c.Id).Append(',').Append(c.Kg).Append(',').Append(c.WeightClass).Append(',')
                  .Append(c.VBase).Append(',').Append(c.Sealed ? 1 : 0).Append(',')
                  .Append(c.Special ? 1 : 0).Append(',').Append(c.IsProtected ? 1 : 0).Append(',');
                if (c.BlockedBy != null)
                    foreach (var b in c.BlockedBy) sb.Append(b).Append('+');
                sb.Append(',');
                if (c.Fasteners != null)
                    foreach (var f in c.Fasteners)
                    {
                        var ft = GameCatalog.FastenerTypeById(f.Type);
                        sb.Append(f.Type).Append(':');
                        if (ft != null) sb.Append(ft.Tool).Append('/').Append(ft.Time).Append('/').Append(ft.Verb);
                        sb.Append('+');
                    }
                sb.Append(',');
                if (c.Recipe != null)
                    foreach (var r in c.Recipe)
                        sb.Append(r.Mat ?? "-").Append('/').Append(r.Part ?? "-").Append('/').Append(r.Base).Append('+');
                sb.Append(';');
            }
            return Fnv1a(sb.ToString());
        }

        /// <summary>FNV-1a 32 bits (determinista entre plataformas).</summary>
        public static int Fnv1a(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                return (int)h;
            }
        }
    }

    // ------------------------------------------------------------------ serialización v4
    public static partial class SaveSerializer
    {
        /// <summary>Captura COMPLETA: todas las piezas (incluidas las físicas, con la
        /// política carried/scale → world), almacenes con fracciones, patio y progreso.
        /// No muta el estado vivo: trabaja sobre clones.</summary>
        public static SaveGame SerializeFull(GameStateData gs, SaveExtras ex)
        {
            var pieces = new List<StoredPart>();
            foreach (var p in PieceRules.SerializeRegistry(gs.Reg))
            {
                // Política determinista de transporte (ver cabecera): carried/scale
                // se persisten como world. La pieza NO reaparece en manos de nadie.
                if (p.Terminal == null && (p.Storage == "carried" || p.Storage == "scale"))
                    p.Storage = "world";
                pieces.Add(p);
            }

            var progress = ex != null && ex.Progress != null ? ex.Progress : DefaultProgress();
            return new SaveGame
            {
                Version = GameInfo.SaveVersionFull,
                Money = gs.Money,
                Pieces = pieces,
                Warehouses = WarehouseRules.SerializeWarehouses(gs.Cwh, gs.Mwh),
                SaveId = ex != null && ex.SaveId != null ? ex.SaveId : System.Guid.NewGuid().ToString("N"),
                SavedAtUtc = ex != null ? ex.SavedAtUtc : null,
                Yard = ex != null && ex.Yard != null ? ex.Yard : new List<YardObject>(),
                PiecePoses = ex != null && ex.PiecePoses != null ? ex.PiecePoses : new List<PiecePose>(),
                Counters = ex != null && ex.Counters != null ? ex.Counters : new SaveCounters { NextInstance = 1 },
                Progress = progress,
            };
        }

        /// <summary>Progreso por defecto: las herramientas del catálogo actual ya
        /// están disponibles (sin compras implementadas todavía); el resto, vacío.</summary>
        public static ProgressData DefaultProgress()
        {
            var tools = new List<string>();
            foreach (var t in GameCatalog.Tools) tools.Add(t.Id);
            return new ProgressData
            {
                ToolsOwned = tools,
                Upgrades = new List<string>(),
                Contracts = new List<ContractSlot>(),      // RESERVADO (GDD §16.1 Contratos)
                NarrativeFlags = new List<NamedFlag>(),    // RESERVADO (GDD §16.1 Historia)
            };
        }

        /// <summary>Restauración lógica v4. Null si la versión no es 4 (el llamador
        /// debe migrar antes con SaveMigrations.Migrate3To4).</summary>
        public static FullRestore DeserializeFull(SaveGame data)
        {
            if (data == null || data.Version != GameInfo.SaveVersionFull) return null;
            var gs = CreateGameState();
            gs.Money = data.Money;
            gs.Reg = PieceRules.DeserializeRegistry(data.Pieces ?? new List<StoredPart>());
            WarehouseRules.DeserializeInto(gs.Cwh, gs.Mwh, data.Warehouses ?? new WarehouseSnapshot());
            return new FullRestore
            {
                Gs = gs,
                SaveId = data.SaveId,
                SavedAtUtc = data.SavedAtUtc,
                Yard = data.Yard ?? new List<YardObject>(),
                PiecePoses = data.PiecePoses ?? new List<PiecePose>(),
                Counters = data.Counters ?? new SaveCounters { NextInstance = 1 },
                Progress = data.Progress ?? DefaultProgress(),
            };
        }
    }

    // ------------------------------------------------------------------ migraciones explícitas
    public static class SaveMigrations
    {
        /// <summary>Migración explícita v3 → v4 (Instrucción 08 §6; GDD §16.5).
        /// Reglas documentadas:
        ///  - Money/Pieces/Warehouses se conservan tal cual (v3 ya validaba sus invariantes).
        ///  - SaveId nuevo (una partida v3 no lo tenía) y SavedAtUtc nulo (desconocido).
        ///  - Yard vacío: v3 (prototipo) no persistía objetos del patio; la partida
        ///    migrada arranca sin objetos físicos, igual que un patio nuevo.
        ///  - PiecePoses vacío: v3 descartaba las piezas físicas (PROV-11); las
        ///    almacenadas/terminales no necesitan pose.
        ///  - Counters.NextInstance = 1 y Progress por defecto (herramientas del catálogo).
        /// Devuelve null si la entrada no es v3.</summary>
        public static SaveGame Migrate3To4(SaveGame v3)
        {
            if (v3 == null || v3.Version != GameInfo.SaveVersion) return null;
            return new SaveGame
            {
                Version = GameInfo.SaveVersionFull,
                Money = v3.Money,
                Pieces = v3.Pieces ?? new List<StoredPart>(),
                Warehouses = v3.Warehouses,
                SaveId = System.Guid.NewGuid().ToString("N"),
                SavedAtUtc = null,
                Yard = new List<YardObject>(),
                PiecePoses = new List<PiecePose>(),
                Counters = new SaveCounters { NextInstance = 1 },
                Progress = SaveSerializer.DefaultProgress(),
            };
        }

        /// <summary>Punto único de entrada: lleva cualquier versión soportada a v4.
        /// Devuelve null si la versión es desconocida (futuro → pasado no se inventa).</summary>
        public static SaveGame MigrateToLatest(SaveGame s)
        {
            if (s == null) return null;
            if (s.Version == GameInfo.SaveVersionFull) return s;
            if (s.Version == GameInfo.SaveVersion) return Migrate3To4(s);
            return null;
        }
    }

    // ------------------------------------------------------------------ validación v4
    public static partial class SaveValidator
    {
        /// <summary>Validación de integridad v4 (§6): identificadores, referencias
        /// cruzadas, estados, huellas de catálogo y capacidad. Vacía = íntegro.</summary>
        public static List<string> ValidateFull(SaveGame s)
        {
            var problems = new List<string>();
            if (s == null) { problems.Add("save nulo"); return problems; }
            if (s.Version != GameInfo.SaveVersionFull) { problems.Add("version_incompatible:" + s.Version); return problems; }
            if (s.SaveId == null || s.SaveId.Length == 0) problems.Add("saveid_vacio");
            if (s.Money < 0) problems.Add("dinero_negativo"); // anti-quiebra GDD §6.6

            var pieces = s.Pieces ?? new List<StoredPart>();
            var byUid = new HashSet<string>();
            foreach (var p in pieces)
            {
                if (p.Uid == null || !byUid.Add(p.Uid)) { problems.Add("uid_duplicado_o_nulo:" + p.Uid); continue; }
                if (p.DefId == null) problems.Add("defid_nulo:" + p.Uid);
                if (p.Quality < 0 || p.Quality > 100) problems.Add("calidad_fuera_rango:" + p.Uid);
                if (!EconomyData.WeightClasses.ContainsKey(p.WeightClass ?? "")) problems.Add("clase_invalida:" + p.Uid);
                if (p.Terminal != null)
                {
                    if (System.Array.IndexOf(PieceRules.TerminalStates, p.Terminal) < 0)
                        problems.Add("terminal_invalido:" + p.Uid);
                    if (p.Storage != "none") problems.Add("terminal_con_almacenamiento:" + p.Uid);
                }
                else
                {
                    if (p.Storage == "none") problems.Add("sin_terminal_storage_none:" + p.Uid);
                    // Política de transporte: carried/scale NUNCA llegan al archivo v4.
                    if (p.Storage == "carried" || p.Storage == "scale") problems.Add("transporte_en_save:" + p.Uid);
                }
                if (p.ReservedFor != null && System.Array.IndexOf(ReservationPurposes, p.ReservedFor) < 0)
                    problems.Add("reserva_desconocida:" + p.Uid);
            }

            // ---- poses: referencias cruzadas pieza ↔ pose ----
            var poses = s.PiecePoses ?? new List<PiecePose>();
            var posedUids = new HashSet<string>();
            foreach (var pose in poses)
            {
                if (pose.Uid == null || !posedUids.Add(pose.Uid)) { problems.Add("pose_duplicada_o_nula:" + pose.Uid); continue; }
                StoredPart p = null;
                foreach (var q in pieces) if (q.Uid == pose.Uid) { p = q; break; }
                if (p == null) { problems.Add("pose_uid_inexistente:" + pose.Uid); continue; }
                if (p.Terminal != null) problems.Add("pose_pieza_terminal:" + pose.Uid);
                else if (p.Storage != "world") problems.Add("pose_pieza_no_world:" + pose.Uid);
            }
            foreach (var p in pieces)
                if (p.Terminal == null && p.Storage == "world" && !posedUids.Contains(p.Uid))
                    problems.Add("world_sin_pose:" + p.Uid); // pieza física sin posición ⇒ reconstrucción imposible

            // ---- almacenes (mismas invariantes que v3) ----
            var snap = s.Warehouses;
            if (snap != null)
            {
                if (snap.Zones != null)
                    foreach (var kv in snap.Zones)
                    {
                        StorageZoneDef def;
                        if (!EconomyData.StorageZones.TryGetValue(kv.Key, out def)) { problems.Add("zona_desconocida:" + kv.Key); continue; }
                        foreach (var uid in kv.Value)
                        {
                            StoredPart p = null;
                            foreach (var q in pieces) if (q.Uid == uid) { p = q; break; }
                            if (p == null) { problems.Add("zona_uid_inexistente:" + uid); continue; }
                            if (p.Storage != "stored:" + kv.Key) problems.Add("zona_storage_inconsistente:" + uid);
                            if (System.Array.IndexOf(def.Accepts, p.WeightClass) < 0) problems.Add("zona_categoria_invalida:" + uid);
                            if (p.Terminal != null) problems.Add("zona_pieza_terminal:" + uid);
                        }
                        if (kv.Value.Count > def.Capacity) problems.Add("zona_sobre_capacidad:" + kv.Key);
                    }
                foreach (var p in pieces)
                {
                    if (p.Terminal == null && p.Storage != null && p.Storage.StartsWith("stored:"))
                    {
                        string zone = p.Storage.Substring(7);
                        List<string> items;
                        if (snap.Zones == null || !snap.Zones.TryGetValue(zone, out items) || !items.Contains(p.Uid))
                            problems.Add("stored_sin_zona:" + p.Uid);
                    }
                }
                if (snap.Acc != null)
                    foreach (var kv in snap.Acc)
                    {
                        if (!EconomyData.Materials.ContainsKey(kv.Key)) { problems.Add("material_desconocido:" + kv.Key); continue; }
                        if (kv.Value < 0) problems.Add("acumulador_negativo:" + kv.Key);
                        if (kv.Value > EconomyData.MaterialContainerCap) problems.Add("acumulador_sobre_capacidad:" + kv.Key);
                    }
            }

            // ---- patio: blueprints, huellas y estados de componentes ----
            var yard = s.Yard ?? new List<YardObject>();
            var domainIds = new HashSet<string>();
            foreach (var y in yard)
            {
                if (y.DomainId == null || !domainIds.Add(y.DomainId)) { problems.Add("yard_id_duplicado_o_nulo:" + y.DomainId); continue; }
                var bp = GameCatalog.ObjectById(y.BlueprintId);
                if (bp == null) { problems.Add("yard_blueprint_desconocido:" + y.BlueprintId); continue; }
                // GDD §16.5: instancia creada con otro catálogo ⇒ migración explícita obligatoria.
                if (y.BpFingerprint != SaveFormat.BlueprintFingerprint(bp))
                    problems.Add("blueprint_incompatible:" + y.DomainId);
                if (y.Components == null) { problems.Add("yard_sin_componentes:" + y.DomainId); continue; }
                foreach (var c in y.Components)
                {
                    var def = GameCatalog.CompById(bp, c.Id);
                    if (def == null) { problems.Add("yard_comp_desconocido:" + y.DomainId + "/" + c.Id); continue; }
                    if (c.State < 0 || c.State > 5) problems.Add("yard_estado_invalido:" + y.DomainId + "/" + c.Id);
                    if (c.Quality < 0 || c.Quality > 100) problems.Add("yard_calidad_invalida:" + y.DomainId + "/" + c.Id);
                    int nFast = def.Fasteners != null ? def.Fasteners.Length : 0;
                    if (c.Fasteners == null) c.Fasteners = new List<YardFastener>();
                    if (c.Fasteners.Count != nFast) problems.Add("yard_fijaciones_count:" + y.DomainId + "/" + c.Id);
                    foreach (var f in c.Fasteners)
                        if (f.Progress < 0 || f.Progress > 1) problems.Add("yard_progreso_rango:" + y.DomainId + "/" + c.Id);
                }
            }

            if (s.Counters != null && s.Counters.NextInstance < 1) problems.Add("contador_instancias_invalido");
            return problems;
        }
    }

    // ------------------------------------------------------------------ codec de instancia de desmontaje
    public static partial class DisassemblyRules
    {
        /// <summary>Estado completo de la instancia para el save v4: precisión total
        /// (progreso double, NO la cuantización de 7 bits de CompSync).</summary>
        public static List<YardComponent> SerializeInstance(DisassemblyInstance inst)
        {
            var list = new List<YardComponent>();
            if (inst == null) return list;
            foreach (var c in inst.Bp.Components)
            {
                ComponentRuntime rt;
                if (!inst.Comps.TryGetValue(c.Id, out rt)) continue;
                var yc = new YardComponent
                {
                    Id = rt.Id,
                    State = (int)rt.State,
                    Quality = rt.Quality,
                    TerminalTag = rt.TerminalTag,
                    Fasteners = new List<YardFastener>(),
                };
                foreach (var f in rt.Fasteners)
                    yc.Fasteners.Add(new YardFastener { Removed = f.Removed, Progress = f.Progress });
                list.Add(yc);
            }
            return list;
        }

        /// <summary>Reconstruye la instancia sobre el blueprint ACTUAL usando los
        /// estados guardados (clave: id de componente, no índice). Entrada ya
        /// validada (SaveValidator.ValidateFull). Los eventos NO se persisten: el
        /// log es derivable/episódico y arranca vacío (documentado en PERSISTENCIA.md).</summary>
        public static DisassemblyInstance DeserializeInstance(ObjectBlueprint bp, string domainId, List<YardComponent> saved)
        {
            var inst = CreateInstance(bp, domainId);
            if (saved == null) return inst;
            foreach (var yc in saved)
            {
                ComponentRuntime rt;
                if (!inst.Comps.TryGetValue(yc.Id, out rt)) continue; // componente quitado por migración: ignorar
                rt.State = (CompState)yc.State;
                rt.Quality = JsMath.ClampInt(yc.Quality, 0, 100);
                rt.TerminalTag = yc.TerminalTag;
                if (yc.Fasteners != null)
                    for (int i = 0; i < rt.Fasteners.Count && i < yc.Fasteners.Count; i++)
                    {
                        rt.Fasteners[i].Removed = yc.Fasteners[i].Removed;
                        rt.Fasteners[i].Progress = System.Math.Max(0, System.Math.Min(1, yc.Fasteners[i].Progress));
                    }
            }
            inst.Events = new List<DisassemblyEvent>(); // log episódico: arranca vacío
            RefreshAccessibility(inst);                 // dependencias resueltas coherentes
            return inst;
        }
    }
}
