"""Blender MCP entry point for the authored house shell and its trim."""
import json
from pathlib import Path

import bpy
import sys
from mathutils import Quaternion


def export_house(project):
    art = project / "Assets/Art/Environment/AnonHouse"
    layout = json.loads((project / "Temp/AnonHouseLayout.json").read_text(encoding="utf-8-sig"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    groups = {}
    for item in sealed_shell(layout):
        if item["name"] in ("Runner border", "Wainscot slat", "Hall chair rail"):
            continue
        x, y, z = item["position"]
        w, h, d = item["size"]
        bpy.ops.mesh.primitive_cube_add(size=1, location=(-x, -z, y))
        obj = bpy.context.object
        obj.name = item["name"]
        obj.dimensions = (w, d, h)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        x, y, z, w = item["rotation"]
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = Quaternion((w, x, z, -y))
        name = item["material"]
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.diffuse_color = (*item["color"], 1)
        obj.data.materials.append(mat)
        # Real-world UVs keep the grain and mortar the same scale on small trim and long walls.
        world = obj.matrix_basis
        uv = obj.data.uv_layers.active.data
        for face in obj.data.polygons:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            u, v = ((1, 2), (0, 2), (0, 1))[axis]
            for loop in face.loop_indices:
                p = world @ obj.data.vertices[obj.data.loops[loop].vertex_index].co
                uv[loop].uv = (p[u] / 2.2, p[v] / 2.2)
        if not structural_surface(item["name"]):
            bevel = obj.modifiers.new("Worn edges", "BEVEL")
            bevel.width = min(.025, min(w, h, d) * .14)
            bevel.segments = 2
            obj.modifiers.new("Face normals", "WEIGHTED_NORMAL")
        groups.setdefault(name, []).append(obj)
    add_details(groups)
    if any(item["name"] == "Attic floor" for item in layout):
        for z in (-7.0, 7.0):
            vertices=[(7,-z,8.88),(-1.5,-z,11.4),(-10,-z,8.88)]
            mesh=bpy.data.meshes.new("Attic Gable")
            mesh.from_pydata(vertices,[],[(0,1,2)]);mesh.update()
            obj=bpy.data.objects.new("Attic Gable",mesh);bpy.context.collection.objects.link(obj)
            obj.data.materials.append(bpy.data.materials["Dark Oak"])
            shell=obj.modifiers.new("Gable thickness","SOLIDIFY");shell.thickness=.15
            groups["Dark Oak"].append(obj)
    finish_architecture(project, groups)


def structural_surface(name):
    return ("wall" in name.lower() or name in {
        "Ground floor", "Cellar floor", "Stair landing", "Stair tower ground",
        "Stair arrival landing", "Half landing", "Second floor slab", "Attic floor",
        "Extended basement slab", "Basement overhead ceiling", "Second floor ceiling",
        "Stair tower roof", "New doorway lintel", "Doorway jamb extension",
    } or name.startswith("Courtyard "))


def sealed_shell(layout):
    """Fit adjacent visual surfaces without overlapping their exposed faces."""
    def set_extent(part, axis, lower=None, upper=None):
        center, size = part["position"], part["size"]
        start = center[axis] - size[axis] / 2 if lower is None else lower
        end = center[axis] + size[axis] / 2 if upper is None else upper
        center[axis], size[axis] = (start + end) / 2, end - start

    expanded = any(part["name"] == "Stair tower ground" for part in layout)
    for part in layout:
        name = part["name"]
        if name == "Stair start landing":
            continue
        if name == "Stair landing":
            set_extent(part, 0, lower=7.125)
        elif name in {"Stair tower ground", "Stair arrival landing"}:
            set_extent(part, 0, lower=10)
        elif name == "Extended basement slab":
            set_extent(part, 0, upper=.85)
        elif name == "Courtyard east terrace":
            set_extent(part, 0, upper=16.35)
            set_extent(part, 2, upper=2.45)
        elif name == "Courtyard kitchen terrace":
            set_extent(part, 0, lower=7.125)
            set_extent(part, 2, lower=2.45)
        elif name == "Plastered wall" and abs(part["size"][1] - 9.3) < .001:
            set_extent(part, 1, upper=9.45)
        yield part

    if expanded:
        plaster = next(part for part in layout if part["name"] == "Plastered wall")
        for bottom, top in ((2.65, 3.3), (6.25, 6.6)):
            yield {**plaster, "position": [10, (bottom + top) / 2, -2.175],
                   "size": [.2, top - bottom, 9.25], "rotation": [0, 0, 0, 1]}
        yield {**plaster, "position": [10, 1.325, 2.225],
               "size": [.2, 2.65, .45], "rotation": [0, 0, 0, 1]}


def finish_architecture(project, groups):
    for material, objects in groups.items():
        for obj in objects:
            name = obj.name.split(".")[0]
            if name == "Door lintel":
                # Sink the top into the ceiling so their exposed horizontal faces never coincide.
                obj.scale.z *= 1.025
                obj.location.z += .004
            elif name == "Baseboard":
                # Perpendicular trim terminates under the taller run at the corner.
                if obj.dimensions.x > obj.dimensions.y:
                    obj.scale.z *= .945
                    obj.location.z += .002
                else:
                    obj.location.z -= .004
            elif name == "Wall" and material == "Damp Plaster" and abs(obj.location.x + 10) < .001:
                # Recess the upper wall at the cellar stair transition.
                obj.scale.x *= .92
            elif name in ("Floor splinter", "Cellar rubble"):
                index = int(obj.name.split(".")[-1]) if "." in obj.name else 0
                obj.scale.z *= 1 + (.09 if name == "Floor splinter" else .027) * index
            elif name == "Window barricade":
                obj.location.y += .009 if abs(obj.location.z - 1.68) < .001 else 0
            elif name == "Tin cap":
                obj.location.z += .003
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(project / "SourceArt/AnonHouse.blend"), compress=True)
    for name, objects in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.convert(target="MESH")
        if len(objects) > 1:
            bpy.ops.object.join()
        bpy.context.object.name = name
    bpy.ops.export_scene.fbx(filepath=str(project / "Assets/Art/Environment/AnonHouse/HouseArchitecture.fbx"), object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", bake_anim=False)
    print(f"Exported {sum(len(parts) for parts in groups.values())} house pieces across {len(groups)} material groups.")


def add_details(groups):
    """Set dressing uses Unity coordinates. Solid gameplay objects stay in Unity."""
    from math import radians
    import random
    rng = random.Random(86)

    def piece(name, p, size, material, yaw=0, shape="cube"):
        x, y, z = p
        if shape == "cylinder":
            bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.5, depth=1, location=(-x, -z, y))
        else:
            bpy.ops.mesh.primitive_cube_add(size=1, location=(-x, -z, y))
        ob = bpy.context.object
        ob.name = name
        w, h, d = size
        ob.dimensions = (w, d, h)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        ob.rotation_euler.z = -radians(yaw)
        ob.data.materials.append(bpy.data.materials[material])
        bevel = ob.modifiers.new("Worn edges", "BEVEL")
        bevel.width = min(.018, min(size) * .20)
        bevel.segments = 2
        groups.setdefault(material, []).append(ob)

    oak, wood, metal = "Dark Oak", "Worn Boards", "Oxidized Metal"
    for side in (-1, 1):
        for z in (-5.8, -1.65, 1.65, 5.8):
            for y in (.09, .92, 2.72):
                piece("Milled hall molding", (side * 1.287, y, z), (.09, .076, 1.7), oak)
            for k in range(11):
                piece("Split wainscot", (side * 1.30, .48, z + (k - 5) * .152),
                      (.055, .75, .146), wood, rng.uniform(-.9, .9))
    for z in (-3.425, 3.425):
        for x in (-1.43, 1.43):
            for j in (-1, 1):
                for k in (-1, 1):
                    piece("Stepped door casing", (x + j * .16, 1.11, z + k * .87),
                          (.05, 2.25, .12), oak)
            piece("Carved header", (x, 2.26, z), (.41, .13, 1.92), oak)
    for x, z, direction in ((-4.6, 6.87, -1), (5.1, -6.86, 1), (-3.6, -6.86, 1)):
        for dx in (-.85, -.27, .27, .85):
            piece("Window mullion", (x+dx,1.74,z+direction*.15), (.064,1.20,.084), oak)
        for dy in (-.59, .06, .60):
            piece("Window crossbar", (x,1.74+dy,z+direction*.15), (1.75,.061,.08), oak)
    for z in (-4.8, 1.7, 5.9):
        piece("Pendant socket", (0,2.57,z), (.31,.20,.31), metal, shape="cylinder")
        piece("Pendant shade", (0,2.47,z), (.43,.10,.43), metal, shape="cylinder")
    for x, y, z in ((9.3,-3,-12.5), (9.32,-3,-8.7), (6.5,0,6.34), (-6.33,0,-1.6)):
        piece("Old cask", (x,y+.36,z), (.64,.72,.64), wood, shape="cylinder")
        for h in (.15,.56):
            piece("Iron hoop", (x,y+h,z), (.67,.071,.67), metal, shape="cylinder")
        piece("Barrel lid", (x,y+.729,z), (.56,.032,.56), oak, shape="cylinder")
    for x, z, y in ((-6.5,-4.9,0),(6.56,-6.45,0),(3.01,-13.24,-3),(3.75,-13.25,-3)):
        piece("Supply crate", (x,y+.24,z), (.63,.48,.58), wood, yaw=rng.randrange(-14,15))
        for dx in (-.22,.22):
            piece("Crate batten", (x+dx,y+.49,z), (.083,.046,.57), oak)
    for i in range(22):
        side = rng.choice((-1, 1))
        piece("Floor splinter", (side*rng.uniform(.96,1.18),.018,rng.uniform(-6.5,6.5)),
              (rng.uniform(.025,.10),.023,rng.uniform(.07,.27)), wood, yaw=rng.randrange(180))
    # Signs of former occupants, placed clear of the playable path.
    for side, z in ((-1, -1.42), (1, -.58), (-1, 5.58)):
        x = side * 1.285
        for height, offset in ((1.78,0), (1.43,.46)):
            piece("Picture frame", (x,height,z+offset), (.06,.50,.36), oak, rng.uniform(-3,3))
            piece("Yellowed picture backing", (x-side*.034,height,z+offset), (.011,.39,.26), "Damp Plaster")
            piece("Dark portrait plate", (x-side*.041,height+.024,z+offset), (.009,.27,.18), metal)
    for y in (1.39,1.82,2.23):
        piece("Kitchen shelf", (6.62,y,1.50), (.51,.07,1.92), oak)
        for z in (.86,1.46,2.05):
            diameter = rng.uniform(.09,.17)
            height = rng.uniform(.13,.24)
            piece("Preserve tin", (6.64,y+.04+height*.5,z), (diameter,height,diameter), metal, shape="cylinder")
            piece("Tin cap", (6.64,y+.049+height,z), (diameter+.008,.018,diameter+.008), "Brass", shape="cylinder")
    for z in (-12.7,-11.68,-9.53):
        piece("Brick cellar pilaster", (1.15,-1.64,z), (.26,2.74,.29), "Cellar Brick")
        piece("Cellar pipe strap", (1.37,-.79,z), (.25,.17,.07), metal)
    for y in (-.78,-1.02):
        piece("Utility conduit", (1.36,y,-11.66), (.065,.065,4.8), metal)
    for x, z in ((2.65,-12.98),(4.12,-13.42),(7.78,-12.94)):
        for k in range(4):
            piece("Cellar rubble", (x+rng.uniform(-.24,.24),-2.965,z+rng.uniform(-.18,.18)),
                  (rng.uniform(.13,.23),.09,.12), "Cellar Brick", rng.randrange(180))


if __name__ == "__main__":
    export_house(Path(__file__).resolve().parents[1])
