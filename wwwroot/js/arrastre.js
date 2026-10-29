// Arrastrar y soltar para reordenar (Campos, reglas, campos de Equipos/Ensayos): Firefox no inicia el
// arrastre sin datos en dataTransfer, y Blazor no los pone; se ponen aquí para todo [data-arrastre].
document.addEventListener('dragstart', function (e) {
    var asa = e.target && e.target.closest ? e.target.closest('[data-arrastre]') : null;
    if (!asa) return;
    try { e.dataTransfer.setData('text/plain', ''); e.dataTransfer.effectAllowed = 'move'; } catch (x) { }
    // Se arrastra la fila completa como imagen, no solo el asa.
    var fila = asa.closest('tr, .pc-regla, li.list-group-item, .hv-tab');
    if (fila && e.dataTransfer.setDragImage) e.dataTransfer.setDragImage(fila, 12, 12);
});

// Lleva a la vista un elemento por id (p. ej. la acción elegida en Planes de acción).
window.sigIrA = function (id) {
    var el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: 'center', behavior: 'smooth' });
};
