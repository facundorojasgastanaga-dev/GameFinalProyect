import bpy
scene = bpy.context.scene
def coll(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in scene.collection.children:
        scene.collection.children.link(c)
    return c
groups = {'V01_LOD0': lambda n: n.endswith('LOD0'), 'V01_LOD1': lambda n: n.endswith('LOD1'),
          'V01_LOD2': lambda n: n.endswith('LOD2'), 'V01_FPS': lambda n: n.endswith('_FPS')}
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    for g, test in groups.items():
        if test(o.name):
            c = coll(g)
            if o.name not in c.objects:
                c.objects.link(o)
            if o.name in scene.collection.objects:
                scene.collection.objects.unlink(o)
lc = bpy.context.view_layer.layer_collection
for g in groups:
    lc.children[g].hide_viewport = g != 'V01_LOD0'
for o in bpy.data.objects:
    if o.type == 'EMPTY':
        o.empty_display_size = 0.04
        o.hide_set(o.name.endswith(('.001', '.002')))
for window in bpy.context.window_manager.windows:
    for area in window.screen.areas:
        if area.type != 'VIEW_3D':
            continue
        sp = area.spaces.active
        sp.shading.type = 'SOLID'
        sp.shading.show_cavity = True
        sp.shading.cavity_type = 'BOTH'
        sp.shading.color_type = 'MATERIAL'
        sp.overlay.show_wireframes = False
        region = next(r for r in area.regions if r.type == 'WINDOW')
        with bpy.context.temp_override(window=window, area=area, region=region):
            bpy.ops.object.select_all(action='DESELECT')
            for o in bpy.data.collections['V01_LOD0'].objects:
                o.select_set(True)
            bpy.ops.view3d.view_selected()
            bpy.ops.object.select_all(action='DESELECT')
print('view ready')
