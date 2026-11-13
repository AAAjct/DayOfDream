using System;
using System.Linq;
using DayOfDream.Horror;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    public static class BedroomExperienceUpgrade
    {
        private const string Dv = "Assets/Art/Props/Camcorder/Camcorder.fbx";
        private const string Art = "Assets/Art/Environment/AnonHouse/";
        [MenuItem("Tools/Day Of Dream/Upgrade Bedroom and DV")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != AnonHouseBuilder.Path || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the saved Anon House in Edit Mode.");
            AssetDatabase.Refresh();
            CamcorderSetup.PrepareMaterials();
            InstallBlur();
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Horror/TomorinExplorer.prefab");
            try
            {
                ReplaceHandheld(prefab.GetComponentInChildren<HandheldCamcorder>(true));
                PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Horror/TomorinExplorer.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var player = Object.FindAnyObjectByType<FirstPersonExplorer>();
            var camcorder = player.GetComponentInChildren<HandheldCamcorder>(true);
            var cameraConfig = new SerializedObject(camcorder);
            // The world pickup and view model always use the same imported asset and materials.
            var previous = cameraConfig.FindProperty("pickup").objectReferenceValue as CamcorderPickup;
            var furniture = GameObject.Find("Furniture and Puzzles").transform;
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var pickup = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dv), furniture);
            pickup.name = "DV Pickup";
            pickup.transform.SetPositionAndRotation(new Vector3(-4.29f, .822f, 5.95f), Quaternion.Euler(0, 135, 0));
            var collider = pickup.AddComponent<BoxCollider>();
            collider.center = new Vector3(.045f, .015f, -.01f);
            collider.size = new Vector3(.31f, .18f, .33f);
            collider.isTrigger = true;
            Set(cameraConfig, "pickup", pickup.AddComponent<CamcorderPickup>());
            cameraConfig.ApplyModifiedPropertiesWithoutUndo();
            UpgradeBed();
            BedroomLight(furniture);
            Nightstand(furniture);
            Door(furniture);
            var house = Object.FindAnyObjectByType<AnonHouse>();
            var settings = new SerializedObject(house);
            Set(settings, "wakePillow", Marker(furniture, "Wake Pillow", new(-5.7f, .77f, 5.92f), 180));
            Set(settings, "wakeExit", Marker(furniture, "Wake Bedside", new(-4.58f, .04f, 5.10f), 90));
            settings.ApplyModifiedPropertiesWithoutUndo();
            var hunter = Object.FindAnyObjectByType<AnonHunter>();
            var hunterSettings = new SerializedObject(hunter);
            Set(hunterSettings, "impact", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/ClubImpact.wav"));
            // Remove bedroom stop; move the western approach into the study.
            hunterSettings.FindProperty("bedroom").boundsValue = new Bounds(new Vector3(-4.215f, 1, 3.5f), new Vector3(5.57f, 4, 7));
            var routes = hunterSettings.FindProperty("patrol");
            for (int i = 0; i < routes.arraySize; i++)
            {
                var stop = (Transform)routes.GetArrayElementAtIndex(i).objectReferenceValue;
                if (stop.position.x < -1.5f && stop.position.z > 0)
                    stop.position = new Vector3(-3.7f, 0, -3.43f);
            }
            hunterSettings.ApplyModifiedPropertiesWithoutUndo();
            RepairSkirt(hunter);
            AnonClub.Attach(hunter.transform);
            AnonArmAnimation.Apply(hunter.gameObject, hunter.GetComponent<Animation>());
            var surface = Object.FindAnyObjectByType<NavMeshSurface>();
            // Bake with the swing leaf ignored: its carving obstacle follows the actual door at runtime.
            var slab = GameObject.Find("Bedroom Door").GetComponentInChildren<BoxCollider>();
            slab.enabled = false;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            slab.enabled = true;
            if (!AssetDatabase.Contains(surface.navMeshData))
                AssetDatabase.CreateAsset(surface.navMeshData, Art + "BedroomUpgradeNavMesh.asset");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void RepairSkirt(AnonHunter hunter)
        {
            var skin = hunter.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            Matrix4x4 toHunter = hunter.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            var materials = skin.sharedMaterials;
            int clothes = Array.FindIndex(materials, m => m.name.EndsWith("ano_clothes"));
            int pelvis = Array.FindIndex(skin.bones, b => b.name == "___ 1" &&
                b.parent == hunter.transform.Find("Anon_Model/____/____/____/_"));
            if (clothes >= 0 && pelvis >= 0)
                foreach (int index in mesh.GetIndices(clothes).Distinct())
                {
                    var p = toHunter.MultiplyPoint3x4(vertices[index]);
                    if (p.y < .69f || p.y > 1.065f || Mathf.Abs(p.x) > .34f) continue;
                    weights[index] = new BoneWeight { boneIndex0 = pelvis, weight0 = 1 };
                }
            mesh.boneWeights = weights;
            // Body surfaces hidden beneath opaque clothing must not poke through when viewed from the floor.
            for (int sub = 0; sub < materials.Length; sub++)
            {
                if (!materials[sub].name.EndsWith("ano_leg")) continue;
                var triangles = mesh.GetTriangles(sub);
                var keep = new System.Collections.Generic.List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    bool concealed = true;
                    for (int j = 0; j < 3; j++)
                    {
                        var p = toHunter.MultiplyPoint3x4(vertices[triangles[i+j]]);
                        if (p.y < .745f || p.y > 1.075f || Mathf.Abs(p.x) > .22f) concealed = false;
                    }
                    if (concealed) continue;
                    keep.Add(triangles[i]); keep.Add(triangles[i+1]); keep.Add(triangles[i+2]);
                }
                mesh.SetTriangles(keep, sub);
            }
            if (clothes >= 0)
            {
                materials[clothes].SetFloat("_Cull", 0);
                EditorUtility.SetDirty(materials[clothes]);
            }
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        private static void ReplaceHandheld(HandheldCamcorder camera)
        {
            var config = new SerializedObject(camera);
            var old = config.FindProperty("model").objectReferenceValue as Transform;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dv), camera.transform);
            model.name = "Camcorder";
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.renderingLayerMask = 129;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            model.SetActive(false);
            Set(config, "model", model.transform);
            config.ApplyModifiedPropertiesWithoutUndo();
            var lightTransform = camera.transform.Find("DV Softbox");
            if (lightTransform != null) Object.DestroyImmediate(lightTransform.gameObject);

        }

        private static void InstallBlur()
        {
            foreach (string quality in new[] { "PC", "Mobile" })
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + quality + "_Renderer.asset");
                var feature = renderer.rendererFeatures.OfType<ConsciousnessRenderFeature>().FirstOrDefault();
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<ConsciousnessRenderFeature>();
                    feature.name = "Waking Vision";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                    var setup = new SerializedObject(renderer);
                    var features = setup.FindProperty("m_RendererFeatures");
                    var map = setup.FindProperty("m_RendererFeatureMap");
                    features.arraySize++;
                    features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    setup.ApplyModifiedPropertiesWithoutUndo();
                }
                var config = new SerializedObject(feature);
                Set(config, "shader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Consciousness.shader"));
                config.ApplyModifiedPropertiesWithoutUndo();
                feature.Create();
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void BedroomLight(Transform furniture)
        {
            var installed = furniture.Find("Bedroom ceiling lamp");
            var root = installed != null ? installed.gameObject : new GameObject("Bedroom ceiling lamp");
            root.transform.SetParent(furniture, false);
            root.transform.position = new Vector3(-4.35f, 2.68f, 4.3f);
            var light = root.GetComponent<Light>();
            if (light == null) light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 4;
            light.range = 4.8f;
            light.color = new Color(1, .82f, .62f);
            light.shadows = LightShadows.Soft;
            if (root.transform.childCount > 0) return;
            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fixture.name = "Low ceiling fixture";
            fixture.transform.SetParent(root.transform, false);
            fixture.transform.localPosition = new Vector3(0,.10f,0);
            fixture.transform.localScale = new Vector3(.38f,.045f,.38f);
            Object.DestroyImmediate(fixture.GetComponent<Collider>());
            fixture.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art+"Oxidized Metal.mat");
            var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glass.name = "Frosted dim lamp";
            glass.transform.SetParent(root.transform, false);
            glass.transform.localScale = new Vector3(.31f,.09f,.31f);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            string path = Art+"Bedroom Frosted Lamp.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(.65f,.56f,.43f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor",new Color(.65f,.49f,.28f));
                AssetDatabase.CreateAsset(material,path);
            }
            glass.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void UpgradeBed()
        {
            var bed = GameObject.Find("Bed").transform;
            bed.position = new Vector3(-5.7f, 0, 4.85f);
            bed.localScale = new Vector3(1, 1, 1.28f);
            var headboard = bed.Find("Headboard");
            if (headboard != null) headboard.gameObject.SetActive(false);
            // Keep hiding exits in the aisle rather than stretching their offsets.
            bed.Find("Beside bed").position = new Vector3(-4.54f, .025f, 5.10f);
            bed.Find("Under bed").position = new Vector3(-5.5f, .02f, 5.10f);
        }

        private static void Nightstand(Transform furniture)
        {
            var old = furniture.Find("Bedside Table");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = furniture.Find("Bedside Cabinet");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            const string path = "Assets/Art/Props/Bedroom/BedroomNightstand.fbx";
            var cabinet = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), furniture);
            cabinet.name = "Bedside Cabinet";
            cabinet.transform.SetPositionAndRotation(new(-4.28f, 0, 5.94f), Quaternion.Euler(0, 180, 0));
            var box = cabinet.AddComponent<BoxCollider>();
            box.center = new Vector3(0, .386f, 0);
            box.size = new Vector3(.70f, .772f, .61f);
            var boards = AssetDatabase.LoadAssetAtPath<Material>(Art + "Worn Boards.mat");
            foreach (var r in cabinet.GetComponentsInChildren<Renderer>())
            {
                var name = r.sharedMaterial.name;
                string materialPath = "Assets/Art/Props/Bedroom/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (m == null)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, materialPath);
                }
                m.color = name.Contains("Brass") ? new Color(.53f,.38f,.17f) :
                    name.Contains("Seams") ? new Color(.025f,.018f,.012f) : new Color(.43f,.31f,.21f);
                m.SetFloat("_Metallic", name.Contains("Brass") ? .72f : 0);
                m.SetFloat("_Smoothness", .36f);
                if (name.Contains("Walnut") || name.Contains("Edge"))
                {
                    m.SetTexture("_BaseMap", boards.GetTexture("_BaseMap"));
                    m.mainTextureScale = new Vector2(1.6f, 1.6f);
                }
                r.sharedMaterial = m;
                EditorUtility.SetDirty(m);
            }
        }

        private static void Door(Transform furniture)
        {
            if (furniture.Find("Bedroom Door") != null) return;
            var root = new GameObject("Bedroom Door", typeof(Rigidbody), typeof(HingedDoor));
            root.transform.SetParent(furniture);
            root.transform.SetPositionAndRotation(new(-1.43f, 0, 2.60f), Quaternion.Euler(0,-90,0));
            var rb = root.GetComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.GetComponent<HingedDoor>().Configure(-95, null);
            var oak = AssetDatabase.LoadAssetAtPath<Material>(Art + "Dark Oak.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>(Art + "Brass.mat");
            var leaf = Part(root.transform, "Solid timber leaf", new(.82f,1.10f,0), new(1.64f,2.20f,.065f), oak, true);
            foreach (float y in new[]{.38f,1.03f,1.72f})
                Part(root.transform, "Raised door panel", new(.82f,y,-.046f),new(1.30f,.46f,.038f),oak,false);
            foreach (float z in new[]{-.065f,.065f})
            {
                Part(root.transform,"Handle plate",new(1.45f,1.04f,z),new(.075f,.21f,.025f),metal,false);
                Part(root.transform,"Brass handle",new(1.39f,1.06f,z*1.6f),new(.18f,.026f,.034f),metal,false);
            }
            var obstacle = leaf.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 size, Material mat, bool collider)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }
        private static Transform Marker(Transform parent, string name, Vector3 position, float yaw)
        {
            var marker = parent.Find(name) ?? new GameObject(name).transform;
            marker.SetParent(parent, true); marker.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
            return marker;
        }
        private static void Set(SerializedObject target, string name, Object value) =>
            target.FindProperty(name).objectReferenceValue = value;
    }
}
