using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Approves connections and spawns players at designated spawn points.
/// </summary>
[DefaultExecutionOrder(-10)]
public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;

    private int _nextSpawnIndex;

    private void Start()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback += OnApprove;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.ConnectionApprovalCallback -= OnApprove;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
    }

    // [ETSV] Approve up to SessionConfig.MaxPlayers (GDD 1.0: 1–7; upstream: 4
    // limitados por los spawn points de la escena). Tell NGO we handle spawning ourselves.
    private void OnApprove(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        res.Approved = _nextSpawnIndex < SessionConfig.MaxPlayers;
        res.CreatePlayerObject = false;
    }

    // Runs on server only — instantiate the player at the next available spawn point.
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (_nextSpawnIndex >= SessionConfig.MaxPlayers) return; // [ETSV] cap de sesión

        int spawnIndex = _nextSpawnIndex++;
        var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        if (prefab == null) { Debug.LogError("[SpawnManager] PlayerPrefab is null on NetworkConfig!"); return; }

        // [ETSV] La escena upstream trae 4 spawn points; los jugadores 5–7 usan
        // posiciones de reserva deterministas en anillo alrededor del punto 0,
        // hasta colocar 7 spawns propios en la escena (pendiente de editor, ver
        // unity/docs/INTEGRACION.md).
        Vector3 spawnPos; Quaternion spawnRot;
        if (spawnIndex < _spawnPoints.Length && _spawnPoints[spawnIndex] != null)
        {
            spawnPos = _spawnPoints[spawnIndex].position;
            spawnRot = _spawnPoints[spawnIndex].rotation;
        }
        else
        {
            Transform anchor = _spawnPoints.Length > 0 ? _spawnPoints[0] : null;
            Vector3 center = anchor != null ? anchor.position : Vector3.zero;
            float angle = spawnIndex * (Mathf.PI * 2f / SessionConfig.MaxPlayers);
            spawnPos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.5f;
            spawnRot = anchor != null ? anchor.rotation : Quaternion.identity;
        }

        var go = Instantiate(prefab, spawnPos, spawnRot);
        var no = go.GetComponent<NetworkObject>();
        if (no == null) { Debug.LogError("[SpawnManager] Player prefab has no NetworkObject!"); return; }

        no.SpawnAsPlayerObject(clientId, destroyWithScene: true);

        var colour = go.GetComponent<PlayerColour>();
        if (colour != null) colour.PlayerIndex.Value = spawnIndex;
    }

    // Reset the index when the session ends so the next host starts fresh.
    private void OnServerStopped(bool _) => _nextSpawnIndex = 0;
}
