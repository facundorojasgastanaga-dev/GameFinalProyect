"""Scoped snapshot and integrity report. Does not restore or modify source assets."""
import argparse, hashlib, json, shutil
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
ROOT=Path(__file__).resolve().parents[2]
BACKUP=ROOT/'Tools/IronRequiem/Backups/20261030-t07-animation-vfx'
REPORT=ROOT/'Tools/IronRequiem/Reports/T07Animation'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
args=argparse.ArgumentParser();args.add_argument('operation',choices=['snapshot','verify']);args=args.parse_args()
if args.operation=='snapshot':
    assert not (BACKUP/'baseline.json').exists(),'Never overwrite baseline'
    paths=[p for d in ['Assets','Packages','ProjectSettings'] for p in (ROOT/d).rglob('*') if p.is_file()]
    paths += [Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07.fbx'),Path('C:/Users/gehbd/OneDrive/Escritorio/Juegos GJ/vistasROBOT/T-07(Texture).glb')]
    BACKUP.mkdir(parents=True,exist_ok=True)
    with ThreadPoolExecutor(max_workers=8) as pool:baseline=dict(zip(map(str,paths),pool.map(sha,paths)))
    (BACKUP/'baseline.json').write_text(json.dumps(baseline,indent=2))
    for folder in ['Assets/IronRequiem','Assets/FPS/Scripts','Assets/FPS/Scenes','Packages','ProjectSettings','Docs/IronRequiem']:
        shutil.copytree(ROOT/folder,BACKUP/folder,dirs_exist_ok=True)
    print(json.dumps({'baseline_files':len(paths),'backup':str(BACKUP)}))
else:
    baseline=json.loads((BACKUP/'baseline.json').read_text())
    changed=[n for n,h in baseline.items() if not Path(n).exists() or sha(Path(n))!=h]
    REPORT.mkdir(parents=True,exist_ok=True)
    result={'baseline_files':len(baseline),'changed':changed}
    (REPORT/'preservation.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result))
