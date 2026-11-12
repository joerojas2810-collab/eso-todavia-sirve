// [ETSV] Dominio puro — ESTADO PERSISTIBLE.
// Port 1:1 de prototype/js/state/gameState.js (VERSION = 3) + validación de
// integridad nueva (Instrucción 05 §6). POCOs serializables: la conversión a
// JSON/archivo vive en la capa Integration (Newtonsoft/JsonUtility), acá solo
// existen el DTO, el versionado, la validación y la restauración lógica.
// PROV-11: las piezas físicas en mundo/mano/balanza no se restauran.

using System.Collections.Generic;

namespace Etsv.Domain
{
    /// <summary>Estado de juego agregado (runtime): dinero + registro + almacenes.</summary>
    public class GameStateData
    {
        public int Money;
        public PieceRegistry Reg;
        public ComponentWarehouse Cwh;
        public MaterialWarehouse Mwh;
    }

    /// <summary>DTO de guardado (versionado). Equivalente al objeto JSON del prototipo.
    /// Instrucción 08: los campos v4 (SaveId, Yard, PiecePoses, Counters, Progress)
    /// se agregan al MISMO DTO — un único formato, sin sistema paralelo. En un
    /// archivo v3 llegan null y la migración explícita los completa.</summary>
    public class SaveGame
    {
        public int Version;
        public int Money;
        public List<StoredPart> Pieces;
        public WarehouseSnapshot Warehouses;

        // ---- v4 (formato completo de producción, Instrucción 08) ----
        public string SaveId;                 // identificador único de partida
        public string SavedAtUtc;             // ISO-8601 del momento del guardado
        public List<YardObject> Yard;         // objetos del patio (desmontaje persistido)
        public List<PiecePose> PiecePoses;    // posición/orientación de piezas físicas
        public SaveCounters Counters;         // contadores de identidad (anti-colisión)
        public ProgressData Progress;         // herramientas/mejoras + estructuras reservadas
    }

    public static partial class SaveSerializer
    {
        public static GameStateData CreateGameState()
        {
            return new GameStateData
            {
                Money = 0,
                Reg = new PieceRegistry(),
                Cwh = WarehouseRules.CreateComponentWarehouse(),
                Mwh = WarehouseRules.CreateMaterialWarehouse(),
            };
        }

        public static SaveGame Serialize(GameStateData gs)
        {
            // Las piezas en world/carried/scale no persisten (PROV-11): se descartan.
            var pieces = new List<StoredPart>();
            foreach (var p in PieceRules.SerializeRegistry(gs.Reg))
                if (p.Terminal != null || (p.Storage != null && p.Storage.StartsWith("stored:")))
                    pieces.Add(p);
            return new SaveGame
            {
                Version = GameInfo.SaveVersion,
                Money = gs.Money,
                Pieces = pieces,
                Warehouses = WarehouseRules.SerializeWarehouses(gs.Cwh, gs.Mwh),
            };
        }

        /// <summary>Restauración lógica: null si la versión no coincide o el DTO es inválido.</summary>
        public static GameStateData Deserialize(SaveGame data)
        {
            if (data == null || data.Version != GameInfo.SaveVersion) return null;
            var gs = CreateGameState();
            gs.Money = data.Money;
            gs.Reg = PieceRules.DeserializeRegistry(data.Pieces ?? new List<StoredPart>());
            WarehouseRules.DeserializeInto(gs.Cwh, gs.Mwh, data.Warehouses ?? new WarehouseSnapshot());
            return gs;
        }
    }

    /// <summary>
    /// Validación de integridad del DTO (anti-corrupción de save): revisa
    /// invariantes del GDD 5.0 §8.3 antes de restaurar. Devuelve la lista de
    /// problemas encontrados (vacía = íntegro).
    /// </summary>
    public static partial class SaveValidator
    {
        public static readonly string[] ReservationPurposes = { "contrato", "fabricacion", "mantenimiento", "construccion" };

        public static List<string> Validate(SaveGame s)
        {
            if (s != null && s.Version == GameInfo.SaveVersionFull) return ValidateFull(s);
            var problems = new List<string>();
            if (s == null) { problems.Add("save nulo"); return problems; }
            if (s.Version != GameInfo.SaveVersion) problems.Add("version_incompatible:" + s.Version);
            if (s.Money < 0) problems.Add("dinero_negativo"); // anti-quiebra GDD §6.6

            var byUid = new HashSet<string>();
            var pieces = s.Pieces ?? new List<StoredPart>();
            foreach (var p in pieces)
            {
                if (p.Uid == null || !byUid.Add(p.Uid)) { problems.Add("uid_duplicado_o_nulo:" + p.Uid); continue; }
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
                    if (p.Storage == "world" || p.Storage == "carried" || p.Storage == "scale")
                        problems.Add("fisica_no_persistible:" + p.Uid); // PROV-11: no deberían estar en el save
                }
                if (p.ReservedFor != null && System.Array.IndexOf(ReservationPurposes, p.ReservedFor) < 0)
                    problems.Add("reserva_desconocida:" + p.Uid);
            }

            var snap = s.Warehouses;
            if (snap != null)
            {
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
                // toda pieza stored:<zona> debe estar listada en su zona
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
                foreach (var kv in snap.Acc)
                {
                    if (!EconomyData.Materials.ContainsKey(kv.Key)) { problems.Add("material_desconocido:" + kv.Key); continue; }
                    if (kv.Value < 0) problems.Add("acumulador_negativo:" + kv.Key);
                    if (kv.Value > EconomyData.MaterialContainerCap) problems.Add("acumulador_sobre_capacidad:" + kv.Key);
                }
            }
            return problems;
        }
    }
}
