// Visor IFC (Three.js + web-ifc, ambos vendorizados en lib/). Módulo ES.
// Lee el archivo .ifc directamente: geometría, estructura espacial y propiedades
// salen del IFC, no del JSON del visor 3D. Admite varias torres a la vez (comparar):
// cada una es un modelo IFC abierto aparte y se coloca a la derecha de la anterior.
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import * as WebIFC from '../lib/web-ifc/web-ifc-api.js';
import { geometriaL } from './perfilesL.js';

const COLOR_SEL = 0xffd400;
const PALETA = ['#3b82c4', '#d9822b', '#8e5bd0', '#2ca58d', '#c44b4b', '#b8a038', '#5a8f29', '#c25b9c', '#4bb3c4', '#7a6a58'];

// Lleva una lista con scroll a una posición (fila resaltada desde el modelo).
export function desplazar(el, top) {
    if (!el) return;
    const cab = el.querySelector('thead')?.getBoundingClientRect().height || 0;
    if (top < el.scrollTop || top > el.scrollTop + el.clientHeight - cab - 40) el.scrollTop = Math.max(0, top - el.clientHeight / 3);
}

// marco: elemento que pasa a pantalla completa (visor + lista de materiales de la página).
export async function crear(contenedor, urlIfc, dotnet, catalogo, nombre, marco) {
    const v = new VisorIfc(contenedor, dotnet, marco);
    v.catalogo = catalogo || [];
    await v.agregar(urlIfc, nombre);
    return v;
}

const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const val = p => (p && typeof p === 'object' && 'value' in p) ? p.value : p;
const clave = (mid, id) => mid + ':' + id;

class VisorIfc {
    constructor(el, dotnet, marco) {
        this.el = el; this.dotnet = dotnet; this.marco = marco || el;
        this.extra = [];              // piezas resaltadas además de la seleccionada (fila de la lista)
        this.modelos = [];            // {mid, url, nombre, grupo, niveles, idTorre, etiqueta}
        this.mallas = new Map();      // 'mid:id' -> Mesh
        this.info = new Map();        // 'mid:id' -> {mid, id, nivel, perfil, leido, tipo, marca}
        this.sel = null;
        this.modo = 'nivel';
        this.medir = false; this.puntos = []; this.marcasMedida = [];
        this.rayos = false; this.mano = false;

        el.style.position = 'relative';
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.renderer.localClippingEnabled = true;
        el.appendChild(this.renderer.domElement);

        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1a1f27);
        this.scene.add(new THREE.HemisphereLight(0xffffff, 0x3a4250, 1.3));
        const d1 = new THREE.DirectionalLight(0xffffff, 1.8); d1.position.set(60, 120, 80); this.scene.add(d1);
        const d2 = new THREE.DirectionalLight(0xffffff, .6); d2.position.set(-80, 40, -60); this.scene.add(d2);

        this.camara = new THREE.PerspectiveCamera(40, 1, 0.05, 5000);
        this.controles = new OrbitControls(this.camara, this.renderer.domElement);
        this.controles.enableDamping = true;
        this.controles.screenSpacePanning = true;

        this.corte = new THREE.Plane(new THREE.Vector3(0, -1, 0), 1e6);
        this.raycaster = new THREE.Raycaster();

        this.crearUi();
        this.ro = new ResizeObserver(() => this.redimensionar());
        this.ro.observe(el);
        document.addEventListener('fullscreenchange', this._fs = () => { this.redimensionar(); this.marcarBoton(this.bFull, document.fullscreenElement === this.marco); });
        const cv = this.renderer.domElement;
        cv.addEventListener('pointerdown', e => { this.down = [e.clientX, e.clientY]; });
        cv.addEventListener('pointerup', e => {
            if (this.down && Math.hypot(e.clientX - this.down[0], e.clientY - this.down[1]) < 5) this.clic(e);
        });
        cv.addEventListener('dblclick', e => { const h = this.tocar(e); if (h) this.enfocar(h.object.userData.k); });
        const loop = () => { this.raf = requestAnimationFrame(loop); this.controles.update(); this.renderer.render(this.scene, this.camara); };
        loop();
    }

    // ---------- Carga de modelos ----------
    async api_() {
        if (this.api) return this.api;
        this.api = new WebIFC.IfcAPI();
        this.api.SetWasmPath(new URL('../lib/web-ifc/', import.meta.url).href, true);
        await this.api.Init();
        return this.api;
    }

    async agregar(url, nombre) {
        const api = await this.api_();
        this.estado(`Cargando ${nombre || url.split('/').pop()}…`);
        const datos = new Uint8Array(await (await fetch(url)).arrayBuffer());
        const mid = api.OpenModel(datos, { COORDINATE_TO_ORIGIN: false });
        const mod = { mid, url, nombre: nombre || '', niveles: [], idTorre: null, porPieza: new Map(), planos: new Map(), planoPieza: {}, asignadas: new Set() };
        await this.leerEstructura(mod);
        if (!mod.nombre && mod.idTorre) mod.nombre = val(api.GetLine(mid, mod.idTorre).Name) || 'Torre';

        // web-ifc entrega la geometría en metros (aplica la unidad del IFC) y con Y arriba.
        mod.grupo = new THREE.Group();
        const matBase = { side: THREE.DoubleSide, clippingPlanes: [this.corte], roughness: .45, metalness: .55 };
        api.StreamAllMeshes(mid, fm => {
            const id = fm.expressID, geos = [];
            let color = null;
            for (let i = 0; i < fm.geometries.size(); i++) {
                const pg = fm.geometries.get(i);
                const g = api.GetGeometry(mid, pg.geometryExpressID);
                const v = api.GetVertexArray(g.GetVertexData(), g.GetVertexDataSize());
                const ix = api.GetIndexArray(g.GetIndexData(), g.GetIndexDataSize());
                const pos = new Float32Array(v.length / 2), nor = new Float32Array(v.length / 2);
                for (let k = 0, j = 0; k < v.length; k += 6, j += 3) {
                    pos[j] = v[k]; pos[j + 1] = v[k + 1]; pos[j + 2] = v[k + 2];
                    nor[j] = v[k + 3]; nor[j + 1] = v[k + 4]; nor[j + 2] = v[k + 5];
                }
                const bg = new THREE.BufferGeometry();
                bg.setAttribute('position', new THREE.BufferAttribute(pos, 3));
                bg.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
                bg.setIndex(new THREE.BufferAttribute(ix, 1));
                bg.applyMatrix4(new THREE.Matrix4().fromArray(pg.flatTransformation));
                geos.push(bg);
                color = pg.color;
                g.delete();
            }
            if (!geos.length) return;
            const geo = geos.length === 1 ? geos[0] : unir(geos);
            const k = clave(mid, id);
            const mat = new THREE.MeshStandardMaterial({ ...matBase, color: new THREE.Color(color.x, color.y, color.z) });
            const mesh = new THREE.Mesh(geo, mat);
            mesh.userData.k = k;
            mesh.userData.colorIfc = mat.color.clone();
            mesh.userData.opacidadIfc = color.w;
            const aristas = new THREE.LineSegments(new THREE.EdgesGeometry(geo, 30),
                new THREE.LineBasicMaterial({ color: 0x0d1117, transparent: true, opacity: .3, clippingPlanes: [this.corte] }));
            mesh.add(aristas);
            mod.grupo.add(mesh);
            this.mallas.set(k, mesh);
            const inf = this.info.get(k) || { mid, id };
            this.info.set(k, inf);
        });

        // A la derecha del último modelo, con una separación de medio ancho.
        const caja = new THREE.Box3().setFromObject(mod.grupo);
        if (this.modelos.length) {
            const prev = new THREE.Box3();
            this.modelos.forEach(m => prev.expandByObject(m.grupo));
            const sep = Math.max(4, (caja.max.x - caja.min.x) * .5);
            mod.grupo.position.x = prev.max.x + sep - caja.min.x;
        }
        this.scene.add(mod.grupo);
        mod.etiqueta = etiqueta(mod.nombre);
        const c2 = new THREE.Box3().setFromObject(mod.grupo);
        mod.etiqueta.position.set((c2.min.x + c2.max.x) / 2, c2.max.y + Math.max(2, (c2.max.y - c2.min.y) * .05), (c2.min.z + c2.max.z) / 2);
        mod.etiqueta.scale.multiplyScalar(Math.max(1, (c2.max.y - c2.min.y) / 40));
        mod.etiqueta.visible = this.modelos.length > 0;
        this.scene.add(mod.etiqueta);
        this.modelos.push(mod);
        if (this.modelos.length > 1) this.modelos.forEach(m => m.etiqueta.visible = true);

        this.recalcularCaja();
        this.vista(this.modelos.length > 1 ? 'frente' : 'iso');
        this.pintar();
        this.panelArbol();
        this.panelTorre();
        this.listaComparar();
        this.estado('');
    }

    quitar(mid) {
        if (this.modelos.length <= 1) return;
        const i = this.modelos.findIndex(m => m.mid === mid); if (i < 0) return;
        const mod = this.modelos[i];
        if (this.sel && this.sel.startsWith(mid + ':')) this.seleccionar(null);
        this.scene.remove(mod.grupo); this.scene.remove(mod.etiqueta);
        mod.grupo.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) o.material.dispose(); });
        for (const k of [...this.mallas.keys()]) if (k.startsWith(mid + ':')) { this.mallas.delete(k); this.info.delete(k); }
        try { this.api.CloseModel(mid); } catch { }
        this.modelos.splice(i, 1);
        // Reacomodar los que quedan, de izquierda a derecha.
        let x = null;
        for (const m of this.modelos) {
            m.grupo.position.x = 0;
            const c = new THREE.Box3().setFromObject(m.grupo);
            if (x !== null) m.grupo.position.x = x - c.min.x;
            const c2 = new THREE.Box3().setFromObject(m.grupo);
            m.etiqueta.position.x = (c2.min.x + c2.max.x) / 2;
            x = c2.max.x + Math.max(4, (c2.max.x - c2.min.x) * .5);
            m.etiqueta.visible = this.modelos.length > 1;
        }
        this.recalcularCaja();
        this.vista(this.modelos.length > 1 ? 'frente' : 'iso');
        this.pintar(); this.panelArbol(); this.panelTorre(); this.listaComparar();
    }

    recalcularCaja() {
        this.caja = new THREE.Box3();
        this.modelos.forEach(m => this.caja.expandByObject(m.grupo));
        if (this.rejillaObj) { this.scene.remove(this.rejillaObj); this.rejillaObj.geometry.dispose(); }
        const s = this.caja.getSize(new THREE.Vector3()), c = this.caja.getCenter(new THREE.Vector3());
        const n = Math.ceil(Math.max(s.x, s.z) * 1.6 / 5) * 5 + 10;
        this.rejillaObj = new THREE.GridHelper(n, n / 5, 0x3a4658, 0x2a3240);
        this.rejillaObj.position.set(c.x, this.caja.min.y, c.z);
        this.scene.add(this.rejillaObj);
        this.corteSlider.max = String(Math.ceil(this.caja.max.y * 10) / 10);
        this.corteSlider.min = String(Math.floor(this.caja.min.y * 10) / 10);
        this.corteSlider.value = this.corteSlider.max;
        this.corte.constant = 1e6; this.txtCorte.textContent = '';
    }

    async leerEstructura(mod) {
        const api = this.api, mid = mod.mid;
        const arbol = await api.properties.getSpatialStructure(mid, false);
        const recorrer = (n, nivel) => {
            const tipo = typeof n.type === 'number' ? api.GetNameFromTypeCode(n.type) : String(n.type).toUpperCase();
            if (tipo === 'IFCBUILDINGSTOREY') { nivel = { id: n.expressID, nombre: '', ids: [] }; mod.niveles.push(nivel); }
            if (tipo === 'IFCBUILDING') mod.idTorre = n.expressID;
            if (tipo === 'IFCMEMBER' && nivel) { nivel.ids.push(n.expressID); this.info.set(clave(mid, n.expressID), { mid, id: n.expressID, nivel: nivel.id, nivelNombre: '' }); }
            (n.children || []).forEach(c => recorrer(c, nivel));
        };
        recorrer(arbol, null);
        for (const nv of mod.niveles) {
            nv.nombre = val(api.GetLine(mid, nv.id).Name) || ('Nivel ' + nv.id);
            nv.ids.forEach(id => { const inf = this.info.get(clave(mid, id)); if (inf) inf.nivelNombre = nv.nombre; });
        }
        // Perfil y estado por miembro (Pset SigWeb_Pieza)
        const rels = api.GetLineIDsWithType(mid, WebIFC.IFCRELDEFINESBYPROPERTIES);
        for (let i = 0; i < rels.size(); i++) {
            const r = api.GetLine(mid, rels.get(i));
            const ps = api.GetLine(mid, r.RelatingPropertyDefinition.value);
            if (val(ps.Name) !== 'SigWeb_Pieza') continue;
            const datos = {};
            for (const h of ps.HasProperties || []) { const p = api.GetLine(mid, h.value); datos[val(p.Name)] = val(p.NominalValue); }
            for (const o of r.RelatedObjects) {
                const k = clave(mid, o.value);
                const inf = this.info.get(k) || { mid, id: o.value };
                inf.perfil = datos.Perfil; inf.leido = datos.PerfilLeido === true || datos.PerfilLeido === 'T' || datos.PerfilLeido === '.T.';
                inf.tipo = datos.Tipo; inf.marca = datos.Marca; inf.pieza = datos.Id != null ? String(datos.Id) : null;
                if (inf.pieza) mod.porPieza.set(inf.pieza, k);
                this.info.set(k, inf);
            }
        }
    }

    // ---------- Colores ----------
    pintar() {
        const nombresNivel = [...new Set(this.modelos.flatMap(m => m.niveles.map(n => n.nombre)))];
        const colorNivel = new Map(nombresNivel.map((n, i) => [n, new THREE.Color(PALETA[i % PALETA.length])]));
        const perfiles = [...new Set([...this.info.values()].filter(x => x.leido).map(x => x.perfil))].sort(ordenPerfil);
        const colorPerfil = new Map(perfiles.map((p, i) => [p, new THREE.Color().setHSL((i * 0.618) % 1, .55, .55)]));
        for (const [k, m] of this.mallas) {
            const inf = this.info.get(k) || {};
            let c = m.userData.colorIfc;
            if (this.modo === 'nivel') c = colorNivel.get(inf.nivelNombre) || c;
            else if (this.modo === 'estado') c = new THREE.Color(inf.leido ? '#2ca58d' : '#d9822b');
            else if (this.modo === 'perfil') c = inf.leido ? colorPerfil.get(inf.perfil) : new THREE.Color('#555b66');
            m.material.color.copy(c);
            const transp = this.rayos ? .18 : (this.modo === 'ifc' && m.userData.opacidadIfc < 1 ? m.userData.opacidadIfc : 1);
            m.material.transparent = transp < 1; m.material.opacity = transp; m.material.depthWrite = transp >= 1;
            m.material.needsUpdate = true;
        }
        this.leyenda = [];
        if (this.modo === 'nivel') this.leyenda = nombresNivel.map((n, i) => [PALETA[i % PALETA.length], n]);
        if (this.modo === 'estado') this.leyenda = [['#2ca58d', 'Perfil leído (L)'], ['#d9822b', 'Perfil no leído']];
        if (this.modo === 'perfil') this.leyenda = [...perfiles.map(p => ['#' + colorPerfil.get(p).getHexString(), p]), ['#555b66', 'No leído']];
        if (this.sel) this.resaltar(this.sel);
        this.extra.forEach(k => this.resaltar(k));
        this.dibujarLeyenda();
    }

    resaltar(k) {
        const m = this.mallas.get(k);
        if (m) { m.material.color.set(COLOR_SEL); m.material.opacity = 1; m.material.transparent = false; m.material.depthWrite = true; }
    }

    // ---------- Interacción ----------
    tocar(e) {
        const r = this.renderer.domElement.getBoundingClientRect();
        this.raycaster.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), this.camara);
        const visibles = [...this.mallas.values()].filter(m => m.visible);
        return this.raycaster.intersectObjects(visibles, false).filter(h => this.corte.distanceToPoint(h.point) >= 0)[0] || null;
    }

    clic(e) {
        if (this.mano) return;
        const h = this.tocar(e);
        if (this.medir) { if (h) this.agregarPuntoMedida(h.point); return; }
        this.seleccionar(h ? h.object.userData.k : null);
    }

    async seleccionar(k, desdeLista) {
        const prev = this.sel, habiaExtra = this.extra.length > 0;
        this.sel = k;
        if (!desdeLista) this.extra = [];
        if (prev || habiaExtra) this.pintar();
        if (!desdeLista && this.dotnet) {
            const i = k ? this.info.get(k) : null, m = i ? this.modelos.find(x => x.mid === i.mid) : null;
            this.dotnet.invokeMethodAsync('AlSeleccionarPieza', m?.url ?? null, i?.pieza ?? null).catch(() => { });
        }
        if (!k) { this.ficha.style.display = 'none'; return; }
        this.resaltar(k);
        const { mid, id } = this.info.get(k) || {};
        const mod = this.modelos.find(m => m.mid === mid);
        const e = this.api.GetLine(mid, id);
        const psets = await this.api.properties.getPropertySets(mid, id, true);
        let html = `<div style="font-weight:600;font-size:13px;margin-bottom:4px">${esc(val(e.Name))}</div>`;
        if (this.modelos.length > 1) html += fila('Torre', mod?.nombre);
        html += fila('Clase IFC', this.api.GetNameFromTypeCode(e.type));
        html += fila('Tipo predefinido', val(e.PredefinedType));
        html += fila('GlobalId', val(e.GlobalId));
        html += fila('Tipo de objeto', val(e.ObjectType));
        html += fila('Tag (marca)', val(e.Tag));
        const inf = this.info.get(k), pl = mod && inf?.pieza ? mod.planos.get(mod.planoPieza[inf.pieza]) : null;
        if (inf?.manual) html += fila('Perfil asignado', `${inf.perfil} · marca ${inf.marca}`);
        if (pl) html += fila('Plano', pl.url
            ? `<a href="${esc(pl.url)}" target="_blank" rel="noopener" style="color:#5aa9ff;text-decoration:none" title="Abrir el plano">📄 ${esc(pl.texto)}</a>`
            : esc(pl.texto), true);
        for (const ps of psets) {
            html += `<div style="margin-top:8px;font-weight:600;color:#9fc3ff">${esc(val(ps.Name))}</div>`;
            for (const h of (ps.HasProperties || ps.Quantities || [])) {
                let v = val(h.NominalValue);
                if (v === undefined) v = val(h.LengthValue) ?? val(h.WeightValue);
                if (typeof v === 'number') v = (Math.round(v * 10) / 10).toLocaleString('es-CO');
                if (v === true || v === 'T') v = 'Sí'; if (v === false || v === 'F') v = 'No';
                html += fila(val(h.Name), v);
            }
        }
        this.ficha.innerHTML = `<button data-x style="float:right;background:none;border:0;color:#aaa;cursor:pointer" title="Cerrar">✕</button>` + html;
        this.ficha.querySelector('[data-x]').onclick = () => this.seleccionar(null);
        this.ficha.style.display = 'block';
    }

    // Acerca la cámara a un elemento (doble clic).
    enfocar(k) {
        const m = this.mallas.get(k);
        if (!m) return;
        const b = new THREE.Box3().setFromObject(m), c = b.getCenter(new THREE.Vector3());
        const r = Math.max(b.getSize(new THREE.Vector3()).length() * 1.3, 1.2);
        const dir = this.camara.position.clone().sub(this.controles.target).normalize();
        this.controles.target.copy(c);
        this.camara.position.copy(c).addScaledVector(dir, r);
        this.camara.near = 0.01; this.camara.updateProjectionMatrix();
    }

    agregarPuntoMedida(p) {
        const s = new THREE.Mesh(new THREE.SphereGeometry(.12, 12, 12), new THREE.MeshBasicMaterial({ color: COLOR_SEL, depthTest: false }));
        s.position.copy(p); s.renderOrder = 10; this.scene.add(s); this.marcasMedida.push(s);
        this.puntos.push(p.clone());
        if (this.puntos.length === 2) {
            const [a, b] = this.puntos;
            const ln = new THREE.Line(new THREE.BufferGeometry().setFromPoints([a, b]), new THREE.LineBasicMaterial({ color: COLOR_SEL, depthTest: false }));
            ln.renderOrder = 10; this.scene.add(ln); this.marcasMedida.push(ln);
            const d = a.distanceTo(b) * 1000;
            this.txtMedida.textContent = `Distancia: ${Math.round(d).toLocaleString('es-CO')} mm  (ΔX ${Math.round((b.x - a.x) * 1000)} · ΔY ${Math.round((b.y - a.y) * 1000)} · ΔZ ${Math.round((b.z - a.z) * 1000)})`;
            this.puntos = [];
        } else this.txtMedida.textContent = 'Elige el segundo punto';
    }

    limpiarMedida() {
        for (const o of this.marcasMedida) { this.scene.remove(o); o.geometry.dispose(); }
        this.marcasMedida = []; this.puntos = [];
        this.txtMedida.textContent = this.medir ? 'Elige el primer punto' : '';
    }

    // ---------- Vistas y modos ----------
    vista(tipo) {
        const c = this.caja.getCenter(new THREE.Vector3()), s = this.caja.getSize(new THREE.Vector3());
        const r = Math.max(s.x, s.y, s.z);
        const dist = r / (2 * Math.tan(THREE.MathUtils.degToRad(20))) * 1.15;
        const dir = { iso: [.9, .45, 1], frente: [0, 0, 1], lateral: [1, 0, 0], planta: [0, 1, 0.0001] }[tipo] || [.9, .45, 1];
        this.camara.position.copy(c).addScaledVector(new THREE.Vector3(...dir).normalize(), dist);
        this.controles.target.copy(c);
        this.camara.near = dist / 500; this.camara.far = dist * 10; this.camara.updateProjectionMatrix();
        this.controles.update();
    }

    // Mano: el botón izquierdo desplaza la vista en vez de girarla.
    alternarMano() {
        this.mano = !this.mano;
        this.controles.mouseButtons.LEFT = this.mano ? THREE.MOUSE.PAN : THREE.MOUSE.ROTATE;
        this.controles.touches.ONE = this.mano ? THREE.TOUCH.PAN : THREE.TOUCH.ROTATE;
        this.renderer.domElement.style.cursor = this.mano ? 'grab' : '';
        if (this.mano && this.medir) this.bMedir.click();
        this.marcarBoton(this.bMano, this.mano);
    }

    pantallaCompleta() {
        if (document.fullscreenElement) document.exitFullscreen();
        else this.marco.requestFullscreen?.();
    }

    // ---------- Lista de materiales (página) ----------
    habilitarLista(si) { if (this.bLista) this.bLista.style.display = si ? '' : 'none'; }

    setPlanos(url, enlaces, porPieza) {
        const mod = this.modelos.find(m => m.url === url) || this.modelos[0];
        if (!mod) return;
        mod.planos = new Map((enlaces || []).map(p => [p.clave, p]));
        mod.planoPieza = porPieza || {};
    }

    // Asignaciones manuales de la lista de materiales (Modelos 3D): cada pieza [{pieza, a, b, tipo, cara, marca, perfil}]
    // se redibuja como el ángulo L real de su perfil (misma regla de orientación que el generador del IFC).
    // Se reemplaza la geometría de la malla del IFC, así la pieza conserva selección, colores y grupos.
    aplicarAsignaciones(url, lista) {
        const mod = this.modelos.find(m => m.url === url) || this.modelos[0];
        if (!mod) return 0;
        for (const k of mod.asignadas) {
            const m = this.mallas.get(k), inf = this.info.get(k);
            if (!m || !m.userData.geoOriginal) continue;
            m.geometry.dispose(); m.geometry = m.userData.geoOriginal; delete m.userData.geoOriginal;
            this.aristas(m);
            if (inf && inf.original) { Object.assign(inf, inf.original); delete inf.original; delete inf.manual; }
        }
        mod.asignadas.clear();
        let n = 0;
        for (const it of lista || []) {
            const k = mod.porPieza.get(String(it.pieza)), m = k ? this.mallas.get(k) : null;
            if (!m || !it.a || !it.b) continue;
            const g = geometriaL({ miembros: [{ a: it.a, b: it.b, tipo: it.tipo, cara: it.cara, marca: it.marca }],
                lista_materiales: [{ marca: it.marca, perfil: it.perfil, clase: 'PERFIL' }] });
            if (!g.userData.conPerfil) { g.dispose(); continue; }   // perfil que no es un ángulo L: se deja como está
            m.userData.geoOriginal = m.geometry;
            m.geometry = g;
            this.aristas(m);
            const inf = this.info.get(k);
            if (inf) {
                inf.original = { perfil: inf.perfil, leido: inf.leido, marca: inf.marca };
                inf.perfil = it.perfil; inf.leido = true; inf.marca = it.marca; inf.manual = true;
            }
            mod.asignadas.add(k); n++;
        }
        this.pintar();
        if (this.sel) this.seleccionar(this.sel, true);
        return n;
    }

    aristas(m) {
        const ln = m.children.find(c => c.isLineSegments);
        if (ln) { ln.geometry.dispose(); ln.geometry = new THREE.EdgesGeometry(m.geometry, 30); }
    }

    // Selecciona y encuadra todas las piezas de una fila de la lista (Ids de pieza del Pset SigWeb_Pieza).
    async verPiezas(url, ids) {
        const mod = this.modelos.find(m => m.url === url) || this.modelos[0];
        if (!mod) return false;
        const ks = (ids || []).map(id => mod.porPieza.get(String(id))).filter(k => k && this.mallas.has(k));
        if (!ks.length) return false;
        // Si la pieza está en un grupo oculto, se vuelve a mostrar el grupo.
        for (const k of ks) {
            const inf = this.info.get(k), ms = this.mallas.get(k);
            if (ms.visible) continue;
            const ch = this.arbolEl.querySelector(`input[data-m="${inf.mid}"][data-n="${inf.nivel}"]`);
            if (ch && !ch.checked) { ch.checked = true; ch.onchange(); } else ms.visible = true;
        }
        if (this.corte.constant < 1e5) { this.corte.constant = 1e6; this.corteSlider.value = this.corteSlider.max; this.txtCorte.textContent = ''; }
        this.extra = ks.slice(1);
        await this.seleccionar(ks[0], true);
        this.extra.forEach(k => this.resaltar(k));
        this.enfocarVarios(ks);
        return true;
    }

    enfocarVarios(ks) {
        const b = new THREE.Box3();
        ks.forEach(k => { const m = this.mallas.get(k); if (m) b.expandByObject(m); });
        if (b.isEmpty()) return;
        const c = b.getCenter(new THREE.Vector3());
        const r = Math.max(b.getSize(new THREE.Vector3()).length() * 1.3, 1.5);
        const dir = this.camara.position.clone().sub(this.controles.target).normalize();
        this.controles.target.copy(c);
        this.camara.position.copy(c).addScaledVector(dir, r);
        this.camara.near = 0.01; this.camara.updateProjectionMatrix();
    }

    marcarBoton(b, on) { if (b) b.style.borderColor = on ? '#ffd400' : '#3a4658'; }

    redimensionar() {
        const w = this.el.clientWidth, h = this.el.clientHeight;
        if (!w || !h) return;
        this.renderer.setSize(w, h);
        this.camara.aspect = w / h; this.camara.updateProjectionMatrix();
        // La barra se parte en dos filas cuando el visor es angosto (p. ej. con la lista abierta).
        if (this.barra && this.arbolEl) this.arbolEl.style.top = (this.barra.offsetTop + this.barra.offsetHeight + 8) + 'px';
    }

    estado(t) { if (this.txtEstado) { this.txtEstado.textContent = t; this.txtEstado.style.display = t ? 'block' : 'none'; } }

    // ---------- Interfaz sobre el lienzo ----------
    crearUi() {
        const panel = 'position:absolute;background:rgba(18,22,30,.92);color:#e8edf3;border:1px solid #3a4658;border-radius:6px;font:12px/1.4 sans-serif;z-index:6;';
        const btn = 'background:rgba(18,22,30,.92);color:#e8edf3;border:1px solid #3a4658;border-radius:5px;padding:4px 9px;font:12px sans-serif;cursor:pointer;';

        this.barra = document.createElement('div');
        this.barra.style.cssText = 'position:absolute;left:10px;top:10px;display:flex;gap:4px;flex-wrap:wrap;z-index:7;max-width:calc(100% - 330px)';
        const b = (txt, title, fn) => { const x = document.createElement('button'); x.type = 'button'; x.textContent = txt; x.title = title; x.style.cssText = btn; x.onclick = fn; this.barra.appendChild(x); return x; };
        b('⌂', 'Vista isométrica', () => this.vista('iso'));
        b('F', 'Vista de frente (cara transversal)', () => this.vista('frente'));
        b('L', 'Vista lateral (cara longitudinal)', () => this.vista('lateral'));
        b('P', 'Vista en planta', () => this.vista('planta'));
        this.bMano = b('✋', 'Mano: desplazar la vista con el botón izquierdo', () => this.alternarMano());
        this.bFull = b('⛶', 'Pantalla completa', () => this.pantallaCompleta());
        this.selModo = document.createElement('select');
        this.selModo.title = 'Colorear por';
        this.selModo.style.cssText = btn;
        [['nivel', 'Color: grupo'], ['perfil', 'Color: perfil'], ['estado', 'Color: perfil leído'], ['ifc', 'Color: IFC']].forEach(([v, t]) => { const o = document.createElement('option'); o.value = v; o.textContent = t; this.selModo.appendChild(o); });
        this.selModo.onchange = () => { this.modo = this.selModo.value; this.pintar(); };
        this.barra.appendChild(this.selModo);
        const bx = b('◫', 'Transparencia (rayos X)', () => { this.rayos = !this.rayos; this.marcarBoton(bx, this.rayos); this.pintar(); });
        this.bMedir = b('📏', 'Medir distancia entre dos puntos', () => {
            this.medir = !this.medir; this.marcarBoton(this.bMedir, this.medir);
            if (this.medir && this.mano) this.alternarMano();
            this.limpiarMedida();
        });
        b('✕📏', 'Borrar medidas', () => this.limpiarMedida());

        const corte = document.createElement('label');
        corte.title = 'Plano de corte horizontal';
        corte.style.cssText = btn + 'display:flex;align-items:center;gap:6px';
        corte.innerHTML = '✂ <input type="range" step="0.1" style="width:120px">';
        this.corteSlider = corte.querySelector('input');
        this.corteSlider.oninput = () => { this.corte.constant = parseFloat(this.corteSlider.value); this.txtCorte.textContent = `${(+this.corteSlider.value).toFixed(1)} m`; };
        this.txtCorte = document.createElement('span'); this.txtCorte.style.minWidth = '44px';
        corte.appendChild(this.txtCorte);
        this.barra.appendChild(corte);

        // Comparar: «+» abre la lista de torres y agrega la elegida al lado.
        this.bComparar = b('+', 'Agregar otra torre para comparar', () => this.alternarMenuComparar());
        this.bComparar.style.fontWeight = '700';
        this.menuComparar = document.createElement('div');
        this.menuComparar.style.cssText = panel + 'display:none;max-height:60%;overflow:auto;padding:4px 0;min-width:170px;z-index:30;background:#161b24';
        this.el.appendChild(this.menuComparar);
        this._fueraMenu = e => { if (this.menuComparar.style.display !== 'none' && !this.menuComparar.contains(e.target) && e.target !== this.bComparar) this.menuComparar.style.display = 'none'; };
        document.addEventListener('pointerdown', this._fueraMenu, true);

        // Lista de materiales (la dibuja la página al lado del visor; se ve también en pantalla completa).
        this.bLista = b('', 'Lista de materiales', () => this.dotnet?.invokeMethodAsync('AlternarLista').catch(() => { }));
        this.bLista.innerHTML = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align:-2px"><path d="M8 6h12M8 12h12M8 18h12"/><circle cx="4" cy="6" r="1"/><circle cx="4" cy="12" r="1"/><circle cx="4" cy="18" r="1"/></svg>';
        this.bLista.style.display = 'none';
        this.el.appendChild(this.barra);

        this.txtMedida = document.createElement('div');
        this.txtMedida.style.cssText = 'position:absolute;left:50%;transform:translateX(-50%);bottom:12px;color:#ffd400;font:13px sans-serif;text-shadow:0 1px 2px #000;z-index:6';
        this.el.appendChild(this.txtMedida);

        this.txtEstado = document.createElement('div');
        this.txtEstado.style.cssText = panel + 'left:50%;top:50%;transform:translate(-50%,-50%);padding:8px 14px;display:none;font-size:13px';
        this.el.appendChild(this.txtEstado);

        this.ficha = document.createElement('div');
        this.ficha.style.cssText = panel + 'right:10px;top:10px;width:300px;max-height:calc(100% - 20px);overflow:auto;padding:10px 12px;display:none';
        this.el.appendChild(this.ficha);

        this.arbolEl = document.createElement('div');
        this.arbolEl.style.cssText = panel + 'left:10px;top:52px;width:250px;max-height:45%;overflow:auto;padding:6px 8px';
        this.el.appendChild(this.arbolEl);

        this.leyendaEl = document.createElement('div');
        this.leyendaEl.style.cssText = panel + 'left:10px;bottom:10px;max-width:240px;max-height:35%;overflow:auto;padding:6px 8px';
        this.el.appendChild(this.leyendaEl);

        this.torreEl = document.createElement('div');
        this.torreEl.style.cssText = panel + 'right:10px;bottom:10px;width:300px;max-height:40%;overflow:auto;padding:8px 10px;display:none';
        this.el.appendChild(this.torreEl);
    }

    listaComparar() {
        this.bComparar.style.display = this.catalogo.length ? '' : 'none';
        if (this.menuComparar.style.display !== 'none') this.dibujarMenuComparar();
    }

    alternarMenuComparar() {
        if (this.menuComparar.style.display !== 'none') { this.menuComparar.style.display = 'none'; return; }
        this.dibujarMenuComparar();
        const rb = this.bComparar.getBoundingClientRect(), re = this.el.getBoundingClientRect();
        this.menuComparar.style.left = (rb.left - re.left) + 'px';
        this.menuComparar.style.top = (rb.bottom - re.top + 4) + 'px';
        this.menuComparar.style.display = 'block';
    }

    dibujarMenuComparar() {
        const ya = new Set(this.modelos.map(m => m.url));
        const fams = [...new Set(this.catalogo.map(c => c.familia || ''))];
        let html = '';
        for (const f of fams) {
            if (f) html += `<div style="padding:4px 10px 2px;color:#9fc3ff;font-weight:600">${esc(f)}</div>`;
            this.catalogo.forEach((c, i) => {
                if ((c.familia || '') !== f) return;
                const usado = ya.has(c.url);
                html += `<div data-i="${i}" style="padding:3px 18px;cursor:${usado ? 'default' : 'pointer'};opacity:${usado ? .4 : 1}">${esc(c.nombre)}</div>`;
            });
        }
        this.menuComparar.innerHTML = html;
        this.menuComparar.querySelectorAll('[data-i]').forEach(d => {
            const it = this.catalogo[+d.dataset.i];
            if (ya.has(it.url)) return;
            d.onmouseenter = () => d.style.background = 'rgba(255,255,255,.08)';
            d.onmouseleave = () => d.style.background = '';
            d.onclick = async () => {
                this.menuComparar.style.display = 'none';
                this.bComparar.disabled = true;
                try { await this.agregar(it.url, it.nombre); } catch (e) { this.estado('No se pudo cargar: ' + (e?.message || e)); }
                this.bComparar.disabled = false;
            };
        });
    }

    panelArbol() {
        let html = `<div style="font-weight:600;margin-bottom:4px">Estructura IFC</div>`;
        for (const m of this.modelos) {
            const total = m.niveles.reduce((s, n) => s + n.ids.length, 0);
            html += `<div style="display:flex;align-items:center;gap:6px;margin-top:4px;color:#9fc3ff;font-weight:600">${esc(m.nombre)}` +
                `<span style="color:#9aa7b8;font-weight:400;margin-left:auto">${total}</span>` +
                (this.modelos.length > 1 ? `<button data-q="${m.mid}" title="Quitar de la comparación" style="background:none;border:0;color:#ff9a9a;cursor:pointer">✕</button>` : '') + `</div>`;
            for (const n of m.niveles)
                html += `<label style="display:flex;gap:6px;align-items:center;cursor:pointer;padding-left:6px"><input type="checkbox" data-m="${m.mid}" data-n="${n.id}" checked> ${esc(n.nombre)} <span style="color:#9aa7b8;margin-left:auto">${n.ids.length}</span></label>`;
        }
        this.arbolEl.innerHTML = html;
        this.arbolEl.querySelectorAll('input[data-n]').forEach(ch => ch.onchange = () => {
            const m = this.modelos.find(x => x.mid === +ch.dataset.m);
            const n = m.niveles.find(x => x.id === +ch.dataset.n);
            n.ids.forEach(id => { const ms = this.mallas.get(clave(m.mid, id)); if (ms) ms.visible = ch.checked; });
        });
        this.arbolEl.querySelectorAll('button[data-q]').forEach(bq => bq.onclick = () => this.quitar(+bq.dataset.q));
    }

    async panelTorre() {
        let html = `<button data-x style="float:right;background:none;border:0;color:#aaa;cursor:pointer" title="Cerrar">✕</button>`;
        for (const m of this.modelos) {
            if (!m.idTorre) continue;
            const psets = await this.api.properties.getPropertySets(m.mid, m.idTorre, true);
            const ps = psets.find(p => val(p.Name) === 'SigWeb_Torre');
            if (!ps) continue;
            const d = {};
            (ps.HasProperties || []).forEach(h => d[val(h.Name)] = val(h.NominalValue));
            const e = this.api.GetLine(m.mid, m.idTorre);
            html += `<div style="font-weight:600;font-size:13px;margin-top:4px">${esc(val(e.Name))}</div>`;
            html += fila('Alto total', `${(+d.AltoTotal_mm).toLocaleString('es-CO')} mm`);
            html += fila('Ancho base', `${(+d.AnchoBase_mm).toLocaleString('es-CO')} mm`);
            html += fila('Miembros', d.Miembros);
            html += fila('Con perfil L', d.MiembrosConPerfil);
            html += fila('Peso perfiles leídos', `${(+d.PesoPerfilesLeidos_kg).toLocaleString('es-CO')} kg`);
            if (this.modelos.length === 1) {
                const sup = Object.keys(d).filter(k => k.startsWith('Supuesto')).map(k => d[k]);
                const vali = Object.keys(d).filter(k => k.startsWith('Validacion')).map(k => d[k]);
                if (vali.length) html += `<div style="margin-top:6px;font-weight:600;color:#9fc3ff">Validación</div>` + vali.map(x => `<div>• ${esc(x)}</div>`).join('');
                if (sup.length) html += `<div style="margin-top:6px;font-weight:600;color:#ffb86b">Supuestos</div>` + sup.map(x => `<div>• ${esc(x)}</div>`).join('');
            }
        }
        this.torreEl.innerHTML = html;
        this.torreEl.querySelector('[data-x]').onclick = () => this.torreEl.style.display = 'none';
    }

    alternarTorre() {
        this.torreEl.style.display = this.torreEl.style.display === 'none' ? 'block' : 'none';
    }

    dibujarLeyenda() {
        if (!this.leyenda.length) { this.leyendaEl.style.display = 'none'; return; }
        this.leyendaEl.style.display = 'block';
        this.leyendaEl.innerHTML = this.leyenda.map(([c, t]) =>
            `<div style="display:flex;gap:6px;align-items:center"><span style="width:10px;height:10px;border-radius:2px;background:${c};display:inline-block"></span>${esc(t)}</div>`).join('');
    }

    dispose() {
        cancelAnimationFrame(this.raf);
        this.ro.disconnect();
        document.removeEventListener('fullscreenchange', this._fs);
        if (document.fullscreenElement === this.marco) document.exitFullscreen();
        document.removeEventListener('pointerdown', this._fueraMenu, true);
        this.scene.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) { o.material.map?.dispose(); o.material.dispose(); } });
        this.renderer.dispose();
        for (const m of this.modelos) { try { this.api.CloseModel(m.mid); } catch { } }
        this.el.innerHTML = '';
    }
}

// Rótulo con el nombre de la torre (sprite que siempre mira a la cámara).
function etiqueta(texto) {
    const c = document.createElement('canvas'), ctx = c.getContext('2d');
    const f = 48; ctx.font = `600 ${f}px sans-serif`;
    const w = Math.ceil(ctx.measureText(texto).width) + 40;
    c.width = w; c.height = f + 28;
    ctx.font = `600 ${f}px sans-serif`;
    ctx.fillStyle = 'rgba(18,22,30,.85)'; ctx.fillRect(0, 0, c.width, c.height);
    ctx.fillStyle = '#ffd400'; ctx.textBaseline = 'middle'; ctx.fillText(texto, 20, c.height / 2);
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(c), depthTest: false }));
    s.scale.set(w / c.height * 2, 2, 1);
    s.renderOrder = 20;
    return s;
}

function fila(k, v, html) {
    if (v === undefined || v === null || v === '') return '';
    return `<div style="display:flex;gap:8px;border-bottom:1px solid #2a3240;padding:1px 0"><span style="color:#9aa7b8;min-width:110px">${esc(k)}</span><span style="word-break:break-word">${html ? v : esc(v)}</span></div>`;
}

function ordenPerfil(a, b) {
    const n = s => (String(s).match(/\d+/g) || []).map(Number);
    const x = n(a), y = n(b);
    return (x[0] - y[0]) || (x[1] - y[1]) || String(a).localeCompare(String(b));
}

function unir(geos) {
    let nv = 0, ni = 0;
    geos.forEach(g => { nv += g.attributes.position.count; ni += g.index.count; });
    const pos = new Float32Array(nv * 3), nor = new Float32Array(nv * 3), ix = new Uint32Array(ni);
    let ov = 0, oi = 0;
    for (const g of geos) {
        pos.set(g.attributes.position.array, ov * 3); nor.set(g.attributes.normal.array, ov * 3);
        const gi = g.index.array; for (let k = 0; k < gi.length; k++) ix[oi + k] = gi[k] + ov;
        ov += g.attributes.position.count; oi += gi.length; g.dispose();
    }
    const r = new THREE.BufferGeometry();
    r.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    r.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
    r.setIndex(new THREE.BufferAttribute(ix, 1));
    return r;
}
