using System;
using System.Linq;
using DayOfDream.Horror;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    public static class AnonHouseDoorWidth
    {
        private const float MaximumWidth = 1.26f;
        private const string JambName = "Narrow doorway jambs";
        private const string Art = "Assets/Art/Environment/AnonHouse/";

        public static int Apply()
        {
            if (SceneManager.GetActiveScene().path != AnonHouseBuilder.Path || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open AnonHouse in Edit Mode.");

            var shell = GameObject.Find("House Expansion Structure").transform;
            var oak = AssetDatabase.LoadAssetAtPath<Material>(Art + "Dark Oak.mat");
            var doors = Object.FindObjectsByType<HingedDoor>();
            int changed = 0;
            foreach (var door in doors)
            {
                var hinge = door.transform;
                var slab = hinge.GetComponentInChildren<BoxCollider>();
                float oldWidth = slab.transform.localScale.x;
                var doorSettings = new SerializedObject(door);
                doorSettings.FindProperty("leafWidth").floatValue = Mathf.Min(oldWidth, MaximumWidth);
                doorSettings.ApplyModifiedPropertiesWithoutUndo();
                if (oldWidth <= MaximumWidth + .001f) continue;

                // Keep the opening centered. The jamb extensions cover the former gap up to the plaster.
                float inset = (oldWidth - MaximumWidth) / 2;
                Vector3 oldHinge = hinge.position;
                var frame = shell.Find(door.name + " Frame");
                if (frame != null) Object.DestroyImmediate(frame.gameObject);
                var jambs = new GameObject(door.name + " " + JambName).transform;
                jambs.SetParent(shell, false);
                jambs.SetPositionAndRotation(oldHinge, hinge.rotation);
                float extension = inset + .19f;
                float height = slab.transform.localScale.y;
                AddJamb(jambs, inset - extension / 2, extension, height, oak);
                AddJamb(jambs, oldWidth - inset + extension / 2, extension, height, oak);

                hinge.position += hinge.right * inset;
                foreach (Transform part in hinge)
                {
                    Vector3 position = part.localPosition;
                    Vector3 scale = part.localScale;
                    if (part == slab.transform || part.name.Contains("panel", StringComparison.OrdinalIgnoreCase) &&
                        !part.name.Contains("moulding", StringComparison.OrdinalIgnoreCase))
                    {
                        position.x -= inset;
                        scale.x -= 2 * inset;
                    }
                    else if (position.x > oldWidth / 2) position.x -= 2 * inset;
                    part.localPosition = position;
                    part.localScale = scale;
                }
                changed++;
            }

            var exit = GameObject.Find("Locked front door").transform;
            if (exit.localScale.x > MaximumWidth + .001f)
            {
                float oldWidth = exit.localScale.x;
                var scale = exit.localScale;
                scale.x = MaximumWidth;
                exit.localScale = scale;
                foreach (Transform fitting in exit.parent)
                {
                    if (fitting.name == "Door brace")
                    {
                        var fittingScale = fitting.localScale;
                        fittingScale.x -= oldWidth - MaximumWidth;
                        fitting.localScale = fittingScale;
                    }
                    else if (fitting.name == "Lock plate")
                        fitting.localPosition -= Vector3.right * ((oldWidth - MaximumWidth) / 2);
                }
                var jambs = new GameObject("Front door " + JambName).transform;
                jambs.SetParent(shell, false);
                jambs.position = exit.position - Vector3.up * (scale.y / 2);
                float extension = (oldWidth - MaximumWidth) / 2 + .05f;
                AddJamb(jambs, -(MaximumWidth + extension) / 2, extension, scale.y, oak);
                AddJamb(jambs, (MaximumWidth + extension) / 2, extension, scale.y, oak);
                changed++;
            }

            foreach (Transform crate in GameObject.Find("House Expansion Furnishings").transform)
            {
                if (crate.name != "Braced wooden crate" ||
                    (crate.position - new Vector3(-2.3f, -3, -8.5f)).sqrMagnitude > .01f) continue;
                crate.position = new Vector3(-2.45f, -3, -7.66f);
                changed++;
                break;
            }
            if (changed > 0) RebuildNavigation(doors);
            return changed;
        }

        private static void AddJamb(Transform frame, float x, float width, float height, Material material)
        {
            var jamb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jamb.name = "Doorway jamb extension";
            jamb.transform.SetParent(frame, false);
            jamb.transform.localPosition = new Vector3(x, (height + .1f) / 2, 0);
            jamb.transform.localScale = new Vector3(width, height + .1f, .265f);
            var renderer = jamb.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false; // The Blender architecture renders this piece.
        }

        private static void RebuildNavigation(HingedDoor[] doors)
        {
            var colliders = doors.SelectMany(door => door.GetComponentsInChildren<Collider>()).Where(c => c.enabled).ToArray();
            var obstacles = doors.SelectMany(door => door.GetComponentsInChildren<NavMeshObstacle>()).Where(o => o.enabled).ToArray();
            var surface = Object.FindAnyObjectByType<NavMeshSurface>();
            var existing = surface.navMeshData;
            try
            {
                foreach (var collider in colliders) collider.enabled = false;
                foreach (var obstacle in obstacles) obstacle.enabled = false;
                surface.overrideVoxelSize = true;
                surface.voxelSize = .06f;
                Physics.SyncTransforms();
                surface.BuildNavMesh();
                if (surface.navMeshData == null || surface.navMeshData == existing)
                    throw new InvalidOperationException("Doorway navigation bake failed.");
                var baked = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(baked, existing);
                surface.navMeshData = existing;
                surface.AddData();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(baked);
            }
            finally
            {
                foreach (var collider in colliders) collider.enabled = true;
                foreach (var obstacle in obstacles) obstacle.enabled = true;
                Physics.SyncTransforms();
            }
        }
    }
}
