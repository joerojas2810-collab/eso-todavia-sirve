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
- **Nuestro, nuevo**: `Assets/_ETSV/**` — `SessionConfig`, dominio puro portado desde
  el prototipo JS (`ETSV.Domain.asmdef`: catálogo, desarmado, piezas, almacenes,
  economía, persistencia), capa de simulación host-autoritativa (`ETSV.Sim`:
  `DisassemblySessionHost`, `ScrapyardSession`, gates, sincronización) y capa de
  integración Unity (`Integration/`: bootstrap runtime, views NetworkBehaviour,
  layout de mesa y de las 14 estaciones de la chatarrería, HUD). Todo se construye
  en runtime: **la escena y los prefabs upstream no se modifican**.

## 5. Tests

Window → General → **Test Runner** → EditMode → Run All.
Hoy: 62 tests EditMode — smoke del dominio, **paridad 1:1 con los 73 tests JS del
prototipo**, 13 de sesión de desmontaje host-autoritativa y 16 de economía
cooperativa (venta simultánea, procesado duplicado, desconexión durante
transporte, residuos especiales, late-join…). En este entorno se ejecutan bajo
Mono (62/62 PASS); en el editor corren con NUnit real.

## 6. Estado

Instrucciones 04–07 completas en código: base integrada, dominio portado,
desmontaje físico y economía cooperativa conectados con autoridad del host
(`docs/INTEGRACION.md` §7–§8, `INTEGRACION_DESARMADO.md`,
`INTEGRACION_ECONOMIA.md`). Pendientes que **requieren el editor Unity** y no
pudieron verificarse en este entorno: compilación real (aquí se compiló contra
stubs), físicas, replicación entre clientes, 7 jugadores en runtime, spawns
visuales 5–7 y las checklists de editor de ambas instrucciones.
