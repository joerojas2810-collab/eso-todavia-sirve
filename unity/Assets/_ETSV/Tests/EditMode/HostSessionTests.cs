// [ETSV] Verificaciones automatizadas de la Instrucción 06 §10 sobre la capa Sim
// (C# puro: DisassemblySessionHost + DisassemblyGate + DisassemblySync).
// Cubren la lógica host-autoritativa SIN Unity: dos jugadores sobre la misma
// pieza, herramienta incorrecta, extracción bloqueada, solicitudes duplicadas,
// distancia inválida, reconstrucción de estados, identidad persistente y
// sincronización de incorporación tardía (snapshot → réplica cliente).
// Lo que NO se puede verificar acá (replicación real por red, Test Runner de
// Unity, físicas) queda documentado en unity/docs/INTEGRACION_DESARMADO.md.

using System;
using NUnit.Framework;
using Etsv.Domain;
using Etsv.Integration;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class HostSessionTests
    {
        private const int CompCarcasa = 0; // GameCatalog.Microondas.Components[0]

        private DisassemblySessionHost _host;
        private int _netId;
        private P3 _objPos = new P3(10f, 1f, 10f);

        [SetUp]
        public void Setup()
        {
            _host = new DisassemblySessionHost();
            // Dos jugadores conectados, cerca del objeto (identidad + distancia OK).
            _host.PlayerPos = (ulong id, out P3 p) =>
            {
                if (id == 0UL) { p = new P3(10.5f, 1f, 10.5f); return true; } // host/jugador 1
                if (id == 1UL) { p = new P3(11.0f, 1f, 10.0f); return true; } // jugador 2
                if (id == 2UL) { p = new P3(80f, 1f, 80f); return true; }      // jugador 3 LEJOS
                p = default(P3);
                return false; // cliente desconocido
            };
            _netId = _host.SpawnInstance("microondas");
            Assert.AreEqual(1, _netId, "primera instancia = NetId 1");
        }

        // ---- §10.1 Dos jugadores intentando extraer la misma pieza
        [Test]
        public void V01_DosJugadoresExtraenLaMismaPieza_SoloUnoLoLogra()
        {
            LiberarCarcasaComo(0UL); // jugador 0 trabaja y libera la carcasa

            var r0 = _host.Extract(0UL, _netId, CompCarcasa, _objPos);
            var r1 = _host.Extract(1UL, _netId, CompCarcasa, _objPos); // solicitud "simultánea"

            Assert.IsTrue(r0.Ok && r0.Piece != null, "el primero extrae");
            Assert.IsFalse(r1.Ok, "el segundo NO extrae");
            Assert.AreEqual("no_liberado", r1.Reason, "el dominio ya lo marcó extraído");

            // Sin duplicación de recursos: una sola pieza en el registro.
            int count = 0;
            foreach (StoredPart p in PieceRules.SerializeRegistry(_host.State.Reg))
                if (p.DefId == "carcasa") count++;
            Assert.AreEqual(1, count, "no se duplica la pieza");
        }

        // ---- §10.2 Trabajo con herramienta incorrecta
        [Test]
        public void V02_HerramientaIncorrecta_Rechazada()
        {
            _host.SelectTool(0UL, "llave"); // la carcasa necesita destornillador
            var r = _host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("herramienta", r.Reason, "paridad con disassembly.test.mjs #3");

            _host.SelectTool(0UL, "destornillador");
            Assert.IsTrue(_host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos).Ok,
                "con la herramienta correcta sí");
        }

        [Test]
        public void V02b_HerramientaEsAutoritativaDelHost_NoDelCliente()
        {
            // El cliente nunca declara su herramienta en WorkTick: el host usa su registro.
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos);
            _host.SelectTool(0UL, "manos"); // cambia a herramienta inválida en el host
            var r = _host.WorkTick(0UL, _netId, CompCarcasa, 0, 0.1, _objPos);
            Assert.IsFalse(r.Ok && r.Reason == null, "tick con herramienta inválida no avanza");
            Assert.AreEqual("herramienta", r.Reason);
            Assert.IsFalse(_host.HasWorker(_netId, CompCarcasa, 0), "la sesión de trabajo se libera");
        }

        // ---- §10.3 Extracción bloqueada
        [Test]
        public void V03_ExtraccionBloqueada_Rechazada()
        {
            // motor (idx 4) está Bloqueado por carcasa en el estado inicial.
            var r = _host.Extract(0UL, _netId, 4, _objPos);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("no_liberado", r.Reason);

            // Tampoco se puede extraer un componente Accesible con fijaciones pendientes.
            var rc = _host.Extract(0UL, _netId, CompCarcasa, _objPos);
            Assert.IsFalse(rc.Ok, "carcasa accesible pero sujeta → no extraíble");
        }

        // ---- §10.4 Solicitudes duplicadas
        [Test]
        public void V04_SolicitudesDuplicadas()
        {
            _host.SelectTool(0UL, "destornillador");
            _host.SelectTool(1UL, "destornillador");

            Assert.IsTrue(_host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos).Ok);
            // Mismo jugador repite Start: idempotente (no rompe nada).
            Assert.IsTrue(_host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos).Ok);
            // OTRO jugador sobre la misma fijación: rechazado (operación simultánea).
            var dup = _host.StartWork(1UL, _netId, CompCarcasa, 0, _objPos);
            Assert.IsFalse(dup.Ok);
            Assert.AreEqual("ocupado_por_otro", dup.Reason);
            // Tick del intruso: rechazado.
            var tick = _host.WorkTick(1UL, _netId, CompCarcasa, 0, 0.1, _objPos);
            Assert.IsFalse(tick.Ok && tick.Reason == null);
            Assert.AreEqual("ocupado_por_otro", tick.Reason);
            // Tick sin sesión iniciada: rechazado.
            Assert.AreEqual("sin_sesion_trabajo",
                _host.WorkTick(0UL, _netId, CompCarcasa, 1, 0.1, _objPos).Reason);
        }

        [Test]
        public void V04b_SesionAbandonadaSeLibera_Pruning()
        {
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos);
            Assert.AreEqual(0, _host.PruneStaleWorkers(0.3), "con ticks recientes no se libera");
            Assert.AreEqual(1, _host.PruneStaleWorkers(0.5), "timeout (0.75 s) → sesión liberada");
            // Ahora el jugador 1 puede tomarla.
            _host.SelectTool(1UL, "destornillador");
            Assert.IsTrue(_host.StartWork(1UL, _netId, CompCarcasa, 0, _objPos).Ok);
        }

        [Test]
        public void V04c_DtDeTickAcotado_NoAceleraElTrabajo()
        {
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos);
            // Un cliente malicioso manda dt=100 s: el host lo acota a MaxTickDt (0.25).
            _host.WorkTick(0UL, _netId, CompCarcasa, 0, 100f, _objPos);
            var snap = _host.Snapshot(_netId)[CompCarcasa];
            double prog = (snap.FastenerBits & 0x7F) / 127.0;
            Assert.Less(prog, 0.2, "el progreso de un tick no puede exceder el tope");
        }

        // ---- §10.5 Distancia inválida
        [Test]
        public void V05_DistanciaInvalida_Rechazada()
        {
            _host.SelectTool(2UL, "destornillador"); // jugador 3 está a ~99 m
            Assert.AreEqual("distancia_invalida",
                _host.StartWork(2UL, _netId, CompCarcasa, 0, _objPos).Reason);
            Assert.AreEqual("distancia_invalida", _host.Extract(2UL, _netId, CompCarcasa, _objPos).Reason);
            Assert.AreEqual("distancia_invalida",
                _host.Damage(2UL, _netId, CompCarcasa, 12, _objPos).Reason);
            // Cliente inexistente (identidad inválida).
            Assert.AreEqual("jugador_desconocido",
                _host.StartWork(99UL, _netId, CompCarcasa, 0, _objPos).Reason);
        }

        // ---- §10.6 Reconstrucción de estados (snapshot → réplica)
        [Test]
        public void V06_ReconstrucciónDeEstadosDesdeSnapshot()
        {
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos);
            for (int i = 0; i < 14; i++) _host.WorkTick(0UL, _netId, CompCarcasa, 0, 0.25, _objPos); // 3.5 s → tornillo 0 fuera
            _host.Damage(0UL, _netId, 1, 12, _objPos); // golpe a la puerta → calidad 88

            var snap = _host.Snapshot(_netId);
            var bp = GameCatalog.Microondas;
            var decoded = DisassemblySync.Decode(snap, bp);

            Assert.AreEqual(bp.Components.Length, decoded.Count, "todos los componentes viajan");
            var carcasa = decoded[CompCarcasa];
            Assert.AreEqual(CompState.Accesible, carcasa.State);
            Assert.IsTrue(carcasa.FastenerRemoved[0], "tornillo 0 retirado");
            Assert.IsFalse(carcasa.FastenerRemoved[1], "tornillo 1 intacto");
            Assert.AreEqual(88, decoded[1].Quality, "calidad dañada reconstruida");
            Assert.AreEqual(CompState.Bloqueado, decoded[4].State, "motor sigue bloqueado");
        }

        // ---- §10.7 Identidad persistente
        [Test]
        public void V07_IdentidadPersistenteDeInstanciasYPiezas()
        {
            LiberarCarcasaComo(0UL);
            var r = _host.Extract(0UL, _netId, CompCarcasa, _objPos);
            string uid = r.Piece.Uid;

            // uid ↔ int de red: ida y vuelta estable.
            int net = DisassemblySessionHost.PieceUidToNet(uid);
            Assert.Greater(net, 0, "uid se mapea a int de red");
            Assert.AreEqual(uid, DisassemblySessionHost.NetToPieceUid(net), "ida y vuelta sin pérdida");

            // La pieza conserva origen e identidad en el registro del dominio.
            var stored = PieceRules.GetPiece(_host.State.Reg, uid);
            Assert.IsNotNull(stored);
            Assert.AreEqual("microondas_1", stored.OriginObjectId, "origen = instancia del dominio");
            Assert.AreEqual("carcasa", stored.DefId);

            // El estado persistible (SaveGame v3) la incluye con su uid una vez
            // ALMACENADA (las piezas sueltas en el mundo no persisten: PROV-11,
            // comportamiento heredado del prototipo y documentado).
            Assert.IsTrue(EconomyOps.PickupPiece(_host.Ctx, uid).Ok, "la levanta");
            Assert.IsTrue(EconomyOps.StorePiece(_host.Ctx, uid, "pale").Ok, "P2 → palé");
            var save = SaveSerializer.Serialize(_host.State);
            var back = SaveSerializer.Deserialize(save);
            Assert.IsNotNull(PieceRules.GetPiece(back.Reg, uid), "uid sobrevive al round-trip");
            Assert.AreEqual("stored:pale", PieceRules.GetPiece(back.Reg, uid).Storage,
                "ubicación lógica persistida");
            Assert.AreEqual(0, SaveValidator.Validate(save).Count, "save íntegro");
        }

        // ---- §10.8 Jugador que se incorpora tarde
        [Test]
        public void V08_IncorporacionTardia_RecibeEstadoActual()
        {
            // Partida avanzada: 2 tornillos fuera, puerta golpeada, panel intacto.
            _host.SelectTool(0UL, "destornillador");
            for (int f = 0; f < 2; f++)
            {
                _host.StartWork(0UL, _netId, CompCarcasa, f, _objPos);
                for (int i = 0; i < 14; i++) _host.WorkTick(0UL, _netId, CompCarcasa, f, 0.25, _objPos);
                _host.StopWork(0UL, _netId, CompCarcasa, f);
            }
            _host.Damage(0UL, _netId, 1, 12, _objPos);

            // "Late join" = NGO entrega el estado inicial de las NetworkVariables con
            // el spawn; en la capa pura equivale a reconstruir desde el snapshot actual.
            var snap = _host.Snapshot(_netId);
            var decoded = DisassemblySync.Decode(snap, GameCatalog.Microondas);

            Assert.IsTrue(decoded[CompCarcasa].FastenerRemoved[0] && decoded[CompCarcasa].FastenerRemoved[1],
                "el tardío ve los tornillos ya retirados");
            Assert.IsFalse(decoded[CompCarcasa].FastenerRemoved[2], "y los pendientes como tales");
            Assert.AreEqual(88, decoded[1].Quality, "calidad actual de la puerta");
            Assert.AreEqual(100, decoded[2].Quality, "panel intacto");
            Assert.Less(decoded[CompCarcasa].FastenerProgress[2], 0.01, "progreso intacto del pendiente");
        }

        // ---- extra: progreso persistente entre sesiones de trabajo (paridad JS #5)
        [Test]
        public void V09_ProgresoSeConservaAlInterrumpir()
        {
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, _netId, CompCarcasa, 0, _objPos);
            // 6 ticks × 0.25 s (el tope del host) = 1.5 s trabajados.
            for (int i = 0; i < 6; i++) _host.WorkTick(0UL, _netId, CompCarcasa, 0, 0.25, _objPos);
            _host.StopWork(0UL, _netId, CompCarcasa, 0);

            var inst = _host.GetInstance(_netId).Inst;
            Assert.AreEqual(0.5, inst.Comps["carcasa"].Fasteners[0].Progress, 1e-9,
                "1.5 s de 3.0 → 50% (paridad con disassembly.test.mjs #5)");

            // Otro jugador retoma donde quedó.
            _host.SelectTool(1UL, "destornillador");
            Assert.IsTrue(_host.StartWork(1UL, _netId, CompCarcasa, 0, _objPos).Ok);
            for (int i = 0; i < 7; i++) _host.WorkTick(1UL, _netId, CompCarcasa, 0, 0.25, _objPos);
            Assert.IsTrue(inst.Comps["carcasa"].Fasteners[0].Removed, "completado entre ambos");
            Assert.AreEqual(CompState.Accesible, inst.Comps["carcasa"].State, "aún sujeta por 3 tornillos");
        }

        // ---- extra: daño por golpe con herramienta incorrecta (paridad JS #13)
        [Test]
        public void V10_GolpeConHerramientaIncorrecta_Dana()
        {
            _host.SelectTool(0UL, "llave"); // incorrecta para tornillos
            var r = _host.Damage(0UL, _netId, CompCarcasa, 12, _objPos);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(12, r.Applied);
            Assert.AreEqual(88, r.Quality, "paridad con disassembly.test.mjs #13");
        }

        // ---------------------------------------------------------------- helper
        private void LiberarCarcasaComo(ulong clientId)
        {
            _host.SelectTool(clientId, "destornillador");
            for (int f = 0; f < 4; f++)
            {
                var s = _host.StartWork(clientId, _netId, CompCarcasa, f, _objPos);
                Assert.IsTrue(s.Ok, "start fijación " + f + ": " + s.Reason);
                for (int i = 0; i < 14; i++)
                    _host.WorkTick(clientId, _netId, CompCarcasa, f, 0.25, _objPos); // 3.5 s
                _host.StopWork(clientId, _netId, CompCarcasa, f);
            }
            Assert.AreEqual(CompState.Liberado,
                _host.GetInstance(_netId).Inst.Comps["carcasa"].State, "carcasa liberada");
        }
    }
}
