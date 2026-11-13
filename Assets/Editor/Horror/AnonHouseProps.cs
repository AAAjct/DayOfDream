using System.Collections.Generic;
using DayOfDream.Horror;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    internal static class AnonHouseProps
    {
        private const string Folder = "Assets/Art/Environment/AnonHouse/";

        internal static void Rebuild(GameObject item, HouseObject.Kind kind)
        {
            foreach (Transform child in item.transform)
                if (child.name == "Pickup model") return;
            if (item.TryGetComponent<MeshRenderer>(out var placeholder)) Object.DestroyImmediate(placeholder);
            if (item.TryGetComponent<MeshFilter>(out var oldMesh)) Object.DestroyImmediate(oldMesh);
            for (int i = item.transform.childCount - 1; i >= 0; i--)
                if (item.transform.GetChild(i).name is "Key shaft" or "Key tooth")
                    Object.DestroyImmediate(item.transform.GetChild(i).gameObject);
            item.transform.localScale = Vector3.one;
            var meshRoot = new GameObject("Pickup model").transform;
            meshRoot.SetParent(item.transform, false);
            var collider = item.GetComponent<BoxCollider>();
            var brass = Material("Aged Brass", new(.56f, .34f, .12f), .68f, .33f);
            var steel = Material("Dulled Nickel", new(.43f, .46f, .43f), .67f, .42f);
            switch (kind)
            {
                case HouseObject.Kind.CabinetKey:
                case HouseObject.Kind.ExitKey:
                    bool master = kind == HouseObject.Kind.ExitKey;
                    var metal = master ? brass : steel;
                    meshRoot.localRotation = Quaternion.Euler(0, master ? -24 : 23, 0);
                    var outline = master ? new[] {new Vector2(.050f, .037f), new(.030f,.061f), new(-.012f,.061f),new(-.037f,.035f),new(-.042f,0),new(-.037f,-.035f),new(-.012f,-.061f),new(.030f,-.061f),new(.050f,-.037f),new(.064f,0)}
                        : new[] {new Vector2(.045f,.026f),new(.030f,.045f),new(-.025f,.045f),new(-.039f,.023f),new(-.039f,-.023f),new(-.025f,-.045f),new(.030f,-.045f),new(.045f,-.026f)};
                    Part(meshRoot,"Key bow",Save(master ? "Front Door Key Bow" : "Small Key Bow", () => Ring(outline)),metal,new(-.078f,0,0));
                    Part(meshRoot,"Key blade",Save(master ? "Front Door Key Blade" : "Small Key Blade", () => Blade(master)),metal,Vector3.zero);
                    collider.size = new Vector3(.34f,.105f,.19f);
                    break;
                case HouseObject.Kind.Fuse:
                    meshRoot.localRotation = Quaternion.Euler(0, -24, 90);
                    var porcelain = Material("Ivory Ceramic", new(.65f,.61f,.49f), 0,.43f);
                    var marking = Material("Fuse Rating Band", new(.22f,.052f,.034f), 0,.29f);
                    Part(meshRoot,"Porcelain cartridge",Save("Fuse Cartridge",()=>Lathe(new(.024f,-.09f),new(.034f,-.08f),new(.034f,.08f),new(.024f,.09f))),porcelain,Vector3.zero);
                    var cap = Save("Fuse Cap",()=>Lathe(new(.002f,-.022f),new(.032f,-.022f),new(.038f,-.016f),new(.038f,.017f),new(.031f,.022f),new(.002f,.022f)));
                    Part(meshRoot,"Contact cap",cap,brass,Vector3.up*.086f);
                    Part(meshRoot,"Contact cap",cap,steel,Vector3.down*.086f);
                    var band = Save("Fuse Band",()=>Lathe(new(.0348f,-.004f),new(.0348f,.004f)));
                    Part(meshRoot,"Rating stripe",band,marking,Vector3.up*.033f);
                    Part(meshRoot,"Rating stripe",band,marking,Vector3.up*.047f);
                    collider.size = new Vector3(.27f,.12f,.16f);
                    break;
                case HouseObject.Kind.Bottle:
                    var glass = Material("Old Green Bottle", new(.063f,.145f,.086f), .15f,.64f);
                    var paper = Material("Watermarked Bottle Paper", new(.42f,.34f,.21f), 0,.23f);
                    Part(meshRoot,"Bottle glass",Save("Old Glass Bottle",()=>Lathe(new(.003f,-.10f),new(.043f,-.108f),new(.054f,-.103f),
                        new(.056f,-.087f),new(.056f,.035f),new(.050f,.067f),new(.025f,.101f),new(.019f,.115f),
                        new(.019f,.163f),new(.024f,.165f),new(.024f,.176f),new(.015f,.178f),new(.014f,.149f))),glass,Vector3.zero);
                    Part(meshRoot,"Paper label",Save("Bottle Label",()=>Lathe(new(.0568f,-.062f),new(.0568f,.011f))),paper,Vector3.zero);
                    collider.center = Vector3.up * .025f;
                    collider.size = new Vector3(.16f,.31f,.16f);
                    break;
            }
        }

        private static Material Material(string name, Color color, float metal, float smooth)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) {name = name, color = color};
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smooth);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh Save(string name, System.Func<Mesh> create)
        {
            string path = Folder + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = create();
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh Ring(Vector2[] outline)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            foreach (float y in new[] {-.009f,.009f})
                foreach (float factor in new[] {1f,.54f})
                    foreach (var p in outline) vertices.Add(new Vector3(p.x*factor,y,p.y*factor));
            int n = outline.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i+1)%n;
                Quad(indices, i,j,j+n,i+n);
                Quad(indices, i+2*n,i+3*n,j+3*n,j+2*n);
                Quad(indices, i,i+2*n,j+2*n,j);
                Quad(indices, i+n,j+n,j+3*n,i+3*n);
            }
            return Mesh(vertices, indices);
        }

        private static Mesh Blade(bool master)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            void Segment(float from, float to, float y, float z0, float z1)
            {
                int i=vertices.Count;
                vertices.AddRange(new[] {new Vector3(from,-y,z0),new(to,-y,z0),new(to,-y,z1),new(from,-y,z1),
                    new(from,y,z0),new(to,y,z0),new(to,y,z1),new(from,y,z1)});
                Quad(indices,i,i+1,i+2,i+3); Quad(indices,i+4,i+7,i+6,i+5);
                Quad(indices,i,i+4,i+5,i+1); Quad(indices,i+1,i+5,i+6,i+2);
                Quad(indices,i+2,i+6,i+7,i+3); Quad(indices,i+3,i+7,i+4,i);
            }
            Segment(-.04f,.128f,.009f,-.013f,.012f);
            Segment(-.038f,-.012f,.012f,-.022f,.022f);
            Segment(.041f,.065f,.008f,.011f,master?.039f:.032f);
            Segment(.080f,.102f,.008f,.011f,.027f);
            Segment(.111f,.128f,.008f,.011f,master?.043f:.033f);
            Segment(-.007f,.11f,.010f,-.007f,-.003f);
            return Mesh(vertices,indices);
        }

        private static Mesh Lathe(params Vector2[] profile)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            const int sides = 32;
            for (int ring=0;ring<profile.Length;ring++)
                for (int i=0;i<sides;i++)
                {
                    float a=2*Mathf.PI*i/sides;
                    vertices.Add(new Vector3(profile[ring].x*Mathf.Cos(a),profile[ring].y,profile[ring].x*Mathf.Sin(a)));
                    if (ring==0) continue;
                    int row=(ring-1)*sides, j=(i+1)%sides;
                    Quad(indices,row+i,row+i+sides,row+j+sides,row+j);
                }
            return Mesh(vertices,indices);
        }

        private static void Quad(List<int> indices,int a,int b,int c,int d)
        { indices.Add(a);indices.Add(b);indices.Add(c);indices.Add(a);indices.Add(c);indices.Add(d); }

        private static Mesh Mesh(List<Vector3> vertices, List<int> indices)
        {
            var mesh=new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
