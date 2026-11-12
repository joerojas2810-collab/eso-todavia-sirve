# Informe de auditoría y estrategia de reutilización técnica

**¡ESO TODAVÍA SIRVE!** · Instrucción 03 · 2026-11-12
Estado: **conclusiones para aprobación** — no se migró nada, no se compró nada,
el repositorio y los cinco GDD quedaron intactos (este archivo es la única adición).
Todo dato de este informe fue verificado hoy contra GitHub API (licencias, versiones,
código) y las páginas oficiales de cada paquete (precios y disponibilidad).

---

## 1. Auditoría del proyecto actual (v0.1–v0.3)

Inventario medido: **2.361 líneas de código propio** (+ 53.044 de three.js vendor,
+ 208 de HTML, + 231 de tests).

### A. Lógica propia que DEBEMOS conservar (≈ 885 líneas, portables a C# casi 1:1)

Puras (sin DOM, sin three.js, testeables en Node → NUnit sin cambios de concepto):

| Archivo | Líneas | Contenido |
|---|---|---|
| `js/systems/economy.js` | 262 | Operaciones atómicas (extraer/procesar/vender/reservar/consumir) con validaciones previas |
| `js/systems/disassembly.js` | 141 | Máquina de estados del desmontaje, fijaciones con progreso, calidad/daño |
| `js/data/catalog.js` | 136 | Blueprint del microondas (componentes, recetas, dependencias) |
| `js/systems/pieces.js` | 114 | Identidad StoredPart e invariantes (storage único, terminal excluyente) |
| `js/systems/warehouse.js` | 86 | Zonas con capacidad/categoría + acumulador fraccionario de materiales |
| `js/data/economyData.js` | 84 | Fuente única económica (MAT-01..08, PART_DEFS, P1–P5, PROV) |
| `js/state/gameState.js` | 62 | Save versionado (dinero + piezas + almacenes) |
| `tests/*.mjs` | 231 | 73 aserciones (20 desmontaje + 53 economía) |

Este es **el juego**: reglas GDD 3.0/4.0/5.0 ya depuradas. En Unity se convierten en
clases puras C# + ScriptableObjects, con los tests portados a NUnit (EditMode).

### B. Implementaciones de librerías externas (NO se portan, se reemplazan)

- `vendor/three.module.js` — three.js (MIT). En Unity lo reemplaza el motor de
  renderizado/física nativo. **Cero valor de port.**

### C. Sistemas genéricos que estábamos reconstruyendo innecesariamente

El prototipo reimplementó a mano cosas que una base Unity ya trae probada:
movimiento FPS, agarre/lanzamiento físico, cámara, guardado, y (futuro) netcode.
**Decisión: no portar nada de esto desde JS**; se adopta base existente (§2/§4).

### D. Código de navegador que habría que reescribir de todos modos (≈ 1.684 líneas)

| Archivo | Líneas | Equivalente Unity |
|---|---|---|
| `js/main.js` | 1.351 | Input System + Física/NGO + zonas `IInteractable` + UI Toolkit/uGUI (HUD) + AudioManager (el audio sintetizado WebAudio se descarta) |
| `index.html` | 208 | UI Toolkit/uGUI |
| `js/world/disassemblyView.js` | 125 | Prefabs por blueprint + `DisassemblyView.cs` |

Conclusión de la auditoría: **~37% del prototipo (reglas puras + datos + tests) es el
activo migrable; ~63% es andamiaje de navegador que Unity y las bases existentes ya
resuelven mejor.** El prototipo cumplió su función (validar reglas y loop) y queda
congelado como referencia ejecutable de las reglas.

## 2. Bases técnicas evaluadas (verificadas vía GitHub API, 2026-11-12)

### 2.1 `Unity-Technologies/U6-First-Person-Multiplayer` ⭐ candidata a BASE

- **Existe y activo**: push 2026-08-21, 35 stars, rama `main`, no archivado.
- **Licencia**: Unity Companion License (leída del `LICENSE.md`) → uso comercial
  permitido en proyectos hechos con Unity. Ojo: es *reference sample*, sin soporte
  (no aceptan PRs/issues).
- **Unity**: `6000.3.5f2` (ProjectVersion.txt). **Dependencias** (manifest.json):
  NGO **2.12.0**, `services.multiplayer` 2.2.3 (Relay/Lobby), Vivox 16.11.0,
  Input System 1.17.0, URP 17.3.0, Cinemachine 3.1.3, Probuilder, Multiplayer PlayMode.
- **Sistemas reales** (17 scripts en `Assets/Scripts`, listados del árbol git):
  movimiento FPS (CharacterController: walk/sprint/jump con coyote+buffering, slam),
  **pickup/throw de objetos físicos server-authoritative** (`PickupItem.cs`),
  flujo Lobby+Relay con UI (`LobbyController`, `RelayManager`), aprobación de
  conexión y spawn points (`PlayerSpawnManager`), voz posicional Vivox
  (`ProximityVoice`), GameManager con NetworkVariable, escena singleplayer aparte.
- **1–7 jugadores**: el README dice "up to 4", pero el código verificado muestra que
  el cap es `_nextSpawnIndex < _spawnPoints.Length` → **ampliable a 7 agregando spawn
  points** (+ paleta `PlayerColour` de 4 colores → 7). Relay free tier: 50 CCU
  simultáneos (investigación previa), suficiente.
- **Reutilizable directo**: los 17 scripts + configuración de paquetes + escena
  Multiplayer. **Adaptación**: cap 7, colores, y conectar `PickupItem` con nuestro
  `PieceRegistry`. **No conviene copiar**: su arte gris y la escena de demo.

### 2.2 `Unity-Technologies/com.unity.multiplayer.samples.coop` (Boss Room)

- **Existe y muy activo**: push 2026-09-13, 2.005 stars. Licencia: Unity Companion
  License (LICENSE.md leído).
- **Unity**: `6000.0.52f1` LTS · NGO **2.4.3** · `services.multiplayer` 1.1.4 +
  Authentication 3.5.1 · URP 17.0.4.
- **Jugadores**: `const int k_MaxPlayers = 8` verificado en
  `MultiplayerServicesInterface.cs`. ✔ compatible con 1–7.
- **Es un RPG top-down, NO un FPS** → no sirve como base de juego.
- **Reutilizable**: su **paquete Utilities** instalable por git URL
  (`com.unity.multiplayer.samples.coop` en manifest) y sus *patrones* netcode
  (acciones que enmascaran latencia, objetos replicados, sesiones MPS,
  connection management). **No conviene copiar**: gameplay, arte, personajes.

### 2.3 `stevenharmongames/Unity-FirstPersonInteractionToolkit`

- **Existe**: push 2025-05-23, 76 stars. **Licencia MPL-2.0** (LICENSE leído):
  comercial OK, pero los *archivos modificados* deben seguir MPL (copyleft por
  archivo) → overhead legal evitable.
- **Unity 2022.3.19f1** (no Unity 6), **sin multiplayer**, Input Manager viejo y
  Post-processing stack v2.
- **Sistemas** (18 scripts): interacción modular (pickup/inspect/throw con retorno),
  puertas físicas, cajones, ascensor, válvula, escalera, teleport, trampolín.
- **Veredicto**: valioso como **referencia de diseño** de interacciones (puertas de
  galpón, cajones del almacén). Portar sus scripts a Unity 6 + Input System + NGO
  cuesta más que reimplementar los 3–4 patrones que nos interesan sobre la base U6.
  **No adoptar como base.**

### 2.4 `bababuyyy/unity-fps-gameplay-systems`

- **Existe**: push 2026-04-13, 7 stars, 1 fork (madurez baja). **Licencia MIT**
  (LICENSE leído, © 2026) → la más permisiva.
- **Solo `Assets/`** (sin ProjectSettings/Packages): se integra como carpeta en
  nuestro proyecto. Requiere Unity 6 (6000.x) + Input System (README).
- **Sistemas** (17 scripts): movimiento **Rigidbody** (walk/sprint/crouch/vault),
  cámara con bob/FOV/anti-clip, **viewmodel de brazos**, escalada libre, **agarre
  físico con spring-force** (anti-jitter), framework `IInteractable` + door/item/
  physics-item, sistema de salud por trauma (WIP, no nos sirve).
- **Conflicto arquitectónico**: su movimiento es Rigidbody; el de U6 es
  CharacterController **networked**. Mezclarlos duplicaría el sistema de movimiento.
- **Reutilizable selectivamente**: `IInteractable` (contrato limpio para balanza/
  trituradora/contenedores), `ViewmodelController` + `CameraController` (solo visual,
  sin red), sensaciones del `PlayerGrab` como referencia de tuning. **No copiar**:
  movimiento/escalada/salud.

## 3. Recursos gráficos (verificados hoy en páginas oficiales)

| Paquete | Estado | Licencia | Contenido verificado |
|---|---|---|---|
| **ithappy — Junkyard Low Poly 3D Models Pack** (Asset Store #256375) | Disponible, **$89** (Single Entity; la oferta de ~$40 de la investigación anterior ya no está) | Asset Store EULA (comercial OK; modificar OK; redistribuir raw NO) | **290 assets**, demo scene lista, 6000.0.28f1+ / URP-HDRP-Built-in, FBX/GLTF/OBJ/Blender, colisiones y animaciones. Maquinaria: **forklift, grúa de garra, grúa magnética, prensa de autos, shredder**. 37 vehículos chocados. Edificios: salvage, **sheds, warehouses**, cabins, water towers. Props: **contenedores, barriles, palés, fences, neumáticos, caños, chatarra**. Dependencia: Dreamteck Splines (FREE) |
| **Batya Studios — Low Poly Junkyard** (#97213) | Disponible, **FREE**, 21,4 MB | Asset Store EULA estándar | 51 modelos estáticos + 1 animado (bañera, autos, bus, heladera, TV, lavavajillas, palés, racks, contenedores cargo). Arte de 2020 (Unity 4 original) → **solo graybox** |
| **Kenney — City Kit (Industrial)** | Disponible, gratis (donación opcional), v2.0 (2025) | **CC0** (verificado en la página del pack) | 40 archivos: fábricas, **warehouses**, energía solar/eólica, con variaciones. Además útiles: Crosshair Pack, Input Prompts, UI y audio (todo CC0) |
| **Quaternius** | Disponible, gratis (Patreon opcional) | **CC0** (verificado en página de pack) | **Ultimate Animated Character Pack**: 52 personajes animados (FBX/OBJ/Blend), comercial OK. También Universal Base Characters y Modular Outfits para operarios customizables |

### Cobertura por necesidad del juego

| Necesidad | Cubierta por | Cobertura |
|---|---|---|
| Patio y galpones | ithappy (demo scene, warehouses, sheds) + Kenney Industrial (CC0) | **Completa** |
| Maquinaria industrial (trituradora, grúas, prensa) | ithappy (shredder, claw/magnet crane, car press, forklift) | **Completa** |
| Contenedores y almacenamiento | ithappy (containers, barrels, pallets) + Batya (racks, cargo) | **Completa** |
| Vehículos | ithappy (37 wrecked cars, buses, pickups) | **Completa** |
| Personajes | Quaternius (52 animados, CC0, retargetables) | **Completa** (chibi/stylized acorde al pilar visual) |
| Herramientas | Quaternius Sci-Fi guns no aplica; ithappy props parciales | **Parcial** → kitbash o modelar 3–4 herramientas simples (bajo esfuerzo) |
| Efectos/decoración | Kenney (partículas, UI, crosshairs) + ithappy props | **Completa** |
| **Objetos desmontables (los 30 del GDD)** | **NINGÚN paquete** | **Brecha real — ver abajo** |

### La brecha crítica: modelos visuales ≠ objetos desmontables

Verificado concepto por concepto: una heladera de ithappy es **una sola malla**.
Nuestro sistema exige que cada objeto sea un blueprint con **componentes separados**
(microondas = 6 meshes: carcasa, puerta, panel, plato, motor, módulo). Ningún paquete
del mercado trae electrodomésticos modularizados para desmontaje.

Estrategia recomendada (aprobación pendiente):
1. **Entorno = 100% paquetes** (ithappy compra única $89 + Kenney/Quaternius CC0).
2. **Objetos desmontables = producción propia asistida**: kitbash en Blender a partir
   de props CC0/Asset Store (las licencias permiten modificar para el juego propio) +
   plantillas de componentes reutilizables entre objetos (motor, panel, puerta, carcasa
   cambian de escala/color). Los 30 objetos del GDD comparten arquetipos: ~8 familias
   de componentes cubren la mayoría.
3. Los objetos NO desmontables del decorado (autos chocados, pilas de chatarra) sí
   salen directo de ithappy: son escenografía, no blueprints.

## 4. Arquitectura de reutilización propuesta (Unity 6)

```
BASE = U6-First-Person-Multiplayer (Unity 6000.3.5f2, Unity Companion License)
 ├─ FPS movement + camera ............ tal cual (CharacterController networked)
 ├─ Pickup/throw server-authoritative  tal cual → se conecta a PieceRegistry
 ├─ Lobby + Relay + Vivox ............ tal cual, cap 4 → 7 (spawn points + colores)
 └─ GameManager/Approval ............. tal cual
MEJORAS SELECTIVAS (MIT, bababuyyy)
 ├─ IInteractable .................... contrato para balanza/trituradora/contenedor/almacén
 ├─ ViewmodelController + bob/FOV .... capa visual (sin red)
 └─ (movimiento Rigidbody, escalada, trauma: NO se adoptan)
PATRONES (Boss Room, Unity Companion License)
 └─ Paquete Utilities por git URL + reference de acciones/latencia y sesiones MPS
NUESTROS SISTEMAS (port puro de JS → C#, sin nada de navegador)
 ├─ Datos: economyData.js/catalog.js → ScriptableObjects
 │   (MaterialSO, PartDefSO, MachineSO, StorageZoneSO, ObjectBlueprintSO, ComponentSO)
 ├─ Reglas: pieces/warehouse/economy/disassembly → clases puras C# (sin UnityEngine
 │   en el dominio; mismas validaciones atómicas) + EconomyContext
 ├─ Red: cada op atómica = ServerRpc del cliente → el HOST ejecuta la misma función
 │   JS-portada y sincroniza resultado (NetworkVariable/ClientRpc). El diseño ctx de
 │   la v0.3 ya anticipa esto: la UI nunca muta estado.
 ├─ Estado: gameState.js → save JSON del host (misma versión/schema)
 ├─ Vista: DisassemblyView.cs genera prefabs por blueprint (meshes por componente)
 └─ Tests: 73 aserciones .mjs → NUnit EditMode (paridad 1:1 como criterio de port)
ARTE
 ├─ ithappy Junkyard ($89, aprobación pendiente) → patio, galpones, maquinaria, vehículos
 ├─ Kenney CC0 → complementos, UI, crosshairs · Quaternius CC0 → personajes animados
 └─ Kitbash propio Blender → los 30 objetos desmontables por componentes
```

**Qué NO se migra del prototipo**: renderer three.js, HUD DOM, WebAudio, física de
props a mano, pointer lock. **Qué se migra**: exclusivamente reglas, datos y tests
(capas 1–3), que es donde vive el diseño del juego.

## 5. Matriz de decisión

| Sistema | Proyecto/paquete reutilizable | Licencia | Verificación | Adaptaciones necesarias | Decisión recomendada |
|---|---|---|---|---|---|
| Movimiento FPS + cámara | U6-FPM `PlayerMovement.cs` | Unity Companion | ✔ API+README+manifest | Ninguna | **ADOPTAR** |
| Pickup/throw networked | U6-FPM `PickupItem.cs` | Unity Companion | ✔ árbol git | Enlazar con PieceRegistry (uid en NetworkVariable) | **ADOPTAR** |
| Lobby/Relay/voz | U6-FPM `LobbyController`/`RelayManager`/`ProximityVoice` | Unity Companion | ✔ manifest (services 2.2.3, Vivox 16.11) | Cap 4→7; Vivox opcional (quitar si no se quiere voz) | **ADOPTAR** |
| Spawn/aprobación | U6-FPM `PlayerSpawnManager` | Unity Companion | ✔ código leído (`_spawnPoints.Length`) | 7 spawn points + 7 colores | **ADOPTAR** |
| Patrones netcode/sesiones | Boss Room Utilities (git URL) | Unity Companion | ✔ README + `k_MaxPlayers=8` en código | Usar selectivamente | **REFERENCIA + UTILITIES** |
| Interacciones single-player (puertas, cajones) | Harmony Toolkit | MPL-2.0 | ✔ LICENSE + 18 scripts listados | Portar a Unity 6 + Input System + NGO (o reimplementar patrones) | **SOLO REFERENCIA** (evitar carga MPL) |
| Interactables + viewmodel + feel de grab | bababuyyy | MIT | ✔ LICENSE + README + 17 scripts | Separar lo visual de lo físico; descartar trauma/climb | **ADOPTAR SELECTIVO** |
| Desmontaje (estados, fijaciones, calidad) | Nuestro `disassembly.js` | MIT (repo propio) | ✔ 20/20 tests | Port a C# + NUnit | **PORTAR** |
| Identidad de piezas / almacenes / economía | Nuestro `pieces/warehouse/economy/gameState.js` | MIT (repo propio) | ✔ 53/53 tests | Port a C# + SO de datos + ServerRpc host | **PORTAR** |
| Datos (materiales, precios, catálogo) | Nuestro `economyData.js/catalog.js` | MIT (repo propio) | ✔ fuente única | Convertir a ScriptableObjects (script de conversión) | **PORTAR** |
| Renderer/física/HUD/audio del prototipo | three.js + DOM | MIT | ✔ | — | **DESCARTAR** (Unity nativo) |
| Patio, galpones, maquinaria, vehículos, contenedores | ithappy Junkyard #256375 | Asset Store EULA | ✔ página oficial: $89, 290 assets, Unity 6000/URP | Demo scene → nuestro layout; + Dreamteck Splines (FREE) | **COMPRAR (aprobación)** |
| Graybox alternativo | Batya #97213 | Asset Store EULA | ✔ FREE, 51 modelos | Arte 2020 | **OPCIONAL** (solo prototipado) |
| Complementos industriales, UI, crosshairs | Kenney City Kit Industrial etc. | CC0 | ✔ página oficial (40 files, v2.0) | Ninguna | **ADOPTAR** |
| Personajes animados | Quaternius Ultimate Animated Characters | CC0 | ✔ página oficial (52 modelos FBX/OBJ/Blend) | Retarget + colores de jugador (7) | **ADOPTAR** |
| Objetos desmontables (30 del GDD) | **Ninguno existe** | — | ✔ revisión de los 4 catálogos | Kitbash Blender + plantillas de componentes | **PRODUCCIÓN PROPIA** |
| Herramientas (llave, destornillador…) | Parcial en props | CC0/EULA | ✔ | Modelar 3–4 simples | **PRODUCCIÓN PROPIA (menor)** |

## 6. Secuencia de implementación propuesta (tras aprobación)

1. **Decisiones**: versión de editor (6000.3.5f2 de la base vs LTS 6000.0.x — ver §7),
   compra ithappy ($89), Vivox sí/no.
2. **Repo Unity**: carpeta `unity/` en este repo (prototipo JS congelado intacto);
   importar U6-FPM; abrir con Unity 6; validar escenas Singleplayer y Multiplayer.
3. **Cap 1–7**: 7 spawn points, 7 colores, lobby maxPlayers=7; probar con 2–3
   instancias (Multiplayer PlayMode ya viene en el manifest).
4. **Datos → ScriptableObjects**: script de conversión desde `economyData.js`/
   `catalog.js` (JSON intermedio). Cero valores re-escritos a mano.
5. **Reglas → C# puro**: `PieceRegistry`, `Warehouse`, `Economy`, `DisassemblyInstance`
   + **73 tests NUnit en verde** = criterio de paridad con el prototipo.
6. **Networking de economía**: `EconomyServerRpc` (cliente pide → host valida con las
   mismas funciones → sincroniza dinero/almacenes por NetworkVariable).
7. **Interactables**: balanza, trituradora, contenedor rojo y almacén como zonas
   `IInteractable` que llaman ops del host (patrón bababuyyy adaptado a NGO).
8. **Primer objeto desmontable en Unity**: microondas (6 componentes, meshes por
   kitbash) + `DisassemblyView.cs` data-driven; validar loop completo en red.
9. **Arte de entorno**: ithappy demo scene → patio definitivo; personajes Quaternius.
10. **Save del host + progresión**; recién después: contratos y objetos 02–30.

## 7. Riesgos y decisiones pendientes

- **Versión de editor**: la base usa 6000.3.5f2 (rama más nueva); Boss Room usa
  6000.0.52f1 LTS. Recomendación: **quedarse en 6000.3.5f2** (la base abre directo,
  NGO 2.12 es más actual); alternativa conservadora: LTS 6000.0.x bajando NGO a 2.4.
- **Unity Companion License**: permite uso comercial en proyectos Unity; no requiere
  abrir nuestro código. MPL de Harmony es la única licencia con obligaciones de
  compartición (por eso: solo referencia).
- **Muestras de Unity sin soporte**: U6-FPM no acepta issues; el mantenimiento del
  fork es nuestro.
- **ithappy $89**: precio verificado hoy (sin descuento activo); Single Entity cubre
  individuo/estudio chico. Dependencia Dreamteck Splines es FREE.
- **Vivox**: voz posicional ya integrada, pero suma dependencia de UGS; se puede
  desactivar sin romper el resto.
- **Arte desmontable**: es el único trabajo de producción grande inevitable
  (~8 familias de componentes × kitbash); ninguna compra lo resuelve.
