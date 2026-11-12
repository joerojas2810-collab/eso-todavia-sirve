# IMPLEMENTACIÓN ARTÍSTICA v1 — Recuperaciones El Milagro

Instrucción 10 (2026-11-12). Primera capa de arte REAL incorporada al proyecto
Unity. Companion de `DIRECCION_ARTISTICA.md` (biblia) y
`MATRIZ_REEMPLAZO_VISUAL.md` (matriz P1–P11). Todo lo hecho acá respeta la
restricción dura: **solo presentación** — no se modificó física, red, dominio,
economía ni persistencia.

## 1. Assets incorporados (reales, en el repo)

**6 packs de texturas PBR de Poly Haven — CC0 1.0 (dominio público)**,
resolución 1k, md5 verificados contra `api.polyhaven.com` en la descarga
(evidencia por carpeta en `Assets/_ETSV/Art/Textures/*/[_SOURCE.md]`; licencia
global en `Art/Textures/LICENSE-CC0.txt`):

| Carpeta | Poly Haven | Uso en el juego |
|---|---|---|
| `ContainerPainted` | `container_side` | metal pintado: contenedores, máquinas, tambores |
| `CorrugatedRusty` | `rusty_corrugated_iron` | chapa acanalada: galpón, murete, cerramientos |
| `SteelRusty` | `rusty_metal_02` | acero oxidado: estructuras, chatarra, postes |
| `ConcreteWorn` | `concrete_floor_worn_02` | hormigón: losa de trabajo, zonas de suelo |
| `WoodPlanks` | `brown_planks_04` | madera: mesa, palés, estantes |
| `GravelYard` | `gravel_floor_02` | grava/tierra: franjas del patio |

Cada pack trae: `_diff.jpg` (sRGB), `_nor.jpg` (normal OpenGL) y `_mask.png`
(**repack local**: R=metallic, G/B=AO, A=smoothness=1−roughness, derivado del
mapa glTF `arm` original; el derivado conserva CC0). Peso total: ~23 MB.

**Sin pack CC0 adecuado** (plástico, caucho, vidrio): materiales procedurales
sólidos (ver §2) — documentado como pendiente en §6.

## 2. Materiales (12, URP Lit, `Assets/_ETSV/Resources/ETSV/Materials/`)

Texturizados PBR completos (BaseMap + Normal + MetallicGloss + Occlusion):
`M_SteelRusty`, `M_MetalPainted`, `M_CorrugatedRusty`, `M_ConcreteWorn`,
`M_WoodPlanks`, `M_GravelYard`.
Procedurales: `M_PlasticGrey` (smoothness .55), `M_RubberBlack` (.30),
`M_GlassClear` (transparente, alpha .32, smoothness .95, queue 3000),
`M_SignAmber` / `M_SignGreen` (acentos de señalización §2.2 biblia),
`M_LampGlow` (emisivo cálido ×2 para lámparas).

Todos con `m_EnableInstancingVariants: 1`, shader URP Lit
(`933532a4fcc9baf4fa0491de14d08ed7`, el mismo de los materiales del upstream),
AssetVersion 10 (formato URP 17.3). Viven bajo `Resources/` para carga
runtime por `ETSVArt`.

**Biblioteca `ETSVArt.cs`** (nueva): cache compartida por nombre, clones por
(nombre+tinte) — los tintes preservan la codificación de color del dominio
(color de material, ámbar de sellado, estados). **Fallback graceful**: si el
asset no existe (banco headless, build sin recursos), todo degrada al material
plano anterior ⇒ la jugabilidad jamás depende del arte.

## 3. Iluminación y ambiente (cambios de escena REALES)

`Assets/Scenes/Multiplayer.unity` (ediciones puntuales verificadas, diff
revisado línea por línea):
- **Sol**: color `#FFD9A0` (cálido atardecer), intensidad 2→1.35 (compensando
  ACES), elevación 50°→**22°** (sombras largas que marcan relieve entre
  chatarra; quaternion recalculado + hint de Euler coherente).
- **Niebla**: activada, exponential² densidad 0.008, color cálido `#C2AD90`
  (bruma de patio; separa planos sin tapar la acción).
- **RenderSettings.m_Sun** referenciado (disco solar del skybox alineado).
- **Volume global nuevo** ("ETSV Global Volume", registrado en SceneRoots) con
  perfil `Assets/_ETSV/Art/Post/ETSVScrapyardProfile.asset`:
  **Tonemapping ACES** + WhiteBalance (+8 temperatura) + ColorAdjustments
  (post-exposición +0.1, contraste +8, saturación +5) + Vignette (0.25).
  GUIDs de scripts tomados del **fuente real de URP 17.3** (rama
  `6000.3/staging` de Unity-Technologies/Graphics) y cross-checkeados contra
  los del `SampleSceneProfile.asset` existente.
- La cámara ya tenía post-procesado ON y SMAA (upstream) — no hizo falta tocar.

`Assets/Settings/PC_RPAsset.asset`:
- **MSAA 1→4×** (geometría; se complementa con SMAA de post).
- **Cascadas de sombra 4→2** (patio chico: más resolución efectiva por cascada,
  atlas 2048 intacto).
- **Sombras de luces adicionales OFF** (las lámparas del galpón son emisivas,
  no hay luces puntuales reales ⇒ se ahorra un atlas de sombras completo).
- **SSAO**: ya estaba activo en `PC_Renderer` (intensidad 0.4, radio 0.3) —
  compatible y conservado. **SRP Batcher**: activo (dynamic batching off es
  lo correcto con SRP Batcher).

## 4. Patio visual (código de construcción, determinista)

**`ETSVScenery.cs` (nuevo)** — escenografía estática construida en runtime
desde constantes respecto del ancla de la mesa (idéntica en todas las peers;
**no viaja por red**):
- Losa de hormigón de la zona de trabajo (7.2 × 5.6 m, +2 cm, sin z-fighting)
  y franja de grava frontal.
- **Galpón al fondo**: pared de chapa acanalada (11 × 3.4 m), techo en losa
  (sombras largas), 4 postes de acero, viga cruzada sobre la mesa.
- **Contenedores apilados**: 2 a la izquierda (azul-verdoso/verde, teñidos
  sobre `M_MetalPainted`), 1 rojo a la derecha, franja de peligro ámbar.
- **Cerramiento frontal bajo** (1.1 m: no tapa cámara ni interacción).
- **Chatarra decorativa**: 3 pilas (cajas, cilindros, vigas cruzadas, barril,
  electrodoméstico viejo con puerta caída) + pila de 3 neumáticos de caucho.
- **3 lámparas de galpón** colgadas de la viga: pantalla de acero + globo
  emisivo `M_LampGlow` (SIN componentes Light ⇒ cero coste de sombras extra;
  decisión de rendimiento §6 biblia para 7 jugadores).
- **Señalización**: cartel "RECUPERACIONES EL MILAGRO" (tablero ámbar +
  TextMesh), placa "ZONA DE DESMONTAJE" (verde) en el murete.

**Reglas duras cumplidas**: ~40 primitivos decorativos, todos **sin collider**
(no pueden tapar el raycast de interacción — que no usa LayerMask — ni alterar
la física) y **sin NetworkObject/NetworkBehaviour** (decoración no sincronizada;
separación explícita de lo interactivo).

**Estaciones (`ScrapyardLayout`)** — mismos ids, offsets, collider raíz
1×1.6×1, `StationRef` y TextMesh (la lógica económica no se entera):
- Balanza: base acero oxidado, plataforma metal pintado, poste + visor con
  marco (plástico verde del dominio como tinte).
- Trituradora: bancada de acero, cuerpo metal pintado rojo-óxido (tinte),
  tolva, salida de caucho y franja de peligro ámbar.
- Residuos especiales: tambor ámbar + tapa de caucho + 4 franjas alternas
  ámbar/negro.
- Estantería: postes naranja (metal pintado teñido), travesaño, estantes de
  madera y 2 cajones decorativos.
- Palé: madera real + segundo palé apilado rotado 7°.
- Zona de suelo: hormigón + 4 marcas ámbar de esquina.
- Contenedores de material 6–13: tambor de metal pintado **teñido con el color
  del dominio** (la codificación por material se conserva), aro de acero y
  muestra frontal del color.

**Mesa de trabajo (`ETSVBootstrap.BuildWorkbench`)**: footprint y altura
IDÉNTICOS (1.6 × 0.9 × 1.0, tope y=0.95 — el desmontaje no cambia); tope de
madera texturada, patas de acero oxidado, 2 travesaños y estante inferior
(sin collider, como antes).

## 5. Microondas (objeto 01) — modular, mejorado, NO indivisible

El sistema modular del GDD queda intacto: los **6 componentes independientes**
(carcasa, puerta, panel, plato, motor, módulo sellado) siguen construidos por
`BlueprintBuilder` en sus slots/pivotes exactos, con sus fijaciones y
`ComponentRef`/`FastenerRef`. Cambios:
- Material artístico por componente, **data-driven** (`ETSVArt.ComponentMaterialFor`,
  mapeo por palabras clave del id ⇒ los objetos 02–30 lo heredan sin código):
  carcasa→metal pintado, puerta→plástico+ventana de vidrio, panel→plástico+
  perilla+botonera verde, plato→vidrio+aro, motor→acero oxidado+eje,
  sellado→ámbar. Detalles = hijos SIN collider (el raycast sigue pegando al
  primitivo del componente).
- `BaseColors` pasa a blanco cuando hay textura: los tintes de estado de
  `ApplyVisuals` (bloqueado oscurecido ×0.45, liberado lerp verde) multiplican
  la textura en vez de ensuciarla.
- **Piezas sueltas** (`DisassemblyPieceView`): material artístico teñido con
  el color del dominio (reconocibilidad por color conservada); mismo fallback.

**Pendiente de Blender** (documentado, para cuando se encarguen modelos):
carcasa con bordes redondeados y chapa plegada; puerta con bisagra real y
manija (hoy la ventana es un plano); panel con display y membrana de botones;
plato con rodillos de giro; motor con bobinado visible y eje excéntrico;
módulo sellado con remaches y etiqueta de peligro. Formato sugerido: un FBX
por componente con pivot en el centro visual (compatibles con slots de
`DisassemblyLayouts`), ≤500 tris por componente, UV0 para el atlas propio.

## 6. Rendimiento (decisiones aplicadas)

- Materiales **compartidos** por (nombre, tinte) con caché ⇒ SRP Batcher
  agrupa por shader; instancing variante activo en los 12 materiales.
- Texturas 1k con mipmaps + **streaming de mipmaps** activado en los
  importers; normales en linear, AO/metallic/suavizado empaquetados en UNA
  máscara (3 mapas por pack en vez de 5).
- Sombras: 2 cascadas, atlas 2048, sin atlas de luces adicionales; SSAO con
  downsample desactivado pero radio corto (0.3).
- Decoración sin luces reales, sin colliders, sin red; ~60 primitivos extra en
  total, todos estáticos y batcheables.
- Presupuesto biblia §6 vigente (≤350k tris, ≤150 draw calls): la escena base
  del upstream es liviana y lo agregado es geometría primitiva.

## 7. Verificación (estado real)

| Check | Resultado |
|---|---|
| Banco C# (dominio+sim+integración, mono) | **78/78 PASS** (con el código de arte compilado contra stubs; fallback headless ejercitado: `Resources.Load`→null→material plano) |
| Prototipo JS | 20/20 + 53/53 PASS |
| YAML de escena parseado (399 documentos) | válido; fileIDs únicos; `SceneRoots` incluye el Volume nuevo |
| YAML de perfil/12 materiales/RPAsset | válido |
| GUIDs: perfil, materiales, texturas, FIX#1 | todos definidos en Assets; Volume→perfil OK; materiales→texturas/shader OK |
| Diff de escena | solo luz/niebla/sol/Volume (nada de red, spawn points, prefabs) |
| Prefabs de red (`Resources/ETSV/*.prefab`) | **sin cambios** (GUIDs, NVs y componentes intactos) |
| Colliders de interacción | sin cambios (raíz de estación; decoración SIN colliders) |

**NO verificado (requiere Unity Editor — no se declara validado)**: render
final, tonemap/SSAO/fog en pantalla, import real de texturas (los .meta
escritos son estándar pero el Editor puede regenerarlos), compilación URP de
los materiales, y la "prueba de los 3 segundos" de la biblia. Todo el arte
degradó a fallback en el banco ⇒ no hay riesgo de crash headless.

**Pendientes**: HDRI de cielo (Poly Haven, CC0) para reemplazar el skybox
procedural — requiere material `Skybox/Panoramic` y su guid (a hacer con
Editor o vía código `Shader.Find`); packs de plástico/caucho/vidrio con
textura (ambientCG u otro CC0); modelos CC0 de la matriz P4–P10 (Kenney/
Quaternius/KayKit); lightmap bake (con Editor). Ninguno comprado (restricción
vigente).
