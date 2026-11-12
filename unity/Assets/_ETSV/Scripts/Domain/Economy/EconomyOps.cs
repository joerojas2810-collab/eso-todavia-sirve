// [ETSV] Dominio puro — ECONOMÍA: operaciones atómicas.
// Port 1:1 de prototype/js/systems/economy.js (Instrucción 02 §4/§6/§7,
// GDD 5.0 §4/§6/§8). Toda mutación de piezas/materiales/dinero pasa por estas
// funciones: verifican disponibilidad, propiedad, capacidad y estado ANTES de
// tocar el estado (atomicidad). En multijugador el anfitrión será quien las
// autorice: la integración (ServerRpc) llama a estas mismas funciones — el
// dominio no sabe nada de red.

using System;
using System.Collections.Generic;

namespace Etsv.Domain
{
    /// <summary>
    /// Contexto de economía (paridad con el ctx de JS):
    /// { reg, cwh, mwh, carry, getMoney(), addMoney(n), onChanged() }.
    /// La integración lo construye sobre el estado del host.
    ///
    /// Instrucción 07 §2 — economía compartida de la chatarrería (GDD coop):
    /// - SessionOwner: titular legal de las piezas de la sesión ("P1" por defecto
    ///   preserva la paridad con el prototipo monojugador; la sesión cooperativa
    ///   usa "chatarrera"). La propiedad ECONÓMICA es de la chatarrería compartida;
    ///   quién transporta cada pieza se registra aparte (CarryByPlayer) y la
    ///   trazabilidad de cada operación vive en la capa de sesión (no acá).
    /// - CarryByPlayer: manos ocupadas POR JUGADOR (GDD 8.1: una pieza por
    ///   jugador). Carry (single-slot) se conserva para la paridad con JS.
    /// </summary>
    public class EconomyContext
    {
        public PieceRegistry Reg;
        public ComponentWarehouse Cwh;
        public MaterialWarehouse Mwh;
        public string Carry;                 // uid de la única pieza en las manos (paridad JS monojugador)
        public string SessionOwner = "P1";   // titular legal de las piezas de la sesión
        public Dictionary<string, string> CarryByPlayer = new Dictionary<string, string>(); // playerId → uid
        public Func<int> GetMoney;
        public Action<int> AddMoney;
        public Action OnChanged;
    }

    public static class EconomyOps
    {
        // ---------------------------------------------------------------- valor (GDD 5.0 §6.3)
        // V = ValorBase × (Calidad/100) × ModificadorComercial
        public static double PieceValue(EconomyContext ctx, StoredPart piece)
        {
            return piece.VBase * (piece.Quality / 100.0) * EconomyData.CommercialMod;
        }

        private static void Touch(EconomyContext ctx)
        {
            if (ctx.OnChanged != null) ctx.OnChanged();
        }

        // Verificaciones comunes para operar una pieza por su dueño.
        private static OpResult CheckOperable(EconomyContext ctx, string uid, bool allowReserved = false)
        {
            var p = PieceRules.GetPiece(ctx.Reg, uid);
            if (p == null) return OpResult.Fail("pieza_inexistente");
            if (PieceRules.IsTerminal(p)) return OpResult.Fail("pieza_terminal");
            if (p.Owner != ctx.SessionOwner) return OpResult.Fail("propiedad_ajena"); // titular de la sesión (coop: chatarrería)
            if (!allowReserved && p.ReservedFor != null) return OpResult.Fail("pieza_reservada");
            return new OpResult { Ok = true, Piece = p };
        }

        // Libera el slot de transporte de la pieza, sea el single-slot legacy (JS)
        // o el de cualquier jugador (sesión cooperativa).
        private static void ClearCarry(EconomyContext ctx, string uid)
        {
            if (ctx.Carry == uid) ctx.Carry = null;
            if (ctx.CarryByPlayer != null && ctx.CarryByPlayer.Count > 0)
            {
                List<string> keys = null;
                foreach (var kv in ctx.CarryByPlayer)
                    if (kv.Value == uid) { if (keys == null) keys = new List<string>(); keys.Add(kv.Key); }
                if (keys != null) foreach (var k in keys) ctx.CarryByPlayer.Remove(k);
            }
        }

        private static bool HandsFree(EconomyContext ctx, string playerId)
        {
            if (playerId == null) return ctx.Carry == null; // legacy monojugador
            return ctx.CarryByPlayer == null || !ctx.CarryByPlayer.ContainsKey(playerId);
        }

        private static void SetCarry(EconomyContext ctx, string playerId, string uid)
        {
            if (playerId == null) ctx.Carry = uid;
            else ctx.CarryByPlayer[playerId] = uid;
        }

        // ---------------------------------------------------------------- extracción
        public static OpResult ExtractPiece(EconomyContext ctx, PieceCreateOptions opts)
        {
            if (!EconomyData.WeightClasses.ContainsKey(opts.WeightClass)) return OpResult.Fail("clase_invalida");
            var piece = PieceRules.RegisterPiece(ctx.Reg, opts);
            Touch(ctx);
            return new OpResult { Ok = true, Piece = piece };
        }

        // ---------------------------------------------------------------- transporte personal
        // GDD 5.0 §8.1: una pieza pequeña en las manos. P2 con movimiento reducido.
        // playerId == null ⇒ comportamiento monojugador legacy (paridad JS: ctx.Carry).
        // playerId != null ⇒ slot de manos de ese jugador (sesión cooperativa).
        public static OpResult PickupPiece(EconomyContext ctx, string uid)
        {
            return PickupPiece(ctx, uid, null);
        }

        public static OpResult PickupPiece(EconomyContext ctx, string uid, string playerId)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            var p = c.Piece;
            if (p.Storage != "world") return OpResult.Fail("no_disponible_en_suelo");
            var wc = EconomyData.WeightClasses[p.WeightClass];
            if (!wc.Carryable) return OpResult.Fail("requiere_carro_o_grua");
            if (!HandsFree(ctx, playerId)) return OpResult.Fail("manos_ocupadas"); // una pieza por jugador
            PieceRules.SetStorage(ctx.Reg, uid, "carried");
            SetCarry(ctx, playerId, uid);
            Touch(ctx);
            return new OpResult { Ok = true, Piece = p, SpeedMul = wc.SpeedMul };
        }

        public static OpResult DropPiece(EconomyContext ctx, string uid)
        {
            var c = CheckOperable(ctx, uid, true);
            if (!c.Ok) return c;
            if (c.Piece.Storage != "carried") return OpResult.Fail("no_transportada");
            PieceRules.SetStorage(ctx.Reg, uid, "world");
            ClearCarry(ctx, uid);
            Touch(ctx);
            return new OpResult { Ok = true, Piece = c.Piece };
        }

        public static OpResult PlaceOnScale(EconomyContext ctx, string uid)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            if (c.Piece.Storage != "carried") return OpResult.Fail("no_transportada");
            PieceRules.SetStorage(ctx.Reg, uid, "scale");
            ClearCarry(ctx, uid);
            Touch(ctx);
            return new OpResult { Ok = true, Piece = c.Piece };
        }

        // ---------------------------------------------------------------- almacenamiento
        public static OpResult StorePiece(EconomyContext ctx, string uid, string zoneId)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            if (c.Piece.Storage != "carried") return OpResult.Fail("no_transportada");
            var cs = WarehouseRules.CanStorePiece(ctx.Cwh, zoneId, c.Piece);
            if (!cs.Ok) return cs;
            WarehouseRules.StorePieceUid(ctx.Cwh, zoneId, uid);
            PieceRules.SetStorage(ctx.Reg, uid, "stored:" + zoneId);
            ClearCarry(ctx, uid);
            Touch(ctx);
            return new OpResult { Ok = true, Piece = c.Piece, Zone = zoneId };
        }

        public static OpResult RetrievePiece(EconomyContext ctx, string uid, string zoneId)
        {
            return RetrievePiece(ctx, uid, zoneId, null);
        }

        // playerId == null ⇒ legacy monojugador (ctx.Carry); si no, slot del jugador.
        public static OpResult RetrievePiece(EconomyContext ctx, string uid, string zoneId, string playerId)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            if (PieceRules.ZoneOf(c.Piece) != zoneId) return OpResult.Fail("no_esta_en_zona");
            var wc = EconomyData.WeightClasses[c.Piece.WeightClass];
            if (!wc.Carryable) return OpResult.Fail("requiere_carro_o_grua");
            if (!HandsFree(ctx, playerId)) return OpResult.Fail("manos_ocupadas");
            WarehouseRules.RemovePieceUid(ctx.Cwh, zoneId, uid);
            PieceRules.SetStorage(ctx.Reg, uid, "carried");
            SetCarry(ctx, playerId, uid);
            Touch(ctx);
            return new OpResult { Ok = true, Piece = c.Piece };
        }

        // ---------------------------------------------------------------- procesamiento (GDD 5.0 §4.2/§4.3)
        // Y_i = B_i × E × P_i acumulado en fracciones; unidades = floor(acumulado).
        public static List<YieldEntry> ComputeYields(EconomyContext ctx, StoredPart piece, RecipeEntry[] recipe, string machineId)
        {
            MachineDef machine;
            if (!EconomyData.Machines.TryGetValue(machineId, out machine)) return null;
            double P = EconomyData.ConservationFactor(piece.Quality);
            var outp = new List<YieldEntry>();
            foreach (var r in recipe)
            {
                if (r.Mat != null) outp.Add(new YieldEntry { Mat = r.Mat, Amount = r.Base * machine.E * P });
                if (r.Part != null) outp.Add(new YieldEntry { Part = r.Part, Amount = r.Base }); // subensambles: unidades enteras
            }
            return outp;
        }

        // El componente se consume en la misma transacción; nunca duplica materiales.
        public static OpResult ProcessPiece(EconomyContext ctx, string uid, string machineId, RecipeEntry[] compRecipe = null)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            var p = c.Piece;
            if (p.Sealed || p.IsProtected) return OpResult.Fail("maquina_incompatible_sellado");
            if (p.Storage != "carried" && !PieceRules.IsStored(p)) return OpResult.Fail("no_disponible_para_procesar");
            var recipe = compRecipe != null ? compRecipe : p.Recipe;
            if (recipe == null || recipe.Length == 0) return OpResult.Fail("sin_receta");
            var yields = ComputeYields(ctx, p, recipe, machineId);
            if (yields == null) return OpResult.Fail("maquina_inexistente");

            // pre-checks atómicos: capacidad de contenedores y de almacén de subensambles
            foreach (var y in yields)
            {
                if (y.Mat == "MAT-08") return OpResult.Fail("residuo_especial_no_procesable"); // solo entrega en contenedor
                if (y.Mat != null)
                {
                    double room = EconomyData.MaterialContainerCap - ctx.Mwh.Acc[y.Mat];
                    if (y.Amount > room) return OpResult.Fail("contenedor_lleno");
                }
                if (y.Part != null)
                {
                    PartDef def;
                    if (!EconomyData.PartDefs.TryGetValue(y.Part, out def)) return OpResult.Fail("pieza_def_inexistente");
                    for (int i = 0; i < (int)y.Amount; i++)
                    {
                        var probe = new StoredPart { WeightClass = def.WeightClass };
                        var cs = WarehouseRules.CanStorePiece(ctx.Cwh, EconomyData.WeightClasses[def.WeightClass].Storage, probe);
                        if (!cs.Ok) return cs;
                    }
                }
            }

            // commit único: materiales + subensambles + consumo del origen
            var gained = new List<MaterialGain>();
            var newParts = new List<StoredPart>();
            foreach (var y in yields)
            {
                if (y.Mat != null)
                {
                    WarehouseRules.AddMaterialYield(ctx.Mwh, y.Mat, y.Amount);
                    gained.Add(new MaterialGain { Mat = y.Mat, Amount = y.Amount });
                }
                else if (y.Part != null)
                {
                    var def = EconomyData.PartDefs[y.Part];
                    for (int i = 0; i < (int)y.Amount; i++)
                    {
                        var np = PieceRules.RegisterPiece(ctx.Reg, new PieceCreateOptions
                        {
                            DefId = y.Part,
                            DefName = def.Name,
                            OriginObjectId = p.OriginObjectId,
                            Quality = p.Quality, // PROV-10: calidad heredada del origen
                            Kg = def.Kg,
                            WeightClass = def.WeightClass,
                            VBase = def.VBase,
                        });
                        string zone = EconomyData.WeightClasses[def.WeightClass].Storage;
                        WarehouseRules.StorePieceUid(ctx.Cwh, zone, np.Uid);
                        PieceRules.SetStorage(ctx.Reg, np.Uid, "stored:" + zone);
                        newParts.Add(np);
                    }
                }
            }
            if (PieceRules.IsStored(p)) WarehouseRules.RemovePieceUid(ctx.Cwh, PieceRules.ZoneOf(p), uid);
            ClearCarry(ctx, uid);
            PieceRules.SetTerminal(ctx.Reg, uid, "procesado", machineId);
            Touch(ctx);
            return new OpResult { Ok = true, Gained = gained, NewParts = newParts };
        }

        // ---------------------------------------------------------------- venta (GDD 5.0 §6.3)
        public static OpResult SellPiece(EconomyContext ctx, string uid)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            var p = c.Piece;
            if (p.Sealed || p.IsProtected) return OpResult.Fail("pieza_protegida_no_vendible");
            if (p.Storage != "carried" && p.Storage != "scale" && !PieceRules.IsStored(p))
                return OpResult.Fail("no_vendible_desde");
            int value = JsMath.Round(PieceValue(ctx, p));
            if (PieceRules.IsStored(p)) WarehouseRules.RemovePieceUid(ctx.Cwh, PieceRules.ZoneOf(p), uid);
            ClearCarry(ctx, uid);
            PieceRules.SetTerminal(ctx.Reg, uid, "vendido", "balanza");
            ctx.AddMoney(value);
            Touch(ctx);
            return new OpResult { Ok = true, Value = value, Piece = p };
        }

        public static OpResult SellMaterial(EconomyContext ctx, string matId, int units)
        {
            MaterialDef m;
            if (!EconomyData.Materials.TryGetValue(matId, out m)) return OpResult.Fail("material_inexistente");
            var r = WarehouseRules.ConsumeMaterialUnits(ctx.Mwh, matId, units);
            if (!r.Ok) return r;
            int delta = units * m.Price; // MAT-08: precio negativo = coste
            ctx.AddMoney(delta);
            Touch(ctx);
            return new OpResult { Ok = true, Delta = delta, Restante = r.Restante };
        }

        // Entrega de residuos especiales (módulos sellados): coste de gestión por unidad.
        // GDD 5.0 §6.2: −$5/unidad; en determinados contratos el cliente cubre ese coste.
        // Anti-quiebra (§6.6): sin fondos suficientes la entrega se rechaza.
        public static OpResult DisposeSpecial(EconomyContext ctx, string uid)
        {
            var c = CheckOperable(ctx, uid);
            if (!c.Ok) return c;
            var p = c.Piece;
            if (!p.Sealed) return OpResult.Fail("no_es_residuo_especial");
            if (p.Storage != "carried" && p.Storage != "scale" && !PieceRules.IsStored(p))
                return OpResult.Fail("no_transportado");
            double units = 0;
            if (p.Recipe != null)
                foreach (var r in p.Recipe)
                    if (r.Mat == "MAT-08") { units = r.Base; break; }
            int cost = (int)(units * Math.Abs(EconomyData.Materials["MAT-08"].Price));
            if (ctx.GetMoney() < cost) return OpResult.Fail("fondos_insuficientes");
            if (PieceRules.IsStored(p)) WarehouseRules.RemovePieceUid(ctx.Cwh, PieceRules.ZoneOf(p), uid);
            ClearCarry(ctx, uid);
            PieceRules.SetTerminal(ctx.Reg, uid, "entregado", "residuo_especial");
            ctx.AddMoney(-cost);
            Touch(ctx);
            return new OpResult { Ok = true, Cost = cost, Units = units };
        }

        // ---------------------------------------------------------------- reservas y consumo (GDD 5.0 §8.4)
        public static OpResult ReservePiece(EconomyContext ctx, string uid, string purpose)
        {
            var p = PieceRules.GetPiece(ctx.Reg, uid);
            if (p == null) return OpResult.Fail("pieza_inexistente");
            if (p.Owner != ctx.SessionOwner) return OpResult.Fail("propiedad_ajena");
            var r = PieceRules.Reserve(ctx.Reg, uid, purpose);
            if (r.Ok) Touch(ctx);
            return r;
        }

        public static OpResult ReleasePiece(EconomyContext ctx, string uid, string purpose)
        {
            var r = PieceRules.Release(ctx.Reg, uid, purpose);
            if (r.Ok) Touch(ctx);
            return r;
        }

        // Consumo para recetas futuras (fabricación/mantenimiento): atómico por lote.
        public static OpResult ConsumeMaterials(EconomyContext ctx, Dictionary<string, int> needs)
        {
            foreach (var kv in needs)
                if (WarehouseRules.MaterialUnits(ctx.Mwh, kv.Key) < kv.Value)
                    return OpResult.Fail("insuficiente");
            foreach (var kv in needs)
                WarehouseRules.ConsumeMaterialUnits(ctx.Mwh, kv.Key, kv.Value);
            Touch(ctx);
            return OpResult.Success();
        }

        // Daño sobre pieza física (golpes): nunca por trabajo correcto.
        public static OpResult DamagePiece(EconomyContext ctx, string uid, int amt)
        {
            var r = PieceRules.DamagePiece(ctx.Reg, uid, amt);
            if (r.Ok) Touch(ctx);
            return r;
        }
    }
}
