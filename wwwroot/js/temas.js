// Temas de la interfaz: se guarda la elección en este navegador (localStorage)
// y se aplica como <html data-tema="…">. "institucional" = estilo original.
(function () {
    const CLAVE = 'sigweb.tema';
    function leer() { try { return localStorage.getItem(CLAVE) || 'institucional'; } catch { return 'institucional'; } }
    function pintar(t) {
        const h = document.documentElement;
        if (!t || t === 'institucional') h.removeAttribute('data-tema'); else h.setAttribute('data-tema', t);
        if (t === 'oscuro') h.setAttribute('data-bs-theme', 'dark'); else h.removeAttribute('data-bs-theme');
        // Textos y cuadrícula de los gráficos (Chart.js) acordes al tema; aplica a los gráficos que se dibujen después.
        if (window.Chart && Chart.defaults) {
            const oscuro = t === 'oscuro';
            Chart.defaults.color = oscuro ? '#c9c9d2' : '#666666';
            Chart.defaults.borderColor = oscuro ? 'rgba(255,255,255,.12)' : 'rgba(0,0,0,.1)';
        }
    }
    window.sigTema = {
        obtener: leer,
        aplicar: function (t) { try { localStorage.setItem(CLAVE, t); } catch { } pintar(t); },
        reaplicar: function () { pintar(leer()); }
    };
    pintar(leer());
    // La navegación mejorada de Blazor puede reescribir atributos del documento: se vuelve a aplicar.
    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', () => pintar(leer()));
})();
