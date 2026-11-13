"""Export beveled furniture for Anon's house with Blender in background mode."""
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[1] / "Assets/Art/Environment/AnonHouse"
root.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    shader = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = mat.diffuse_color
    shader.inputs["Roughness"].default_value = .82
    return mat


oak = material("Stained Oak", (.16, .086, .044))
trim = material("Dark Molding", (.072, .041, .028))
linen = material("Faded Linen", (.39, .34, .24))
iron = material("Tarnished Iron", (.19, .17, .12))


def panel(name, location, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Worn edges", "BEVEL")
    bevel.width = .016
    bevel.segments = 2
    obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")


def export(name):
    bpy.ops.export_scene.fbx(filepath=str(root / (name + ".fbx")), object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", bake_anim=False)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


panel("Apron", (0, 0, .69), (1.55, .69, .21), oak)
panel("Top", (0, 0, .84), (1.66, .78, .09), trim)
for x in (-.69, .69):
    for y in (-.28, .28):
        panel("Leg", (x, y, .35), (.11, .11, .7), oak)
for x in (-.38, .38):
    panel("Drawer", (x, .352, .70), (.69, .05, .13), oak)
    panel("Pull", (x, .39, .70), (.12, .038, .025), iron)
export("OakTable")

panel("Frame", (0, 0, .28), (1.42, 2.07, .20), trim)
panel("Mattress", (0, 0, .46), (1.35, 1.95, .19), linen)
panel("Bedspread", (0, -.33, .57), (1.38, 1.26, .035), oak)
panel("Pillow", (0, .71, .59), (.8, .35, .12), linen)
panel("Headboard", (0, 1.02, .58), (1.48, .11, 1.14), oak)
for x in (-.64, .64):
    for y in (-.91, .93):
        panel("Foot", (x, y, .14), (.09, .1, .28), trim)
export("Bed")

for x in (-.63, .63):
    panel("Side", (x, 0, 1.03), (.10, .75, 2.06), oak)
panel("Back", (0, -.34, 1.03), (1.26, .08, 2.06), trim)
panel("Cornice", (0, 0, 2.04), (1.47, .85, .14), trim)
for x in (-.32, .32):
    panel("Door", (x, .34, 1.04), (.62, .065, 1.86), oak)
    for z in (.54, 1.49):
        panel("Raised Panel", (x, .38, z), (.48, .05, .72), trim)
    panel("Knob", (x * .23, .43, .96), (.045, .045, .045), iron)
export("Wardrobe")
