# Prototipo v0.3 — ¡ESO TODAVÍA SIRVE!

Graybox jugable en el navegador. v0.1 validó el loop *golpear → romper → pesar → vender*;
v0.2 (Instrucción 01) agregó el **desmontaje modular data-driven** (microondas jugable).
**v0.3 (Instrucción 02) agrega el sistema integral de recuperación, inventario,
almacenamiento y economía** sobre los GDD 3.0/4.0/5.0: cada pieza extraída tiene
identidad única (StoredPart), se puede vender intacta (`V = Vb × Q/100 × M_c`) o
procesar en materiales (`Y = ⌊B × E × P⌋` con fracciones acumuladas), con almacén de
componentes por zonas (estantería/palé/suelo), almacén de materiales compartido,
reservas, residuos especiales con coste de gestión y partida persistible.

> ⚠️ Esto NO es el juego final. El juego final va en **Unity 6 + URP** (ver `../INFORME_tecnico.md`).
> Documentación de sistemas: `../docs/sistemas_recuperacion_economia.md` · catálogo: `../docs/GDD_catalogo.md`.

## Cómo correrlo

```bash
python3 -m http.server 8080 --bind 0.0.0.0 --directory prototype
# abrir http://localhost:8080
```

Tests de reglas (sin navegador):

```bash
cd prototype
node tests/disassembly.test.mjs   # 20 aserciones del desmontaje
node tests/economy.test.mjs       # 53 aserciones de economía/almacenes/identidad
```

## Controles

| Tecla | Acción |
|---|---|
| WASD / Shift | moverse / correr (con pieza P2 en manos: ×0.6) |
| Mouse (clic para capturar) | mirar · fallback: clic derecho mantenido |
| 1 / 2 / 3 | llave inglesa / destornillador manual / manos |
| CLIC mantenido | trabajar la fijación apuntada (progreso se conserva al soltar) |
| CLIC izq. | golpear (daña calidad de componentes y piezas) / lanzar lo sostenido |
| F | levantar pieza (una por vez) / soltar / depositar en balanza / procesar / entregar residuo |
| E | inspeccionar el objeto en la mesada (estados, fijaciones, calidad) |
| I | **almacén**: zonas de piezas (vender/retirar/entregar) y contenedores de materiales |
| R | vender en la balanza: lo depositado **+ todas las unidades de materiales** |
| B | oficina de mejoras |
| Espacio | saltar |

## Arquitectura en capas (Instrucción 02 §9)

| Capa | Archivo | Responsabilidad |
|---|---|---|
| 1 · Datos | `js/data/economyData.js` | **Fuente única económica**: materiales MAT-01..08 y precios (GDD 5.0 §6.2), piezas genéricas `PART_DEFS` (§6.3), clases de peso P1–P5 (§13.1), máquinas, zonas/capacidades (§8.5), registro PROVISORIO. |
| 1 · Datos | `js/data/catalog.js` | Blueprints de objetos: componentes con clase de peso, valor base y `recipe` que referencia MAT-xx/PART_DEFS. Agregar objeto = agregar datos. |
| 2 · Reglas | `js/systems/disassembly.js` | Máquina de estados del objeto en mesada, fijaciones con progreso persistente, calidad/daño. |
| 2 · Reglas | `js/systems/pieces.js` | Identidad `StoredPart`: uid, calidad, dueño, almacenamiento único, reserva, terminal excluyente, daño, serialización. |
| 2 · Reglas | `js/systems/warehouse.js` | Almacén de componentes (zonas con capacidad/categoría) y de materiales (acumulador float, unidades = floor). |
| 2 · Reglas | `js/systems/economy.js` | **Operaciones atómicas** (única vía de mutación): extraer, levantar/soltar, almacenar/retirar, procesar, vender pieza/material, entregar residuo, reservar/liberar, consumir. Valida disponibilidad, propiedad, capacidad y estado ANTES de mutar; listas para autorización del host en multijugador. |
| 3 · Estado | `js/state/gameState.js` | Estado persistible con versión: dinero + registro de piezas + almacenes (localStorage en el prototipo; save del anfitrión en producción). |
| 4 · Vista | `js/world/disassemblyView.js` + HUD | Meshes por blueprint, highlights, raycast; paneles de inspección, tienda y almacén. |
| 5 · Integración | `js/main.js` | Input → **pedidos** a `economy.js`; nunca muta estado directamente. Conserva escenario, física, balanza, trituradora/cinta, contenedores y tienda v0.1. |

### Flujo de una pieza

```
componente ──extraer──► world ──levantar──► carried ──┬─ balanza ──R──► VENDIDO
                                              │       ├─ trituradora ─► PROCESADO (materiales/subensamble al almacén)
                                              │       ├─ contenedor rojo (sellada) ─► ENTREGADO (−$ gestión)
                                              │       └─ almacenar ─► stored:estanteria|pale|suelo ──(I)──► vender/retirar
                                              └─ una sola pieza en las manos; P2 reduce velocidad
```

## Sistemas v0.1/v0.2 conservados

Escenario y movimiento · física de chatarra y lanzamiento · balanza/venta/tienda de
mejoras · trituradora + cinta + contenedores (modelos y animaciones intactos) ·
desmontaje del microondas · UI/HUD · audio sintetizado · partículas.
Cambios v0.2→v0.3 sobre sistemas existentes: el dinero y los materiales viven en el
estado persistible; la venta de piezas usa `V = Vb × Q/100 × M_c` (adiós `effPartValue`);
el módulo sellado ya **no paga $35**: entregarlo **cuesta** $20 de gestión (GDD 5.0 §6.2);
los pedazos de la cinta son visuales (el rendimiento se acredita al almacén, sin
duplicación); el motor rinde un **motor pequeño** reutilizable ($90 base).

## Mapa de port a Unity (cuando el loop esté aprobado)

| Prototipo | Unity 6 |
|---|---|
| `economyData.js` | ScriptableObjects `MaterialSO` / `PartDefSO` / `MachineSO` / `StorageZoneSO` |
| `catalog.js` | ScriptableObjects `ObjectBlueprintSO` / `ComponentSO` |
| `pieces.js` + `warehouse.js` + `gameState.js` | `PieceRegistry.cs` / `Warehouse.cs` + save del host (Newtonsoft/Addressables) |
| `economy.js` | `EconomySystem.cs` con ops `[ServerRpc]`-ready: el host autoriza cada operación |
| `disassembly.js` | `DisassemblyInstance.cs` (misma máquina de estados, sin Unity API) |
| `disassemblyView.js` | Prefabs generados por blueprint + `DisassemblyView.cs` |
| balanza/trituradora/contenedor/almacén | zonas con `IInteractable` + UI que solo pide ops |
