// Stub local de Unity.Netcode (NGO 2.12) — firmas copiadas del fuente REAL del
// paquete descargado (packages.unity.com). Solo para verificación de tipos fuera
// de Unity; no va al repositorio.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Netcode
{
    // NGO 2.12 real: tag interface (Runtime/Serialization/INetworkSerializeByMemcpy.cs)
    public interface INetworkSerializeByMemcpy { }

    public enum NetworkVariableReadPermission { Everyone, Server }
    public enum NetworkVariableWritePermission { Server, Owner }

    public abstract class NetworkVariableBase
    {
        public NetworkVariableBase(NetworkVariableReadPermission r, NetworkVariableWritePermission w) {}
        public virtual void ResetDirty() {}
        public virtual bool IsDirty() => false;
    }

    public class NetworkVariable<T> : NetworkVariableBase
    {
        public T Value;
        public NetworkVariable(T value = default(T),
            NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
            : base(readPerm, writePerm) { Value = value; }
        public event Action<T, T> OnValueChanged;
    }

    public delegate void OnListChangedDelegate<T>(NetworkListEvent<T> e);

    public class NetworkList<T> : NetworkVariableBase where T : struct, IEquatable<T>
    {
        private List<T> _l = new List<T>();
        public NetworkList() : base(NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server) {}
        public NetworkList(IEnumerable<T> values = default(IEnumerable<T>),
            NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
            : base(readPerm, writePerm) {}
        public event Action<NetworkListEvent<T>> OnListChanged;
        public int Count => _l.Count;
        public T this[int i] { get => _l[i]; set => _l[i] = value; }
        public void Add(T v) { _l.Add(v); }
        public void RemoveAt(int i) { _l.RemoveAt(i); }
        public void Clear() { _l.Clear(); }
    }

    public struct NetworkListEvent<T>
    {
        public enum EventType : byte { Add, Insert, Remove, RemoveAt, Value, Clear, Full }
        public EventType Type;
        public int Index;
        T Value;
    }

    // NGO 2.12 real: public enum SendTo (RpcTarget.cs)
    public enum SendTo { Server, Client, Everyone, NotServer, Me, NotMe, Owner, NotOwner, Authority, NotAuthority, Single }
    public abstract class BaseRpcTarget {}

    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public RpcAttribute(SendTo target) {}
        public RpcInvokePermission InvokePermission;
        public bool RequireOwnership;
        public bool DeferLocal;
    }
    public enum RpcInvokePermission { Default, Everyone, Owner, Server }

    public struct RpcReceiveParams { public ulong SenderClientId; }
    public struct RpcSendParams { public BaseRpcTarget Target; }
    public struct RpcParams
    {
        public RpcSendParams Send;
        public RpcReceiveParams Receive;
    }

    public class NetworkObject : MonoBehaviour
    {
        public ulong NetworkObjectId;
        public bool IsSpawned => false;
        public void Spawn(bool destroyWithScene = false) {}
        public void SpawnWithOwnership(ulong clientId, bool destroyWithScene = false) {}
        public void Despawn(bool destroy = false) {}
    }

    public abstract class NetworkBehaviour : MonoBehaviour
    {
        public bool IsServer => true;
        public bool IsClient => true;
        public bool IsOwner => true;
        public bool IsHost => true;
        public ulong OwnerClientId => 0;
        public NetworkObject NetworkObject => null;
        public virtual void OnNetworkSpawn() {}
        public virtual void OnNetworkDespawn() {}
    }

    public class NetworkClient
    {
        public GameObject PlayerObject;
        public ulong ClientId;
    }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Singleton => null;
        public bool IsServer => true;
        public bool IsHost => true;
        public bool IsClient => true;
        public bool IsListening => false;
        public NetworkConfig NetworkConfig => null;
        public Dictionary<ulong, NetworkClient> ConnectedClients => null;
        public NetworkClient LocalClient => null;
        public event Action OnServerStarted;
        public event Action<ulong> OnClientConnectedCallback;
        public event Action<ulong> OnClientDisconnectCallback;
        public event Action<bool> OnServerStopped;
        public void AddNetworkPrefab(GameObject prefab) {}
        public ConnectionApprovalDelegate ConnectionApprovalCallback;
        public delegate void ConnectionApprovalDelegate(ConnectionApprovalRequest req, ConnectionApprovalResponse res);
        public class ConnectionApprovalRequest {}
        public class ConnectionApprovalResponse { public bool Approved; public bool CreatePlayerObject; }
    }

    public class NetworkConfig
    {
        public bool ConnectionApproval;
        public GameObject PlayerPrefab;
    }
}
