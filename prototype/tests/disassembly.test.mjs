import { OBJECT_CATALOG, FASTENER_TYPES, compById } from '../js/data/catalog.js';
import * as DIS from '../js/systems/disassembly.js';
const BP = OBJECT_CATALOG[0];
let pass = 0, fail = 0;
const ok = (cond, msg) => { if (cond) { pass++; } else { fail++; console.log('  FAIL: ' + msg); } };
const workAll = (inst, comp, time) => { const n = inst.comps[comp].fasteners.length; for (let i = 0; i < n; i++) DIS.work(inst, comp, i, time, time); };

const inst = DIS.createInstance(BP, 'test_1');
ok(inst.comps.carcasa.state === 'accesible' && inst.comps.puerta.state === 'accesible' && inst.comps.panel.state === 'accesible', 'accesibles iniciales');
ok(inst.comps.plato.state === 'bloqueado' && inst.comps.motor.state === 'bloqueado' && inst.comps.modulo.state === 'bloqueado', 'bloqueados por dependencia');

let cw = DIS.canWork(inst, 'carcasa', 0, 'llave', FASTENER_TYPES);
ok(!cw.ok && cw.why === 'herramienta', 'herramienta incorrecta rechazada');
cw = DIS.canWork(inst, 'carcasa', 0, 'destornillador', FASTENER_TYPES);
ok(cw.ok && cw.time === 3.0, 'destornillador 3s por tornillo');

DIS.work(inst, 'carcasa', 0, 1.5, 3.0);
ok(Math.abs(inst.comps.carcasa.fasteners[0].progress - 0.5) < 1e-9, 'progreso interrumpido se conserva');
DIS.work(inst, 'carcasa', 0, 1.5, 3.0);
ok(inst.comps.carcasa.fasteners[0].removed, 'tornillo retirado tras 3s acumulados');
ok(!DIS.canExtract(inst, 'carcasa'), 'NO se extrae pieza aún sujeta');
workAll(inst, 'carcasa', 3.0);
ok(inst.comps.carcasa.state === 'liberado', 'carcasa liberada (4 tornillos)');
ok(inst.comps.motor.state === 'bloqueado', 'motor sigue tapado hasta EXTRAER la carcasa');
DIS.extract(inst, 'carcasa');
ok(inst.comps.motor.state === 'accesible' && inst.comps.modulo.state === 'accesible', 'motor y módulo accesibles al retirar carcasa');

workAll(inst, 'puerta', 2.0);
DIS.extract(inst, 'puerta');
ok(inst.comps.plato.state === 'liberado', 'plato: extracción directa al retirar puerta');
DIS.extract(inst, 'plato');

const q0 = inst.comps.motor.quality;
workAll(inst, 'motor', 3.0);
ok(inst.comps.motor.quality === q0, 'trabajo correcto no daña');
ok(DIS.damage(inst, 'motor', 12) === 12 && inst.comps.motor.quality === 88, 'golpe baja calidad a 88');
ok(DIS.damage(inst, 'carcasa', 12) === 0, 'no se daña lo ya extraído');
ok(DIS.effPartValue(compById(BP, 'motor'), 100) === 26, 'valor intacto');
ok(DIS.effPartValue(compById(BP, 'motor'), 88) === Math.round(26 * (0.4 + 0.6 * 0.88)), 'valor según calidad');

ok(!DIS.setTerminal(inst, 'panel', DIS.ST.PROCESADO, 'x'), 'NO se procesa sin extraer antes');
workAll(inst, 'modulo', 2.5);
DIS.extract(inst, 'modulo');
ok(DIS.setTerminal(inst, 'modulo', DIS.ST.PROCESADO, 'residuo especial'), 'módulo sellado → residuo especial');
ok(!DIS.setTerminal(inst, 'modulo', DIS.ST.VENDIDO), 'doble recuperación imposible (módulo)');

DIS.extract(inst, 'motor');
ok(DIS.setTerminal(inst, 'motor', DIS.ST.VENDIDO, 'balanza'), 'motor vendido');
ok(!DIS.setTerminal(inst, 'motor', DIS.ST.PROCESADO), 'doble recuperación imposible (motor)');
workAll(inst, 'panel', 3.0);
DIS.extract(inst, 'panel');
DIS.setTerminal(inst, 'panel', DIS.ST.PROCESADO, 'triturado');
for (const c of ['carcasa', 'puerta', 'plato']) DIS.setTerminal(inst, c, DIS.ST.VENDIDO, 'balanza');
ok(DIS.allSettled(inst), 'objeto cerrado cuando todo fue vendido/procesado');
console.log(`\n${pass} passed, ${fail} failed`);
process.exit(fail ? 1 : 0);
