using System;
using System.Linq;
using DayOfDream.Horror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DayOfDream.Editor
{
    public static class CamcorderSetup
    {
        private const string PropPath = "Assets/Art/Props/Camcorder/";
        private const string PlayerPrefab = "Assets/Prefabs/Horror/TomorinExplorer.prefab";
        private static readonly Color Ink = new(.88f, .94f, .87f, .88f);
        private static readonly Color FaintInk = new(.73f, .82f, .74f, .62f);
        // Rendering layer 8 is reserved for the view model, away from the near-field IR beam.
        private const uint ViewModelRenderingLayer = (1u << 7) | 1u;
        private static readonly Vector2 TopLeft = new(0, 1);
        private static readonly Vector2 TopRight = Vector2.one;
        private static readonly Vector2 Top = new(.5f, 1);
        private static readonly Vector2 Center = new(.5f, .5f);

        [MenuItem("Tools/Day Of Dream/Install Night Vision Camcorder")]
        public static void Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HorrorSchoolBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved night school scene in Edit Mode before installing the camcorder.");

            var player = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<FirstPersonExplorer>())
                .Single(explorer => explorer != null);
            PrepareRenderers();
            PrepareMaterials();
            var prefab = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                Attach(prefab.GetComponent<FirstPersonExplorer>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Attach(player);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        public static void Attach(FirstPersonExplorer player)
        {
            var explorer = new SerializedObject(player);
            var eye = ((Transform)explorer.FindProperty("view").objectReferenceValue).GetComponent<Camera>();
            if (eye.TryGetComponent<HandheldCamcorder>(out var installed))
            {
                var installedSettings = new SerializedObject(installed);
                var installedModel = (Transform)installedSettings.FindProperty("model").objectReferenceValue;
                SetViewModelRenderingLayer(installedModel.gameObject);
                ConfigureInfrared((Light)installedSettings.FindProperty("infrared").objectReferenceValue);
                return;
            }

            var hud = (HorrorHud)explorer.FindProperty("hud").objectReferenceValue;
            var font = hud.GetComponentInChildren<TMP_Text>(true).font;
            var hudSettings = new SerializedObject(hud);
            var hide = hudSettings.FindProperty("hideWhileFilming");
            string[] names = { "Title", "Subtitle", "Reticle" };
            hide.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                hide.GetArrayElementAtIndex(i).objectReferenceValue = hud.transform.Find(names[i]).gameObject;
            hudSettings.ApplyModifiedPropertiesWithoutUndo();

            var model = InstantiateModel("Camcorder", eye.transform);
            SetViewModelRenderingLayer(model);
            model.SetActive(false);

            var emitter = new GameObject("Infrared Emitter", typeof(Light));
            emitter.transform.SetParent(eye.transform, false);
            // A near-eye emitter keeps the illumination and the view aligned at doorways.
            emitter.transform.localPosition = new Vector3(0, .025f, .07f);
            var light = emitter.GetComponent<Light>();
            ConfigureInfrared(light);

            var speaker = emitter.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0;
            speaker.volume = .7f;
            var viewfinder = BuildViewfinder((RectTransform)hud.transform, font);
            var camcorder = eye.gameObject.AddComponent<HandheldCamcorder>();
            var settings = new SerializedObject(camcorder);
            SetReference(settings, "infrared", light);
            SetReference(settings, "model", model.transform);
            SetReference(settings, "viewfinder", viewfinder);
            SetReference(settings, "speaker", speaker);
            SetReference(settings, "switchSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/CamcorderSwitch.wav"));
            settings.ApplyModifiedPropertiesWithoutUndo();
            SetReference(explorer, "camcorder", camcorder);
            explorer.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void PlacePickup(FirstPersonExplorer player, Transform table, Vector3 position, float yaw)
        {
            Attach(player);
            var camcorder = player.GetComponentInChildren<HandheldCamcorder>(true);
            var settings = new SerializedObject(camcorder);
            var pickupField = settings.FindProperty("pickup");
            var pickup = (CamcorderPickup)pickupField.objectReferenceValue;
            if (pickup == null)
            {
                var worldModel = table == null ? InstantiateModel("Camcorder", null) : InstantiateModel("Camcorder", table);
                worldModel.name = "DV Pickup";
                pickup = worldModel.AddComponent<CamcorderPickup>();
                var collider = worldModel.GetComponent<BoxCollider>();
                collider.center = new Vector3(-.055f, .012f, 0);
                collider.size = new Vector3(.25f, .13f, .27f);
                collider.isTrigger = true;
                pickupField.objectReferenceValue = pickup;
            }
            pickup.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            pickup.gameObject.SetActive(true);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CamcorderViewfinder BuildViewfinder(RectTransform hud, TMP_FontAsset font)
        {
            var root = new GameObject("DV Viewfinder", typeof(RectTransform), typeof(CanvasGroup), typeof(CamcorderViewfinder));
            var rect = (RectTransform)root.transform;
            rect.SetParent(hud, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            group.alpha = 0;

            // The frame and readouts follow the viewport edges. The focus marks follow its center.
            FrameCorner(rect, TopLeft, new Vector2(58, -46), 1, -1, 48, 30);
            FrameCorner(rect, TopRight, new Vector2(-58, -46), -1, -1, 48, 30);
            FrameCorner(rect, Vector2.zero, new Vector2(58, 62), 1, 1, 48, 30);
            FrameCorner(rect, Vector2.right, new Vector2(-58, 62), -1, 1, 48, 30);
            FrameCorner(rect, Center, new Vector2(-36, 24), 1, -1, 12, 8);
            FrameCorner(rect, Center, new Vector2(36, 24), -1, -1, 12, 8);
            FrameCorner(rect, Center, new Vector2(-36, -24), 1, 1, 12, 8);
            FrameCorner(rect, Center, new Vector2(36, -24), -1, 1, 12, 8);

            var dot = Image(rect, "Record light", TopLeft, new Vector2(91, -83), new Vector2(10, 10),
                new Color(1, .17f, .12f, 1));
            Label(rect, font, "REC", "REC", TopLeft, new Vector2(134, -83), new Vector2(58, 34), 24,
                new Color(1, .25f, .20f, 1));
            var timer = Label(rect, font, "Counter", "00:00:00", TopLeft, new Vector2(228, -83),
                new Vector2(120, 34), 23, Ink);

            Label(rect, font, "Wide", "W", Top, new Vector2(-134, -83), new Vector2(28, 30), 21, Ink);
            Label(rect, font, "Telephoto", "T", Top, new Vector2(134, -83), new Vector2(28, 30), 21, Ink);
            Image(rect, "Zoom rail", Top, new Vector2(0, -83), new Vector2(220, 2), FaintInk);
            for (int i = -2; i <= 2; i++)
                Image(rect, "Zoom tick", Top, new Vector2(i * 55, -83), new Vector2(2, 8), FaintInk);
            var zoom = Image(rect, "Zoom position", Top, new Vector2(-110, -83), new Vector2(4, 15), Ink);

            var mode = Label(rect, font, "Infrared state", "IR  OFF", Vector2.zero, new Vector2(137, 103),
                new Vector2(105, 28), 22, Ink);
            var viewfinder = root.GetComponent<CamcorderViewfinder>();
            var settings = new SerializedObject(viewfinder);
            SetReference(settings, "canvas", group);
            SetReference(settings, "timer", timer);
            SetReference(settings, "mode", mode);
            SetReference(settings, "recordDot", dot);
            SetReference(settings, "zoomMarker", zoom.rectTransform);
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return viewfinder;
        }

        private static void FrameCorner(RectTransform parent, Vector2 anchor, Vector2 position, int dx, int dy,
            float width, float height)
        {
            Image(parent, "Frame horizontal", anchor, position + new Vector2(dx * width * .5f, 0), new Vector2(width, 2), FaintInk);
            Image(parent, "Frame vertical", anchor, position + new Vector2(0, dy * height * .5f), new Vector2(2, height), FaintInk);
        }

        private static Image Image(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            var image = item.GetComponent<Image>();
            Layout(image.rectTransform, parent, anchor, position, size);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Label(RectTransform parent, TMP_FontAsset font, string name, string text,
            Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var label = item.GetComponent<TextMeshProUGUI>();
            Layout(label.rectTransform, parent, anchor, position, size);
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.text = text;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void Layout(RectTransform child, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            child.SetParent(parent, false);
            child.anchorMin = child.anchorMax = anchor;
            child.anchoredPosition = position;
            child.sizeDelta = size;
        }

        private static void SetViewModelRenderingLayer(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.renderingLayerMask = ViewModelRenderingLayer;
            }
        }

        private static void ConfigureInfrared(Light light)
        {
            light.type = LightType.Spot;
            light.color = Color.white;
            light.range = 10;
            light.intensity = 64;
            light.spotAngle = 83;
            light.innerSpotAngle = 32;
            light.shadows = LightShadows.Soft;
            light.shadowNearPlane = .045f;
            light.shadowBias = .07f;
            light.shadowNormalBias = .13f;
            light.enabled = false;
            light.renderingLayerMask = 1;
            var lightData = light.GetUniversalAdditionalLightData();
            lightData.renderingLayers = 1;
            // URP's custom resolution is stored in the same field its Light inspector edits.
            var lightSettings = new SerializedObject(light);
            lightSettings.FindProperty("m_Shadows.m_Resolution").intValue = 512;
            lightSettings.ApplyModifiedPropertiesWithoutUndo();
            var additionalSettings = new SerializedObject(lightData);
            additionalSettings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierCustom;
            additionalSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject InstantiateModel(string name, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PropPath + name + ".fbx");
            return (GameObject)(parent == null ? PrefabUtility.InstantiatePrefab(asset) :
                PrefabUtility.InstantiatePrefab(asset, parent));
        }

        internal static void PrepareMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string file in new[] { "Camcorder.fbx" })
            {
                var path = PropPath + file;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var imported in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    var materialPath = PropPath + imported.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(lit);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.color = imported.color;
                    material.SetFloat("_Metallic", imported.name == "Machined Alloy" ? .78f :
                        imported.name == "Optical Glass" ? .45f : imported.name == "Graphite" ? .28f : .08f);
                    material.SetFloat("_Smoothness", imported.HasProperty("_Smoothness") ? imported.GetFloat("_Smoothness") : .35f);
                    EditorUtility.SetDirty(material);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(imported), material);
                }
                importer.SaveAndReimport();
            }
            SetEmission("Graphite", Color.black);
            SetEmission("Rubber", Color.black);
            SetEmission("Machined Alloy", Color.black);
            SetEmission("LCD Glass", new Color(.014f, .060f, .041f));
            SetEmission("Record Button", new Color(.24f, .018f, .012f));
            PrecisionFinish("Graphite", new Color(.21f, .23f, .24f), .32f, .48f);
            PrecisionFinish("Rubber", new Color(.10f, .105f, .11f), 0, .26f);
            PrecisionFinish("Machined Alloy", new Color(.42f, .44f, .46f), .78f, .64f);
        }

        private static void PrecisionFinish(string name, Color tint, float metal, float smooth)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PropPath + name + ".mat");
            string normalPath = PropPath + name + " Micro Normal.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            material.color = tint;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PropPath + name + " Finish.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", .35f);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smooth);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        private static void SetEmission(string name, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PropPath + name + ".mat");
            material.SetColor("_EmissionColor", color);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }

        private static void PrepareRenderers()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CamcorderSensor.shader");
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (!mobile.supportsAdditionalLightShadows)
            {
                var mobileSettings = new SerializedObject(mobile);
                mobileSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                mobileSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(mobile);
            }
            foreach (string quality in new[] { "PC", "Mobile" })
            {
                string path = $"Assets/Settings/{quality}_Renderer.asset";
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var feature = renderer.rendererFeatures.OfType<CamcorderRenderFeature>().FirstOrDefault();
                if (feature != null) continue;
                feature = ScriptableObject.CreateInstance<CamcorderRenderFeature>();
                feature.name = "Camcorder Sensor";
                feature.hideFlags = HideFlags.HideInHierarchy;
                var settings = new SerializedObject(feature);
                SetReference(settings, "shader", shader);
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var rendererSettings = new SerializedObject(renderer);
                var features = rendererSettings.FindProperty("m_RendererFeatures");
                var map = rendererSettings.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                renderer.SetDirty();
                AssetDatabase.SaveAssetIfDirty(renderer);
            }
        }

        private static void SetReference(SerializedObject target, string name, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException($"Missing {name} for {target.targetObject.name}.");
            target.FindProperty(name).objectReferenceValue = value;
        }
    }
}
