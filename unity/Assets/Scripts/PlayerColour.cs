using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Assigns a head material colour to each player based on spawn index.
/// </summary>
public class PlayerColour : NetworkBehaviour
{
    [SerializeField] private Renderer _headRenderer;
    [SerializeField] private Material[] _materials; // index 0=Blue 1=Pink 2=Green 3=Yellow

    public NetworkVariable<int> PlayerIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        PlayerIndex.OnValueChanged += OnIndexChanged;

        // Value may already be set if we're a late-joining observer.
        if (PlayerIndex.Value >= 0)
            Apply(PlayerIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        PlayerIndex.OnValueChanged -= OnIndexChanged;
    }

    private void OnIndexChanged(int _, int current)
    {
        if (current >= 0) Apply(current);
    }

    private void Apply(int index)
    {
        Debug.Log($"[PlayerColour] Apply({index}) headRenderer={_headRenderer} matCount={_materials?.Length}");
        // [ETSV] sesiones de hasta 7 jugadores con la paleta upstream de 4 materiales:
        // los índices extra ciclan la paleta hasta agregar materiales 5–7 en el prefab
        // (pendiente de editor, ver unity/docs/INTEGRACION.md).
        if (_headRenderer == null || index < 0 || _materials == null || _materials.Length == 0) return;
        var m = _materials[index % _materials.Length];
        if (m == null) return;
        _headRenderer.material = m;
        Debug.Log($"[PlayerColour] Material set to {m.name}");
    }
}
