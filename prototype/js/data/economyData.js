/* ============================================================================
   CAPA DE DATOS — ECONOMÍA (fuente única de verdad económica)
   Fuentes: GDD 5.0 §6.2 (precios), §6.3 (valores base de piezas),
            §4.2 (rendimiento), §8.5 (capacidades), GDD 3.0 §13.1 (pesos).
   Ningún otro archivo define precios, recetas ni capacidades: se importan de acá.
   Los valores PROVISORIOS (numéricos no especificados por los GDD) están
   registrados en PROVISIONAL y documentados en docs/sistemas_recuperacion_economia.md
   ============================================================================ */

// ---------------------------------------------------------------- materiales (GDD 5.0 §6.2)
// price = créditos por unidad. MAT-08 tiene precio NEGATIVO: coste de gestión.
export const MATERIALS = {
  'MAT-01': { id: 'MAT-01', name: 'Acero/hierro',      price: 4,  color: 0x9aa2ad },
  'MAT-02': { id: 'MAT-02', name: 'Aluminio',          price: 9,  color: 0xc9d2da },
  'MAT-03': { id: 'MAT-03', name: 'Cobre',             price: 18, color: 0xc47a3d },
  'MAT-04': { id: 'MAT-04', name: 'Plástico',          price: 2,  color: 0x4f6d8c },
  'MAT-05': { id: 'MAT-05', name: 'Vidrio',            price: 2,  color: 0xbfe3da },
  'MAT-06': { id: 'MAT-06', name: 'Electrónica',       price: 14, color: 0x7d9a4a },
  'MAT-07': { id: 'MAT-07', name: 'Caucho/mixtos',     price: 1,  color: 0x3a3a3a },
  'MAT-08': { id: 'MAT-08', name: 'Residuos especiales', price: -5, color: 0xa8433a },
};
export const MAT_IDS = Object.keys(MATERIALS);

// ---------------------------------------------------------------- piezas genéricas reutilizables (GDD 5.0 §6.3)
// "Una misma pieza no debería cambiar de precio según el objeto del que proviene."
export const PART_DEFS = {
  motor_pequenio:          { id: 'motor_pequenio',          name: 'Motor pequeño',         vbase: 90,   kg: 3,  weightClass: 'P2' },
  motor_electrodomestico:  { id: 'motor_electrodomestico',  name: 'Motor de electrodoméstico', vbase: 300, kg: 9, weightClass: 'P3' },
  bomba_pequenio:          { id: 'bomba_pequenio',          name: 'Bomba pequeña',         vbase: 160,  kg: 4,  weightClass: 'P2' },
  placa_electronica:       { id: 'placa_electronica',       name: 'Placa electrónica común', vbase: 110, kg: 1, weightClass: 'P1' },
  procesador:              { id: 'procesador',              name: 'Procesador',            vbase: 180,  kg: 0.5, weightClass: 'P1' },
  alternador:              { id: 'alternador',              name: 'Alternador',            vbase: 420,  kg: 6,  weightClass: 'P2' },
  motor_motocicleta:       { id: 'motor_motocicleta',       name: 'Motor de motocicleta',  vbase: 650,  kg: 22, weightClass: 'P3' },
  motor_automovil:         { id: 'motor_automovil',         name: 'Motor de automóvil',    vbase: 1400, kg: 90, weightClass: 'P4' },
  motor_industrial:        { id: 'motor_industrial',        name: 'Motor industrial',      vbase: 1800, kg: 200, weightClass: 'P5' },
};

// ---------------------------------------------------------------- clases de peso (GDD 5.0 §13.1)
// P1 ligera · P2 media (movimiento reducido) · P3+ requiere carro/grúa (no implementado: PROV-06)
export const WEIGHT_CLASSES = {
  P1: { id: 'P1', name: 'Ligera',  carryable: true,  speedMul: 1.0, storage: 'estanteria' },
  P2: { id: 'P2', name: 'Media',   carryable: true,  speedMul: 0.6, storage: 'pale' },
  P3: { id: 'P3', name: 'Pesada',  carryable: false, speedMul: 0,   storage: 'suelo' },
  P4: { id: 'P4', name: 'Muy pesada', carryable: false, speedMul: 0, storage: 'suelo' },
  P5: { id: 'P5', name: 'Industrial', carryable: false, speedMul: 0, storage: 'suelo' },
};

// ---------------------------------------------------------------- máquinas y eficiencia (GDD 4.0 / GDD 5.0 §4.2)
// E = eficiencia del proceso. Y_i = floor-acumulado(B_i × E × P_i)
export const MACHINES = {
  trituradora_basica: { id: 'trituradora_basica', name: 'Trituradora básica', E: 0.9 }, // PROV-01
};

// Factor de conservación por daño P_i = calidad/100  (PROV-02: forma funcional no numérica en GDD)
export const conservationFactor = quality => quality / 100;

// Modificador comercial M_c (GDD 5.0 §6.3). Sin bonificaciones activas todavía.
export const COMMERCIAL_MOD = 1.0; // PROV-03

// ---------------------------------------------------------------- almacenamiento (GDD 5.0 §8.5)
// Capacidad física por categoría de espacio. Sin peso global abstracto.
export const STORAGE_ZONES = {
  estanteria: { id: 'estanteria', name: 'Estantería', accepts: ['P1'], capacity: 8 },   // PROV-04
  pale:       { id: 'pale',       name: 'Palé',       accepts: ['P2'], capacity: 6 },   // PROV-04
  suelo:      { id: 'suelo',      name: 'Suelo',      accepts: ['P3', 'P4', 'P5'], capacity: 4 }, // PROV-04
};
// Materiales procesados: capacidad de contenedores (unidades por material).
export const MATERIAL_CONTAINER_CAP = 120; // PROV-05

// ---------------------------------------------------------------- registro PROVISORIO
// Valores numéricos NO especificados por los GDD; requieren aprobación.
export const PROVISIONAL = [
  { id: 'PROV-01', campo: 'MACHINES.trituradora_basica.E', valor: 0.9, nota: 'Eficiencia de proceso no numérica en GDD 4.0/5.0' },
  { id: 'PROV-02', campo: 'conservationFactor', valor: 'q/100', nota: 'Forma funcional del factor de conservación P_i' },
  { id: 'PROV-03', campo: 'COMMERCIAL_MOD', valor: 1.0, nota: 'Modificador comercial M_c sin bonificaciones activas' },
  { id: 'PROV-04', campo: 'STORAGE_ZONES.capacity', valor: '8/6/4', nota: 'Capacidades de estantería/palé/suelo no numéricas en GDD 5.0 §8.5' },
  { id: 'PROV-05', campo: 'MATERIAL_CONTAINER_CAP', valor: 120, nota: 'Capacidad de contenedores de materiales por unidad' },
  { id: 'PROV-06', campo: 'WEIGHT_CLASSES.P3+.carryable', valor: false, nota: 'Carro/grúa no implementados todavía' },
  { id: 'PROV-07', campo: 'recetas B_i del microondas', valor: 'ver catalog.js', nota: 'GDD 3.0 lista materiales sin cantidades; se usan unidades provisorias' },
  { id: 'PROV-08', campo: 'vbase carcasa/puerta/panel/plato', valor: '22/14/18/9', nota: 'No están en la tabla GDD 5.0 §6.3; se conservan del catálogo v0.2' },
  { id: 'PROV-09', campo: 'coste de gestión módulo sellado', valor: '4 × $5', nota: 'Unidades MAT-08 del módulo no especificadas; se cobran al entregar' },
  { id: 'PROV-10', campo: 'calidad de pieza procesada heredada', valor: 'q origen', nota: 'Calidad de subensamblajes producidos (motor pequeño)' },
  { id: 'PROV-11', campo: 'persistencia física', valor: 'parcial', nota: 'Piezas en mundo/mano no se restauran al cargar; almacén y dinero sí' },
];
