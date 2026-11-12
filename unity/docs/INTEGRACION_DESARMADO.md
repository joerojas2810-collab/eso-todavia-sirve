# Integración del desmontaje físico (Instrucción 06)

Estado: **código escrito y verificado sin Unity Editor** (compilación real bajo
Mono contra el dominio y contra stubs; tests puros ejecutados 46/46 PASS).
La compilación en Unity, la replicación real por red y las físicas **solo se
pueden verificar abriendo el proyecto en Unity 6** (checklist al final).

## 1. Infraestructura de U6-FPM inspeccionada y REUTILIZADA

Verificado leyendo el código real de la base (`Assets/Scripts/`, prefabs, escena
`Multiplayer.unity`, fuente del paquete NGO 2.12.0 descargado de
packages.unity.com):

| Mecanismo de la base | Cómo se reutiliza |
|---|---|
| `PlayerMovement.InteractRay` / `InteractRange` (5 m) | El targeting del desmontaje usa exactamente el mismo raycast que el pickup upstream |
| Acción de input `Interact` (E / gamepad buttonNorth, Hold ~0.4 s) | Mismo gesto para trabajar/extraer/golpear; se subscribe `performed`/`canceled`, no se toca el asset de input |
| `PickupItem` (NetworkBehaviour: `HeldBy` NV server-write, `PickUpRpc`/`DropRpc`, hold kinemático con anti-stuck) | Las piezas extraídas LLEVAN `PickupItem`: levantar/transportar/soltar/lanzar queda resuelto por la base, sin duplicarlo |
| Patrón RPC de NGO 2.12 `[Rpc(SendTo.Server)]` + `RpcParams.Receive.SenderClientId` | Todas las solicitudes de desmontaje siguen el patrón exacto de `PickupItem` |
| `NetworkVariable` server-write para estado compartido | Estado del objeto = `NetworkList<CompSync>` (8 bytes/componente) |
| `PlayerSpawnManager` + `SessionConfig` (1–7) | Sin cambios; las validaciones de identidad usan `ConnectedClients[clientId].PlayerObject` |
| HUD UI Toolkit (`HudTemplate.uxml`, `PanelSettings`) | El HUD de desmontaje reutiliza el `PanelSettings` de la base y se construye en código (cero assets que cablear) |
| `GameManager.OnGameStarted` | No se modificó; el bootstrap ETSV arranca con `NetworkManager.OnServerStarted` |

**Ningún script upstream fue modificado en esta instrucción.**

## 2. Arquitectura nueva (todo bajo `Assets/_ETSV/`)

```
Scripts/Integration/
├── Sim/  ← assembly ETSV.Sim (C# PURO, noEngineReferences — compilado y testeado sin Unity)
│   ├── ETSV.Sim.asmdef
│   ├── ETSVSession.cs               punto de acceso al cerebro del host (null en clientes)
│   ├── CatalogIndex.cs              ids de texto ↔ índices de red (determinista en todas las peers)
│   ├── DisassemblyGate.cs           validadores puros: presencia/distancia/trabajador/índices
│   ├── DisassemblySync.cs           CompSync (unmanaged+IEquatable p/ NetworkList) + pack/unpack
│   ├── DisassemblySessionHost.cs    CEREBRO DEL HOST: convierte solicitudes en ops del dominio
│   ├── DomainHost.cs                (movido acá desde Integration/ — mismo código)
│   └── SaveJsonAdapter.cs           (movido acá — DTO↔JSON Newtonsoft)
├── Disassembly/  ← Assembly-CSharp (cáscaras Unity delgadas)
│   ├── DisassemblyObjectView.cs     NetworkBehaviour: RPCs + NetworkList<CompSync> + visuales
│   ├── DisassemblyPieceView.cs      NetworkBehaviour de pieza (+ PickupItem de la base)
│   ├── ETSVSessionView.cs           canal autoritativo de selección de herramienta
│   ├── BlueprintBuilder.cs          GameObjects genéricos desde ObjectBlueprint (30 objetos, 0 controladores particulares)
│   ├── DisassemblyLayouts.cs        tabla visual del microondas + layout genérico en rejilla
│   ├── DisassemblyRefs.cs           ComponentRef/FastenerRef (marcadores de raycast)
│   ├── DisassemblyPlayerController.cs  input local → solicitudes RPC + info p/ HUD
│   └── DisassemblyHud.cs            UI Toolkit en código (objeto/componente/fijación/herramienta/progreso/calidad/pistas)
└── ETSVBootstrap.cs                 arranque automático SIN tocar escena ni prefabs de la base
Resources/ETSV/                      prefabs de red (YAML escrito a mano, validado estructuralmente)
    ETSVSession.prefab · DisassemblyObject.prefab · DisassemblyPiece.prefab
```

Regla de oro respetada: **el cliente solicita, el host decide**. Los RPC llevan
solo índices (netId, compIdx, fIdx, dt); la identidad del solicitante sale de
`RpcParams.Receive.SenderClientId` (la pone NGO, no el cliente), la posición del
jugador la lee el host de `ConnectedClients`, y la herramienta equipada vive en
el registro autoritativo del host (`SelectToolRpc` → `DisassemblySessionHost`).

### Flujo de una sesión de trabajo

1. El jugador apunta a una fijación (esfera roja) y mantiene E.
2. `performed` → `WorkStartRpc` → host: gate (identidad+distancia) → conflicto de
   trabajador → `DisassemblyRules.CanWork` con la herramienta del registro.
3. Cada 100 ms el cliente manda `WorkTickRpc(dt=0.1)`; el host acota dt a 0.25 s
   (anti-aceleración), aplica `DisassemblyRules.Work` y repuebla la
   `NetworkList<CompSync>` solo si algo cambió (sincronización por eventos, no
   continua).
4. Al soltar E → `WorkStopRpc`; el progreso fraccional queda guardado (paridad JS #5).
5. Sin ticks por 0.75 s (desconexión) → `PruneStaleWorkers` libera la fijación.
6. Componente Liberado + E → `ExtractRpc` → dominio `Extract` + `EconomyOps.ExtractPiece`
   (uid persistente `p_N`) → host spawnea `DisassemblyPiece.prefab` con la identidad
   codificada → todos ven la pieza física (PickupItem de la base).

### Sincronización y late-join

- Estado por objeto: `NetworkList<CompSync>` — 8 bytes/componente
  (índice, estado, calidad, fijaciones empaquetadas: bit retirado + progreso 7 bits).
- NGO replica el estado completo de NetworkVariables/NetworkList con el spawn ⇒
  **el jugador tardío recibe el estado actual automáticamente** (verificado en la
  capa pura: `V08_IncorporacionTardia_RecibeEstadoActual`).
- Piezas sueltas: NetworkObject propio con `NetworkTransform` (posición/movimiento
  por la base) + identidad en NVs.
- Nada derivable viaja: colores/ocultamiento/HUD se calculan localmente desde el snapshot.

## 3. Adaptaciones y decisiones registradas (NO son cambios al dominio)

1. **Owner = "P1" en todas las piezas (cooperativo provisional).** El dominio
   actual solo reconoce "P1" (heredado del prototipo monojugador; el propio
   código JS decía "futuro: otros jugadores/contratos"). En coop todos operan
   todo; la propiedad por jugador queda como decisión pendiente de la evolución
   del dominio (no se modificó `ETSV.Domain`).
2. **El transporte físico todavía no muta `storage` del dominio.**
   `EconomyContext.Carry` es single-slot (prototipo monojugador); sincronizar
   "manos" por jugador requiere evolucionar el dominio. La pieza extraída queda
   `storage="world"` hasta la etapa de balanza/almacén. `PickupItem` da la
   conducta física correcta mientras tanto.
3. **Golpe = mantener E sobre un componente con herramienta que no sirve para
   NINGUNA de sus fijaciones** → `DamageRpc(12)`, paridad con `disassembly.test.mjs`
   #13 (calidad 100→88). El trabajo correcto nunca daña (#12).
4. **Herramientas virtuales (teclas 1/2/3)**: llave/destornillador/manos son datos
   del catálogo (`GameCatalog.Tools`, con su tecla). No hay meshes de herramienta
   en mano todavía (etapa posterior).
5. **Geometría primitiva**: el microondas se construye con cubos/cilindros por
   componente (tabla visual en `DisassemblyLayouts`, SOLO presentación — el
   dominio no tiene geometría a propósito). Cada componente y cada fijación son
   entidades físicas diferenciadas con collider propio: no es una interacción
   genérica sobre el objeto completo.
6. **Bootstrap sin editor**: prefabs en `Resources/ETSV/` registrados con
   `NetworkManager.AddNetworkPrefab` en `RuntimeInitializeOnLoadMethod` (API
   verificada contra el fuente de NGO 2.12: exige NetworkObject y registro antes
   de `IsListening`). La mesa de trabajo y el microondas los spawnea el host junto
   al spawn point "Player 2 Spawn" de la escena (fallback: origen). CERO ediciones
   a la escena o prefabs de la base.
7. **Meta files con GUID fijos** solo para los 3 scripts referenciados por los
   prefabs YAML (`DisassemblyObjectView`, `DisassemblyPieceView`, `ETSVSessionView`)
   + los 3 prefabs. El resto sigue la convención del repo (Unity genera las .meta
   al abrir).

## 4. Verificaciones automatizadas (Instrucción 06 §10)

`Assets/_ETSV/Tests/EditMode/HostSessionTests.cs` — 13 tests NUnit sobre la capa
Sim (C# puro ⇒ ejecutables de verdad sin Unity):

| Requisito §10 | Test | Resultado (Mono) |
|---|---|---|
| Dos jugadores extrayendo la misma pieza | `V01` | ✅ uno logra, otro `no_liberado`, 1 sola pieza en registro |
| Herramienta incorrecta | `V02`, `V02b` | ✅ `herramienta`; el host usa SU registro, no lo que diga el cliente |
| Extracción bloqueada | `V03` | ✅ `no_liberado` (Bloqueado y Accesible-sujeto) |
| Solicitudes duplicadas | `V04`, `V04b`, `V04c` | ✅ `ocupado_por_otro`, `sin_sesion_trabajo`, pruning por timeout, dt acotado |
| Distancia inválida | `V05` | ✅ `distancia_invalida` (>5 m), `jugador_desconocido` |
| Reconstrucción de estados | `V06` | ✅ snapshot → réplica idéntica (fijaciones, calidad, bloqueos) |
| Identidad persistente | `V07` | ✅ uid `p_N` ↔ int de red ida/vuelta; origen; round-trip SaveGame v3 |
| Late join | `V08` | ✅ réplica desde snapshot de partida avanzada |
| (extra) Progreso entre jugadores | `V09` | ✅ 50%+50% entre dos jugadores distintos |
| (extra) Daño por golpe | `V10` | ✅ 100→88, paridad JS #13 |

Suite completa bajo Mono: **46/46 PASS** (33 de Instrucción 05 + 13 nuevas).
Lo que NO se puede testear sin Unity (queda pendiente de Editor): replicación
real por red entre dos instancias, físicas de piezas, raycasts contra colliders
reales, HUD renderizado, flujo de lobby completo.

## 5. Checklist en Unity Editor (primera apertura)

1. Abrir `unity/` con Unity 6000.3.5f2. Si git pull se queja de `.meta`
   untracked bajo `Assets/_ETSV` (porque ya habías abierto el proyecto antes),
   borrar esas `.meta` locales y volver a hacer pull: los GUID fijos de los
   prefabs deben ganar.
2. Compilación sin errores en Console (dominio + Sim + cáscaras).
3. Test Runner → EditMode → Run All: esperar 46/46 (ahora con NUnit real).
4. Abrir escena `Multiplayer` → Play → Start Host: debe aparecer la mesa con el
   microondas junto al spawn; HUD de herramientas abajo.
5. Mantener E sobre un tornillo con destornillador (tecla 2): la esfera pasa de
   rojo a verde en ~3 s y desaparece; con llave equipada, E sobre la carcasa la
   golpea (calidad baja en el HUD).
6. Segunda instancia (Paralell Sync o build): el cliente ve el mismo estado y
   puede disputar fijaciones (el host las arbitra).

## 6. Límites conocidos de esta etapa

- Sin maquinaria (trituradora/balanza/contenedor físicos): las ops económicas de
  la pieza extraída llegarán en la próxima etapa; el dominio YA las soporta.
- Sin modelos 3D definitivos (geometría primitiva a propósito, §3.5).
- Sin persistencia de escena: los objetos desmontables se rearman desde el
  bootstrap en cada sesión; `SaveGame` v3 guarda piezas/almacenes/dinero
  (Instrucción 05) y los ids de instancia son estables para reconstruir después.
- `NotifyRejected` al HUD no viaja por RPC: las pistas se derivan del estado
  sincronizado (más barato y suficiente).
