"""Bake authored scorch/scratch marks into NEW albedo copies using actual surface UVs."""
import json,numpy as np
from PIL import Image
from pathlib import Path
r=Path(__file__).resolve().parents[2];data=json.loads((r/'Tools/IronRequiem/Reports/T07Destruction/damage-uv.json').read_text());size=1024
source=Image.open(r/'Assets/IronRequiem/Art/Enemies/T07New/Textures/T07_BaseColor.png').convert('RGB').resize((size,size),Image.Resampling.LANCZOS)
base=np.asarray(source).astype(float);mask=np.zeros((size,size),dtype=float);scratches=np.zeros_like(mask);points=np.array(data['points'])
for t in data['triangles']:
 uv=np.array(t['uv'])*[size-1,-(size-1)]+[0,size-1];co=np.array(t['co']);low=np.maximum(np.floor(uv.min(0)).astype(int),0);high=np.minimum(np.ceil(uv.max(0)).astype(int),size-1)
 if np.any(high<low):continue
 a,b,c=uv;den=np.cross(b-a,c-a)
 if abs(den)<1e-8:continue
 xx,yy=np.meshgrid(np.arange(low[0],high[0]+1),np.arange(low[1],high[1]+1));q=np.stack([xx,yy],-1)
 wb=((q-a)[...,0]*(c-a)[1]-(q-a)[...,1]*(c-a)[0])/den;wc=((b-a)[0]*(q-a)[...,1]-(b-a)[1]*(q-a)[...,0])/den;wa=1-wb-wc;inside=(wa>=0)&(wb>=0)&(wc>=0)
 p=wa[...,None]*co[0]+wb[...,None]*co[1]+wc[...,None]*co[2];distance=np.linalg.norm(p[...,None,:]-points,axis=-1).min(-1)
 burn=np.exp(-(distance/.038)**2)*inside
 scratch=((np.sin(p[...,0]*470+p[...,2]*190)>.94)&(distance<.048)&inside)*.8
 block=mask[low[1]:high[1]+1,low[0]:high[0]+1];np.maximum(block,burn,out=block)
 block=scratches[low[1]:high[1]+1,low[0]:high[0]+1];np.maximum(block,scratch,out=block)
out=r/'Assets/IronRequiem/Art/Enemies/T07Destructible';out.mkdir(parents=True,exist_ok=True)
for i,strength in enumerate([.55,.78,.94]):
 alpha=np.clip(mask*strength,0,.94)[...,None];color=base*(1-alpha)+np.array([18,15,12])*alpha
 edge=(scratches*(.35+i*.12))[...,None];color=color*(1-edge)+np.array([165,155,130])*edge
 Image.fromarray(np.clip(color,0,255).astype('uint8')).save(out/f'DamageAlbedo_{i+1}.png')
print('Created three shared 1024x1024 damage albedos; original unchanged; stamped pixels:',int((mask>.1).sum()))
