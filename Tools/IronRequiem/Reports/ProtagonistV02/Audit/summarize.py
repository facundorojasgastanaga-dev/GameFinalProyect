import json,sys
keys=['verts','tris','face_sides','ngons','ngons_concave','ngon_max_sides','quads_concave','edges_boundary','edges_nonmanifold_gt2','edges_wire','verts_loose','verts_nonmanifold','faces_zero_area','faces_duplicate_same_verts','faces_duplicate_same_positions','verts_coincident_extra','faces_interior_bmesh','edges_inconsistent_winding','faces_flipped_vs_recalc_outside','islands','open_islands','inverted_closed_islands','tiny_islands_lt_8verts','faces_enclosed_by_own_shell','self_intersections','dimensions','origin_to_bbox_center','scale']
for f in sys.argv[1:]:
    d=json.load(open(f)); print('#####',f, 'mats',d['materials']['names'], 'dupstem',d['materials']['same_stem_groups'],'ident',d['materials']['identical_signature_groups'])
    print(' X-inter',d['cross_object_intersections_same_lod'])
    for a in d['armatures']: print(' ARM',a['name'],a['scale'],'bones',a['bone_count'],'noverts',a['bones_without_vertices'])
    for o in d['objects']:
        print(' ==',o['object'])
        print('   ',{k:o.get(k) for k in keys})
        fl=o['floating_islands_gt_1mm']; print('    floating>1mm:',len(fl), sorted(fl,key=lambda x:-x['dist_m'])[:6])
        if 'uv' in o:
            u=dict(o['uv']); u.pop('texel_density_by_uv_island_largest30',None); r=u.pop('raster',None); print('    UV',u); print('    RASTER',r)
        if 'weights' in o:
            w=dict(o['weights']); m=w.pop('multi_bone_islands'); w.pop('groups'); print('    W',w); print('    MULTI', m[:5])
