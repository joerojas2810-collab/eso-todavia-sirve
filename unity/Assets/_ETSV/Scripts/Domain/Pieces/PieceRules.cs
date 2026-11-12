// [ETSV] Dominio puro — REGISTRO DE PIEZAS: identidad y propiedad.
// Port 1:1 de prototype/js/systems/pieces.js (Instrucción 02 §2, GDD 5.0 §8.3).
// Invariante central: una pieza no puede estar instalada, almacenada y vendida a
// la vez — `Storage` es único mientras vive y `Terminal` la cierra de forma
// excluyente (vendido | procesado | entregado, una sola vez).

using System.Collections.Generic;

namespace Etsv.Domain
{
    /// <summary>StoredPart (GDD 5.0 §8.3). POCO serializable, sin engine.</summary>
    public class StoredPart
    {
        public string Uid;              // identificador único de instancia ("p_42")
        public string DefId;            // definición (componente del catálogo o PART_DEF)
        public string DefName;
        public string OriginObjectId;   // instancia del objeto de origen
        public string ContractId;       // contrato de origen, si corresponde (GDD §6.4)
        public int Quality;             // 0–100, solo baja por daño
        public double Kg;
        public string WeightClass;      // P1–P5
        public int VBase;               // valor base (GDD §6.3)
        public string Owner;            // "P1"… (futuro: otros jugadores)
        public string Storage;          // world | carried | scale | stored:<zona> | none
        public string ReservedFor;      // contrato | fabricacion | mantenimiento | construccion
        public string Terminal;         // vendido | procesado | entregado | null
        public string TerminalTag;
        public bool Sealed;             // módulo sellado: sin máquinas comunes ni venta
        public bool IsProtected;        // componente narrativo protegido (campaña)
        public RecipeEntry[] Recipe;    // receta de recuperación del componente de origen

        public StoredPart Clone()
        {
            return (StoredPart)MemberwiseClone(); // paridad con {...p} de JS (comparte Recipe)
        }
    }

    /// <summary>Datos de creación de una pieza (paridad con el opts de registerPiece).</summary>
    public class PieceCreateOptions
    {
        public string DefId;
        public string DefName;
        public string OriginObjectId;
        public int Quality;
        public double Kg;
        public string WeightClass;
        public string Owner = "P1";
        public int VBase;
        public bool Sealed;
        public bool IsProtected;
        public RecipeEntry[] Recipe;
    }

    public class PieceRegistry
    {
        public Dictionary<string, StoredPart> Pieces = new Dictionary<string, StoredPart>();
        public int UidSeq;
    }

    public static class PieceRules
    {
        public static readonly string[] StorageStates = { "world", "carried", "scale", "stored", "none" };
        public static readonly string[] TerminalStates = { "vendido", "procesado", "entregado" };

        // Crea una pieza extraída con identidad completa.
        public static StoredPart RegisterPiece(PieceRegistry reg, PieceCreateOptions o)
        {
            reg.UidSeq++;
            var piece = new StoredPart
            {
                Uid = "p_" + reg.UidSeq,
                DefId = o.DefId,
                DefName = o.DefName,
                OriginObjectId = o.OriginObjectId,
                ContractId = null,
                Quality = JsMath.ClampInt(o.Quality, 0, 100),
                Kg = o.Kg,
                WeightClass = o.WeightClass,
                VBase = o.VBase,
                Owner = o.Owner ?? "P1",
                Storage = "world",
                ReservedFor = null,
                Terminal = null,
                Sealed = o.Sealed,
                IsProtected = o.IsProtected,
                Recipe = o.Recipe,
            };
            reg.Pieces[piece.Uid] = piece;
            return piece;
        }

        public static StoredPart GetPiece(PieceRegistry reg, string uid)
        {
            StoredPart p;
            return reg.Pieces.TryGetValue(uid, out p) ? p : null;
        }

        public static bool IsTerminal(StoredPart p) { return p.Terminal != null; }

        public static bool IsStored(StoredPart p)
        {
            return p.Storage != null && p.Storage.StartsWith("stored:");
        }

        public static string ZoneOf(StoredPart p)
        {
            return IsStored(p) ? p.Storage.Substring(7) : null;
        }

        public static OpResult SetStorage(PieceRegistry reg, string uid, string storage)
        {
            var p = GetPiece(reg, uid);
            if (p == null) return OpResult.Fail("pieza_inexistente");
            if (IsTerminal(p)) return OpResult.Fail("pieza_terminal");
            bool valid = false;
            foreach (var s in StorageStates)
                if (storage == s || (s == "stored" && storage.StartsWith("stored:"))) { valid = true; break; }
            if (!valid) return OpResult.Fail("estado_invalido");
            p.Storage = storage;
            return new OpResult { Ok = true, Piece = p };
        }

        // Cierre excluyente: vendido | procesado | entregado, una sola vez.
        public static OpResult SetTerminal(PieceRegistry reg, string uid, string terminal, string tag = null)
        {
            var p = GetPiece(reg, uid);
            if (p == null) return OpResult.Fail("pieza_inexistente");
            if (System.Array.IndexOf(TerminalStates, terminal) < 0) return OpResult.Fail("terminal_invalido");
            if (IsTerminal(p)) return OpResult.Fail("ya_terminal");
            p.Terminal = terminal;
            p.TerminalTag = tag;
            p.Storage = "none";
            p.ReservedFor = null;
            return new OpResult { Ok = true, Piece = p };
        }

        // Reservas (GDD 5.0 §8.4): no consumen, pero bloquean otros usos.
        public static OpResult Reserve(PieceRegistry reg, string uid, string purpose)
        {
            var p = GetPiece(reg, uid);
            if (p == null) return OpResult.Fail("pieza_inexistente");
            if (IsTerminal(p)) return OpResult.Fail("pieza_terminal");
            if (p.ReservedFor != null) return OpResult.Fail("ya_reservada");
            p.ReservedFor = purpose;
            return new OpResult { Ok = true, Piece = p };
        }

        public static OpResult Release(PieceRegistry reg, string uid, string purpose = null)
        {
            var p = GetPiece(reg, uid);
            if (p == null) return OpResult.Fail("pieza_inexistente");
            if (purpose != null && p.ReservedFor != purpose) return OpResult.Fail("reserva_ajena");
            p.ReservedFor = null;
            return new OpResult { Ok = true, Piece = p };
        }

        // Daño: la calidad nunca baja de 0 (GDD 5.0 §5.2).
        public static OpResult DamagePiece(PieceRegistry reg, string uid, int amt)
        {
            var p = GetPiece(reg, uid);
            if (p == null || IsTerminal(p)) return OpResult.Fail("pieza_inexistente");
            int before = p.Quality;
            p.Quality = System.Math.Max(0, p.Quality - amt);
            return new OpResult { Ok = true, Applied = before - p.Quality, Quality = p.Quality };
        }

        // ---- serialización (capa de estado persistible) ----
        public static List<StoredPart> SerializeRegistry(PieceRegistry reg)
        {
            var list = new List<StoredPart>();
            foreach (var p in reg.Pieces.Values) list.Add(p.Clone());
            return list;
        }

        public static PieceRegistry DeserializeRegistry(List<StoredPart> arr)
        {
            var reg = new PieceRegistry();
            int max = 0;
            if (arr != null)
                foreach (var p in arr)
                {
                    reg.Pieces[p.Uid] = p.Clone();
                    int n;
                    if (int.TryParse(p.Uid.Substring(2), out n) && n > max) max = n;
                }
            reg.UidSeq = max;
            return reg;
        }
    }
}
