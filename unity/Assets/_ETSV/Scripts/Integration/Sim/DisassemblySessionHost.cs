// [ETSV] Capa Sim (C# PURO, assembly ETSV.Sim) — CEREBRO DEL HOST.
// Único punto donde las solicitudes de red se convierten en operaciones del
// dominio (ETSV.Domain, sin modificar). Los NetworkBehaviour de Unity son
// cáscaras delgadas que reenvían RPCs acá; el dominio no sabe nada de red.
//
// Decisiones de integración registradas (NO son cambios al dominio):
// - Owner = "P1" en todas las piezas de esta etapa: el GDD es cooperativo y el
//   dominio actual solo reconoce "P1" (comentario original: "futuro: otros
//   jugadores/contratos"). La propiedad por jugador queda como decisión
//   pendiente para cuando el dominio evolucione (Instrucción 06 §11).
// - El transporte físico (PickupItem) NO muta storage del dominio todavía:
//   EconomyContext.Carry es single-slot (prototipo monojugador) y no debe
//   modificarse arbitrariamente; la pieza extraída queda storage="world" hasta
//   la etapa de balanza/almacén. Registrado en docs/INTEGRACION_DESARMADO.md.

using System.Collections.Generic;
using Etsv.Domain;

namespace Etsv.Integration
{
    /// <summary>Una instancia de objeto desmontable viva en la sesión.</summary>
    public class SessionInstance
    {
        public int NetId;              // id numérico para la red
        public string DomainId;        // id del dominio ("microondas_1")
        public ObjectBlueprint Bp;
        public DisassemblyInstance Inst;
    }

    public sealed class DisassemblySessionHost
    {
        // ---- estado de juego (dominio) ----
        public GameStateData State;
        public EconomyContext Ctx;

        // ---- registro de instancias ----
        private readonly Dictionary<int, SessionInstance> _byNet = new Dictionary<int, SessionInstance>();
        private int _nextNetId = 1;
        private int _nextInstance = 1;

        // ---- herramientas autoritativas por cliente ----
        private readonly Dictionary<ulong, string> _tools = new Dictionary<ulong, string>();

        // ---- sesiones de trabajo (anti simultaneidad) ----
        // clave: (netId << 20) | (compIdx << 8) | fIdx  — máx 256 fijaciones/comp, 4096 comps
        private readonly Dictionary<long, WorkSession> _workers = new Dictionary<long, WorkSession>();

        /// <summary>Lo aporta la integración Unity: posición del avatar de un cliente conectado.</summary>
        public delegate bool TryGetPlayerPos(ulong clientId, out P3 pos);
        public TryGetPlayerPos PlayerPos;

        public DisassemblySessionHost()
        {
            State = SaveSerializer.CreateGameState();
            Ctx = new EconomyContext
            {
                Reg = State.Reg,
                Cwh = State.Cwh,
                Mwh = State.Mwh,
                Carry = null,
                GetMoney = () => State.Money,
                AddMoney = n => { State.Money += n; },
                OnChanged = () => { },
            };
        }

        private static long WorkerKey(int netId, int compIdx, int fIdx)
        {
            return ((long)(uint)netId << 20) | ((long)(uint)compIdx << 8) | (uint)fIdx;
        }

        // ---------------------------------------------------------------- instancias
        /// <summary>Crea una instancia de blueprint. Devuelve el NetId (>=1) o -1 si el blueprint no existe.</summary>
        public int SpawnInstance(string blueprintId)
        {
            var bp = GameCatalog.ObjectById(blueprintId);
            if (bp == null) return -1;
            string domainId = blueprintId + "_" + _nextInstance++;
            var inst = DisassemblyRules.CreateInstance(bp, domainId);
            var si = new SessionInstance { NetId = _nextNetId++, DomainId = domainId, Bp = bp, Inst = inst };
            _byNet[si.NetId] = si;
            return si.NetId;
        }

        public SessionInstance GetInstance(int netId)
        {
            SessionInstance si;
            return _byNet.TryGetValue(netId, out si) ? si : null;
        }

        // ---------------------------------------------------------------- herramientas
        public OpResult SelectTool(ulong clientId, string toolId)
        {
            if (GameCatalog.ToolById(toolId) == null) return OpResult.Fail("herramienta_inexistente");
            _tools[clientId] = toolId;
            return OpResult.Success();
        }

        /// <summary>Herramienta autoritativa del host (nunca la declarada por el cliente).</summary>
        public string ToolOf(ulong clientId)
        {
            string t;
            return _tools.TryGetValue(clientId, out t) ? t : "manos";
        }

        // ---------------------------------------------------------------- validación común
        private OpResult Gate(ulong clientId, SessionInstance si, P3 objectPos, out ComponentDef def)
        {
            def = null;
            if (si == null) return OpResult.Fail("instancia_inexistente");
            P3 ppos = default(P3);
            bool known = PlayerPos != null && PlayerPos(clientId, out ppos);
            var presence = DisassemblyGate.ValidatePresence(known, known ? ppos : default(P3), objectPos);
            return presence;
        }

        // ---------------------------------------------------------------- trabajo
        public OpResult StartWork(ulong clientId, int netId, int compIdx, int fIdx, P3 objectPos)
        {
            var si = GetInstance(netId);
            var g = Gate(clientId, si, objectPos, out _);
            if (!g.Ok) return g;
            if (si == null) return OpResult.Fail("instancia_inexistente");
            var def = DisassemblyGate.ValidateCompIdx(si.Bp, compIdx);
            if (def == null) return OpResult.Fail("componente_invalido");
            if (!DisassemblyGate.ValidateFastenerIdx(def, fIdx)) return OpResult.Fail("fijacion_invalida");

            var key = WorkerKey(netId, compIdx, fIdx);
            WorkSession active;
            if (_workers.TryGetValue(key, out active))
            {
                string conflict = DisassemblyGate.ValidateWorker(active, clientId);
                if (conflict != null) return OpResult.Fail(conflict); // otro jugador ya trabaja ahí
            }

            string tool = ToolOf(clientId);
            var check = DisassemblyRules.CanWork(si.Inst, def.Id, fIdx, tool, GameCatalog.FastenerTypes);
            if (!check.Ok) return OpResult.Fail(check.Why); // estado | ya | herramienta

            _workers[key] = new WorkSession { ClientId = clientId, Age = 0 };
            DisassemblyRules.DrainEvents(si.Inst);
            return OpResult.Success();
        }

        public OpResult WorkTick(ulong clientId, int netId, int compIdx, int fIdx, double dt, P3 objectPos)
        {
            var si = GetInstance(netId);
            var g = Gate(clientId, si, objectPos, out _);
            if (!g.Ok) return g;
            if (si == null) return OpResult.Fail("instancia_inexistente");
            var def = DisassemblyGate.ValidateCompIdx(si.Bp, compIdx);
            if (def == null) return OpResult.Fail("componente_invalido");
            if (!DisassemblyGate.ValidateFastenerIdx(def, fIdx)) return OpResult.Fail("fijacion_invalida");

            var key = WorkerKey(netId, compIdx, fIdx);
            WorkSession active;
            if (!_workers.TryGetValue(key, out active)) return OpResult.Fail("sin_sesion_trabajo");
            if (active.ClientId != clientId) return OpResult.Fail("ocupado_por_otro");

            if (dt < 0) dt = 0;
            if (dt > DisassemblyGate.MaxTickDt) dt = DisassemblyGate.MaxTickDt; // tope anti-aceleración

            string tool = ToolOf(clientId);
            var check = DisassemblyRules.CanWork(si.Inst, def.Id, fIdx, tool, GameCatalog.FastenerTypes);
            if (!check.Ok)
            {
                _workers.Remove(key);
                return OpResult.Fail(check.Why);
            }

            var outcome = DisassemblyRules.Work(si.Inst, def.Id, fIdx, dt, check.Time);
            active.Age = 0;
            _workers[key] = active;
            DisassemblyRules.DrainEvents(si.Inst);
            return new OpResult { Ok = true, Applied = outcome == WorkOutcome.Done ? 1 : 0 };
        }

        /// <summary>Interrumpir es siempre idempotente para el propio trabajador.</summary>
        public OpResult StopWork(ulong clientId, int netId, int compIdx, int fIdx)
        {
            var key = WorkerKey(netId, compIdx, fIdx);
            WorkSession active;
            if (_workers.TryGetValue(key, out active))
            {
                if (active.ClientId != clientId) return OpResult.Fail("trabajador_ajeno");
                _workers.Remove(key);
            }
            return OpResult.Success();
        }

        /// <summary>Envejece las sesiones y libera las abandonadas (desconexión, soltar sin Stop).</summary>
        public int PruneStaleWorkers(double dt)
        {
            // Dos pasadas: el índice de Dictionary se invalida al reasignar durante
            // la enumeración, primero se recolecta y después se muta.
            List<KeyValuePair<long, WorkSession>> aged = null;
            foreach (var kv in _workers)
            {
                var w = kv.Value;
                w.Age += dt;
                if (aged == null) aged = new List<KeyValuePair<long, WorkSession>>();
                aged.Add(new KeyValuePair<long, WorkSession>(kv.Key, w));
            }
            if (aged == null) return 0;
            int removed = 0;
            foreach (var kv in aged)
            {
                if (kv.Value.Age > DisassemblyGate.WorkerTimeout) { _workers.Remove(kv.Key); removed++; }
                else _workers[kv.Key] = kv.Value;
            }
            return removed;
        }

        public bool HasWorker(int netId, int compIdx, int fIdx)
        {
            return _workers.ContainsKey(WorkerKey(netId, compIdx, fIdx));
        }

        // ---------------------------------------------------------------- extracción
        public OpResult Extract(ulong clientId, int netId, int compIdx, P3 objectPos)
        {
            var si = GetInstance(netId);
            var g = Gate(clientId, si, objectPos, out _);
            if (!g.Ok) return g;
            if (si == null) return OpResult.Fail("instancia_inexistente");
            var def = DisassemblyGate.ValidateCompIdx(si.Bp, compIdx);
            if (def == null) return OpResult.Fail("componente_invalido");

            // El dominio es la única fuente: si ya fue extraído por otro jugador
            // (solicitud simultánea), CanExtract falla ⇒ imposible duplicar.
            if (!DisassemblyRules.CanExtract(si.Inst, def.Id)) return OpResult.Fail("no_liberado");
            var rt = DisassemblyRules.Extract(si.Inst, def.Id);
            if (rt == null) return OpResult.Fail("no_liberado");

            // Liberar sesiones de trabajo de sus fijaciones
            for (int f = 0; f < (def.Fasteners != null ? def.Fasteners.Length : 0); f++)
                _workers.Remove(WorkerKey(netId, compIdx, f));

            // Pieza del dominio con identidad única (uid "p_N" persistente).
            var created = EconomyOps.ExtractPiece(Ctx, new PieceCreateOptions
            {
                DefId = def.Id,
                DefName = def.Name,
                OriginObjectId = si.DomainId,
                Quality = rt.Quality,
                Kg = def.Kg,
                WeightClass = def.WeightClass,
                VBase = def.VBase,
                Owner = "P1", // cooperativo provisional — ver nota de cabecera
                Sealed = def.Sealed,
                IsProtected = def.IsProtected,
                Recipe = def.Recipe,
            });
            DisassemblyRules.DrainEvents(si.Inst);
            return created;
        }

        // ---------------------------------------------------------------- daño (golpes)
        public OpResult Damage(ulong clientId, int netId, int compIdx, int amount, P3 objectPos)
        {
            var si = GetInstance(netId);
            var g = Gate(clientId, si, objectPos, out _);
            if (!g.Ok) return g;
            if (si == null) return OpResult.Fail("instancia_inexistente");
            var def = DisassemblyGate.ValidateCompIdx(si.Bp, compIdx);
            if (def == null) return OpResult.Fail("componente_invalido");
            int applied = DisassemblyRules.Damage(si.Inst, def.Id, amount);
            DisassemblyRules.DrainEvents(si.Inst);
            return new OpResult
            {
                Ok = true,
                Applied = applied,
                Quality = si.Inst.Comps[def.Id].Quality,
            };
        }

        // ---------------------------------------------------------------- snapshot
        public CompSync[] Snapshot(int netId)
        {
            var si = GetInstance(netId);
            return si == null ? null : DisassemblySync.EncodeInstance(si.Inst);
        }

        // ---------------------------------------------------------------- identidad de piezas
        /// <summary>"p_42" → 42 (uid del dominio ↔ int de red). -1 si no tiene el formato.</summary>
        public static int PieceUidToNet(string uid)
        {
            if (uid == null || !uid.StartsWith("p_")) return -1;
            int n;
            return int.TryParse(uid.Substring(2), out n) ? n : -1;
        }

        public static string NetToPieceUid(int n) { return "p_" + n; }
    }
}
