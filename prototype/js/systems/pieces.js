/* ============================================================================
   REGISTRO DE PIEZAS — identidad y propiedad (Instrucción 02 §2, GDD 5.0 §8.3)
   Capa 2 (reglas): StoredPart con identidad completa e invariantes de estado.
   Una pieza no puede estar instalada, almacenada y vendida a la vez: el campo
   `storage` es único y `terminal` la cierra de forma excluyente.
   ============================================================================ */

export const STORAGE_STATES = ['world', 'carried', 'scale', 'stored', 'none'];
export const TERMINAL_STATES = ['vendido', 'procesado', 'entregado'];

let uidSeq = 0;
export function resetUidSeq(n = 0) { uidSeq = n; }

export function createPieceRegistry() {
  return { pieces: new Map() };
}

// Crea una pieza extraída con identidad completa.
// Contrato de origen: null hasta que un contrato la reclame (GDD 5.0 §6.4).
export function registerPiece(reg, { defId, defName, originObjectId, quality, kg, weightClass, owner = 'P1', vbase, sealed, protected: prot, recipe }) {
  uidSeq++;
  const piece = {
    uid: 'p_' + uidSeq,
    defId,                 // identificador de definición (componente o PART_DEF)
    defName,
    originObjectId,        // objeto de origen (instancia)
    contractId: null,      // contrato de origen, si corresponde
    quality: Math.max(0, Math.min(100, Math.round(quality))),
    kg,
    weightClass,
    vbase,
    owner,
    storage: 'world',      // world | carried | scale | stored:<zona> | none
    reservedFor: null,     // reserva: contrato | fabricacion | mantenimiento | construccion
    terminal: null,        // vendido | procesado | entregado
    sealed: !!sealed,         // módulo sellado: sin apertura ni máquinas comunes
    protected: !!prot,   // componente narrativo protegido (campaña)
    recipe: recipe || null,   // receta de recuperación del componente de origen
  };
  reg.pieces.set(piece.uid, piece);
  return piece;
}

export const getPiece = (reg, uid) => reg.pieces.get(uid) || null;

export const isTerminal = p => p.terminal !== null;
export const isStored = p => typeof p.storage === 'string' && p.storage.startsWith('stored:');
export const zoneOf = p => isStored(p) ? p.storage.slice(7) : null;

// Invariante central: sin estado terminal, la pieza vive en un único lugar.
export function setStorage(reg, uid, storage) {
  const p = getPiece(reg, uid);
  if (!p) return { ok: false, reason: 'pieza_inexistente' };
  if (isTerminal(p)) return { ok: false, reason: 'pieza_terminal' };
  if (!STORAGE_STATES.some(s => storage === s || (s === 'stored' && storage.startsWith('stored:'))))
    return { ok: false, reason: 'estado_invalido' };
  p.storage = storage;
  return { ok: true, piece: p };
}

// Cierre excluyente: vendido | procesado | entregado, una sola vez.
export function setTerminal(reg, uid, terminal, tag = null) {
  const p = getPiece(reg, uid);
  if (!p) return { ok: false, reason: 'pieza_inexistente' };
  if (!TERMINAL_STATES.includes(terminal)) return { ok: false, reason: 'terminal_invalido' };
  if (isTerminal(p)) return { ok: false, reason: 'ya_terminal', current: p.terminal };
  p.terminal = terminal;
  p.terminalTag = tag;
  p.storage = 'none';
  p.reservedFor = null;
  return { ok: true, piece: p };
}

// Reservas (GDD 5.0 §8.4): no consumen, pero bloquean otros usos.
export function reserve(reg, uid, purpose) {
  const p = getPiece(reg, uid);
  if (!p) return { ok: false, reason: 'pieza_inexistente' };
  if (isTerminal(p)) return { ok: false, reason: 'pieza_terminal' };
  if (p.reservedFor) return { ok: false, reason: 'ya_reservada', by: p.reservedFor };
  p.reservedFor = purpose;
  return { ok: true, piece: p };
}
export function release(reg, uid, purpose = null) {
  const p = getPiece(reg, uid);
  if (!p) return { ok: false, reason: 'pieza_inexistente' };
  if (purpose && p.reservedFor !== purpose) return { ok: false, reason: 'reserva_ajena' };
  p.reservedFor = null;
  return { ok: true, piece: p };
}

// Daño: la calidad nunca baja de 0 ni supera la inicial (GDD 5.0 §5.2).
export function damagePiece(reg, uid, amt) {
  const p = getPiece(reg, uid);
  if (!p || isTerminal(p)) return { ok: false, reason: 'pieza_inexistente' };
  const before = p.quality;
  p.quality = Math.max(0, p.quality - amt);
  return { ok: true, applied: before - p.quality, quality: p.quality };
}

// ---- serialización (capa de estado persistible) ----
export function serializeRegistry(reg) {
  return [...reg.pieces.values()].map(p => ({ ...p }));
}
export function deserializeRegistry(arr) {
  const reg = createPieceRegistry();
  let max = 0;
  for (const p of arr) {
    reg.pieces.set(p.uid, { ...p });
    const n = parseInt(p.uid.slice(2), 10);
    if (!Number.isNaN(n) && n > max) max = n;
  }
  resetUidSeq(max);
  return reg;
}
