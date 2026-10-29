// Tablero de inicio (Home.razor): todo cabe en la pantalla, sin scroll vertical.
// El tablero toma el alto que queda bajo él; en cada apartado se ven los elementos que caben
// y "+N" lleva al módulo con el resto. En pantallas angostas (una columna, ≤ 1000 px) se deja el scroll.
(function () {
    function ajustar() {
        var t = document.querySelector('.ini-tablero');
        if (!t) return;
        var angosta = window.matchMedia('(max-width: 1000px)').matches;
        if (angosta) t.style.height = '';
        else {
            var alto = Math.max(320, window.innerHeight - t.getBoundingClientRect().top - 12);
            t.style.height = alto + 'px';
            // Márgenes de la página bajo el tablero: si aún sobra scroll, se descuenta.
            var sobra = document.documentElement.scrollHeight - window.innerHeight;
            if (sobra > 0) t.style.height = Math.max(320, alto - sobra) + 'px';
        }
        t.querySelectorAll('.ini-panel').forEach(function (p) {
            var lista = p.querySelector('.ini-lista'), mas = p.querySelector('.ini-mas');
            if (!lista) return;
            var items = lista.querySelectorAll('.ini-item');
            items.forEach(function (i) { i.style.display = ''; });
            var total = parseInt(lista.getAttribute('data-total') || items.length, 10);
            var visibles = items.length;
            if (!angosta) {
                // Con "+N" visible el espacio de la lista se reduce: se mide con él puesto.
                if (mas) mas.style.display = '';
                var limite = lista.getBoundingClientRect().bottom;
                for (var k = 0; k < items.length; k++) {
                    if (items[k].getBoundingClientRect().bottom > limite + 1) { visibles = k; break; }
                }
                for (var j = visibles; j < items.length; j++) items[j].style.display = 'none';
            }
            if (mas) {
                var resto = total - visibles;
                mas.textContent = '+' + resto;
                mas.style.display = resto > 0 ? '' : 'none';
            }
        });
    }
    window.sigInicioAjustar = function () { requestAnimationFrame(ajustar); };
    window.addEventListener('resize', function () { requestAnimationFrame(ajustar); });
    // La fuente (Inter) puede llegar después del primer ajuste y cambiar el alto de las filas.
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { requestAnimationFrame(ajustar); });
})();
