"""Decode the supplied reference and inspect cannon geometry. No source writes."""
import bpy,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Tools/IronRequiem/Reports/CannonCharge'
OUT.mkdir(parents=True,exist_ok=True)
video=Path('C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-10-30 064107.mp4')
scene=bpy.context.scene;editor=scene.sequence_editor_create()
movie=editor.strips.new_movie('Reference',str(video),channel=1,frame_start=1)
fps=float(movie.fps);count=int(movie.frame_final_duration);duration=count/fps
scene.render.fps=round(fps);scene.render.fps_base=round(fps)/fps
scene.render.resolution_x=movie.elements[0].orig_width;scene.render.resolution_y=movie.elements[0].orig_height;scene.render.resolution_percentage=100
scene.render.use_sequencer=True;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
samples=[]
for t in sorted(set([min(i*2,duration-2/fps) for i in range(int(duration/2)+1)]+[max(0,duration-1)])):
    scene.frame_set(1+int(t*fps));path=OUT/f'reference-{t:06.2f}.png';scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
    samples.append({'seconds':t,'file':path.name})
(OUT/'reference-metadata.json').write_text(json.dumps({'source':str(video),'sha256':hashlib.file_digest(video.open('rb'),'sha256').hexdigest(),'fps':fps,'frames':count,'duration':duration,'width':movie.elements[0].orig_width,'height':movie.elements[0].orig_height,'samples':samples},indent=2))
scene.sequence_editor_clear()
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD0.fbx'),use_anim=False)
obj=bpy.data.objects['CannonModule_LOD0'];vertices=[v.co[:] for v in obj.data.vertices]
low=min(v[2] for v in vertices);high=max(v[2] for v in vertices)
sections=[]
for fraction in [.02,.05,.1,.2,.5,.9]:
    points=[v for v in vertices if v[2]<=low+(high-low)*fraction]
    sections.append({'fraction_from_front':fraction,'count':len(points),'bounds_min':[min(p[i] for p in points) for i in range(3)],'bounds_max':[max(p[i] for p in points) for i in range(3)],'center':[sum(p[i] for p in points)/len(points) for i in range(3)]})
(OUT/'cannon-geometry.json').write_text(json.dumps({'name':obj.name,'matrix':[list(r) for r in obj.matrix_world],'vertices':vertices,'sections':sections},indent=2))
print('CANNON_REFERENCE_INSPECTED',duration,fps)
