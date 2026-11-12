// [ETSV] Integración — NetworkBehaviour de sesión (objeto invisible spawned por
// el host). Canal autoritativo para estado global ligero: hoy, la selección de
// herramienta de cada jugador (registro del host; el cliente NUNCA declara su
// herramienta en las solicitudes de trabajo — Instrucción 06 §5).

using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    public class ETSVSessionView : NetworkBehaviour
    {
        /// <summary>Instancia local (todas las peers la tienen cuando el objeto replica).</summary>
        public static ETSVSessionView Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>El cliente pide cambiar su herramienta; el host valida y registra.</summary>
        [Rpc(SendTo.Server)]
        public void SelectToolRpc(byte toolIdx, RpcParams rpcParams = default)
        {
            var host = ETSVSession.Host;
            if (host == null) return;
            if (toolIdx >= GameCatalog.Tools.Length) return;
            host.SelectTool(rpcParams.Receive.SenderClientId, GameCatalog.Tools[toolIdx].Id);
        }
    }
}
