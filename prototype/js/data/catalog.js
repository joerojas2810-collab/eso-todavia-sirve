/* ============================================================================
   CATÁLOGO DE DATOS — ¡ESO TODAVÍA SIRVE!
   Capa 1: definiciones puras (sin lógica ni three.js).
   Acá viven los objetos desmontables del GDD (30 previstos; hoy: objeto 01).
   Agregar un objeto nuevo = agregar una entrada a OBJECT_CATALOG con sus
   componentes, fijaciones, dependencias, materiales y geometría. Nada más.
   ============================================================================ */

// ---------------------------------------------------------------- materiales
// vpk = valor por kg al vender el material recuperado en la balanza
export const MATERIALS = {
  acero:    { id: 'acero',    name: 'acero',    vpk: 1.2, color: 0x9aa2ad },
  cobre:    { id: 'cobre',    name: 'cobre',    vpk: 6.0, color: 0xc47a3d },
  plastico: { id: 'plastico', name: 'plástico', vpk: 0.9, color: 0x4f6d8c },
  vidrio:   { id: 'vidrio',   name: 'vidrio',   vpk: 0.5, color: 0xbfe3da },
};

// ---------------------------------------------------------------- fijaciones
// Cada tipo de fijación define qué herramienta la opera y cuánto tarda.
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

// ---------------------------------------------------------------- objeto 01
// Microondas — tabla de desmontaje del GDD:
//   Carcasa metálica ........ retirar 4 tornillos
//   Puerta .................. liberar 2 bisagras
//   Panel de control ........ retirar 3 tornillos
//   Plato giratorio ......... extracción directa
//   Motor de giro ........... retirar 2 tornillos
//   Módulo eléctrico sellado  liberar 4 fijaciones (residuo especial, no se abre)
//
// Geo: shape 'box' usa size [w,h,d]; 'cyl' usa r,h. offsets en metros, locales
// al centro del electrodoméstico. fasteners: offsets locales al componente.
export const OBJECT_CATALOG = [
  {
    id: 'microondas',
    name: 'Microondas',
    version: 1,
    chassis: { geo: { shape: 'box', size: [0.56, 0.34, 0.42], color: 0x565b63 }, offset: [0, 0, 0] },
    components: [
      {
        id: 'carcasa', name: 'Carcasa metálica', kg: 6, partValue: 22, blockedBy: [],
        materials: { acero: 4.5, plastico: 0.6 },
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
        id: 'puerta', name: 'Puerta', kg: 2, partValue: 14, blockedBy: [],
        materials: { vidrio: 0.8, acero: 0.9, plastico: 0.3 },
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
        id: 'panel', name: 'Panel de control', kg: 0.8, partValue: 18, blockedBy: [],
        materials: { plastico: 0.4, cobre: 0.2 },
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
        id: 'plato', name: 'Plato giratorio', kg: 1.5, partValue: 9, blockedBy: ['puerta'],
        materials: { vidrio: 1.4 },
        geo: { shape: 'cyl', r: 0.15, h: 0.02, color: 0xbfe3da, offset: [-0.06, -0.06, 0.02] },
        fasteners: [], // extracción directa
      },
      {
        id: 'motor', name: 'Motor de giro', kg: 3.5, partValue: 26, blockedBy: ['carcasa'],
        materials: { cobre: 1.6, acero: 1.4 },
        geo: { shape: 'cyl', r: 0.08, h: 0.16, color: 0xc47a3d, offset: [-0.06, -0.15, 0.02] },
        fasteners: [
          { type: 'tornillo', offset: [0.07, -0.06, 0] },
          { type: 'tornillo', offset: [-0.07, -0.06, 0] },
        ],
      },
      {
        id: 'modulo', name: 'Módulo eléctrico sellado', kg: 4, partValue: 0, blockedBy: ['carcasa'],
        sealed: true, special: true,
        note: 'Sellado de fábrica: no se abre. Se gestiona como residuo especial.',
        materials: {},
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
  // ... objetos 02–30 del GDD (lavadoras, heladeras, computadoras, vehículos,
  // maquinaria) se agregan acá con el mismo esquema, sin tocar el resto del código.
];

export const objectById = id => OBJECT_CATALOG.find(o => o.id === id);
export const compById = (bp, id) => bp.components.find(c => c.id === id);
