#!/usr/bin/env python3
"""
Genera wwwroot/modelos3d/torre-be-500kv.json (modelo + lista de materiales).

Entradas : carpeta con los DXF (dwg2dxf -y -o H10.dxf <plano>.dwg) nombrados H1.dxf … H41.dxf
Salida   : JSON del modelo.   Uso: python3 generar_torre_be.py <carpeta_dxf> <salida.json>

Método (todo verificable):
 1. Geometría global = esquema a escala de la hoja 19 (capa TORN), extruido a 4 caras.
    Escala 33,3 mm/unidad, validada contra cotas exactas de la hoja 10.
 2. Marcas: en las hojas con burbujas "pos" (H10–H29, H39–H41) cada burbuja se une por su
    LEADER a un eje M-EJES. Cada vista de eje se registra rígidamente sobre el esquema (votación
    de traslación + giro, tolerancia 0,8 u ≈ 27 mm). Solo se asigna marca a un miembro del esquema
    si el eje del plano lo cubre geométricamente; los ejes con marca que el esquema no trae se
    agregan como miembros con la geometría del propio plano.
 3. Perfil / longitud / cantidad / acero salen de la lista de materiales (bloques PERFILES y LAMINAS).
"""
import sys, os, json, math, re, collections
import numpy as np
import ezdxf
from scipy.spatial import cKDTree

DXFDIR, OUT = sys.argv[1], sys.argv[2]
ESCALA, VC = 33.3, 1.00927074
CX, Y0 = -1016628.13, -1741114.0
TOL = 0.8
NECK = 2697

def doc(n):
    ruta = os.path.join(DXFDIR, f"H{n}.dxf")
    try: return ezdxf.readfile(ruta).modelspace()
    except Exception:                      # DXF con capas/layouts que ezdxf no carga directo
        from ezdxf import recover
        return recover.readfile(ruta)[0].modelspace()

HOJA_TXT = {43: "43–45", 46: "46–47"}      # archivos que agrupan varias hojas

# ---------------------------------------------------------------- esquema (hoja 19)
ICON, seen = [], set()
for e in doc(19):
    if e.dxftype() == "LINE" and e.dxf.layer == "TORN":
        a = (round(e.dxf.start.x - CX, 2), round(e.dxf.start.y - Y0, 2))
        b = (round(e.dxf.end.x - CX, 2), round(e.dxf.end.y - Y0, 2))
        l = math.dist(a, b)
        if l < 14: continue
        if abs(a[0]) < .5 and abs(b[0]) < .5 and l > 300: continue
        k = tuple(sorted([a, b]))
        if k in seen: continue
        seen.add(k); ICON.append((a, b))
NODES = np.unique(np.round(np.array([p for l in ICON for p in l]), 1), axis=0)
TREE = cKDTree(NODES)

def env_at(y):
    m = 0.0
    for a, b in ICON:
        if abs(a[1] - b[1]) < 1e-6:
            if abs(a[1] - y) < 0.6: m = max(m, abs(a[0]), abs(b[0]))
        elif min(a[1], b[1]) - 1e-6 <= y <= max(a[1], b[1]) + 1e-6:
            t = (y - a[1]) / (b[1] - a[1]); m = max(m, abs(a[0] + t * (b[0] - a[0])))
    return m
ENV = {y: env_at(y) for y in range(0, 3222)}
def env(y):
    y0 = max(0, min(3221, y)); i = int(y0); f = y0 - i
    return ENV[i] * (1 - f) + ENV[min(3221, i + 1)] * f

ESC_VAL = [(y, w, w / (2 * env(y))) for y, w in ((2426, 7481.3), (2591, 5998.1))]

# ---------------------------------------------------------------- lista de materiales
BOM, BOMKEY = [], collections.defaultdict(list)
vistos_bom = set()
for n in range(1, 48):
    try: ms = doc(n)
    except Exception: continue
    conj = ""
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name == "CONJUNTO":
            a = {x.dxf.tag: x.dxf.text for x in e.attribs}
            if a.get("NOM") and not conj: conj = (a.get("NOM") + " " + (a.get("TIP") or "")).strip()
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name in ("PERFILES", "LAMINAS"):
            a = {x.dxf.tag: x.dxf.text.strip() for x in e.attribs}
            pos = a.get("POS", "")
            if not pos: continue
            fila = {"hoja": n, "hoja_txt": HOJA_TXT.get(n, str(n)), "clase": "PERFIL" if e.dxf.name == "PERFILES" else "LÁMINA",
                    "marca": pos, "perfil": a.get("DES", ""), "long_mm": a.get("LON", ""),
                    "cant": a.get("CAN", ""), "acero": a.get("CAL", ""),
                    "conjunto": a.get("CON") or conj, "obs": a.get("OBS", "")}
            k = tuple(fila.values())
            if k in vistos_bom: continue
            vistos_bom.add(k); BOM.append(fila); BOMKEY[(n, pos)].append(fila)

# ---------------------------------------------------------------- vínculo hoja -> eje -> marca
def seg_dist(p, a, b):
    ax_, ay = a; bx, by = b; px, py = p
    dx, dy = bx - ax_, by - ay; L2 = dx * dx + dy * dy
    t = 0 if L2 == 0 else max(0, min(1, ((px - ax_) * dx + (py - ay) * dy) / L2))
    return math.hypot(px - (ax_ + t * dx), py - (ay + t * dy))

def extraer(n):
    ms = doc(n)
    capas = ("EJES", "M-EJES") if n <= 9 else ("M-EJES",)       # las hojas 1–9 usan otro estilo de dibujo
    ax = [((e.dxf.start.x, e.dxf.start.y), (e.dxf.end.x, e.dxf.end.y)) for e in ms
          if e.dxftype() == "LINE" and e.dxf.layer in capas]
    bub = []
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name.lower() == "pos":
            pos = "".join(a.dxf.text for a in e.attribs if a.dxf.tag == "POS").strip()
            nota = next((a.dxf.text for a in e.attribs if a.dxf.tag == "NOTA"), "").strip()
            bub.append((e.dxf.insert.x, e.dxf.insert.y, pos + nota))
    marcas = collections.defaultdict(list)
    if ax and bub:
        for l in ms:
            if l.dxftype() != "LEADER": continue
            v = [(p[0], p[1]) for p in l.vertices]
            ends = [v[0], v[-1]]
            db = [min(math.hypot(e[0] - b[0], e[1] - b[1]) for b in bub) for e in ends]
            ti = 0 if db[0] > db[1] else 1
            tip, base = ends[ti], ends[1 - ti]
            b = min(bub, key=lambda b: math.hypot(b[0] - base[0], b[1] - base[1]))
            best = min(range(len(ax)), key=lambda i: seg_dist(tip, *ax[i]))
            if seg_dist(tip, *ax[best]) < 130 and b[2] and b[2] not in marcas[best]:
                marcas[best].append(b[2])
    return ax, marcas

def clusters(ax, gap=200):
    n = len(ax); par = list(range(n))
    def f(a):
        while par[a] != a: par[a] = par[par[a]]; a = par[a]
        return a
    for i in range(n):
        for j in range(i + 1, n):
            a, b = ax[i]; c, d = ax[j]
            if min(seg_dist(a, c, d), seg_dist(b, c, d), seg_dist(c, a, b), seg_dist(d, a, b)) < gap:
                par[f(i)] = f(j)
    g = {}
    for i in range(n): g.setdefault(f(i), []).append(i)
    return list(g.values())

def to_icon(P, th, mirror):
    c, s = math.cos(th), math.sin(th)
    x = P[:, 0] * (-1 if mirror else 1)
    return np.column_stack([(x * c - P[:, 1] * s) / ESCALA, (x * s + P[:, 1] * c) / (ESCALA * VC)])

def registrar(pts, step=0.5):
    pts = np.unique(np.round(np.array(pts, float), 0), axis=0)
    best = (0, None)
    for mirror in (False, True):
        for th in np.arange(-180, 180, step):
            Q = to_icon(pts, math.radians(th), mirror)
            T = (NODES[None, :, :] - Q[:, None, :]).reshape(-1, 2)
            k = np.round(T / TOL).astype(np.int64); keys = k[:, 0] * 100000 + k[:, 1]
            u, cnt = np.unique(keys, return_counts=True); i = cnt.argmax()
            if cnt[i] > best[0]:
                best = (cnt[i], dict(th=th, mirror=mirror, t=T[keys == u[i]].mean(0)))
    p = best[1]
    d, _ = TREE.query(to_icon(pts, math.radians(p["th"]), p["mirror"]) + p["t"])
    return int((d < TOL).sum()), len(pts), p

def xf(P, p): return to_icon(np.array(P, float).reshape(-1, 2), math.radians(p["th"]), p["mirror"]) + p["t"]

def limpiar(t): return re.sub(r"\\[A-Za-z0-9.;|]*|[{}]", "", t).replace("\\P", " ").strip()

def etiquetas_cara(n, maxd=450):
    """Marcas que el propio plano rotula como 'CARA TRANSVERSAL' (T) o 'CARA LONGITUDINAL' (L):
    cada rótulo de texto se asigna al leader más cercano (≤ 450 mm) y de ahí a la marca de su burbuja."""
    ms = doc(n); bub = []
    for e in ms:
        if e.dxftype() == "INSERT" and e.dxf.name == "pos":
            pos = "".join(a.dxf.text for a in e.attribs if a.dxf.tag == "POS").strip()
            nota = next((a.dxf.text for a in e.attribs if a.dxf.tag == "NOTA"), "").strip()
            bub.append((e.dxf.insert.x, e.dxf.insert.y, pos + nota))
    textos = []
    for e in ms:
        if e.dxftype() in ("TEXT", "MTEXT"):
            t = limpiar(e.dxf.text if e.dxftype() == "TEXT" else e.text).upper()
            if "CARA" in t and ("TRANSV" in t or "LONGIT" in t): textos.append((e.dxf.insert.x, e.dxf.insert.y, "T" if "TRANSV" in t else "L"))
    if not bub or not textos: return {}
    lead = []
    for l in ms:
        if l.dxftype() == "LEADER":
            v = [(q[0], q[1]) for q in l.vertices]
            db = [min(math.hypot(q[0] - b[0], q[1] - b[1]) for b in bub) for q in (v[0], v[-1])]
            base = v[0] if db[0] < db[1] else v[-1]
            b = min(bub, key=lambda b: math.hypot(b[0] - base[0], b[1] - base[1]))
            lead.append((v, b[2]))
    out = {}
    for x, y, t in textos:
        best = min(lead, key=lambda L: min(seg_dist((x, y), L[0][i], L[0][i + 1]) for i in range(len(L[0]) - 1)))
        d = min(seg_dist((x, y), best[0][i], best[0][i + 1]) for i in range(len(best[0]) - 1))
        if d < maxd and best[1]: out.setdefault(f"{n}|{best[1]}", set()).add(t)
    return {k: next(iter(v)) for k, v in out.items() if len(v) == 1}

TAG_CARA = {}
for _n in [10, 12, 13, 15, 16, 18, 19, 21, 22, 24, 25, 27, 28]:
    try: TAG_CARA.update(etiquetas_cara(_n))
    except Exception: pass
print("marcas con rótulo de cara:", TAG_CARA)

ICON_MARCAS = collections.defaultdict(list)     # idx linea esquema -> [(hoja, marca, parcial)]
EXTRA = []                                      # miembros agregados desde planos
REGS = []                                       # resumen de registros
for n in [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 39, 40, 41]:
    try: ax, marcas = extraer(n)
    except Exception: continue
    for c in sorted(clusters(ax), key=len, reverse=True):
        if len(c) < 5: continue
        todos = np.unique(np.round(np.array([q for i in c for q in ax[i]], float), 0), axis=0)
        muestra = todos if len(todos) <= 90 else todos[np.random.RandomState(1).choice(len(todos), 90, replace=False)]
        _, _, p = registrar(muestra)
        d, _ = TREE.query(to_icon(todos, math.radians(p["th"]), p["mirror"]) + p["t"])
        m, tot = int((d < TOL).sum()), len(todos)
        if n <= 9:
            # cabeza/brazos/delta: coincidencia parcial con el esquema, pero estadísticamente significativa
            # (≥ 6 nodos de >100 a 27 mm: probabilidad por azar < 1e-3) y dentro de la zona de cabeza/cuello
            yy = (to_icon(todos, math.radians(p["th"]), p["mirror"]) + p["t"])[:, 1]
            if m < 6 or yy.min() < 2560: continue
        elif m < 8 or m / tot < 0.35: continue
        pts = xf([q for i in c for q in ax[i]], p)
        REGS.append({"hoja": n, "nodos_coinciden": m, "nodos": tot, "y_min_mm": round(pts[:, 1].min() * ESCALA), "y_max_mm": round(pts[:, 1].max() * ESCALA)})
        for i in c:
            mk = marcas.get(i)
            if not mk: continue
            a, b = xf(ax[i], p)
            ln = [k for k, (u, v) in enumerate(ICON) if seg_dist(u, a, b) < TOL and seg_dist(v, a, b) < TOL]
            if ln:
                for k in ln:
                    for x in mk: ICON_MARCAS[k].append((n, x, False))
                continue
            sub = [k for k, (u, v) in enumerate(ICON) if seg_dist(a, u, v) < TOL and seg_dist(b, u, v) < TOL]
            if sub:
                for k in sub:
                    for x in mk: ICON_MARCAS[k].append((n, x, True))
            else:
                EXTRA.append(((tuple(a), tuple(b)), [(n, x, False) for x in mk]))
print("registros:", REGS)

# ---------------------------------------------------------------- 3D
BANDS = [(0, 966, "Extensión / Patas", "T1"), (966, 1217, "Extensión / Patas", "T2"),
         (1217, 1471, "Extensión / Patas", "T3"), (1471, 1700, "Extensión / Patas", "T4"),
         (1700, 1950, "Cuerpo común", "T5"), (1950, 2280, "Cuerpo común", "T6"),
         (2280, 2565, "Cuerpo común", "T7"), (2565, NECK, "Cuello", "T8"),
         (NECK, 3060, "Cabeza", "T9"), (3060, 3300, "Brazos", "T10")]
GRUPOS = {"Extensión / Patas": "#d9822b", "Cuerpo común": "#3b82c4", "Cuello": "#8e5bd0", "Cabeza": "#2e9e6b", "Brazos": "#d04b5b"}
def banda(ym):
    for b in BANDS:
        if b[0] <= ym < b[1]: return b
    return BANDS[-1]
def p3(face, x, y, d): return {"A": (x, y, d), "B": (d, y, -x), "C": (-x, y, -d), "D": (-d, y, x)}[face]

CATALOGO = {}
def datos_marca(hoja, marca, parcial):
    k = f"{hoja}|{marca}"
    if k not in CATALOGO:
        filas = BOMKEY.get((hoja, marca)) or []
        f = filas[0] if filas else {}
        CATALOGO[k] = {"marca": marca, "hoja": hoja, "clase": f.get("clase"), "perfil": f.get("perfil"), "long_mm": f.get("long_mm"),
                       "cant": f.get("cant"), "acero": f.get("acero"), "conjunto": f.get("conjunto"), "en_lista": bool(filas)}
    return k

def coherencia(keys, eje_mm):
    """Compara las longitudes de pieza de la lista con la longitud de eje del modelo."""
    L = []
    for k in keys:
        c = CATALOGO[k]
        if c["clase"] == "PERFIL" and str(c["long_mm"]).isdigit(): L.append(int(c["long_mm"]))
    if not L: return "n/a", "Marca(s) sin perfil con longitud numérica (lámina, accesorio o no listada)."
    import itertools
    for r in (1, 2, 3):
        for comb in itertools.combinations(L, r):
            if abs(sum(comb) - eje_mm) <= max(200, 0.03 * eje_mm):
                return "ok", ("Longitud de pieza " + " + ".join(map(str, comb)) + f" mm ≈ eje del modelo {eje_mm} mm.")
    return "revisar", f"Ninguna longitud de pieza ({', '.join(map(str, L))} mm) coincide con el eje del modelo ({eje_mm} mm): vínculo por revisar."

def lineas():
    for k, (a, b) in enumerate(ICON): yield a, b, ICON_MARCAS.get(k, []), "esquema H19"
    for (a, b), mk in EXTRA: yield a, b, mk, "plano H" + str(mk[0][0])

miembros, vistos, cont = [], set(), {}
for a, b, mk, fuente in lineas():
    ym = (a[1] + b[1]) / 2
    _, _, zona, tramo = banda(ym)
    cuerpo = max(a[1], b[1]) <= NECK + 1
    for f in (["A", "B", "C", "D"] if cuerpo else ["A", "C"]):
        dd = (lambda y: env(y)) if cuerpo else (lambda y: env(NECK))
        pa, pb = p3(f, a[0], a[1], dd(a[1])), p3(f, b[0], b[1], dd(b[1]))
        k = tuple(sorted([tuple(round(v, 1) for v in pa), tuple(round(v, 1) for v in pb)]))
        if k in vistos: continue
        vistos.add(k)
        dy = abs(a[1] - b[1]); dx = abs(a[0] - b[0])
        en_env = lambda q: abs(abs(q[0]) - env(q[1])) < 3
        tipo = "Montante" if (cuerpo and en_env(a) and en_env(b) and dy > 1) else "Horizontal" if dy < 1 else "Vertical" if dx < 1 else "Diagonal"
        key = (zona, tramo, f if tipo != "Montante" else "M", tipo)
        cont[key] = cont.get(key, 0) + 1; n = cont[key]
        tipo_cara = "T" if f in ("A", "C") else "L"          # convención del visor: A y C transversales, B y D longitudinales
        marcas = list(dict.fromkeys(datos_marca(h, x, par) for h, x, par in mk)) if mk else []
        marcas = [k for k in marcas if TAG_CARA.get(k) in (None, tipo_cara)]
        m = {"id": f"BE-{tramo}-{f}-{tipo[0]}{n:02d}",
             "nombre": f"{tipo} {n:02d} · {tramo} · Cara {f}" if tipo != "Montante" else f"Montante {n:02d} · {tramo}",
             "grupo": zona, "tramo": tramo, "tipo": tipo, "cara": f,
             "a": [round(v * ESCALA) for v in pa], "b": [round(v * ESCALA) for v in pb],
             "longitud_mm": round(math.dist(pa, pb) * ESCALA),
             "dx_mm": round((pb[0] - pa[0]) * ESCALA), "dy_mm": round((pb[1] - pa[1]) * ESCALA), "dz_mm": round((pb[2] - pa[2]) * ESCALA),
             "fuente": fuente, "cara_tipo": "transversal" if tipo_cara == "T" else "longitudinal"}
        if marcas:
            m["marcas"] = marcas
            m["marca"] = " / ".join(CATALOGO[x]["marca"] for x in marcas)
            m["coherencia"], m["coherencia_txt"] = coherencia(marcas, m["longitud_mm"])
            m["nombre"] = f"{m['marca']} · {tipo} {n:02d} · {tramo}" + ("" if tipo == "Montante" else f" · Cara {f}")
            m["verificacion"] = (f"Marca leída del plano H{CATALOGO[marcas[0]]['hoja']} (leader → eje → coincidencia con el esquema ≤ 27 mm), cara A del plano"
                                 + ("" if f == "A" else f"; en cara {f} asignada por simetría de 4 caras")
                                 + ("; el plano rotula esta marca como cara " + ("transversal" if TAG_CARA.get(marcas[0]) == "T" else "longitudinal") if any(k in TAG_CARA for k in marcas) else ""))
        miembros.append(m)


# ---------------------------------------------------------------- secciones en planta (arriostramiento horizontal)
SECCIONES = {11: "JJ", 14: "KK", 17: "LL", 20: "MM", 23: "NN", 26: "OO", 29: "PP"}
GRUPOS["Arriostramiento horizontal"] = "#20b2aa"

PEND = (ENV[0] - ENV[2591]) / 2591.0           # semi-ancho que se pierde por unidad de altura (esquema)
OFFSET_PLANO = 68.7                              # ancho de planta - ancho de cara (medido en H10 vs H11)

def recorte_y(a, b, ylo, yhi):
    """recorta el segmento al rango y [ylo, yhi]; None si queda fuera"""
    (x1, y1), (x2, y2) = a, b
    if abs(y1 - y2) < 1e-9:
        return (a, b) if ylo - 1 <= y1 <= yhi + 1 else None
    t1, t2 = (ylo - y1) / (y2 - y1), (yhi - y1) / (y2 - y1)
    t0, t3 = max(0.0, min(t1, t2)), min(1.0, max(t1, t2))
    if t0 >= t3: return None
    return ((x1 + t0 * (x2 - x1), y1 + t0 * (y2 - y1)), (x1 + t3 * (x2 - x1), y1 + t3 * (y2 - y1)))

def solapa(p, q, tol=25):
    """True si el segmento p cae sobre el q (colineal y dentro)"""
    return seg_dist(p[0], *q) < tol and seg_dist(p[1], *q) < tol

PLANTAS = []
for hoja, nom in SECCIONES.items():
    try: ax, marcas = extraer(hoja)
    except Exception: continue
    cs = sorted(clusters(ax), key=len, reverse=True)
    if not cs: continue
    c = cs[0]
    xs = [q[0] for i in c for q in ax[i]]; ys = [q[1] for i in c for q in ax[i]]
    W = max(xs) - min(xs)
    # El borde de la planta es la línea horizontal larga (≥ 0,9 W) del extremo que tiene el dibujo hacia adentro; el cluster
    # trae piezas que sobresalen del borde (ymin no es el borde) y en H14 el borde está arriba. Se re-expresa y como u = (y-borde)*s.
    # niveles horizontales (y agrupados a ±150) con cobertura ≥ 0,85 W: el borde puede venir en varios tramos (H14) y el eje de simetría
    # también es una línea larga; se distingue por el voladizo del dibujo (el borde casi no sobresale, el centro sí).
    hs = sorted((ax[i][0][1], min(ax[i][0][0], ax[i][1][0]), max(ax[i][0][0], ax[i][1][0])) for i in c if abs(ax[i][0][1] - ax[i][1][1]) < 1)
    niveles = []
    for y, x1, x2 in hs:
        if niveles and abs(y - niveles[-1][0]) <= 150: niveles[-1][1].append((x1, x2))
        else: niveles.append([y, [(x1, x2)]])
    cand = []
    for y, segs in niveles:
        segs.sort(); cov = 0.0; fin = None
        for x1, x2 in segs:
            if fin is None or x1 > fin: cov += x2 - x1; fin = x2
            elif x2 > fin: cov += x2 - fin; fin = x2
        if cov >= 0.85 * W: cand.append((y, min(a for a, _ in segs), max(b for _, b in segs)))
    sg = 1
    if len(cand) >= 2:
        L, U = cand[0], cand[-1]
        if (U[0] - L[0]) >= 0.9 * W: e0, Wl = L[0], U[0] - L[0]                       # planta completa (LL)
        else:                                                                           # media planta: borde + eje de simetría
            e0 = L[0]                                                                   # en todas las hojas la media planta se dibuja hacia arriba desde el borde
            Wl = W
        xc = (min(c_[1] for c_ in cand) + max(c_[2] for c_ in cand)) / 2
    elif len(cand) == 1:
        y1 = cand[0][0]
        if (max(ys) - y1) > (y1 - min(ys)): e0 = y1
        else: e0, sg = y1, -1
        Wl = max(cand[0][2] - cand[0][1], 0.9 * W); xc = (cand[0][1] + cand[0][2]) / 2
    else:
        e0, Wl, xc = min(ys), W, (min(xs) + max(xs)) / 2
    W0, W = W, Wl
    ax = {i: ((ax[i][0][0], (ax[i][0][1] - e0) * sg), (ax[i][1][0], (ax[i][1][1] - e0) * sg)) for i in c}
    ymin = 0.0
    yc = Wl / 2
    h_u = (ENV[0] - (W0 - OFFSET_PLANO) / (2 * ESCALA)) / PEND          # altura en unidades de esquema
    h_mm = h_u * ESCALA
    # El ancho de planta da la altura con ±~600 mm de incertidumbre (pendiente 0.27 mm de ancho por mm de altura);
    # las secciones se cortan en niveles de diafragma, así que se ajusta al anillo horizontal más cercano del cuerpo.
    h_calc = h_mm
    cand = {}
    for mm_ in miembros:
        if mm_.get("cara") == "A" and abs(mm_["dy_mm"]) < 40 and mm_["longitud_mm"] > 800:
            cand[round(mm_["a"][1])] = cand.get(round(mm_["a"][1]), 0) + 1
    cerca = [y for y in cand if abs(y - h_calc) <= 700]
    if cerca: h_mm = float(min(cerca, key=lambda y: abs(y - h_calc)))
    base = []                                                          # (a, b, marcas, espejo)
    for i in c:
        r = recorte_y(ax[i][0], ax[i][1], ymin, ymin + W)
        if r: base.append([r[0], r[1], marcas.get(i, []), False])
    n0 = len(base)
    for a, b, mk, _ in list(base):
        r = recorte_y(a, b, ymin, yc)
        if r is None or (r[0][1] > yc - 1 and r[1][1] > yc - 1): continue
        a2, b2 = (r[0][0], 2 * yc - r[0][1]), (r[1][0], 2 * yc - r[1][1])
        if any(solapa((a2, b2), (e[0], e[1])) for e in base[:n0]): continue      # ya dibujado en esa mitad
        base.append([a2, b2, mk, True])
    PLANTAS.append({"seccion": nom, "hoja": hoja, "ancho_mm": round(W), "altura_mm": round(h_mm), "altura_calculada_mm": round(h_calc), "ejes": len(base), "espejados": len(base) - n0})
    for k, (a, b, mk, esp) in enumerate(base):
        if math.dist(a, b) < 14: continue
        pa = ((a[0] - xc) , h_mm, -sg * (a[1] - yc)); pb = ((b[0] - xc), h_mm, -sg * (b[1] - yc))
        key = tuple(sorted([tuple(round(v / 5) for v in pa), tuple(round(v / 5) for v in pb)]))
        if key in vistos: continue
        vistos.add(key)
        dx, dz = pb[0] - pa[0], pb[2] - pa[2]
        tipo = "Horizontal" if abs(dz) < 1 or abs(dx) < 1 else "Diagonal"
        kk = ("Arriostramiento horizontal", nom, tipo); cont[kk] = cont.get(kk, 0) + 1; n = cont[kk]
        marcas_k = list(dict.fromkeys(datos_marca(hoja, x, False) for x in mk))
        m = {"id": f"BE-S{nom}-{tipo[0]}{n:03d}", "nombre": f"{tipo} {n:02d} · Sección {nom}", "grupo": "Arriostramiento horizontal",
             "tramo": f"Sec. {nom}", "tipo": tipo, "cara": "planta",
             "a": [round(pa[0]), round(h_mm), round(pa[2])], "b": [round(pb[0]), round(h_mm), round(pb[2])],
             "longitud_mm": round(math.dist(pa, pb)), "dx_mm": round(dx), "dy_mm": 0, "dz_mm": round(dz),
             "fuente": f"plano H{hoja} (sección {nom})" + (" · espejado" if esp else "")}
        if marcas_k and all("PLATAFORMA" in (CATALOGO[x].get("conjunto") or "").upper() for x in marcas_k):
            m["grupo"] = "Plataformas"      # piezas cuyo conjunto en la lista es "PLATAFORMA…"
        if marcas_k:
            m["marcas"] = marcas_k; m["marca"] = " / ".join(CATALOGO[x]["marca"] for x in marcas_k)
            m["coherencia"], m["coherencia_txt"] = coherencia(marcas_k, m["longitud_mm"])
            m["nombre"] = f"{m['marca']} · {tipo} {n:02d} · Sección {nom}"
            m["verificacion"] = (f"Marca leída del plano H{hoja} (leader → eje de la sección {nom}); posición en planta del plano, "
                                 f"altura calculada con el ancho de la sección ({round(W)} mm)" + ("; elemento espejado respecto al eje de simetría de la planta" if esp else "") + f". Altura ajustada al anillo horizontal más cercano del cuerpo (calculada {round(h_calc)} mm)")
        miembros.append(m)
print("plantas:", PLANTAS)

n_marc = sum(1 for m in miembros if m.get("marca"))
print(collections.Counter(m.get("coherencia") for m in miembros if m["cara"] == "A"))
print(len(miembros), "miembros;", n_marc, "con marca;", len(EXTRA), "ejes agregados desde planos")
alto = 3221 * ESCALA
data = {"meta": {
    "nombre": "Torre especial de retención tipo BE",
    "linea": "Sogamoso – Norte – Tequendama 500 kV (EEB) · Consorcio Conexión Sogamoso",
    "fuente": "Planos DIS6054-H1…H47 (sin H31). Geometría global: esquema a escala de la hoja 19 (capa TORN); marcas y perfiles: burbujas y listas de materiales de cada hoja.",
    "escala_mm_por_unidad": ESCALA, "alto_total_mm": round(alto), "ancho_base_mm": round(2 * env(0) * ESCALA),
    "marcas_vinculadas": n_marc, "registros": REGS, "secciones_planta": PLANTAS,
    "validacion": [{"hoja": "H10", "cota": f"ancho de cara a z={y}", "plano_mm": w, "modelo_mm": round(2 * env(y) * ESCALA, 1)} for y, w, _ in ESC_VAL],
    "supuestos": [
        "Geometría global tomada de la silueta a escala de la hoja 19, extruida con planta cuadrada y simetría de 4 caras (el esquema solo da una vista de elevación). Corresponde a la variante de mayor altura (alto 107,3 m).",
        "Marcas: solo aparecen en los miembros donde la burbuja del plano, su leader y el eje coinciden geométricamente con un miembro del esquema (tolerancia ≈ 27 mm). Los miembros sin marca NO tienen vínculo verificado, aunque su marca exista en la lista de materiales.",
        "La marca se verificó en la cara A del plano; en las caras B, C y D se asigna por simetría (el plano indica caras transversal y longitudinal distintas, así que puede diferir).",
        "Caras: el plano H10 rotula cada marca de diagonal como 'cara transversal' (802D, 803D…) o 'cara longitudinal' (810D, 811D…); el visor asigna A y C como transversales y B y D como longitudinales (convención del visor, por confirmar). Donde el plano no rotula la cara, la marca se muestra en las cuatro.",
        "Ejes con marca que el esquema no trae se agregaron con la geometría del propio plano (fuente indicada en cada miembro).",
        "Cabeza y brazos: marcos planos en las caras A y C, sin profundidad real.",
        "Secciones en planta (JJ…PP, hojas 11, 14, 17, 20, 23, 26, 29): modeladas a la altura que da su ancho (planta cuadrada). Las hojas dibujan media planta y se espeja respecto al eje de simetría; la orientación de la planta respecto a las caras A–D es una convención del visor.",
        "Hojas 1–9 (cabeza, brazos, viga, delta): solo se colocaron las vistas que coinciden estadísticamente con el esquema (≥ 6 nodos a 27 mm); las demás vistas de esas hojas siguen sin ubicar. Patas y stubs (H32–H40) tampoco están ubicados; su lista de materiales sí está disponible.",
        "Secciones de barra con espesor esquemático; el perfil real es el de la lista de materiales. Longitudes de modelo = distancia entre nodos del eje; la longitud de pieza (lista) es la de despiece y difiere por cortes y holguras.",
        "El esquema tiene un corte de vista a ≈ 56 m de altura (entre T4 y T5), que aparece como vacío.",
        "Nombres de tramo (T1…T10) y de cara (A–D) son convenciones del visor, no marcas del plano. No se procesó la hoja 31 (no recibida).",
        "Hojas 41–47 (acceso, plataformas, barandas, escaleras, guardas y soportes de escalera): sus piezas están en la lista de materiales, pero NO están ubicadas en el 3D (las hojas no fijan la cota de cada plataforma).",
    ]},
    "grupos": [{"nombre": n, "color": c} for n, c in GRUPOS.items()],
    "marcas": CATALOGO, "miembros": miembros, "lista_materiales": BOM}
json.dump(data, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
print("BOM filas:", len(BOM))
