# DIRECCIÓN ARTÍSTICA — ¡ESO TODAVÍA SIRVE!

Biblia visual del proyecto (Instrucción 09, Fase 5). Fuente de verdad del
diseño: GDDs (`docs/DOc *.txt`, `docs/GDD_catalogo.md`). Este documento
define el look objetivo, las reglas para elegir/reemplazar assets y la
organización del contenido visual. Acompaña a `docs/MATRIZ_REEMPLAZO_VISUAL.md`
(qué se reemplaza, con qué, en qué orden).

Target técnico: **Unity 6000.3.5f2 + URP 17.3.0**, PC (Standalone), 1–7
jugadores cooperativos en una sola escena (chatarrería), host autoritativo.

---

## 1. Pilares del look

> **Estilizado con materiales industriales detallados, iluminación cuidada y
> estética de chatarrería.**

1. **Legibilidad primero (coop 7 jugadores).** Con hasta 7 avatares, piezas
   volantes y 14 estaciones en un patio chico, el ojo necesita jerarquía:
   lo interactuable se distingue de lo decorativo SIN leer texto. Regla: cualquier
   elemento con el que se pueda operar (estación, objeto desmontable, pieza,
   contenedor) tiene **silueta propia + acento de color de estado**; el decorado
   es más gris, más bajo en contraste y nunca usa los colores de acento.
2. **Estilizado ≠ plano.** Geometría low-poly limpia (bordes biselados, formas
   exageradas 5–15 %), pero materiales con **detalle PBR real**: óxido, grasa,
   rayones, polvo. El contraste "forma simple + superficie rica" ES el estilo
   del juego (referencia: Deep Rock Galactic, Teardown en modo estilizado,
   Hardspace Shipbreaker en tono amable).
3. **Chatarrería cálida, no post-apocalíptica sucia.** Es un juego de
   recuperación y cooperativa: la basura es **tesoro**. Paleta base terrosa/
   industrial con acentos vivos (ver §2). Iluminación dorada de patio al
   atardecer, no neón ni niebla tóxica.
4. **Todo modelo sirve a una regla, nunca al revés.** Prohibido modificar
   lógica económica, física o de desmontaje para adaptar un modelo (decisión
   explícita del proyecto). Los modelos se eligen/ajustan a: colliders
   existentes, offsets de `ScrapyardLayout`, slots de `BlueprintBuilder` y
   tamaños de pieza del catálogo.

## 2. Paleta

### 2.1 Base del mundo (decorado y estructuras)
| Rol | Hex aprox. | Uso |
|---|---|---|
| Tierra/suelo del patio | `#6B5D4F` → `#57493C` | terreno, polvo, grava |
| Metal estructural viejo | `#7A7D82` | paredes, postes, contenedores neutros |
| Metal oscuro (maquinaria) | `#33383F` | cuerpos de estaciones, sombras de forma |
| Madera de trabajo | `#8A6A45` | mesa de trabajo, palés, mangos |
| Hormigón | `#9A968D` | platea de la mesa, bordes |

### 2.2 Acentos de estado (RESERVADOS — solo elementos interactivos)
| Rol | Hex | Semántica (fuente: razones del dominio) |
|---|---|---|
| Verde chatarrería | `#5FD97A` | operable / progreso OK / saldo (ya usado por el HUD) |
| Ámbar herramienta | `#F2B83C` | requiere herramienta / fijación a la vista / sellado |
| Naranja cobre | `#C87832` | piezas valiosas (motor pequeño, cobre) |
| Rojo rechazo | `#E05545` | operación rechazada / residuo especial / daño |
| Azul eléctrico | `#4FA3E3` | electrónica / placas (MAT-06) |

Regla de oro: **ningún asset decorativo puede usar los 5 acentos** en más de
un 10 % de su superficie (calcomanías, botones, cintas). Si un modelo comprado/
descargado los trae, se desaturan en el material.

### 2.3 Materiales del dominio (piezas y rendimientos)
Los 7 materiales + MAT-08 tienen color de reconocimiento en contenedores y
piezas (consistencia con `DisassemblyLayouts.PieceColor` actual):
acero `#8F9298`, aluminio `#C9CDD2`, cobre `#C87832`, plástico `#4FA3E3`,
vidrio `#BFE3E8`, electrónica `#3E6B4F` (verde PCB), caucho `#3A3A3C`,
residuo especial `#E05545` con franjas ámbar.

## 3. Geometría y escala

- **Escala humana de referencia**: avatar ≈ 1.8 m; cápsula del jugador base
  (U6-FPM) tiene radio ~0.8 m a escala 3.19 — los modelos nuevos NO pueden
  invadir ese cilindro en pasillos de circulación.
- **Mesa de trabajo**: 1.6 × 0.9 × 1.0 m (ya construida en `BuildWorkbench`).
  El microondas y futuros objetos 02–30 se apoyan a y≈0.95.
- **Estaciones**: collider de interacción EXISTENTE = BoxCollider 1.0 × 1.6 ×
  1.0 m en la raíz (`ScrapyardLayout.BuildStations`). Todo modelo de estación
  debe caber visualmente en ~1.2 × 1.8 × 1.2 m y **no** agregar colliders
  propios que tapen el raycast (la interacción base hace
  `Physics.Raycast` sin máscara desde la cámara del jugador, rango 5 m).
- **Piezas**: tamaño de referencia 0.15–0.5 m (las lleva el jugador en la
  mano, `PickupItem._holdDistance` = 3 → se ven cerca de la cámara). Modelos
  de pieza: < 500 tris, sin partes móviles.
- **Objetos desmontables**: el blueprint define slots locales
  (`DisassemblyLayouts`); cada componente es un primitivo hoy. El modelo
  final se compone **reemplazando primitivo por primitivo** (misma posición/
  escala local del slot) o con un mesh único + children vacíos que respeten
  los slots. La extracción mueve el child a la mano del jugador: el pivot de
  cada componente debe ser su centro visual.
- **Densidad de triángulos objetivo (escena completa)**: ≤ 350 k tris
  visibles (7 jugadores + 14 estaciones + patio + 1–3 objetos en mesa).

## 4. Materiales y shading (URP)

- **Shader único de escena**: `Universal Render Pipeline/Lit` (los primitivos
  actuales ya usan `_BaseColor` — compatible). Excepciones: HUD (UI Toolkit)
  y VFX (Shader Graph de partículas).
- **Workflow metálico/roughness** con maps 1K (props) y 2K (suelo/terreno,
  tileables). Normal maps solo en piezas cercanas y estaciones; decorado
  lejano sin normal map.
- **Detalle estilizado**: un **atlas de calcomanías** propio (óxido, grasa,
  rayones, cintas de peligro, números pintados) proyectado con decals URP o
  segundas UV. Es lo que da "industrial detallado" sobre geometría simple.
- **Paleta por material de Unity**: crear materiales en
  `Assets/_ETSV/Art/Materials/` (ver §8), nunca materiales `new Material()`
  en runtime para assets finales (hoy `BlueprintBuilder.MakeMaterial` crea
  materiales de color planos: son **provisorios** y se reemplazan por el atlas
  de materiales sin tocar el código de reglas).
- **Trim sheets** para estructuras repetidas (paredes de contenedores,
  estanterías): una textura trim 2K cubre toda la chatarrería.

## 5. Iluminación

- **Sol direccional cálido** (atardecer de patio): color `#FFD9A0`,
  intensidad ~1.2, ángulo bajo (15–25°) para sombras largas que marquen
  relieve entre objetos apilados.
- **Skybox HDRI** (Poly Haven, CC0): cielo industrial/nublado cálido. Aporta
  reflections probe única (boxed, la escena es un patio cerrado).
- **Baked**: la escena es estática salvo jugadores/piezas ⇒ **Lightmaps
  baked** (Static en decorado) + Light Probe Groups en zonas de circulación y
  estaciones. Sombras en tiempo real SOLO para el sol sobre objetos dinámicos
  (jugadores, piezas, objetos de la mesa) — `Soft Shadows`, cascadas 2.
- **Rellenos prácticos**: 2–4 luces puntuales cálidas (lámparas colgantes de
  galpón sobre la mesa y la trituradora) con intensidad baja; son
  justificación narrativa de la iluminación, no neones.
- **Post (URP Volume)**: ACES tonemap, exposición fija, Color Adjustments
  (contraste +10, saturación +5), **Vignette suave**, Depth of field SOLO en
  modo foto si se agrega. Nada de bloom fuerte (los acentos de estado deben
  leerse limpios).

## 6. Rendimiento (7 jugadores en coop)

Presupuesto por frame (PC medio, 60 fps): draw calls ≤ 150 (SRP Batcher ON:
todos los materiales URP/Lit con mismas propiedades), tris ≤ 350 k, sombras
2048 px una cascada + 1024 la segunda.

- **LODs**: obligatorio en estaciones y objetos desmontables (LOD0 < 2.5 k
  tris, LOD1 40 %, LOD2 15 % + impostor no necesario en patio cerrado).
  Piezas: sin LOD (son pequeñas y cercanas), pero ≤ 500 tris.
- **Occlusion culling**: estático activado (los contenedores apilados tapan
  mucho). **Static batching** en decorado.
- **NetworkTransform** ya existe en piezas/objetos: los modelos NO agregan
  NetworkBehaviours nuevos (regla de arquitectura: la lógica no se toca).
- **Partículas/VFX**: GPU particles, ≤ 200 partículas vivas por efecto; los
  efectos de estación (chispas de trituradora, polvo de balanza) duran < 1 s.
- Texturas: streaming mipmaps ON; decorado lejano limitado a 1K.

## 7. Consistencia entre paquetes (multi-artista / multi-paquete)

Como el contenido vendrá de varios paquetes gratuitos (§9 y matriz), la
consistencia se impone con estas reglas duras:

1. **Un solo atlas de paleta**: todo modelo importado se re-texturiza o se le
   aplica un material del set propio (§8) — no se dejan las texturas
   originales de paquetes distintos conviviendo crudas.
2. **Proporción de estilización común**: escala de cabeza/ruedas/botones
   +10 % vs real; bordes con bisel mínimo 2 cm; nada de superficies de un
   solo triángulo sin grosor.
3. **Altura de horizonte de detalle**: objetos por encima de 2.5 m (postes,
   techos) llevan solo color base + trim; el detalle fino vive por debajo de
   la línea de cámara (zona de interacción).
4. **Prueba de los 3 segundos**: screenshot de la mesa con una estación y un
   objeto → debe poder decirse qué es cada cosa y qué está interactuable. Si
   falla, se ajusta silueta/acento, no se agrega más detalle.
5. **Mismos pivotes y ejes**: Y-up, pivot en la base para estaciones (apoyan
   en el suelo) y en el centro para piezas/objetos (rotan en la mesa).

## 8. Organización de assets en el repo

```
unity/Assets/_ETSV/Art/            ← TODO lo visual nuevo vive acá
  Meshes/            (FBX/glTF importados, por carpeta de paquete)
    Stations/  Props/  Characters/  Appliances/
  Materials/         (set propio URP/Lit: M_Metal_Oxidado, M_Madera_Palet, …)
  Textures/          (atlas de calcomanías, trims, PBR 1K/2K)
  Prefabs/           (prefabs VISUALES: variantes y props; los prefabs de RED
                      siguen en _ETSV/Resources/ETSV/ y NO se mueven)
  VFX/               (sistemas de partículas de estaciones)
  Artifacts/         (.blend de ajustes propios, opcional)
```

Reglas:
- Los **prefabs de red** (`ETSVSession`, `DisassemblyObject`, `DisassemblyPiece`)
  mantienen sus GUIDs y estructura de componentes; el reemplazo visual se hace
  DENTRO de ellos (meshes/materiales de los children que construye
  `BlueprintBuilder`/`ScrapyardLayout`) o sustituyendo los primitivos que
  esos builders crean, por prefabs visuales — sin cambiar NetworkObject,
  NetworkVariables ni RPCs.
- Nada de assets con licencia incompatible o de origen desconocido
  (restricción del proyecto). Cada carpeta de paquete incluye una copia de su
  `LICENSE` y la URL de origen (`_SOURCE.md` por paquete).
- Los assets comprados (si alguna vez se aprueban) van a `Art/Paid/` con su
  recibo/factura de Asset Store en `_SOURCE.md`.

## 9. Tratamiento visual de los desmontables (corazón del juego)

El desmontaje es EL verbo del juego; su tratamiento visual tiene prioridad
sobre cualquier otra pieza de arte:

- **Fijaciones visibles**: tornillos/pernos/clip deben verse como geometría
  propia del componente (hoy: cubitos rojos 0.05 m de `BlueprintBuilder`).
  Reemplazo: mesh de tornillo 20–40 tris en ámbar cuando requiere herramienta
  equipada correcta, gris cuando no.
- **Estados del componente** (fuente: `CompState` del dominio — intacto /
  trabajando / liberado / extraído): se expresan con **outline de color de
  acento** + emisión sutil del material:
  - seleccionable con herramienta correcta → outline verde;
  - en progreso → barra 3D flotante mínima + partículas de herramienta;
  - liberado (listo para extraer) → pulso leve de emisión ámbar (1 Hz);
  - dañado (calidad < 100) → decals de golpe/abolladura proporcionales.
- **Extracción**: la pieza sale con un "pop" físico corto (lo hace el dominio;
  el arte agrega partícula de polvo + sonido). En la mano del jugador la pieza
  usa el mismo mesh con material ligeramente más saturado (feedback de "esto
  es mío ahora").
- **Calidad**: la calidad (0–100) se ve: suciedad/óxido del material de la
  pieza escala con (100 − calidad) — un objeto de calidad 40 se ve claramente
  peor que uno de 95, sin números en pantalla.

## 10. Personajes

- 7 jugadores ⇒ siluetas distinguibles por **color de equipo** (el upstream ya
  tiene `PlayerColour` con materiales Red/Blue/Green/Yellow/Pink/White/Black).
- Estilo: low-poly estilizado con proporciones 1:6 (no chibi), ropa de trabajo
  (overol, guantes, gorra) — coherente con chatarrería.
- Candidatos gratuitos (§ matriz): Quaternius Ultimate Animated Characters
  (CC0, 50+ con animaciones, FBX) o KayKit Adventurers (CC0, rigged). El
  avatar actual de U6-FPM (cápsula con cabeza/ojos) se reemplaza por un
  prefab visual **sin tocar** PlayerMovement/PlayerInteraction/NetworkObject.
- Animaciones mínimas: idle, walk, run, carry (llevando pieza con ambas
  manos), interact (golpe de herramienta). Retarget al rig elegido con
  Mecanim/Animator — el controlador de movimiento NO se modifica.

## 11. VFX y audio (alcance de esta biblia: VFX)

- Chispas (trituradora/herramienta), polvo (caída de piezas, balanza), humo
  suave (residuo especial). Paleta de VFX = acentos §2.2.
- Regla: un VFX nunca tapa el crosshair ni el panel del HUD (zona central
  inferior reservada).

## 12. Criterios de aceptación de un asset nuevo (checklist)

1. Licencia verificada y documentada (CC0 / Asset Store EULA con recibo).
2. Encaja en la paleta §2 o se re-texturiza para encajar.
3. Respeta escala/collider/slots §3 (prueba: reemplazar el primitivo sin
   mover un solo número de `ScrapyardLayout`/`DisassemblyLayouts`).
4. ≤ presupuesto de tris/LOD §6.
5. Material URP/Lit del set propio (§4), pivot correcto (§7.5).
6. No requiere cambios en lógica económica/física/desmontaje (restricción
   dura del proyecto).
7. Pasa la prueba de los 3 segundos (§7.4) en contexto.

## 13. Estado actual (qué existe hoy vs. qué es candidato)

> **UPDATE Instrucción 11 (2026-11-12)**: primer set de **modelos 3D reales
> CC0** incorporado (27 FBX: Kenney City Kit Industrial + Furniture Kit,
> Quaternius Modular Sci-Fi MegaKit) con integración por `ETSVModels` y
> fallback automático a primitivos. Sustituyen la geometría provisional en
> contenedores, galpón (postes/ventilación), hitos del perímetro, pilas de
> chatarra, estantería, residuos especiales, contenedores de material,
> balanza/trituradora y utilería de la mesa. Los materiales siguen siendo los
> 12 de la Instrucción 10 (nunca los del pack) ⇒ coherencia de estilo por
> construcción. **La importación/capturas en Unity Editor están bloqueadas
> SOLO por licencia** (el binario 6000.3.5f2 se ejecuta en el sandbox; ver
> `MODELOS_3D.md` §4 y la evidencia en `unity/ci/logs/`).
>
> **UPDATE Instrucción 10 (2026-11-12)**: la primera implementación artística
> REAL ya está incorporada (texturas PBR CC0 de Poly Haven, 12 materiales URP
> Lit, sol cálido + niebla + Volume ACES, galpón/contenedores/chatarra/
> señalización, estaciones y microondas con materiales y detalles). Ver
> `ARTE_v1.md` para el detalle completo y el estado de verificación.

**Existente (provisorio, funcional):**
- Primitivos con colores planos creados en runtime: mesa (`BuildWorkbench`),
  14 estaciones (`ScrapyardLayout.BuildStations` + etiquetas TextMesh),
  componentes de objetos (`BlueprintBuilder`), piezas
  (`DisassemblyLayouts.PieceColor`).
- 7 materiales de color del upstream (Assets/Materials) + Red.mat en piezas.
- Avatar U6-FPM (cápsula escalada con cabeza/ojos/boca) y HUD UI Toolkit.
- Escena Multiplayer.unity del upstream con terreno ProBuilder, iluminación
  y Cinemachine.

**Candidatos (evaluados en Fase 4, ver MATRIZ_REEMPLAZO_VISUAL.md):**
Kenney (CC0), Quaternius (CC0), KayKit (CC0), Poly Haven (CC0: HDRIs y PBR),
ambientCG (CC0: PBR industrial), Asset Store solo con aprobación explícita y
presupuesto (por ahora NADA comprado — restricción del proyecto).
