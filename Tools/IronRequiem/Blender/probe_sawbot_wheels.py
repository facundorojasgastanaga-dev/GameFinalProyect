import bpy,json,numpy as np
from pathlib import Path
root=Path.cwd();bpy.ops.wm.open_mainfile(filepath=str(root/'Tools/IronRequiem/Exports/IR_SawBot_Optimized_v01/IR_SawBot_Editable.blend'))
o=next(o for o in bpy.data.collections['LOD0_Editable'].objects if o.name=='Body_LOD0');p=np.array([o.matrix_world@v.co for v in o.data.vertices]);out=[]
for lo,hi in [(.15,.20),(.20,.23),(.23,.27),(.27,.30),(.30,.33),(.33,.40)]:
 q=p[(p[:,0]>lo)&(p[:,0]<hi)&(p[:,2]<.295)]
 out.append({'band':[lo,hi],'count':len(q),'min':q.min(axis=0).tolist(),'max':q.max(axis=0).tolist()})
(root/'Tools/IronRequiem/Reports/SawBotSustain/wheel-regions.json').write_text(json.dumps(out,indent=2))
