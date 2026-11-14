using System;
using System.Linq;
using DayOfDream.Horror;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    public static class ExpandedHouseBuilder
    {
        private const string Art = "Assets/Art/Environment/AnonHouse/";
        private static Transform structure, decor, routes;
        private static Material plaster, timber, oak, stone, metal, brass, cloth;
        private const float Storey = 3.3f;

        [MenuItem("Tools/Day Of Dream/Expand Four Storey House")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != AnonHouseBuilder.Path || scene.isDirty)
                throw new InvalidOperationException("Open the saved Anon House in Edit Mode.");
            if (GameObject.Find("House Expansion Structure") != null)
                throw new InvalidOperationException("The four-storey expansion is already installed.");
            plaster = Load("Damp Plaster"); timber = Load("Worn Boards"); oak = Load("Dark Oak");
            stone = Load("Cellar Brick"); metal = Load("Oxidized Metal"); brass = Load("Brass"); cloth = Load("Wine Fabric");
            structure = new GameObject("House Expansion Structure").transform;
            decor = new GameObject("House Expansion Furnishings").transform;
            routes = new GameObject("Expanded patrol and room markers").transform;
            Physics.SyncTransforms();
            // Preserve the original house, puzzles and bedroom. Only cut explicit new apertures.
            CutOriginalWall(true, 10, .92f, 0, 1.85f, 2.28f);
            CutOriginalWall(true, 7, 4.6f, 0, 1.85f, 2.28f);
            CutOriginalWall(true, 1, -10.5f, -3, 1.85f, 2.28f);
            var originalVisuals = GameObject.Find("House Architecture • Blender");
            if (originalVisuals != null) originalVisuals.SetActive(false);
            foreach (string group in new[] { "House Structure", "House Finishing" })
                foreach (var renderer in GameObject.Find(group).GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.GetComponent<MeshFilter>()?.sharedMesh?.name == "Cube") renderer.enabled = true;
            ExistingDoors();
            StairTower();
            UpperFloor();
            Attic();
            Basement();
            Courtyard();
            AddRoutes();
            MoveBreakerClear();
            Bake();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Art + name + ".mat");
        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool collision = true, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent != null ? parent : structure, false);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static void Floor(string name, float x0, float x1, float z0, float z1, float y, Material mat) =>
            Box(name, new((x0+x1)*.5f,y-.10f,(z0+z1)*.5f),new(x1-x0,.20f,z1-z0),mat);
        private static void WallX(float x, float z0, float z1, float y, float height=2.95f, Material mat=null)
        {
            if (z1-z0>.03f) Box("Plastered wall",new(x,y+height*.5f,(z0+z1)*.5f),new(.20f,height,z1-z0),mat??plaster);
        }
        private static void WallZ(float z, float x0, float x1, float y, float height=2.95f, Material mat=null)
        {
            if (x1-x0>.03f) Box("Plastered wall",new((x0+x1)*.5f,y+height*.5f,z),new(x1-x0,height,.20f),mat??plaster);
        }

        private static void CutOriginalWall(bool alongX, float plane, float opening, float y, float width, float height)
        {
            var colliders = GameObject.Find("House Structure").GetComponentsInChildren<BoxCollider>();
            foreach(var c in colliders)
            {
                Bounds b=c.bounds;
                float normal=alongX?b.center.x:b.center.z;
                float thickness=alongX?b.size.x:b.size.z;
                float start=alongX?b.min.z:b.min.x, end=alongX?b.max.z:b.max.x;
                if(Mathf.Abs(normal-plane)>.15f || thickness>.4f || b.min.y>y+.2f || b.max.y<y+2 ||
                    opening<=start || opening>=end)continue;
                Object.DestroyImmediate(c.gameObject);
                if(alongX){WallX(plane,start,opening-width*.5f,y,b.max.y-y);WallX(plane,opening+width*.5f,end,y,b.max.y-y);}
                else{WallZ(plane,start,opening-width*.5f,y,b.max.y-y);WallZ(plane,opening+width*.5f,end,y,b.max.y-y);}
                Vector3 lintel=alongX?new(plane,y+(height+b.max.y-y)*.5f,opening):new(opening,y+(height+b.max.y-y)*.5f,plane);
                Box("New doorway lintel",lintel,alongX?new(.20f,b.max.y-y-height,width):new(width,b.max.y-y-height,.20f),plaster);
                return;
            }
            throw new InvalidOperationException("Missing wall for aperture at "+plane+", "+opening+", "+y);
        }

        private static void DoorwayX(string name,float x,float z,float y,float z0,float z1,float height=2.95f)
        {
            WallX(x,z0,z-.93f,y,height);WallX(x,z+.93f,z1,y,height);
            Box("Door lintel",new(x,y+(2.28f+height)*.5f,z),new(.22f,height-2.28f,1.86f),oak);
            Door(name,new(x,y,z-.90f),-90);
        }
        private static void DoorwayZ(string name,float z,float x,float y,float x0,float x1,float height=2.95f)
        {
            WallZ(z,x0,x-.93f,y,height);WallZ(z,x+.93f,x1,y,height);
            Box("Door lintel",new(x,y+(2.28f+height)*.5f,z),new(1.86f,height-2.28f,.22f),oak);
            Door(name,new(x-.90f,y,z),0);
        }

        private static void ExistingDoors()
        {
            foreach(float x in new[]{-1.43f,1.43f})
                foreach(float z in new[]{-3.425f,3.425f})
                {
                    if(x<0&&z>0)continue; // Existing bedroom door.
                    Door(x<0?"Study Hall Door":z>0?"Kitchen Hall Door":"Dining Hall Door",new(x,0,z-.84f),-90,1.65f,2.20f);
                }
            foreach(float x in new[]{-5.2f,5.2f})
                Door(x<0?"Bedroom Study Connecting Door":"Kitchen Dining Connecting Door",new(x-.84f,0,0),0,1.65f,2.20f);
            Door("Courtyard Kitchen Door",new(7,0,4.6f-.90f),-90);
            Door("Ground Floor Stair Door",new(10,0,.02f),-90);
            Door("Basement Workshop Door",new(1,-3,-11.4f),-90);
        }

        private static void StairTower()
        {
            const float west=10.15f,east=16.2f;
            Floor("Stair tower ground",west,east,-6.8f,2.45f,0,stone);
            for(int storey=0;storey<2;storey++)
            {
                float y=storey*Storey;
                Floor("Half landing",10.5f,15.85f,-6.6f,-4.55f,y+1.65f,timber);
                Floor("Stair arrival landing",west,east,.18f,2.45f,y+Storey,timber);
                Flight(new(11.55f,y,.18f),new(11.55f,y+1.65f,-4.55f));
                Flight(new(14.65f,y+1.65f,-4.55f),new(14.65f,y+Storey,.18f));
                for(int i=0;i<8;i++)
                {
                    float t=i/7f;
                    Box("Stair rail upright",new(12.64f,y+.68f+1.65f*t,.18f-4.73f*t),new(.055f,1.03f,.055f),oak)
                        .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Box("Stair rail upright",new(13.56f,y+2.33f+1.65f*t,-4.55f+4.73f*t),new(.055f,1.03f,.055f),oak)
                        .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                Beam("Sloping handrail",new(12.64f,y+1.14f,.18f),new(12.64f,y+2.79f,-4.55f),.065f,oak)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Beam("Sloping handrail",new(13.56f,y+2.79f,-4.55f),new(13.56f,y+4.44f,.18f),.065f,oak)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                LightFixture(new(15.92f,y+2.4f,-5.6f),new Color(1,.69f,.39f),24,8);
            }
            WallX(east,-6.8f,2.45f,0,9.3f);WallZ(-6.8f,10.1f,east,0,9.3f);
            WallZ(2.45f,10.1f,east,0,9.3f);
            // Tower's western side joins the cellar stair at ground and both upper floors.
            for(int level=1;level<=2;level++)
                DoorwayX(level==1?"Second Floor Stair Door":"Attic Stair Door",10,.92f,level*Storey,-6.8f,2.45f);
            Floor("Stair tower roof",9.9f,16.4f,-7,2.65f,9.65f,oak);
            Mark("Ground Floor Landing",new(11.5f,.05f,1.35f));
            Mark("Second Floor Landing",new(11.5f,3.35f,1.35f));
            Mark("Attic Landing",new(11.5f,6.65f,1.35f));
        }

        private static void Flight(Vector3 start,Vector3 end)
        {
            Vector3 mid=(start+end)*.5f,dir=end-start;
            var ramp=Box("Smooth stair collision",mid-Vector3.up*.10f,new(2.05f,.18f,dir.magnitude+.10f),timber);
            ramp.transform.rotation=Quaternion.LookRotation(new Vector3(0,-dir.y,-dir.z));
            // Quaternion.LookRotation above maps local Z down the flight; choose the physical slope explicitly.
            float slope=Mathf.Atan2(dir.y,Mathf.Abs(dir.z))*Mathf.Rad2Deg;
            ramp.transform.rotation=Quaternion.Euler(dir.z<0?slope:-slope,0,0);
            ramp.GetComponent<Renderer>().enabled=false;
            for(int i=0;i<15;i++)
            {
                float t=(i+.5f)/15;
                Vector3 p=Vector3.Lerp(start,end,t);
                Box("Timber stair tread",p-Vector3.up*.045f,new(2.08f,.09f,Mathf.Abs(dir.z)/15+.018f),timber,false);
                Box("Stair nosing",p+new Vector3(0,.003f,Mathf.Sign(-dir.z)*.14f),new(2.10f,.025f,.042f),oak,false);
            }
        }

        private static void UpperFloor()
        {
            float y=Storey;
            Floor("Second floor slab",-7,10,-7,7,y,timber);
            Floor("Second floor ceiling",-7,10,-7,7,y+3.05f,oak);
            WallX(-7,-7,7,y);WallZ(-7,-7,10,y);WallZ(7,-7,10,y);
            WallX(10,-7,-6.8f,y);WallX(10,2.45f,7,y);
            DoorwayX("Library Door",-1.4f,3.4f,y,0,7);
            DoorwayX("Guest Bedroom Door",-1.4f,-3.4f,y,-7,0);
            DoorwayX("Master Bedroom Door",1.4f,3.4f,y,0,7);
            DoorwayX("Bathroom Door",1.4f,-3.4f,y,-7,0);
            DoorwayZ("Library Guest Connecting Door",0,-4.5f,y,-7,-1.4f);
            DoorwayZ("Master Bathroom Connecting Door",0,5.2f,y,1.4f,10);
            Bookshelf(new(-6.50f,y,3.8f),90,2.3f);
            Bookshelf(new(-4.3f,y,6.55f),180,2.7f);
            Table(new(-4.1f,y,3.25f),0);
            FurnishModel("Bed","Guest Bed",new(-5.5f,y,-4.8f),0,new(1,1,1.28f));
            FurnishModel("BedroomNightstand","Guest Nightstand",new(-4.1f,y,-5.7f),0,Vector3.one);
            FurnishModel("Bed","Master Bed",new(7.9f,y,5.2f),180,new(1.2f,1,1.28f));
            FurnishModel("OakTable","Master Writing Desk",new(3.1f,y,5.9f),180,Vector3.one);
            FurnishModel("Wardrobe","Master Wardrobe",new(9.2f,y,3.4f),-90,Vector3.one);
            Bathtub(new(7.5f,y,-5.6f));
            Box("Stone washstand",new(3.0f,y+.72f,-5.95f),new(1.45f,1.44f,.68f),stone,true,decor);
            Box("Porcelain wash basin",new(3.0f,y+1.48f,-5.95f),new(.88f,.12f,.58f),Load("Ivory Ceramic"),false,decor);
            Window(new(-6.87f,y+1.72f,4.3f),90);
            Window(new(5.5f,y+1.72f,6.87f),180);
            foreach(var p in new[]{new Vector3(-4,y+2.65f,3.4f),new(-4,y+2.65f,-3.4f),new(5.3f,y+2.65f,3.4f),new(5.3f,y+2.65f,-3.4f),new(0,y+2.65f,0)})
                LightFixture(p,new Color(1,.72f,.46f),22,8);
            Mark("Library Interior",new(-4.3f,y+.05f,2.8f));
            Mark("Guest Bedroom Interior",new(-4.3f,y+.05f,-2.8f));
            Mark("Master Bedroom Interior",new(4.3f,y+.05f,2.8f));
            Mark("Bathroom Interior",new(4.3f,y+.05f,-2.8f));
        }

        private static void Attic()
        {
            float y=Storey*2;
            Floor("Attic floor",-7,10,-7,7,y,timber);
            WallX(-7,-7,7,y,2.25f,oak);WallX(10,-7,-6.8f,y,2.25f,oak);WallX(10,2.45f,7,y,2.25f,oak);
            WallZ(-7,-7,10,y,2.6f,oak);WallZ(7,-7,10,y,2.6f,oak);
            DoorwayX("Attic Storage Door",-.6f,3.4f,y,0,7,2.6f);
            DoorwayX("Attic Archive Door",-.6f,-3.4f,y,-7,0,2.6f);
            DoorwayX("Attic Workshop Door",3.6f,3.4f,y,0,7,2.6f);
            DoorwayX("Attic Trunk Room Door",3.6f,-3.4f,y,-7,0,2.6f);
            DoorwayZ("Attic West Connecting Door",0,-4.2f,y,-7,-.6f,2.6f);
            DoorwayZ("Attic East Connecting Door",0,6.8f,y,3.6f,10,2.6f);
            float ridge=11.4f,eave=8.88f;
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 a=new(1.5f,ridge,0),b=new(1.5f+side*8.85f,eave,0);
                var roof=Box("Pitched attic roof",(a+b)*.5f,new(Vector3.Distance(a,b),.22f,14.7f),oak);
                roof.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
            }
            for(int i=0;i<7;i++)
            {
                float z=-6.6f+i*2.2f;
                Beam("Attic roof rafter",new(-7.15f,eave-.05f,z),new(1.5f,ridge-.12f,z),.18f,oak);
                Beam("Attic roof rafter",new(10.15f,eave-.05f,z),new(1.5f,ridge-.12f,z),.18f,oak);
                Beam("Attic tie beam",new(-6.7f,9.0f,z),new(9.7f,9.0f,z),.15f,oak);
            }
            for(int i=0;i<9;i++)Crate(new(-5.8f+(i%3)*1.2f,y,-5.7f+(i/3)*1.6f),new(.75f,.65f+(i%2)*.28f,.75f),i*17);
            for(int i=0;i<6;i++)Crate(new(5.1f+(i%2)*2,y,2.1f+(i/2)*1.5f),new(1.05f,.58f,.68f),i*11);
            Bookshelf(new(-6.4f,y,4),90,2.2f);Table(new(7,y,-4.9f),0);
            for(int i=0;i<4;i++)Box("Attic covered trunk",new(5.2f+i*.9f,y+.38f,-5.8f),new(.75f,.76f,.62f),i%2==0?cloth:oak,true,decor);
            foreach(var p in new[]{new Vector3(1.5f,y+2.8f,4.5f),new(1.5f,y+2.8f,-4.5f),new(-4,y+2.1f,3.4f),new(6,y+2.1f,-3.4f)})
                LightFixture(p,new Color(.78f,.84f,1),20,8);
            Mark("Attic West Interior",new(-3.8f,y+.05f,2.8f));
            Mark("Attic East Interior",new(6.2f,y+.05f,-2.8f));
            Mark("Attic Central Aisle",new(1.5f,y+.05f,-3.5f));
        }

        private static void Basement()
        {
            Floor("Extended basement slab",-7,1,-14,-7,-3,stone);
            Floor("Basement overhead ceiling",-7,1,-14,-7,-.25f,stone);
            WallX(-7,-14,-7,-3,2.75f,stone);WallZ(-14,-7,1,-3,2.75f,stone);WallZ(-7,-7,1,-3,2.75f,stone);
            DoorwayX("Basement Boiler Room Door",-1.5f,-12.1f,-3,-14,-10.5f,2.75f);
            DoorwayX("Basement Storage Door",-1.5f,-8.8f,-3,-10.5f,-7,2.75f);
            DoorwayZ("Basement Connecting Door",-10.5f,-4.3f,-3,-7,-1.5f,2.75f);
            for(int i=0;i<3;i++)
            {
                Cylinder("Boiler tank",new(-5.8f+i*1.5f,-1.95f,-12.7f),.48f,1.9f,metal);
                Beam("Boiler pipe",new(-5.8f+i*1.5f,-1,-12.7f),new(-5.8f+i*1.5f,-.55f,-12.7f),.12f,metal);
                Crate(i == 2 ? new(-2.45f,-3,-7.66f) : new(-5.7f+i*1.7f,-3,-8.5f),new(1,.88f,.85f),i*13);
            }
            Bookshelf(new(-6.5f,-3,-9),90,2.2f);
            LightFixture(new(-3.8f,-.65f,-12),new Color(.65f,.78f,1),22,7);
            LightFixture(new(-3.8f,-.65f,-8.7f),new Color(1,.63f,.34f),22,7);
            Mark("Basement Boiler Interior",new(-3.8f,-2.95f,-12));
            Mark("Basement Storage Interior",new(-3.8f,-2.95f,-8.6f));
        }

        private static void Courtyard()
        {
            Floor("Courtyard west",-13,-7.15f,-20,15,0,stone);
            Floor("Courtyard east",16.35f,21,-20,15,0,stone);
            Floor("Courtyard north",-7.15f,16.35f,7.15f,15,0,stone);
            Floor("Courtyard south",-7.15f,16.35f,-20,-7.15f,0,stone);
            Floor("Courtyard kitchen terrace",7.12f,16.35f,2.6f,7.15f,0,stone);
            Floor("Courtyard east terrace",16.2f,16.4f,-7.15f,7.15f,0,stone);
            // Short threshold bridges stop seams between the house and outdoor paving.
            Floor("Kitchen exterior sill",6.8f,7.35f,3.6f,5.6f,.01f,stone);
            WallX(-13,-20,15,0,2.4f,stone);WallX(21,-20,15,0,2.4f,stone);
            WallZ(-20,-13,21,0,2.4f,stone);WallZ(15,-13,21,0,2.4f,stone);
            for(int i=0;i<8;i++)
            {
                float x=i%2==0?-10.4f:18.6f,z=-16+i/2*8;
                Tree(new(x,0,z),i);
            }
            Bench(new(-5.2f,0,11.8f), 1);
            Bench(new(.1f,0,11.8f), 2);
            var fountain = FurnishingRoot("Courtyard fountain", new(5,0,10.6f), structure);
            Box("Courtyard fountain plinth",new(5, .22f,10.6f),new(2.5f,.44f,2.5f),stone,true,fountain);
            for(int i=0;i<4;i++)
            {
                float angle=i*90;
                var p=Box("Fountain stone rim",new Vector3(5,.61f,10.6f)+Quaternion.Euler(0,angle,0)*new Vector3(0,0,1.1f),new(2.4f,.35f,.22f),stone,true,fountain);
                p.transform.rotation=Quaternion.Euler(0,angle,0);
            }
            Box("Still dark fountain water",new(5,.52f,10.6f),new(2.1f,.025f,2.1f),metal,false,fountain);
            foreach(var p in new[]{new Vector3(-10,2.8f,10),new(18.8f,2.8f,10),new(-10,2.8f,-15),new(18.8f,2.8f,-15)})
            {
                Box("Courtyard lamp post",p-Vector3.up*1.4f,new(.09f,2.8f,.09f),metal);
                LightFixture(p,new Color(.64f,.77f,1),35,11);
            }
            Mark("Courtyard Kitchen Terrace",new(9.2f,.05f,4.6f));
            Mark("Courtyard Fountain",new(5,.05f,8.7f));
            Mark("Courtyard South",new(4,.05f,-17));
            Mark("Courtyard West",new(-10,.05f,-5));
        }

        private static void Door(string name,Vector3 hinge,float yaw,float width=1.78f,float height=2.24f)
        {
            var root=new GameObject(name,typeof(Rigidbody),typeof(HingedDoor));root.transform.SetParent(decor,false);
            root.transform.SetPositionAndRotation(hinge,Quaternion.Euler(0,yaw,0));
            var rb=root.GetComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;
            root.GetComponent<HingedDoor>().Configure(96,null);
            GameObject Piece(string n,Vector3 p,Vector3 s,Material m,bool collision=false)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=n;go.transform.SetParent(root.transform,false);
                go.transform.localPosition=p;go.transform.localScale=s;go.GetComponent<Renderer>().sharedMaterial=m;
                if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
            }
            var leaf=Piece("Paneled door leaf",new(width*.5f,height*.5f,0),new(width,height,.065f),oak,true);
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float t in new[]{.23f,.70f})
                {
                    Piece("Door inset panel",new(width*.5f,height*t,side*.045f),new(width-.28f,height*.35f,.033f),timber);
                    foreach(float x in new[]{.13f,width-.13f})
                        Piece("Panel side moulding",new(x,height*t,side*.067f),new(.035f,height*.38f,.025f),oak);
                }
                Piece("Latch plate",new(width-.16f,1.05f,side*.06f),new(.075f,.20f,.025f),brass);
                Piece("Lever handle",new(width-.23f,1.07f,side*.108f),new(.20f,.025f,.035f),brass);
            }
            var obstacle=leaf.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;
            obstacle.carving=true;obstacle.carveOnlyStationary=false;
            var frame= new GameObject(name+" Frame").transform;frame.SetParent(structure);
            frame.SetPositionAndRotation(hinge,Quaternion.Euler(0,yaw,0));
            foreach(float x in new[]{-.075f,width+.075f})
            {
                var post=Box("Door frame upright",Vector3.zero,new(.11f,height+.12f,.18f),oak);
                post.transform.SetParent(frame,false);post.transform.localPosition=new(x,height*.5f,0);
            }
        }
        private static GameObject Beam(string name,Vector3 a,Vector3 b,float size,Material mat)
        {
            var go=Box(name,(a+b)*.5f,new(size,size,Vector3.Distance(a,b)),mat);
            go.transform.rotation=Quaternion.LookRotation(b-a);
            return go;
        }
        private static void Cylinder(string name,Vector3 p,float radius,float height,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(decor,false);
            go.transform.position=p;go.transform.localScale=new(radius*2,height*.5f,radius*2);go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        private static void LightFixture(Vector3 p,Color color,float intensity,float range)
        {
            var lamp=new GameObject("Architectural lamp",typeof(Light));lamp.transform.SetParent(decor);lamp.transform.position=p;
            var light=lamp.GetComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;
            light.shadows=LightShadows.None;
            Box("Lamp housing",p,new(.19f,.13f,.19f),brass,false,decor);
        }
        private static void Table(Vector3 p,float yaw)=>FurnishModel("OakTable","Writing Table",p,yaw,Vector3.one);
        private static GameObject FurnishModel(string asset,string name,Vector3 p,float yaw,Vector3 scale)
        {
            string path=asset=="BedroomNightstand"?"Assets/Art/Props/Bedroom/BedroomNightstand.fbx":Art+asset+".fbx";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)return null;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source,decor);go.name=name;go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));go.transform.localScale=scale;
            // Reuse the existing room prop finishes rather than imported placeholder materials.
            var reference=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include)
                .Where(r=>r.transform.IsChildOf(GameObject.Find("Furniture and Puzzles").transform))
                .GroupBy(r=>r.name).ToDictionary(g=>g.Key,g=>g.First().sharedMaterials);
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())if(reference.TryGetValue(r.name,out var mats))r.sharedMaterials=mats;
            var bounds=new Bounds(p,Vector3.zero);foreach(var r in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            var box=go.AddComponent<BoxCollider>();box.center=go.transform.InverseTransformPoint(bounds.center);
            var size=Quaternion.Inverse(go.transform.rotation)*bounds.size;
            box.size=new(Mathf.Abs(size.x)/scale.x,Mathf.Abs(size.y)/scale.y,Mathf.Abs(size.z)/scale.z);
            if(asset=="Bed")go.transform.Find("Headboard")?.gameObject.SetActive(false);
            return go;
        }
        private static void Bookshelf(Vector3 p,float yaw,float width)
        {
            var root=new GameObject("Bookcase").transform;root.SetParent(decor);root.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            void Part(string n,Vector3 v,Vector3 s,Material m)
            {
                var g=Box(n,Vector3.zero,s,m,n!="Bound volume",decor);g.transform.SetParent(root,false);g.transform.localPosition=v;
            }
            Part("Bookcase back",new(0,1.05f,.19f),new(width,2.1f,.08f),oak);
            for(int k=0;k<5;k++)
            {
                Part("Book shelf",new(0,.10f+k*.49f,0),new(width,.065f,.44f),timber);
                for(int i=0;i<14;i++)
                    Part("Bound volume",new(-width*.44f+i*width*.064f,.28f+k*.49f,-.025f),new(.06f,.24f+(i%3)*.035f,.25f),i%4==0?cloth:i%3==0?oak:timber);
            }
        }
        private static void Bathtub(Vector3 p)
        {
            var ceramic=Load("Ivory Ceramic");
            var root = FurnishingRoot("Bathtub", p, decor);
            Box("Bathtub base",p+Vector3.up*.16f,new(2.35f,.25f,1.12f),ceramic,true,root);
            foreach(float side in new[]{-1f,1f})Box("Bathtub long rim",p+new Vector3(0,.52f,side*.51f),new(2.35f,.53f,.13f),ceramic,true,root);
            foreach(float side in new[]{-1f,1f})Box("Bathtub end rim",p+new Vector3(side*1.10f,.52f,0),new(.15f,.53f,1.12f),ceramic,true,root);
            Box("Tap",p+new Vector3(1.07f,.85f,0),new(.13f,.24f,.09f),brass,false,root);
        }
        private static void Window(Vector3 p,float yaw)
        {
            var root=new GameObject("Recessed window").transform;root.SetParent(decor);root.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            var glass=Box("Dark window glass",Vector3.zero,new(1.80f,1.45f,.07f),metal,false,decor);
            glass.transform.SetParent(root,false);
            foreach(float x in new[]{-.94f,0,.94f})
            {
                var bar=Box("Window mullion",Vector3.zero,new(.065f,1.6f,.11f),oak,false,decor);
                bar.transform.SetParent(root,false);bar.transform.localPosition=new(x,0,-.06f);
            }
        }
        private static void Crate(Vector3 p,Vector3 size,float yaw)
        {
            var root=new GameObject("Braced wooden crate").transform;root.SetParent(decor);root.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            var body=Box("Crate body",Vector3.zero,size,timber,true,decor);body.transform.SetParent(root,false);body.transform.localPosition=Vector3.up*size.y*.5f;
            foreach(float x in new[]{-size.x*.42f,size.x*.42f})
            {
                var brace=Box("Crate strap",Vector3.zero,new(.065f,size.y+.025f,size.z+.025f),oak,false,decor);
                brace.transform.SetParent(root,false);brace.transform.localPosition=new(x,size.y*.5f,0);
            }
        }
        private static void Tree(Vector3 p,int index)
        {
            Cylinder("Bare courtyard tree",p+Vector3.up*1.9f,.17f,3.8f,oak);
            for(int i=0;i<6;i++)
            {
                float angle=(i*137+index*33)*Mathf.Deg2Rad;
                Vector3 a=p+Vector3.up*(1.5f+i*.28f),b=a+new Vector3(Mathf.Cos(angle)*1.3f,1.4f,Mathf.Sin(angle)*1.3f);
                Beam("Bare branch",a,b,.08f,oak);
            }
        }
        private static void Bench(Vector3 p, int number)
        {
            var root = FurnishingRoot($"Courtyard bench {number:00}", p, decor);
            Box("Courtyard bench seat",p+Vector3.up*.48f,new(2.4f,.12f,.58f),timber,true,root).tag = "Glide";
            Box("Courtyard bench back",p+new Vector3(0,.89f,.26f),new(2.4f,.64f,.10f),oak,true,root);
            foreach(float x in new[]{-.85f,.85f})Box("Bench leg",p+new Vector3(x,.23f,0),new(.13f,.46f,.45f),metal,true,root);
        }

        private static Transform FurnishingRoot(string name, Vector3 position, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            return root;
        }
        private static Transform Mark(string name,Vector3 p)
        {
            var go=new GameObject(name).transform;go.SetParent(routes);go.position=p;return go;
        }
        private static void AddRoutes()
        {
            var hunter=Object.FindAnyObjectByType<AnonHunter>();var setup=new SerializedObject(hunter);
            var list=setup.FindProperty("patrol");int old=list.arraySize;
            string[] names={"Ground Floor Landing","Second Floor Landing","Library Interior","Master Bedroom Interior",
                "Attic Landing","Attic Central Aisle","Basement Boiler Interior","Courtyard Kitchen Terrace","Courtyard Fountain","Courtyard South"};
            list.arraySize=old+names.Length;
            for(int i=0;i<names.Length;i++)list.GetArrayElementAtIndex(old+i).objectReferenceValue=routes.Find(names[i]);
            setup.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void FinishExterior()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode required.");
            structure=GameObject.Find("House Expansion Structure").transform;
            decor=GameObject.Find("House Expansion Furnishings").transform;
            oak=Load("Dark Oak");timber=Load("Worn Boards");metal=Load("Oxidized Metal");
            if(decor.Find("Exterior Facade")!=null)return;
            var facade=new GameObject("Exterior Facade").transform;facade.SetParent(decor,false);
            string materialPath=Art+"Night Window Glass.mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(glass==null)
            {
                glass=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                glass.color=new Color(.045f,.075f,.10f);glass.SetFloat("_Metallic",.45f);glass.SetFloat("_Smoothness",.8f);
                glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",new Color(.002f,.004f,.007f));
                AssetDatabase.CreateAsset(glass,materialPath);
            }
            void WindowAt(Vector3 p,float yaw)
            {
                var root=new GameObject("Exterior sash window").transform;root.SetParent(facade);
                root.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
                void Piece(string n,Vector3 v,Vector3 size,Material mat)
                {
                    var part=Box(n,Vector3.zero,size,mat,false,facade);part.transform.SetParent(root,false);part.transform.localPosition=v;
                }
                Piece("Window glass",Vector3.zero,new(1.6f,1.42f,.055f),glass);
                foreach(float x in new[]{-.86f,0,.86f})Piece("Sash stile",new(x,0,.035f),new(.075f,1.57f,.09f),oak);
                foreach(float y in new[]{-.77f,0,.77f})Piece("Sash rail",new(0,y,.035f),new(1.80f,.075f,.09f),oak);
                Piece("Stone sill",new(0,-.83f,.065f),new(1.98f,.13f,.25f),timber);
            }
            foreach(float y in new[]{1.65f,4.95f,8.0f})
                foreach(float x in new[]{-4.6f,5.4f})
                {
                    WindowAt(new(x,y,7.14f),0);WindowAt(new(x,y,-7.14f),180);
                }
            foreach(float y in new[]{1.65f,4.95f,8.0f})
                foreach(float z in new[]{-4.3f,4.3f})WindowAt(new(-7.14f,y,z),-90);
            foreach(float y in new[]{1.65f,4.95f,8.0f})
                foreach(float z in new[]{-4.6f,1.1f})WindowAt(new(16.34f,y,z),90);
            foreach(float y in new[]{.16f,3.18f,6.48f,8.85f})
                foreach(float z in new[]{-7.15f,7.15f})
                    Box("Exterior floor band",new(1.5f,y,z),new(17.4f,.13f,.12f),oak,false,facade);
            foreach(float x in new[]{-7.13f,10.13f})
                foreach(float z in new[]{-7.13f,7.13f})
                    Box("Exterior corner timber",new(x,4.46f,z),new(.16f,8.92f,.16f),oak,false,facade);
            var moon=new GameObject("Courtyard moonlight",typeof(Light));moon.transform.SetParent(facade);
            moon.transform.rotation=Quaternion.Euler(43,-32,0);var light=moon.GetComponent<Light>();
            light.type=LightType.Directional;light.intensity=.24f;light.color=new Color(.55f,.68f,1);
            light.shadows=LightShadows.Soft;light.shadowBias=.035f;light.shadowNormalBias=.12f;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }

        private static void MoveBreakerClear()
        {
            foreach (string name in new[]{"Cellar breaker","Breaker handle"})
            {
                var item=GameObject.Find(name);
                if(item==null)continue;
                var p=item.transform.position;p.z=-12.5f;item.transform.position=p;
            }
        }

        public static void WidenBasementHall()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit Mode required.");
            structure = GameObject.Find("House Expansion Structure").transform;
            decor = GameObject.Find("House Expansion Furnishings").transform;
            foreach (Transform item in structure)
            {
                if (item.name.StartsWith("Basement") && item.name.EndsWith(" Frame") && Mathf.Abs(item.position.x + .4f)<.02f)
                { item.position += Vector3.left*1.1f; continue; }
                if (!item.TryGetComponent<MeshRenderer>(out var r)) continue;
                Bounds b=r.bounds;
                if(b.min.y> -2.9f || b.max.y>-.1f || b.min.z< -14.2f || b.max.z> -6.8f)continue;
                if(Mathf.Abs(b.center.x+.4f)<.02f && b.size.x<.3f)
                    item.position += Vector3.left*1.1f;
                else if(Mathf.Abs(b.max.x+.4f)<.02f && b.size.z<.3f)
                {
                    item.position += Vector3.left*.55f;
                    var scale=item.localScale;scale.x-=1.1f;item.localScale=scale;
                }
            }
            foreach(string name in new[]{"Basement Boiler Room Door","Basement Storage Door"})
                GameObject.Find(name).transform.position += Vector3.left*1.1f;
            Bake();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static void Bake()
        {
            var doors=Object.FindObjectsByType<HingedDoor>();
            var colliders=doors.SelectMany(d=>d.GetComponentsInChildren<Collider>()).ToArray();
            var obstacles=doors.SelectMany(d=>d.GetComponentsInChildren<NavMeshObstacle>()).ToArray();
            foreach(var c in colliders)c.enabled=false;foreach(var o in obstacles)o.enabled=false;
            Physics.SyncTransforms();var surface=Object.FindAnyObjectByType<NavMeshSurface>();surface.BuildNavMesh();
            if(!AssetDatabase.Contains(surface.navMeshData))AssetDatabase.CreateAsset(surface.navMeshData,Art+"ExpandedHouseNavMesh.asset");
            foreach(var c in colliders)c.enabled=true;foreach(var o in obstacles)o.enabled=true;
        }
    }
}
