# Prototipo v0.2 — ¡ESO TODAVÍA SIRVE!

Graybox jugable en el navegador. v0.1 validó el loop *golpear → romper → pesar → vender*
y el estilo visual. **v0.2 (Instrucción 01) agrega el sistema de desmontaje modular
data-driven**, con el objeto 01 del GDD (microondas) totalmente jugable:
inspeccionar → retirar fijaciones con la herramienta correcta → extraer componentes
respetando dependencias → clasificar (balanza / trituradora / residuo especial) → vender.

> ⚠️ Esto NO es el juego final. El juego final va en **Unity 6 + URP** (ver `../INFORME_tecnico.md`).
> Este prototipo es un instrumento de diseño y el banco de pruebas del sistema de desmontaje.

## Cómo correrlo

```bash
python3 -m http.server 8080 --bind 0.0.0.0 --directory prototype
# abrir http://localhost:8080
```

Test de reglas (sin navegador):

```bash
cd prototype && node tests/disassembly.test.mjs   # 22 aserciones del sistema de desmontaje
```

## Controles

| Tecla | Acción |
|---|---|
| WASD / Shift | moverse / correr |
| Mouse (clic para capturar) | mirar · fallback: clic derecho mantenido |
| 1 / 2 / 3 | llave inglesa / destornillador manual / manos |
| CLIC mantenido | trabajar la fijación apuntada (progreso se conserva al soltar) |
| CLIC izq. | golpear (daña calidad de componentes y piezas) / lanzar lo sostenido |
| F | agarrar / soltar / depositar en balanza / procesar en trituradora / residuo especial |
| E | inspeccionar el objeto en la mesada (estados, fijaciones, calidad) |
| R | vender lo depositado en la balanza |
| B | oficina de mejoras |
| Espacio | saltar |

## Arquitectura en capas (Instrucción 01 §5)

| Capa | Archivo | Responsabilidad |
|---|---|---|
| 1 · Datos | `js/data/catalog.js` | Materiales, tipos de fijación, herramientas y blueprints de objetos (componentes, fijaciones, dependencias, materiales, geometría). Agregar objeto = agregar datos. |
| 2 · Reglas | `js/systems/disassembly.js` | Máquina de estados (bloqueado→accesible→liberado→extraido→procesado/vendido), dependencias, trabajo con progreso persistente, calidad/daño, valor por calidad, cierre terminal único. Puro, testeable sin navegador. |
| 3 · Vista | `js/world/disassemblyView.js` | Construcción de meshes desde el blueprint, visibilidad por estado, pulso de fijaciones accesibles, highlight de mira, entrega del mesh al mundo físico al extraer. |
| 4 · Integración | `js/main.js` | Input, herramientas, raycast de trabajo, recuperación (balanza/trituradora/contenedor), economía, HUD, inspección, respawn. Conserva todos los sistemas v0.1. |

### Flujo de un componente

```
bloqueado ──(dependencias retiradas)──> accesible ──(todas sus fijaciones)──> liberado
   liberado ──(manos + CLIC)──> extraido ──> vendido (balanza)
                                        └─> procesado (trituradora → materiales)
                                        └─> procesado (contenedor rojo, si es sellado)
```

## Sistemas v0.1 conservados

Escenario y movimiento · física de chatarra y lanzamiento · economía/inventario/materiales ·
balanza/venta/tienda de mejoras · UI y HUD · audio sintetizado · partículas · journe de cámara.
Cambios v0.1→v0.2 sobre sistemas existentes: la balanza ahora valora piezas intactas por
calidad (`partValueOf`), el hotbar pasó a ser selector de herramientas (las mejoras quedaron
como mini-slots) y los golpes además de empujar **dañan calidad**.

## Mapa de port a Unity (cuando el loop esté aprobado)

| Prototipo | Unity 6 |
|---|---|
| `catalog.js` | ScriptableObjects `ObjectBlueprintSO` / `ComponentSO` / `MaterialSO` |
| `disassembly.js` | `DisassemblyInstance.cs` (misma máquina de estados, sin Unity API) |
| `disassemblyView.js` | Prefabs generados por blueprint + `DisassemblyView.cs` |
| trabajo sostenido + progreso | `WorkInteraction.cs` con `hold` y `NetworkVariable` de progreso |
| balanza/trituradora/contenedor | zonas con `IInteractable` + economía del host |
