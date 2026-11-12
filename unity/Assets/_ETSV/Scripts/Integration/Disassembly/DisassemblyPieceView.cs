// [ETSV] Integración — pieza física extraída (NetworkBehaviour delgado).
// Identidad: DomainUidN ↔ uid del dominio "p_N" (persistente, Instrucción 06 §9).
// El transporte físico reutiliza PickupItem de la base U6-FPM SIN modificarlo.
//
// Instrucción 07 §3 — el físico y el dominio van SIEMPRE de la mano:
//  - El HOST observa HeldBy (server-write) y refleja cada cambio en
//    ScrapyardSession (PickupPiece/DropPiece por jugador). Si el dominio rechaza
//    (manos ocupadas, pieza ajena, terminal…), el físico se fuerza a soltar:
//    imposible que dos jugadores transporten la misma pieza o que una pieza
//    vendida/almacenada siga activa en el mundo.
//  - Registro estático por uid: el host lo usa para despawnear al vender/
//    procesar/entregar/almacenar, y los clientes para saber qué llevan en las
//    manos (HUD/estaciones).

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    [RequireComponent(typeof(PickupItem))]
    public class DisassemblyPieceView : NetworkBehaviour
    {
        /// <summary>uidNet → view viva (todas las peers).</summary>
        public static readonly Dictionary<int, DisassemblyPieceView> Registry =
            new Dictionary<int, DisassemblyPieceView>();

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

        public string DomainUid { get { return DisassemblySessionHost.NetToPieceUid(DomainUidN.Value); } }
        public string DefId { get { return CatalogIndex.PieceDefAt(DefIdx.Value); } }

        private PickupItem _pickup;
        private bool _suppressWatch;

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
                // Instrucción 10: material artístico teñido con el color del dominio
                // (la pieza sigue siendo reconocible por color); sin el asset, plano.
                Color pc = DisassemblyLayouts.PieceColor(defId, bp != null ? bp.Id : null);
                string lib = ETSVArt.PieceMaterialFor(defId);
                rend.sharedMaterial = lib != null
                    ? ETSVArt.Tinted(lib, pc)
                    : BlueprintBuilder.MakeMaterial(pc);
            }
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                var partDef = defId != null && EconomyData.PartDefs.ContainsKey(defId)
                    ? EconomyData.PartDefs[defId] : null;
                rb.mass = partDef != null ? Mathf.Max(1f, (float)partDef.Kg) : 2f;
            }
            name = "Piece_" + DomainUid + "_" + (defId ?? "def");

            Registry[DomainUidN.Value] = this;

            if (IsServer)
            {
                _pickup = GetComponent<PickupItem>();
                if (_pickup != null) _pickup.HeldBy.OnValueChanged += OnHeldByChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            Registry.Remove(DomainUidN.Value);
            if (_pickup != null) _pickup.HeldBy.OnValueChanged -= OnHeldByChanged;
        }

        // Host: el físico cambió de manos ⇒ reflejar en el dominio (o revertir el físico).
        private void OnHeldByChanged(ulong previous, ulong current)
        {
            if (!IsServer || _suppressWatch) return;
            var sc = ETSVSession.Scrapyard;
            if (sc == null) return;
            string uid = DomainUid;

            if (current == ulong.MaxValue)
            {
                if (previous != ulong.MaxValue) sc.OnPhysicalDrop(previous, uid);
                return;
            }

            // Retiro de almacén: el dominio YA la marcó carried por este jugador
            // (StationUseRpc) ⇒ consistencia alcanzada, no pisar.
            var p = PieceRules.GetPiece(sc.State.Reg, uid);
            if (p != null && p.Storage == "carried")
            {
                var carrier = sc.CarrierOf(uid);
                if (carrier.HasValue && carrier.Value == current) return;
            }

            var r = sc.OnPhysicalPickup(current, uid);
            if (!r.Ok)
            {
                // Rechazo del dominio (manos ocupadas, ajena, terminal, P3+…):
                // el físico vuelve al suelo. Una pieza en las manos de otro jugador
                // nunca pasa el check de HeldBy (single-value) ⇒ sin dobles portes.
                _suppressWatch = true;
                _pickup.HeldBy.Value = ulong.MaxValue;
                _suppressWatch = false;
            }
        }
    }
}
