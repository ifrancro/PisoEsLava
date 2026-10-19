"""Ten portable, in-place FK actions on the male character's Generic rig."""
import bpy, math, json
from mathutils import Vector, Quaternion
from pathlib import Path
SOURCE=Path(r'C:\Users\ASUS TUF\Documents\6 Semestre\Realidad Virtual\pisoEsLava\PisoEsLava\ArtSource\Characters\Male')
scene=bpy.data.scenes['HR_Male_Character'];bpy.context.window.scene=scene
rig=bpy.data.objects['HR_Male_Rig']
rig.animation_data_create()
defs={'Idle':60,'Run':24,'Jump':24,'Fall':40,'Land':16,'Grow':36,'Shrink':30,'ReturnNormal':30,'Hit':20,'Victory':72}
axes={b.name:[b.matrix_local.to_3x3().inverted()@Vector(a) for a in [(1,0,0),(0,1,0),(0,0,1)]] for b in rig.data.bones}
def rotate(name,x=0,y=0,z=0):
    pb=rig.pose.bones[name]
    pb.rotation_mode='QUATERNION'
    a=axes[name]
    pb.rotation_quaternion=Quaternion(a[0],x)@Quaternion(a[1],y)@Quaternion(a[2],z)
def move(name,world):
    rig.pose.bones[name].location=rig.data.bones[name].matrix_local.to_3x3().inverted()@Vector(world)
def reset():
    for pb in rig.pose.bones:
        pb.location=(0,0,0);pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.scale=(1,1,1)
def wave(t):return math.sin(math.pi*t)
def envelope(t):return max(0,math.sin(math.pi*t))**1.2
def pose(clip,t):
    reset();w=math.sin(t*2*math.pi);c=math.cos(t*2*math.pi)
    upper={'L':(0,.40,0),'R':(0,-.40,0)}
    fore={'L':(-.09,0,0),'R':(-.09,0,0)}
    thighs={'L':0,'R':0};knees={'L':0,'R':0};feet={'L':0,'R':0}
    if clip=='Idle':
        move('Hips',(0,0,.004*w));rotate('Chest',x=.012*w,z=.014*w);rotate('Head',x=-.012*w,z=-.018*w)
        upper['L']=(-.025*w,.40,.012*w);upper['R']=(.025*w,-.40,.012*w)
    elif clip=='Run':
        move('Hips',(0,0,.018*(1-math.cos(t*4*math.pi))));rotate('Hips',z=.035*w)
        rotate('Chest',x=.12,z=-.055*w);rotate('Head',x=-.08,z=.025*w)
        for side,sg in [('L',1),('R',-1)]:
            phase=t*2*math.pi+(0 if sg==1 else math.pi);v=math.sin(phase)
            thighs[side]=-.61*v;knees[side]=.10+.88*max(0,-v)
            feet[side]=-.12-.32*max(0,-v)
            upper[side]=(.65*v,sg*.30,sg*.02);fore[side]=(-.62-.14*v,0,0)
    elif clip=='Jump':
        charge=math.exp(-((t-.13)/.13)**2);reach=math.exp(-((t-.50)/.21)**2);tuck=math.exp(-((t-.80)/.25)**2)
        move('Hips',(0,0,-.055*charge+.018*reach));rotate('Chest',x=.17*charge-.08*reach);rotate('Head',x=-.07*charge)
        for side,sg in [('L',1),('R',-1)]:
            thighs[side]=-.25*charge-.30*tuck;knees[side]=.48*charge+.46*tuck
            feet[side]=-.12*charge+.20*reach
            upper[side]=(.25*charge-1.14*reach-.72*tuck,sg*(.36-.30*reach),0)
            fore[side]=(-.12-.35*tuck,0,0)
    elif clip=='Fall':
        rotate('Chest',x=-.06+.025*w);rotate('Head',x=.10,z=.045*w)
        for side,sg in [('L',1),('R',-1)]:
            upper[side]=(-.42+.10*w,-sg*(.80+.08*c),sg*.08*w);fore[side]=(-.70-sg*.12*w,0,0)
            thighs[side]=sg*.13*w-.10;knees[side]=.30+sg*.08*w;feet[side]=.15
    elif clip=='Land':
        k=math.sin(math.pi*min(1,t/.75))*(1-t)
        move('Hips',(0,-.008*k,-.068*k));rotate('Chest',x=.22*k);rotate('Head',x=-.08*k)
        rig.pose.bones['Spine'].scale=(1+.05*k,1-.10*k,1+.05*k)
        for side,sg in [('L',1),('R',-1)]:
            thighs[side]=-.40*k;knees[side]=.72*k;feet[side]=-.15*k
            upper[side]=(-.35*k,sg*(.4-.35*k),0);fore[side]=(-.18-.35*k,0,0)
    elif clip=='Grow':
        k=envelope(t);charge=math.exp(-((t-.23)/.16)**2);pop=math.exp(-((t-.57)/.16)**2)
        move('Hips',(0,0,-.048*charge+.024*pop));rotate('Chest',x=.12*charge-.10*pop);rotate('Head',x=-.05*k)
        rig.pose.bones['Chest'].scale=(1+.065*pop,1+.04*pop,1+.05*pop)
        for side,sg in [('L',1),('R',-1)]:
            upper[side]=(-.25*k,sg*(.40-.9*k),0);fore[side]=(-.09-.90*k,0,0)
            thighs[side]=-.28*charge;knees[side]=.50*charge
    elif clip=='Shrink':
        k=envelope(t);pop=math.exp(-((t-.60)/.22)**2)
        move('Hips',(0,0,-.045*k));rotate('Chest',x=.16*k);rotate('Head',x=.14*k,z=.08*k)
        rig.pose.bones['Spine'].scale=(1+.045*pop,1-.08*pop,1+.045*pop)
        for side,sg in [('L',1),('R',-1)]:
            upper[side]=(-.46*k,sg*(.40+.12*k),-sg*.10*k);fore[side]=(-.09-.85*k,0,0)
            thighs[side]=-.22*k;knees[side]=.40*k
    elif clip=='ReturnNormal':
        k=envelope(t);bounce=math.sin(3*math.pi*t)*(1-t)*.012
        move('Hips',(0,0,bounce));rotate('Chest',x=-.06*k);rotate('Head',z=.06*w*(1-t))
        for side,sg in [('L',1),('R',-1)]:
            upper[side]=(-.15*k,sg*(.40-.25*k),0);fore[side]=(-.09-.18*k,0,0)
    elif clip=='Hit':
        k=math.sin(math.pi*t)**.75;recoil=math.sin(2*math.pi*t)*(1-t)
        move('Hips',(0,.035*k,-.025*k));rotate('Chest',x=-.24*k,z=.10*recoil);rotate('Head',x=-.13*k,z=-.13*recoil)
        for side,sg in [('L',1),('R',-1)]:
            upper[side]=(-.40*k,sg*(.40-.75*k),0);fore[side]=(-.09-.20*k,0,0)
            thighs[side]=-.10*k;knees[side]=.22*k
    elif clip=='Victory':
        bounce=(1-math.cos(4*math.pi*t))*.5
        move('Hips',(0,0,.025*bounce));rotate('Chest',z=.045*w);rotate('Head',z=-.06*w,x=-.04)
        for side,sg in [('L',1),('R',-1)]:
            upper[side]=(-.55,-sg*(1.35+.10*w),0);fore[side]=(-.9-.2*c,0,0)
            thighs[side]=-.08*bounce;knees[side]=.16*bounce
    for side in ['L','R']:
        rotate('UpperArm.'+side,*upper[side]);rotate('Forearm.'+side,*fore[side])
        rotate('Thigh.'+side,x=thighs[side]);rotate('Shin.'+side,x=knees[side]);rotate('Foot.'+side,x=feet[side])

for clip,end in defs.items():
    old=bpy.data.actions.get(clip)
    if old:bpy.data.actions.remove(old)
    action=bpy.data.actions.new(clip);action.use_fake_user=True
    rig.animation_data.action=action
    for f in list(range(1,end+1,2))+([end] if end%2==0 else []):
        scene.frame_set(f);pose(clip,(f-1)/(end-1))
        for pb in rig.pose.bones:
            if pb.name=='Root' or pb.name.startswith('Socket_'):continue
            for path in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=path,frame=f,group=pb.name)
    action['loop']=clip in ['Idle','Run','Fall','Victory']
    action['root_motion']=False
    action['description']='Normalized in-place gesture. Gameplay controls world motion and final character scale.'
    for layer in action.layers:
        for strip in layer.strips:
            for channelbag in strip.channelbags:
                for fc in channelbag.fcurves:
                    for key in fc.keyframe_points:key.interpolation='LINEAR'
rig.animation_data.action=bpy.data.actions['Idle']
scene.frame_start=1;scene.frame_end=60;scene.frame_set(1)
scene.timeline_markers.clear()
scene.timeline_markers.new('Idle | 30 fps | select other clips in Action Editor',frame=1)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MaleCharacter.blend'))
print(json.dumps({name:list(bpy.data.actions[name].frame_range) for name in defs}))
