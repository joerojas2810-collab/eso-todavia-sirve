// [ETSV] Integración — helper del host para materializar piezas del dominio
// como NetworkObjects (extracción de componentes y retiro de almacenes).
// La identidad viaja en NetworkVariables ANTES de Spawn() ⇒ estado inicial
// replicado a todos, incluidos los que se incorporan tarde.

using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    public static class PieceSpawner
    {
        public static DisassemblyPieceView Spawn(StoredPart piece, Vector3 at)
        {
            if (piece == null || ETSVBootstrap.PiecePrefab == null) return null;
            var go = Object.Instantiate(ETSVBootstrap.PiecePrefab, at, Quaternion.identity);
            var pv = go.GetComponent<DisassemblyPieceView>();
            if (pv != null)
            {
                pv.DomainUidN.Value = DisassemblySessionHost.PieceUidToNet(piece.Uid);
                int defIdx = CatalogIndex.PieceDefIndexOf(piece.DefId);
                pv.DefIdx.Value = (byte)(defIdx < 0 ? 0 : defIdx);
                pv.Quality.Value = (byte)piece.Quality;
                pv.OriginBlueprintIdx.Value = BlueprintIdxForOrigin(piece.OriginObjectId);
            }
            go.GetComponent<NetworkObject>().Spawn();
            return pv;
        }

        /// <summary>"microondas_1" → índice del blueprint "microondas" (0 si no existe).</summary>
        public static byte BlueprintIdxForOrigin(string originObjectId)
        {
            if (originObjectId == null) return 0;
            int i = originObjectId.LastIndexOf('_');
            string bpId = i > 0 ? originObjectId.Substring(0, i) : originObjectId;
            int idx = CatalogIndex.BlueprintIndexOf(bpId);
            return (byte)(idx < 0 ? 0 : idx);
        }
    }
}
