# MODELOS 3D — Instrucción 11 (2026-11-12)

Primera integración de **modelos 3D reales y reutilizables** en *Recuperaciones
El Milagro*. Sustituyen progresivamente la geometría provisional (primitivas)
de la Instrucción 10 **sin ampliarla** y sin tocar colliders, identificadores,
lógica de desmontaje ni red.

Estado base: commit `8ae6bfa`. Editor Unity del proyecto: **6000.3.5f2
(3fa8bc678cb0)** (`ProjectSettings/ProjectVersion.txt`).

---

## 1. Principio de diseño: modelo = piel, dominio = esqueleto

```
┌ Identidad / collider / lógica / red ──── NO cambia nunca (raíz + BoxCollider,
│                                          componentes del blueprint, NetworkObjects)
└ Representación visual ────────────────── intercambiable (modelo CC0 ⇄ primitiva)
```

- Cada colocación de modelo usa el patrón **`Model(...)` / `ModelPart(...)` con
  fallback**: si el FBX no está importado (banco headless, Resources vacíos),
  se construye el primitivo exacto de la Instrucción 10. El juego jamás
  depende del arte (tests M01–M06 de `ModelsSceneryTests.cs` fijan el contrato).
- Los modelos **nunca traen collider** (`addColliders: 0` en el `.meta` del
  importer + no se agrega en runtime) ni `NetworkObject`/`NetworkBehaviour`:
  son visuales estáticos (punto 6 de la instrucción: 7 jugadores ⇒ menos draw
  calls, geometría estática compartida, cero tráfico de red en decoración).
- **Materiales**: NUNCA se importan los materiales/texturas originales de los
  packs (`materialImportMode: 0 = None`). Todos los meshes usan los 12
  materiales URP/Lit de la Instrucción 10 vía `ETSVArt.Tinted(...)` ⇒ coherencia
  de estilo garantizada por construcción (no se pueden mezclar estilos: los
  tres packs son low-poly/estilizados y comparten paleta terrosa).
- Multi-mesh: `ETSVModels.TryCreate` instancia **todos** los meshes del FBX
  (un hijo por mesh) con un solo material compartido ⇒ 1 draw call por
  material cuando Unity agrupe (SRP Batcher: mismo shader URP/Lit).

## 2. API nueva

`Assets/_ETSV/Scripts/Integration/Disassembly/ETSVModels.cs` (namespace
`Etsv.Integration`):

| Miembro | Función |
|---|---|
| `KenneyCity / KenneyFurniture / Quaternius` | nombres de carpeta en `Resources/ETSV/Meshes/` |
| `KenneyCityScale = 7f` | escala medida del City Kit (contenedor 0.823 u → 5.76 m) |
| `LoadAll(pack, model)` | todos los meshes del FBX (cacheados; `[]` si no existe) |
| `Load(pack, model)` | primer mesh o null |
| `TryCreate(pack, model, nombre, parent, posLocal, escala, yaw°, material)` | GO visual completo o **null** (fallback del llamador). Sin collider, sin red |
| `Reset()` | vacía la caché (tests / recarga de dominio) |

Llamadores integrados:
- `ETSVScenery.cs` — helper `Model(...)` (posición absoluta bajo `_root`, que
  está en el origen): contenedores, postes del galpón, ventilación, hitos del
  perímetro (`BuildLandmarks` nuevo: galpones lejanos, torre de agua, chimenea,
  tanque), pilas de chatarra (cajones, barril, cajas de cartón, microondas y TV
  viejos como chatarra), artefactos de lámpara, placa de señalización, mordaza.
- `ScrapyardLayout.cs` — helper `ModelPart(...)` (hijo local de la estación):
  balanza (terminal `Prop_Computer` como visor), trituradora (extractor
  `Prop_Fan_Small` + terminal), residuos especiales (tambor
  `Prop_Barrel_Large`), estantería (`bookcaseOpen` + cajas de cartón en los
  estantes), palé (mejorado: 5 tablas + 3 largueros ×2 palés apilados — sigue
  procedural, ver §5), contenedores de material (tambor teñido con el color
  del dominio).
- `ETSVBootstrap.cs` — utilería junto a la mesa de desmontaje: caja de
  herramientas (`Prop_Chest`), tacho (`trashcan`), caja de cartón. Coordenadas
  de mundo sin padre, igual que `AddBenchDetail`.

**Límites respetados**: footprint de la mesa 1.6×0.9×1.0 idéntico; estaciones
conservan raíz + BoxCollider 1×1.6×1 + `StationRef` + etiqueta TextMesh; los 6
componentes del microondas siguen siendo el sistema modular del blueprint
(`kitchenMicrowave` de Kenney se usa **solo** como chatarra decorativa en la
pila 2 — es indivisible y jamás reemplaza al microondas jugable).

## 3. Modelos incorporados (27 FBX, 1.1 MB) — `Assets/_ETSV/Resources/ETSV/Meshes/`

Procedencia y licencias completas en `Meshes/_SOURCE.md` (licencias originales
copiadas junto a los modelos). **Todo CC0 1.0**, descargado el 2026-11-12,
sin compras.

### KenneyCity/ — City Kit Industrial 2.0 (Kenney, CC0)
Escala del pack ≈ 7 m/unidad (medida sobre los OBJ; se aplica ×7 en código,
pivotes en la base).

| FBX | Uso |
|---|---|
| shipping-container-a/b/c | los 3 contenedores del patio (teñidos azul-verdoso / verde / rojo oxidado) |
| building-c, building-m | galpones lejanos del perímetro |
| water-tower, chimney-medium, detail-tank-large | hitos industriales panorámicos |

### KenneyFurniture/ — Furniture Kit 2.0 (Kenney, CC0)
Escala real 1:1 en metros, pivotes en la base (FBX ASCII 7.3 — Unity los
importa igual; verificados: contienen `Vertices` + `PolygonVertexIndex`).

| FBX | Uso |
|---|---|
| bookcaseOpen (+Low) | estación estantería (×2.1 llena el volumen del collider) |
| cardboardBoxClosed / Open | cajas en estantería, pilas de chatarra y junto a la mesa |
| kitchenMicrowave, televisionVintage | **chatarra decorativa** en la pila 2 (el microondas jugable sigue siendo el modular de 6 componentes) |
| trashcan | tacho junto a la mesa |
| lampSquareCeiling | artefacto de las 3 lámparas colgantes del galpón |
| bench | incorporado al repo como reserva (aún sin colocación) |

### Quaternius/ — Modular Sci-Fi MegaKit [Standard] (@Quaternius, CC0)
Escala real 1:1 en metros (carpeta `FBX (Unity)` del pack; se aplican escalas
0.34–1.4 según prop). Estilo industrial coherente: se usa **solo** en
estructura/utilería metálica teñida con la paleta terrosa (no se usa su
estética sci-fi emissive: las texturas ORM originales NO se importan).

| FBX | Uso |
|---|---|
| Prop_Barrel_Large | tambor de residuos especiales, barril de la pila 1 y contenedores de material (teñido con el color del dominio) |
| Prop_Crate3 / Prop_Crate4 | cajones de las pilas de chatarra |
| Prop_Chest | caja de herramientas junto a la mesa |
| Prop_Computer | visor de la balanza y terminal de la trituradora (teñido verde/ámbar) |
| Prop_Fan_Small | extractor sobre la trituradora |
| Prop_Light_Wide | incorporado como reserva (las lámparas actuales usan Furniture Kit) |
| Column_MetalSupport | 4 postes estructurales del galpón (4 m, ×0.9) |
| Prop_Vent_Small | rejilla de ventilación en la pared del fondo |
| Decal_Sign | placa de señalización sobre el contenedor derecho |

`.meta` de importer generados con el esquema determinista de GUIDs del
proyecto (`md5('etsv-meta-v1:'+ruta)` para `.meta` de carpeta/texto,
`md5('etsv-mesh-v1:'+ruta)` para los FBX): `materialImportMode: 0` (None),
`importAnimation: 0`, `animationType: 0`, `addColliders: 0`, `useFileScale: 1`,
`globalScale: 1`. 0 GUIDs duplicados en `Assets/` (verificado).

## 4. Intento de validación con Unity Editor real — resultado (estado honesto)

Punto 1 de la instrucción: el banco Mono/stubs/YAML **no es suficiente**. Se
ejecutó el intento completo en el sandbox; este es el resultado real:

1. **Descarga**: `download.unity3d.com/download_unity/3fa8bc678cb0/LinuxEditorInstaller/Unity.tar.xz`
   → completada (4.2 GB, exit 0, md5 `add30368f33370d36f3923a490734d61`). El
   changeset `3fa8bc678cb0` es el exacto del `ProjectVersion.txt`. (El CDN
   oficial solo expone md5 de las 2 últimas releases, por lo que no hubo
   contraste oficial para 6000.3.5f2.)
2. **Extracción**: 8.1 GB en `.arena/unity/editor/` (zona no persistente del
   sandbox), exit 0.
3. **Dependencias**: el sandbox es Debian trixie sin root; faltaban
   `libgtk-3/libgdk-3/libatk-1.0`. Resueltas sin tocar el sistema:
   `apt-get download libgtk-3-0t64 libatk1.0-0t64` + cascada
   (libxdamage1, libatk-bridge2.0-0t64, libcloudproviders0, libepoxy0,
   libwayland-cursor0, libwayland-egl1, libxkbcommon0, libatspi2.0-0t64),
   extraídas con `dpkg-deb -x` y cargadas con `LD_LIBRARY_PATH`.
4. **El binario funciona**: `Unity -version` respondió **`6000.3.5f2`** — el
   Editor real corre en este sandbox.
5. **BLOQUEO = LICENCIA**: `Unity -batchmode -nographics -projectPath unity
   -quit` llegó hasta el cliente de licencias y murió con:
   `[Licensing::Client] Error: Code 404 ... Found 0 entitlement groups and 0
   free entitlements` / `'com.unity.editor.headless' was not found` /
   `No valid Unity Editor license found. Please activate your license.` /
   **exit 198**. Activar requiere una cuenta Unity real (Personal) o licencia
   floating; por restricción explícita **no se inventan credenciales**.
   Evidencia verbatim: `unity/ci/logs/unity-license-block-2026-11-12.log`.
   (RAM disponible al momento del intento: 857 MB — incluso con licencia, la
   importación completa sería marginal; el bloqueo primario es la licencia.)
6. **Capturas**: **no se generó ninguna captura simulada** (prohibido). El
   pipeline de capturas quedó implementado y listo para el primer entorno con
   licencia: `Assets/_ETSV/Scripts/Editor/CaptureSceneryShots.cs`
   (`-executeMethod Etsv.EditorTools.CaptureSceneryShots.CaptureAll` bajo
   `xvfb-run` ⇒ `Captures/01-patio-general.png` … `05-almacen.png`: patio,
   mesa, microondas, trituradora, almacén — las 5 vistas pedidas). En GitHub
   Actions se ejecutaría con `game-ci/unity-test-runner` + secretos
   `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` del mantenedor (los secrets
   NO existen aún: no se inventan).

## 5. Pendientes de modelos (sin fuente CC0 con descarga directa verificable)

| Necesidad | Estado |
|---|---|
| Palé de madera | Ningún pack descargado trae modelo de palé ⇒ se mejoró el procedural (tablones). Buscar en futuras instrucciones (Kenney no publica pallet suelto; poly.pizza exige API key) |
| Herramientas manuales 3D (destornillador, llave) | No encontradas en CC0 descargable ⇒ el HUD sigue con iconos 2D (matriz P9 del GDD) |
| Chatarra suelta variada (piezas pequeñas) | Cubierta parcialmente con cajones/barril/cajas; ampliar con decalcomanías o props futuros |
| KayKit (Dungeon/Adventurers) | DESCARTADO: el zip mirror de GitHub no trae archivo de licencia ⇒ fuente no verificable (política: nada sin licencia verificable) |

## 6. Verificaciones EJECUTADAS vs PENDIENTES DE UNITY

**Ejecutadas en este entorno (todo reproducible con `bash unity/ci/build-and-test.sh`):**
- ✅ Banco C# EditMode: **84/84** (78 anteriores + M01–M06 nuevos de
  `ModelsSceneryTests.cs`: contrato de fallback headless, escala ×7, Build()
  y BuildStations() sin excepción con la vía primitiva).
- ✅ Compilación Mono de todo `Assets/_ETSV` (incluye ETSVModels.cs y los
  llamadores modificados): 0 errores.
- ✅ Integridad de los 27 FBX: 18 binarios `Kaydara FBX Binary` + 9 ASCII
  `FBX 7.3.0` con geometría verificada (Vertices + PolygonVertexIndex).
- ✅ `.meta`: cobertura 100 % de `Assets/_ETSV`, 0 GUIDs duplicados en Assets.
- ✅ Licencias: CC0 confirmado en los 3 packs (archivos originales copiados al
  repo junto a los modelos + `_SOURCE.md`).
- ✅ Tests JS de dominio intactos: 20/20 disassembly + 53/53 economy (no toca
  esta capa, re-ejecutados post-cambio).
- ✅ Unity Editor 6000.3.5f2 REAL: descarga (4.2 GB), extracción (8.1 GB),
  dependencias resueltas y binario ejecutado (`Unity -version` ⇒ 6000.3.5f2).
  La importación del proyecto está bloqueada SOLO por licencia (§4, con log de
  evidencia en `unity/ci/logs/`).

**PENDIENTES de Unity Editor (bloqueadas SOLO por licencia — ver §4; NO
declarar validado sin Editor):**
- ⏳ Importación real de los FBX (que Unity genere la Library con los
  importers `.meta` provistos) y compilación Mono del proyecto completo
  (URP + Netcode for GameObjects). El binario ya demostró ejecutarse
  (`-version` OK); falta la licencia del mantenedor.
- ⏳ Capturas automatizadas: las 5 vistas pedidas ya están scripteadas en
  `CaptureSceneryShots.CaptureAll()` (patio general, mesa de desmontaje,
  microondas, zona de maquinaria, almacén); ejecutar con licencia + xvfb.
- ⏳ Revisión visual de escala/orientación/pivotes de cada modelo en escena
  (los pivotes/escalas se midieron sobre los OBJ reales de cada pack, pero la
  conversión FBX→Unity puede requerir ajustar `bakeAxisConversion`).
- ⏳ Draw calls / SRP Batcher con los modelos reales (budget: ≤150 draw calls,
  §6 biblia artística).
- ⏳ Lightmap bake y coherencia de iluminación final.

## 7. Rendimiento (punto 6 de la instrucción)

- 27 modelos, ~34 k vértices en total (medido sobre los OBJ) ⇒ muy por debajo
  del budget de 350 k tris.
- 1 material compartido por instancia (12 materiales del set en total) ⇒ SRP
  Batcher agrupa; los repetidos (contenedores, tambores, postes) comparten
  mesh+material.
- Todo visual es estático (sin animación, sin luces reales nuevas: las
  lámparas siguen con material emisivo sin `Light`, igual que la Instr. 10).
- Cero `NetworkObject` en decoración: los modelos no se sincronizan; cada
  cliente los construye idénticos desde el layout determinista.
