# ¡ESO TODAVÍA SIRVE! 🛠️

> El simulador de chatarrería donde todo tiene valor… hasta que tus amigos lo rompen.
> Simulación cooperativa / comedia física · 1–7 jugadores · primera persona · Windows PC

**Prototipo jugable v0.1 en el navegador** — validación del loop (*golpear → romper → recuperar → pesar → vender*)
y del objetivo de estilo visual (low-poly plano, paleta corta, emisivos, UI redonda con números gigantes,
en el lenguaje de *Moon Sweepers* / sims cooperativos modernos).

![moodboard del patio de chatarrería](refs/moodboard_escena.png)

## ▶️ Jugar el prototipo

```bash
git clone https://github.com/joerojas2810-collab/eso-todavia-sirve
cd eso-todavia-sirve/prototype
python3 -m http.server 8080
# abrir http://localhost:8080
```

(Sirve por HTTP: usa ES modules. No necesita build ni dependencias: three.js r160 viene vendoreado en `prototype/vendor/`.)

| Tecla | Acción |
|---|---|
| WASD / Shift | moverse / correr |
| Mouse (clic para capturar) | mirar · fallback: clic derecho mantenido |
| Clic izq. | golpear con la llave · si tenés algo, lo lanza |
| F | agarrar / soltar / depositar en la balanza |
| R | vender todo lo que haya en la balanza |
| B | oficina de mejoras |
| Espacio | saltar |

## 📐 Principios de diseño (pitch)

1. **Una actividad sencilla satisfactoria por sí misma**: desarmar, romper, recuperar y vender.
2. **Progresión constante**: cada encargo consigue herramientas, máquinas, espacios y objetos nuevos.
3. **Caos cooperativo**: los jugadores trabajan juntos, pero sus decisiones producen situaciones inesperadas y cómicas.

## 🗂️ Contenido del repo

| Ruta | Qué es |
|---|---|
| `prototype/` | Prototipo jugable (three.js, sin build). v0.2: loop vender/romper + **desmontaje modular data-driven** (objeto 01: microondas). Incluye tests de reglas y mapa de port a Unity 6 |
| `docs/GDD_catalogo.md` | Catálogo de objetos desmontables: reglas globales, objeto 01 completo y plantilla de los 30 |
| `INFORME_tecnico.md` | Investigación: reverse-engineering del estilo de Moon Sweepers, stack recomendado (Unity 6 + URP + NGO/Relay), bases de assets y código usables, plan de vertical slice |
| `refs/` | Moodboards objetivo de estilo (imagen generada como referencia de dirección de arte, no es arte final) |

## 🎯 Hoja de ruta

- [x] Investigación de estilo y stack (`INFORME_tecnico.md`)
- [x] Prototipo v0.1: loop romper→vender + mejoras + estilo visual
- [x] **v0.2 (Instrucción 01): sistema de desmontaje modular + microondas jugable + tests de reglas**
- [ ] v0.3: objetos 02–05 del catálogo (lavadora, heladera, PC, cocina) sobre la misma arquitectura
- [ ] Port del loop validado a Unity 6 (scripts C#, ScriptableObjects, netcode NGO + Relay)
- [ ] Vertical slice co-op 4–7 jugadores

## ⚖️ Licencia y créditos

- Código del prototipo: **MIT** (ver `LICENSE`).
- [`three.js`](https://threejs.org/) r160 vendoreado: MIT © three.js authors (encabezado de licencia conservado en `prototype/vendor/three.module.js`).
- Los moodboards de `refs/` son referencias de dirección de arte generadas por IA para uso interno de diseño.
- **No se incluyen capturas ni assets de Moon Sweepers u otros juegos con copyright**: las referencias visuales del informe se consultan externamente (Steam/community) y no se redistribuyen aquí.
- El juego final se desarrollará en Unity 6; los packs de assets de terceros (Kenney CC0, Quaternius CC0, ithappy UAS) se acreditarán según sus licencias cuando se incorporen.
