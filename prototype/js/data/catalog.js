/* ============================================================================
   CATÁLOGO DE DATOS — ¡ESO TODAVÍA SIRVE!
   Capa 1: definiciones puras (sin lógica ni three.js).
   Objeto 01 según GDD 3.0 (componentes, fijaciones, dependencias, resultados)
   y GDD 5.0 (clases de peso, valores base). Las recetas usan IDs MAT-xx y
   piezas genéricas de economyData; las cantidades B_i son PROVISORIAS (PROV-07)
   porque GDD 3.0 lista materiales sin unidades.
   Los precios/valores/capacidades viven SOLO en js/data/economyData.js.
   ============================================================================ */
import { PART_DEFS } from './economyData.js';

// ---------------------------------------------------------------- fijaciones
// Cada tipo de fijación define qué herramienta la opera y cuánto tarda (T_b).
export const FASTENER_TYPES = {
  tornillo: { id: 'tornillo', name: 'tornillo',   tool: 'destornillador', time: 3.0, verb: 'Retirar tornillo',  icon: '🪛' },
  bisagra:  { id: 'bisagra',  name: 'bisagra',    tool: 'llave',          time: 2.0, verb: 'Liberar bisagra',   icon: '🔧' },
  fijacion: { id: 'fijacion', name: 'fijación',   tool: 'llave',          time: 2.5, verb: 'Liberar fijación',  icon: '🔧' },
};

// ---------------------------------------------------------------- herramientas
export const TOOLS = [
  { id: 'llave',          name: 'Llave inglesa',         icon: '🔧', key: '1' },
  { id: 'destornillador', name: 'Destornillador manual', icon: '🪛', key: '2' },
  { id: 'manos',          name: 'Manos',                 icon: '✋', key: '3' },
];
export const toolById = id => TOOLS.find(t => t.id === id);

// ---------------------------------------------------------------- objeto 01 · Microondas
// GDD 3.0: E · M · D1 · Capítulo 1 · 6 componentes
// Secuencia GDD: abrir carcasa → retirar controles y plato → extraer motor →
//                entregar módulo sellado → procesar estructura.
// recipe = resultados de descomposición: unidades abstractas de inventario
//          (GDD 5.0 §4.1: "no representan kilogramos reales").
export const OBJECT_CATALOG = [
  {
    id: 'microondas',
    name: 'Microondas',
    family: 'E', size: 'M', complexity: 'D1',
    version: 2,
    chassis: { geo: { shape: 'box', size: [0.56, 0.34, 0.42], color: 0x565b63 }, offset: [0, 0, 0] },
    components: [
      {
        id: 'carcasa', name: 'Carcasa metálica', kg: 6, weightClass: 'P2', vbase: 22, // PROV-08
        blockedBy: [],
        recipe: [{ mat: 'MAT-01', base: 4 }, { mat: 'MAT-04', base: 1 }], // PROV-07 (GDD: Acero)
        geo: {
          shape: 'box', size: [0.62, 0.40, 0.48], color: 0xe8dcc0, offset: [0, 0, 0],
          children: [{ shape: 'box', size: [0.64, 0.06, 0.50], color: 0xb4552d, offset: [0, -0.19, 0] }],
        },
        fasteners: [
          { type: 'tornillo', offset: [0.28, 0.16, -0.245] },
          { type: 'tornillo', offset: [-0.28, 0.16, -0.245] },
          { type: 'tornillo', offset: [0.28, -0.16, -0.245] },
          { type: 'tornillo', offset: [-0.28, -0.16, -0.245] },
        ],
      },
      {
        id: 'puerta', name: 'Puerta', kg: 2, weightClass: 'P1', vbase: 14, // PROV-08
        blockedBy: [],
        recipe: [{ mat: 'MAT-05', base: 2 }, { mat: 'MAT-04', base: 1 }], // PROV-07 (GDD: Vidrio, plástico)
        geo: {
          shape: 'box', size: [0.40, 0.34, 0.06], color: 0x4f6d8c, offset: [-0.10, 0.02, 0.26],
          children: [
            { shape: 'box', size: [0.28, 0.22, 0.02], color: 0x1d232b, offset: [-0.02, 0.02, 0.035] },
            { shape: 'box', size: [0.03, 0.28, 0.03], color: 0x2e2a26, offset: [0.16, 0, 0.04] },
          ],
        },
        fasteners: [
          { type: 'bisagra', offset: [-0.205, 0.13, 0] },
          { type: 'bisagra', offset: [-0.205, -0.13, 0] },
        ],
      },
      {
        id: 'panel', name: 'Panel de control', kg: 0.8, weightClass: 'P1', vbase: 18, // PROV-08
        blockedBy: [],
        recipe: [{ mat: 'MAT-06', base: 2 }], // PROV-07 (GDD: Electrónica)
        geo: {
          shape: 'box', size: [0.14, 0.34, 0.06], color: 0x2e2a26, offset: [0.22, 0.02, 0.26],
          children: [
            { shape: 'box', size: [0.08, 0.05, 0.02], color: 0xe8871f, offset: [0, 0.12, 0.035] },
            { shape: 'box', size: [0.08, 0.05, 0.02], color: 0xe8871f, offset: [0, 0.04, 0.035] },
            { shape: 'box', size: [0.08, 0.05, 0.02], color: 0x7d9a4a, offset: [0, -0.04, 0.035] },
          ],
        },
        fasteners: [
          { type: 'tornillo', offset: [0, 0.15, 0.035] },
          { type: 'tornillo', offset: [0, 0, 0.035] },
          { type: 'tornillo', offset: [0, -0.15, 0.035] },
        ],
      },
      {
        id: 'plato', name: 'Plato giratorio', kg: 1.5, weightClass: 'P1', vbase: 9, // PROV-08
        blockedBy: ['puerta'],
        recipe: [{ mat: 'MAT-05', base: 2 }], // PROV-07 (GDD: Vidrio)
        geo: { shape: 'cyl', r: 0.15, h: 0.02, color: 0xbfe3da, offset: [-0.06, -0.06, 0.02] },
        fasteners: [], // extracción directa
      },
      {
        // GDD 3.0: resultado "Motor pequeño" ⇒ la descomposición produce un
        // subensamble reutilizable (PART_DEFS.motor_pequenio, Vb $90), no materiales sueltos.
        id: 'motor', name: 'Motor de giro', kg: 3.5, weightClass: 'P2',
        vbase: PART_DEFS.motor_pequenio.vbase, // GDD 5.0 §6.3: misma pieza, mismo precio
        blockedBy: ['carcasa'],
        recipe: [{ part: 'motor_pequenio', base: 1 }],
        geo: { shape: 'cyl', r: 0.08, h: 0.16, color: 0xc47a3d, offset: [-0.06, -0.15, 0.02] },
        fasteners: [
          { type: 'tornillo', offset: [0.07, -0.06, 0] },
          { type: 'tornillo', offset: [-0.07, -0.06, 0] },
        ],
      },
      {
        // GDD 3.0/5.0: módulo sellado, sin procedimientos reales de apertura.
        // No procesable en máquinas comunes; se entrega como residuo especial.
        id: 'modulo', name: 'Módulo eléctrico sellado', kg: 4, weightClass: 'P2', vbase: 0,
        blockedBy: ['carcasa'],
        sealed: true, special: true,
        note: 'Sellado de fábrica: no se abre ni se procesa. Entrega en contenedor de residuos especiales (coste de gestión).',
        recipe: [{ mat: 'MAT-08', base: 4 }], // PROV-09: unidades de coste de gestión
        geo: {
          shape: 'box', size: [0.20, 0.16, 0.14], color: 0x3a3f45, offset: [0.15, -0.05, -0.08],
          children: [{ shape: 'box', size: [0.21, 0.05, 0.15], color: 0xe8871f, offset: [0, 0, 0] }],
        },
        fasteners: [
          { type: 'fijacion', offset: [0.09, 0.07, 0.075] },
          { type: 'fijacion', offset: [-0.09, 0.07, 0.075] },
          { type: 'fijacion', offset: [0.09, -0.07, 0.075] },
          { type: 'fijacion', offset: [-0.09, -0.07, 0.075] },
        ],
      },
    ],
  },
  // ... objetos 02–30 del GDD 3.0 se agregan acá con el mismo esquema.
];

export const objectById = id => OBJECT_CATALOG.find(o => o.id === id);
export const compById = (bp, id) => bp.components.find(c => c.id === id);
