# ¡ESO TODAVÍA SIRVE! — Informe técnico de viabilidad
### Cómo acercarse a los gráficos de *Moon Sweepers* y qué bases ya existen para no empezar de cero

> Documento de investigación · v1 · 2026-11-12
> Referencias visuales incluidas en `refs/` (moodboards generados como objetivo de estilo) y capturas reales de Moon Sweepers en `image-search/` (descargadas de Steam/comunidad).

---

## 0. Resumen ejecutivo (TL;DR)

| Pregunta | Respuesta |
|---|---|
| ¿Qué motor usa Moon Sweepers? | **Unity** ([IndieDB](https://www.indiedb.com/games/moon-sweepers/downloads)), hecho por un estudio de **2 personas** de Finlandia ([windowsmode](https://www.windowsmode.com/moon-sweepers-for-windows/), [moonsweepers.org](https://moonsweepers.org/game/)). |
| ¿Es alcanzable ese look para un equipo chico? | **Sí, es de los estilos 3D más baratos que existen**: low-poly redondeado, shading casi plano, 1 luz direccional, sin outlines, paleta corta. El juego pesa ~2 GB y pide 8 GB de RAM: el techo técnico es deliberadamente bajo. |
| ¿Motor recomendado para ustedes? | **Unity 6 LTS + URP**. Todo el género "co-op physics comedy" (Moon Sweepers, Lethal Company, Content Warning, Schedule I) vive ahí: templates, netcode y packs de assets listos. |
| ¿Multiplayer 1–7 jugadores sin pagar servidores? | **Netcode for GameObjects + Unity Relay** (free tier: 50 CCU promedio/mes + 3 GiB por CCU hasta 150 GiB/mes) o **FishNet + Steam P2P** (gratis si publicás en Steam). Listen-server con host autoritativo. |
| ¿Hay base de código usable? | **Sí**: el sample oficial [`Unity-Technologies/U6-First-Person-Multiplayer`](https://github.com/Unity-Technologies/U6-First-Person-Multiplayer) ya trae: primera persona, **agarrar/soltar/lanzar objetos físicos con autoridad de servidor**, lobby con Relay y voz por proximidad. Es exactamente el esqueleto de su vertical slice. |
| ¿Hay base de arte usable? | **Sí**: pack *Junkyard – Low Poly* de ithappy (292 modelos: autos destrozados, **prensa de autos, trituradora/shredder, grúa de garra, grúa imán, forklift** = su loop completo), más packs CC0 gratis de Kenney/Quaternius para graybox. |
| Mayor riesgo técnico | La **física networked** (el "caos cooperativo"). Mitigación: host autoritativo + ownership transfer al agarrar + resto de props como física local cosmética. |

---

## 1. Reverse-engineering de Moon Sweepers: por qué ese look es "barato" (en el buen sentido)

Datos duros del juego de referencia:

- **Motor: Unity** ([IndieDB](https://www.indiedb.com/games/moon-sweepers/downloads)).
- **Equipo: 2 personas** (DragDrop Games, Finlandia) ([moonsweepers.org](https://moonsweepers.org/game/)).
- Lanzado el 23/oct/2026, US$ 8.99, co-op online hasta **16 jugadores**, Windows-only, ~2 GB de instalación, 8 GB RAM mínimo ([windowsmode](https://www.windowsmode.com/moon-sweepers-for-windows/), [SteamDB](https://steamdb.info/app/5335510/info/)).
- Recepción "Mayormente positivas" (~78% de +4.400 reseñas): el género tolera acabado indie si el loop es satisfactorio.

### Anatomía del estilo visual (mirando las capturas de `image-search/`)

1. **Low-poly redondeado, no "retro PS1"**: siluetas suaves y chunky (personajes chibi con cabezón, ~1–2k tris), vehículos blocky pero con bordes blandos. Nada de pixel-snapping ni dithering: es low-poly *limpio*.
2. **Materiales casi planos**: albedo sólido o con ruido/AO muy sutil, sin reflejos PBR llamativos. El "estilizado" viene de la **paleta y la forma**, no de shaders caros.
3. **Iluminación mínima**: 1 luz direccional con sombras suaves + cielo degradado/ambiente. Los interiores oscuros se resuelven con emisivos (visores, luces de máquinas) que además dan identidad ("ojos brillantes" = marca del juego).
4. **Sin outlines/toon lines**: shading limpio con leve color grading. (Si ustedes quisieran línea de contorno, es un pase extra de post-proceso con Sobel sobre depth+normals, pero Moon Sweepers *no* la usa: no la copien por copiar.)
5. **Legibilidad funcional por color**: naranja peligro / blanco / gris acero / acentos saturados. Cada objeto se entiende por silueta + color antes que por textura.
6. **Progreso visible en el mundo**: las franjas de polvo barrido son el "feedback" central (shader/decals con máscara por zona). En su chatarrería el equivalente es: pieza **sucia/oxidada → desarmada → limpia/vendida**, y pilas de chatarra que menguan.
7. **UI casual y juguetona**: tipografía redonda y gruesa, números gigantes (30.23K $), hotbar circular, badges de color. La UI hace el 30% del "feel" y cuesta poco hacerla bien.

**Conclusión**: el estilo es alcanzable porque *renuncia* a lo caro (texturas PBR 4K, GI dinámica, personajes realistas, VFX fotorrealistas) y *duplica* en lo barato (forma, color, luz plana, juice de UI). Un equipo de 2–4 personas puede producir este arte a ritmo industrial si fija budgets de tris y una paleta cerrada.

### Moodboards objetivo para SU juego
- `refs/moodboard_escena.png` — patio de chatarrería en el lenguaje visual de Moon Sweepers.
- `refs/moodboard_personajes_props.png` — trabajadores chibi + props de scrap (objetivo de personaje/prop, no arte final).
- Comparar contra capturas reales: `image-search/moon-sweepers-steam-gameplay-screenshot-1.jpg` y `-4.jpg`.

---

## 2. Stack técnico recomendado (y por qué)

### 2.1 Motor: Unity 6 LTS (6000.x) + URP

- **Es el motor del juego que quieren parecer** y de casi todo el género co-op de física ([Lethal Company usa Unity + NGO](https://unbaked.games/games/lethal-company/)).
- Ecosistema: templates oficiales de FPS multiplayer, packs de assets low-poly masivos, Shader Graph, Cinemachine, Input System, todo integrado.
- Licencia indie: Unity 6 sin runtime fee para ingresos < US$200k/año (verificar términos vigentes al firmar).
- **URP Forward+**: suficiente para este look (1 luz direccional + pocas luces puntuales emisivas + lightmaps horneados en el galpón).

**Alternativas consideradas:**

| Motor | Veredicto |
|---|---|
| **Godot 4** | Viable y 100% gratis, pero el ecosistema de *física networked* y de packs low-poly estilizados es mucho más chico; tendrían que construir más desde cero. Elegir solo si el equipo ya lo domina. |
| **Unreal 5** | Exagerado para este estilo: Nanite/Lumen no aportan nada al look flat-low-poly, y el P2P de physics comedy es más fricción. Descartado. |

### 2.2 Multiplayer: host autoritativo, 1–7 jugadores

Capas del stack (según el comparativo 2026 de [dev.to/gamedevtoollab](https://dev.to/gamedevtoollab/choosing-the-right-real-time-networking-stack-for-unity-in-2026-29f4)):

| Capa | Elección recomendada | Alternativa |
|---|---|---|
| Netcode | **Netcode for GameObjects (NGO) 2.x** — server-authoritative, integración Unity-first, docs excelentes | **FishNet 4.x** (gratis, source-available, client-side prediction madura) si quieren más control |
| Transporte | **Unity Relay** (P2P relay UDP, sin servidores propios). Free tier: **50 CCU promedio/mes gratis + 3 GiB/CCU hasta 150 GiB/mes**; luego US$0.16/CCU ([Unity Relay](https://activation.unity.cn/products/relay), [tech-insider 2026](https://tech-insider.org/unity-multiplay-relay-lobby-setup-2026/)) | **Steam P2P (SDR) vía Facepunch.Steamworks**: gratis si publican en Steam, sin límites de CCU ([r/Unity3D](https://www.reddit.com/r/Unity3D/comments/1crtz3y/best_p2p_option/)). Ideal como 2.º transporte o para post-lanzamiento |
| Lobby/matchmaking | **Unity Lobby** (códigos de acceso tipo "unite con amigos") | Steam Lobby API |
| Voz | **Vivox** (voz por proximidad 3D, ya cableada en el sample oficial) | Descartar en MVP; chat de radio con RPCs alcanza |

**Nota importante 2026**: Unity **descontinuó Multiplay Game Server Hosting** (1/abr/2026) ([edgegap](https://edgegap.com/blog/what-are-relays-servers-for-a-peer-to-peer-networking-guide)). Para este género *no importa*: el modelo correcto es **listen-server** (el host juega y simula), igual que Moon Sweepers y Lethal Company. No presupuesten dedicated servers.

**Reglas de oro para física networked (el corazón del "caos cooperativo"):**
1. El **host simula** todos los Rigidbodies "importantes"; los clientes interpolan.
2. Al **agarrar** un objeto: `ChangeOwnership` al jugador que lo sostiene (patrón del sample oficial `PickupItem.cs` y del prototipo [ArtDock-Coop](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission)); al soltar/lanzar, el dueño aplica el impulso y devuelve autoridad al host.
3. Todo lo demás (chispas, piezas decorativas que ruedan, ragdolls cómicos) = **física local cosmética**, no se replica: es gratis en ancho de banda y *suma* caos sin romper la simulación.
4. Tick 30–60 Hz para jugadores; props a 10–20 Hz con interpolación. Con 7 jugadores y ~300 props networked, Relay lo aguanta holgado dentro del free tier.

### 2.3 Resto del stack

| Área | Herramienta | Nota |
|---|---|---|
| Modelado | **Blender** (+ add-on **Cell Fracture** gratis para pre-fracturar piezas desarmables) | Pipeline low-poly estándar |
| Texturas | Colores planos + atlas; AO horneado por mesh; ruido procedural sutil en shader | Sin Substance si no hay artista dedicado |
| Audio | Unity Audio + packs SFX CC0 (freesound/Kenney); **FMOD** gratis bajo umbral de ingresos si crece | El "clank" de chatarra es 50% de la comedia |
| Persistencia | JSON/SQLite por chatarrería (guarda el host al cerrar sesión) + Steam Cloud | "Progresión persistente por chatarrería" del pitch |
| Destrucción | **Pre-fractura en Blender + joints que se rompen por fuerza** (host decide el break) | Más controlable y networkable que fractura runtime |

---

## 3. Receta gráfica concreta: "acercarse a esos gráficos"

### Budgets de arte (fijar como ley del proyecto)
| Asset | Tris | Texturas |
|---|---|---|
| Personaje jugador | 1.500–3.000 | 1 atlas 512–1K (cuerpo) + emisivo visor |
| Prop chico (latas, ruedas, puertas) | 100–600 | Atlas compartido del pack |
| Prop medio (motor, contenedor) | 600–1.500 | Atlas compartido |
| Máquina grande (prensa, shredder, grúa) | 2.000–5.000 | 1 atlas 1–2K propio |
| Vehículo jugable | 3.000–6.000 | 1 atlas 1–2K |

- **Paleta cerrada de 16–24 colores** compartida por todo el juego (vertex colors o atlas): cohesión instantánea estilo Moon Sweepers y draw calls baratos.
- **Shader URP Lit simplificado** (o Shader Graph): albedo de atlas + AO en alpha + emisivo por máscara; specular casi nulo; normal maps solo en héroes (máquinas).
- **Iluminación**: 1 Directional Light (sombras suaves, cascadas cortas) + Lightmap horneado en galpón/interiores + Light Probes en el patio + Skybox degradado. Emisivos para visores, paneles, peligros.
- **Post-proceso (URP Volume)**: SSAO/GTAO suave, Color Grading con LUT propia (levemente cálida/saturada), Bloom mínimo solo en emisivos, viñeta sutil. *Sin* outlines.
- **Progreso visible** (su versión de las "franjas limpias"): shader con máscara por pieza: estado A sucio/oxidado → B desarmado → C limpio; las pilas de scrap menguan con pooling. Esto es el principio 1 de su pitch ("actividad satisfactoria por sí misma") hecho shader.
- **Juice**: squash & stretch en animaciones, ragdoll al caer, screen-shake chico al golpear, partículas de polvo/chispas baratas (GPU particles), números de la UI que "popan" al vender. La comedia física vive acá.

### Checklist de parecido con Moon Sweepers
- [ ] Siluetas chunky y redondeadas (subdivisión + shrinkwrap suave en Blender, sin hard-surface filoso).
- [ ] Cabezas grandes / proporciones chibi en personajes; visor emisivo con "ojos".
- [ ] Colores por función (peligro naranja, estructura gris, acento crema).
- [ ] Sombras largas y suaves, cielo limpio.
- [ ] UI redonda, números enormes, hotbar circular.

---

## 4. Bases usables (lo que NO hay que escribir/modelar desde cero)

### 4.1 Base de código / gameplay ⭐

| Base | Licencia | Qué trae y por qué sirve |
|---|---|---|
| **[Unity-Technologies/U6-First-Person-Multiplayer](https://github.com/Unity-Technologies/U6-First-Person-Multiplayer)** (sample oficial Unity 6) | Unity Companion License | **LA base recomendada.** Primera persona (walk/sprint/jump), **pickup/hold/rotate/throw de Rigidbodies con autoridad de servidor** (`PickupItem.cs`, `PlayerInteraction.cs`), lobby + Relay hasta 4 jugadores (escalable a 7), voz por proximidad Vivox, NGO 2.x + URP 17 + Cinemachine 3. Es su vertical slice con el networking ya resuelto. |
| [com.unity.multiplayer.samples.coop (Boss Room)](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop) | Unity Companion | Patrones NGO de producción: pooling de NetworkObjects, acciones server-side (ver `TossAction.cs` para lanzar físicos), conexión por IP directa o Relay intercambiables. |
| [stevenharmongames/Unity-FirstPersonInteractionToolkit](https://github.com/stevenharmongames/Unity-FirstPersonInteractionToolkit) | **MPL-2.0** | Interacciones de "sim": puertas físicas, cajones que guardan objetos físicos, escaleras, ascensor, válvulas, highlight + crosshair dinámico. Ideal para el galpón y las máquinas. |
| [bababuyyy/unity-fps-gameplay-systems](https://github.com/bababuyyy/unity-fps-gameplay-systems) | **MIT** | Grab físico con spring-force + snap anti-jitter, throw con impulso y conservación de momentum, climb. El "feel" de comedia física ya debuggeado. |
| [FishNet](https://fish-networking.gitbook.io/) + sus demos | Source-available gratis | Si NGO se queda corto: client-side prediction probada, ownership flexible, transporte Steam/EOS. |

**Ruta sugerida**: clonar U6-First-Person-Multiplayer → reemplazar el props de prueba por chatarra → portar el grab/throw de `bababuyyy` si el feel no convence → agregar las interacciones del toolkit de stevenharmongames para máquinas/puertas.

### 4.2 Base de arte

| Pack | Precio/Licencia | Contenido relevante |
|---|---|---|
| **[Junkyard – Low Poly 3D Models Pack (ithappy)](https://assetstore.unity.com/packages/3d/environments/industrial/junkyard-low-poly-3d-models-pack-256375)** | US$ 89 (frecuente a ~US$ 39,50 en sales) | **292 modelos**: 37 autos destrozados, **forklift, grúa de garra, grúa imán, prensa de autos y SHREDDER** (¡su máquina de vender chatarra ya modelada!), galpones, contenedores, barriles, pallets, ruedas, puertas. Estética cartoon low-poly coherente, formatos Unity/Blender/FBX/GLTF. **Es el pack que más se acerca a su pitch completo.** |
| [Low poly junkyard models pack (Batya Studios)](https://assetstore.unity.com/packages/3d/props/low-poly-junkyard-models-pack-97213) | **GRATIS** (UAS) | Chatarra básica para graybox/jams desde el día 1. |
| **Kenney [City Kit Industrial](https://neonroof.com/software/game-assets/kenney-city-kit-industrial) + [Factory Kit](https://assetsifter.com/best/best-free-ue5-assets/)** | **CC0** | Galpones, naves, y el Factory Kit con **cintas transportadoras, cajas, tuberías y engranajes** (140+ modelos) = base de la automatización del principio 2. |
| Packs de [Quaternius](https://quaternius.itch.io/) (vehículos, props, personajes) | **CC0** | Props y personajes low-poly de relleno con estilo consistente. |
| Personajes jugadores | Hacer propios en Blender | Referencia: moodboard `refs/moodboard_personajes_props.png` + capturas de Moon Sweepers. Chibi = pocos huesos, animaciones simples, ragdoll fácil. Rig base con Mixamo si apura. |

### 4.3 Base de sistemas de negocio/progresión
- **ScriptableObjects**: `ItemSO` (peso, materiales, precio/kg), `MachineSO`, `ToolSO`, `UpgradeSO`. Tablas editables sin tocar código → la "progresión constante" del principio 2 se balancea en una hoja de cálculo.
- Guarda: JSON serializado por chatarrería + Steam Cloud. Sin backend propio.

---

## 5. Arquitectura co-op recomendada (1–7 jugadores, caos que no rompe el save)

```
[Host = jugador 1]  →  autoritativo: física de props "importantes", economía, guarda, spawns
        │  NGO 2.x sobre Unity Relay (o Steam SDR)
[Clientes 2..7]     →  predicen su avatar (CharacterController kinematic replicado),
                       interpolan props del host,
                       simulan LOCALMENTE física cosmética (chispas, pedazos, ragdolls)
```

- **Ownership**: objeto agarrado = del que lo agarra (`ChangeOwnership`); lanzado = impulso del dueño y vuelta al host al dormir el Rigidbody.
- **Anti-grief de diseño**: el progreso persistente (plata, máquinas compradas, chatarra vendida) vive **solo en el host y solo en la zona de venta**; el caos del patio es efímero por sesión. Así "tus amigos lo rompen" es comedia, no pérdida de save.
- **Escalado a 7**: interest management por zonas del patio (un jugador en el galpón no replica la pila de autos del fondo) + pooling de NetworkObjects. Con esto, 7 jugadores × 300 props activos es trivial para Relay.

---

## 6. Plan de prototipo: vertical slice en ~12 semanas

| Fase | Semanas | Entregable | Base usada |
|---|---|---|---|
| **0. Setup** | 1 | Unity 6 LTS + URP; clonar U6-First-Person-Multiplayer; graybox del patio con Kenney Factory Kit + Batya free | Sección 4.1/4.2 |
| **1. Loop de mano** | 2–3 | Agarrar/golpear/lanzar chatarra con feel cómico; pesar y vender en balanza; plata persistente | grab/throw de bababuyyy (MIT) + ItemSO |
| **2. Desarmar** | 4–5 | Herramienta 1 (llave/martillo) que **pre-fractura** piezas (Cell Fracture + joints); estados sucio→desarmado→limpio con shader de máscara | Blender Cell Fracture |
| **3. Primera máquina** | 6–7 | Cinta + prensa: llevar pieza, prensar, vender más caro; tienda de 3 upgrades | Kenney Factory Kit / ithappy shredder |
| **4. Co-op online** | 8–9 | Lobby Relay 4→7 jugadores, ownership de props, stress test 300 props, voz por proximidad | NGO + Relay del sample oficial |
| **5. Arte final** | 10–11 | Reemplazo de graybox por pack ithappy + props propios con paleta cerrada; personajes chibi + visores emisivos; LUT y post | Sección 3 |
| **6. Juice y test** | 12 | Audio, partículas, UI redonda con números gigantes; playtest cerrado 30 min; demo | — |

**Regla de validación (principio 1 del pitch)**: si en la semana 3 "golpear y vender chatarra" en un patio gris *no* es divertido solo, se itera el loop antes de agregar cualquier cosa. Moon Sweepers valida esto con un cepillo de dientes: la actividad mínima tiene que cerrar sola.

---

## 7. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Física networked desincronizada (el clásico del género) | Host autoritativo + ownership al agarrar + física cosmética local. No replicar todo. Prototipar esto en la semana 8, no al final. |
| Scope de 7 jugadores vs. 2–4 devs | Diseñar zonas; el patio completo no necesita replicarse entero. Empezar validando a 4 y subir a 7 con interest management. |
| Vehículos (grúas, camiones) temprano | Postergar a fase post-slice: son la fuente n.º 1 de bugs de física. El loop a pie primero (Moon Sweepers también: cepillo → recién después vehículos). |
| Estilo "se ve barato" en vez de "estilizado" | Fijar paleta cerrada + budgets de tris + 1 luz + LUT desde el día 1; revisar contra moodboards semanalmente. Lo barato se nota en la *inconsistencia*, no en el polycount. |
| Costos de red al crecer | Relay free tier (50 CCU) cubre desarrollo y lanzamiento chico; plan B: transporte Steam SDR gratis. |
| Licencias de packs | ithappy/UAS = licencia estándar de Asset Store (uso en un juego comercial OK, no reventa); Kenney/Quaternius CC0 sin atribución; MPL-2.0 y MIT del código: conservar avisos de licencia en créditos. |

---

## 8. Checklist de arranque (esta semana)

1. Instalar **Unity Hub → Unity 6 LTS** con template **URP 3D**.
2. `git clone https://github.com/Unity-Technologies/U6-First-Person-Multiplayer` y correrlo en LAN/Relay para sentir el pickup/throw networked.
3. Bajar gratis: **Batya junkyard pack**, **Kenney Factory Kit + City Kit Industrial**, un pack de props de **Quaternius**.
4. Comprar en sale: **ithappy Junkyard Low Poly** (US$ ~39,50) → tiene prensa y shredder, las máquinas núcleo del loop.
5. Blender + add-on **Cell Fracture**; fracturar 1 auto de prueba en 8–12 chunks con joints.
6. Definir paleta de 20 colores + budget de tris como documento interno (sección 3).
7. Crear proyecto Git con estructura `_Project/{Scripts,Art,Audio,Packs,_Scenes}` y los ScriptableObjects de items.

---

## Fuentes principales

- Motor/equipo de Moon Sweepers: [IndieDB](https://www.indiedb.com/games/moon-sweepers/downloads) · [windowsmode](https://www.windowsmode.com/moon-sweepers-for-windows/) · [moonsweepers.org](https://moonsweepers.org/game/) · [SteamDB](https://steamdb.info/app/5335510/info/)
- Netcode 2026 (tabla comparativa): [dev.to – Choosing the Right Real-Time Networking Stack for Unity in 2026](https://dev.to/gamedevtoollab/choosing-the-right-real-time-networking-stack-for-unity-in-2026-29f4)
- Sample oficial FPS multiplayer: [GitHub Unity-Technologies/U6-First-Person-Multiplayer](https://github.com/Unity-Technologies/U6-First-Person-Multiplayer)
- Patrones NGO/física: [Boss Room TossAction](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/main/Assets/Scripts/Gameplay/Action/ConcreteActions/TossAction.cs) · [ArtDock coop prototype](https://github.com/Yurii-Tor/ArtDock-CoopGame-Submission)
- Toolkits de interacción: [FirstPersonInteractionToolkit (MPL-2.0)](https://github.com/stevenharmongames/Unity-FirstPersonInteractionToolkit) · [unity-fps-gameplay-systems (MIT)](https://github.com/bababuyyy/unity-fps-gameplay-systems)
- Relay free tier y fin de Multiplay: [Unity Relay](https://activation.unity.cn/products/relay) · [tech-insider 2026](https://tech-insider.org/unity-multiplay-relay-lobby-setup-2026/) · [edgegap](https://edgegap.com/blog/what-are-relays-servers-for-a-peer-to-peer-networking-guide/) · [r/Unity3D P2P](https://www.reddit.com/r/Unity3D/comments/1crtz3y/best_p2p_option/)
- Packs de arte: [ithappy Junkyard (UAS)](https://assetstore.unity.com/packages/3d/environments/industrial/junkyard-low-poly-3d-models-pack-256375) · [Batya junkyard free (UAS)](https://assetstore.unity.com/packages/3d/props/low-poly-junkyard-models-pack-97213) · [Kenney City Kit Industrial](https://neonroof.com/software/game-assets/kenney-city-kit-industrial) · [Kenney/Quaternius CC0](https://assetsifter.com/best/best-free-ue5-assets/)
- Lethal Company (Unity + NGO, referencia de género): [unbaked.games](https://unbaked.games/games/lethal-company/)
