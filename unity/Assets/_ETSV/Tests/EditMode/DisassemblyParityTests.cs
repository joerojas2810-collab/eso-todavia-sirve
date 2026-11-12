// [ETSV] PARIDAD 1:1 — port de prototype/tests/disassembly.test.mjs (20 aserciones).
// Cada [Test] con [Order(n)] replica la aserción n del script JS sobre el mismo
// escenario secuencial (instancia compartida), igual que el original.
// Matriz de equivalencia: unity/docs/MATRIZ_PARIDAD_TESTS.md

using System;
using NUnit.Framework;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class DisassemblyParityTests
    {
        private DisassemblyInstance _inst;
        private static readonly ObjectBlueprint BP = GameCatalog.Microondas; // OBJECT_CATALOG[0]

        private void EnsureInst()
        {
            if (_inst == null) _inst = DisassemblyRules.CreateInstance(BP, "test_1");
        }

        // workAll del JS: aplica `time` segundos a cada fijación del componente.
        private static void WorkAll(DisassemblyInstance inst, string comp, double time)
        {
            int n = inst.Comps[comp].Fasteners.Count;
            for (int i = 0; i < n; i++) DisassemblyRules.Work(inst, comp, i, time, time);
        }

        [Test, Order(1)]
        public void A01_AccesiblesIniciales()
        {
            EnsureInst();
            Assert.AreEqual(CompState.Accesible, _inst.Comps["carcasa"].State);
            Assert.AreEqual(CompState.Accesible, _inst.Comps["puerta"].State);
            Assert.AreEqual(CompState.Accesible, _inst.Comps["panel"].State);
        }

        [Test, Order(2)]
        public void A02_BloqueadosPorDependencia()
        {
            Assert.AreEqual(CompState.Bloqueado, _inst.Comps["plato"].State);
            Assert.AreEqual(CompState.Bloqueado, _inst.Comps["motor"].State);
            Assert.AreEqual(CompState.Bloqueado, _inst.Comps["modulo"].State);
        }

        [Test, Order(3)]
        public void A03_HerramientaIncorrectaRechazada()
        {
            var cw = DisassemblyRules.CanWork(_inst, "carcasa", 0, "llave", GameCatalog.FastenerTypes);
            Assert.IsFalse(cw.Ok);
            Assert.AreEqual("herramienta", cw.Why);
        }

        [Test, Order(4)]
        public void A04_DestornilladorTresSegundosPorTornillo()
        {
            var cw = DisassemblyRules.CanWork(_inst, "carcasa", 0, "destornillador", GameCatalog.FastenerTypes);
            Assert.IsTrue(cw.Ok);
            Assert.AreEqual(3.0, cw.Time);
        }

        [Test, Order(5)]
        public void A05_ProgresoInterrumpidoSeConserva()
        {
            DisassemblyRules.Work(_inst, "carcasa", 0, 1.5, 3.0);
            Assert.IsTrue(Math.Abs(_inst.Comps["carcasa"].Fasteners[0].Progress - 0.5) < 1e-9);
        }

        [Test, Order(6)]
        public void A06_TornilloRetiradoTrasTresSegundosAcumulados()
        {
            DisassemblyRules.Work(_inst, "carcasa", 0, 1.5, 3.0);
            Assert.IsTrue(_inst.Comps["carcasa"].Fasteners[0].Removed);
        }

        [Test, Order(7)]
        public void A07_NoSeExtraePiezaAunSujeta()
        {
            Assert.IsFalse(DisassemblyRules.CanExtract(_inst, "carcasa"));
        }

        [Test, Order(8)]
        public void A08_CarcasaLiberadaCuatroTornillos()
        {
            WorkAll(_inst, "carcasa", 3.0);
            Assert.AreEqual(CompState.Liberado, _inst.Comps["carcasa"].State);
        }

        [Test, Order(9)]
        public void A09_MotorSigueTapadoHastaExtraerCarcasa()
        {
            Assert.AreEqual(CompState.Bloqueado, _inst.Comps["motor"].State);
        }

        [Test, Order(10)]
        public void A10_MotorYModuloAccesiblesAlRetirarCarcasa()
        {
            DisassemblyRules.Extract(_inst, "carcasa");
            Assert.AreEqual(CompState.Accesible, _inst.Comps["motor"].State);
            Assert.AreEqual(CompState.Accesible, _inst.Comps["modulo"].State);
        }

        [Test, Order(11)]
        public void A11_PlatoExtraccionDirectaAlRetirarPuerta()
        {
            WorkAll(_inst, "puerta", 2.0);
            DisassemblyRules.Extract(_inst, "puerta");
            Assert.AreEqual(CompState.Liberado, _inst.Comps["plato"].State);
            DisassemblyRules.Extract(_inst, "plato");
        }

        [Test, Order(12)]
        public void A12_TrabajoCorrectoNoDana()
        {
            int q0 = _inst.Comps["motor"].Quality;
            WorkAll(_inst, "motor", 3.0);
            Assert.AreEqual(q0, _inst.Comps["motor"].Quality);
        }

        [Test, Order(13)]
        public void A13_GolpeBajaCalidadA88()
        {
            Assert.AreEqual(12, DisassemblyRules.Damage(_inst, "motor", 12));
            Assert.AreEqual(88, _inst.Comps["motor"].Quality);
        }

        [Test, Order(14)]
        public void A14_NoSeDanaLoYaExtraido()
        {
            Assert.AreEqual(0, DisassemblyRules.Damage(_inst, "carcasa", 12));
        }

        [Test, Order(15)]
        public void A15_NoSeProcesaSinExtraerAntes()
        {
            Assert.IsFalse(DisassemblyRules.SetTerminal(_inst, "panel", CompState.Procesado, "x"));
        }

        [Test, Order(16)]
        public void A16_ModuloSelladoResiduoEspecial()
        {
            WorkAll(_inst, "modulo", 2.5);
            DisassemblyRules.Extract(_inst, "modulo");
            Assert.IsTrue(DisassemblyRules.SetTerminal(_inst, "modulo", CompState.Procesado, "residuo especial"));
        }

        [Test, Order(17)]
        public void A17_DobleRecuperacionImposibleModulo()
        {
            Assert.IsFalse(DisassemblyRules.SetTerminal(_inst, "modulo", CompState.Vendido, null));
        }

        [Test, Order(18)]
        public void A18_MotorVendido()
        {
            DisassemblyRules.Extract(_inst, "motor");
            Assert.IsTrue(DisassemblyRules.SetTerminal(_inst, "motor", CompState.Vendido, "balanza"));
        }

        [Test, Order(19)]
        public void A19_DobleRecuperacionImposibleMotor()
        {
            Assert.IsFalse(DisassemblyRules.SetTerminal(_inst, "motor", CompState.Procesado, null));
        }

        [Test, Order(20)]
        public void A20_ObjetoCerradoCuandoTodoFueVendidoOProcesado()
        {
            WorkAll(_inst, "panel", 3.0);
            DisassemblyRules.Extract(_inst, "panel");
            DisassemblyRules.SetTerminal(_inst, "panel", CompState.Procesado, "triturado");
            foreach (var c in new[] { "carcasa", "puerta", "plato" })
                DisassemblyRules.SetTerminal(_inst, c, CompState.Vendido, "balanza");
            Assert.IsTrue(DisassemblyRules.AllSettled(_inst));
        }
    }
}
