import bpy, json
from pathlib import Path
out=Path(r'D:\unity\unity_file\project\DayOfDream\Tools')
data={'scene':bpy.context.scene.name,'objects':[],'materials':[],'lights':[]}
for ob in bpy.context.scene.objects:
    if ob.type=='LIGHT':
        data['lights'].append({'name':ob.name,'type':ob.data.type,'position':list(ob.location),'color':list(ob.data.color),'energy':ob.data.energy})
    else:
        data['objects'].append({'name':ob.name,'type':ob.type,'vertices':len(ob.data.vertices) if ob.type=='MESH' else 0,'position':list(ob.location),'dimensions':list(ob.dimensions),'collections':[c.name for c in ob.users_collection]})
for m in bpy.data.materials:
    data['materials'].append({'name':m.name,'nodes':[n.type for n in m.node_tree.nodes] if m.use_nodes else []})
(out/'school_inventory.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
print('INVENTORY',len(data['objects']),len(data['materials']),len(data['lights']))
