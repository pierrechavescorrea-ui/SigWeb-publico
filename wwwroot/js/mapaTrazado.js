// Interop del mapa del Trazado (Leaflet). Un solo mapa vive a la vez;
// si el usuario navega fuera y vuelve, destruirMapaTrazado() lo limpia
// para que Leaflet no reclame que el contenedor ya está inicializado.
let mapaTrazado = null;
let marcadoresTorres = {}; // id de la torre -> marcador de Leaflet, para poder "volar" hacia ella

// ================================================================
// Librería de íconos: cada uno es un generador de SVG embebido que
// recibe un color y devuelve el markup. Se usan con L.divIcon en vez
// de imágenes fijas, para poder recolorearlos por torre/pata desde la
// configuración del mapa (⚙ Vista del mapa), sin depender de archivos
// distintos por color.
// ================================================================

// Aclara (cantidad > 0, hacia blanco) u oscurece (cantidad < 0, hacia
// negro) un color hex "#rrggbb", para las dos caras del ícono 3D de la
// torre a partir del único color que elige el usuario.
function ajustarColor(colorHex, cantidad) {
    const hex = (colorHex || '#EC1C24').replace('#', '');
    const r = parseInt(hex.substring(0, 2), 16) || 0;
    const g = parseInt(hex.substring(2, 4), 16) || 0;
    const b = parseInt(hex.substring(4, 6), 16) || 0;
    const mezclar = (canal) => cantidad >= 0
        ? Math.round(canal + (255 - canal) * cantidad)
        : Math.round(canal * (1 + cantidad));
    return `rgb(${mezclar(r)}, ${mezclar(g)}, ${mezclar(b)})`;
}

function svgTorre(color) {
    // Copia exacta de la imagen de referencia que entregó el usuario
    // (wwwroot/images/icono-torre.png), sin redibujar nada. Al ser una
    // imagen fija ya no admite recoloreo por torre: el parámetro
    // "color" se ignora aquí a propósito, para no alterar el diseño
    // original que pidió usar "exactamente igual".
    return `<img src="images/icono-torre.png" width="30" height="44" style="display:block" alt="" />`;
}

function svgPedestal(color) {
    // Pedestal de concreto con stub metálico embebido (una pata de
    // torre real, no una pierna): el cuerpo de concreto conserva su
    // tono natural en cualquier color elegido, y el color se aplica al
    // stub y al contorno, para que siga leyéndose como concreto.
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 40" width="26" height="33">
        <line x1="19.5" y1="2" x2="17" y2="15" stroke="#ffffff" stroke-width="3.6" stroke-linecap="round" opacity="0.85"/>
        <line x1="19.5" y1="2" x2="17" y2="15" stroke="${color}" stroke-width="2.2" stroke-linecap="round"/>
        <polygon points="16,11 22,17 20,37 12,37 10,17" fill="#CBBFA6" stroke="${color}" stroke-width="1.8" stroke-linejoin="round"/>
        <line x1="11" y1="23.5" x2="21" y2="23.5" stroke="#8a8171" stroke-width="1"/>
        <line x1="10.4" y1="30.5" x2="21.6" y2="30.5" stroke="#8a8171" stroke-width="1"/>
    </svg>`;
}

function svgPin(color) {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 40" width="26" height="34">
        <path d="M16 2C9.4 2 4 7.3 4 13.8c0 8.6 11 22 11.3 22.3.4.4 1 .4 1.4 0C17 35.8 28 22.4 28 13.8 28 7.3 22.6 2 16 2z"
              fill="${color}" stroke="#ffffff" stroke-width="1.8"/>
        <circle cx="16" cy="14" r="5" fill="#ffffff"/>
    </svg>`;
}

function svgCirculo(color) {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="20" height="20">
        <circle cx="16" cy="16" r="9" fill="${color}" stroke="#ffffff" stroke-width="2.6"/>
    </svg>`;
}

function svgCuadrado(color) {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="20" height="20">
        <rect x="7" y="7" width="18" height="18" rx="2" fill="${color}" stroke="#ffffff" stroke-width="2.6"/>
    </svg>`;
}

function svgDiamante(color) {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="22" height="22">
        <polygon points="16,3 29,16 16,29 3,16" fill="${color}" stroke="#ffffff" stroke-width="2.6" stroke-linejoin="round"/>
    </svg>`;
}

function svgTriangulo(color) {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="22" height="22">
        <polygon points="16,3 29,27 3,27" fill="${color}" stroke="#ffffff" stroke-width="2.6" stroke-linejoin="round"/>
    </svg>`;
}

function svgEstrella(color) {
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="24" height="24">
        <polygon points="16,2 19.6,11.2 29.6,11.8 21.7,18 24.3,27.6 16,22.1 7.7,27.6 10.3,18 2.4,11.8 12.4,11.2"
                 fill="${color}" stroke="#ffffff" stroke-width="1.8" stroke-linejoin="round"/>
    </svg>`;
}

// tipo -> { generador de SVG, tamaño del ícono, punto de anclaje (la
// "punta" que debe coincidir exactamente con la coordenada del punto) }
const iconosDisponibles = {
    torre: { gen: svgTorre, size: [30, 44], anchor: [15, 43] },
    pedestal: { gen: svgPedestal, size: [26, 33], anchor: [13, 32] },
    pin: { gen: svgPin, size: [26, 34], anchor: [13, 33] },
    circulo: { gen: svgCirculo, size: [20, 20], anchor: [10, 10] },
    cuadrado: { gen: svgCuadrado, size: [20, 20], anchor: [10, 10] },
    diamante: { gen: svgDiamante, size: [22, 22], anchor: [11, 11] },
    triangulo: { gen: svgTriangulo, size: [22, 22], anchor: [11, 19] },
    estrella: { gen: svgEstrella, size: [24, 24], anchor: [12, 12] }
};

function crearIcono(tipo, color) {
    // "icono" es el valor antiguo (única opción de silueta que existía
    // antes de tener varias formas); se mantiene como alias de "torre"
    // para no romper configuraciones ya guardadas.
    const clave = tipo === 'icono' ? 'torre' : tipo;
    const def = iconosDisponibles[clave] || iconosDisponibles.circulo;
    return L.divIcon({
        html: def.gen(color || '#EC1C24'),
        className: 'icono-mapa-trazado',
        iconSize: def.size,
        iconAnchor: def.anchor,
        popupAnchor: [0, -def.anchor[1]],
        tooltipAnchor: [0, -def.anchor[1] + 6]
    });
}

// "icono"/"color" son el override propio de ese elemento (torre o pata),
// guardado en su ficha; si no lo tiene, se usa el valor por defecto que
// trae "config" (el último guardado globalmente desde Trazado).
function marcadorTorre(latlng, icono, color, config) {
    return L.marker(latlng, { icon: crearIcono(icono || config.iconoTorre || 'torre', color || config.colorTorre || '#EC1C24') });
}

function marcadorPata(latlng, icono, color, config) {
    return L.marker(latlng, { icon: crearIcono(icono || config.iconoPata || 'pedestal', color || config.colorPata || '#666666') });
}

// ================================================================
// Capas base del mapa (como el selector de capas de Google Earth):
// Calle (OpenStreetMap), Satélite e Híbrido (imágenes de Esri, sin
// necesitar clave de API) y Terreno (OpenTopoMap).
// ================================================================
function crearCapasBase() {
    const calle = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 22, maxNativeZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    });

    const atribucionEsri = 'Tiles &copy; Esri — Source: Esri, Maxar, Earthstar Geographics';

    const satelite = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 22, maxNativeZoom: 17, attribution: atribucionEsri
    });

    const hibridoImagen = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 22, maxNativeZoom: 17, attribution: atribucionEsri
    });
    const hibridoEtiquetas = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 22, maxNativeZoom: 15
    });
    const hibrido = L.layerGroup([hibridoImagen, hibridoEtiquetas]);

    const terreno = L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
        maxZoom: 22, maxNativeZoom: 17, attribution: 'Map data: &copy; OpenStreetMap contributors, SRTM | Map style: &copy; OpenTopoMap'
    });

    return { calle, satelite, hibrido, terreno };
}

window.inicializarMapaTrazado = (torres, patas, formas, config, dotNetRef) => {
    const contenedor = document.getElementById('mapaTrazado');
    if (!contenedor) return;

    if (mapaTrazado) {
        mapaTrazado.remove();
        mapaTrazado = null;
    }
    marcadoresTorres = {};

    mapaTrazado = L.map('mapaTrazado', { maxZoom: 22, touchZoom: true, bounceAtZoomLimits: false });

    const { calle, satelite, hibrido, terreno } = crearCapasBase();
    const capasBase = { 'Calle': calle, 'Satélite': satelite, 'Híbrido (satélite + nombres)': hibrido, 'Terreno': terreno };
    const clavesPorCapa = new Map([[calle, 'calle'], [satelite, 'satelite'], [hibrido, 'hibrido'], [terreno, 'terreno']]);

    const capaInicial = capasBase[
        config && config.capaBase === 'satelite' ? 'Satélite' :
        config && config.capaBase === 'hibrido' ? 'Híbrido (satélite + nombres)' :
        config && config.capaBase === 'terreno' ? 'Terreno' :
        'Calle'
    ];
    capaInicial.addTo(mapaTrazado);

    L.control.layers(capasBase, null, { position: 'topright', collapsed: true }).addTo(mapaTrazado);

    mapaTrazado.on('baselayerchange', (e) => {
        const clave = clavesPorCapa.get(e.layer) || 'calle';
        if (dotNetRef) dotNetRef.invokeMethodAsync('GuardarCapaBaseMapa', clave);
    });

    const puntos = [];
    torres.forEach(el => {
        const marcador = marcadorTorre([el.latitud, el.longitud], el.icono, el.color, config).addTo(mapaTrazado);

        marcador.bindTooltip(el.codigo);
        marcador.on('click', () => dotNetRef.invokeMethodAsync('SeleccionarElemento', el.id));

        marcadoresTorres[el.id] = marcador;
        puntos.push([el.latitud, el.longitud]);
    });

    (patas || []).forEach(pt => {
        const marcador = marcadorPata([pt.latitud, pt.longitud], pt.icono, pt.color, config).addTo(mapaTrazado);
        marcador.bindTooltip(pt.nombre);
        marcador.on('click', () => dotNetRef.invokeMethodAsync('SeleccionarElemento', pt.torreId));
        puntos.push([pt.latitud, pt.longitud]);
    });

    // Elementos importados de DWG/DXF (cunetas, trinchos...): cada "forma"
    // trae uno o más caminos (polilíneas) de [lat, lon] ya convertidos.
    (formas || []).forEach(f => {
        (f.caminos || []).forEach(camino => {
            if (!camino || camino.length < 2) return;
            const linea = L.polyline(camino, { color: f.color || '#8e44ad', weight: 3 }).addTo(mapaTrazado);
            linea.bindTooltip(f.nombre);
            linea.on('click', () => dotNetRef.invokeMethodAsync('SeleccionarElemento', f.torreId));
            camino.forEach(p => puntos.push(p));
        });
    });

    if (puntos.length > 0) {
        mapaTrazado.fitBounds(puntos, { padding: [30, 30] });
    } else {
        mapaTrazado.setView([4.6, -74.1], 6); // Colombia, vista por defecto sin datos aún
    }
};

// Centra el mapa en una torre y hace parpadear su tooltip un momento,
// para que sea fácil ubicarla visualmente después de buscarla por nombre.
window.volarATorre = (id) => {
    const marcador = marcadoresTorres[id];
    if (!marcador || !mapaTrazado) return;

    const zoomActual = mapaTrazado.getZoom();
    mapaTrazado.flyTo(marcador.getLatLng(), Math.max(zoomActual, 17));
    marcador.openTooltip();
    setTimeout(() => marcador.closeTooltip(), 1800);
};

window.redimensionarMapaTrazado = () => {
    if (mapaTrazado) {
        mapaTrazado.invalidateSize();
    }
};

window.destruirMapaTrazado = () => {
    if (mapaTrazado) {
        mapaTrazado.remove();
        mapaTrazado = null;
    }
    marcadoresTorres = {};
};
