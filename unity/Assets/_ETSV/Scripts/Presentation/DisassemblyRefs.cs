// [ETSV] Integración — marcadores de raycast. Componentes y fijaciones llevan
// estas referencias mínimas para que la interacción FPS (raycast al collider)
// sepa a qué elemento físico del blueprint está apuntando el jugador.

using UnityEngine;

namespace Etsv.Integration
{
    /// <summary>Marca el collider de un componente del blueprint.</summary>
    public class ComponentRef : MonoBehaviour
    {
        public int CompIdx { get; private set; }
        public void Init(int compIdx) { CompIdx = compIdx; }
    }

    /// <summary>Marca el collider de una fijación (punto de interacción).</summary>
    public class FastenerRef : MonoBehaviour
    {
        public int CompIdx { get; private set; }
        public int FastenerIdx { get; private set; }
        public void Init(int compIdx, int fastenerIdx) { CompIdx = compIdx; FastenerIdx = fastenerIdx; }
    }
}
