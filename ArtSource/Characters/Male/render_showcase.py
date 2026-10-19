"""Render an illustrative movie. World jump and game scales are preview-only."""
import bpy, math, json, time
from pathlib import Path
from mathutils import Vector
PROJECT=Path(r'C:\Users\ASUS TUF\Documents\6 Semestre\Realidad Virtual\pisoEsLava\PisoEsLava')
SOURCE=PROJECT/'ArtSource/Characters/Male'
FRAMES=PROJECT/'Logs/MaleCharacterPreview';FRAMES.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['HR_Male_Character'];bpy.context.window.scene=scene
rig=bpy.data.objects['HR_Male_Rig'];camera=scene.camera
original=(scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage,camera.data.ortho_scale,camera.location.copy(),camera.rotation_euler.copy(),scene.render.filepath)
scene.render.resolution_x=640;scene.render.resolution_y=800;scene.render.resolution_percentage=100
camera.data.ortho_scale=3.4
camera.rotation_euler=(Vector((0,0,1.30))-camera.location).to_track_quat('-Z','Y').to_euler()
labeldata=bpy.data.curves.new('Preview_Label','FONT');labeldata.size=.080;labeldata.align_x='CENTER'
label=bpy.data.objects.new('Preview_Label',labeldata);scene.collection.objects.link(label);label.parent=camera;label.location=(0,1.38,-3)
mat=bpy.data.materials.new('Preview_Text');mat.use_nodes=True
bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bsdf.inputs['Base Color'].default_value=(.8,.9,.95,1);bsdf.inputs['Emission Color'].default_value=(.8,.9,.95,1);bsdf.inputs['Emission Strength'].default_value=.6
label.data.materials.append(mat)
captiondata=bpy.data.curves.new('Preview_Caption','FONT');captiondata.size=.047;captiondata.align_x='CENTER';captiondata.body='HEAT RISE  /  PERSONAJE MASCULINO'
caption=bpy.data.objects.new('Preview_Caption',captiondata);scene.collection.objects.link(caption);caption.parent=camera;caption.location=(0,1.53,-3);caption.data.materials.append(mat)
plan=[]
segments=[('Idle',60,'REPOSO',1,1),('Run',48,'CARRERA',1,1),('Jump',24,'SALTO',1,1),('Fall',24,'CAIDA',1,1),('Land',16,'ATERRIZAJE',1,1),('Grow',36,'CRECER  /  x1.55',1,1.55),('Idle',20,'GIGANTE  /  x1.55',1.55,1.55),('Shrink',30,'ENCoger  /  x0.46',1.55,.46),('Idle',20,'PEQUENO  /  x0.46',.46,.46),('ReturnNormal',30,'VOLVER AL TAMANO NORMAL',.46,1),('Hit',20,'GOLPE',1,1),('Victory',72,'VICTORIA',1,1)]
for clip,count,title,a,b in segments:
    for i in range(0,count,2):
        t=i/max(1,count-2);end=int(bpy.data.actions[clip].frame_range.y)
        f=1+i%(end-1)
        scale=a+(b-a)*(t*t*(3-2*t))
        z=.45*math.sin(math.pi*.5*t) if clip=='Jump' else .45*(1-t*t) if clip=='Fall' else 0
        plan.append((clip,f,title.upper(),scale,z))
state={'index':0,'total':len(plan),'done':False}
def render_next():
    try:
        i=state['index']
        if i>=len(plan):
            rig.scale=(1,1,1);rig.location=(0,0,0);rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
            scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage,camera.data.ortho_scale=original[:4]
            camera.location=original[4];camera.rotation_euler=original[5];scene.render.filepath=original[6]
            bpy.data.objects.remove(label,do_unlink=True);bpy.data.objects.remove(caption,do_unlink=True)
            state['done']=True;(SOURCE/'preview_progress.json').write_text(json.dumps(state))
            return None
        clip,f,title,scale,z=plan[i]
        rig.animation_data.action=bpy.data.actions[clip];scene.frame_set(f);rig.scale=(scale,scale,scale);rig.location=(0,0,z);labeldata.body=title
        scene.render.filepath=str(FRAMES/('%04d.png'%i));bpy.ops.render.render(write_still=True)
        state['index']+=1;(SOURCE/'preview_progress.json').write_text(json.dumps(state))
        return .02
    except Exception as exc:
        state['error']=str(exc);(SOURCE/'preview_progress.json').write_text(json.dumps(state));raise
bpy.app.timers.register(render_next,first_interval=.1)
print('Preview rendering scheduled:',len(plan),'frames at 15 fps')
