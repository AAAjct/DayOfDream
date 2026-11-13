using System;
using DayOfDream.Horror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Editor
{
    public static class AnonHouseArtPass
    {
        private const string Root = "Assets/Art/Environment/AnonHouse/";
        private const string TextureRoot = Root + "Textures/";

        [MenuItem("Tools/Day Of Dream/Apply Anon House Art Pass")]
        public static void Apply()
        {
            AnonHouseVisuals.Apply();
            Surface("Worn Boards", "Floor", new Color(.78f,.73f,.67f), .80f, .05f);
            Surface("Dark Oak", "Timber", new Color(.66f,.58f,.48f), .82f, .035f);
            Surface("Damp Plaster", "Plaster", new Color(.61f,.56f,.49f), .92f, .025f);
            Surface("Cellar Brick", "Cellar", new Color(.78f,.75f,.68f), .86f, .03f);
            Surface("Oxidized Metal", "Iron", new Color(.68f,.62f,.55f), .54f, .26f);
            Surface("Wine Fabric", "Rug", new Color(.60f,.37f,.30f), .45f, .015f);
            var props = GameObject.Find("Furniture and Puzzles").transform;
            foreach (Transform prop in props)
                if (prop.name == "Runner border") prop.GetComponent<MeshRenderer>().enabled = false;

            var lighting = GameObject.Find("House Atmosphere") ?? new GameObject("House Atmosphere");
            Accent(lighting.transform, "Bedroom window spill",new(-4.58f,2.18f,6.63f),new(-4.72f,.23f,2.5f),
                new Color(.35f,.55f,1),2.2f,4.5f,76);
            Accent(lighting.transform, "Kitchen counter spill",new(4.83f,2.58f,5.71f),new(4.25f,.65f,4.85f),
                new Color(1,.57f,.26f),2.4f,3.3f,100);
            Accent(lighting.transform, "Study window spill",new(-3.5f,2.18f,-6.58f),new(-4.8f,.32f,-2.88f),
                new Color(.44f,.63f,1),1.5f,4.2f,79);
            Accent(lighting.transform, "Cellar maintenance light",new(5.9f,-.48f,-12.4f),new(3.2f,-2.26f,-10.5f),
                new Color(1,.60f,.34f),2.6f,4.8f,69);
            Accent(lighting.transform, "Hall edge spill",new(.96f,2.44f,-2.34f),new(-.53f,1.10f,2.9f),
                new Color(.54f,.65f,1),.55f,3.3f,82);

            var volume = lighting.GetComponent<Volume>() ?? lighting.AddComponent<Volume>();
            volume.isGlobal = true;
            string profilePath = Root + "HouseAtmosphere.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile,profilePath);
                var bloom = profile.Add<Bloom>();
                bloom.threshold.Override(1.13f);
                bloom.intensity.Override(.14f);
                bloom.scatter.Override(.62f);
                AssetDatabase.AddObjectToAsset(bloom,profile);
                var tone = profile.Add<Tonemapping>();
                tone.mode.Override(TonemappingMode.Neutral);
                AssetDatabase.AddObjectToAsset(tone,profile);
                EditorUtility.SetDirty(profile);
            }
            volume.sharedProfile = profile;
            ApplyLowLight(GameObject.Find("House Structure").transform, lighting.transform);
            LightHunter(UnityEngine.Object.FindAnyObjectByType<AnonHunter>().GetComponentInChildren<SkinnedMeshRenderer>());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        internal static void LightHunter(SkinnedMeshRenderer skin)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var material in skin.sharedMaterials)
            {
                if (material == null || material.shader == shader) continue;
                var texture = material.GetTexture("_BaseMap");
                var color = material.color;
                bool cutout = material.GetFloat("_AlphaClip") > .5f;
                float cutoff = material.GetFloat("_Cutoff");
                material.shader = shader;
                material.SetTexture("_BaseMap", texture);
                material.color = color;
                material.SetFloat("_Smoothness", .19f);
                material.SetFloat("_Cull", 0);
                material.SetFloat("_AlphaClip", cutout ? 1 : 0);
                material.SetFloat("_Cutoff", cutoff);
                if (cutout) { material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = 2450; }
                EditorUtility.SetDirty(material);
            }
        }

        internal static void ApplyLowLight(Transform structure, Transform atmosphere)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.043f,.048f,.060f);
            RenderSettings.ambientEquatorColor = new Color(.026f,.029f,.037f);
            RenderSettings.ambientGroundColor = new Color(.016f,.014f,.017f);
            RenderSettings.reflectionIntensity = .04f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.016f,.019f,.026f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = .017f;
            foreach (var light in structure.GetComponentsInChildren<Light>(true))
            {
                bool indicator = light.color.r > .8f && light.color.g < .3f;
                light.intensity = indicator ? .7f : 1.05f;
                light.range = indicator ? 1.65f : 3.25f;
            }
            if (atmosphere == null) return;
            foreach (var light in atmosphere.GetComponentsInChildren<Light>(true))
            {
                (light.intensity, light.range) = light.name switch
                {
                    "Bedroom window spill" => (2.2f, 4.5f),
                    "Kitchen counter spill" => (2.4f, 3.3f),
                    "Study window spill" => (1.5f, 4.2f),
                    "Cellar maintenance light" => (2.6f, 4.8f),
                    _ => (.55f, 3.3f)
                };
            }
            if (atmosphere.TryGetComponent<Volume>(out var volume) && volume.sharedProfile != null &&
                volume.sharedProfile.TryGet<Bloom>(out var bloom))
            {
                bloom.intensity.Override(.035f);
                EditorUtility.SetDirty(bloom);
            }
        }

        private static void Surface(string materialName, string texture, Color tint, float bump, float metal)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + materialName + ".mat");
            material.color = tint;
            material.SetTexture("_BaseMap", ReadTexture(texture + "_Color.jpg",TextureImporterType.Default,true));
            material.SetTexture("_BumpMap", ReadTexture(texture + "_Normal.jpg",TextureImporterType.NormalMap,false));
            material.SetFloat("_BumpScale",bump);
            material.EnableKeyword("_NORMALMAP");
            var mask = ReadTexture(texture + "_Mask.png",TextureImporterType.Default,false);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetTexture("_OcclusionMap",mask);
            material.SetFloat("_OcclusionStrength", .65f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Metallic",metal);
            material.SetFloat("_Smoothness",.87f);
            EditorUtility.SetDirty(material);
        }

        private static Texture2D ReadTexture(string name, TextureImporterType type, bool srgb)
        {
            string path = TextureRoot + name;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing house texture " + name);
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.anisoLevel != 8)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Accent(Transform parent,string name,Vector3 position,Vector3 target,Color color,
            float intensity,float range,float angle)
        {
            var existing = parent.Find(name);
            var light = existing != null ? existing.GetComponent<Light>() : new GameObject(name,typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(parent,false);
            light.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            light.type=LightType.Spot;
            light.color=color;
            light.range=range;
            light.intensity=intensity;
            light.spotAngle=angle;
            light.innerSpotAngle=angle*.48f;
            light.shadows=LightShadows.Soft;
            light.shadowBias=.045f;
            light.shadowNormalBias=.15f;
        }
    }
}
