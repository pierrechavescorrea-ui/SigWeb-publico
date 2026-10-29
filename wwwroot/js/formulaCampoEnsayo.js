// Permite "armar" una fórmula de Campo de Ensayo como en Excel: en vez de
// escribir {Clave} a mano, se hace clic en un campo o función y se inserta
// justo donde estaba el cursor en el input (igual que Excel inserta una
// referencia de celda donde estaba escribiendo la fórmula).
window.sigwebInsertarEnFormula = function (inputId, texto, offsetDesdeFinal) {
    const input = document.getElementById(inputId);
    if (!input) return;

    const inicio = input.selectionStart ?? input.value.length;
    const fin = input.selectionEnd ?? input.value.length;
    const valor = input.value;

    input.value = valor.slice(0, inicio) + texto + valor.slice(fin);

    const nuevaPosicion = inicio + texto.length - (offsetDesdeFinal || 0);
    input.focus();
    input.setSelectionRange(nuevaPosicion, nuevaPosicion);

    // Blazor escucha "input" para sincronizar @bind:event="oninput"; hay
    // que dispararlo a mano porque el cambio se hizo desde JS, no tecleado.
    input.dispatchEvent(new Event('input', { bubbles: true }));
};
