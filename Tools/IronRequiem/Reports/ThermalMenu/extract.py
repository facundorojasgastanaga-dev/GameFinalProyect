import bpy,json
from pathlib import Path
out=Path.cwd()/'Tools/IronRequiem/Reports/ThermalMenu'
for n,name in enumerate(['113415','113601']):
 s=bpy.context.scene
 s.sequence_editor_clear()
 v=s.sequence_editor_create().strips.new_movie('Reference',f'C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-11-06 {name}.mp4',channel=1,frame_start=1)
 fps=float(v.fps); count=int(v.frame_final_duration)
 s.render.fps=round(fps);s.render.fps_base=round(fps)/fps
 s.render.resolution_x=v.elements[0].orig_width;s.render.resolution_y=v.elements[0].orig_height;s.render.resolution_percentage=55
 s.render.use_sequencer=True;s.render.image_settings.file_format='PNG';s.view_settings.view_transform='Standard'
 (out/f'video{n}.json').write_text(json.dumps({'fps':fps,'frames':count,'seconds':count/fps,'width':s.render.resolution_x,'height':s.render.resolution_y}))
 for i,f in enumerate([.05,.35,.65,.95]):
  s.frame_set(1+int((count-2)*f));s.render.filepath=str(out/f'video{n}-{i}.png');bpy.ops.render.render(write_still=True)
