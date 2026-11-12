// [ETSV] Integración — pieza física extraída (NetworkBehaviour delgado).
// Identidad: DomainUidN ↔ uid del dominio "p_N" (persistente, Instrucción 06 §9).
// El transporte físico reutiliza PickupItem de la base U6-FPM SIN modificarlo:
// levantar/soltar/lanzar ya están resueltos ahí (HeldBy + PickUpRpc/DropRpc).
//
// Nota de alcance (documentada en docs/INTEGRACION_DESARMADO.md): el dominio
// todavía NO muta storage al llevar la pieza (EconomyContext.Carry es single-slot
// del prototipo monojugador y no se modifica arbitrariamente); la pieza queda
// storage="world" hasta la etapa de balanza/almacén.

using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    [RequireComponent(typeof(PickupItem))]
    public class DisassemblyPieceView : NetworkBehaviour
    {
        /// <summary>uid del dominio: "p_" + DomainUidN (identidad persistente).</summary>
        public NetworkVariable<int> DomainUidN = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Índice en CatalogIndex.PieceDefIds (definición de la pieza).</summary>
        public NetworkVariable<byte> DefIdx = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<byte> Quality = new NetworkVariable<byte>(
            100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Blueprint de origen (para color/tamaño de presentación).</summary>
        public NetworkVariable<byte> OriginBlueprintIdx = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public string DomainUid => DisassemblySessionHost.NetToPieceUid(DomainUidN.Value);
        public string DefId => CatalogIndex.PieceDefAt(DefIdx.Value);

        public override void OnNetworkSpawn()
        {
            // Visual derivado de la definición (todos los peers reconstruyen lo mismo,
            // incluido el que se incorpora tarde: los NetworkVariables llegan con el spawn).
            string defId = DefId;
            var bp = CatalogIndex.BlueprintAt(OriginBlueprintIdx.Value);
            transform.localScale = DisassemblyLayouts.PieceScale(defId);
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var m = BlueprintBuilder.MakeMaterial(
                    DisassemblyLayouts.PieceColor(defId, bp != null ? bp.Id : null));
                rend.sharedMaterial = m;
            }
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                var partDef = defId != null && EconomyData.PartDefs.ContainsKey(defId)
                    ? EconomyData.PartDefs[defId] : null;
                // Peso físico orientativo por clase (presentación/feel, no economía).
                rb.mass = partDef != null ? Mathf.Max(1f, (float)partDef.Kg) : 2f;
            }
            name = "Piece_" + DomainUid + "_" + (defId ?? "def");
        }
    }
}
