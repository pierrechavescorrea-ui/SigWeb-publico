#!/usr/bin/env python3
"""Genera wwwroot/modelos3d/torre-aae.json (torre AAE, planos GEB-U113-CT101850-L000-DIS6012 H1..H16).

Uso: python3 generar_torre_aae.py <dxf1:dxf2> <carpeta modelos3d>
  carpeta_dxf: H1.dxf .. H16.dxf (dwg2dxf), con las variantes H12b, H13b, H14b, H15b opcionales.

Método (cuerpo común, tramos 1-5):
 * Cada tramo tiene una vista de la cara en verdadera magnitud (plano de la cara). Se ubica con una línea de referencia
   (cota del tramo + ancho conocido) y se pasa a 3D: u = x - cx (horizontal, 1:1), Y = Yref + (y - yref)/1.009555607
   (factor 'EN CARA' del plano), profundidad = semiancho del tronco a esa cota (pendiente 0.138573 por mm vertical).
 * Los dibujos de T2 y T4 muestran media cara (eje de simetría = vertical por el centro de la viga superior): se espeja
   y el sufijo de marca Z <-> D se intercambia.
 * Marcas: burbuja POS(+NOTA) -> leader -> eje EJES más cercano (<=130 mm del plano).
 * Perfil/longitud/cantidad/acero salen de los bloques PERFILES y LAMINAS de cada hoja.
"""
import sys, os, json, math, re, collections, itertools
import numpy as np
from ezdxf import recover

DXFDIRS, OUT = sys.argv[1].split(":"), sys.argv[2]          # OUT = carpeta modelos3d (se escriben torre-aae1..4.json)
FC = 1.009555607                 # factor "EN CARA" (plano H9/H11/...)
S = 0.138573170                  # pendiente vertical (semiancho por mm vertical)
HW0 = 5054.29 / 2                # semiancho a Y=18000 (inicio del tramo 2)
Y_BASE = 0                       # cota 0 = base del cuerpo común (tramo 5), Y=20000 = tope del tramo 1
# (cota Y, ancho mm): H= de los planos; entre nodos el tronco es recto
NODOS = [(20000, 4500.0), (18000, 5054.29), (11250, 6925.03), (9000, 7548.61), (2250, 9419.35), (0, 10042.93), (-6750, 11883.38), (-9000, 12506.96),
         (-14700, 14086.70), (-17700, 14918.14), (-23700, 16581.01), (-27700, 17689.60), (-31700, 18798.19), (-37700, 20461.06), (-41700, 21569.64), (-47700, 23232.52), (-51700, 24341.11), (-57700, 26003.99)]
_NY = [n[0] for n in NODOS][::-1]; _NW = [n[1] / 2 for n in NODOS][::-1]
def hw(Y): return float(np.interp(Y, _NY, _NW, left=None, right=None)) if _NY[0] <= Y <= _NY[-1] else (_NW[-1] + (Y - _NY[-1]) * (_NW[-1] - _NW[-2]) / (_NY[-1] - _NY[-2]))

_DOCS = {}
def ruta(n):
    for d in DXFDIRS:
        p = os.path.join(d, f"H{n}.dxf")
        if os.path.exists(p): return p
def doc(n):
    if n not in _DOCS: _DOCS[n] = recover.readfile(ruta(n))[0].modelspace()
    return _DOCS[n]

def limpiar(t): return re.sub(r"\\[A-Za-z0-9.;|]*|[{}]", "", t).replace("\\P", " ").strip()

def seg_dist(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]; L2 = dx * dx + dy * dy
    t = 0 if L2 == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / L2))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)

def clusters(ax, gap=200):
    n = len(ax); par = list(range(n))
    def f(a):
        while par[a] != a: par[a] = par[par[a]]; a = par[a]
        return a
    for i in range(n):
        for j in range(i + 1, n):
            a, b = ax[i]; c, d = ax[j]
            if min(seg_dist(a, c, d), seg_dist(b, c, d), seg_dist(c, a, b), seg_dist(d, a, b)) < gap: par[f(i)] = f(j)
    g = {}
    for i in range(n): g.setdefault(f(i), []).append(i)
    return list(g.values())

# ---------------------------------------------------------------- lista de materiales (H1..H16, incl. variantes)
HOJAS = [("1", 1), ("2", 2), ("3", 3), ("4", 4), ("5", 5), ("6", 6), ("7", 7), ("8", 8), ("9", 9), ("10", 10), ("11", 11),
         ("12", 12), ("12b", 12), ("13", 13), ("13b", 13), ("14", 14), ("14b", 14), ("15", 15), ("15b", 15), ("16", 16)] + \
        [(str(i), i) for i in range(17, 33)] + [("33", 33), ("33b", 33), ("34", 34), ("35", 35)] + \
        [(str(i), i) for i in range(36, 53)] + [("40b", 40), ("48b", 48), ("52b", 52)] + \
        [("53_A1", 53), ("54_A4", 54), ("55_A5", 55), ("56_A6", 56), ("57_A4", 57), ("58_A6", 58), ("59_A6", 59), ("60_A6", 60), ("61_A1", 61), ("62_A2", 62), ("62_A5", 62)]
# componente a la que pertenece cada hoja (para armar AAE1..AAE4): comun / b1 (cuerpo AAE1) / e2 / b2 / e3 / b3 / e4 / b4
COMP_HOJA = {**{i: "comun" for i in range(1, 21)}, 22: "comun", 21: "b1", 23: "e2", 24: "b2", 25: "b2", 26: "e3", 27: "e3", 28: "b3", 29: "b3", 30: "b3",
             31: "e4", 32: "e4", 33: "b4", 34: "b4", 35: "b4", 36: "e5", 37: "e5", 38: "b5", 39: "b5", 40: "b5", **{i: "patas" for i in range(41, 54)},
             54: "comun", 55: "comun", 56: "comun", 57: "b2", 58: "b3", 59: "b4", 60: "b5", 61: "comun", 62: "escalera"}
def comp_fila(f):
    """comp de una fila de la lista; la hoja 61 (barandas) se reparte por conjunto: B4/B5 -> Y-Y (b4), B6/B7 -> Y'-Y' (b5)"""
    if f["hoja"] == 61:
        c = f["conjunto"]
        if re.search(r"B[67]", c): return "b5"
        if re.search(r"B[45]", c) and not re.search(r"B[12367]", c): return "b4"
        return "comun"
    return COMP_HOJA.get(f["hoja"])
BOM, BOMKEY, visto = [], collections.defaultdict(list), set()
CONJ_HOJA = {}
for arch, n in HOJAS:
    if not ruta(arch): continue
    ms = doc(arch)
    conj = ""
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name == "CONJUNTO":
            a = {x.dxf.tag: x.dxf.text for x in e.attribs}
            if a.get("NOM") and not conj: conj = a["NOM"].replace("CUEPRO", "CUERPO").strip()
    CONJ_HOJA[arch] = conj
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name in ("PERFILES", "LAMINAS"):
            a = {x.dxf.tag: x.dxf.text.strip() for x in e.attribs}
            if not a.get("POS"): continue
            f = {"hoja": n, "hoja_txt": str(n) + ("" if arch == str(n) else " (variante)"), "clase": "PERFIL" if e.dxf.name == "PERFILES" else "LÁMINA",
                 "marca": a["POS"], "perfil": a.get("DES", "") + (" x " + a["ANC"] if a.get("ANC") else ""), "long_mm": a.get("LON", ""),
                 "cant": a.get("CAN", ""), "acero": a.get("CAL", ""), "conjunto": (a.get("CON") or conj).replace("CUEPRO", "CUERPO"), "obs": a.get("OBS", "")}
            k = tuple(f.values())
            if k in visto: continue
            visto.add(k); BOM.append(f); BOMKEY[(arch, a["POS"])].append(f)

for arch in ("52", "53_A1"):                         # parrillas: la lista está en los bloques PARR (POS, DESC, LONG, CANT) en vez de PERFILES/LAMINAS
    if not ruta(arch): continue
    n = 52 if arch == "52" else 53
    cnt = collections.Counter(); meta = {}
    for e in doc(arch):
        if e.dxftype() == "INSERT" and e.dxf.name.startswith("PARR") and e.attribs:
            a = {x.dxf.tag: x.dxf.text.strip() for x in e.attribs}
            if a.get("POS") and (a.get("DESC") or a.get("LONG")):
                k = (a["POS"], a.get("DESC", ""), a.get("LONG", "")); cnt[k] += 1; meta[k] = a.get("CANT")
    for (pos, desc, lon), c in sorted(cnt.items()):
        cant = meta[(pos, desc, lon)] or str(c)
        f = {"hoja": n, "hoja_txt": str(n), "clase": "LÁMINA" if desc.upper().startswith("LAM") else "PERFIL", "marca": pos, "perfil": re.sub(r"\s+", " ", desc) or "(sin descripción)",
             "long_mm": lon, "cant": cant, "acero": "", "conjunto": "PARRILLA " + ("LIVIANA" if n == 52 else "PESADA"),
             "obs": "" if meta[(pos, desc, lon)] else "cantidad = n.º de veces que el plano dibuja la pieza"}
        BOM.append(f); BOMKEY[(arch, pos)].append(f)

def marcas_hoja(arch):
    """eje (índice en ax) -> [marca]  (burbuja POS+NOTA -> leader -> eje EJES más cercano)"""
    ms = doc(arch)
    ax = [((e.dxf.start.x, e.dxf.start.y), (e.dxf.end.x, e.dxf.end.y)) for e in ms if e.dxftype() == "LINE" and e.dxf.layer == "EJES"]
    bub = []
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name.lower() == "pos":
            pos = "".join(a.dxf.text for a in e.attribs if a.dxf.tag == "POS").strip()
            nota = "".join(a.dxf.text for a in e.attribs if a.dxf.tag == "NOTA").strip()
            bub.append((e.dxf.insert.x, e.dxf.insert.y, pos + nota))
    mk = collections.defaultdict(list)
    for l in ms:
        if l.dxftype() != "LEADER" or not bub or not ax: continue
        v = [(p[0], p[1]) for p in l.vertices]
        ends = [v[0], v[-1]]
        db = [min(math.hypot(q[0] - b[0], q[1] - b[1]) for b in bub) for q in ends]
        ti = 0 if db[0] > db[1] else 1
        tip, base = ends[ti], ends[1 - ti]
        b = min(bub, key=lambda b: math.hypot(b[0] - base[0], b[1] - base[1]))
        best = min(range(len(ax)), key=lambda i: seg_dist(tip, *ax[i]))
        if seg_dist(tip, *ax[best]) < 130 and b[2] and b[2] not in mk[best]: mk[best].append(b[2])
    return ax, mk

# bubbles with POS attached by proximity (no leader): marcas sin leader se ignoran

# ---------------------------------------------------------------- definición de tramos
# (id, hoja, Ybase, Ytope, referencia (y_dibujo, Y real), cx, espejo, escala_x_afín)
TRAMOS = [
    dict(id="T1", hoja="9",  y0=18000, y1=20000, ref=(134690, 18000), cx=3995.0, espejo=False, comp="comun"),
    dict(id="T2", hoja="11", y0=11250, y1=18000, ref=(107739, 18000), cx=6529.0, espejo=True, comp="comun"),
    dict(id="T3", hoja="12", y0=9000,  y1=11250, ref=(95081, 9000),   cx=5649.0, espejo=False, comp="comun"),
    dict(id="T4", hoja="14", y0=2250,  y1=9000,  ref=(87349, 9000),   cx=6284.0, espejo=True, panel=True, comp="comun"),
    dict(id="T5", hoja="15", y0=0,     y1=2250,  ref=(56909, 2250),   cx=6660.5, espejo=False, comp="comun"),
    dict(id="T6", hoja="17", y0=-6750, y1=0,     ref=(43595, 0),      cx=6940.0, espejo=True, panel=True, comp="comun"),
    dict(id="T7", hoja="18", y0=-9000, y1=-6750, ref=(32815, -6750),  cx=8569.5, espejo=False, comp="comun"),
    dict(id="T8", hoja="20", y0=-14700, y1=-9000, ref=(8802, -9000),  cx=6940.0, espejo=True, panel=True, comp="comun"),
    dict(id="C1", hoja="21", y0=-17700, y1=-14700, ref=(96480, -14700), cx=10803.0, espejo=False, comp="b1", nombre="Cuerpo AAE1"),
    dict(id="E2", hoja="23", y0=-23700, y1=-17700, ref=(69659, -17700), cx=10277.0, espejo=True, panel=True, comp="e2", nombre="Extensión AAE2"),
    dict(id="C2", hoja="24", y0=-27700, y1=-23700, ref=(57671, -23700), cx=13585.0, espejo=True, comp="b2", nombre="Cuerpo AAE2"),
    dict(id="C3", hoja="28", y0=-37700, y1=-31700, ref=(-2324, -31700), cx=14329.0, espejo=True, comp="b3", nombre="Cuerpo AAE3"),
    dict(id="C4", hoja="33", y0=-47700, y1=-41700, ref=(-246449, -41700), cx=-114774.0, espejo=True, comp="b4", nombre="Cuerpo AAE4"),
    dict(id="C5", hoja="38", y0=-57700, y1=-51700, ref=(-76430, -51700), cx=16475.0, espejo=True, comp="b5", nombre="Cuerpo AAE5"),
]
# extensiones sin plano interpretado (H26/H27, H31/H32): solo montantes
EXT_SOLO_MONTANTES = [("E3", -31700, -27700, "e3", "Extensión AAE3"), ("E4", -41700, -37700, "e4", "Extensión AAE4"), ("E5", -51700, -47700, "e5", "Extensión AAE5")]
GRUPOS = {"Plataformas": "#d9a441", "Cuerpo común": "#3b82c4", "Arriostramiento horizontal": "#20b2aa", "Viga y brazos": "#d04b5b", "Mástiles (delta)": "#8e5bd0", "Péndulo": "#2e9e6b"}
def p3(face, u, Y, d): return {"A": (u, Y, d), "B": (d, Y, -u), "C": (-u, Y, -d), "D": (-d, Y, u)}[face]
CARAS = ["A", "B", "C", "D"]

def swap_zd(m):
    if re.search(r"Z", m[3:] if m[:3].isdigit() else m): return m.replace("Z", "D", 1)
    if re.search(r"D", m[3:] if m[:3].isdigit() else m): return m.replace("D", "Z", 1)
    return m

def clipY(a, b, Y0, Y1):
    (u1, y1), (u2, y2) = a, b
    if abs(y1 - y2) < 1e-9: return (a, b) if Y0 - 1 <= y1 <= Y1 + 1 else None
    t1, t2 = (Y0 - y1) / (y2 - y1), (Y1 - y1) / (y2 - y1)
    t0, t3 = max(0.0, min(t1, t2)), min(1.0, max(t1, t2))
    if t0 >= t3 - 1e-9: return None
    return ((u1 + t0 * (u2 - u1), y1 + t0 * (y2 - y1)), (u1 + t3 * (u2 - u1), y1 + t3 * (y2 - y1)))

CATALOGO = {}
def datos_marca(arch, marca):
    k = f"{arch}|{marca}"
    if k not in CATALOGO:
        filas = BOMKEY.get((arch, marca)) or []
        f = filas[0] if filas else {}
        CATALOGO[k] = {"marca": marca, "hoja": int(re.match(r"\d+", arch).group()), "clase": f.get("clase"), "perfil": f.get("perfil"), "long_mm": f.get("long_mm"),
                       "cant": f.get("cant"), "acero": f.get("acero"), "conjunto": f.get("conjunto"), "en_lista": bool(filas)}
    return k

def coherencia(keys, eje_mm):
    L = []
    for k in keys:
        c = CATALOGO[k]
        if c["clase"] == "PERFIL" and str(c["long_mm"]).isdigit(): L.append(int(c["long_mm"]))
    if not L: return "n/a", "Marca(s) sin perfil con longitud numérica (lámina o no listada)."
    for r in (1, 2, 3):
        for comb in itertools.combinations(L, r):
            if abs(sum(comb) - eje_mm) <= max(250, 0.04 * eje_mm):
                return "ok", ("Longitud de pieza " + " + ".join(map(str, comb)) + f" mm ≈ eje del modelo {eje_mm} mm.")
    return "revisar", f"Ninguna longitud de pieza ({', '.join(map(str, L))} mm) coincide con el eje del modelo ({eje_mm} mm): vínculo por revisar."

miembros, vistos, cont = [], set(), {}
PANELES = {}
VALID, RESUMEN = [], []

CUR = ["comun"]
def agregar(tramo, a3, b3, marcas_keys, fuente, tipo, grupo, cara, nota=None, hoja_txt=None):
    k = tuple(sorted([tuple(round(v / 3) for v in a3), tuple(round(v / 3) for v in b3)]))
    if k in vistos: return None
    vistos.add(k)
    key = (grupo, tramo, cara, tipo); cont[key] = cont.get(key, 0) + 1; n = cont[key]
    L = round(math.dist(a3, b3))
    m = {"id": f"AAE-{tramo}-{cara}-{tipo[0]}{n:03d}", "nombre": f"{tipo} {n:02d} · {tramo} · Cara {cara}", "grupo": grupo, "tramo": tramo, "tipo": tipo, "cara": cara,
         "a": [round(v) for v in a3], "b": [round(v) for v in b3], "longitud_mm": L,
         "dx_mm": round(b3[0] - a3[0]), "dy_mm": round(b3[1] - a3[1]), "dz_mm": round(b3[2] - a3[2]), "fuente": fuente,
         "cara_tipo": "transversal" if cara in "AC" else "longitudinal", "comp": CUR[0]}
    if marcas_keys:
        m["marcas"] = marcas_keys; m["marca"] = " / ".join(CATALOGO[x]["marca"] for x in marcas_keys)
        m["coherencia"], m["coherencia_txt"] = coherencia(marcas_keys, L)
        m["nombre"] = f"{m['marca']} · {tipo} {n:02d} · {tramo}" + ("" if cara == "planta" else f" · Cara {cara}")
        m["verificacion"] = "Marca leída del plano (burbuja → leader → eje a ≤130 mm); la misma marca se asigna por simetría a las demás caras del tramo (los planos dibujan una cara)."
    if nota: m["nota"] = nota
    miembros.append(m)
    return m

def tipo_de(a, b, esleg):
    du, dY = abs(b[0] - a[0]), abs(b[1] - a[1])
    if esleg: return "Montante"
    if dY < 1: return "Horizontal"
    return "Diagonal"

for T in TRAMOS:
    arch = T["hoja"]; CUR[0] = T["comp"]
    ax, mk = marcas_hoja(arch)
    # cluster de la vista de cara = el que contiene el punto de referencia
    cs = clusters(ax)
    cx, (yr, Yr) = T["cx"], T["ref"]
    def bb(c):
        xs = [p[0] for i in c for p in ax[i]]; ys = [p[1] for i in c for p in ax[i]]
        return min(xs), max(xs), min(ys), max(ys)
    cand = [c for c in cs if bb(c)[0] - 5 <= cx <= bb(c)[1] + 5 and bb(c)[2] - 5 <= yr <= bb(c)[3] + 5]
    keep = set(max(cand, key=len))
    toY = lambda y: Yr + (y - yr) / FC
    shift = 0.0
    if T.get("panel"):
        # el dibujo es el panel lateral apoyado en el montante: semiancho dibujado = distancia del ápice de la diagonal al montante (a la cota de referencia)
        legs = [i for i in keep if math.dist(*ax[i]) > 1500 and 0.12 < abs(ax[i][1][0] - ax[i][0][0]) / max(abs(ax[i][1][1] - ax[i][0][1]), 1) < 0.15]
        li = max(legs, key=lambda i: math.dist(*ax[i]))
        (x1_, y1_), (x2_, y2_) = ax[li]
        xl = x1_ + (yr - y1_) * (x2_ - x1_) / (y2_ - y1_)
        shift = hw(Yr) - abs(cx - xl)       # se desplaza hacia afuera hasta el montante real; queda un hueco central entre paneles
        PANELES[T["id"]] = {"hoja": int(arch), "semiancho_dibujado_mm": round(abs(cx - xl)), "semiancho_real_mm": round(hw(Yr)), "hueco_central_mm": round(2 * (hw(Yr) - abs(cx - xl)) - 0)}
    def tr(p):
        return (p[0] - cx - shift, toY(p[1]))
    for i in sorted(keep):
        a, b = ax[i]
        if math.dist(a, b) < 14: continue
        pa, pb = tr(a), tr(b)
        r = clipY(pa, pb, T["y0"], T["y1"])
        if r is None: continue
        pa, pb = r
        if math.dist(pa, pb) < 14: continue
        # montante: ambos extremos sobre la arista del tronco (±hw) a ≤120 mm
        esleg = False
        for sg in (-1, 1):
            if all(abs(p[0] - sg * hw(p[1])) < 120 for p in (pa, pb)) and abs(pb[1] - pa[1]) > 300:
                pa, pb = (sg * hw(pa[1]), pa[1]), (sg * hw(pb[1]), pb[1]); esleg = True
        marcas = mk.get(i, [])
        variantes = [(pa, pb, marcas)]
        if T["espejo"]:
            variantes.append(((-pa[0], pa[1]), (-pb[0], pb[1]), [swap_zd(m) for m in marcas]))
        for (qa, qb, mks) in variantes:
            mkeys = list(dict.fromkeys(datos_marca(arch, m) for m in mks if (arch, m) in BOMKEY))
            for cara in CARAS:
                A = p3(cara, qa[0], qa[1], hw(qa[1])); B = p3(cara, qb[0], qb[1], hw(qb[1]))
                agregar(T["id"], A, B, mkeys, f"plano H{arch} (cara del tramo)", tipo_de(qa, qb, esleg), "Cuerpo común", cara)
    RESUMEN.append((T["id"], arch, len(keep)))
print("tramos:", RESUMEN)

# ---------------------------------------------------------------- secciones en planta (cortes F-F, H-H, J-J)
PLANTAS = [("10", 18000, False, "F-F", "T1/T2", "comun"), ("13", 11250, False, "H-H", "T2/T3", "comun"), ("16", 2250, True, "J-J", "T4/T5", "comun"),
           ("19", -6750, True, "M-M", "T6/T7", "comun"), ("22", -14700, True, "P-P", "T8/cuerpo AAE1", "comun"), ("25", -23700, True, "U-U", "extensión AAE2/cuerpo AAE2", "b2"),
           ("30", -31700, True, "W-W", "extensión AAE3/cuerpo AAE3", "b3"), ("35", -41700, True, "Y-Y", "extensión AAE4/cuerpo AAE4", "b4"),
           ("40", -51700, True, "Y'-Y'", "extensión AAE5/cuerpo AAE5", "b5")]
GRUPOS_ORDEN = []
SECC = []
for arch, Yp, media, nom, junta, comp_ in PLANTAS:
    CUR[0] = comp_
    ax, mk = marcas_hoja(arch)
    cs = sorted(clusters(ax), key=len, reverse=True)
    c = cs[0]
    xs = [p[0] for i in c for p in ax[i]]; ys = [p[1] for i in c for p in ax[i]]
    W = max(xs) - min(xs); xc = (max(xs) + min(xs)) / 2
    yc = (min(ys) + W / 2) if media else (max(ys) + min(ys)) / 2
    esc = 2 * hw(Yp) / W
    base = []
    for i in c:
        a_, b_ = ax[i]
        if media:
            r = clipY((a_[0], a_[1]), (b_[0], b_[1]), -1e9, yc)
            if r is None: continue
            a_, b_ = r
        if math.dist(a_, b_) < 14: continue
        base.append((a_, b_, mk.get(i, []), False))
        if media: base.append(((a_[0], 2 * yc - a_[1]), (b_[0], 2 * yc - b_[1]), [swap_zd(m) for m in mk.get(i, [])], True))
    n0 = 0
    for a_, b_, mks, esp in base:
        pa = ((a_[0] - xc) * esc, Yp, (a_[1] - yc) * esc); pb = ((b_[0] - xc) * esc, Yp, (b_[1] - yc) * esc)
        mkeys = list(dict.fromkeys(datos_marca(arch, m) for m in mks if (arch, m) in BOMKEY))
        dx, dz = pb[0] - pa[0], pb[2] - pa[2]
        tp = "Horizontal" if abs(dx) < 1 or abs(dz) < 1 else "Diagonal"
        m = agregar(f"Sec. {nom}", pa, pb, mkeys, f"plano H{arch} (corte {nom})" + (" · espejado" if esp else ""), tp, "Arriostramiento horizontal", "planta")
    SECC.append({"seccion": nom, "hoja": int(arch), "cota_mm": Yp, "junta": junta, "ancho_plano_mm": round(W), "ancho_modelo_mm": round(2 * hw(Yp)), "media_planta": media, "comp": comp_})
print("secciones:", SECC)


# ---------------------------------------------------------------- plataformas de descanso (H54..H60) y barandas (H61)
# Cada hoja dibuja la planta del corte con DOS plataformas (D y Z) en la misma esquina del tronco: el bloque PLAT-xx insertado normal (D) y reflejado
# respecto a la diagonal (Z). Se registra por la esquina del tronco (intersección del eje de cara inferior y del eje de cara derecho de la hoja).
PLATAFORMAS = [("54_A4", 19877, "E-E", "comun"), ("55_A5", 2250, "J-J", "comun"), ("56_A6", -14700, "P-P", "comun"), ("57_A4", -23700, "U-U", "b2"),
               ("58_A6", -31700, "W-W", "b3"), ("59_A6", -41700, "Y-Y", "b4"), ("60_A6", -51700, "Y'-Y'", "b5")]
H_BAR, H_REJ = 1100.0, 60.0
from ezdxf.math import Vec3
def en_ventana(x, y): return 0 < x < 6600 and 2500 < y < 8500
PLAT_META = []
BARAS_ARCH = "61_A1"
def marcas_con(arch, nombre, clases=("PERFIL",), lam=None):
    out = []
    for (a, pos), filas in BOMKEY.items():
        if a != arch: continue
        for f in filas:
            toks = f["conjunto"].split("-")
            if nombre in toks and f["clase"] in (clases if lam is None else ("LÁMINA",)):
                if lam is not None and not re.search(r"x (%s)$" % lam, f["perfil"]): continue
                if pos not in out: out.append(pos)
    return out
for arch, Yp, nom, comp_ in PLATAFORMAS:
    CUR[0] = comp_
    ms = doc(arch); dd = ms.doc
    ejes = [(e.dxf.start, e.dxf.end) for e in ms if e.dxftype() == "LINE" and e.dxf.layer == "EJES" and en_ventana(e.dxf.start.x, e.dxf.start.y)]
    Hs = [l for l in ejes if abs(l[0].y - l[1].y) < 2 and math.dist((l[0].x, l[0].y), (l[1].x, l[1].y)) > 1000]
    Vs = [l for l in ejes if abs(l[0].x - l[1].x) < 2 and math.dist((l[0].x, l[0].y), (l[1].x, l[1].y)) > 1000]
    cy = min(l[0].y for l in Hs); cxx = max(l[0].x for l in Vs)
    dX, dZ = hw(Yp) - cxx, -hw(Yp) - cy
    T3 = lambda x, y: (x + dX, Yp, y + dZ)
    labels = [(limpiar(e.dxf.text), e.dxf.insert.x, e.dxf.insert.y) for e in ms if e.dxftype() == "TEXT" and e.dxf.layer == "TEXT" and en_ventana(e.dxf.insert.x, e.dxf.insert.y)]
    vlab = [(t[5:].strip(), x, y) for t, x, y in labels if t.startswith("VIGA ")]
    blab = [(t[8:].strip(), x, y) for t, x, y in labels if t.startswith("BARANDA ")]
    mk_rej = [({a.dxf.tag: a.dxf.text.strip() for a in e.attribs}, e.dxf.insert.x, e.dxf.insert.y) for e in ms if e.dxftype() == "INSERT" and e.dxf.name == "MK" and en_ventana(e.dxf.insert.x, e.dxf.insert.y)]
    nb = ng = nr = 0
    for ins in [e for e in ms if e.dxftype() == "INSERT" and e.dxf.name.startswith("PLAT-")]:
        M = ins.matrix44()
        W = lambda x, y: (lambda v: (v.x, v.y))(M.transform(Vec3(x, y, 0)))
        blk = dd.blocks.get(ins.dxf.name)
        loc = [(l.dxf.start.x, l.dxf.start.y, l.dxf.end.x, l.dxf.end.y) for l in blk if l.dxftype() == "LINE" and l.dxf.layer == "EJES"]
        loc = [l for l in loc if math.dist(l[:2], l[2:]) > 300]
        for l in loc:                                              # vigas de la plataforma
            a_, b_ = W(l[0], l[1]), W(l[2], l[3]); mid = ((a_[0] + b_[0]) / 2, (a_[1] + b_[1]) / 2)
            lab = min(vlab, key=lambda t: seg_dist((t[1], t[2]), a_, b_), default=None)
            nombre = lab[0] if lab and seg_dist((lab[1], lab[2]), a_, b_) < 450 else None
            mkeys = [datos_marca(arch, m) for m in (marcas_con(arch, nombre) if nombre else [])]
            m_ = agregar(f"Plat. {nom}", T3(*a_), T3(*b_), mkeys, f"plano H{arch.split('_')[0]} (planta de plataforma {nom})" + (f", viga {nombre}" if nombre else ""), "Viga", "Plataformas", "planta")
            if m_: nb += 1
        # rejillas: una por vano entre vigas E3 consecutivas; el último vano incluye el triángulo hasta la diagonal (rejilla trapezoidal a 45°)
        hz = sorted([l for l in loc if abs(l[1] - l[3]) < 1], key=lambda l: l[1]); vt = sorted([l for l in loc if abs(l[0] - l[2]) < 1], key=lambda l: l[0])
        dg = [l for l in loc if abs(l[0] - l[2]) >= 1 and abs(l[1] - l[3]) >= 1]
        if len(hz) >= 2 and len(vt) >= 2:
            y1, y2 = hz[0][1], hz[-1][1]; xs_ = [l[0] for l in vt]
            for i in range(len(xs_) - 1):
                ult = i == len(xs_) - 2
                if ult and dg:
                    xe = max(max(l[0], l[2]) for l in dg)
                    fin_ = xe - xs_[i]
                else: fin_ = xs_[i + 1] - xs_[i]
                cx_, cy_ = W(xs_[i] + fin_ / 2, (y1 + y2) / 2)
                ml = min(mk_rej, key=lambda t: math.hypot(t[1] - cx_, t[2] - cy_), default=None)
                pos = ml[0].get("POSICION", "") if ml else ""
                fl = BOMKEY.get((arch, pos), [{}])[0] if pos else {}
                ancho = float(fl["perfil"].split(" x ")[-1]) if fl.get("perfil") and " x " in fl["perfil"] and fl["perfil"].split(" x ")[-1].replace('.', '').isdigit() else 830.0
                lon = float(fl["long_mm"]) if str(fl.get("long_mm", "")).isdigit() else fin_ + 95
                ov = (lon - (xs_[i + 1] - xs_[i])) / 2 if not ult else (lon - fin_) / 2 + 0
                ox = xs_[i] - 47.5; yy0 = y1 - (ancho - (y2 - y1)) / 2; yy1 = yy0 + ancho
                if ult and dg: pts = [(ox, yy0), (ox + lon, yy0), (ox + lon - ancho, yy1), (ox, yy1)]
                else: pts = [(ox, yy0), (ox + lon, yy0), (ox + lon, yy1), (ox, yy1)]
                mk_ = [datos_marca(arch, pos)] if pos and (arch, pos) in BOMKEY else []
                for j in range(4):
                    a_, b_ = W(*pts[j]), W(*pts[(j + 1) % 4])
                    A, B = T3(*a_), T3(*b_); A = (A[0], Yp + H_REJ, A[2]); B = (B[0], Yp + H_REJ, B[2])
                    if agregar(f"Plat. {nom}", A, B, mk_, f"plano H{arch.split('_')[0]} (rejilla {ml[0].get('MARCA', '') if ml else ''})", "Rejilla", "Plataformas", "planta"): ng += 1
    # barandas: las dos polilíneas BARANDA de la hoja (trayectoria de B1-B3-B2 de cada plataforma)
    for pl in [e for e in ms if e.dxftype() == "LWPOLYLINE" and e.dxf.layer == "BARANDA"]:
        pts = [(q[0], q[1]) for q in pl.get_points("xy")]
        if not en_ventana(*pts[0]): continue
        for i in range(len(pts) - 1):
            a_, b_ = pts[i], pts[i + 1]; L = math.dist(a_, b_)
            lab = min(blab, key=lambda t: seg_dist((t[1], t[2]), a_, b_), default=None)
            nombre = lab[0] if lab and seg_dist((lab[1], lab[2]), a_, b_) < 600 else None
            mpf = [datos_marca(BARAS_ARCH, m) for m in marcas_con(BARAS_ARCH, nombre)] if nombre else []
            mpl50 = [datos_marca(BARAS_ARCH, m) for m in marcas_con(BARAS_ARCH, nombre, lam=r"50")] if nombre else []
            mpl100 = [datos_marca(BARAS_ARCH, m) for m in marcas_con(BARAS_ARCH, nombre, lam=r"100")] if nombre else []
            A, B = T3(*a_), T3(*b_)
            for yoff, mk_, ft in ((H_BAR, mpf, "pasamanos"), (H_BAR / 2, mpl50, "travesaño intermedio"), (60.0, mpl100, "rodapié")):
                if agregar(f"Plat. {nom}", (A[0], Yp + yoff, A[2]), (B[0], Yp + yoff, B[2]), mk_, f"plano H61 (baranda {nombre or '?'}, {ft})", "Baranda", "Plataformas", "planta"): nr += 1
            npost = math.ceil(L / 950) + 1
            for j in range(npost):
                t = j / (npost - 1); P = (A[0] + t * (B[0] - A[0]), A[2] + t * (B[2] - A[2]))
                pm = [datos_marca(BARAS_ARCH, "3200")] if (BARAS_ARCH, "3200") in BOMKEY else []
                if agregar(f"Plat. {nom}", (P[0], Yp, P[1]), (P[0], Yp + 1127, P[1]), pm, f"plano H61 (poste de baranda {nombre or '?'})", "Poste", "Plataformas", "planta"): nr += 1
    PLAT_META.append({"corte": nom, "hoja": int(arch.split("_")[0]), "cota_mm": Yp, "esquina_plano": [round(cxx), round(cy)], "vigas": nb, "aristas_rejilla": ng, "baranda_miembros": nr, "comp": comp_})
    print("plataforma", nom, "vigas", nb, "rejilla", ng, "baranda", nr, "esquina", round(cxx), round(cy))

# ---------------------------------------------------------------- extensiones sin plano interpretado: solo montantes
for eid, Ya, Yb, comp_, nom in EXT_SOLO_MONTANTES:
    CUR[0] = comp_
    for cara in CARAS:
        for sg in (-1, 1):
            agregar(eid, p3(cara, sg * hw(Ya), Ya, hw(Ya)), p3(cara, sg * hw(Yb), Yb, hw(Yb)), [], "ejes de montantes por los anchos H= de los planos (el arriostramiento de la extensión no se interpretó)",
                    "Montante", "Cuerpo común", cara, nota="Solo se modela el montante; el arriostramiento de esta extensión no está ubicado (H26/H27, H31/H32).")

# ---------------------------------------------------------------- delta inferior (H6): dos mástiles triangulares en V sobre el tope del cuerpo
# H6 es la elevación (x transversal, y vertical, escala 1:1). Los nudos de los montantes exteriores bajan a y=938 con separación 4500 mm,
# igual al ancho del tramo 1 en su tope (Y=20000): y=938 <-> Y=20000. El eje de simetría vertical (x=6854.8) y el eje horizontal y=7875
# son líneas de referencia (sin marca): no se modelan.
CUR[0] = "comun"
DY0, DX0 = 938.5, 6854.8
def hw_delta(Y): return max(HW0 + S * (18000 - Y), 300.0)           # el tronco sigue la misma pendiente por encima del cuerpo
ax, mk = marcas_hoja("6")
cs = sorted(clusters(ax), key=len, reverse=True)
n_del = 0
for i in cs[0]:
    a_, b_ = ax[i]
    if math.dist(a_, b_) < 14: continue
    if abs(a_[0] - DX0) < 1 and abs(b_[0] - DX0) < 1: continue
    if abs(a_[1] - 7875.3) < 1 and abs(b_[1] - 7875.3) < 1: continue
    pa = (a_[0] - DX0, 20000 + a_[1] - DY0); pb = (b_[0] - DX0, 20000 + b_[1] - DY0)
    mkeys = list(dict.fromkeys(datos_marca("6", m) for m in mk.get(i, []) if ("6", m) in BOMKEY))
    for sg in (1, -1):
        A = (pa[0], pa[1], sg * hw_delta(pa[1])); B = (pb[0], pb[1], sg * hw_delta(pb[1]))
        esleg = math.degrees(math.atan2(abs(pb[1] - pa[1]), abs(pb[0] - pa[0]))) > 60 and math.dist(a_, b_) > 2000
        tp = "Montante" if esleg else ("Horizontal" if abs(pb[1] - pa[1]) < 1 else "Diagonal")
        if agregar("Delta", A, B, mkeys, "plano H6 (delta inferior, elevación)", tp, "Mástiles (delta)", "A" if sg > 0 else "C"): n_del += 1
print("delta:", n_del)

# ---------------------------------------------------------------- viga (H1): 3 vistas -> caja reticulada
VX0, VYB, VZ0 = 9385.3, 19053.0, 750.0                  # centro, cordón inferior en elevación, semiancho (1500 mm entre cordones)
YV = 20000 + (10071 - 938.5)                             # cota del cordón inferior: cabeza de los mástiles (y=10071 en H6)
ax, mk = marcas_hoja("1")
def depth_top(X): return 1000.0 + 535.0 * min(abs(X) / 8440.0, 1.0)      # canto de la viga: 1000 al centro, 1535 en los extremos (vista de elevación)
nv = 0
for i, (a_, b_) in enumerate(ax):
    if math.dist(a_, b_) < 14: continue
    yy = (a_[1] + b_[1]) / 2; xs = (a_[0], b_[0])
    if min(xs) < 500 or max(xs) > 18300: continue
    mkeys = list(dict.fromkeys(datos_marca("1", m) for m in mk.get(i, []) if ("1", m) in BOMKEY))
    if 18700 <= min(a_[1], b_[1]) and max(a_[1], b_[1]) <= 20700:              # elevación: dos caras z = ±750
        for sg in (1, -1):
            P = [(p[0] - VX0, YV + (p[1] - VYB), sg * VZ0) for p in (a_, b_)]
            tp = "Horizontal" if abs(P[0][1] - P[1][1]) < 1 else "Diagonal"
            if agregar("Viga", P[0], P[1], mkeys, "plano H1 (viga, elevación)", tp, "Viga y brazos", "A" if sg > 0 else "C"): nv += 1
    elif 21900 <= min(a_[1], b_[1]) and max(a_[1], b_[1]) <= 23800:            # planta superior
        P = [(p[0] - VX0, YV + depth_top(p[0] - VX0), p[1] - 22858.3) for p in (a_, b_)]
        tp = "Horizontal" if abs(P[0][2] - P[1][2]) < 1 or abs(P[0][0] - P[1][0]) < 1 else "Diagonal"
        if agregar("Viga", P[0], P[1], mkeys, "plano H1 (viga, planta superior)", tp, "Viga y brazos", "planta"): nv += 1
    elif 15800 <= min(a_[1], b_[1]) and max(a_[1], b_[1]) <= 17600:            # planta inferior
        P = [(p[0] - VX0, YV, p[1] - 16687.0) for p in (a_, b_)]
        tp = "Horizontal" if abs(P[0][2] - P[1][2]) < 1 or abs(P[0][0] - P[1][0]) < 1 else "Diagonal"
        if agregar("Viga", P[0], P[1], mkeys, "plano H1 (viga, planta inferior)", tp, "Viga y brazos", "planta"): nv += 1
print("viga:", nv)

SUPUESTOS = [
    "Cota de trabajo: Y=0 en la base del cuerpo AAEk. El cuerpo común (T1…T8) y el cuerpo/extensiones de cada variante se ensamblan con los anchos H= de los planos: T1 4500→5054,29 · T2 →6925,03 · T3 →7548,61 · T4 →9419,35 · T5 →10042,93 · T6 →11883,38 · T7 →12506,96 · T8 →14086,70 · cuerpo AAE1 (3000 mm) →14918,14 · extensión AAE2 (6000) →16581,01 · cuerpo AAE2 (4000) →17689,60 · extensión AAE3 (4000) →18798,19 · cuerpo AAE3 (6000) →20461,06 · extensión AAE4 (4000) →21569,64 · cuerpo AAE4 (6000) →23232,52. Las alturas de cada tramo salen de Δancho / 0,27715 (pendiente de los planos) y coinciden con las alturas de los dibujos.",
    "Cada AAEk incluye todas las piezas de las variantes menores (AAE2 = AAE1 + extensión 6 m + cuerpo 4 m, etc.), deducido de que el ancho superior de cada extensión coincide con el ancho inferior del cuerpo anterior; confirmar con la tabla de alturas de la torre.",
    "Planta cuadrada con simetría de 4 caras: cada plano dibuja una cara en verdadera magnitud (factor 'EN CARA' 1,009556) y se proyecta a las cuatro caras; las marcas se asignan a las cuatro caras (los planos no distinguen cara transversal de longitudinal).",
    "T2, T4, T6, T8, extensión AAE2 y cuerpos AAE2–AAE4 se dibujan como media cara (o un panel lateral): se espeja respecto al eje de simetría y el sufijo de marca Z ↔ D se intercambia. En los dibujos tipo panel (T4, T6, T8, extensión AAE2) el panel se apoya en el montante real y queda un hueco central (ver 'paneles_laterales'); el hueco es consecuencia de lo dibujado, no una cota del plano.",
    "Tramo 6 (H17): el cuadro de funciones del plano da otra pendiente (0,106) que no coincide con las alturas ni con las longitudes de lista; se usó la cadena de anchos H= (10042,93 → 11883,38 en 6750 mm), con ≈ 30 mm de diferencia frente a la pendiente estándar.",
    "Cortes en planta F-F, H-H, J-J, M-M, P-P, U-U, W-W y Y-Y (media planta espejada en J-J y siguientes): ubicados en la unión de tramos cuyo ancho coincide con el de la planta; se escalan ≤0,5 % para coincidir con el ancho del tronco. Los cortes giradas G-G, L-L, N7, O-O, V-V, S-S, R1-R4, Q-Q, X-X, Z-Z y las vistas internas (H27, H32) no se modelaron.",
    "Delta inferior (H6): H6 es la elevación (x transversal, y vertical, 1:1). Los montantes exteriores arrancan en el tope del cuerpo (separación 4500 = ancho del tope de T1) y suben formando una V. La profundidad longitudinal del delta (z) se supone igual al semiancho del tronco prolongado con su misma pendiente; la vista longitudinal H8 NO se utilizó, por lo que las caras longitudinales del delta no están modeladas.",
    "Viga (H1): vistas de elevación (dos caras a z = ±750 mm), planta superior (cordones a la cota del canto de la elevación, 1000 mm al centro → 1535 mm en los extremos) y planta inferior. Se apoya sobre la cabeza de los mástiles del delta inferior (y = 10071 en H6); la cota real de apoyo depende de H5 (delta superior), que no se ubicó.",
    "No ubicados en el 3D (sus piezas sí están en la lista de materiales): H2 brazo, H3 secciones, H4 platinas, H5 delta superior, H7 vista D inferior y péndulo, H8 vista longitudinal del delta, variantes (12b, 13b, 14b, 15b, 33b).",
    "Marcas: solo las que tienen burbuja + leader dentro de la tolerancia del eje del plano (≤ 130 mm). Los miembros sin marca no tienen vínculo verificado. 'coherencia' compara la longitud de pieza de la lista con el eje del modelo (típicamente pieza = eje + 50 mm de holgura); las piezas de empalme de 350–530 mm sobre montantes aparecen como 'revisar'.",
    "Secciones de barra esquemáticas; el perfil real es el de la lista de materiales. Los nombres de tramo, de cara (A–D) y de corte son convenciones del visor.",
]

SUPUESTOS_PLAT = [
    "Plataformas de descanso (H54–H60, cortes E-E, J-J, P-P, U-U, W-W, Y-Y, Y'-Y'): cada hoja dibuja la planta con dos plataformas (D y Z) en una esquina del tronco; el bloque de la plataforma D se refleja respecto a la diagonal para obtener la Z. Se registran por la esquina del tronco (intersección de los ejes de cara de la hoja) y se colocan siempre en la misma esquina (+X, −Z): los planos no indican a qué esquina de la torre real corresponden respecto a la cruceta ni a la escalera.",
    "Cota de las plataformas: la del corte en planta (se supone que el plano de corte pasa por el eje de las vigas MCP 100, por lo que la rejilla queda a +60 mm). El corte E-E no tiene cota escrita: se dedujo del ancho de su planta (4534 mm → Y = 19877) y es consistente con el marcador de corte E de la hoja H9 (≈ 20089 mm).",
    "Rejillas: una por vano entre vigas E3 consecutivas; el último vano incluye el triángulo hasta la diagonal (trapecio a 45°). Se modelan como contorno (4 aristas) con la longitud/ancho de la lista de materiales; la marca REJ se tomó del rótulo más cercano.",
    "Barandas (H61): se siguen las polilíneas BARANDA de la planta; pasamanos a +1100 mm, travesaño intermedio a +550 mm y rodapié a +60 mm (alturas supuestas: la hoja H61 acota 1100 y 900/445/420 pero no se interpretó la sección), postes de 1127 mm cada ≈950 mm. Las barandas B4/B5 solo existen desde el corte Y-Y y B6/B7 en Y'-Y'.",
    "Escalera de acceso (H62, H63) y parrillas pesada/liviana (H53, H52): solo en la lista de materiales; la lista de las parrillas se leyó de los bloques PARR del plano (cantidad = n.º de veces que se dibuja la pieza cuando el plano no da CANT).",
]

# ---------------------------------------------------------------- salida: AAE1..AAE4
COMPS = {5: ["comun", "b1", "e2", "b2", "e3", "b3", "e4", "b4", "e5", "b5"], 1: ["comun", "b1"], 2: ["comun", "b1", "e2", "b2"], 3: ["comun", "b1", "e2", "b2", "e3", "b3"], 4: ["comun", "b1", "e2", "b2", "e3", "b3", "e4", "b4"]}
for _k in COMPS: COMPS[_k] += ["patas", "escalera"]              # patas/parrillas y escalera de acceso: solo lista de materiales (sin ubicar)
YMIN = {5: -57700, 1: -17700, 2: -27700, 3: -37700, 4: -47700}
ALTO_ABAJO = {5: 43000, 1: 3000, 2: 13000, 3: 23000, 4: 33000}
VALID_BASE = [("H9", "ancho en el tope del cuerpo (H=4500)", 4500, 20000), ("H9", "ancho en la base del tramo 1 (H=5054,29)", 5054.29, 18000),
              ("H12", "ancho en la base del tramo 3 (H=7548,61)", 7548.61, 9000), ("H12", "ancho en el tope del tramo 3 (H=6925,03)", 6925.03, 11250),
              ("H15", "ancho en la base del tramo 5 (H=10042,93)", 10042.93, 0), ("H15", "ancho en el tope del tramo 5 (H=9419,35)", 9419.35, 2250),
              ("H18", "ancho en la base del tramo 7 (H=12506,96)", 12506.96, -9000), ("H18", "ancho en el tope del tramo 7 (H=11883,38)", 11883.38, -6750),
              ("H21", "ancho en la base del cuerpo AAE1 (H=14918,14)", 14918.14, -17700), ("H21", "ancho en el tope del cuerpo AAE1 (H=14086,7)", 14086.7, -14700),
              ("H24", "ancho en el tope del cuerpo AAE2 (H=16581,01)", 16581.01, -23700), ("H24", "ancho en la base del cuerpo AAE2 (H=17689,6)", 17689.6, -27700),
              ("H28", "ancho en el tope del cuerpo AAE3 (H=18798,19)", 18798.19, -31700), ("H28", "ancho en la base del cuerpo AAE3 (H=20461,06)", 20461.06, -37700),
              ("H33", "ancho en el tope del cuerpo AAE4 (H=21569,64)", 21569.64, -41700), ("H33", "ancho en la base del cuerpo AAE4 (H=23232,52)", 23232.52, -47700),
              ("H38", "ancho en el tope del cuerpo AAE5 (H=24341,11)", 24341.11, -51700), ("H38", "ancho en la base del cuerpo AAE5 (H=26003,99)", 26003.99, -57700)]
FUENTE_HOJAS = {5: "H1–H63", 1: "H1–H22 y H53–H63", 2: "H1–H25 y H53–H63", 3: "H1–H30 y H53–H63", 4: "H1–H35 y H53–H63"}
for k in (1, 2, 3, 4, 5):
    comps = set(COMPS[k]); Ym = YMIN[k]
    sel = []
    for m in miembros:
        if m["comp"] not in comps: continue
        m = json.loads(json.dumps(m))
        m["a"][1] -= Ym; m["b"][1] -= Ym
        m["id"] = m["id"].replace("AAE-", f"AAE{k}-", 1)
        sel.append(m)
    usadas = {x for m in sel for x in m.get("marcas", [])}
    cat = {x: CATALOGO[x] for x in CATALOGO if x in usadas}
    bom = [f for f in BOM if comp_fila(f) in comps]
    n_marc = sum(1 for m in sel if m.get("marca"))
    alto = max(max(m["a"][1], m["b"][1]) for m in sel)
    ext = max(max(abs(m["a"][0]), abs(m["b"][0])) for m in sel if m["grupo"] == "Viga y brazos")
    base_w = round(2 * hw(Ym))
    secc = [p for p in SECC if p["comp"] in comps]
    val = [{"hoja": h, "cota": c, "plano_mm": w, "modelo_mm": round(2 * hw(Y), 1)} for h, c, w, Y in VALID_BASE if Y >= Ym and not (Y < -14700 and h in ("H24", "H28", "H33", "H38") and k < {"H24": 2, "H28": 3, "H33": 4, "H38": 5}[h]) and not (h == "H21" and False)]
    val += [{"hoja": f"H{p['hoja']}", "cota": f"ancho de la planta del corte {p['seccion']}", "plano_mm": p["ancho_plano_mm"], "modelo_mm": p["ancho_modelo_mm"]} for p in secc]
    ncoh = collections.Counter(m.get("coherencia") for m in sel if m.get("marca") and m["cara"] in ("A", "planta"))
    nombre = f"Torre AAE{k}"
    data = {"meta": {
        "nombre": nombre, "linea": "GEB U113 CT101850",
        "fuente": f"Planos GEB-U113-CT101850-L000-DIS6012 ({FUENTE_HOJAS[k]}): viga, delta, cuerpo común tramos 1–8 y cuerpo AAE1" + ("" if k == 1 else ", extensiones y cuerpos AAE2–AAE%d" % k) + ".",
        "plano_base": "GEB-U113-CT101850-L000-DIS6012", "etapa": "4 (H1–H63; faltan brazos, delta superior, péndulo, patas/stub/parrillas, escalera de acceso y arriostramientos de extensiones AAE3–AAE5)",
        "alto_total_mm": round(alto), "alto_cuerpo_mm": 20000 + 14700 + ALTO_ABAJO[k], "ancho_base_mm": base_w, "largo_base_mm": base_w, "ancho_cruceta_mm": round(2 * ext),
        "marcas_vinculadas": n_marc, "miembros_total": len(sel), "secciones_planta": secc, "paneles_laterales": PANELES, "plataformas": [p for p in PLAT_META if p["comp"] in comps], "coherencia_marcas": dict(ncoh), "validacion": val,
        "supuestos": SUPUESTOS + ([] if k < 3 else ["Extensiones AAE3, AAE4 y AAE5 (4000 mm cada una): sus planos (H26/H27, H31/H32, H36/H37) son vistas desarrolladas giradas que no se interpretaron; solo se modelaron los montantes por los anchos H= de los planos vecinos."]) + SUPUESTOS_PLAT + ["Patas de 3,0 a 12,0 m (H41–H50), stub (H51) y parrilla liviana (H52): sus piezas están en la lista de materiales pero NO se ubicaron en el 3D. Las patas se dibujan como triángulos desarrollados (vistas giradas) cuya posición respecto al cuerpo no se pudo deducir de los planos; el modelo termina en la base del cuerpo."]},
        "grupos": [{"nombre": n, "color": c} for n, c in GRUPOS.items() if any(m["grupo"] == n for m in sel)],
        "marcas": cat, "miembros": sel, "lista_materiales": bom}
    json.dump(data, open(os.path.join(OUT, f"torre-aae{k}.json"), "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
    print(f"AAE{k}:", len(sel), "miembros |", n_marc, "marcas |", len(bom), "filas BOM | alto", round(alto), "| coherencia", dict(ncoh))
print("paneles:", PANELES)
