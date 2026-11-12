/* ============================================================================
   VISTA DE DESMONTAJE — representación three.js de un objeto del catálogo
   Capa 4: construye meshes desde el blueprint (data-driven), gobierna
   visibilidad por estado, pulso de fijaciones accesibles, highlight de mira
   y entrega el mesh de un componente al mundo físico al extraerlo.
   No contiene reglas: consulta el estado de la instancia (capa 2).
   ============================================================================ */
import * as THREE from '../vendor/three.module.js';
import { FASTENER_TYPES } from '../data/catalog.js';
import { ST } from '../systems/disassembly.js';

const M = (color, extra = {}) => new THREE.MeshLambertMaterial({ color, flatShading: true, ...extra });
const buildGeo = g => g.shape === 'cyl'
  ? new THREE.CylinderGeometry(g.r, g.r, g.h, 14)
  : new THREE.BoxGeometry(...g.size);

export class DisassemblyView {
  constructor(bp, inst) {
    this.bp = bp;
    this.inst = inst;
    this.group = new THREE.Group();
    this.compMesh = {};
    this.fastMesh = {};
    this.targetMeshes = [];   // meshes raycasteables (componentes + fijaciones + hijos)
    this.detached = new Set();// componentes cuyo mesh ya vive en el mundo físico
    this.aim = null;

    if (bp.chassis) {
      const m = new THREE.Mesh(buildGeo(bp.chassis.geo), M(bp.chassis.geo.color));
      m.position.set(...bp.chassis.offset);
      m.castShadow = m.receiveShadow = true;
      this.group.add(m);
    }

    for (const c of bp.components) {
      const mesh = new THREE.Mesh(buildGeo(c.geo), M(c.geo.color));
      mesh.position.set(...c.geo.offset);
      mesh.castShadow = mesh.receiveShadow = true;
      mesh.userData.t = { kind: 'comp', comp: c.id };
      this.targetMeshes.push(mesh);
      for (const ch of (c.geo.children || [])) {
        const cm = new THREE.Mesh(buildGeo(ch), M(ch.color));
        cm.position.set(...ch.offset);
        cm.castShadow = true;
        cm.userData.t = { kind: 'comp', comp: c.id };
        mesh.add(cm);
        this.targetMeshes.push(cm);
      }
      this.compMesh[c.id] = mesh;
      this.fastMesh[c.id] = [];
      (c.fasteners || []).forEach((f, i) => {
        let fm;
        if (f.type === 'tornillo') fm = new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.022, 0.016, 8), M(0xbfc7d1, { emissive: 0xe8871f, emissiveIntensity: 0 }));
        else if (f.type === 'bisagra') fm = new THREE.Mesh(new THREE.BoxGeometry(0.045, 0.11, 0.05), M(0x8a8f98, { emissive: 0xe8871f, emissiveIntensity: 0 }));
        else fm = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.04, 0.03), M(0xe8871f, { emissive: 0xe8871f, emissiveIntensity: 0 }));
        fm.position.set(...f.offset);
        fm.userData.t = { kind: 'fast', comp: c.id, fIdx: i };
        mesh.add(fm);
        this.fastMesh[c.id].push(fm);
        this.targetMeshes.push(fm);
      });
      this.group.add(mesh);
    }
  }

  attach(scene, pos, rotY = 0) {
    this.group.position.copy(pos);
    this.group.rotation.y = rotY;
    scene.add(this.group);
  }
  detach(scene) { scene.remove(this.group); }

  compWorldPos(compId, out) { return this.compMesh[compId].getWorldPosition(out); }
  fastWorldPos(compId, fIdx, out) { return this.fastMesh[compId][fIdx].getWorldPosition(out); }

  // El mesh del componente pasa al mundo físico (lo agarra el sistema de props).
  detachCompMesh(compId, scene) {
    const m = this.compMesh[compId];
    scene.attach(m); // conserva transform de mundo al re-parentar
    this.detached.add(compId);
    return m;
  }

  setAim(target) { this.aim = target; }

  // Meshes raycasteables este frame (respeta estados y visibilidad)
  activeTargets() {
    const out = [];
    for (const m of this.targetMeshes) {
      const t = m.userData.t;
      if (!t) continue;
      if (t.kind === 'comp') {
        if (this.detached.has(t.comp)) continue;
        const s = this.inst.comps[t.comp];
        if (s.state === ST.BLOQUEADO || s.state === ST.EXTRAIDO ||
            s.state === ST.PROCESADO || s.state === ST.VENDIDO) continue;
        out.push(m);
      } else if (t.kind === 'fast') {
        if (m.visible) out.push(m);
      }
    }
    return out;
  }

  update(t) {
    const inst = this.inst;
    for (const c of this.bp.components) {
      const s = inst.comps[c.id];
      const mesh = this.compMesh[c.id];
      if (this.detached.has(c.id)) continue; // ya lo gobierna el mundo físico
      mesh.visible = s.state !== ST.BLOQUEADO;
      const aimedComp = this.aim && this.aim.kind === 'comp' && this.aim.comp === c.id;
      mesh.material.emissive.setHex(aimedComp ? 0x3a2a12 : 0x000000);
      (c.fasteners || []).forEach((f, i) => {
        const fm = this.fastMesh[c.id][i];
        const shown = mesh.visible && s.state === ST.ACCESIBLE && !s.fasteners[i].removed;
        fm.visible = shown;
        if (shown) {
          const aimed = this.aim && this.aim.kind === 'fast' && this.aim.comp === c.id && this.aim.fIdx === i;
          fm.material.emissiveIntensity = aimed ? 1.5 : 0.45 + 0.35 * Math.sin(t * 5 + i * 1.7);
        }
      });
    }
  }
}
