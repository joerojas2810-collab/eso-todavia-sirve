// [ETSV] Verificaciones automatizadas de la Instrucción 08 §8 sobre la capa Sim
// (SessionPersistence + SaveRepository + dominio v4). C# puro ⇒ ejecutables sin
// Unity: round-trip, desmontaje parcial, piezas extraídas, anti-duplicación,
// almacenes/fracciones, calidad/reservas, desconexión durante transporte, copia
// de respaldo, corrupción, migración v3→v4 y sync de incorporación tardía.
// La reconstrucción física real (NetworkObjects) y la replicación quedan
// pendientes del Unity Editor (documentado en unity/docs/PERSISTENCIA.md).

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Etsv.Domain;
using Etsv.Integration;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class SavePersistenceTests
    {
        private const int Balanza = 0, Trituradora = 1, Estanteria = 3;
        private static readonly P3 StationPos = new P3(5f, 0f, 5f);
        // Objeto cerca del cliente 0 Y de las estaciones (el gate de presencia
        // exige ≤5 m tanto para trabajar como para operar estaciones).
        private static readonly P3 ObjPos = new P3(5.8f, 0f, 5.8f);

        private DisassemblySessionHost _host;
        private ScrapyardSession _sc;
        private string _dir;
        private string _path;
        private static int _seq;

        /// <summary>Poses sintéticas deterministas (en Unity las aportan los transforms).</summary>
        private class TestPoses : IPoseSource
        {
            public readonly Dictionary<string, SavePose> Objects = new Dictionary<string, SavePose>();
            public readonly Dictionary<string, SavePose> Pieces = new Dictionary<string, SavePose>();
            public bool ObjectPose(string domainId, out SavePose pose) { return Objects.TryGetValue(domainId, out pose); }
            public bool PiecePose(string uid, out SavePose pose) { return Pieces.TryGetValue(uid, out pose); }
        }

        private TestPoses _poses;

        [SetUp]
        public void Setup()
        {
            _host = NewHost();
            _sc = new ScrapyardSession(_host);
            _poses = new TestPoses();
            _dir = Path.Combine(Path.GetTempPath(), "etsv_save_tests");
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, "s" + (_seq++) + "_" + Guid.NewGuid().ToString("N") + ".json");
            CleanFiles();
        }

        [TearDown]
        public void Teardown() { CleanFiles(); }

        private void CleanFiles()
        {
            foreach (var f in new[] { _path, _path + ".bak", _path + ".tmp" })
                if (File.Exists(f)) File.Delete(f);
        }

        private DisassemblySessionHost NewHost()
        {
            var h = new DisassemblySessionHost();
            h.PlayerPos = (ulong id, out P3 p) =>
            {
                if (id == 0UL) { p = new P3(5.5f, 0f, 5.5f); return true; }  // junto a estaciones y objeto
                if (id == 1UL) { p = new P3(10.5f, 1f, 10.5f); return true; }
                p = default(P3);
                return false;
            };
            return h;
        }

        private StoredPart NewPiece(string defId = "puerta", int quality = 100, int vbase = 14,
            string wc = "P1", bool sealed_ = false, RecipeEntry[] recipe = null)
        {
            var r = EconomyOps.ExtractPiece(_host.Ctx, new PieceCreateOptions
            {
                DefId = defId, DefName = defId, OriginObjectId = "test_1",
                Quality = quality, Kg = 1, WeightClass = wc, VBase = vbase,
                Owner = _host.Ctx.SessionOwner, Sealed = sealed_,
                Recipe = recipe ?? new[] { RecipeEntry.OfMat("MAT-05", 2) },
            });
            Assert.IsTrue(r.Ok, "registro de pieza de prueba");
            return r.Piece;
        }

        private void Carry(ulong clientId, string uid)
        {
            var r = _sc.OnPhysicalPickup(clientId, uid);
            Assert.IsTrue(r.Ok, "llevar " + uid + ": " + r.Reason);
        }

        /// <summary>Devuelve un host nuevo con la partida del archivo cargada.</summary>
        private DisassemblySessionHost RestoreNewHost()
        {
            var res = SaveRepository.Load(_path);
            Assert.IsNotNull(res.Save, "carga del archivo: " + string.Join(";", res.Problems.ToArray()));
            var h2 = NewHost();
            var problems = SessionPersistence.RestoreInto(res.Save, h2);
            Assert.AreEqual(0, problems.Count, "restauración: " + string.Join(";", problems.ToArray()));
            return h2;
        }

        // ---- S01 (§8.1) Guardar y cargar sin modificaciones: round-trip completo
        [Test]
        public void S01_RoundTrip_DineroPiezasAlmacenesYPatio()
        {
            int netId = _host.SpawnInstance("microondas");
            _poses.Objects["microondas_1"] = new SavePose(10, 1, 10, 30);

            // Desmontaje real: tornillo 0 de la carcasa retirado, tornillo 1 a la mitad.
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, netId, 0, 0, ObjPos);
            for (int i = 0; i < 14; i++) _host.WorkTick(0UL, netId, 0, 0, 0.25, ObjPos); // 3.5 s ⇒ fuera
            _host.StopWork(0UL, netId, 0, 0);
            _host.StartWork(0UL, netId, 0, 1, ObjPos);
            for (int i = 0; i < 6; i++) _host.WorkTick(0UL, netId, 0, 1, 0.25, ObjPos);   // 1.5/3.0 = 0.5
            _host.Damage(0UL, netId, 1, 12, ObjPos); // puerta golpeada: calidad 88

            // Una pieza extraída por el flujo real (carcasa liberada ⇒ hace falta
            // completar los 4 tornillos; el 1 ya va por la mitad).
            for (int f = 1; f < 4; f++)
            {
                _host.StartWork(0UL, netId, 0, f, ObjPos);
                for (int i = 0; i < 14; i++) _host.WorkTick(0UL, netId, 0, f, 0.25, ObjPos);
                _host.StopWork(0UL, netId, 0, f);
            }
            var ext = _host.Extract(0UL, netId, 0, ObjPos);
            Assert.IsTrue(ext.Ok, "carcasa extraída");
            _poses.Pieces[ext.Piece.Uid] = new SavePose(9.5, 1.2, 9.5, 0);

            var loose = NewPiece(quality: 73, vbase: 50);
            _poses.Pieces[loose.Uid] = new SavePose(4, 0.2, 6, 90);

            // Economía: algo de plata y fracciones de material.
            WarehouseRules.AddMaterialYield(_host.State.Mwh, "MAT-03", 3.6);
            WarehouseRules.AddMaterialYield(_host.State.Mwh, "MAT-03", 3.6); // 7.2 ⇒ 7 u + 0.2

            var save = SessionPersistence.Capture(_host, null, _poses);
            Assert.IsNotNull(save.SaveId, "id de partida generado");
            Assert.IsNotNull(save.SavedAtUtc, "fecha de guardado");
            var w = SaveRepository.SafeWrite(_path, save);
            Assert.IsTrue(w.Ok, "escritura segura: " + string.Join(";", w.Problems.ToArray()));

            // Cargar en un host NUEVO y comparar todo.
            var saveId = save.SaveId;
            var h2 = RestoreNewHost();
            var sc2 = new ScrapyardSession(h2);

            Assert.AreEqual(_host.State.Money, h2.State.Money, "dinero");
            Assert.AreEqual(2, h2.State.Reg.Pieces.Count, "piezas (extraída + suelta)");
            var p1 = PieceRules.GetPiece(h2.State.Reg, ext.Piece.Uid);
            Assert.IsNotNull(p1, "pieza extraída restaurada");
            Assert.AreEqual("carcasa", p1.DefId);
            Assert.AreEqual(100, p1.Quality, "calidad de la pieza");
            Assert.AreEqual("microondas_1", p1.OriginObjectId, "origen");
            var p2 = PieceRules.GetPiece(h2.State.Reg, loose.Uid);
            Assert.AreEqual(73, p2.Quality, "calidad dañada conservada");
            Assert.AreEqual(7, WarehouseRules.MaterialUnits(h2.State.Mwh, "MAT-03"), "unidades de material");
            Assert.IsTrue(Math.Abs(WarehouseRules.MaterialFraction(h2.State.Mwh, "MAT-03") - 0.2) < 1e-9,
                "fracción sin perder");

            // Patio: instancia, huella, estados, fijaciones y daño.
            var insts = h2.AllInstances();
            Assert.AreEqual(1, insts.Count, "un objeto en el patio");
            Assert.AreEqual("microondas_1", insts[0].DomainId);
            var ic = insts[0].Inst.Comps["carcasa"];
            Assert.AreEqual(CompState.Extraido, ic.State, "carcasa extraída");
            var puerta = insts[0].Inst.Comps["puerta"];
            Assert.AreEqual(88, puerta.Quality, "daño de la puerta conservado");
            var f0 = ic.Fasteners[0];
            Assert.IsTrue(f0.Removed, "tornillo 0 retirado");

            // El SaveId es estable entre guardados de la misma partida.
            var save2 = SessionPersistence.Capture(h2, saveId, _poses);
            Assert.AreEqual(saveId, save2.SaveId, "SaveId estable");
            Assert.AreEqual(GameInfo.SaveVersionFull, save2.Version, "versión del formato");
            _ = sc2;
        }

        // ---- S02 (§8.2) Guardar durante un desmontaje parcial
        [Test]
        public void S02_GuardadoDuranteDesmontajeParcial_PuntoDeConsistencia()
        {
            int netId = _host.SpawnInstance("microondas");
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, netId, 0, 0, ObjPos);
            for (int i = 0; i < 6; i++) _host.WorkTick(0UL, netId, 0, 0, 0.25, ObjPos);
            double progressAntes = _host.GetInstance(netId).Inst.Comps["carcasa"].Fasteners[0].Progress;
            Assert.Greater(progressAntes, 0, "hay progreso acumulado");
            Assert.IsTrue(_host.HasWorker(netId, 0, 0), "sesión de trabajo activa");

            var save = SessionPersistence.Capture(_host, null, _poses);

            // GDD §16.3/§16.4: la operación manual activa se cancela SIN penalización.
            Assert.IsFalse(_host.HasWorker(netId, 0, 0), "sesión cancelada en el punto de consistencia");
            double progressDespues = _host.GetInstance(netId).Inst.Comps["carcasa"].Fasteners[0].Progress;
            Assert.AreEqual(progressAntes, progressDespues, 1e-12, "progreso conservado (sin penalización)");

            Assert.AreEqual(progressAntes, save.Yard[0].Components[0].Fasteners[0].Progress, 1e-12,
                "progreso con precisión completa en el save (no cuantizado)");
            Assert.IsFalse(save.Yard[0].Components[0].Fasteners[0].Removed, "no se completa solo");

            var w = SaveRepository.SafeWrite(_path, save);
            Assert.IsTrue(w.Ok);
            var h2 = RestoreNewHost();
            var f = h2.AllInstances()[0].Inst.Comps["carcasa"].Fasteners[0];
            Assert.AreEqual(progressAntes, f.Progress, 1e-12, "progreso restaurado exacto");
            Assert.IsFalse(f.Removed);
            // Y se puede seguir trabajando donde quedó.
            h2.PlayerPos = _host.PlayerPos;
            h2.SelectTool(0UL, "destornillador");
            Assert.IsTrue(h2.StartWork(0UL, h2.AllInstances()[0].NetId, 0, 0, ObjPos).Ok,
                "el trabajo continúa tras la carga");
        }

        // ---- S03 (§8.3) Restaurar piezas extraídas; lo recuperado NO reaparece
        [Test]
        public void S03_PiezasVendidasProcesadasAlmacenadas_NoReaparecenEnElPatio()
        {
            var vendida = NewPiece(defId: "panel", vbase: 100);
            var procesada = NewPiece(defId: "carcasa", recipe: new[] { RecipeEntry.OfMat("MAT-01", 4) });
            var almacenada = NewPiece(defId: "puerta");
            var mundo = NewPiece(defId: "plato", vbase: 9);

            _poses.Pieces[mundo.Uid] = new SavePose(3, 0.2, 4, 0);

            Carry(0UL, vendida.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Balanza, StationPos).Ok, "venta");
            Carry(0UL, procesada.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Trituradora, StationPos).Ok, "procesado");
            Carry(0UL, almacenada.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Estanteria, StationPos).Ok, "almacenado");

            var save = SessionPersistence.Capture(_host, null, _poses);

            // Solo la pieza en mundo tiene pose física en el save.
            Assert.AreEqual(1, save.PiecePoses.Count, "una única pieza física");
            Assert.AreEqual(mundo.Uid, save.PiecePoses[0].Uid);

            var w = SaveRepository.SafeWrite(_path, save);
            Assert.IsTrue(w.Ok);
            var h2 = RestoreNewHost();

            Assert.AreEqual("vendido", PieceRules.GetPiece(h2.State.Reg, vendida.Uid).Terminal, "terminal venta");
            Assert.AreEqual("procesado", PieceRules.GetPiece(h2.State.Reg, procesada.Uid).Terminal, "terminal procesado");
            var al = PieceRules.GetPiece(h2.State.Reg, almacenada.Uid);
            Assert.AreEqual("stored:estanteria", al.Storage, "almacenada sigue almacenada (sin físico)");
            Assert.IsTrue(WarehouseRules.StoredUids(h2.State.Cwh, "estanteria").Contains(almacenada.Uid), "zona");
            var m = PieceRules.GetPiece(h2.State.Reg, mundo.Uid);
            Assert.AreEqual("world", m.Storage, "pieza en mundo con identidad completa");
            Assert.AreEqual(9, m.VBase);
            Assert.AreEqual(3, WarehouseRules.MaterialUnits(h2.State.Mwh, "MAT-01"), "materiales del procesado (4×0.9=3.6→3u)");
        }

        // ---- S04 (§8.4) Evitar duplicaciones: cadena guardar→cargar→guardar→cargar
        [Test]
        public void S04_CadenaDeGuardados_NoDuplicaNiResucita()
        {
            var p = NewPiece(vbase: 100);
            Carry(0UL, p.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Balanza, StationPos).Ok, "venta $100");
            Assert.AreEqual(100, _host.State.Money);

            var save1 = SessionPersistence.Capture(_host, null, _poses);
            Assert.IsTrue(SaveRepository.SafeWrite(_path, save1).Ok);

            var h2 = RestoreNewHost();
            Assert.AreEqual(100, h2.State.Money, "dinero tras primera carga");
            Assert.AreEqual(1, h2.State.Reg.Pieces.Count, "una sola pieza");

            var save2 = SessionPersistence.Capture(h2, save1.SaveId, _poses);
            Assert.IsTrue(SaveRepository.SafeWrite(_path, save2).Ok, "segundo guardado (con .bak)");
            Assert.IsTrue(File.Exists(_path + ".bak"), "copia de respaldo creada");

            var h3 = RestoreNewHost();
            Assert.AreEqual(100, h3.State.Money, "dinero NO duplicado");
            Assert.AreEqual(1, h3.State.Reg.Pieces.Count, "piezas NO duplicadas");
            Assert.AreEqual(save1.SaveId, save2.SaveId, "misma partida");

            // La pieza vendida no se puede volver a operar tras la carga.
            var sc3 = new ScrapyardSession(h3);
            var r = sc3.OnPhysicalPickup(0UL, p.Uid);
            Assert.IsFalse(r.Ok, "pieza vendida no re-levantable");
            Assert.AreEqual("pieza_terminal", r.Reason);
        }

        // ---- S05 (§8.5) Almacenes y fracciones de material
        [Test]
        public void S05_FraccionesDeMaterial_SobrevivenAlRoundTrip()
        {
            WarehouseRules.AddMaterialYield(_host.State.Mwh, "MAT-03", 3.6);
            WarehouseRules.AddMaterialYield(_host.State.Mwh, "MAT-03", 3.6); // 7.2
            WarehouseRules.AddMaterialYield(_host.State.Mwh, "MAT-02", 0.4);
            var p = NewPiece();
            Carry(0UL, p.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Estanteria, StationPos).Ok, "almacenar");

            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);
            var h2 = RestoreNewHost();

            Assert.AreEqual(7, WarehouseRules.MaterialUnits(h2.State.Mwh, "MAT-03"));
            Assert.IsTrue(Math.Abs(WarehouseRules.MaterialFraction(h2.State.Mwh, "MAT-03") - 0.2) < 1e-9,
                "0.2 de fracción exacta");
            Assert.AreEqual(0, WarehouseRules.MaterialUnits(h2.State.Mwh, "MAT-02"));
            Assert.IsTrue(Math.Abs(WarehouseRules.MaterialFraction(h2.State.Mwh, "MAT-02") - 0.4) < 1e-9,
                "fracción menor que 1 unidad conservada");
            Assert.IsTrue(WarehouseRules.StoredUids(h2.State.Cwh, "estanteria").Contains(p.Uid));
        }

        // ---- S06 (§8.6) Calidad y reservas conservadas
        [Test]
        public void S06_CalidadYReserva_SobrevivenYSiguenBloqueando()
        {
            var p = NewPiece(quality: 41, vbase: 80);
            Assert.IsTrue(EconomyOps.ReservePiece(_host.Ctx, p.Uid, "contrato").Ok, "reserva");

            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);
            var h2 = RestoreNewHost();

            var q = PieceRules.GetPiece(h2.State.Reg, p.Uid);
            Assert.AreEqual(41, q.Quality, "calidad conservada");
            Assert.AreEqual("contrato", q.ReservedFor, "reserva conservada");

            // Tras la carga la reserva SIGUE bloqueando la venta (CheckOperable).
            var sell = EconomyOps.SellPiece(h2.Ctx, p.Uid);
            Assert.IsFalse(sell.Ok, "venta bloqueada por reserva");
            Assert.AreEqual("pieza_reservada", sell.Reason);
        }

        // ---- S07 (§8.7) Desconexión durante transporte
        [Test]
        public void S07_DesconexionDuranteTransporte_PiezaAlSueloSinDuplicar()
        {
            var p = NewPiece(vbase: 30);
            Carry(1UL, p.Uid);
            _poses.Pieces[p.Uid] = new SavePose(7, 0.5, 8, 0);

            // El cliente 1 se desconecta: la pieza vuelve al suelo lógico.
            var d = _sc.OnPlayerDisconnected(1UL);
            Assert.IsTrue(d.Ok, "soltar al desconectar");

            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);
            var h2 = RestoreNewHost();

            int count = 0;
            foreach (var q in h2.State.Reg.Pieces.Values) if (q.DefId == p.DefId) count++;
            Assert.AreEqual(1, count, "una sola pieza (sin duplicar)");
            Assert.AreEqual("world", PieceRules.GetPiece(h2.State.Reg, p.Uid).Storage, "en el suelo");
            Assert.AreEqual(0, h2.Ctx.CarryByPlayer.Count, "nadie transporta tras la carga");
        }

        // ---- S08 (§8.8) Recuperar la copia de respaldo
        [Test]
        public void S08_ArchivoPrincipalCorrupto_SeRecuperaDesdeElBackup()
        {
            var p = NewPiece(vbase: 55);
            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);

            // Segundo guardado válido ⇒ el anterior queda como .bak.
            _host.State.Money += 77;
            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);

            // Corrupción del archivo principal.
            File.WriteAllText(_path, "{{{ esto no es JSON");

            var res = SaveRepository.Load(_path);
            Assert.IsNotNull(res.Save, "carga desde el respaldo");
            Assert.IsTrue(res.FromBackup, "marcado como recuperado del .bak");
            // El .bak conserva el guardado ANTERIOR al último (GDD §16.3 paso 7:
            // respaldo del archivo sustituido). El último (money 77) se perdió con
            // la corrupción; se recupera la última copia íntegra conocida.
            Assert.AreEqual(0, res.Save.Money, "contenido del guardado anterior válido");

            var h2 = NewHost();
            Assert.AreEqual(0, SessionPersistence.RestoreInto(res.Save, h2).Count, "restaurable");
        }

        // ---- S09 (§8.9) Detectar partidas corruptas y NO pisar la copia válida
        [Test]
        public void S09_Corrupcion_DetectadaYNoSobrescribeLaCopiaValida()
        {
            var p = NewPiece(vbase: 60);
            var good = SessionPersistence.Capture(_host, null, _poses);
            Assert.IsTrue(SaveRepository.SafeWrite(_path, good).Ok, "guardado válido inicial");

            // (a) DTO inválido: el repositorio se niega a escribir y el archivo queda intacto.
            var bad = SessionPersistence.Capture(_host, good.SaveId, _poses);
            bad.Money = -5;
            var wBad = SaveRepository.SafeWrite(_path, bad);
            Assert.IsFalse(wBad.Ok, "escritura rechazada");
            Assert.IsTrue(wBad.Problems.Contains("dinero_negativo"));
            var after = SaveRepository.Load(_path);
            Assert.IsNotNull(after.Save, "la última copia válida sigue ahí");
            Assert.IsFalse(after.FromBackup, "ni siquiera hizo falta el respaldo");
            Assert.AreEqual(0, after.Save.Money);

            // (b) Validador: referencias y estados corruptos se detectan uno por uno.
            var t1 = SessionPersistence.Capture(_host, good.SaveId, _poses);
            t1.Pieces.Add(t1.Pieces[0].Clone()); // uid duplicado
            Assert.IsTrue(SaveValidator.ValidateFull(t1).Contains("uid_duplicado_o_nulo:" + p.Uid));

            var t2 = SessionPersistence.Capture(_host, good.SaveId, _poses);
            t2.Pieces[0].Terminal = "vendido"; // terminal con almacenamiento
            Assert.IsTrue(SaveValidator.ValidateFull(t2).Contains("terminal_con_almacenamiento:" + p.Uid));

            var t3 = SessionPersistence.Capture(_host, good.SaveId, _poses);
            t3.PiecePoses[0].Uid = "p_999"; // pose que no existe
            var probs3 = SaveValidator.ValidateFull(t3);
            Assert.IsTrue(probs3.Contains("pose_uid_inexistente:p_999"));
            Assert.IsTrue(probs3.Contains("world_sin_pose:" + p.Uid), "y la pieza world queda sin pose");

            var t4 = SessionPersistence.Capture(_host, good.SaveId, _poses);
            t4.Yard.Add(new YardObject { DomainId = "lava_1", BlueprintId = "inexistente", Components = new List<YardComponent>() });
            Assert.IsTrue(SaveValidator.ValidateFull(t4).Contains("yard_blueprint_desconocido:inexistente"));

            var t5 = SessionPersistence.Capture(_host, good.SaveId, _poses);
            t5.SaveId = null;
            Assert.IsTrue(SaveValidator.ValidateFull(t5).Contains("saveid_vacio"));

            // (c) GDD §16.5: huella de catálogo manipulada ⇒ exige migración explícita.
            int netId = _host.SpawnInstance("microondas");
            var t6 = SessionPersistence.Capture(_host, good.SaveId, _poses);
            Assert.AreEqual(1, t6.Yard.Count);
            t6.Yard[0].BpFingerprint += 1;
            Assert.IsTrue(SaveValidator.ValidateFull(t6).Contains("blueprint_incompatible:microondas_" + netId));
        }

        // ---- S10 (§8.10) Migrar formatos anteriores (v3 → v4 explícito)
        [Test]
        public void S10_MigracionV3A4_ExplicitaYConservadora()
        {
            // Estado con pieza almacenada + pieza en mundo (la v3 descartaba, PROV-11).
            var guardada = NewPiece(defId: "puerta");
            Carry(0UL, guardada.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Estanteria, StationPos).Ok);
            var mundana = NewPiece(defId: "plato");
            _host.State.Money = 250;

            var v3 = SaveSerializer.Serialize(_host.State); // serializer de paridad (prototipo)
            Assert.AreEqual(GameInfo.SaveVersion, v3.Version, "v3 del prototipo");

            var v4 = SaveMigrations.Migrate3To4(v3);
            Assert.IsNotNull(v4, "migración explícita");
            Assert.AreEqual(GameInfo.SaveVersionFull, v4.Version);
            Assert.IsNotNull(v4.SaveId, "id nuevo asignado");
            Assert.AreEqual(250, v4.Money, "dinero conservado");
            Assert.AreEqual(0, v4.Yard.Count, "v3 no persistía patio");
            Assert.AreEqual(0, v4.PiecePoses.Count, "v3 no persistía físicos");
            Assert.AreEqual(0, SaveValidator.ValidateFull(v4).Count, "v4 migrado válido: " +
                string.Join(";", SaveValidator.ValidateFull(v4).ToArray()));

            var h2 = NewHost();
            Assert.AreEqual(0, SessionPersistence.RestoreInto(v4, h2).Count, "restauración del migrado");
            Assert.AreEqual(250, h2.State.Money);
            Assert.IsNotNull(PieceRules.GetPiece(h2.State.Reg, guardada.Uid), "almacenada conservada");
            Assert.IsNull(PieceRules.GetPiece(h2.State.Reg, mundana.Uid),
                "la pieza en mundo de la v3 ya había sido descartada por ese formato (documentado)");

            // Y a través del repositorio: un archivo v3 se carga migrado.
            File.WriteAllText(_path, SaveJsonAdapter.ToJson(v3));
            var res = SaveRepository.Load(_path);
            Assert.IsNotNull(res.Save, "archivo v3 utilizable");
            Assert.IsTrue(res.Migrated, "marcado como migrado");
            Assert.AreEqual(GameInfo.SaveVersionFull, res.Save.Version);

            // Versión desconocida: ni se inventa ni se carga.
            var futuro = SaveMigrations.MigrateToLatest(new SaveGame { Version = 99 });
            Assert.IsNull(futuro, "versión desconocida rechazada");
        }

        // ---- S11 (§8.11) Sync del estado restaurado para incorporación tardía
        [Test]
        public void S11_LateJoin_EstadoRestauradoIdenticoAlOriginal()
        {
            int netId = _host.SpawnInstance("microondas");
            _host.SelectTool(0UL, "destornillador");
            _host.StartWork(0UL, netId, 0, 0, ObjPos);
            for (int i = 0; i < 6; i++) _host.WorkTick(0UL, netId, 0, 0, 0.25, ObjPos);
            _host.Damage(0UL, netId, 1, 20, ObjPos);

            var vendida = NewPiece(vbase: 100);
            Carry(0UL, vendida.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Balanza, StationPos).Ok);
            var material = NewPiece(recipe: new[] { RecipeEntry.OfMat("MAT-01", 4) });
            Carry(0UL, material.Uid);
            Assert.IsTrue(_sc.UseStation(0UL, Trituradora, StationPos).Ok);

            var antes = _sc.Snapshot();               // lo que un late-join veía ANTES de guardar
            var compsAntes = _host.Snapshot(netId);   // réplica cliente del objeto (NetworkList)

            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);
            var h2 = RestoreNewHost();
            var sc2 = new ScrapyardSession(h2);

            // El late-join recibe esto por las NVs/NetworkLists existentes (SyncEconomy
            // copia Snapshot() a la red; acá se verifica la fuente autoritativa).
            Assert.AreEqual(antes, sc2.Snapshot(), "snapshot económico idéntico tras restaurar");

            int netId2 = h2.AllInstances()[0].NetId;
            var compsDespues = h2.Snapshot(netId2);
            Assert.AreEqual(compsAntes.Length, compsDespues.Length, "mismos componentes");
            for (int i = 0; i < compsAntes.Length; i++)
                Assert.AreEqual(compsAntes[i], compsDespues[i], "CompSync[" + i + "] idéntico (estados, calidad, fijaciones)");
        }

        // ---- S12 (§8.2/§3) La captura NO muta el estado vivo
        [Test]
        public void S12_Captura_NoMutaElEstadoVivo()
        {
            var p = NewPiece(vbase: 45);
            Carry(0UL, p.Uid);
            int netId = _host.SpawnInstance("microondas");

            var save = SessionPersistence.Capture(_host, null, _poses);

            // En vivo la pieza SIGUE transportada (la política carried→world aplica
            // solo al archivo, nunca al estado vivo): sin mutaciones sorpresa.
            Assert.AreEqual("carried", PieceRules.GetPiece(_host.State.Reg, p.Uid).Storage, "vivo intacto");
            Assert.AreEqual(p.Uid, _sc.CarriedUid(0UL), "sigue en sus manos");
            Assert.AreEqual(1, _host.AllInstances().Count, "instancia intacta");

            // En el archivo, la política determinista la registra en el suelo.
            StoredPart clon = null;
            foreach (var q in save.Pieces) if (q.Uid == p.Uid) clon = q;
            Assert.IsNotNull(clon);
            Assert.AreEqual("world", clon.Storage, "save: carried → world (GDD §16.4)");
            Assert.AreEqual(1, save.PiecePoses.Count, "pose incluida");
            Assert.AreEqual(ScrapyardSession.PlayerTag(0UL), save.PiecePoses[0].CarriedBy,
                "trazabilidad de quién la llevaba");
            _ = netId;
        }

        // ---- S13 (§3) Pieza transportada al guardar: restauración en el suelo
        [Test]
        public void S13_PoliticaDeTransporte_RestauraEnElSueloConTrazabilidad()
        {
            var p = NewPiece(vbase: 65);
            Carry(1UL, p.Uid);
            _poses.Pieces[p.Uid] = new SavePose(11, 0.8, 11, 45); // pose del portador

            var save = SessionPersistence.Capture(_host, null, _poses);
            Assert.AreEqual("c1", save.PiecePoses[0].CarriedBy, "quién la transportaba");
            Assert.IsTrue(SaveRepository.SafeWrite(_path, save).Ok);

            var h2 = RestoreNewHost();
            var q = PieceRules.GetPiece(h2.State.Reg, p.Uid);
            Assert.AreEqual("world", q.Storage, "aparece en el suelo (cancelación sin penalización)");
            Assert.AreEqual(65, q.VBase, "identidad completa conservada");
            Assert.AreEqual(0, h2.Ctx.CarryByPlayer.Count, "manos vacías en la sesión nueva");
            // Y cualquiera puede levantarla de nuevo.
            var sc2 = new ScrapyardSession(h2);
            Assert.IsTrue(sc2.OnPhysicalPickup(0UL, p.Uid).Ok, "re-levantable por otro jugador");
        }

        // ---- S14 (§6) Contadores de identidad: nada colisiona tras cargar
        [Test]
        public void S14_Contadores_SinColisionDeIdentidades()
        {
            _host.SpawnInstance("microondas");
            var p = NewPiece(); // p_1
            _poses.Pieces[p.Uid] = new SavePose(2, 0.2, 2, 0);

            Assert.IsTrue(SaveRepository.SafeWrite(_path, SessionPersistence.Capture(_host, null, _poses)).Ok);
            var h2 = RestoreNewHost();

            // Nueva instancia ⇒ microondas_2 (no reutiliza _1 del patio restaurado).
            int netId = h2.SpawnInstance("microondas");
            Assert.AreEqual("microondas_2", h2.GetInstance(netId).DomainId, "siguiente instancia");

            // Nueva pieza ⇒ p_2 (UidSeq restaurado desde los uids persistidos).
            var r = EconomyOps.ExtractPiece(h2.Ctx, new PieceCreateOptions
            {
                DefId = "puerta", DefName = "puerta", OriginObjectId = "microondas_2",
                Quality = 100, Kg = 1, WeightClass = "P1", VBase = 14,
                Owner = h2.Ctx.SessionOwner, Recipe = new[] { RecipeEntry.OfMat("MAT-05", 2) },
            });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual("p_2", r.Piece.Uid, "siguiente uid sin colisión");
            Assert.AreEqual(2, h2.State.Reg.Pieces.Count);
        }
    }
}
