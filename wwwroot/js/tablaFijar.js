// Fija (inmoviliza) columnas de una tabla según una lista de claves de
// columna elegidas por el usuario (ver "📌" en los headers de la tabla de
// Ensayos). No trunca ni oculta nada: solo calcula, a partir del ANCHO REAL
// ya renderizado de cada columna fija, el offset acumulado para que cada
// una quede pegada (position: sticky) justo después de la anterior.
window.sigwebFijarColumnas = function (tablaId, columnasFijas) {
    const tabla = document.getElementById(tablaId);
    if (!tabla) return;

    const filaHeader = tabla.querySelector('thead tr');
    if (!filaHeader) return;

    // Calcula, en el orden visual de las columnas, el "left" acumulado de
    // cada columna fija (solo cuenta el ancho de las columnas fijas que
    // vienen antes, para que queden una al lado de la otra sin huecos).
    const offsets = {};
    let acumulado = 0;
    Array.from(filaHeader.children).forEach(th => {
        const clave = th.getAttribute('data-col');
        if (!clave) return;
        if (columnasFijas.includes(clave)) {
            offsets[clave] = acumulado;
            acumulado += th.getBoundingClientRect().width;
        }
    });

    tabla.querySelectorAll('tr').forEach(fila => {
        Array.from(fila.children).forEach(celda => {
            const clave = celda.getAttribute('data-col');
            if (!clave) return;
            if (Object.prototype.hasOwnProperty.call(offsets, clave)) {
                celda.style.position = 'sticky';
                celda.style.left = offsets[clave] + 'px';
                // El header (<thead>) ya tiene su propio z-index alto en línea
                // para quedar sobre las filas al hacer scroll vertical; aquí solo
                // hace falta que una celda fija quede sobre las que no lo están,
                // dentro de su propia fila/franja (scroll horizontal).
                celda.style.zIndex = celda.tagName === 'TH' ? '2' : '1';
            } else {
                celda.style.position = '';
                celda.style.left = '';
                celda.style.zIndex = '';
            }
        });
    });
};
