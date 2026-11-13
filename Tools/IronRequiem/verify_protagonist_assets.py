"""Preservation audit. This task prepares files outside Assets and never drives Unity."""
import argparse,hashlib,json
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'Tools/IronRequiem/Backups/20261031-protagonist-optimization/baseline.json'
SOURCES=[Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/PrincipalBot/71443cd84dd6034935129fc02c5facfa.fbx'),Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/PrincipalBot/777659ccac8b343c8e38039f454a2621.glb')]
def digest(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
parser=argparse.ArgumentParser();parser.add_argument('operation',choices=['snapshot','verify']);args=parser.parse_args()
if args.operation=='snapshot':
    assert not BASE.exists()
    paths=[p for name in ('Assets','Packages','ProjectSettings') for p in (ROOT/name).rglob('*') if p.is_file()]+SOURCES
    with ThreadPoolExecutor(max_workers=8) as pool:data=dict(zip(map(str,paths),pool.map(digest,paths)))
    BASE.parent.mkdir(parents=True,exist_ok=True);BASE.write_text(json.dumps(data,indent=2),encoding='utf-8')
    print(json.dumps({'protected_files':len(data)}))
else:
    data=json.loads(BASE.read_text(encoding='utf-8'));changed=[];missing=[]
    for name,before in data.items():
        p=Path(name)
        if not p.exists():missing.append(name)
        elif digest(p)!=before:changed.append(name)
    new_assets=[str(p.relative_to(ROOT)) for p in (ROOT/'Assets').rglob('*') if p.is_file() and str(p) not in data]
    report={'success':not changed and not missing and not new_assets,'protected_files':len(data),'changed':changed,'missing':missing,'new_assets':new_assets,'unity_import_performed':False}
    folder=ROOT/'Tools/IronRequiem/Reports/ProtagonistOptimization';folder.mkdir(parents=True,exist_ok=True)
    (folder/'preservation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
