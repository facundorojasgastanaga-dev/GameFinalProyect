"""Verify the final task without changing sources or accepting unexpected asset edits."""
import hashlib,json,xml.etree.ElementTree as ET
from datetime import datetime,timezone
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Tools/IronRequiem/Reports/T07Animation'
BACKUP=ROOT/'Tools/IronRequiem/Backups/20261030-t07-animation-vfx'
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
allowed={
 'Assets/IronRequiem/Editor/IronRequiem.Editor.asmdef',
 'Assets/IronRequiem/Scripts/IronRequiem.Runtime.asmdef',
 'Assets/IronRequiem/Scripts/Weapons/CannonExplosion.cs',
 'Assets/IronRequiem/Scenes/T07OptimizationLab.unity',
 'Assets/IronRequiem/Scenes/CharacterLab.unity',
 'Assets/IronRequiem/Prefabs/Enemies/T07_Common_Optimized.prefab',
 'Assets/IronRequiem/Tests/PlayMode/CharacterBehaviorTests.cs',
}
baseline=json.loads((BACKUP/'baseline.json').read_text())
changes=[]
for name,expected in baseline.items():
    path=Path(name)
    if not path.exists() or sha(path)!=expected:
        relative=str(path.relative_to(ROOT)).replace('\\','/') if path.is_relative_to(ROOT) else str(path)
        changes.append({'path':relative,'expected':relative in allowed,'exists':path.exists()})
tests=[]
for prefix in ['', 'regression-']:
    for mode in ['play','edit']:
        path=REPORT/f'{prefix}{mode}.xml'
        if not path.exists():tests.append({'report':str(path),'success':False});continue
        run=ET.parse(path).getroot()
        cases=run.findall('.//test-case')
        tests.append({'report':path.name,'total':int(run.attrib['total']),'passed':int(run.attrib['passed']),
                      'failed':int(run.attrib['failed']),'success':run.attrib['result']=='Passed' and all(c.attrib['result']=='Passed' for c in cases)})
audit=json.loads((ROOT/'Tools/IronRequiem/Reports/unity-audit.json').read_text(encoding='utf-8-sig'))
missing_meta=[str(p.relative_to(ROOT)) for p in (ROOT/'Assets/IronRequiem').rglob('*') if p.is_file() and p.suffix!='.meta' and not Path(str(p)+'.meta').exists()]
result={'utc':datetime.now(timezone.utc).isoformat(),'protected_files':len(baseline),'changes':changes,
 'unexpected_changes':[c for c in changes if not c['expected']],'tests':tests,'missing_metas':missing_meta,
 'audit_utc':audit['utc'],'playing':audit['playing'],'compilation_errors':audit['compilationErrors'],'scenes':audit['scenes'],
 'fps_gain_measured':False}
result['all_checks_pass']=not result['unexpected_changes'] and not missing_meta and not audit['playing'] and not audit['compilationErrors'] and all(t['success'] for t in tests) and all(not s['dirty'] and s['missingScripts']==0 for s in audit['scenes'])
(REPORT/'delivery-verification.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2));raise SystemExit(0 if result['all_checks_pass'] else 1)
