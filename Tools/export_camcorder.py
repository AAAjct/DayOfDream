"""Run in Blender background mode to export the DV and battery models."""
from pathlib import Path
from math import cos, pi, sin, tau
import sys
import bpy

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets" / "Art" / "Props" / "Camcorder"
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = mat.diffuse_color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


def finish(name, mat, bevel):
    part = bpy.context.object
    part.name = name
    part.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    edge = part.modifiers.new("Rounded edges", "BEVEL")
    edge.width, edge.segments = bevel, 5
    part.modifiers.new("Face normals", "WEIGHTED_NORMAL")


def box(name, location, size, mat, bevel=0.002):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    bpy.context.object.dimensions = size
    finish(name, mat, bevel)


def tube(name, location, radius, depth, mat, along_lens=True):
    rotation = (pi / 2, 0, 0) if along_lens else (0, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=radius, depth=depth,
                                       location=location, rotation=rotation)
    finish(name, mat, 0.001)


def ring(name, y, outer, inner, depth, mat, z=0.002):
    vertices = [(radius * cos(tau * i / 48), y + face, z + radius * sin(tau * i / 48))
                for radius, face in ((outer, -depth / 2), (outer, depth / 2),
                                     (inner, depth / 2), (inner, -depth / 2)) for i in range(48)]
    faces = [(a * 48 + i, a * 48 + (i + 1) % 48, ((a + 1) % 4) * 48 + (i + 1) % 48,
              ((a + 1) % 4) * 48 + i) for a in range(4) for i in range(48)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    part = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(part)
    bpy.ops.object.select_all(action="DESELECT")
    part.select_set(True)
    bpy.context.view_layer.objects.active = part
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    finish(name, mat, 0.0006)


def legend(name, text, location, size, mat):
    bpy.ops.object.text_add(location=location, rotation=(pi / 2, 0, 0))
    curve = bpy.context.object.data
    curve.body, curve.size, curve.align_x = text, size, "CENTER"
    bpy.ops.object.convert(target="MESH")
    part = bpy.context.object
    part.name = name
    part.data.materials.append(mat)


def export(name):
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (name + ".fbx")),
                             object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             bake_anim=False, path_mode="STRIP")


shell = material("Graphite", (0.064, 0.074, 0.081), 0.48, 0.35)
rubber = material("Rubber", (0.024, 0.027, 0.030), 0.08, 0.75)
metal = material("Machined Alloy", (0.22, 0.24, 0.25), 0.78, 0.31)
glass = material("Optical Glass", (0.038, 0.14, 0.15), 0.38, 0.12)
lcd = material("LCD Glass", (0.080, 0.19, 0.16), 0.16, 0.22)
red = material("Record Button", (0.61, 0.026, 0.019), 0.12, 0.44)
copper = material("Battery Foil", (0.65, 0.27, 0.070), 0.4, 0.4)
label = material("Battery Label", (0.60, 0.54, 0.38), 0.05, 0.55)

# Blender +Y points toward the lens. The flip-out LCD faces the operator.
box("Chassis", (0, 0, 0), (0.084, 0.176, 0.076), shell, 0.008)
box("Upper Alloy Rail", (-0.007, -0.002, 0.035), (0.065, 0.159, 0.009), metal, 0.003)
box("Hand Grip", (0.039, -0.018, -0.004), (0.022, 0.123, 0.067), rubber, 0.006)
for index in range(6):
    box("Grip Ridge", (0.051, -0.055 + index * 0.015, -0.013), (0.004, 0.004, 0.036), shell, 0.001)
box("Battery Door", (0, -0.088, -0.007), (0.069, 0.018, 0.056), rubber, 0.004)
tube("Thumb Record", (0.023, -0.099, 0.018), 0.008, 0.005, red)
tube("Lens Barrel", (0, 0.092, 0.002), 0.037, 0.033, rubber)
ring("Focus Ring", 0.109, 0.040, 0.033, 0.020, rubber)
ring("Alloy Filter Ring", 0.120, 0.038, 0.032, 0.004, metal)
ring("Lens Hood", 0.129, 0.042, 0.036, 0.018, shell)
ring("Optical Retainer", 0.1215, 0.0315, 0.026, 0.003, metal)
tube("Objective", (0, 0.120, 0.002), 0.0267, 0.002, glass)
for index in range(32):
    angle = index * 2 * pi / 32
    box("Focus Knurl", (0.040 * cos(angle), 0.107, 0.002 + 0.040 * sin(angle)),
        (0.0024, 0.014, 0.0036), metal, 0.00045)
    bpy.context.object.rotation_euler.y = -angle
box("IR Emitter Housing", (0.004, 0.062, 0.047), (0.050, 0.048, 0.019), shell, 0.003)
for x in (-0.010, 0.004, 0.018):
    tube("IR Diode", (x, 0.087, 0.047), 0.0045, 0.005, glass)
box("Zoom Rocker", (0.022, -0.030, 0.041), (0.028, 0.038, 0.009), rubber)
for i in range(5):
    box("Rocker Tread", (0.022, -0.042 + i * 0.006, 0.046),
        (0.020, 0.0024, 0.0025), metal, 0.00055)
for i in range(5):
    box("Microphone Grille", (-0.023 + i * 0.006, 0.039, 0.0404),
        (0.0022, 0.016, 0.0013), rubber, 0.00045)
box("Control Deck", (-0.007, -0.093, 0.016), (0.065, 0.011, 0.046), shell, 0.004)
tube("Menu Wheel", (-0.012, -0.100, 0.016), 0.0105, 0.005, metal)
tube("Menu Wheel Center", (-0.012, -0.103, 0.016), 0.0063, 0.003, rubber)
for x in (-0.027, 0.004):
    tube("Control Key", (x, -0.100, -0.0005), 0.0033, 0.004, metal)
for x in (-0.027, 0.027):
    tube("Rear Fastener", (x, -0.099, -0.017), 0.0028, 0.004, metal)
    box("Fastener Slot", (x, -0.1012, -0.017), (0.003, 0.0007, 0.0006), rubber, 0.00015)
legend("DV Marking", "D V", (0.000, -0.099, 0.031), 0.0055, metal)
box("LCD Hinge", (-0.046, 0.007, 0), (0.025, 0.025, 0.049), metal)
box("Flip-out LCD", (-0.104, 0.004, 0), (0.107, 0.012, 0.068), shell, 0.004)
box("Display Bezel", (-0.106, -0.003, 0.002), (0.092, 0.005, 0.055), rubber)
box("Display", (-0.106, -0.0074, 0.002), (0.081, 0.0012, 0.045), lcd, 0.0003)
box("Tally Light", (-0.143, -0.008, 0.028), (0.008, 0.002, 0.0024), red, 0.0006)
legend("LCD Marking", "NIGHT  /  REC", (-0.109, -0.0061, -0.024), 0.0038, metal)

# Precision details: moulded panels, optical finder, leather strap, vents and legible engraving.
box("Tape Compartment", (-.043, -.012, -.004), (.006, .125, .062), shell, .004)
box("Panel Gasket", (-.0465, -.014, -.004), (.001, .109, .048), rubber, .002)
box("Tape Door Inset", (-.0472, -.014, -.004), (.002, .105, .044), metal, .003)
box("Upper Accent", (-.024, .006, .043), (.018, .110, .003), shell, .001)
box("Finder Pedestal", (.012, -.065, .049), (.031, .039, .024), rubber, .004)
tube("Optical Viewfinder", (.012, -.086, .061), .017, .041, shell)
ring("Finder Eyecup", -.108, .021, .010, .014, rubber, .061)
tube("Finder Glass", (.012, -.112, .061), .010, .002, glass)
for x in (-.026, .026):
    for y in (-.070, .059):
        tube("Top Screw", (x, y, .041), .0021, .002, metal, False)
        box("Screw Recess", (x, y, .0422), (.0028, .0006, .0005), rubber, .00015)
for i in range(9):
    box("Cooling Vent", (.0515, .016+i*.005, .010), (.0015, .0020, .023), rubber, .0005)
box("Port Cover", (.0518, -.058, .009), (.003, .029, .019), shell, .002)
box("HDMI Port", (.054, -.057, .009), (.001, .015, .007), rubber, .001)
for y in (-.063, .051):
    box("Strap Lug", (.056, y, -.005), (.009, .012, .024), metal, .002)
# Curved, padded side strap with stitched borders.
verts=[]; faces=[]
for i in range(25):
    t=i/24
    y=-.063+t*.114
    x=.058+.026*sin(pi*t)
    for z in (-.020,.014):
        verts.append((x,y,z))
    if i:
        a=(i-1)*2
        faces.append((a,a+1,a+3,a+2))
mesh=bpy.data.meshes.new("Padded Strap"); mesh.from_pydata(verts,[],faces); mesh.update()
obj=bpy.data.objects.new("Padded Hand Strap",mesh);bpy.context.collection.objects.link(obj)
bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True);bpy.context.view_layer.objects.active=obj
obj.data.materials.append(rubber)
solid=obj.modifiers.new("Leather Thickness","SOLIDIFY");solid.thickness=.003
edge=obj.modifiers.new("Soft Strap Edge","BEVEL");edge.width=.002;edge.segments=3
for i in range(18):
    t=(i+.5)/18
    for z in (-.018,.012):
        box("Strap Stitch",(.060+.026*sin(pi*t),-.063+t*.114,z),(.001,.003,.0007),metal,.0002)
legend("Lens Specification","1:1.8 / 4.8-57.6", (0,.140,.032),.0032,metal)
legend("Rear Serial","DV-X7  /  4K",(.005,-.101,-.025),.0032,metal)
legend("LCD Brand","DREAM OPTICS",(-.106,-.007,-.027),.0035,metal)
# Screen markings are outside the active image, so tabletop and carried versions share the same bezel.
for x in (-.148,-.064):
    tube("LCD Frame Screw",(x,-.004,-.025),.0014,.002,metal)
# Export a manageable number of draw calls, preserving Display as the live-screen anchor.
keep={"Display"}
groups={}
for obj in list(bpy.context.scene.objects):
    if obj.type=="MESH" and obj.name not in keep:
        groups.setdefault(obj.data.materials[0].name,[]).append(obj)
for material_name, objects in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.convert(target="MESH")
        obj.select_set(False)
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    bpy.context.object.name="DV "+material_name
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "SourceArt" / "CamcorderDeluxe.blend"))
export("Camcorder")

if "--camcorder-only" in sys.argv:
    sys.exit()

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
tube("Cell Jacket", (0, 0, 0.026), 0.014, 0.048, copper, False)
tube("Insulation", (0, 0, 0.025), 0.0144, 0.030, rubber, False)
tube("Label Band", (0, 0, 0.034), 0.0146, 0.007, label, False)
tube("Positive Terminal", (0, 0, 0.052), 0.006, 0.006, metal, False)
tube("Negative Terminal", (0, 0, 0.002), 0.012, 0.004, metal, False)
export("CamcorderBattery")
