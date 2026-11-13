using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DayOfDream.Horror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Editor
{
    public static class HorrorSchoolBuilder
    {
        public const string ScenePath = "Assets/Scenes/HaojieSchool_Night.unity";
        private const string SchoolRoot = "Assets/Art/Environment/HaojieSchool/";
        [Serializable] private sealed class Manifest { public MaterialInfo[] materials; public LightInfo[] lights; public ChunkInfo[] chunks; }
        [Serializable] private sealed class MaterialInfo
        { public string name, sourceName, texture; public float[] color, emission; public float roughness, metallic, emissionStrength; }
        [Serializable] private sealed class LightInfo { public string name, type; public float[] position, color; public float energy; }
        [Serializable] private sealed class ChunkInfo { public string name; public bool collision; }

        [MenuItem("Tools/Day Of Dream/Create Night School Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before building the school scene.");
            // Refuse to replace an authored scene or discard unsaved work.
            if (File.Exists(ScenePath)) throw new InvalidOperationException("School scene already exists. Edit it directly; the builder never overwrites it.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            AssetDatabase.Refresh();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(SchoolRoot + "school_manifest.json"));
            PrepareImporter(SchoolRoot + "HaojieSchool.fbx");
            CreateSchoolMaterials(manifest);
            var font = PrepareFont();
            var schoolAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SchoolRoot + "HaojieSchool.fbx");
            var tomorinAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Tomorin/Tomorin.fbx");
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            if (schoolAsset == null || tomorinAsset == null || input == null || font == null)
                throw new InvalidOperationException("Missing school, Tomorin, font or input source.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var school = (GameObject)PrefabUtility.InstantiatePrefab(schoolAsset);
            school.name = "鹅企中学 — EQI Middle School";
            var collision = manifest.chunks.ToDictionary(c => c.name, c => c.collision);
            int colliderCount = 0;
            foreach (var filter in school.GetComponentsInChildren<MeshFilter>())
            {
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                if (collision.TryGetValue(filter.name, out bool solid) && solid)
                { filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh; colliderCount++; }
            }
            BuildLighting(manifest);
            EnsureAccessPlatforms();
            var hud = BuildHud(font);
            var player = new GameObject("Tomorin — First Person");
            player.transform.SetPositionAndRotation(new Vector3(0, .20f, 35.5f), Quaternion.Euler(0, 180, 0));
            var capsule = player.AddComponent<CharacterController>();
            capsule.height = 1.8f; capsule.radius = .28f; capsule.center = new Vector3(0, .9f, 0);
            capsule.stepOffset = .28f; capsule.slopeLimit = 48; capsule.skinWidth = .025f; capsule.minMoveDistance = 0;
            var explorer = player.AddComponent<FirstPersonExplorer>();
            var cameraObject = new GameObject("First Person Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, .055f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 72; camera.nearClipPlane = .035f; camera.farClipPlane = 180;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.008f, .015f, .024f);
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true; cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var body = (GameObject)PrefabUtility.InstantiatePrefab(tomorinAsset);
            body.name = "Tomorin Body"; body.transform.SetParent(player.transform, false);
            var bounds = CombinedBounds(body);
            float scale = 1.72f / bounds.size.y;
            body.transform.localScale = Vector3.one * scale;
            body.transform.localPosition = new Vector3(0, -bounds.min.y * scale, -.13f);
            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            var bones = body.GetComponentsInChildren<Transform>();
            PoseArm(bones, "arm_stretch_l", "forearm_stretch_l", new Vector3(-.08f, -1, .12f));
            PoseArm(bones, "arm_stretch_r", "forearm_stretch_r", new Vector3(.08f, -1, .12f));
            var animation = body.AddComponent<TomorinFirstPersonBody>();
            animation.Configure(explorer, bones.FirstOrDefault(t => t.name == "leftthigh"), bones.FirstOrDefault(t => t.name == "rightthigh"));
            explorer.Configure(input, camera.transform, hud, null);
            hud.transform.SetParent(player.transform, false);
            CamcorderSetup.Attach(explorer);
            AddNotice();
            Directory.CreateDirectory("Assets/Prefabs/Horror");
            PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Horror/TomorinExplorer.prefab");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var builds = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            builds.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = builds.ToArray();
            Selection.activeGameObject = player;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 7, -5), Quaternion.Euler(15, 180, 0), 45);
            AssetDatabase.SaveAssets();
            HorrorSchoolPolish.Apply();
            Debug.Log($"Night school created: {school.GetComponentsInChildren<MeshRenderer>().Length} mesh chunks, {colliderCount} colliders. Play with WASD, mouse, Shift, Ctrl, right click, F and E.");
        }

        private static void PrepareImporter(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.isReadable = false; importer.SaveAndReimport();
        }

        private static void CreateSchoolMaterials(Manifest manifest)
        {
            Directory.CreateDirectory(SchoolRoot + "Materials");
            var importer = (ModelImporter)AssetImporter.GetAtPath(SchoolRoot + "HaojieSchool.fbx");
            foreach (var info in manifest.materials)
            {
                string path = SchoolRoot + "Materials/" + info.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
                material.SetColor("_BaseColor", string.IsNullOrEmpty(info.texture) ? Col(info.color) : Color.white);
                if (!string.IsNullOrEmpty(info.texture))
                {
                    string texPath = SchoolRoot + "Textures/" + info.texture;
                    var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                    ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 512; ti.SaveAndReimport();
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                }
                material.SetFloat("_Metallic", info.metallic); material.SetFloat("_Smoothness", 1 - info.roughness);
                if (info.emissionStrength > 0)
                { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Col(info.emission) * info.emissionStrength); }
                // Source window panes are deliberately opaque and very dark; keep their silhouettes.
                material.enableInstancing = true;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), info.name), material);
                EditorUtility.SetDirty(material);
            }
            importer.SaveAndReimport();
        }

        private static TMP_FontAsset PrepareFont()
        {
            string path = "Assets/Art/UI/Fonts/SchoolChinese SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset != null) return asset;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/SchoolChinese.ttf");
            asset = TMP_FontAsset.CreateFontAsset(font, 36, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            AssetDatabase.CreateAsset(asset, path);
            if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var texture in asset.atlasTextures) if (texture != null) AssetDatabase.AddObjectToAsset(texture, asset);
            asset.TryAddCharacters("鹅企中学夜间探索移动视角奔跑蹲下跳跃空格手电筒阅读开关门暂停继续点击画面锁定鼠标主角公告校门已经封闭请沿外侧楼梯进入教室所有灯熄灭后不要回头。\n /—：0123456789WASDFECtrlShiftEscXYTomorin");
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets(); return asset;
        }

        private static void BuildLighting(Manifest manifest)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.11f, .16f, .22f);
            RenderSettings.ambientEquatorColor = new Color(.045f,.065f,.085f);
            RenderSettings.ambientGroundColor = new Color(.018f,.024f,.028f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.018f,.033f,.046f); RenderSettings.fogDensity = .009f;
            var lights = new GameObject("Night Lighting");
            var moon = new GameObject("Moonlight"); moon.transform.SetParent(lights.transform);
            moon.transform.rotation = Quaternion.Euler(38, -35, 0);
            var main = moon.AddComponent<Light>(); main.type = LightType.Directional;
            main.intensity = .65f; main.color = new Color(.48f,.64f,.88f); main.shadows = LightShadows.Soft; RenderSettings.sun = main;
            foreach (var source in manifest.lights)
            {
                if (source.type == "SUN" || source.name.StartsWith("MOON") || source.name.StartsWith("SKY") || source.name.StartsWith("RIM")) continue;
                var ob = new GameObject(source.name); ob.transform.SetParent(lights.transform);
                ob.transform.position = ConvertPosition(source.position);
                var light = ob.AddComponent<Light>(); light.type = LightType.Point; light.color = Col(source.color);
                light.intensity = Mathf.Clamp(source.energy / 70, .5f, 4.5f); light.range = source.name.Contains("room") ? 6 : 10;
                light.shadows = LightShadows.None;
                if (source.name.Contains("failing") || source.name.Contains("emergency")) ob.AddComponent<SchoolLightFlicker>();
            }
            var volumeOb = new GameObject("Night Atmosphere", typeof(Volume));
            var volume = volumeOb.GetComponent<Volume>(); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Bloom>().intensity.Override(.2f);
            profile.Add<Vignette>().intensity.Override(.32f);
            var tone = profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            Directory.CreateDirectory("Assets/Settings/Horror"); AssetDatabase.CreateAsset(profile, "Assets/Settings/Horror/NightAtmosphere.asset");
            volume.sharedProfile = profile;
        }

        private static HorrorHud BuildHud(TMP_FontAsset font)
        {
            var root = new GameObject("Exploration HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(HorrorHud));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var hud = root.GetComponent<HorrorHud>();
            Label(root.transform, font, "Title", "鹅企中学", 30, new Vector2(42,-35), new Vector2(500,55), new Vector2(0,1));
            Label(root.transform, font, "Subtitle", "夜间探索  /  Tomorin", 17, new Vector2(44,-88), new Vector2(500,38), new Vector2(0,1)).color = new Color(.56f,.7f,.69f);
            var dot = Label(root.transform, font, "Reticle", "·", 29, Vector2.zero, new Vector2(25,32), new Vector2(.5f,.5f)); dot.alignment = TextAlignmentOptions.Center;
            var prompt = Label(root.transform, font, "Interaction", "", 23, new Vector2(0,-52), new Vector2(600,50), new Vector2(.5f,.5f)); prompt.alignment = TextAlignmentOptions.Center;
            var note = Panel(root.transform,"School Notice",new Vector2(.5f,.5f),new Vector2(650,260),new Vector2(0,-150),new Color(.015f,.025f,.028f,.94f));
            var body = Label(note.transform,font,"Text","",23,new Vector2(30,-28),new Vector2(590,210),new Vector2(0,1)); body.textWrappingMode = TextWrappingModes.Normal;
            var pause = Panel(root.transform,"Pause",new Vector2(.5f,.5f),new Vector2(650,210),Vector2.zero,new Color(.008f,.014f,.019f,.96f));
            var pauseText=Label(pause.transform,font,"Paused","暂停\n\nEsc 继续 / 点击画面锁定鼠标",28,new Vector2(25,-25),new Vector2(600,160),new Vector2(0,1));pauseText.alignment=TextAlignmentOptions.Center;
            note.SetActive(false);pause.SetActive(false);hud.Configure(prompt,body,pause,note);return hud;
        }

        private static TextMeshProUGUI Label(Transform parent,TMP_FontAsset font,string name,string text,int size,Vector2 position,Vector2 dimensions,Vector2 anchor)
        {
            var ob=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));ob.transform.SetParent(parent,false);
            var rt=ob.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=anchor;rt.anchoredPosition=position;rt.sizeDelta=dimensions;
            var label=ob.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.text=text;label.color=new Color(.81f,.85f,.79f);label.raycastTarget=false;return label;
        }
        private static GameObject Panel(Transform parent,string name,Vector2 anchor,Vector2 dimensions,Vector2 position,Color color)
        {
            var ob=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));ob.transform.SetParent(parent,false);
            var rt=ob.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=rt.pivot=anchor;rt.sizeDelta=dimensions;rt.anchoredPosition=position;
            ob.GetComponent<UnityEngine.UI.Image>().color=color;ob.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;return ob;
        }
        private static void AddNotice()
        {
            var notice = GameObject.CreatePrimitive(PrimitiveType.Cube);notice.name="Readable Gate Notice";
            notice.transform.position=new Vector3(-1.8f,1.4f,31);notice.transform.localScale=new Vector3(.8f,.65f,.06f);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.42f,.40f,.28f);
            AssetDatabase.CreateAsset(material,SchoolRoot+"Materials/GateNotice.mat");notice.GetComponent<Renderer>().sharedMaterial=material;
            notice.AddComponent<ReadableNote>().Configure("校门公告","校门已经封闭。\n请沿外侧楼梯进入教室。\n\n所有灯熄灭后，不要回头。");
        }
        public static void EnsureAccessPlatforms()
        {
            if (GameObject.Find("Evacuation Stair Landings") != null) return;
            var root = new GameObject("Evacuation Stair Landings");
            var material = AssetDatabase.FindAssets("t:Material", new[]{SchoolRoot+"Materials"})
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First(m=>m.name.Contains("CONCRETE"));
            void Box(string name,Vector3 center,Vector3 size)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(root.transform);
                box.transform.position=center;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=material;box.isStatic=true;
            }
            foreach(int side in new[]{-1,1})
            {
                for(int step=0;step<3;step++)
                {
                    float top=.18f*(step+1);
                    Box("Stair approach",new Vector3(side*37,top/2,-15.6f-step*.48f),new Vector3(2.1f,top,.5f));
                }
                for(int floor=0;floor<3;floor++)
                {
                    float top=.55f+floor*4.2f;
                    Box("Floor "+(floor+1)+" landing",new Vector3(side*37,top-.1f,-26.4f),new Vector3(6.8f,.2f,4.65f));
                    Box("Return walkway",new Vector3(side*39.5f,top-.1f,-20.25f),new Vector3(1.8f,.2f,8));
                    Box("Next flight entry",new Vector3(side*38.2f,top-.1f,-16.6f),new Vector3(4.4f,.2f,1.3f));
                    Box("Outer railing",new Vector3(side*40.4f,top+.6f,-22.2f),new Vector3(.09f,1.2f,13));
                    Box("Rear railing",new Vector3(side*37,top+.6f,-28.72f),new Vector3(6.8f,1.2f,.09f));
                }
            }
        }
        private static void PoseArm(Transform[] bones,string parent,string child,Vector3 direction)
        {
            var arm=bones.FirstOrDefault(t=>t.name==parent);var elbow=bones.FirstOrDefault(t=>t.name==child);
            if(arm!=null&&elbow!=null) arm.rotation=Quaternion.FromToRotation(elbow.position-arm.position,direction.normalized)*arm.rotation;
        }
        private static Bounds CombinedBounds(GameObject ob)
        { var renderers=ob.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds; }
        private static Color Col(float[] value) => new Color(value[0],value[1],value[2],value.Length>3?value[3]:1);
        private static Vector3 ConvertPosition(float[] v) => new Vector3(-v[0],v[2],-v[1]);
    }
}
