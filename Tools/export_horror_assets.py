"""Run with Blender --background <source.blend> --python Tools/export_horror_assets.py.
Writes game exports only. The original school .blend is never saved or modified.
"""
import bpy, bmesh, math, json, re
from pathlib import Path
from collections import defaultdict
from mathutils import Vector, noise

ROOT=Path(r'D:\unity\unity_file\project\DayOfDream')
OUT=ROOT/'Assets/Art/Environment/HaojieSchool'
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'Textures').mkdir(exist_ok=True)
scene=bpy.context.scene
# Rename text in the game export while preserving the external art source.
for label in scene.objects:
    if label.type=='FONT':
        label.data=label.data.copy()
        label.data.body=label.data.body.replace('浩 杰','鹅 企').replace('浩杰','鹅企').replace('HAOJIE','EQI')
manifest={'source':bpy.data.filepath,'materials':[],'lights':[],'chunks':[],
    'labels':[{'name':label.name,'text':label.data.body} for label in scene.objects if label.type=='FONT']}

def safe(name): return re.sub(r'[^A-Za-z0-9_]+','_',name).strip('_')

# Preserve material identity and recreate the source's weathering as tiled maps.
for i,m in enumerate(list(bpy.data.materials)):
    principled=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if m.use_nodes else None
    if not principled: continue
    original=m.name; m.name=f'School_{i:02d}_{safe(original)}'
    base=list(principled.inputs['Base Color'].default_value)
    ramps=[n for n in m.node_tree.nodes if n.type=='VALTORGB']
    ramp=next((n for n in ramps if n.name=='Color Ramp'),ramps[0] if ramps else None)
    lo=list(ramp.color_ramp.elements[0].color) if ramp else base
    hi=list(ramp.color_ramp.elements[-1].color) if ramp else base
    emission=list(principled.inputs['Emission Color'].default_value)
    strength=principled.inputs['Emission Strength'].default_value
    texname=''
    if ramp:
        side=256; texname=m.name+'.png'; img=bpy.data.images.new(m.name,side,side)
        pixels=[]
        for y in range(side):
            for x in range(side):
                u=x/side; v=y/side
                # Sample a torus for seamless tiling.
                p=Vector((math.cos(u*math.tau)*2,math.sin(u*math.tau)*2,0))
                q=Vector((math.cos(v*math.tau)*2,math.sin(v*math.tau)*2,0))
                t=max(0,min(1,.5+.65*noise.noise_vector(p+Vector((0,0,q.x)))[0]+.10*noise.noise_vector(p*11+q*7)[1]))
                rgb=[lo[c]*(1-t)+hi[c]*t for c in range(3)]
                pixels.extend((*rgb,1))
        img.pixels.foreach_set(pixels); img.filepath_raw=str(OUT/'Textures'/texname); img.file_format='PNG'; img.save()
    manifest['materials'].append({'name':m.name,'sourceName':original,'color':base,'texture':texname,'roughness':float(principled.inputs['Roughness'].default_value),'metallic':float(principled.inputs['Metallic'].default_value),'emission':emission,'emissionStrength':float(strength)})

exports=[]
deps=bpy.context.evaluated_depsgraph_get()
for ob in list(scene.objects):
    if ob.type=='LIGHT':
        manifest['lights'].append({'name':ob.name,'type':ob.data.type,'position':list(ob.location),'rotation':list(ob.rotation_euler),'color':list(ob.data.color),'energy':ob.data.energy})
        continue
    if ob.type not in {'MESH','FONT','CURVE'} or ob.name.startswith('FOG'): continue
    ev=ob.evaluated_get(deps); mesh=bpy.data.meshes.new_from_object(ev,depsgraph=deps)
    coords=[ob.matrix_world@v.co for v in mesh.vertices]
    # The presentation model's classroom partitions originally crossed the rear
    # corridor. Shorten those partitions and cut doors in the main block ends.
    # Work on the evaluated export mesh only; never save the source .blend.
    if ob.name.startswith('ACADEMIC'):
        origin=(0,23) if 'ACADEMIC A' in ob.name else ((-30,-.6) if 'ACADEMIC B' in ob.name else (30,-.6))
        angle=0 if 'ACADEMIC A' in ob.name else (math.pi/2 if 'ACADEMIC B' in ob.name else -math.pi/2)
        ca,sa=math.cos(angle),math.sin(angle)
        def local(v):
            dx,dy=v.x-origin[0],v.y-origin[1]
            return Vector((dx*ca+dy*sa,-dx*sa+dy*ca,v.z))
        def world(v):return Vector((origin[0]+v.x*ca-v.y*sa,origin[1]+v.x*sa+v.y*ca,v.z))
        faces=list(mesh.polygons)
        for start in range(0,len(faces)-5):
            polys=faces[start:start+6]
            ids={j for p in polys for j in p.vertices}
            if len(ids)!=24:continue
            vv=[local(coords[j]) for j in ids]
            low=Vector(tuple(min(v[a] for v in vv) for a in range(3)));high=Vector(tuple(max(v[a] for v in vv) for a in range(3)))
            extent=high-low
            if abs(extent.x-.18)<.005 and abs(extent.y-11.8)<.005 and abs(extent.z-3.9)<.005:
                for j in ids:
                    v=local(coords[j]);v.y=min(v.y,2.83);coords[j]=world(v)
        # Carve a person-sized end doorway by replacing each end-wall box.
        if ob.name.startswith('ACADEMIC'):
            removed=set();added=[]
            for start in range(0,len(faces)-5):
                if any(p.index in removed for p in faces[start:start+6]):continue
                polys=faces[start:start+6];ids={j for p in polys for j in p.vertices}
                if len(ids)!=24:continue
                vv=[local(coords[j]) for j in ids]
                low=Vector(tuple(min(v[a] for v in vv) for a in range(3)));high=Vector(tuple(max(v[a] for v in vv) for a in range(3)));extent=high-low
                if abs(extent.x-.3)<.005 and abs(extent.y-11.8)<.005 and abs(extent.z-3.98)<.005:
                    removed.update(p.index for p in polys)
                    def cuboid(y0,y1,z0,z1):
                        vv=[world(Vector((x,y,z))) for x,y,z in [(low.x,y0,z0),(high.x,y0,z0),(high.x,y1,z0),(low.x,y1,z0),(low.x,y0,z1),(high.x,y0,z1),(high.x,y1,z1),(low.x,y1,z1)]]
                        for f in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:added.append(([vv[j] for j in f],polys[0].material_index))
                    cuboid(low.y,3.0,low.z,high.z);cuboid(5.65,high.y,low.z,high.z);cuboid(3.0,5.65,low.z+2.8,high.z)
            if removed:
                verts=[];newfaces=[];materials=[]
                for p in faces:
                    if p.index in removed:continue
                    first=len(verts);verts.extend(coords[j] for j in p.vertices);newfaces.append(tuple(range(first,len(verts))));materials.append(p.material_index)
                for points,mid in added:
                    first=len(verts);verts.extend(points);newfaces.append(tuple(range(first,len(verts))));materials.append(mid)
                replacement=bpy.data.meshes.new('Walkable end doorways');replacement.from_pydata(verts,[],newfaces);replacement.update()
                for mat in mesh.materials:replacement.materials.append(mat)
                for p,mid in zip(replacement.polygons,materials):p.material_index=mid
                bpy.data.meshes.remove(mesh);mesh=replacement;coords=[v.co.copy() for v in mesh.vertices]
    # Cut corridor and classroom portals against evaluated geometry. This also
    # handles bevelled/welded source walls that are no longer six-face boxes.
    if ob.name.startswith('ACADEMIC'):
        bays=16 if 'ACADEMIC A' in ob.name else 8
        portals=[]
        for floor in range(4):
            top=.55+floor*4.2
            portals.append(((-bays*2.1-.6,3.16,top+.04),(bays*2.1+.6,5.68,top+2.9)))
            if floor==3:continue
            for bay in range(bays):
                center=-bays*2.1+(bay+.5)*4.2+1.63
                portals.append(((center-.61,2.65,top+.04),(center+.61,3.22,top+2.7)))
        def outside_box(points,lo,hi):
            if any(max(v[a] for v in points)<=lo[a] or min(v[a] for v in points)>=hi[a] for a in range(3)):return [points]
            pieces=[];remaining=points
            for axis,bound,below in [(0,lo[0],False),(0,hi[0],True),(1,lo[1],False),(1,hi[1],True),(2,lo[2],False),(2,hi[2],True)]:
                if len(remaining)<3:break
                kept=[];other=[]
                for a,b in zip(remaining,remaining[1:]+remaining[:1]):
                    ain=a[axis]<=bound if below else a[axis]>=bound
                    bin=b[axis]<=bound if below else b[axis]>=bound
                    (kept if ain else other).append(a)
                    if ain!=bin:
                        cross=a+(b-a)*((bound-a[axis])/(b[axis]-a[axis]));kept.append(cross);other.append(cross)
                if len(other)>=3:pieces.append(other)
                remaining=kept
            return pieces
        vertices=[];faces=[];mids=[]
        for p in mesh.polygons:
            pieces=[[local(coords[j]) for j in p.vertices]]
            for lo,hi in portals:
                pieces=[fragment for points in pieces for fragment in outside_box(points,lo,hi)]
                if not pieces:break
            for piece in pieces:
                first=len(vertices);vertices.extend(world(v) for v in piece);faces.append(tuple(range(first,len(vertices))));mids.append(p.material_index)
        replacement=bpy.data.meshes.new('Continuous corridors and classroom portals');replacement.from_pydata(vertices,[],faces);replacement.update()
        for mat in mesh.materials:replacement.materials.append(mat)
        for p,mid in zip(replacement.polygons,mids):p.material_index=mid
        bpy.data.meshes.remove(mesh);mesh=replacement;coords=[v.co.copy() for v in mesh.vertices]
    # Make the central entrance a real passage, rather than walking through
    # a door that opens onto an intact facade and a classroom partition.
    portal=None
    if ob.name.startswith('ENTRANCE'):portal=((-2.32,14.3,.30),(2.32,16.9,3.65))
    elif ob.name.startswith('ACADEMIC A'):portal=((-1.30,16.8,.60),(1.30,29.2,3.40))
    elif ob.name.startswith('INTERIORS'):portal=((-1.30,16.8,.60),(1.30,29.2,3.40))
    if portal:
        lo,hi=portal
        def split_polygon(points,axis,bound,below):
            kept=[];other=[]
            for a,b in zip(points,points[1:]+points[:1]):
                ain=(a[axis]<=bound) if below else (a[axis]>=bound)
                bin=(b[axis]<=bound) if below else (b[axis]>=bound)
                (kept if ain else other).append(a)
                if ain!=bin:
                    t=(bound-a[axis])/(b[axis]-a[axis]);cross=a+(b-a)*t
                    kept.append(cross);other.append(cross)
            return kept,other
        vertices=[];faces=[];mids=[]
        for p in mesh.polygons:
            points=[coords[j] for j in p.vertices]
            if any(max(v[a] for v in points)<=lo[a] or min(v[a] for v in points)>=hi[a] for a in range(3)):
                pieces=[points]
            else:
                pieces=[];remaining=points
                for axis,bound,below in [(0,lo[0],False),(0,hi[0],True),(1,lo[1],False),(1,hi[1],True),(2,lo[2],False),(2,hi[2],True)]:
                    if len(remaining)<3:break
                    remaining,outside=split_polygon(remaining,axis,bound,below)
                    if len(outside)>=3:pieces.append(outside)
            for piece in pieces:
                first=len(vertices);vertices.extend(piece);faces.append(tuple(range(first,len(vertices))));mids.append(p.material_index)
        replacement=bpy.data.meshes.new('Walkable central entrance');replacement.from_pydata(vertices,[],faces);replacement.update()
        for mat in mesh.materials:replacement.materials.append(mat)
        for p,mid in zip(replacement.polygons,mids):p.material_index=mid
        bpy.data.meshes.remove(mesh);mesh=replacement;coords=[v.co.copy() for v in mesh.vertices]
    groups=defaultdict(list)
    chunked=max(ob.dimensions)>22 and len(mesh.polygons)>500
    for poly in mesh.polygons:
        center=sum((coords[j] for j in poly.vertices),Vector())/len(poly.vertices)
        key=(math.floor(center.x/12),math.floor(center.y/12),max(0,math.floor(center.z/4.2))) if chunked else (0,0,0)
        groups[key].append(poly)
    for key,polys in groups.items():
        verts=[]; faces=[]; mids=[]
        for p in polys:
            first=len(verts); verts.extend([coords[j] for j in p.vertices]); faces.append(tuple(range(first,len(verts)))); mids.append(p.material_index)
        name=safe(ob.name)+f'_{key[0]}_{key[1]}_{key[2]}'
        target=bpy.data.meshes.new(name);target.from_pydata(verts,[],faces);target.update()
        for mat in mesh.materials: target.materials.append(mat)
        uv=target.uv_layers.new(name='UVMap')
        for p,mid in zip(target.polygons,mids):
            p.material_index=mid
            axis=max(range(3),key=lambda j:abs(p.normal[j]))
            axes=((1,2),(0,2),(0,1))[axis]
            for li in p.loop_indices:
                co=target.vertices[target.loops[li].vertex_index].co
                uv.data[li].uv=(co[axes[0]]/3,co[axes[1]]/3)
        clone=bpy.data.objects.new(name,target);scene.collection.objects.link(clone);exports.append(clone)
        # Export explicit triangles: Unity otherwise drops some of the source's
        # complex beveled ngons as self-intersecting during FBX triangulation.
        bm=bmesh.new();bm.from_mesh(target)
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        tiny=[f for f in bm.faces if f.calc_area()<1e-10]
        if tiny:bmesh.ops.delete(bm,geom=tiny,context='FACES')
        bm.to_mesh(target);bm.free();target.update()
        manifest['chunks'].append({'name':name,'source':ob.name,'vertices':len(verts),'faces':len(faces),'collision':not (ob.name.startswith(('MOON','DECAY','DETAIL')) or ob.type=='FONT')})
    bpy.data.meshes.remove(mesh)

bpy.ops.object.select_all(action='DESELECT')
for o in exports:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'HaojieSchool.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
(OUT/'school_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
print('SCHOOL_EXPORT',len(exports),'chunks',sum(len(o.data.polygons) for o in exports),'faces',flush=True)


# Export the independent prop separately with --factory-startup and Tools/export_flashlight.py.
