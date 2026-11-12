// [ETSV] Integración — NetworkBehaviour del objeto desmontable (cáscara delgada).
// TODA la lógica vive en ETSV.Domain y en DisassemblySessionHost (capa Sim, C#
// puro). Esta clase solo: (1) construye visuales genéricos desde el blueprint,
// (2) expone RPCs [Rpc(SendTo.Server)] que el host valida y ejecuta, (3) mantiene
// una NetworkList<CompSync> compacta (8 bytes/componente) que replica a todos —
// incluido el que se incorpora tarde: NGO envía el estado inicial con el spawn.
//
// Autoridad (Instrucción 06 §5): el cliente solicita, NUNCA decide. La posición
// del jugador y la herramienta equipada las lee el HOST de sus propios registros.

using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    public class DisassemblyObjectView : NetworkBehaviour
    {
        // Id de instancia del host (DisassemblySessionHost.SpawnInstance) e índice
        // de blueprint en CatalogIndex. Los fija el host ANTES de Spawn() ⇒ viajan
        // como estado inicial a todos los clientes.
        public NetworkVariable<int> NetInstanceId = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<byte> BlueprintIdx = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Estado sincronizable mínimo por componente (no derivable: fuente host).</summary>
        public NetworkList<CompSync> Comps = new NetworkList<CompSync>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        private ObjectBlueprint _bp;
        private ObjectVisuals _vis;

        public ObjectBlueprint Blueprint => _bp;

        public override void OnNetworkSpawn()
        {
            _bp = CatalogIndex.BlueprintAt(BlueprintIdx.Value);
            if (_bp == null)
            {
                Debug.LogError("[ETSV] DisassemblyObjectView: BlueprintIdx inválido " + BlueprintIdx.Value);
                return;
            }
            _vis = BlueprintBuilder.Build(_bp, transform);

            if (IsServer)
            {
                // El host llena la lista ANTES/de/spawn (bootstrap ya creó la instancia
                // de dominio); si llega acá vacía, la pobla desde el snapshot.
                if (Comps.Count == 0 && ETSVSession.Host != null)
                {
                    var snap = ETSVSession.Host.Snapshot(NetInstanceId.Value);
                    if (snap != null)
                        for (int i = 0; i < snap.Length; i++) Comps.Add(snap[i]);
                }
            }

            Comps.OnListChanged += OnCompsChanged;
            ApplyVisuals();
        }

        public override void OnNetworkDespawn()
        {
            Comps.OnListChanged -= OnCompsChanged;
        }

        private void OnCompsChanged(NetworkListEvent<CompSync> _) => ApplyVisuals();

        // ------------------------------------------------------------------ RPCs (cliente → host)
        [Rpc(SendTo.Server)]
        public void WorkStartRpc(byte compIdx, byte fIdx, RpcParams rpcParams = default)
        {
            var host = ETSVSession.Host;
            if (host == null) return;
            var r = host.StartWork(rpcParams.Receive.SenderClientId, NetInstanceId.Value, compIdx, fIdx, Pos3());
            if (r.Ok) Resync();
        }

        [Rpc(SendTo.Server)]
        public void WorkTickRpc(byte compIdx, byte fIdx, float dt, RpcParams rpcParams = default)
        {
            var host = ETSVSession.Host;
            if (host == null) return;
            var r = host.WorkTick(rpcParams.Receive.SenderClientId, NetInstanceId.Value, compIdx, fIdx, dt, Pos3());
            if (r.Ok) Resync(); // event-driven: solo cambia algo cuando el host aplicó trabajo
        }

        [Rpc(SendTo.Server)]
        public void WorkStopRpc(byte compIdx, byte fIdx, RpcParams rpcParams = default)
        {
            var host = ETSVSession.Host;
            if (host == null) return;
            host.StopWork(rpcParams.Receive.SenderClientId, NetInstanceId.Value, compIdx, fIdx);
        }

        [Rpc(SendTo.Server)]
        public void ExtractRpc(byte compIdx, RpcParams rpcParams = default)
        {
            var host = ETSVSession.Host;
            if (host == null) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            var r = host.Extract(sender, NetInstanceId.Value, compIdx, Pos3());
            if (!r.Ok)
            {
                // Solicitud simultánea/duplicada: el dominio ya la rechazó ⇒ no hay
                // duplicación posible. El HUD del cliente deriva la pista del estado
                // sincronizado (el componente ya no está Liberado), sin RPC extra.
                return;
            }
            Resync();
            SpawnPiece(r.Piece, compIdx);
        }

        [Rpc(SendTo.Server)]
        public void DamageRpc(byte compIdx, byte amount, RpcParams rpcParams = default)
        {
            var host = ETSVSession.Host;
            if (host == null) return;
            var r = host.Damage(rpcParams.Receive.SenderClientId, NetInstanceId.Value, compIdx, amount, Pos3());
            if (r.Ok) Resync();
        }

        // ------------------------------------------------------------------ host: spawn de pieza
        private void SpawnPiece(StoredPart piece, int compIdx)
        {
            Vector3 at = transform.position;
            if (_vis != null && compIdx < _vis.ComponentGOs.Length && _vis.ComponentGOs[compIdx] != null)
                at = _vis.ComponentGOs[compIdx].transform.position + Vector3.up * 0.15f;
            PieceSpawner.Spawn(piece, at); // identidad en NVs antes de Spawn() ⇒ late-join correcto
        }

        /// <summary>Refleja el snapshot del dominio en la NetworkList (solo host).</summary>
        private void Resync()
        {
            if (!IsServer || ETSVSession.Host == null) return;
            var snap = ETSVSession.Host.Snapshot(NetInstanceId.Value);
            if (snap == null) return;
            for (int i = 0; i < snap.Length; i++)
            {
                if (i < Comps.Count)
                {
                    if (!Comps[i].Equals(snap[i])) Comps[i] = snap[i]; // setter ⇒ dirty ⇒ delta
                }
                else Comps.Add(snap[i]);
            }
        }

        private P3 Pos3()
        {
            var p = transform.position;
            return new P3(p.x, p.y, p.z);
        }

        // ------------------------------------------------------------------ visuales (todos)
        /// <summary>Aplica el estado sincronizado a los GameObjects. Derivable puro:
        /// no viaja nada más por la red (Instrucción 06 §6).</summary>
        public void ApplyVisuals()
        {
            if (_vis == null) return;
            for (int ci = 0; ci < _vis.ComponentGOs.Length && ci < Comps.Count; ci++)
            {
                var s = Comps[ci];
                var state = (CompState)s.State;
                var go = _vis.ComponentGOs[ci];

                bool gone = DisassemblyStates.IsUnlock(state); // extraido/procesado/vendido
                go.SetActive(!gone);
                if (gone) continue;

                // Color por estado: bloqueado oscurecido, accesible base, liberado verde.
                Color c = _vis.BaseColors[ci];
                if (state == CompState.Bloqueado) c *= 0.45f;
                else if (state == CompState.Liberado) c = Color.Lerp(c, Color.green, 0.55f);
                SetColor(_vis.ComponentRenderers[ci], c);

                var fs = _vis.FastenerGOs[ci];
                for (int fi = 0; fi < fs.Length; fi++)
                {
                    ulong field = (s.FastenerBits >> (fi * 8)) & 0xFF;
                    bool removed = (field & 0x80) != 0;
                    int q = (int)(field & 0x7F);
                    fs[fi].SetActive(!removed);
                    if (!removed && _vis.FastenerRenderers[ci][fi] != null)
                    {
                        // progreso: rojo → amarillo → verde claro
                        SetColor(_vis.FastenerRenderers[ci][fi],
                            Color.Lerp(new Color(0.85f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.35f), q / 127f));
                    }
                }
            }
        }

        private static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            if (r.material == null) return;
            r.material.color = c;
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c);
        }

        /// <summary>Estado sincronizado de un componente (lectura cliente para HUD/interacción).</summary>
        public bool TryGetComp(int compIdx, out CompSync s)
        {
            s = default(CompSync);
            if (compIdx < 0 || compIdx >= Comps.Count) return false;
            s = Comps[compIdx];
            return true;
        }
    }
}
