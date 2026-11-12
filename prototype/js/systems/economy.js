/* ============================================================================
   ECONOMÍA — operaciones atómicas (Instrucción 02 §4/§6/§7, GDD 5.0 §4/§6/§8)
   Capa 2 (reglas). Toda mutación de piezas/materiales/dinero pasa por estas
   funciones: verifican disponibilidad, propiedad, capacidad y estado ANTES de
   tocar el estado (atomicidad). En multijugador, el anfitrión será quien las
   autorice: la UI nunca muta directamente (ver docs/sistemas_recuperacion_economia.md).

   Contratos públicos (resultado siempre {ok:true,…} | {ok:false, reason,…}):
     pieceValue(ctx, piece)            → float  V = Vb × (Q/100) × M_c
     extractPiece(ctx, comp, opts)     → pieza nueva en 'world'
     pickupPiece(ctx, uid)             → 'world' → 'carried'  (1 pieza en manos, P1/P2)
     dropPiece(ctx, uid)               → 'carried' → 'world'
     placeOnScale(ctx, uid)            → 'carried' → 'scale'
     storePiece(ctx, uid, zoneId)      → 'carried' → 'stored:<zona>'
     retrievePiece(ctx, uid, zoneId)   → 'stored:<zona>' → 'carried'
     processPiece(ctx, uid, machineId) → consume pieza, genera materiales/subensambles
     sellPiece(ctx, uid)               → vende intacta (carry/scale/almacén)
     sellMaterial(ctx, matId, units)   → vende unidades del almacén
     disposeSpecial(ctx, uid)          → entrega residuo especial con coste de gestión
     reserve/release/consumeMaterials  → reservas y consumo para recetas futuras
   ============================================================================ */
import * as PIE from './pieces.js';
import * as WH from './warehouse.js';
import { MATERIALS, PART_DEFS, MACHINES, WEIGHT_CLASSES, conservationFactor, COMMERCIAL_MOD, MATERIAL_CONTAINER_CAP } from '../data/economyData.js';

// ctx = { reg, cwh, mwh, getMoney(), addMoney(n), onChanged() }

// ---------------------------------------------------------------- valor (GDD 5.0 §6.3)
export function pieceValue(ctx, piece) {
  return piece.vbase * (piece.quality / 100) * COMMERCIAL_MOD;
}

const fail = reason => ({ ok: false, reason });
const touch = ctx => ctx.onChanged && ctx.onChanged();

// Verificaciones comunes para operar una pieza por su dueño.
function checkOperable(ctx, uid, { allowReserved = false } = {}) {
  const p = PIE.getPiece(ctx.reg, uid);
  if (!p) return fail('pieza_inexistente');
  if (PIE.isTerminal(p)) return fail('pieza_terminal', p.terminal);
  if (p.owner !== 'P1') return fail('propiedad_ajena');           // futuro: otros jugadores/contratos
  if (!allowReserved && p.reservedFor) return fail('pieza_reservada', p.reservedFor);
  return { ok: true, piece: p };
}

// ---------------------------------------------------------------- extracción
// opts: { defId, defName, originObjectId, quality, kg, weightClass, vbase }
export function extractPiece(ctx, opts) {
  const wc = WEIGHT_CLASSES[opts.weightClass];
  if (!wc) return fail('clase_invalida');
  const piece = PIE.registerPiece(ctx.reg, opts);
  touch(ctx);
  return { ok: true, piece };
}

// ---------------------------------------------------------------- transporte personal
// GDD 5.0 §8.1: una pieza pequeña en las manos. P2 con movimiento reducido.
export function pickupPiece(ctx, uid) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  const p = c.piece;
  if (p.storage !== 'world') return fail('no_disponible_en_suelo', p.storage);
  const wc = WEIGHT_CLASSES[p.weightClass];
  if (!wc.carryable) return fail('requiere_carro_o_grua', p.weightClass);
  if (ctx.carry) return fail('manos_ocupadas'); // GDD 5.0 §8.1: una pieza en las manos
  PIE.setStorage(ctx.reg, uid, 'carried');
  ctx.carry = uid;
  touch(ctx);
  return { ok: true, piece: p, speedMul: wc.speedMul };
}
export function dropPiece(ctx, uid) {
  const c = checkOperable(ctx, uid, { allowReserved: true });
  if (!c.ok) return c;
  if (c.piece.storage !== 'carried') return fail('no_transportada');
  PIE.setStorage(ctx.reg, uid, 'world');
  if (ctx.carry === uid) ctx.carry = null;
  touch(ctx);
  return { ok: true, piece: c.piece };
}
export function placeOnScale(ctx, uid) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  if (c.piece.storage !== 'carried') return fail('no_transportada');
  PIE.setStorage(ctx.reg, uid, 'scale');
  if (ctx.carry === uid) ctx.carry = null;
  touch(ctx);
  return { ok: true, piece: c.piece };
}

// ---------------------------------------------------------------- almacenamiento
export function storePiece(ctx, uid, zoneId) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  if (c.piece.storage !== 'carried') return fail('no_transportada');
  const cs = WH.canStorePiece(ctx.cwh, zoneId, c.piece);
  if (!cs.ok) return cs;
  WH.storePieceUid(ctx.cwh, zoneId, uid);
  PIE.setStorage(ctx.reg, uid, 'stored:' + zoneId);
  if (ctx.carry === uid) ctx.carry = null;
  touch(ctx);
  return { ok: true, piece: c.piece, zone: zoneId };
}
export function retrievePiece(ctx, uid, zoneId) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  if (PIE.zoneOf(c.piece) !== zoneId) return fail('no_esta_en_zona', c.piece.storage);
  const wc = WEIGHT_CLASSES[c.piece.weightClass];
  if (!wc.carryable) return fail('requiere_carro_o_grua', c.piece.weightClass);
  if (ctx.carry) return fail('manos_ocupadas');
  WH.removePieceUid(ctx.cwh, zoneId, uid);
  PIE.setStorage(ctx.reg, uid, 'carried');
  ctx.carry = uid;
  touch(ctx);
  return { ok: true, piece: c.piece };
}

// ---------------------------------------------------------------- procesamiento (GDD 5.0 §4.2/§4.3)
// Y_i = B_i × E × P_i acumulado en fracciones; unidades = floor(acumulado).
// El componente se consume en la misma transacción; nunca duplica materiales.
export function computeYields(ctx, piece, recipe, machineId) {
  const machine = MACHINES[machineId];
  if (!machine) return null;
  const P = conservationFactor(piece.quality);
  const out = [];
  for (const r of recipe) {
    if (r.mat) out.push({ mat: r.mat, amount: r.base * machine.E * P });
    if (r.part) out.push({ part: r.part, amount: r.base }); // subensambles: unidades enteras
  }
  return out;
}

export function processPiece(ctx, uid, machineId, compRecipe) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  const p = c.piece;
  if (p.sealed || p.protected) return fail('maquina_incompatible_sellado');
  if (p.storage !== 'carried' && !PIE.isStored(p)) return fail('no_disponible_para_procesar', p.storage);
  const recipe = compRecipe || p.recipe;
  if (!recipe || !recipe.length) return fail('sin_receta');
  const yields = computeYields(ctx, p, recipe, machineId);
  if (!yields) return fail('maquina_inexistente');
  // pre-checks atómicos: capacidad de contenedores y de almacén de subensambles
  for (const y of yields) {
    if (y.mat === 'MAT-08') return fail('residuo_especial_no_procesable'); // solo entrega en contenedor
    if (y.mat) {
      const room = MATERIAL_CONTAINER_CAP - ctx.mwh.acc[y.mat];
      if (y.amount > room) return fail('contenedor_lleno', y.mat);
    }
    if (y.part) {
      const def = PART_DEFS[y.part];
      if (!def) return fail('pieza_def_inexistente', y.part);
      for (let i = 0; i < y.amount; i++) {
        const cs = WH.canStorePiece(ctx.cwh, WEIGHT_CLASSES[def.weightClass].storage, { weightClass: def.weightClass });
        if (!cs.ok) return cs;
      }
    }
  }
  // commit único: materiales + subensambles + consumo del origen
  const gained = [];
  const newParts = [];
  for (const y of yields) {
    if (y.mat) {
      WH.addMaterialYield(ctx.mwh, y.mat, y.amount);
      gained.push({ mat: y.mat, amount: y.amount });
    } else if (y.part) {
      const def = PART_DEFS[y.part];
      for (let i = 0; i < y.amount; i++) {
        const np = PIE.registerPiece(ctx.reg, {
          defId: y.part, defName: def.name, originObjectId: p.originObjectId,
          quality: p.quality, kg: def.kg, weightClass: def.weightClass, vbase: def.vbase, // PROV-10
        });
        const zone = WEIGHT_CLASSES[def.weightClass].storage;
        WH.storePieceUid(ctx.cwh, zone, np.uid);
        PIE.setStorage(ctx.reg, np.uid, 'stored:' + zone);
        newParts.push(np);
      }
    }
  }
  if (PIE.isStored(p)) WH.removePieceUid(ctx.cwh, PIE.zoneOf(p), uid);
  if (ctx.carry === uid) ctx.carry = null;
  PIE.setTerminal(ctx.reg, uid, 'procesado', machineId);
  touch(ctx);
  return { ok: true, gained, newParts };
}

// ---------------------------------------------------------------- venta (GDD 5.0 §6.3)
export function sellPiece(ctx, uid) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  const p = c.piece;
  if (p.sealed || p.protected) return fail('pieza_protegida_no_vendible');
  if (!['carried', 'scale'].includes(p.storage) && !PIE.isStored(p)) return fail('no_vendible_desde', p.storage);
  const value = Math.round(pieceValue(ctx, p));
  if (PIE.isStored(p)) WH.removePieceUid(ctx.cwh, PIE.zoneOf(p), uid);
  if (ctx.carry === uid) ctx.carry = null;
  PIE.setTerminal(ctx.reg, uid, 'vendido', 'balanza');
  ctx.addMoney(value);
  touch(ctx);
  return { ok: true, value, piece: p };
}

export function sellMaterial(ctx, matId, units) {
  const m = MATERIALS[matId];
  if (!m) return fail('material_inexistente');
  const r = WH.consumeMaterialUnits(ctx.mwh, matId, units);
  if (!r.ok) return r;
  const delta = units * m.price; // MAT-08: precio negativo = coste
  ctx.addMoney(delta);
  touch(ctx);
  return { ok: true, delta, restante: r.restante };
}

// Entrega de residuos especiales (módulos sellados): coste de gestión por unidad.
// GDD 5.0 §6.2: −$5/unidad; "en determinados contratos el cliente cubre ese coste".
export function disposeSpecial(ctx, uid) {
  const c = checkOperable(ctx, uid);
  if (!c.ok) return c;
  const p = c.piece;
  if (!p.sealed) return fail('no_es_residuo_especial');
  if (!['carried', 'scale'].includes(p.storage) && !PIE.isStored(p)) return fail('no_transportado');
  const entry = (p.recipe || []).find(r => r.mat === 'MAT-08');
  const units = entry ? entry.base : 0;
  const cost = units * Math.abs(MATERIALS['MAT-08'].price);
  if (ctx.getMoney() < cost) return fail('fondos_insuficientes', cost);
  if (PIE.isStored(p)) WH.removePieceUid(ctx.cwh, PIE.zoneOf(p), uid);
  if (ctx.carry === uid) ctx.carry = null;
  PIE.setTerminal(ctx.reg, uid, 'entregado', 'residuo_especial');
  ctx.addMoney(-cost);
  touch(ctx);
  return { ok: true, cost, units };
}

// ---------------------------------------------------------------- reservas y consumo (GDD 5.0 §8.4)
export function reservePiece(ctx, uid, purpose) {
  const p = PIE.getPiece(ctx.reg, uid);
  if (!p) return fail('pieza_inexistente');
  if (p.owner !== 'P1') return fail('propiedad_ajena');
  const r = PIE.reserve(ctx.reg, uid, purpose);
  if (r.ok) touch(ctx);
  return r;
}
export function releasePiece(ctx, uid, purpose) {
  const r = PIE.release(ctx.reg, uid, purpose);
  if (r.ok) touch(ctx);
  return r;
}
// Consumo para recetas futuras (fabricación/mantenimiento): atómico por lote.
export function consumeMaterials(ctx, needs) {
  for (const [matId, units] of Object.entries(needs)) {
    if (WH.materialUnits(ctx.mwh, matId) < units) return fail('insuficiente', matId);
  }
  for (const [matId, units] of Object.entries(needs)) WH.consumeMaterialUnits(ctx.mwh, matId, units);
  touch(ctx);
  return { ok: true };
}

// Daño sobre pieza física (golpes): nunca por trabajo correcto.
export function damagePiece(ctx, uid, amt) {
  const r = PIE.damagePiece(ctx.reg, uid, amt);
  if (r.ok) touch(ctx);
  return r;
}
