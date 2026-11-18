"""Read-only YAML dumper for Unity prefabs: GameObjects, transforms, MonoBehaviours (resolved class names),
nested prefab instances and overrides that touch selected properties. Usage: python -I dump_prefab.py <prefab> [filter-regex]"""
import re
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[6]
p = root / sys.argv[1]
flt = re.compile(sys.argv[2]) if len(sys.argv) > 2 else None
t = p.read_text(encoding='utf-8', errors='ignore')
docs = re.split(r'\n(?=--- !u!)', t)
_script_cache = {}


def script_name(guid):
    if guid in _script_cache:
        return _script_cache[guid]
    r = subprocess.run(['git', 'grep', '-l', '--untracked', '-e', f'guid: {guid}', '--', '*.cs.meta'],
                       cwd=root, capture_output=True, text=True)
    name = Path(r.stdout.strip().splitlines()[0]).name[:-8] if r.stdout.strip() else guid
    if name == guid:
        r = subprocess.run(['git', 'grep', '-l', '--untracked', '-e', f'guid: {guid}', '--', '*.meta'],
                           cwd=root, capture_output=True, text=True)
        name = (Path(r.stdout.strip().splitlines()[0]).name[:-5] if r.stdout.strip() else guid)
    _script_cache[guid] = name
    return name


objs = {}
for d in docs:
    m = re.match(r'--- !u!(\d+) &(-?\d+)( stripped)?\n(\w+):', d)
    if not m:
        continue
    cls, fid, stripped, typ = m.group(1), m.group(2), m.group(3), m.group(4)
    objs[fid] = (typ, d, bool(stripped))

go_name = {}
for fid, (typ, d, s) in objs.items():
    if typ == 'GameObject':
        n = re.search(r'm_Name: (.*)', d)
        go_name[fid] = n.group(1) if n else '?'

print('##', sys.argv[1])
for fid, (typ, d, s) in objs.items():
    if typ == 'PrefabInstance':
        src = re.search(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)', d).group(1)
        par = re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', d).group(1)
        print(f'PrefabInstance {fid} source={script_name(src)} parentTransform={par}')
        for mm in re.finditer(r'- target: \{fileID: (-?\d+), guid: \w+,\s*type: \d\}\s*propertyPath: (.*?)\n\s*value: (.*?)\n\s*objectReference: (\{.*?\})', d, re.S):
            prop = mm.group(2)
            if flt is None or flt.search(prop) or flt.search(mm.group(3)):
                ref = mm.group(4)
                g = re.search(r'guid: (\w+)', ref)
                ref = script_name(g.group(1)) if g else ref
                print(f'   ov {mm.group(1)} {prop} = {mm.group(3).strip()} {ref if ref != "{fileID: 0}" else ""}')
        for mm in re.finditer(r'addedObject: \{fileID: (-?\d+)\}', d):
            pass
for fid, (typ, d, s) in objs.items():
    if typ == 'GameObject' and not s:
        print(f'GO {fid} "{go_name[fid]}" active={re.search(r"m_IsActive: (\d)", d).group(1)}')
    elif typ == 'Transform' and not s:
        g = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', d).group(1)
        _g = lambda k: (re.search(k + r': (\{.*?\})', d, re.S) or re.search(k + r': (.*)', d)).group(1).replace('\n', ' ')
        pos = _g('m_LocalPosition')
        rot = _g('m_LocalRotation')
        sc = _g('m_LocalScale')
        fa = re.search(r'm_Father: \{fileID: (-?\d+)\}', d).group(1)
        print(f'  TR {fid} of "{go_name.get(g, g)}" pos={pos} rot={rot} scale={sc} father={fa}')
    elif typ == 'MonoBehaviour' and not s:
        g = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', d).group(1)
        sg = re.search(r'm_Script: \{fileID: \d+, guid: (\w+)', d)
        name = script_name(sg.group(1)) if sg else '?'
        body = d.split('m_EditorClassIdentifier:', 1)[-1]
        lines = [l for l in body.splitlines()[1:] if l.strip()]
        if flt is not None:
            lines = [l for l in lines if flt.search(l)]
        print(f'  MB {name} on "{go_name.get(g, g)}"')
        for l in lines[:60]:
            print('     ' + l.strip()[:200])
    elif typ in ('MeshFilter', 'SkinnedMeshRenderer', 'MeshRenderer') and not s:
        g = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', d).group(1)
        mesh = re.search(r'm_Mesh: \{fileID: (-?\d+)(?:, guid: (\w+))?', d)
        en = re.search(r'm_Enabled: (\d)', d)
        mats = re.findall(r'- \{fileID: 2100000, guid: (\w+)', d)
        extra = ''
        if mesh and mesh.group(2):
            extra = f' mesh={script_name(mesh.group(2))}:{mesh.group(1)}'
        if mats:
            extra += ' mats=' + ','.join(script_name(x) for x in mats)
        print(f'  {typ} on "{go_name.get(g, g)}" enabled={en.group(1) if en else "-"}{extra}')
