/* ============================================================================
   SISTEMA DE DESMONTAJE — reglas puras (sin three.js, sin DOM)
   Capa 2: máquina de estados, dependencias, trabajo, calidad y valor.
   Reutilizable para cualquier objeto del catálogo: las reglas no conocen
   nombres de componentes, solo leen el blueprint.
   ============================================================================ */

// Estados de un componente (coherentes y en este orden):
//   bloqueado → accesible → liberado → extraido → (procesado | vendido)
export const ST = {
  BLOQUEADO: 'bloqueado',
  ACCESIBLE: 'accesible',
  LIBERADO: 'liberado',
  EXTRAIDO: 'extraido',
  PROCESADO: 'procesado',
  VENDIDO: 'vendido',
};
// Estados que consideran "retirado" al componente a efectos de dependencias
export const UNLOCKS = [ST.EXTRAIDO, ST.PROCESADO, ST.VENDIDO];
// Estados terminales: ya no se puede operar ni recuperar nada de él
export const TERMINAL = [ST.PROCESADO, ST.VENDIDO];

export function createInstance(bp, id) {
  const inst = { id, bp, comps: {}, events: [] };
  for (const c of bp.components) {
    inst.comps[c.id] = {
      id: c.id,
      state: ST.BLOQUEADO,
      quality: 100,
      fasteners: (c.fasteners || []).map((f, i) => ({ i, type: f.type, removed: false, progress: 0 })),
      terminalTag: null,
    };
  }
  refreshAccessibility(inst);
  return inst;
}

// bloqueado → accesible cuando todas sus dependencias fueron retiradas
export function refreshAccessibility(inst) {
  let changed = true;
  while (changed) {
    changed = false;
    for (const c of inst.bp.components) {
      const s = inst.comps[c.id];
      if (s.state !== ST.BLOQUEADO) continue;
      const free = (c.blockedBy || []).every(d => UNLOCKS.includes(inst.comps[d].state));
      if (free) {
        s.state = ST.ACCESIBLE;
        // sin fijaciones ⇒ queda liberado de inmediato (extracción directa)
        if (s.fasteners.length === 0) s.state = ST.LIBERADO;
        inst.events.push({ t: 'accessible', comp: c.id });
        changed = true;
      }
    }
  }
}

export function fastenerStats(inst, compId) {
  const f = inst.comps[compId].fasteners;
  return { done: f.filter(x => x.removed).length, total: f.length };
}

// ¿Puede el jugador trabajar esta fijación con esta herramienta AHORA?
export function canWork(inst, compId, fIdx, toolId, ftTypes) {
  const s = inst.comps[compId];
  const comp = inst.bp.components.find(c => c.id === compId);
  if (s.state !== ST.ACCESIBLE) return { ok: false, why: 'estado', state: s.state };
  const f = s.fasteners[fIdx];
  if (!f || f.removed) return { ok: false, why: 'ya' };
  const ft = ftTypes[f.type];
  if (ft.tool !== toolId) return { ok: false, why: 'herramienta', need: ft.tool, ft };
  return { ok: true, time: ft.time, verb: ft.verb, ft };
}

// Aplica dt segundos de trabajo sobre una fijación cuyo operación dura `time`.
// El progreso queda guardado en la fijación: interrumpir no lo pierde.
// Devuelve 'done' al retirar la fijación, si no 'progress'.
export function work(inst, compId, fIdx, dt, time) {
  const s = inst.comps[compId];
  const f = s.fasteners[fIdx];
  f.progress = Math.min(1, f.progress + dt / Math.max(0.01, time));
  if (f.progress >= 1 && !f.removed) {
    f.removed = true;
    inst.events.push({ t: 'fastener', comp: compId, fIdx });
    if (s.fasteners.every(x => x.removed)) {
      s.state = ST.LIBERADO;
      inst.events.push({ t: 'liberado', comp: compId });
      refreshAccessibility(inst);
    }
    return 'done';
  }
  return 'progress';
}

export function canExtract(inst, compId) {
  return inst.comps[compId].state === ST.LIBERADO;
}

export function extract(inst, compId) {
  const s = inst.comps[compId];
  if (s.state !== ST.LIBERADO) return null;
  s.state = ST.EXTRAIDO;
  inst.events.push({ t: 'extraido', comp: compId });
  refreshAccessibility(inst); // retirar esta pieza puede destapar otras
  return s;
}

// Daño por acciones destructivas (golpes). Nunca por uso correcto de herramienta.
export function damage(inst, compId, amt) {
  const s = inst.comps[compId];
  if (TERMINAL.includes(s.state) || s.state === ST.EXTRAIDO) return 0;
  const before = s.quality;
  s.quality = Math.max(0, s.quality - amt);
  const applied = before - s.quality;
  if (applied > 0) inst.events.push({ t: 'damage', comp: compId, quality: s.quality });
  return applied;
}

// Cierre de ciclo: procesado (trituradora / residuo especial) o vendido (balanza).
// Solo desde EXTRAIDO: no se puede vender ni procesar lo que no se extrajo.
// Única vía de recuperación de valor ⇒ imposible recuperar dos veces.
export function setTerminal(inst, compId, state, tag) {
  const s = inst.comps[compId];
  if (s.state !== ST.EXTRAIDO) return false;
  if (TERMINAL.includes(s.state)) return false;
  s.state = state;
  s.terminalTag = tag || null;
  inst.events.push({ t: state, comp: compId, tag });
  refreshAccessibility(inst);
  return true;
}

export function allSettled(inst) {
  return inst.bp.components.every(c => TERMINAL.includes(inst.comps[c.id].state));
}

export function drainEvents(inst) {
  const e = inst.events;
  inst.events = [];
  return e;
}
