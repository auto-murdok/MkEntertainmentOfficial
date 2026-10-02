#!/usr/bin/env python3
"""Build a kit-wide material library from the repo's real .mat files.

Resolves each Building_kit material's _BaseMap / _BumpMap / _MetallicGlossMap
GUIDs to texture paths via .meta files, classifies the ORM pack convention
(unity: R=metal/A=smooth vs gltf: B=metal/G=rough) by channel analysis, and
writes _blender/polish/matlib_building_kit.json keyed by material name.
"""
import json, os, re, subprocess, sys

REPO = os.path.expanduser('~/workspace/mk-entertainment')
MAT_DIR = os.path.join(REPO, 'Assets/ImportedContent/Building_kit/Materials')
OUT = os.path.join(REPO, '_blender/polish/matlib_building_kit.json')

def build_guid_index():
    idx = {}
    out = subprocess.run(['grep', '-r', '--include=*.meta', '-H', '-m1', '^guid: ',
                          os.path.join(REPO, 'Assets')],
                         capture_output=True, text=True).stdout
    for line in out.splitlines():
        path, _, guid = line.partition(':guid: ')
        guid = guid.strip()
        if guid:
            idx[guid] = os.path.relpath(path[:-5], REPO)  # strip '.meta'
    return idx

def parse_mat(path):
    """Return (name, {prop: guid}) for texture props with real textures."""
    text = open(path).read()
    name = re.search(r'm_Name: (\S+)', text).group(1)
    props = {}
    # entries look like: "- _BaseMap:\n        m_Texture: {fileID: 2800000, guid: XXXX, type: 3}"
    for m in re.finditer(r'- (_\w+):\s*\n\s*m_Texture: \{fileID: (\d+)(?:, guid: ([0-9a-f]{32}))?',
                         text):
        prop, fileid, guid = m.groups()
        if fileid != '0' and guid and prop not in props:
            props[prop] = guid
    return name, props

def classify_orm(path):
    """unity (R=metal/A=smooth) vs gltf (B=metal/G=rough) vs unknown."""
    out = subprocess.run(['identify', '-verbose', os.path.join(REPO, path)],
                         capture_output=True, text=True).stdout
    def stddev(ch):
        m = re.search(ch + r':\n(?:[^\n]*\n){0,4}?\s+standard deviation: ([0-9.e+-]+|-)', out)
        v = m.group(1) if m else '0'
        return float(v) if v != '-' else 0.0
    sd = {c: stddev(c) for c in ('Red', 'Green', 'Blue', 'Alpha')}
    if sd['Alpha'] > 5 and sd['Blue'] < 5:
        return 'unity'
    if sd['Blue'] > 5 and sd['Alpha'] < 5:
        return 'gltf'
    return 'unknown'

def main():
    guids = build_guid_index()
    print(f'indexed {len(guids)} guids')
    lib, warnings = {}, []
    for fn in sorted(os.listdir(MAT_DIR)):
        if not fn.endswith('.mat'):
            continue
        name, props = parse_mat(os.path.join(MAT_DIR, fn))
        entry = {}
        for prop, key in (('_BaseMap', 'albedo'), ('_BumpMap', 'normal'),
                          ('_MetallicGlossMap', 'orm')):
            g = props.get(prop)
            if not g:
                continue
            p = guids.get(g)
            if not p:
                warnings.append(f'{name}: {prop} guid {g} not found')
                continue
            entry[key] = p
        if 'orm' in entry:
            style = classify_orm(entry['orm'])
            entry['orm_style'] = style
            if style == 'unknown':
                warnings.append(f'{name}: orm pack {entry["orm"]} has ambiguous channels')
        else:
            entry['orm_style'] = 'none'
        lib[name] = entry
    json.dump(lib, open(OUT, 'w'), indent=1)
    print(f'wrote {OUT} with {len(lib)} materials')
    styles = {}
    for e in lib.values():
        styles[e.get('orm_style', '-')] = styles.get(e.get('orm_style', '-'), 0) + 1
    print('orm styles:', styles)
    for w in warnings:
        print('WARN:', w)

if __name__ == '__main__':
    main()
