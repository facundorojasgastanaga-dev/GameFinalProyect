import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[3]
scene=bpy.context.scene;editor=scene.sequence_editor_create()
movie=editor.strips.new_movie('Reference','C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-10-30 002636.mp4',channel=1,frame_start=1)
scene.render.fps=30;scene.render.resolution_x=movie.elements[0].orig_width;scene.render.resolution_y=movie.elements[0].orig_height;scene.render.resolution_percentage=100
scene.render.use_sequencer=True;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
for t in [30,35,40,45,50,55]:
    scene.frame_set(1+round(t*30));scene.render.filepath=str(root/f'Tools/IronRequiem/Reports/T07Animation/reference-tail-{t}.png');bpy.ops.render.render(write_still=True)
