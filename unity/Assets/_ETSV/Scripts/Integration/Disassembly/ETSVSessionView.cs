// [ETSV] Integración — NetworkBehaviour de sesión (objeto invisible spawned por
// el host). Canales autoritativos globales:
//  1. Selección de herramienta (registro del host; el cliente nunca la declara).
//  2. Uso de estaciones (balanza/trituradora/residuos/almacenes/contenedores):
//     el cliente pide StationUseRpc(id); el host valida (identidad por
//     RpcParams.Receive.SenderClientId, distancia, estado, reservas, propiedad)
//     y ejecuta la operación atómica del dominio vía ScrapyardSession.
//  3. Estado económico mínimo sincronizado (Instrucción 07 §8): saldo compartido
//     + ocupación de zonas + unidades por material. Las FRACCIONES de material y
//     el registro de piezas viven solo en el host (reconstruibles/derivables);
//     un jugador tardío recibe estos valores con el spawn del NetworkObject.
//  4. Feed de transacciones (RPC discreto a todos) para el HUD: resultado y
//     motivo de rechazo codificado (ReasonCodes).

using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    public class ETSVSessionView : NetworkBehaviour
    {
        public static ETSVSessionView Instance { get; private set; }

        /// <summary>Evento local del feed de transacciones (HUD).</summary>
        public static event System.Action<FeedEntry> OnFeed;

        public class FeedEntry
        {
            public ulong PlayerId;
            public byte Kind;        // TxKind
            public int Value;        // dinero/unidades
            public bool Ok;
            public byte ReasonCode;  // ReasonCodes
            public byte StationId;
        }

        // Códigos estables de operación para el feed.
        public static class TxKind
        {
            public const byte Venta = 0, Procesado = 1, Entrega = 2, Almacenado = 3,
                              Retirado = 4, VentaMaterial = 5, Rechazo = 6,
                              Guardado = 7, Restaurado = 8; // Instrucción 08
        }

        /// <summary>Saldo compartido de la chatarrería (GDD: economía cooperativa).</summary>
        public NetworkVariable<int> Money = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Ocupación de zonas: [estanteria, pale, suelo].</summary>
        public NetworkList<byte> ZoneCounts = new NetworkList<byte>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        /// <summary>Unidades enteras por material (orden EconomyData.MatIds).</summary>
        public NetworkList<ushort> MaterialUnits = new NetworkList<ushort>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                if (ZoneCounts.Count == 0)
                    for (int i = 0; i < 3; i++) ZoneCounts.Add(0);
                if (MaterialUnits.Count == 0)
                    for (int i = 0; i < EconomyData.MatIds.Length; i++) MaterialUnits.Add(0);
                SyncEconomy();
            }
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

        /// <summary>Uso de estación: el host decide TODO (Instrucción 07 §4–§8).</summary>
        [Rpc(SendTo.Server)]
        public void StationUseRpc(byte stationId, RpcParams rpcParams = default)
        {
            var sc = ETSVSession.Scrapyard;
            if (sc == null) return;
            ulong sender = rpcParams.Receive.SenderClientId;

            var def = StationsCatalog.ById(stationId);
            if (def == null) return;

            // Pieza en manos ANTES de la operación (para despawn/spawn posterior).
            string carriedUid = sc.CarriedUid(sender);
            Vector3 spos = ScrapyardLayout.StationPos(stationId);
            var r = sc.UseStation(sender, stationId, new P3(spos.x, spos.y, spos.z));

            if (r.Ok)
            {
                switch (def.Kind)
                {
                    case StationKind.Balanza:
                    case StationKind.Trituradora:
                    case StationKind.ResiduosEspeciales:
                    case StationKind.Estanteria:
                    case StationKind.Pale:
                    case StationKind.Suelo:
                        // La pieza fue consumida/almacenada ⇒ el físico sale del mundo.
                        if (carriedUid != null) DespawnPiece(carriedUid);
                        break;
                }
                if (def.Kind == StationKind.Estanteria || def.Kind == StationKind.Pale
                    || def.Kind == StationKind.Suelo)
                {
                    // Retiro (manos libres): la pieza vuelve al mundo físico y a las
                    // manos del jugador (HeldBy server-write; el watcher de la pieza
                    // reconoce el estado ya consistente y no lo pisa).
                    if (carriedUid == null && r.Piece != null)
                    {
                        var pv = PieceSpawner.Spawn(r.Piece, spos + Vector3.up * 0.4f);
                        if (pv != null)
                        {
                            var pickup = pv.GetComponent<PickupItem>();
                            if (pickup != null) pickup.HeldBy.Value = sender;
                        }
                    }
                }
            }

            SyncEconomy();
            BroadcastFeed(sender, def, r, carriedUid != null);
        }

        private void DespawnPiece(string uid)
        {
            int n = DisassemblySessionHost.PieceUidToNet(uid);
            DisassemblyPieceView pv;
            if (n >= 0 && DisassemblyPieceView.Registry.TryGetValue(n, out pv) && pv != null)
            {
                var no = pv.NetworkObject;
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
        }

        /// <summary>Copia el snapshot económico del host a las variables de red
        /// (solo tras operaciones: sincronización por eventos, no continua).</summary>
        public void SyncEconomy()
        {
            if (!IsServer || ETSVSession.Scrapyard == null) return;
            var s = ETSVSession.Scrapyard.Snapshot();
            Money.Value = s.Money;
            byte[] zones = { s.EstanteriaUsed, s.PaleUsed, s.SueloUsed };
            for (int i = 0; i < zones.Length && i < ZoneCounts.Count; i++)
                if (ZoneCounts[i] != zones[i]) ZoneCounts[i] = zones[i];
            for (int i = 0; i < s.MaterialUnits.Length && i < MaterialUnits.Count; i++)
                if (MaterialUnits[i] != s.MaterialUnits[i]) MaterialUnits[i] = s.MaterialUnits[i];
        }

        private void BroadcastFeed(ulong sender, StationDef def, OpResult r, bool wasCarrying)
        {
            byte kind = KindFor(def.Kind, r, wasCarrying);
            int value = r.Ok ? ValueFor(def.Kind, r) : 0;
            byte reason = ReasonCodes.Encode(r.Reason);
            TransactionFeedRpc(sender, kind, value, (byte)(r.Ok ? 1 : 0), reason, (byte)def.Id);
        }

        private static byte KindFor(StationKind k, OpResult r, bool wasCarrying)
        {
            if (!r.Ok) return TxKind.Rechazo;
            switch (k)
            {
                case StationKind.Balanza: return TxKind.Venta;
                case StationKind.Trituradora: return TxKind.Procesado;
                case StationKind.ResiduosEspeciales: return TxKind.Entrega;
                case StationKind.ContenedorMaterial: return TxKind.VentaMaterial;
                case StationKind.Estanteria:
                case StationKind.Pale:
                case StationKind.Suelo:
                    return wasCarrying ? TxKind.Almacenado : TxKind.Retirado;
                default: return TxKind.Rechazo;
            }
        }

        private static int ValueFor(StationKind k, OpResult r)
        {
            switch (k)
            {
                case StationKind.Balanza: return r.Value;
                case StationKind.ResiduosEspeciales: return -r.Cost;
                case StationKind.ContenedorMaterial: return r.Delta;
                default: return 0;
            }
        }

        [Rpc(SendTo.Everyone)]
        private void TransactionFeedRpc(ulong playerId, byte kind, int value, byte ok,
            byte reasonCode, byte stationId, RpcParams rpcParams = default)
        {
            var h = OnFeed;
            if (h != null)
                h(new FeedEntry
                {
                    PlayerId = playerId,
                    Kind = kind,
                    Value = value,
                    Ok = ok != 0,
                    ReasonCode = reasonCode,
                    StationId = stationId,
                });
        }

        // ------------------------------------------------------------------ persistencia (Instrucción 08)
        /// <summary>Un cliente SOLICITA guardar; el host decide y ejecuta (§5: un
        /// cliente jamás reemplaza estado del anfitrión, solo pide la captura).</summary>
        [Rpc(SendTo.Server)]
        public void RequestSaveRpc(RpcParams rpcParams = default)
        {
            if (PersistenceHub.Instance == null) return;
            PersistenceHub.Instance.SaveNow(rpcParams.Receive.SenderClientId, true);
        }

        /// <summary>Feedback de guardado/restauración para todos los clientes.</summary>
        public void FeedSaveResult(ulong playerId, bool ok)
        {
            TransactionFeedRpc(playerId, TxKind.Guardado, 0, (byte)(ok ? 1 : 0),
                ok ? (byte)0 : ReasonCodes.Encode("guardado_fallido"), 0);
        }

        public void FeedRestored()
        {
            TransactionFeedRpc(NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                    ? NetworkManager.Singleton.LocalClient.ClientId : 0UL,
                TxKind.Restaurado, 0, 1, 0, 0);
        }
    }
}
