import bpy,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Tools/IronRequiem/Reports/NewRobotAnimation/Reference';path=Path('C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-11-01 021348.mp4')
scene=bpy.context.scene;editor=scene.sequence_editor_create();movie=editor.strips.new_movie('AnimationReference',str(path),channel=1,frame_start=1)
fps=float(movie.fps);scene.render.fps=round(fps);scene.render.fps_base=round(fps)/fps
width=movie.elements[0].orig_width;height=movie.elements[0].orig_height;count=int(movie.frame_final_duration);duration=count/fps
scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.resolution_percentage=100;scene.render.use_sequencer=True;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
times=sorted(set([min(float(t),max(0,duration-.10)) for t in range(0,int(duration)+2,2)]+[min(.5,duration-.1),min(1,duration-.1)]))
metadata={'path':str(path),'sha256':hashlib.file_digest(path.open('rb'),'sha256').hexdigest(),'fps':fps,'frames':count,'duration':duration,'width':width,'height':height,'times':times}
(OUT/'metadata.json').write_text(json.dumps(metadata,indent=2));print(json.dumps(metadata),flush=True)
for t in times:
 scene.frame_set(1+round(t*fps));scene.render.filepath=str(OUT/f'frame-{t:06.2f}.png');bpy.ops.render.render(write_still=True)
print('REFERENCE_FRAMES_READY',flush=True)
