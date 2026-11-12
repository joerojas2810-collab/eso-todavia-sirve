// [ETSV] Integración — arranque automático SIN tocar la escena ni los prefabs de
// la base (cero trabajo de editor para que la Instrucción 06 sea visible):
//  1. RuntimeInitializeOnLoadMethod: registra los prefabs ETSV (Resources) en el
//     NetworkManager ANTES de que empiece la red (AddNetworkPrefab, API verificada
//     contra el fuente de NGO 2.12).
//  2. OnServerStarted (host): crea DisassemblySessionHost (cerebro puro) y
//     ScrapyardSession (economía cooperativa, Instrucción 07), spawnea el objeto
//     de sesión, una mesa de trabajo y las 14 estaciones de la chatarrería;
//     RESTAURA la partida guardada (Instrucción 08, PersistenceHub) o, si no
//     existe, spawnea el microondas de partida nueva; engancha la desconexión.
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
            if (_nm != null)
            {
                _nm.OnServerStarted -= OnServerStarted;
                _nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
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

            // Persistencia (Instrucción 08): SOLO el host guarda/restaura.
            var hubGo = new GameObject("ETSV Persistence");
            Object.DontDestroyOnLoad(hubGo);
            var hub = hubGo.AddComponent<PersistenceHub>();
            hub.InitHost(System.IO.Path.Combine(Application.persistentDataPath, "etsv_save.json"));

            // Geometría de referencia (idéntica en cada peer: no se replica).
            BuildWorkbench(ScrapyardLayout.TableAnchor());
            ScrapyardLayout.BuildStations();
            ETSVScenery.Build(); // Instrucción 10: escenografía estática (sin colliders ni red)

            if (hub.TryRestore(host))
            {
                // Partida restaurada: el mundo físico ya lo re-materializó el hub;
                // la sync económica viaja por las NVs/NetworkLists existentes.
                var sv = ETSVSessionView.Instance;
                if (sv != null) { sv.SyncEconomy(); sv.FeedRestored(); }
            }
            else
            {
                SpawnMicrowave(host); // partida nueva: el microondas del GDD sobre la mesa
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var sc = ETSVSession.Scrapyard;
            if (sc != null)
            {
                // Pieza transportada vuelve al suelo lógico; el NetworkObject físico
                // persiste (PickupItem auto-drop) ⇒ sin duplicación ni pérdida.
                sc.OnPlayerDisconnected(clientId);

                // Físico: soltar lo que el cliente que se fue tuviera agarrado
                // (HeldBy server-write; el watcher de la pieza lo refleja idempotente).
                foreach (var kv in DisassemblyPieceView.Registry)
                {
                    if (kv.Value == null) continue;
                    var pickup = kv.Value.GetComponent<PickupItem>();
                    if (pickup != null && pickup.HeldBy.Value == clientId)
                        pickup.HeldBy.Value = ulong.MaxValue;
                }
            }

            // GDD §16.2: checkpoint al cerrarse la sesión de un jugador.
            if (_nm != null && _nm.IsServer && PersistenceHub.Instance != null)
                PersistenceHub.Instance.SaveNow(clientId, false);
        }

        private bool TryGetPlayerPos(ulong clientId, out P3 pos)
        {
            pos = default(P3);
            if (_nm == null) return false;
            // NGO real (2.12): ConnectedClients es IReadOnlyDictionary<ulong, NetworkClient>
            // (NetworkManager.cs:599) — no tiene TryGetValue; ContainsKey + indexador sí.
            var clients = _nm.ConnectedClients;
            if (!clients.ContainsKey(clientId)) return false;
            var client = clients[clientId];
            if (client.PlayerObject == null) return false;
            var p = client.PlayerObject.transform.position;
            pos = new P3(p.x, p.y, p.z);
            return true;
        }

        private void SpawnMicrowave(DisassemblySessionHost host)
        {
            Vector3 tablePos = ScrapyardLayout.TableAnchor(); // misma referencia que las estaciones

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

            // Footprint y altura de trabajo IDÉNTICOS (1.6 × 0.9 × 1.0, tope y=0.95):
            // la geometría funcional no cambia, solo el material (Instrucción 10).
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "ETSV_Workbench_Top";
            top.transform.position = pos + new Vector3(0f, 0.9f, 0f);
            top.transform.localScale = new Vector3(1.6f, 0.1f, 1.0f);
            top.GetComponent<Renderer>().sharedMaterial = ETSVArt.Tinted(ETSVArt.WoodPlanks, wood);

            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "ETSV_Workbench_Leg" + i;
                float x = (i % 2 == 0 ? -0.7f : 0.7f);
                float z = (i < 2 ? -0.4f : 0.4f);
                leg.transform.position = pos + new Vector3(x, 0.45f, z);
                leg.transform.localScale = new Vector3(0.08f, 0.9f, 0.08f);
                leg.GetComponent<Renderer>().sharedMaterial = ETSVArt.Tinted(ETSVArt.SteelRusty, metal);
            }

            // Travesaño inferior + estante (detalles visuales SIN collider: la mesa
            // nunca tuvo colliders propios y el raycast de interacción no debe cambiar).
            AddBenchDetail(pos, "Travesanio", new Vector3(0f, 0.2f, 0.4f), new Vector3(1.44f, 0.06f, 0.06f), metal);
            AddBenchDetail(pos, "Travesanio2", new Vector3(0f, 0.2f, -0.4f), new Vector3(1.44f, 0.06f, 0.06f), metal);
            AddBenchDetail(pos, "Estante", new Vector3(0f, 0.24f, 0f), new Vector3(1.5f, 0.03f, 0.86f), new Color(0.4f, 0.29f, 0.18f));

            // Utilería junto a la mesa (Instrucción 11): caja de herramientas, tacho y
            // una caja de cartón. Modelos CC0 1:1, pivotes en la base, SIN colliders
            // (la mesa nunca los tuvo; el raycast de interacción no debe cambiar).
            // Sin padre (igual que AddBenchDetail): coordenadas de mundo absolutas.
            ETSVModels.TryCreate(ETSVModels.Quaternius, "Prop_Chest", "ETSV_Workbench_CajaHerramientas",
                null, pos + new Vector3(-1.9f, 0f, -1.1f), 0.55f, 25f,
                ETSVArt.Tinted(ETSVArt.MetalPainted, new Color(0.7f, 0.45f, 0.3f)));
            ETSVModels.TryCreate(ETSVModels.KenneyFurniture, "trashcan", "ETSV_Workbench_Tacho",
                null, pos + new Vector3(1.15f, 0f, 0.35f), 1f, -15f,
                ETSVArt.Tinted(ETSVArt.PlasticGrey, new Color(0.7f, 0.72f, 0.7f)));
            ETSVModels.TryCreate(ETSVModels.KenneyFurniture, "cardboardBoxClosed", "ETSV_Workbench_Caja",
                null, pos + new Vector3(-0.95f, 0f, 0.95f), 1.2f, 12f,
                ETSVArt.Tinted(ETSVArt.WoodPlanks, new Color(0.85f, 0.75f, 0.6f)));
        }

        private static void AddBenchDetail(Vector3 pos, string name, Vector3 off, Vector3 scale, Color tint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ETSV_Workbench_" + name;
            go.transform.position = pos + off;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = ETSVArt.Tinted(ETSVArt.SteelRusty, tint);
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        // ------------------------------------------------------------ cada peer
        private void Update()
        {
            if (_nm == null) return;

            // Host: envejecer sesiones de trabajo abandonadas (desconexión, soltar sin Stop).
            if (ETSVSession.Host != null && _nm.IsServer)
                ETSVSession.Host.PruneStaleWorkers(Time.deltaTime);

            // HUD + controlador locales cuando existe el avatar del jugador local.
            // NGO real (2.12): NetworkClient.PlayerObject es NetworkObject (Component),
            // no GameObject — la referencia GameObject se toma vía .gameObject.
            var localNo = _nm.LocalClient != null ? _nm.LocalClient.PlayerObject : null;
            if (localNo == null) return;
            var local = localNo.gameObject;

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
