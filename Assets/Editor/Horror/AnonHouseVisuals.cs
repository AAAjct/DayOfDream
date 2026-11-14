using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    public static class AnonHouseVisuals
    {
        private const string Art = "Assets/Art/Environment/AnonHouse/";

        [MenuItem("Tools/Day Of Dream/Export Anon House Layout")]
        public static void ExportLayout()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != AnonHouseBuilder.Path)
                throw new InvalidOperationException("Open Anon House first.");
            var parts = new[] { "House Structure", "House Finishing", "House Expansion Structure" }
                .Select(group => GameObject.Find(group)).Where(group => group != null)
                .SelectMany(group => group.GetComponentsInChildren<MeshRenderer>())
                .Where(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.name == "Cube" && renderer.name != "Smooth stair collision")
                .Select(renderer =>
                {
                    var transform = renderer.transform;
                    var p = transform.position;
                    var s = transform.lossyScale;
                    var q = transform.rotation;
                    var c = renderer.sharedMaterial.color;
                    return new { name = renderer.name, position = new[] { p.x,p.y,p.z }, size = new[] { s.x,s.y,s.z },
                        rotation = new[] { q.x,q.y,q.z,q.w }, material = renderer.sharedMaterial.name,
                        color = new[] { c.r,c.g,c.b } };
                });
            File.WriteAllText("Temp/AnonHouseLayout.json", Newtonsoft.Json.JsonConvert.SerializeObject(parts));
        }

        [MenuItem("Tools/Day Of Dream/Import Anon House Visuals")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                    .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes and exit Play Mode.");
            var house = EditorSceneManager.OpenScene(AnonHouseBuilder.Path);
            InstallArchitecture();
            RepairAnon();
            EditorSceneManager.SaveScene(house);
            var school = EditorSceneManager.OpenScene(HorrorSchoolBuilder.ScenePath);
            foreach (var label in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include)
                         .Where(t => t.name.StartsWith("Wayfinding ") || t.name.EndsWith("note label")))
                StyleSign(label);
            EditorSceneManager.SaveScene(school);
            EditorSceneManager.OpenScene(AnonHouseBuilder.Path);
            AssetDatabase.SaveAssets();
        }

        public static void RefreshShell()
        {
            InstallArchitecture();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static void InstallArchitecture()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "HouseArchitecture.fbx");
            if (asset == null) throw new InvalidOperationException("Import the Blender architecture first.");
            var previous = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "House Architecture • Blender")?.gameObject;
            if (previous != null) Object.DestroyImmediate(previous);
            var architecture = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            architecture.name = "House Architecture • Blender";
            foreach (var renderer in architecture.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    AssetDatabase.LoadAssetAtPath<Material>(Art + material.name + ".mat") ?? material).ToArray();
                // 墙体和楼板挡住视线，普通装饰只参与被遮挡剔除。
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.OccludeeStatic;
                if (renderer.name.StartsWith("Occluder ", StringComparison.Ordinal))
                    flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
            }
            // The authored collision shell remains the source for the baked NavMesh.
            foreach (string group in new[] { "House Structure", "House Finishing", "House Expansion Structure" })
            {
                var root = GameObject.Find(group);
                if (root == null) continue;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    renderer.enabled = false;
            }

            if (GameObject.Find("House Expansion Furnishings") is { } furnishings)
                OptimizeFurnishings(furnishings);
        }

        private static void OptimizeFurnishings(GameObject furnishings)
        {
            // 固定家具参与剔除和静态合批。开合门保留动态渲染，由门板负责投影。
            var staticProps = new[] { "Bookcase", "Exterior Facade", "Braced wooden crate", "Recessed window" };
            foreach (var item in furnishings.transform.Cast<Transform>().Where(t => staticProps.Contains(t.name)))
            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.GetComponentInParent<Rigidbody>() != null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    flags | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
                if (renderer.name is "Bound volume" or "Window mullion")
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            foreach (var door in furnishings.GetComponentsInChildren<Horror.HingedDoor>())
            foreach (var renderer in door.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name is "Door inset panel" or "Panel side moulding" or "Latch plate" or "Lever handle")
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            // 建筑点光只负责补光。主光和局部重点灯光负责投影。
            foreach (var lamp in furnishings.GetComponentsInChildren<Light>())
                if (lamp.name == "Architectural lamp") lamp.shadows = LightShadows.None;
        }

        private static void RepairAnon()
        {
            var hunter = Object.FindAnyObjectByType<Horror.AnonHunter>();
            var skin = hunter.GetComponentInChildren<SkinnedMeshRenderer>();
            const string model = "Assets/Art/Models/Anon/Anon.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(model);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().Single();
            var mesh = Object.Instantiate(source);
            mesh.name = "Anon House garment";
            // Remove the old toon outline shell, which overlapped the new URP surface.
            int outline = Array.FindIndex(skin.sharedMaterials, material => material.name.Contains("Outline"));
            if (outline >= 0) mesh.SetTriangles(Array.Empty<int>(), outline);

            int clothing = Array.FindIndex(skin.sharedMaterials, material => material.name.EndsWith("ano_clothes"));
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            Matrix4x4 toHunter = hunter.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            Matrix4x4 toMesh = toHunter.inverse;
            foreach (int index in mesh.GetIndices(clothing).Distinct())
            {
                Vector3 p = toHunter.MultiplyPoint3x4(vertices[index]);
                if (weights[index].boneIndex0 != 1 || p.y < .70f || p.y >= 1.13f) continue;
                float hem = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.13f,.75f,p.y));
                p.x *= 1 + .12f * hem;
                p.z = .025f + (p.z - .025f) * (1 + .95f * hem);
                vertices[index] = toMesh.MultiplyPoint3x4(p);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = Art + "AnonHouseSkin.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh,path);
            else
            {
                EditorUtility.CopySerialized(mesh,existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            skin.sharedMesh = mesh;
            skin.updateWhenOffscreen = true;
            foreach (var mat in skin.sharedMaterials.Where(m => !m.name.Contains("Outline")).Distinct())
            {
                string originalName = mat.name.Substring("Anon_".Length);
                var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Models/Anon/Materials/" + originalName + ".mat");
                if (original != null)
                {
                    mat.shader = original.shader;
                    mat.CopyPropertiesFromMaterial(original);
                }
                EditorUtility.SetDirty(mat);
            }
        }

        internal static void StyleSign(TextMeshPro label)
        {
            label.fontSize = label.text.Contains('\n') ? 2.8f : 3.25f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.95f,.94f,.84f);
            label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (label.transform.Find("Matte sign backing") != null) return;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Matte sign backing";
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.transform.SetParent(label.transform,false);
            board.transform.localPosition = Vector3.forward * .025f;
            Vector2 size = label.rectTransform.sizeDelta;
            board.transform.localScale = new Vector3(size.x,size.y,.018f);
            string path = Art + "Matte Sign.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(.028f,.036f,.032f) };
                AssetDatabase.CreateAsset(mat,path);
            }
            var renderer = board.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
