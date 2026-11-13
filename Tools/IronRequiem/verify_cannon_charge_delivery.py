"""Read-only verification against this task's snapshot and actual Unity Test Runner XML."""
from pathlib import Path
import hashlib,json,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Tools/IronRequiem/Reports/CannonCharge'
BACKUP=ROOT/'Tools/IronRequiem/Backups/20261030-cannon-charge'
baseline=json.loads((BACKUP/'baseline.json').read_text())
allowed={
 'Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab',
 'Assets/IronRequiem/Scripts/Weapons/CannonChargePresentation.cs',
 'Assets/IronRequiem/Scripts/Weapons/CannonChargeShot.cs',
 'Assets/IronRequiem/Tests/PlayMode/MechanicsBehaviorTests.cs',
 'Assets/IronRequiem/Tests/PlayMode/T07AnimationBehaviorTests.cs',
}
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
changed=[Path(p).relative_to(ROOT).as_posix() for p,h in baseline.items() if not Path(p).is_file() or sha(Path(p))!=h]
new=[p.relative_to(ROOT).as_posix() for p in (ROOT/'Assets').rglob('*') if p.is_file() and str(p) not in baseline]
missing=[p for p in new if not p.endswith('.meta') and not Path(str(ROOT/p)+'.meta').exists()]
audit=json.loads((ROOT/'Tools/IronRequiem/Reports/unity-audit.json').read_text(encoding='utf-8-sig'))
tests={}
for name in ['regression-play','regression-edit','play','edit']:
 path=REPORT/(name+'.xml')
 if path.exists():
  t=ET.parse(path).getroot();tests[name]={'result':t.get('result'),'passed':int(t.get('passed',0)),'failed':int(t.get('failed',0)),'start':t.get('start-time'),'end':t.get('end-time')}
result={'baseline_files':len(baseline),'changed_preexisting':changed,'unexpected_changes':sorted(set(changed)-allowed),'new_assets':new,'missing_meta':missing,
 'original_meta_unchanged':not any(p.endswith('.meta') for p in changed),'all_scenes_unchanged':not any(p.endswith('.unity') for p in changed),
 'packages_and_project_settings_unchanged':not any(p.startswith(('Packages/','ProjectSettings/')) for p in changed),
 'fps_production_scripts_unchanged':not any(p.startswith('Assets/FPS/Scripts/') for p in changed),
 'enemy_and_world_cannon_unchanged':not any(p.startswith(('Assets/IronRequiem/Prefabs/Enemies/','Assets/IronRequiem/Art/Enemies/')) for p in changed),
 'audit_utc':audit['utc'],'playing':audit['playing'],'scene_states':audit['scenes'],'compilation_errors':audit['compilationErrors'],'tests':tests}
result['all_checks_pass']=not result['unexpected_changes'] and not missing and not audit['playing'] and not audit['compilationErrors'] and all(not s['dirty'] and s['missingScripts']==0 for s in audit['scenes']) and len(tests)==4 and all(t['result']=='Passed' for t in tests.values())
(REPORT/'delivery-verification.json').write_text(json.dumps(result,indent=2))
print(json.dumps({k:v for k,v in result.items() if k!='new_assets'},indent=2))
