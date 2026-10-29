// Tablas que llenan el resto de la pantalla (.tabla-scroll.tabla-auto): su alto se ajusta a lo que
// queda visible debajo de su posición, para que el scroll horizontal siempre se vea sin bajar la
// página (antes, con mosaicos o paneles arriba, la barra quedaba por debajo de la ventana).
(function () {
    let pendiente = false;
    function ajustar() {
        pendiente = false;
        document.querySelectorAll('.tabla-scroll.tabla-auto').forEach(el => {
            // En pantalla completa (.ens-pantalla, position:fixed) la posición no depende del scroll de la página.
            const fija = !!el.closest('.ens-pantalla');
            const arriba = el.getBoundingClientRect().top + (fija ? 0 : window.scrollY);
            const alto = Math.max(fija ? 160 : 240, window.innerHeight - arriba - (fija ? 12 : 14));
            const v = alto + 'px';
            if (el.style.maxHeight !== v) el.style.maxHeight = v;
        });
    }
    function programar() { if (!pendiente) { pendiente = true; requestAnimationFrame(ajustar); } }
    window.addEventListener('resize', programar);
    document.addEventListener('DOMContentLoaded', programar);
    // Blazor cambia el contenido sin recargar (paneles que se abren, filtros): se vuelve a medir.
    new MutationObserver(programar).observe(document.documentElement, { childList: true, subtree: true });
    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', programar);
    programar();
})();
