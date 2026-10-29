// Inmovilizar columnas en las listas de la app (tablas con clase "tabla-pin").
// Por defecto queda inmóvil la primera columna; con clic en el 📌 del
// encabezado (zona derecha de la celda) se fija o suelta cualquier otra.
// Todo se hace por DOM/atributos data-fija, sin tocar el estado de Blazor, y se
// reaplica tras cada re-render con un MutationObserver.
(function () {
    var estado = {};          // clave tabla -> array de índices fijos
    var ocupado = false;

    function clave(t) {
        var tablas = Array.prototype.slice.call(document.querySelectorAll('table.tabla-pin'));
        return location.pathname + '#' + tablas.indexOf(t);
    }
    function fijas(t) {
        var k = clave(t);
        if (!estado[k]) {
            try { var g = localStorage.getItem('sigwebPin:' + k); if (g) estado[k] = JSON.parse(g); } catch (e) { }
            if (!estado[k]) estado[k] = [0];
        }
        return estado[k];
    }
    function guardar(t) { try { localStorage.setItem('sigwebPin:' + clave(t), JSON.stringify(estado[clave(t)])); } catch (e) { } }

    function aplicar(t) {
        var ths = t.querySelectorAll('thead tr:first-child > th');
        if (!ths.length) return;
        var f = fijas(t).filter(function (i) { return i < ths.length; }).sort(function (a, b) { return a - b; });
        var left = 0, lefts = {};
        f.forEach(function (i) { lefts[i] = left; left += ths[i].getBoundingClientRect().width; });
        var filas = t.querySelectorAll('tr');
        filas.forEach(function (tr) {
            var celdas = tr.children;
            if (celdas.length !== ths.length) return;
            for (var i = 0; i < celdas.length; i++) {
                var c = celdas[i];
                if (lefts.hasOwnProperty(i)) {
                    if (c.getAttribute('data-fija') !== '1') c.setAttribute('data-fija', '1');
                    if (c.style.left !== lefts[i] + 'px') c.style.left = lefts[i] + 'px';
                } else if (c.hasAttribute('data-fija')) {
                    c.removeAttribute('data-fija'); c.style.left = '';
                }
            }
        });
    }
    function aplicarTodas() {
        if (ocupado) return;
        ocupado = true;
        try { document.querySelectorAll('table.tabla-pin').forEach(aplicar); } finally { setTimeout(function () { ocupado = false; }, 0); }
    }

    var temporizador = null;
    new MutationObserver(function () {
        clearTimeout(temporizador);
        temporizador = setTimeout(aplicarTodas, 60);
    }).observe(document.documentElement, { childList: true, subtree: true });
    setTimeout(aplicarTodas, 100);
    window.addEventListener('resize', function () { clearTimeout(temporizador); temporizador = setTimeout(aplicarTodas, 100); });

    document.addEventListener('click', function (e) {
        var th = e.target.closest && e.target.closest('table.tabla-pin thead th');
        if (!th) return;
        var r = th.getBoundingClientRect();
        if (e.clientX < r.right - 26) return;
        var t = th.closest('table');
        var idx = Array.prototype.indexOf.call(th.parentNode.children, th);
        var f = fijas(t), pos = f.indexOf(idx);
        if (pos >= 0) f.splice(pos, 1); else f.push(idx);
        guardar(t);
        aplicar(t);
        e.stopImmediatePropagation();
        e.preventDefault();
    }, true);
})();
