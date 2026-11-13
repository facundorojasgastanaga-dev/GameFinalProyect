import bpy,numpy as np,json
from pathlib import Path
R=Path(__file__).resolve().parents[3]/'Tools/IronRequiem/Reports/NewRobotAnimation/Reference';s=bpy.context.scene;m=s.sequence_editor_create().strips.new_movie('Ref','C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-11-01 021348.mp4',channel=1,frame_start=1)
s.render.fps=30;s.render.resolution_x=442;s.render.resolution_y=354;s.render.resolution_percentage=100;s.render.use_sequencer=True;s.render.image_settings.file_format='PNG';s.view_settings.view_transform='Standard'
canvas=np.zeros((354*3,442*4,4),dtype=np.float32)
for i in range(12):
 t=i/6;s.frame_set(1+round(t*30));path=R/f'gait-{i:02}.png';s.render.filepath=str(path);bpy.ops.render.render(write_still=True);im=bpy.data.images.load(str(path));a=np.empty(354*442*4,dtype=np.float32);im.pixels.foreach_get(a);row=i//4;col=i%4;canvas[(2-row)*354:(3-row)*354,col*442:(col+1)*442]=a.reshape(354,442,4)
im=bpy.data.images.new('MotionSamples',width=442*4,height=354*3);im.pixels.foreach_set(canvas.ravel());im.filepath_raw=str(R/'gait-sheet.png');im.file_format='PNG';im.save()
