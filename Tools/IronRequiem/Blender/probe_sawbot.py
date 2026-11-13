import bpy,json,sys
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[3];bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/IronRequiem/Blender/Working/SawBot/sawbot_source.blend'))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH');p=np.array([o.matrix_world@v.co for v in o.data.vertices]);out={}
for sign in [-1,1]:
 rows=[]
 for xmin,xmax in [(.28,.32),(.32,.35),(.35,.38),(.38,.41),(.41,.44),(.44,.47),(.47,.50),(.50,.55)]:
  a=p[(p[:,0]*sign>=xmin)&(p[:,0]*sign<xmax)&(p[:,2]<.37)]
  rows.append({'x':[xmin,xmax],'count':len(a),'min':a.min(axis=0).tolist() if len(a) else [],'max':a.max(axis=0).tolist() if len(a) else [],'median':np.median(a,axis=0).tolist() if len(a) else []})
 out[str(sign)]=rows
print(json.dumps(out,indent=2));(ROOT/'Tools/IronRequiem/Reports/SawBot/blade-regions.json').write_text(json.dumps(out,indent=2))