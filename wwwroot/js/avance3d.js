// Visor 3D del avance de obra de una torre (Three.js). Módulo ES, lo usa AvanceObra.razor.
// Diorama: bloque de terreno con las 4 excavaciones (cotas de la tabla de torres o genéricas), solado,
// acero, zapata y pedestal (o parrilla), relleno y la torre montada según el % de montaje, por peso de
// las piezas del modelo de Modelos 3D (o una silueta aproximada si la torre no tiene modelo).
// Trabajadores, equipos, pluma, vientos y ancladeros son una SIMULACIÓN ilustrativa (sin escala ni procedimiento).
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { miembrosParametricos } from './linea3d.js';

const PATAS = ['A', 'B', 'C', 'D'];
// SUPUESTO: pata A = (-x,+z), B = (+x,+z), C = (+x,-z), D = (-x,-z) del modelo.
const SIGNO = { A: [-1, 1], B: [1, 1], C: [1, -1], D: [-1, -1] };
const KG_M_SIN_PESO = 6;   // SUPUESTO: pieza sin peso en la lista de materiales
const CLAVES_PATA = ['exc', 'sol', 'niv', 'ace', 'con', 'rel'];
const CLAVES_TORRE = ['pre', 'mon', 'ver', 'ves'];

const cacheModelos = new Map();
function leerModelo(url) {
    if (!cacheModelos.has(url))
        cacheModelos.set(url, fetch(url).then(r => r.ok ? r.json() : null).catch(() => null));
    return cacheModelos.get(url);
}

// Piezas del modelo de planos (mm → m), con su peso. Los stubs van con la cimentación.
function desdeModelo(js) {
    const unit = new Map();
    for (const f of js.lista_materiales || []) {
        const cant = parseFloat(f.cant) || 1;
        if (f.peso_kg) unit.set(`${f.hoja}|${f.marca}`, f.peso_kg / cant);
    }
    const miembros = [], stubs = [];
    for (const m of js.miembros || []) {
        const a = m.a.map(v => v / 1000), b = m.b.map(v => v / 1000);
        if (m.tramo === 'Stub') { stubs.push(a[1] < b[1] ? { pie: a, cab: b } : { pie: b, cab: a }); continue; }
        const L = Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
        let w = null;
        for (const k of m.marcas || []) if (unit.has(k)) { w = unit.get(k); break; }
        miembros.push({ a, b, w: w ?? KG_M_SIN_PESO * L, r: .07 });
    }
    return { miembros, stubs, nombre: js.meta?.nombre || '', plano: js.meta?.plano_base || '' };
}

function desdeSilueta(familia, cuerpo) {
    const d = miembrosParametricos(familia || 'AA', cuerpo || 1);
    const miembros = d.M.map(([a, b, r]) => {
        const L = Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
        return { a, b, w: KG_M_SIN_PESO * L, r: Math.max(.06, r * .5) };
    });
    return { miembros, stubs: [], nombre: 'Silueta aproximada', plano: '' };
}

// ---------- Texturas ----------
function ruido(g, w, h, n, colores, tam = [1, 2.5]) {
    for (let i = 0; i < n; i++) {
        g.fillStyle = colores[(Math.random() * colores.length) | 0];
        const r = tam[0] + Math.random() * (tam[1] - tam[0]);
        g.beginPath(); g.ellipse(Math.random() * w, Math.random() * h, r, r * (.6 + Math.random() * .6), Math.random() * 3, 0, 7); g.fill();
    }
}
function texEstratos() {
    const W = 256, H = 512, c = document.createElement('canvas'); c.width = W; c.height = H; const g = c.getContext('2d');
    // Horizontes del suelo con contactos ondulados (capa vegetal, limos, arcillas, arenas, roca meteorizada).
    const capas = [['#4a3b2a', 40], ['#6e5439', 110], ['#8a6844', 130], ['#a5845b', 110], ['#7f7a72', 122]];
    let y0 = 0;
    for (const [col, h] of capas) {
        g.fillStyle = col; g.beginPath(); g.moveTo(0, y0);
        for (let x = 0; x <= W; x += 16) g.lineTo(x, y0 + Math.sin(x / 37 + y0) * 4);
        g.lineTo(W, H); g.lineTo(0, H); g.closePath(); g.fill(); y0 += h;
    }
    const sombra = g.createLinearGradient(0, 0, 0, H); sombra.addColorStop(0, 'rgba(0,0,0,0)'); sombra.addColorStop(1, 'rgba(0,0,0,.22)');
    g.fillStyle = sombra; g.fillRect(0, 0, W, H);
    ruido(g, W, H, 2600, ['rgba(0,0,0,.14)', 'rgba(255,255,255,.07)', 'rgba(40,25,10,.18)'], [.6, 1.8]);
    ruido(g, W, H, 140, ['rgba(160,150,135,.55)', 'rgba(110,100,90,.5)'], [1.5, 3.5]);   // gravas
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = THREE.RepeatWrapping; t.anisotropy = 4; return t;
}
function texPasto() {
    const c = document.createElement('canvas'); c.width = c.height = 512; const g = c.getContext('2d');
    g.fillStyle = '#5f7d3c'; g.fillRect(0, 0, 512, 512);
    // Manchas suaves de tono + briznas.
    for (let i = 0; i < 40; i++) {
        const x = Math.random() * 512, y = Math.random() * 512, r = 40 + Math.random() * 90, gr = g.createRadialGradient(x, y, 0, x, y, r);
        const col = Math.random() > .5 ? '95,118,62' : '150,160,98'; gr.addColorStop(0, `rgba(${col},.35)`); gr.addColorStop(1, `rgba(${col},0)`);
        g.fillStyle = gr; g.fillRect(x - r, y - r, 2 * r, 2 * r);
    }
    for (let i = 0; i < 9000; i++) {
        const x = Math.random() * 512, y = Math.random() * 512, l = 2 + Math.random() * 4;
        g.strokeStyle = Math.random() > .55 ? 'rgba(62,84,38,.45)' : 'rgba(176,186,120,.35)'; g.lineWidth = .8;
        g.beginPath(); g.moveTo(x, y); g.lineTo(x + (Math.random() - .5) * 2, y - l); g.stroke();
    }
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(4, 4); t.anisotropy = 8; return t;
}
function texRelleno() {
    const c = document.createElement('canvas'); c.width = 64; c.height = 256; const g = c.getContext('2d');
    for (let i = 0; i < 8; i++) { g.fillStyle = i % 2 ? '#ad8858' : '#a07c4f'; g.fillRect(0, i * 32, 64, 32); g.fillStyle = 'rgba(60,40,20,.35)'; g.fillRect(0, i * 32 + 31, 64, 1); }
    ruido(g, 64, 256, 500, ['rgba(0,0,0,.12)', 'rgba(255,255,255,.08)'], [.5, 1.4]);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(1, 2); return t;
}
// Sombra de contacto (degradado radial) bajo el diorama.
function texSombra() {
    const c = document.createElement('canvas'); c.width = c.height = 256; const g = c.getContext('2d');
    const gr = g.createRadialGradient(128, 128, 30, 128, 128, 128); gr.addColorStop(0, 'rgba(0,0,0,.42)'); gr.addColorStop(.55, 'rgba(0,0,0,.16)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 256, 256);
    return new THREE.CanvasTexture(c);
}
function etiqueta(txt) {
    const c = document.createElement('canvas'); c.width = c.height = 128; const g = c.getContext('2d');
    g.shadowColor = 'rgba(0,0,0,.35)'; g.shadowBlur = 10; g.shadowOffsetY = 3;
    g.fillStyle = 'rgba(255,255,255,.94)'; g.beginPath(); g.arc(64, 62, 40, 0, 7); g.fill();
    g.shadowColor = 'transparent'; g.lineWidth = 4; g.strokeStyle = '#EC1C24'; g.stroke();
    g.fillStyle = '#1f2a3a'; g.font = '700 46px system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(txt, 64, 65);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false })); s.scale.set(.8, .8, 1); s.renderOrder = 10; return s;
}

const pieza = (geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; return m; };
const piv = (x = 0, y = 0, z = 0) => { const g = new THREE.Group(); g.position.set(x, y, z); return g; };
const caja = (w, h, d, mat) => { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat); m.castShadow = m.receiveShadow = true; return m; };
function barra(a, b, r, mat) {
    const va = new THREE.Vector3(...a), vb = new THREE.Vector3(...b), L = va.distanceTo(vb);
    const m = caja(r, L, r, mat); m.position.copy(va).add(vb).multiplyScalar(.5);
    m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), vb.clone().sub(va).normalize()); return m;
}
const semi = (x, z) => new THREE.Vector3(x, 0, z).normalize();
const ease = (a, b, k) => a + (b - a) * k;

// Entorno simple (cielo claro, suelo oscuro y dos paneles de luz) para los reflejos del acero galvanizado.
function entorno(renderer) {
    const pm = new THREE.PMREMGenerator(renderer), esc = new THREE.Scene();
    const cielo = new THREE.Mesh(new THREE.SphereGeometry(50, 32, 16), new THREE.MeshBasicMaterial({ side: THREE.BackSide, vertexColors: true }));
    const pos = cielo.geometry.attributes.position, col = [];
    for (let i = 0; i < pos.count; i++) { const k = (pos.getY(i) / 50 + 1) / 2; col.push(.35 + .6 * k, .33 + .62 * k, .3 + .7 * k); }
    cielo.geometry.setAttribute('color', new THREE.Float32BufferAttribute(col, 3)); esc.add(cielo);
    for (const [x, y, z] of [[20, 25, 10], [-25, 18, -15]]) {
        const l = new THREE.Mesh(new THREE.PlaneGeometry(14, 8), new THREE.MeshBasicMaterial({ color: 0xffffff, side: THREE.DoubleSide }));
        l.position.set(x, y, z); l.lookAt(0, 0, 0); esc.add(l);
    }
    const tex = pm.fromScene(esc, .04).texture; pm.dispose();
    esc.traverse(o => { o.geometry?.dispose(); o.material?.dispose(); });
    return tex;
}

export function crear(contenedor) { return new VisorAvance(contenedor); }

// Pantalla completa del marco del visor (si el navegador no la permite, lo maximiza en la ventana).
export async function pantallaCompleta(marco) {
    const doc = document;
    if (doc.fullscreenElement === marco) { await doc.exitFullscreen(); return false; }
    if (marco.classList.contains('av3d-max')) { marco.classList.remove('av3d-max'); return false; }
    try { await marco.requestFullscreen({ navigationUI: 'hide' }); }
    catch { marco.classList.add('av3d-max'); }
    return true;
}

class VisorAvance {
    constructor(cont) {
        this.cont = cont;
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setPixelRatio(Math.min(2, devicePixelRatio));
        this.renderer.shadowMap.enabled = true; this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = .95;
        this.renderer.domElement.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;display:block;';
        cont.appendChild(this.renderer.domElement);
        this.scene = new THREE.Scene();
        this.cam = new THREE.PerspectiveCamera(38, 1, 0.1, 1200);
        this.ctl = new OrbitControls(this.cam, this.renderer.domElement);
        this.ctl.enableDamping = true; this.ctl.maxPolarAngle = Math.PI * .49; this.ctl.autoRotateSpeed = .6;
        // Zoom hacia donde apunta el mouse (el centro de giro lo sigue: no se queda "pegado" al acercarse)
        // y paneo en el plano de la pantalla. Rueda dentro del visor = solo zoom (la página no se desplaza).
        this.ctl.zoomToCursor = true; this.ctl.screenSpacePanning = true;
        this.ctl.minDistance = 1.2; this.ctl.maxDistance = 600;
        this._rueda = e => e.preventDefault();
        cont.addEventListener('wheel', this._rueda, { passive: false });
        this.modo('girar');
        this.fondo();
        this._mq = matchMedia('(prefers-color-scheme: dark)'); this._mq.addEventListener('change', this._fondo = () => this.fondo());
        this._mo = new MutationObserver(this._fondo); this._mo.observe(document.documentElement, { attributes: true });

        this.scene.add(new THREE.HemisphereLight(0xeef4ff, 0x5d4b38, 1.0));
        this.sol = new THREE.DirectionalLight(0xfff1dc, 2.6);
        this.sol.castShadow = true; this.sol.shadow.mapSize.set(2048, 2048); this.sol.shadow.bias = -0.0004; this.sol.shadow.normalBias = .02; this.sol.shadow.radius = 3;
        const relleno = new THREE.DirectionalLight(0xcfe0ff, .55); relleno.position.set(-30, 18, -22); this.scene.add(relleno);
        this.scene.environment = entorno(this.renderer);
        this.scene.add(this.sol, this.sol.target);

        this.tEstr = texEstratos(); this.tRel = texRelleno(); this.tPasto = texPasto(); this.tSombra = texSombra();
        this.M = {
            pared: new THREE.MeshStandardMaterial({ map: this.tEstr, side: THREE.BackSide, roughness: 1, transparent: true }),
            lado: new THREE.MeshStandardMaterial({ map: this.tEstr, roughness: 1, transparent: true }),
            pasto: new THREE.MeshStandardMaterial({ map: this.tPasto, roughness: .95, transparent: true }),
            suelo: new THREE.MeshStandardMaterial({ color: 0x7d6142, roughness: 1, transparent: true }),
            relleno: new THREE.MeshStandardMaterial({ map: this.tRel, roughness: 1, transparent: true }),
            relTope: new THREE.MeshStandardMaterial({ color: 0xa88458, roughness: 1, transparent: true }),
            solado: new THREE.MeshStandardMaterial({ color: 0x8f8d88, roughness: .9 }),
            conc: new THREE.MeshStandardMaterial({ color: 0xc9c5bd, roughness: .9, envMapIntensity: .4 }),
            acero: new THREE.LineBasicMaterial({ color: 0x9c4a2c }),
            galv: new THREE.MeshStandardMaterial({ color: 0xa3acb5, metalness: .75, roughness: .38, envMapIntensity: .9 }),
            hoy: new THREE.MeshStandardMaterial({ color: 0xec1c24, metalness: .3, roughness: .5 }),
            base: new THREE.MeshStandardMaterial({ color: 0x2f2c29, roughness: .55, metalness: .1 }),
            borde: new THREE.MeshStandardMaterial({ color: 0x5f7243, roughness: .9 }),
            torre: new THREE.MeshStandardMaterial({ metalness: .72, roughness: .36, envMapIntensity: 1 }),
            aisl: new THREE.MeshStandardMaterial({ color: 0x3f7f73, roughness: .3, metalness: .1 }),
        };
        this.TIERRA = [this.M.pasto, this.M.lado, this.M.pared, this.M.suelo, this.M.relleno, this.M.relTope];
        this.MQ = {
            amarillo: new THREE.MeshStandardMaterial({ color: 0xe8b324, roughness: .55 }),
            negro: new THREE.MeshStandardMaterial({ color: 0x2a2a2a, roughness: .8 }),
            vidrio: new THREE.MeshStandardMaterial({ color: 0x8fb3c7, roughness: .1, metalness: .2, transparent: true, opacity: .55 }),
            naranja: new THREE.MeshStandardMaterial({ color: 0xe2672a, roughness: .5 }),
            piel: new THREE.MeshStandardMaterial({ color: 0xc28b62, roughness: .8 }),
            jean: new THREE.MeshStandardMaterial({ color: 0x34465e, roughness: .8 }),
            casco: new THREE.MeshStandardMaterial({ color: 0xf4f4f0, roughness: .4 }),
            chaleco: new THREE.MeshStandardMaterial({ color: 0xff7a1a, roughness: .5, emissive: 0x331100 }),
            gris: new THREE.MeshStandardMaterial({ color: 0x6d737a, roughness: .6, metalness: .3 }),
            viento: new THREE.LineBasicMaterial({ color: 0x3b3b3b }),
        };
        this.crearEquipos();

        this.vis = null; this.obj = null; this.prev = null;
        this.transp = false;
        this.reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
        this._ro = new ResizeObserver(() => this.tam()); this._ro.observe(cont); this.tam();
        this._raf = requestAnimationFrame(this._bucle = t => this.bucle(t));
    }

    // Fondo en degradado (más claro arriba) a partir de --av3d-fondo del tema.
    fondo() {
        let base;
        try { base = new THREE.Color(getComputedStyle(this.cont).getPropertyValue('--av3d-fondo').trim() || '#e9ecef'); } catch { base = new THREE.Color('#e9ecef'); }
        const arriba = base.clone().lerp(new THREE.Color(0xffffff), base.getHSL({}).l > .5 ? .55 : .12);
        const c = document.createElement('canvas'); c.width = 2; c.height = 256; const g = c.getContext('2d');
        const gr = g.createLinearGradient(0, 0, 0, 256); gr.addColorStop(0, '#' + arriba.getHexString()); gr.addColorStop(1, '#' + base.getHexString());
        g.fillStyle = gr; g.fillRect(0, 0, 2, 256);
        const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
        this.scene.background?.dispose?.(); this.scene.background = t;
    }

    tam() {
        const r = this.cont.getBoundingClientRect();
        this.renderer.setSize(Math.max(1, r.width), Math.max(1, r.height), false);
        this.cam.aspect = r.width / Math.max(1, r.height); this.cam.updateProjectionMatrix();
    }

    // ---------- Torre y diorama ----------
    async cargarTorre(datos, cuadro, previo) {
        const pedido = this._pedido = (this._pedido || 0) + 1;
        let fuente = null;
        if (datos.modeloUrl) { const js = await leerModelo(datos.modeloUrl); if (js?.miembros?.length) fuente = desdeModelo(js); }
        if (pedido !== this._pedido) return;   // se pidió otra torre mientras cargaba
        if (!fuente) fuente = desdeSilueta(datos.familia, datos.cuerpo);
        this.limpiar();
        this.datos = datos; this.fuente = fuente;
        this.armar();
        this.vis = null;
        this.actualizar(cuadro, previo);
        // Sin montaje todavía: se encuadra la cimentación (la torre completa deja el diorama diminuto).
        const tq = cuadro?.torre || cuadro?.Torre || {};
        this.vista((tq.pre > 0 || tq.mon > 0) ? 'torre' : 'cimentacion');
    }

    limpiar() {
        if (!this.grupo) return;
        this.scene.remove(this.grupo);
        this.grupo.traverse(o => {
            if (o.geometry) o.geometry.dispose();
            if (o.isSprite) { o.material.map?.dispose(); o.material.dispose(); }
        });
        this.inst?.dispose?.();
        this.grupo = null;
    }

    armar() {
        const g = this.grupo = new THREE.Group(); this.scene.add(g);
        const F = this.fuente, M = this.M;

        // Miembros de la torre ordenados de abajo hacia arriba, con su peso acumulado.
        const mm = F.miembros.map(m => {
            let va = new THREE.Vector3(...m.a), vb = new THREE.Vector3(...m.b);
            if (va.y > vb.y) [va, vb] = [vb, va];
            const L = va.distanceTo(vb);
            return { va, vb, L, dir: vb.clone().sub(va).normalize(), w: m.w, r: m.r };
        }).filter(m => m.L > 1e-3).sort((p, q) => (p.va.y + p.vb.y) - (q.va.y + q.vb.y));   // por cota media: crece de abajo hacia arriba
        let acum = 0; for (const m of mm) { acum += m.w; m.cum = acum; }
        this.miembros = mm; this.peso = acum || 1;
        this.alto = Math.max(1, ...mm.map(m => m.vb.y));
        this.yMin = Math.min(0, ...mm.map(m => m.va.y));

        // Pie de cada pata: el stub del modelo o, si no hay, el punto más bajo de la esquina.
        this.POS = {};
        for (const p of PATAS) {
            const [sx, sz] = SIGNO[p];
            const st = F.stubs.filter(s => Math.sign(s.pie[0]) === sx && Math.sign(s.pie[2]) === sz)[0];
            if (st) { this.POS[p] = { x: st.pie[0], z: st.pie[2], stub: st }; continue; }
            let best = null, sc = -Infinity;
            for (const m of mm) {
                if (m.va.y > this.yMin + .6) continue;
                const s = Math.min(sx * m.va.x, sz * m.va.z);
                if (s > sc) { sc = s; best = m.va; }
            }
            const x = best ? best.x : sx * 3.5, z = best ? best.z : sz * 3.5;
            this.POS[p] = { x, z, stub: { pie: [x, -.4, z], cab: [x * .97, .6, z * .97] } };
        }

        // Cotas por pata (m) — vienen de la página (tabla de torres o genéricas).
        this.C = {};
        for (const p of PATAS) {
            const c = (this.datos.patas || []).find(x => x.letra === p)?.cotas || this.datos.cotas || {};
            const H = c.excProf || 3.2, Lx = c.excLargo || 3, Lz = c.excAncho || 3;
            const ZL = Math.min(c.zapLado || 2.4, Math.min(Lx, Lz) * .95), ZH = c.zapAlto || .45;
            const PL = Math.min(c.pedLado || .7, ZL), PA = c.pedAlto || (H - ZH + .3);
            this.C[p] = { H, Lx, Lz, ZL, ZH, PL, PA, parrilla: !!c.parrilla, pesada: !!c.pesada, pila: !!c.pila };
            if (c.pila) Object.assign(this.C[p], { Lx: Lx, Lz: Lx, R: Lx / 2, NA: Math.max(1, Math.round(H / .5)) });   // pila: diámetro = largo
        }

        // Bloque de terreno: medio lado según el ancho de la base y las fosas.
        const maxX = Math.max(...PATAS.map(p => Math.abs(this.POS[p].x) + this.C[p].Lx / 2));
        const maxZ = Math.max(...PATAS.map(p => Math.abs(this.POS[p].z) + this.C[p].Lz / 2));
        const B = this.B = Math.max(11, Math.max(maxX, maxZ) + 6.5);
        const PROF = Math.max(5, ...PATAS.map(p => this.C[p].H + 1.5));
        const forma = new THREE.Shape([new THREE.Vector2(-B, -B), new THREE.Vector2(B, -B), new THREE.Vector2(B, B), new THREE.Vector2(-B, B)]);
        for (const p of PATAS) {
            const { x, z } = this.POS[p], { Lx, Lz, pila, R } = this.C[p], h = new THREE.Path();
            if (pila) h.absarc(x, -z, R, 0, Math.PI * 2, true);
            else { h.moveTo(x - Lx / 2, -z - Lz / 2); h.lineTo(x + Lx / 2, -z - Lz / 2); h.lineTo(x + Lx / 2, -z + Lz / 2); h.lineTo(x - Lx / 2, -z + Lz / 2); }
            forma.holes.push(h);
        }
        this.tPasto.repeat.set(B / 2.75, B / 2.75);
        const sup = new THREE.Mesh(new THREE.ShapeGeometry(forma), M.pasto);
        sup.rotation.x = -Math.PI / 2; sup.receiveShadow = true; g.add(sup);
        for (let i = 0; i < 4; i++) {
            const l = new THREE.Mesh(new THREE.PlaneGeometry(2 * B, PROF), M.lado);
            const ang = i * Math.PI / 2; l.position.set(Math.sin(ang) * B, -PROF / 2, Math.cos(ang) * B); l.rotation.y = ang; l.receiveShadow = true; g.add(l);
        }
        // Zócalo oscuro, ribete del césped y sombra de contacto: el diorama se apoya en el piso de la escena.
        const base = new THREE.Mesh(new THREE.BoxGeometry(2 * B + .5, .45, 2 * B + .5), M.base);
        base.position.y = -PROF - .225; base.castShadow = base.receiveShadow = true; g.add(base);
        for (let i = 0; i < 4; i++) {
            const r = caja(i % 2 ? .12 : 2 * B + .12, .1, i % 2 ? 2 * B + .12 : .12, M.borde);
            r.position.set(i === 1 ? B : i === 3 ? -B : 0, -.03, i === 0 ? B : i === 2 ? -B : 0); g.add(r);
        }
        const sombra = new THREE.Mesh(new THREE.PlaneGeometry(B * 3.6, B * 3.6), new THREE.MeshBasicMaterial({ map: this.tSombra, transparent: true, depthWrite: false, toneMapped: false }));
        sombra.rotation.x = -Math.PI / 2; sombra.position.y = -PROF - .449; g.add(sombra);

        // Cimentación de cada pata.
        this.CIM = {};
        for (const p of PATAS) {
            const { x, z, stub } = this.POS[p], C = this.C[p], gp = new THREE.Group(); gp.position.set(x, 0, z); g.add(gp);
            if (C.pila) { this.CIM[p] = this.armarPila(gp, C, stub, x, z); const e = etiqueta(p); e.position.set(x + Math.sign(x) * (C.R + .9), .7, z + Math.sign(z) * (C.R + .9)); g.add(e); continue; }
            const pared = new THREE.Mesh(new THREE.BoxGeometry(C.Lx, C.H, C.Lz), M.pared); pared.position.y = -C.H / 2; pared.receiveShadow = true; gp.add(pared);
            const c = { g: gp };
            c.suelo = caja(C.Lx - .01, 1, C.Lz - .01, M.suelo); gp.add(c.suelo);
            c.solado = caja(C.Lx - .1, .07, C.Lz - .1, M.solado); c.solado.position.y = -C.H + .035; gp.add(c.solado);
            const yTope = -C.H + .07 + C.ZH + C.PA;   // corona del pedestal
            if (!C.parrilla) {
                const pts = [], n = 7, Z = C.ZL, P = C.PL, H = C.H;
                for (let i = 0; i < n; i++) { const t = -Z / 2 + .1 + i * (Z - .2) / (n - 1); pts.push(t, -H + .12, -Z / 2 + .1, t, -H + .12, Z / 2 - .1, -Z / 2 + .1, -H + .16, t, Z / 2 - .1, -H + .16, t); }
                for (const [sx, sz] of [[-1, -1], [1, -1], [1, 1], [-1, 1], [0, -1], [0, 1], [-1, 0], [1, 0]])
                    pts.push(sx * (P / 2 - .06), -H + .12, sz * (P / 2 - .06), sx * (P / 2 - .06), yTope - .05, sz * (P / 2 - .06));
                for (let k = 0; k < 6; k++) { const y = -H + C.ZH + .2 + k * ((yTope - (-H + C.ZH) - .3) / 5), h = P / 2 - .05; pts.push(-h, y, -h, h, y, -h, h, y, -h, h, y, h, h, y, h, -h, y, h, -h, y, h, -h, y, -h); }
                const gg = new THREE.BufferGeometry(); gg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
                c.acero = new THREE.LineSegments(gg, M.acero); c.nAcero = pts.length / 6; gp.add(c.acero);
                c.zap = caja(C.ZL, C.ZH, C.ZL, M.conc); gp.add(c.zap);
                c.ped = caja(C.PL, 1, C.PL, M.conc); gp.add(c.ped);
                c.stub = barra([stub.pie[0] - x, Math.min(stub.pie[1], yTope - .8), stub.pie[2] - z], [stub.cab[0] - x, stub.cab[1] + .1, stub.cab[2] - z], .1, M.galv); gp.add(c.stub);
            } else {
                // Parrilla (esquemática): dos capas de vigas + montante inclinado hacia el centro de la torre.
                // Pesada: más vigas, más gruesas, una tercera capa y riostras del montante a la parrilla.
                c.parr = [];
                const Z = C.ZL, n = C.pesada ? 9 : 6, e = C.pesada ? .14 : .1;
                for (let i = 0; i < n; i++) {
                    const t = -Z / 2 + .1 + i * (Z - .2) / (n - 1);
                    const b1 = caja(Z, e, e, M.galv); b1.position.set(0, -C.H + .2, t);
                    const b2 = caja(e, e, Z, M.galv); b2.position.set(t, -C.H + .2 + e, 0); c.parr.push(b1, b2);
                    if (C.pesada && i % 2 === 0) { const b3 = caja(Z, e, e, M.galv); b3.position.set(0, -C.H + .2 + 2 * e, t); c.parr.push(b3); }
                }
                const dir = new THREE.Vector3(-x, 0, -z).normalize(), k = Math.min(.8, C.H * .17), y0 = -C.H + .2 + (C.pesada ? 3 : 2) * e;
                c.parr.push(barra([0, y0, 0], [dir.x * k, .35, dir.z * k], C.pesada ? .2 : .14, M.galv));
                if (C.pesada) for (const [sx, sz] of [[1, 1], [-1, 1], [1, -1], [-1, -1]])
                    c.parr.push(barra([sx * Z * .38, y0, sz * Z * .38], [dir.x * k * .45, y0 + (C.H + .35) * .45, dir.z * k * .45], .08, M.galv));
                c.parr.forEach(m => gp.add(m));
            }
            c.rel = caja(C.Lx - .02, 1, C.Lz - .02, [M.relleno, M.relleno, M.relTope, M.relleno, M.relleno, M.relleno]); gp.add(c.rel);
            this.CIM[p] = c;
            const e = etiqueta(p); e.position.set(x + Math.sign(x) * (C.Lx / 2 + .8), .7, z + Math.sign(z) * (C.Lz / 2 + .8)); g.add(e);
        }

        // Torre: una caja instanciada por pieza (se escala según lo montado).
        this.inst = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), M.torre, Math.max(1, mm.length));
        this.inst.castShadow = true; this.inst.receiveShadow = true; g.add(this.inst);
        const blanco = new THREE.Color(0x9aa3ad); for (let i = 0; i < mm.length; i++) this.inst.setColorAt(i, blanco);

        // Pluma de izaje (ilustrativa) y carga.
        const hP = Math.max(7, Math.min(12, this.alto * .25));
        this.pluma = new THREE.Group(); g.add(this.pluma);
        {
            const pts = [], a = .18, esq = [[-a, -a], [a, -a], [a, a], [-a, a]];
            for (const [x, z] of esq) pts.push(x, 0, z, x, hP, z);
            for (let y = 0; y < hP; y += .6) for (let i = 0; i < 4; i++) { const [x1, z1] = esq[i], [x2, z2] = esq[(i + 1) % 4]; pts.push(x1, y, z1, x2, y + .6, z2); }
            const gg = new THREE.BufferGeometry(); gg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
            this.pluma.add(new THREE.LineSegments(gg, new THREE.LineBasicMaterial({ color: 0xd99a1c })));
            this.pluma.userData.h = hP;
        }
        this.carga = new THREE.Group(); g.add(this.carga);
        [[0, 0, 0, 2.2, 0, 0], [0, 0, 0, 1.1, 1.6, 0], [2.2, 0, 0, 1.1, 1.6, 0]].forEach(([a, b, c, d, e, f]) => this.carga.add(barra([a - 1.1, b, c], [d - 1.1, e, f], .07, M.hoy)));
        this.cable = new THREE.Line(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0x2b2b2b })); g.add(this.cable);

        // Cadenas de aisladores (vestida, esquemáticas) en las puntas de los brazos.
        this.aisl = new THREE.Group(); g.add(this.aisl);
        const top = mm.filter(m => m.vb.y > this.alto * .6).map(m => m.vb);
        const puntas = [];
        for (const s of [-1, 1]) { const v = top.filter(p => Math.sign(p.x) === s).sort((p, q) => Math.abs(q.x) - Math.abs(p.x))[0]; if (v) puntas.push(v); }
        for (const p of puntas) for (let k = 0; k < 14; k++) {
            const d = new THREE.Mesh(new THREE.CylinderGeometry(.16, .16, .05, 14), M.aisl);
            d.position.set(p.x * .97, p.y - .3 - k * .2, p.z); d.castShadow = true; this.aisl.add(d);
        }

        // Ancladeros de los vientos (simulación, sin cálculo).
        this.ANCLAS = [[1, 1], [-1, 1], [-1, -1], [1, -1]].map(([sx, sz]) => new THREE.Vector3(sx * (B - 1.3), 0, sz * (B - 1.3)));
        this.anclajes = this.ANCLAS.map(p => {
            const a = new THREE.Group(); a.position.copy(p); g.add(a);
            a.add(pieza(new THREE.BoxGeometry(.6, .25, .6), M.conc, 0, .12, 0));
            const est = pieza(new THREE.CylinderGeometry(.04, .04, .9, 8), this.MQ.gris, 0, .45, 0); est.rotation.z = Math.atan2(p.x, 6) * .4; a.add(est);
            a.add(pieza(new THREE.BoxGeometry(.03, .22, .16), M.hoy, 0, .85, .08));
            a.visible = false; return a;
        });
        this.lineas = new THREE.LineSegments(new THREE.BufferGeometry(), this.MQ.viento); g.add(this.lineas);

        // Sol y sombras a la escala del diorama.
        const R = Math.max(B, this.alto * .6);
        this.sol.position.set(R * .8, R * 1.6, R * .65); this.sol.target.position.set(0, 0, 0);
        Object.assign(this.sol.shadow.camera, { left: -R * 1.1, right: R * 1.1, top: R * 1.1, bottom: -R * 1.1, near: 1, far: R * 5 });
        this.sol.shadow.camera.updateProjectionMatrix();
        this.ctl.minDistance = 4; this.ctl.maxDistance = Math.max(120, this.alto * 4);
        this.aplicarTransparencia();
    }

    // Pila excavada (APROXIMACIÓN): fuste cilíndrico revestido con anillos de concreto de 0,5 m que se funden de
    // arriba hacia abajo a medida que avanza la excavación; luego canasta de acero, concreto del fuste y stub.
    armarPila(gp, C, stub, x, z) {
        const M = this.M, R = C.R, H = C.H, c = { g: gp, anillos: [] };
        const pared = new THREE.Mesh(new THREE.CylinderGeometry(R, R, H, 40, 1, true), M.pared); pared.position.y = -H / 2; pared.receiveShadow = true; gp.add(pared);
        c.suelo = new THREE.Mesh(new THREE.CylinderGeometry(R - .01, R - .01, 1, 40), M.suelo); c.suelo.receiveShadow = true; gp.add(c.suelo);
        const e = .12, alto = H / C.NA;
        const perfil = [new THREE.Vector2(R - e, 0), new THREE.Vector2(R - .005, 0), new THREE.Vector2(R - .005, alto - .03), new THREE.Vector2(R - e, alto - .03)];
        const geoAnillo = new THREE.LatheGeometry(perfil, 40);
        for (let k = 0; k < C.NA; k++) {
            const a = new THREE.Mesh(geoAnillo, M.conc); a.position.y = -(k + 1) * alto; a.castShadow = a.receiveShadow = true; a.visible = false; gp.add(a); c.anillos.push(a);
        }
        const pts = [], rr = R - e - .08;
        for (let i = 0; i < 10; i++) { const t = i / 10 * Math.PI * 2; pts.push(Math.cos(t) * rr, -H + .1, Math.sin(t) * rr, Math.cos(t) * rr, .1, Math.sin(t) * rr); }
        for (let y = -H + .3; y < 0; y += .5) for (let i = 0; i < 16; i++) {
            const t1 = i / 16 * Math.PI * 2, t2 = (i + 1) / 16 * Math.PI * 2;
            pts.push(Math.cos(t1) * rr, y, Math.sin(t1) * rr, Math.cos(t2) * rr, y, Math.sin(t2) * rr);
        }
        const gg = new THREE.BufferGeometry(); gg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
        c.acero = new THREE.LineSegments(gg, M.acero); c.nAcero = pts.length / 6; gp.add(c.acero);
        c.fuste = new THREE.Mesh(new THREE.CylinderGeometry(R - e, R - e, 1, 40), M.conc); c.fuste.castShadow = c.fuste.receiveShadow = true; gp.add(c.fuste);
        c.dado = caja(Math.min(C.PL + .3, 1.1), .5, Math.min(C.PL + .3, 1.1), M.conc); c.dado.position.y = .25; gp.add(c.dado);
        c.stub = barra([stub.pie[0] - x, -1.2, stub.pie[2] - z], [stub.cab[0] - x, stub.cab[1] + .1, stub.cab[2] - z], .1, M.galv); gp.add(c.stub);
        return c;
    }

    // ---------- Trabajadores y equipos ----------
    crearEquipos() {
        const MQ = this.MQ, s = this.scene;
        const obrero = () => {
            const g = new THREE.Group(), cuerpo = piv(0, .85, 0); g.add(cuerpo);
            for (const x of [-1, 1]) cuerpo.add(pieza(new THREE.CapsuleGeometry(.07, .62, 4, 8), MQ.jean, x * .09, -.42, 0));
            const torso = piv(0, 0, 0); cuerpo.add(torso);
            torso.add(pieza(new THREE.CapsuleGeometry(.16, .34, 4, 10), MQ.chaleco, 0, .24, 0));
            torso.add(pieza(new THREE.SphereGeometry(.11, 12, 10), MQ.piel, 0, .62, 0));
            torso.add(pieza(new THREE.SphereGeometry(.125, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), MQ.casco, 0, .66, 0));
            const brazos = [];
            for (const x of [-1, 1]) { const h = piv(x * .21, .42, 0); h.add(pieza(new THREE.CapsuleGeometry(.05, .44, 4, 8), MQ.chaleco, 0, -.24, 0)); torso.add(h); brazos.push(h); }
            const herr = piv(0, .1, .32); torso.add(herr);
            g.userData = { cuerpo, torso, brazos, herr, fase: Math.random() * 6 };
            g.visible = false; s.add(g); return g;
        };
        const pala = () => { const h = new THREE.Group(); h.add(pieza(new THREE.CylinderGeometry(.02, .02, 1.1, 6), MQ.negro)); h.add(pieza(new THREE.BoxGeometry(.22, .02, .28), MQ.gris, 0, -.55, .08)); h.rotation.x = .5; return h; };
        this.cuadrilla = Array.from({ length: 12 }, obrero);
        this.palas = this.cuadrilla.map(o => { const p = pala(); p.visible = false; o.userData.herr.add(p); return p; });

        const retro = this.retro = new THREE.Group(); s.add(retro); retro.visible = false;
        retro.add(pieza(new THREE.BoxGeometry(2.6, .7, 1.5), MQ.amarillo, 0, .95, 0));
        retro.add(pieza(new THREE.BoxGeometry(1.1, .5, 1.3), MQ.amarillo, 1.05, 1.5, 0));
        retro.add(pieza(new THREE.BoxGeometry(1.05, 1.05, 1.25), MQ.vidrio, -.15, 1.85, 0));
        retro.add(pieza(new THREE.BoxGeometry(1.15, .08, 1.35), MQ.amarillo, -.15, 2.4, 0));
        for (const [x, r, w] of [[-.75, .62, .45], [1.0, .42, .35]]) for (const z of [-.72, .72]) { const rr = pieza(new THREE.CylinderGeometry(r, r, w, 18), MQ.negro, x, r, z); rr.rotation.x = Math.PI / 2; retro.add(rr); }
        retro.add(pieza(new THREE.BoxGeometry(.35, .6, 1.9), MQ.amarillo, 2.15, .55, 0));
        const b0 = piv(-1.35, 1.2, 0); retro.add(b0);
        b0.add(pieza(new THREE.BoxGeometry(2.2, .22, .22), MQ.amarillo, -1.1, 0, 0));
        const b1 = piv(-2.2, 0, 0); b0.add(b1);
        b1.add(pieza(new THREE.BoxGeometry(.2, 1.7, .2), MQ.amarillo, 0, -.85, 0));
        const b2 = piv(0, -1.7, 0); b1.add(b2);
        b2.add(pieza(new THREE.BoxGeometry(.5, .35, .55), MQ.gris, -.15, -.12, 0));
        retro.userData = { b0, b1, b2 }; retro.scale.setScalar(.85);

        const trompo = this.trompo = new THREE.Group(); s.add(trompo); trompo.visible = false;
        trompo.add(pieza(new THREE.BoxGeometry(1.6, .12, .9), MQ.gris, 0, .55, 0));
        for (const z of [-.5, .5]) { const r = pieza(new THREE.CylinderGeometry(.32, .32, .16, 16), MQ.negro, -.3, .32, z); r.rotation.x = Math.PI / 2; trompo.add(r); }
        trompo.add(pieza(new THREE.BoxGeometry(.12, .6, .12), MQ.gris, .7, .3, 0));
        const eje = piv(.1, 1.15, 0); eje.rotation.z = .55; trompo.add(eje);
        const tambor = new THREE.Mesh(new THREE.LatheGeometry([[0, -.55], [.38, -.5], [.55, -.15], [.55, .1], [.3, .55], [.22, .62]].map(([r, y]) => new THREE.Vector2(r, y)), 20), MQ.naranja);
        tambor.castShadow = true; eje.add(tambor); tambor.add(pieza(new THREE.BoxGeometry(.04, .5, .2), MQ.gris, .56, 0, 0));
        trompo.userData = { tambor };

        const rana = this.rana = new THREE.Group(); s.add(rana); rana.visible = false;
        rana.add(pieza(new THREE.BoxGeometry(.35, .08, .45), MQ.gris, 0, .04, 0)); rana.add(pieza(new THREE.CylinderGeometry(.09, .12, .6, 10), MQ.amarillo, 0, .38, 0)); rana.add(pieza(new THREE.BoxGeometry(.5, .05, .05), MQ.negro, 0, .78, -.15));
        const malacate = this.malacate = new THREE.Group(); s.add(malacate); malacate.visible = false;
        malacate.add(pieza(new THREE.BoxGeometry(1.2, .5, .8), MQ.naranja, 0, .25, 0));
        const t = pieza(new THREE.CylinderGeometry(.22, .22, .7, 16), MQ.gris, 0, .7, 0); t.rotation.x = Math.PI / 2; malacate.add(t);
    }

    postura(o, tipo, t) {
        const u = o.userData, k = t / 1000 + u.fase;
        u.cuerpo.position.y = .85; u.torso.rotation.set(0, 0, 0); u.brazos.forEach(b => b.rotation.set(0, 0, 0));
        if (tipo === 'pala') { const c = (Math.sin(k * 2.4) + 1) / 2; u.torso.rotation.x = .2 + .55 * c; u.brazos.forEach(b => b.rotation.x = -.6 - .9 * c); }
        else if (tipo === 'agachado') { u.cuerpo.position.y = .55; u.torso.rotation.x = .9; u.brazos.forEach((b, i) => b.rotation.x = -1.2 + Math.sin(k * 3 + i * 1.7) * .45); }
        else if (tipo === 'vaciar') { u.torso.rotation.x = .35; u.brazos.forEach(b => b.rotation.x = -1.1 + Math.sin(k * 1.5) * .15); }
        else if (tipo === 'rana') { u.cuerpo.position.y = .85 + Math.abs(Math.sin(k * 14)) * .03; u.brazos.forEach(b => b.rotation.x = -.9); }
        else if (tipo === 'trepar') { u.torso.rotation.x = -.15; u.brazos.forEach((b, i) => b.rotation.x = -2.6 + Math.sin(k * 2 + i * Math.PI) * .35); }
        else if (tipo === 'halar') { u.torso.rotation.x = -.25; u.brazos.forEach((b, i) => b.rotation.x = -1.3 + Math.sin(k * 2.2 + i) * .25); }
        else if (tipo === 'mirar') { u.torso.rotation.y = Math.sin(k * .6) * .5; u.brazos[1].rotation.x = Math.sin(k * .9) > .6 ? -2.2 : 0; }
    }

    // Punto de la pierna de la torre (esquina sx, sz) a la cota y: entre las piezas montadas que cruzan esa
    // cota, la que está más hacia la esquina (min(sx·x, sz·z)). Así el liniero queda sobre la pierna y no
    // en el aire frente a un brazo.
    puntoPierna(y, sx, sz, hasta) {
        let mejor = null, sc = -Infinity;
        for (let i = 0; i < hasta; i++) {
            const m = this.miembros[i];
            if (m.va.y > y || m.vb.y < y || m.dir.y < .85 || m.L < .8) continue;   // solo piezas casi verticales: piernas y montantes
            const f = (y - m.va.y) / (m.vb.y - m.va.y), x = m.va.x + (m.vb.x - m.va.x) * f, z = m.va.z + (m.vb.z - m.va.z) * f;
            const s = Math.min(sx * x, sz * z);
            if (s > sc) { sc = s; mejor = new THREE.Vector3(x, y, z); }
        }
        return sc > 0 ? mejor : null;
    }

    equipos(d, t, alto, nMont, plumaActiva) {
        const obj = this.obj, prev = this.prev;
        const v = (c, p, k) => Math.max(0, c?.patas?.[p]?.[k] ?? 0);
        const trabajo = (p, k) => { const a = obj?.patas?.[p]?.[k] ?? 0, b = prev?.patas?.[p]?.[k] ?? 0; return a > 0 && a > b + 1e-4; };
        let i = 0;
        const usar = (tipo, x, y, z, rotY, conPala) => {
            const o = this.cuadrilla[i]; if (!o) return;
            o.visible = true; o.position.set(x, y, z); o.rotation.y = rotY; this.palas[i].visible = !!conPala; this.postura(o, tipo, t); i++;
        };
        this.retro.visible = this.trompo.visible = this.rana.visible = this.malacate.visible = false;
        if (prev) for (const p of PATAS) {
            const { x, z } = this.POS[p], C = this.C[p], r = semi(x, z), mira = Math.atan2(-r.x, -r.z), lado = new THREE.Vector3(-r.z, 0, r.x);
            const borde = Math.max(C.Lx, C.Lz) / 2 * Math.SQRT2;   // distancia al borde de la fosa en la diagonal
            const fondo = -C.H + (v(d, p, 'sol') > 0 ? .07 : 0);
            const dentro = Math.min(.7, Math.min(C.Lx, C.Lz) / 2 - .35);
            if (trabajo(p, 'exc') && C.pila) {
                // Pila: excavación manual dentro del anillo y un ayudante en el borde.
                usar('pala', x, -C.H * v(d, p, 'exc'), z, mira, true);
                usar('mirar', x + r.x * (C.R + .6), 0, z + r.z * (C.R + .6), mira, false);
            } else if (trabajo(p, 'exc')) {
                if (!this.retro.visible) {
                    this.retro.visible = true; this.retro.position.set(x + r.x * (borde + 2.4), 0, z + r.z * (borde + 2.4)); this.retro.rotation.y = Math.atan2(r.z, -r.x) + Math.PI;
                    const c = (Math.sin(t / 900) + 1) / 2, u = this.retro.userData;
                    u.b0.rotation.z = -.35 + .45 * c; u.b1.rotation.z = .5 - .9 * c; u.b2.rotation.z = -.6 + 1.2 * c;
                }
                usar('pala', x + lado.x * dentro, -C.H * v(d, p, 'exc'), z + lado.z * dentro, mira + .4, true);
            } else if (trabajo(p, 'con')) {
                this.trompo.visible = true; this.trompo.position.set(x + r.x * (borde + 1.4) + lado.x * 1.6, 0, z + r.z * (borde + 1.4) + lado.z * 1.6); this.trompo.rotation.y = mira;
                this.trompo.userData.tambor.rotation.y = t / 260;
                usar('vaciar', x + r.x * (borde + .4), 0, z + r.z * (borde + .4), mira, false);
                usar('agachado', x - lado.x * dentro, -C.H + .07 + C.ZH * Math.min(1, v(d, p, 'con') / .55), z - lado.z * dentro, mira, false);
            } else if (trabajo(p, 'rel')) {
                const yr = -C.H + C.H * v(d, p, 'rel');
                this.rana.visible = true; this.rana.position.set(x + lado.x * dentro, yr + Math.abs(Math.sin(t / 70)) * .04, z + lado.z * dentro);
                usar('rana', x + lado.x * dentro + r.x * .45, yr, z + lado.z * dentro + r.z * .45, mira + Math.PI, false);
                usar('pala', x + r.x * (borde + .6), 0, z + r.z * (borde + .6), mira, true);
            } else if (trabajo(p, 'ace') || trabajo(p, 'niv') || trabajo(p, 'sol')) {
                usar('agachado', x + lado.x * dentro, fondo, z + lado.z * dentro, mira, false);
                usar('agachado', x - lado.x * dentro, fondo, z - lado.z * dentro, mira + Math.PI, false);
            }
        }
        const pts = [];
        this.anclajes.forEach(a => a.visible = !!plumaActiva);
        if (plumaActiva) {
            const pl = this.pluma, tip = new THREE.Vector3(0, pl.userData.h, 0).applyEuler(pl.rotation).add(pl.position);
            for (const a of this.ANCLAS) pts.push(tip, a.clone().setY(.8));
            const w = this.POS.A, rA = semi(w.x, w.z), dA = Math.max(this.C.A.Lx, this.C.A.Lz) / 2 * Math.SQRT2 + 2.2;
            this.malacate.visible = true; this.malacate.position.set(w.x + rA.x * dA, 0, w.z + rA.z * dA); this.malacate.rotation.y = Math.atan2(rA.x, rA.z);
            pts.push(tip, pl.position.clone(), pl.position.clone(), new THREE.Vector3(0, .3, 0), new THREE.Vector3(0, .3, 0), this.malacate.position.clone().setY(.7));
            usar('halar', this.malacate.position.x + rA.x * .9, 0, this.malacate.position.z + rA.z * .9, Math.atan2(-rA.x, -rA.z), false);
            const ret = this.POS.C, rc = semi(ret.x, ret.z), dC = Math.max(this.C.C.Lx, this.C.C.Lz) / 2 * Math.SQRT2 + 1.2;
            const pie = new THREE.Vector3(ret.x + rc.x * dC, 1, ret.z + rc.z * dC);
            pts.push(this.carga.position.clone(), pie);
            usar('halar', pie.x, 0, pie.z, Math.atan2(-rc.x, -rc.z), false);
            usar('mirar', -1.2, 0, Math.min(this.B - 1, Math.abs(this.POS.A.z) + this.C.A.Lz + 2.5), Math.PI, false);
            // Linieros apoyados en las piernas montadas (pies sobre la pierna, manos por debajo del borde montado).
            // Si a esa cota no hay pierna (cabeza o crucetas), se baja hasta encontrar una.
            for (const [sx, sz] of [[1, 1], [-1, -1]]) {
                let q = null;
                for (let yPie = alto - 2.1; yPie > .6 && !q; yPie -= .5) q = this.puntoPierna(yPie, sx, sz, Math.max(0, nMont - 1));
                if (!q) continue;
                const o = semi(sx, sz);
                usar('trepar', q.x + o.x * .3, q.y, q.z + o.z * .3, Math.atan2(-sx, -sz), false);
            }
        }
        this.lineas.geometry.dispose(); this.lineas.geometry = new THREE.BufferGeometry().setFromPoints(pts); this.lineas.visible = pts.length > 0;
        for (; i < this.cuadrilla.length; i++) this.cuadrilla[i].visible = false;
    }

    // ---------- Estado ----------
    // cuadro = { patas: { A: { exc, sol, niv, ace, con, rel } }, torre: { pre, mon, ver, ves } } (0–1; -1 = no aplica).
    actualizar(cuadro, previo) {
        const limpio = c => {
            const r = { patas: {}, torre: {} };
            for (const p of PATAS) { r.patas[p] = {}; for (const k of CLAVES_PATA) r.patas[p][k] = Math.max(0, c?.patas?.[p]?.[k] ?? 0); }
            for (const k of CLAVES_TORRE) r.torre[k] = Math.max(0, c?.torre?.[k] ?? 0);
            return r;
        };
        this.obj = limpio(cuadro);
        this.prev = previo ? limpio(previo) : null;
        if (!this.vis || this.reduce) this.vis = JSON.parse(JSON.stringify(this.obj));
    }

    pintarTorre(p, pHoy) {
        const meta = p * this.peso, desde = pHoy * this.peso, inst = this.inst;
        const _q = new THREE.Quaternion(), _p = new THREE.Vector3(), _s = new THREE.Vector3(), _m4 = new THREE.Matrix4(), Y = new THREE.Vector3(0, 1, 0);
        const gris = new THREE.Color(0x9aa3ad), rojo = new THREE.Color(0xec1c24);
        let alto = 0, n = 0;
        this.miembros.forEach((m, i) => {
            const pr = m.cum - m.w, f = meta >= m.cum ? 1 : meta <= pr ? 0 : (meta - pr) / m.w;
            if (f > 0) { alto = Math.max(alto, m.va.y + m.dir.y * m.L * f); n = i + 1; }
            const L = Math.max(1e-4, m.L * f);
            _p.copy(m.va).addScaledVector(m.dir, L / 2); _q.setFromUnitVectors(Y, m.dir); _s.set(f ? m.r : 0, L, f ? m.r : 0);
            inst.setMatrixAt(i, _m4.compose(_p, _q, _s));
            inst.setColorAt(i, m.cum > desde && f > 0 && p < 1 ? rojo : gris);
        });
        inst.instanceMatrix.needsUpdate = true; if (inst.instanceColor) inst.instanceColor.needsUpdate = true;
        return { alto, n };
    }

    aplicar(t) {
        const d = this.vis;
        for (const p of PATAS) {
            const c = this.CIM[p], C = this.C[p], v = k => d.patas[p][k];
            const exc = v('exc'), rel = v('rel');
            if (C.pila) {
                // Un anillo por cada 0,5 m excavado (la excavación del día se reparte en partes iguales).
                const hx = C.H * Math.max(0, exc), na = Math.min(C.NA, Math.floor(exc * C.NA + 1e-3));
                c.suelo.scale.y = Math.max(.001, C.H - hx); c.suelo.position.y = -C.H + c.suelo.scale.y / 2; c.suelo.visible = exc < .999;
                c.anillos.forEach((a, i) => a.visible = i < na);
                c.acero.visible = v('ace') > 0; c.acero.geometry.setDrawRange(0, Math.ceil(c.nAcero * Math.max(0, v('ace'))) * 2);
                const cc = Math.max(0, v('con')), fz = Math.min(1, cc / .9), dz = Math.max(0, (cc - .9) / .1);
                c.fuste.visible = fz > 0; c.fuste.scale.y = Math.max(.001, C.H * fz); c.fuste.position.y = -C.H + C.H * fz / 2;
                c.dado.visible = dz > 0; c.dado.scale.y = Math.max(.001, dz); c.dado.position.y = .25 * dz;
                c.stub.visible = v('niv') > 0 || cc > 0;
                continue;
            }
            c.suelo.scale.y = Math.max(.001, C.H * (1 - exc)); c.suelo.position.y = -C.H + c.suelo.scale.y / 2; c.suelo.visible = exc < .999;
            c.solado.visible = v('sol') > 0; c.solado.scale.set(Math.max(.01, v('sol')), 1, 1);
            if (!C.parrilla) {
                c.stub.visible = v('niv') > 0;
                c.acero.visible = v('ace') > 0; c.acero.geometry.setDrawRange(0, Math.ceil(c.nAcero * v('ace')) * 2);
                const cc = v('con'), z1 = Math.min(1, cc / .55), z2 = Math.max(0, (cc - .55) / .45);
                c.zap.visible = z1 > 0; c.zap.scale.y = Math.max(.001, z1); c.zap.position.y = -C.H + .07 + C.ZH * z1 / 2;
                c.ped.visible = z2 > 0; c.ped.scale.y = Math.max(.001, C.PA * z2); c.ped.position.y = -C.H + .07 + C.ZH + C.PA * z2 / 2;
            } else {
                const n = Math.round(c.parr.length * v('niv')); c.parr.forEach((m, i) => m.visible = i < n);
            }
            const hr = C.H * rel; c.rel.visible = rel > 0; c.rel.scale.y = Math.max(.001, hr); c.rel.position.y = -C.H + hr / 2;
        }
        // Montaje: el prearmado no levanta piezas; lo del día (sobre el cuadro anterior) va en rojo.
        const pm = d.torre.mon, pHoy = this.prev ? this.prev.torre.mon : pm;
        const { alto, n } = this.pintarTorre(pm, Math.min(pm, pHoy));
        // Pluma, vientos y ancladeros: solo mientras el montaje está en curso según el registro (no según la animación,
        // que nunca llega exacto a 0 ni a 1).
        const pmReg = this.obj?.torre?.mon ?? pm, activa = pmReg > 0.001 && pmReg < 0.999 && pm > 0.001;
        this.pluma.visible = this.carga.visible = this.cable.visible = activa;
        if (activa) {
            const pl = this.pluma, base = Math.max(0, alto - pl.userData.h * .5);
            pl.position.set(.6, base, .4); pl.rotation.set(.1, 0, -.12);
            const tip = new THREE.Vector3(0, pl.userData.h, 0).applyEuler(pl.rotation).add(pl.position);
            const ciclo = (t / 5200) % 1, yc = ease(.3, tip.y - 1.8, ciclo < .8 ? ciclo / .8 : 1 - (ciclo - .8) / .2);
            this.carga.position.set(tip.x + 2.6, yc, tip.z + .4); this.carga.rotation.y = Math.sin(t / 1400) * .25;
            this.cable.geometry.dispose();
            this.cable.geometry = new THREE.BufferGeometry().setFromPoints([tip, this.carga.position.clone().add(new THREE.Vector3(0, 1.6, 0))]);
        }
        const ves = d.torre.ves;
        this.aisl.visible = ves > 0;
        this.aisl.children.forEach((m, i) => m.visible = i < this.aisl.children.length * ves);
        this.equipos(d, t, alto, n, activa ? this.pluma : null);
    }

    bucle(t) {
        this._raf = requestAnimationFrame(this._bucle);
        if (this.grupo && this.obj && this.vis) {
            const dt = this._t0 ? Math.min(1000, t - this._t0) : 16;
            const k = this.reduce ? 1 : 1 - Math.exp(-dt / 220);   // ~1 s para llegar, sin depender de los fps
            for (const p of PATAS) for (const c of CLAVES_PATA) this.vis.patas[p][c] = ease(this.vis.patas[p][c], this.obj.patas[p][c], k);
            for (const c of CLAVES_TORRE) this.vis.torre[c] = ease(this.vis.torre[c], this.obj.torre[c], k);
            this.aplicar(t);
        }
        this._t0 = t;
        this.ctl.update();
        this.renderer.render(this.scene, this.cam);
    }

    // ---------- Controles ----------
    aplicarTransparencia() {
        for (const m of this.TIERRA) { m.opacity = this.transp ? (m === this.M.pasto ? .3 : .2) : 1; m.depthWrite = !this.transp; m.needsUpdate = true; }
    }
    transparente(v) { this.transp = !!v; this.aplicarTransparencia(); }
    girar(v) { this.ctl.autoRotate = !!v; }
    // Botón izquierdo: 'girar' (orbita; derecho = mover) o 'mano' (mueve; derecho = orbita).
    modo(m) {
        const mano = m === 'mano';
        this.ctl.mouseButtons = { LEFT: mano ? THREE.MOUSE.PAN : THREE.MOUSE.ROTATE, MIDDLE: THREE.MOUSE.DOLLY, RIGHT: mano ? THREE.MOUSE.ROTATE : THREE.MOUSE.PAN };
        this.ctl.touches = { ONE: mano ? THREE.TOUCH.PAN : THREE.TOUCH.ROTATE, TWO: THREE.TOUCH.DOLLY_PAN };
        this.renderer.domElement.style.cursor = mano ? 'grab' : '';
    }
    recentrar() { this.vista(this._vista || 'torre'); }
    vista(nombre) {
        this._vista = nombre;
        const A = this.alto || 30, B = this.B || 11;
        if (nombre === 'cimentacion') { this.cam.position.set(B * 1.05, B * .75, B * 1.2); this.ctl.target.set(0, -1.5, 0); }
        else { const r = Math.max(A * 1.65, B * 3); this.cam.position.set(r * .85, A * .55 + 4, r); this.ctl.target.set(0, A * .45, 0); }
        this.ctl.update();
    }
    info() { return { nombre: this.fuente?.nombre || '', plano: this.fuente?.plano || '', pesoKg: Math.round(this.peso || 0), alto: Math.round((this.alto || 0) * 10) / 10 }; }

    liberar() {
        cancelAnimationFrame(this._raf);
        this.cont.removeEventListener('wheel', this._rueda);
        this._ro.disconnect(); this._mo.disconnect(); this._mq.removeEventListener('change', this._fondo);
        this.limpiar();
        this.scene.traverse(o => { if (o.geometry) o.geometry.dispose(); });
        for (const m of [...Object.values(this.M), ...Object.values(this.MQ)]) m.dispose();
        this.tEstr.dispose(); this.tRel.dispose(); this.tPasto.dispose(); this.tSombra.dispose();
        this.scene.environment?.dispose(); this.scene.background?.dispose?.();
        this.ctl.dispose(); this.renderer.dispose(); this.renderer.domElement.remove();
    }
}
