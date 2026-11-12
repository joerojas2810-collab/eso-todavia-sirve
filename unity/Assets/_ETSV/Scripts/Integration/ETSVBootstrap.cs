// [ETSV] Integración — arranque automático SIN tocar la escena ni los prefabs de
// la base (cero trabajo de editor para que la Instrucción 06 sea visible):
//  1. RuntimeInitializeOnLoadMethod: registra los prefabs ETSV (Resources) en el
//     NetworkManager ANTES de que empiece la red (AddNetworkPrefab, API verificada
//     contra el fuente de NGO 2.12).
//  2. OnServerStarted (host): crea DisassemblySessionHost (cerebro puro) y
//     ScrapyardSession (economía cooperativa, Instrucción 07), spawnea el objeto
//     de sesión, una mesa de trabajo, el microondas del blueprint y las 14
//     estaciones de la chatarrería; engancha la desconexión de clientes.
//  3. Cada peer: cuando aparece su avatar local, le agrega el controlador de
//     desmontaje y crea el HUD (UI Toolkit en código).
// Todo lo construido es genérico: otros blueprints se spawnean igual (30 objetos
// sin controladores particulares). La escena y prefabs upstream NO se modifican.

using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using Etsv.Domain;

namespace Etsv.Integration
{
    public static class ETSVBootstrap
    {
        public static GameObject SessionPrefab { get; private set; }
        public static GameObject ObjectPrefab { get; private set; }
        public static GameObject PiecePrefab { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            var go = new GameObject("ETSV Bootstrap");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<BootstrapBehaviour>();
        }

        internal static bool LoadPrefabs()
        {
            if (SessionPrefab != null) return true;
            SessionPrefab = Resources.Load<GameObject>("ETSV/ETSVSession");
            ObjectPrefab = Resources.Load<GameObject>("ETSV/DisassemblyObject");
            PiecePrefab = Resources.Load<GameObject>("ETSV/DisassemblyPiece");
            bool ok = SessionPrefab != null && ObjectPrefab != null && PiecePrefab != null;
            if (!ok)
                Debug.LogError("[ETSV] Faltan prefabs en Assets/_ETSV/Resources/ETSV/ " +
                    "(ETSVSession/DisassemblyObject/DisassemblyPiece)");
            return ok;
        }
    }

    public class BootstrapBehaviour : MonoBehaviour
    {
        private NetworkManager _nm;
        private bool _serverReady;
        private bool _hudCreated;
        private GameObject _attachedPlayer;

        private void Awake()
        {
            _nm = NetworkManager.Singleton;
            if (_nm == null || !ETSVBootstrap.LoadPrefabs())
            {
                Destroy(gameObject);
                return;
            }
            // Registro dinámico de prefabs (válido antes de iniciar la red).
            TryRegister(ETSVBootstrap.SessionPrefab);
            TryRegister(ETSVBootstrap.ObjectPrefab);
            TryRegister(ETSVBootstrap.PiecePrefab);
        }

        private void TryRegister(GameObject prefab)
        {
            try { _nm.AddNetworkPrefab(prefab); }
            catch (System.Exception e) { Debug.LogWarning("[ETSV] AddNetworkPrefab: " + e.Message); }
        }

        private void Start()
        {
            if (_nm == null) return;
            _nm.OnServerStarted += OnServerStarted;
        }

        private void OnDestroy()
        {
            if (_nm != null) _nm.OnServerStarted -= OnServerStarted;
            ETSVSession.Host = null;
        }

        // ------------------------------------------------------------ host
        private void OnServerStarted()
        {
            if (_serverReady) return;
            _serverReady = true;

            var host = new DisassemblySessionHost();
            host.PlayerPos = TryGetPlayerPos;
            ETSVSession.Host = host;

            // Economía cooperativa (Instrucción 07): ANTES de spawnear instancias,
            // porque fija Ctx.SessionOwner="chatarrera" y las piezas nacen con ese
            // titular. Una sola copia de estado: el dominio ES el estado económico.
            ETSVSession.Scrapyard = new ScrapyardSession(host);
            _nm.OnClientDisconnectCallback += OnClientDisconnected;

            // Objeto de sesión (canal autoritativo de herramientas + economía).
            var sessionGo = Instantiate(ETSVBootstrap.SessionPrefab);
            sessionGo.GetComponent<NetworkObject>().Spawn();

            SpawnWorkbenchAndMicrowave(host);
            ScrapyardLayout.BuildStations(); // decorativas: mismas coordenadas en cada peer
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var sc = ETSVSession.Scrapyard;
            if (sc == null) return;
            // Pieza transportada vuelve al suelo lógico; el NetworkObject físico
            // persiste (PickupItem auto-drop) ⇒ sin duplicación ni pérdida.
            sc.OnPlayerDisconnected(clientId);
        }

        private bool TryGetPlayerPos(ulong clientId, out P3 pos)
        {
            pos = default(P3);
            if (_nm == null) return false;
            if (!_nm.ConnectedClients.TryGetValue(clientId, out var client)) return false;
            if (client.PlayerObject == null) return false;
            var p = client.PlayerObject.transform.position;
            pos = new P3(p.x, p.y, p.z);
            return true;
        }

        private void SpawnWorkbenchAndMicrowave(DisassemblySessionHost host)
        {
            Vector3 tablePos = ScrapyardLayout.TableAnchor(); // misma referencia que las estaciones

            BuildWorkbench(tablePos);

            int netId = host.SpawnInstance("microondas");
            if (netId < 0)
            {
                Debug.LogError("[ETSV] Blueprint 'microondas' no encontrado en el catálogo");
                return;
            }
            var go = Instantiate(ETSVBootstrap.ObjectPrefab,
                tablePos + new Vector3(0f, 0.95f, 0f), Quaternion.Euler(0f, -35f, 0f));
            var view = go.GetComponent<DisassemblyObjectView>();
            view.NetInstanceId.Value = netId;
            view.BlueprintIdx.Value = (byte)CatalogIndex.BlueprintIndexOf("microondas");
            go.GetComponent<NetworkObject>().Spawn();
        }

        /// <summary>Mesa de trabajo: geometría local determinista (misma posición en
        /// todas las peers ⇒ no necesita red; es decoración estática).</summary>
        private static void BuildWorkbench(Vector3 pos)
        {
            var wood = new Color(0.45f, 0.32f, 0.2f);
            var metal = new Color(0.4f, 0.42f, 0.45f);

            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "ETSV_Workbench_Top";
            top.transform.position = pos + new Vector3(0f, 0.9f, 0f);
            top.transform.localScale = new Vector3(1.6f, 0.1f, 1.0f);
            top.GetComponent<Renderer>().sharedMaterial = BlueprintBuilder.MakeMaterial(wood);

            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "ETSV_Workbench_Leg" + i;
                float x = (i % 2 == 0 ? -0.7f : 0.7f);
                float z = (i < 2 ? -0.4f : 0.4f);
                leg.transform.position = pos + new Vector3(x, 0.45f, z);
                leg.transform.localScale = new Vector3(0.08f, 0.9f, 0.08f);
                leg.GetComponent<Renderer>().sharedMaterial = BlueprintBuilder.MakeMaterial(metal);
            }
        }

        // ------------------------------------------------------------ cada peer
        private void Update()
        {
            if (_nm == null) return;

            // Host: envejecer sesiones de trabajo abandonadas (desconexión, soltar sin Stop).
            if (ETSVSession.Host != null && _nm.IsServer)
                ETSVSession.Host.PruneStaleWorkers(Time.deltaTime);

            // HUD + controlador locales cuando existe el avatar del jugador local.
            var local = _nm.LocalClient != null ? _nm.LocalClient.PlayerObject : null;
            if (local == null) return;

            if (!_hudCreated)
            {
                _hudCreated = true;
                var hudGo = new GameObject("ETSV Disassembly HUD");
                Object.DontDestroyOnLoad(hudGo);
                hudGo.AddComponent<UIDocument>();
                hudGo.AddComponent<DisassemblyHud>();
            }

            if (_attachedPlayer != local)
            {
                _attachedPlayer = local;
                if (local.GetComponent<DisassemblyPlayerController>() == null)
                    local.AddComponent<DisassemblyPlayerController>();
            }
        }
    }
}
