// [ETSV] PARIDAD 1:1 — port de prototype/tests/economy.test.mjs (53 aserciones).
// Cada [Test] replica un bloque {} independiente del script JS, con las mismas
// operaciones y los mismos resultados esperados.
// Matriz de equivalencia: unity/docs/MATRIZ_PARIDAD_TESTS.md

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class EconomyParityTests
    {
        // ---- helpers (paridad con makeCtx/mkPiece del JS) ----
        private class TestCtx
        {
            public GameStateData Gs;
            public EconomyContext Ctx;
        }

        private static TestCtx MakeCtx(int money = 0)
        {
            var gs = SaveSerializer.CreateGameState();
            gs.Money = money;
            var ctx = new EconomyContext
            {
                Reg = gs.Reg,
                Cwh = gs.Cwh,
                Mwh = gs.Mwh,
                Carry = null,
                GetMoney = () => gs.Money,
                AddMoney = n => { gs.Money += n; },
                OnChanged = () => { },
            };
            return new TestCtx { Gs = gs, Ctx = ctx };
        }

        private static StoredPart MkPiece(EconomyContext ctx,
            int? vbase_ = null, int? quality = null, string weightClass = null,
            bool sealed_ = false, bool protected_ = false, RecipeEntry[] recipe = null,
            string owner = null)
        {
            var r = EconomyOps.ExtractPiece(ctx, new PieceCreateOptions
            {
                DefId = "test",
                DefName = "Pieza test",
                OriginObjectId = "obj_1",
                Quality = quality ?? 100,
                Kg = 1,
                WeightClass = weightClass ?? "P1",
                VBase = vbase_ ?? 100,
                Owner = owner ?? "P1",
                Sealed = sealed_,
                IsProtected = protected_,
                Recipe = recipe,
            });
            Assert.IsTrue(r.Ok, "extractPiece del helper debe succeeder");
            return r.Piece;
        }

        // ---- bloque 1: fórmula de valor (GDD 5.0 §6.3): V = Vb × (Q/100) × M_c — 1 assert
        [Test]
        public void B01_Valor_FormulaGdd()
        {
            var t = MakeCtx();
            var p = MkPiece(t.Ctx, vbase_: 90, quality: 88);
            Assert.IsTrue(Math.Abs(EconomyOps.PieceValue(t.Ctx, p) - 79.2) < 1e-9, "V = 90 × 0.88 × 1 = 79.2");
        }

        // ---- bloque 2: identidad y exclusividad de estados — 10 asserts
        [Test]
        public void B02_Identidad_Exclusividad()
        {
            var t = MakeCtx();
            var p = MkPiece(t.Ctx);
            Assert.IsTrue(p.Storage == "world" && p.Terminal == null && p.ReservedFor == null, "identidad inicial");
            Assert.IsTrue(EconomyOps.PickupPiece(t.Ctx, p.Uid).Ok, "pickup world→carried");
            Assert.AreEqual("carried", p.Storage);
            var p2 = MkPiece(t.Ctx);
            Assert.AreEqual("manos_ocupadas", EconomyOps.PickupPiece(t.Ctx, p2.Uid).Reason,
                "una sola pieza en las manos (GDD 8.1)");
            Assert.IsTrue(EconomyOps.StorePiece(t.Ctx, p.Uid, "estanteria").Ok, "store carried→estantería (P1)");
            Assert.AreEqual("stored:estanteria", p.Storage);
            Assert.IsTrue(EconomyOps.SellPiece(t.Ctx, p.Uid).Ok, "vender desde almacén");
            Assert.IsTrue(p.Terminal == "vendido" && p.Storage == "none",
                "terminal excluyente: ni almacenada ni vendida a la vez");
            Assert.IsFalse(EconomyOps.SellPiece(t.Ctx, p.Uid).Ok, "no se vende dos veces");
            Assert.IsFalse(EconomyOps.StorePiece(t.Ctx, p.Uid, "estanteria").Ok, "terminal no se almacena");
        }

        // ---- bloque 3: capacidades y categorías por zona (GDD 5.0 §8.5) — 5 asserts
        [Test]
        public void B03_Zonas_CapacidadYCategorias()
        {
            var t = MakeCtx();
            var heavy = MkPiece(t.Ctx, weightClass: "P2");
            Assert.IsTrue(EconomyOps.PickupPiece(t.Ctx, heavy.Uid).Ok, "P2 se puede llevar (penalización en main)");
            var r = EconomyOps.StorePiece(t.Ctx, heavy.Uid, "estanteria");
            Assert.AreEqual("categoria_incompatible", r.Reason, "P2 no entra en estantería");
            Assert.IsTrue(EconomyOps.StorePiece(t.Ctx, heavy.Uid, "pale").Ok, "P2 → palé");
            for (int i = 0; i < 5; i++) // palé cap 6: 1 usado + 5 = lleno
            {
                var p = MkPiece(t.Ctx, weightClass: "P2");
                EconomyOps.PickupPiece(t.Ctx, p.Uid);
                EconomyOps.StorePiece(t.Ctx, p.Uid, "pale");
            }
            var p7 = MkPiece(t.Ctx, weightClass: "P2");
            EconomyOps.PickupPiece(t.Ctx, p7.Uid);
            Assert.AreEqual("zona_llena", EconomyOps.StorePiece(t.Ctx, p7.Uid, "pale").Reason,
                "capacidad de palé respetada");
            Assert.AreEqual("carried", p7.Storage, "operación fallida no muta estado");
        }

        // ---- bloque 4: rendimiento con fracciones (GDD 5.0 §4.2): Y = B × E × P acumulado — 6 asserts
        [Test]
        public void B04_Rendimiento_FraccionesEntreLotes()
        {
            var t = MakeCtx();
            var recipe = new[] { RecipeEntry.OfMat("MAT-01", 4) };
            var p1 = MkPiece(t.Ctx);
            EconomyOps.PickupPiece(t.Ctx, p1.Uid);
            var r1 = EconomyOps.ProcessPiece(t.Ctx, p1.Uid, "trituradora_basica", recipe);
            Assert.IsTrue(r1.Ok, "procesar pieza P1");
            Assert.AreEqual(3, WarehouseRules.MaterialUnits(t.Ctx.Mwh, "MAT-01"),
                "floor(4 × 0.9 × 1) = 3 unidades, fracción 0.6 guardada");
            Assert.IsTrue(Math.Abs(WarehouseRules.MaterialFraction(t.Ctx.Mwh, "MAT-01") - 0.6) < 1e-9,
                "residuo fraccionario conservado");
            Assert.IsFalse(EconomyOps.ProcessPiece(t.Ctx, p1.Uid, "trituradora_basica", recipe).Ok,
                "no se procesa dos veces (pieza terminal)");
            int before = WarehouseRules.MaterialUnits(t.Ctx.Mwh, "MAT-01");
            var p2 = MkPiece(t.Ctx);
            EconomyOps.PickupPiece(t.Ctx, p2.Uid);
            EconomyOps.ProcessPiece(t.Ctx, p2.Uid, "trituradora_basica", recipe);
            Assert.AreEqual(7, WarehouseRules.MaterialUnits(t.Ctx.Mwh, "MAT-01"),
                "lote 2: 3.6+3.6=7.2 → 7 unidades (fracciones no se pierden)");
            Assert.AreEqual(3, before, "antes del lote 2 había 3");
        }

        // ---- bloque 5: subensambles: motor de microondas → motor pequeño (GDD 3.0) — 5 asserts
        [Test]
        public void B05_Subensamble_MotorPequenio()
        {
            var t = MakeCtx();
            var p = MkPiece(t.Ctx, quality: 88, recipe: new[] { RecipeEntry.OfPart("motor_pequenio", 1) });
            EconomyOps.PickupPiece(t.Ctx, p.Uid);
            var r = EconomyOps.ProcessPiece(t.Ctx, p.Uid, "trituradora_basica");
            Assert.IsTrue(r.Ok && r.NewParts.Count == 1, "descomposición genera 1 motor pequeño");
            var np = r.NewParts[0];
            Assert.IsTrue(np.DefId == "motor_pequenio" && np.VBase == 90, "Vb $90 del GDD 5.0 §6.3");
            Assert.AreEqual(88, np.Quality, "calidad heredada (PROV-10)");
            Assert.AreEqual("stored:pale", np.Storage, "subensamble P2 va al palé");
            Assert.AreEqual(79, EconomyOps.SellPiece(t.Ctx, np.Uid).Value,
                "venta del subensamble: round(90×0.88) = 79");
        }

        // ---- bloque 6: sellado: sin máquinas comunes, entrega con coste de gestión — 7 asserts
        [Test]
        public void B06_Sellado_GestionConCoste()
        {
            var t = MakeCtx(100);
            var s = MkPiece(t.Ctx, sealed_: true, recipe: new[] { RecipeEntry.OfMat("MAT-08", 4) });
            EconomyOps.PickupPiece(t.Ctx, s.Uid);
            Assert.AreEqual("maquina_incompatible_sellado",
                EconomyOps.ProcessPiece(t.Ctx, s.Uid, "trituradora_basica").Reason, "sellado no se tritura");
            Assert.AreEqual("pieza_protegida_no_vendible", EconomyOps.SellPiece(t.Ctx, s.Uid).Reason,
                "sellado no se vende");
            var d = EconomyOps.DisposeSpecial(t.Ctx, s.Uid);
            Assert.IsTrue(d.Ok && d.Cost == 20, "entrega residuo especial: 4 × $5 = $20");
            Assert.AreEqual(80, t.Ctx.GetMoney(), "dinero 100 − 20");
            Assert.AreEqual("entregado", s.Terminal, "estado terminal entregado");
            Assert.IsFalse(EconomyOps.DisposeSpecial(t.Ctx, s.Uid).Ok, "no se entrega dos veces");
            var poor = MakeCtx(5);
            var s2 = MkPiece(poor.Ctx, sealed_: true, recipe: new[] { RecipeEntry.OfMat("MAT-08", 4) });
            EconomyOps.PickupPiece(poor.Ctx, s2.Uid);
            Assert.AreEqual("fondos_insuficientes", EconomyOps.DisposeSpecial(poor.Ctx, s2.Uid).Reason,
                "sin fondos no hay entrega");
        }

        // ---- bloque 7: reservas (GDD 5.0 §8.4) — 6 asserts
        [Test]
        public void B07_Reservas_BloqueanVentaYProceso()
        {
            var t = MakeCtx();
            var p = MkPiece(t.Ctx);
            EconomyOps.PickupPiece(t.Ctx, p.Uid);
            EconomyOps.StorePiece(t.Ctx, p.Uid, "estanteria");
            Assert.IsTrue(EconomyOps.ReservePiece(t.Ctx, p.Uid, "contrato").Ok, "reservar");
            Assert.AreEqual("pieza_reservada", EconomyOps.SellPiece(t.Ctx, p.Uid).Reason,
                "vender reservada → bloqueado");
            Assert.AreEqual("pieza_reservada",
                EconomyOps.ProcessPiece(t.Ctx, p.Uid, "trituradora_basica",
                    new[] { RecipeEntry.OfMat("MAT-01", 1) }).Reason,
                "procesar reservada → bloqueado");
            Assert.IsFalse(EconomyOps.ReservePiece(t.Ctx, p.Uid, "mantenimiento").Ok, "doble reserva no");
            Assert.IsTrue(EconomyOps.ReleasePiece(t.Ctx, p.Uid, "contrato").Ok, "liberar");
            Assert.IsTrue(EconomyOps.SellPiece(t.Ctx, p.Uid).Ok, "tras liberar se puede vender");
        }

        // ---- bloque 8: propiedad ajena — 1 assert
        [Test]
        public void B08_PropiedadAjena_Bloqueada()
        {
            var t = MakeCtx();
            var p = MkPiece(t.Ctx, owner: "P2");
            Assert.AreEqual("propiedad_ajena", EconomyOps.PickupPiece(t.Ctx, p.Uid).Reason,
                "pieza de otro jugador");
        }

        // ---- bloque 9: venta y consumo de materiales — 7 asserts
        [Test]
        public void B09_Materiales_VentaYConsumoAtomico()
        {
            var t = MakeCtx();
            WarehouseRules.AddMaterialYield(t.Ctx.Mwh, "MAT-03", 5);
            Assert.AreEqual(90, EconomyOps.SellMaterial(t.Ctx, "MAT-03", 5).Delta, "5 cobre × $18 = $90");
            Assert.AreEqual("insuficiente", EconomyOps.SellMaterial(t.Ctx, "MAT-03", 1).Reason,
                "no se vende lo inexistente");
            Assert.AreEqual("insuficiente",
                EconomyOps.ConsumeMaterials(t.Ctx, new Dictionary<string, int> { { "MAT-01", 1 } }).Reason,
                "consumo inexistente falla");
            WarehouseRules.AddMaterialYield(t.Ctx.Mwh, "MAT-01", 4);
            Assert.IsTrue(EconomyOps.ConsumeMaterials(t.Ctx, new Dictionary<string, int> { { "MAT-01", 3 } }).Ok,
                "consumo válido");
            Assert.AreEqual(1, WarehouseRules.MaterialUnits(t.Ctx.Mwh, "MAT-01"), "queda 1");
            Assert.AreEqual("insuficiente",
                EconomyOps.ConsumeMaterials(t.Ctx, new Dictionary<string, int> { { "MAT-01", 2 } }).Reason,
                "lote atómico: todo o nada");
            Assert.AreEqual(1, WarehouseRules.MaterialUnits(t.Ctx.Mwh, "MAT-01"), "el lote fallido no consumió");
        }

        // ---- bloque 10: persistencia (capa de estado) — 5 asserts
        [Test]
        public void B10_Persistencia_RoundTrip()
        {
            var t = MakeCtx(500);
            var p = MkPiece(t.Ctx, quality: 70);
            EconomyOps.PickupPiece(t.Ctx, p.Uid);
            EconomyOps.StorePiece(t.Ctx, p.Uid, "estanteria");
            WarehouseRules.AddMaterialYield(t.Ctx.Mwh, "MAT-06", 3.4);
            var lost = MkPiece(t.Ctx); // en world: no persiste (PROV-11)
            var snap = SaveSerializer.Serialize(t.Gs);
            var gs2 = SaveSerializer.Deserialize(snap);
            Assert.AreEqual(500, gs2.Money, "dinero persistido");
            Assert.AreEqual("stored:estanteria", PieceRules.GetPiece(gs2.Reg, p.Uid).Storage,
                "pieza almacenada persistida");
            Assert.IsNull(PieceRules.GetPiece(gs2.Reg, lost.Uid), "pieza en mundo no persiste (PROV-11)");
            Assert.IsTrue(Math.Abs(gs2.Mwh.Acc["MAT-06"] - 3.4) < 1e-9, "fracciones persistidas");
            Assert.IsTrue(WarehouseRules.StoredUids(gs2.Cwh, "estanteria").Contains(p.Uid), "zona persistida");
        }
    }
}
