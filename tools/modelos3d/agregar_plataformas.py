"""Agrega plataformas ESQUEMÁTICAS al JSON del modelo (python3 agregar_plataformas.py <json>).
No salen de un plano de ubicación (los planos H41-H47 son detalles sueltos): se colocan en los niveles de las
secciones en planta que tienen hoja "PLATAFORMA PARA CORTE I-I" (KK, MM, NN, OO), dentro del cuadro de la torre.
"""
import json, math, sys
p = sys.argv[1]
d = json.load(open(p))
d["miembros"] = [m for m in d["miembros"] if m.get("grupo") != "Plataformas (esquemático)"]
d["grupos"] = [g for g in d["grupos"] if g["nombre"] != "Plataformas (esquemático)"]
d["grupos"].append({"nombre": "Plataformas (esquemático)", "color": "#e6c229"})
NIVELES = {"KK": 14, "MM": 20, "NN": 23, "OO": 26}
HOLGURA = 1200      # mm entre el borde de la plataforma y la línea de las patas
PASO = 800          # separación de travesaños (mm)
SUBE = 150          # la plataforma queda sobre el arriostramiento horizontal
def mk(sec, n, a, b, tipo, nota):
    return {"id": f"BE-PL{sec}-{n:03d}", "nombre": f"{tipo} {n:02d} · Plataforma {sec}", "grupo": "Plataformas (esquemático)",
            "tramo": f"Plat. {sec}", "tipo": tipo, "cara": "planta", "a": a, "b": b,
            "longitud_mm": round(math.dist(a, b)),
            "dx_mm": b[0] - a[0], "dy_mm": 0, "dz_mm": b[2] - a[2],
            "fuente": "supuesto de ubicación (no leído de un plano)",
            "verificacion": f"Ubicación ESQUEMÁTICA: plataforma al nivel de la sección {sec} ({nota}). Su existencia sale de la hoja "
                            f"'PLATAFORMA PARA CORTE I-I' del conjunto; posición, perímetro y separación de travesaños son supuestos de modelado, "
                            f"no medidas del plano. Perfiles de la lista: L 50X4H (771H) y platina 5X50 / 5X100 (770)."}
n_tot = 0
for s in d["meta"]["secciones_planta"]:
    if s["seccion"] not in NIVELES: continue
    sec = s["seccion"]
    h = s["altura_mm"] + SUBE
    half = s["ancho_mm"] / 2 - HOLGURA
    n = 0
    nota = f"altura {h} mm, semiancho {round(half)} mm"
    for (x1, z1, x2, z2) in [(-half, -half, half, -half), (half, -half, half, half), (half, half, -half, half), (-half, half, -half, -half)]:
        n += 1; d["miembros"].append(mk(sec, n, [round(x1), h, round(z1)], [round(x2), h, round(z2)], "Larguero perimetral", nota))
    k = int(2 * half // PASO)
    paso = 2 * half / k
    for i in range(1, k):
        x = -half + i * paso
        n += 1; d["miembros"].append(mk(sec, n, [round(x), h, round(-half)], [round(x), h, round(half)], "Travesaño", nota))
    n_tot += n
d["meta"]["supuestos"] = [t for t in d["meta"]["supuestos"] if not t.startswith("Plataformas esquemáticas")]
d["meta"]["supuestos"].append("Plataformas esquemáticas (amarillo): marco perimetral + travesaños cada ~800 mm en los niveles KK, MM, NN y OO (los que tienen hoja 'PLATAFORMA PARA CORTE I-I'), inset 1200 mm de las patas. No hay plano de ubicación: posición y trama son supuestos, no medidas. Escaleras, barandas y módulos reales (H41–H47) siguen sin ubicar.")
json.dump(d, open(p, "w"), ensure_ascii=False, separators=(",", ":"))
print(n_tot, "miembros de plataforma")
