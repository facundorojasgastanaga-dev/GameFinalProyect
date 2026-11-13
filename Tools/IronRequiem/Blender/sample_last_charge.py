import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[3];out=root/'Tools/IronRequiem/Reports/CannonCharge'
scene=bpy.context.scene;movie=scene.sequence_editor_create().strips.new_movie('LastShot','C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-10-30 064107.mp4',channel=1,frame_start=1)
scene.render.fps=30;scene.render.resolution_x=movie.elements[0].orig_width;scene.render.resolution_y=movie.elements[0].orig_height;scene.render.resolution_percentage=100
scene.render.use_sequencer=True;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
for frame in range(18*30,23*30+1,3):
    scene.frame_set(1+frame);scene.render.filepath=str(out/f'last-{frame/30:05.2f}.png');bpy.ops.render.render(write_still=True)
