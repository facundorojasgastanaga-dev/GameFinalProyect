import hashlib, json, shutil
from pathlib import Path
root = Path(__file__).resolve().parents[2]
backup = root / 'Tools/IronRequiem/Backups/20261102-native-weapon-clarity'
backup.mkdir(parents=True, exist_ok=True)
paths = set()
for folder in ['Assets/FPS', 'Assets/Rendering', 'ProjectSettings', 'Packages']:
    paths.update(p for p in (root / folder).rglob('*') if p.is_file())
paths.update((root / 'Assets/IronRequiem').rglob('*.unity'))
for rel in ['Assets/DefaultVolumeProfile.asset', 'Assets/IronRequiem/Art/Rendering/CombatClarity.asset',
            'Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_NativeVisual_v06.prefab',
            'Assets/IronRequiem/Prefabs/Player/Player_NativeVisual_v06.prefab']:
    paths.add(root / rel)
snapshot = backup / 'baseline.json'
if not snapshot.exists():
    data = {}
    for path in sorted(paths):
        rel = path.relative_to(root).as_posix()
        data[rel] = hashlib.sha256(path.read_bytes()).hexdigest()
        if rel.endswith('.prefab') and 'NativeVisual_v06' in rel or rel in [
            'Assets/DefaultVolumeProfile.asset', 'Assets/IronRequiem/Art/Rendering/CombatClarity.asset',
            'Assets/Rendering/PostProcessingProfile.asset']:
            dest = backup / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, dest)
    snapshot.write_text(json.dumps(data, indent=2), encoding='utf-8')
    print('Protected baseline:', len(data), 'files')
else:
    data = json.loads(snapshot.read_text(encoding='utf-8'))
    changed = [p for p, digest in data.items() if not (root/p).exists() or hashlib.sha256((root/p).read_bytes()).hexdigest() != digest]
    allowed = {'Assets/DefaultVolumeProfile.asset', 'Assets/IronRequiem/Art/Rendering/CombatClarity.asset',
               'Assets/Rendering/PostProcessingProfile.asset',
               'Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_NativeVisual_v06.prefab',
               'Assets/IronRequiem/Prefabs/Player/Player_NativeVisual_v06.prefab'}
    report = {'protected_files': len(data), 'changed': changed,
              'unexpected_changes': [p for p in changed if p not in allowed],
              'all_scene_files_unchanged': not any(p.endswith('.unity') for p in changed),
              'template_assets_unchanged': not any(p.startswith('Assets/FPS/') for p in changed),
              'packages_and_project_settings_unchanged': not any(p.startswith(('Packages/', 'ProjectSettings/')) for p in changed)}
    destination = root / 'Tools/IronRequiem/Reports/NativeWeaponRepair/preservation.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))
    assert not report['unexpected_changes'], 'Unexpected protected file changes'
