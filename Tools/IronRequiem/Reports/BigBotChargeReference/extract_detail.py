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
for i,fraction in enumerate([.15/7.3,.35/7.3,.55/7.3,.75/7.3,4.8/7.3,5.0/7.3,5.1/7.3,5.2/7.3,5.3/7.3]):
 f=1+int((count-2)*fraction);scene.frame_set(f);scene.render.filepath=str(out/f'detail-{i}.png');bpy.ops.render.render(write_still=True)
 data['samples'].append({'file':f'detail-{i}.png','seconds':(f-1)/fps})
(out/'detail.json').write_text(json.dumps(data,indent=2))
