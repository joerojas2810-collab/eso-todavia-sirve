/* ============================================================================
   ALMACENES — componentes y materiales (Instrucción 02 §5, GDD 5.0 §8.2/§8.5)
   Capa 2 (reglas): capacidades físicas por tipo de espacio y cantidades
   compartidas de materiales con acumulador fraccionario (GDD 5.0 §4.2:
   los residuos fraccionarios se conservan hasta cerrar el procesamiento).
   ============================================================================ */
import { STORAGE_ZONES, MATERIAL_CONTAINER_CAP, MATERIALS, WEIGHT_CLASSES } from '../data/economyData.js';

// ---------------------------------------------------------------- almacén de componentes
export function createComponentWarehouse() {
  const zones = {};
  for (const z of Object.values(STORAGE_ZONES)) zones[z.id] = { def: z, items: [] }; // uids
  return { zones };
}

export function zoneFreeSpace(wh, zoneId) {
  const z = wh.zones[zoneId];
  return z ? z.def.capacity - z.items.length : 0;
}

// ¿Puede esta pieza (clase de peso) entrar en esta zona con espacio disponible?
export function canStorePiece(wh, zoneId, piece) {
  const z = wh.zones[zoneId];
  if (!z) return { ok: false, reason: 'zona_inexistente' };
  const wc = WEIGHT_CLASSES[piece.weightClass];
  if (!wc) return { ok: false, reason: 'clase_invalida' };
  if (!z.def.accepts.includes(piece.weightClass))
    return { ok: false, reason: 'categoria_incompatible', zona: z.def.name, clase: piece.weightClass };
  if (z.items.length >= z.def.capacity)
    return { ok: false, reason: 'zona_llena', zona: z.def.name };
  return { ok: true };
}

export function storePieceUid(wh, zoneId, uid) {
  const z = wh.zones[zoneId];
  z.items.push(uid);
}
export function removePieceUid(wh, zoneId, uid) {
  const z = wh.zones[zoneId];
  const i = z.items.indexOf(uid);
  if (i >= 0) z.items.splice(i, 1);
}
export const storedUids = (wh, zoneId) => [...wh.zones[zoneId].items];
export const allStoredUids = wh => Object.values(wh.zones).flatMap(z => z.items);

// ---------------------------------------------------------------- almacén de materiales
// acc: acumulador float por material. units = floor(acc) = unidades disponibles.
export function createMaterialWarehouse() {
  const acc = {};
  for (const id of Object.keys(MATERIALS)) acc[id] = 0;
  return { acc };
}
export const materialUnits = (mw, id) => Math.floor(mw.acc[id] + 1e-9);
export const materialFraction = (mw, id) => mw.acc[id] - Math.floor(mw.acc[id] + 1e-9);

// Agrega rendimiento fraccionario; las unidades aparecen al pisar el entero.
export function addMaterialYield(mw, id, amount) {
  if (!(id in mw.acc)) return { ok: false, reason: 'material_inexistente' };
  if (amount < 0) return { ok: false, reason: 'cantidad_negativa' };
  const cap = MATERIAL_CONTAINER_CAP;
  const room = cap - mw.acc[id];
  const added = Math.max(0, Math.min(amount, room));
  mw.acc[id] += added;
  return { ok: true, added, units: materialUnits(mw, id), overflow: amount - added };
}

// Consumo atómico: solo si hay unidades enteras suficientes.
export function consumeMaterialUnits(mw, id, units) {
  if (!(id in mw.acc)) return { ok: false, reason: 'material_inexistente' };
  if (!Number.isInteger(units) || units <= 0) return { ok: false, reason: 'cantidad_invalida' };
  if (materialUnits(mw, id) < units) return { ok: false, reason: 'insuficiente', disponible: materialUnits(mw, id) };
  mw.acc[id] -= units;
  return { ok: true, restante: materialUnits(mw, id) };
}

// ---- serialización ----
export function serializeWarehouses(cwh, mwh) {
  return {
    zones: Object.fromEntries(Object.entries(cwh.zones).map(([k, z]) => [k, [...z.items]])),
    acc: { ...mwh.acc },
  };
}
export function deserializeInto(cwh, mwh, snap) {
  for (const [k, items] of Object.entries(snap.zones || {})) if (cwh.zones[k]) cwh.zones[k].items = [...items];
  for (const [k, v] of Object.entries(snap.acc || {})) if (k in mwh.acc) mwh.acc[k] = v;
}
