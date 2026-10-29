// Descarga un archivo generado en el servidor (Excel, etc.) directamente en
// el navegador, sin pasar por un enlace real: crea un <a> temporal apuntando
// a los datos en memoria (base64) y simula el clic.
window.descargarArchivo = (nombreArchivo, contentType, base64) => {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64}`;
    link.download = nombreArchivo;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Descarga por dirección (Dossier: el servidor genera el ZIP en streaming). El navegador muestra su
// propia barra de progreso y permite cancelar; la página sigue usable mientras tanto.
window.sigDescargarUrl = (url) => {
    const link = document.createElement('a');
    link.href = url;
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
