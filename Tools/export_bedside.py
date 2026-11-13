from pathlib import Path
import bpy
from math import pi
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/"Assets/Art/Props/Bedroom";OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name,color,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 n=m.node_tree.nodes.get("Principled BSDF");n.inputs["Base Color"].default_value=m.diffuse_color
 n.inputs["Metallic"].default_value=metal;n.inputs["Roughness"].default_value=.4
 return m
wood=mat("Nightstand Walnut",(.22,.12,.067));edge=mat("Nightstand Edge",(.115,.061,.032));brass=mat("Nightstand Brass",(.43,.30,.13),.7);dark=mat("Nightstand Seams",(.025,.018,.012))
def box(name,pos,size,m,b=.005):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m)
 mod=o.modifiers.new("Furniture Edges","BEVEL");mod.width=b;mod.segments=3;o.modifiers.new("Normals","WEIGHTED_NORMAL")
# Blender vertical Z. Front faces -Y.
box("Overhanging Top",(0,0,.752),(.70,.61,.048),wood,.013)
box("Top Underlip",(0,0,.715),(.65,.57,.025),edge)
box("Carcase",(0,0,.42),(.63,.54,.57),edge,.009)
box("Drawer Gaps",(0,-.277,.439),(.55,.009,.48),dark)
for z in (.318,.559):
 box("Drawer Front",(0,-.290,z),(.557,.040,.224),wood,.007)
 box("Raised Drawer Panel",(0,-.313,z),(.483,.016,.158),edge,.004)
 box("Inset Walnut",(0,-.323,z),(.453,.009,.131),wood,.004)
 for x in (-.088,.088):
  box("Pull Escutcheon",(x,-.338,z),(.035,.012,.032),brass)
  box("Handle Standoff",(x,-.357,z),(.012,.045,.012),brass)
 box("Drawer Handle",(0,-.379,z),(.188,.014,.016),brass)
for x in (-.255,.255):
 for y in (-.204,.204):
  box("Tapered Foot",(x,y,.09),(.079,.079,.18),edge,.009)
for x in (-.321,.321):
 box("Side Panel",(x,0,.43),(.016,.421,.436),wood)
 box("Side Inset",(x*1.025,0,.43),(.012,.352,.36),edge)
for m in (wood,edge,brass,dark):
 obs=[o for o in bpy.context.scene.objects if o.type=="MESH" and o.data.materials[0]==m]
 bpy.ops.object.select_all(action="DESELECT")
 for o in obs:
  o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target="MESH");o.select_set(False)
 for o in obs:o.select_set(True)
 bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();bpy.context.object.name=m.name
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"SourceArt/BedroomNightstand.blend"))
bpy.ops.export_scene.fbx(filepath=str(OUT/"BedroomNightstand.fbx"),object_types={"MESH"},axis_forward="-Z",axis_up="Y",bake_anim=False,path_mode="STRIP")
