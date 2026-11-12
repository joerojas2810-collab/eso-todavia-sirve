# INFORME — Instrucción 09 (Auditoría de integración, CI, dirección artística)

Fecha: 2026-11-12 · Commit: **ebf8751** (pusheado a `main`) · Anterior: 97414bd

## 1. Qué se hizo

Auditoría profunda de la integración Unity del proyecto contra el **fuente
real de NGO 2.12.0** (no contra suposiciones): serialización de
NetworkVariables, ILPP, registro de prefabs, escena Multiplayer.unity,
asmdefs, GUIDs, input, ciclo de vida del NetworkManager y persistencia.
Se aplicaron 2 fixes críticos, se completaron los .meta faltantes, se
versionó la CI en el repo, se agregó una suite de flujo integrado (F01/F02)
y se escribieron la biblia visual y la matriz de reemplazo de assets.

## 2. Bugs encontrados y fixeados

**BUG #1 — GUID roto del registro de prefabs de red (heredado de upstream).**
`Multiplayer.unity:14324` referencia el guid `5dd0674a934303a49ba86f0fc8f992d8`,
que no existía en el proyecto: el asset real
`Assets/DefaultNetworkPrefabs.asset` tenía guid `bf94dd35…`. Roto desde el
commit upstream c133e3b. Consecuencia: `NetworkPrefabs.Initialize`
(NetworkPrefabs.cs:88-160) hace `RemoveAll(null)` ⇒ el registro quedaba
**vacío** ⇒ `Item.prefab` (pickups base del upstream) nunca registrado.
PlayerPrefab se salvaba por el auto-registro de
`RegisterPlayerPrefab` (NetworkPrefabHandler.cs:452) y los prefabs ETSV por
el registro runtime en Awake. **Fix**: reescribir el guid del
`.meta` a `5dd0674a…` (verificado antes: nada más en Assets referenciaba el
guid viejo ⇒ la escena upstream queda intacta y el fix es de 1 línea).

**BUG #2 — `CompSync` sin serializador ⇒ crash en el primer sync.**
`CompSync` (struct POD: CompIdx/State/Quality byte + FastenerBits ulong) se
usa en `NetworkList<CompSync>`. El ILPP real (NetworkBehaviourILPP.cs:225-435,
:552-589) solo genera serializer para primitivas, built-ins, enums,
`INetworkSerializable`, FixedString y `INetworkSerializeByMemcpy`; un struct
custom sin tag cae en `FallbackSerializer`, que lanza `ArgumentException`
cuando los delegates son null (FallbackSerializer.cs:39-90) ⇒ excepción en el
primer spawn/sync de cada objeto desmontable en red. Nota: la conclusión
previa "el constraint `unmanaged, IEquatable<T>` de NetworkList basta" era
FALSA y fue refutada leyendo el fuente. **Fix (camino (a), tag interface)**:
`CompSync : IEquatable<CompSync>, Unity.Netcode.INetworkSerializeByMemcpy`
(memcpy puro: 11 bytes, sin referencias ⇒ legal) + referencia
`Unity.Netcode.Runtime` en `ETSV.Sim.asmdef` (el assembly NGO es C# puro,
compatible con `noEngineReferences: true`). El camino (b) (delegates manuales
vía `UserNetworkVariableSerialization`) quedó descartado por frágil.

## 3. Verificaciones (sin fix necesario)

- **CheckDirtyState**: las mutaciones de NetworkList en las views ETSV
  (`DisassemblyObjectView.Resync`, `ETSVSessionView`) usan SIEMPRE el setter
  de índice completo (`Comps[i] = snap[i]`), que en NGO 2.12 pasa por
  `Set()` → `HandleAddListEvent` (marca dirty + evento + delta). Cero
  mutación in-place de campos de structs ⇒ no hace falta `CheckDirtyState()`
  explícito en ningún punto.
- RPCs de las 3 views: formato NGO 2.x correcto (`[Rpc(SendTo.*)]`,
  `RpcParams` último parámetro `= default`).
- Prefabs YAML `_ETSV/Resources/ETSV/`: GUIDs de NGO correctos, bloques de
  NetworkVariable coinciden campo por campo con las views, fileIDs verificados;
  `DisassemblyObject.prefab` ≡ `Item.prefab` en sus 44 componentes comunes.
- Escena: 14 estaciones en offsets deterministas, raycast de interacción OK,
  `activeInputHandler: 1` (nuevo InputSystem) y cero `UnityEngine.Input`
  legacy en `_ETSV`; EditorBuildSettings con las 3 escenas enabled.
- Ciclo de vida: `OnServerStarted` se invoca DESPUÉS de los spawn sweeps
  (NetworkManager.cs:1357/:1517) ⇒ el bootstrap ETSV que restaaura partida
  ahí encuentra la red lista; orden de NVs-antes-de-Spawn en PieceSpawner OK
  para late-join; `NetworkList` serializa la lista completa ⇒ late-join
  recibe estado; desconexión → `OnPlayerDisconnected` → pieza al suelo.
- asmdefs: referencias y constraints correctos; hashes de RPC estables.
- Singleplayer.unity no tiene NetworkManager (correcto: las views no se
  instancian ahí).

## 4. Tests (todo ejecutado, resultados reales)

| Suite | Resultado |
|---|---|
| Banco C# (unity/ci/build-and-test.sh, mono) | **78 passed, 0 failed** |
| — de los cuales NUEVOS: IntegrationFlowTests F01/F02 | 2 passed |
| Prototipo JS desmontaje | 20 passed |
| Prototipo JS economía | 53 passed |

F01 recorre el juego de punta a punta: spawn de microondas → liberar 4
tornillos con StartWork/WorkTick reales → extraer carcasa → pickup → vender
en balanza ($22) → segundo microondas → procesar carcasa en trituradora
(3 u de acero) → vender material en contenedor (+$12 = $34) → guardar
(SafeWrite) → restaurar en host nuevo → verificar dinero/terminales/
almacenes exactos → **seguir jugando** sobre la partida cargada (puerta,
bisagras con llave, venta +$14 = $48). F02: dos jugadores trabajan fijaciones
distintas de la misma pieza en paralelo, uno extrae, el otro vende, y la
plata es del estado de sesión (sobrevive al guardado).

## 5. Limitaciones honestas (qué NO está verificado)

- **No se ejecutó Unity Editor** (el entorno no lo tiene): la compilación
  real (incluido el ILPP que valida el FIX #2), el import de los prefabs YAML
  y de los 58 .meta nuevos, y la física/replicación reales quedan pendientes
  de Editor. La combinación `ETSV.Sim.asmdef` (noEngineReferences + ref NGO)
  está validada solo por el banco mono, NO por el compilador de Unity.
- **Los workflows de GitHub Actions NO están activos**: el token de push no
  tiene el scope `workflow` (GitHub rechazó crear `.github/workflows/`). Los
  YAML están listos en `unity/ci/workflows/` — activarlos es copiarlos a
  `.github/workflows/` (1 paso, lo hace el propietario; instrucciones en
  `unity/ci/README.md`).
- `unity-compile.yml` (compilación real + EditMode con game-ci) está
  **preparado pero desactivado** (workflow_dispatch): requiere que el
  propietario configure `UNITY_EMAIL`/`UNITY_PASSWORD`/`UNITY_LICENSE`
  (Personal, gratis). Sin secretos falla de forma explícita. **No se
  inventaron credenciales.**
- El banco con stubs no ejercita el ILPP: por eso la auditoría estática
  contra el fuente real de NGO fue (y seguirá siendo) parte del proceso.
- No se compró ningún asset (restricción respetada). No se empezaron los
  objetos 02–30 (restricción respetada).

## 6. Archivos (76 en el commit)

- **Fixes**: `unity/Assets/DefaultNetworkPrefabs.asset.meta` (BUG #1);
  `unity/Assets/_ETSV/Scripts/Integration/Sim/DisassemblySync.cs` +
  `ETSV.Sim.asmdef` (BUG #2).
- **Nuevo**: `unity/Assets/_ETSV/Tests/EditMode/IntegrationFlowTests.cs`;
  58 `.meta` en `Assets/_ETSV` (GUIDs deterministas por ruta, 0 colisiones
  verificadas en todo Assets+Packages); `unity/ci/` (build-and-test.sh,
  README.md, stubs/, shim/, workflows/); `unity/docs/DIRECCION_ARTISTICA.md`;
  `unity/docs/MATRIZ_REEMPLAZO_VISUAL.md`; `.gitignore` += unity/ci/.build/.

## 7. Recomendaciones / próximos pasos

1. **Activar CI (5 min)**: copiar `unity/ci/workflows/*.yml` a
   `.github/workflows/` (web UI de GitHub alcanza). `verify-domain.yml`
   corre el banco (78 tests) + JS en cada push, gratis y sin secretos.
2. **Compilación real (cuando se pueda)**: configurar los 3 secretos de
   licencia Unity Personal y ejecutar `unity-compile.yml` manualmente. Eso
   validará de una vez: ILPP del FIX #2, import de .meta/prefabs YAML,
   asmdef con ref NGO, y los 78 tests bajo Unity Test Framework real.
   Alternativa local: abrir `unity/` en Unity 6000.3.5f2 y correr
   Window > General > Test Runner (EditMode).
3. **Arte**: seguir la matriz (`unity/docs/MATRIZ_REEMPLAZO_VISUAL.md`) en el
   orden P1→P11; primer paso recomendado = HDRI + set de materiales PBR CC0
   (Poly Haven/ambientCG): máximo impacto, cero riesgo de red. Cada asset que
   entre pasa por la checklist §12 de la biblia y se documenta en `_SOURCE.md`
   con su licencia.
4. Si el Editor reporta algo sobre `ETSV.Sim.asmdef` + NGO (improbable: NGO
   Runtime es C# puro), el fallback documentado es el camino (b) — delegates
   `UserNetworkVariableSerialization` en el bootstrap (detalles en el
   comentario de cabecera de DisassemblySync.cs).

**STOP aquí**: no se avanzó sobre objetos 02–30 ni sobre fases de
implementación artística (solo documentación), conforme a las restricciones
de la Instrucción 09.
