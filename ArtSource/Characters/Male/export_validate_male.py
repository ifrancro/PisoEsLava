"""Validate Blender source and export the selected character, not the studio."""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector
PROJECT=Path(r'C:\Users\ASUS TUF\Documents\6 Semestre\Realidad Virtual\pisoEsLava\PisoEsLava')
SOURCE=PROJECT/'ArtSource/Characters/Male'
DEST=PROJECT/'Assets/TowerRush/Models/MaleCharacter'
DEST.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['HR_Male_Character'];bpy.context.window.scene=scene
rig=bpy.data.objects['HR_Male_Rig']
names=['Idle','Run','Jump','Fall','Land','Grow','Shrink','ReturnNormal','Hit','Victory']
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1);scene.frame_end=60
meshes=[o for o in rig.children if o.type=='MESH']
report={'rig':rig.name,'bones':len(rig.data.bones),'units':'metres','mesh_count':len(meshes),'meshes':[],'clips':{},'errors':[]}
for ob in meshes:
    bm=bmesh.new();bm.from_mesh(ob.data)
    bad_edges=sum(not e.is_manifold for e in bm.edges)
    loose=sum(not v.link_faces for v in bm.verts)
    bm.free()
    uv=ob.data.uv_layers.active
    unweighted=sum(sum(g.weight for g in v.groups)<.999 for v in ob.data.vertices)
    overweights=sum(abs(sum(g.weight for g in v.groups)-1)>1e-4 for v in ob.data.vertices)
    outside=sum(not(-.0001<=d.uv.x<=1.0001 and -.0001<=d.uv.y<=1.0001) for d in uv.data) if uv else -1
    ob.data.calc_loop_triangles()
    zero_uv=0
    if uv:
        for tri in ob.data.loop_triangles:
            a,b,c=[uv.data[i].uv for i in tri.loops]
            area=abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5
            if area<1e-10:zero_uv+=1
    row={'name':ob.name,'vertices':len(ob.data.vertices),'triangles':len(ob.data.loop_triangles),'quads':sum(len(p.vertices)==4 for p in ob.data.polygons),'ngons':sum(len(p.vertices)>4 for p in ob.data.polygons),'nonmanifold_edges':bad_edges,'loose_vertices':loose,'unweighted':unweighted,'weight_sum_errors':overweights,'uv_outside_0_1':outside,'zero_area_uv_triangles':zero_uv,'seam_edges':sum(e.use_seam for e in ob.data.edges),'materials':[m.name for m in ob.data.materials]}
    report['meshes'].append(row)
    if bad_edges or loose or unweighted or overweights or outside or zero_uv:report['errors'].append(row)
for name in names:
    action=bpy.data.actions[name];rig.animation_data.action=action
    start,end=map(int,action.frame_range)
    max_step=0;previous=None
    for f in range(start,end+1):
        scene.frame_set(f)
        for bone in rig.pose.bones:
            if not all(math.isfinite(v) for row in bone.matrix for v in row):report['errors'].append(name+' nonfinite bone '+bone.name)
        root=rig.pose.bones['Root']
        if root.location.length>1e-6 or (root.scale-Vector((1,1,1))).length>1e-6:report['errors'].append(name+' root transform changed')
    report['clips'][name]={'first_frame':start,'last_frame':end,'fps':30,'loop':bool(action.get('loop'))}
report['triangles']=sum(m['triangles'] for m in report['meshes'])
report['vertices']=sum(m['vertices'] for m in report['meshes'])
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1);scene.frame_start=1;scene.frame_end=60
if report['errors']:
    (SOURCE/'blender_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    raise RuntimeError('Character validation failed: '+str(report['errors']))
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
visibility={ob:ob.hide_get() for ob in meshes}
for ob in meshes:
    # A hidden detachable cosmetic is still part of the deliverable FBX.
    ob.hide_set(False)
    ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(DEST/'MaleCharacter.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},global_scale=1.0,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,mesh_smooth_type='FACE',use_tspace=True,path_mode='AUTO')
for ob,hidden in visibility.items():ob.hide_set(hidden)
report['fbx']=str(DEST/'MaleCharacter.fbx');report['fbx_bytes']=(DEST/'MaleCharacter.fbx').stat().st_size
(SOURCE/'blender_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
# Save a concise in-file readme beside the editable rig and actions.
readme=bpy.data.texts.get('START_HERE') or bpy.data.texts.new('START_HERE')
readme.clear();readme.write('HEAT RISE | PERSONAJE MASCULINO\n\n6 mallas, '+str(report['triangles'])+' triangulos, 24 huesos.\nSelecciona HR_Male_Rig y cambia la accion en Dope Sheet > Action Editor.\nIdle, Run, Jump, Fall, Land, Grow, Shrink, ReturnNormal, Hit, Victory.\nClips a 30 fps. Sin root motion. La escala real (.46 / 1 / 1.55) pertenece al juego.\nUV0 con cortes y atlas compartido; materiales lisos editables.\nHat es desmontable; Socket_Hat y Socket_Back permiten accesorios basicos.\nEl estudio y la escena inicial se conservan; no se exportan al FBX.\nFuente generada con build_male.py, animate_male.py y export_validate_male.py.\n')
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MaleCharacter.blend'))
print(json.dumps({'PASS':True,'triangles':report['triangles'],'vertices':report['vertices'],'clips':len(report['clips']),'fbx_bytes':report['fbx_bytes']}))
