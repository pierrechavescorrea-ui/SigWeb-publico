// Exportar listas/tablas: lee la tabla tal como se ve (columnas visibles, filtro y orden
// actuales) y la imprime como PDF desde el navegador. El Excel lo arma el servidor.
(function () {
    const excluir = '.modal-box-custom, .modal-backdrop-custom, .pc-capa, .no-exportar';

    function textoCelda(c) {
        const n = c.cloneNode(true);
        // Controles: su valor en vez del texto.
        c.querySelectorAll('input, select, textarea').forEach((el, i) => {
            const copia = n.querySelectorAll('input, select, textarea')[i];
            let v = '';
            if (el.type === 'checkbox' || el.type === 'radio') v = el.checked ? 'S\u00ed' : 'No';
            else if (el.tagName === 'SELECT') v = el.selectedIndex >= 0 ? el.options[el.selectedIndex].text : '';
            else v = el.value;
            copia.replaceWith(document.createTextNode(v));
        });
        n.querySelectorAll('button, .btn-fijar-col, .no-exportar, svg').forEach(x => x.remove());
        return (n.textContent || '').replace(/[\u25b2\u25bc]/g, '').replace(/\s+/g, ' ').trim();
    }

    function buscar(selector) {
        for (const sel of selector ? [selector] : ['table.tabla-pin', 'table.mpie', 'table.table']) {
            const t = [...document.querySelectorAll(sel)].find(x => x.offsetParent !== null && !x.closest(excluir));
            if (t) return t;
        }
        return null;
    }

    // Expande rowspan/colspan a una grilla de textos.
    function grilla(filas) {
        const g = [];
        filas.forEach((tr, r) => {
            g[r] = g[r] || [];
            let c = 0;
            [...tr.children].forEach(td => {
                while (g[r][c] !== undefined) c++;
                const txt = textoCelda(td), rs = td.rowSpan || 1, cs = td.colSpan || 1;
                for (let i = 0; i < rs; i++)
                    for (let j = 0; j < cs; j++) {
                        g[r + i] = g[r + i] || [];
                        g[r + i][c + j] = txt;
                    }
                c += cs;
            });
        });
        return g;
    }

    // { encabezados: [...], filas: [[...]] } — sin columnas de acciones (encabezado vacío).
    window.leerTablaExportar = (selector) => {
        const t = buscar(selector);
        if (!t) return null;
        let filasCab = [...t.querySelectorAll('thead tr')];
        if (!filasCab.length && t.querySelector('tr')) filasCab = [t.querySelector('tr')];
        const gc = grilla(filasCab);
        const ancho = Math.max(0, ...gc.map(f => f.length));
        const enc = [];
        for (let c = 0; c < ancho; c++) {
            const partes = [];
            gc.forEach(f => { const v = f[c]; if (v && partes[partes.length - 1] !== v) partes.push(v); });
            enc.push(partes.join(' \u00b7 '));
        }
        const usar = enc.map((e, i) => e.length > 0 ? i : -1).filter(i => i >= 0);
        const cuerpo = [...t.querySelectorAll('tbody tr')].filter(tr => tr.offsetParent !== null);
        const gb = grilla(cuerpo);
        const filas = [];
        gb.forEach((f, r) => {
            const tr = cuerpo[r];
            if (tr && tr.children.length === 1 && tr.children[0].colSpan > 1) return;   // fila de "sin datos"
            filas.push(usar.map(i => f[i] ?? ''));
        });
        return { encabezados: usar.map(i => enc[i]), filas };
    };

    const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

    // PDF: documento aparte con la tabla y el diálogo de impresión del navegador (Guardar como PDF).
    window.imprimirTablaExportar = (titulo, datos) => {
        if (!datos) return;
        const f = document.createElement('iframe');
        f.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(f);
        const fecha = new Date().toLocaleString('es-CO', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
        const html = `<!doctype html><html><head><meta charset="utf-8"><title>${esc(titulo)}</title><style>
            @page { size: landscape; margin: 12mm; }
            body { font: 10px/1.35 'Inter', Arial, sans-serif; color: #111; }
            h1 { font-size: 14px; margin: 0 0 2px; } .f { color: #666; margin-bottom: 8px; }
            table { border-collapse: collapse; width: 100%; }
            th { background: #1f2a3a; color: #fff; text-align: left; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            th, td { border: 1px solid #c9d0da; padding: 3px 5px; vertical-align: top; }
            tr:nth-child(even) td { background: #f4f6f9; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
            thead { display: table-header-group; }
        </style></head><body><h1>${esc(titulo)}</h1><div class="f">${esc(fecha)} \u00b7 ${datos.filas.length} fila(s)</div>
        <table><thead><tr>${datos.encabezados.map(h => `<th>${esc(h)}</th>`).join('')}</tr></thead>
        <tbody>${datos.filas.map(r => `<tr>${r.map(c => `<td>${esc(c)}</td>`).join('')}</tr>`).join('')}</tbody></table></body></html>`;
        const d = f.contentDocument;
        d.open(); d.write(html); d.close();
        setTimeout(() => {
            f.contentWindow.focus(); f.contentWindow.print();
            setTimeout(() => f.remove(), 1000);
        }, 250);
    };
})();
