using System.IO;
using System.Linq;
using DayOfDream.Horror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Editor
{
    public static class HorrorSchoolPolish
    {
        [MenuItem("Tools/Day Of Dream/Apply Night Exploration Fixes")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode before saving exploration fixes.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != HorrorSchoolBuilder.ScenePath)
                throw new System.InvalidOperationException("Open the night school scene first.");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.018f,.026f,.043f);
            RenderSettings.ambientEquatorColor = new Color(.006f,.011f,.018f);
            RenderSettings.ambientGroundColor = new Color(.001f,.002f,.003f);
            RenderSettings.fogColor = new Color(.002f,.004f,.007f);
            RenderSettings.fogDensity = .012f;
            RenderSettings.reflectionIntensity = .025f;
            var moon=GameObject.Find("Moonlight").GetComponent<Light>();moon.intensity=.045f;
            var player=Object.FindAnyObjectByType<FirstPersonExplorer>();
            var camera=player.GetComponentInChildren<Camera>();camera.backgroundColor=new Color(.001f,.002f,.004f);
            // Rendered cracks and uneven paving remain visible; this simple slab
            // provides continuous contacts across tiny gaps in the art mesh.
            var ground=GameObject.Find("Continuous Campus Ground");
            if(ground==null)
            {
                ground=new GameObject("Continuous Campus Ground");var collider=ground.AddComponent<BoxCollider>();
                ground.transform.position=new Vector3(0,-.34f,-2);collider.size=new Vector3(132,1,134);ground.isStatic=true;
            }
            HorrorSchoolBuilder.EnsureAccessPlatforms();
            if(GameObject.Find("Entrance Threshold Step")==null)
            {
                var threshold=GameObject.CreatePrimitive(PrimitiveType.Cube);threshold.name="Entrance Threshold Step";
                threshold.transform.position=new Vector3(0,.27f,-16.45f);threshold.transform.localScale=new Vector3(2.6f,.2f,1.1f);
                var stone=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/Environment/HaojieSchool/Materials"})
                    .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First(m=>m.name.Contains("CONCRETE"));
                threshold.GetComponent<Renderer>().sharedMaterial=stone;threshold.isStatic=true;
            }
            EnsureDoors();
            var hud=Object.FindAnyObjectByType<HorrorHud>();hud.transform.SetParent(player.transform,false);
            foreach(var text in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                text.text=text.text.Replace("浩杰","鹅企");
                text.font.TryAddCharacters("鹅企跳跃空格交互开关门");EditorUtility.SetDirty(text.font);
            }
            var school=GameObject.Find("Haojie School");if(school!=null)school.name="鹅企中学 — EQI Middle School";
            PrefabUtility.SaveAsPrefabAsset(player.gameObject,"Assets/Prefabs/Horror/TomorinExplorer.prefab");
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            SchoolExplorationExpansion.Apply();
        }

        private static void EnsureDoors()
        {
            if(GameObject.Find("Interactive Main Doors")!=null)return;
            var root=new GameObject("Interactive Main Doors");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.045f,.075f,.065f);
            material.SetFloat("_Smoothness",.25f);
            const string path="Assets/Art/Environment/HaojieSchool/Materials/InteractiveDoor.mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing==null)AssetDatabase.CreateAsset(material,path);else {Object.DestroyImmediate(material);material=existing;}
            var doors=new HingedDoor[2];
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                var pivot=new GameObject(side<0?"Left door hinge":"Right door hinge");pivot.transform.SetParent(root.transform);
                pivot.transform.position=new Vector3(side*2.3f,.46f,-14.88f);
                var rb=pivot.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;
                var leaf=GameObject.CreatePrimitive(PrimitiveType.Cube);leaf.name="Door Leaf";leaf.transform.SetParent(pivot.transform,false);
                leaf.transform.localPosition=new Vector3(-side*1.13f,1.55f,0);leaf.transform.localScale=new Vector3(2.25f,3.1f,.14f);leaf.GetComponent<Renderer>().sharedMaterial=material;
                var handle=GameObject.CreatePrimitive(PrimitiveType.Cube);handle.name="Door Handle";handle.transform.SetParent(pivot.transform,false);
                handle.transform.localPosition=new Vector3(-side*2.05f,1.35f,.13f);handle.transform.localScale=new Vector3(.035f,.32f,.1f);handle.GetComponent<Renderer>().sharedMaterial=material;
                Object.DestroyImmediate(handle.GetComponent<Collider>());
                doors[i]=pivot.AddComponent<HingedDoor>();
            }
            doors[0].Configure(100,doors[1]);doors[1].Configure(-100,doors[0]);
            // Unity's mirrored FBX X axis makes the left hinge's +100 degree
            // rotation open inward, toward the teaching block.
        }
    }
}
