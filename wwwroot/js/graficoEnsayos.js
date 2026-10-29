// Interop con Chart.js para el panel "📈 Gráfico" de Ensayos
// (Components/Pages/GraficoEnsayos.razor). Guarda una instancia de Chart
// por cada <canvas>, identificada por su id, para poder destruirla y
// recrearla cada vez que el usuario cambia filtros, campos o tipo de gráfico.
window.sigwebGraficos = window.sigwebGraficos || {};

// Etiquetas de datos (valor sobre cada barra / punto / porción), activables por gráfico.
window.sigwebPluginEtiquetas = {
    id: 'sigwebEtiquetas',
    afterDatasetsDraw(chart) {
        const ctx = chart.ctx;
        const horizontal = chart.options.indexAxis === 'y';
        ctx.save();
        ctx.font = '11px sans-serif';
        const texto = getComputedStyle(chart.canvas).color || '#222';
        ctx.fillStyle = texto;
        chart.data.datasets.forEach((ds, i) => {
            if (!chart.isDatasetVisible(i) || !ds.sigwebEtiquetas) return;
            const meta = chart.getDatasetMeta(i);
            meta.data.forEach((el, k) => {
                const v = ds.data[k];
                if (v === null || v === undefined || isNaN(v)) return;
                const txt = Number(v).toLocaleString('es-CO', { maximumFractionDigits: 2 });
                const pos = el.tooltipPosition ? el.tooltipPosition() : { x: el.x, y: el.y };
                if (meta.type === 'pie' || meta.type === 'doughnut') {
                    ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillStyle = '#fff';
                    ctx.fillText(txt, pos.x, pos.y);
                } else if (horizontal && meta.type === 'bar') {
                    ctx.textAlign = 'left'; ctx.textBaseline = 'middle'; ctx.fillStyle = texto;
                    ctx.fillText(txt, pos.x + 4, pos.y);
                } else {
                    ctx.textAlign = 'center'; ctx.textBaseline = 'bottom'; ctx.fillStyle = texto;
                    ctx.fillText(txt, pos.x, pos.y - 4);
                }
            });
        });
        ctx.restore();
    }
};

// Textos, ejes y cuadrícula toman los colores del tema activo (Temas).
function sigwebColoresTema(el) {
    const cs = getComputedStyle(el);
    const texto = cs.color || '#222';
    const borde = getComputedStyle(document.documentElement).getPropertyValue('--bs-border-color').trim() || 'rgba(0,0,0,.1)';
    if (window.Chart) {
        Chart.defaults.color = texto;
        Chart.defaults.borderColor = borde;
        Chart.defaults.font.family = cs.fontFamily;
    }
}

// Configuración tal como llegó del servidor (sin los ajustes del botón "+"), para volver a dibujar.
window.sigwebConfigOriginal = window.sigwebConfigOriginal || {};

window.sigwebRenderGrafico = (canvasId, config) => {
    try { window.sigwebConfigOriginal[canvasId] = JSON.parse(JSON.stringify(config)); } catch { }
    sigwebAplicarElementos(canvasId, config);
    sigwebLeyendaLineas(config);
    // Espacio arriba para que la etiqueta de la barra más alta no se corte.
    if (!sigwebEsTorta(config) && ((config.data && config.data.datasets) || []).some(d => d.sigwebEtiquetas)) {
        config.options = config.options || {};
        const lay = config.options.layout = config.options.layout || {};
        if (lay.padding === undefined) lay.padding = { top: 18 };
    }
    config.plugins = (config.plugins || []).concat([window.sigwebPluginEtiquetas]);
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    sigwebColoresTema(ctx);

    if (window.sigwebGraficos[canvasId]) {
        window.sigwebGraficos[canvasId].destroy();
        delete window.sigwebGraficos[canvasId];
    }
    // Blazor puede reutilizar el mismo <canvas> para otro gráfico (al filtrar): Chart.js no deja
    // dibujar sobre un lienzo ocupado, así que se suelta el gráfico anterior, sea cual sea su id.
    const previo = window.Chart && Chart.getChart(ctx);
    if (previo) {
        Object.keys(window.sigwebGraficos).forEach(k => { if (window.sigwebGraficos[k] === previo) delete window.sigwebGraficos[k]; });
        previo.destroy();
    }

    window.sigwebGraficos[canvasId] = new Chart(ctx, config);
};

// Leyenda de gráficos con líneas (meta, plazo, tendencias): la línea se ve como un trazo (punteado si la
// serie lo es) y no como un cuadro relleno; barras = cuadro, dispersión = punto.
function sigwebLeyendaLineas(cfg) {
    if (sigwebEsTorta(cfg) || !window.Chart) return;
    const datasets = (cfg.data && cfg.data.datasets) || [];
    const esLinea = (d) => (d.type || cfg.type) === 'line' && d.showLine !== false;
    if (!datasets.some(esLinea)) return;
    cfg.options = cfg.options || {};
    cfg.options.plugins = cfg.options.plugins || {};
    const lg = cfg.options.plugins.legend = cfg.options.plugins.legend || {};
    const lb = lg.labels = Object.assign({}, lg.labels);
    lb.usePointStyle = true;
    lb.pointStyleWidth = lb.pointStyleWidth || 28;
    lb.generateLabels = (chart) => Chart.defaults.plugins.legend.labels.generateLabels(chart).map(it => {
        const d = chart.data.datasets[it.datasetIndex] || {};
        const tipo = d.type || chart.config.type;
        if (tipo === 'line' && d.showLine !== false) {
            const c = Array.isArray(d.borderColor) ? d.borderColor[0] : d.borderColor;
            it.pointStyle = 'line';
            it.strokeStyle = c || it.strokeStyle;
            it.fillStyle = c || it.fillStyle;
            it.lineWidth = Math.max(2, d.borderWidth || 2);
            it.lineDash = d.borderDash || [];
        } else {
            it.pointStyle = tipo === 'line' ? 'circle' : 'rectRounded';
            it.lineWidth = 0;
            it.lineDash = [];
        }
        return it;
    });
}

// ---------- Botón "+" de cada gráfico: elementos del gráfico (como en Excel) ----------
// Título, leyenda (y su posición), etiquetas de datos, ejes, títulos de ejes y líneas de cuadrícula.
// El botón se pone en el marcado: <button class="gx-mas" data-grafico="{canvasId}" data-clave="{clave}"
// onclick="sigwebElementosGrafico(this)">. Lo elegido se recuerda en este navegador por clave
// (la misma para ese gráfico en todas las sesiones) y se aplica cada vez que el gráfico se dibuja.
const sigwebElemClave = (canvasId) => {
    const b = document.querySelector(`[data-grafico="${canvasId}"]`);
    return 'sigweb.grafico.' + ((b && b.dataset.clave) || canvasId);
};
const sigwebElemLeer = (canvasId) => { try { return JSON.parse(localStorage.getItem(sigwebElemClave(canvasId)) || 'null'); } catch { return null; } };
const sigwebElemGuardar = (canvasId, v) => { try { localStorage.setItem(sigwebElemClave(canvasId), JSON.stringify(v)); } catch { } };
const sigwebEsTorta = (cfg) => cfg.type === 'pie' || cfg.type === 'doughnut' || cfg.type === 'polarArea';

function sigwebAplicarElementos(canvasId, cfg) {
    const a = sigwebElemLeer(canvasId);
    if (!a) return;
    cfg.options = cfg.options || {};
    const o = cfg.options;
    o.plugins = o.plugins || {};
    if (a.titulo !== undefined) o.plugins.title = { display: !!a.titulo, text: a.tituloTexto || '', font: { size: 13, weight: '600' }, padding: { bottom: 8 } };
    if (a.leyenda !== undefined) o.plugins.legend = Object.assign({}, o.plugins.legend, { display: !!a.leyenda, position: a.leyendaPos || 'bottom' });
    if (a.etiquetas !== undefined) ((cfg.data && cfg.data.datasets) || []).forEach(ds => ds.sigwebEtiquetas = !!a.etiquetas);
    // Por serie (etiquetas y visibilidad independientes); manda sobre lo general.
    if (a.series && !sigwebEsTorta(cfg)) ((cfg.data && cfg.data.datasets) || []).forEach(ds => {
        const p = a.series[ds.label];
        if (!p) return;
        if (p.etiquetas !== undefined) ds.sigwebEtiquetas = !!p.etiquetas;
        if (p.oculta) ds.hidden = true;
    });
    if (sigwebEsTorta(cfg)) return;
    o.scales = o.scales || {};
    if (!Object.keys(o.scales).length) { o.scales.x = {}; o.scales.y = {}; }
    const horizontal = o.indexAxis === 'y';
    Object.keys(o.scales).forEach(k => {
        const sc = o.scales[k] = o.scales[k] || {};
        const esX = k.startsWith('x');
        if (a.ejes !== undefined) sc.display = !!a.ejes;
        if (a.cuadricula !== undefined) sc.grid = Object.assign({}, sc.grid, { display: !!a.cuadricula });
        if (a.titulosEjes !== undefined) {
            // Eje de categorías / eje de valores (en barras horizontales se invierten).
            const texto = (esX !== horizontal) ? (a.tituloCategorias || '') : (a.tituloValores || '');
            sc.title = { display: !!a.titulosEjes && texto !== '', text: texto };
        }
    });
}

// Estado que muestra el panel: lo guardado o, si no hay nada, lo que tiene el gráfico ahora.
function sigwebElemEstado(canvasId) {
    const g = window.sigwebGraficos[canvasId];
    const o = (g && g.options) || {};
    const pl = o.plugins || {};
    const sc = o.scales || {};
    const primer = sc.x || sc.y || {};
    const actual = {
        titulo: !!(pl.title && pl.title.display), tituloTexto: (pl.title && pl.title.text) || '',
        leyenda: !(pl.legend && pl.legend.display === false), leyendaPos: (pl.legend && pl.legend.position) || 'top',
        etiquetas: !!(g && g.data.datasets.some(d => d.sigwebEtiquetas)),
        ejes: primer.display !== false,
        titulosEjes: !!(primer.title && primer.title.display), tituloCategorias: '', tituloValores: '',
        cuadricula: Object.values(sc).some(s => !s.grid || s.grid.display !== false)
    };
    return Object.assign(actual, sigwebElemLeer(canvasId) || {});
}

function sigwebElemRedibujar(canvasId) {
    const orig = window.sigwebConfigOriginal[canvasId];
    if (orig) window.sigwebRenderGrafico(canvasId, JSON.parse(JSON.stringify(orig)));
}

window.sigwebElementosGrafico = (boton) => {
    const canvasId = boton.dataset.grafico;
    let pop = document.getElementById('gx-elementos');
    if (pop && pop.dataset.grafico === canvasId) { pop.remove(); return; }
    if (pop) pop.remove();
    const g = window.sigwebGraficos[canvasId];
    if (!g) return;
    const torta = sigwebEsTorta(g.config);
    const e = sigwebElemEstado(canvasId);
    const esc = (t) => String(t || '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
    // Opciones de cada serie (solo si hay más de una y no es torta).
    const datasets = torta ? [] : g.data.datasets;
    const colorDe = (ds) => { const c = ds.borderColor || ds.backgroundColor; return Array.isArray(c) ? c[0] : (c || '#888'); };
    const pref = e.series || {};
    const seriesHtml = datasets.length < 2 ? '' : `
        <div class="gxe-sep">Series</div>
        <div class="gxe-series">
        ${datasets.map((ds, i) => {
            const p = pref[ds.label] || {};
            const etq = p.etiquetas !== undefined ? p.etiquetas : !!ds.sigwebEtiquetas;
            return `<div class="gxe-serie">
                <span class="gxe-punto" style="background:${esc(colorDe(ds))}"></span>
                <span class="gxe-nom" title="${esc(ds.label)}">${esc(ds.label) || 'Serie ' + (i + 1)}</span>
                <label class="gxe-mini" title="Etiquetas de datos de esta serie"><input type="checkbox" data-serie="${i}" data-sk="etiquetas" ${etq ? 'checked' : ''}> Etiq.</label>
                <label class="gxe-mini" title="Mostrar esta serie"><input type="checkbox" data-serie="${i}" data-sk="visible" ${p.oculta ? '' : 'checked'}> Ver</label>
            </div>`;
        }).join('')}
        </div>`;
    pop = document.createElement('div');
    pop.id = 'gx-elementos';
    pop.dataset.grafico = canvasId;
    pop.innerHTML = `
        <div class="gxe-tit">Elementos del gráfico</div>
        <label class="gxe-op"><input type="checkbox" data-k="titulo" ${e.titulo ? 'checked' : ''}> Título</label>
        <input class="form-control form-control-sm gxe-txt" data-k="tituloTexto" placeholder="Título del gráfico" value="${esc(e.tituloTexto)}">
        <label class="gxe-op"><input type="checkbox" data-k="leyenda" ${e.leyenda ? 'checked' : ''}> Leyenda</label>
        <select class="form-select form-select-sm gxe-txt" data-k="leyendaPos">
            ${[['top', 'Arriba'], ['bottom', 'Abajo'], ['left', 'Izquierda'], ['right', 'Derecha']].map(([v, n]) => `<option value="${v}" ${e.leyendaPos === v ? 'selected' : ''}>${n}</option>`).join('')}
        </select>
        <label class="gxe-op"><input type="checkbox" data-k="etiquetas" ${e.etiquetas ? 'checked' : ''}> Etiquetas de datos</label>
        ${torta ? '' : `
        <label class="gxe-op"><input type="checkbox" data-k="ejes" ${e.ejes ? 'checked' : ''}> Ejes</label>
        <label class="gxe-op"><input type="checkbox" data-k="titulosEjes" ${e.titulosEjes ? 'checked' : ''}> Títulos de ejes</label>
        <input class="form-control form-control-sm gxe-txt" data-k="tituloCategorias" placeholder="Eje de categorías" value="${esc(e.tituloCategorias)}">
        <input class="form-control form-control-sm gxe-txt" data-k="tituloValores" placeholder="Eje de valores" value="${esc(e.tituloValores)}">
        <label class="gxe-op"><input type="checkbox" data-k="cuadricula" ${e.cuadricula ? 'checked' : ''}> Líneas de cuadrícula</label>`}
        ${seriesHtml}
        <div class="gxe-pie"><button type="button" class="gxe-restablecer" title="Volver a como venía el gráfico">Restablecer</button></div>`;
    document.body.appendChild(pop);
    const r = boton.getBoundingClientRect();
    const ancho = datasets.length < 2 ? 230 : 270;
    pop.style.width = ancho + 'px';
    pop.style.left = Math.max(8, Math.min(r.right - ancho, window.innerWidth - ancho - 8)) + 'px';
    pop.style.top = Math.min(r.bottom + 6, window.innerHeight - pop.offsetHeight - 8) + 'px';
    const aplicarSerie = (el) => {
        const v = sigwebElemEstado(canvasId);
        const ds = datasets[+el.dataset.serie];
        if (!ds) return;
        v.series = v.series || {};
        const p = v.series[ds.label] = v.series[ds.label] || {};
        if (el.dataset.sk === 'etiquetas') p.etiquetas = el.checked; else p.oculta = !el.checked;
        sigwebElemGuardar(canvasId, v);
        sigwebElemRedibujar(canvasId);
    };
    const aplicar = (el) => {
        const v = sigwebElemEstado(canvasId);
        v[el.dataset.k] = el.type === 'checkbox' ? el.checked : el.value;
        // "Etiquetas de datos" general: vale para todas las series (quita lo puesto serie por serie).
        if (el.dataset.k === 'etiquetas' && v.series) {
            Object.values(v.series).forEach(p => delete p.etiquetas);
            pop.querySelectorAll('[data-sk=etiquetas]').forEach(c => c.checked = el.checked);
        }
        // Escribir un texto enciende su elemento.
        if (el.dataset.k === 'tituloTexto' && el.value) { v.titulo = true; pop.querySelector('[data-k=titulo]').checked = true; }
        if ((el.dataset.k === 'tituloCategorias' || el.dataset.k === 'tituloValores') && el.value) { v.titulosEjes = true; const c = pop.querySelector('[data-k=titulosEjes]'); if (c) c.checked = true; }
        if (el.dataset.k === 'leyendaPos') { v.leyenda = true; pop.querySelector('[data-k=leyenda]').checked = true; }
        sigwebElemGuardar(canvasId, v);
        sigwebElemRedibujar(canvasId);
    };
    pop.addEventListener('change', ev => { if (ev.target.dataset.sk) aplicarSerie(ev.target); else if (ev.target.dataset.k) aplicar(ev.target); });
    pop.addEventListener('input', ev => { if (ev.target.classList.contains('gxe-txt') && ev.target.tagName === 'INPUT') aplicar(ev.target); });
    pop.querySelector('.gxe-restablecer').addEventListener('click', () => {
        try { localStorage.removeItem(sigwebElemClave(canvasId)); } catch { }
        pop.remove();
        sigwebElemRedibujar(canvasId);
    });
    setTimeout(() => document.addEventListener('pointerdown', function cerrar(ev) {
        if (!document.getElementById('gx-elementos')) { document.removeEventListener('pointerdown', cerrar); return; }
        if (!pop.contains(ev.target) && ev.target !== boton && !boton.contains(ev.target)) { pop.remove(); document.removeEventListener('pointerdown', cerrar); }
    }), 0);
};

window.sigwebDestruirGrafico = (canvasId) => {
    if (window.sigwebGraficos[canvasId]) {
        window.sigwebGraficos[canvasId].destroy();
        delete window.sigwebGraficos[canvasId];
    }
};


// ---------- Marco redimensionable del gráfico (estilo Excel) ----------
// Tres tiradores (derecha, abajo, esquina). Se mueve el marco en vivo y solo al
// soltar se avisa a Blazor, para no re-renderizar durante el arrastre.
window.sigwebIniciarMarcoGrafico = (marcoId, canvasId, ref) => {
    const marco = document.getElementById(marcoId);
    if (!marco || marco.dataset.gxListo) return;
    marco.dataset.gxListo = '1';
    marco.addEventListener('pointerdown', (ev) => {
        const h = ev.target.closest('.gx-h');
        if (!h) return;
        ev.preventDefault();
        const modo = h.dataset.modo;
        const x0 = ev.clientX, y0 = ev.clientY;
        const w0 = marco.offsetWidth, h0 = marco.offsetHeight;
        h.setPointerCapture(ev.pointerId);
        marco.classList.add('gx-arrastrando');
        const mover = (e) => {
            if (modo.includes('e')) marco.style.width = Math.max(240, w0 + e.clientX - x0) + 'px';
            if (modo.includes('s')) marco.style.height = Math.max(160, h0 + e.clientY - y0) + 'px';
            const c = window.sigwebGraficos[canvasId]; if (c) c.resize();
        };
        const soltar = (e) => {
            h.removeEventListener('pointermove', mover);
            h.removeEventListener('pointerup', soltar);
            h.removeEventListener('pointercancel', soltar);
            marco.classList.remove('gx-arrastrando');
            const c = window.sigwebGraficos[canvasId]; if (c) c.resize();
            ref.invokeMethodAsync('AlSoltarMarco', marco.offsetWidth, marco.offsetHeight);
        };
        h.addEventListener('pointermove', mover);
        h.addEventListener('pointerup', soltar);
        h.addEventListener('pointercancel', soltar);
    });
};

window.sigwebRedimensionarGrafico = (canvasId, marcoId, ancho, alto) => {
    const marco = document.getElementById(marcoId);
    if (marco && ancho) marco.style.width = ancho + 'px';
    if (marco && alto) marco.style.height = alto + 'px';
    const c = window.sigwebGraficos[canvasId];
    if (c) requestAnimationFrame(() => c.resize());
};
