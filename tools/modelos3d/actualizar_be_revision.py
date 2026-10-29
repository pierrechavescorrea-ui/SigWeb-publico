#!/usr/bin/env python3
"""Aplica a un modelo BE ya generado la revisión nueva de las hojas DIS6054-H1…H20:
lista de materiales, perfiles/longitudes de las marcas y coherencia con el eje del modelo.
La geometría (esquema de la hoja 19, capa TORN, y los ejes M-EJES/EJES de las hojas) se comparó
hoja a hoja con la revisión anterior y es la misma; solo cambian anotaciones y algunas longitudes de pieza.
Uso: python3 actualizar_be_revision.py <carpeta_dxf_nuevos H1..H20> <modelo_origen.json> <salida.json> [hoja_ini hoja_fin]"""
import sys, os, json, itertools, collections
import ezdxf
from ezdxf import recover

DXF, SRC, OUT = sys.argv[1:4]
H_INI, H_FIN = (int(sys.argv[4]), int(sys.argv[5])) if len(sys.argv) >= 6 else (1, 20)
mod = json.load(open(SRC, encoding='utf-8'))

def doc(n):
    r = os.path.join(DXF, f'H{n}.dxf')
    try: return ezdxf.readfile(r).modelspace()
    except Exception: return recover.readfile(r)[0].modelspace()

HOJA_TXT = {43: '43–45', 46: '46–47'}
nuevas, nueva_key, vistos = [], collections.defaultdict(list), set()
hojas_nuevas = set()
for n in range(H_INI, H_FIN + 1):
    try: ms = doc(n)
    except Exception: continue
    hojas_nuevas.add(n); conj = ''
    for e in ms:
        if e.dxftype() == 'INSERT' and e.dxf.name == 'CONJUNTO':
            a = {x.dxf.tag: x.dxf.text for x in e.attribs}
            if a.get('NOM') and not conj: conj = (a.get('NOM') + ' ' + (a.get('TIP') or '')).strip()
    for e in ms:
        if e.dxftype() == 'INSERT' and e.dxf.name in ('PERFILES', 'LAMINAS'):
            a = {x.dxf.tag: x.dxf.text.strip() for x in e.attribs}
            pos = a.get('POS', '')
            if not pos: continue
            f = {'hoja': n, 'hoja_txt': HOJA_TXT.get(n, str(n)), 'clase': 'PERFIL' if e.dxf.name == 'PERFILES' else 'LÁMINA',
                 'marca': pos, 'perfil': a.get('DES', ''), 'long_mm': a.get('LON', ''), 'cant': a.get('CAN', ''),
                 'acero': a.get('CAL', ''), 'conjunto': a.get('CON') or conj, 'obs': a.get('OBS', '')}
            k = tuple(f.values())
            if k in vistos: continue
            vistos.add(k); nuevas.append(f); nueva_key[(n, pos)].append(f)

viejas = [f for f in mod['lista_materiales'] if f['hoja'] not in hojas_nuevas]
cambios = 0; antes = {(f['hoja'], f['marca']): f for f in mod['lista_materiales'] if f['hoja'] in hojas_nuevas}
for k, fs in nueva_key.items():
    o = antes.get(k)
    if o is None or any(o[c] != fs[0][c] for c in ('perfil', 'long_mm', 'cant', 'acero', 'conjunto')): cambios += 1
mod['lista_materiales'] = sorted(viejas + nuevas, key=lambda f: (f['hoja'], f['marca']))

# catálogo de marcas (clave 'hoja|marca')
for k, c in mod['marcas'].items():
    h = c['hoja']
    if h not in hojas_nuevas: continue
    fs = nueva_key.get((h, c['marca'])) or []
    f = fs[0] if fs else {}
    c.update({'clase': f.get('clase'), 'perfil': f.get('perfil'), 'long_mm': f.get('long_mm'), 'cant': f.get('cant'),
              'acero': f.get('acero'), 'conjunto': f.get('conjunto'), 'en_lista': bool(fs)})

def coherencia(keys, eje):
    L = [int(mod['marcas'][k]['long_mm']) for k in keys if mod['marcas'][k]['clase'] == 'PERFIL' and str(mod['marcas'][k]['long_mm']).isdigit()]
    if not L: return 'n/a', 'Marca(s) sin perfil con longitud numérica (lámina, accesorio o no listada).'
    for r in (1, 2, 3):
        for c in itertools.combinations(L, r):
            if abs(sum(c) - eje) <= max(200, 0.03 * eje): return 'ok', 'Longitud de pieza ' + ' + '.join(map(str, c)) + f' mm ≈ eje del modelo {eje} mm.'
    return 'revisar', f"Ninguna longitud de pieza ({', '.join(map(str, L))} mm) coincide con el eje del modelo ({eje} mm): vínculo por revisar."

antes_c = collections.Counter(m.get('coherencia') for m in mod['miembros'])
for m in mod['miembros']:
    if m.get('marcas') and any(mod['marcas'][k]['hoja'] in hojas_nuevas for k in m['marcas']):
        m['coherencia'], m['coherencia_txt'] = coherencia(m['marcas'], m['longitud_mm'])
despues_c = collections.Counter(m.get('coherencia') for m in mod['miembros'])
print('filas BOM', len(mod['lista_materiales']), f'· filas nuevas/cambiadas en H{H_INI}-H{H_FIN}:', cambios)
print('coherencia antes', dict(antes_c), 'después', dict(despues_c))
mod['meta']['nombre'] = 'Torre especial de retención tipo BE'
mod['meta']['supuestos'] = list(mod['meta'].get('supuestos', [])) + [
    f'Revisión nueva de las hojas DIS6054-H{H_INI}…H{H_FIN}: la geometría de las hojas (esquema TORN y ejes) es idéntica a la revisión anterior; se actualizaron la lista de materiales, los perfiles/longitudes de las marcas y la coherencia con el eje del modelo.']
json.dump(mod, open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
