// [ETSV] Capa Sim (C# PURO, assembly ETSV.Sim) — ECONOMÍA COOPERATIVA DE SESIÓN.
// Instrucción 07: conecta el desmontaje físico con balanza, trituradora,
// contenedor de residuos especiales, almacenes y contenedores de materiales.
//
// Modelo de propiedad (§2), cuatro conceptos DISTINTOS:
//  1. Transportista (quién lleva la pieza): clientId de NGO → registro _carrying
//     y CarryByPlayer del dominio (una pieza por jugador, GDD 8.1).
//  2. Propiedad legal de la pieza: Owner = SessionOwner = "chatarrera" (economía
//     compartida cooperativa conforme a los GDD; la trazabilidad de quién hizo
//     cada operación va en el log de transacciones, no en el owner).
//  3. Autoridad de red sobre el objeto físico: NGO (HeldBy server-write,
//     OwnerClientId) — NO se confunde con propiedad económica.
//  4. Titularidad de recursos de la chatarrería: dinero/almacenes/materiales son
//     de la sesión compartida (un solo GameStateData en el host).
//
// Toda operación: gate de presencia (identidad+distancia) → pre-chequeos →
// ops atómicas de ETSV.Domain → log con playerId → snapshot para sincronizar.

using System.Collections.Generic;
using Etsv.Domain;

namespace Etsv.Integration
{
    public enum StationKind : byte
    {
        Balanza = 0,
        Trituradora = 1,
        ResiduosEspeciales = 2,
        Estanteria = 3,
        Pale = 4,
        Suelo = 5,
        ContenedorMaterial = 6,
    }

    public class StationDef
    {
        public int Id;
        public StationKind Kind;
        public string Name;
        public string ZoneId;   // estaciones de almacenamiento
        public string MatId;    // contenedores de materiales
    }

    /// <summary>Catálogo de estaciones: determinista en todas las peers
    /// (ids estables para RPCs; las posiciones viven en ScrapyardLayout, Unity).</summary>
    public static class StationsCatalog
    {
        public static readonly StationDef[] Stations;

        static StationsCatalog()
        {
            var list = new List<StationDef>
            {
                new StationDef { Id = 0, Kind = StationKind.Balanza, Name = "Balanza" },
                new StationDef { Id = 1, Kind = StationKind.Trituradora, Name = "Trituradora básica" },
                new StationDef { Id = 2, Kind = StationKind.ResiduosEspeciales, Name = "Contenedor de residuos especiales" },
                new StationDef { Id = 3, Kind = StationKind.Estanteria, Name = "Estantería (P1)", ZoneId = "estanteria" },
                new StationDef { Id = 4, Kind = StationKind.Pale, Name = "Palé (P2)", ZoneId = "pale" },
                new StationDef { Id = 5, Kind = StationKind.Suelo, Name = "Suelo (P3+)", ZoneId = "suelo" },
            };
            for (int i = 0; i < EconomyData.MatIds.Length; i++)
            {
                var m = EconomyData.Materials[EconomyData.MatIds[i]];
                list.Add(new StationDef
                {
                    Id = 6 + i,
                    Kind = StationKind.ContenedorMaterial,
                    Name = "Contenedor " + m.Name,
                    MatId = m.Id,
                });
            }
            Stations = list.ToArray();
        }

        public static StationDef ById(int id)
        {
            return id >= 0 && id < Stations.Length ? Stations[id] : null;
        }
    }

    /// <summary>Códigos estables de motivo (razones del dominio → byte para la red).</summary>
    public static class ReasonCodes
    {
        private static readonly string[] Reasons =
        {
            "ok", "sin_pieza_en_manos", "pieza_inexistente", "pieza_terminal", "propiedad_ajena",
            "pieza_reservada", "no_transportada", "pieza_protegida_no_vendible", "zona_llena",
            "categoria_incompatible", "fondos_insuficientes", "maquina_incompatible_sellado",
            "residuo_especial_no_procesable", "contenedor_lleno", "sin_receta", "no_es_residuo_especial",
            "insuficiente", "zona_vacia", "manos_ocupadas", "requiere_carro_o_grua",
            "no_disponible_en_suelo", "distancia_invalida", "jugador_desconocido",
            "estacion_inexistente", "no_disponible_para_procesar", "no_vendible_desde",
            "ocupado_por_otro", "no_liberado", "estado", "ya", "herramienta",
        };

        public static byte Encode(string reason)
        {
            if (reason == null) return 0;
            for (byte i = 0; i < Reasons.Length; i++) if (Reasons[i] == reason) return i;
            return 1; // motivo no mapeado → genérico
        }

        public static string Decode(byte code)
        {
            return code < Reasons.Length ? Reasons[code] : "ok";
        }
    }

    /// <summary>Entrada del log de transacciones (trazabilidad cooperativa).</summary>
    public class TxEntry
    {
        public ulong PlayerId;
        public string Op;        // venta | procesado | entrega | almacenado | retirado | venta_material | llevar | soltar
        public string Uid;       // pieza involucrada (null para venta_material)
        public int Amount;       // dinero/unidades según op
        public bool Ok;
        public string Reason;    // clave estable de rechazo (null si Ok)
    }

    /// <summary>Estado económico mínimo sincronizable (reconstruible en cualquier peer).</summary>
    public struct EconomySnapshot : System.IEquatable<EconomySnapshot>
    {
        public int Money;
        public byte EstanteriaUsed;  // ocupación de zonas (piezas)
        public byte PaleUsed;
        public byte SueloUsed;
        public ushort[] MaterialUnits; // unidades enteras por MAT (orden EconomyData.MatIds)

        public bool Equals(EconomySnapshot other)
        {
            if (Money != other.Money || EstanteriaUsed != other.EstanteriaUsed
                || PaleUsed != other.PaleUsed || SueloUsed != other.SueloUsed) return false;
            if (MaterialUnits == null || other.MaterialUnits == null) return MaterialUnits == other.MaterialUnits;
            if (MaterialUnits.Length != other.MaterialUnits.Length) return false;
            for (int i = 0; i < MaterialUnits.Length; i++) if (MaterialUnits[i] != other.MaterialUnits[i]) return false;
            return true;
        }

        public override bool Equals(object obj) { return obj is EconomySnapshot && Equals((EconomySnapshot)obj); }
        public override int GetHashCode() { return Money ^ (EstanteriaUsed << 8) ^ (PaleUsed << 16) ^ (SueloUsed << 24); }
    }

    public sealed class ScrapyardSession
    {
        /// <summary>Titular legal compartido de las piezas de la sesión (Instrucción 07 §2).</summary>
        public const string OwnerTag = "chatarrera";

        public const string MachineId = "trituradora_basica";
        private const int LogCap = 64;

        private readonly DisassemblySessionHost _dh;
        public GameStateData State { get { return _dh.State; } }
        public EconomyContext Ctx { get { return _dh.Ctx; } }

        // clientId → uid de la pieza que transporta (espejo del CarryByPlayer del dominio)
        private readonly Dictionary<ulong, string> _carrying = new Dictionary<ulong, string>();
        private readonly List<TxEntry> _log = new List<TxEntry>();

        public ScrapyardSession(DisassemblySessionHost dh)
        {
            _dh = dh;
            dh.Ctx.SessionOwner = OwnerTag; // economía compartida: las piezas son de la chatarrería
        }

        public static string PlayerTag(ulong clientId) { return "c" + clientId; }

        public IReadOnlyList<TxEntry> Log { get { return _log; } }

        private TxEntry LogTx(ulong playerId, string op, string uid, int amount, OpResult r)
        {
            var tx = new TxEntry { PlayerId = playerId, Op = op, Uid = uid, Amount = amount, Ok = r.Ok, Reason = r.Ok ? null : r.Reason };
            _log.Add(tx);
            while (_log.Count > LogCap) _log.RemoveAt(0);
            return tx;
        }

        // ---------------------------------------------------------------- transporte
        public string CarriedUid(ulong clientId)
        {
            string uid;
            return _carrying.TryGetValue(clientId, out uid) ? uid : null;
        }

        public ulong? CarrierOf(string uid)
        {
            foreach (var kv in _carrying) if (kv.Value == uid) return kv.Key;
            return null;
        }

        /// <summary>El físico (PickupItem.HeldBy) cambió a un jugador: reflejar en el dominio.</summary>
        public OpResult OnPhysicalPickup(ulong clientId, string uid)
        {
            var r = EconomyOps.PickupPiece(Ctx, uid, PlayerTag(clientId));
            if (r.Ok) _carrying[clientId] = uid;
            LogTx(clientId, "llevar", uid, 0, r);
            return r;
        }

        /// <summary>El físico soltó la pieza (E, auto-drop, atasco): reflejar en el dominio.</summary>
        public OpResult OnPhysicalDrop(ulong clientId, string uid)
        {
            string cur;
            if (_carrying.TryGetValue(clientId, out cur) && cur != uid)
                return OpResult.Fail("pieza_inexistente"); // no la transporta él
            var r = EconomyOps.DropPiece(Ctx, uid);
            if (r.Ok || r.Reason == "no_transportada") _carrying.Remove(clientId); // defensivo: limpiar registro
            LogTx(clientId, "soltar", uid, 0, r);
            return r;
        }

        /// <summary>Desconexión durante el transporte: la pieza vuelve al suelo.
        /// El objeto físico persiste (NO del servidor) ⇒ imposible duplicar.</summary>
        public OpResult OnPlayerDisconnected(ulong clientId)
        {
            string uid;
            if (!_carrying.TryGetValue(clientId, out uid)) return OpResult.Success();
            var r = EconomyOps.DropPiece(Ctx, uid);
            _carrying.Remove(clientId);
            LogTx(clientId, "desconexion", uid, 0, r);
            return r;
        }

        // ---------------------------------------------------------------- estaciones
        public OpResult UseStation(ulong clientId, int stationId, P3 stationPos)
        {
            var def = StationsCatalog.ById(stationId);
            if (def == null) return OpResult.Fail("estacion_inexistente");

            P3 ppos = default(P3);
            bool known = _dh.PlayerPos != null && _dh.PlayerPos(clientId, out ppos);
            var gate = DisassemblyGate.ValidatePresence(known, known ? ppos : default(P3), stationPos);
            if (!gate.Ok) { LogTx(clientId, def.Name, null, 0, gate); return gate; }

            switch (def.Kind)
            {
                case StationKind.Balanza: return SellCarried(clientId, "venta");
                case StationKind.Trituradora: return ProcessCarried(clientId);
                case StationKind.ResiduosEspeciales: return DisposeCarried(clientId);
                case StationKind.Estanteria:
                case StationKind.Pale:
                case StationKind.Suelo:
                    return CarriedUid(clientId) != null
                        ? StoreAt(clientId, def.ZoneId)
                        : RetrieveFrom(clientId, def.ZoneId);
                case StationKind.ContenedorMaterial:
                    return SellMaterialAll(clientId, def.MatId);
                default:
                    return OpResult.Fail("estacion_inexistente");
            }
        }

        /// <summary>Venta en balanza con pre-chequeos ANTES de mutar (transacción
        /// fallida ⇒ cero cambios, §10). Colocar en balanza y vender viajan juntos.</summary>
        public OpResult SellCarried(ulong clientId, string opName)
        {
            string uid = CarriedUid(clientId);
            if (uid == null) return LogAndFail(clientId, opName, null, "sin_pieza_en_manos");
            var p = PieceRules.GetPiece(Ctx.Reg, uid);
            if (p == null) return LogAndFail(clientId, opName, uid, "pieza_inexistente");
            if (PieceRules.IsTerminal(p)) return LogAndFail(clientId, opName, uid, "pieza_terminal");
            if (p.Owner != Ctx.SessionOwner) return LogAndFail(clientId, opName, uid, "propiedad_ajena");
            if (p.ReservedFor != null) return LogAndFail(clientId, opName, uid, "pieza_reservada");
            if (p.Sealed || p.IsProtected) return LogAndFail(clientId, opName, uid, "pieza_protegida_no_vendible");
            if (p.Storage != "carried") return LogAndFail(clientId, opName, uid, "no_transportada");

            var placed = EconomyOps.PlaceOnScale(Ctx, uid);
            if (!placed.Ok) { LogTx(clientId, opName, uid, 0, placed); return placed; }
            var sold = EconomyOps.SellPiece(Ctx, uid);
            if (sold.Ok) _carrying.Remove(clientId);
            LogTx(clientId, opName, uid, sold.Ok ? sold.Value : 0, sold);
            return sold;
        }

        public OpResult ProcessCarried(ulong clientId)
        {
            string uid = CarriedUid(clientId);
            if (uid == null) return LogAndFail(clientId, "procesado", null, "sin_pieza_en_manos");
            // ProcessPiece hace TODOS los pre-chequeos antes del commit único
            // (sellado, receta, capacidad de contenedores y de almacén, reservas).
            var r = EconomyOps.ProcessPiece(Ctx, uid, MachineId);
            if (r.Ok) _carrying.Remove(clientId);
            int units = 0;
            if (r.Ok && r.Gained != null) foreach (var g in r.Gained) units += (int)g.Amount;
            LogTx(clientId, "procesado", uid, units, r);
            return r;
        }

        public OpResult DisposeCarried(ulong clientId)
        {
            string uid = CarriedUid(clientId);
            if (uid == null) return LogAndFail(clientId, "entrega", null, "sin_pieza_en_manos");
            var r = EconomyOps.DisposeSpecial(Ctx, uid); // pre-chequea sellado y fondos (anti-quiebra)
            if (r.Ok) _carrying.Remove(clientId);
            LogTx(clientId, "entrega", uid, r.Ok ? -r.Cost : 0, r);
            return r;
        }

        public OpResult StoreAt(ulong clientId, string zoneId)
        {
            string uid = CarriedUid(clientId);
            if (uid == null) return LogAndFail(clientId, "almacenado", null, "sin_pieza_en_manos");
            var r = EconomyOps.StorePiece(Ctx, uid, zoneId); // valida categoría/capacidad/reserva
            if (r.Ok) _carrying.Remove(clientId);
            LogTx(clientId, "almacenado", uid, 0, r);
            return r;
        }

        /// <summary>Retira la última pieza almacenada en la zona (LIFO) a las manos
        /// del jugador. Identidad/calidad/origen viajan con la pieza del dominio.</summary>
        public OpResult RetrieveFrom(ulong clientId, string zoneId)
        {
            var uids = WarehouseRules.StoredUids(Ctx.Cwh, zoneId);
            if (uids == null || uids.Count == 0) return LogAndFail(clientId, "retirado", null, "zona_vacia");
            string uid = uids[uids.Count - 1];
            var r = EconomyOps.RetrievePiece(Ctx, uid, zoneId, PlayerTag(clientId));
            if (r.Ok) _carrying[clientId] = uid;
            LogTx(clientId, "retirado", uid, 0, r);
            return r;
        }

        public OpResult SellMaterialAll(ulong clientId, string matId)
        {
            int units = WarehouseRules.MaterialUnits(Ctx.Mwh, matId);
            if (units <= 0) return LogAndFail(clientId, "venta_material", matId, "insuficiente");
            var r = EconomyOps.SellMaterial(Ctx, matId, units);
            LogTx(clientId, "venta_material", matId, r.Ok ? r.Delta : 0, r);
            return r;
        }

        private OpResult LogAndFail(ulong clientId, string op, string uid, string reason)
        {
            var r = OpResult.Fail(reason);
            LogTx(clientId, op, uid, 0, r);
            return r;
        }

        // ---------------------------------------------------------------- sincronización (§8)
        /// <summary>Estado mínimo para replicar: dinero + ocupación de zonas +
        /// unidades de materiales. Todo lo demás se reconstruye (piezas = NetworkObjects
        /// propios; historial = feed de eventos).</summary>
        public EconomySnapshot Snapshot()
        {
            var s = new EconomySnapshot
            {
                Money = State.Money,
                EstanteriaUsed = (byte)WarehouseRules.StoredUids(State.Cwh, "estanteria").Count,
                PaleUsed = (byte)WarehouseRules.StoredUids(State.Cwh, "pale").Count,
                SueloUsed = (byte)WarehouseRules.StoredUids(State.Cwh, "suelo").Count,
                MaterialUnits = new ushort[EconomyData.MatIds.Length],
            };
            for (int i = 0; i < EconomyData.MatIds.Length; i++)
                s.MaterialUnits[i] = (ushort)WarehouseRules.MaterialUnits(State.Mwh, EconomyData.MatIds[i]);
            return s;
        }
    }
}
