// Navegación rápida:
//  · Buscador (Ctrl + K o la lupa del menú): módulos del menú, favoritos, recientes y búsqueda en
//    torres, planes, riesgos, planos, documentos, materiales, equipos y ensayos (GET /api/buscar).
//  · Favoritos: estrella del encabezado de página (.pg-fav). Recientes: se anotan solos al navegar.
// Todo vive en este navegador (localStorage). La ventana se crea fuera de Blazor y se recrea si la
// navegación mejorada la quita.
(function () {
    const C_FAV = 'sigweb.nav.favoritos';
    const C_REC = 'sigweb.nav.recientes';
    const MAX_REC = 8;

    const leer = (k) => { try { return JSON.parse(localStorage.getItem(k) || '[]'); } catch { return []; } };
    const guardar = (k, v) => { try { localStorage.setItem(k, JSON.stringify(v)); } catch { } };
    const rutaActual = () => location.pathname.replace(/^\/+/, '') + location.search;
    const norm = (s) => (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    function ir(url) {
        cerrar();
        const destino = url.startsWith('/') ? url : '/' + url;
        if (window.Blazor && typeof Blazor.navigateTo === 'function') Blazor.navigateTo(destino);
        else location.href = destino;
    }

    // ---------- Página actual (título y módulo, del encabezado) ----------
    function paginaActual() {
        const h = document.querySelector('.pg-cab h4');
        const mod = document.querySelector('.pg-migas [data-modulo]');
        return {
            url: rutaActual(),
            titulo: (h ? h.textContent.trim() : document.title || '').slice(0, 120),
            modulo: mod ? mod.textContent.trim() : ''
        };
    }

    // ---------- Recientes ----------
    let ultimaUrl = null, pendiente = null;
    function anotarReciente() {
        const p = paginaActual();
        if (!p.titulo || p.url === '' ) return;
        let l = leer(C_REC).filter(x => x.url !== p.url);
        l.unshift(p);
        guardar(C_REC, l.slice(0, MAX_REC));
    }
    function vigilarDireccion() {
        if (location.href === ultimaUrl) return;
        ultimaUrl = location.href;
        clearTimeout(pendiente);
        pendiente = setTimeout(() => { anotarReciente(); pintarFavoritos(); }, 900);   // espera a que la página pinte su encabezado
    }
    setInterval(vigilarDireccion, 600);

    // ---------- Favoritos (estrella del encabezado) ----------
    const esFavorito = (url) => leer(C_FAV).some(x => x.url === url);
    function pintarFavoritos() {
        const on = esFavorito(rutaActual());
        document.querySelectorAll('.pg-fav').forEach(b => {
            b.classList.toggle('activo', on);
            b.title = on ? 'Quitar de favoritos' : 'Agregar a favoritos';
        });
    }
    function alternarFavorito() {
        const p = paginaActual();
        let l = leer(C_FAV);
        if (l.some(x => x.url === p.url)) l = l.filter(x => x.url !== p.url);
        else l.unshift(p);
        guardar(C_FAV, l.slice(0, 20));
        pintarFavoritos();
    }
    // Blazor vuelve a dibujar el encabezado: se repinta la estrella cuando aparece una nueva.
    new MutationObserver(() => {
        if (document.querySelector('.pg-fav:not([data-fav-pintada])')) {
            document.querySelectorAll('.pg-fav').forEach(b => b.setAttribute('data-fav-pintada', ''));
            pintarFavoritos();
        }
    }).observe(document.documentElement, { childList: true, subtree: true });

    // ---------- Íconos de los resultados ----------
    const svg = (d) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${d}</svg>`;
    const ICONOS = {
        'Torre': svg('<path d="M12 2 7 22M12 2l5 20M8.6 15h6.8M9.6 10h4.8M10.6 6h2.8M5 22h14"/>'),
        'Plan de acción': svg('<rect x="5" y="3.5" width="14" height="18" rx="2"/><path d="M9 2.5h6v3H9z"/><path d="m8.5 11.5 1.6 1.6 3.2-3.2M8.5 17h7"/>'),
        'Riesgo': svg('<path d="M10.3 3.9 1.9 18.4A2 2 0 0 0 3.6 21.4h16.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4.5M12 17.2v.1"/>'),
        'Plano': svg('<path d="M4 4h16v16H4z"/><path d="M4 10h16M10 4v16"/>'),
        'Documento': svg('<path d="M6 3h8l4 4v14H6z"/><path d="M14 3v4h4M9 12h6M9 16h6"/>'),
        'Comunicado': svg('<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3.5 6.5 8.5 6 8.5-6"/>'),
        'Material': svg('<path d="M12 3l9 4.5v9L12 21l-9-4.5v-9z"/><path d="M3 7.5l9 4.5 9-4.5M12 12v9"/>'),
        'Equipo': svg('<path d="M14.5 6.5a4 4 0 01-5 5L4 17l3 3 5.5-5.5a4 4 0 005-5l-2.5 2.5-2-2z"/>'),
        'Ensayo': svg('<path d="M9 3h6M10 3v6l-5 10a2 2 0 002 3h10a2 2 0 002-3l-5-10V3"/><path d="M8 15h8"/>'),
        'reciente': svg('<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>'),
        'favorito': svg('<path d="m12 3.5 2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z"/>'),
        'modulo': svg('<rect x="4" y="4" width="7" height="7" rx="1.5"/><rect x="13" y="4" width="7" height="7" rx="1.5"/><rect x="4" y="13" width="7" height="7" rx="1.5"/><rect x="13" y="13" width="7" height="7" rx="1.5"/>')
    };

    // Módulos: los enlaces del menú lateral (con su ícono y su grupo).
    function modulos() {
        const vistos = new Set();
        return [...document.querySelectorAll('.sidebar a.nav-link')].map(a => {
            const nombre = (a.querySelector('.nav-texto') || a).textContent.trim();
            const grupo = a.closest('.nav-grupo')?.querySelector('.nav-grupo-tit .nav-texto')?.textContent.trim() || '';
            const ico = a.querySelector('.nav-ico');
            return { url: a.getAttribute('href') || '', titulo: nombre, detalle: grupo, icono: ico ? ico.outerHTML : ICONOS.modulo };
        }).filter(m => !vistos.has(m.url) && vistos.add(m.url));
    }

    // ---------- Ventana ----------
    let caja, entrada, lista, items = [], sel = 0, peticion = null, temporizador = null;

    function crear() {
        let raiz = document.getElementById('sig-paleta');
        if (raiz) return raiz;
        raiz = document.createElement('div');
        raiz.id = 'sig-paleta';
        raiz.innerHTML = `
            <div class="sp-fondo" data-sp-cerrar></div>
            <div class="sp-caja" role="dialog" aria-modal="true" aria-label="Buscar">
                <div class="sp-entrada">
                    ${svg('<circle cx="11" cy="11" r="6.5"/><path d="m20 20-4.2-4.2"/>')}
                    <input type="text" autocomplete="off" spellcheck="false" placeholder="Torres, planes, riesgos, comunicados, planos, documentos…" />
                    <span class="sp-cargando" aria-hidden="true"></span>
                    <kbd>Esc</kbd>
                </div>
                <div class="sp-lista" role="listbox"></div>
                <div class="sp-pie"><span><kbd>↑</kbd><kbd>↓</kbd></span><span><kbd>Enter</kbd></span><span class="ms-auto"><kbd>Ctrl</kbd><kbd>K</kbd></span></div>
            </div>`;
        document.body.appendChild(raiz);
        raiz.addEventListener('click', e => {
            if (e.target.closest('[data-sp-cerrar]')) { cerrar(); return; }
            const quitar = e.target.closest('[data-sp-quitar]');
            if (quitar) {
                e.stopPropagation();
                const url = quitar.getAttribute('data-sp-quitar');
                guardar(quitar.getAttribute('data-sp-lista'), leer(quitar.getAttribute('data-sp-lista')).filter(x => x.url !== url));
                pintarFavoritos();
                buscar();
                return;
            }
            const it = e.target.closest('.sp-item');
            if (it) ir(it.getAttribute('data-url'));
        });
        raiz.addEventListener('mousemove', e => {
            const it = e.target.closest('.sp-item');
            if (it) marcar(+it.getAttribute('data-i'), false);
        });
        const inp = raiz.querySelector('input');
        inp.addEventListener('input', () => { clearTimeout(temporizador); temporizador = setTimeout(buscar, 160); pintarLocal(); });
        inp.addEventListener('keydown', e => {
            if (e.key === 'ArrowDown') { e.preventDefault(); marcar(Math.min(sel + 1, items.length - 1), true); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); marcar(Math.max(sel - 1, 0), true); }
            else if (e.key === 'Enter') { e.preventDefault(); if (items[sel]) ir(items[sel].url); }
            else if (e.key === 'Escape') { e.preventDefault(); cerrar(); }
        });
        return raiz;
    }

    function abrir() {
        const raiz = crear();
        caja = raiz.querySelector('.sp-caja'); entrada = raiz.querySelector('input'); lista = raiz.querySelector('.sp-lista');
        raiz.classList.add('abierta');
        entrada.value = '';
        pintarLocal();
        setTimeout(() => entrada.focus(), 10);
    }
    function cerrar() {
        const raiz = document.getElementById('sig-paleta');
        if (raiz) raiz.classList.remove('abierta', 'buscando');
        if (peticion) peticion.abort();
    }
    const abierta = () => document.getElementById('sig-paleta')?.classList.contains('abierta');

    function marcar(i, desplazar) {
        sel = i;
        lista.querySelectorAll('.sp-item').forEach(x => x.classList.toggle('sel', +x.getAttribute('data-i') === i));
        if (desplazar) lista.querySelector(`.sp-item[data-i="${i}"]`)?.scrollIntoView({ block: 'nearest' });
    }

    // Secciones → HTML. Cada ítem: { url, titulo, detalle, tipo, icono, quitar? }
    function pintar(secciones) {
        items = [];
        let html = '';
        for (const s of secciones) {
            if (!s.items.length) continue;
            html += `<div class="sp-sec">${esc(s.titulo)}</div>`;
            for (const it of s.items) {
                const i = items.length; items.push(it);
                html += `<div class="sp-item" role="option" data-i="${i}" data-url="${esc(it.url)}">
                    <span class="sp-ico">${it.icono || ICONOS[it.tipo] || ICONOS.modulo}</span>
                    <span class="sp-txt"><span class="sp-tit">${esc(it.titulo)}</span>${it.detalle ? `<span class="sp-det">${esc(it.detalle)}</span>` : ''}</span>
                    ${it.etiqueta ? `<span class="sp-tipo">${esc(it.etiqueta)}</span>` : ''}
                    ${it.quitar ? `<button type="button" class="sp-quitar" title="Quitar" data-sp-quitar="${esc(it.url)}" data-sp-lista="${it.quitar}">✕</button>` : ''}
                    <span class="sp-enter" aria-hidden="true">↵</span>
                </div>`;
            }
        }
        lista.innerHTML = html || `<div class="sp-vacio">${svg('<circle cx="11" cy="11" r="6.5"/><path d="m20 20-4.2-4.2"/>')}<span>Sin resultados</span></div>`;
        marcar(Math.min(sel, Math.max(0, items.length - 1)), false);
    }

    let remotos = [];
    function pintarLocal() {
        const q = norm(entrada.value.trim());
        sel = 0;
        const mods = modulos().filter(m => !q || norm(m.titulo + ' ' + m.detalle).includes(q))
            .map(m => ({ ...m, etiqueta: q ? 'Módulo' : '' }));
        if (!q) {
            remotos = [];
            pintar([
                { titulo: 'Favoritos', items: leer(C_FAV).map(f => ({ url: f.url, titulo: f.titulo, detalle: f.modulo, icono: ICONOS.favorito, quitar: C_FAV })) },
                { titulo: 'Recientes', items: leer(C_REC).filter(r => !esFavorito(r.url)).slice(0, 5).map(r => ({ url: r.url, titulo: r.titulo, detalle: r.modulo, icono: ICONOS.reciente, quitar: C_REC })) },
                { titulo: 'Módulos', items: mods }
            ]);
            return;
        }
        pintarConRemotos(mods);
    }

    function pintarConRemotos(mods) {
        const grupos = {};
        for (const r of remotos) (grupos[r.tipo] ||= []).push({ url: r.url, titulo: r.titulo, detalle: r.detalle, tipo: r.tipo });
        const orden = ['Torre', 'Plan de acción', 'Riesgo', 'Comunicado', 'Plano', 'Documento', 'Material', 'Equipo', 'Ensayo'];
        const plural = { 'Torre': 'Torres', 'Plan de acción': 'Planes de acción', 'Riesgo': 'Riesgos', 'Plano': 'Planos', 'Documento': 'Documentos', 'Comunicado': 'Comunicados', 'Material': 'Materiales', 'Equipo': 'Equipos', 'Ensayo': 'Ensayos' };
        pintar([{ titulo: 'Módulos', items: mods.slice(0, 4) }, ...orden.map(t => ({ titulo: plural[t], items: grupos[t] || [] }))]);
    }

    async function buscar() {
        if (!entrada) return;
        const q = entrada.value.trim();
        if (q.length < 2) { remotos = []; pintarLocal(); return; }
        if (peticion) peticion.abort();
        peticion = new AbortController();
        const raiz = document.getElementById('sig-paleta');
        raiz?.classList.add('buscando');
        try {
            const r = await fetch('/api/buscar?q=' + encodeURIComponent(q), { signal: peticion.signal, headers: { 'Accept': 'application/json' } });
            remotos = r.ok ? await r.json() : [];
        } catch (e) { if (e.name === 'AbortError') return; remotos = []; }
        finally { raiz?.classList.remove('buscando'); }
        if (entrada.value.trim() !== q) return;
        const nq = norm(q);
        pintarConRemotos(modulos().filter(m => norm(m.titulo + ' ' + m.detalle).includes(nq)).map(m => ({ ...m, etiqueta: 'Módulo' })));
    }

    // ---------- Atajos y botones ----------
    document.addEventListener('keydown', e => {
        if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
            e.preventDefault();
            abierta() ? cerrar() : abrir();
        }
    });
    document.addEventListener('click', e => {
        if (e.target.closest('[data-nav="buscar"]')) { e.preventDefault(); abrir(); return; }
        if (e.target.closest('.pg-fav')) { e.preventDefault(); alternarFavorito(); }
    });

    window.sigBuscar = abrir;
})();
