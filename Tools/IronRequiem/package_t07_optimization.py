"""Package verified optimized models, with no changes to source assets."""
import hashlib, json, shutil, tarfile, zipfile, sys
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
reports=ROOT/'Tools/IronRequiem/Reports/T07Optimization'
exports=ROOT/'Tools/IronRequiem/Exports'
folder=exports/'T07Optimized'
unity=json.loads((reports/'unity-final.json').read_text())
geometry=json.loads((reports/'geometry.json').read_text())
roundtrip=json.loads((reports/'roundtrip.json').read_text())
preservation=json.loads((reports/'preservation.json').read_text())
assert unity['success'] and roundtrip['success'] and preservation['success']
assert unity['trianglesByLod']==[lod['triangles'] for lod in geometry['lods']]
for name, count in [('play',1),('edit',2)]:
    node=ET.parse(reports/f'{name}.xml').getroot()
    assert node.attrib['result']=='Passed' and int(node.attrib['passed'])==count

package=exports/'T07_Common_Optimized.unitypackage'
with tarfile.open(package,'r:gz') as archive:
    pathnames={member.name.split('/')[0]:archive.extractfile(member).read().decode('utf-8').strip() for member in archive.getmembers() if member.name.endswith('/pathname')}
    names=set(pathnames.values())
    required=[unity['enemy'],unity['weapon'],unity['lab']]+[f'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD{i}.fbx' for i in range(3)]
    assert all(name in names for name in required), required
    assert 'Assets/FPS/Scenes/MainScene.unity' not in names, 'Do not ship an unrelated main scene override'
    package_info={'success':True,'package':str(package),'entries':len(names),'required_assets_present':True,'main_scene_excluded':True}
(reports/'package.json').write_text(json.dumps(package_info,indent=2),encoding='utf-8')
shutil.copyfile(ROOT/'Docs/IronRequiem/T07_OPTIMIZATION.md',folder/'README.md')
target=exports/'T07_Optimized_Models.zip'
assert not target.exists() or '--revise-draft' in sys.argv, 'Do not overwrite delivered archives'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for p in folder.iterdir():
        if p.is_file():archive.write(p,p.name)
    blend=ROOT/'Tools/IronRequiem/Blender/Working/T07Optimized/T07_Optimized.blend'
    archive.write(blend,'Editable/'+blend.name)
    for p in reports.glob('comparison-*.png'):archive.write(p,'Comparisons/'+p.name)
    for name in ('geometry.json','roundtrip.json','unity-final.json','preservation.json','runtime.json','play.xml','edit.xml'):
        archive.write(reports/name,'Verification/'+name)
with zipfile.ZipFile(target) as archive:
    assert archive.testzip() is None
print(json.dumps({'unitypackage':str(package),'unitypackage_bytes':package.stat().st_size,
                  'portable_zip':str(target),'portable_zip_bytes':target.stat().st_size,'package':package_info}))
