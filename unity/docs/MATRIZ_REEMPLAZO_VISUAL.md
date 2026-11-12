# MATRIZ DE REEMPLAZO VISUAL — ¡ESO TODAVÍA SIRVE!

Instrucción 09, Fase 6. Qué elemento visual provisorio se reemplaza, por qué
candidato, con qué licencia, cuánto trabajo lleva y en qué orden. Regla dura:
**el reemplazo es SIEMPRE visual** — no se modifica lógica económica, física
ni de desmontaje para adaptar un modelo, y se prioriza lo gratuito/ya
incluido. Companion de `DIRECCION_ARTISTICA.md` (paleta, escala, checklist).

## A. Inventario de candidatos (Fase 4 — auditoría de recursos)

| # | Fuente | Enlace | Licencia | Precio | Qué aporta al juego | Unity 6 + URP | Calidad/estilo | Modificable | Trabajo de integración |
|---|---|---|---|---|---|---|---|---|---|
| A1 | **Kenney — City Kit Industrial** | kenney.nl/assets/city-kit-industrial | **CC0** (sin atribución) | Gratis | 40 assets: fábricas, galpones, silos, containers → perímetro del patio | Sí (FBX/OBJ genérico; materiales a rehacer en URP/Lit) | Low-poly limpio, algo "plano" → base para re-texturizar con atlas propio (§4 biblia) | Sí (blend/FBX simples) | BAJO-MEDIO: re-texturizar y montar perímetro estático |
| A2 | **Kenney — Prototype Textures + Kenney Mini/Tools** | kenney.nl/assets | CC0 | Gratis | Texturas guía, props pequeños (herramientas estilizadas) | Sí | Minimalista | Sí | BAJO |
| A3 | **Quaternius — Ultimate Animated Characters** | quaternius.com/packs/ultimatedanimatedcharacter.html | **CC0** (la página del pack 2019 lo confirma; el sitio anunció QAL v1.0 solo para releases futuros — archivar evidencia) | Gratis | 50+ personajes rigged/animados (FBX/OBJ/Blend) → avatares de los 7 jugadores | Sí (FBX + Mecanim; animaciones genéricas retargetables) | Low-poly estilizado, coherente con la biblia | Sí (.blend incluidos) | MEDIO: elegir 1 base, retarget idle/walk/run/carry, color por equipo |
| A4 | **Quaternius — Modular Sci-Fi MegaKit (Standard, gratis)** | quaternius.com/packs/modularscifimegakit.html | CC0 (versión Standard 190 modelos; Source de pago opcional — NO necesaria) | Gratis | Estructuras modulares, crates, paneles industriales → estanterías, casillas, contenedores | Sí (la versión Source trae proyecto URP; la Standard son FBX/glTF sueltos) | Industrial modular detallado | Sí | MEDIO: selección + re-texturizado a paleta chatarrería (es sci-fi, hay que "ensuciarlo") |
| A5 | **KayKit — Adventurers + Dungeon Remastered** | kaylousberg.itch.io | **CC0** (tier gratis; Extra/Source de pago opcionales — NO necesarios) | Gratis | Personajes rigged alternativos; barrels, crates, furniture → props de patio | Sí (hecho con Unity/Godot en mente) | Low-poly estilizado con atlas de gradiente (rendimiento excelente) | Sí | BAJO-MEDIO |
| A6 | **Poly Haven — HDRIs + PBR + modelos** | polyhaven.com | **CC0** | Gratis | HDRI de cielo (iluminación §5), textures PBR metal/hormigón/madera 1K–2K, modelos sueltos | Sí (PBR met/rough estándar; HDRI como skybox) | Foto-real → se usa para MATERIALES (detalle industrial), no para geometría estilizada | Sí | BAJO: skybox + set de materiales |
| A7 | **ambientCG — PBR industrial** | ambientcg.com | **CC0** | Gratis | 2000+ materiales (Metal 028/019/036, Concrete, Wood, Rust…) → atlas de óxido/grasa/rayones | Sí (PBR met/rough; webp→convertir a png/tga en import) | Foto-real, seamless | Sí | BAJO-MEDIO: curated de ~10 materiales + conversión de formato |
| A8 | **Unity Asset Store — packs industriales de pago** (p. ej. search "industrial props", "scrapyard", "appliance") | assetstore.unity.com | EULA Asset Store (uso en proyecto OK; NO redistribuible) | USD 0–60 típico | Electrodomésticos, maquinaria, herramientas específicas que ningún CC0 cubre bien (microondas realista, balanzas, trituradoras) | Sí (la mayoría declara URP) | Variable — auditar por pack | Variable | MEDIO-ALTO + **compra requiere aprobación explícita del propietario** (restricción del proyecto: no comprar assets). Por ahora: NO se usa; queda como plan B documentado |
| A9 | **Kenney — Furniture Kit / Kitchen (2D y 3D)** | kenney.nl/assets | CC0 | Gratis | Muebles/electrodomésticos simples | Sí | Muy estilizado | Sí | BAJO |

Descartados: cualquier marketplace sin licencia clara (Sketchfab con licencias
mixtas CC-BY que obligan atribución en pantalla, TurboSquid de pago, modelos
"free" de origen desconocido) — restricción del proyecto: nada de licencias
incompatibles ni origen desconocido. CC-BY se evitaría por fricción de
atribución en un juego multiplayer; si se necesitara, se documentaría el
crédito en los títulos.

## B. Matriz de reemplazo elemento → candidato

Orden de prioridad = impacto en el juego × bajo riesgo (1 = primero).
"Anclaje" indica qué NO debe tocarse (fuente de verdad del código).

| P | Elemento (hoy) | Reemplazo propuesto | Lic. | Anclaje intocable | Trabajo |
|---|---|---|---|---|---|
| 1 | **Iluminación/cielo** (skybox default + sol) | HDRI Poly Haven (A6) + sol cálido §5 biblia; Lightmaps baked | CC0 | `Multiplayer.unity` (objetos de red, spawn points, NetworkManager) — solo tocar luz/skybox/volumen | Bajo |
| 2 | **Materiales base** (colores planos `MakeMaterial`) | Set propio URP/Lit con PBR ambientCG/Poly Haven (A6/A7): metal oxidado, madera palet, hormigón, metal pintado | CC0 | `BlueprintBuilder.MakeMaterial` mantiene su firma; se agregan materiales por nombre/atlas | Bajo |
| 3 | **Mesa de trabajo** (cubo+4 patas `BuildWorkbench`) | Mesa industrial Kenney/KayKit (A2/A5) o mesh propio sobre el mismo footprint 1.6×0.9×1.0 | CC0 | Posición = `ScrapyardLayout.TableAnchor()`; altura de trabajo 0.95 | Bajo |
| 4 | **Patio/perímetro** (terreno ProBuilder del upstream) | Valla/silos/galpones Kenney City Kit Industrial (A1) alrededor del play-space; suelo con material tierra §2.1 | CC0 | Spawn points ("Player 1..4 Spawn"), navegación existente; no bloquear el raycast de interacción hacia las estaciones | Medio |
| 5 | **Estaciones 0–5** (primitivos + TextMesh: balanza, trituradora, residuos, estantería, palé, suelo) | Modelos CC0: balanza = caja+display (A4/A5), trituradora = Sci-Fi MegaKit "grinder"/containers (A4), estantería = Dungeon racks (A5), palé = KayKit/Kenney (A5/A2), residuos = tambor rojo con franjas ámbar (A4 + decal propio) | CC0 | `BuildStations`: raíz con BoxCollider 1×1.6×1 + `StationRef` + offsets; los meshes son CHILDREN visuales; ids 0–13 y gating intactos | Medio |
| 6 | **Contenedores de material 6–13** (cilindros/cubos de color) | Contenedores/tambores A4/A1 con banda de color §2.3 por material + número pintado (decal) | CC0 | Mismo anclaje que fila 5 (offsets 6–13) | Medio |
| 7 | **Microondas (objeto 01)** (primitivos por slot de `DisassemblyLayouts`) | Electrodoméstico estilizado: cuerpo + puerta con bisagra + panel; componentes (carcasa, puerta, motor, placa) como meshes independientes en los slots existentes | CC0 (A9/A4) o modelado propio simple | Slots y pivotes de `DisassemblyLayouts` (el dominio los usa para extracción); el blueprint NO cambia | Medio |
| 8 | **Piezas en mano/suelo** (cubos `PieceColor`) | Meshes de pieza por defId (puerta, carcasa, motor_pequenio, placa…): kit de props pequeños propios sobre primitivos, ≤500 tris, material con calidad visible §9 biblia | CC0/propio | `DisassemblyPiece.prefab` (NetworkObject+NetworkTransform+PickupItem+PieceView, GUID y NVs intactos) — se cambia el mesh del child visual | Medio |
| 9 | **Herramientas** (solo HUD de texto hoy) | Props de mano Kenney Tools (A2) + iconos en el HUD UI Toolkit | CC0 | `GameCatalog.Tools` (ids/nombres); el HUD solo agrega iconos | Bajo |
| 10 | **Personajes** (cápsula U6-FPM con ojos) | Quaternius Ultimate Animated (A3) o KayKit Adventurers (A5): 1 rig base + color de equipo (`PlayerColour` existente) | CC0 | Prefab "Multiplayer Controller" (NetworkObject, PlayerMovement, PlayerInteraction, cámara Cinemachine) — el mesh es child visual; collider cápsula se mantiene | Medio-alto |
| 11 | **VFX de estaciones** (ninguno) | Partículas URP propias (chispas/polvo/humo) con paleta §2.2 | propio | Ninguno (capa presentation pura) | Bajo |
| 12 | **Objetos 02–30** (futuro, NO empezar — Instrucción 09) | Cuando llegue su instrucción: mismo patrón que fila 7 (meshes por slot) | CC0/propio | Blueprints del catálogo (fuente de verdad GDD) | — |

## C. Orden de implementación recomendado (por instrucciones futuras)

1. **P1+P2 (iluminación + materiales)**: máximo impacto visual por hora de
   trabajo; cero riesgo de red. Deja el juego "lindo" aunque todo siga siendo
   primitivo.
2. **P3+P5+P6 (mesa + estaciones + contenedores)**: el escenario de juego
   completo; todo es child visual de raíces con collider ya validado.
3. **P7+P8 (microondas + piezas)**: el corazón interactivo; exige cuidado con
   pivotes/slots (prueba: extraer una pieza debe verse igual de bien).
4. **P4 (perímetro)**: decoración de contexto; puede esperar.
5. **P10 (personajes)**: el más caro (rigging/retarget/animaciones) y el que
   más arriesga el feel del movimiento — se hace cuando lo anterior esté
   estable. Mientras tanto la cápsula con color de equipo es funcional.
6. **P9, P11**: pulido.

Cada paso: importar a `_ETSV/Art/` (§8 biblia), reemplazar primitivos en el
builder correspondiente o como children de prefabs visuales, correr banco
`unity/ci/build-and-test.sh` (la lógica no debe enterarse), y verificar la
checklist §12 de la biblia.

## D. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Modelo CC0 "contamina" el estilo (sci-fi limpio en chatarrería) | Re-texturizado obligatorio con atlas propio (regla §7.1 biblia) |
| Quaternius cambió a licencia QAL v1.0 para releases futuros | Usar packs con CC0 verificado (página del pack + FAQ); archivar captura/descarga con fecha en `_SOURCE.md` |
| Collider nuevo tapa el raycast de interacción (la base no usa LayerMask) | Ningún child visual lleva collider; solo la raíz de estación (ya existente) |
| Mesh pesado hunde los 60 fps con 7 jugadores | Presupuesto §6 biblia + LODs + revisión por instrucción |
| Cambiar el prefab de red al tocar visuales | Los prefabs de `_ETSV/Resources/ETSV/` solo reciben cambios en children visuales/materiales; GUIDs, NVs y componentes de red NO se tocan (auditoría de prefab YAML tras cada cambio) |
