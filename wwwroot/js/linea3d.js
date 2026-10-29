// Visor 3D de la línea completa (Three.js). Módulo ES.
// Cada torre se coloca en su coordenada real (metros locales) y se orienta según
// el vano (dirección anterior → siguiente). La torre con modelo real (JSON de
// Modelos 3D) se instancia con sus ángulos L (ver perfilesL.js); las demás se generan de forma APROXIMADA
// según su tipo y número de cuerpo (no son modelos de ingeniería).
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { geometriaL } from './perfilesL.js';

const K = 0.001;
const AMARILLO = new THREE.Color(0xffd400);

// Dimensiones aproximadas por familia: altura base h0 (cuerpo 1), ancho de base wb0 y su
// incremento por cuerpo, ancho de cabeza wt, largo de cruceta y alto de cabeza.
const FAM = {
    A:   { h0: 28, dwb: .55, wb0: 4.5, wt: 1.6, arm: 9,    head: 8 },
    AA:  { h0: 30, dwb: .60, wb0: 5.5, wt: 1.8, arm: 10,   head: 9 },
    AAE: { h0: 34, dwb: .70, wb0: 7.0, wt: 2.4, arm: 12,   head: 10 },
    AP:  { h0: 32, dwb: .60, wb0: 6.0, wt: 2.0, arm: 11,   head: 9 },
    BP:  { h0: 36, dwb: .70, wb0: 8.0, wt: 2.6, arm: 12.5, head: 10 },
    C:   { h0: 38, dwb: .80, wb0: 9.0, wt: 3.0, arm: 14,   head: 11 },
    D:   { h0: 40, dwb: .90, wb0: 10,  wt: 3.4, arm: 15,   head: 12 },
};

function parametros(fam, n) {
    n = Math.max(1, n || 1);
    if (fam === 'BE') {
        const H = n >= 50 ? n : 55 + 5 * n;
        return { H, wb: Math.max(12, H * .22), wt: 6, arm: 15, head: 14 };
    }
    const f = FAM[fam] || FAM.AA;
    return { H: f.h0 + (n - 1) * 3, wb: f.wb0 + (n - 1) * f.dwb, wt: f.wt, arm: f.arm, head: f.head };
}

export function miembrosParametricos(fam, n) {
    const p = parametros(fam, n), M = [];
    const add = (a, b, t) => M.push([a, b, t]);
    const H = p.H, N = Math.max(3, Math.round(H / 3.4));
    const anillo = (y, hw) => [[-hw, y, -hw], [hw, y, -hw], [hw, y, hw], [-hw, y, hw]];
    const tramo = (prev, c, tl, td) => {
        for (let i = 0; i < 4; i++) { const j = (i + 1) % 4; add(prev[i], c[i], tl); add(prev[i], c[j], td); add(prev[j], c[i], td); }
    };
    let prev = null;
    for (let k = 0; k <= N; k++) {
        const y = H * k / N, hw = (p.wb + (p.wt - p.wb) * (y / H)) / 2, c = anillo(y, hw);
        for (let i = 0; i < 4; i++) add(c[i], c[(i + 1) % 4], .12);
        if (prev) tramo(prev, c, .26, .1);
        prev = c;
    }
    const hw = p.wt / 2, ys = [.3, .65, 1].map(l => H + p.head * l);
    ys.forEach(y => {
        const c = anillo(y, hw);
        for (let i = 0; i < 4; i++) add(c[i], c[(i + 1) % 4], .12);
        tramo(prev, c, .26, .1); prev = c;
    });
    ys.forEach((y, j) => {
        const L = p.arm * [1, .88, .76][j];
        [-1, 1].forEach(s => [-hw, hw].forEach(z => {
            const r = s * hw, tip = s * L;
            add([r, y + 1.4, z], [tip, y + .2, z], .12);
            add([r, y - .6, z], [tip, y - .2, z], .12);
            add([tip, y - .2, z], [tip, y + .2, z], .1);
            const est = 4;
            for (let q = 0; q < est; q++) {
                const x0 = r + (tip - r) * q / est, x1 = r + (tip - r) * (q + 1) / est;
                const yt = (x) => y + 1.4 + ((x - r) / (tip - r)) * (.2 - 1.4), yb = (x) => y - .6 + ((x - r) / (tip - r)) * (.4);
                add([x0, yb(x0), z], [x1, yt(x1), z], .08);
                add([x1, yt(x1), z], [x1, yb(x1), z], .08);
            }
        }));
        [-1, 1].forEach(s => add([s * p.arm * [1, .88, .76][j], y, -hw], [s * p.arm * [1, .88, .76][j], y, hw], .1));
    });
    const apice = [0, ys[2] + p.wt * 1.4 + 2, 0], top = anillo(ys[2], hw);
    top.forEach(c => add(c, apice, .12));
    return { M, alto: apice[1], ancho: p.wb };
}

function geometria(miembros) {
    const n = miembros.length, pos = new Float32Array(n * 8 * 3), idx = new Uint32Array(n * 24);
    const a = new THREE.Vector3(), b = new THREE.Vector3(), d = new THREE.Vector3(), u = new THREE.Vector3(), w = new THREE.Vector3();
    const s4 = [[1, 1], [-1, 1], [-1, -1], [1, -1]];
    miembros.forEach(([pa, pb, tk], i) => {
        a.set(...pa); b.set(...pb);
        d.subVectors(b, a); if (d.lengthSq() < 1e-9) return; d.normalize();
        const ref = Math.abs(d.y) > .95 ? new THREE.Vector3(1, 0, 0) : new THREE.Vector3(0, 1, 0);
        u.crossVectors(d, ref).normalize(); w.crossVectors(d, u).normalize();
        const t = tk / 2;
        [a, b].forEach((p, e) => s4.forEach(([su, sw], k) => {
            const o = (i * 8 + e * 4 + k) * 3;
            pos[o] = p.x + (u.x * su + w.x * sw) * t; pos[o + 1] = p.y + (u.y * su + w.y * sw) * t; pos[o + 2] = p.z + (u.z * su + w.z * sw) * t;
        }));
        const base = i * 8;
        for (let k = 0; k < 4; k++) { const k2 = (k + 1) % 4; idx.set([base + k, base + k2, base + 4 + k, base + k2, base + 4 + k2, base + 4 + k], i * 24 + k * 6); }
    });
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setIndex(new THREE.BufferAttribute(idx, 1));
    g.computeVertexNormals();
    return g;
}

// Altura de la malla (triángulos exactos, igual que se dibuja) en una coordenada local.
function alturaMalla(alt, sx, sz, xa, xb, za, zb, x, z) {
    const gx = Math.min(sx - 1e-6, Math.max(0, (x - xa) / (xb - xa) * sx)), gz = Math.min(sz - 1e-6, Math.max(0, (z - za) / (zb - za) * sz));
    const i = Math.floor(gx), j = Math.floor(gz), fx = gx - i, fz = gz - j, w = sx + 1;
    const a = alt[j * w + i], b = alt[j * w + i + 1], c = alt[(j + 1) * w + i], d = alt[(j + 1) * w + i + 1];
    return fx + fz <= 1 ? a + (b - a) * fx + (c - a) * fz : d + (c - d) * (1 - fx) + (b - d) * (1 - fz);
}

export async function crear(contenedor, torres, urls, origen, dotnet) {
    const v = new Visor(contenedor, dotnet);
    v.origen = origen;
    await v.cargar(torres, urls);
    return v;
}

class Visor {
    constructor(el, dotnet) {
        this.el = el; this.dotnet = dotnet; this.activo = true;
        this.escala = 10; this.modo = 'tipo'; this.sel = -1; this.relieve = 1; this.aspectoTerreno = 'satelite'; this.colorFijo = new THREE.Color(0xb7c0cc);
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
        el.appendChild(this.renderer.domElement);
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x14181f);
        this.camera = new THREE.PerspectiveCamera(40, 1, 2, 2000000);
        this.controls = new OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.enableZoom = false;    // el zoom lo maneja este visor (hacia el cursor, sin topes)
        this.controls.maxDistance = Infinity; this.controls.minDistance = 0;
        this.scene.add(new THREE.HemisphereLight(0xffffff, 0x334455, 1.5));
        const dl = new THREE.DirectionalLight(0xffffff, 1.5); dl.position.set(300, 800, 400); this.scene.add(dl);
        this.ray = new THREE.Raycaster(); this.ptr = new THREE.Vector2();
        this.crearBarra();
        document.addEventListener('fullscreenchange', this._fs = () => this.ajustar());
        this._ro = new ResizeObserver(() => this.ajustar()); this._ro.observe(el);
        const cv = this.renderer.domElement;
        cv.addEventListener('wheel', e => { e.preventDefault(); this.zoom(e); }, { passive: false });
        // Zoom táctil: pellizco con dos dedos, hacia el punto medio entre ellos
        const dedos = new Map(); let dPrev = 0;
        const dist2 = () => { const [a, b] = [...dedos.values()]; return Math.hypot(a.x - b.x, a.y - b.y); };
        cv.addEventListener('pointerdown', e => { if (e.pointerType !== 'touch') return; dedos.set(e.pointerId, { x: e.clientX, y: e.clientY }); dPrev = dedos.size === 2 ? dist2() : 0; });
        cv.addEventListener('pointermove', e => {
            if (e.pointerType !== 'touch' || !dedos.has(e.pointerId)) return;
            dedos.set(e.pointerId, { x: e.clientX, y: e.clientY });
            if (dedos.size !== 2) return;
            const [a, b] = [...dedos.values()], d = dist2();
            if (dPrev > 0 && Math.abs(d - dPrev) > 1) this.zoomEn((a.x + b.x) / 2, (a.y + b.y) / 2, d > dPrev, Math.min(Math.abs(d - dPrev) / 40, 3));
            dPrev = d;
        });
        const soltar = e => { dedos.delete(e.pointerId); dPrev = 0; };
        cv.addEventListener('pointerup', soltar); cv.addEventListener('pointercancel', soltar);
        cv.addEventListener('pointerdown', e => { this._d0 = [e.clientX, e.clientY]; });
        cv.addEventListener('pointerup', e => {
            if (!this._d0 || Math.hypot(e.clientX - this._d0[0], e.clientY - this._d0[1]) > 4) return;
            this.seleccionar(this.pick(e), true);
        });
        const loop = () => { if (!this.activo) return; this.controls.update(); this.sobreTerreno(); this.renderer.render(this.scene, this.camera); requestAnimationFrame(loop); };
        loop();
    }

    async cargar(torres, urls) {
        this.torres = torres;
        // modelos desde planos (JSON de miembros, mm): uno por clave (BE, BP1..BP6)
        // Blazor serializa las claves en camelCase (BP6 -> bp6): se comparan en minúsculas.
        const urlsMin = Object.fromEntries(Object.entries(urls || {}).map(([k, v]) => [k.toLowerCase(), v]));
        const reales = {};
        const claves = [...new Set(torres.filter(t => t.real && t.modelo).map(t => t.modelo))];
        await Promise.all(claves.map(async k => {
            if (!urlsMin[k.toLowerCase()]) return;
            try {
                const data = await (await fetch(urlsMin[k.toLowerCase()])).json();
                const M = data.miembros.map(m => [[m.a[0] * K, m.a[1] * K, m.a[2] * K], [m.b[0] * K, m.b[1] * K, m.b[2] * K], m.tipo === 'Montante' ? .22 : .12]);
                reales[k] = { M, data, alto: data.meta.alto_total_mm * K, ancho: data.meta.ancho_base_mm * K };
            } catch { }
        }));
        // posición y orientación (según vano)
        const n = torres.length;
        torres.forEach((t, i) => {
            const p = torres[Math.max(0, i - 1)], q = torres[Math.min(n - 1, i + 1)];
            let dx = q.x - p.x, dz = q.z - p.z; if (Math.hypot(dx, dz) < 1e-6) { dx = 0; dz = 1; }
            t.rot = Math.atan2(dx, dz);
        });
        // modelos (uno por tipo+cuerpo)
        const grupos = new Map();
        torres.forEach((t, i) => {
            const usaReal = t.real && reales[t.modelo];
            const clave = usaReal ? 'real|' + t.modelo : `${t.familia}|${t.cuerpo}`;
            t.modelo = usaReal ? 'real' : 'aprox';
            if (!grupos.has(clave)) grupos.set(clave, { clave, usaReal, real: usaReal ? reales[clave.slice(5)] : null, familia: t.familia, cuerpo: t.cuerpo, idx: [] });
            grupos.get(clave).idx.push(i);
        });
        const mat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: .5, metalness: .35, side: THREE.DoubleSide });
        // Volumen de selección: invisible (solo sirve para elegir la torre con el clic).
        const halo = new THREE.MeshBasicMaterial({ visible: false });
        this.mallas = [];
        const hg = new THREE.CylinderGeometry(1, 1, 1, 10, 1, true); hg.translate(0, .5, 0);
        this.halo = new THREE.InstancedMesh(hg, halo, n); this.halo.frustumCulled = false;
        this.halo.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(n * 3), 3);
        this.scene.add(this.halo);
        for (const g of grupos.values()) {
            const d = g.usaReal ? g.real : miembrosParametricos(g.familia, g.cuerpo);
            // Torres con modelo desde planos: ángulos L reales (misma regla que los IFC); las aproximadas, barras.
            const geo = g.usaReal ? geometriaL(g.real.data) : geometria(d.M);
            const mesh = new THREE.InstancedMesh(geo, mat, g.idx.length);
            mesh.frustumCulled = false;
            mesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(g.idx.length * 3), 3);
            g.idx.forEach(i => { torres[i].alto = d.alto; torres[i].ancho = d.ancho; });
            this.mallas.push({ mesh, idx: g.idx });
            this.scene.add(mesh);
        }
        // ruta, suelo
        const box = new THREE.Box3();
        torres.forEach(t => box.expandByPoint(new THREE.Vector3(t.x, 0, t.z)));
        const c = box.getCenter(new THREE.Vector3()), sz = box.getSize(new THREE.Vector3());
        this.ext = Math.max(sz.x, sz.z, 500); this.centro = c;
        this.ruta = new THREE.Line(new THREE.BufferGeometry().setFromPoints(torres.map(t => new THREE.Vector3(t.x, .5, t.z))),
            new THREE.LineBasicMaterial({ color: 0xec1c24 }));
        this.scene.add(this.ruta);
        const suelo = new THREE.Mesh(new THREE.PlaneGeometry(this.ext * 3, this.ext * 3), new THREE.MeshBasicMaterial({ color: 0x1c222c }));
        suelo.rotation.x = -Math.PI / 2; suelo.position.set(c.x, -.3, c.z); this.scene.add(suelo); this.suelo = suelo;
        const grid = new THREE.GridHelper(this.ext * 3, 60, 0x445566, 0x2a3340); grid.position.set(c.x, -.2, c.z); this.scene.add(grid); this.grid = grid;
        this.actualizar();
        this.vista('todo');
        this.ajustar();
        this.terreno().catch(e => this.avisarTerreno(false, 'Terreno: ' + (e && e.message ? e.message : e)));
    }

    // Altura del terreno (con el relieve actual) en una coordenada local.
    alturaTerreno(x, z) {
        if (this.aspectoTerreno === 'plano') return 0;
        if (this.aspectoTerreno === 'vacio') return -1e9;
        const T = this.terr; if (!T) return 0;
        return (alturaMalla(T.alt, T.sx, T.sz, T.xa, T.xb, T.za, T.zb, x, z) - T.emin) * this.relieve;
    }

    // Zoom hacia el punto del terreno que está bajo el cursor: mueve cámara y centro de giro a lo largo
    // del rayo, así no existe un tope por distancia al centro de la línea.
    zoom(e) { this.zoomEn(e.clientX, e.clientY, e.deltaY < 0, Math.min(Math.abs(e.deltaY) / 100, 3)); }

    zoomEn(cx, cy, acercar, paso) {
        const cv = this.renderer.domElement, r = cv.getBoundingClientRect();
        this.ptr.set(((cx - r.left) / r.width) * 2 - 1, -((cy - r.top) / r.height) * 2 + 1);
        this.ray.setFromCamera(this.ptr, this.camera);
        const o = this.ray.ray.origin, d = this.ray.ray.direction;
        let dist = null;
        if (this.terr) {
            for (let t = 5; t < 600000; t += Math.max(5, t * .02))
                if (o.y + d.y * t < this.alturaTerreno(o.x + d.x * t, o.z + d.z * t)) { dist = t; break; }
        } else if (d.y < 0) dist = -o.y / d.y;
        if (dist === null) dist = this.camera.position.distanceTo(this.controls.target);
        let mov = acercar ? Math.max(0, Math.min(dist * .2 * paso, dist - 4)) : -dist * .25 * paso;
        this.camera.position.addScaledVector(d, mov);
        this.controls.target.addScaledVector(d, mov);
    }

    // Mantiene cámara y punto de mira por encima del suelo, para no "meterse" bajo el terreno.
    sobreTerreno() {
        if (!this.terr) return;
        const c = this.camera.position;
        const hc = this.alturaTerreno(c.x, c.z) + 3;
        if (c.y < hc) c.y = hc;
    }

    avisarTerreno(ok, msg) { if (this.dotnet) this.dotnet.invokeMethodAsync('AlTerreno', ok, msg); }

    // ---------- Terreno real: elevaciones (Terrarium/AWS) + imagen satelital (Esri) ----------
    async terreno() {
        const T = this.torres, o = this.origen, kLon = 111320 * Math.cos(o.lat0 * Math.PI / 180), kLat = 110574;
        const lonX = (lon, z) => (lon + 180) / 360 * 2 ** z;
        const latY = (lat, z) => (1 - Math.asinh(Math.tan(lat * Math.PI / 180)) / Math.PI) / 2 * 2 ** z;
        const lats = T.map(t => t.lat), lons = T.map(t => t.lon);
        let mLat = Math.max((Math.max(...lats) - Math.min(...lats)) * .12, .015), mLon = Math.max((Math.max(...lons) - Math.min(...lons)) * .12, .015);
        // Margen elegido por el usuario (km alrededor de la línea); null = automático (12 % de la extensión).
        if (this.margenKm > 0) { mLat = this.margenKm * 1000 / kLat; mLon = this.margenKm * 1000 / kLon; }
        const la0 = Math.min(...lats) - mLat, la1 = Math.max(...lats) + mLat, lo0 = Math.min(...lons) - mLon, lo1 = Math.max(...lons) + mLon;
        let z = 13, x0, x1, y0, y1;
        for (; z >= 7; z--) {
            x0 = Math.floor(lonX(lo0, z)); x1 = Math.floor(lonX(lo1, z)); y0 = Math.floor(latY(la1, z)); y1 = Math.floor(latY(la0, z));
            if ((x1 - x0 + 1) <= 10 && (y1 - y0 + 1) <= 10) break;
        }
        const nx = x1 - x0 + 1, ny = y1 - y0 + 1, W = nx * 256, H = ny * 256;
        const cargar = url => new Promise((res, rej) => { const i = new Image(); i.crossOrigin = 'anonymous'; i.onload = () => res(i); i.onerror = () => rej(new Error('no se pudo cargar ' + url)); i.src = url; });

        // 1) elevaciones
        const cH = document.createElement('canvas'); cH.width = W; cH.height = H;
        const gH = cH.getContext('2d', { willReadFrequently: true });
        const jobs = [];
        for (let i = 0; i < nx; i++) for (let j = 0; j < ny; j++)
            jobs.push(cargar(`https://s3.amazonaws.com/elevation-tiles-prod/terrarium/${z}/${x0 + i}/${y0 + j}.png`).then(im => gH.drawImage(im, i * 256, j * 256)));
        await Promise.all(jobs);
        const px = gH.getImageData(0, 0, W, H).data, elev = new Float32Array(W * H);
        for (let k = 0; k < W * H; k++) elev[k] = px[k * 4] * 256 + px[k * 4 + 1] + px[k * 4 + 2] / 256 - 32768;
        const muestra = (lat, lon) => {
            const fx = Math.min(W - 1.001, Math.max(0, (lonX(lon, z) - x0) * 256)), fy = Math.min(H - 1.001, Math.max(0, (latY(lat, z) - y0) * 256));
            const ix = Math.floor(fx), iy = Math.floor(fy), tx = fx - ix, ty = fy - iy, a = iy * W + ix;
            return (elev[a] * (1 - tx) + elev[a + 1] * tx) * (1 - ty) + (elev[a + W] * (1 - tx) + elev[a + W + 1] * tx) * ty;
        };

        // 2) imagen satelital (si falla, se pinta por altura)
        let textura;
        try {
            // La imagen se pide un nivel de zoom más fino que las elevaciones (más nitidez) cuando cabe en 4096 px.
            const f = (nx * 2 <= 16 && ny * 2 <= 16 && z < 17) ? 2 : 1, zi = z + (f === 2 ? 1 : 0);
            const cI = document.createElement('canvas'); cI.width = W * f; cI.height = H * f;
            const gI = cI.getContext('2d'); const tj = [];
            for (let i = 0; i < nx * f; i++) for (let j = 0; j < ny * f; j++)
                tj.push(cargar(`https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/${zi}/${y0 * f + j}/${x0 * f + i}`).then(im => gI.drawImage(im, i * 256, j * 256)));
            await Promise.all(tj);
            textura = new THREE.CanvasTexture(cI);
        } catch {
            let mn = Infinity, mx = -Infinity; for (const v of elev) { if (v < mn) mn = v; if (v > mx) mx = v; }
            const cI = document.createElement('canvas'); cI.width = W; cI.height = H;
            const gI = cI.getContext('2d'), im = gI.createImageData(W, H);
            for (let k = 0; k < W * H; k++) {
                const c = new THREE.Color().setHSL(.33 - .33 * ((elev[k] - mn) / Math.max(1, mx - mn)) , .45, .4 + .25 * ((elev[k] - mn) / Math.max(1, mx - mn)));
                im.data[k * 4] = c.r * 255; im.data[k * 4 + 1] = c.g * 255; im.data[k * 4 + 2] = c.b * 255; im.data[k * 4 + 3] = 255;
            }
            gI.putImageData(im, 0, 0); textura = new THREE.CanvasTexture(cI);
        }
        textura.colorSpace = THREE.SRGBColorSpace; textura.anisotropy = 8;

        // 3) malla del terreno en las mismas coordenadas locales de las torres
        const xa = (lo0 - o.lon0) * kLon, xb = (lo1 - o.lon0) * kLon, za = -(la1 - o.lat0) * kLat, zb = -(la0 - o.lat0) * kLat;
        T.forEach(t => { t.elev = muestra(t.lat, t.lon); });
        let emin = Infinity, emax = -Infinity;
        const crear = (paso) => {
            const sx = Math.max(2, Math.round((xb - xa) / paso)), sz = Math.max(2, Math.round((zb - za) / paso));
            const n = (sx + 1) * (sz + 1);
            const pos = new Float32Array(n * 3), uv = new Float32Array(n * 2), alt = new Float32Array(n);
            for (let j = 0; j <= sz; j++) for (let i = 0; i <= sx; i++) {
                const k = j * (sx + 1) + i, x = xa + (xb - xa) * i / sx, zz = za + (zb - za) * j / sz;
                const lat = o.lat0 - zz / kLat, lon = o.lon0 + x / kLon, e = muestra(lat, lon);
                alt[k] = e;
                pos[k * 3] = x; pos[k * 3 + 2] = zz;
                uv[k * 2] = (lonX(lon, z) - x0) * 256 / W; uv[k * 2 + 1] = 1 - (latY(lat, z) - y0) * 256 / H;
            }
            // El terreno se adapta a la cota de la base de cada torre: se levanta o baja la malla
            // alrededor de la torre (con caída suave) hasta que su superficie pase por esa cota.
            const dx = (xb - xa) / sx, dz = (zb - za) / sz, R = Math.max(dx, dz) * 1.8;
            for (let pasada = 0; pasada < 3; pasada++) for (const t of T) {
                const falta = t.elev - alturaMalla(alt, sx, sz, xa, xb, za, zb, t.x, t.z);
                if (Math.abs(falta) < .05) continue;
                const i0 = Math.max(0, Math.floor((t.x - R - xa) / dx)), i1 = Math.min(sx, Math.ceil((t.x + R - xa) / dx));
                const j0 = Math.max(0, Math.floor((t.z - R - za) / dz)), j1 = Math.min(sz, Math.ceil((t.z + R - za) / dz));
                for (let j = j0; j <= j1; j++) for (let i = i0; i <= i1; i++) {
                    const d = Math.hypot(xa + i * dx - t.x, za + j * dz - t.z);
                    if (d < R) alt[j * (sx + 1) + i] += falta * .5 * (1 + Math.cos(Math.PI * d / R));
                }
            }
            for (let k = 0; k < alt.length; k++) { if (alt[k] < emin) emin = alt[k]; if (alt[k] > emax) emax = alt[k]; }
            const idx = new Uint32Array(sx * sz * 6);
            let q = 0;
            for (let j = 0; j < sz; j++) for (let i = 0; i < sx; i++) {
                const a = j * (sx + 1) + i, b = a + 1, c = a + sx + 1, d = c + 1;
                idx[q++] = a; idx[q++] = c; idx[q++] = b; idx[q++] = b; idx[q++] = c; idx[q++] = d;
            }
            const g = new THREE.BufferGeometry();
            g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
            g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
            g.setIndex(new THREE.BufferAttribute(idx, 1));
            return { geom: g, alt, sx, sz, xa, xb, za, zb };
        };
        const fino = crear(Math.max(25, Math.max(xb - xa, zb - za) / 600));
        const grueso = crear(Math.max(120, Math.max(xb - xa, zb - za) / 110));   // facetas visibles (modo polígonos)
        for (const t of [fino, grueso]) {
            t.emin = emin;
            // color por altura (verde → amarillo → café → blanco), para los modos sin imagen
            const col = new Float32Array(t.alt.length * 3), c = new THREE.Color();
            const rampa = [[0, 0x3f7d3a], [.35, 0xb5b04a], [.7, 0x8a6a45], [1, 0xf2f2f2]];
            for (let k = 0; k < t.alt.length; k++) {
                const h = (t.alt[k] - emin) / Math.max(1, emax - emin);
                let r = 1; while (r < rampa.length - 1 && h > rampa[r][0]) r++;
                const [h0, c0] = rampa[r - 1], [h1, c1] = rampa[r];
                c.set(c0).lerp(new THREE.Color(c1), Math.min(1, Math.max(0, (h - h0) / (h1 - h0))));
                col[k * 3] = c.r; col[k * 3 + 1] = c.g; col[k * 3 + 2] = c.b;
            }
            t.geom.setAttribute('color', new THREE.BufferAttribute(col, 3));
        }
        this.emin = emin;
        textura.generateMipmaps = true; textura.minFilter = THREE.LinearMipmapLinearFilter;
        this.mallas3d = {
            satelite: { terr: fino, mat: new THREE.MeshStandardMaterial({ map: textura, roughness: 1, metalness: 0 }) },
            poligonos: { terr: grueso, mat: new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 1, metalness: 0 }) },
            malla: { terr: grueso, mat: new THREE.MeshBasicMaterial({ vertexColors: true, wireframe: true }) },
        };
        this.vistaTerreno = 'satelite';
        this.mostrarTerreno('satelite', true);
        this.sueloBk = this.suelo; this.gridBk = this.grid;
        if (this.sueloBk) this.sueloBk.visible = false;
        if (this.gridBk) this.gridBk.visible = false;
        this.terrenoListo = true;
        this.mostrarTerreno(this.aspectoTerreno, true);
        this.aplicarRelieve();
        this.vista('todo');
        this.avisarTerreno(true, `Terreno real (zoom ${z}): ${Math.round(emin)}–${Math.round(emax)} m s.n.m.`);
    }

    // Cambia el margen de terreno alrededor del trazado (km; 0 = automático) y reconstruye el terreno.
    async setMargen(km) {
        this.margenKm = km > 0 ? km : null;
        this.avisarTerreno(false, 'Recargando terreno...');
        if (this.malla) { this.scene.remove(this.malla); this.malla = null; }
        if (this.mallas3d) for (const k of Object.keys(this.mallas3d)) { const m = this.mallas3d[k]; m.terr.geom.dispose(); m.mat.map?.dispose(); m.mat.dispose(); }
        this.mallas3d = null; this.terr = null; this.terrenoListo = false;
        try { await this.terreno(); }
        catch (e) { this.avisarTerreno(false, 'Terreno: ' + (e && e.message ? e.message : e)); }
    }

    // Cambia el aspecto del terreno: 'satelite' (imagen), 'poligonos' (facetas por altura) o 'malla' (alambre).
    mostrarTerreno(modo, inicial) {
        this.aspectoTerreno = modo;
        const sinMalla = modo === 'plano' || modo === 'vacio';
        if (this.malla) { this.scene.remove(this.malla); this.malla = null; }
        if (!sinMalla) {
            const m = this.mallas3d && this.mallas3d[modo];
            if (m) {
                this.terr = m.terr; this.altMedia = undefined;
                this.malla = new THREE.Mesh(m.terr.geom, m.mat);
                this.malla.frustumCulled = false;
                this.scene.add(this.malla);
            }
        }
        if (this.sueloBk) this.sueloBk.visible = modo === 'plano';
        if (this.gridBk) this.gridBk.visible = modo === 'plano';
        if (this.terrenoListo) {
            if (sinMalla) { this.scene.background = new THREE.Color(0x14181f); this.scene.fog = null; }
            else {
                const cielo = new THREE.Color(0xa9c4dc);   // día: cielo y neblina suave que oculta el borde del terreno
                this.scene.background = cielo; this.scene.fog = new THREE.Fog(cielo, this.ext * .9, this.ext * 3.2);
            }
        }
        if (!inicial) this.aplicarRelieve();
    }

    // Reposiciona terreno, torres y ruta con el factor de relieve actual.
    aplicarRelieve() {
        if (!this.terr) return;
        const { geom, alt, emin } = this.terr, p = geom.attributes.position;
        for (let k = 0; k < alt.length; k++) p.setY(k, (alt[k] - emin) * this.relieve);
        p.needsUpdate = true; if (this.malla) geom.computeVertexNormals(); geom.computeBoundingSphere();
        const T = this.terr;
        this.torres.forEach(t => { t.y = this.aspectoTerreno === 'plano' ? 0 : (alturaMalla(alt, T.sx, T.sz, T.xa, T.xb, T.za, T.zb, t.x, t.z) - emin) * this.relieve; });
        const rp = this.ruta.geometry.attributes.position;
        this.torres.forEach((t, i) => rp.setY(i, t.y + 1)); rp.needsUpdate = true;
        this.actualizar();
    }

    setRelieve(f) { this.relieve = Math.max(1, f); this.aplicarRelieve(); }

    colorDe(t, i) {
        if (i === this.sel) return AMARILLO;
        if (this.modo === 'pie') {
            if (t.pct === null || t.pct === undefined) return new THREE.Color(0x777f8c);
            const h = Math.max(0, Math.min(1, t.pct / 100)) * 0.33; // rojo → verde
            return new THREE.Color().setHSL(h, .75, .5);
        }
        if (this.modo === 'fijo') return this.colorFijo.clone();
        if (this.modo === 'plano') return new THREE.Color(0xb7c0cc);
        return new THREE.Color(t.color || '#b7c0cc');
    }

    actualizar() {
        const s = this.escala, m = new THREE.Matrix4(), q = new THREE.Quaternion(), up = new THREE.Vector3(0, 1, 0);
        const pos = new THREE.Vector3(), esc = new THREE.Vector3();
        this.torres.forEach((t, i) => {
            q.setFromAxisAngle(up, t.rot);
            pos.set(t.x, t.y || 0, t.z);
            const oculta = this.ocultas && this.ocultas.has(t.familia);
            esc.set(s, s, s);
            if (oculta) esc.set(0, 0, 0);
            m.compose(pos, q, esc);
            const col = this.colorDe(t, i);
            for (const g of this.mallas) {
                const k = g.idx.indexOf(i);
                if (k >= 0) { g.mesh.setMatrixAt(k, m); g.mesh.setColorAt(k, col); g.mesh.instanceMatrix.needsUpdate = true; g.mesh.instanceColor.needsUpdate = true; }
            }
            const r = Math.max(t.ancho * .7, 6) * s, h = (t.alto || 40) * s;
            this.halo.setMatrixAt(i, m.compose(pos, new THREE.Quaternion(), oculta ? esc.set(0, 0, 0) : esc.set(r, h, r)));
            this.halo.setColorAt(i, col);
        });
        this.halo.instanceMatrix.needsUpdate = true; this.halo.instanceColor.needsUpdate = true;
    }

    // Familias (tipos) apagadas en la leyenda: sus torres no se dibujan ni se pueden seleccionar.
    setFamiliasOcultas(lista) { this.ocultas = new Set(lista || []); this.actualizar(); }
    setModo(m) { this.modo = m; this.actualizar(); }
    setColorFijo(hex) { this.colorFijo = new THREE.Color(hex); if (this.modo === 'fijo') this.actualizar(); }
    setLinea(visible, hex) { if (this.ruta) { this.ruta.visible = !!visible; this.ruta.material.color.set(hex); } }
    setEscala(e) { this.escala = Math.max(1, e); this.actualizar(); }

    pick(e) {
        const r = this.renderer.domElement.getBoundingClientRect();
        this.ptr.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
        this.ray.setFromCamera(this.ptr, this.camera);
        const hit = this.ray.intersectObject(this.halo, false)[0];
        return hit ? hit.instanceId : -1;
    }

    seleccionar(i, desdeVisor) {
        this.sel = i; this.actualizar();
        if (desdeVisor && this.dotnet) {
            const t = i >= 0 ? this.torres[i] : null;
            this.dotnet.invokeMethodAsync('AlSeleccionarTorre', t ? t.id : null, t ? t.alto || 0 : 0, t ? t.ancho || 0 : 0, t ? t.modelo : '');
        }
    }

    enfocar(id) {
        const i = this.torres.findIndex(t => t.id === id); if (i < 0) return;
        const t = this.torres[i], h = (t.alto || 40) * this.escala;
        this.seleccionar(i, true);
        this.controls.target.set(t.x, (t.y || 0) + h / 2, t.z);
        const off = new THREE.Vector3(Math.sin(t.rot + .9), .45, Math.cos(t.rot + .9)).normalize().multiplyScalar(Math.max(h * 3, 120));
        this.camera.position.copy(this.controls.target).add(off);
        this.controls.update();
    }

    // Vistas: iso (isométrico), planta, perfil (de lado, ve el relieve a lo largo de la línea) y frontal (a lo largo del eje).
    vista(tipo) {
        const c = this.centro, R = this.ext;
        if (this.terr && this.altMedia === undefined) this.altMedia = this.terr.alt.reduce((a, b) => a + b, 0) / this.terr.alt.length - this.terr.emin;
        const y = (this.terr && this.aspectoTerreno !== 'plano') ? this.altMedia * this.relieve : 0;
        const a = this.torres[0], b = this.torres[this.torres.length - 1];
        let ax = b.x - a.x, az = b.z - a.z; const L = Math.hypot(ax, az) || 1; ax /= L; az /= L;
        const dirs = { iso: [.5, .55, .9], todo: [.5, .55, .9], planta: [0, 1, .001], perfil: [-az, .1, ax], frontal: [ax, .15, az] };
        const dist = { iso: 1.1, todo: 1.1, planta: 1.3, perfil: 1.5, frontal: 1.1 }[tipo] || 1.1;
        this.controls.target.set(c.x, y, c.z);
        this.camera.position.copy(this.controls.target).add(new THREE.Vector3(...(dirs[tipo] || dirs.iso)).normalize().multiplyScalar(R * dist));
        this.camera.far = Math.max(2000000, R * 20); this.camera.updateProjectionMatrix();
        this.controls.update();
    }

    crearBarra() {
        const bar = document.createElement('div');
        bar.style.cssText = 'position:absolute;left:10px;top:10px;display:flex;flex-wrap:wrap;gap:4px;z-index:6;max-width:calc(100% - 290px)';
        const mk = (txt, titulo, fn) => {
            const b = document.createElement('button');
            b.type = 'button'; b.textContent = txt; b.title = titulo;
            b.style.cssText = 'background:rgba(18,22,30,.9);color:#e8edf3;border:1px solid #3a4658;border-radius:5px;padding:3px 9px;font:12px sans-serif;cursor:pointer';
            b.addEventListener('click', fn); bar.appendChild(b); return b;
        };
        this.btnMano = mk('✋ Mano', 'Modo mano: arrastra con el botón izquierdo para desplazarte', () => this.modoMano(!this.mano));
        mk('⛶ Pantalla completa', 'Pantalla completa (Esc para salir)', () => this.pantallaCompleta());
        for (const [k, t] of [['iso', 'Isométrico'], ['planta', 'Planta'], ['perfil', 'Perfil'], ['frontal', 'Frontal']])
            mk(t, 'Vista ' + t.toLowerCase(), () => this.vista(k));
        this.el.appendChild(bar);
    }

    modoMano(on) {
        this.mano = on;
        this.controls.mouseButtons.LEFT = on ? THREE.MOUSE.PAN : THREE.MOUSE.ROTATE;
        this.controls.touches.ONE = on ? THREE.TOUCH.PAN : THREE.TOUCH.ROTATE;
        this.controls.screenSpacePanning = true;
        this.btnMano.style.background = on ? '#2f6fdb' : 'rgba(18,22,30,.9)';
        this.btnMano.textContent = on ? '✋ Mano (activa)' : '✋ Mano';
        this.renderer.domElement.style.cursor = on ? 'grab' : 'default';
    }

    pantallaCompleta() {
        const objetivo = this.el.closest('.l3d-wrap') || this.el; // incluye controles, leyenda y tarjeta
        if (document.fullscreenElement) document.exitFullscreen();
        else if (objetivo.requestFullscreen) objetivo.requestFullscreen();
    }

    ajustar() {
        const w = this.el.clientWidth, h = this.el.clientHeight; if (!w || !h) return;
        this.renderer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix();
    }

    liberar() {
        this.activo = false; this._ro.disconnect(); document.removeEventListener('fullscreenchange', this._fs);
        this.scene.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) o.material.dispose?.(); });
        this.renderer.dispose(); this.renderer.domElement.remove();
    }
}
