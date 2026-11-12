# Estado de integración — Instrucción 04

Registro de qué funciona, qué se modificó, qué se verificó sin editor y qué
**requiere el editor Unity** para validarse. Fecha: 2026-11-12.

## 1. Procedencia de cada funcionalidad

| Funcionalidad | Origen | Estado |
|---|---|---|
| Movimiento FPS (walk/sprint/jump coyote+buffer, slam, camera look) | U6-FPM `PlayerMovement.cs` | **Sin tocar** |
| Pickup/hold/rotate/throw de objetos físicos, server-authoritative | U6-FPM `PickupItem.cs` + `PlayerInteraction.cs` | **Sin tocar** |
| Lobby UI + flujo Host/Join/Start, host local automático | U6-FPM `LobbyController.cs`, `LobbyUI.cs`, `GameManager.cs` | **Modificación menor** (Vivox gated) |
| Relay + anonymous auth | U6-FPM `RelayManager.cs` | **Modificado** (cap 7, Vivox gated) |
| Aprobación de conexiones + spawn | U6-FPM `PlayerSpawnManager.cs` | **Modificado** (cap 7 + fallback) |
| Identificación visual de jugador | U6-FPM `PlayerColour.cs` | **Modificado** (ciclo de paleta) |
| Voz posicional Vivox | U6-FPM `ProximityVoice.cs` | **Desactivada** (dependencia presente) |
| Parámetros de sesión (7 jugadores, voz off) | **Nuestro** `_ETSV/Scripts/Integration/SessionConfig.cs` | Nuevo |
| Dominio puro (assembly sin UnityEngine) | **Nuestro** `_ETSV/Scripts/Domain/` | Nuevo (esqueleto) |
| Tests EditMode | **Nuestro** `_ETSV/Tests/EditMode/` | Nuevo (3 smoke tests) |
| Reglas de juego (desmontaje, piezas, economía, almacenes, save) | Prototipo JS (`../prototype/js`) | **Pendiente de port** (próxima instrucción) |
| Contratos, progresión, objetos 02–30, robots | GDD | Fuera de alcance por instrucción |

## 2. Cambios realizados (todos marcados `// [ETSV]` en el código)

1. **`ProjectSettings.asset`**: `productName: "¡ESO TODAVÍA SIRVE!"`,
   `companyName: Chatarreria El Recorte` (nombre del negocio según GDD 1.0).
2. **`RelayManager.cs`**: `private const int MaxPlayers = 4` →
   `SessionConfig.MaxPlayers` (= 7, fuente única en `Etsv.Domain.GameInfo`).
   La asignación Relay se crea con `MaxPlayers - 1 = 6` conexiones (el host completa 7).
   Login/logout de Vivox solo si `SessionConfig.VoiceEnabled` (hoy `false`).
3. **`LobbyController.cs`**: cleanup de Vivox en transport-failure condicionado al flag.
4. **`ProximityVoice.cs`**: `OnNetworkSpawn` y `Update` retornan inmediatamente con la
   voz desactivada; el componente y el prefab quedan intactos para reactivarla.
5. **`PlayerSpawnManager.cs`**: la aprobación ya no depende de `_spawnPoints.Length`
   (la escena trae 4 puntos) sino de `SessionConfig.MaxPlayers`. Jugadores 5–7 spawnean
   en posiciones **procedurales deterministas** (anillo de radio 2.5 m alrededor del
   spawn 0) hasta que la escena tenga 7 puntos propios.
6. **`PlayerColour.cs`**: `index % _materials.Length` — con la paleta upstream de 4
   materiales, los jugadores 5–7 reciclan colores hasta agregar 3 materiales nuevos
   al prefab. La identidad de jugador (clientId, ownership, PlayerIndex) no depende
   del color: es solo cosmético.
7. **Nuevos**: `_ETSV/` (SessionConfig, dominio puro con `GameInfo`, asmdefs, tests).

**No se modificaron**: escenas (`.unity`), prefabs, manifest/paquetes, input, URP,
ni ningún otro script upstream. Verificado por diff contra el clone del commit
`a69cb8d`.

## 3. Verificado en este entorno (sin editor Unity)

- ✅ Commit upstream exacto incorporado; `Assets/Packages/ProjectSettings` idénticos
  al clone salvo los 6 archivos de la sección anterior (diff automatizado).
- ✅ `ProjectVersion.txt` = `6000.3.5f2` (decisión Instrucción 04 #1).
- ✅ `manifest.json` intacto: NGO 2.12.0, services.multiplayer 2.2.3, Vivox 16.11.0,
  Input 1.17.0, URP 17.3.0 (Vivox presente = decisión #4 cumplida a nivel dependencia).
- ✅ Búsquedas textuales: no queda ningún `MaxPlayers = 4`; `VivoxService` solo se
  invoca detrás de `SessionConfig.VoiceEnabled` o del flag de ProximityVoice.
- ✅ Balance de llaves/paréntesis de los .cs editados (chequeo automático).
- ✅ asmdefs JSON válidos; `ETSV.Domain` con `noEngineReferences: true` (pureza
  garantizada por el compilador cuando abra el editor).
- ✅ `prototype/` y `docs/` (GDD) sin cambios (`git status`).

## 4. Pendiente de verificación en el editor Unity (checklist)

Abrir el proyecto con 6000.3.5f2 y:

- [ ] Compilación sin errores (Console limpia); Unity genera los `.meta` de `_ETSV/**` → commitearlos.
- [ ] Test Runner → EditMode → Run All: 3/3 verdes (`DomainSmokeTests`).
- [ ] Escena `Multiplayer`: host local → jugar (movimiento + pickup/throw intactos).
- [ ] **7 jugadores reales**: con Multiplayer PlayMode (2–3 instancias) y/o Relay +
      build de Windows en varias PCs: hostear, unir con 6 clientes, confirmar que la
      aprobación deja entrar al 7.º y rechaza al 8.º, spawns sin superposición y
      ownership correcto de objetos lanzados.
      **Hasta completar este paso, el soporte de 7 jugadores es "adaptado en código",
      NO validado en runtime** (requisito explícito de la Instrucción 04 §3).
- [ ] Reemplazar el fallback procedural: agregar 7 GameObjects "Player N Spawn" a la
      escena Multiplayer y asignarlos a `_spawnPoints` (Inspector).
- [ ] Agregar 3 materiales de cabeza (colores 5–7) al prefab del jugador y asignarlos
      a `PlayerColour._materials`.
- [ ] Vivox: confirmar en Console que NO hay login ni canales con `VoiceEnabled=false`;
      al activarlo en el futuro, requiere vincular el proyecto Unity Cloud con Vivox.
- [ ] Relay: vincular Services y probar join code entre dos máquinas distintas.

## 5. Procedimiento para actualizar la base upstream

1. `git clone` del upstream y checkout del tag/commit nuevo.
2. Copiar `Assets/`, `Packages/`, `ProjectSettings/` sobre `unity/` **excepto** los 6
   archivos modificados (lista en §2) y `Assets/_ETSV/`.
3. Re-aplicar los cambios `[ETSV]` (son pocos y están marcados; el diff histórico de
   este repo muestra exactamente qué tocar: `git log --follow` por archivo).
4. Correr la checklist §4 otra vez.

## 6. Escenario / chatarrería

La escena Multiplayer upstream queda como **espacio de trabajo funcional** (decisión:
no modelar nada a mano en esta instrucción). Plan de arte aprobado para cuando
corresponda: Kenney City Kit Industrial (CC0) + Quaternius (CC0) para personajes;
ithappy Junkyard ($89) **en espera** (decisión Instrucción 04 #3); la demo scene de
ithappy reemplazaría este espacio cuando se compre. Los objetos desmontables serán
producción propia (kitbash) — fuera del alcance actual.

## 7. Instrucción 06 — desmontaje físico integrado

El sistema de desmontaje ya está conectado al mundo físico y a la red
host-authoritative (microondas del GDD como primer objeto). **No hace falta
modelar ni colocar nada en la escena a mano**: `ETSVBootstrap` registra los
prefabs de `Assets/_ETSV/Resources/ETSV/` y el host spawnea mesa + microondas
junto al spawn point al iniciar. Detalles, sistemas reutilizados de la base,
adaptaciones y checklist de verificación en el editor:
[`INTEGRACION_DESARMADO.md`](INTEGRACION_DESARMADO.md).
