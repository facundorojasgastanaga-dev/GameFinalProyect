"""Read-only model inspection and local VSE frame extraction. Sources are never saved."""
import bpy, json, sys, hashlib
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Tools/IronRequiem/Reports/T07Animation'
OUT.mkdir(parents=True,exist_ok=True)
videos=[Path('C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-10-30 002636.mp4'),
        Path('C:/Users/gehbd/Videos/Screen Recordings/Screen Recording 2026-10-30 003005.mp4')]
metadata=[]
for i,path in enumerate(videos):
    scene=bpy.context.scene
    if scene.sequence_editor: scene.sequence_editor_clear()
    editor=scene.sequence_editor_create()
    movie=editor.strips.new_movie('Reference',str(path),channel=1,frame_start=1)
    scene.render.resolution_x=movie.elements[0].orig_width
    scene.render.resolution_y=movie.elements[0].orig_height
    scene.render.resolution_percentage=50
    scene.render.image_settings.file_format='PNG'
    scene.render.use_sequencer=True
    count=int(movie.frame_final_duration)
    fps=float(movie.fps)
    times=[0,.15,.3,.5,.75,1,1.5,2,3,4,5,7,10,15,20,30]
    times=sorted(set(min(t,max(0,(count-2)/fps)) for t in times))
    records=[]
    for t in times:
        scene.frame_set(1+int(t*fps))
        dest=OUT/f'reference-{i+1}-{t:06.2f}.png'
        scene.render.filepath=str(dest)
        bpy.ops.render.render(write_still=True)
        records.append({'seconds':t,'image':str(dest.relative_to(ROOT))})
    metadata.append({'source':str(path),'sha256':hashlib.file_digest(path.open('rb'),'sha256').hexdigest(),
                     'fps':fps,'frames':count,'duration':count/fps,'width':movie.elements[0].orig_width,
                     'height':movie.elements[0].orig_height,'samples':records})
    scene.sequence_editor_clear()
(OUT/'video-metadata.json').write_text(json.dumps(metadata,indent=2))

model=[]
for lod in range(3):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    source=ROOT/f'Assets/IronRequiem/Art/Enemies/T07Optimized/T07_LOD{lod}.fbx'
    bpy.ops.import_scene.fbx(filepath=str(source),use_anim=False)
    entry={'lod':lod,'armatures':sum(o.type=='ARMATURE' for o in bpy.context.scene.objects),'objects':[]}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        vertices=[obj.matrix_world@v.co for v in obj.data.vertices]
        obj.data.calc_loop_triangles()
        adjacency=[set() for v in vertices]
        for edge in obj.data.edges:
            a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
        seen=set();islands=[]
        for start in range(len(vertices)):
            if start in seen:continue
            stack=[start];seen.add(start);group=[]
            while stack:
                a=stack.pop();group.append(a)
                for b in adjacency[a]:
                    if b not in seen:seen.add(b);stack.append(b)
            islands.append(len(group))
        entry['objects'].append({'name':obj.name,'vertices':len(vertices),'triangles':len(obj.data.loop_triangles),
          'matrix':[list(row) for row in obj.matrix_world],'bounds_min':[min(v[a] for v in vertices) for a in range(3)],
          'bounds_max':[max(v[a] for v in vertices) for a in range(3)],'uv_layers':len(obj.data.uv_layers),
          'materials':len(obj.data.materials),'weighted_vertices':sum(bool(v.groups) for v in obj.data.vertices),
          'connected_components':sorted(islands,reverse=True)})
    model.append(entry)
(OUT/'geometry-inspection.json').write_text(json.dumps(model,indent=2))
print('REFERENCE_AND_GEOMETRY_INSPECTION_COMPLETE')
