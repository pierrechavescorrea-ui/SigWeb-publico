// Vista "tipo Excel" de Ensayos, basada en Univer (lib/univer — motor de
// hoja de cálculo open-source con barra de fórmulas real y fórmulas que se
// escriben igual que en Excel/Google Sheets: =SUMA(), =PROMEDIO(), etc.
// Reemplaza a la vista anterior basada en Tabulator (ya quitada de la app).
//
// window.sigwebHojaExcel(idDiv, columnas, filas, dotNetRef) crea o
// actualiza la hoja. Las columnas/filas llegan con la misma forma que ya
// se usaba antes (ver ColumnasExcel()/FilasExcel() en EnsayosDatos.razor):
// columnas = [{ title, field, editor? }, ...]; filas = [{ __Id, <field>: valor, ... }, ...].
// El bloque de datos (todas las columnas, no solo Torre/Elemento/Informe)
// NO se edita escribiendo directo en la celda — eso quedó reservado para
// el botón "✎ Editar muestra" de la pantalla, que abre el diálogo
// Agregar/Editar de siempre para toda la fila seleccionada, con los datos
// ya cargados (no en blanco). La propiedad "editor" de cada columna queda
// en las columnas/filas que llegan desde EnsayosDatos.razor mas no se usa
// ya para habilitar escritura aquí.
//
// A la derecha de los datos se deja un área en blanco ("de trabajo") donde
// el usuario puede escribir sus propias fórmulas sueltas (=SUMA(B2:B20),
// =PROMEDIO(...), etc.) para cálculos propios — esas celdas NO se guardan
// en el servidor, son solo de la sesión de pantalla (se pierden si cambia
// el filtro, el orden, o si recarga la página).
window._sigwebHojas = window._sigwebHojas || {};
window._sigwebHojaLibCargando = window._sigwebHojaLibCargando || null;

// Pantalla completa: Univer pide fullscreen solo para el div de la hoja, y
// entonces el botón "Editar muestra" y su diálogo (fuera del div) quedan
// ocultos. Se redirige al contenedor .hoja-wrap que los incluye.
(function () {
    if (Element.prototype._sigwebFs) return;
    var orig = Element.prototype.requestFullscreen;
    if (!orig) return;
    Element.prototype._sigwebFs = true;
    Element.prototype.requestFullscreen = function (o) {
        var wrap = this.id && this.id.indexOf('hoja-') === 0 && this.closest ? this.closest('.hoja-wrap') : null;
        return orig.call(wrap || this, o);
    };
})();

(function () {
    var GAP_COLS = 1;
    var SCRATCH_COLS = 14;
    var SCRATCH_FILAS_EXTRA = 80;
    var DEBOUNCE_MS = 300;
    var COLOR_BANDA = "#f5f5f5";

    // Sombrea filas alternas (cada fila = una muestra) en el bloque de
    // datos, fila por fila (setValues no toca el estilo, así que esto hace
    // falta aparte cada vez que se reescriben los datos en el lugar).
    function aplicarBandasFilas(worksheet, numFilas, numCols) {
        for (var f = 1; f <= numFilas; f++) {
            try { worksheet.getRange(f, 0, 1, numCols).setBackgroundColor(f % 2 === 0 ? COLOR_BANDA : null); } catch (e) { /* no crítico */ }
        }
    }

    // La navegación mejorada de Blazor reescribe el <head> y quita el <link> agregado a mano: se vuelve a poner cada vez.
    function asegurarEstilos() {
        if (document.querySelector('link[data-univer-css]')) return;
        var link = document.createElement("link");
        link.rel = "stylesheet";
        link.href = "lib/univer/univer.bundle.min.css";
        link.setAttribute("data-univer-css", "1");
        link.setAttribute("data-permanent", "");
        document.head.appendChild(link);
    }

    function cargarLibreria() {
        asegurarEstilos();
        if (window.SigWebUniverLib) return Promise.resolve();
        if (window._sigwebHojaLibCargando) return window._sigwebHojaLibCargando;

        window._sigwebHojaLibCargando = new Promise(function (resolve, reject) {
            var script = document.createElement("script");
            script.src = "lib/univer/univer.bundle.min.js";
            script.onload = function () { resolve(); };
            script.onerror = function () { reject(new Error("No se pudo cargar el motor de hoja de cálculo.")); };
            document.head.appendChild(script);
        });
        return window._sigwebHojaLibCargando;
    }

    function anchoColumna(titulo) {
        var len = (titulo || "").length;
        return Math.max(72, Math.min(230, len * 9 + 28));
    }

    function valorCelda(v) {
        return (v === undefined || v === null) ? "" : v;
    }

    function construirCellData(columnas, filas) {
        var cellData = {};
        var filaEncabezado = {};
        columnas.forEach(function (c, i) {
            filaEncabezado[i] = { v: c.title, s: { bl: 1, bg: { rgb: "#e9ecef" } } };
        });
        cellData[0] = filaEncabezado;

        // Sin relleno especial por columna "no editable" — se ve nativo,
        // como en Excel. En su lugar, se sombrean filas alternas (cada
        // muestra es una fila) con un gris muy claro, al estilo de una
        // tabla con "filas en bandas".
        filas.forEach(function (fila, r) {
            var filaCelda = {};
            var esImpar = r % 2 === 1;
            columnas.forEach(function (c, i) {
                var celda = { v: valorCelda(fila[c.field]) };
                if (esImpar) celda.s = { bg: { rgb: "#f5f5f5" } };
                filaCelda[i] = celda;
            });
            cellData[r + 1] = filaCelda;
        });
        return cellData;
    }

    function aplicarAnchosYCongelado(worksheet, columnas) {
        columnas.forEach(function (c, i) {
            try { worksheet.setColumnWidth(i, anchoColumna(c.title)); } catch (e) { /* no crítico */ }
        });
        var colsFijas = 0;
        for (var i = 0; i < columnas.length; i++) {
            if (columnas[i].frozen) colsFijas = i + 1; else break;
        }
        try {
            worksheet.setFreeze({ startRow: 1, startColumn: colsFijas, xSplit: colsFijas, ySplit: 1 });
        } catch (e) { /* no crítico */ }
    }

    // Guarda en el servidor una celda editada (debounced por celda, porque
    // Univer dispara varios eventos intermedios mientras se escribe/confirma
    // una fórmula) y, si la respuesta trae columnas calculadas (Área,
    // Cumplimiento, Seguimiento...), las aplica a esa misma fila.
    function programarGuardado(estado, fila, col) {
        var clave = fila + ":" + col;
        if (estado.pendientes[clave]) clearTimeout(estado.pendientes[clave]);
        estado.pendientes[clave] = setTimeout(function () {
            delete estado.pendientes[clave];
            if (!estado.dotNetRef) return;

            var filaDatos = estado.filas[fila - 1];
            var columna = estado.columnas[col];
            if (!filaDatos || !columna || !columna.editor) return;

            var valor = estado.worksheet.getRange(fila, col).getValue();
            var valorTexto = (valor === null || valor === undefined) ? "" : String(valor);
            var id = filaDatos.__Id;

            estado.dotNetRef.invokeMethodAsync("CeldaEditada", id, columna.field, valorTexto).then(function (actualizacion) {
                if (!actualizacion) return;
                Object.keys(actualizacion).forEach(function (claveActualizada) {
                    var colIdx = estado.columnas.findIndex(function (c) { return c.field === claveActualizada; });
                    if (colIdx === -1) return;
                    try { estado.worksheet.getRange(fila, colIdx).setValue(actualizacion[claveActualizada]); } catch (e) { /* ignorar */ }
                });
            }).catch(function (err) {
                console.error("No se pudo guardar la celda editada:", err);
            });
        }, DEBOUNCE_MS);
    }

    // Ninguna celda del bloque de datos se puede editar escribiendo/doble
    // clic/F2 directamente — se cancela SIEMPRE que Univer intente entrar
    // en modo edición dentro de esa área (params.cancel = true), sin
    // importar cómo se disparó. El área de trabajo a la derecha (columnas
    // en blanco para fórmulas sueltas del usuario) no se toca: ahí sí se
    // puede escribir libremente, igual que antes.
    function bloquearEdicionDirecta(estado) {
        estado.univerAPI.addEvent(estado.univerAPI.Event.BeforeSheetEditStart, function (params) {
            var fila = params.row, col = params.column;
            if (fila < 1 || fila > estado.filas.length) return; // encabezado: no aplica
            if (col < 0 || col >= estado.columnas.length) return; // área de trabajo: se deja libre
            params.cancel = true;
        });
    }

    function engancharEdicion(estado) {
        estado.univerAPI.addEvent(estado.univerAPI.Event.SheetValueChanged, function (params) {
            (params.effectedRanges || []).forEach(function (rango) {
                var filaInicio = rango.getRow();
                var colInicio = rango.getColumn();
                var numFilas = typeof rango.getHeight === "function" ? rango.getHeight() : 1;
                var numCols = typeof rango.getWidth === "function" ? rango.getWidth() : 1;
                for (var f = filaInicio; f < filaInicio + numFilas; f++) {
                    if (f < 1 || f > estado.filas.length) continue;
                    for (var c = colInicio; c < colInicio + numCols; c++) {
                        if (c < 0 || c >= estado.columnas.length) continue;
                        if (!estado.columnas[c].editor) continue;
                        programarGuardado(estado, f, c);
                    }
                }
            });
        });
    }

    function crearHoja(idDiv, columnas, filas, dotNetRef) {
        var contenedor = document.getElementById(idDiv);
        if (!contenedor) return;
        contenedor.innerHTML = "";

        var lib = window.SigWebUniverLib;
        var numColsDatos = columnas.length;
        var numFilasReservadas = Math.max(filas.length, 1) + SCRATCH_FILAS_EXTRA;
        var numColsTotal = numColsDatos + GAP_COLS + SCRATCH_COLS;

        var creado = lib.createUniver({
            locale: lib.LocaleType.ES_ES,
            locales: (function () { var o = {}; o[lib.LocaleType.ES_ES] = lib.esES; return o; })(),
            presets: [lib.UniverSheetsCorePreset({ container: idDiv, formulaBar: true })],
        });

        var workbook = creado.univerAPI.createWorkbook({
            id: "sigweb-" + idDiv,
            sheetOrder: ["sheet1"],
            sheets: {
                sheet1: {
                    id: "sheet1",
                    name: "Datos",
                    rowCount: numFilasReservadas,
                    columnCount: numColsTotal,
                    cellData: construirCellData(columnas, filas),
                },
            },
        });

        var worksheet = workbook.getActiveSheet();
        aplicarAnchosYCongelado(worksheet, columnas);

        var estado = {
            contenedor: contenedor,
            univer: creado.univer,
            univerAPI: creado.univerAPI,
            workbook: workbook,
            worksheet: worksheet,
            columnas: columnas,
            filas: filas,
            numFilasReservadas: numFilasReservadas,
            dotNetRef: dotNetRef,
            pendientes: {},
        };
        engancharEdicion(estado);
        bloquearEdicionDirecta(estado);
        window._sigwebHojas[idDiv] = estado;
    }

    // Actualiza los datos de una hoja ya creada sin recrearla (para no
    // perder la posición de scroll ni las fórmulas sueltas que el usuario
    // haya escrito en el área de trabajo) — se usa al filtrar, ordenar o
    // recargar resultados. Si cambiaron las columnas (se agregó/quitó un
    // campo del Tipo de Ensayo), de todos modos se recrea completa.
    function actualizarHoja(estado, columnas, filas) {
        var mismasColumnas = estado.columnas.length === columnas.length &&
            estado.columnas.every(function (c, i) { return c.field === columnas[i].field && !!c.editor === !!columnas[i].editor; });

        if (!mismasColumnas) {
            var idDiv = Object.keys(window._sigwebHojas).find(function (k) { return window._sigwebHojas[k] === estado; });
            destruirHoja(idDiv);
            crearHoja(idDiv, columnas, filas, estado.dotNetRef);
            return;
        }

        var worksheet = estado.worksheet;
        var numCols = columnas.length;
        var filaMaxAnterior = estado.filas.length;

        if (filas.length > estado.numFilasReservadas - SCRATCH_FILAS_EXTRA) {
            // Hay más resultados de los que se reservaron al crear la hoja
            // (carga nueva más grande que antes) — se agranda antes de
            // escribir, sin tocar el área de trabajo (queda más abajo).
            var nuevoTotal = filas.length + SCRATCH_FILAS_EXTRA;
            try { worksheet.setRowCount(nuevoTotal); } catch (e) { /* no crítico */ }
            estado.numFilasReservadas = nuevoTotal;
        }

        if (filas.length > 0) {
            var matriz = filas.map(function (fila) {
                return columnas.map(function (c) { return valorCelda(fila[c.field]); });
            });
            worksheet.getRange(1, 0, filas.length, numCols).setValues(matriz);
            aplicarBandasFilas(worksheet, filas.length, numCols);
        }
        if (filaMaxAnterior > filas.length) {
            var vacias = [];
            for (var i = 0; i < (filaMaxAnterior - filas.length); i++) vacias.push(new Array(numCols).fill(""));
            worksheet.getRange(1 + filas.length, 0, filaMaxAnterior - filas.length, numCols).setValues(vacias);
        }

        estado.filas = filas;
        estado.columnas = columnas;
        estado.dotNetRef = estado.dotNetRef || null;
    }

    window.sigwebHojaExcel = function (idDiv, columnas, filas, dotNetRef) {
        asegurarEstilos();
        var existente = window._sigwebHojas[idDiv];
        // Instancia de una página anterior (el contenedor ya no es el que está en el DOM): se destruye y se crea de cero.
        if (existente && (existente.contenedor !== document.getElementById(idDiv) || !existente.contenedor.isConnected)) {
            destruirHoja(idDiv);
            existente = null;
        }
        if (existente) {
            existente.dotNetRef = dotNetRef;
            actualizarHoja(existente, columnas, filas);
            return Promise.resolve();
        }
        return cargarLibreria().then(function () {
            // Puede haberse destruido mientras cargaba la librería (cambio
            // rápido de pestaña) — no crear sobre un contenedor que ya no
            // está en esta vista.
            if (!document.getElementById(idDiv)) return;
            crearHoja(idDiv, columnas, filas, dotNetRef);
        }).catch(function (err) {
            console.error(err);
            var contenedor = document.getElementById(idDiv);
            if (contenedor) {
                contenedor.innerHTML = '<p class="text-danger small p-2">No se pudo cargar la vista Excel. Intenta recargar la página.</p>';
            }
        });
    };

    // IMPORTANTE: se destruye con estado.univer.dispose() (el objeto que
    // devuelve createUniver en "univer", no el "univerAPI"). Llamar
    // univerAPI.dispose() aquí deja el desmontaje de React a medias (lanza
    // "removeChild... no es hijo de este nodo") y, encima, limpiar el
    // contenedor a mano después agrava el problema: la próxima vez que se
    // crea la hoja en el mismo div, el CSS de Univer queda aplicado solo a
    // medias (la barra de herramientas se ve sin estilo, en una columna) y
    // las ediciones dejan de confirmarse bien. univer.dispose() desmonta
    // todo correctamente solo, así que no hace falta tocar el DOM después.
    function destruirHoja(idDiv) {
        var estado = window._sigwebHojas[idDiv];
        if (!estado) return;
        Object.keys(estado.pendientes).forEach(function (k) { clearTimeout(estado.pendientes[k]); });
        try { estado.univer.dispose(); } catch (e) { /* ya destruida */ }
        delete window._sigwebHojas[idDiv];
    }

    window.sigwebHojaExcelDestruir = destruirHoja;

    // Usado por el botón "✎ Editar muestra" (ver EnsayosDatos.razor): como
    // el bloque de datos no se edita escribiendo directo en la celda, se
    // identifica a qué resultado corresponde la celda donde está el cursor
    // ahora mismo y se abre el diálogo Agregar/Editar para ese resultado,
    // con sus datos ya cargados. Devuelve null si no hay una celda activa
    // sobre una fila de datos (ej. está sobre el encabezado o el área de
    // trabajo de fórmulas).
    window.sigwebHojaExcelFilaActiva = function (idDiv) {
        var estado = window._sigwebHojas[idDiv];
        if (!estado) return null;
        try {
            var seleccion = estado.worksheet.getSelection();
            var rango = seleccion && seleccion.getActiveRange ? seleccion.getActiveRange() : null;
            if (!rango) return null;
            var fila = rango.getRow();
            var datos = estado.filas[fila - 1];
            return (datos && typeof datos.__Id !== "undefined") ? datos.__Id : null;
        } catch (e) {
            return null;
        }
    };
})();
