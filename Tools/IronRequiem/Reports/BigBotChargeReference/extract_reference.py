import bpy,json
from pathlib import Path
root=Path.cwd();out=root/'Tools/IronRequiem/Reports/BigBotChargeReference'
video=Path(r'C:/Users/gehbd/AppData/Local/Packages/Microsoft.ScreenSketch_8wekyb3d8bbwe/TempState/Recordings/20261102-2202-46.5756978.mp4')
scene=bpy.context.scene
movie=scene.sequence_editor_create().strips.new_movie('Reference',str(video),channel=1,frame_start=1)
fps=float(movie.fps);count=int(movie.frame_final_duration)
scene.render.fps=round(fps);scene.render.fps_base=round(fps)/fps
scene.render.resolution_x=movie.elements[0].orig_width;scene.render.resolution_y=movie.elements[0].orig_height;scene.render.resolution_percentage=100
scene.render.use_sequencer=True;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
data={'fps':fps,'frames':count,'duration':count/fps,'width':scene.render.resolution_x,'height':scene.render.resolution_y,'samples':[]}
for i,fraction in enumerate([0,.125,.25,.375,.5,.625,.75,.875,.99]):
 f=1+int((count-2)*fraction);scene.frame_set(f);scene.render.filepath=str(out/f'ref-{i}.png');bpy.ops.render.render(write_still=True)
 data['samples'].append({'file':f'ref-{i}.png','seconds':(f-1)/fps})
(out/'video.json').write_text(json.dumps(data,indent=2))
