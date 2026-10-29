"""Lee las burbujas de marca (bloque POS + NOTA), sus leaders y la lista de materiales (bloques PERFILES/LAMINAS)
de los DXF de los planos DIS6052 y las vincula a los miembros de torre-bp5/bp6.json por coincidencia geométrica
(traslación de cada vista del plano -> proyección del modelo; tolerancia 40 mm).
Uso: python3 -I marcas_bp.py <carpeta con DXF> <carpeta de modelos3d> """
import sys, re, glob, json, math, itertools, collections, os
import numpy as np, ezdxf
from ezdxf import recover

DXF, OUT = sys.argv[1], sys.argv[2]
TOL = 40.0

def leer_hoja(fn):
    d, _ = recover.readfile(fn); ms = d.modelspace()
    L, bub, lead, bom = [], [], [], []
    for e in ms:
        t = e.dxftype()
        if t == 'LINE' and e.dxf.layer == 'EJES':
            a, b = e.dxf.start, e.dxf.end
            if math.hypot(b.x - a.x, b.y - a.y) > 100: L.append((a.x, a.y, b.x, b.y))
        elif t == 'INSERT':
            at = {a.dxf.tag: a.dxf.text for a in e.attribs}
            if e.dxf.layer == 'POS' and at.get('POS'):
                bub.append((e.dxf.insert.x, e.dxf.insert.y, at['POS'].strip(), (at.get('NOTA') or '').strip()))
            elif e.dxf.name in ('PERFILES', 'LAMINAS'):
                bom.append((e.dxf.name, at))
        elif t == 'LEADER':
            v = [(p[0], p[1]) for p in e.vertices]
            if len(v) >= 2: lead.append(v)
    return L, bub, lead, bom

def seg_dist(P, A, B):
    """distancia de puntos P (n,2) a segmentos A-B (m,2) -> (n,m)"""
    AB = B - A; l2 = np.maximum((AB ** 2).sum(1), 1e-9)
    t = np.clip(((P[:, None, :] - A[None]) * AB[None]).sum(2) / l2[None], 0, 1)
    C = A[None] + t[..., None] * AB[None]
    return np.hypot(*(P[:, None, :] - C).transpose(2, 0, 1))

def ang(S):
    return np.degrees(np.arctan2(S[:, 3] - S[:, 1], S[:, 2] - S[:, 0])) % 180

def dang(a, b):
    d = np.abs(a[:, None] - b[None]); return np.minimum(d, 180 - d)

def proyecciones(mi):
    P = {'XZ': [], 'YZ': [], 'XY': []}
    for i, m in enumerate(mi):
        a, b = m['a'], m['b']
        P['XZ'].append((a[0], a[1], b[0], b[1], i)); P['YZ'].append((a[2], a[1], b[2], b[1], i))
        if abs(m['dy_mm']) < 1: P['XY'].append((a[0], a[2], b[0], b[2], i))
    out = {}
    for k, v in P.items():
        v = np.array(v, float)
        v = v[np.hypot(v[:, 2] - v[:, 0], v[:, 3] - v[:, 1]) > 30] if len(v) else v
        out[k] = v
    return out

def candidatos(S, M, n=14):
    """traslaciones sheet->modelo por votación de extremos de líneas paralelas"""
    if len(M) == 0: return []
    aS, aM = ang(S), ang(M[:, :4]); D = dang(aS, aM) < 1.5
    V = collections.Counter()
    for i, j in zip(*np.nonzero(D)):
        for p in ((S[i, 0], S[i, 1]), (S[i, 2], S[i, 3])):
            for q in ((M[j, 0], M[j, 1]), (M[j, 2], M[j, 3])):
                V[(round((q[0] - p[0]) / 15), round((q[1] - p[1]) / 15))] += 1
    return [(k[0] * 15.0, k[1] * 15.0, c) for k, c in V.most_common(n) if c >= 6]

def soportada(s, t, M, aM):
    """la línea s del plano, trasladada, cae sobre segmentos paralelos del modelo en toda su longitud"""
    s = np.array(s, float); a = ang(s[None])[0]
    ok = np.abs(((aM - a) + 90) % 180 - 90) < 2
    if not ok.any(): return False
    A = M[ok][:, :2]; B = M[ok][:, 2:4]
    f = np.linspace(.1, .9, 7)
    P = np.stack([s[0] + f * (s[2] - s[0]) + t[0], s[1] + f * (s[3] - s[1]) + t[1]], 1)
    return bool((seg_dist(P, A, B).min(1) <= TOL).all())

def marca_de(bub):
    return bub[2] + bub[3]

def principal():
    hojas = {}
    for fn in sorted(glob.glob(os.path.join(DXF, '*DIS6052-H*.dxf'))):
        h = int(re.search(r'DIS6052-H(\d+)', fn).group(1)); hojas[h] = leer_hoja(fn)
    print('hojas leídas', len(hojas))
    # lista de materiales
    BOM = {}
    for h, (_, _, _, bom) in hojas.items():
        for nom, at in bom:
            pos = (at.get('POS') or '').strip()
            if not pos: continue
            clase = 'PERFIL' if nom == 'PERFILES' else 'LÁMINA'
            per = (at.get('DES') or '').strip()
            if nom == 'LAMINAS' and at.get('ANC'): per = f"{per} x {at['ANC'].strip()}"
            BOM.setdefault((h, pos), []).append({'hoja': h, 'hoja_txt': str(h), 'clase': clase, 'marca': pos, 'perfil': per,
                'long_mm': (at.get('LON') or '').strip(), 'cant': (at.get('CAN') or '').strip(), 'acero': (at.get('CAL') or '').strip(),
                'conjunto': (at.get('CON') or '').strip(), 'obs': (at.get('OBS') or '').strip()})
    for cod in ('bp6', 'bp5'):
        ruta = os.path.join(OUT, f'torre-{cod}.json'); mod = json.load(open(ruta, encoding='utf-8'))
        mi = mod['miembros']; PR = proyecciones(mi)
        # restricción por cara: la vista XZ solo ve caras transversales, YZ las longitudinales, XY los horizontales
        permit = {'XZ': lambda m: m['cara'].startswith('Transversal') or 'Riostra' in m['cara'], 'YZ': lambda m: m['cara'].startswith('Longitudinal'), 'XY': lambda m: True}
        for k in PR: PR[k] = np.array([r for r in PR[k] if permit[k](mi[int(r[4])])]) if len(PR[k]) else PR[k]
        asign = collections.defaultdict(set); sinres = 0; total = 0; cat = {}; est = collections.defaultdict(lambda: [0, 0])
        # hojas que describen ESTE modelo: cabeza/viga/delta (1-8), extensión común (15), cuerpo propio y pata de 12 m
        PERMITIDAS = {1, 2, 3, 4, 5, 6, 7, 8, 15, 27, 28, 18 if cod == 'bp5' else 19}
        for h, (L, bub, lead, bom) in hojas.items():
            if h not in PERMITIDAS or not L or not bub: continue
            S = np.array(L, float)
            cand = []
            for k, M in PR.items():
                for tx, ty, c in candidatos(S, M): cand.append((k, tx, ty, c))
            for b in bub:
                total += 1; est[h][0] += 1
                bx, by = b[0], b[1]
                # leader asociado: el que tiene un extremo junto a la burbuja
                tgt = None; best = 1e9
                for v in lead:
                    for p, q in ((v[0], v[-1]), (v[-1], v[0])):
                        d = math.hypot(p[0] - bx, p[1] - by)
                        if d < 700 and d < best: best, tgt = d, q
                if tgt is None: tgt = (bx, by)
                P = np.array([tgt]); d = seg_dist(P, S[:, :2], S[:, 2:4])[0]
                idx = [i for i in np.argsort(d)[:3] if d[i] <= 260]
                if not idx: sinres += 1; continue
                mejor = None
                for i in idx:
                    s = S[i]
                    for (k, tx, ty, c) in cand:
                        M = PR[k]
                        if len(M) == 0 or not soportada(s, (tx, ty), M[:, :4], ang(M[:, :4])): continue
                        # apoyo local: líneas del plano a ≤ 2500 de la diana también soportadas
                        loc = [j for j in range(len(S)) if seg_dist(np.array([[ (S[j,0]+S[j,2])/2, (S[j,1]+S[j,3])/2 ]]), np.array([[tgt[0], tgt[1]]]), np.array([[tgt[0], tgt[1]]]))[0, 0] <= 2500]
                        ap = sum(1 for j in loc if soportada(S[j], (tx, ty), M[:, :4], ang(M[:, :4])))
                        sc = (ap, -d[i])
                        if mejor is None or sc > mejor[0]: mejor = (sc, i, k, tx, ty)
                if mejor is None or mejor[0][0] < 3: sinres += 1; continue
                _, i, k, tx, ty = mejor; s = S[i]; M = PR[k]
                # miembros que coinciden con la línea (colineales y solapados)
                sa = ang(s[None])[0]
                hit = []
                for r in M:
                    pa = np.array([[r[0] - tx, r[1] - ty], [r[2] - tx, r[3] - ty], [(r[0] + r[2]) / 2 - tx, (r[1] + r[3]) / 2 - ty]])
                    if abs(((ang(np.array([r[:4]]))[0] - sa) + 90) % 180 - 90) > 2: continue
                    ds = seg_dist(pa, s[None, :2], s[None, 2:4])[:, 0]
                    if ds[2] <= TOL and ds[0] <= TOL and ds[1] <= TOL: hit.append(int(r[4]))   # miembro dentro de la línea
                    elif ds[2] <= TOL or (ds[0] <= TOL and ds[1] <= TOL): hit.append(int(r[4]))
                # clave de lista de materiales
                nom = marca_de(b)
                filas = BOM.get((h, nom)) or BOM.get((h, b[2])) or []
                key = f'{h}|{nom}'
                if key not in cat:
                    f = filas[0] if filas else {}
                    cat[key] = {'marca': nom, 'hoja': h, 'clase': f.get('clase'), 'perfil': f.get('perfil'), 'long_mm': f.get('long_mm'),
                                'cant': f.get('cant'), 'acero': f.get('acero'), 'conjunto': f.get('conjunto'), 'en_lista': bool(filas)}
                est[h][1] += 1
                for j in hit: asign[j].add(key)
        print(cod, {h: tuple(v) for h, v in sorted(est.items())})
        print(cod, 'burbujas', total, 'sin resolver', sinres, 'miembros con marca', len(asign))
        # escribir
        def coh(keys, eje):
            Ls = [int(cat[k]['long_mm']) for k in keys if cat[k]['clase'] == 'PERFIL' and str(cat[k]['long_mm']).isdigit()]
            if not Ls: return 'n/a', 'Marca(s) sin perfil con longitud numérica (lámina, accesorio o no listada).'
            for r in (1, 2, 3):
                for c in itertools.combinations(Ls, r):
                    if abs(sum(c) - eje) <= max(200, 0.03 * eje): return 'ok', 'Longitud de pieza ' + ' + '.join(map(str, c)) + f' mm ≈ eje del modelo {eje} mm.'
            return 'revisar', f"Ninguna longitud de pieza ({', '.join(map(str, Ls))} mm) coincide con el eje del modelo ({eje} mm): vínculo por revisar."
        for j, keys in asign.items():
            m = mi[j]; ks = sorted(keys, key=lambda k: (cat[k]['hoja'], cat[k]['marca']))
            if 'marca_orig_nombre' not in m: m['marca_orig_nombre'] = m['nombre']
            m['marcas'] = ks; m['marca'] = ' / '.join(cat[k]['marca'] for k in ks)
            m['nombre'] = f"{m['marca']} · {m['marca_orig_nombre']}"
            m['coherencia'], m['coherencia_txt'] = coh(ks, m['longitud_mm'])
            m['verificacion'] = ('Marca leída del plano H' + str(cat[ks[0]]['hoja']) + ' (burbuja → leader → eje de la vista → coincidencia con el modelo ≤ ' + str(int(TOL)) + ' mm).')
            m.pop('nota', None)
        for m in mi:
            if 'marca_orig_nombre' in m and 'marcas' not in m: m.pop('marca_orig_nombre')
        mod['marcas'] = cat
        usadas = PERMITIDAS
        mod['lista_materiales'] = [f for (h, _), fs in sorted(BOM.items()) if h in usadas for f in fs]
        sup = [s for s in mod['meta'].get('supuestos', []) if 'No se han leído aún las marcas' not in s]
        sup.append('Marcas: solo aparecen en los miembros donde la burbuja del plano, su leader y el eje de la vista coinciden con un miembro del modelo (≤ 40 mm); los miembros sin marca no tienen vínculo verificado, aunque su marca exista en la lista de materiales.')
        mod['meta']['supuestos'] = sup; mod['meta']['marcas_vinculadas'] = len(asign)
        for m in mi: m.pop('marca_orig_nombre', None)
        json.dump(mod, open(ruta, 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
        print(cod, collections.Counter(m.get('coherencia') for m in mi))

principal()
