import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[3]
out=root/'Tools/IronRequiem/Reports/CannonBurr'
scene=bpy.context.scene
movie=scene.sequence_editor_create().strips.new_movie('UserReference',str(out/'reference.mp4'),channel=1,frame_start=1)
fps=float(movie.fps);count=int(movie.frame_final_duration)
scene.render.fps=round(fps);scene.render.fps_base=round(fps)/fps
scene.render.resolution_x=movie.elements[0].orig_width;scene.render.resolution_y=movie.elements[0].orig_height;scene.render.resolution_percentage=100
scene.render.use_sequencer=True;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
metadata={'fps':fps,'frames':count,'duration':count/fps,'width':scene.render.resolution_x,'height':scene.render.resolution_y,'samples':[]}
for i,fraction in enumerate([.04,.2,.4,.6,.8,.96]):
    frame=1+int((count-2)*fraction);scene.frame_set(frame);scene.render.filepath=str(out/f'frame-{i}.png');bpy.ops.render.render(write_still=True)
    metadata['samples'].append({'file':f'frame-{i}.png','seconds':(frame-1)/fps})
(out/'video.json').write_text(json.dumps(metadata,indent=2))
