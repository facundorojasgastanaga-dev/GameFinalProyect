import hashlib, json, shutil
from pathlib import Path
root = Path(__file__).resolve().parents[2]
backup = root / 'Tools/IronRequiem/Backups/20261102-modular-arm-pickup'
paths = {p for folder in ['Assets/FPS', 'Assets/IronRequiem', 'Packages', 'ProjectSettings']
         for p in (root/folder).rglob('*') if p.is_file()}
snapshot = backup / 'baseline.json'
if not snapshot.exists():
    backup.mkdir(parents=True, exist_ok=True)
    data = {}
    targets = ['Assets/IronRequiem/Scripts/AI/BigBotDestruction.cs',
               'Assets/IronRequiem/Scripts/AI/BigBotCannonDebris.cs',
               'Assets/IronRequiem/Scripts/Player/ProtagonistHandsPresentation.cs',
               'Assets/IronRequiem/Prefabs/Enemies/BigBot_Destructible_v01.prefab']
    for path in paths:
        rel = path.relative_to(root).as_posix()
        data[rel] = hashlib.sha256(path.read_bytes()).hexdigest()
        if rel in targets:
            dest=backup/rel; dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(path, dest)
    snapshot.write_text(json.dumps(data, indent=2), encoding='utf-8')
    print('Protected:',len(data))
else:
    data=json.loads(snapshot.read_text(encoding='utf-8'))
    changed=[p for p,h in data.items() if not (root/p).exists() or hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
    report={'protected':len(data),'changed':sorted(changed),
            'scenes_unchanged':not any(p.endswith('.unity') for p in changed),
            'template_unchanged':not any(p.startswith('Assets/FPS/') for p in changed),
            'configuration_unchanged':not any(p.startswith(('Packages/','ProjectSettings/')) for p in changed)}
    out=root/'Tools/IronRequiem/Reports/ModularArm'; out.mkdir(parents=True,exist_ok=True)
    (out/'preservation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report,indent=2))
