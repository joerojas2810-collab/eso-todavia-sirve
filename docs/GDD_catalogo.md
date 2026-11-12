# GDD — Catálogo de objetos desmontables

**¡ESO TODAVÍA SIRVE!** · Instrucción 01 + 02 · v0.3
Estado: **objeto 01 implementado y jugable** · objetos 02–30: plantilla lista, sin implementar.
Economía completa (piezas, almacenes, materiales): ver `docs/sistemas_recuperacion_economia.md`.

## Reglas globales del sistema

- Cada objeto es **data-driven**: una entrada en `prototype/js/data/catalog.js`
  (`OBJECT_CATALOG`). El motor de reglas (`js/systems/disassembly.js`) y la vista
  (`js/world/disassemblyView.js`) no conocen nombres de objetos: leen el blueprint.
- Estados de componente: `bloqueado → accesible → liberado → extraido → (procesado | vendido)`.
- No se puede extraer un componente con fijaciones pendientes ni bloqueado por dependencias.
- Al extraer, el componente se **registra como pieza con identidad** (`StoredPart`,
  GDD 5.0 §8.3) con uid único, calidad, clase de peso, valor base y receta.
- Recuperación de valor **una sola vez** por pieza: venta intacta O procesamiento O
  entrega como residuo especial. Los estados terminales (`vendido/procesado/entregado`)
  son excluyentes y definitivos.
- La calidad (0–100) solo baja por **acciones destructivas** (golpes), nunca por trabajo correcto.
- **Valor de pieza intacta (GDD 5.0 §6.3):** `V = ValorBase × (Calidad/100) × M_c`,
  con `M_c = 1.0` provisorio (PROV-03). Ya NO existe el valor automático de $35
  ni la fórmula `0.4 + 0.6q` de la v0.2: los precios salen de `js/data/economyData.js`.
- Fijaciones: `tornillo` → destornillador manual (3,0 s c/u, progreso conservado al interrumpir),
  `bisagra` → llave inglesa (2,0 s), `fijacion` → llave inglesa (2,5 s).
- Destinos de recuperación:
  - **Balanza**: venta de pieza intacta (fórmula anterior), de chatarra suelta (kg × vpk)
    y de las unidades del almacén de materiales (tecla `R`).
  - **Trituradora**: procesamiento atómico `Y_i = ⌊B_i × E × P_i⌋` con acumulación de
    fracciones; el rendimiento se acredita al **almacén de materiales** (los pedazos que
    salen por la cinta son visuales y no duplican valor).
  - **Contenedor rojo**: residuos especiales (módulos sellados). **Cuestan dinero**:
    −$5 por unidad de gestión (GDD 5.0 §6.2), no pagan tarifa.
  - **Almacén (tecla I)**: estantería (P1), palé (P2) y suelo (P3–P5) con capacidades
    físicas; vender o retirar desde ahí.

## Recetas de descomposición del microondas

`recipe` por componente (cantidades `B_i` **provisorias**, PROV-07: GDD 3.0 lista
materiales sin unidades). Precios por unidad (GDD 5.0 §6.2): acero $4 · aluminio $9 ·
cobre $18 · plástico $2 · vidrio $2 · electrónica $14 · caucho $1 · residuos −$5.

---

## Objeto 01 — MICROONDAS ✅ (implementado)

Puesto de desmontaje: mesada del patio. Inspección: tecla `E`. Almacén: tecla `I`.

| Componente | Operación | Fijaciones | Dependencias | kg | Clase | Vb | Receta (procesamiento) |
|---|---|---|---|---|---|---|---|
| Carcasa metálica | Retirar 4 tornillos | 4× tornillo | — | 6.0 | P2 | $22¹ | MAT-01 ×4, MAT-04 ×1 |
| Puerta | Liberar 2 bisagras | 2× bisagra | — | 2.0 | P1 | $14¹ | MAT-05 ×2, MAT-04 ×1 |
| Panel de control | Retirar 3 tornillos | 3× tornillo | — | 0.8 | P1 | $18¹ | MAT-06 ×2 |
| Plato giratorio | Extracción directa | — | puerta | 1.5 | P1 | $9¹ | MAT-05 ×2 |
| Motor de giro | Retirar 2 tornillos | 2× tornillo | carcasa | 3.5 | P2 | $90² | **Motor pequeño** ×1 (subensamble) |
| Módulo eléctrico sellado | Liberar 4 fijaciones | 4× fijación | carcasa | 4.0 | P2 | — | ☣️ no procesable: entrega MAT-08 ×4 (−$20) |

¹ PROV-08: no están en la tabla GDD 5.0 §6.3; se conservan los valores del catálogo v0.2.
² GDD 5.0 §6.3: "Motor pequeño $90" — misma pieza, mismo precio sin importar el objeto
  del que proviene (el motor del microondas ES un motor pequeño, `PART_DEFS.motor_pequenio`).

Notas de diseño:
- El **módulo sellado** nunca se abre: se extrae cerrado, la trituradora lo rechaza y la
  balanza no lo compra. Se **entrega** en el contenedor rojo con **coste de gestión**
  (4 u × $5 = $20, PROV-09). Si no hay fondos, la entrega se rechaza (anti-quiebra: el
  coste nunca puede dejar dinero negativo).
- El **motor** no rinde materiales sueltos: la descomposición produce un **motor pequeño**
  reutilizable (GDD 3.0 "resultados") que va directo al palé con la calidad heredada
  (PROV-10) y puede venderse intacto (`V = 90 × Q/100`).
- Decisiones económicas reales: vender intacto (más valor, más trabajo de extracción
  cuidadoso) vs. procesar (más rápido, rinde menos y pierde la identidad comercial).
- Golpear componentes con la llave daña calidad (−12 por golpe): el apuro cuesta plata,
  y la calidad afecta TANTO el valor intacto como el rendimiento (`P_i = Q/100`).

## Objetos 02–30 — plantilla (sin implementar)

Se agregan como nuevas entradas de `OBJECT_CATALOG` con el mismo esquema:
`id, name, family, size, complexity, chassis, components[{ id, name, kg, weightClass,
vbase, blockedBy, recipe, geo, fasteners }]` — las recetas referencian `MAT-xx` y
`PART_DEFS` de `economyData.js` (nunca precios propios).

| # | Objeto | # | Objeto | # | Objeto |
|---|---|---|---|---|---|
| 02 | Lavarropas | 12 | Aspiradora | 22 | Motoguadaña |
| 03 | Heladera | 13 | Cafetera | 23 | Bicicleta |
| 04 | Freezer | 14 | Tostadora | 24 | Monopatin eléctrico |
| 05 | Cocina | 15 | Pava eléctrica | 25 | Auto chico |
| 06 | Horno eléctrico | 16 | Licuadora | 26 | Camioneta |
| 07 | Lavavajillas | 17 | Procesadora | 27 | Moto |
| 08 | PC de escritorio | 18 | Ventilador | 28 | Tractor de jardín |
| 09 | Notebook | 19 | Estufa | 29 | Generador |
| 10 | Monitor CRT | 20 | Aire acondicionado | 30 | Compresor |
| 11 | Televisor | 21 | Microondas industrial | | |

Cada objeto nuevo hereda sin código extra: estados, dependencias, calidad, daño,
herramientas, identidad de pieza, almacenes, balanza/trituradora/residuo especial,
inspección y respawn.
