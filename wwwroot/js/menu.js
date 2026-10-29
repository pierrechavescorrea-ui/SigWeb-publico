// Menú lateral. El layout se renderiza estático (las páginas son interactivas por separado),
// así que todo va en JS y no en @onclick. Se recuerda en este navegador.
//   <html data-menu="contraido">  → modo barra (64 px, solo íconos). Es el modo por defecto.
//   sin atributo                    → expandido (íconos + texto, grupos desplegables).
//   .nav-grupo.abierto              → grupo desplegado (modo expandido)
//   .nav-grupo.flotando             → panel flotante abierto (modo barra)
(function () {
    const C_CONTRAIDO = 'sigweb.menu.contraido';
    const C_GRUPOS = 'sigweb.menu.grupos';

    function leer(k, def) { try { const v = localStorage.getItem(k); return v === null ? def : JSON.parse(v); } catch { return def; } }
    function guardar(k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch { } }
    const enBarra = () => document.documentElement.getAttribute('data-menu') === 'contraido' && window.innerWidth > 640;

    function pintar() {
        const h = document.documentElement;
        const contraido = leer(C_CONTRAIDO, true);
        if (contraido) h.setAttribute('data-menu', 'contraido'); else h.removeAttribute('data-menu');
        const abiertos = leer(C_GRUPOS, []);
        document.querySelectorAll('.nav-grupo[data-grupo]').forEach(g => {
            // Abierto si el usuario lo dejó así o si contiene la página actual.
            const activo = !!g.querySelector('a.nav-link.active');
            const abierto = abiertos.includes(g.dataset.grupo) || activo;
            g.classList.toggle('abierto', abierto);
            g.classList.toggle('con-activo', activo);
            g.classList.remove('flotando');
            const t = g.querySelector('.nav-grupo-tit');
            if (t) t.setAttribute('aria-expanded', abierto ? 'true' : 'false');
        });
        const b = document.querySelector('.btn-hamburguesa');
        if (b) {
            b.title = contraido ? 'Expandir menú' : 'Contraer menú';
            b.dataset.titulo = b.title;
            const t = b.querySelector('.nav-texto');
            if (t) t.textContent = contraido ? 'Expandir menú' : 'Contraer menú';
        }
        ocultarTip();
    }

    function alternarGrupo(g) {
        const id = g.dataset.grupo;
        let abiertos = leer(C_GRUPOS, []);
        if (g.classList.contains('abierto')) {
            abiertos = abiertos.filter(x => x !== id);
            g.classList.remove('abierto');
        } else {
            if (!abiertos.includes(id)) abiertos.push(id);
            g.classList.add('abierto');
        }
        guardar(C_GRUPOS, abiertos);
        const t = g.querySelector('.nav-grupo-tit');
        if (t) t.setAttribute('aria-expanded', g.classList.contains('abierto') ? 'true' : 'false');
    }

    // ---------- Modo barra: panel flotante por grupo ----------
    let cierre = null;
    function abrirFlotante(g) {
        clearTimeout(cierre);
        document.querySelectorAll('.nav-grupo.flotando').forEach(x => { if (x !== g) x.classList.remove('flotando'); });
        const tit = g.querySelector('.nav-grupo-tit');
        const panel = g.querySelector('.nav-hijos');
        if (!tit || !panel) return;
        const r = tit.getBoundingClientRect();
        // Alineado con el ícono, sin salirse por abajo.
        const alto = panel.scrollHeight || 200;
        const top = Math.max(8, Math.min(r.top - 6, window.innerHeight - alto - 12));
        panel.style.setProperty('--fly-top', top + 'px');
        g.classList.add('flotando');
        ocultarTip();
    }
    function programarCierre() {
        clearTimeout(cierre);
        cierre = setTimeout(() => document.querySelectorAll('.nav-grupo.flotando').forEach(x => x.classList.remove('flotando')), 180);
    }

    // ---------- Modo barra: etiqueta flotante de los íconos sueltos ----------
    function mostrarTip(el, texto) {
        const tip = document.querySelector('.nav-tip');
        if (!tip || !texto) return;
        const r = el.getBoundingClientRect();
        tip.textContent = texto;
        tip.style.top = (r.top + r.height / 2 - 14) + 'px';
        tip.classList.add('visible');
    }
    function ocultarTip() { const tip = document.querySelector('.nav-tip'); if (tip) tip.classList.remove('visible'); }

    document.addEventListener('mouseover', e => {
        if (!enBarra()) return;
        const sidebar = e.target.closest('.sidebar');
        if (!sidebar) return;
        // En la barra, la etiqueta propia reemplaza al tooltip del navegador (no salen los dos).
        const conTitulo = e.target.closest('.sidebar [title]');
        if (conTitulo) { conTitulo.dataset.titulo = conTitulo.getAttribute('title'); conTitulo.removeAttribute('title'); }
        const g = e.target.closest('.nav-grupo');
        if (g) { abrirFlotante(g); return; }
        programarCierre();
        const suelto = e.target.closest('.nav-item > a.nav-link, .nav-buscar, .btn-hamburguesa');
        if (suelto && !suelto.closest('.nav-hijos')) {
            const t = suelto.dataset.titulo || suelto.getAttribute('title') || suelto.textContent.trim();
            mostrarTip(suelto, suelto.classList.contains('nav-buscar') ? 'Buscar  ·  Ctrl K' : t);
        } else ocultarTip();
    });
    document.addEventListener('mouseout', e => {
        if (!enBarra()) return;
        const sidebar = e.target.closest('.sidebar');
        if (sidebar && !sidebar.contains(e.relatedTarget)) { programarCierre(); ocultarTip(); }
    });

    // Delegado en el documento: sobrevive a la navegación mejorada de Blazor.
    document.addEventListener('click', e => {
        if (e.target.closest('.btn-hamburguesa')) {
            guardar(C_CONTRAIDO, !leer(C_CONTRAIDO, true));
            pintar();
            return;
        }
        const tit = e.target.closest('.nav-grupo-tit');
        if (tit) {
            const g = tit.closest('.nav-grupo');
            if (enBarra()) {
                // En la barra, el clic (táctil o teclado) abre/cierra el panel.
                if (g.classList.contains('flotando')) g.classList.remove('flotando'); else abrirFlotante(g);
            } else alternarGrupo(g);
            return;
        }
        // Elegir un módulo del panel lo cierra.
        if (e.target.closest('.nav-hijos a')) { document.querySelectorAll('.nav-grupo.flotando').forEach(x => x.classList.remove('flotando')); ocultarTip(); return; }
        if (!e.target.closest('.sidebar')) document.querySelectorAll('.nav-grupo.flotando').forEach(x => x.classList.remove('flotando'));
    });
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') { document.querySelectorAll('.nav-grupo.flotando').forEach(x => x.classList.remove('flotando')); ocultarTip(); }
    });

    pintar();
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', pintar);
    // La navegación mejorada reescribe el menú con el HTML del servidor: se vuelve a aplicar.
    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', pintar);
})();
