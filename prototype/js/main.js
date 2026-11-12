/* ============================================================================
   ¡ESO TODAVÍA SIRVE! — prototipo v0.1 (graybox de diseño)
   Loop: golpear → romper → recuperar → pesar → vender → mejorar
   Look objetivo: low-poly plano estilo Moon Sweepers (paleta corta, 1 luz,
   emisivos, UI redonda con números gigantes).
   Esto NO es el juego final: valida feel + estilo. El final va en Unity 6.
   ============================================================================ */
import * as THREE from '../vendor/three.module.js';

// ---------------------------------------------------------------- paleta
const PAL = {
  sky: 0x9cc4e4, dirt: 0xb39a78, steel: 0x8a8f98, rust: 0xb4552d,
  olive: 0x6b7a3f, cream: 0xe8dcc0, hazard: 0xe8871f, blue: 0x4f6d8c,
  dark: 0x2e2a26, green: 0x7d9a4a, red: 0xa8433a, yellow: 0xd9b23a,
};
const SCRAP_COLORS = [PAL.rust, PAL.olive, PAL.blue, PAL.red, PAL.yellow, PAL.steel, PAL.green];

// ---------------------------------------------------------------- estado
const state = {
  money: 0, carryCap: 60, grabRange: 3.0, dmg: 1,
  owned: { magnet: false, back: false, mace: false },
  scaleKg: 0, scaleVal: 0, shopOpen: false, started: false,
};
const UPGRADES = [
  { id: 'magnet', ico: '🧲', slot: 'slot-magnet', name: 'Guante imán', desc: 'Alcance de agarre +50% y resorte más firme', cost: 150, apply: () => { state.grabRange = 4.5; } },
  { id: 'back', ico: '🎒', slot: 'slot-back', name: 'Espalda reforzada', desc: 'Capacidad de carga: 60 → 140 kg', cost: 250, apply: () => { state.carryCap = 140; } },
  { id: 'mace', ico: '🔨', slot: 'slot-mace', name: 'Mazo hidráulico', desc: 'Rompe al doble de rápido y empuja mucho más', cost: 400, apply: () => { state.dmg = 2; } },
];

// ---------------------------------------------------------------- renderer / escena
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.setSize(innerWidth, innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.NoToneMapping; // paleta plana = colores exactos
document.body.appendChild(renderer.domElement);
const canvas = renderer.domElement;

const scene = new THREE.Scene();
scene.background = new THREE.Color(PAL.sky);
scene.fog = new THREE.Fog(PAL.sky, 55, 150);

const camera = new THREE.PerspectiveCamera(72, innerWidth / innerHeight, 0.05, 300);

// luz: 1 direccional suave + hemisférica (la receta del informe)
const hemi = new THREE.HemisphereLight(0xcfe4f5, 0x7a6a55, 0.95);
scene.add(hemi);
const sun = new THREE.DirectionalLight(0xfff2dd, 1.7);
sun.position.set(34, 48, 22);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
const sc = sun.shadow.camera;
sc.left = -48; sc.right = 48; sc.top = 48; sc.bottom = -48; sc.near = 5; sc.far = 130;
sun.shadow.bias = -0.0006;
scene.add(sun);

const mat = (color, opts = {}) => new THREE.MeshLambertMaterial({ color, flatShading: true, ...opts });
const signMat = cv => { const tx = new THREE.CanvasTexture(cv); tx.colorSpace = THREE.SRGBColorSpace; return new THREE.MeshLambertMaterial({ map: tx }); };
const box = (w, h, d, color, x = 0, y = 0, z = 0) => {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat(color));
  m.position.set(x, y, z); m.castShadow = m.receiveShadow = true; scene.add(m); return m;
};

// ---------------------------------------------------------------- mundo estático
const ground = new THREE.Mesh(new THREE.CircleGeometry(90, 48), mat(PAL.dirt));
ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; scene.add(ground);

// piedritas instanciadas = textura visual barata
{
  const g = new THREE.DodecahedronGeometry(0.13, 0);
  const im = new THREE.InstancedMesh(g, mat(0xffffff), 150);
  const d = new THREE.Object3D(), c = new THREE.Color();
  for (let i = 0; i < 150; i++) {
    d.position.set((Math.random() - .5) * 68, 0.05, (Math.random() - .5) * 68);
    d.scale.setScalar(0.5 + Math.random() * 1.4); d.rotation.y = Math.random() * 3;
    d.updateMatrix(); im.setMatrixAt(i, d.matrix);
    c.setHex([PAL.steel, PAL.cream, PAL.rust][i % 3]).multiplyScalar(0.85 + Math.random() * 0.3);
    im.setColorAt(i, c);
  }
  im.receiveShadow = true; scene.add(im);
}

// cerco perimetral
const BOUND = 33;
box(BOUND * 2 + 1, 1.3, 0.5, PAL.steel, 0, 0.65, -BOUND);
box(BOUND * 2 + 1, 1.3, 0.5, PAL.steel, 0, 0.65, BOUND);
box(0.5, 1.3, BOUND * 2 + 1, PAL.steel, -BOUND, 0.65, 0);
box(0.5, 1.3, BOUND * 2 + 1, PAL.steel, BOUND, 0.65, 0);

// colisionadores estáticos (esferas aproximadas)
const statics = [];
const addStatic = (x, y, z, r) => statics.push(new THREE.Vector3(x, y, z), r);

// galpón / oficina
{
  const g = new THREE.Group();
  const body = new THREE.Mesh(new THREE.BoxGeometry(9, 3.6, 6.5), mat(PAL.cream));
  body.position.y = 1.8; body.castShadow = body.receiveShadow = true; g.add(body);
  const roof1 = new THREE.Mesh(new THREE.BoxGeometry(9.6, 0.25, 4), mat(PAL.rust));
  roof1.position.set(0, 3.9, -1.6); roof1.rotation.x = 0.32; roof1.castShadow = true; g.add(roof1);
  const roof2 = roof1.clone(); roof2.position.z = 1.6; roof2.rotation.x = -0.32; g.add(roof2);
  const door = new THREE.Mesh(new THREE.BoxGeometry(1.8, 2.6, 0.15), mat(PAL.dark));
  door.position.set(1.8, 1.3, 3.3); g.add(door);
  const win = new THREE.Mesh(new THREE.BoxGeometry(1.6, 1.1, 0.15), mat(PAL.hazard, { emissive: 0xffb35c, emissiveIntensity: 0.7 }));
  win.position.set(-1.6, 2.0, 3.3); g.add(win);
  g.position.set(-20, 0, -22); scene.add(g);
  statics.push(new THREE.Vector3(-20, 1.5, -22), 5.2);
  // cartel con textura canvas (tipografía redonda, como la UI del referente)
  const cv = document.createElement('canvas'); cv.width = 1024; cv.height = 256;
  const cx = cv.getContext('2d');
  cx.fillStyle = '#b4552d'; cx.fillRect(0, 0, 1024, 256);
  cx.fillStyle = '#f2e8d5'; cx.font = '900 108px system-ui, sans-serif';
  cx.textAlign = 'center'; cx.textBaseline = 'middle';
  cx.fillText('¡ESO TODAVÍA SIRVE!', 512, 136);
  const sign = new THREE.Mesh(new THREE.BoxGeometry(8.4, 2.1, 0.2), signMat(cv));
  sign.position.set(-20, 5.1, -18.6); scene.add(sign);
  const pole = box(0.25, 5, 0.25, PAL.dark, -20, 2.5, -18.6); pole.castShadow = true;
}

// trituradora (shredder) + cinta
{
  const g = new THREE.Group();
  const base = new THREE.Mesh(new THREE.BoxGeometry(6.5, 3.2, 4.5), mat(PAL.olive));
  base.position.y = 1.6; base.castShadow = base.receiveShadow = true; g.add(base);
  const hopper = new THREE.Mesh(new THREE.CylinderGeometry(3.1, 1.7, 2.4, 4), mat(PAL.olive));
  hopper.position.y = 4.3; hopper.rotation.y = Math.PI / 4; hopper.castShadow = true; g.add(hopper);
  const mouth = new THREE.Mesh(new THREE.BoxGeometry(3.2, 0.4, 3.2), mat(PAL.dark));
  mouth.position.y = 5.3; g.add(mouth);
  const drum = new THREE.Mesh(new THREE.CylinderGeometry(0.9, 0.9, 3.4, 10), mat(PAL.steel));
  drum.rotation.z = Math.PI / 2; drum.position.set(0, 5.05, 0); g.add(drum);
  g.userData.drum = drum;
  const belt = new THREE.Mesh(new THREE.BoxGeometry(9, 0.4, 1.8), mat(PAL.dark));
  belt.position.set(6.5, 2.2, 0); belt.rotation.z = 0.42; belt.castShadow = true; g.add(belt);
  const rail = new THREE.Mesh(new THREE.BoxGeometry(9, 0.5, 0.15), mat(PAL.hazard));
  rail.position.set(6.5, 2.6, 0.9); rail.rotation.z = 0.42; g.add(rail);
  g.position.set(20, 0, -20); scene.add(g);
  statics.push(new THREE.Vector3(20, 2, -20), 4.4);
  window.__drum = drum;
}

// grúa de garra (silueta)
{
  const g = new THREE.Group();
  const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.5, 0.7, 13, 8), mat(PAL.hazard));
  pole.position.y = 6.5; pole.castShadow = true; g.add(pole);
  const arm = new THREE.Mesh(new THREE.BoxGeometry(11, 0.7, 0.7), mat(PAL.hazard));
  arm.position.set(4, 12.6, 0); arm.castShadow = true; g.add(arm);
  const cable = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 6, 6), mat(PAL.dark));
  cable.position.set(8, 9.5, 0); g.add(cable);
  const claw = new THREE.Group();
  for (let i = 0; i < 4; i++) {
    const f = new THREE.Mesh(new THREE.BoxGeometry(0.18, 1.6, 0.5), mat(PAL.steel));
    f.position.y = -0.8; f.rotation.x = 0.5; f.rotation.y = i * Math.PI / 2;
    const pivot = new THREE.Group(); pivot.rotation.y = i * Math.PI / 2;
    f.position.set(0, -0.8, 0.35); pivot.add(f); claw.add(pivot);
  }
  claw.position.set(8, 6.4, 0); g.add(claw);
  g.position.set(-22, 0, 14); scene.add(g);
  statics.push(new THREE.Vector3(-22, 3, 14), 1.6);
}

// pilas de autos aplastados (landmark + colisor)
function carStack(x, z, rot) {
  const g = new THREE.Group();
  for (let i = 0; i < 3; i++) {
    const c = new THREE.Mesh(new THREE.BoxGeometry(2.3, 0.75, 4.4), mat(SCRAP_COLORS[(i + x | 0) % SCRAP_COLORS.length]));
    c.position.set((Math.random() - .5) * 0.5, 0.4 + i * 0.78, (Math.random() - .5) * 0.4);
    c.rotation.y = (Math.random() - .5) * 0.3; c.castShadow = c.receiveShadow = true; g.add(c);
  }
  g.position.set(x, 0, z); g.rotation.y = rot; scene.add(g);
  statics.push(new THREE.Vector3(x, 1.2, z), 2.8);
}
carStack(-26, -6, 0.3); carStack(-25, 2, -0.2); carStack(24, 8, 1.2); carStack(10, -27, 0.1);

// contenedores
{
  const a = box(6, 2.6, 2.5, PAL.olive, 4, 1.3, -30); a.castShadow = true;
  const b = box(6, 2.6, 2.5, PAL.blue, 4.4, 3.9, -30.2); b.rotation.y = 0.12; b.castShadow = true;
  const c = box(6, 2.6, 2.5, PAL.rust, -3.4, 1.3, -30.4); c.castShadow = true;
  statics.push(new THREE.Vector3(4, 1.5, -30), 3.6, new THREE.Vector3(-3.4, 1.5, -30.4), 3.6);
}

// balanza (zona de venta)
const SCALE_POS = new THREE.Vector3(12, 0, -6);
const scalePad = new THREE.Mesh(new THREE.CylinderGeometry(2.6, 2.7, 0.25, 24), mat(PAL.hazard));
scalePad.position.copy(SCALE_POS).setY(0.12); scalePad.receiveShadow = true; scene.add(scalePad);
const scaleRing = new THREE.Mesh(new THREE.TorusGeometry(2.65, 0.09, 8, 32), mat(PAL.hazard, { emissive: 0xe8871f, emissiveIntensity: 0.5 }));
scaleRing.rotation.x = -Math.PI / 2; scaleRing.position.copy(SCALE_POS).setY(0.3); scene.add(scaleRing);
{
  const pole = box(0.22, 3.2, 0.22, PAL.dark, SCALE_POS.x + 2.9, 1.6, SCALE_POS.z - 1.4);
  const cv = document.createElement('canvas'); cv.width = 512; cv.height = 256;
  const cx = cv.getContext('2d');
  cx.fillStyle = '#e8871f'; cx.fillRect(0, 0, 512, 256);
  cx.fillStyle = '#241d16'; cx.font = '900 120px system-ui, sans-serif';
  cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.fillText('BALANZA', 256, 100);
  cx.font = '800 64px system-ui, sans-serif'; cx.fillText('$ / KG', 256, 190);
  const sign = new THREE.Mesh(new THREE.BoxGeometry(2.6, 1.3, 0.15), signMat(cv));
  sign.position.set(SCALE_POS.x + 2.9, 3.4, SCALE_POS.z - 1.4); sign.rotation.y = -0.5; scene.add(sign);
  statics.push(new THREE.Vector3(SCALE_POS.x + 2.9, 1.5, SCALE_POS.z - 1.4), 0.6);
}

// compañero chibi (ojos emisivos = marca del estilo)
const buddy = new THREE.Group();
{
  const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.42, 0.62, 4, 8), mat(PAL.olive));
  body.position.y = 0.85; body.castShadow = true; buddy.add(body);
  const head = new THREE.Mesh(new THREE.SphereGeometry(0.44, 12, 10), mat(PAL.cream));
  head.position.y = 1.62; head.castShadow = true; buddy.add(head);
  const eyeG = new THREE.SphereGeometry(0.09, 8, 8);
  const eyeM = mat(0x9ff2e0, { emissive: 0x63f2d0, emissiveIntensity: 1.2 });
  const e1 = new THREE.Mesh(eyeG, eyeM); e1.position.set(-0.16, 1.66, 0.38); buddy.add(e1);
  const e2 = new THREE.Mesh(eyeG, eyeM); e2.position.set(0.16, 1.66, 0.38); buddy.add(e2);
  const cap = new THREE.Mesh(new THREE.SphereGeometry(0.46, 12, 6, 0, Math.PI * 2, 0, 1.1), mat(PAL.hazard));
  cap.position.y = 1.68; buddy.add(cap);
  buddy.position.set(7, 0, 2); buddy.rotation.y = -0.7; scene.add(buddy);
  statics.push(new THREE.Vector3(7, 1, 2), 0.8);
}

// montañas lejanas + nubes chunky
for (let i = 0; i < 6; i++) {
  const a = (i / 6) * Math.PI * 2 + 0.4;
  const m = new THREE.Mesh(new THREE.ConeGeometry(26 + i * 4, 13 + (i % 3) * 5, 5), mat(0x8fa3b8));
  m.position.set(Math.cos(a) * 95, 0, Math.sin(a) * 95); m.rotation.y = i; scene.add(m);
}
const clouds = [];
for (let i = 0; i < 5; i++) {
  const g = new THREE.Group();
  const cm = mat(0xffffff);
  for (let j = 0; j < 3; j++) {
    const s = new THREE.Mesh(new THREE.SphereGeometry(2.4 + Math.random() * 1.6, 8, 6), cm);
    s.position.set(j * 2.6 - 2.6, Math.random() * 0.8, Math.random() * 1.5); s.scale.y = 0.55; g.add(s);
  }
  g.position.set((Math.random() - .5) * 140, 26 + Math.random() * 8, (Math.random() - .5) * 140);
  scene.add(g); clouds.push(g);
}

// ---------------------------------------------------------------- viewmodel: llave inglesa
const wrench = new THREE.Group();
{
  const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.06, 0.62, 8), mat(PAL.steel));
  handle.rotation.x = 0.5; wrench.add(handle);
  const head = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.16, 0.1), mat(PAL.steel));
  head.position.set(0, 0.33, -0.16); wrench.add(head);
  const grip = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 0.16, 8), mat(PAL.hazard));
  grip.rotation.x = 0.5; grip.position.set(0, -0.3, 0.15); wrench.add(grip);
}
camera.add(wrench); wrench.position.set(0.42, -0.36, -0.75); wrench.rotation.z = -0.25;
scene.add(camera);

// ---------------------------------------------------------------- partículas (chispas)
const P_MAX = 160;
const pPos = new Float32Array(P_MAX * 3), pCol = new Float32Array(P_MAX * 3);
const pVel = new Float32Array(P_MAX * 3), pLife = new Float32Array(P_MAX);
const pGeo = new THREE.BufferGeometry();
pGeo.setAttribute('position', new THREE.BufferAttribute(pPos, 3));
pGeo.setAttribute('color', new THREE.BufferAttribute(pCol, 3));
const points = new THREE.Points(pGeo, new THREE.PointsMaterial({
  size: 0.16, vertexColors: true, transparent: true, opacity: 0.95,
  blending: THREE.AdditiveBlending, depthWrite: false,
}));
scene.add(points);
let pCursor = 0;
function sparks(at, n = 14, hot = true) {
  for (let i = 0; i < n; i++) {
    const k = pCursor = (pCursor + 1) % P_MAX;
    pPos[k * 3] = at.x; pPos[k * 3 + 1] = at.y; pPos[k * 3 + 2] = at.z;
    const v = new THREE.Vector3(Math.random() - .5, Math.random() * 0.9, Math.random() - .5).normalize().multiplyScalar(2.5 + Math.random() * 4);
    pVel[k * 3] = v.x; pVel[k * 3 + 1] = v.y; pVel[k * 3 + 2] = v.z;
    pLife[k] = 0.45 + Math.random() * 0.3;
    const c = hot ? (Math.random() < 0.5 ? 0xffb347 : 0xffe08a) : 0xc9c9c9;
    pCol[k * 3] = ((c >> 16) & 255) / 255; pCol[k * 3 + 1] = ((c >> 8) & 255) / 255; pCol[k * 3 + 2] = (c & 255) / 255;
  }
}

// ---------------------------------------------------------------- audio sintetizado (WebAudio)
let AC = null;
function audio() { if (!AC) AC = new (window.AudioContext || window.webkitAudioContext)(); return AC; }
function blip(freq, dur, type = 'square', vol = 0.25, slide = 0, when = 0) {
  const ac = audio(), t = ac.currentTime + when;
  const o = ac.createOscillator(), g = ac.createGain();
  o.type = type; o.frequency.setValueAtTime(freq, t);
  if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, freq + slide), t + dur);
  g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
  o.connect(g).connect(ac.destination); o.start(t); o.stop(t + dur + 0.02);
}
function noiseBurst(dur, freq, vol = 0.4, when = 0) {
  const ac = audio(), t = ac.currentTime + when;
  const len = Math.max(1, (dur * ac.sampleRate) | 0);
  const buf = ac.createBuffer(1, len, ac.sampleRate), d = buf.getChannelData(0);
  for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / len);
  const src = ac.createBufferSource(); src.buffer = buf;
  const f = ac.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = freq; f.Q.value = 1.1;
  const g = ac.createGain(); g.gain.value = vol;
  src.connect(f).connect(g).connect(ac.destination); src.start(t);
}
const sfx = {
  clank() { noiseBurst(0.09, 2600, 0.5); blip(520 + Math.random() * 380, 0.13, 'triangle', 0.22, -260); },
  bigclank() { noiseBurst(0.16, 1800, 0.6); blip(300, 0.22, 'triangle', 0.3, -160); blip(90, 0.25, 'sine', 0.4, -40); },
  thud() { blip(130, 0.14, 'sine', 0.4, -70); noiseBurst(0.05, 500, 0.25); },
  whoosh() { noiseBurst(0.12, 1300, 0.22); },
  grab() { blip(440, 0.07, 'sine', 0.25, 120); },
  coin() { blip(880, 0.08, 'square', 0.2); blip(1320, 0.14, 'square', 0.2, 0, 0.08); blip(1760, 0.18, 'square', 0.15, 0, 0.16); },
  deny() { blip(120, 0.22, 'sawtooth', 0.28, -30); },
  upgrade() { [523, 659, 784, 1046].forEach((f, i) => blip(f, 0.12, 'square', 0.2, 0, i * 0.07)); },
};

// ---------------------------------------------------------------- física de chatarra
const bodies = [];      // props dinámicos
const held = [];        // refs a bodies sostenidos
let bodyId = 0;
const KINDS = {
  door:    { name: 'puerta',    kg: 18, vpk: 1.2, hp: 2, make: c => new THREE.Mesh(new THREE.BoxGeometry(1.1, 0.95, 0.12), mat(c)), r: 0.62 },
  barrel:  { name: 'bidón',     kg: 25, vpk: 1.0, hp: 2, make: c => new THREE.Mesh(new THREE.CylinderGeometry(0.36, 0.36, 0.95, 10), mat(c)), r: 0.55 },
  tire:    { name: 'cubierta',  kg: 12, vpk: 1.6, hp: 1, make: c => new THREE.Mesh(new THREE.CylinderGeometry(0.46, 0.46, 0.32, 12), mat(0x3a3a3a)), r: 0.5 },
  engine:  { name: 'motor',     kg: 40, vpk: 1.5, hp: 3, make: c => new THREE.Mesh(new THREE.BoxGeometry(0.62, 0.55, 0.7), mat(c)), r: 0.52 },
  toaster: { name: 'tostadora', kg: 3,  vpk: 4.0, hp: 1, make: c => new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.26, 0.4), mat(PAL.cream)), r: 0.28 },
  pipe:    { name: 'caño',      kg: 15, vpk: 1.1, hp: 2, make: c => new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.09, 1.5, 8), mat(c)), r: 0.5 },
  chunk:   { name: 'chatarra',  kg: 0,  vpk: 0,   hp: 999, make: c => new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.24, 0.4), mat(c)), r: 0.3 },
};
function spawnProp(kind, pos, opts = {}) {
  const K = KINDS[kind];
  const color = opts.color ?? SCRAP_COLORS[(Math.random() * SCRAP_COLORS.length) | 0];
  const mesh = K.make(color);
  mesh.castShadow = mesh.receiveShadow = true;
  mesh.position.copy(pos);
  mesh.rotation.set(Math.random() * 3, Math.random() * 3, Math.random() * 3);
  scene.add(mesh);
  const kg = opts.kg ?? K.kg;
  const b = {
    id: bodyId++, kind, mesh, r: K.r, kg,
    vpk: opts.vpk ?? K.vpk, hp: opts.hp ?? K.hp,
    vel: new THREE.Vector3(), av: new THREE.Vector3((Math.random() - .5) * 2, (Math.random() - .5) * 2, (Math.random() - .5) * 2),
    held: false, frozen: false, pop: 0, shrinking: 0, color,
  };
  bodies.push(b);
  return b;
}
function removeBody(b) {
  scene.remove(b.mesh);
  const i = bodies.indexOf(b); if (i >= 0) bodies.splice(i, 1);
  const h = held.indexOf(b); if (h >= 0) held.splice(h, 1);
}
// reparto inicial de chatarra
const KIND_LIST = ['door', 'barrel', 'tire', 'engine', 'toaster', 'pipe', 'door', 'tire', 'barrel', 'pipe'];
for (let i = 0; i < 26; i++) {
  const a = Math.random() * Math.PI * 2, d = 5 + Math.random() * 24;
  spawnProp(KIND_LIST[i % KIND_LIST.length], new THREE.Vector3(Math.cos(a) * d, 0.8 + Math.random(), Math.sin(a) * d));
}
// goteo de chatarra nueva (la actividad nunca se seca)
setInterval(() => {
  if (!state.started) return;
  const live = bodies.filter(b => !b.shrinking).length;
  if (live < 26) spawnProp(KIND_LIST[(Math.random() * KIND_LIST.length) | 0], new THREE.Vector3(-22 + (Math.random() - .5) * 3, 4.5, 14 + (Math.random() - .5) * 3));
}, 7000);

// ---------------------------------------------------------------- jugador
const player = {
  pos: new THREE.Vector3(0, 1.7, 10), vel: new THREE.Vector3(), vy: 0,
  yaw: 0, pitch: -0.08, onGround: true, bob: 0,
};
const keys = new Set();
let locked = false, dragLook = false, rightDown = false;

canvas.addEventListener('contextmenu', e => e.preventDefault());
document.addEventListener('pointerlockchange', () => { locked = document.pointerLockElement === canvas; });
document.addEventListener('pointerlockerror', () => { dragLook = true; hint('pointer lock no disponible acá: mantené CLIC DERECHO para mirar'); });
function tryLock() {
  try {
    const p = canvas.requestPointerLock();
    if (p && p.catch) p.catch(() => { dragLook = true; hint('mantené CLIC DERECHO para mirar'); });
  } catch (e) { dragLook = true; hint('mantené CLIC DERECHO para mirar'); }
}
function hint(t) { document.getElementById('lookhint').textContent = t; }

document.addEventListener('mousemove', e => {
  if (state.shopOpen || !state.started) return;
  if (locked || (dragLook && rightDown)) {
    player.yaw -= e.movementX * 0.0024;
    player.pitch = THREE.MathUtils.clamp(player.pitch - e.movementY * 0.0024, -1.35, 1.35);
  }
});
document.addEventListener('mousedown', e => {
  if (!state.started || state.shopOpen) return;
  if (e.button === 2) { rightDown = true; return; }
  if (e.button === 0) { if (!locked && dragLook) { /* mirar */ } swing(); }
});
document.addEventListener('mouseup', e => { if (e.button === 2) rightDown = false; });
canvas.addEventListener('click', () => { if (state.started && !state.shopOpen && !locked) tryLock(); });

document.addEventListener('keydown', e => {
  if (!state.started) return;
  keys.add(e.code);
  if (e.code === 'KeyF') interact();
  if (e.code === 'KeyR') sell();
  if (e.code === 'KeyB') toggleShop();
  if (e.code === 'Space' && player.onGround && !state.shopOpen) { player.vy = 7.2; player.onGround = false; sfx.whoosh(); }
});
document.addEventListener('keyup', e => keys.delete(e.code));

// ---------------------------------------------------------------- interacción
const fwd = new THREE.Vector3(), right = new THREE.Vector3(), tmp = new THREE.Vector3();
function camDir() {
  camera.getWorldDirection(fwd);
  right.crossVectors(fwd, camera.up).normalize();
  return fwd;
}
function findGrabbable() {
  camDir();
  let best = null, bestD = 1e9;
  for (const b of bodies) {
    if (b.held || b.frozen || b.shrinking) continue;
    tmp.copy(b.mesh.position).sub(camera.position);
    const d = tmp.length();
    if (d > state.grabRange + b.r) continue;
    if (tmp.normalize().dot(fwd) < 0.45) continue;
    if (d < bestD) { bestD = d; best = b; }
  }
  return best;
}
function heldKg() { return held.reduce((s, b) => s + b.kg, 0); }
function nearScale() { return player.pos.distanceTo(tmp.copy(SCALE_POS).setY(player.pos.y)) < 4.2; }

function interact() {
  if (state.shopOpen) return;
  if (nearScale() && held.length) {           // depositar en balanza
    for (const b of [...held]) {
      b.held = false; b.frozen = true; b.vel.set(0, 0, 0);
      const i = bodies.filter(x => x.frozen && !x.shrinking).length;
      b.mesh.position.set(SCALE_POS.x + (Math.random() - .5) * 2.4, 0.45 + (i % 4) * 0.34, SCALE_POS.z + (Math.random() - .5) * 2.4);
      b.mesh.rotation.set(0, Math.random() * 3, 0);
      b.pop = 1;
      state.scaleKg += b.kg; state.scaleVal += b.kg * b.vpk;
      held.splice(held.indexOf(b), 1);
    }
    sfx.thud(); toast('Chatarra depositada en la balanza ⚖️');
    return;
  }
  if (held.length) {                          // soltar
    for (const b of [...held]) { b.held = false; b.vel.copy(camDir()).multiplyScalar(3); held.splice(held.indexOf(b), 1); }
    sfx.thud();
    return;
  }
  const b = findGrabbable();                  // agarrar
  if (!b) return;
  if (heldKg() + b.kg > state.carryCap) { sfx.deny(); toast(`⚠ ${b.kg} kg — te queda grande (B: mejoras)`); return; }
  b.held = true; b.frozen = false; held.push(b); sfx.grab();
}
function sell() {
  if (!nearScale() || state.scaleKg <= 0) { if (nearScale()) sfx.deny(); return; }
  const gain = Math.round(state.scaleVal);
  state.money += gain;
  for (const b of bodies.filter(x => x.frozen && !x.shrinking)) b.shrinking = 1;
  state.scaleKg = 0; state.scaleVal = 0;
  sfx.coin(); moneyPop(); toast(`💰 +$ ${gain.toLocaleString('es-AR')} — ¡eso todavía servía!`);
}

// golpe / lanzamiento
let swingT = -1;
function swing() {
  if (swingT >= 0) return;
  swingT = 0;
  if (held.length) {                          // lanzar lo sostenido (caos cooperativo)
    for (const b of [...held]) {
      b.held = false;
      b.vel.copy(camDir()).multiplyScalar(13).add(new THREE.Vector3(0, 2.5, 0));
      b.av.set((Math.random() - .5) * 14, (Math.random() - .5) * 14, (Math.random() - .5) * 14);
      held.splice(held.indexOf(b), 1);
    }
    sfx.whoosh();
    return;
  }
  sfx.whoosh();
  camDir();
  let hit = null, hd = 1e9;
  for (const b of bodies) {
    if (b.frozen || b.shrinking) continue;
    tmp.copy(b.mesh.position).sub(camera.position);
    const d = tmp.length();
    if (d > 2.7 + b.r || tmp.normalize().dot(fwd) < 0.55) continue;
    if (d < hd) { hd = d; hit = b; }
  }
  if (!hit) return;
  const power = 150 / (hit.kg + 12) * (state.owned.mace ? 1.6 : 1);
  hit.vel.addScaledVector(fwd, power).add(new THREE.Vector3(0, 3.2, 0));
  hit.av.set((Math.random() - .5) * 12, (Math.random() - .5) * 12, (Math.random() - .5) * 12);
  hit.hp -= state.dmg;
  sparks(hit.mesh.position, state.owned.mace ? 20 : 12);
  shake = Math.min(shake + 0.16, 0.4);
  if (hit.hp <= 0) breakProp(hit); else sfx.clank();
}
function breakProp(b) {
  sfx.bigclank();
  sparks(b.mesh.position, 26);
  shake = Math.min(shake + 0.3, 0.55);
  const pos = b.mesh.position.clone();
  const n = 2 + (Math.random() < 0.4 ? 1 : 0);
  for (let i = 0; i < n; i++) {
    const c = spawnProp('chunk', pos.clone().add(new THREE.Vector3((Math.random() - .5) * .6, 0.3, (Math.random() - .5) * .6)), {
      kg: Math.max(1, Math.round(b.kg * 0.45)), vpk: +(b.vpk * 1.3).toFixed(2), color: b.color,
    });
    c.vel.set((Math.random() - .5) * 6, 3 + Math.random() * 3, (Math.random() - .5) * 6);
    c.pop = 1;
  }
  removeBody(b);
  toast('💥 ¡se desarmó! los pedazos valen +30% por kg');
}

// ---------------------------------------------------------------- shop UI
const shopEl = document.getElementById('shop');
const listEl = document.getElementById('upg-list');
function renderShop() {
  listEl.innerHTML = '';
  for (const u of UPGRADES) {
    const owned = state.owned[u.id];
    const poor = !owned && state.money < u.cost;
    const div = document.createElement('div');
    div.className = 'upg' + (owned ? ' owned' : '') + (poor ? ' poor' : '');
    div.innerHTML = `<div class="ico">${u.ico}</div><div><div class="nm">${u.name}</div><div class="ds">${u.desc}</div></div><div class="pr">${owned ? '✓ COMPRADA' : '$ ' + u.cost.toLocaleString('es-AR')}</div>`;
    if (!owned) div.onclick = () => {
      if (state.money < u.cost) { sfx.deny(); div.classList.add('shake'); setTimeout(() => div.classList.remove('shake'), 350); return; }
      state.money -= u.cost; state.owned[u.id] = true; u.apply();
      document.getElementById(u.slot).classList.add('on');
      sfx.upgrade(); moneyPop(); renderShop(); toast(`${u.ico} ¡${u.name} comprada!`);
    };
    listEl.appendChild(div);
  }
}
function toggleShop(force) {
  state.shopOpen = force ?? !state.shopOpen;
  shopEl.classList.toggle('hidden', !state.shopOpen);
  if (state.shopOpen) { renderShop(); if (locked) document.exitPointerLock(); }
}
shopEl.addEventListener('mousedown', e => e.stopPropagation());

// ---------------------------------------------------------------- HUD
const el = id => document.getElementById(id);
const handKgEl = el('hand-kg'), handItemsEl = el('hand-items'), moneyEl = el('money');
const scaleBadge = el('scale-badge'), scaleKgEl = el('scale-kg'), scaleValEl = el('scale-val');
const promptEl = el('prompt'), toastEl = el('toast');
let toastTimer = 0;
function toast(t) {
  toastEl.textContent = t; toastEl.classList.remove('hidden');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => toastEl.classList.add('hidden'), 2200);
}
function moneyPop() { moneyEl.classList.remove('pop'); void moneyEl.offsetWidth; moneyEl.classList.add('pop'); }
const fmt = n => '$ ' + Math.round(n).toLocaleString('es-AR');

// ---------------------------------------------------------------- loop
let shake = 0, prevHand = -1, prevMoney = -1, prevScale = -1;
const clock = new THREE.Clock();
function tick() {
  requestAnimationFrame(tick);
  const dt = Math.min(clock.getDelta(), 0.05);
  const t = clock.elapsedTime;

  if (state.started) {
    // ---- input movimiento
    camDir();
    const run = keys.has('ShiftLeft') || keys.has('ShiftRight');
    const speed = run ? 8.5 : 5.0;
    const wish = new THREE.Vector3();
    if (keys.has('KeyW') || keys.has('ArrowUp')) wish.add(fwd);
    if (keys.has('KeyS') || keys.has('ArrowDown')) wish.sub(fwd);
    if (keys.has('KeyD') || keys.has('ArrowRight')) wish.add(right);
    if (keys.has('KeyA') || keys.has('ArrowLeft')) wish.sub(right);
    wish.y = 0;
    if (wish.lengthSq() > 0) wish.normalize().multiplyScalar(speed);
    player.vel.lerp(wish, 1 - Math.pow(0.0001, dt));
    player.pos.addScaledVector(player.vel, dt);
    player.vy -= 22 * dt; player.pos.y += player.vy * dt;
    if (player.pos.y <= 1.7) { player.pos.y = 1.7; player.vy = 0; if (!player.onGround) sfx.thud(); player.onGround = true; }
    // límites + estáticos
    player.pos.x = THREE.MathUtils.clamp(player.pos.x, -BOUND + 1, BOUND - 1);
    player.pos.z = THREE.MathUtils.clamp(player.pos.z, -BOUND + 1, BOUND - 1);
    for (let i = 0; i < statics.length; i += 2) {
      const c = statics[i], r = statics[i + 1] + 0.5;
      tmp.set(player.pos.x - c.x, 0, player.pos.z - c.z);
      const d = tmp.length();
      if (d < r && d > 0.001 && player.pos.y < c.y + r) { tmp.normalize(); player.pos.x = c.x + tmp.x * r; player.pos.z = c.z + tmp.z * r; }
    }
    player.bob += player.vel.length() * dt * 1.7;

    // ---- cámara + shake + bob
    const bobY = Math.sin(player.bob * 6) * 0.035 * Math.min(1, player.vel.length() / 5);
    camera.position.set(
      player.pos.x + (Math.random() - .5) * shake * 0.22,
      player.pos.y + bobY + (Math.random() - .5) * shake * 0.22,
      player.pos.z + (Math.random() - .5) * shake * 0.22);
    camera.rotation.set(0, 0, 0, 'YXZ');
    camera.rotation.y = player.yaw; camera.rotation.x = player.pitch;
    camera.rotation.z = Math.sin(player.bob * 3) * 0.008 + (Math.random() - .5) * shake * 0.05;
    shake = Math.max(0, shake - dt * 1.6);

    // ---- swing anim del viewmodel
    if (swingT >= 0) {
      swingT += dt / 0.26;
      const s = Math.sin(Math.min(swingT, 1) * Math.PI);
      wrench.rotation.x = -1.9 * s; wrench.position.z = -0.75 + 0.22 * s; wrench.position.y = -0.36 + 0.1 * s;
      if (swingT >= 1) { swingT = -1; wrench.rotation.x = 0; wrench.position.set(0.42, -0.36, -0.75); }
    } else {
      wrench.position.y = -0.36 + Math.sin(player.bob * 6 + 1) * 0.012;
    }

    // ---- física de props
    for (const b of bodies) {
      if (b.shrinking) {
        b.shrinking += dt * 3;
        const s = Math.max(0, 1 - b.shrinking);
        b.mesh.scale.setScalar(s); b.mesh.rotation.y += dt * 9; b.mesh.position.y += dt * 2.2;
        if (s <= 0) removeBody(b);
        continue;
      }
      if (b.pop > 0) { b.pop = Math.max(0, b.pop - dt * 4); b.mesh.scale.setScalar(1 + b.pop * 0.35); }
      if (b.frozen) continue;
      if (b.held) {
        const i = held.indexOf(b);
        const target = camera.position.clone().addScaledVector(camDir(), 1.9)
          .addScaledVector(right, (i - (held.length - 1) / 2) * 0.55).add(new THREE.Vector3(0, -0.28, 0));
        b.vel.copy(target.sub(b.mesh.position).multiplyScalar(state.owned.magnet ? 16 : 11));
        b.mesh.position.addScaledVector(b.vel, dt);
        b.mesh.rotation.x += (0 - b.mesh.rotation.x % (Math.PI * 2)) * 0; // mantiene pose
        b.av.multiplyScalar(1 - 6 * dt);
        b.mesh.rotation.x += b.av.x * dt; b.mesh.rotation.z += b.av.z * dt;
        continue;
      }
      b.vel.y -= 22 * dt;
      b.mesh.position.addScaledVector(b.vel, dt);
      const onG = b.mesh.position.y <= b.r + 0.001;
      if (b.mesh.position.y < b.r) {
        b.mesh.position.y = b.r;
        if (b.vel.y < -4) sfx.thud();
        b.vel.y = -b.vel.y * 0.25; if (Math.abs(b.vel.y) < 1.2) b.vel.y = 0;
      }
      if (onG || b.mesh.position.y <= b.r + 0.05) {
        b.vel.x *= 1 - Math.min(1, 5 * dt); b.vel.z *= 1 - Math.min(1, 5 * dt);
        // rueda como si rodara
        b.av.x = THREE.MathUtils.lerp(b.av.x, b.vel.z / b.r, 0.2);
        b.av.z = THREE.MathUtils.lerp(b.av.z, -b.vel.x / b.r, 0.2);
      }
      b.av.multiplyScalar(1 - Math.min(1, 1.6 * dt));
      b.mesh.rotation.x += b.av.x * dt; b.mesh.rotation.y += b.av.y * dt; b.mesh.rotation.z += b.av.z * dt;
      // cerco
      b.mesh.position.x = THREE.MathUtils.clamp(b.mesh.position.x, -BOUND + b.r, BOUND - b.r);
      b.mesh.position.z = THREE.MathUtils.clamp(b.mesh.position.z, -BOUND + b.r, BOUND - b.r);
      // estáticos
      for (let i = 0; i < statics.length; i += 2) {
        const c = statics[i], r = statics[i + 1] + b.r;
        tmp.copy(b.mesh.position).sub(c);
        const d = tmp.length();
        if (d < r && d > 0.001) { tmp.normalize(); b.mesh.position.copy(c).addScaledVector(tmp, r); b.vel.addScaledVector(tmp, Math.max(0, -b.vel.dot(tmp)) * 1.3); }
      }
      // jugador empuja (comedia de tropezar)
      tmp.set(b.mesh.position.x - player.pos.x, 0, b.mesh.position.z - player.pos.z);
      const pd = tmp.length();
      if (pd < b.r + 0.55 && pd > 0.001) {
        tmp.normalize();
        b.mesh.position.x = player.pos.x + tmp.x * (b.r + 0.55);
        b.mesh.position.z = player.pos.z + tmp.z * (b.r + 0.55);
        b.vel.addScaledVector(tmp, player.vel.length() * 0.5 + 1.2);
      }
      if (b.vel.lengthSq() < 0.02 && onG) b.vel.set(0, 0, 0);
    }
    // pares prop-prop (apilamiento torpe)
    for (let i = 0; i < bodies.length; i++) for (let j = i + 1; j < bodies.length; j++) {
      const a = bodies[i], b = bodies[j];
      if (a.frozen || b.frozen || a.held || b.held || a.shrinking || b.shrinking) continue;
      tmp.copy(a.mesh.position).sub(b.mesh.position);
      const d = tmp.length(), rr = a.r + b.r;
      if (d < rr && d > 0.001) {
        tmp.normalize();
        const push = (rr - d) * 0.5;
        a.mesh.position.addScaledVector(tmp, push); b.mesh.position.addScaledVector(tmp, -push);
        const rel = a.vel.dot(tmp) - b.vel.dot(tmp);
        if (rel < 0) { const m = a.kg + b.kg; a.vel.addScaledVector(tmp, -rel * (b.kg / m) * 1.1); b.vel.addScaledVector(tmp, rel * (a.kg / m) * 1.1); }
      }
    }

    // ---- partículas
    for (let k = 0; k < P_MAX; k++) {
      if (pLife[k] <= 0) { pCol[k * 3] = pCol[k * 3 + 1] = pCol[k * 3 + 2] = 0; continue; }
      pLife[k] -= dt;
      pVel[k * 3 + 1] -= 12 * dt;
      pPos[k * 3] += pVel[k * 3] * dt; pPos[k * 3 + 1] += pVel[k * 3 + 1] * dt; pPos[k * 3 + 2] += pVel[k * 3 + 2] * dt;
      if (pPos[k * 3 + 1] < 0.05) { pPos[k * 3 + 1] = 0.05; pVel[k * 3 + 1] *= -0.4; }
      const f = Math.max(0, pLife[k] * 2);
      pCol[k * 3] *= 1; // color fijo, el fade va por vida en tamaño aparente
      void f;
    }
    pGeo.attributes.position.needsUpdate = true;
    pGeo.attributes.color.needsUpdate = true;

    // ---- vida de escena
    if (window.__drum) window.__drum.rotation.x += dt * 2.2;
    for (const c of clouds) { c.position.x += dt * 0.6; if (c.position.x > 90) c.position.x = -90; }
    buddy.position.y = Math.sin(t * 2.2) * 0.05; buddy.rotation.y = -0.7 + Math.sin(t * 0.7) * 0.25;
    const ns = nearScale();
    scaleRing.scale.setScalar(ns ? 1 + Math.sin(t * 6) * 0.05 : 1);
    scaleRing.material.emissiveIntensity = ns ? 1.1 : 0.5;

    // ---- HUD
    const hkg = heldKg();
    if (hkg !== prevHand) { handKgEl.textContent = hkg; handItemsEl.textContent = held.length ? `· ${held.length} obj` : ''; prevHand = hkg; }
    if (state.money !== prevMoney) { moneyEl.textContent = fmt(state.money); prevMoney = state.money; if (state.shopOpen) renderShop(); }
    if (state.scaleKg !== prevScale) {
      scaleKgEl.textContent = state.scaleKg; scaleValEl.textContent = state.scaleKg ? `≈ ${fmt(state.scaleVal)}` : '';
      scaleBadge.classList.toggle('hidden', state.scaleKg === 0);
      prevScale = state.scaleKg;
    }
    // prompt contextual
    let p = '';
    if (!state.shopOpen) {
      if (ns && held.length) p = `<kbd>F</kbd>Depositar ${hkg} kg en la balanza`;
      else if (ns && state.scaleKg > 0) p = `<kbd>R</kbd>Vender ${state.scaleKg} kg por ${fmt(state.scaleVal)}`;
      else if (held.length) p = `<kbd>F</kbd>Soltar · <kbd>CLIC</kbd>lanzar`;
      else {
        const g = findGrabbable();
        if (g) p = heldKg() + g.kg > state.carryCap
          ? `⚠ ${g.name} de ${g.kg} kg — te queda grande (B: mejoras)`
          : `<kbd>F</kbd>Agarrar ${g.name} (${g.kg} kg · ${fmt(g.kg * g.vpk)})`;
      }
    }
    if (p) { promptEl.innerHTML = p; promptEl.classList.remove('hidden'); }
    else promptEl.classList.add('hidden');
  }

  renderer.render(scene, camera);
}
tick();

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
});

// ---------------------------------------------------------------- arranque
document.getElementById('enter').addEventListener('click', () => {
  document.getElementById('intro').classList.add('hidden');
  state.started = true;
  audio().resume?.();
  tryLock();
  toast('🔧 Rompé chatarra y vendela en la balanza naranja');
});
