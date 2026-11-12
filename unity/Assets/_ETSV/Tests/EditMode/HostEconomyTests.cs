// [ETSV] Verificaciones automatizadas de la Instrucción 07 §10 sobre la capa Sim
// (ScrapyardSession + dominio). C# puro ⇒ ejecutables sin Unity: venta simultánea,
// procesamiento duplicado, desconexión durante el transporte, pieza reservada,
// propiedad inválida, capacidad insuficiente, residuos especiales, transacción
// fallida sin mutaciones, sincronización de saldos y reconstrucción para
// incorporación tardía. La replicación real por red y las físicas quedan
// pendientes del Unity Editor (documentado en INTEGRACION_ECONOMIA.md).

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Etsv.Domain;
using Etsv.Integration;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class HostEconomyTests
    {
        private const int Balanza = 0, Trituradora = 1, Residuos = 2, Estanteria = 3, Pale = 4, Suelo = 5;

        private DisassemblySessionHost _dh;
        private ScrapyardSession _sc;
        private static readonly P3 StationPos = new P3(5f, 0f, 5f);

        [SetUp]
        public void Setup()
        {
            _dh = new DisassemblySessionHost();
            _dh.PlayerPos = (ulong id, out P3 p) =>
            {
                if (id == 0UL) { p = new P3(5.5f, 0f, 5.5f); return true; }  // host/j1, junto a la estación
                if (id == 1UL) { p = new P3(5.2f, 0f, 5.2f); return true; }  // j2, junto a la estación
                if (id == 2UL) { p = new P3(80f, 0f, 80f); return true; }    // j3, LEJOS
                p = default(P3);
                return false;
            };
            _sc = new ScrapyardSession(_dh);
        }

        // ---- helpers ----
        private StoredPart NewPiece(string defId = "puerta", string wc = "P1", int quality = 100,
            int vbase = 14, bool sealed_ = false, string owner = null, RecipeEntry[] recipe = null)
        {
            var r = EconomyOps.ExtractPiece(_dh.Ctx, new PieceCreateOptions
            {
                DefId = defId,
                DefName = defId,
                OriginObjectId = "test_1",
                Quality = quality,
                Kg = 1,
                WeightClass = wc,
                VBase = vbase,
                Owner = owner ?? _dh.Ctx.SessionOwner, // economía compartida por defecto
                Sealed = sealed_,
                Recipe = recipe ?? new[] { RecipeEntry.OfMat("MAT-05", 2) },
            });
            Assert.IsTrue(r.Ok, "registro de pieza de prueba");
            return r.Piece;
        }

        private void Carry(ulong clientId, string uid)
        {
            var r = _sc.OnPhysicalPickup(clientId, uid);
            Assert.IsTrue(r.Ok, "llevar " + uid + " por j" + clientId + ": " + r.Reason);
        }

        // ---- §10.1 Venta simultánea de la misma pieza
        [Test]
        public void E01_VentaSimultanea_SoloUnaAcredita()
        {
            var p = NewPiece(quality: 100, vbase: 100);
            Carry(0UL, p.Uid);

            var r1 = _sc.UseStation(0UL, Balanza, StationPos);
            var r2 = _sc.UseStation(0UL, Balanza, StationPos); // "simultánea" (el host serializa)
            Assert.IsTrue(r1.Ok && r1.Value == 100, "primera venta acredita $100");
            Assert.IsFalse(r2.Ok, "la segunda no");
            Assert.AreEqual("sin_pieza_en_manos", r2.Reason, "tras vender ya no la lleva");
            Assert.AreEqual(100, _dh.State.Money, "dinero acreditado UNA vez");
            Assert.AreEqual("vendido", p.Terminal, "estado terminal");

            // Otro jugador intenta vender la misma pieza por la estación:
            var r3 = _sc.UseStation(1UL, Balanza, StationPos);
            Assert.IsFalse(r3.Ok, "j2 sin pieza en las manos no vende nada");
        }

        // ---- §10.2 Procesamiento duplicado
        [Test]
        public void E02_ProcesamientoDuplicado_NoDuplicaMateriales()
        {
            var p = NewPiece(recipe: new[] { RecipeEntry.OfMat("MAT-03", 4) }); // 4 × 0.9 × 1 = 3.6 cobre
            Carry(0UL, p.Uid);

            var r1 = _sc.UseStation(0UL, Trituradora, StationPos);
            var r2 = _sc.UseStation(0UL, Trituradora, StationPos);
            Assert.IsTrue(r1.Ok, "primer proceso");
            Assert.IsFalse(r2.Ok, "segundo proceso rechazado");
            Assert.AreEqual(3, WarehouseRules.MaterialUnits(_dh.State.Mwh, "MAT-03"),
                "unidades acreditadas UNA vez (fracción 0.6 conservada en el host)");
            Assert.AreEqual("procesado", p.Terminal);
        }

        // ---- §10.3 Desconexión durante el transporte
        [Test]
        public void E03_DesconexionDuranteTransporte_PiezaAlSueloSinDuplicar()
        {
            var p = NewPiece();
            Carry(0UL, p.Uid);
            int piezasAntes = PieceRules.SerializeRegistry(_dh.State.Reg).Count;

            var r = _sc.OnPlayerDisconnected(0UL);
            Assert.IsTrue(r.Ok, "drop por desconexión");
            Assert.AreEqual("world", p.Storage, "la pieza vuelve al suelo");
            Assert.IsNull(_sc.CarriedUid(0UL), "registro de transporte limpio");
            Assert.AreEqual(piezasAntes, PieceRules.SerializeRegistry(_dh.State.Reg).Count,
                "sin duplicación: el registro tiene las mismas piezas");

            // Otro jugador la levanta sin problema.
            Carry(1UL, p.Uid);
            Assert.AreEqual(1UL, _sc.CarrierOf(p.Uid).Value);
        }

        // ---- §10.4 Pieza reservada
        [Test]
        public void E04_PiezaReservada_BloqueaVentaProcesoYAlmacen()
        {
            var p = NewPiece();
            Carry(0UL, p.Uid);
            Assert.IsTrue(EconomyOps.ReservePiece(_dh.Ctx, p.Uid, "contrato").Ok);

            Assert.AreEqual("pieza_reservada", _sc.UseStation(0UL, Balanza, StationPos).Reason);
            Assert.AreEqual("pieza_reservada", _sc.UseStation(0UL, Trituradora, StationPos).Reason);
            Assert.AreEqual("pieza_reservada", _sc.UseStation(0UL, Estanteria, StationPos).Reason);
            Assert.AreEqual("carried", p.Storage, "sigue en las manos, sin mutaciones");

            Assert.IsTrue(EconomyOps.ReleasePiece(_dh.Ctx, p.Uid, "contrato").Ok);
            Assert.IsTrue(_sc.UseStation(0UL, Balanza, StationPos).Ok, "tras liberar, vende");
        }

        // ---- §10.5 Propiedad inválida
        [Test]
        public void E05_PropiedadInvalida_Rechazada()
        {
            var ajena = NewPiece(owner: "contrato_externo"); // dueño fuera de la sesión compartida
            var r = _sc.OnPhysicalPickup(0UL, ajena.Uid);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("propiedad_ajena", r.Reason);
            Assert.AreEqual("world", ajena.Storage, "no se mueve");

            // La pieza de la sesión compartida SÍ es operable por cualquier jugador.
            var propia = NewPiece();
            Assert.IsTrue(_sc.OnPhysicalPickup(1UL, propia.Uid).Ok,
                "economía compartida: j2 opera piezas de la chatarrería");
        }

        // ---- §10.6 Capacidad insuficiente
        [Test]
        public void E06_CapacidadInsuficiente_ZonasYContenedor()
        {
            // Estantería: 8 × P1 (GDD 5.0 §8.5)
            for (int i = 0; i < 8; i++)
            {
                var p = NewPiece();
                Carry(0UL, p.Uid);
                Assert.IsTrue(_sc.UseStation(0UL, Estanteria, StationPos).Ok, "pieza " + i + " almacenada");
            }
            var p9 = NewPiece();
            Carry(0UL, p9.Uid);
            var r = _sc.UseStation(0UL, Estanteria, StationPos);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("zona_llena", r.Reason);
            Assert.AreEqual("carried", p9.Storage, "rechazo sin mutaciones: sigue en las manos");

            // Categoría incorrecta: P2 no entra en estantería.
            var p2 = NewPiece(defId: "carcasa", wc: "P2", vbase: 22);
            Carry(1UL, p2.Uid);
            Assert.AreEqual("categoria_incompatible", _sc.UseStation(1UL, Estanteria, StationPos).Reason);
            Assert.IsTrue(_sc.UseStation(1UL, Pale, StationPos).Ok, "P2 → palé");

            // Contenedor de materiales: PROV-05 cap 120 — pre-chequeo atómico.
            WarehouseRules.AddMaterialYield(_dh.State.Mwh, "MAT-01", 118);
            var acero = NewPiece(defId: "carcasa", wc: "P2", recipe: new[] { RecipeEntry.OfMat("MAT-01", 4) });
            Carry(1UL, acero.Uid);
            var rp = _sc.UseStation(1UL, Trituradora, StationPos); // rinde 3.6 > espacio 2
            Assert.IsFalse(rp.Ok);
            Assert.AreEqual("contenedor_lleno", rp.Reason);
            Assert.AreEqual("carried", acero.Storage, "procesamiento rechazado no consume la pieza");
        }

        // ---- §10.7 Residuos especiales (módulo sellado, PROV-09 = 4 × MAT-08)
        [Test]
        public void E07_ResiduosEspeciales_CicloCompleto()
        {
            var modulo = NewPiece(defId: "modulo", wc: "P2", vbase: 0, sealed_: true,
                recipe: new[] { RecipeEntry.OfMat("MAT-08", 4) }); // PROV-09 intacto

            Carry(0UL, modulo.Uid);
            Assert.AreEqual("maquina_incompatible_sellado", _sc.UseStation(0UL, Trituradora, StationPos).Reason,
                "el sellado no entra en la trituradora");
            Assert.AreEqual("pieza_protegida_no_vendible", _sc.UseStation(0UL, Balanza, StationPos).Reason,
                "ni se vende en balanza");
            Assert.AreEqual("fondos_insuficientes", _sc.UseStation(0UL, Residuos, StationPos).Reason,
                "sin dinero no hay entrega (anti-quiebra GDD §6.6)");
            Assert.AreEqual("carried", modulo.Storage, "todo rechazado ⇒ sin mutaciones");

            _dh.State.Money += 100;
            var r = _sc.UseStation(0UL, Residuos, StationPos);
            Assert.IsTrue(r.Ok, "entrega aceptada con fondos");
            Assert.AreEqual(20, r.Cost, "4 × $5 = $20 (PROV-09)");
            Assert.AreEqual(80, _dh.State.Money);
            Assert.AreEqual("entregado", modulo.Terminal);
            Assert.IsNull(_sc.CarriedUid(0UL), "ya no la transporta");
        }

        // ---- §10.8 Transacción fallida sin mutaciones
        [Test]
        public void E08_TransaccionFallida_CeroMutaciones()
        {
            var p = NewPiece(quality: 70, vbase: 100);
            Carry(0UL, p.Uid);
            Assert.IsTrue(EconomyOps.ReservePiece(_dh.Ctx, p.Uid, "mantenimiento").Ok);

            int money0 = _dh.State.Money;
            var snap0 = _sc.Snapshot();
            var r = _sc.UseStation(0UL, Balanza, StationPos);

            Assert.IsFalse(r.Ok);
            Assert.AreEqual(money0, _dh.State.Money, "dinero intacto");
            Assert.AreEqual("carried", p.Storage, "ubicación intacta (ni pasó por la balanza)");
            Assert.IsNull(p.Terminal, "sin estado terminal");
            Assert.AreEqual("mantenimiento", p.ReservedFor, "reserva intacta");
            Assert.AreEqual(0UL, _sc.CarrierOf(p.Uid).Value, "sigue transportada por j0");
            Assert.AreEqual(snap0, _sc.Snapshot(), "snapshot económico idéntico");
        }

        // ---- §10.9 Sincronización de saldos (snapshot = estado autoritativo)
        [Test]
        public void E09_Snapshot_ReflejaElEstadoAutoritativo()
        {
            // circuito completo: vender + procesar + almacenar + entregar
            var vendible = NewPiece(vbase: 50); Carry(0UL, vendible.Uid);
            _sc.UseStation(0UL, Balanza, StationPos);                       // +$50
            var procesable = NewPiece(defId: "carcasa", wc: "P2", recipe: new[] { RecipeEntry.OfMat("MAT-01", 4) });
            Carry(0UL, procesable.Uid);
            _sc.UseStation(0UL, Trituradora, StationPos);                   // +3.6 MAT-01
            var almacenable = NewPiece(); Carry(1UL, almacenable.Uid);
            _sc.UseStation(1UL, Estanteria, StationPos);                    // zona +1
            _dh.State.Money += 100;
            var modulo = NewPiece(defId: "modulo", wc: "P2", sealed_: true,
                recipe: new[] { RecipeEntry.OfMat("MAT-08", 4) });
            Carry(1UL, modulo.Uid);
            _sc.UseStation(1UL, Residuos, StationPos);                      // −$20

            var s = _sc.Snapshot();
            Assert.AreEqual(_dh.State.Money, s.Money, "saldo compartido = dinero autoritativo");
            Assert.AreEqual(130, s.Money, "50 + 100 − 20");
            Assert.AreEqual(1, s.EstanteriaUsed);
            Assert.AreEqual(0, s.PaleUsed);
            Assert.AreEqual(3, s.MaterialUnits[0], "MAT-01: floor(3.6) = 3");
            Assert.AreEqual(_sc.Snapshot(), s, "snapshot determinista (sin deriva)");

            // Venta de material desde su contenedor (id 6 = MAT-01).
            var r = _sc.UseStation(0UL, 6, StationPos);
            Assert.IsTrue(r.Ok && r.Delta == 12, "3 × $4 = $12");
            Assert.AreEqual(0, _sc.Snapshot().MaterialUnits[0], "contenedor vacío");
            Assert.AreEqual(142, _sc.Snapshot().Money);
        }

        // ---- §10.10 Reconstrucción para jugador que se incorpora tarde
        [Test]
        public void E10_LateJoin_ReconstruyeDesdeElSnapshot()
        {
            var a = NewPiece(vbase: 30); Carry(0UL, a.Uid); _sc.UseStation(0UL, Balanza, StationPos);
            var b = NewPiece(); Carry(1UL, b.Uid); _sc.UseStation(1UL, Estanteria, StationPos);
            var c = NewPiece(defId: "carcasa", wc: "P2"); Carry(0UL, c.Uid); _sc.UseStation(0UL, Pale, StationPos);

            // El tardío recibe SOLO el snapshot (+ los NetworkObjects de piezas sueltas).
            var s = _sc.Snapshot();

            // "Réplica cliente" reconstruida desde el snapshot:
            Assert.AreEqual(_dh.State.Money, s.Money);
            Assert.AreEqual(WarehouseRules.StoredUids(_dh.State.Cwh, "estanteria").Count, s.EstanteriaUsed);
            Assert.AreEqual(WarehouseRules.StoredUids(_dh.State.Cwh, "pale").Count, s.PaleUsed);
            Assert.AreEqual(WarehouseRules.StoredUids(_dh.State.Cwh, "suelo").Count, s.SueloUsed);
            for (int i = 0; i < EconomyData.MatIds.Length; i++)
                Assert.AreEqual(WarehouseRules.MaterialUnits(_dh.State.Mwh, EconomyData.MatIds[i]),
                    s.MaterialUnits[i], "material " + EconomyData.MatIds[i]);

            // Las piezas almacenadas conservan identidad/calidad/origen en el dominio
            // (el tardío las verá al retirarlas, no necesita replicarlas pieza a pieza).
            var stored = PieceRules.GetPiece(_dh.State.Reg, b.Uid);
            Assert.AreEqual("stored:estanteria", stored.Storage);
            Assert.AreEqual("test_1", stored.OriginObjectId);
        }

        // ---- extra §3: dos jugadores NO transportan la misma pieza
        [Test]
        public void E11_DosJugadoresNoTransportanLaMismaPieza()
        {
            var p = NewPiece();
            Carry(0UL, p.Uid);
            var r = _sc.OnPhysicalPickup(1UL, p.Uid);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("no_disponible_en_suelo", r.Reason, "ya no está en el suelo");
            Assert.AreEqual(0UL, _sc.CarrierOf(p.Uid).Value, "el transportista no cambia");
        }

        // ---- extra §2/§3: manos por jugador independientes (coop)
        [Test]
        public void E12_ManosPorJugador_Independientes()
        {
            var a = NewPiece(); var b = NewPiece();
            Carry(0UL, a.Uid);
            Assert.AreEqual("manos_ocupadas", _sc.OnPhysicalPickup(0UL, b.Uid).Reason,
                "j0 ya lleva una pieza (GDD 8.1)");
            Carry(1UL, b.Uid); // j1 independiente
            Assert.AreEqual(a.Uid, _sc.CarriedUid(0UL));
            Assert.AreEqual(b.Uid, _sc.CarriedUid(1UL));
        }

        // ---- extra §7: retiro LIFO con identidad conservada
        [Test]
        public void E13_RetiroDeZona_LifoConIdentidad()
        {
            var a = NewPiece(quality: 100); var b = NewPiece(quality: 55);
            Carry(0UL, a.Uid); _sc.UseStation(0UL, Estanteria, StationPos);
            Carry(0UL, b.Uid); _sc.UseStation(0UL, Estanteria, StationPos);
            Assert.AreEqual(2, _sc.Snapshot().EstanteriaUsed);

            var r = _sc.UseStation(1UL, Estanteria, StationPos); // j1 con manos libres retira
            Assert.IsTrue(r.Ok, "retiro: " + r.Reason);
            Assert.AreEqual(b.Uid, r.Piece.Uid, "LIFO: la última almacenada");
            Assert.AreEqual(55, r.Piece.Quality, "calidad conservada");
            Assert.AreEqual("carried", r.Piece.Storage);
            Assert.AreEqual(1UL, _sc.CarrierOf(b.Uid).Value, "queda en manos de j1");
            Assert.AreEqual(1, _sc.Snapshot().EstanteriaUsed);

            // j1 llevando una pieza frente a la estación ⇒ la ALMACENA (semántica
            // contextual: con pieza = guardar, manos libres = retirar).
            var r2 = _sc.UseStation(1UL, Estanteria, StationPos);
            Assert.IsTrue(r2.Ok, "vuelve a almacenar b");
            Assert.IsNull(_sc.CarriedUid(1UL), "manos libres otra vez");
            Assert.AreEqual(2, _sc.Snapshot().EstanteriaUsed);

            // Retiro directo con manos ocupadas ⇒ rechazado.
            var d = NewPiece();
            Carry(0UL, d.Uid);
            var r3 = _sc.RetrieveFrom(0UL, "estanteria");
            Assert.IsFalse(r3.Ok);
            Assert.AreEqual("manos_ocupadas", r3.Reason);
        }

        // ---- extra §3: pieza vendida NO vuelve a levantarse
        [Test]
        public void E14_PiezaVendida_NoVuelveAlMundo()
        {
            var p = NewPiece();
            Carry(0UL, p.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Balanza, StationPos).Ok);
            var r = _sc.OnPhysicalPickup(1UL, p.Uid);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("pieza_terminal", r.Reason);
        }

        // ---- extra §2: trazabilidad de operaciones (log con autor)
        [Test]
        public void E15_LogDeTransacciones_Trazabilidad()
        {
            var p = NewPiece();
            Carry(1UL, p.Uid);
            _sc.UseStation(1UL, Balanza, StationPos);

            bool encontroVenta = false;
            foreach (var tx in _sc.Log)
                if (tx.Op == "venta" && tx.Ok)
                {
                    encontroVenta = true;
                    Assert.AreEqual(1UL, tx.PlayerId, "quién vendió");
                    Assert.AreEqual(p.Uid, tx.Uid, "qué vendió");
                    Assert.AreEqual(14, tx.Amount, "por cuánto");
                }
            Assert.IsTrue(encontroVenta, "la venta quedó registrada");
        }

        // ---- extra: gate de distancia aplicado a estaciones
        [Test]
        public void E16_Estaciones_DistanciaObligatoria()
        {
            var p = NewPiece();
            Carry(2UL, p.Uid); // j2 transporta (el pickup físico no usa gate de estación)
            var r = _sc.UseStation(2UL, Balanza, StationPos); // pero está a ~106 m
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("distancia_invalida", r.Reason);
        }
    }
}
