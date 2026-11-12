# Escenario v2 — Recuperaciones El Milagro (Instrucción 12)

Rediseño del patio tras la primera ejecución real en Unity 6000.3.5f2 / Windows.
Todos los sistemas existentes quedan intactos: movimiento, raycasts de
interacción, mesa/estaciones, desmontaje, transporte, economía, networking y
persistencia. La decoración nueva no lleva colliders ni NetworkObjects.

## 1. Problemas observados y causa raíz

| # | Problema | Causa raíz (auditoría) | Corrección |
|---|----------|------------------------|------------|
| a | Edificios industriales desproporcionados | Landmarks Quaternius escalados con `KenneyCityScale` (×7) colocados a 5–10 m del patio y **solapados entre sí** (tanque sobre el spawn P4) | Landmarks a ≥15 m del ancla, separados, sin solapes (tabla §5) |
| b | Estructuras superpuestas | Murete perimetral continuo que **cruzaba por delante del spawn P3**; pilas de chatarra y neumáticos sobre zonas de paso | Murete en 2 tramos con portón central; pilas/neumáticos reubicados y verificados por AABB |
| c | Terreno cuadriculado (heredado U6-FPM) | `Terrain.terrainlayer` con `Grid Texture.png` (guid `b2f90968…`), TileSize 5×5, metallic 0.213 | Textura sustituida por grava CC0 (`GravelYard`, Poly Haven) con normal map y mask; TileSize 8×8; metallic 0. **TerrainData y colliders sin tocar** |
| d | Iluminación demasiado oscura | Sol 1.35 @ 22° de elevación + ambiental plano (0.212,0.227,0.259) x1 + ACES con `postExposure 0.1` + niebla 0.008 | Sol 2.3 @ 32°, ambiental Trilight x1.4, niebla 0.0035, `postExposure 0.6`, viñeta 0.15 |
| e | Textos de estaciones superpuestos | Rótulos TextMesh a altura fija 1.75 m con `characterSize 0.09` sobre estaciones de 0.3–2.1 m y separadas 0.7 m | `LabelHeightFor(StationKind)` por tipo de estación; `characterSize 0.075`; separación mínima entre estaciones 1.15 m |
| f | Aparición inicial dentro/dejunto de elementos | `TableAnchor = P2Spawn + (1.8, 0, 1.2)` ⇒ mesa a ~1 m del jugador | `TableAnchor = P2Spawn + (3.9, 0, 0.2)` ⇒ mesa a 3.9 m, vista frontal despejada del patio |

## 2. Sistema de coordenadas

Ancla **A** = `TableAnchor` = Player 2 Spawn + (3.9, 0, 0.2).

- Spawn P2 mundo: (106.40, 0, 101.34) → **A = (110.30, 0, 101.54)**
- Spawn P3 mundo: (109.36, 0, 107.12) → rel. A: (−0.94, +5.58)
- Spawn P4 mundo: (115.21, 0, 110.49) → rel. A: (+4.91, +8.95)
- Eje +X = este, +Z = sur (entrada del patio por el sur, galpón al norte).
- Todas las medidas en **metros**, escala 1:1 (`KenneyCityScale = 7` sólo para
  assets Kenney City, cuyo modelo base mide ~0.86 m ⇒ contenedor real 5.8×2.6×2.4 m).

## 3. Zonas del patio (offsets desde A)

| Zona | Contenido | Rango XZ rel. A |
|------|-----------|------------------|
| Entrada y circulación | Grava 15×4.4 en (0, 5.6); muretes con portón central (hueco x −5.2..+5.2 en z 6.6); 2 farolas (±5.8, 4.6) | z +3.4 .. +7.8 |
| Mesa de desmontaje | Mesa en A (1.6×0.9, h 1.0) + utilería sin collider | x −0.8..+0.8, z −0.45..+0.45 |
| Balanza y venta | Estación 0 en (−2.6, +1.4) | esquina SO de la mesa |
| Trituradora | Estación 1 en (+2.7, +1.2) | esquina SE |
| Residuos especiales | Estación 2 en (+3.2, −1.2) | este, junto a pared del galpón |
| Almacenamiento | Estación 3 estantería (−5.0, −2.2); estación 4 palé (−2.0, −2.6); estación 5 suelo (0.6, −2.6); estaciones 6–13 fila de 8 contenedores en z −4.2, x −4.025..+4.025 paso 1.15 | interior/entrada del galpón |
| Galpón (taller) | Pad hormigón 13.4×10.4 en (0, −1.6); paredes fondo y laterales; techo 13.8×6.6 a 4.28 m; 4 postes ×1.02 (remate exacto bajo techo); viga (0, 3.6, −4.2) con 3 lámparas; fachaleta (0, 3.85, −0.86) con cartel | x −6.7..+6.7, z −7.2..−0.6 |
| Decoración periférica | Landmarks ≥15 m (§5); pila izq de contenedores Kenney (−9.2, −2.2) 2 alturas; contenedor der (9.2, +0.8) yaw 90; pilas de chatarra p1 (−5.6, +2.2), p2 (+5.4, +3.4), p3 (+4.8, −0.4); neumáticos (−4.3, +4.45) | perímetro |

### Estaciones interactivas (ScrapyardLayout.Offsets, v2)

| # | Estación | Offset XZ | Altura rótulo | Mundo (x, z) |
|---|----------|-----------|---------------|--------------|
| 0 | Balanza | (−2.6, +1.4) | 1.05 m | (107.70, 102.94) |
| 1 | Trituradora | (+2.7, +1.2) | 1.85 m | (113.00, 102.74) |
| 2 | Residuos | (+3.2, −1.2) | 1.15 m | (113.50, 100.34) |
| 3 | Estantería | (−5.0, −2.2) | 2.05 m | (105.30, 99.34) |
| 4 | Palé | (−2.0, −2.6) | 0.62 m | (108.30, 98.94) |
| 5 | Suelo | (+0.6, −2.6) | 0.32 m | (110.90, 98.94) |
| 6–13 | Contenedores | z −4.2; x = −4.025 + 1.15·i | 1.02 m | x = 106.275 + 1.15·i, z = 97.34 |

Rótulos: `TextMesh` `characterSize 0.075`, `fontSize 48`, anclados a
`LabelHeightFor(kind)` sobre cada estación (antes altura fija 1.75 m que hacía
flotar los textos de palé/suelo y solapar los de la fila de contenedores).
Separación libre entre colliders de estaciones ≥0.55 m (fila de contenedores:
0.65 m con acceso frontal compartido por pasillo de 0.75 m + zona st5 rasante).

## 4. Spawns — despeje verificado

Distancia del spawn al obstáculo sólido más cercano (verificador AABB):

- P2: 1.43 m (estación 0, balanza) — mesa a 3.9 m al frente, vista del patio despejada.
- P3: 3.01 m (neumáticos).
- P4: 2.24 m (murete este).

Corredores spawn→mesa: sin cruces con ningún sólido (margen 0.35 m).
La utilería de la mesa (baúl, tacho, caja) no tiene colliders —igual que en la
Instrucción 11— y se reubicó fuera de los corredores: baúl (−1.9, −1.1),
tacho (+1.15, +0.35), caja (−0.95, +0.95).

## 5. Landmarks periféricos (×KenneyCityScale, sin solapes, ≥15 m de A)

| Landmark | Offset XZ | Yaw |
|----------|-----------|-----|
| building-c | (−15.0, −17.0) | 8° |
| water-tower | (−24.0, −5.0) | 0° |
| building-m | (+14.0, −20.0) | −12° |
| tanque | (+16.5, −7.5) | −20° |
| chimenea | (+24.0, −4.0) | 0° |

El tanque ya **no** pisa el spawn P4 (antes a ~5 m; ahora a 17.5 m).

## 6. Señalización

| Texto | Posición rel. A | charSize |
|-------|------------------|----------|
| RECUPERACIONES EL MILAGRO | fachaleta (0, 3.85, −0.755) | 0.16 |
| ZONA DE DESMONTAJE | murete izq (−7.25, 1.15, +6.42), yaw 180 | 0.09 |
| ENTRADA | murete der (+7.25, 1.15, +6.42), yaw 180 | 0.10 |

Rótulos de estación por `LabelHeightFor` (§3). Ningún texto flotante superpuesto.

## 7. Terreno

`Assets/Terrain/Terrain.terrainlayer` (única referencia al guid de Grid
Texture en todo el proyecto):

- `m_DiffuseTexture`: GravelYard_diff.jpg (guid `8cfe6250217fe62bafd08e624465b6fc`, Poly Haven CC0)
- `m_NormalMapTexture`: GravelYard_nor.jpg (guid `5dff7cdfefadc1eb08c82fe5c83e2354`, importer `textureType: 8`)
- `m_MaskMapTexture`: GravelYard_mask.png (guid `d3dd914f8ddda66ff01f958f6f3e1084`)
- TileSize 5×5 → **8×8**; metallic 0.213 → **0** (la grava no es metálica).

`New Terrain.asset` (TerrainData binario) y el TerrainCollider **no se
modifican**: alturas y colisiones heredadas de U6-FPM intactas.

## 8. Iluminación y post-proceso

Escena `Multiplayer.unity`:

- Sol (Directional Light, fileID 410087040): intensity 1.35 → **2.3**;
  rotación Euler (22, −30, 0) → **(32, −30, 0)** (quat
  0.26624524, −0.24879283, 0.07134020, 0.92850750); color cálido (1, 0.851, 0.627) y sombras soft (strength 1) sin cambios.
- RenderSettings: `m_AmbientMode` 0 (Skybox) → **1 (Trilight)**;
  cielo (0.565, 0.635, 0.745), ecuador (0.545, 0.505, 0.435), suelo
  (0.285, 0.245, 0.195); `m_AmbientIntensity` 1 → **1.4**.
- Niebla: densidad 0.008 → **0.0035** (color conservado).

Volume `ETSVScrapyardProfile.asset` (URP, ACES):

- `postExposure` 0.1 → **0.6 EV**; contraste 8 y saturación 5 sin cambios;
  `Vignette.intensity` 0.25 → **0.15**.

Iluminación práctica nueva (emisivos, sin Light components para no consumir
 draw calls): 3 lámparas bajo la viga del galpón (x −3.2/0/+3.2, z −4.2,
 y≈2.95–3.4, material LampGlow) + 2 farolas de entrada (±5.8, +4.6) de 4.4 m
 con cabezal emisivo.

## 9. Verificación realizada

- **Verificador de layout (AABB)**: despeje de spawns ≥1.0 m, solapes entre
  sólidos, corredores spawn→mesa, separación entre estaciones, gálibo bajo el
  techo del galpón ⇒ **todo OK** (únicos contactos intencionados: postes
  empotrados en las esquinas del galpón y remate a 4.28 m bajo la losa).
- Banco C# (`bash unity/ci/build-and-test.sh`, mono + stubs alineados a NGO
  2.12/InputSystem 1.17/URP 17.3.0): **84/84**.
- Prototipo JS: `disassembly.test.mjs` **20/20**, `economy.test.mjs` **53/53**.
- **Pendiente**: validación visual en el Editor real (capturas del propietario).
  No se declara validación visual completa sin nuevas capturas de Unity.

## 10. Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `_ETSV/Scripts/Presentation/ScrapyardLayout.cs` | TableAnchor, Offsets v2, LabelHeightFor, characterSize 0.075 |
| `_ETSV/Scripts/Presentation/ETSVScenery.cs` | Reescrito: plan de zonas v2 (§3–§6, §8) |
| `_ETSV/Scripts/Integration/ETSVBootstrap.cs` | Utilería de mesa fuera de corredores (§4) |
| `Assets/Terrain/Terrain.terrainlayer` | Grava CC0 (§7) |
| `Assets/Scenes/Multiplayer.unity` | Sol + ambiental + niebla (§8) |
| `_ETSV/Art/Post/ETSVScrapyardProfile.asset` | postExposure 0.6, viñeta 0.15 (§8) |
