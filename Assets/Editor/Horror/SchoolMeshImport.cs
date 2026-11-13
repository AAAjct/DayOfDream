using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DayOfDream.Editor
{
    internal sealed class SchoolMeshImport : AssetPostprocessor
    {
        private const string SchoolModel = "Assets/Art/Environment/HaojieSchool/HaojieSchool.fbx";

        private void OnPostprocessModel(GameObject root)
        {
            if (assetPath != SchoolModel) return;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                var faces = new HashSet<(Vector3Int, Vector3Int, Vector3Int)>();
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    int[] indices = mesh.GetTriangles(submesh);
                    var kept = new List<int>(indices.Length);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var a = Quantize(vertices[indices[i]]);
                        var b = Quantize(vertices[indices[i + 1]]);
                        var c = Quantize(vertices[indices[i + 2]]);
                        if (Compare(a, b) > 0) (a, b) = (b, a);
                        if (Compare(b, c) > 0) (b, c) = (c, b);
                        if (Compare(a, b) > 0) (a, b) = (b, a);
                        if (!faces.Add((a, b, c))) continue;
                        kept.Add(indices[i]);
                        kept.Add(indices[i + 1]);
                        kept.Add(indices[i + 2]);
                    }
                    if (kept.Count != indices.Length) mesh.SetTriangles(kept, submesh, false);
                }
            }
        }

        private static Vector3Int Quantize(Vector3 position) => Vector3Int.RoundToInt(position * 100000);

        private static int Compare(Vector3Int a, Vector3Int b)
        {
            int x = a.x.CompareTo(b.x);
            if (x != 0) return x;
            int y = a.y.CompareTo(b.y);
            return y != 0 ? y : a.z.CompareTo(b.z);
        }
    }
}
