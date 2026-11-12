# Sistemas de recuperación, inventario, almacenamiento y economía

**¡ESO TODAVÍA SIRVE!** · Instrucción 02 · v0.3
Fuentes normativas: GDD 3.0 (objeto 01, materiales por componente), GDD 4.0 (máquinas),
GDD 5.0 §4 (recuperación y rendimiento), §5 (calidad), §6 (economía), §8 (inventario y
almacenamiento), §13.1 (clases de peso). Ningún valor económico se inventó sin fuente:
los que el GDD no especifica numéricamente están en el **registro PROVISORIO** (§9) y
requieren aprobación.

---

## 1. Arquitectura por capas (sin valores duplicados)

```
Capa 1 · DATOS (sin lógica)
  js/data/economyData.js   fuente única: materiales y precios, piezas genéricas
                           (PART_DEFS), clases de peso, máquinas, zonas y
                           capacidades, registro PROVISORIO.
  js/data/catalog.js       blueprints de objetos; las recetas referencian
                           MAT-xx / PART_DEFS de economyData (nunca precios).
Capa 2 · REGLAS (sin three.js, sin DOM)
  js/systems/disassembly.js  estados del objeto en la mesada, fijaciones, calidad.
  js/systems/pieces.js       identidad StoredPart e invariantes de estado.
  js/systems/warehouse.js    zonas de componentes + acumulador de materiales.
  js/systems/economy.js      operaciones atómicas (única vía de mutación).
Capa 3 · ESTADO PERSISTIBLE
  js/state/gameState.js      dinero + registro + almacenes; save con versión.
Capa 4 · PRESENTACIÓN
  js/world/disassemblyView.js, HUD y paneles de index.html.
Capa 5 · INTEGRACIÓN
  js/main.js                 input → pedidos a economy.js; nunca muta estado.
```

Regla de oro: **la presentación nunca muta estado**. Toda acción del jugador se
traduce en una operación atómica de `economy.js` que valida y responde
`{ok:true,…}` o `{ok:false, reason}`. En multijugador, el anfitrión será quien
ejecute estas mismas funciones con el estado autoritativo: el diseño ya está
preparado (las ops no tocan DOM ni three.js).

## 2. Identidad de pieza — `StoredPart` (GDD 5.0 §8.3)

```js
{
  uid: 'p_42',            // identificador único de instancia
  defId: 'motor',         // definición (componente del catálogo o PART_DEF)
  defName: 'Motor de giro',
  originObjectId: 'microondas_3',  // instancia del objeto de origen
  contractId: null,       // contrato de origen (futuro: GDD 5.0 §6.4)
  quality: 88,            // 0–100, solo baja por daño
  kg: 3.5,
  weightClass: 'P2',      // P1–P5 (GDD 5.0 §13.1)
  vbase: 90,              // valor base (GDD 5.0 §6.3)
  owner: 'P1',            // dueño (futuro: otros jugadores)
  storage: 'carried',     // world | carried | scale | stored:<zona> | none
  reservedFor: null,      // contrato | fabricacion | mantenimiento | construccion
  terminal: null,         // vendido | procesado | entregado  (excluyente, 1 vez)
  sealed: false,          // módulo sellado: sin máquinas comunes ni venta
  protected: false,       // componente narrativo protegido (campaña futura)
  recipe: [{mat:'MAT-01', base:4}, …] | [{part:'motor_pequenio', base:1}],
}
```

**Invariante central:** una pieza no puede estar instalada, almacenada y vendida a la
vez. `storage` es único mientras vive; `terminal` la cierra definitivamente y lleva
`storage` a `none`. Ambos cambios pasan por `pieces.js`, que los verifica.

### Transiciones de estado

```
            extractPiece
  (componente) ──────────► world
                            │  pickupPiece (1 en manos, P1/P2)      dropPiece
                            ▼ ◄────────────────────────────────────────┐
                          carried ──► placeOnScale ──► scale           │
                            │  storePiece(zona)          │ sellPiece   │
                            ▼                            ▼             ▼
                     stored:<zona> ── retrievePiece    [terminal: vendido]
                       │        │
       processPiece    │        │  sellPiece        disposeSpecial (selladas)
                       ▼        ▼                        ▼
              [terminal: procesado]  [terminal: vendido]  [terminal: entregado]
```

Bloqueos validados en cada transición: pieza terminal, reservada, de otro dueño,
protegida/sellada, clase de peso no aceptada por la zona, zona llena, manos ocupadas,
contenedor lleno, fondos insuficientes, material inexistente/insuficiente.

## 3. Materiales (GDD 5.0 §6.2) — fuente: `economyData.MATERIALS`

| ID | Material | $/unidad |
|---|---|---|
| MAT-01 | Acero/hierro | 4 |
| MAT-02 | Aluminio | 9 |
| MAT-03 | Cobre | 18 |
| MAT-04 | Plástico | 2 |
| MAT-05 | Vidrio | 2 |
| MAT-06 | Electrónica | 14 |
| MAT-07 | Caucho/mixtos | 1 |
| MAT-08 | Residuos especiales | **−5** (coste de gestión) |

Las cantidades de las recetas son **unidades abstractas de inventario**, no kilogramos
(GDD 5.0 §4.1). MAT-08 nunca se acumula ni se vende: solo se **paga** al entregar
residuos especiales (módulos sellados), salvo que un contrato lo cubra (§6.4, futuro).

## 4. Procesamiento (GDD 5.0 §4.2/§4.3)

```
Y_i = B_i × E × P_i        B_i: unidades base de la receta (datos, PROV-07)
                           E:   eficiencia de máquina (trituradora básica 0.9, PROV-01)
                           P_i: factor de conservación = calidad/100 (PROV-02)
unidades acreditadas = floor(acumulado)   acumulado += Y_i  (fracciones se conservan)
```

- El acumulador es **float por material** en el almacén: los residuos fraccionarios de
  un lote se suman al siguiente y las unidades aparecen al pisar el entero. Nada se
  pierde ni se redondea dos veces.
- `processPiece` es **una sola transacción**: valida todo (capacidad de contenedores y
  de zonas de subensambles), consume la pieza (terminal `procesado`) y acredita
  materiales/subensambles en el mismo commit. **Nunca se duplican materiales**: los
  pedazos que salen por la cinta son visuales (`meta.visual`, kg 0, desaparecen solos)
  y no se pueden recoger ni pesar.
- La trituradora básica **no preclasifica**: en el prototipo los materiales salen por
  receta del componente (equivalente a preclasificar). Cuando haya mezcla sin
  preclasificar (GDD 4.0 §máquinas), se agregará como otra máquina con penalización.
- Subensambles: el motor del microondas rinde **1 motor pequeño** (`PART_DEFS`,
  Vb $90, GDD 5.0 §6.3) que entra directo al palé con la calidad heredada (PROV-10).
- Sellados y protegidos: `maquina_incompatible_sellado`. El módulo sellado solo se
  entrega en el contenedor rojo: `disposeSpecial` cobra `unidades MAT-08 × $5`
  (rechaza sin fondos; el dinero nunca queda negativo: anti-quiebra GDD 5.0 §6.6).

## 5. Venta de pieza intacta (GDD 5.0 §6.3)

```
V = ValorBase × (Calidad/100) × ModificadorComercial      M_c = 1.0 (PROV-03)
```

Verificado contra el ejemplo del GDD: motor calidad 85, Vb $300 → $255. ✔
Se vende desde las manos, la balanza o el almacén. **No** se puede vender: pieza
reservada, de otro dueño, terminal, sellada o protegida. La chatarra suelta v0.1
(props sin identidad) conserva su regla simple `kg × vpk` en la balanza.

## 6. Inventario y almacenamiento (GDD 5.0 §8.1/§8.5)

Tres sistemas separados:

1. **Inventario personal**: herramientas (1 activa) + **una** pieza física en las manos
   (`ctx.carry`; `manos_ocupadas` si ya hay una). P1 sin penalización; P2 reduce la
   velocidad a ×0.6 (GDD 5.0 §13.1); P3+ no se puede llevar (requiere carro/grúa,
   aún no implementados: PROV-06).
2. **Almacén de componentes** (`warehouse.js`): piezas individuales con calidad,
   origen, dueño y reserva, en zonas físicas con capacidad y categoría:

   | Zona | Acepta | Capacidad |
   |---|---|---|
   | Estantería | P1 | 8 (PROV-04) |
   | Palé | P2 | 6 (PROV-04) |
   | Suelo | P3–P5 | 4 (PROV-04) |

3. **Almacén de materiales**: cantidades compartidas de los 8 materiales en
   contenedores (capacidad 120 u por material, PROV-05), con acumulador fraccionario.

Panel del almacén: tecla `I` (retirar, vender piezas, vender materiales, entregar
sellados estando junto al contenedor rojo). La balanza (`R`) vende lo depositado **y**
todas las unidades enteras de materiales con precio positivo.

## 7. Contratos públicos — `js/systems/economy.js`

Todas reciben `ctx = {reg, cwh, mwh, carry, getMoney(), addMoney(n), onChanged()}`
y devuelven `{ok:true,…}` o `{ok:false, reason,…}`. Ninguna muta si una validación falla.

| Función | Efecto |
|---|---|
| `pieceValue(ctx, p)` | float `Vb × Q/100 × M_c` |
| `extractPiece(ctx, opts)` | registra pieza nueva en `world` |
| `pickupPiece / dropPiece` | `world ⇄ carried` (1 pieza, clase portable) |
| `placeOnScale(ctx, uid)` | `carried → scale` |
| `storePiece / retrievePiece` | `carried ⇄ stored:<zona>` (categoría + capacidad) |
| `processPiece(ctx, uid, machineId, recipe?)` | transacción única: consume pieza y acredita materiales/subensambles |
| `sellPiece(ctx, uid)` | venta intacta; agrega dinero; terminal `vendido` |
| `sellMaterial(ctx, matId, units)` | vende unidades enteras del almacén |
| `disposeSpecial(ctx, uid)` | entrega sellada; **cobra** gestión; terminal `entregado` |
| `reservePiece / releasePiece` | reservas (contrato/fabricación/mantenimiento) |
| `consumeMaterials(ctx, needs)` | consumo atómico por lote para recetas futuras |
| `damagePiece(ctx, uid, amt)` | baja calidad (nunca por trabajo correcto) |

## 8. Persistencia (capa 3)

`gameState.js` serializa dinero, piezas (terminales y almacenadas) y almacenes con
`version: 3`; guarda en `localStorage` con debounce tras cada operación exitosa.
Al entrar al patio se carga la partida si existe. **PROV-11**: las piezas físicas en
el mundo/en la mano no se restauran (en producción quedarán en el save del anfitrión
con su posición; en el prototipo se descartan al salir).

## 9. Registro PROVISORIO — decisiones que requieren aprobación

Viven en `economyData.PROVISIONAL` (fuente única) y están marcadas en el código:

| ID | Campo | Valor usado | Por qué es provisorio |
|---|---|---|---|
| PROV-01 | `MACHINES.trituradora_basica.E` | 0.9 | GDD 4.0 describe la eficiencia pero no da el número |
| PROV-02 | `conservationFactor` | `q/100` | GDD 5.0 §4.3 da la regla, no la forma funcional exacta |
| PROV-03 | `COMMERCIAL_MOD` | 1.0 | Sin bonificaciones de mercado activas todavía |
| PROV-04 | Capacidades estantería/palé/suelo | 8/6/4 | GDD §8.5 pide capacidades físicas, no las numera |
| PROV-05 | Capacidad contenedor de material | 120 u | Idem |
| PROV-06 | P3+ no transportables | `carryable:false` | Carro/grúa/polipasto aún no implementados |
| PROV-07 | Cantidades `B_i` recetas microondas | ver catálogo | GDD 3.0 lista materiales sin unidades |
| PROV-08 | Vb carcasa/puerta/panel/plato | 22/14/18/9 | No están en la tabla §6.3; se conservan de v0.2 |
| PROV-09 | Gestión del módulo sellado | MAT-08 ×4 → −$20 | Unidades de residuo del módulo no especificadas |
| PROV-10 | Calidad de subensambles | hereda la del origen | GDD no define calidad de piezas procesadas |
| PROV-11 | Persistencia física | parcial | Piezas en mundo/mano no se restauran al cargar |

## 10. Verificaciones automáticas

`prototype/tests/economy.test.mjs` (53 checks) + `prototype/tests/disassembly.test.mjs`
(20 checks). Cubren, entre otros: fórmula de valor contra el ejemplo del GDD;
rendimiento con fracciones entre lotes (3.6+3.6 → 7 u, no 6); imposibilidad de
procesar/vender dos veces la misma pieza; venta de pieza reservada/ajena/terminal/
protegida bloqueada; capacidades y categorías por zona; operación fallida no muta
estado; subensamble motor pequeño ($90, calidad heredada); sellado no se tritura ni
se vende y su entrega cobra $20 (rechazada sin fondos); consumo inexistente falla;
lote de consumo atómico (todo o nada); una sola pieza en las manos; round-trip de
persistencia (dinero, piezas almacenadas, fracciones).

## 11. Lo que NO se hizo (por instrucción)

Sin objetos nuevos, sin contratos completos (solo el campo `contractId` y las ops de
reserva listas), sin robots, sin multijugador (solo el diseño host-authoritative),
sin carro/grúa para P3+. Los modelos y comportamientos visuales de balanza,
trituradora, cinta y contenedores siguen intactos.
