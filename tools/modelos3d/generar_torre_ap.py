#!/usr/bin/env python3
"""
Genera wwwroot/modelos3d/torre-ap1..ap7.json (torres AP, EEB-U113-CT100501-L000-DIS6049 H01–H37).

Uso: python3 generar_torre_ap.py <carpeta con H1.dxf … H37.dxf> <carpeta modelos3d> <LISTADO_DE_MATERIALES_TORRE_AP.xls>
     (DXF: dwg2dxf -y -o H20.dxf <plano>.dwg)

Método (verificable, ver 'supuestos' y 'validacion' del JSON):
 1. Cada cuerpo/extensión se lee de su VISTA DE CARA (verdadero tamaño, mm). Montantes = líneas de trabajo;
    cada ángulo viene con dos aristas, que se promedian en un eje; los ejes colineales se unen.
 2. La cara se lleva a 3D con la pendiente del plano (bloque FUNCIONES: 0,1375757 por vertical; 1,009419 en cara)
    y se replica en las 4 caras (planta cuadrada).
 3. Cota Y: un único tronco de pirámide; Y sale del ancho de los montantes (Y = -(w-3585)/(2*0,1375757)),
    Y=0 en la base de la extensión AP1. Se valida contra la longitud de cada cara.
 4. Composición de cada torre y pesos: hoja RESUMEN del listado de materiales (rev. A14).
    Cada marca del plano se asocia a su conjunto del listado; un miembro entra en una torre solo si su conjunto entra.
 5. Patas: pata de 12,0 m (H33, la de la silueta) en las 4 esquinas, cara del plano en ambas caras de la esquina; stub (H35).
 6. Cortes en planta de contorno cuadrado: en el amarre horizontal de la cara cuyo largo coincide con el lado.
"""
import sys, os, re, json, math, collections
from ezdxf import recover

DXFDIR, OUTDIR, XLSPATH = sys.argv[1], sys.argv[2], sys.argv[3]
S_VERT, VC = 0.1375757575, 1.009419184      # pendiente por vertical / factor en cara (bloque FUNCIONES)
W_REF = 3585.0

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


# ------------------------------------------------------------------ lectura
def cargar(n):
    ms = recover.readfile(os.path.join(DXFDIR, f"H{n}.dxf"))[0].modelspace()
    TR, M = [], []
    for e in ms:
        t = e.dxftype()
        if t == "LINE" and e.dxf.layer == "TR":
            a, b = e.dxf.start, e.dxf.end
            if math.hypot(b.x - a.x, b.y - a.y) > 12:
                TR.append((a.x, a.y, b.x, b.y))
        elif t == "INSERT" and e.dxf.name in ("WORDING", "3D_INFOM", "M8_PRO", "M8_LAM", "3d_infop2", "VIEW_NAME"):
            M.append((e.dxf.name, e.dxf.insert.x, e.dxf.insert.y, {a.dxf.tag: a.dxf.text.strip() for a in e.attribs}))
    return TR, M

def num(s, d=0.0):
    try: return float(str(s).replace(",", "."))
    except Exception: return d

def lon(l): return math.hypot(l[2] - l[0], l[3] - l[1])

def ang(l):
    th = math.atan2(l[3] - l[1], l[2] - l[0]) % math.pi
    return th

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

def interseca(l, p, q):
    """interseccion de la recta de l con la recta pq (None si paralelas)"""
    x1, y1, x2, y2 = l; dx, dy = x2 - x1, y2 - y1
    ex, ey = q[0] - p[0], q[1] - p[1]
    den = dx * ey - dy * ex
    if abs(den) < 1e-9: return None
    t = ((p[0] - x1) * ey - (p[1] - y1) * ex) / den
    return (x1 + t * dx, y1 + t * dy)


# ------------------------------------------------------------------ listado de materiales (xls rev. A14)
def leer_listado(path):
    import xlrd
    b = xlrd.open_workbook(path)
    s = b.sheet_by_name("AP")
    conj, filas, plano, nombre, anti = [], [], None, None, 0
    def txt(r, c):
        v = s.cell_value(r, c)
        if isinstance(v, float) and v.is_integer(): return str(int(v))
        return str(v).strip()
    for r in range(s.nrows):
        c0, c1, c4, c5 = txt(r, 0), txt(r, 1), txt(r, 4), s.cell_value(r, 5)
        if "PLANO" in c4:
            m = re.search(r"H(\d+)", c4); plano = int(m.group(1)) if m else plano
            nombre = c1 or txt(r + 1, 1)
        elif not c0 and c1 and c1 != "OTROS" and not c1.startswith("SIGLA") and c5 == "" and not c1.startswith("PESO"):
            nombre = c1
        else:
            if c0 and isinstance(c5, float) and nombre:
                if nombre.startswith("ANTIESCALATORIO"):
                    key = f"ANTIESCALATORIO #{anti_idx[(nombre, plano)]}"
                else: key = nombre
                filas.append(dict(conjunto=key, hoja=plano, pos=c0, desc=txt(r, 1), calidad=txt(r, 2), dim=txt(r, 3),
                                  long=txt(r, 4), cant=s.cell_value(r, 7) if isinstance(s.cell_value(r, 7), float) else c5,
                                  peso=s.cell_value(r, 8) if isinstance(s.cell_value(r, 8), float) else 0.0))
            continue
        if nombre and nombre.startswith("ANTIESCALATORIO") and (nombre, plano) not in anti_idx:
            anti_idx[(nombre, plano)] = len(anti_idx) + 1
    # RESUMEN: multiplicidad de cada conjunto por torre (AP1..AP7)
    rs = b.sheet_by_name("RESUMEN"); mult = {}; n_anti = 0
    for r in range(2, rs.nrows):
        d = str(rs.cell_value(r, 1)).strip()
        if not d: break
        v = [rs.cell_value(r, c) for c in range(5, 12)]
        v = [int(x) if isinstance(x, float) else 0 for x in v]
        if d.startswith("ANTIESCALATORIO"):
            n_anti += 1; d = f"ANTIESCALATORIO #{n_anti}"
        mult[d] = v
    return filas, mult
anti_idx = {}
FILAS, MULT = leer_listado(XLSPATH)
CONJUNTOS = list(dict.fromkeys(f["conjunto"] for f in FILAS))
HOJA_CONJ = {}
for f in FILAS: HOJA_CONJ.setdefault(f["conjunto"], f["hoja"])
# elementos de montaje: van con el cuerpo de la misma hoja
for c in CONJUNTOS:
    if c not in MULT and c.startswith("ELEMENTOS DE MONTAJE"):
        cuerpo = next((k for k in MULT if k.startswith("CUERPO") and HOJA_CONJ.get(k) == HOJA_CONJ[c]), None)
        if cuerpo: MULT[c] = list(MULT[cuerpo])
MULT = {k.strip(): v for k, v in MULT.items()}
POR_POS = collections.defaultdict(list)          # (hoja, pos) -> conjuntos
for f in FILAS: POR_POS[(f["hoja"], f["pos"])].append(f["conjunto"])

POR_POS_G = collections.defaultdict(set)         # pos -> {(hoja, pos)}
for f in FILAS: POR_POS_G[f["pos"]].add((f["hoja"], f["pos"]))

def ubicar_marca(hoja, mk):
    """(hoja del listado, pos) para una marca leída del plano: misma hoja (marca o marca+H);
    si no, la única coincidencia en otra hoja (los cortes en planta se listan con su cuerpo)."""
    for p in (mk, mk + "H", mk.rstrip("H")):
        if (hoja, p) in POR_POS: return (hoja, p)
    for p in (mk, mk + "H", mk.rstrip("H")):
        if len(POR_POS_G.get(p, ())) == 1: return next(iter(POR_POS_G[p]))
    return None

def clave_marca(hoja, mk):
    u = ubicar_marca(hoja, mk)
    return u[1] if u else None

def conj_de(hoja, mk):
    u = ubicar_marca(hoja, mk)
    if not u: return None
    cs = POR_POS[u]
    return cs[0] if len(set(cs)) == 1 else cs

def incluye(k, hoja, mk):
    if not mk: return True
    c = conj_de(hoja, mk)
    if c is None: return True
    cs = c if isinstance(c, list) else [c]
    return any(MULT.get(x, [1] * 7)[k - 1] > 0 for x in cs)

# ------------------------------------------------------------------ modulos (vistas de cara)
MODS = [  # id, hoja, titulo (x,y) de la vista de cara, nombre, semiancho y alto de la ventana de lectura
    ("AP1", 7, (19573, 10591), "Extensión AP1", 5000, 9000),
    ("AP2", 8, (21701, 9793), "Cuerpo AP2", 5000, 9000),
    ("EXT3", 9, (56217, 9731), "Extensión AP3", 5000, 9000),
    ("B3", 10, (76338, 10302), "Cuerpo AP3", 5000, 9000),
    ("B4", 12, (6356, 5703), "Extensión y cuerpo AP4", 5000, 9000),
    ("EXT5", 15, (40260, 4872), "Extensión AP5", 5000, 9000),
    ("B5", 17, (15390, 4300), "Cuerpo AP5", 5000, 9000),
    ("EXT6", 18, (61767, 8173), "Extensión AP6", 5000, 9000),
    ("B6", 20, (95127, 7545), "Cuerpo AP6", 5200, 9000),
    ("B7", 21, (11879, 9407), "Cuerpo AP7", 6300, 10000),
]
COMPOSICION = {  # orden de arriba abajo (coincide con la hoja RESUMEN del listado)
    1: ["AP1"], 2: ["AP1", "AP2"], 3: ["AP1", "EXT3", "B3"], 4: ["AP1", "EXT3", "B4"],
    5: ["AP1", "EXT3", "B4s", "EXT5", "B5"], 6: ["AP1", "EXT3", "B4s", "EXT5", "EXT6", "B6"],
    7: ["AP1", "EXT3", "B4s", "EXT5", "EXT6", "B7"],
}
CORTES_HOJAS = [7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23]
COLORES = ["#3b82c4", "#4aa3a2", "#7aa63b", "#d9a441", "#c8693a", "#8e5bd0", "#d04b5b", "#5b7fd0", "#9a7b4f"]

HOJ = {n: cargar(n) for n in list(range(7, 24)) + [33, 34, 35]}

def region(n, tx, ty, half=5000, alto=9000):
    TR, M = HOJ[n]
    L = merge(TR)
    R = [l for l in L if abs((l[0] + l[2]) / 2 - tx) < half and ty < min(l[1], l[3]) and max(l[1], l[3]) < ty + alto]
    W = [(x, y, a) for nm, x, y, a in M if nm == "WORDING" and abs(x - tx) < half + 1500 and ty < y < ty + alto]
    return R, W

def leer_modulo(mid, n, t, nombre, half, alto):
    R, W = region(n, t[0], t[1], half, alto)
    steep = []
    for l in R:
        dv = l[3] - l[1]
        if abs(dv) > 1500 and 0.1 < abs((l[2] - l[0]) / dv) < 0.2:
            steep.append((l[0], l[1], l[2], l[3]) if l[1] < l[3] else (l[2], l[3], l[0], l[1]))
    # cada montante trae dos juegos de líneas largas: aristas (más largas) y línea de trabajo (más corta, ~180 mm menos);
    # se toma el grupo de longitud más corto, entre los largos, que tenga líneas a ambos lados
    lmax = max(lon(l) for l in steep)
    grupos_l = sorted({round(lon(l) / 6) for l in steep if lon(l) >= 0.9 * lmax})
    mejor = None
    for g in grupos_l:            # entre los grupos con líneas a ambos lados, el de mayor separación (línea de trabajo)
        work = [l for l in steep if round(lon(l) / 6) == g]
        izq = [l for l in work if l[2] > l[0]]; der = [l for l in work if l[2] < l[0]]
        if not (izq and der): continue
        a_, b_ = max(izq, key=lambda l: l[0]), min(der, key=lambda l: l[0])
        if mejor is None or b_[0] - a_[0] > mejor[1][0] - mejor[0][0]: mejor = (a_, b_)
    LL, RR = mejor
    vb, vt = (LL[1] + RR[1]) / 2, (LL[3] + RR[3]) / 2
    def xl(v): return LL[0] + (v - LL[1]) * (LL[2] - LL[0]) / (LL[3] - LL[1])
    def xr(v): return RR[0] + (v - RR[1]) * (RR[2] - RR[0]) / (RR[3] - RR[1])
    return dict(id=mid, hoja=n, titulo=t, nombre=nombre, R=R, W=W, LL=LL, RR=RR, vb=vb, vt=vt,
                xl=xl, xr=xr, wb=xr(vb) - xl(vb), wt=xr(vt) - xl(vt), xc=(xl(vb) + xr(vb)) / 2)

def YC(w): return -(w - W_REF) / (2 * S_VERT)

MOD = {m[0]: leer_modulo(*m) for m in MODS}

def hw(m, v): return (m["xr"](v) - m["xl"](v)) / 2
def Yv(m, v): return m["Yb"] + (v - m["vb"]) / VC
def rot(X, Z, k): return [(X, Z), (Z, -X), (-X, -Z), (-Z, X)][k]
def punto3(m, x, v, k):
    X, Z = rot(x - m["xc"], hw(m, v), k)
    return [round(X), round(Yv(m, v)), round(Z)]
def marca_de(a):
    mk = a.get("MAR_PEZ", "").strip(); cod = a.get("COD_PEZ", "").strip()
    return mk + cod if cod in ("H", "D", "Z") else mk
def asignar_marcas(miembros, W, maxrel=0.08, maxd=1300):
    """miembros: lista de dict con len, mid. W: wordings. greedy por costo."""
    cand = []
    for i, mi in enumerate(miembros):
        for j, (x, y, a) in enumerate(W):
            lun = num(a.get("LUN_PRO"))
            if lun <= 0: continue
            rel = abs(lun - mi["len"]) / max(mi["len"], 1)
            d = math.hypot(x - mi["mid"][0], y - mi["mid"][1])
            if rel <= max(maxrel, 60 / max(mi["len"], 1)) and d <= maxd:
                cand.append((rel * 4 + d / 2500, i, j))
    ui, uj = set(), set()
    for c, i, j in sorted(cand):
        if i in ui or j in uj: continue
        ui.add(i); uj.add(j); miembros[i]["wording"] = W[j]
    return miembros

def cara_miembros(m):
    """ejes de arriostramiento de la cara (coord. de hoja) + montantes por piezas."""
    vb, vt = m["vb"], m["vt"]
    cand = []
    for l in m["R"]:
        if lon(l) < 150: continue
        dv = l[3] - l[1]; dx = l[2] - l[0]
        if abs(dv) > 150 and 0.09 < abs(dx / dv) < 0.19: continue            # montantes / pestanas
        ok = True
        for (x, y) in ((l[0], l[1]), (l[2], l[3])):
            if not (vb - 700 <= y <= vt + 150 and m["xl"](y) - 320 <= x <= m["xr"](y) + 320): ok = False
        if ok: cand.append(l)
    ejes = emparejar(cand)
    ejes = merge(ejes, tol=10, gap=420)
    out = []
    for l in ejes:
        P = [(l[0], l[1]), (l[2], l[3])]
        Q = []
        for (x, y) in P:
            best = (None, 1e9)
            for lado, f in (("L", m["xl"]), ("R", m["xr"])):
                d = abs(x - f(y))
                if d < best[1]: best = (lado, d)
            fuera = x < m["xl"](y) - 20 or x > m["xr"](y) + 20
            if best[1] < 170 or fuera:
                lg = m["LL"] if best[0] == "L" else m["RR"]
                q = interseca(l, (lg[0], lg[1]), (lg[2], lg[3]))
                if q and (math.hypot(q[0] - x, q[1] - y) < 320 or fuera): x, y = q
            Q.append((x, y))
        a, b = Q
        if abs(b[1] - a[1]) < 25: b = (b[0], a[1])
        if math.hypot(b[0] - a[0], b[1] - a[1]) < 400: continue
        out.append(dict(a=a, b=b, len=math.hypot(b[0] - a[0], b[1] - a[1]), mid=((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)))
    asignar_marcas(out, m["W"])
    return out

def montantes_piezas(m):
    """piezas del montante: wordings del perfil principal cerca de cada montante, ordenadas de abajo arriba"""
    res = {}
    for lado, lg in (("L", m["LL"]), ("R", m["RR"])):
        f = m["xl"] if lado == "L" else m["xr"]
        total = lon(lg)
        ws = [(y, a) for x, y, a in m["W"] if m["vb"] - 50 <= y <= m["vt"] + 50 and abs(x - f(y)) < 1600 and num(a.get("LUN_PRO")) >= 800
              and (a.get("NOM_PRO", "").startswith("L1") or a.get("NOM_PRO", "").startswith("L9") or a.get("NOM_PRO", "").startswith("L8"))]
        # piezas distintas por marca; mismo montante pero lado opuesto tiene su propia marca
        seen = {}
        for y, a in ws:
            seen.setdefault(a["MAR_PEZ"], (y, a))
        piezas = sorted(seen.values(), key=lambda t: t[0])
        res[lado] = piezas
    return res


# ------------------------------------------------------------------ anclaje vertical global
for k in ("AP1", "AP2", "EXT3", "B3", "B4"):
    MOD[k]["Yb"] = YC(MOD[k]["wb"])
for k, m in MOD.items():
    m["miembros_cara"] = cara_miembros(m)
    m["piezas"] = montantes_piezas(m)

def amarres(m):
    res = []
    for e in m["miembros_cara"]:
        if abs(e["b"][1] - e["a"][1]) < 25:
            v = (e["a"][1] + e["b"][1]) / 2
            if e["len"] >= 0.6 * (m["xr"](v) - m["xl"](v)): res.append((v, e["len"]))
    return sorted(res)

b4 = MOD["B4"]
am = [a for a in amarres(b4) if b4["vb"] + 0.3 * (b4["vt"] - b4["vb"]) < a[0] < b4["vb"] + 0.7 * (b4["vt"] - b4["vb"])]
V_KK = max(am, key=lambda a: a[1])[0]
Y_KK = Yv(b4, V_KK)
def dY(m): return (m["vt"] - m["vb"]) / VC
MOD["EXT5"]["Yb"] = Y_KK - dY(MOD["EXT5"])
MOD["B5"]["Yb"] = MOD["EXT5"]["Yb"] - dY(MOD["B5"])
MOD["EXT6"]["Yb"] = MOD["EXT5"]["Yb"] - dY(MOD["EXT6"])
MOD["B6"]["Yb"] = MOD["EXT6"]["Yb"] - dY(MOD["B6"])
MOD["B7"]["Yb"] = MOD["EXT6"]["Yb"] - dY(MOD["B7"])
DESAJ_KK = YC(MOD["EXT5"]["wt"]) - Y_KK

# ------------------------------------------------------------------ cortes en planta
def leer_cortes():
    cortes = []
    for n in CORTES_HOJAS:
        TR, M = HOJ[n]
        L = merge(TR)
        for nm, tx, ty, a in M:
            if nm != "VIEW_NAME" or not a.get("VIE_NAM", "").upper().startswith("CORTE"): continue
            R = [l for l in L if abs((l[0] + l[2]) / 2 - tx) < 6400 and ty + 100 < min(l[1], l[3]) and max(l[1], l[3]) < ty + 12500]
            ejes = merge(emparejar(R), tol=10, gap=260)
            Hs = [l for l in ejes if abs(l[3] - l[1]) < 20 and lon(l) >= 2500]
            if not Hs: continue
            yb = min((l[1] + l[3]) / 2 for l in Hs)
            base = [l for l in Hs if abs((l[1] + l[3]) / 2 - yb) < 40]
            xl_, xr_ = min(min(l[0], l[2]) for l in base), max(max(l[0], l[2]) for l in base)
            Vs = [l for l in ejes if abs(l[2] - l[0]) < 20 and lon(l) >= 2200 and abs(min(l[1], l[3]) - yb) < 150]
            Vi = [l for l in Vs if abs((l[0] + l[2]) / 2 - xl_) < 150]; Vd = [l for l in Vs if abs((l[0] + l[2]) / 2 - xr_) < 150]
            if not Vi or not Vd: continue
            Wc = xr_ - xl_; Wy = min(max(lon(l) for l in Vi), max(lon(l) for l in Vd))
            if abs(Wy - Wc) > 0.1 * Wc: continue
            cx, cy = (xl_ + xr_) / 2, yb + Wy / 2
            inte = []
            for l in ejes:
                if not (xl_ - 50 <= min(l[0], l[2]) and max(l[0], l[2]) <= xr_ + 50 and yb - 50 <= min(l[1], l[3]) and max(l[1], l[3]) <= yb + Wy + 50): continue
                if lon(l) < 400: continue
                horiz = abs(l[3] - l[1]) < 20; vert = abs(l[2] - l[0]) < 20
                if horiz and (abs(l[1] - yb) < 70 or abs(l[1] - (yb + Wy)) < 70): continue
                if vert and (abs(l[0] - xl_) < 70 or abs(l[0] - xr_) < 70): continue
                inte.append(dict(a=(l[0], l[1]), b=(l[2], l[3]), len=lon(l), mid=((l[0] + l[2]) / 2, (l[1] + l[3]) / 2)))
            W = [(x, y, aa) for nm2, x, y, aa in M if nm2 == "WORDING" and xl_ - 500 < x < xr_ + 500 and yb - 500 < y < yb + Wy + 500]
            asignar_marcas(inte, W, maxd=1500)
            cortes.append(dict(hoja=n, nombre=a["VIE_NAM"].replace(" ", ""), Wc=Wc, cx=cx, cy=cy, miembros=inte))
    return cortes

CORTES = leer_cortes()
def ubicar(c):
    cands = []
    for mid, m in MOD.items():
        for v, ln in amarres(m):
            for ln2 in (ln, ln + 300):          # el amarre se dibuja partido por la placa central (300 mm)
                cands.append((abs(ln2 - c["Wc"]) / c["Wc"] + (0 if m["hoja"] == c["hoja"] else 0.01), mid, v, ln2))
    if not cands: return None
    d, mid, v, ln = min(cands)
    return dict(modulo=mid, v=v, dif_pct=round(abs(ln - c["Wc"]) / c["Wc"] * 100, 2), tie=round(ln)) if d < 0.04 else None
for c in CORTES: c["ubic"] = ubicar(c)

# ------------------------------------------------------------------ pata 12,0 m (H33) y stub (H35)
def leer_pata():
    TR, M = HOJ[33]
    L = merge(TR, tol=4, gap=120)
    largas = [l for l in L if lon(l) > 10000 and abs((l[3] - l[1]) / (l[2] - l[0]) + 0.1363) < 0.01]
    largas.sort(key=lambda l: -(l[1] + l[3]))
    l1, l2 = largas[0], largas[1]                  # las dos aristas del montante (la tercera es la pestaña desplegada)
    def ext(l): return sorted([(l[0], l[1]), (l[2], l[3])])
    a1, b1 = ext(l1); a2, b2 = ext(l2)
    top = ((a1[0] + a2[0]) / 2, (a1[1] + a2[1]) / 2); bot = ((b1[0] + b2[0]) / 2, (b1[1] + b2[1]) / 2)
    pend = (bot[1] - top[1]) / (bot[0] - top[0])
    def yleg(x): return top[1] + (x - top[0]) * pend
    hips = [l for l in L if lon(l) > 8000 and -0.35 < (l[3] - l[1]) / (l[2] - l[0]) < -0.2]
    hip = max(hips, key=lambda l: lon(l)); ha, hb = ext(hip)
    hp = (hb[1] - ha[1]) / (hb[0] - ha[0])
    def yhip(x): return ha[1] + (x - ha[0]) * hp
    cand = []
    for l in L:
        if lon(l) < 300 or l in (l1, l2, hip): continue
        sl = (l[3] - l[1]) / (l[2] - l[0]) if abs(l[2] - l[0]) > 1e-6 else 1e9
        if lon(l) > 2000 and (abs(sl - pend) < 0.01 or abs(sl - hp) < 0.01): continue
        ok = all(top[0] - 60 <= x <= bot[0] + 1200 and yleg(x) - 60 <= y <= max(yhip(x), yleg(x)) + 80 for x, y in ((l[0], l[1]), (l[2], l[3])))
        if ok: cand.append(l)
    ejes = merge(emparejar(cand), tol=10, gap=260)
    out = []
    for l in ejes:
        P = []
        for (x, y) in ((l[0], l[1]), (l[2], l[3])):
            for lin, f in (((top[0], top[1], bot[0], bot[1]), yleg), ((ha[0], ha[1], hb[0], hb[1]), yhip)):
                if abs(y - f(x)) < 170:
                    q = interseca(l, (lin[0], lin[1]), (lin[2], lin[3]))
                    if q and math.hypot(q[0] - x, q[1] - y) < 320: x, y = q
                    break
            P.append((x, y))
        if math.dist(P[0], P[1]) < 400: continue
        out.append(dict(a=P[0], b=P[1], len=math.dist(P[0], P[1]), mid=((P[0][0] + P[1][0]) / 2, (P[0][1] + P[1][1]) / 2)))
    W = [(x, y, a) for nm, x, y, a in M if nm == "WORDING" and top[0] - 500 < x < bot[0] + 1500 and yleg(x) - 1500 < y < yhip(x) + 1500]
    asignar_marcas(out, W)
    def piezas(f, prefijos, minl):
        ws = {}
        for x, y, a in W:
            if a.get("NOM_PRO", "").startswith(prefijos) and num(a.get("LUN_PRO")) >= minl and abs(y - f(x)) < 1500:
                ws.setdefault(marca_de(a), (x, a))
        return sorted(ws.values(), key=lambda t: t[0])       # de arriba (x menor) hacia abajo
    return dict(top=top, bot=bot, yleg=yleg, hip=(ha, hb), miembros=out,
                p_leg=piezas(yleg, ("L100", "L90", "L80"), 1500), p_hip=piezas(yhip, ("L70", "L75", "L65"), 2500))
PATA = leer_pata()

def leer_cierre():
    """H34: panel de cierre entre los dos cordones interiores de la pata (uno por cara de la esquina)."""
    TR, M = HOJ[34]
    L = merge(TR, tol=4, gap=120)
    largas = [l for l in L if lon(l) > 10000]
    def ext(l): return sorted([(l[0], l[1]), (l[2], l[3])])
    # de las cuatro líneas largas, las dos interiores son los cordones (las exteriores son las pestañas desplegadas)
    largas.sort(key=lambda l: (l[1] + l[3]) / 2)
    lo_, up_ = ext(largas[1]), ext(largas[2])
    def yl(x): return lo_[0][1] + (x - lo_[0][0]) * (lo_[1][1] - lo_[0][1]) / (lo_[1][0] - lo_[0][0])
    def yu(x): return up_[0][1] + (x - up_[0][0]) * (up_[1][1] - up_[0][1]) / (up_[1][0] - up_[0][0])
    x0 = min(lo_[0][0], up_[0][0]); x1 = max(lo_[1][0], up_[1][0])
    cand = [l for l in L if lon(l) >= 300 and l not in largas and
            all(x0 - 50 <= x <= x1 + 50 and yl(x) - 60 <= y <= yu(x) + 60 for x, y in ((l[0], l[1]), (l[2], l[3])))]
    ejes = merge(emparejar(cand), tol=10, gap=200)
    out = []
    for l in ejes:
        if lon(l) < 400: continue
        out.append(dict(a=(l[0], l[1]), b=(l[2], l[3]), len=lon(l), mid=((l[0] + l[2]) / 2, (l[1] + l[3]) / 2)))
    W = [(x, y, a) for nm, x, y, a in M if nm == "WORDING" and x0 - 500 < x < x1 + 500 and yl(x) - 1500 < y < yu(x) + 1500]
    asignar_marcas(out, W)
    def st(p):
        s_ = (p[0] - x0) / (x1 - x0); t_ = (p[1] - yl(p[0])) / (yu(p[0]) - yl(p[0]))
        return min(1.0, max(0.0, s_)), min(1.0, max(0.0, t_))
    return [dict(e, sa=st(e["a"]), sb=st(e["b"])) for e in out]
CIERRE = leer_cierre()

def stub_largo():
    rows = [f for f in FILAS if f["conjunto"] == "STUB" and f["desc"].startswith("L ")]
    r = max(rows, key=lambda f: num(f["long"])) if rows else None
    return (num(r["long"]), r["pos"], r["desc"]) if r else (0, None, None)
STUB = stub_largo()

# ------------------------------------------------------------------ cabeza (H1 brazo, H3 viga, H4 delta superior, H5/H6 delta inferior)
# Cada vista es la cara a verdadero tamaño; se lleva a 3D con una isometría plano->espacio deducida de la geometría
# de los montantes (ver SUPUESTOS_CABEZA). Coordenadas: X transversal, Y vertical, Z longitudinal.
for k_ in (1, 2, 3, 4, 5, 6):
    if k_ not in HOJ: HOJ[k_] = cargar(k_)
Y_T = Yv(MOD["AP1"], MOD["AP1"]["vt"])            # tope de la extensión AP1 = pie de los deltas
TPHI = 0.05356                                     # inclinación de la cara frontal de los deltas (dz/dY)
CPHI, SPHI = 1 / math.sqrt(1 + TPHI ** 2), TPHI / math.sqrt(1 + TPHI ** 2)
X_PIE, Z_PIE = 1566.0, 1554.0                      # pie de los deltas (frontal H5 / cara longitudinal H6)
E_EXT = (-3906 / 10812, 10082 / 10812)             # dirección de la cara exterior (montante exterior, H5/H6)
X_CRU, DY_CRU = -56.5, 1952 * CPHI                 # cruce de los montantes interiores (H5)
E_INT = (-4508 / 7382, 5846 / 7382)
DY_DS = 7798 * CPHI                                # base del delta superior = tope del montante interior del delta inferior
X_DS = 4564.0                                      # montante interior (vertical) del delta superior
Y_BB = Y_T + DY_DS + (18960 - 12620)               # cordón inferior de la viga (tope del delta superior, H4)
ZV = 797.0                                         # semiancho de la viga (H3 cortes A-A / B-B)

def z_front(Y): return Z_PIE - TPHI * (Y - Y_T)

def m_di_frontal(x, y):
    d = y - 22167.5; Y = Y_T + d * CPHI
    return (x - 85956.5, Y, Z_PIE - d * SPHI)
def m_di_ext(x, y):
    s = 19773.5 - x
    return (-X_PIE + s * E_EXT[0], Y_T + s * E_EXT[1], y - 24573.7)
def m_di_int(x, y):
    s = 16393 - x
    return (X_CRU + s * E_INT[0], Y_T + DY_CRU + s * E_INT[1], y - 20247)
def m_ds_frontal(x, y):
    Y = Y_T + DY_DS + (18960 - x) * CPHI
    return (-(X_DS + (y - 20970)), Y, z_front(Y))
_A0, _A1 = (22540, 23849), (13642, 26305); _B0, _B1 = (21751, 21462), (13143, 24793)
_M0 = ((_A0[0] + _B0[0]) / 2, (_A0[1] + _B0[1]) / 2); _M1 = ((_A1[0] + _B1[0]) / 2, (_A1[1] + _B1[1]) / 2)
_LU = math.dist(_M0, _M1); _U = ((_M1[0] - _M0[0]) / _LU, (_M1[1] - _M0[1]) / _LU); _V = (-_U[1], _U[0])
_S0 = (3109 - math.dist(_A0, _B0)) / 0.1          # distancia en la cara desde el pie (el ancho decrece 0,1 por mm)
def m_ds_ext(x, y):
    p = (x - _M0[0], y - _M0[1]); s = _S0 + p[0] * _U[0] + p[1] * _U[1]; t = p[0] * _V[0] + p[1] * _V[1]
    return (-X_PIE + s * E_EXT[0], Y_T + s * E_EXT[1], t)
def _yinc(x): return 18245 + (x - 12545) * (17580 - 18245) / (18841 - 12545)
def m_ds_int(x, y):
    zh = (19831 - _yinc(x)) / 2
    return (-X_DS, Y_T + DY_DS + (18960 - x), zh - (19831 - y))
def m_viga_frontal(x, y): return (x - 26801, Y_BB + (y - 9462.5), ZV)
def h_sup(X): return 1041 + min(abs(X), 7672) * (1632 - 1041) / 7672
def m_viga_sup(x, y):
    X = x - 26801
    return (X, Y_BB + h_sup(X), y - 13112.5)
def m_viga_inf(x, y): return (x - 26801, Y_BB, y - 7244)
def z_brazo(X, Y):
    zb = ZV - max(0.0, X - 7112) * (ZV - 400) / 3820 if X <= 10932 else max(0.0, 400 * (1 - (X - 10932) / 996))
    zt = max(0.0, ZV * (1 - (X - 7672) / 2612))
    ht = 1632 + max(0.0, X - 7672) * 153 / 2612
    h = min(1.0, max(0.0, (Y - Y_BB) / ht))
    return (1 - h) * zb + h * zt
def m_brazo_frontal(x, y):
    X = 7112 + (x - 62870); Y = Y_BB + (y - 19722)
    return (X, Y, z_brazo(X, Y))
def m_brazo_sup(x, y):
    X = 7672 + (x - 51191)
    return (X, Y_BB + 1632 + (X - 7672) * 153 / 2612, y - 20594)
def m_brazo_inf(x, y): return (7112 + (x - 55374), Y_BB, y - 20512)

def _dist_lin(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    return ((p[0] - a[0]) * dy - (p[1] - a[1]) * dx) / math.hypot(dx, dy)

VISTAS_CABEZA = [  # hoja, caja (x0,y0,x1,y1), filtro, mapeo, copias (sx, sz), grupo, vista
    (5, (76000, 21900, 92500, 32700), None, m_di_frontal, [(1, 1), (1, -1)], "Delta inferior", "frontal delta inferior"),
    (6, (8800, 22700, 20100, 26300), None, m_di_ext, [(1, 1), (-1, 1)], "Delta inferior", "cara exterior delta inferior"),
    (6, (8800, 18600, 16600, 22000), None, m_di_int, [(1, 1), (-1, 1)], "Delta inferior", "corte D1-D1 (cara interior)"),
    (4, (12500, 20300, 19100, 23700), lambda x, y: _dist_lin((x, y), _B0, _B1) < -450, m_ds_frontal,
     [(1, 1), (1, -1), (-1, 1), (-1, -1)], "Delta superior", "frontal delta superior"),
    (4, (12500, 20000, 23000, 26500), lambda x, y: _dist_lin((x, y), _B0, _B1) > -80 and _dist_lin((x, y), _A0, _A1) < 80, m_ds_ext, [(1, 1), (-1, 1)], "Delta superior", "delta superior long."),
    (4, (12400, 17300, 19100, 19950), None, m_ds_int, [(1, 1), (-1, 1)], "Delta superior", "corte D-D (cara interior)"),
    (3, (19000, 9300, 34600, 11300), None, m_viga_frontal, [(1, 1), (1, -1)], "Viga", "frontal viga"),
    (3, (19000, 12200, 34600, 14050), None, m_viga_sup, [(1, 1)], "Viga", "corte A-A (plano superior)"),
    (3, (19000, 6300, 34600, 8200), None, m_viga_inf, [(1, 1)], "Viga", "corte B-B (plano inferior)"),
    (1, (62700, 19600, 67000, 21650), None, m_brazo_frontal, [(1, 1), (1, -1), (-1, 1), (-1, -1)], "Brazos", "frontal brazo"),
    (1, (51000, 19600, 54100, 21600), None, m_brazo_sup, [(1, 1), (-1, 1)], "Brazos", "brazo superior"),
    (1, (55200, 19500, 60400, 21500), None, m_brazo_inf, [(1, 1), (-1, 1)], "Brazos", "brazo inferior"),
]

def leer_cabeza():
    piezas = []
    for n, (x0, y0, x1, y1), filt, f, copias, grupo, vista in VISTAS_CABEZA:
        TR, M = HOJ[n]
        L = [l for l in merge(TR) if all(x0 <= x <= x1 and y0 <= y <= y1 and (filt is None or filt(x, y)) for x, y in ((l[0], l[1]), (l[2], l[3])))]
        ejes = merge(emparejar(L), tol=10, gap=200)
        mm = [dict(a=(l[0], l[1]), b=(l[2], l[3]), len=lon(l), mid=((l[0] + l[2]) / 2, (l[1] + l[3]) / 2)) for l in ejes if lon(l) >= 350]
        W = [(x, y, a) for nm, x, y, a in M if nm == "WORDING" and x0 - 1200 <= x <= x1 + 1200 and y0 - 1200 <= y <= y1 + 1200]
        asignar_marcas(mm, W)
        for e in mm:
            A, B = f(*e["a"]), f(*e["b"])
            for sx, sz in copias:
                pa = [round(sx * A[0]), round(A[1]), round(sz * A[2])]; pb = [round(sx * B[0]), round(B[1]), round(sz * B[2])]
                piezas.append(dict(a=pa, b=pb, grupo=grupo, vista=vista, hoja=n, wording=e.get("wording"), len=e["len"]))
    # una misma barra aparece en dos vistas (p. ej. montante en la frontal y en la lateral): se deja una
    out = []
    for p in piezas:
        dup = None
        for q in out:
            if (math.dist(p["a"], q["a"]) < 90 and math.dist(p["b"], q["b"]) < 90) or (math.dist(p["a"], q["b"]) < 90 and math.dist(p["b"], q["a"]) < 90):
                dup = q; break
        if dup is None: out.append(p)
        elif dup.get("wording") is None and p.get("wording") is not None:
            dup["wording"], dup["hoja"], dup["len"] = p["wording"], p["hoja"], p["len"]
    return out
CABEZA = leer_cabeza()

def miembros_cabeza(k):
    out = []
    for i, p in enumerate(CABEZA):
        a = p["wording"][2] if p.get("wording") else None
        tipo = "Montante" if (p["grupo"].startswith("Delta") and math.dist(p["a"], p["b"]) > 4000) else (
            "Horizontal" if abs(p["a"][1] - p["b"][1]) < 30 else "Diagonal")
        out.append(mk(f"AP{k}-CAB-{i+1:04d}", f"{tipo} · {p['grupo']} · {p['vista']}", p["grupo"], p["grupo"], tipo, "cabeza",
                      p["a"], p["b"], f"plano H{p['hoja']} ({p['vista']})", "transversal", wa=a, hoja=p["hoja"], len_eje=p["len"]))
    return out

# ------------------------------------------------------------------ construir torres
def coh(len_eje, a):
    lun = num(a.get("LUN_PRO")); d = abs(lun - len_eje)
    return ("ok" if d <= max(0.04 * len_eje, 70) else "revisar"), f"Longitud de pieza {round(lun)} mm vs eje del modelo {round(len_eje)} mm."

def mk(id_, nombre, grupo, tramo, tipo, cara, a, b, fuente, cara_tipo, wa=None, len_eje=None, hoja=None, verif=None):
    d = dict(id=id_, nombre=nombre, grupo=grupo, tramo=tramo, tipo=tipo, cara=cara, a=a, b=b,
             longitud_mm=round(math.dist(a, b)), dx_mm=b[0] - a[0], dy_mm=b[1] - a[1], dz_mm=b[2] - a[2],
             fuente=fuente, cara_tipo=cara_tipo, comp="comun")
    if wa is not None:
        m0 = marca_de(wa); u = ubicar_marca(hoja, m0) or (hoja, m0)
        d["marca"] = u[1]; d["marcas"] = [f"{u[0]}|{u[1]}"]
        c = conj_de(hoja, m0)
        if c: d["conjunto"] = c if isinstance(c, str) else " / ".join(sorted(set(c)))
        d["coherencia"], d["coherencia_txt"] = coh(len_eje if len_eje else d["longitud_mm"], wa)
        d["verificacion"] = verif or "Marca (WORDING) asignada por longitud de pieza y cercanía al eje; se replica por simetría en las 4 caras."
    return d

def tramos_pieza(p0, p1, piezas):
    """parte el segmento p0->p1 (de arriba a abajo) segun las longitudes de las piezas (ordenadas igual)"""
    if not piezas: return [(p0, p1, None)]
    tot = sum(num(a["LUN_PRO"]) for _, a in piezas) or 1
    out, t0 = [], 0.0
    for _, a in piezas:
        t1 = t0 + num(a["LUN_PRO"]) / tot
        out.append(((p0[0] + (p1[0] - p0[0]) * t0, p0[1] + (p1[1] - p0[1]) * t0), (p0[0] + (p1[0] - p0[0]) * t1, p0[1] + (p1[1] - p0[1]) * t1), a))
        t0 = t1
    return out

def miembros_modulo(mid, k):
    real = mid.rstrip("s"); m = MOD[real]; n = m["hoja"]
    grupo = "Extensión AP4" if mid == "B4s" else m["nombre"]
    vcut = (V_KK - 30) if mid == "B4s" else None
    out = []
    for k_ in (0, 2):
        for lado in ("L", "R"):
            lg = m["LL"] if lado == "L" else m["RR"]
            # piezas ordenadas de abajo hacia arriba en la hoja
            for i, (pa, pb, a) in enumerate(tramos_pieza((lg[0], lg[1]), (lg[2], lg[3]), [(y, a) for y, a in m["piezas"][lado]])):
                va, vb_ = pa[1], pb[1]
                if vcut is not None:
                    if vb_ <= vcut: continue
                    va = max(va, vcut)
                if a is not None and not incluye(k, n, marca_de(a)): continue
                f = m["xl"] if lado == "L" else m["xr"]
                out.append(mk(f"AP{k}-{real}-M{k_}{lado}{i+1}", f"Montante {lado}{k_//2+1}.{i+1} · {grupo}", grupo, grupo, "Montante",
                              "A" if k_ == 0 else "C", punto3(m, f(va), va, k_), punto3(m, f(vb_), vb_, k_),
                              f"plano H{n} (vista de cara)", "transversal", wa=a, hoja=n, len_eje=math.dist(pa, pb)))
    for j, e in enumerate(m["miembros_cara"]):
        if vcut is not None and min(e["a"][1], e["b"][1]) < vcut: continue
        wd = e.get("wording"); a = wd[2] if wd else None
        if a is not None and not incluye(k, n, marca_de(a)): continue
        tipo = "Horizontal" if abs(e["b"][1] - e["a"][1]) < 25 else "Diagonal"
        for kk in range(4):
            out.append(mk(f"AP{k}-{real}-{'ABCD'[kk]}-{tipo[0]}{j+1:02d}", f"{tipo} {j+1:02d} · {grupo} · Cara {'ABCD'[kk]}", grupo, grupo, tipo,
                          "ABCD"[kk], punto3(m, e["a"][0], e["a"][1], kk), punto3(m, e["b"][0], e["b"][1], kk),
                          f"plano H{n} (vista de cara)", "transversal" if kk % 2 == 0 else "longitudinal", wa=a, hoja=n, len_eje=e["len"]))
    return out

def miembros_cortes(k, mids):
    out = []; usados = []
    reales = [x.rstrip("s") for x in mids]
    for c in CORTES:
        u = c["ubic"]
        if not u or u["modulo"] not in reales: continue
        m = MOD[u["modulo"]]
        if "B4s" in mids and u["modulo"] == "B4" and u["v"] < V_KK - 30: continue
        marc = [e for e in c["miembros"] if e.get("wording")]
        if marc and not any(incluye(k, c["hoja"], marca_de(e["wording"][2])) for e in marc): continue
        Y = round(Yv(m, u["v"])); n0 = len(out)
        for j, e in enumerate(c["miembros"]):
            a = e["wording"][2] if e.get("wording") else None
            if a is not None and not incluye(k, c["hoja"], marca_de(a)): continue
            P = [[round(p[0] - c["cx"]), Y, round(p[1] - c["cy"])] for p in (e["a"], e["b"])]
            tipo = "Horizontal" if abs(e["a"][1] - e["b"][1]) < 20 or abs(e["a"][0] - e["b"][0]) < 20 else "Diagonal"
            out.append(mk(f"AP{k}-C{c['hoja']}-{c['nombre']}-{j+1:02d}", f"{tipo} {j+1:02d} · Corte {c['nombre']}", "Arriostramiento horizontal",
                          f"Corte {c['nombre']}", tipo, "planta", P[0], P[1], f"plano H{c['hoja']} (corte {c['nombre']})", "longitudinal",
                          wa=a, hoja=c["hoja"], len_eje=e["len"], verif="Marca (WORDING) del corte asignada por longitud de pieza y cercanía."))
        if len(out) > n0: usados.append(c)
    return out, usados

def miembros_patas(k, ultimo):
    m = MOD[ultimo]; Ytop = m["Yb"]; htop = m["wb"] / 2
    P = PATA; x0, y0 = P["top"]
    def p3(pt, kk, lado):
        d = pt[0] - x0; u = pt[1] - y0
        Y = Ytop - d / VC; h = htop + S_VERT * (Ytop - Y)
        X, Z = (htop - u, h) if lado == 0 else (h, htop - u)
        X, Z = rot(X, Z, kk)
        return [round(X), round(Y), round(Z)]
    out = []; G = "Patas 12,0 m"
    for kk in range(4):
        esq = kk + 1
        for i, (pa, pb, a) in enumerate(tramos_pieza(P["top"], P["bot"], P["p_leg"])):
            out.append(mk(f"AP{k}-PATA-E{esq}-M{i+1}", f"Montante pata · esquina {esq} · tramo {i+1}", G, "Pata 12,0 m", "Montante", f"E{esq}",
                          p3(pa, kk, 0), p3(pb, kk, 0), "plano H33 (pata 12,0 m)", "transversal", wa=a, hoja=33, len_eje=math.dist(pa, pb),
                          verif="Pieza del montante de la pata (marca del plano), partida en proporción a las longitudes de pieza."))
        for lado in (0, 1):
            cara = f"E{esq}{'AB'[lado]}"
            ha, hb = P["hip"]
            for i, (pa, pb, a) in enumerate(tramos_pieza(ha, hb, P["p_hip"])):
                out.append(mk(f"AP{k}-PATA-{cara}-C{i+1}", f"Cordón pata · {cara} · tramo {i+1}", G, "Pata 12,0 m", "Diagonal", cara,
                              p3(pa, kk, lado), p3(pb, kk, lado), "plano H33 (pata 12,0 m)", "transversal" if lado == 0 else "longitudinal",
                              wa=a, hoja=33, len_eje=None,
                              verif="Cordón interior de la pata dibujado en proyección sobre la cara; su longitud real (vista de cierre H34) es mayor."))
                if a is not None:
                    out[-1]["coherencia"] = "n/a"
                    out[-1]["coherencia_txt"] = f"Pieza {round(num(a['LUN_PRO']))} mm; el eje del modelo es su proyección sobre la cara ({out[-1]['longitud_mm']} mm)."
            for j, e in enumerate(P["miembros"]):
                wd = e.get("wording"); a = wd[2] if wd else None
                tipo = "Horizontal" if abs(e["a"][0] - e["b"][0]) < 25 else "Diagonal"
                out.append(mk(f"AP{k}-PATA-{cara}-{tipo[0]}{j+1:02d}", f"{tipo} {j+1:02d} · pata · {cara}", G, "Pata 12,0 m", tipo, cara,
                              p3(e["a"], kk, lado), p3(e["b"], kk, lado), "plano H33 (pata 12,0 m)", "transversal" if lado == 0 else "longitudinal",
                              wa=a, hoja=33, len_eje=e["len"]))
        # panel de cierre (H34) entre los cordones de las dos caras de la esquina
        ha, hb = P["hip"]
        hA = (p3(ha, kk, 0), p3(hb, kk, 0)); hB = (p3(ha, kk, 1), p3(hb, kk, 1))
        def pc(stv):
            s_, t_ = stv
            pa_ = [hA[0][i] + (hA[1][i] - hA[0][i]) * s_ for i in range(3)]
            pb_ = [hB[0][i] + (hB[1][i] - hB[0][i]) * s_ for i in range(3)]
            return [round(pa_[i] + (pb_[i] - pa_[i]) * t_) for i in range(3)]
        for j, e in enumerate(CIERRE):
            wd = e.get("wording"); a = wd[2] if wd else None
            out.append(mk(f"AP{k}-PATA-E{esq}-CI{j+1:02d}", f"Diagonal {j+1:02d} · cierre de pata · esquina {esq}", G, "Pata 12,0 m", "Diagonal",
                          f"E{esq}", pc(e["sa"]), pc(e["sb"]), "plano H34 (cierre pata 12,0 m)", "longitudinal", wa=a, hoja=34, len_eje=e["len"],
                          verif="Panel de cierre colocado entre los cordones interiores de la esquina (interpolación por posición a lo largo del cordón)."))
        # stub: prolonga el montante de la pata por debajo de la fundación
        if STUB[0] > 0:
            A = p3(P["top"], kk, 0); B = p3(P["bot"], kk, 0)
            L = math.dist(A, B); u = [(B[i] - A[i]) / L for i in range(3)]
            C = [round(B[i] + u[i] * STUB[0]) for i in range(3)]
            d = mk(f"AP{k}-STUB-E{esq}", f"Stub · esquina {esq}", "Stubs", "Stub", "Montante", f"E{esq}", B, C, "listado H35 (stub)", "transversal")
            d["marca"] = STUB[1]; d["marcas"] = [f"35|{STUB[1]}"]; d["conjunto"] = "STUB"
            d["coherencia"] = "n/a"; d["coherencia_txt"] = f"Longitud del listado ({round(STUB[0])} mm); colocado en la prolongación del montante (supuesto)."
            out.append(d)
    return out

# ------------------------------------------------------------------ lista de materiales por torre
def peso_resumen():
    import xlrd
    rs = xlrd.open_workbook(XLSPATH).sheet_by_name("RESUMEN"); out = {}; na = 0
    for r in range(2, rs.nrows):
        d = str(rs.cell_value(r, 1)).strip(); p = rs.cell_value(r, 2)
        if d and isinstance(p, float):
            if d.startswith("ANTIESCALATORIO"): na += 1; d = f"ANTIESCALATORIO #{na}"
            out[d] = p
    return out
PESO_RES = peso_resumen()

def clase(desc):
    if desc.startswith("L "): return "PERFIL"
    if desc.startswith("LAM"): return "LÁMINA"
    if desc.startswith("VAR"): return "VARILLA"
    return "TORNILLERÍA"

def bom_torre(k):
    filas, marcas, peso, peso_res = [], {}, 0.0, 0.0
    usar = [(c, MULT[c][k - 1], "") for c in CONJUNTOS if c in MULT and MULT[c][k - 1] > 0]
    usar += [("PATA 12.0m", 4, "pata modelada, una por esquina"), ("STUB", 4, "uno por esquina")]
    for c, mlt, obs in usar:
        peso_res += PESO_RES.get(c, 0.0) * mlt
        for f in FILAS:
            if f["conjunto"] != c: continue
            cl = clase(f["desc"])
            perfil = f"{f['desc']} x {f['dim']}" if cl == "LÁMINA" and f["dim"] else f["desc"]
            cant = num(f["cant"]) * mlt; pk = f["peso"] * mlt; peso += pk
            filas.append(dict(hoja=f["hoja"], hoja_txt=str(f["hoja"]), clase=cl, marca=f["pos"], perfil=perfil, long_mm=f["long"],
                              cant=str(round(cant)), acero=f["calidad"], conjunto=c, obs=obs, peso_kg=round(pk, 2)))
            key = f"{f['hoja']}|{f['pos']}"
            if key not in marcas:
                marcas[key] = dict(marca=f["pos"], hoja=f["hoja"], clase=cl, perfil=perfil, long_mm=f["long"], cant=str(round(cant)),
                                   acero=f["calidad"], conjunto=c, en_lista=True)
    return filas, marcas, peso, peso_res

OPCIONES_PIE = [dict(conjunto=c, hoja=HOJA_CONJ.get(c), peso_kg=round(PESO_RES.get(c, 0.0), 1))
                for c in CONJUNTOS if c.startswith(("PATA", "PARRILLA"))]

SUPUESTOS = [
    "Geometría de las vistas de cara (verdadero tamaño, mm); las 4 caras se asumen iguales y la planta cuadrada.",
    "Cota Y=0 en el pie de las patas (terreno); los stubs quedan bajo el terreno. Las alturas relativas salen del ancho de los montantes (tronco único, pendiente 0,1375757 por vertical) y coincide con la longitud de cara de cada módulo (ver validación).",
    "Composición y pesos según la hoja RESUMEN del listado de materiales rev. A14; cada marca del plano se asocia a su conjunto y solo entra si el conjunto pertenece a la torre (p. ej. el cierre 'cuerpo AP1' solo en AP1).",
    "AP5–AP7: la extensión AP4 es la parte superior de la hoja H12 hasta el amarre K-K; la extensión AP5 se acopla en ese nivel (ver 'desajuste_kk_mm').",
    "Patas: se modela la pata de 12,0 m (la de la silueta) en las 4 esquinas; la cara dibujada en H33 se coloca en las dos caras de cada esquina. Las demás patas (1,5–10,5 m) y las parrillas solo se listan en 'opciones_pie'.",
    "Pata: el cordón interior se coloca con su proyección sobre la cara (H33); el panel de cierre (H34) se coloca entre los cordones de las dos caras de cada esquina, por interpolación a lo largo del cordón (aproximado).",
    "Stub: pieza más larga del conjunto STUB, colocada en la prolongación del montante bajo la pata.",
    "Barras de base (amarre de montaje ~490 mm bajo el extremo del montante) se ubican donde las dibuja la vista de cara.",
    "Cabeza (H1, H3–H6): cada vista es una cara a verdadero tamaño llevada a 3D con su plano: frontal de deltas z = 1554 − 0,05356·(Y−Ytope AP1); caras exterior/interior por la dirección de sus montantes; viga con caras verticales a ±797 mm y cordón superior según la frontal; brazos con la cara frontal interpolada entre los cordones de los planos superior e inferior (aproximado).",
    "Cabeza: ménsula y péndulo (H2) no modelados (sus piezas sí están en la lista); los miembros que aparecen en dos vistas se dejan una sola vez.",
    "Cortes en planta: solo los de contorno cuadrado, en el amarre horizontal cuya longitud coincide con el lado (tolerancia 4 %).",
    "Marcas: asignadas si la longitud de pieza coincide con el eje (≤8 %) y la marca está a ≤1300 mm; si no, el miembro queda sin vínculo.",
]

def construir(k):
    mids = COMPOSICION[k]; reales = [x.rstrip("s") for x in mids]
    miembros = []
    for mid in mids: miembros += miembros_modulo(mid, k)
    mc, cut_usados = miembros_cortes(k, mids); miembros += mc
    miembros += miembros_patas(k, reales[-1])
    miembros = miembros_cabeza(k) + miembros
    # Y=0 en el pie de las patas (terreno), como los demás modelos; los stubs quedan bajo el terreno
    y0 = min(min(m_["a"][1], m_["b"][1]) for m_ in miembros if m_["grupo"].startswith("Patas"))
    for m_ in miembros:
        m_["a"] = [m_["a"][0], m_["a"][1] - y0, m_["a"][2]]; m_["b"] = [m_["b"][0], m_["b"][1] - y0, m_["b"][2]]
    grupos = [dict(nombre="Brazos", color="#d04b5b"), dict(nombre="Viga", color="#e07b39"),
              dict(nombre="Delta superior", color="#8e5bd0"), dict(nombre="Delta inferior", color="#b07cd8")]
    for i, mid in enumerate(mids):
        nombre = "Extensión AP4" if mid == "B4s" else MOD[mid]["nombre"]
        grupos.append(dict(nombre=nombre, color=COLORES[i % len(COLORES)]))
    grupos += [dict(nombre="Arriostramiento horizontal", color="#20b2aa"), dict(nombre="Patas 12,0 m", color="#9a7b4f"),
               dict(nombre="Stubs", color="#6b6b6b")]
    usados_g = {m_["grupo"] for m_ in miembros}; grupos = [g for g in grupos if g["nombre"] in usados_g]
    bom, marcas, peso, peso_res = bom_torre(k)
    for m_ in miembros:                       # marcas del modelo que no estan en el listado
        for key in m_.get("marcas", []):
            if key not in marcas:
                h, p = key.split("|", 1)
                marcas[key] = dict(marca=p, hoja=int(h), clase="", perfil="", long_mm="", cant="", acero="", conjunto=m_.get("conjunto", ""), en_lista=False)
    ys = [p[1] for mm in miembros for p in (mm["a"], mm["b"])]
    ncoh = collections.Counter(m_.get("coherencia") for m_ in miembros if m_.get("marca"))
    ultimo = MOD[reales[-1]]
    val = dict(
        altura_por_largo_vs_ancho_mm={x: round(dY(MOD[x]) - (YC(MOD[x]["wt"]) - YC(MOD[x]["wb"]))) for x in reales},
        anchos_montantes_mm={x: [round(MOD[x]["wb"]), round(MOD[x]["wt"])] for x in reales},
        juntas_ancho_mm=[dict(arriba=reales[i], abajo=reales[i + 1], dif=round(MOD[reales[i + 1]]["wt"] - MOD[reales[i]]["wb"]))
                         for i in range(len(reales) - 1) if not (reales[i] == "B4" and mids[i] == "B4s")],
        desajuste_kk_mm=round(DESAJ_KK) if "B4s" in mids else None,
        miembros_con_marca=sum(1 for m_ in miembros if m_.get("marca")), miembros_total=len(miembros),
        marcas_fuera_de_listado=sorted({k_ for k_, v in marcas.items() if not v["en_lista"]}),
        peso_lista_kg=round(peso, 1), peso_resumen_galvanizado_kg=round(peso_res, 1),
        cortes_ubicados=[dict(hoja=c["hoja"], corte=c["nombre"], lado_mm=round(c["Wc"]), **c["ubic"]) for c in cut_usados],
        cortes_no_ubicados=[dict(hoja=c["hoja"], corte=c["nombre"], lado_mm=round(c["Wc"])) for c in CORTES if not c["ubic"]],
    )
    comp = [c for c in CONJUNTOS if c in MULT and MULT[c][k - 1] > 0]
    # tabla de validacion en el formato del visor: (hoja, cota, plano_mm, modelo_mm)
    tabla = []
    for x in reales:
        m_ = MOD[x]
        tabla.append(dict(hoja=f"H{m_['hoja']}", cota=f"{m_['nombre']}: altura por longitud de cara vs por ancho de montantes",
                          plano_mm=round(dY(m_)), modelo_mm=round(YC(m_["wt"]) - YC(m_["wb"]))))
    for i in range(len(reales) - 1):
        if reales[i] == "B4" and mids[i] == "B4s": continue
        a_, b_ = MOD[reales[i]], MOD[reales[i + 1]]
        tabla.append(dict(hoja=f"H{a_['hoja']}/H{b_['hoja']}", cota=f"ancho de montantes en la junta {a_['nombre']} / {b_['nombre']}",
                          plano_mm=round(a_["wb"]), modelo_mm=round(b_["wt"])))
    for c in cut_usados:
        tabla.append(dict(hoja=f"H{c['hoja']}", cota=f"lado del corte {c['nombre']} vs amarre de la cara",
                          plano_mm=round(c["Wc"]), modelo_mm=c["ubic"]["tie"]))
    ymin = min(ys); pie = [abs(p[i]) for mm in miembros for p in (mm["a"], mm["b"]) if p[1] < ymin + 50 for i in (0, 2)]
    meta = dict(nombre=f"Torre AP{k}", linea="EEB U113 CT100501", plano_base="EEB-U113-CT100501-L000-DIS6049",
                fuente="Planos DIS6049 H01–H37 y listado de materiales torre AP rev. A14 (hoja RESUMEN para la composición).",
                etapa="3 (cabeza, cuerpos, extensiones, cortes, patas 12,0 m y stubs; faltan ménsula y péndulo)",
                composicion=comp + ["PATA 12.0m (x4)", "STUB (x4)"],
                alto_modelo_mm=round(max(ys) - min(ys)), alto_total_mm=round(max(ys) - min(ys)),
                ancho_base_mm=round(2 * max(pie)) if pie else round(ultimo["wb"]), cota_superior_mm=round(max(ys)), cota_inferior_mm=round(min(ys)),
                ancho_base_cuerpo_mm=round(ultimo["wb"]), largo_base_cuerpo_mm=round(ultimo["wb"]),
                peso_total_kg=round(peso, 1), opciones_pie=OPCIONES_PIE,
                marcas_vinculadas=len({m_["marca"] for m_ in miembros if m_.get("marca")}), miembros_total=len(miembros),
                coherencia_marcas=dict(ncoh), validacion=tabla, validacion_detalle=val, supuestos=SUPUESTOS)
    data = dict(meta=meta, grupos=grupos, marcas=marcas, miembros=miembros, lista_materiales=bom)
    with open(os.path.join(OUTDIR, f"torre-ap{k}.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
    print(f"AP{k}:", len(miembros), "miembros |", meta["marcas_vinculadas"], "marcas |", len(bom), "filas BOM | peso", round(peso),
          "/ resumen", round(peso_res), "| alto", meta["alto_modelo_mm"], "| coh", dict(ncoh), "| fuera lista", len(val["marcas_fuera_de_listado"]))
    return val

if __name__ == "__main__":
    for k in range(1, 8): V = construir(k)
    print(json.dumps(V, ensure_ascii=False)[:2500])
    print("cortes:", [(c["hoja"], c["nombre"], round(c["Wc"]), c["ubic"] and c["ubic"]["modulo"]) for c in CORTES])
    print("pata:", len(PATA["miembros"]), [a["MAR_PEZ"] for _, a in PATA["p_leg"]], [a["MAR_PEZ"] for _, a in PATA["p_hip"]], "stub", STUB)
