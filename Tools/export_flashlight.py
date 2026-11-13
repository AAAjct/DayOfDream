import bpy, math
from pathlib import Path
ROOT=Path(r'D:\unity\unity_file\project\DayOfDream')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
prop=[]
def material(name,col,metal,rough):
    m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
    p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if p is None:
        p=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        output=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],output.inputs['Surface'])
    p.inputs['Base Color'].default_value=(*col,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    return m
black=material('Flashlight_Rubber',(.018,.024,.028),.15,.75)
metal=material('Flashlight_Anodized',(.055,.068,.075),.8,.3)
silver=material('Flashlight_Reflector',(.6,.66,.7),.95,.14)
lens=material('Flashlight_Lens',(.62,.78,.73),.25,.1)
def cylinder(name,r,depth,y,mat,vertices=40):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=(0,y,0),rotation=(math.pi/2,0,0))
    ob=bpy.context.object;ob.name=name;ob.data.materials.append(mat);prop.append(ob)
    bevel=ob.modifiers.new('Machined edges','BEVEL');bevel.width=.0018;bevel.segments=2
    ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return ob
cylinder('Grip',.023,.15,.015,black)
cylinder('Rear cap',.026,.018,.099,metal)
cylinder('Lamp housing',.041,.052,-.079,metal)
cylinder('Reflector',.035,.008,-.108,silver)
cylinder('Lens',.030,.003,-.114,lens)
for j in range(8):cylinder('Grip ring',.0245,.004,-.038+j*.014,metal)
for j in range(3):cylinder('Cooling fin',.043,.003,-.061-j*.012,metal)
bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.009,location=(0,-.005,.022))
switch=bpy.context.object;switch.name='Power switch';switch.scale=(1,1,.35);switch.data.materials.append(black);prop.append(switch)
bpy.ops.object.select_all(action='DESELECT')
for o in prop:o.select_set(True)
propout=ROOT/'Assets/Art/Props/Flashlight';propout.mkdir(parents=True,exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(propout/'Flashlight.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,bake_anim=False,path_mode='STRIP')
# Save only the newly made prop scene as an editable source.
newscene=bpy.data.scenes.new('Flashlight source')
for o in prop:newscene.collection.objects.link(o)
source=ROOT/'SourceArt';source.mkdir(exist_ok=True)
bpy.context.window.scene=newscene
bpy.ops.wm.save_as_mainfile(filepath=str(source/'Flashlight.blend'))
print('FLASHLIGHT_EXPORT complete',flush=True)

