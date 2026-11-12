# ¡ESO TODAVÍA SIRVE! — proyecto Unity 6

Juego definitivo (el prototipo web de `../prototype/` queda congelado como referencia
ejecutable de las reglas). Base técnica: **U6-First-Person-Multiplayer** de Unity
Technologies, incorporada en el commit `a69cb8d70da855fa8396f0d37068a8470430f640`
(2026-08-20) bajo **Unity Companion License** — ver `ATTRIBUTION.md`.

## 1. Cómo abrirlo (Unity Hub)

1. Instalá **Unity Hub**: https://unity.com/download
2. Instalá el editor **Unity 6000.3.5f2** (exacto; ver `ProjectSettings/ProjectVersion.txt`):
   Hub → Installs → Install Editor → 6000.3.5f2.
   Módulos sugeridos: ninguno obligatorio para Windows; si apuntás a otras plataformas,
   agregá los módulos correspondientes.
3. Cloná el repo y abrí **esta carpeta** (`unity/`) desde el Hub: Add project from disk.
4. Primera apertura: Unity importa y genera los `.meta` de los archivos nuevos
   (`Assets/_ETSV/**`) — es esperado; commitealos después.
5. Para multiplayer con Relay: vincular el proyecto a un Unity Cloud project
   (Edit → Project Settings → Services) con **Relay** habilitado. Sin eso, la escena
   Multiplayer funciona igual en **host local** (solitario o LAN/IP directa).

## 2. Escenas

| Escena | Uso |
|---|---|
| `Assets/Scenes/Singleplayer.unity` | Movimiento, pickup/throw y cámara sin red |
| `Assets/Scenes/Multiplayer.unity` | Flujo completo: lobby UI → host local/Relay → spawn autoritativo |
| `Assets/Scenes/SampleScene/` | Muestra del template (upstream) |

El flujo de lobby arranca **host local automático** (P1 spawnea al toque) y ofrece
Host (Relay + join code) / Join / Start — comportamiento upstream intacto.

## 3. Dependencias (del manifest upstream, sin cambios)

| Paquete | Versión | Rol |
|---|---|---|
| com.unity.netcode.gameobjects | 2.12.0 | Networking (NGO) host-authoritative |
| com.unity.services.multiplayer | 2.2.3 | Relay + Lobby |
| com.unity.services.vivox | 16.11.0 | Voz posicional — **instalada pero desactivada** (`SessionConfig.VoiceEnabled = false`, decisión Instrucción 04 #4) |
| com.unity.inputsystem | 1.17.0 | Input |
| com.unity.render-pipelines.universal | 17.3.0 | URP |
| com.unity.cinemachine | 3.1.3 | Cámara |
| com.unity.multiplayer.playmode | 2.0.2 | Probar multi-instancia en el editor |
| com.unity.ai.navigation, probuilder, timeline, ugui, visualscripting, etc. | (ver manifest) | Soporte del template |

## 4. Qué es de quién (resumen; detalle en `docs/INTEGRACION.md`)

- **Upstream sin tocar**: movimiento FPS (`PlayerMovement`), pickup/throw networked
  (`PickupItem`, `PlayerInteraction`), `GameManager`, UI de lobby/HUD/pausa, escenas,
  prefabs, arte del template, configuración de paquetes.
- **Upstream modificado (marcas `// [ETSV]` en el código)**: `RelayManager` (cap 4→7
  vía `SessionConfig`, Vivox gated), `LobbyController` (Vivox gated), `ProximityVoice`
  (no inicializa con voz desactivada), `PlayerSpawnManager` (aprueba hasta 7; spawns
  5–7 con fallback procedural en anillo), `PlayerColour` (paleta cicla si faltan
  materiales 5–7), `ProjectSettings.asset` (nombre del producto).
- **Nuestro, nuevo**: `Assets/_ETSV/**` — `SessionConfig`, dominio puro
  (`ETSV.Domain.asmdef`, `GameInfo`) y tests EditMode (`ETSV.Domain.Tests`).
  Ahí se portarán las reglas del prototipo JS (próxima instrucción).

## 5. Tests

Window → General → **Test Runner** → EditMode → Run All.
Hoy: 3 smoke tests del dominio puro (`DomainSmokeTests`). Cuando se porten las reglas
desde `prototype/js`, el criterio de aceptación es la **paridad 1:1 con los 73 tests JS**.

## 6. Estado

Integración de base completada (Instrucción 04). Pendientes que **requieren el editor
Unity** y no pudieron verificarse en este entorno: compilación, 7 jugadores en runtime,
spawns visuales 5–7, materiales 5–7. Detalle y checklist: `docs/INTEGRACION.md`.
