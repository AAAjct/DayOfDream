using System;
using System.IO;
using System.Linq;
using DayOfDream.Horror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DayOfDream.Editor
{
    public static class SchoolExplorationExpansion
    {
        [MenuItem("Tools/Day Of Dream/Connect All School Floors")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != HorrorSchoolBuilder.ScenePath) throw new InvalidOperationException("Open the night school scene.");
            var stone=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/Environment/HaojieSchool/Materials"})
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First(m=>m.name.Contains("CONCRETE"));
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/SchoolChinese SDF.asset");
            var manifest=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Art/Environment/HaojieSchool/school_manifest.json"));
            var solids=manifest["chunks"].ToDictionary(c=>(string)c["name"],c=>(bool)c["collision"]);
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>())
            {
                if(!solids.TryGetValue(filter.name,out bool solid)||!solid)continue;
                var collider=filter.GetComponent<MeshCollider>()??filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
            }
            foreach(var collider in UnityEngine.Object.FindObjectsByType<MeshCollider>())
                if(collider.name.StartsWith("STAIRS"))collider.enabled=false;
            var root=GameObject.Find("Connected School Exploration")??new GameObject("Connected School Exploration");
            Directory.CreateDirectory("Assets/Art/Environment/HaojieSchool/Collision");
            GameObject Box(string name,Vector3 center,Vector3 size,bool visible=true)
            {
                var existing=root.transform.Find(name);
                var ob=existing!=null?existing.gameObject:GameObject.CreatePrimitive(PrimitiveType.Cube);ob.name=name;ob.transform.SetParent(root.transform);
                ob.transform.SetPositionAndRotation(center,Quaternion.identity);ob.transform.localScale=size;ob.GetComponent<Renderer>().sharedMaterial=stone;ob.GetComponent<Renderer>().enabled=visible;ob.isStatic=true;return ob;
            }
            // The art mesh has individual treads. A continuous wedge prevents the
            // capsule from catching their front faces or the next-floor landing.
            for(int side=-1;side<=1;side+=2)
            {
                for(int floor=0;floor<3;floor++)
                {
                    string name=$"StairRamp_{side}_{floor}";
                    {
                        float x=side*37,low=.55f+floor*4.2f,high=low+4.2f;
                        float front=-16.65f,back=-24.2f,half=.94f;
                        var vertices=new[]{new Vector3(x-half,low-.18f,front),new Vector3(x+half,low-.18f,front),new Vector3(x-half,low,front),new Vector3(x+half,low,front),
                            new Vector3(x-half,high-.18f,back),new Vector3(x+half,high-.18f,back),new Vector3(x-half,high,back),new Vector3(x+half,high,back)};
                        var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=new[]{2,3,7,2,7,6,0,4,5,0,5,1,0,1,3,0,3,2,4,6,7,4,7,5,0,2,6,0,6,4,1,5,7,1,7,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
                        string path=$"Assets/Art/Environment/HaojieSchool/Collision/{name}.asset";
                        var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if(asset==null)AssetDatabase.CreateAsset(mesh,path);
                        else
                        {
                            asset.Clear();asset.vertices=mesh.vertices;asset.triangles=mesh.triangles;asset.RecalculateNormals();asset.RecalculateBounds();EditorUtility.SetDirty(asset);
                            UnityEngine.Object.DestroyImmediate(mesh);mesh=asset;
                        }
                        var existingRamp=root.transform.Find(name);
                        var ob=existingRamp!=null?existingRamp.gameObject:new GameObject(name,typeof(MeshCollider));ob.transform.SetParent(root.transform);
                        ob.GetComponent<MeshCollider>().sharedMesh=null;ob.GetComponent<MeshCollider>().sharedMesh=mesh;ob.isStatic=true;
                    }
                    for(int edge=-1;edge<=1;edge+=2)
                    {
                        float low=.55f+floor*4.2f;
                        // Leave a real side-entry gap at the start of each
                        // flight; the return walkway approaches from outside.
                        var guard=Box($"Stair guard {side} {floor} {edge}",new Vector3(side*37+edge*1.04f,low+2.92f,-20.85f),new Vector3(.08f,1.1f,7.67f),false);
                        guard.transform.rotation=Quaternion.Euler(29.1f,0,0);
                    }
                }
                for(int floor=0;floor<4;floor++)
                {
                    float top=.55f+floor*4.2f;
                    if(floor==3)
                    {
                        Box($"Roof landing {side}",new Vector3(side*37,top-.1f,-26.4f),new Vector3(6.8f,.2f,4.65f));
                        Box($"Roof return {side}",new Vector3(side*39.5f,top-.1f,-20.25f),new Vector3(1.8f,.2f,8));
                        Box($"Roof wing connector {side}",new Vector3(side*38.2f,top-.1f,-16.6f),new Vector3(4.4f,.2f,1.3f));
                        Box($"Roof outer rail {side}",new Vector3(side*40.4f,top+.6f,-22.2f),new Vector3(.09f,1.2f,13));
                        Box($"Roof rear rail {side}",new Vector3(side*37,top+.6f,-28.72f),new Vector3(6.8f,1.2f,.09f));
                    }
                    // Wing corridor portals now meet the same external landings
                    // and return walkways as the main block on all three floors.
                    Box($"Wing link {side} {floor}",new Vector3(side*35.6f,top-.1f,-16.1f),new Vector3(4.6f,.2f,1.8f));
                    // This edge continues into the wing corridor; an end rail
                    // here would close the corridor immediately after its door.
                    var obsoleteGuard=root.transform.Find($"Wing guard {side} {floor}");
                    if(obsoleteGuard!=null)obsoleteGuard.gameObject.SetActive(false);
                    // A full-width floor under each corridor keeps visual mesh
                    // seams from becoming narrow holes in the walkable surface.
                    Box($"Main corridor floor {side} {floor}",new Vector3(side*16.6f,top-.12f,-27.3f),new Vector3(33.5f,.24f,2.5f),false);
                    Sign(root.transform,font,$"Wayfinding {side} {floor}",floor==3?"屋顶平台\nROOF ACCESS":$"{floor+1}F  教学区\n主楼教室  /  侧楼教室",new Vector3(side*38.5f,top+1.7f,-26.7f),side<0?-90:90);
                    var lightName=$"Landing guide {side} {floor}";
                    if(root.transform.Find(lightName)==null)
                    {
                        var glow=new GameObject(lightName,typeof(Light));glow.transform.SetParent(root.transform);glow.transform.position=new Vector3(side*37,top+2.7f,-25.8f);
                        var lamp=glow.GetComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(.38f,.65f,.55f);lamp.intensity=.7f;lamp.range=5;lamp.shadows=LightShadows.None;
                    }
                }
            }
            // Tiny approach steps are replaced by a walkable shallow ramp, too.
            for(int side=-1;side<=1;side+=2)
            {
                var approach=Box($"Ground approach {side}",new Vector3(side*37,.26f,-14.6f),new Vector3(1.9f,.22f,1.64f));
                approach.transform.rotation=Quaternion.Euler(12.85f,0,0);
            }
            AddDiscovery(root.transform,font,stone,"Archive note","旧资料室","停课前最后一次清点，少了三份借阅记录。\n\n二楼办公室留着交接说明。",new Vector3(10.1f,1.5f,-22.2f));
            AddDiscovery(root.transform,font,stone,"Office note","二楼办公室","教学区电路巡检已停止。\n夜间值班请改走外侧楼梯。\n\n三楼留下了一张没有署名的值班表。",new Vector3(18.4f,5.7f,-22.2f));
            AddDiscovery(root.transform,font,stone,"Broadcast note","三楼广播室","值班表停在那一夜，名单只写了一半。\n最后一行，是一个反复划掉的名字。\n\n窗外的灯还亮着，走廊却没有脚步声。",new Vector3(-14.6f,9.9f,-22.2f));
            AddDiscovery(root.transform,font,stone,"Rooftop note","屋顶值班记录","这里能看见整座庭院。\n最后离开的人，没有在交接簿上签名。\n\n请在天亮之前关好所有的门。",new Vector3(0,13.2f,-24.5f));
            font.TryAddCharacters("教学区主楼教室侧楼资料室二办公室三广播值班表鹅企中学屋顶平台记录");EditorUtility.SetDirty(font);
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);AssetDatabase.SaveAssets();
        }

        private static void Sign(Transform parent,TMP_FontAsset font,string name,string text,Vector3 position,float yaw)
        {
            var existing=parent.Find(name);
            var ob=existing!=null?existing.gameObject:new GameObject(name,typeof(TextMeshPro));ob.transform.SetParent(parent);ob.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            var label=ob.GetComponent<TextMeshPro>();label.font=font;label.text=text;label.fontSize=2.2f;label.alignment=TextAlignmentOptions.Center;label.color=new Color(.6f,.83f,.72f);
            label.rectTransform.sizeDelta=new Vector2(2.5f,1.0f);label.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            AnonHouseVisuals.StyleSign(label);
        }
        private static void AddDiscovery(Transform parent,TMP_FontAsset font,Material material,string name,string title,string body,Vector3 position)
        {
            if(parent.Find(name)!=null)
            {
                Sign(parent,font,name+" label",title+"\nE 阅读记录",position+new Vector3(0,.25f,0),0);
                return;
            }
            var paper=GameObject.CreatePrimitive(PrimitiveType.Cube);paper.name=name;paper.transform.SetParent(parent);paper.transform.position=position;paper.transform.localScale=new Vector3(.5f,.035f,.35f);paper.GetComponent<Renderer>().sharedMaterial=material;
            paper.AddComponent<ReadableNote>().Configure(title,body);
            Sign(parent,font,name+" label",title+"\nE 阅读记录",position+new Vector3(0,.25f,0),0);
        }
    }
}
