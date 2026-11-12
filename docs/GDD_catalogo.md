# GDD — Catálogo de objetos desmontables

**¡ESO TODAVÍA SIRVE!** · Instrucción 01 · v0.2
Estado: **objeto 01 implementado y jugable** · objetos 02–30: plantilla lista, sin implementar.

## Reglas globales del sistema

- Cada objeto es **data-driven**: una entrada en `prototype/js/data/catalog.js`
  (`OBJECT_CATALOG`). El motor de reglas (`js/systems/disassembly.js`) y la vista
  (`js/world/disassemblyView.js`) no conocen nombres de objetos: leen el blueprint.
- Estados de componente: `bloqueado → accesible → liberado → extraido → (procesado | vendido)`.
- No se puede extraer un componente con fijaciones pendientes ni bloqueado por dependencias.
- No se puede vender/procesar sin extraer antes (`setTerminal` solo desde `extraido`).
- Recuperación de valor **una sola vez** por componente (balanza O trituradora O residuo especial).
- La calidad (0–100) solo baja por **acciones destructivas** (golpes), nunca por trabajo correcto.
  Valor de pieza intacta = `partValue × (0.4 + 0.6 × calidad/100)`.
- Fijaciones: `tornillo` → destornillador manual (3,0 s c/u, progreso conservado al interrumpir),
  `bisagra` → llave inglesa (2,0 s), `fijacion` → llave inglesa (2,5 s).
- Destinos de recuperación:
  - **Balanza**: venta de pieza intacta (por calidad) o de materiales (por kg × vpk).
  - **Trituradora**: procesa la pieza extraída en sus materiales (salen por la cinta).
  - **Contenedor rojo**: residuos especiales (módulos sellados), tarifa fija de gestión.

## Materiales y precios por kg

| Material | $/kg |
|---|---|
| cobre | 6.0 |
| acero | 1.2 |
| plástico | 0.9 |
| vidrio | 0.5 |

---

## Objeto 01 — MICROONDAS ✅ (implementado)

Puesto de desmontaje: mesada del patio. Inspección: tecla `E`.

| Componente | Operación | Fijaciones | Dependencias | kg | Valor intacto | Materiales |
|---|---|---|---|---|---|---|
| Carcasa metálica | Retirar 4 tornillos | 4× tornillo | — | 6.0 | $22 | acero 4.5, plástico 0.6 |
| Puerta | Liberar 2 bisagras | 2× bisagra | — | 2.0 | $14 | vidrio 0.8, acero 0.9, plástico 0.3 |
| Panel de control | Retirar 3 tornillos | 3× tornillo | — | 0.8 | $18 | plástico 0.4, cobre 0.2 |
| Plato giratorio | Extracción directa | — | puerta | 1.5 | $9 | vidrio 1.4 |
| Motor de giro | Retirar 2 tornillos | 2× tornillo | carcasa | 3.5 | $26 | cobre 1.6, acero 1.4 |
| Módulo eléctrico sellado | Liberar 4 fijaciones | 4× fijación | carcasa | 4.0 | no se vende intacto | **residuo especial (+$35)** |

Notas de diseño:
- El **módulo sellado** nunca se abre: se extrae cerrado y se clasifica como residuo especial.
  La trituradora lo rechaza con mensaje.
- Ruta óptima de valor: desmontar completo y vender intacto ≈ $89 + gestión $35;
  triturar todo rinde menos pero es más rápido → decisión económica real del jugador.
- Golpear componentes con la llave daña calidad (−12 por golpe): el apuro cuesta plata.

## Objetos 02–30 — plantilla (sin implementar)

Se agregan como nuevas entradas de `OBJECT_CATALOG` con el mismo esquema:
`id, name, chassis, components[{ id, name, kg, partValue, blockedBy, materials, geo, fasteners }]`.

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
herramientas, balanza/trituradora/residuo especial, inspección y respawn.
