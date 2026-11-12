// Stub local de los scripts UPSTREAM de U6-FPM que mi código referencia
// (verificación de tipos fuera de Unity; no va al repo).
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public Ray InteractRay { get { return default(Ray); } }
    public float InteractRange { get { return 5f; } }
    public void StartPlaying() {}
}

public class PickupItem : Unity.Netcode.NetworkBehaviour
{
    public Unity.Netcode.NetworkVariable<ulong> HeldBy =
        new Unity.Netcode.NetworkVariable<ulong>(ulong.MaxValue);
    public void PickUpRpc(ulong clientId) {}
    public void DropRpc() {}
}
