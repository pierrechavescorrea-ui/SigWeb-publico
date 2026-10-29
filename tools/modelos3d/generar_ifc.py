#!/usr/bin/env python3
"""Genera un IFC4 de una torre a partir del JSON del visor 3D (wwwroot/modelos3d/torre-*.json).

Uso: python3 generar_ifc.py <torre.json> <salida.ifc>

- Cada miembro del JSON → IfcMember (Montante = CHORD, resto = BRACE) con su eje.
- Perfil: solo si la marca del miembro está en la lista de materiales (perfil "L AxT").
  Ángulo de alas iguales → IfcLShapeProfileDef extruido sobre el eje.
  Sin marca vinculada → tubo de Ø40 mm (IfcCircleProfileDef) y PerfilLeido = No.
- Orientación de las alas (SUPUESTO, no viene en los planos procesados):
  montantes con el talón en el eje y las alas hacia el interior de la torre;
  diagonales/horizontales con un ala apoyada en el plano de su cara y la otra hacia el interior.
- El eje de la pieza coincide con la línea de trabajo del plano (sin gramil ni excentricidad).
- Ejes: visor (X, Y arriba, Z) → IFC (X, −Z, Y), Z arriba; unidades mm.
"""
import json
import math
import re
import sys
import time

import ifcopenshell
import ifcopenshell.guid

ACERO_DENSIDAD = 7850.0  # kg/m³

SUPUESTOS_IFC = [
    "El perfil de cada pieza sale de su marca leída en el plano y de la lista de materiales; las piezas sin marca vinculada se dibujan como tubo Ø40 mm (PerfilLeido = No).",
    "La orientación de las alas de los ángulos no se lee de los planos procesados: se usa la convención de montaje (montantes con alas hacia el interior; diagonales con un ala en el plano de la cara y la otra hacia el interior).",
    "El talón del ángulo se ubica sobre la línea de trabajo del plano (sin gramil ni excentricidad de conexión).",
    "No se modelan placas, tornillos ni cartelas.",
]


def unit(v):
    n = math.sqrt(sum(c * c for c in v))
    return [c / n for c in v] if n > 1e-9 else None


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def perp(v, u):
    """Componente de v perpendicular a u (u unitario), normalizada."""
    k = dot(v, u)
    return unit([v[i] - k * u[i] for i in range(3)])


def a_ifc(p):
    x, y, z = p
    return [float(x), float(-z), float(y)]


def parse_perfil(txt):
    m = re.match(r"\s*L\s*(\d+(?:[.,]\d+)?)\s*[Xx]\s*(\d+(?:[.,]\d+)?)(?:\s*[Xx]\s*(\d+(?:[.,]\d+)?))?", txt or "")
    if not m:
        return None
    f = lambda s: float(s.replace(",", "."))
    if m.group(3):
        return f(m.group(1)), f(m.group(2)), f(m.group(3))   # L A x B x t
    return f(m.group(1)), f(m.group(1)), f(m.group(2))       # L A x t (alas iguales)


def main(src, dst):
    d = json.load(open(src, encoding="utf-8"))
    meta = d["meta"]
    lista = {}
    for l in d.get("lista_materiales", []):
        if l.get("clase", "PERFIL") == "PERFIL" and l.get("marca"):
            lista.setdefault(str(l["marca"]).strip().upper(), l)

    f = ifcopenshell.file(schema="IFC4")
    own = None
    ctx_now = int(time.time())

    def guid():
        return ifcopenshell.guid.new()

    # --- Proyecto, unidades, contexto ---
    person = f.createIfcPerson(None, None, "SigWeb")
    org = f.createIfcOrganization(None, "JE Jaimes Ingenieros")
    app = f.createIfcApplication(org, "1.0", "SigWeb generar_ifc.py", "SigWeb")
    own = f.createIfcOwnerHistory(f.createIfcPersonAndOrganization(person, org), app, None, "ADDED", None, None, None, ctx_now)

    units = f.createIfcUnitAssignment([
        f.createIfcSIUnit(None, "LENGTHUNIT", "MILLI", "METRE"),
        f.createIfcSIUnit(None, "AREAUNIT", None, "SQUARE_METRE"),
        f.createIfcSIUnit(None, "VOLUMEUNIT", None, "CUBIC_METRE"),
        f.createIfcSIUnit(None, "MASSUNIT", "KILO", "GRAM"),
        f.createIfcSIUnit(None, "PLANEANGLEUNIT", None, "RADIAN"),
    ])
    origen = f.createIfcCartesianPoint((0.0, 0.0, 0.0))
    eje_z = f.createIfcDirection((0.0, 0.0, 1.0))
    eje_x = f.createIfcDirection((1.0, 0.0, 0.0))
    wcs = f.createIfcAxis2Placement3D(origen, eje_z, eje_x)
    ctx = f.createIfcGeometricRepresentationContext(None, "Model", 3, 1.0e-5, wcs, None)
    body = f.createIfcGeometricRepresentationSubContext("Body", "Model", None, None, None, None, ctx, None, "MODEL_VIEW", None)
    axis_ctx = f.createIfcGeometricRepresentationSubContext("Axis", "Model", None, None, None, None, ctx, None, "GRAPH_VIEW", None)

    proyecto = f.createIfcProject(guid(), own, meta.get("linea") or "SigWeb", None, None, None, None, [ctx], units)

    def placement(rel=None):
        return f.createIfcLocalPlacement(rel, f.createIfcAxis2Placement3D(origen, None, None))

    pl_site = placement()
    sitio = f.createIfcSite(guid(), own, "Sitio", None, None, pl_site, None, None, "ELEMENT", None, None, None, None, None)
    pl_bld = placement(pl_site)
    torre = f.createIfcBuilding(guid(), own, meta.get("nombre", "Torre"), meta.get("fuente"), None, pl_bld, None, None, "ELEMENT", None, None, None)
    f.createIfcRelAggregates(guid(), own, None, None, proyecto, [sitio])
    f.createIfcRelAggregates(guid(), own, None, None, sitio, [torre])

    # Un nivel (IfcBuildingStorey) por grupo del visor: Patas, Cuerpo, Mástiles, Viga y brazos...
    grupos = []
    for m in d["miembros"]:
        if m.get("grupo") not in grupos:
            grupos.append(m.get("grupo"))
    niveles = {}
    for g in grupos:
        pl = placement(pl_bld)
        niveles[g] = (f.createIfcBuildingStorey(guid(), own, g, None, None, pl, None, None, "ELEMENT", 0.0), pl)
    f.createIfcRelAggregates(guid(), own, None, None, torre, [n for n, _ in niveles.values()])

    # Materiales
    mats = {}

    def material(nombre):
        nombre = nombre or "Acero (grado no leído)"
        if nombre not in mats:
            mats[nombre] = f.createIfcMaterial(nombre, None, "Acero")
        return mats[nombre]

    # Perfiles (uno por designación)
    perfiles = {}

    def perfil_l(txt):
        if txt in perfiles:
            return perfiles[txt]
        a, b, t = parse_perfil(txt)
        # IfcLShapeProfileDef: el origen va en el centro del rectángulo envolvente; se desplaza
        # (A/2, B/2) para que el talón quede en el origen y las alas hacia +X y +Y.
        pos = f.createIfcAxis2Placement2D(f.createIfcCartesianPoint((b / 2.0, a / 2.0)), None)
        p = f.createIfcLShapeProfileDef("AREA", txt, pos, a, b, t, None, None, None)
        perfiles[txt] = (p, a, b, t)
        return perfiles[txt]

    tubo = f.createIfcCircleProfileDef("AREA", "Ø40 (perfil no leído)", None, 20.0)

    # Colores por estado de perfil
    def estilo(rgb, transp=0.0):
        col = f.createIfcColourRgb(None, *rgb)
        rend = f.createIfcSurfaceStyleRendering(col, transp, None, None, None, None, None, None, "NOTDEFINED")
        return f.createIfcSurfaceStyle(None, "BOTH", [rend])

    st_leido = estilo((0.62, 0.66, 0.70))       # galvanizado
    st_no = estilo((0.85, 0.45, 0.20), 0.35)    # naranja translúcido: perfil no leído

    por_nivel = {g: [] for g in grupos}
    mat_rel = {}
    n_leido = 0
    peso_total = 0.0

    for m in d["miembros"]:
        a = [float(c) for c in m["a"]]
        b = [float(c) for c in m["b"]]
        A, B = a_ifc(a), a_ifc(b)
        u = unit([B[i] - A[i] for i in range(3)])
        if u is None:
            continue
        L = math.dist(A, B)

        marcas = [s.strip().upper() for s in re.split(r"[/,]", str(m.get("marca") or "")) if s.strip()]
        fila = next((lista[k] for k in marcas if k in lista), None)
        dims = parse_perfil(fila["perfil"]) if fila else None

        # Orientación (SUPUESTO): referencia "hacia el interior" = desde el punto medio hacia el eje vertical.
        mid = [(A[i] + B[i]) / 2.0 for i in range(3)]
        hacia_eje = [-mid[0], -mid[1], 0.0]
        cara = (m.get("cara") or "")
        if m.get("tipo") == "Montante":
            p1 = perp([-math.copysign(1, mid[0]), 0, 0], u) or perp([0, 0, 1], u)
            q_des = perp([0, -math.copysign(1, mid[1]), 0], u)
            xdir = p1
            if q_des and dot(cross(u, xdir), q_des) < 0:
                xdir = q_des   # así el ala Y (= u × X) queda también hacia el interior
        else:
            # Normal de la cara (en IFC: transversal = plano Y=cte; longitudinal = plano X=cte)
            if cara.startswith("Transversal"):
                n_ext = [0, math.copysign(1, mid[1]), 0]
            elif cara.startswith("Longitudinal"):
                n_ext = [math.copysign(1, mid[0]), 0, 0]
            else:
                n_ext = unit([-hacia_eje[0], -hacia_eje[1], 0]) or [1, 0, 0]
            xdir = perp([-c for c in n_ext], u) or perp([0, 0, 1], u) or perp([1, 0, 0], u)

        if dims:
            prof, pa, pb, pt = perfil_l(fila["perfil"])
            n_leido += 1
            area_mm2 = (pa + pb - pt) * pt
            peso = area_mm2 * 1e-6 * L * 1e-3 * ACERO_DENSIDAD
            peso_total += peso
        else:
            prof, peso = tubo, None

        nivel, pl_niv = niveles[m.get("grupo")]
        pl = f.createIfcLocalPlacement(pl_niv, f.createIfcAxis2Placement3D(
            f.createIfcCartesianPoint(tuple(A)), f.createIfcDirection(tuple(u)), f.createIfcDirection(tuple(xdir))))
        solido = f.createIfcExtrudedAreaSolid(prof, None, eje_z, L)
        f.createIfcStyledItem(solido, [st_leido if dims else st_no], None)
        rep_body = f.createIfcShapeRepresentation(body, "Body", "SweptSolid", [solido])
        linea = f.createIfcPolyline([f.createIfcCartesianPoint((0.0, 0.0, 0.0)), f.createIfcCartesianPoint((0.0, 0.0, L))])
        rep_axis = f.createIfcShapeRepresentation(axis_ctx, "Axis", "Curve3D", [linea])
        forma = f.createIfcProductDefinitionShape(None, None, [rep_body, rep_axis])

        nombre = m.get("nombre") or m.get("id")
        tipo_pred = "CHORD" if m.get("tipo") == "Montante" else "BRACE"
        mem = f.createIfcMember(guid(), own, nombre, m.get("fuente"), m.get("tipo"), pl, forma, m.get("marca") or None, tipo_pred)
        por_nivel[m.get("grupo")].append(mem)
        if dims:
            mat_rel.setdefault(fila.get("acero") or "", []).append(mem)

        def txt(v):
            return f.createIfcText(str(v)) if v not in (None, "") else None

        props = [
            ("Id", f.createIfcIdentifier(str(m.get("id")))),
            ("Tipo", txt(m.get("tipo"))),
            ("Grupo", txt(m.get("grupo"))),
            ("Tramo", txt(m.get("tramo"))),
            ("Cara", txt(m.get("cara"))),
            ("Marca", txt(m.get("marca"))),
            ("Perfil", txt(fila["perfil"] if fila else "No leído")),
            ("PerfilLeido", f.createIfcBoolean(bool(dims))),
            ("Acero", txt(fila.get("acero") if fila else None)),
            ("Conjunto", txt(fila.get("conjunto") if fila else m.get("conjunto"))),
            ("LongitudListado_mm", txt(fila.get("long_mm") if fila else None)),
            ("Coherencia", txt(m.get("coherencia_txt"))),
            ("Verificacion", txt(m.get("verificacion"))),
            ("Plano", txt((m.get("plano") or {}).get("codigo"))),
            ("Hoja", txt((m.get("plano") or {}).get("hoja"))),
            ("OrientacionAlas", txt("Supuesto (convención de montaje)" if dims else None)),
        ]
        hp = [f.createIfcPropertySingleValue(k, None, v, None) for k, v in props if v is not None]
        pset = f.createIfcPropertySet(guid(), own, "SigWeb_Pieza", None, hp)
        f.createIfcRelDefinesByProperties(guid(), own, None, None, [mem], pset)

        q = [f.createIfcQuantityLength("Length", None, None, L, None)]
        if peso is not None:
            q.append(f.createIfcQuantityWeight("NetWeight", "Calculado: área del perfil × longitud del eje × 7850 kg/m³", None, peso, None))
        qset = f.createIfcElementQuantity(guid(), own, "Qto_MemberBaseQuantities", None, None, q)
        f.createIfcRelDefinesByProperties(guid(), own, None, None, [mem], qset)

    for g, (niv, _) in niveles.items():
        if por_nivel[g]:
            f.createIfcRelContainedInSpatialStructure(guid(), own, None, None, por_nivel[g], niv)
    for acero, mems in mat_rel.items():
        f.createIfcRelAssociatesMaterial(guid(), own, None, None, mems, material(acero))

    # Datos de la torre (validación y supuestos) en la propia torre
    def pv(k, v):
        return f.createIfcPropertySingleValue(k, None, f.createIfcText(str(v)), None)

    hp = [pv("Fuente", meta.get("fuente", "")), pv("AltoTotal_mm", meta.get("alto_total_mm", "")),
          pv("AnchoBase_mm", meta.get("ancho_base_mm", "")), pv("Miembros", len(d["miembros"])),
          pv("MiembrosConPerfil", n_leido), pv("PesoPerfilesLeidos_kg", round(peso_total, 1))]
    for i, s in enumerate(meta.get("supuestos", []) + SUPUESTOS_IFC, 1):
        hp.append(pv(f"Supuesto{i:02d}", s))
    for i, v in enumerate(meta.get("validacion", []), 1):
        hp.append(pv(f"Validacion{i:02d}", f"{v.get('hoja')} · {v.get('cota')}: plano {v.get('plano_mm')} mm / modelo {v.get('modelo_mm')} mm"))
    f.createIfcRelDefinesByProperties(guid(), own, None, None, [torre], f.createIfcPropertySet(guid(), own, "SigWeb_Torre", None, hp))

    f.write(dst)
    print(f"{dst}: {len(d['miembros'])} miembros, {n_leido} con perfil L, peso perfiles leídos {peso_total:.0f} kg")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
