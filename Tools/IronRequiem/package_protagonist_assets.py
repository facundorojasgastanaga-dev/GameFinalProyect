"""Package only offline protagonist assets and their handoff instructions."""
from pathlib import Path
import hashlib,json,shutil,zipfile
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Tools/IronRequiem/Exports/IR_Protagonist_Optimized_v01'
REPORTS=ROOT/'Tools/IronRequiem/Reports/ProtagonistOptimization'
audit=json.loads((REPORTS/'preservation.json').read_text())
roundtrip=json.loads((REPORTS/'roundtrip.json').read_text())
geometry=json.loads((REPORTS/'geometry.json').read_text())
assert audit['success'] and roundtrip['success']
assert not audit['new_assets'] and not audit['unity_import_performed']
verification=OUT/'Verification';verification.mkdir(exist_ok=True)
for name in ('fbx-inspection.json','glb-inspection.json','reduction-probe.json','geometry.json','roundtrip.json','preservation.json'):
    shutil.copy2(REPORTS/name,verification/name)
metal=np.asarray(Image.open(OUT/'Textures/IR_Protagonist_Metallic.png').convert('RGB'),dtype=np.int16)[:,:,0]
rough=np.asarray(Image.open(OUT/'Textures/IR_Protagonist_Roughness.png').convert('RGB'),dtype=np.int16)[:,:,0]
packed=np.asarray(Image.open(OUT/'Textures/IR_Protagonist_MetalSmooth.png').convert('RGBA'),dtype=np.int16)
assert packed.shape==(2048,2048,4)
metal_error=int(np.abs(packed[:,:,0]-metal).max())
smooth_error=int(np.abs(packed[:,:,3]-(255-rough)).max())
assert metal_error<=1 and smooth_error<=1,(metal_error,smooth_error)
material_check={'success':True,'metallic_r_max_error_8bit':metal_error,'smoothness_a_max_error_8bit':smooth_error}
(verification/'urp-channels.json').write_text(json.dumps(material_check,indent=2),encoding='utf-8')
paths=[p for p in OUT.rglob('*') if p.is_file() and p.name!='file-manifest.json']
manifest=[]
for p in sorted(paths):
    with p.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    manifest.append({'file':p.relative_to(OUT).as_posix(),'bytes':p.stat().st_size,'sha256':digest})
(OUT/'file-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
target=OUT.with_suffix('.zip')
assert not target.exists(),'Never overwrite a delivered archive'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for p in sorted(OUT.rglob('*')):
        if p.is_file():archive.write(p,p.relative_to(OUT).as_posix())
with zipfile.ZipFile(target) as archive:
    assert archive.testzip() is None
    assert len([n for n in archive.namelist() if n.endswith('.fbx')])==4
    assert len([n for n in archive.namelist() if n.endswith('.glb')])==4
print(json.dumps({'zip':str(target),'bytes':target.stat().st_size,'lod_triangles':[d['triangles'] for d in geometry['lods']],
                  'fps_triangles':geometry['fps_triangles'],'protected_files':audit['protected_files'],'unity_imported':False,'urp_channels':material_check}))
