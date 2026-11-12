# Prototipo v0.1 — ¡ESO TODAVÍA SIRVE!

Graybox jugable en el navegador para **validar el principio 1 del pitch**:
que *golpear → romper → recuperar → pesar → vender* sea satisfactorio por sí mismo,
y para fijar el **objetivo de estilo visual** (low-poly plano, paleta corta, 1 luz,
emisivos, UI redonda con números gigantes — lenguaje de Moon Sweepers).

> ⚠️ Esto NO es el juego final. El juego final va en **Unity 6 + URP** (ver `../INFORME_tecnico.md`).
> Este prototipo es un instrumento de diseño: se itera acá en minutos, no en builds de Unity.

## Cómo correrlo

```bash
# desde la raíz del workspace
python3 -m http.server 8080 --bind 0.0.0.0 --directory eso-todavia-sirve/prototype
# abrir http://localhost:8080 (o el live preview del sandbox)
```

(Servir por HTTP y no abrir el .html directo: usa ES modules.)

## Controles

| Tecla | Acción |
|---|---|
| WASD / Shift | moverse / correr |
| Mouse (clic para capturar) | mirar · fallback: clic derecho mantenido |
| Clic izq. | golpear con la llave · si tenés algo, lo **lanza** |
| F | agarrar / soltar / depositar en la balanza |
| R | vender todo lo que haya en la balanza |
| B | oficina de mejoras (progresión, principio 2) |
| Espacio | saltar |

## Qué valida cada sistema

- **Golpe → rotura**: hp por pieza, pre-"fractura" en 2–3 chunks que valen **+30%/kg** (incentivo de desarmar).
- **Peso y capacidad**: límite de kg en mano → presión de progresión (Espalda reforzada).
- **Balanza + venta**: números grandes que "popan" = juice de recompensa.
- **Mejoras**: 3 upgrades que cambian el loop (imán, capacidad, daño), no solo números.
- **Estilo**: paleta `PAL` en `js/main.js` es la paleta candidata del juego; 1 direccional + hemisférica,
  flat shading, emisivos en visores/carteles, fog limpio.

## Mapa de port a Unity (cuando el loop esté aprobado)

| Prototipo (three.js) | Unity 6 |
|---|---|
| `bodies[]` + colisiones esfera | Rigidbodies + colliders convexos, pool de NetworkObjects |
| `held` + resorte a cámara | `PickupItem.cs` del sample U6-First-Person-Multiplayer (ownership al agarrar) |
| `hp` + chunks | pre-fractura Blender (Cell Fracture) + joints con break force |
| `state.money/scaleKg` | ScriptableObjects `ItemSO`/`MachineSO` + save JSON del host |
| `UPGRADES` | `UpgradeSO` + tienda UI (misma regla: cambios de loop, no stats) |
| `PAL` + flat shading | URP Lit simplificado + atlas + LUT de color grading |
