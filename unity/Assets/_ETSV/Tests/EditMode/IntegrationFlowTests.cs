// [ETSV] Verificaciones de flujo integrado (Instrucción 09, Fase 3).
// NO duplican los tests unitarios existentes (HostSessionTests V01–V10,
// HostEconomyTests E01–E16, SavePersistenceTests S01–S14): acá se recorre
// UNA narrativa completa de juego a través de las tres capas (Domain → Sim →
// contratos de la capa Unity) para detectar descosidos entre subsistemas que
// los tests aislados no ven:
//   F01: desmontar de verdad un microondas (herramienta + ticks + extracción),
//        vender la pieza, procesar otra, vender el material, GUARDAR y
//        RESTAURAR — el dinero y los terminales deben cerrar exactos.
//   F02: dos jugadores cooperando sobre la MISMA pieza en tiempo real: el
//        progreso se acumula de ambos, uno extrae, el otro vende; la plata es
//        de la sesión (no del jugador) y sobrevive a un guardado.
// C# puro ⇒ corre en el banco unity/ci/build-and-test.sh sin Unity Editor.

using System;
using System.IO;
using NUnit.Framework;
using Etsv.Domain;
using Etsv.Integration;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class IntegrationFlowTests
    {
        private const int Balanza = 0, Trituradora = 1, ContenedorAcero = 6; // MAT-01
        private static readonly P3 StationPos = new P3(5f, 0f, 5f);
        private static readonly P3 ObjPos = new P3(5.8f, 0f, 5.8f);

        private DisassemblySessionHost _host;
        private ScrapyardSession _sc;
        private string _dir;
        private string _path;
        private static int _seq;

        [SetUp]
        public void Setup()
        {
            _host = new DisassemblySessionHost();
            _host.PlayerPos = (ulong id, out P3 p) =>
            {
                if (id == 0UL) { p = new P3(5.5f, 0f, 5.5f); return true; } // junto a objeto y estaciones
                if (id == 1UL) { p = new P3(6.0f, 0f, 5.0f); return true; } // idem
                p = default(P3);
                return false;
            };
            _sc = new ScrapyardSession(_host);
            _dir = Path.Combine(Path.GetTempPath(), "etsv_flow_tests");
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "f" + (_seq++) + "_" + Guid.NewGuid().ToString("N") + ".json");
            CleanFiles();
        }

        [TearDown]
        public void Teardown() { CleanFiles(); }

        private void CleanFiles()
        {
            foreach (var f in new[] { _path, _path + ".bak", _path + ".tmp" })
                if (File.Exists(f)) File.Delete(f);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Libera TODAS las fijaciones de un componente con la herramienta
        /// correcta, a fuerza de StartWork + WorkTick (el camino real del juego).</summary>
        private void Liberar(ulong who, int netId, string compId, int compIdx, string toolId)
        {
            Assert.IsTrue(_host.SelectTool(who, toolId).Ok, "herramienta equipada");
            var def = GameCatalog.Microondas.Components[compIdx];
            Assert.AreEqual(compId, def.Id, "índice del componente coherente con el catálogo");
            var ft = GameCatalog.FastenerTypes;
            for (int f = 0; f < def.Fasteners.Length; f++)
            {
                FastenerTypeDef type;
                Assert.IsTrue(ft.TryGetValue(def.Fasteners[f].Type, out type), "tipo de fijación existe");
                Assert.AreEqual(toolId, type.Tool, "la herramienta del flujo es la que pide la fijación");
                Assert.IsTrue(_host.StartWork(who, netId, compIdx, f, ObjPos).Ok, "StartWork f" + f);
                double t = 0;
                while (!_host.GetInstance(netId).Inst.Comps[compId].Fasteners[f].Removed && t < type.Time + 1.0)
                {
                    Assert.IsTrue(_host.WorkTick(who, netId, compIdx, f, 0.25, ObjPos).Ok, "WorkTick f" + f);
                    t += 0.25;
                }
                Assert.IsTrue(_host.GetInstance(netId).Inst.Comps[compId].Fasteners[f].Removed,
                    compId + " fijación " + f + " liberada en tiempo razonable");
            }
        }

        /// <summary>Extrae el componente (ya liberado) y devuelve la pieza del dominio.</summary>
        private StoredPart Extraer(ulong who, int netId, int compIdx)
        {
            var r = _host.Extract(who, netId, compIdx, ObjPos);
            Assert.IsTrue(r.Ok, "extracción ok (reason: " + r.Reason + ")");
            Assert.IsNotNull(r.Piece, "la extracción registra la pieza");
            return r.Piece;
        }

        /// <summary>La pieza pasa del mundo a las manos del jugador (físico → dominio).</summary>
        private void Levantar(ulong who, string uid)
        {
            Assert.IsTrue(_sc.OnPhysicalPickup(who, uid).Ok, "pickup físico reflejado en el dominio");
            Assert.AreEqual(who, _sc.CarrierOf(uid), "el dominio sabe quién la lleva");
        }

        // ---------------------------------------------------------------- F01

        [Test]
        public void F01_FlujoCompleto_DesmontarVenderProcesarVenderMaterialGuardarRestaurar()
        {
            Assert.AreEqual(0, _host.State.Money, "sesión arranca sin plata");

            // 1) Aparece el microondas y se desmonta DE VERDAD: carcasa (4 tornillos).
            int netId = _host.SpawnInstance("microondas");
            Liberar(0UL, netId, "carcasa", 0, "destornillador");
            var carcasa1 = Extraer(0UL, netId, 0);
            Assert.AreEqual("carcasa", carcasa1.DefId);
            Assert.AreEqual(22, carcasa1.VBase, "VBase del catálogo (GDD 3.0)");
            Assert.AreEqual(100, carcasa1.Quality, "sin daño: el flujo usó la herramienta correcta");

            // 2) La levanta y la vende en la balanza: Round(22 × 100/100) = 22.
            Levantar(0UL, carcasa1.Uid);
            var venta = _sc.UseStation(0UL, Balanza, StationPos);
            Assert.IsTrue(venta.Ok, "venta ok (reason: " + venta.Reason + ")");
            Assert.AreEqual(22, _host.State.Money, "dinero de la venta");
            Assert.AreEqual("vendido", PieceRules.GetPiece(_host.State.Reg, carcasa1.Uid).Terminal);

            // 3) Segundo microondas: su carcasa se PROCESA en la trituradora.
            int netId2 = _host.SpawnInstance("microondas");
            Liberar(1UL, netId2, "carcasa", 0, "destornillador");
            var carcasa2 = Extraer(1UL, netId2, 0);
            Levantar(1UL, carcasa2.Uid);
            var proc = _sc.UseStation(1UL, Trituradora, StationPos);
            Assert.IsTrue(proc.Ok, "procesado ok (reason: " + proc.Reason + ")");
            Assert.AreEqual("procesado", PieceRules.GetPiece(_host.State.Reg, carcasa2.Uid).Terminal);
            // Receta carcasa: MAT-01×4 ⇒ yields 4×0.9×100/100 = 3.6 ⇒ 3 unidades enteras.
            Assert.AreEqual(3, WarehouseRules.MaterialUnits(_host.State.Mwh, "MAT-01"),
                "unidades enteras de acero");

            // 4) Venta del material en su contenedor: 3 × $4 (acero) = $12 ⇒ total $34.
            var ventaMat = _sc.UseStation(1UL, ContenedorAcero, StationPos);
            Assert.IsTrue(ventaMat.Ok, "venta de material ok (reason: " + ventaMat.Reason + ")");
            Assert.AreEqual(34, _host.State.Money, "dinero acumulado del flujo completo");
            Assert.AreEqual(0, WarehouseRules.MaterialUnits(_host.State.Mwh, "MAT-01"),
                "el contenedor vendió TODO el material del tipo");

            // 5) Guardado y restauración: el flujo económico completo sobrevive.
            var save = SessionPersistence.Capture(_host, null, new EmptyPoses());
            var w = SaveRepository.SafeWrite(_path, save);
            Assert.IsTrue(w.Ok, "escritura segura");

            var h2 = new DisassemblySessionHost();
            h2.PlayerPos = _host.PlayerPos;
            var problemas = SessionPersistence.RestoreInto(SaveRepository.Load(_path).Save, h2);
            Assert.AreEqual(0, problemas.Count, "restauración sin problemas");

            Assert.AreEqual(34, h2.State.Money, "dinero idéntico tras cargar");
            Assert.AreEqual("vendido", PieceRules.GetPiece(h2.State.Reg, carcasa1.Uid).Terminal,
                "lo vendido NO reaparece");
            Assert.AreEqual("procesado", PieceRules.GetPiece(h2.State.Reg, carcasa2.Uid).Terminal,
                "lo procesado NO reaparece");
            Assert.AreEqual(0, WarehouseRules.MaterialUnits(h2.State.Mwh, "MAT-01"),
                "el material vendido no se regenera");
            Assert.AreEqual(2, h2.AllInstances().Count, "los dos objetos del patio siguen ahí");

            // 6) Y se puede SEGUIR jugando sobre la partida restaurada
            //    (los helpers pasan a apuntar al host restaurado).
            _host = h2;
            _sc = new ScrapyardSession(h2);
            var sc2 = _sc;
            Liberar(0UL, netId, "puerta", 1, "llave"); // bisagras del primer microondas
            var puerta = Extraer(0UL, netId, 1);
            var pick = sc2.OnPhysicalPickup(0UL, puerta.Uid);
            Assert.IsTrue(pick.Ok, "pickup post-carga (reason: " + pick.Reason + ")");
            Assert.IsTrue(sc2.UseStation(0UL, Balanza, StationPos).Ok, "venta post-carga");
            Assert.AreEqual(34 + 14, h2.State.Money, "Round(14 × 100/100) = 14 sobre los 34 guardados");
        }

        // ---------------------------------------------------------------- F02

        [Test]
        public void F02_Cooperativo_DosJugadoresSobreLaMismaPieza_LaPlataEsDeLaSesion()
        {
            int netId = _host.SpawnInstance("microondas");

            // Ambos equipan destornillador y trabajan fijaciones DISTINTAS de la
            // misma carcasa (coop real: no se pisan, el progreso se acumula).
            Assert.IsTrue(_host.SelectTool(0UL, "destornillador").Ok);
            Assert.IsTrue(_host.SelectTool(1UL, "destornillador").Ok);
            for (int f = 0; f < 4; f++)
            {
                ulong who = (f % 2 == 0) ? 0UL : 1UL;
                Assert.IsTrue(_host.StartWork(who, netId, 0, f, ObjPos).Ok, "j" + who + " arranca f" + f);
            }
            for (int round = 0; round < 14; round++) // 14 × 0.25 s = 3.5 s ≥ Time(3.0)
                for (int f = 0; f < 4; f++)
                {
                    ulong who = (f % 2 == 0) ? 0UL : 1UL;
                    if (!_host.GetInstance(netId).Inst.Comps["carcasa"].Fasteners[f].Removed)
                        _host.WorkTick(who, netId, 0, f, 0.25, ObjPos);
                }

            for (int f = 0; f < 4; f++)
                Assert.IsTrue(_host.GetInstance(netId).Inst.Comps["carcasa"].Fasteners[f].Removed,
                    "fijación " + f + " liberada por su jugador");

            // Extrae el jugador 1 (el 0 trabajó igual: la pieza es de la sesión).
            var carcasa = Extraer(1UL, netId, 0);
            Assert.IsFalse(_host.HasWorker(netId, 0, 0), "el trabajador 0 ya no tiene sesión activa");

            // El jugador 0 (que NO extrajo) la levanta y la vende.
            Levantar(0UL, carcasa.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Balanza, StationPos).Ok, "venta por el otro jugador");

            // La plata NO es del jugador: es del estado compartido de la sesión.
            Assert.AreEqual(22, _host.State.Money, "dinero de la sesión, no de un jugador");

            // Snapshot coherente para un late-joiner en medio del flujo.
            var snap = _host.Snapshot(netId);
            Assert.IsNotNull(snap, "snapshot disponible");

            // Guardado en caliente con los dos jugadores conectados.
            var save = SessionPersistence.Capture(_host, null, new EmptyPoses());
            Assert.IsTrue(SaveRepository.SafeWrite(_path, save).Ok);
            var h2 = new DisassemblySessionHost();
            h2.PlayerPos = _host.PlayerPos;
            Assert.AreEqual(0, SessionPersistence.RestoreInto(SaveRepository.Load(_path).Save, h2).Count);
            Assert.AreEqual(22, h2.State.Money, "la plata cooperativa sobrevive al guardado");
            Assert.AreEqual("vendido", PieceRules.GetPiece(h2.State.Reg, carcasa.Uid).Terminal);
        }

        /// <summary>Sin piezas sueltas en el mundo: poses vacías (el flujo las consume todo).</summary>
        private class EmptyPoses : IPoseSource
        {
            public bool ObjectPose(string domainId, out SavePose pose) { pose = default(SavePose); return false; }
            public bool PiecePose(string uid, out SavePose pose) { pose = default(SavePose); return false; }
        }
    }
}
