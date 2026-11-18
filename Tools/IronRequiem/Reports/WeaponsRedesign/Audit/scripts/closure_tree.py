"""Read-only: turn guid-closure-raw.json into a dependency tree with paths and relation kind
(variant = m_SourcePrefab at document root, nested = PrefabInstance inside, field = plain reference)."""
import json
import re
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[6]
audit = Path(__file__).resolve().parents[1]
raw = json.loads((audit / 'guid-closure-raw.json').read_text(encoding='utf-8-sig'))
seeds = {
    '60c069240a47c1248b8576ed5920ca5f': 'PistolModule_1000.fbx',
    '899ab1d0bf4636a41a5549499212bc3e': 'IR_PrimaryModule_v02.fbx',
    '48ac3df5d72f0eb44a98b3d277f045d0': 'T07_CannonClean.fbx',
    '3167e5df9a314d44a9213ef269e96bf4': 'T07_Cannon_PivotFixed.fbx',
}
guid_of = {}
for e in raw:
    f = e['file']
    if f.endswith('.prefab'):
        meta = (root / (f + '.meta')).read_text(encoding='utf-8')
        guid_of[f] = re.search(r'^guid: (\w+)', meta, re.M).group(1)
path_of = {g: p for p, g in guid_of.items()}
path_of.update(seeds)


def kind(f, g):
    t = (root / f).read_text(encoding='utf-8', errors='ignore')
    if not f.endswith('.prefab'):
        return 'scene' if f.endswith('.unity') else 'ref'
    # variant: the root PrefabInstance has m_TransformParent fileID 0 and SourcePrefab == g
    for m in re.finditer(r'--- !u!1001 &\d+\nPrefabInstance:(.*?)(?=\n--- )', t + '\n--- ', re.S):
        body = m.group(1)
        if f'm_SourcePrefab: {{fileID: 100100000, guid: {g}' in body:
            return 'variant' if 'm_TransformParent: {fileID: 0}' in body else 'nested'
    return 'field-ref'


edges = []
for e in raw:
    f = e['file']
    if f.endswith('.meta'):
        continue
    edges.append({'source': path_of.get(e['from'], e['from']), 'source_guid': e['from'], 'consumer': f,
                  'kind': kind(f, e['from'])})
(audit / 'guid-closure-tree.json').write_text(json.dumps(edges, indent=1), encoding='utf-8')
by = {}
for e in edges:
    by.setdefault(e['source'], []).append(e)


def walk(node, depth, seen):
    for e in sorted(by.get(node, []), key=lambda x: x['consumer']):
        c = e['consumer']
        print('  ' * depth + f"- [{e['kind']}] {c}")
        if c.endswith('.prefab') and c not in seen:
            seen.add(c)
            walk(c, depth + 1, seen)


for s in seeds.values():
    print(s)
    walk(s, 1, set())
