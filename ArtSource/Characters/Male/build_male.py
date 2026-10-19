"""Heat Rise male character. Run inside Blender. Units: metres; front: -Y."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector

PROJECT = Path(r'C:\Users\ASUS TUF\Documents\6 Semestre\Realidad Virtual\pisoEsLava\PisoEsLava')
SOURCE = PROJECT / 'ArtSource/Characters/Male'
SOURCE.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('HR_Male_Character')
bpy.context.window.scene = scene
model_collection = bpy.data.collections.new('CHARACTER | Male')
scene.collection.children.link(model_collection)
studio = bpy.data.collections.new('STUDIO | not exported')
scene.collection.children.link(studio)
parts = []
materials = {}

def material(key, color, roughness=.68, metallic=0):
    m = bpy.data.materials.new('HR_' + key)
    m.use_nodes = True
    m.diffuse_color = (*color, 1)
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = roughness
    p.inputs['Metallic'].default_value = metallic
    materials[key] = m
    return m

material('Skin', (.58, .285, .125))
material('Hair', (.115, .035, .012))
material('Clothing_Shirt', (.025, .34, .37))
material('Clothing_Pants', (.025, .105, .15))
material('Boots', (.065, .038, .022))
material('Soles', (.20, .14, .085))
material('Hat', (.58, .36, .13))
material('HatBand', (.18, .071, .026))
material('EyeWhite', (.88, .86, .71), .35)
material('Iris', (.19, .25, .085), .32)
material('Pupil', (.008, .012, .009), .32)
material('Mouth', (.075, .016, .008))
material('Buttons', (.74, .67, .42), .45)

def weights(obj, bone_or_fn):
    for v in obj.data.vertices:
        w = {bone_or_fn: 1.0} if isinstance(bone_or_fn, str) else bone_or_fn(v.co)
        total = sum(w.values())
        for name, value in w.items():
            if value > 1e-5:
                group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
                group.add([v.index], value/total, 'REPLACE')

def mesh(name, verts, faces, mat, bone, seams=None):
    data = bpy.data.meshes.new(name + '_Topology')
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    model_collection.objects.link(obj)
    obj.data.materials.append(materials[mat])
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    weights(obj, bone)
    if seams:
        pairs = {tuple(sorted(e)) for e in seams}
        for e in data.edges: e.use_seam = tuple(sorted(e.vertices)) in pairs
    parts.append(obj)
    return obj

def box(name, centre, dimensions, mat, bone, bevel=.012):
    x,y,z=centre; a,b,c=[v/2 for v in dimensions]
    vs=[(x+sx*a,y+sy*b,z+sz*c) for sz in (-1,1) for sy in (-1,1) for sx in (-1,1)]
    fs=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
    ob=mesh(name,vs,fs,mat,bone)
    if bevel:
        bm=bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=1,affect='EDGES')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(ob.data); bm.free()
        ob.vertex_groups.clear(); weights(ob,bone)
    return ob

def rings(name, rings_data, mat, bone, sides=8, shape='circle'):
    verts=[]; faces=[]; seams=[]
    for centre,rx,ry in rings_data:
        for i in range(sides):
            a=2*math.pi*i/sides
            if shape=='square':
                pts=[(1,.72),(.72,1),(-.72,1),(-1,.72),(-1,-.72),(-.72,-1),(.72,-1),(1,-.72)]
                u,v=pts[i]
            else: u,v=math.cos(a),math.sin(a)
            verts.append((centre[0]+rx*u,centre[1]+ry*v,centre[2]))
    for j in range(len(rings_data)-1):
        for i in range(sides):
            a=j*sides+i; b=j*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides))
        seams.append((j*sides,(j+1)*sides))
    faces.extend([tuple(reversed(range(sides))),tuple((len(rings_data)-1)*sides+i for i in range(sides))])
    for j in [0,len(rings_data)-1]:
        seams.extend([(j*sides+i,j*sides+(i+1)%sides) for i in range(sides)])
    return mesh(name,verts,faces,mat,bone,seams)

def ellipsoid(name,centre,radii,mat,bone,segments=12,rings_count=6,smooth=False):
    vs=[(centre[0],centre[1],centre[2]-radii[2])]; fs=[]
    for j in range(1,rings_count):
        lat=-math.pi/2+math.pi*j/rings_count
        for i in range(segments):
            a=2*math.pi*i/segments
            vs.append((centre[0]+radii[0]*math.cos(lat)*math.cos(a),centre[1]+radii[1]*math.cos(lat)*math.sin(a),centre[2]+radii[2]*math.sin(lat)))
    top=len(vs); vs.append((centre[0],centre[1],centre[2]+radii[2]))
    for i in range(segments):
        fs.append((0,1+(i+1)%segments,1+i))
        fs.append((top,1+(rings_count-2)*segments+i,1+(rings_count-2)*segments+(i+1)%segments))
    for j in range(rings_count-2):
        for i in range(segments):
            a=1+j*segments+i;b=1+j*segments+(i+1)%segments
            fs.append((a,b,b+segments,a+segments))
    ob=mesh(name,vs,fs,mat,bone)
    for p in ob.data.polygons: p.use_smooth=smooth
    return ob

def prism(name, outline_xz, front, depth, mat, bone):
    vs=[(x,front,z) for x,z in outline_xz]+[(x,front+depth,z) for x,z in outline_xz]
    n=len(outline_xz)
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    fs += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,mat,bone)

def blend(z,a,b,lo,hi):
    t=max(0,min(1,(z-lo)/(hi-lo)))
    return {a:1-t,b:t}

def torso_weight(v):
    if v.z<.65: return blend(v.z,'Hips','Spine',.55,.65)
    return blend(v.z,'Spine','Chest',.72,.87)

# Clothes: ring topology follows waist, chest and shoulder contours.
rings('Body_Shirt', [((0,0,z),rx,ry) for z,rx,ry in [(.51,.205,.126),(.54,.211,.133),(.65,.201,.129),(.76,.214,.135),(.86,.236,.14),(.92,.224,.13),(.95,.15,.103)]], 'Clothing_Shirt',torso_weight,shape='square')
rings('Body_Hips',[((0,.005,z),rx,ry) for z,rx,ry in [(.46,.175,.115),(.50,.184,.121),(.55,.19,.12)]],'Clothing_Pants','Hips',shape='square')
rings('Neck',[((0,0,z),.065,.063) for z in [.91,.97,1.035]],'Skin','Neck',sides=8)
prism('Neckline',[(-.09,.948),(0,.832),(.09,.948)],-.139,.035,'Skin','Chest')
prism('Collar.L',[(0,.832),(.055,.975),(.15,.925),(.108,.838)],-.155,.022,'Clothing_Shirt','Chest')
prism('Collar.R',[(0,.832),(-.055,.975),(-.15,.925),(-.108,.838)],-.155,.022,'Clothing_Shirt','Chest')
for z in [.756,.645,.552]: ellipsoid('Button',(0,-.14,z),(.015,.009,.015),'Buttons','Spine' if z<.8 else 'Chest',8,4)

for side,sg in [('L',1),('R',-1)]:
    def arm_weight(v,side=side): return blend(v.z,'Forearm.'+side,'UpperArm.'+side,.72,.79)
    arm_rings=[((sg*x,0,z),r,r*.87) for x,z,r in [(.246,.90,.093),(.28,.865,.092),(.332,.811,.078),(.368,.77,.064),(.385,.746,.062),(.398,.715,.059),(.427,.662,.052),(.445,.625,.047)]]
    rings('Arm.'+side,arm_rings,'Skin',arm_weight)
    rings('Sleeve.'+side,[((sg*x,0,z),r,r*.95) for x,z,r in [(.229,.919,.105),(.266,.88,.106),(.303,.837,.101),(.318,.818,.097)]],'Clothing_Shirt','UpperArm.'+side)
    ellipsoid('Hand.'+side,(sg*.456,-.006,.588),(.06,.047,.075),'Skin','Hand.'+side,8,6)
    ellipsoid('Thumb.'+side,(sg*.425,-.036,.604),(.029,.033,.045),'Skin','Hand.'+side,8,4)
    def leg_weight(v,side=side): return blend(v.z,'Shin.'+side,'Thigh.'+side,.285,.365)
    rings('Leg.'+side,[((sg*x,y,z),rx,ry) for x,y,z,rx,ry in [(.104,0,.515,.096,.102),(.108,0,.455,.091,.095),(.11,-.006,.366,.081,.084),(.113,-.01,.327,.079,.08),(.115,-.006,.291,.076,.077),(.116,0,.185,.068,.069),(.116,0,.125,.069,.07)]],'Clothing_Pants',leg_weight,shape='square')
    rings('Cuff.'+side,[((sg*.116,0,z),.076,.077) for z in [.123,.16,.17]],'Clothing_Pants','Shin.'+side,shape='square')
    box('Boot.'+side,(sg*.116,-.035,.078),(.165,.255,.136),'Boots','Foot.'+side,.025)
    box('Sole.'+side,(sg*.116,-.04,.019),(.173,.266,.038),'Soles','Foot.'+side,.008)

# Oversized faceted head, with broad planar cheeks and bevelled silhouette.
box('Head',(0,0,1.278),(.642,.49,.61),'Skin','Head',.048)
box('Hair_Back',(0,.165,1.29),(.646,.22,.57),'Hair','Head',.045)
crown=box('Hair_Crown',(0,0,1.54),(.646,.50,.10),'Hair','Head',.032)
for v in crown.data.vertices:
    taper=1-.12*max(0,min(1,(v.co.z-1.55)/.04))
    v.co.x*=taper;v.co.y*=taper
for side,sg in [('L',1),('R',-1)]:
    ellipsoid('Ear.'+side,(sg*.325,-.008,1.29),(.067,.077,.104),'Skin','Head',10,6)
    box('Sideburn.'+side,(sg*.294,-.085,1.283),(.068,.194,.38),'Hair','Head',.014)
prism('Hair_Fringe',[(-.29,1.555),(.29,1.555),(.27,1.474),(.17,1.50),(.09,1.468),(-.015,1.514),(-.10,1.466),(-.29,1.493)],-.252,.07,'Hair','Head')
box('Beard_Chin',(0,-.02,1.035),(.582,.51,.177),'Hair','Head',.032)
for side,sg in [('L',1),('R',-1)]:
    prism('Beard_Cheek.'+side,[(sg*.284,1.25),(sg*.245,1.19),(sg*.16,1.125),(sg*.072,1.104),(sg*.11,1.015),(sg*.289,1.042)],-.257,.07,'Hair','Head')
prism('Moustache',[(-.20,1.136),(-.174,1.187),(-.063,1.193),(0,1.176),(.063,1.193),(.174,1.187),(.20,1.136),(.148,1.145),(.07,1.158),(0,1.146),(-.07,1.158),(-.148,1.145)],-.322,.045,'Hair','Head')
prism('Smile',[(-.086,1.136),(-.04,1.118),(.035,1.116),(.084,1.141),(.06,1.102),(0,1.088),(-.06,1.104)],-.286,.012,'Mouth','Head')
prism('Smile_Teeth',[(-.057,1.128),(.055,1.13),(.04,1.114),(-.035,1.113)],-.301,.008,'EyeWhite','Head')
mesh('Nose',[(-.046,-.246,1.382),(.046,-.246,1.382),(-.072,-.395,1.196),(.072,-.395,1.196),(-.067,-.244,1.191),(.067,-.244,1.191)],[(0,1,3,2),(2,3,5,4),(0,2,4),(1,5,3),(0,4,5,1)],'Skin','Head')
for side,sg in [('L',1),('R',-1)]:
    x=sg*.145;z=1.372
    ellipsoid('Eye_Rim.'+side,(x,-.238,z),(.073,.033,.079),'Hair','Head',12,8,True)
    ellipsoid('Eye_White.'+side,(x,-.257,z),(.063,.035,.067),'EyeWhite','Head',16,8,True)
    ellipsoid('Eye_Iris.'+side,(x-sg*.005,-.289,z-.004),(.034,.012,.038),'Iris','Head',12,6,True)
    ellipsoid('Eye_Pupil.'+side,(x-sg*.005,-.300,z-.003),(.017,.005,.023),'Pupil','Head',12,6,True)
    ellipsoid('Eye_Glint.'+side,(x-.012,-.305,z+.013),(.009,.004,.01),'EyeWhite','Head',8,4,True)
    if sg>0: pts=[(.075,1.466),(.22,1.473),(.223,1.447),(.082,1.442)]
    else: pts=[(-.224,1.482),(-.076,1.466),(-.082,1.44),(-.226,1.457)]
    prism('Brow.'+side,pts,-.274,.028,'Hair','Head')

# Removable hat: single low-poly crown/brim with planar concentric quad strips.
hat=rings('Hat',[((0,.02,z),rx,ry) for z,rx,ry in [(1.557,.40,.335),(1.57,.44,.365),(1.585,.44,.365),(1.601,.27,.235),(1.62,.252,.214),(1.723,.213,.187),(1.75,.187,.165)]],'Hat','Head',sides=24)
band=rings('Hat_Band',[((0,.02,z),rx,ry) for z,rx,ry in [(1.606,.262,.224),(1.64,.247,.211)]],'HatBand','Head',sides=24)

# Deliberate cut seams: longitudinal/cap cuts on ring meshes, sharp cuts elsewhere.
for ob in parts:
    bm=bmesh.new();bm.from_mesh(ob.data)
    has_seams=any(e.seam for e in bm.edges)
    if not has_seams:
        for e in bm.edges:
            if len(e.link_faces)!=2 or e.calc_face_angle()>.48: e.seam=True
        # Spheres need a back meridian and pole cut, not a cut on every quad.
        if 'Eye_' in ob.name or ob.name.startswith(('Ear.','Hand.','Thumb.','Button')):
            for e in bm.edges:
                p=(e.verts[0].co+e.verts[1].co)/2
                if abs(e.verts[0].co.x-e.verts[1].co.x)<1e-5 and p.y>sum(v.co.y for v in bm.verts)/len(bm.verts): e.seam=True
    ngons=[f for f in bm.faces if len(f.verts)>4]
    if ngons: bmesh.ops.triangulate(bm,faces=ngons)
    bm.to_mesh(ob.data);bm.free()

# UVs are shared in one non-overlapping atlas; solid-color material slots remain editable.
bpy.ops.object.select_all(action='DESELECT')
for ob in parts: ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.unwrap(method='ANGLE_BASED',margin=.012)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True,margin=.008)
bpy.ops.object.mode_set(mode='OBJECT')

# Consolidate small rigid details, preserving all material slots and bone weights.
def combine(name,predicate):
    obs=[o for o in list(parts) if predicate(o)]
    if not obs:return
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.object.join()
    for o in obs[1:]:parts.remove(o)
    obs[0].name=name
    return obs[0]
combine('Eyes',lambda o:o.name.startswith('Eye_'))
combine('Hair_and_Beard',lambda o:o.name.startswith(('Hair_','Sideburn.','Beard_','Moustache','Brow.')))
combine('Face',lambda o:o.name.startswith(('Head','Ear.','Nose','Smile')))
combine('Outfit',lambda o:o.name.startswith(('Body_','Sleeve.','Collar.','Button','Leg.','Cuff.','Boot.','Sole.')))
combine('Skin_Body',lambda o:o.name.startswith(('Neck','Arm.','Hand.','Thumb.')))
combine('Hat',lambda o:o.name.startswith('Hat'))

rigdata=bpy.data.armatures.new('HR_Male_Skeleton')
rig=bpy.data.objects.new('HR_Male_Rig',rigdata)
model_collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None,deform=True):
    b=rigdata.edit_bones.new(name);b.head=head;b.tail=tail;b.use_deform=deform
    if parent:b.parent=rigdata.edit_bones[parent]
    return b
bone('Root',(0,0,0),(0,0,.13),deform=False)
bone('Hips',(0,0,.51),(0,0,.64),'Root')
bone('Spine',(0,0,.64),(0,0,.80),'Hips')
bone('Chest',(0,0,.80),(0,0,.94),'Spine')
bone('Neck',(0,0,.94),(0,0,1.055),'Chest')
bone('Head',(0,0,1.055),(0,0,1.56),'Neck')
for side,sg in [('L',1),('R',-1)]:
    bone('Clavicle.'+side,(sg*.045,0,.92),(sg*.23,0,.91),'Chest')
    bone('UpperArm.'+side,(sg*.23,0,.91),(sg*.38,0,.75),'Clavicle.'+side)
    bone('Forearm.'+side,(sg*.38,0,.75),(sg*.445,0,.625),'UpperArm.'+side)
    bone('Hand.'+side,(sg*.445,0,.625),(sg*.46,0,.55),'Forearm.'+side)
    bone('Thigh.'+side,(sg*.104,0,.50),(sg*.113,-.01,.325),'Hips')
    bone('Shin.'+side,(sg*.113,-.01,.325),(sg*.116,0,.125),'Thigh.'+side)
    bone('Foot.'+side,(sg*.116,0,.125),(sg*.116,-.15,.06),'Shin.'+side)
bone('Socket_Hat',(0,.02,1.60),(0,.02,1.73),'Head',False)
bone('Socket_Back',(0,.14,.84),(0,.22,.84),'Chest',False)
bone('Socket_Hand.L',(.46,-.015,.59),(.46,-.08,.59),'Hand.L',False)
bone('Socket_Hand.R',(-.46,-.015,.59),(-.46,-.08,.59),'Hand.R',False)
bpy.ops.object.mode_set(mode='OBJECT')
for ob in parts:
    ob.parent=rig
    mod=ob.modifiers.new('Deform | HR skeleton','ARMATURE');mod.object=rig
rig.show_in_front=True
rig['Character']='Heat Rise | male / low-poly'
rig['Size_contract']='Unity game root controls size: normal 1, small 0.46, large 1.55. Clips keep Root at unit scale.'
rig['Forward']='Blender -Y; FBX exported -Z forward, Y up for Unity'
rig['Topology']='Built directly with low-poly ring topology; elbow/knee loops, UV seams and packed UV0. No high-poly decimation.'
rig['Cosmetics']='Hat object is removable. Socket_Hat / Socket_Back / Socket_Hand.L / Socket_Hand.R'
for pb in rig.pose.bones:pb.rotation_mode='XYZ'

# Quiet studio presentation, excluded from exported model.
def to_studio(ob):
    for c in list(ob.users_collection):c.objects.unlink(ob)
    studio.objects.link(ob)
def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008));floor=bpy.context.object;floor.name='Studio_Ground';to_studio(floor)
floor.data.materials.append(material('Studio',(.16,.205,.22),.8))
bpy.ops.object.camera_add(location=(2.3,-4.8,2.4));cam=bpy.context.object;cam.name='Portrait_Camera';to_studio(cam);aim(cam,(0,0,.91));cam.data.type='ORTHO';cam.data.ortho_scale=2.3;scene.camera=cam
for name,loc,power,size,color in [('Key',(-3,-4,5),450,4,(1,.84,.69)),('Fill',(3,-2,2.8),260,3,(.70,.85,1)),('Rim',(1,3,4),600,3,(1,.72,.42))]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc;aim(ob,(0,0,1))
scene.world=bpy.data.worlds.new('HR Studio World');scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.35,.42,.48,1);bg.inputs['Strength'].default_value=.35
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.fps=30
scene.view_settings.view_transform='AgX'
scene.render.film_transparent=False
scene.render.filepath=str(SOURCE/'preview.png')
scene.frame_start=1;scene.frame_end=60
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.shading.type='MATERIAL'
        area.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MaleCharacter.blend'))
print(json.dumps({'scene':scene.name,'meshes':len(parts),'vertices':sum(len(o.data.vertices) for o in parts),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts),'bones':len(rigdata.bones),'saved':str(SOURCE/'MaleCharacter.blend')}))
