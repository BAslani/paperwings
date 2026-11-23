using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 21: the Break Room, a second place to fly.
    /// Menu: PaperWings > Build Stage 21 (Break Room)
    ///
    /// Built inside DeskPrototype, 600 units east of the desk (past the fog, so neither room can see
    /// the other). The ModeDirector moves your hand between rooms.
    ///   - Kitchen counter with a microwave (a hot thermal), a mug of tea (steam), a sink under an AC
    ///     vent (downdraft), a coffee machine and the landing mug.
    ///   - Tall fridge with a desk fan on top blowing into the room, a vending machine, a water
    ///     cooler and a table with chairs (your throw spot).
    ///   - Its own five-ring route and two rival spawners: Free Flight, Ring Run and Dogfight work here.
    ///   - The office becomes a Room too; the mode menu gets a room picker (Left/Right).
    ///   - Three "Overtime" campaign nights in the break room are added after The Stapler.
    /// Safe to re-run.
    /// </summary>
    public static class Stage21Builder
    {
        const string Root = "Assets/_Project/";
        const string DeskScene = Root + "Scenes/DeskPrototype.unity";
        static readonly Vector3 Origin = new Vector3(600f, 0f, 0f);
        const float Counter = 30f;     // counter-top height (same as the office desk)
        const float CeilingY = 125f;

        [MenuItem("PaperWings/Build Stage 21 (Break Room)", priority = 121)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "DeskPrototype scene not found. Run the earlier stages first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Settings for the new air source (created before opening the scene).
            var microwave = Stage2Builder.LoadOrCreate<ThermalSettings>(Root + "Settings/Thermal_Microwave.asset");
            microwave.liftSpeed = 11f;
            microwave.radius = 10f;
            microwave.height = 42f;
            microwave.shimmerColor = new Color(1f, 0.75f, 0.45f, 0.12f);
            EditorUtility.SetDirty(microwave);
            AddOvertimeNights();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(DeskScene);
            var director = Object.FindAnyObjectByType<ModeDirector>();
            if (director == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No ModeDirector in DeskPrototype. Run Build Stage 12 first.", "OK");
                return;
            }

            var office = MakeOfficeRoom(scene, director);
            var breakRoom = BuildBreakRoom(scene);

            var so = new SerializedObject(director);
            var rooms = so.FindProperty("rooms");
            rooms.arraySize = 2;
            rooms.GetArrayElementAtIndex(0).objectReferenceValue = office;
            rooms.GetArrayElementAtIndex(1).objectReferenceValue = breakRoom;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = breakRoom.gameObject;
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 21 built: the Break Room.\n\n" +
                "Mode menu: Left/Right picks the room (Office / Break Room). Free Flight, Ring Run and Dogfight work there.\n" +
                "Campaign: three new Overtime nights after The Stapler.\n\n" +
                "To look at it in the Scene view: double-click 'BreakRoom' in the Hierarchy.", "Take a break");
        }

        // ------------------------------------------------------------------ office as a room

        internal static Room MakeOfficeRoom(Scene scene, ModeDirector director)
        {
            Remove(scene, "Room_Office");
            var room = new GameObject("Room_Office").AddComponent<Room>();
            room.displayName = "Office";
            room.description = "The desk: laptop heat, desk fan, tea steam.";

            // Same groups the director used before rooms existed.
            var d = new SerializedObject(director);
            room.routeGroup = d.FindProperty("routeGroup").objectReferenceValue as GameObject;
            room.combatGroup = d.FindProperty("combatGroup").objectReferenceValue as GameObject;
            room.bossGroup = d.FindProperty("bossGroup").objectReferenceValue as GameObject;
            room.squadronGroup = d.FindProperty("squadronGroup").objectReferenceValue as GameObject;

            var launcher = Object.FindAnyObjectByType<LaunchAndReset>();
            if (launcher != null) room.spawnPoint = new SerializedObject(launcher).FindProperty("spawnPoint").objectReferenceValue as Transform;
            return room;
        }

        // ------------------------------------------------------------------ the break room

        internal static Room BuildBreakRoom(Scene scene)
        {
            Remove(scene, "BreakRoom");
            var root = new GameObject("BreakRoom").transform;
            root.position = Origin;

            // Assets AFTER opening the scene.
            var mugPrefab = Load<GameObject>("Prefabs/CoffeeMug.prefab");
            var teaPrefab = Load<GameObject>("Prefabs/TeaSteam.prefab");
            var ventPrefab = Load<GameObject>("Prefabs/CeilingVent.prefab");
            var fanPrefab = Load<GameObject>("Prefabs/DeskFan.prefab");
            var thermalPrefab = Load<GameObject>("Prefabs/LaptopThermal.prefab");
            var ringPrefab = Load<GameObject>("Prefabs/StickyRing.prefab");
            var rivalPrefab = Load<GameObject>("Prefabs/RivalDart.prefab");
            var rivalPilot = Load<RivalPilotSettings>("Settings/Pilot_Rival.asset");
            var microwaveAir = Load<ThermalSettings>("Settings/Thermal_Microwave.asset");

            // ---- Materials ----
            var lino = BuilderUtil.CreateMaterial("M_Lino", Color.white,
                BuilderUtil.CheckerTexture(Root + "Art/T_Lino.asset", new Color(0.86f, 0.84f, 0.78f), new Color(0.62f, 0.66f, 0.68f)), new Vector2(24f, 18f));
            var tile = BuilderUtil.CreateMaterial("M_WallTile", Color.white,
                BuilderUtil.CheckerTexture(Root + "Art/T_WallTile.asset", new Color(0.93f, 0.94f, 0.92f), new Color(0.86f, 0.89f, 0.88f)), new Vector2(40f, 24f));
            var ceiling = BuilderUtil.CreateMaterial("M_BreakCeiling", new Color(0.85f, 0.85f, 0.82f));
            var cabinet = BuilderUtil.CreateMaterial("M_Cabinet", new Color(0.36f, 0.5f, 0.56f));
            var laminate = BuilderUtil.CreateMaterial("M_Laminate", new Color(0.88f, 0.86f, 0.8f));
            var steel = BuilderUtil.CreateMaterial("M_BrushedSteel", new Color(0.7f, 0.72f, 0.74f));
            steel.SetFloat("_Metallic", 0.7f);
            var white = BuilderUtil.CreateMaterial("M_ApplianceWhite", new Color(0.92f, 0.92f, 0.9f));
            var black = BuilderUtil.CreateMaterial("M_ApplianceBlack", new Color(0.08f, 0.08f, 0.09f));
            var red = BuilderUtil.CreateMaterial("M_VendingRed", new Color(0.7f, 0.12f, 0.12f));
            var tableMat = BuilderUtil.CreateMaterial("M_TableTop", new Color(0.78f, 0.72f, 0.6f));
            var chairMat = BuilderUtil.CreateMaterial("M_Chair", new Color(0.2f, 0.32f, 0.5f));
            var water = BuilderUtil.CreateMaterial("M_WaterBottle", new Color(0.45f, 0.7f, 0.95f));
            water.SetFloat("_Smoothness", 0.85f);
            var microGlow = Emissive("M_MicrowaveWindow", new Color(0.3f, 0.2f, 0.1f), new Color(1.6f, 0.9f, 0.35f));
            var vendGlow = Emissive("M_VendingPanel", new Color(0.4f, 0.7f, 0.9f), new Color(0.5f, 1.1f, 1.5f));
            var panelGlow = Emissive("M_FluorescentPanel", new Color(0.95f, 0.97f, 1f), new Color(2.2f, 2.3f, 2.4f));
            var ledGreen = Emissive("M_LedGreen", new Color(0.2f, 1f, 0.3f), new Color(0.3f, 2f, 0.5f));

            // ---- Shell: floor, three walls, ceiling (the front is open for the camera) ----
            var shell = Group(root, "Shell");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(shell, false);
            floor.transform.position = A(0f, 0f, -34f);
            floor.transform.localScale = new Vector3(26.4f, 1f, 15.4f);   // 264 x 154
            floor.GetComponent<Renderer>().sharedMaterial = lino;
            // Four walls: you can't fly out (and the camera can see through them from outside).
            Wall("BackWall", shell, A(0f, CeilingY * 0.5f, 42f), Vector3.back, 264f, CeilingY, tile);
            Wall("LeftWall", shell, A(-131f, CeilingY * 0.5f, -34f), Vector3.right, 154f, CeilingY, tile);
            Wall("RightWall", shell, A(131f, CeilingY * 0.5f, -34f), Vector3.left, 154f, CeilingY, tile);
            Wall("FrontWall", shell, A(0f, CeilingY * 0.5f, -111f), Vector3.forward, 264f, CeilingY, tile);
            Deco(PrimitiveType.Cube, "Door", shell, A(-70f, 20f, -110.9f), Vector3.zero, new Vector3(16f, 40f, 0.2f), cabinet);
            var ceil = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceil.name = "Ceiling";
            ceil.transform.SetParent(shell, false);
            ceil.transform.position = A(0f, CeilingY, -34f);
            ceil.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            ceil.transform.localScale = new Vector3(26.4f, 1f, 15.4f);
            ceil.GetComponent<Renderer>().sharedMaterial = ceiling;
            ceil.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // ---- Kitchen counter along the back wall (x -70..30), top at 30 ----
            var kitchen = Group(root, "Kitchen");
            Box("CounterBase", kitchen, A(-20f, 14.5f, 29f), new Vector3(100f, 29f, 22f), cabinet);
            Box("CounterTop", kitchen, A(-20f, 29.5f, 28.5f), new Vector3(102f, 1f, 23f), laminate);
            // Cabinets only over the sink end, so the microwave has open air above it.
            Box("UpperCabinets", kitchen, A(-55f, 63.5f, 37.5f), new Vector3(30f, 17f, 9f), cabinet);
            Deco(PrimitiveType.Cube, "Sink", kitchen, A(-55f, Counter + 0.06f, 28f), Vector3.zero, new Vector3(14f, 0.1f, 10f), black);
            Box("Faucet", kitchen, A(-55f, Counter + 4f, 35f), new Vector3(1.2f, 8f, 1.2f), steel);
            Box("Spout", kitchen, A(-55f, Counter + 7.5f, 32.5f), new Vector3(1f, 1f, 5f), steel);

            // Microwave in the middle of the counter, straight ahead of the throw: its warm top is a
            // thermal (the laptop's air source without the laptop), with clear air up to the ceiling.
            Box("Microwave", kitchen, A(-15f, Counter + 5f, 29f), new Vector3(16f, 10f, 11f), white);
            Deco(PrimitiveType.Cube, "MicrowaveWindow", kitchen, A(-17f, Counter + 5f, 23.42f), Vector3.zero, new Vector3(9f, 6f, 0.1f), microGlow);
            Deco(PrimitiveType.Cube, "MicrowaveKeypad", kitchen, A(-9.5f, Counter + 5f, 23.42f), Vector3.zero, new Vector3(3f, 7f, 0.1f), black);
            if (thermalPrefab != null)
            {
                var heat = (GameObject)PrefabUtility.InstantiatePrefab(thermalPrefab, kitchen);
                PrefabUtility.UnpackPrefabInstance(heat, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                heat.name = "MicrowaveHeat";
                heat.transform.SetPositionAndRotation(A(-15f, Counter + 10f, 27f), Quaternion.identity);
                foreach (var child in new[] { "Deck", "Screen", "HeatStrip", "ScreenHinge" })
                {
                    var c = heat.transform.Find(child);
                    if (c != null) Object.DestroyImmediate(c.gameObject);
                }
                var source = heat.GetComponent<ThermalSource>();
                if (source != null && microwaveAir != null) BuilderUtil.SetRef(source, "settings", microwaveAir);
            }

            // Tea (steam thermal), coffee machine, a coffee mug (just a prop).
            if (teaPrefab != null)
            {
                var tea = (GameObject)PrefabUtility.InstantiatePrefab(teaPrefab, kitchen);
                tea.transform.SetPositionAndRotation(A(6f, Counter, 26f), Quaternion.Euler(0f, 40f, 0f));
            }
            Box("CoffeeMachine", kitchen, A(22f, Counter + 9f, 33f), new Vector3(12f, 18f, 10f), black);
            Box("CoffeeMachineTray", kitchen, A(22f, Counter + 0.6f, 26.5f), new Vector3(9f, 1.2f, 4f), steel);
            Deco(PrimitiveType.Cube, "CoffeeMachineLed", kitchen, A(25f, Counter + 15f, 27.95f), Vector3.zero, new Vector3(1f, 1f, 0.1f), ledGreen);
            if (mugPrefab != null)
            {
                var mug = (GameObject)PrefabUtility.InstantiatePrefab(mugPrefab, kitchen);
                mug.transform.SetPositionAndRotation(A(-38f, Counter, 26f), Quaternion.Euler(0f, 200f, 0f));
            }

            // AC vent over the sink: a downdraft in front of the cabinets.
            if (ventPrefab != null)
            {
                var vent = (GameObject)PrefabUtility.InstantiatePrefab(ventPrefab, kitchen);
                vent.transform.SetPositionAndRotation(A(-50f, CeilingY - 0.2f, 10f), Quaternion.Euler(90f, 0f, 0f));
            }

            // ---- Fridge with a desk fan on top, blowing out into the room ----
            var fridgeArea = Group(root, "Fridge");
            Box("Fridge", fridgeArea, A(55f, 36f, 29f), new Vector3(22f, 72f, 20f), white);
            Box("FridgeHandle", fridgeArea, A(46f, 40f, 18.6f), new Vector3(1f, 16f, 1f), steel);
            Box("FridgeDoorLine", fridgeArea, A(55f, 50f, 18.95f), new Vector3(21f, 0.3f, 0.2f), steel);
            if (fanPrefab != null)
            {
                var fan = (GameObject)PrefabUtility.InstantiatePrefab(fanPrefab, fridgeArea);
                fan.transform.SetPositionAndRotation(A(52f, 72f, 28f), Quaternion.Euler(0f, 205f, 0f));
                var head = fan.transform.Find("Head");
                if (head != null) head.localRotation = Quaternion.Euler(6f, 0f, 0f);   // slightly down, across the room
            }

            // ---- Room furniture (obstacles) ----
            var furniture = Group(root, "Furniture");
            Box("VendingMachine", furniture, A(-120f, 35f, -30f), new Vector3(16f, 70f, 24f), red);
            Deco(PrimitiveType.Cube, "VendingPanel", furniture, A(-111.9f, 40f, -32f), Vector3.zero, new Vector3(0.1f, 44f, 16f), vendGlow);
            Box("WaterCoolerBase", furniture, A(118f, 15f, -45f), new Vector3(10f, 30f, 10f), white);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "WaterBottle", furniture, Vector3.zero, Vector3.zero, new Vector3(8f, 7f, 8f), water, keepCollider: true).transform.position = A(118f, 37f, -45f);

            Box("TableTop", furniture, A(-5f, 24f, -22f), new Vector3(40f, 2f, 26f), tableMat);
            foreach (var (x, z) in new[] { (-23f, -33f), (13f, -33f), (-23f, -11f), (13f, -11f) })
                Box("TableLeg", furniture, A(x, 11.5f, z), new Vector3(1.6f, 23f, 1.6f), steel);
            foreach (var (x, z, yaw) in new[] { (-14f, -40f, 0f), (4f, -40f, 0f), (-14f, -4f, 180f), (4f, -4f, 180f) })
            {
                var chair = Group(furniture, "Chair");
                chair.position = A(x, 0f, z);
                chair.rotation = Quaternion.Euler(0f, yaw, 0f);
                Stage2Builder.Prim(PrimitiveType.Cube, "Seat", chair, new Vector3(0f, 14f, 0f), Vector3.zero, new Vector3(9f, 1.5f, 9f), chairMat, keepCollider: true);
                Stage2Builder.Prim(PrimitiveType.Cube, "Back", chair, new Vector3(0f, 21f, -4.2f), Vector3.zero, new Vector3(9f, 13f, 1f), chairMat, keepCollider: true);
                Stage2Builder.Prim(PrimitiveType.Cylinder, "Post", chair, new Vector3(0f, 6.5f, 0f), Vector3.zero, new Vector3(1.2f, 6.5f, 1.2f), steel, keepCollider: true);
            }

            // ---- Lights: two fluorescent panels, plus appliance glows ----
            var lights = Group(root, "Lights");
            // A 3 x 2 grid of panels over the bigger room; one point light per pair of panels.
            foreach (float z in new[] { -2f, -70f })
                foreach (float x in new[] { -75f, 0f, 75f })
                    Deco(PrimitiveType.Cube, "FluorescentPanel", lights, A(x, CeilingY - 0.6f, z), Vector3.zero, new Vector3(30f, 1f, 7f), panelGlow);
            foreach (var (x, z) in new[] { (-50f, -2f), (50f, -2f), (-50f, -70f), (50f, -70f) })
                PointLight("CeilingLight", lights, A(x, CeilingY - 10f, z), new Color(0.92f, 0.96f, 1f), 7f, 160f, shadows: x < 0f && z > -10f);

            // A couch along the front wall (more to fly around in the bigger room).
            var couch = BuilderUtil.CreateMaterial("M_Couch", new Color(0.45f, 0.25f, 0.2f));
            Box("CouchSeat", furniture, A(40f, 7f, -100f), new Vector3(50f, 14f, 16f), couch);
            Box("CouchBack", furniture, A(40f, 18f, -107f), new Vector3(50f, 22f, 4f), couch);
            Box("CouchArmL", furniture, A(13f, 12f, -100f), new Vector3(4f, 10f, 16f), couch);
            Box("CouchArmR", furniture, A(67f, 12f, -100f), new Vector3(4f, 10f, 16f), couch);
            PointLight("MicrowaveGlow", lights, A(-15f, Counter + 6f, 20f), new Color(1f, 0.6f, 0.25f), 1.5f, 16f, false);
            PointLight("VendingGlow", lights, A(-106f, 40f, -30f), new Color(0.4f, 0.8f, 1f), 3f, 32f, false);

            // ---- Throw point: the near edge of the table, facing the microwave ----
            var spawn = new GameObject("BreakRoomSpawn").transform;
            spawn.SetParent(root, false);
            spawn.position = A(-20f, 28.5f, -30f);
            spawn.rotation = Quaternion.Euler(0f, 5f, 0f);

            // ---- Ring route: microwave heat, under the cabinets, over the tea, out by the fan, back to the mug ----
            var route = new GameObject("BreakRoomRoute");
            route.transform.SetParent(root, false);
            var raceRoute = route.AddComponent<RaceRoute>();
            PlaneController player = null;
            foreach (var p in Object.FindObjectsByType<PlaneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.GetComponent<PlaneInput>() != null) player = p;
            BuilderUtil.SetRef(raceRoute, "plane", player);
            var rso = new SerializedObject(raceRoute);
            rso.FindProperty("routeKey").stringValue = "BreakRoom";
            rso.ApplyModifiedPropertiesWithoutUndo();
            if (ringPrefab != null)
            {
                Ring(ringPrefab, route.transform, "Ring1_ToMicrowave", A(-17f, 36f, 0f), 5f);
                Ring(ringPrefab, route.transform, "Ring2_MicrowaveTop", A(-15f, 72f, 20f), 90f);
                Ring(ringPrefab, route.transform, "Ring3_OverTea", A(8f, 62f, 18f), 100f);
                Ring(ringPrefab, route.transform, "Ring4_FanStream", A(34f, 60f, -4f), 150f);
                Ring(ringPrefab, route.transform, "Ring5_WaterCooler", A(40f, 42f, -36f), -110f);
            }

            // ---- Rivals (Dogfight here) ----
            var combat = new GameObject("BreakRoomCombat").transform;
            combat.SetParent(root, false);
            var spots = new[] { A(-12f, 62f, 2f), A(35f, 55f, -18f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var go = new GameObject($"RivalSpawner_{i + 1}");
                go.transform.SetParent(combat, false);
                go.transform.position = spots[i];
                go.transform.rotation = Quaternion.Euler(0f, 90f + i * 120f, 0f);
                var spawner = go.AddComponent<EnemySpawner>();
                BuilderUtil.SetRef(spawner, "rivalPrefab", rivalPrefab);
                BuilderUtil.SetRef(spawner, "pilotSettings", rivalPilot);
                var sso = new SerializedObject(spawner);
                sso.FindProperty("firstSpawnDelay").floatValue = 2f + i * 4f;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- The Room ----
            var room = root.gameObject.AddComponent<Room>();
            room.displayName = "Break Room";
            room.description = "Microwave heat, a fan on the fridge, rivals by the vending machine.";
            room.spawnPoint = spawn;
            room.routeGroup = route;
            room.combatGroup = combat.gameObject;
            room.ringRunMedals = new Vector3(55f, 40f, 30f);
            route.SetActive(false);
            combat.gameObject.SetActive(false);
            return room;
        }

        // ------------------------------------------------------------------ campaign

        /// <summary>Appends the three break-room nights to an existing campaign (once).</summary>
        static void AddOvertimeNights()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(Root + "Settings/Campaign.asset");
            if (campaign == null || campaign.nights == null) return;
            foreach (var n in campaign.nights) if (n.room > 0) return;   // already there
            var list = new System.Collections.Generic.List<CampaignNight>(campaign.nights);
            list.AddRange(CampaignSettings.OvertimeNights());
            campaign.nights = list.ToArray();
            EditorUtility.SetDirty(campaign);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Break-room position: x/z around the room centre, y above the floor.</summary>
        static Vector3 A(float x, float y, float z) => Origin + new Vector3(x, y, z);

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + path);

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>A solid box (keeps its collider: you can crash into it).</summary>
        static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// A room wall: a one-sided picture facing into the room plus a solid (invisible) box behind it.
        /// UNITY NOTE: a Quad is only drawn from its front, so if the chase camera swings outside the
        /// room it sees straight through the wall to the plane instead of a blank slab.
        /// </summary>
        internal static void Wall(string name, Transform parent, Vector3 centre, Vector3 inward, float width, float height, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.LookRotation(-inward);   // a Quad's visible face points along -forward

            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(go.transform, false);
            face.transform.localScale = new Vector3(width, height, 1f);
            face.GetComponent<Renderer>().sharedMaterial = mat;
            face.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var solid = go.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0f, 1.5f);   // behind the face (outside the room)
            solid.size = new Vector3(width, height, 3f);
        }

        /// <summary>Decoration only (no collider).</summary>
        static void Deco(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Vector3 euler, Vector3 size, Material mat)
        {
            var go = Stage2Builder.Prim(type, name, parent, Vector3.zero, euler, size, mat);
            go.transform.position = worldPos;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Ring(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw)
        {
            var ring = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            ring.name = name;
            ring.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        static void PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = BuilderUtil.CreateMaterial(name, baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static void Remove(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == rootName) Object.DestroyImmediate(root);
        }
    }
}
