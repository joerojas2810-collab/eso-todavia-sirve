/* ============================================================================
   ESTADO PERSISTIBLE — capa 3 (Instrucción 02 §9, GDD 2.0: "progreso compartido
   y guardado por la partida del anfitrión").
   Agrega dinero, registro de piezas y almacenes; serializa/deserializa con
   versión. En el prototipo persiste en localStorage; en producción será el
   save del anfitrión. PROV-11: las piezas físicas en mundo/mano no se
   restauran (se pierden al cargar); almacén, dinero y piezas almacenadas sí.
   ============================================================================ */
import * as PIE from '../systems/pieces.js';
import * as WH from '../systems/warehouse.js';

const SAVE_KEY = 'ets_save_v3';
const VERSION = 3;

export function createGameState() {
  const gs = {
    money: 0,
    reg: PIE.createPieceRegistry(),
    cwh: WH.createComponentWarehouse(),
    mwh: WH.createMaterialWarehouse(),
  };
  return gs;
}

export function serialize(gs) {
  // Las piezas en world/carried/scale no persisten (PROV-11): se descartan.
  const pieces = PIE.serializeRegistry(gs.reg).filter(p => p.terminal || p.storage.startsWith('stored:'));
  return {
    version: VERSION,
    money: gs.money,
    pieces,
    warehouses: WH.serializeWarehouses(gs.cwh, gs.mwh),
  };
}

export function deserialize(json) {
  try {
    const data = typeof json === 'string' ? JSON.parse(json) : json;
    if (!data || data.version !== VERSION) return null;
    const gs = createGameState();
    gs.money = data.money || 0;
    gs.reg = PIE.deserializeRegistry(data.pieces || []);
    WH.deserializeInto(gs.cwh, gs.mwh, data.warehouses || {});
    return gs;
  } catch (e) {
    return null;
  }
}

export function saveToStorage(gs, storage = globalThis.localStorage) {
  if (!storage) return false;
  try { storage.setItem(SAVE_KEY, JSON.stringify(serialize(gs))); return true; }
  catch (e) { return false; }
}
export function loadFromStorage(storage = globalThis.localStorage) {
  if (!storage) return null;
  try { return deserialize(storage.getItem(SAVE_KEY)); }
  catch (e) { return null; }
}
export function clearSave(storage = globalThis.localStorage) {
  if (storage) storage.removeItem(SAVE_KEY);
}
