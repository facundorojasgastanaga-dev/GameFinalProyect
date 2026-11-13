"""Hash snapshot/verification, scoped to this Unity project and the two external sources."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT/'Tools/IronRequiem/Backups/20261029-t07-optimization/baseline.json'
REPORT = ROOT/'Tools/IronRequiem/Reports/T07Optimization/preservation.json'

def digest(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()

parser = argparse.ArgumentParser()
parser.add_argument('operation', choices=['snapshot','verify'])
args = parser.parse_args()
if args.operation == 'snapshot':
    assert not BASE.exists(), 'Do not replace the baseline'
    paths = [p for folder in ('Assets','Packages','ProjectSettings') for p in (ROOT/folder).rglob('*') if p.is_file()]
    paths += [Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07.fbx'), Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07(Texture).glb')]
    with ThreadPoolExecutor(max_workers=8) as pool:
        result = dict(zip(map(str,paths),pool.map(digest,paths)))
    BASE.parent.mkdir(parents=True,exist_ok=True)
    BASE.write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({'snapshot_files':len(result),'baseline':str(BASE)}))
else:
    baseline = json.loads(BASE.read_text(encoding='utf-8'))
    changed=[]; missing=[]
    for name, expected in baseline.items():
        p=Path(name)
        if not p.exists(): missing.append(name)
        elif digest(p)!=expected: changed.append(name)
    result={'protected_files':len(baseline),'changed':changed,'missing':missing,'success':not changed and not missing}
    REPORT.parent.mkdir(parents=True,exist_ok=True)
    REPORT.write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))
