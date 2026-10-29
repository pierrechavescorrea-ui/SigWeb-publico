"""Genera los modelos BP1..BP4 a partir de torre-bp6.json / torre-bp5.json.

Evidencia (planos DIS6052, anchos de la cara en la base de cada cuerpo):
  BP6 11564 mm (z=12 m)  BP5 10278  BP4 8991  BP3 7705  BP2 6419  BP1 5133
  = ancho del tronco de la silueta BP6 a z = 12 / 16.5 / 21 / 25.5 / 30 / 34.5 m (pendiente 286 mm/m por lado).
Es decir, cada cuerpo menor es el mismo tronco cortado 4.5 m más arriba: BPk = BP6 sin los (6-k)*4.5 m inferiores del tronco,
con la pata de 12 m debajo (la pata se estrecha a la nueva base).
Uso: python3 generar_bp_cuerpos.py <carpeta modelos3d>
"""
import json, sys, os, copy, collections, itertools
import numpy as np

D = sys.argv[1]
H6 = json.load(open(os.path.join(D, 'torre-bp6.json'), encoding='utf-8'))
H5 = json.load(open(os.path.join(D, 'torre-bp5.json'), encoding='utf-8'))

# (cuerpo, origen, corte en z de la silueta de origen [mm], hoja del cuerpo, ancho del plano en la base del cuerpo [mm])
OBJ = [(4, H6, 21000, 16, 8991), (2, H6, 30000, 11, 6419), (3, H5, 21000, 14, 7705), (1, H5, 30000, 9, 5133)]

def perfiles(mod):
    """semianchura x e y (|prof|) en función de z, con los nodos de las caras del modelo"""
    px, py = collections.defaultdict(float), collections.defaultdict(float)
    for m in mod['miembros']:
        for p in (m['a'], m['b']):
            z = p[1]
            if m['cara'].startswith('Longitudinal') and 'interior' not in m['cara']: px[z] = max(px[z], abs(p[0]))
            if m['cara'].startswith('Transversal'): py[z] = max(py[z], abs(p[2]))
    f = lambda d: (np.array(sorted(d)), np.array([d[z] for z in sorted(d)]))
    return f(px), f(py)

def interp(pf, z):
    zs, vs = pf
    # tramo recto del tronco: se ajusta una recta a los nodos en la zona 12-30 m y se extrapola
    return float(np.interp(z, zs, vs))

def recta(pf, z0, z1):
    zs, vs = pf; m = (zs >= z0) & (zs <= z1)
    a, b = np.polyfit(zs[m], vs[m], 1); return lambda z: a * z + b

for cuerpo, SRC, corte, hoja, ancho_plano in OBJ:
    mod = copy.deepcopy(SRC); s = corte - 12000
    pfx, pfy = perfiles(SRC)
    rx = recta(pfx, 12000, 30000); ry = recta(pfy, 12000, 30000)
    # semiancho de la pata a z (recta del tronco prolongada) antes y después del desplazamiento
    escx = lambda z: rx(z + s) / rx(z); escy = lambda z: ry(z + s) / ry(z)
    nuevos = []
    for m in mod['miembros']:
        za, zb = m['a'][1], m['b'][1]
        if min(za, zb) >= corte - 1:                      # cuerpo y parte alta: solo se baja
            m['a'][1] -= s; m['b'][1] -= s; nuevos.append(m)
        elif max(za, zb) <= 12000 + 1:                    # pata de 12 m: se estrecha a la nueva base
            for p in (m['a'], m['b']):
                p[0] = round(p[0] * escx(p[1])); p[2] = round(p[2] * escy(p[1]))
            m.pop('marcas', None); m.pop('marca', None); m.pop('coherencia', None); m.pop('coherencia_txt', None); m.pop('verificacion', None)
            if 'marca_orig' in m: m.pop('marca_orig')
            m['nota'] = 'Pata de 12 m (H27) estrechada a la base del cuerpo BP%d; la marca no se vincula porque la longitud de pieza cambia.' % cuerpo
            nuevos.append(m)
        # miembros entre 12 m y el corte: pertenecen al tronco eliminado
    # recalcular longitudes y marcar origen
    for i, m in enumerate(nuevos, 1):
        a, b = m['a'], m['b']
        m['dx_mm'] = b[0] - a[0]; m['dy_mm'] = b[1] - a[1]; m['dz_mm'] = b[2] - a[2]
        m['longitud_mm'] = round((m['dx_mm'] ** 2 + m['dy_mm'] ** 2 + m['dz_mm'] ** 2) ** .5)
        pref, _, resto = m['id'].partition('-')
        m['id'] = f'BP{cuerpo}-{resto}'
    # nombre sin la marca vieja de nombre (si el nombre empieza con marcas, se conserva)
    if cuerpo == 3:   # en BP3 el cuerpo inferior (z 21-24 de la silueta BP5) se detalla en H14
        for m in nuevos:
            if m['plano']['hoja'] == 16:
                m['plano'] = {'codigo': m['plano']['codigo'], 'hoja': 14, 'titulo': 'CUERPO BP3'}
                m['fuente'] = 'silueta del plano H14 (cuerpo bp3)'
    # el cuerpo propio (base de la parte recta) se detalla en su hoja de cuerpo
    ymin_cuerpo = 12000; ymax_cuerpo = 12000 + 3000
    for m in nuevos:
        if ymin_cuerpo - 1 <= min(m['a'][1], m['b'][1]) and max(m['a'][1], m['b'][1]) <= ymax_cuerpo + 1 and m['plano']['hoja'] in (15, 16, 14, 11, 9, 13):
            if cuerpo in (4, 3) or m['plano']['hoja'] == 15:
                pass
    alto = max(max(m['a'][1], m['b'][1]) for m in nuevos)
    xs = [abs(p[0]) for m in nuevos for p in (m['a'], m['b'])]
    ys = [abs(p[2]) for m in nuevos for p in (m['a'], m['b'])]
    z0 = [m for m in nuevos if min(m['a'][1], m['b'][1]) == 0]
    ancho_base = round(2 * max(abs(p[0]) for m in z0 for p in (m['a'], m['b'])))
    largo_base = round(2 * max(abs(p[2]) for m in z0 for p in (m['a'], m['b'])))
    # semiancho del cuerpo al nivel z=12 m (plano: ancho de la cara en la base del cuerpo)
    en12 = max(abs(p[0]) for m in nuevos for p in (m['a'], m['b']) if abs(p[1] - 12000) < 1 and 'interior' not in m['cara'] and 'Riostra' not in m['cara'])
    # marcas: solo las que siguen referenciadas
    usadas = {k for m in nuevos for k in m.get('marcas', [])}
    mod['marcas'] = {k: v for k, v in mod['marcas'].items() if k in usadas}
    hojas = {m['plano']['hoja'] for m in nuevos} | {v['hoja'] for v in mod['marcas'].values()} | {hoja}
    mod['lista_materiales'] = [f for f in mod['lista_materiales'] if f['hoja'] in hojas]
    # coherencia con longitudes nuevas (por si cambió alguna)
    mod['miembros'] = nuevos
    # coherencia: la longitud de los miembros desplazados no cambia
    meta = mod['meta']
    meta['nombre'] = f'Torre tipo BP, cuerpo BP{cuerpo}'
    meta['alto_total_mm'] = alto; meta['ancho_base_mm'] = ancho_base; meta['largo_base_mm'] = largo_base
    meta['marcas_vinculadas'] = sum(1 for m in nuevos if m.get('marcas'))
    meta['fuente'] = (f'Planos DIS6052 (silueta BP{"6" if SRC is H6 else "5"}); cuerpo BP{cuerpo} = tronco cortado a z = {corte/1000:g} m de esa silueta '
                      f'(ancho de la cara en la base del cuerpo en el plano H{hoja}: {ancho_plano} mm; modelo: {round(2*en12)} mm).')
    meta['validacion'] = [{'hoja': f'H{hoja}', 'cota': f'ancho de la cara en la base del cuerpo BP{cuerpo}', 'plano_mm': ancho_plano, 'modelo_mm': round(2 * en12)}]
    sup = [t for t in meta.get('supuestos', []) if not t.startswith('La base corresponde') and 'No se han leído' not in t]
    sup.insert(0, f'BP{cuerpo} = misma silueta que BP{"6" if SRC is H6 else "5"} sin los {s/1000:g} m inferiores del tronco: el ancho del plano H{hoja} en la base del cuerpo coincide con el de la silueta a z = {corte/1000:g} m; cada cuerpo menor está 4.5 m más arriba que el anterior.')
    sup.insert(1, f'La pata de 12 m (H27) se estrecha en planta para empalmar con la base del cuerpo BP{cuerpo}; su despiece real puede diferir. Sus piezas no llevan marca vinculada.')
    sup.append('Las marcas y hojas de los paneles compartidos son las del modelo de origen (los paneles son iguales en todos los cuerpos).')
    meta['supuestos'] = sup
    ruta = os.path.join(D, f'torre-bp{cuerpo}.json')
    json.dump(mod, open(ruta, 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
    print(cuerpo, 'miembros', len(nuevos), 'alto', alto, 'ancho_base', ancho_base, 'largo_base', largo_base, 'ancho@12', round(2 * en12), 'plano', ancho_plano)
