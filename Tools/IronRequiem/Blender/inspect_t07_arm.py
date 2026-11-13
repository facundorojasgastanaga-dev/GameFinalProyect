import bpy, json
from pathlib import Path
r=Path(__file__).resolve().parents[3]
bpy.ops.wm.open_mainfile(filepath=str(r/'Tools/IronRequiem/Blender/Working/T07Destructible/T07_ArmorRig.blend'))
rig=bpy.data.objects['T07Rig']
print('BONES',json.dumps([{'name':b.name,'head':list(b.head_local),'tail':list(b.tail_local)} for b in rig.data.bones]))
for i in range(3):
 o=bpy.data.objects['Body_LOD'+str(i)];groups={g.index:g.name for g in o.vertex_groups};record={}
 for v in o.data.vertices:
  for w in v.groups:
   if w.weight>.5:
    name=groups[w.group]
    if 'Arm_L' in name or 'Hand_L' in name:
     p=o.matrix_world@v.co
     a=record.setdefault(name,{'count':0,'min':[99,99,99],'max':[-99,-99,-99]});a['count']+=1
     for j in range(3):a['min'][j]=min(a['min'][j],p[j]);a['max'][j]=max(a['max'][j],p[j])
 print('ARM',i,json.dumps(record))
