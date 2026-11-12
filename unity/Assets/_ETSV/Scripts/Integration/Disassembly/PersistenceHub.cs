// [ETSV] Integración — PERSISTENCIA EN UNITY (Instrucción 08), solo HOST.
// Cáscara delgada sobre la capa Sim (SessionPersistence + SaveRepository, C# puro
// y testeado): este MonoBehaviour aporta únicamente lo que requiere UnityEngine —
// la ruta (Application.persistentDataPath), las poses físicas reales (transforms
// de los NetworkObjects vivos) y los disparadores (arranque, autoguardado
// periódico, desconexión, cierre, solicitud manual por RPC).
//
// Autoridad cooperativa (§5): SOLO el host guarda/restaura. Los clientes reciben
// el estado por la infraestructura existente (NetworkVariables/NetworkLists del
// SessionView + spawn de NetworkObjects): un cliente jamás reemplaza estado
// económico; como mucho SOLICITA un guardado (RequestSaveRpc) y el host decide.
//
// Reconstrucción física (§3): al cargar se re-materializan ÚNICAMENTE los objetos
// del patio y las piezas no terminales en "world" (con su pose). Una pieza
// almacenada/vendida/procesada/entregada NO reaparece: su estado vive solo en el
// dominio y los almacenes. Política de transporte: las piezas carried al guardar
// se restauran en el suelo (cancelación sin penalización, GDD §16.4).

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    public class PersistenceHub : MonoBehaviour
    {
        public static PersistenceHub Instance { get; private set; }

        /// <summary>Autoguardado periódico (GDD §16.2). Intervalo elegido: 60 s —
        /// decisión documentada en PERSISTENCIA.md (no es un valor del GDD).</summary>
        public const float AutoSaveInterval = 60f;

        public string SavePath { get; private set; }
        public string SaveId { get; private set; }
        public bool Restored { get; private set; }
        public float TimeSinceLastSave { get { return _autoTimer; } }

        private float _autoTimer;
        private bool _enabled;

        // ---------------------------------------------------------------- ciclo de vida
        public void InitHost(string savePath)
        {
            Instance = this;
            SavePath = savePath;
            _autoTimer = 0f;
            _enabled = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_enabled) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            _autoTimer += Time.deltaTime;
            if (_autoTimer >= AutoSaveInterval) SaveNow(nm.LocalClient != null ? nm.LocalClient.ClientId : 0UL, false);
        }

        private void OnApplicationQuit()
        {
            // GDD §16.2: guardar al cerrar la sesión.
            if (_enabled && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SaveNow(0UL, false);
        }

        // ---------------------------------------------------------------- restauración (al iniciar el host)
        /// <summary>Intenta cargar la partida. Devuelve true si se restauró (el
        /// bootstrap entonces NO spawnea el microondas de partida nueva). Los
        /// problemas del archivo quedan en el log; un archivo corrupto NUNCA se
        /// sobrescribe en la carga (solo el próximo guardado válido lo reemplaza,
        /// conservando .bak).</summary>
        public bool TryRestore(DisassemblySessionHost host)
        {
            var res = SaveRepository.Load(SavePath);
            if (res.Save == null)
            {
                if (res.Problems.Count > 0 && res.Problems[0] != "archivo_inexistente:" + SavePath)
                    Debug.LogWarning("[ETSV] Save no utilizable (" + string.Join("; ", res.Problems.ToArray()) + ")");
                return false;
            }

            var problems = SessionPersistence.RestoreInto(res.Save, host);
            if (problems.Count > 0)
            {
                Debug.LogError("[ETSV] Restauración rechazada: " + string.Join("; ", problems.ToArray()));
                return false;
            }

            SaveId = res.Save.SaveId;
            Restored = true;
            SpawnPhysicalWorld(host, res.Save);

            string extra = res.FromBackup ? " (recuperada desde la copia de respaldo)" :
                           res.Migrated ? " (migrada desde el formato v3)" : "";
            Debug.Log("[ETSV] Partida restaurada" + extra + ": $" + host.State.Money +
                      ", " + res.Save.Yard.Count + " objeto(s) en el patio, " +
                      res.Save.PiecePoses.Count + " pieza(s) física(s).");
            return true;
        }

        /// <summary>Reconstrucción física: objetos del patio + piezas world.
        /// Las NetworkVariables de identidad se fijan ANTES de Spawn() ⇒ los
        /// clientes (incluido el late-join) reciben el mundo restaurado por la
        /// infraestructura existente, sin protocolo nuevo.</summary>
        private void SpawnPhysicalWorld(DisassemblySessionHost host, SaveGame save)
        {
            // Objetos del patio (blueprints parcialmente desmontados).
            foreach (var si in host.AllInstances())
            {
                YardObject y = null;
                foreach (var cand in save.Yard) if (cand.DomainId == si.DomainId) { y = cand; break; }
                if (y == null || ETSVBootstrap.ObjectPrefab == null) continue;

                var go = Instantiate(ETSVBootstrap.ObjectPrefab,
                    new Vector3((float)y.X, (float)y.Y, (float)y.Z),
                    Quaternion.Euler(0f, (float)y.Yaw, 0f));
                var view = go.GetComponent<DisassemblyObjectView>();
                view.NetInstanceId.Value = si.NetId;
                view.BlueprintIdx.Value = (byte)CatalogIndex.BlueprintIndexOf(si.Bp.Id);
                go.GetComponent<NetworkObject>().Spawn();
                // OnNetworkSpawn del view pobla Comps desde host.Snapshot(netId).
            }

            // Piezas físicas: SOLO no terminales en "world" (§3). Almacenadas,
            // vendidas, procesadas y entregadas no reaparecen en el patio.
            foreach (var pose in save.PiecePoses)
            {
                var piece = PieceRules.GetPiece(host.State.Reg, pose.Uid);
                if (piece == null || piece.Terminal != null || piece.Storage != "world") continue;
                PieceSpawner.Spawn(piece,
                    new Vector3((float)pose.X, (float)pose.Y, (float)pose.Z),
                    (float)pose.Yaw);
            }
        }

        // ---------------------------------------------------------------- guardado (solo host)
        /// <summary>Captura + escritura segura + feedback al solicitante.
        /// Devuelve true si el archivo quedó escrito.</summary>
        public bool SaveNow(ulong requesterId, bool announce)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || ETSVSession.Host == null) return false;
            _autoTimer = 0f;

            var save = SessionPersistence.Capture(ETSVSession.Host, SaveId, new UnityPoseSource());
            if (save == null) return false;
            SaveId = save.SaveId; // estable entre guardados de la misma partida

            var res = SaveRepository.SafeWrite(SavePath, save);
            if (!res.Ok)
                Debug.LogError("[ETSV] Guardado rechazado (la última copia válida quedó intacta): " +
                    string.Join("; ", res.Problems.ToArray()));

            if (announce && ETSVSessionView.Instance != null)
                ETSVSessionView.Instance.FeedSaveResult(requesterId, res.Ok);
            return res.Ok;
        }

        // ---------------------------------------------------------------- poses físicas reales
        /// <summary>Lee los transforms de los NetworkObjects vivos (solo host).
        /// Si un objeto lógico no tiene físico visible (caso excepcional: pieza
        /// sin spawnear), la captura usa la pose de respaldo determinista de Sim.</summary>
        private sealed class UnityPoseSource : IPoseSource
        {
            public bool ObjectPose(string domainId, out SavePose pose)
            {
                pose = default(SavePose);
                var host = ETSVSession.Host;
                if (host == null) return false;
                foreach (var kv in DisassemblyObjectView.Registry)
                {
                    var si = host.GetInstance(kv.Key);
                    if (si == null || si.DomainId != domainId || kv.Value == null) continue;
                    var t = kv.Value.transform;
                    pose = new SavePose(t.position.x, t.position.y, t.position.z, t.eulerAngles.y);
                    return true;
                }
                return false;
            }

            public bool PiecePose(string uid, out SavePose pose)
            {
                pose = default(SavePose);
                int n = DisassemblySessionHost.PieceUidToNet(uid);
                DisassemblyPieceView pv;
                if (n < 0 || !DisassemblyPieceView.Registry.TryGetValue(n, out pv) || pv == null) return false;
                var t = pv.transform;
                pose = new SavePose(t.position.x, t.position.y, t.position.z, t.eulerAngles.y);
                return true;
            }
        }
    }
}
