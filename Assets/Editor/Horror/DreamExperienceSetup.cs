using System;
using System.IO;
using System.Linq;
using DayOfDream.Horror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    public static class DreamExperienceSetup
    {
        private const string Art = "Assets/Art/Environment/AnonHouse/";
        private const string MenuArt = "Assets/Art/UI/MainMenu/";
        private const string HouseScene = "Assets/Scenes/AnonHouse.unity";
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";
        private static readonly Color GlassesFrameColor = new(.16f, .115f, .13f);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/SchoolChinese SDF.asset");

        [MenuItem("Day Of Dream/Apply inventory bedroom and main menu")]
        public static void Apply() => ApplyMenuAndBedroom(true);

        internal static void ApplyMenuAndBedroom(bool rebuildBedroom)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes first.");
            Directory.CreateDirectory(MenuArt);
            var house = EditorSceneManager.OpenScene(HouseScene);
            if (rebuildBedroom)
            {
                Bedroom();
                MakeIcons();
                EditorSceneManager.SaveScene(house);
            }
            var hunter = Object.FindAnyObjectByType<AnonHunter>();
            var menuModel = new GameObject("爱音");
            var model = Object.Instantiate(hunter.transform.Find("Anon_Model").gameObject, menuModel.transform);
            model.name = "Anon_Model";
            foreach (var club in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Club")).ToArray())
                if (club != null) Object.DestroyImmediate(club.gameObject);
            var idle = AnonArmAnimation.MenuIdle(menuModel);
            var animation = menuModel.AddComponent<Animation>();
            animation.AddClip(idle, idle.name); animation.clip = idle; animation.playAutomatically = true;
            idle.SampleAnimation(menuModel, 0);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (source == null) return source;
                    var color = source.name.EndsWith("ano_glass", StringComparison.Ordinal)
                        ? GlassesFrameColor
                        : new Color(.66f, .61f, .66f);
                    var material = Mat(MenuArt + source.name + ".mat", color);
                    material.shader = unlit;
                    material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                    material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                    material.SetFloat("_Cull", 0);
                    if (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > .5f)
                    {
                        material.SetFloat("_AlphaClip", 1);
                        material.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
                        material.EnableKeyword("_ALPHATEST_ON");
                        material.renderQueue = (int)RenderQueue.AlphaTest;
                    }
                    return material;
                }).ToArray();
            }
            // Move the temporary model without serializing a second hunter into the house.
            var menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(menuModel, menu);
            SceneManager.SetActiveScene(menu);
            foreach (var root in menu.GetRootGameObjects()) if (root != menuModel) Object.DestroyImmediate(root);
            BuildMenu(menuModel);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            // The temporary model was created after house authoring, so remove its dirty state by reloading.
            EditorSceneManager.CloseScene(house, true);
            var builds = EditorBuildSettings.scenes.Where(s => s.path != MenuScene).ToList();
            builds.Insert(0, new EditorBuildSettingsScene(MenuScene, true));
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
        }

        private static Material Mat(string path, Color color, float smoothness = .1f)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        internal static void TuneBedroomLamp(Light lamp)
        {
            lamp.intensity = 1.12f;
            lamp.range = 5.9f;
            lamp.color = new Color(1, .77f, .52f);
            lamp.shadows = LightShadows.Soft;
        }

        private static void Bedroom()
        {
            var lamp = GameObject.Find("Bedroom ceiling lamp").GetComponent<Light>();
            foreach (var other in Object.FindObjectsByType<Light>())
            {
                var p = other.transform.position;
                if (other != lamp && p.x > -7 && p.x < -1.43f && p.z > 0 && p.z < 7 && p.y > 2 && p.y < 3.2f)
                    other.enabled = false;
            }
            TuneBedroomLamp(lamp);
            var glass = lamp.transform.Find("Frosted dim lamp")?.GetComponent<Renderer>();
            if (glass != null)
            {
                var material = glass.sharedMaterial;
                material.SetColor("_EmissionColor", new Color(.12f, .065f, .025f));
                EditorUtility.SetDirty(material);
            }
            var bed = GameObject.Find("Bed").transform;
            var quilt = Mat(Art + "Dusty Blue Quilt.mat", new Color(.18f, .25f, .28f), .015f);
            quilt.SetFloat("_Cull", 0);
            var linen = Mat(Art + "Bedroom Cotton.mat", new Color(.55f, .51f, .43f), .015f);
            var wood = Mat(Art + "Bed Painted Walnut.mat", new Color(.075f, .047f, .033f), .18f);
            foreach (var renderer in bed.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Bedspread") renderer.enabled = false;
                else if (renderer.name is "Mattress" or "Pillow") renderer.sharedMaterial = linen;
                else renderer.sharedMaterial = wood;
            }
            var old = bed.Find("Draped quilt");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            const int columns = 49, rows = 57;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
            {
                float u = x / (float)(columns - 1), v = z / (float)(rows - 1);
                float width = Mathf.Lerp(-.82f, .82f, u);
                float drape = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, .82f, Mathf.Abs(width)));
                float y = .615f - .25f * drape + .014f * Mathf.Sin(u * 51 + v * 9) + .009f * Mathf.Cos(v * 43);
                // A folded lip and softly padded quilting catch the dim practical light.
                y += .045f * Mathf.Exp(-Mathf.Pow((v - .95f) * 24, 2));
                vertices[z * columns + x] = bed.InverseTransformPoint(new Vector3(-5.7f + width, y, Mathf.Lerp(3.60f, 5.34f, v)));
                uv[z * columns + x] = new Vector2(u * 3, v * 4);
            }
            int at = 0;
            for (int z = 0; z < rows - 1; z++) for (int x = 0; x < columns - 1; x++)
            {
                int a = z * columns + x;
                triangles[at++] = a; triangles[at++] = a + columns; triangles[at++] = a + 1;
                triangles[at++] = a + 1; triangles[at++] = a + columns; triangles[at++] = a + columns + 1;
            }
            var mesh = new Mesh { name = "Draped stitched quilt", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = Art + "Bedroom Quilt.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
            var cover = new GameObject("Draped quilt", typeof(MeshFilter), typeof(MeshRenderer));
            cover.transform.SetParent(bed, false);
            cover.GetComponent<MeshFilter>().sharedMesh = saved;
            cover.GetComponent<Renderer>().sharedMaterial = quilt;
            // A subtle woven normal gives fabric a visibly different response from the floorboards.
            quilt.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/Rug_Normal.jpg"));
            quilt.EnableKeyword("_NORMALMAP"); quilt.SetFloat("_BumpScale", .25f);
        }

        private static void MakeIcons()
        {
            Directory.CreateDirectory("Assets/Resources/InventoryIcons");
            var pickups = Object.FindObjectsByType<HouseObject>(FindObjectsInactive.Include);
            var names = new[] { "CabinetKey", "ExitKey", "Fuse", "Bottle", "DV" };
            var sources = new[] {
                pickups.First(x => new SerializedObject(x).FindProperty("kind").enumValueIndex == (int)HouseObject.Kind.CabinetKey).gameObject,
                pickups.First(x => new SerializedObject(x).FindProperty("kind").enumValueIndex == (int)HouseObject.Kind.ExitKey).gameObject,
                pickups.First(x => new SerializedObject(x).FindProperty("kind").enumValueIndex == (int)HouseObject.Kind.Fuse).gameObject,
                pickups.First(x => new SerializedObject(x).FindProperty("kind").enumValueIndex == (int)HouseObject.Kind.Bottle).gameObject,
                GameObject.Find("DV Pickup") };
            var cameraRoot = new GameObject("Icon camera", typeof(Camera));
            var camera = cameraRoot.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 30; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            var lightRoot = new GameObject("Icon key light", typeof(Light));
            var key = lightRoot.GetComponent<Light>(); key.type = LightType.Directional; key.intensity = 3; key.cullingMask = 1 << 30;
            key.transform.rotation = Quaternion.Euler(35, -35, 0);
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            try
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    var copy = Object.Instantiate(sources[i]);
                    try
                    {
                        copy.SetActive(true); copy.transform.position = new Vector3(0, 1000, 0);
                        copy.transform.rotation = i < 2 ? Quaternion.Euler(-65, 10, 35) : Quaternion.Euler(-10, 145, 25);
                        foreach (var component in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(component);
                        foreach (var child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                        var renderers = copy.GetComponentsInChildren<Renderer>();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        camera.transform.SetPositionAndRotation(bounds.center + new Vector3(0, .2f, -4), Quaternion.identity);
                        camera.transform.LookAt(bounds.center);
                        camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * .68f;
                        camera.Render();
                        var previous = RenderTexture.active; RenderTexture.active = rt;
                        var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                        texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); texture.Apply(); RenderTexture.active = previous;
                        string path = "Assets/Resources/InventoryIcons/" + names[i] + ".png";
                        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                        AssetDatabase.ImportAsset(path);
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                    finally { Object.DestroyImmediate(copy); }
                }
            }
            finally { camera.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(lightRoot); }
        }

        private static void BuildMenu(GameObject model)
        {
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.055f, .065f, .085f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.012f, .018f, .023f); RenderSettings.fogDensity = .025f;
            var set = new GameObject("Doorway at night").transform;
            var wall = AssetDatabase.LoadAssetAtPath<Material>(Art + "Damp Plaster.mat");
            var floor = AssetDatabase.LoadAssetAtPath<Material>(Art + "Worn Boards.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Art + "Dark Oak.mat");
            Box(set, "Floor", new Vector3(0,-.11f,3), new Vector3(20,.2f,20), floor);
            Box(set, "Left wall", new Vector3(-2.27f,2,3.45f), new Vector3(7.66f,4,.3f), wall);
            Box(set, "Right wall", new Vector3(4.21f,2,3.45f), new Vector3(2.68f,4,.3f), wall);
            Box(set, "Lintel wall", new Vector3(2.215f,3.3f,3.45f), new Vector3(1.31f,1.4f,.3f), wall);
            Box(set, "Dark room", new Vector3(2.2f,2,6.6f), new Vector3(5,4,.3f), wall);
            Box(set, "Frame left", new Vector3(1.53f,1.3f,3.2f), new Vector3(.16f,2.6f,.25f), wood);
            Box(set, "Frame right", new Vector3(2.90f,1.3f,3.2f), new Vector3(.16f,2.6f,.25f), wood);
            Box(set, "Frame top", new Vector3(2.215f,2.65f,3.2f), new Vector3(1.53f,.16f,.25f), wood);
            Box(set, "Threshold", new Vector3(2.215f,.025f,3.22f), new Vector3(1.53f,.05f,.38f), wood);
            var hinge = new GameObject("Half open door").transform;
            hinge.SetParent(set, false); hinge.localPosition = new Vector3(2.84f,0,3.46f); hinge.localRotation = Quaternion.Euler(0,57,0);
            Box(hinge, "Door leaf", new Vector3(-.63f,1.25f,0), new Vector3(1.26f,2.5f,.09f), wood);
            var brass = AssetDatabase.LoadAssetAtPath<Material>(Art + "Brass.mat");
            Box(hinge, "Door handle", new Vector3(-1.04f,1.05f,-.08f), new Vector3(.18f,.035f,.08f), brass);
            model.transform.SetPositionAndRotation(new Vector3(2.12f,0,3.0f), Quaternion.Euler(0,190,0));
            var cameraRoot = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraRoot.tag = "MainCamera";
            var camera = cameraRoot.GetComponent<Camera>();
            camera.transform.position = new Vector3(.25f,1.3f,-4.25f);
            camera.transform.LookAt(new Vector3(.25f,1.3f,3.1f));
            camera.fieldOfView = 31; camera.farClipPlane = 24; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.008f,.011f,.016f);
            var lamp = new GameObject("Flickering doorway light", typeof(Light), typeof(SchoolLightFlicker));
            lamp.transform.position = new Vector3(2.1f,2.5f,2.15f);
            var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.intensity = 6; light.range = 5.5f;
            light.color = new Color(1,.72f,.43f); light.shadows = LightShadows.Soft;
            var fill = new GameObject("Cool doorway rim", typeof(Light));
            fill.transform.position = new Vector3(2.1f,1.6f,5.2f);
            var rim = fill.GetComponent<Light>(); rim.type = LightType.Point; rim.intensity = 2.3f; rim.range = 4;
            rim.color = new Color(.24f,.42f,.65f); rim.shadows = LightShadows.None;
            var fixture = Mat(MenuArt + "Lamp glass.mat", new Color(.55f,.38f,.20f));
            fixture.EnableKeyword("_EMISSION"); fixture.SetColor("_EmissionColor", new Color(.6f,.29f,.08f));
            var lampMesh = Box(set,"Doorway lamp",new Vector3(2.22f,2.9f,3.19f),new Vector3(.42f,.11f,.14f),fixture);
            var flicker = new SerializedObject(lamp.GetComponent<SchoolLightFlicker>());
            flicker.FindProperty("fixture").objectReferenceValue = lampMesh.GetComponent<Renderer>();
            flicker.ApplyModifiedPropertiesWithoutUndo();

            var canvas = new GameObject("Main menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var controller = canvas.AddComponent<DreamMainMenu>();
            Gradient(canvas.transform);
            Title(canvas.transform);
            var start = Button(canvas.transform, "开始游戏", new Vector2(152, -456), 444);
            var resume = Button(canvas.transform, "继续游戏", new Vector2(172, -542), 398);
            var settings = Button(canvas.transform, "设置", new Vector2(192, -628), 352);
            var quit = Button(canvas.transform, "退出", new Vector2(212, -714), 306);
            var panel = new GameObject("Settings panel",typeof(RectTransform),typeof(Image));
            panel.transform.SetParent(canvas.transform,false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.015f,.023f,.029f,.98f);
            Text(panel.transform,"设置",new Vector2(160,-150),new Vector2(800,100),64);
            Text(panel.transform,"主音量",new Vector2(170,-350),new Vector2(300,60),28);
            Text(panel.transform,"鼠标灵敏度",new Vector2(170,-510),new Vector2(300,60),28);
            var volume = Slider(panel.transform,"Volume",new Vector2(520,-377),0,1);
            var sensitivity = Slider(panel.transform,"Sensitivity",new Vector2(520,-537),.25f,2.5f);
            var back = Button(panel.transform,"返回",new Vector2(160,-740));
            var config = new SerializedObject(controller);
            void Set(string name,Object value) => config.FindProperty(name).objectReferenceValue = value;
            Set("startButton",start); Set("continueButton",resume); Set("settingsButton",settings); Set("quitButton",quit);
            Set("backButton",back); Set("settingsPanel",panel); Set("volumeSlider",volume); Set("sensitivitySlider",sensitivity);
            config.ApplyModifiedPropertiesWithoutUndo(); panel.SetActive(false);
        }

        private static TMP_Text Text(Transform parent,string text,Vector2 position,Vector2 size,float fontSize)
        {
            var label = new GameObject(text,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            label.rectTransform.SetParent(parent,false); label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0,1);
            label.rectTransform.pivot = new Vector2(0,1); label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
            label.text = text; label.font = Font; label.fontSize = fontSize; label.color = new Color(.78f,.81f,.77f);
            label.raycastTarget = false;
            return label;
        }
        private static void Title(Transform parent)
        {
            const string wordmark = MenuArt + "Dream Wordmark.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(wordmark);
            if (importer.textureType != TextureImporterType.Sprite || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            var title = new GameObject("梦之日", typeof(RectTransform), typeof(Image));
            var image = title.GetComponent<Image>();
            Place(image.rectTransform, parent, new Vector2(90, -166), new Vector2(570, 190));
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(wordmark);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image Ornament(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(image.rectTransform, parent, position, size);
            image.raycastTarget = false;
            return image;
        }

        private static Button Button(Transform parent, string label, Vector2 position, float width = 350)
        {
            var image = Ornament(parent, label, position, new Vector2(width, 67));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var rect = image.rectTransform;
            var colors = button.colors;
            colors.normalColor = new Color(.13f, .078f, .087f, .93f);
            colors.selectedColor = new Color(.29f, .09f, .112f, .98f);
            colors.highlightedColor = colors.selectedColor;
            colors.pressedColor = new Color(.46f, .13f, .14f);
            colors.disabledColor = new Color(.075f, .059f, .066f, .78f);
            colors.fadeDuration = .18f;
            button.colors = colors;
            var caption = Text(rect, label, new Vector2(55, -7), new Vector2(width - 90, 51), 30);
            caption.characterSpacing = 6;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            var seal = Ornament(rect, "Diamond", new Vector2(23, -26), new Vector2(12, 12));
            seal.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var accent = Ornament(rect, "Bottom edge", new Vector2(16, -64), new Vector2(width - 35, 2));
            var feedback = new SerializedObject(button.gameObject.AddComponent<MenuButtonCaption>());
            feedback.FindProperty("seal").objectReferenceValue = seal;
            feedback.FindProperty("accent").objectReferenceValue = accent;
            feedback.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        private static Slider Slider(Transform parent,string name,Vector2 position,float min,float max)
        {
            var slider = new GameObject(name,typeof(RectTransform),typeof(Slider)).GetComponent<Slider>();
            var rect = (RectTransform)slider.transform; rect.SetParent(parent,false); rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,.5f); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(650,40);
            Image Part(string label,Color color)
            {
                var part = new GameObject(label,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                part.rectTransform.SetParent(rect,false); part.rectTransform.anchorMin = Vector2.zero; part.rectTransform.anchorMax = Vector2.one;
                part.rectTransform.offsetMin = new Vector2(0,12); part.rectTransform.offsetMax = new Vector2(0,-12); part.color = color;
                return part;
            }
            Part("Track",new Color(.10f,.15f,.17f));
            var fill = Part("Fill",new Color(.48f,.63f,.57f));
            var handle = Part("Handle",new Color(.8f,.84f,.76f)); handle.rectTransform.sizeDelta = new Vector2(18,32);
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max;
            return slider;
        }
        private static void Gradient(Transform parent)
        {
            string path = MenuArt + "Menu shade.png";
            var texture = new Texture2D(256,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++) for(int x=0;x<256;x++)
            {
                float u=x/255f,v=y/127f;
                float shade = Mathf.Lerp(.95f,.04f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.7f,u)));
                shade = Mathf.Clamp01(shade + .25f * Mathf.Pow(Mathf.Abs(v-.5f)*2,3));
                texture.SetPixel(x,y,new Color(.008f,.014f,.020f,shade));
            }
            texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var shadeObject = new GameObject("Atmosphere shade",typeof(RectTransform),typeof(RawImage));
            shadeObject.transform.SetParent(parent,false); var image = shadeObject.GetComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); image.raycastTarget = false;
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one; image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
