// Visor 3D de modelos de torres (Three.js, vendorizado en lib/three). Módulo ES.
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

const K = 0.001; // mm -> m
const COLOR_SEL = 0xffd400, COLOR_HOVER = 0xffffff;

export async function crear(contenedor, urlJson, dotnet) {
    const v = new Visor(contenedor, dotnet);
    await v.cargar(urlJson);
    return v;
}


// ---------- Miniaturas de los modelos (para la cuadrícula de selección) ----------
// Un solo renderizador compartido; cada miniatura se genera una vez y se guarda como imagen (data URL).
const cacheMini = new Map();
let rendMini = null;

export async function miniatura(urlJson) {
    if (cacheMini.has(urlJson)) return cacheMini.get(urlJson);
    const data = await (await fetch(urlJson)).json();
    const W = 340, H = 360;
    if (!rendMini) {
        rendMini = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
        rendMini.setPixelRatio(1); rendMini.setSize(W, H);
    }
    const scene = new THREE.Scene(); scene.background = new THREE.Color(0x14181f);
    scene.add(new THREE.HemisphereLight(0xffffff, 0x334455, 1.4));
    const dl = new THREE.DirectionalLight(0xffffff, 1.6); dl.position.set(40, 120, 60); scene.add(dl);
    const colores = Object.fromEntries(data.grupos.map(g => [g.nombre, g.color]));
    const porGrupo = new Map();
    for (const m of data.miembros) { if (!porGrupo.has(m.grupo)) porGrupo.set(m.grupo, []); porGrupo.get(m.grupo).push(m); }
    const mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: .6, metalness: .2 });
    const box = new THREE.Box3(), construir = Visor.prototype.construir;
    for (const [nombre, miembros] of porGrupo) {
        const mesh = construir.call(null, miembros, new THREE.Color(colores[nombre] || '#888'), mat);
        scene.add(mesh); box.expandByObject(mesh);
    }
    const c = box.getCenter(new THREE.Vector3()), sz = box.getSize(new THREE.Vector3());
    const cam = new THREE.PerspectiveCamera(40, W / H, 0.5, 4000);
    const dist = Math.max(sz.y, sz.x * 1.1, sz.z * 1.1) / (2 * Math.tan(THREE.MathUtils.degToRad(20))) * 1.35;
    cam.position.copy(c).addScaledVector(new THREE.Vector3(.8, .35, 1).normalize(), dist);
    cam.lookAt(c);
    rendMini.render(scene, cam);
    const url = rendMini.domElement.toDataURL('image/png');
    scene.traverse(o => { if (o.geometry) o.geometry.dispose(); });
    mat.dispose();
    cacheMini.set(urlJson, url);
    return url;
}

class Visor {
    constructor(el, dotnet) {
        this.el = el; this.dotnet = dotnet;
        this.grupos = new Map();           // nombre -> {mesh, miembros, color}
        this.porId = new Map();
        this.sel = null; this.hover = null;
        this.planos = new Map();           // 'codigo|hoja' -> {texto, url}

        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
        el.appendChild(this.renderer.domElement);
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x14181f);
        this.camera = new THREE.PerspectiveCamera(40, 1, 0.5, 2000);
        this.controls = new OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.enableZoom = true;
        this.controls.touches = { ONE: THREE.TOUCH.ROTATE, TWO: THREE.TOUCH.DOLLY_PAN }; // pellizco = zoom y desplazamiento
        this.scene.add(new THREE.HemisphereLight(0xffffff, 0x334455, 1.4));
        const dl = new THREE.DirectionalLight(0xffffff, 1.6); dl.position.set(40, 120, 60); this.scene.add(dl);
        this.ray = new THREE.Raycaster(); this.ptr = new THREE.Vector2();

        this.tip = document.createElement('div');
        this.tip.style.cssText = 'position:absolute;pointer-events:none;background:rgba(0,0,0,.82);color:#fff;font:12px sans-serif;padding:3px 7px;border-radius:4px;display:none;white-space:nowrap;z-index:5';
        el.style.position = 'relative'; el.appendChild(this.tip);
        this.crearBarra();
        this.card = document.createElement('div');
        this.card.style.cssText = 'position:absolute;right:10px;top:10px;width:270px;max-height:calc(100% - 20px);overflow:auto;background:rgba(18,22,30,.92);color:#e8edf3;font:12px/1.4 sans-serif;padding:10px 12px;border-radius:6px;border:1px solid #3a4658;display:none;z-index:6';
        el.appendChild(this.card);
        document.addEventListener('fullscreenchange', this._fs = () => this.ajustar());

        this._ro = new ResizeObserver(() => this.ajustar()); this._ro.observe(el);
        const cv = this.renderer.domElement;
        cv.addEventListener('pointerdown', e => { this._d0 = [e.clientX, e.clientY]; });
        cv.addEventListener('pointerup', e => {
            if (!this._d0 || Math.hypot(e.clientX - this._d0[0], e.clientY - this._d0[1]) > 4) return;
            const m = this.pick(e); this.seleccionar(m ? m.id : null, true);
        });
        cv.addEventListener('pointermove', e => {
            if (e.buttons) { this.tip.style.display = 'none'; return; }
            if (this._raf) return;
            this._raf = requestAnimationFrame(() => {
                this._raf = 0; const m = this.pick(e);
                this.setHover(m);
                if (m) {
                    const r = el.getBoundingClientRect();
                    this.tip.textContent = `${m.marca ? m.marca + ' · ' : ''}${m.nombre} · ${(m.longitud_mm / 1000).toFixed(3)} m`;
                    this.tip.style.left = (e.clientX - r.left + 14) + 'px';
                    this.tip.style.top = (e.clientY - r.top + 14) + 'px';
                    this.tip.style.display = 'block';
                } else this.tip.style.display = 'none';
            });
        });
        cv.addEventListener('pointerleave', () => { this.tip.style.display = 'none'; this.setHover(null); });
        this.activo = true;
        const loop = () => { if (!this.activo) return; this.controls.update(); this.renderer.render(this.scene, this.camera); requestAnimationFrame(loop); };
        loop();
    }

    async cargar(url) {
        const data = await (await fetch(url)).json();
        this.meta = data.meta; this.cat = data.marcas || {}; this.lista = data.lista_materiales || [];
        const colores = Object.fromEntries(data.grupos.map(g => [g.nombre, g.color]));
        const porGrupo = new Map();
        for (const m of data.miembros) {
            this.porId.set(m.id, m);
            if (!porGrupo.has(m.grupo)) porGrupo.set(m.grupo, []);
            porGrupo.get(m.grupo).push(m);
        }
        const mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: .6, metalness: .2 });
        const box = new THREE.Box3();
        const raiz = new THREE.Group(); this.scene.add(raiz); this.raiz = raiz;
        for (const [nombre, miembros] of porGrupo) {
            const mesh = this.construir(miembros, new THREE.Color(colores[nombre] || '#888'), mat);
            raiz.add(mesh);
            this.grupos.set(nombre, { mesh, miembros, color: colores[nombre] });
            box.expandByObject(mesh);
        }
        this.caja = box; this.cajaPrincipal = box.clone();
        this.porMarca = new Map();
        for (const m of data.miembros) for (const k of (m.marcas || [])) {
            const cur = this.porMarca.get(k);
            if (!cur || (m.cara === 'A' && cur.cara !== 'A')) this.porMarca.set(k, m);
        }
        this.crearPanel();
        const grid = new THREE.GridHelper(80, 16, 0x445566, 0x2a3340); this.scene.add(grid);
        this.vista('iso');
        this.ajustar();
    }

    construir(miembros, color, mat) {
        const n = miembros.length, pos = new Float32Array(n * 16 * 3), col = new Float32Array(n * 16 * 3), idx = new Uint32Array(n * 24);
        const a = new THREE.Vector3(), b = new THREE.Vector3(), d = new THREE.Vector3(), u = new THREE.Vector3(), w = new THREE.Vector3();
        miembros.forEach((m, i) => {
            a.set(m.a[0] * K, m.a[1] * K, m.a[2] * K); b.set(m.b[0] * K, m.b[1] * K, m.b[2] * K);
            d.subVectors(b, a).normalize();
            const ref = Math.abs(d.y) > .95 ? new THREE.Vector3(1, 0, 0) : new THREE.Vector3(0, 1, 0);
            u.crossVectors(d, ref).normalize(); w.crossVectors(d, u).normalize();
            const t = (m.tipo === 'Montante' ? .22 : .12) / 2;
            const s = [[1, 1], [-1, 1], [-1, -1], [1, -1]];
            [a, b].forEach((p, e) => s.forEach(([su, sw], k) => {
                const o = (i * 8 + e * 4 + k) * 3;
                pos[o] = p.x + (u.x * su + w.x * sw) * t; pos[o + 1] = p.y + (u.y * su + w.y * sw) * t; pos[o + 2] = p.z + (u.z * su + w.z * sw) * t;
                col[o] = color.r; col[o + 1] = color.g; col[o + 2] = color.b;
            }));
            const base = i * 8, o = i * 24; // 4 caras laterales x 2 tri; (8 tri por miembro -> 24 índices)
            for (let k = 0; k < 4; k++) {
                const k2 = (k + 1) % 4;
                idx.set([base + k, base + k2, base + 4 + k, base + k2, base + 4 + k2, base + 4 + k], o + k * 6);
            }
        });
        // buffers de 8 vértices por miembro
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(pos.subarray(0, n * 24), 3));
        g.setAttribute('color', new THREE.BufferAttribute(col.subarray(0, n * 24), 3));
        g.setIndex(new THREE.BufferAttribute(idx, 1));
        g.computeVertexNormals();
        const mesh = new THREE.Mesh(g, mat);
        mesh.userData = { miembros, color };
        return mesh;
    }

    pick(e) {
        if (this.raiz && !this.raiz.visible) return null;
        const r = this.renderer.domElement.getBoundingClientRect();
        this.ptr.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
        this.ray.setFromCamera(this.ptr, this.camera);
        const meshes = [...this.grupos.values()].map(g => g.mesh).filter(m => m.visible);
        const hit = this.ray.intersectObjects(meshes, false)[0];
        return hit ? hit.object.userData.miembros[Math.floor(hit.faceIndex / 8)] : null;
    }

    base(m, gr) {
        const c = gr.mesh.userData.color;
        return (this.soloMarcas && !m.marcas) ? c.clone().multiplyScalar(.28) : c;
    }
    resaltarMarcas(on) {
        this.soloMarcas = on;
        for (const gr of this.grupos.values()) gr.miembros.forEach(m => { if (m !== this.sel) this.pintar(m, null); });
        if (this.btnMarcas) { this.btnMarcas.style.background = on ? '#2f6fdb' : 'rgba(18,22,30,.9)'; }
    }
    pintar(m, hex) {
        if (!m) return;
        const gr = this.grupos.get(m.grupo), i = gr.miembros.indexOf(m);
        const c = hex === null ? this.base(m, gr) : new THREE.Color(hex);
        const at = gr.mesh.geometry.attributes.color;
        for (let k = 0; k < 8; k++) at.setXYZ(i * 8 + k, c.r, c.g, c.b);
        at.needsUpdate = true;
    }
    setHover(m) {
        if (m === this.hover) return;
        if (this.hover && this.hover !== this.sel) this.pintar(this.hover, null);
        this.hover = m;
        if (m && m !== this.sel) this.pintar(m, COLOR_HOVER);
        this.renderer.domElement.style.cursor = m ? 'pointer' : (this.mano ? 'grab' : 'default');
    }

    seleccionar(id, desdeVisor) {
        if (this.sel) this.pintar(this.sel, null);
        this.sel = id ? this.porId.get(id) || null : null;
        if (this.sel) {
            this.pintar(this.sel, COLOR_SEL);
            if (this.marcas) { this.scene.remove(this.marcas); }
            const m = this.sel, g = new THREE.Group();
            for (const p of [m.a, m.b]) {
                const s = new THREE.Mesh(new THREE.SphereGeometry(.2, 12, 12), new THREE.MeshBasicMaterial({ color: COLOR_SEL }));
                s.position.set(p[0] * K, p[1] * K, p[2] * K); g.add(s);
            }
            const ln = new THREE.Line(new THREE.BufferGeometry().setFromPoints(g.children.map(c => c.position)), new THREE.LineBasicMaterial({ color: COLOR_SEL, depthTest: false }));
            ln.renderOrder = 9; g.add(ln);
            this.marcas = g; this.scene.add(g);
        } else if (this.marcas) { this.scene.remove(this.marcas); this.marcas = null; }
        this.mostrarCard(this.sel);
        if (desdeVisor && this.dotnet) this.dotnet.invokeMethodAsync('AlSeleccionarMiembro', this.sel ? this.sel.id : null);
    }

    enfocar(id) {
        const m = this.porId.get(id); if (!m) return;
        const c = new THREE.Vector3((m.a[0] + m.b[0]) / 2, (m.a[1] + m.b[1]) / 2, (m.a[2] + m.b[2]) / 2).multiplyScalar(K);
        const off = this.camera.position.clone().sub(this.controls.target);
        this.controls.target.copy(c);
        this.camera.position.copy(c).add(off.setLength(Math.max(12, m.longitud_mm * K * 3)));
    }

    grupoVisible(nombre, vis) { const g = this.grupos.get(nombre); if (g) g.mesh.visible = vis; }
    aislar(nombre) { for (const [n, g] of this.grupos) g.mesh.visible = (nombre === null || n === nombre); }

    vista(tipo) {
        const c = new THREE.Vector3(); this.caja.getCenter(c);
        const sz = this.caja.getSize(new THREE.Vector3()), R = Math.max(sz.y * 1.7, Math.max(sz.x, sz.z) * 1.3, 60);
        const dirs = { frontal: [0, 0, 1], lateral: [1, 0, 0], posterior: [0, 0, -1], planta: [0, 1, .001], iso: [.8, .45, 1] };
        const d = new THREE.Vector3(...(dirs[tipo] || dirs.iso)).normalize();
        this.controls.target.copy(c);
        this.camera.position.copy(c).addScaledVector(d, R);
        this.camera.up.set(tipo === 'planta' ? 0 : 0, tipo === 'planta' ? 0 : 1, tipo === 'planta' ? -1 : 0);
        this.controls.update();
    }


    // ---------- Comparar con otras torres (se colocan una al lado de otra) ----------
    // El catálogo lo manda la página: [{nombre, url}]. Todo el panel vive dentro del visor, así que
    // también se ve en pantalla completa.
    setCatalogo(lista) {
        this.catalogo = lista || []; this.compar = []; this.cmpAbierto = false;
        this.panelCmp = document.createElement('div');
        this.panelCmp.style.cssText = 'position:absolute;left:10px;bottom:10px;width:230px;max-height:45%;overflow:auto;background:rgba(18,22,30,.9);color:#e8edf3;border:1px solid #3a4658;border-radius:6px;font:12px/1.3 sans-serif;padding:6px 8px;z-index:6';
        this.el.appendChild(this.panelCmp);
        this.dibujarComparar();
    }

    dibujarComparar() {
        const esc = (t) => String(t ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
        const css = 'background:rgba(18,22,30,.9);color:#e8edf3;border:1px solid #3a4658;border-radius:5px;cursor:pointer;';
        const p = this.panelCmp; p.replaceChildren();
        const cab = document.createElement('div');
        cab.style.cssText = 'display:flex;justify-content:space-between;align-items:center;font-weight:600;margin-bottom:3px';
        cab.innerHTML = '<span>Comparar torres</span>';
        const mas = document.createElement('button');
        mas.type = 'button'; mas.textContent = '＋'; mas.title = 'Agregar otra torre para comparar';
        mas.style.cssText = css + 'padding:0 8px;font-size:14px;' + (this.cmpAbierto ? 'background:#2f6fdb;' : '');
        mas.addEventListener('click', () => { this.cmpAbierto = !this.cmpAbierto; this.dibujarComparar(); });
        cab.appendChild(mas); p.appendChild(cab);
        const fila = (nombre, visible, onVis, onQuitar, color) => {
            const r = document.createElement('div');
            r.style.cssText = 'display:flex;align-items:center;gap:5px;margin:2px 0';
            r.innerHTML = `<input type="checkbox" ${visible ? 'checked' : ''} title="Mostrar / ocultar" style="margin:0">` +
                `<span style="width:9px;height:9px;border-radius:2px;background:${color};display:inline-block"></span>` +
                `<span style="flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="${esc(nombre)}">${esc(nombre)}</span>`;
            r.firstChild.addEventListener('change', e => onVis(e.target.checked));
            if (onQuitar) {
                const x = document.createElement('button');
                x.type = 'button'; x.textContent = '✕'; x.title = 'Quitar de la comparación';
                x.style.cssText = css + 'padding:0 5px;color:#ff6b6b'; x.addEventListener('click', onQuitar); r.appendChild(x);
            }
            p.appendChild(r);
        };
        fila((this.meta && this.meta.nombre) || 'Torre actual', this.raiz.visible, v => { this.raiz.visible = v; this.rotulos(); this.encuadrarTodo(); }, null, '#5aa2ff');
        for (const c of this.compar) fila(c.nombre, c.grupo.visible, v => { c.grupo.visible = v; this.rotulos(); }, () => this.quitarComparada(c), c.color);
        if (this.cmpAbierto) {
            const lst = document.createElement('div');
            lst.style.cssText = 'margin-top:4px;border-top:1px solid #3a4658;padding-top:4px;max-height:160px;overflow:auto';
            for (const m of this.catalogo) {
                const it = document.createElement('div');
                it.style.cssText = 'padding:2px 4px;cursor:pointer;border-radius:3px';
                it.textContent = m.nombre; it.title = 'Agregar ' + m.nombre;
                it.addEventListener('mouseenter', () => it.style.background = '#26334a');
                it.addEventListener('mouseleave', () => it.style.background = '');
                it.addEventListener('click', () => this.agregarComparada(m));
                lst.appendChild(it);
            }
            if (!this.catalogo.length) lst.textContent = 'No hay otros modelos.';
            p.appendChild(lst);
        }
        if (this.msgCmp) { const a = document.createElement('div'); a.style.cssText = 'color:#c9a227;margin-top:3px'; a.textContent = this.msgCmp; p.appendChild(a); }
    }

    async agregarComparada(m) {
        this.msgCmp = 'Cargando ' + m.nombre + '...'; this.dibujarComparar();
        try {
            const data = await (await fetch(m.url)).json();
            const colores = Object.fromEntries(data.grupos.map(g => [g.nombre, g.color]));
            const porGrupo = new Map();
            for (const mi of data.miembros) { if (!porGrupo.has(mi.grupo)) porGrupo.set(mi.grupo, []); porGrupo.get(mi.grupo).push(mi); }
            const mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: .6, metalness: .2 });
            const grupo = new THREE.Group(), box = new THREE.Box3();
            for (const [nombre, miembros] of porGrupo) {
                const mesh = this.construir(miembros, new THREE.Color(colores[nombre] || '#888'), mat);
                grupo.add(mesh); box.expandByObject(mesh);
            }
            this.scene.add(grupo);
            const paleta = ['#e5a50a', '#4cc38a', '#d96ad1', '#5ad1e0', '#ff7a59', '#a3e635'];
            this.compar.push({ nombre: m.nombre, grupo, caja: box, color: paleta[this.compar.length % paleta.length] });
            this.msgCmp = '';
            this.reubicar(); this.encuadrarTodo();
        } catch (e) { this.msgCmp = 'No se pudo cargar: ' + (e && e.message ? e.message : e); }
        this.dibujarComparar();
    }

    quitarComparada(c) {
        this.scene.remove(c.grupo);
        c.grupo.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) o.material.dispose?.(); });
        this.compar = this.compar.filter(x => x !== c);
        this.reubicar(); this.encuadrarTodo(); this.dibujarComparar();
    }

    // Coloca las torres en fila a lo largo de X: la actual queda donde está y las demás a su derecha, con 8 m de separación.
    reubicar() {
        let cursor = this.cajaPrincipal.max.x + 8;
        for (const c of this.compar) {
            const w = c.caja.max.x - c.caja.min.x;
            c.grupo.position.set(cursor - c.caja.min.x, 0, 0);
            c.desde = cursor; c.hasta = cursor + w; cursor += w + 8;
        }
        this.rotulos();
    }

    // Rótulos con el nombre sobre cada torre (solo cuando hay más de una).
    rotulos() {
        if (this.etiquetas) { for (const e of this.etiquetas) { this.scene.remove(e); e.material.map.dispose(); e.material.dispose(); } }
        this.etiquetas = [];
        if (!this.compar.length) return;
        const poner = (texto, x, y) => {
            const cv = document.createElement('canvas'); cv.width = 512; cv.height = 96;
            const g = cv.getContext('2d'); g.fillStyle = 'rgba(18,22,30,.85)'; g.fillRect(0, 0, 512, 96);
            g.fillStyle = '#fff'; g.font = 'bold 40px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
            g.fillText(texto.length > 26 ? texto.slice(0, 25) + '…' : texto, 256, 48);
            const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(cv), depthTest: false }));
            sp.scale.set(14, 2.6, 1); sp.position.set(x, y + 3, 0); sp.renderOrder = 10;
            this.scene.add(sp); this.etiquetas.push(sp);
        };
        if (this.raiz.visible) poner((this.meta && this.meta.nombre) || 'Torre actual', (this.cajaPrincipal.min.x + this.cajaPrincipal.max.x) / 2, this.cajaPrincipal.max.y);
        for (const c of this.compar) if (c.grupo.visible) poner(c.nombre, (c.desde + c.hasta) / 2, c.caja.max.y);
    }

    // Reencuadra la cámara para que entren todas las torres visibles.
    encuadrarTodo() {
        const b = new THREE.Box3();
        if (this.raiz.visible) b.union(this.cajaPrincipal);
        for (const c of this.compar) if (c.grupo.visible) b.union(new THREE.Box3(new THREE.Vector3(c.desde, c.caja.min.y, c.caja.min.z), new THREE.Vector3(c.hasta, c.caja.max.y, c.caja.max.z)));
        if (b.isEmpty()) return;
        this.caja = b; this.vista('frontal');
    }

    crearPanel() {
        const css = 'background:rgba(18,22,30,.9);color:#e8edf3;border:1px solid #3a4658;border-radius:6px;font:11px/1.3 sans-serif;';
        const p = document.createElement('div');
        p.style.cssText = css + 'position:absolute;left:10px;top:46px;width:200px;max-height:calc(100% - 60px);overflow:auto;padding:6px 8px;z-index:6';
        const esc = (t) => String(t ?? '').replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));
        // Partes
        const cab = document.createElement('div');
        cab.style.cssText = 'display:flex;justify-content:space-between;align-items:center;cursor:pointer;font-weight:600';
        cab.innerHTML = '<span>Partes</span><span class="m3d-flecha">▾</span>';
        const cuerpo = document.createElement('div');
        cab.addEventListener('click', () => { const a = cuerpo.style.display === 'none'; cuerpo.style.display = a ? '' : 'none'; cab.lastChild.textContent = a ? '▾' : '▸'; });
        for (const [nombre, g] of this.grupos) {
            const r = document.createElement('div');
            r.style.cssText = 'display:flex;align-items:center;gap:5px;margin:2px 0';
            r.innerHTML = `<input type="checkbox" checked style="margin:0"><span style="width:9px;height:9px;border-radius:2px;background:${esc(g.color)};display:inline-block"></span>` +
                `<span style="flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="${esc(nombre)}">${esc(nombre)} <span style="color:#8fa0b5">${g.miembros.length}</span></span>` +
                `<button type="button" title="Mostrar solo esta parte" style="${css}padding:0 5px;cursor:pointer">solo</button>`;
            r.firstChild.addEventListener('change', e => this.grupoVisible(nombre, e.target.checked));
            r.lastChild.addEventListener('click', () => { this.aislar(nombre); cuerpo.querySelectorAll('input').forEach(i => i.checked = false); r.firstChild.checked = true; });
            cuerpo.appendChild(r);
        }
        const todo = document.createElement('button');
        todo.type = 'button'; todo.textContent = 'Mostrar todo';
        todo.style.cssText = css + 'padding:1px 6px;margin-top:3px;cursor:pointer;width:100%';
        todo.addEventListener('click', () => { this.aislar(null); cuerpo.querySelectorAll('input').forEach(i => i.checked = true); });
        cuerpo.appendChild(todo);
        // Buscar elemento
        const tit = document.createElement('div');
        tit.style.cssText = 'font-weight:600;margin-top:8px'; tit.textContent = 'Buscar elemento';
        const inp = document.createElement('input');
        inp.type = 'text'; inp.autocomplete = 'off'; inp.spellcheck = false;
        inp.style.cssText = css + 'width:100%;padding:3px 6px;margin-top:3px;box-sizing:border-box';
        const res = document.createElement('div');
        res.style.cssText = 'max-height:220px;overflow:auto;margin-top:3px';
        const aviso = document.createElement('div');
        aviso.style.cssText = 'color:#c9a227;margin-top:3px';
        inp.addEventListener('input', () => {
            const q = inp.value.trim().toLowerCase(); res.innerHTML = ''; aviso.textContent = '';
            if (q.length < 2) return;
            const hit = this.lista.filter(f => String(f.marca).toLowerCase().includes(q) || String(f.perfil || '').toLowerCase().includes(q) || String(f.conjunto || '').toLowerCase().includes(q));
            hit.slice(0, 40).forEach(f => {
                const k = `${f.hoja}|${f.marca}`, ok = this.porMarca.has(k);
                const it = document.createElement('div');
                it.style.cssText = 'padding:2px 4px;cursor:pointer;border-radius:3px';
                it.innerHTML = `<b>${esc(f.marca)}</b> ${esc(f.perfil)}${f.long_mm ? ' · ' + esc(f.long_mm) : ''} <span style="color:#8fa0b5">H${esc(f.hoja_txt || f.hoja)}</span>${ok ? ' <span style="color:#5aa2ff">●</span>' : ''}`;
                it.addEventListener('mouseenter', () => it.style.background = '#26334a');
                it.addEventListener('mouseleave', () => it.style.background = '');
                it.addEventListener('click', () => {
                    const m = this.porMarca.get(k);
                    if (!m) { aviso.textContent = 'En la lista de materiales, aún sin ubicar en el modelo.'; return; }
                    aviso.textContent = '';
                    this.grupoVisible(m.grupo, true);
                    this.seleccionar(m.id, true); this.enfocar(m.id);
                });
                res.appendChild(it);
            });
            if (hit.length > 40) { const t = document.createElement('div'); t.style.color = '#8fa0b5'; t.textContent = `… ${hit.length - 40} más`; res.appendChild(t); }
            if (!hit.length) { aviso.textContent = 'Sin coincidencias.'; }
        });
        p.append(cab, cuerpo, tit, inp, res, aviso);
        this.el.appendChild(p); this.panel = p;
    }

    crearBarra() {
        const bar = document.createElement('div');
        bar.style.cssText = 'position:absolute;left:10px;top:10px;display:flex;flex-wrap:wrap;gap:4px;z-index:6;max-width:calc(100% - 300px)';
        const mk = (txt, titulo, fn) => {
            const b = document.createElement('button');
            b.type = 'button'; b.textContent = txt; b.title = titulo;
            b.style.cssText = 'background:rgba(18,22,30,.9);color:#e8edf3;border:1px solid #3a4658;border-radius:5px;padding:3px 9px;font:12px sans-serif;cursor:pointer';
            b.addEventListener('click', fn); bar.appendChild(b); return b;
        };
        this.btnMano = mk('✋ Mano', 'Modo mano: arrastra con el botón izquierdo para desplazarte por el modelo', () => this.modoMano(!this.mano));
        this.btnMarcas = mk('🏷 Solo con marca', 'Atenúa los miembros sin marca verificada', () => this.resaltarMarcas(!this.soloMarcas));
        mk('⛶ Pantalla completa', 'Pantalla completa (Esc para salir)', () => this.pantallaCompleta());
        for (const [k, t] of [['iso', 'Iso'], ['frontal', 'Frontal'], ['lateral', 'Lateral'], ['posterior', 'Posterior'], ['planta', 'Planta']])
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
        if (document.fullscreenElement) document.exitFullscreen();
        else if (this.el.requestFullscreen) this.el.requestFullscreen();
    }

    // Enlaces a los planos del listado maestro (los resuelve la página): clave 'codigo|hoja'.
    setPlanos(lista) {
        this.planos = new Map((lista || []).map(p => [p.clave, p]));
        if (this.sel) this.mostrarCard(this.sel);
    }

    claveSet(m) {
        if (m.plano) return `${m.plano.codigo}|${m.plano.hoja}`;
        const base = this.meta && this.meta.plano_base, h = /H(\d+)/.exec(m.fuente || '');
        return base && h ? `${base}|${h[1]}` : null;
    }

    mostrarCard(m) {
        if (!m) { this.card.style.display = 'none'; return; }
        const f = (mm) => (mm / 1000).toFixed(3) + ' m';
        const esc = (t) => String(t ?? '').replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));
        const fila = (k, v) => v === undefined || v === null || v === '' ? '' : `<tr><td style="color:#8fa0b5;padding:1px 8px 1px 0;vertical-align:top">${k}</td><td>${v}</td></tr>`;
        const col = { ok: '#4cc38a', revisar: '#e5484d', 'n/a': '#c9a227' }[m.coherencia] || '#c9a227';
        let marcas = '';
        for (const k of (m.marcas || [])) {
            const c = this.cat[k] || {};
            const det = c.en_lista ? `${esc(c.perfil)}${c.long_mm ? ' · ' + esc(c.long_mm) + ' mm' : ''}${c.cant ? ' · cant. ' + esc(c.cant) : ''}${c.acero ? ' · ' + esc(c.acero) : ''}`
                : '<span style="color:#c9a227">no figura en la lista de materiales de la hoja</span>';
            marcas += `<div style="margin:2px 0"><b style="color:#ffd400">${esc(c.marca)}</b> <span style="color:#8fa0b5">(H${esc(c.hoja)}${c.clase ? ' · ' + c.clase.toLowerCase() : ''})</span><br>${det}</div>`;
        }
        this.card.innerHTML = `<div style="font-weight:600;font-size:13px;margin-bottom:2px">${esc(m.nombre)}</div>` +
            `<div style="color:#8fa0b5;margin-bottom:6px">${esc(m.id)}</div><table>` +
            fila('Parte', esc(m.grupo)) + fila('Tramo', esc(m.tramo)) + fila('Tipo', esc(m.tipo)) + fila('Cara', esc(m.cara) + (m.cara_tipo ? ' (' + esc(m.cara_tipo) + ')' : '')) +
            fila('Long. eje (modelo)', '<b>' + f(m.longitud_mm) + '</b>') +
            fila('Δ vertical', f(Math.abs(m.dy_mm))) + fila('Δ horizontal', f(Math.hypot(m.dx_mm, m.dz_mm))) +
            fila('Fuente', this.htmlFuente(m, esc)) + '</table>' +
            (m.marcas ? `<div style="margin-top:6px;border-top:1px solid #3a4658;padding-top:6px"><div style="color:#8fa0b5">Marcas del plano</div>${marcas}` +
                `<div style="margin-top:4px;color:${col}">${m.coherencia === 'ok' ? '✔ ' : m.coherencia === 'revisar' ? '⚠ ' : ''}${esc(m.coherencia_txt)}</div>` +
                `<div style="margin-top:4px;color:#8fa0b5;font-size:11px">${esc(m.verificacion)}</div></div>`
                : `<div style="margin-top:6px;color:#c9a227">${esc(m.nota || 'Sin marca verificada para este miembro (geometría del esquema).')}</div>`);
        this.card.style.display = 'block';
    }

    // Fuente: nombre del plano; azul con 📄 y enlace si el listado maestro tiene el PDF adjunto.
    htmlFuente(m, esc) {
        const det = m.fuente ? `<div style="color:#8fa0b5;font-size:11px">${esc(m.fuente)}</div>` : '';
        const k = this.claveSet(m), p = k && this.planos.get(k);
        if (!p) return esc(m.fuente || '');
        const nombre = p.url
            ? `<a href="${esc(p.url).replace(/"/g, '&quot;')}" target="_blank" rel="noopener" style="color:#5aa9ff;text-decoration:none" title="Abrir el plano">📄 ${esc(p.texto)}</a>`
            : `<span title="Plano sin adjunto en el listado maestro">${esc(p.texto)}</span>`;
        return nombre + det;
    }

    ajustar() {
        const w = this.el.clientWidth, h = this.el.clientHeight || 500;
        if (!w) return;
        this.renderer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix();
    }

    liberar() {
        this.activo = false; document.removeEventListener('fullscreenchange', this._fs); this._ro.disconnect(); this.renderer.dispose();
        this.renderer.domElement.remove(); this.tip.remove(); this.el.replaceChildren();
    }
}
