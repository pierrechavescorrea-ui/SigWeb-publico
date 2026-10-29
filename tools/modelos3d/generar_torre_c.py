#!/usr/bin/env python3
"""
Genera wwwroot/modelos3d/torre-c1..c6.json (torre tipo C, EEB-U113-CT100501-L000-DIS6022 H01–H19).

Uso: python3 generar_torre_c.py <carpeta con H1.dxf … H36.dxf> <carpeta modelos3d> <LISTADO_DE_MATERIALES_TORRE_C.xls>
     (DXF: dwg2dxf -y -o H10.dxf <plano>.dwg)

Método (ver 'supuestos' y 'validacion' del JSON):
 1. Cuerpos y extensiones: vista de cara (verdadero tamaño, ejes de la capa m-ejes). Cada vista se ubica con su
    nudo superior (amarre horizontal) y el ancho H= del plano; la cota sale del tronco de pirámide
    (bloque 'func': pendiente 0,13636486 por vertical, 0,13511439 en cara, VC 1,00925486). Y=0 en el tope del cuerpo
    (ancho 6000, sección I-I). Las vistas de media cara se espejan respecto al eje.
 2. Composición: el conjunto de cada pieza (bloques PERFILES/LAMINAS) dice a qué torres pertenece
    ('EXTENSION CUERPO C1 C3 Y C5' -> C1, C3, C5); un miembro con marca solo entra si su conjunto entra.
 3. Cabeza (deltas tramo 1 y 2, viga y brazos): vistas frontales en X-Y (verdadero tamaño), profundidad por el
    perfil de anchos de las caras laterales; las caras laterales se ubican interpolando entre sus montantes.
 4. Marcas: burbuja 'pos' -> leader -> eje m-ejes a <=130 mm.
"""
import sys, os, re, json, math, collections
from ezdxf import recover

DXFDIR, OUTDIR, XLSPATH = sys.argv[1], sys.argv[2], sys.argv[3]
SV, SF, VC = 0.13636486, 0.13511439, 1.00925486        # bloque 'func' de los planos
W0 = 6000.0                                             # ancho en el tope del cuerpo (sección I-I)

def seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]; L2 = dx * dx + dy * dy
    t = 0 if L2 == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L2))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)
def lon(l): return math.hypot(l[2] - l[0], l[3] - l[1])
def num(s, d=0.0):
    try: return float(str(s).replace(",", "."))
    except Exception: return d

def ang(l):
    th = math.atan2(l[3] - l[1], l[2] - l[0]) % math.pi
    return th

def merge(TR,tol=4.0,gap=40.0):
    """une segmentos colineales solapados/contiguos -> lineas maximales"""
    buckets=collections.defaultdict(list)
    for (x1,y1,x2,y2) in TR:
        L=math.hypot(x2-x1,y2-y1)
        th=math.atan2(y2-y1,x2-x1)
        if th<0 or (abs(th)<1e-9 and False): 
            if th<0: th+=math.pi
        if th>=math.pi-1e-9: th-=math.pi
        d=-x1*math.sin(th)+y1*math.cos(th)
        buckets[(round(th/0.004),round(d/tol))].append((x1,y1,x2,y2,th))
    # fusionar buckets vecinos
    keys=sorted(buckets)
    out=[]
    used=set()
    for k in keys:
        if k in used: continue
        grp=list(buckets[k]); used.add(k)
        for dk in((0,1),(1,0),(1,1),(1,-1),(0,-1),(-1,0)):
            kk=(k[0]+dk[0],k[1]+dk[1])
            if kk in buckets and kk not in used: grp+=buckets[kk]; used.add(kk)
        th=grp[0][4]; c,s=math.cos(th),math.sin(th)
        iv=sorted((min(g[0]*c+g[1]*s,g[2]*c+g[3]*s),max(g[0]*c+g[1]*s,g[2]*c+g[3]*s),g) for g in grp)
        cur=None
        for a,b,g in iv:
            if cur and a<=cur[1]+gap: cur[1]=max(cur[1],b); cur[2].append(g)
            else:
                if cur: out.append(cur+[th])
                cur=[a,b,[g]]
        if cur: out.append(cur+[th])
    res=[]
    for a,b,gs,th in out:
        c,s=math.cos(th),math.sin(th)
        # punto de la recta: promedio de d
        d=sum(-g[0]*s+g[1]*c for g in gs)/len(gs)
        p1=(a*c-d*s,a*s+d*c); p2=(b*c-d*s,b*s+d*c)
        res.append((p1[0],p1[1],p2[0],p2[1]))
    return res

def emparejar(L, dmin=20, dmax=125):
    """cada angulo trae 2 aristas paralelas -> un eje (promedio). L: lineas fusionadas."""
    usado = set(); ejes = []
    cand = []
    for i in range(len(L)):
        for j in range(i + 1, len(L)):
            a, b = L[i], L[j]
            da = abs(ang(a) - ang(b)); da = min(da, math.pi - da)
            if da > 0.012: continue
            th = ang(a); c, s = math.cos(th), math.sin(th)
            d = abs((b[0] - a[0]) * -s + (b[1] - a[1]) * c)
            if not dmin <= d <= dmax: continue
            a0, a1 = sorted((a[0] * c + a[1] * s, a[2] * c + a[3] * s))
            b0, b1 = sorted((b[0] * c + b[1] * s, b[2] * c + b[3] * s))
            ov = min(a1, b1) - max(a0, b0)
            if ov >= 0.55 * min(a1 - a0, b1 - b0) and ov > 60:
                cand.append((-ov, i, j))
    for _, i, j in sorted(cand):
        if i in usado or j in usado: continue
        usado.update((i, j))
        a, b = L[i], L[j]
        th = ang(a); c, s = math.cos(th), math.sin(th)
        t = [a[0] * c + a[1] * s, a[2] * c + a[3] * s, b[0] * c + b[1] * s, b[2] * c + b[3] * s]
        da = -a[0] * s + a[1] * c; db = -b[0] * s + b[1] * c; d = (da + db) / 2
        t0, t1 = min(t), max(t)
        ejes.append((t0 * c - d * s, t0 * s + d * c, t1 * c - d * s, t1 * s + d * c))
    ejes += [L[i] for i in range(len(L)) if i not in usado]
    return ejes


# ------------------------------------------------------------------ lectura de hojas
HOJ = {}
for n in range(1, 37):
    p = os.path.join(DXFDIR, f"H{n}.dxf")
    if not os.path.exists(p): continue
    ms = recover.readfile(p)[0].modelspace()
    ax, bub, lead, filas, conjs, cen, dib = [], [], [], [], [], [], []
    for e in ms:
        t = e.dxftype()
        if t == "LINE" and e.dxf.layer.lower() in ("m-ejes", "center"):
            l = (e.dxf.start.x, e.dxf.start.y, e.dxf.end.x, e.dxf.end.y)
            if lon(l) > 10: (ax if e.dxf.layer.lower() == "m-ejes" or n >= 25 else cen).append(l)
        elif t == "LINE" and e.dxf.layer in ("DIB", "4") and n >= 25:
            dib.append((e.dxf.start.x, e.dxf.start.y, e.dxf.end.x, e.dxf.end.y))
        elif t == "LWPOLYLINE" and e.dxf.layer in ("DIB", "4") and n >= 25:
            pts = list(e.get_points("xy"))
            for p_, q_ in zip(pts, pts[1:]): dib.append((p_[0], p_[1], q_[0], q_[1]))
        elif t == "LEADER":
            lead.append([(q[0], q[1]) for q in e.vertices])
        elif t == "INSERT":
            nm = e.dxf.name; a = {x.dxf.tag: x.dxf.text.strip() for x in e.attribs}
            if nm.lower() == "pos":
                bub.append((e.dxf.insert.x, e.dxf.insert.y, a.get("POS", "") + a.get("NOTA", "")))
            elif nm in ("PERFILES", "LAMINAS", "UNIDADES") and a.get("POS"):
                filas.append((nm, a))
            elif nm == "CONJUNTO" and a.get("NOM"):
                conjs.append(a["NOM"].strip())
    # quitar ejes duplicados exactos
    vistos, ax2 = set(), []
    for l in ax:
        k = tuple(sorted([(round(l[0]), round(l[1])), (round(l[2]), round(l[3]))]))
        if k not in vistos: vistos.add(k); ax2.append(l)
    # marcas: burbuja -> leader -> eje
    mk = collections.defaultdict(list)
    if ax2 and bub:
        for v in lead:
            ends = [v[0], v[-1]]
            db = [min(math.hypot(q[0] - b[0], q[1] - b[1]) for b in bub) for q in ends]
            ti = 0 if db[0] > db[1] else 1
            tip, base = ends[ti], ends[1 - ti]
            b = min(bub, key=lambda b: math.hypot(b[0] - base[0], b[1] - base[1]))
            i = min(range(len(ax2)), key=lambda i: seg_dist(tip, ax2[i][:2], ax2[i][2:]))
            if seg_dist(tip, ax2[i][:2], ax2[i][2:]) < 130 and b[2] and b[2] not in mk[i]: mk[i].append(b[2])
    HOJ[n] = dict(ax=ax2, mk=mk, filas=filas, conjs=conjs, dib=dib)

# ------------------------------------------------------------------ lista de materiales (listado rev. A8) y composición (hoja RESUMEN)
import unicodedata
def norm(t): return re.sub(r"[^A-Z0-9]", "", unicodedata.normalize("NFKD", str(t)).encode("ascii", "ignore").decode().upper())
def leer_listado(path):
    import xlrd
    bk = xlrd.open_workbook(path)
    sh = bk.sheet_by_name("TORRE C")
    def txt(r, c):
        v = sh.cell_value(r, c)
        if isinstance(v, float) and v.is_integer(): return str(int(v))
        return str(v).strip()
    filas, plano, nombre, cab = [], None, None, []
    for r in range(sh.nrows):
        c0, c1, c4, c5 = txt(r, 0), txt(r, 1), txt(r, 4), sh.cell_value(r, 5)
        if "PLANO" in c4:
            m = re.search(r"H(\d+)", c4); plano = int(m.group(1)) if m else plano
            nombre = c1 or txt(r + 1, 1) or nombre; cab.append((nombre, plano))
        elif not c0 and c1 and c1 != "OTROS" and not c1.startswith("SIGLA") and c5 == "" and not c1.startswith("PESO"):
            nombre = c1; cab.append((nombre, plano))
        elif c0 and isinstance(c5, float) and nombre:
            cant = sh.cell_value(r, 7); peso = sh.cell_value(r, 8)
            filas.append(dict(conjunto=nombre, hoja=plano, pos=c0, desc=txt(r, 1), calidad=txt(r, 2), dim=txt(r, 3), long=txt(r, 4),
                              cant=cant if isinstance(cant, float) else c5, peso=peso if isinstance(peso, float) else 0.0))
    rs = bk.sheet_by_name("RESUMEN"); mult, peso_res = {}, {}; hj = None
    for r in range(2, rs.nrows):
        d = str(rs.cell_value(r, 1)).strip(); pw = rs.cell_value(r, 2)
        m = re.search(r"HOJA\s*(\d+)", str(rs.cell_value(r, 0)).upper())
        if m: hj = int(m.group(1))
        if d and isinstance(pw, float): peso_res[norm(d)] = pw
        if not d or r > 39: continue
        v = [int(x) if isinstance(x, float) else 0 for x in (rs.cell_value(r, c) for c in range(5, 11))]
        if any(v): mult[(hj, norm(d))] = v
    tot = None
    for r in range(2, min(rs.nrows, 45)):
        if not str(rs.cell_value(r, 1)).strip() and isinstance(rs.cell_value(r, 11), float):
            tot = [rs.cell_value(r, c) for c in range(11, 17)]; break
    peso_res["__TOTAL__"] = tot
    return filas, mult, peso_res, cab
FILAS, MULT_RES, PESO_RES, CABECERAS = leer_listado(XLSPATH)
CONJUNTOS = list(dict.fromkeys(f["conjunto"] for f in FILAS))
HOJA_CONJ = {}
for f in FILAS: HOJA_CONJ.setdefault(f["conjunto"], f["hoja"])
MULT = {}
_ult = {}
for c, h in CABECERAS:
    if c in MULT: continue
    nc = norm(c)
    m = next((v for (hh, k_), v in MULT_RES.items() if k_ == nc), None) or \
        next((v for (hh, k_), v in MULT_RES.items() if hh == h and (k_.startswith(nc) or nc.startswith(k_))), None)
    if m is None and c.upper().startswith("ELEMENTOS DE MONTAJE"):
        t = re.findall(r"C(\d)", c.upper()); m = [1 if str(i + 1) in t else 0 for i in range(6)] if t else None
    if m is None and nc.startswith("ANTIESCALATORIO"): m = [1] * 6
    if m is None: m = _ult.get(h)                     # subconjuntos sin fila propia (CONJUNTO AA01, CORTE H-H…): los del conjunto anterior del plano
    if m is not None: MULT[c] = m; _ult[h] = m
POR_POS = collections.defaultdict(list)
for f in FILAS: POR_POS[(f["hoja"], f["pos"])].append(f)
POR_POS_G = collections.defaultdict(set)
for f in FILAS: POR_POS_G[f["pos"]].add((f["hoja"], f["pos"]))

def ubicar(n, mk_):
    for p in (mk_, mk_ + "H", mk_.rstrip("H")):
        if (n, p) in POR_POS: return (n, p)
    for p in (mk_, mk_ + "H", mk_.rstrip("H")):
        if len(POR_POS_G.get(p, ())) == 1: return next(iter(POR_POS_G[p]))
    return None

def incluye(k, n, marca):
    u = ubicar(n, marca)
    if not u: return True
    return any(MULT.get(f["conjunto"], [1] * 6)[k - 1] > 0 for f in POR_POS[u])

PATA_CONJ = ["PATA 12,0m", "PATA 12,0m VISTA INTERNA", "STUB"]
def clase(desc):
    return "PERFIL" if desc.startswith("L ") else "LÁMINA" if desc.startswith("LAM") else "VARILLA" if desc.startswith("VAR") else "TORNILLERÍA"
def bom_torre(k):
    out, peso, pres = [], 0.0, 0.0
    usar = [(c, MULT[c][k - 1], "") for c in CONJUNTOS if c in MULT and MULT[c][k - 1] > 0]
    usar += [(c, 4, "una por esquina (pata de 12,0 m modelada)") for c in PATA_CONJ if c in HOJA_CONJ]
    tot = PESO_RES.get("__TOTAL__")
    pres = tot[k - 1] if tot else 0.0                 # total de la hoja RESUMEN para la torre (sin patas)
    for c in PATA_CONJ: pres += PESO_RES.get(norm(c), 0.0) * 4
    for c, mlt, obs in usar:
        for f in FILAS:
            if f["conjunto"] != c: continue
            cl = clase(f["desc"])
            per = f"{f['desc']} x {f['dim']}" if cl == "LÁMINA" and f["dim"] else f["desc"]
            out.append(dict(hoja=f["hoja"], hoja_txt=str(f["hoja"]), clase=cl, marca=f["pos"], perfil=per, long_mm=f["long"],
                            cant=str(round(num(f["cant"]) * mlt)), acero=f["calidad"], conjunto=c, obs=obs, peso_kg=round(f["peso"] * mlt, 2)))
            peso += f["peso"] * mlt
    return out, peso, pres
OPCIONES_PIE = [dict(conjunto=c, hoja=HOJA_CONJ.get(c), peso_kg=round(PESO_RES.get(norm(c), 0.0), 1)) for c in CONJUNTOS if c.upper().startswith(("PATA", "PARIILLA", "PARRILLA"))]

# ------------------------------------------------------------------ utilidades de vistas
def cluster_de(n, semilla, gap=150):
    ax = HOJ[n]["ax"]
    i0 = min(range(len(ax)), key=lambda i: seg_dist(semilla, ax[i][:2], ax[i][2:]))
    sel, frente = {i0}, [i0]
    while frente:
        i = frente.pop(); a = ax[i]
        for j, b in enumerate(ax):
            if j in sel: continue
            if min(seg_dist(a[:2], b[:2], b[2:]), seg_dist(a[2:], b[:2], b[2:]), seg_dist(b[:2], a[:2], a[2:]), seg_dist(b[2:], a[:2], a[2:])) < gap:
                sel.add(j); frente.append(j)
    return sorted(sel)

def en_caja(l, caja):
    x0, y0, x1, y1 = caja
    return all(x0 <= x <= x1 and y0 <= y <= y1 for x, y in ((l[0], l[1]), (l[2], l[3])))

def rot(X, Z, k): return [(X, Z), (Z, -X), (-X, -Z), (-Z, X)][k]

# ------------------------------------------------------------------ cuerpos y extensiones
# id, hoja, nudo superior (x del montante izquierdo, y), ancho sup/inf (H= del plano), media cara, caras, torres, nombre
MODS = [
    ("E1T", 10, (-1377681, -687095), 6000.0, 6681.82, False, "T", {1, 3, 5}, "Extensión C1-C3-C5"),
    ("E1L", 10, (-1377878, -692074), 6000.0, 6681.82, False, "L", {1, 3, 5}, "Extensión C1-C3-C5"),
    ("E35", 11, (-1375492, -701511), 6681.82, 8318.20, True, "TL", {3, 5}, "Extensión C3-C5"),
    ("E5", 12, (-1377606, -714105), 8318.20, 9136.39, False, "TL", {3, 5}, "Extensión C5 / cuerpo C3"),
    ("E5b", 13, (-1374869, -728538), 9136.39, 10772.77, True, "TL", {5}, "Extensión cuerpo C5"),
    ("B5", 14, (-1377720, -741705), 10772.77, 11590.96, False, "TL", {5}, "Cuerpo C5"),
    ("E246T", 16, (-1286185, -681000), 6000.0, 7909.11, False, "T", {2, 4, 6}, "Extensión C2-C4-C6"),
    ("E246L", 17, (-1286052, -695449), 6000.0, 7909.11, False, "L", {2, 4, 6}, "Extensión C2-C4-C6"),
    ("E46", 19, (-1284709, -725391), 7909.11, 9545.49, True, "TL", {4, 6}, "Extensión C4-C6"),
    ("B46", 20, (-1286825, -740838), 9545.49, 10363.68, False, "TL", {4, 6}, "Cuerpo C4-C6"),
    ("E6", 22, (-1284247, -770562), 10363.68, 12000.05, True, "TL", {6}, "Extensión cuerpo C6"),
    ("B6", 23, (-1286387, -783504), 12000.05, 12818.24, False, "TL", {6}, "Cuerpo C6"),
]
COMPOSICION = {1: ["E1"], 2: ["E246"], 3: ["E1", "E35", "E5"], 4: ["E246", "E46", "B46"], 5: ["E1", "E35", "E5", "E5b", "B5"],
               6: ["E246", "E46", "B46", "E6", "B6"]}       # = hoja RESUMEN del listado
FALTA = {}

def YC(w): return -(w - W0) / (2 * SV)

class Modulo:
    def __init__(s, mid, n, nudo, wt, wb, media, caras, torres, nombre):
        s.id, s.n, s.wt, s.wb, s.media, s.caras, s.torres, s.nombre = mid, n, wt, wb, media, caras, torres, nombre
        s.xl, s.yt = nudo
        s.c = s.xl + wt / 2
        s.vb = -(wb - wt) / (2 * SF)                       # cota en cara del nudo inferior (negativa)
        s.Yt = YC(wt)
        s.ids = cluster_de(n, (s.xl, s.yt - 200))
    def w(s, y): return s.wt - 2 * SF * (y - s.yt)
    def Y(s, y): return s.Yt + (y - s.yt) / VC
    def p3(s, x, y, k):
        X, Z = rot(x - s.c, s.w(y) / 2, k)
        return [round(X), round(s.Y(y)), round(Z)]
    def es_montante(s, l):
        for x, y in ((l[0], l[1]), (l[2], l[3])):
            if min(abs(x - (s.c - s.w(y) / 2)), abs(x - (s.c + s.w(y) / 2))) > 40: return False
        return abs(l[3] - l[1]) > 300
    def dentro(s, l):
        for x, y in ((l[0], l[1]), (l[2], l[3])):
            if not (s.yt + s.vb - 700 <= y <= s.yt + 700): return False
            if abs(x - s.c) > s.w(y) / 2 + 300: return False
        return True
MOD = {m[0]: Modulo(*m) for m in MODS}

# ------------------------------------------------------------------ miembros
def mk(id_, nombre, grupo, tramo, tipo, cara, a, b, fuente, cara_tipo, n=None, marcas=None, comp="comun"):
    a, b = list(a), list(b)
    d = dict(id=id_, nombre=nombre, grupo=grupo, tramo=tramo, tipo=tipo, cara=cara, a=a, b=b,
             longitud_mm=round(math.dist(a, b)), dx_mm=b[0] - a[0], dy_mm=b[1] - a[1], dz_mm=b[2] - a[2],
             fuente=fuente, cara_tipo=cara_tipo, comp=comp)
    if marcas:
        keys, L, conj = [], [], set()
        for x in marcas:
            u = ubicar(n, x) or (n, x)
            keys.append(f"{u[0]}|{u[1]}")
            for f in POR_POS.get(u, []):
                conj.add(f["conjunto"])
                if f["desc"].startswith("L ") and num(f["long"]) > 0: L.append(num(f["long"]))
        d["marcas"] = list(dict.fromkeys(keys)); d["marca"] = " / ".join(dict.fromkeys(k_.split("|", 1)[1] for k_ in keys))
        if conj: d["conjunto"] = " / ".join(sorted(conj))
        if L:
            ok = any(abs(l - d["longitud_mm"]) <= max(250, 0.05 * d["longitud_mm"]) for l in L)
            d["coherencia"] = "ok" if ok else "revisar"
            d["coherencia_txt"] = f"Longitud de pieza {', '.join(str(round(l)) for l in L)} mm vs eje del modelo {d['longitud_mm']} mm."
        else:
            d["coherencia"], d["coherencia_txt"] = "n/a", "Marca sin perfil con longitud numérica (lámina, accesorio o no listada)."
        d["verificacion"] = "Marca leída del plano (burbuja → leader → eje a ≤130 mm); se replica por simetría en las caras equivalentes."
    return d

def miembros_modulo(m, k, pref):
    out = []; ax = HOJ[m.n]["ax"]; mkd = HOJ[m.n]["mk"]
    ks = [0, 2] if m.caras == "T" else [1, 3] if m.caras == "L" else [0, 1, 2, 3]
    # montantes (una vez por esquina: con las caras T se cubren las cuatro esquinas)
    if m.caras in ("T", "TL"):
        for kk in (0, 2):
            for sg in (-1, 1):
                a = (m.c + sg * m.wt / 2, m.yt); yb = m.yt + m.vb; b = (m.c + sg * m.w(yb) / 2, yb)
                out.append(mk(f"{pref}-{m.id}-M{kk}{'LR'[sg > 0]}", f"Montante · {m.nombre}", m.nombre, m.nombre, "Montante",
                              "ABCD"[kk], m.p3(*a, kk), m.p3(*b, kk), f"plano H{m.n} (vista de cara)", "transversal"))
    for j, i in enumerate(m.ids):
        l = ax[i]
        if m.es_montante(l) or not m.dentro(l): continue
        marcas = mkd.get(i, [])
        if marcas and not any(incluye(k, m.n, x) for x in marcas): continue
        en_eje = all(abs(x - m.c) < 5 for x in (l[0], l[2]))
        if en_eje and not marcas: continue                          # eje de simetría del dibujo
        variantes = [((l[0], l[1]), (l[2], l[3]))]
        if m.media and not en_eje: variantes.append(((2 * m.c - l[0], l[1]), (2 * m.c - l[2], l[3])))
        tipo = "Horizontal" if abs(l[3] - l[1]) < 5 else "Diagonal"
        for vi, (a, b) in enumerate(variantes):
            for kk in ks:
                out.append(mk(f"{pref}-{m.id}-{'ABCD'[kk]}-{tipo[0]}{j:03d}{'e' if vi else ''}", f"{tipo} · {m.nombre} · Cara {'ABCD'[kk]}",
                              m.nombre, m.nombre, tipo, "ABCD"[kk], m.p3(*a, kk), m.p3(*b, kk),
                              f"plano H{m.n} (vista de cara{' · espejada' if vi else ''})", "transversal" if kk % 2 == 0 else "longitudinal",
                              n=m.n, marcas=marcas))
    return out

# ------------------------------------------------------------------ cortes en planta (contorno cuadrado)
def cortes_hoja(n):
    ax = HOJ[n]["ax"]; usados = set(); res = []
    hs = [i for i, l in enumerate(ax) if abs(l[3] - l[1]) < 3 and lon(l) > 2500]
    for i in sorted(hs, key=lambda i: ax[i][1]):
        if i in usados: continue
        cl = cluster_de(n, ((ax[i][0] + ax[i][2]) / 2, ax[i][1]))
        L = [ax[j] for j in cl]
        xs = [v for l in L for v in (l[0], l[2])]; ys = [v for l in L for v in (l[1], l[3])]
        W, H = max(xs) - min(xs), max(ys) - min(ys)
        usados |= set(cl)
        vert = [l for l in L if abs(l[2] - l[0]) < 3 and lon(l) > 0.8 * H]
        if abs(W - H) > 0.06 * W or len(vert) < 2 or W < 4000: continue
        res.append(dict(hoja=n, W=W, cx=(min(xs) + max(xs)) / 2, cy=(min(ys) + max(ys)) / 2, ids=cl))
    return res
CORTES = []
for n in (9, 11, 12, 14, 15, 18, 21, 24):
    if n in HOJ: CORTES += cortes_hoja(n)
NIVELES = {}                                   # ancho -> cota Y (nudos de los módulos)
EXTRA_NIVELES = {"E246": [7500.01]}          # H17: H=7500.01 (nivel de la sección J-J)
for m in MOD.values():
    NIVELES[round(m.wt)] = m.Yt; NIVELES[round(m.wb)] = m.Y(m.yt + m.vb)
    for w in EXTRA_NIVELES.get(m.id[:-1] if m.id[-1] in "TL" else m.id, []): NIVELES[round(w)] = YC(w)
def ubicar_corte(c):
    best = min(NIVELES, key=lambda w: abs(w - c["W"]))
    return (best, NIVELES[best]) if abs(best - c["W"]) < 0.025 * best else (None, None)

def miembros_cortes(k, mods, pref):
    out, usados = [], []
    anchos = set()
    for mid in mods:
        for m in MOD.values():
            if m.id.startswith(mid) or m.id == mid:
                anchos |= {round(m.wt), round(m.wb)} | {round(w) for w in EXTRA_NIVELES.get(mid, [])}
    for ci, c in enumerate(CORTES):
        w, Y = ubicar_corte(c)
        if w is None or w not in anchos: continue
        ax = HOJ[c["hoja"]]["ax"]; mkd = HOJ[c["hoja"]]["mk"]
        esc = w / c["W"]; n0 = len(out)
        for i in c["ids"]:
            l = ax[i]; marcas = mkd.get(i, [])
            if marcas and not any(incluye(k, c["hoja"], x) for x in marcas): continue
            a = [round((l[0] - c["cx"]) * esc), round(Y), round((l[1] - c["cy"]) * esc)]
            b = [round((l[2] - c["cx"]) * esc), round(Y), round((l[3] - c["cy"]) * esc)]
            if math.dist(a, b) < 50: continue
            tipo = "Horizontal" if abs(a[0] - b[0]) < 5 or abs(a[2] - b[2]) < 5 else "Diagonal"
            out.append(mk(f"{pref}-S{c['hoja']}{ci}-{i:03d}", f"{tipo} · sección H{c['hoja']}", "Arriostramiento horizontal", f"Sección H{c['hoja']}",
                          tipo, "planta", a, b, f"plano H{c['hoja']} (sección en planta)", "longitudinal", n=c["hoja"], marcas=marcas))
        if len(out) > n0: usados.append(dict(hoja=c["hoja"], ancho_plano_mm=round(c["W"]), ancho_modelo_mm=w, cota_mm=round(Y)))
    return out, usados

# ------------------------------------------------------------------ cabeza
CF = 1 / math.sqrt(1 + 0.1365 ** 2)          # inclinación de la cara frontal de los deltas (H8: 6000 -> 4071 en la altura del tramo 2)
Y_T2 = 7067 * CF                             # tope del delta tramo 2 (H7)
Y_BB = 14008 * CF                            # cordón inferior de la viga = tope del delta tramo 1 (H5)
ZB = 1043.0                                  # semiancho de la viga (H2: 2087)
def ZH(Y):                                   # semiprofundidad de los deltas (H8: 6000 -> 4071; H6: 4308 -> 2086)
    if Y <= Y_T2: return 3000 - (3000 - 2035.5) * Y / Y_T2
    return max(ZB, 2035.5 - (2035.5 - ZB) * (Y - Y_T2) / (Y_BB - Y_T2))
def X_EXT(Y): return -3000 - 2332 / 7067 * (Y / CF)       # montante exterior (H7/H5, recta única)

def bilineal(L1, L2, P1, P2):
    """vista trapecial con montantes L1, L2 (2D, abajo->arriba, misma cota en el dibujo) -> montantes 3D P1, P2 (funciones de s)"""
    yb = (L1[0][1] + L2[0][1]) / 2; yt = (L1[1][1] + L2[1][1]) / 2
    def f(x, y):
        s = (y - yb) / (yt - yb)
        x1 = L1[0][0] + (L1[1][0] - L1[0][0]) * s; x2 = L2[0][0] + (L2[1][0] - L2[0][0]) * s
        t = (x - x1) / (x2 - x1) if abs(x2 - x1) > 1e-6 else 0.5
        A, B = P1(s), P2(s)
        return tuple(A[i] + (B[i] - A[i]) * t for i in range(3))
    return f

def m_dt2_frontal(x, y):
    Y = (y - 9086) * CF; return (x + 149626, Y, ZH(Y))
def m_dt1_frontal(x, y):
    Y = (y - 9200) * CF; X = x + (144037 if x < -146813 else 149589); return (X, Y, ZH(Y))
m_dt2_lat = bilineal(((-135515, 7563), (-134552, 14839)), ((-129516, 7560), (-130481, 14839)),
                     lambda s: (X_EXT(s * Y_T2), s * Y_T2, -ZH(s * Y_T2)), lambda s: (X_EXT(s * Y_T2), s * Y_T2, ZH(s * Y_T2)))
_YC, _YI = 2259 * CF, 6009 * CF              # cruce de montantes interiores y tope del interior (H7)
m_dt2_int = bilineal(((-142100, 7999), (-141582, 14161)), ((-136755, 7999), (-137273, 14157)),
                     lambda s: (-4983 * s, _YC + (_YI - _YC) * s, -ZH(_YC + (_YI - _YC) * s)),
                     lambda s: (-4983 * s, _YC + (_YI - _YC) * s, ZH(_YC + (_YI - _YC) * s)))
_Y1b = 5980 * CF
m_dt1_int = bilineal(((-171367, 15329), (-170256, 23287)), ((-167059, 15331), (-168170, 23287)),
                     lambda s: (-4983, _Y1b + (Y_BB - _Y1b) * s, -ZH(_Y1b + (Y_BB - _Y1b) * s)),
                     lambda s: (-4983, _Y1b + (Y_BB - _Y1b) * s, ZH(_Y1b + (Y_BB - _Y1b) * s)))
_Y1o, _Y1t = 5118 * CF, 14389 * CF
m_dt1_ext = bilineal(((-165603, 14201), (-164452, 22898)), ((-161198, 14201), (-162350, 22899)),
                     lambda s: (X_EXT(_Y1o + (_Y1t - _Y1o) * s), _Y1o + (_Y1t - _Y1o) * s, -ZH(_Y1o + (_Y1t - _Y1o) * s)),
                     lambda s: (X_EXT(_Y1o + (_Y1t - _Y1o) * s), _Y1o + (_Y1t - _Y1o) * s, ZH(_Y1o + (_Y1t - _Y1o) * s)))
XV = 174542.0
def m_viga_frontal(x, y): return (x + XV, Y_BB + (y - 37264), ZB)
def m_viga_sup(x, y): return (x + XV, Y_BB + 2000, y - 47089.5)
def m_viga_inf(x, y): return (x + XV, Y_BB, y - 34733)
XB = 8348.0
def m_brazo_frontal(x, y):
    X = XB + (x + 125744); return (X, Y_BB + (y - 24497), ZB * max(0.0, 1 - (X - XB) / 3994))
def m_brazo_inf(x, y): return (XB + (x + 125616), Y_BB, y - 21980)

VISTAS = [  # hoja, caja, mapeo, copias (sx, sz), grupo, vista
    (7, (-155500, 8800, -143800, 16400), m_dt2_frontal, [(1, 1), (1, -1)], "Delta tramo 2", "frontal delta tramo 2"),
    (8, (-136000, 7400, -129000, 15000), m_dt2_lat, [(1, 1), (-1, 1)], "Delta tramo 2", "cara longitudinal delta tramo 2"),
    (8, (-142300, 7900, -136500, 14300), m_dt2_int, [(1, 1), (-1, 1)], "Delta tramo 2", "corte H (cara interior)"),
    (5, (-152500, 14000, -141500, 23700), m_dt1_frontal, [(1, 1), (1, -1)], "Delta tramo 1", "frontal delta tramo 1"),
    (6, (-171500, 15200, -166900, 23400), m_dt1_int, [(1, 1), (-1, 1)], "Delta tramo 1", "corte Z-Z (cara interior)"),
    (6, (-165700, 14100, -161100, 23000), m_dt1_ext, [(1, 1), (-1, 1)], "Delta tramo 1", "cara longitudinal delta tramo 1"),
    (3, (-183200, 37150, -165900, 39400), m_viga_frontal, [(1, 1), (1, -1)], "Viga", "vista frontal viga"),
    (2, (-183000, 45950, -166100, 48200), m_viga_sup, [(1, 1)], "Viga", "vista superior viga"),
    (3, (-182400, 33650, -166700, 35850), m_viga_inf, [(1, 1)], "Viga", "vista inferior viga"),
    (4, (-125800, 24400, -121400, 26700), m_brazo_frontal, [(1, 1), (1, -1), (-1, 1), (-1, -1)], "Brazos", "frontal brazo"),
    (4, (-125700, 20800, -121400, 23100), m_brazo_inf, [(1, 1), (-1, 1)], "Brazos", "vista inferior brazo"),
]

def leer_cabeza():
    piezas = []
    for n, caja, f, copias, grupo, vista in VISTAS:
        if n not in HOJ: continue
        ax = HOJ[n]["ax"]; mkd = HOJ[n]["mk"]
        for i, l in enumerate(ax):
            if not en_caja(l, caja) or lon(l) < 150: continue
            if n == 7 and abs(l[0] + 149626) < 3 and abs(l[2] + 149626) < 3 and not mkd.get(i): continue     # eje de simetría
            if n == 5 and abs(l[0] + 146813) < 3 and abs(l[2] + 146813) < 3 and not mkd.get(i): continue
            A, B = f(l[0], l[1]), f(l[2], l[3])
            for sx, sz in copias:
                pa = [round(sx * A[0]), round(A[1]), round(sz * A[2])]; pb = [round(sx * B[0]), round(B[1]), round(sz * B[2])]
                piezas.append(dict(a=pa, b=pb, grupo=grupo, vista=vista, hoja=n, marcas=mkd.get(i, [])))
    out = []
    for p in piezas:
        dup = next((q for q in out if (math.dist(p["a"], q["a"]) < 90 and math.dist(p["b"], q["b"]) < 90) or
                    (math.dist(p["a"], q["b"]) < 90 and math.dist(p["b"], q["a"]) < 90)), None)
        if dup is None: out.append(p)
        elif not dup["marcas"] and p["marcas"]: dup.update(marcas=p["marcas"], hoja=p["hoja"])
    return out
CABEZA = leer_cabeza()

def miembros_cabeza(k, pref):
    out = []
    for i, p in enumerate(CABEZA):
        if p["marcas"] and not any(incluye(k, p["hoja"], x) for x in p["marcas"]): continue
        tipo = "Horizontal" if abs(p["a"][1] - p["b"][1]) < 30 else ("Montante" if math.dist(p["a"], p["b"]) > 4000 and p["grupo"].startswith("Delta") else "Diagonal")
        out.append(mk(f"{pref}-CAB-{i:04d}", f"{tipo} · {p['grupo']} · {p['vista']}", p["grupo"], p["grupo"], tipo, "cabeza",
                      p["a"], p["b"], f"plano H{p['hoja']} ({p['vista']})", "transversal", n=p["hoja"], marcas=p["marcas"]))
    return out


# ------------------------------------------------------------------ pata 12,0 m (H33 cara, H34 vista interna) y stub (H35)
P_NODO, P_PIE = (103278.0, -5788.0), (115389.0, -7424.0)     # cota de la pata: 12000 EN VERTICAL / 12111 EN CARA (H33)
def _yleg(x): return P_NODO[1] + (x - P_NODO[0]) * (P_PIE[1] - P_NODO[1]) / (P_PIE[0] - P_NODO[0])
def leer_pata():
    h = HOJ[33]; ax = h["ax"]; mkd = h["mk"]
    out, vistos = [], []
    for i, l in enumerate(ax):
        if lon(l) < 300: continue
        pts = ((l[0], l[1]), (l[2], l[3]))
        if any(y < _yleg(x) - 150 or x < P_NODO[0] - 650 or x > P_PIE[0] + 150 for x, y in pts): continue      # pestañas desplegadas
        # recorte al nudo superior de la pata (el montante sobresale hacia el cuerpo)
        a, b = sorted(pts)
        if b[0] < P_NODO[0] - 5: continue
        if a[0] < P_NODO[0]:
            t = (P_NODO[0] - a[0]) / (b[0] - a[0]); a = (P_NODO[0], a[1] + t * (b[1] - a[1]))
        if any(seg_dist(a, q[0], q[1]) < 60 and seg_dist(b, q[0], q[1]) < 60 for q in vistos): continue
        vistos.append((a, b))
        es_leg = all(abs(y - _yleg(x)) < 80 for x, y in (a, b)) and lon((*a, *b)) > 5000
        out.append(dict(a=a, b=b, marcas=mkd.get(i, []), leg=es_leg))
    return out
def leer_cierre():
    h = HOJ.get(34)
    if not h: return []
    yu = lambda x: 28428 + (x - 104521) * (26008 - 28428) / (117716 - 104521)
    yl = lambda x: 23587 + (x - 104521) * (26008 - 23587) / (117716 - 104521)
    cand = [l for l in h["dib"] if lon(l) >= 300 and all(104400 <= x <= 117800 and yl(x) - 40 <= y <= yu(x) + 40 for x, y in ((l[0], l[1]), (l[2], l[3])))]
    ejes = merge(emparejar(cand), tol=10, gap=200)
    out = []
    for l in ejes:
        if lon(l) < 400: continue
        if all(abs(y - yu(x)) < 120 or abs(y - yl(x)) < 120 for x, y in ((l[0], l[1]), (l[2], l[3]))) and lon(l) > 3000: continue   # cordones (ya son los de la cara)
        def st(x, y): return ((x - 104521) / (117716 - 104521), (y - yl(x)) / max(yu(x) - yl(x), 1))
        out.append(dict(sa=st(l[0], l[1]), sb=st(l[2], l[3])))
    return out
PATA, CIERRE = leer_pata(), leer_cierre()
def _stub():
    rows = [f for f in FILAS if f["conjunto"] == "STUB" and f["desc"].startswith("L ")]
    r = max(rows, key=lambda f: num(f["long"])) if rows else None
    return (num(r["long"]), r["pos"]) if r else (0, None)
STUB = _stub()
HIP = None
for q in PATA:
    pass

def miembros_patas(k, ultimo, pref):
    Ytop, htop = ultimo.Y(ultimo.yt + ultimo.vb), ultimo.wb / 2
    def p3(pt, kk, lado):
        d = pt[0] - P_NODO[0]; u = pt[1] - P_NODO[1]
        Y = Ytop - d / VC; hh = htop + SV * (Ytop - Y)
        X, Z = (htop - u, hh) if lado == 0 else (hh, htop - u)
        X, Z = rot(X, Z, kk); return [round(X), round(Y), round(Z)]
    # cordón interior (hip) de la cara: arranca en el tope a 3341 del montante y llega al pie
    hip_top, hip_pie = (P_NODO[0], P_NODO[1] + 3341), P_PIE
    out, G = [], "Patas 12,0 m"
    for kk in range(4):
        e = kk + 1
        for j, q in enumerate(PATA):
            for lado in ((0,) if q["leg"] else (0, 1)):
                cara = f"E{e}{'AB'[lado]}"
                tipo = "Montante" if q["leg"] else ("Horizontal" if abs(q["a"][0] - q["b"][0]) < 25 else "Diagonal")
                out.append(mk(f"{pref}-PATA-{cara}-{j:02d}", f"{tipo} · pata 12,0 m · esquina {e}", G, "Pata 12,0 m", tipo, cara,
                              p3(q["a"], kk, lado), p3(q["b"], kk, lado), "plano H33 (pata 12,0 m)", "transversal" if lado == 0 else "longitudinal",
                              n=33, marcas=q["marcas"]))
        hA = (p3(hip_top, kk, 0), p3(hip_pie, kk, 0)); hB = (p3(hip_top, kk, 1), p3(hip_pie, kk, 1))
        def pc(stv):
            s_, t_ = stv
            A = [hA[0][i] + (hA[1][i] - hA[0][i]) * s_ for i in range(3)]; B = [hB[0][i] + (hB[1][i] - hB[0][i]) * s_ for i in range(3)]
            return [round(A[i] + (B[i] - A[i]) * t_) for i in range(3)]
        for j, c in enumerate(CIERRE):
            out.append(mk(f"{pref}-PATA-E{e}-CI{j:02d}", f"Diagonal · vista interna pata 12,0 m · esquina {e}", G, "Pata 12,0 m", "Diagonal", f"E{e}",
                          pc(c["sa"]), pc(c["sb"]), "plano H34 (pata 12,0 m, vista interna)", "longitudinal"))
        if STUB[0] > 0:
            A = p3(P_NODO, kk, 0); B = p3(P_PIE, kk, 0); L = math.dist(A, B); u_ = [(B[i] - A[i]) / L for i in range(3)]
            C = [round(B[i] + u_[i] * STUB[0]) for i in range(3)]
            d = mk(f"{pref}-STUB-E{e}", f"Stub · esquina {e}", "Stubs", "Stub", "Montante", f"E{e}", B, C, "listado H35 (stub)", "transversal")
            d["marca"], d["marcas"], d["conjunto"] = STUB[1], [f"35|{STUB[1]}"], "STUB"
            d["coherencia"], d["coherencia_txt"] = "n/a", f"Longitud del listado ({round(STUB[0])} mm) colocada en la prolongación del montante (supuesto)."
            out.append(d)
    return out

# ------------------------------------------------------------------ salida
SUPUESTOS = [
    "Y=0 en el pie de las patas (terreno; los stubs quedan bajo el terreno). Las cotas relativas salen del tronco de pirámide (bloque 'func': 0,13636486 por vertical, 0,13511439 en cara) con Y=0 de trabajo en el tope del cuerpo (ancho 6000).",
    "Cada vista de cara se ubica con su amarre superior y el ancho H= del plano; los montantes de los dibujos sobresalen ≈ 590 mm del nudo (empalme) y no se usan para la cota.",
    "Planta cuadrada en el cuerpo; las extensiones C1-C3-C5 y C2-C4-C6 tienen vistas separadas de cara transversal y longitudinal (H10, H16/H17), las demás se replican en las 4 caras. Las vistas de media cara (H11, H13, H19) se espejan respecto al eje.",
    "Cabeza: vistas frontales de los deltas (H7, H5) en X-Y a verdadero tamaño con la inclinación de la cara (factor 0,9908); la profundidad sale de los anchos de las caras laterales (H8: 6000 → 4071; H6: 4308 → 2086; viga 2087) interpolada linealmente con la altura. Las caras laterales (H8, H6) se colocan interpolando entre sus montantes. Viga (H2/H3) con caras a ±1043 mm; brazos (H4) con cara frontal que se cierra hacia la punta (aproximado).",
    "Delta tramo 1 (H5): el dibujo junta los dos mástiles; cada uno se ubica por su montante interior (X = ±4983, tope del montante interior del tramo 2) y se verifica con el montante exterior, que coincide con la prolongación del exterior del tramo 2.",
    "Patas: se modela la de 12,0 m (H33, cara a verdadero tamaño colocada en las dos caras de cada esquina; H34, vista interna entre los dos cordones de la esquina, interpolada) en las 4 esquinas; stub (H35) en la prolongación del montante. Las patas de 1,5–10,5 m y las parrillas solo se listan en 'opciones_pie'.",
    "Composición y pesos: hoja RESUMEN del listado rev. A8; cada marca del plano se asocia a su conjunto del listado y solo entra si el conjunto pertenece a la torre. Subconjuntos sin fila propia en el RESUMEN (CONJUNTO AA01, CORTE H-H, CONJUNTO CUERPO AA7) toman la composición del conjunto anterior del mismo plano.",
    "No modelados: brazo de guarda (H1, vistas giradas) y cortes de la cabeza (H2 G-G/F-F/F1-F1/Z-Z); sus piezas sí están en la lista de materiales.",
    "Cortes en planta: solo los de contorno cuadrado; se colocan en el nudo del módulo cuyo ancho coincide con el del corte (tolerancia 2,5 %).",
    "Marcas: solo las que tienen burbuja + leader a ≤130 mm del eje del plano; 'coherencia' compara la longitud de pieza de la lista con el eje del modelo.",
]

def construir(k):
    pref = f"C{k}"
    miembros = []
    mods = []
    for mid in COMPOSICION[k]:
        for m in MOD.values():
            if m.id == mid or (m.id[:-1] == mid and m.id[-1] in "TL"):
                mods.append(m); miembros += miembros_modulo(m, k, pref)
    mc, cortes = miembros_cortes(k, COMPOSICION[k], pref); miembros += mc
    ult = [m for m in mods if m.caras in ("T", "TL")][-1]
    miembros += miembros_patas(k, ult, pref)
    miembros = miembros_cabeza(k, pref) + miembros
    y0 = min(min(m_["a"][1], m_["b"][1]) for m_ in miembros if m_["grupo"].startswith("Patas"))       # Y=0 en el pie de las patas (terreno)
    for m_ in miembros:
        m_["a"][1] -= y0; m_["b"][1] -= y0
    grupos = [dict(nombre="Brazos", color="#d04b5b"), dict(nombre="Viga", color="#e07b39"), dict(nombre="Delta tramo 1", color="#8e5bd0"),
              dict(nombre="Delta tramo 2", color="#b07cd8"), dict(nombre="Arriostramiento horizontal", color="#20b2aa"),
              dict(nombre="Patas 12,0 m", color="#9a7b4f"), dict(nombre="Stubs", color="#6b6b6b")]
    cols = ["#3b82c4", "#4aa3a2", "#7aa63b", "#d9a441", "#c8693a"]
    for i, m in enumerate(dict.fromkeys(x.nombre for x in mods)): grupos.append(dict(nombre=m, color=cols[i % len(cols)]))
    usados = {m_["grupo"] for m_ in miembros}; grupos = [g for g in grupos if g["nombre"] in usados]
    bom, peso, peso_res = bom_torre(k)
    marcas = {}
    for f in bom:
        marcas.setdefault(f"{f['hoja']}|{f['marca']}", dict(marca=f["marca"], hoja=f["hoja"], clase=f["clase"], perfil=f["perfil"], long_mm=f["long_mm"],
                                                             cant=f["cant"], acero=f["acero"], conjunto=f["conjunto"], en_lista=True))
    for m_ in miembros:
        for key in m_.get("marcas", []):
            if key not in marcas:
                h, p = key.split("|", 1)
                marcas[key] = dict(marca=p, hoja=int(h), clase="", perfil="", long_mm="", cant="", acero="", conjunto="", en_lista=False)
    val = []
    for m in mods:
        val.append(dict(hoja=f"H{m.n}", cota=f"{m.nombre} ({m.caras}): altura por la cara vs por los anchos H=",
                        plano_mm=round(-m.vb / VC), modelo_mm=round(YC(m.wt) - YC(m.wb))))
    for c in cortes:
        val.append(dict(hoja=f"H{c['hoja']}", cota="lado de la sección en planta vs ancho del módulo", plano_mm=c["ancho_plano_mm"], modelo_mm=c["ancho_modelo_mm"]))
    ys = [p[1] for mm in miembros for p in (mm["a"], mm["b"])]
    xs = [abs(p[0]) for mm in miembros for p in (mm["a"], mm["b"])]
    ncoh = collections.Counter(m_.get("coherencia") for m_ in miembros if m_.get("marca"))
    nom = f"Torre C{k}"
    meta = dict(nombre=nom, linea="EEB U113 CT100501", plano_base="EEB-U113-CT100501-L000-DIS6022",
                fuente="Planos DIS6022 H01–H36 y listado de materiales torre C rev. A8 (hoja RESUMEN para la composición).",
                etapa="2 (cabeza sin brazo de guarda, cuerpos, extensiones, secciones, pata 12,0 m y stub)",
                peso_total_kg=round(peso, 1), peso_resumen_galvanizado_kg=round(peso_res, 1), opciones_pie=OPCIONES_PIE,
                composicion=list(dict.fromkeys(m.nombre for m in mods)), alto_total_mm=round(max(ys) - min(ys)), ancho_base_mm=round(2 * max(abs(p[0]) for mm in miembros for p in (mm["a"], mm["b"]) if abs(p[1]) < 60)),
                ancho_cruceta_mm=round(2 * max(xs)), marcas_vinculadas=len({m_["marca"] for m_ in miembros if m_.get("marca")}),
                miembros_total=len(miembros), coherencia_marcas=dict(ncoh), validacion=val, secciones_planta=cortes, supuestos=SUPUESTOS)
    data = dict(meta=meta, grupos=grupos, marcas=marcas, miembros=miembros, lista_materiales=bom)
    with open(os.path.join(OUTDIR, f"torre-c{k}.json"), "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, separators=(",", ":"))
    print(f"C{k}:", len(miembros), "miembros |", meta["marcas_vinculadas"], "marcas |", len(bom), "filas BOM | peso", round(peso), "/", round(peso_res), "| alto", meta["alto_total_mm"], "| coh", dict(ncoh), "| cortes", [(c["hoja"], c["cota_mm"]) for c in cortes])

if __name__ == "__main__":
    print("modulos:", [(m.id, len(m.ids), round(m.Yt), round(m.Y(m.yt + m.vb))) for m in MOD.values()])
    print("cortes:", [(c["hoja"], round(c["W"]), ubicar_corte(c)) for c in CORTES])
    print("cabeza:", len(CABEZA), collections.Counter(p["grupo"] for p in CABEZA))
    for k in range(1, 7): construir(k)
