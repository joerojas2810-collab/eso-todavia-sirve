/* ============================================================================
   TESTS — economía, identidad, almacenes y operaciones atómicas (Instrucción 02)
   Ejecutar: node tests/economy.test.mjs
   Verifican: fórmula de valor, rendimiento con fracciones, no duplicación de
   materiales, ventas inválidas (reservada/ajena/terminal/protegida),
   capacidades por zona, reservas, consumo inexistente, exclusividad de estado
   y round-trip de persistencia.
   ============================================================================ */
import * as PIE from '../js/systems/pieces.js';
import * as WH from '../js/systems/warehouse.js';
import * as ECO from '../js/systems/economy.js';
import { GS } from './helpers.mjs';

let pass = 0, fail = 0;
const ok = (cond, msg) => { if (cond) pass++; else { fail++; console.log('  FAIL: ' + msg); } };

function makeCtx(money = 0) {
  const gs = GS.createGameState();
  gs.money = money;
  const ctx = {
    reg: gs.reg, cwh: gs.cwh, mwh: gs.mwh, carry: null,
    getMoney: () => gs.money,
    addMoney: n => { gs.money += n; },
    onChanged: () => {},
    _gs: gs,
  };
  return ctx;
}
const mkPiece = (ctx, over = {}) => ECO.extractPiece(ctx, {
  defId: 'test', defName: 'Pieza test', originObjectId: 'obj_1',
  quality: 100, kg: 1, weightClass: 'P1', vbase: 100, ...over,
}).piece;

// ---- fórmula de valor (GDD 5.0 §6.3): V = Vb × (Q/100) × M_c
{
  const ctx = makeCtx();
  const p = mkPiece(ctx, { vbase: 90, quality: 88 });
  ok(Math.abs(ECO.pieceValue(ctx, p) - 79.2) < 1e-9, 'V = 90 × 0.88 × 1 = 79.2');
}

// ---- identidad y exclusividad de estados
{
  const ctx = makeCtx();
  const p = mkPiece(ctx);
  ok(p.storage === 'world' && p.terminal === null && p.reservedFor === null, 'identidad inicial');
  ok(ECO.pickupPiece(ctx, p.uid).ok, 'pickup world→carried');
  ok(p.storage === 'carried', 'carried');
  const p2 = mkPiece(ctx);
  ok(ECO.pickupPiece(ctx, p2.uid).reason === 'manos_ocupadas', 'una sola pieza en las manos (GDD 8.1)');
  ok(ECO.storePiece(ctx, p.uid, 'estanteria').ok, 'store carried→estantería (P1)');
  ok(p.storage === 'stored:estanteria', 'stored');
  ok(ECO.sellPiece(ctx, p.uid).ok, 'vender desde almacén');
  ok(p.terminal === 'vendido' && p.storage === 'none', 'terminal excluyente: ni almacenada ni vendida a la vez');
  ok(!ECO.sellPiece(ctx, p.uid).ok, 'no se vende dos veces');
  ok(!ECO.storePiece(ctx, p.uid, 'estanteria').ok, 'terminal no se almacena');
}

// ---- capacidades y categorías por zona (GDD 5.0 §8.5)
{
  const ctx = makeCtx();
  const heavy = mkPiece(ctx, { weightClass: 'P2' });
  ok(ECO.pickupPiece(ctx, heavy.uid).ok, 'P2 se puede llevar (penalización en main)');
  const r = ECO.storePiece(ctx, heavy.uid, 'estanteria');
  ok(r.reason === 'categoria_incompatible', 'P2 no entra en estantería');
  ok(ECO.storePiece(ctx, heavy.uid, 'pale').ok, 'P2 → palé');
  for (let i = 0; i < 5; i++) {           // palé cap 6: 1 usado + 5 = lleno
    const p = mkPiece(ctx, { weightClass: 'P2' });
    ECO.pickupPiece(ctx, p.uid); ECO.storePiece(ctx, p.uid, 'pale');
  }
  const p7 = mkPiece(ctx, { weightClass: 'P2' });
  ECO.pickupPiece(ctx, p7.uid);
  ok(ECO.storePiece(ctx, p7.uid, 'pale').reason === 'zona_llena', 'capacidad de palé respetada');
  ok(p7.storage === 'carried', 'operación fallida no muta estado');
}

// ---- rendimiento con fracciones (GDD 5.0 §4.2): Y = B × E × P acumulado
{
  const ctx = makeCtx();
  const recipe = [{ mat: 'MAT-01', base: 4 }];
  const p1 = mkPiece(ctx); ECO.pickupPiece(ctx, p1.uid);
  const r1 = ECO.processPiece(ctx, p1.uid, 'trituradora_basica', recipe);
  ok(r1.ok, 'procesar pieza P1');
  ok(WH.materialUnits(ctx.mwh, 'MAT-01') === 3, 'floor(4 × 0.9 × 1) = 3 unidades, fracción 0.6 guardada');
  ok(Math.abs(WH.materialFraction(ctx.mwh, 'MAT-01') - 0.6) < 1e-9, 'residuo fraccionario conservado');
  ok(!ECO.processPiece(ctx, p1.uid, 'trituradora_basica', recipe).ok, 'no se procesa dos veces (pieza terminal)');
  const before = WH.materialUnits(ctx.mwh, 'MAT-01');
  const p2 = mkPiece(ctx); ECO.pickupPiece(ctx, p2.uid);
  ECO.processPiece(ctx, p2.uid, 'trituradora_basica', recipe);
  ok(WH.materialUnits(ctx.mwh, 'MAT-01') === 7, 'lote 2: 3.6+3.6=7.2 → 7 unidades (fracciones no se pierden)');
  ok(before === 3, 'antes del lote 2 había 3');
}

// ---- subensambles: motor de microondas → motor pequeño (GDD 3.0)
{
  const ctx = makeCtx();
  const p = mkPiece(ctx, { quality: 88, recipe: [{ part: 'motor_pequenio', base: 1 }] });
  ECO.pickupPiece(ctx, p.uid);
  const r = ECO.processPiece(ctx, p.uid, 'trituradora_basica');
  ok(r.ok && r.newParts.length === 1, 'descomposición genera 1 motor pequeño');
  const np = r.newParts[0];
  ok(np.defId === 'motor_pequenio' && np.vbase === 90, 'Vb $90 del GDD 5.0 §6.3');
  ok(np.quality === 88, 'calidad heredada (PROV-10)');
  ok(np.storage === 'stored:pale', 'subensamble P2 va al palé');
  ok(ECO.sellPiece(ctx, np.uid).value === 79, 'venta del subensamble: round(90×0.88) = 79');
}

// ---- sellado: sin máquinas comunes, entrega con coste de gestión
{
  const ctx = makeCtx(100);
  const s = mkPiece(ctx, { sealed: true, recipe: [{ mat: 'MAT-08', base: 4 }] });
  ECO.pickupPiece(ctx, s.uid);
  ok(ECO.processPiece(ctx, s.uid, 'trituradora_basica').reason === 'maquina_incompatible_sellado', 'sellado no se tritura');
  ok(ECO.sellPiece(ctx, s.uid).reason === 'pieza_protegida_no_vendible', 'sellado no se vende');
  const d = ECO.disposeSpecial(ctx, s.uid);
  ok(d.ok && d.cost === 20, 'entrega residuo especial: 4 × $5 = $20');
  ok(ctx.getMoney() === 80, 'dinero 100 − 20');
  ok(s.terminal === 'entregado', 'estado terminal entregado');
  ok(!ECO.disposeSpecial(ctx, s.uid).ok, 'no se entrega dos veces');
  const poor = makeCtx(5);
  const s2 = mkPiece(poor, { sealed: true, recipe: [{ mat: 'MAT-08', base: 4 }] });
  ECO.pickupPiece(poor, s2.uid);
  ok(ECO.disposeSpecial(poor, s2.uid).reason === 'fondos_insuficientes', 'sin fondos no hay entrega');
}

// ---- reservas (GDD 5.0 §8.4)
{
  const ctx = makeCtx();
  const p = mkPiece(ctx);
  ECO.pickupPiece(ctx, p.uid); ECO.storePiece(ctx, p.uid, 'estanteria');
  ok(ECO.reservePiece(ctx, p.uid, 'contrato').ok, 'reservar');
  ok(ECO.sellPiece(ctx, p.uid).reason === 'pieza_reservada', 'vender reservada → bloqueado');
  ok(ECO.processPiece(ctx, p.uid, 'trituradora_basica', [{ mat: 'MAT-01', base: 1 }]).reason === 'pieza_reservada', 'procesar reservada → bloqueado');
  ok(!ECO.reservePiece(ctx, p.uid, 'mantenimiento').ok, 'doble reserva no');
  ok(ECO.releasePiece(ctx, p.uid, 'contrato').ok, 'liberar');
  ok(ECO.sellPiece(ctx, p.uid).ok, 'tras liberar se puede vender');
}

// ---- propiedad ajena
{
  const ctx = makeCtx();
  const p = mkPiece(ctx, { owner: 'P2' });
  ok(ECO.pickupPiece(ctx, p.uid).reason === 'propiedad_ajena', 'pieza de otro jugador');
}

// ---- venta y consumo de materiales
{
  const ctx = makeCtx();
  WH.addMaterialYield(ctx.mwh, 'MAT-03', 5);
  ok(ECO.sellMaterial(ctx, 'MAT-03', 5).delta === 90, '5 cobre × $18 = $90');
  ok(ECO.sellMaterial(ctx, 'MAT-03', 1).reason === 'insuficiente', 'no se vende lo inexistente');
  ok(ECO.consumeMaterials(ctx, { 'MAT-01': 1 }).reason === 'insuficiente', 'consumo inexistente falla');
  WH.addMaterialYield(ctx.mwh, 'MAT-01', 4);
  ok(ECO.consumeMaterials(ctx, { 'MAT-01': 3 }).ok, 'consumo válido');
  ok(WH.materialUnits(ctx.mwh, 'MAT-01') === 1, 'queda 1');
  ok(ECO.consumeMaterials(ctx, { 'MAT-01': 2 }).reason === 'insuficiente', 'lote atómico: todo o nada');
  ok(WH.materialUnits(ctx.mwh, 'MAT-01') === 1, 'el lote fallido no consumió');
}

// ---- persistencia (capa de estado)
{
  const ctx = makeCtx(500);
  const p = mkPiece(ctx, { quality: 70 });
  ECO.pickupPiece(ctx, p.uid); ECO.storePiece(ctx, p.uid, 'estanteria');
  WH.addMaterialYield(ctx.mwh, 'MAT-06', 3.4);
  const lost = mkPiece(ctx); // en world: no persiste (PROV-11)
  const snap = GS.serialize(ctx._gs);
  const gs2 = GS.deserialize(snap);
  ok(gs2.money === 500, 'dinero persistido');
  ok(PIE.getPiece(gs2.reg, p.uid).storage === 'stored:estanteria', 'pieza almacenada persistida');
  ok(PIE.getPiece(gs2.reg, lost.uid) === null, 'pieza en mundo no persiste (PROV-11)');
  ok(Math.abs(gs2.mwh.acc['MAT-06'] - 3.4) < 1e-9, 'fracciones persistidas');
  ok(WH.storedUids(gs2.cwh, 'estanteria').includes(p.uid), 'zona persistida');
}

console.log(`\n${pass} passed, ${fail} failed`);
process.exit(fail ? 1 : 0);
