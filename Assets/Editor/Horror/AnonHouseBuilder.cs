using System;
using System.IO;
using System.Linq;
using DayOfDream.Horror;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Kind = DayOfDream.Horror.HouseObject.Kind;

namespace DayOfDream.Editor
{
    public sealed class AnonHouseBuilder
    {
        public const string Path = "Assets/Scenes/AnonHouse.unity";
        private const string Art = "Assets/Art/Environment/AnonHouse/";
        private const string Player = "Assets/Prefabs/Horror/TomorinExplorer.prefab";
        private const string Textures = "Assets/Art/Environment/HaojieSchool/Textures/";
        private Transform shell, props;
        private Material plaster, wood, brick, darkWood, steel, brass, fabric;

        [MenuItem("Tools/Day Of Dream/Build Anon House")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                    .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes and leave Play Mode first.");
            if (File.Exists(Path)) throw new InvalidOperationException("Anon House already exists.");
            RemoveFlashlights();
            new AnonHouseBuilder().Create();
        }

        private static void RemoveFlashlights()
        {
            void Remove(FirstPersonExplorer explorer)
            {
                var lamp = explorer.GetComponentInChildren<HandheldFlashlight>(true);
                var camera = explorer.GetComponentInChildren<Camera>(true);
                var beam = camera.transform.Find("Flashlight Beam");
                var settings = new SerializedObject(explorer);
                settings.FindProperty("flashlight").objectReferenceValue = null;
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (lamp != null) Object.DestroyImmediate(lamp.gameObject);
                if (beam != null) Object.DestroyImmediate(beam.gameObject);
            }
            var prefab = PrefabUtility.LoadPrefabContents(Player);
            try
            {
                Remove(prefab.GetComponent<FirstPersonExplorer>());
                PrefabUtility.SaveAsPrefabAsset(prefab, Player);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var school = EditorSceneManager.GetSceneByPath(HorrorSchoolBuilder.ScenePath);
            if (!school.isLoaded) school = EditorSceneManager.OpenScene(HorrorSchoolBuilder.ScenePath, OpenSceneMode.Additive);
            var player = school.GetRootGameObjects().Select(root => root.GetComponentInChildren<FirstPersonExplorer>())
                .Single(explorer => explorer != null);
            Remove(player);
            EditorSceneManager.SaveScene(school);
        }

        private void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            shell = new GameObject("House Structure").transform;
            props = new GameObject("Furniture and Puzzles").transform;
            plaster = MaterialAsset("Damp Plaster", new Color(.47f,.40f,.32f), "School_18_PLASTER_water_stained_ash_001");
            wood = MaterialAsset("Worn Boards", new Color(.43f,.32f,.24f), "School_30_WOOD_rotten_boards_001");
            brick = MaterialAsset("Cellar Brick", new Color(.44f,.34f,.31f), "School_02_BRICK_soot_red_001");
            darkWood = MaterialAsset("Dark Oak", new Color(.20f,.116f,.071f));
            steel = MaterialAsset("Oxidized Metal", new Color(.20f,.24f,.24f));
            brass = MaterialAsset("Brass", new Color(.76f,.50f,.18f));
            fabric = MaterialAsset("Wine Fabric", new Color(.24f,.055f,.040f));
            BuildStructure();

            var playerRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
            playerRoot.transform.SetPositionAndRotation(new Vector3(-4.65f,.04f,5.6f), Quaternion.Euler(0,150,0));
            playerRoot.layer = 2;
            var player = playerRoot.GetComponent<FirstPersonExplorer>();
            var hud = player.GetComponentInChildren<HorrorHud>();
            var house = new GameObject("Escape rules", typeof(AudioSource), typeof(AnonHouse)).GetComponent<AnonHouse>();
            var hunter = BuildHunter(player, house);
            var exitLight = BuildPuzzles();
            InstallBedsideCamcorder(player, props);
            DressHouse();
            var sound = house.GetComponent<AudioSource>();
            sound.playOnAwake = false;
            Wire(house, ("player",player), ("hunter",hunter), ("hud",hud), ("exitLamp",exitLight),
                ("audioSource",sound), ("crash", Clip("BottleBreak")), ("creak", Clip("FloorCreak")),
                ("feedback",HudLabel(hud,"Objective",new Vector2(0,-145), 24, new Vector2(.5f,.5f))));
            var config = new SerializedObject(house);
            var boards = config.FindProperty("noisyBoards");
            boards.arraySize = 2;
            boards.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0,.1f,.8f),new Vector3(2.65f,.8f,2));
            boards.GetArrayElementAtIndex(1).boundsValue = new Bounds(new Vector3(8.5f,-1,-2),new Vector3(2.9f,4,3));
            config.ApplyModifiedPropertiesWithoutUndo();
            Wire(player, ("house",house));
            hud.transform.Find("Title").gameObject.SetActive(false);
            hud.transform.Find("Subtitle").gameObject.SetActive(false);
            var hiddenHud = new SerializedObject(hud);
            var hideList = hiddenHud.FindProperty("hideWhileFilming");
            var visible = Enumerable.Range(0,hideList.arraySize).Select(i=>hideList.GetArrayElementAtIndex(i).objectReferenceValue)
                .Where(item=>item != null && item.name is not ("Title" or "Subtitle")).ToArray();
            hideList.arraySize = visible.Length;
            for (int i=0;i<visible.Length;i++) hideList.GetArrayElementAtIndex(i).objectReferenceValue = visible[i];
            hiddenHud.ApplyModifiedPropertiesWithoutUndo();

            var surface = shell.gameObject.AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << 2);
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("No navigation mesh baked.");
            AssetDatabase.CreateAsset(surface.navMeshData, Art + "HouseNavMesh.asset");
            EditorSceneManager.SaveScene(scene, Path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Path,true) }
                .Concat(EditorBuildSettings.scenes.Where(entry=>entry.path!=Path)).ToArray();
            AssetDatabase.SaveAssets();
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,0,0), Quaternion.Euler(38,145,0), 24);
        }

        private void BuildStructure()
        {
            Box("Ground floor",new(0,-.14f,0),new(14.25f,.28f,14.25f),wood);
            Box("Low plaster ceiling",new(0,2.95f,0),new(14.25f,.25f,14.25f),wood);
            WallX(-7,-7,7,0,plaster);
            WallZ(-7,-7,7,0,plaster);
            WallZ(7,-7,7,0,plaster);
            WallX(7,-7,-.3f,0,plaster);
            WallX(7,2,7,0,plaster);
            Box("Cellar threshold lintel",new(7,2.56f,.85f),new(.22f,.55f,2.3f),wood);
            foreach (float x in new[]{-1.43f,1.43f})
            {
                WallX(x,-7,-4.3f,0,plaster);
                WallX(x,-2.55f,2.55f,0,plaster);
                WallX(x,4.3f,7,0,plaster);
                foreach(float z in new[]{-3.425f,3.425f})
                {
                    Box("Door lintel",new(x,2.52f,z),new(.26f,.61f,1.78f),wood);
                    foreach(float edge in new[]{-1f,1f})
                        Box("Door jamb",new(x,1.13f,z+edge*.87f),new(.29f,2.26f,.095f),wood);
                }
            }
            WallZ(0,-7,-6.15f,0,plaster); WallZ(0,-4.25f,-1.43f,0,plaster);
            WallZ(0,1.43f,4.25f,0,plaster); WallZ(0,6.15f,7,0,plaster);
            for(int i=0;i<8;i++) Box("Ceiling joist",new(0,2.76f,-6.2f+i*1.75f),new(13.9f,.14f,.14f),darkWood);

            Box("Cellar floor",new(5.5f,-3.14f,-10.5f),new(9.3f,.28f,7.2f),brick);
            Box("Cellar ceiling",new(5.5f,-.25f,-10.5f),new(9.3f,.22f,7.2f),brick);
            WallX(1,-14,-7,-3,brick); WallX(10,-14,2,-3,brick);
            WallX(10,-7,2,-.2f,plaster); WallX(7,-7,-.3f,-3,brick);
            WallZ(-14,1,10,-3,brick); WallZ(-7,1,7,-3,brick);
            Box("Stair landing",new(8.5f,-.13f,.94f),new(3,.26f,2.22f),wood);
            WallZ(2,7,10,0,plaster);
            var slope = Box("Stair slope",new(8.5f,-1.64f,-3.61f),new(2.99f,.22f,7.64f),wood);
            slope.transform.rotation = Quaternion.Euler(-23.2f,0,0);
            for(int i=0;i<13;i++)
            {
                float z = -.34f-i*.51f;
                float y = (z+.18f)*.428f;
                Box("Timber stair nosing",new(8.5f,y-.055f,z),new(2.94f,.09f,.15f),darkWood,false);
            }
            Box("Stair roof",new(8.5f,2.85f,-2.5f),new(3.15f,.23f,9.1f),wood);
            foreach (float z in new[]{-4.8f,1.7f,5.9f}) Lamp(new Vector3(0,2.5f,z), new Color(1,.66f,.37f), 7, 6);
            Lamp(new(-5.4f,2.45f,5.7f),new Color(.63f,.74f,1),10,6);
            Lamp(new(4.8f,2.45f,4.8f),new Color(1,.73f,.42f),8,5);
            Lamp(new(8.75f,-.35f,-10.2f),new Color(.64f,.78f,1),12,7);
            Lamp(new(8.5f,2.25f,.1f),new Color(1,.6f,.33f),6,6);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.095f,.083f,.081f);
            RenderSettings.reflectionIntensity = .1f;
        }

        private Light BuildPuzzles()
        {
            ConfigureBed(Furniture("Bed", new(-5.7f,0,5.1f),180,new(1.48f,.66f,2.14f)));
            Hide("Bedroom Wardrobe",new(-2.35f,0,5.85f),0);
            Hide("Study Wardrobe",new(-5.9f,0,-6.49f),0);
            Furniture("OakTable",new(4.75f,0,5.9f),180,new(1.66f,.89f,.78f));
            Furniture("OakTable",new(-4.15f,0,-5.8f),0,new(1.66f,.89f,.78f));
            Furniture("OakTable",new(4.7f,-3,-12.2f),0,new(1.66f,.89f,.78f));
            Furniture("OakTable",new(4.6f,0,-5.55f),0,new(1.66f,.89f,.78f));
            var key = Item("Kitchen key", new(4.75f,.96f,5.78f), new(.25f,.12f,.14f), brass,Kind.CabinetKey);
            var master = Item("Front door key",new(-4.15f,1.01f,-5.66f),new(.25f,.12f,.14f),brass,Kind.ExitKey);
            master.SetActive(false);
            var cabinet = Box("Locked cabinet",new(-4.15f,1.18f,-5.79f),new(.74f,.58f,.58f),steel,true,props);
            cabinet.AddComponent<HouseObject>().Configure(Kind.Cabinet,master);
            Box("Iron cabinet latch",new(-4.15f,1.18f,-5.48f),new(.14f,.15f,.055f),brass,false,props).transform.SetParent(cabinet.transform,true);
            Item("Ceramic fuse",new(4.7f,-2.03f,-12.12f),new(.23f,.11f,.14f),brass,Kind.Fuse);
            var panel = Box("Cellar breaker",new(1.22f,-1.52f,-10.3f),new(.24f,.57f,.66f),steel,true,props);
            panel.AddComponent<HouseObject>().Configure(Kind.PowerBox);
            Box("Breaker handle",new(1.37f,-1.53f,-10.3f),new(.07f,.18f,.08f),brass,false,props);
            var exit = Box("Locked front door",new(0,1.18f,6.82f),new(1.62f,2.36f,.19f),darkWood,true,props);
            exit.AddComponent<HouseObject>().Configure(Kind.Exit);
            foreach(float y in new[]{.37f,1.72f})
                Box("Door brace",new(0,y,6.69f),new(1.65f,.18f,.09f),steel,false,props);
            Box("Lock plate",new(.56f,1.07f,6.69f),new(.17f,.29f,.11f),brass,false,props);
            foreach(Vector3 p in new Vector3[]{new(4.4f,.98f,5.83f),new(-4.68f,.98f,-5.66f),new(4.8f,.98f,-5.48f),new(5.16f,-2.02f,-12.07f)})
                Item("Glass bottle",p,new(.13f,.24f,.13f),fabric,Kind.Bottle);
            foreach(float z in new[]{-3.35f,3.35f})
            {
                Box("Runner",new(0,.013f,z),new(1.52f,.024f,2.65f),fabric,false,props);
                Box("Runner border",new(0,.028f,z),new(1.39f,.013f,2.48f),darkWood,false,props);
            }
            var indicator = Lamp(new(.65f,2.09f,6.58f), new Color(1,.15f,.08f),4,2.8f);
            AnonHouseArtPass.ApplyLowLight(shell, null);
            return indicator;
        }

        [MenuItem("Tools/Day Of Dream/Apply Anon House Finishing")]
        public static void FinishScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != Path || scene.isDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the saved Anon House scene in Edit Mode.");
            new AnonHouseBuilder().DressHouse();
            foreach (string name in new[]{"Bedroom Wardrobe","Study Wardrobe"})
            {
                var wardrobe = GameObject.Find(name);
                ConfigureWardrobe(wardrobe, GameObject.Find(name+" inside").transform,
                    GameObject.Find(name+" outside").transform);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private void DressHouse()
        {
            if (GameObject.Find("House Finishing") != null) return;
            var decor = new GameObject("House Finishing").transform;
            var oak = MaterialAsset("Dark Oak",new Color(.20f,.116f,.071f));
            var boards = MaterialAsset("Worn Boards",new Color(.43f,.32f,.24f));
            var metal = MaterialAsset("Oxidized Metal",new Color(.20f,.24f,.24f));
            var cloth = MaterialAsset("Wine Fabric",new Color(.24f,.055f,.040f));
            var trim = MaterialAsset("Brass",new Color(.76f,.50f,.18f));
            void Detail(string name, Vector3 center, Vector3 size, Material mat) => Box(name,center,size,mat,false,decor);
            foreach (float side in new[]{-1f,1f})
            {
                foreach (float z in new[]{-5.7f,-.3f,1.1f,5.7f})
                {
                    Detail("Hall chair rail",new(side*1.29f,.93f,z),new(.08f,.07f,1.54f),oak);
                    for(int i=0;i<5;i++)
                        Detail("Wainscot slat",new(side*1.298f,.52f,z-.68f+i*.34f),new(.064f,.79f,.045f),boards);
                }
            }
            foreach (Vector3 p in new Vector3[]{new(-4.6f,1.7f,6.87f),new(5.1f,1.7f,-6.86f),new(-3.6f,1.7f,-6.86f)})
            {
                Detail("Boarded window frame",p,new(1.7f,1.24f,.095f),oak);
                float front = p.z>0 ? -.078f : .078f;
                Detail("Dark window",p+new Vector3(0,0,front),new(1.46f,1.02f,.08f),metal);
                for(int i=0;i<3;i++)
                {
                    var plank = Box("Window barricade",p+new Vector3(0,-.36f+i*.34f,front*2),new(1.62f,.16f,.07f),boards,false,decor);
                    plank.transform.rotation = Quaternion.Euler(0,0, i%2==0 ? 8 : -12);
                }
                Detail("Window sill",p+new Vector3(0,-.62f,front),new(1.86f,.1f,.29f),oak);
            }
            for(int i=0;i<11;i++)
            {
                var book=Box("Mildewed books",new(-4.83f+i*.076f,1.0f,-6.05f),new(.064f,.19f+(i%3)*.042f,.19f),i%3==0?cloth:oak,false,decor);
                book.transform.rotation=Quaternion.Euler(0,0,(i%3-1)*5);
            }
            for(int i=0;i<4;i++)
            {
                Detail("Cellar shelf",new(9.58f,-2.77f+i*.46f,-11.2f),new(.63f,.065f,2.5f),boards);
                Detail("Shelf post",new(9.42f,-2.09f,-12.3f+i*.73f),new(.08f,1.78f,.085f),metal);
            }
            Detail("Clock casing",new(.81f,1.65f,6.85f),new(.27f,.55f,.15f),oak);
            Detail("Clock face",new(.81f,1.78f,6.76f),new(.16f,.17f,.034f),trim);
        }

        private AnonHunter BuildHunter(FirstPersonExplorer player, AnonHouse house)
        {
            Vector3[] route = { new(0,0,-4),new(4,0,-3.43f),new(5.15f,0,2),new(0,0,3.43f),
                new(-3.7f,0,3.43f),new(-5.23f,0,-2.6f),new(8.4f,0,.8f),new(8.4f,-3,-10.5f) };
            var patrolRoot = new GameObject("Anon patrol").transform;
            var patrol = route.Select((position,i)=>Marker(patrolRoot,$"Stop {i+1}",position,0)).ToArray();
            var root = new GameObject("Anon",typeof(NavMeshAgent),typeof(AnonHunter),typeof(AudioSource));
            root.transform.position = route[0];
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius=.29f; agent.height=1.78f; agent.angularSpeed=450; agent.acceleration=16; agent.stoppingDistance=.3f;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Anon/Anon.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset,root.transform);
            model.name = "Anon_Model";
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var bounds = skin.bounds;
            float scale = 1.77f / bounds.size.y;
            model.transform.localScale *= scale;
            model.transform.localPosition = Vector3.up * (root.transform.position.y-bounds.min.y) * scale;
            foreach (var animator in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
            skin.sharedMaterials = skin.sharedMaterials.Select(EnemyMaterial).ToArray();
            AnonHouseArtPass.LightHunter(skin);
            AnonClub.Attach(root.transform);
            var motion = root.AddComponent<Animation>();
            AnonArmAnimation.Apply(root, motion);
            var footsteps = root.GetComponent<AudioSource>();
            footsteps.playOnAwake=false; footsteps.spatialBlend=1; footsteps.minDistance=1.2f; footsteps.maxDistance=14;
            var hunter = root.GetComponent<AnonHunter>();
            Wire(hunter,("house",house),("player",player),("motion",motion),("footsteps",footsteps),("step",Clip("HunterStep")));
            var serialized = new SerializedObject(hunter);
            var array = serialized.FindProperty("patrol");
            array.arraySize = patrol.Length;
            for (int i=0;i<patrol.Length;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=patrol[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hunter;
        }

        private static Material EnemyMaterial(Material source)
        {
            var path = Art + "Anon_" + source.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(source);
            if (source.name == "OH_Outline_Material")
            {
                material.SetFloat("_AlphaClip",1);
                material.SetFloat("_Cutoff",1.01f);
                material.EnableKeyword("_ALPHATEST_ON");
            }
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        private void Hide(string name, Vector3 position, float yaw)
        {
            var wardrobe = Furniture("Wardrobe",position,yaw,new(1.47f,2.11f,.92f));
            wardrobe.name=name;
            var inside = Marker(props,name+" inside",position,yaw);
            var outside = Marker(props,name+" outside",position,yaw);
            wardrobe.AddComponent<HouseObject>().Configure(Kind.Hide,null,inside,outside);
            ConfigureWardrobe(wardrobe, inside, outside);
        }

        public static void ConfigureWardrobe(GameObject wardrobe, Transform inside, Transform outside)
        {
            if (wardrobe.TryGetComponent<WardrobeDoors>(out _)) return;

            var frame = wardrobe.transform;
            float facing = frame.eulerAngles.y + 180;
            inside.SetPositionAndRotation(frame.TransformPoint(new Vector3(0, .025f, .18f)), Quaternion.Euler(0, facing, 0));
            outside.SetPositionAndRotation(frame.TransformPoint(new Vector3(0, .025f, -.94f)), Quaternion.Euler(0, facing, 0));

            var left = Marker(frame, "Left door hinge", frame.TransformPoint(new Vector3(-.63f, 0, -.34f)), frame.eulerAngles.y);
            var right = Marker(frame, "Right door hinge", frame.TransformPoint(new Vector3(.63f, 0, -.34f)), frame.eulerAngles.y);
            foreach (var part in wardrobe.GetComponentsInChildren<MeshRenderer>())
            {
                if (part.name is not ("Door" or "Door.001" or "Knob" or "Knob.001") &&
                    !part.name.StartsWith("Raised Panel", StringComparison.Ordinal)) continue;
                bool isLeft = frame.InverseTransformPoint(part.bounds.center).x < 0;
                var movingPart = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
                movingPart.transform.SetPositionAndRotation(
                    part.transform.position + frame.right * (isLeft ? -.025f : .025f), part.transform.rotation);
                movingPart.transform.localScale = part.transform.lossyScale;
                movingPart.transform.SetParent(isLeft ? left : right, true);
                movingPart.GetComponent<MeshFilter>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
                movingPart.GetComponent<MeshRenderer>().sharedMaterials = part.sharedMaterials;
                part.enabled = false;
            }
            wardrobe.AddComponent<WardrobeDoors>().Configure(left, right);
        }

        public static void ConfigureBed(GameObject bed)
        {
            if (bed.TryGetComponent<HouseObject>(out _)) return;

            var frame = bed.transform;
            var box = bed.GetComponent<BoxCollider>();
            // The bedroom aisle is on the bed's left. Face it from under the frame and after exiting.
            float yaw = frame.eulerAngles.y - 90;
            var inside = Marker(frame, "Under bed", frame.TransformPoint(new Vector3(-.2f, .02f, 0)), yaw);
            var outside = Marker(frame, "Beside bed",
                frame.TransformPoint(new Vector3(box.center.x - box.size.x * .5f - .42f, .025f, 0)), yaw);
            bed.AddComponent<HouseObject>().Configure(Kind.HideUnderBed, null, inside, outside);
        }

        public static void InstallBedsideCamcorder(FirstPersonExplorer player, Transform furniture)
        {
            const string name = "Bedside Table";
            var table = furniture.Find(name);
            if (table == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "OakTable.fbx");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, furniture);
                model.name = name;
                table = model.transform;
                table.SetPositionAndRotation(new Vector3(-4.12f, 0, 4.50f), Quaternion.Euler(0, 180, 0));
                table.localScale = new Vector3(.57f, .85f, .76f);
                var collider = model.AddComponent<BoxCollider>();
                collider.size = new Vector3(1.66f, .89f, .78f);
                collider.center = new Vector3(0, .445f, 0);
                var references = furniture.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.transform.parent != table && renderer.transform.parent.name == "OakTable")
                    .GroupBy(renderer => renderer.name).ToDictionary(group => group.Key, group => group.First().sharedMaterials);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    if (references.TryGetValue(renderer.name, out var materials)) renderer.sharedMaterials = materials;
            }
            CamcorderSetup.PlacePickup(player, furniture, new Vector3(-4.20f, .801f, 4.53f), 147);
        }

        private GameObject Furniture(string name, Vector3 position, float yaw, Vector3 size)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".fbx"),props);
            model.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m=>m.name switch
                {
                    "Faded Linen" => MaterialAsset("Yellowed Linen",new Color(.43f,.38f,.29f)),
                    "Tarnished Iron" => steel,
                    "Dark Molding" => darkWood,
                    _ => wood
                }).ToArray();
            var box = model.AddComponent<BoxCollider>(); box.size=size; box.center=Vector3.up*size.y*.5f;
            return model;
        }

        private GameObject Item(string name, Vector3 position, Vector3 size, Material material, Kind kind)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name; obj.transform.SetParent(props); obj.transform.position=position;
            obj.transform.localScale=new Vector3(size.x*.56f,size.y*.42f,size.z*.6f);
            obj.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var collider = obj.AddComponent<BoxCollider>(); collider.size = new Vector3(2,2.5f,2); collider.isTrigger=true;
            obj.AddComponent<HouseObject>().Configure(kind);
            AnonHouseProps.Rebuild(obj, kind);
            return obj;
        }

        private void WallX(float x,float from,float to,float y,Material m)
        {
            Box("Wall",new(x,y+1.42f,(from+to)*.5f),new(.22f,2.86f,to-from),m);
            Box("Baseboard",new(x,y+.105f,(from+to)*.5f),new(.29f,.21f,to-from),darkWood,false);
        }
        private void WallZ(float z,float from,float to,float y,Material m)
        {
            Box("Wall",new((from+to)*.5f,y+1.42f,z),new(to-from,2.86f,.22f),m);
            Box("Baseboard",new((from+to)*.5f,y+.105f,z),new(to-from,.21f,.29f),darkWood,false);
        }
        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid=true, Transform parent=null)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name=name; box.transform.SetParent(parent==null?shell:parent); box.transform.position=position;
            box.transform.localScale=scale; box.GetComponent<Renderer>().sharedMaterial=material;
            if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
        private Light Lamp(Vector3 pos, Color color,float intensity,float range)
        {
            Box("Lamp fitting",pos+Vector3.up*.16f,new(.28f,.13f,.23f),steel,false);
            var light=new GameObject("House lamp",typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(shell); light.transform.position=pos;
            light.type=LightType.Point; light.color=color; light.intensity=intensity; light.range=range;
            light.shadows=LightShadows.Hard;
            return light;
        }
        private static Transform Marker(Transform parent,string name,Vector3 pos,float yaw)
        {
            var marker=new GameObject(name).transform;
            marker.SetParent(parent); marker.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0)); return marker;
        }
        private static Material MaterialAsset(string name, Color color, string texture=null)
        {
            string path=Art+name+".mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing!=null)return existing;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")) {color=color};
            mat.SetFloat("_Smoothness",.19f);
            if(texture!=null)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+texture+".png"));
            AssetDatabase.CreateAsset(mat,path); return mat;
        }
        private static TMP_Text HudLabel(HorrorHud hud,string name,Vector2 pos,float size,Vector2 anchor)
        {
            var label=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(hud.transform,false);
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=anchor;
            label.rectTransform.pivot=new Vector2(anchor.x,.5f); label.rectTransform.anchoredPosition=pos;
            label.rectTransform.sizeDelta=new Vector2(880,48);
            label.font=hud.GetComponentInChildren<TMP_Text>(true).font;
            label.alignment=anchor.x==0?TextAlignmentOptions.Left:TextAlignmentOptions.Center;
            label.fontSize=size; label.color=new Color(.89f,.87f,.77f); label.raycastTarget=false;
            return label;
        }
        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/"+name+".wav");
        private static void Wire(Object target, params (string field,Object value)[] references)
        {
            var data=new SerializedObject(target);
            foreach(var entry in references)data.FindProperty(entry.field).objectReferenceValue=entry.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
