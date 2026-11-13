using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DayOfDream.Editor
{
    internal static class AnonClub
    {
        private const string WristPath = "Anon_Model/____/____/____/_/___/___2/__ 1/__/___/___ 1";
        private const string Art = "Assets/Art/Environment/AnonHouse/";
        private const string MeshPath = Art + "AnonClub.asset";

        internal static void Attach(Transform hunter)
        {
            var wrist = hunter.Find(WristPath);
            if (wrist == null) throw new InvalidOperationException("Anon right wrist is missing.");
            var existing = wrist.Find("Wooden Club");
            var club = existing != null ? existing.gameObject :
                new GameObject("Wooden Club", typeof(MeshFilter), typeof(MeshRenderer));
            club.transform.SetParent(wrist, false);
            club.transform.localPosition = new Vector3(.026f, -.010f, .002f);
            club.transform.localRotation = Quaternion.Euler(0, 9, 0);
            club.transform.localScale = Vector3.one / wrist.lossyScale.x;
            club.GetComponent<MeshFilter>().sharedMesh = LoadMesh();
            club.GetComponent<MeshRenderer>().sharedMaterials = new[]
            {
                ClubMaterial("Club Timber", "Worn Boards", new Color(.72f, .59f, .48f)),
                ClubMaterial("Club Grip", "Dark Oak", new Color(.42f, .35f, .30f))
            };
        }

        private static Material ClubMaterial(string name, string source, Color tint)
        {
            string path = Art + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Material>(Art + source + ".mat")
                    .GetTexture("_BaseMap"));
                material.SetColor("_BaseColor", tint);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        private static Mesh LoadMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh != null) return mesh;

            // The grip is at Z = 0; the thicker, uneven end extends beyond the knuckles.
            float[] length = { -.105f, -.095f, -.078f, -.055f, .065f, .084f, .24f, .43f, .60f, .626f, .63f };
            float[] radius = { .002f, .022f, .019f, .021f, .021f, .018f, .024f, .034f, .043f, .037f, .002f };
            const int sides = 10;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var timber = new List<int>();
            var grip = new List<int>();
            for (int ring = 0; ring < length.Length; ring++)
            {
                for (int side = 0; side <= sides; side++)
                {
                    float angle = (side % sides) * 2 * Mathf.PI / sides;
                    float r = radius[ring] * (1 + .075f * Mathf.Sin(3 * angle + ring * .28f));
                    vertices.Add(new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), length[ring]));
                    uv.Add(new Vector2((float)side / sides, length[ring] + .105f));
                    if (ring == 0 || side == sides) continue;
                    int a = (ring - 1) * (sides + 1) + side;
                    int b = a + sides + 1;
                    var triangles = ring is 3 or 4 or 5 ? grip : timber;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
            mesh = new Mesh { name = "Anon Wooden Club" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(timber, 0);
            mesh.SetTriangles(grip, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }
    }
}
