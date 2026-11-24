using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 37: the Kitchen, the first room of the Tiny War campaign (Act 1, "The Kitchen Front").
    /// Menu: PaperWings > Build Stage 37 (Tiny War: Kitchen)
    ///
    /// A family kitchen on a Friday night, at paper-plane scale (room 5, 800 east / 800 north):
    ///   - the COUNTER along the back wall: toaster, kettle, sink, cookie jar, cereal-box "bunkers"
    ///     and a sugar-cube tower, with gaps in the upper cabinets above the toaster and kettle
    ///   - the TOASTER: a steady warm column, and every so often TOAST POPS (a big burst of heat)
    ///   - the KETTLE: a column of steam (lifts you, but soaks your paper)
    ///   - the FRIDGE: tall, with a door that swings open and spills cold air down
    ///   - the RADIATOR under the night window: a warm curtain up the west wall
    ///   - the KITCHEN TABLE in the middle with a blanket fort under it (Blue's base) and chairs
    ///   - BISCUIT THE CAT sitting in front of the counter. Every so often she swats: her paw sweeps
    ///     up and a downdraft slams anything flying low in front of her to the floor. Stay high.
    ///
    /// Free Flight, Ring Run and Dogfight work here now. The Dreadnought (boss) and the Tiny War
    /// campaign nights come in later stages. Safe to re-run (rebuilds the Kitchen from scratch).
    /// </summary>
    public static class Stage37Builder
    {
        public static readonly Vector3 Origin = new Vector3(800f, 0f, 800f);
        const float W = 160f, D = 120f, H = 130f;          // half width (x), half depth (z), height
        const float CounterTop = 36f, CounterFront = 93f;  // counter height, z of its front edge

        [MenuItem("PaperWings/Build Stage 37 (Tiny War: Kitchen)", priority = 149)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            if (StageKit.Director() == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No ModeDirector in DeskPrototype. Run the earlier stages first.", "OK");
                return;
            }
            if (StageKit.RoomAt(StageKit.Rooftop) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run Build Stage 25 first (rooms 0-4 must exist before the kitchen takes slot 5).", "OK");
                return;
            }
            StageKit.SetRoom(StageKit.Kitchen, BuildKitchen(scene));
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 37 built: the Kitchen (Tiny War, act 1).\n\n" +
                "- Toaster heat (and TOAST POPS bursts), kettle steam, the radiator under the window.\n" +
                "- The fridge door swings open and spills cold air.\n" +
                "- Biscuit the cat swats anything flying low in front of the counter.\n\n" +
                "Pick KITCHEN with Left/Right on the main menu: Free Flight, Ring Run or Dogfight.", "Into the kitchen");
        }

        static Room BuildKitchen(Scene scene)
        {
            Vector3 o = Origin;
            Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);

            var root = StageKit.FreshRoot(scene, "Kitchen", o);

            // ---- Materials (all named M_K_* so they don't touch the office ones) ----
            var floorTex = BuilderUtil.CheckerTexture(StageKit.Root + "Art/T_KitchenTiles.asset", new Color(0.93f, 0.89f, 0.8f), new Color(0.72f, 0.42f, 0.3f));
            var floor = StageKit.Textured("M_K_Floor", Color.white, floorTex, new Vector2(16f, 12f));
            var tileTex = BuilderUtil.CheckerTexture(StageKit.Root + "Art/T_KitchenSplash.asset", new Color(0.97f, 0.97f, 0.95f), new Color(0.86f, 0.9f, 0.9f));
            var splash = StageKit.Textured("M_K_Backsplash", Color.white, tileTex, new Vector2(40f, 6f));
            var wall = StageKit.Mat("M_K_Wall", new Color(0.95f, 0.88f, 0.66f));
            var ceiling = StageKit.Mat("M_K_Ceiling", new Color(0.96f, 0.95f, 0.92f));
            var cabinet = StageKit.Mat("M_K_Cabinet", new Color(0.55f, 0.72f, 0.66f), 0.3f);
            var counterTop = StageKit.Textured("M_K_Worktop", new Color(0.85f, 0.72f, 0.55f), TextureFactory.WoodGrain(), new Vector2(8f, 1f));
            var handle = StageKit.Mat("M_K_Handle", new Color(0.75f, 0.76f, 0.78f), 0.7f, 0.9f);
            var steel = StageKit.Mat("M_K_Steel", new Color(0.78f, 0.8f, 0.83f), 0.75f, 0.85f);
            var darkSteel = StageKit.Mat("M_K_DarkSteel", new Color(0.18f, 0.18f, 0.2f), 0.5f, 0.6f);
            var white = StageKit.Mat("M_K_Appliance", new Color(0.95f, 0.95f, 0.94f), 0.5f);
            var kettleMat = StageKit.Mat("M_K_Kettle", new Color(0.2f, 0.42f, 0.62f), 0.6f, 0.3f);
            var toast = StageKit.Mat("M_K_Toast", new Color(0.78f, 0.52f, 0.25f));
            var table = StageKit.Textured("M_K_Table", new Color(0.8f, 0.6f, 0.42f), TextureFactory.WoodGrain(), new Vector2(3f, 2f));
            var chair = StageKit.Mat("M_K_Chair", new Color(0.62f, 0.42f, 0.28f));
            var blanket = StageKit.Mat("M_K_Blanket", new Color(0.23f, 0.41f, 0.71f));
            var flagRed = StageKit.Mat("M_K_FlagBlue", new Color(0.23f, 0.41f, 0.71f));
            var cerealA = StageKit.Mat("M_K_CerealRed", new Color(0.8f, 0.25f, 0.18f));
            var cerealB = StageKit.Mat("M_K_CerealYellow", new Color(0.93f, 0.7f, 0.2f));
            var cerealC = StageKit.Mat("M_K_CerealBlue", new Color(0.25f, 0.45f, 0.78f));
            var sugar = StageKit.Mat("M_K_Sugar", new Color(0.98f, 0.98f, 0.98f), 0.4f);
            var jar = StageKit29.Glass("M_K_JarGlass", new Color(0.8f, 0.9f, 0.95f, 0.35f));
            var cookie = StageKit.Mat("M_K_Cookie", new Color(0.72f, 0.5f, 0.28f));
            var cat = StageKit.Mat("M_K_Cat", new Color(0.17f, 0.16f, 0.16f), 0.25f);
            var catPink = StageKit.Mat("M_K_CatNose", new Color(0.9f, 0.55f, 0.6f));
            var catEye = StageKit.Emissive("M_K_CatEye", new Color(0.8f, 0.95f, 0.35f), new Color(1.2f, 1.6f, 0.4f));
            var night = StageKit.Emissive("M_K_NightWindow", new Color(0.15f, 0.2f, 0.38f), new Color(0.08f, 0.12f, 0.3f));
            var frame = StageKit.Mat("M_K_Frame", new Color(0.97f, 0.97f, 0.96f));
            var lamp = StageKit.Emissive("M_K_LampShade", new Color(1f, 0.85f, 0.6f), new Color(1.6f, 1.1f, 0.5f));
            var panel = StageKit.Emissive("M_K_CeilingLight", new Color(1f, 0.97f, 0.9f), new Color(1.3f, 1.25f, 1.1f));
            var underCab = StageKit.Emissive("M_K_UnderCabinet", new Color(1f, 0.92f, 0.75f), new Color(1.2f, 1f, 0.6f));
            var fruitA = StageKit.Mat("M_K_Apple", new Color(0.8f, 0.15f, 0.12f), 0.5f);
            var fruitB = StageKit.Mat("M_K_Orange", new Color(0.95f, 0.55f, 0.12f), 0.4f);
            var door = StageKit.Mat("M_K_Door", new Color(0.88f, 0.84f, 0.76f));

            // ---- Shell ----
            var shell = StageKit.Group(root, "Shell");
            StageKit.Slab("Floor", shell, A(0f, 0f, 0f), W * 2f, D * 2f, floor, true);
            StageKit.Slab("Ceiling", shell, A(0f, H, 0f), W * 2f, D * 2f, ceiling, false);
            StageKit.Wall("WestWall", shell, A(-W, H * 0.5f, 0f), Vector3.right, D * 2f, H, wall);
            StageKit.Wall("EastWall", shell, A(W, H * 0.5f, 0f), Vector3.left, D * 2f, H, wall);
            StageKit.Wall("NorthWall", shell, A(0f, H * 0.5f, D), Vector3.back, W * 2f, H, wall);
            StageKit.Wall("SouthWall", shell, A(0f, H * 0.5f, -D), Vector3.forward, W * 2f, H, wall);
            // Door to the hall (the living room is through here, later).
            StageKit.Box("HallDoor", shell, A(105f, 45f, -D + 0.4f), new Vector3(40f, 90f, 0.6f), door, collider: false);
            StageKit.Box("HallDoorKnob", shell, A(90f, 44f, -D + 1f), new Vector3(2f, 2f, 1f), handle, collider: false);

            // ---- The counter run along the north wall ----
            var counter = StageKit.Group(root, "Counter");
            const float cx0 = -156f, cx1 = 62f;
            float clen = cx1 - cx0, cmid = (cx0 + cx1) * 0.5f;
            StageKit.Box("BaseCabinets", counter, A(cmid, (CounterTop - 2f) * 0.5f, 107f), new Vector3(clen, CounterTop - 2f, 26f), cabinet);
            StageKit.Box("Worktop", counter, A(cmid, CounterTop - 1f, 106f), new Vector3(clen + 2f, 2f, 29f), counterTop);
            for (float x = cx0 + 14f; x < cx1; x += 28f)
                StageKit.Box("DrawerHandle", counter, A(x, CounterTop - 7f, CounterFront + 0.6f), new Vector3(8f, 1f, 1f), handle, collider: false);
            StageKit.Box("Backsplash", counter, A(cmid, CounterTop + 18f, D - 0.3f), new Vector3(clen, 36f, 0.4f), splash, collider: false);
            // Upper cabinets with gaps above the toaster and the kettle (so their air can rise).
            foreach (var (x0, x1) in new[] { (-156f, -116f), (-74f, -40f), (2f, 62f) })
            {
                float w = x1 - x0, mx = (x0 + x1) * 0.5f;
                StageKit.Box("UpperCabinet", counter, A(mx, 90f, 112f), new Vector3(w, 34f, 16f), cabinet);
                StageKit.Box("UpperHandle", counter, A(mx, 76f, 103.6f), new Vector3(6f, 1f, 1f), handle, collider: false);
                StageKit.Box("UnderCabinetLight", counter, A(mx, 72.8f, 112f), new Vector3(w - 4f, 0.4f, 6f), underCab, collider: false);
            }
            // Sink and tap.
            StageKit.Box("Sink", counter, A(-58f, CounterTop + 0.05f, 105f), new Vector3(30f, 0.2f, 18f), darkSteel, collider: false);
            StageKit.Cyl("TapPost", counter, A(-58f, CounterTop + 7f, 116f), new Vector3(2f, 14f, 2f), steel);
            StageKit.Box("TapSpout", counter, A(-58f, CounterTop + 13.5f, 111f), new Vector3(1.6f, 1.6f, 10f), steel, collider: false);

            // ---- Things on the counter ----
            var props = StageKit.Group(root, "CounterProps");
            // Cookie jar (the rations of level 1).
            StageKit.Cyl("CookieJar", props, A(-136f, CounterTop + 10f, 106f), new Vector3(16f, 20f, 16f), jar);
            StageKit.Cyl("JarLid", props, A(-136f, CounterTop + 21f, 106f), new Vector3(17f, 2f, 17f), steel, collider: false);
            for (int i = 0; i < 4; i++)
                StageKit.Cyl("Cookie", props, A(-136f + (i % 2) * 4f - 2f, CounterTop + 3f + i * 3.2f, 106f + (i % 3) - 1f), new Vector3(8f, 1.4f, 8f), cookie, collider: false);
            // Cereal-box bunkers and the sugar-cube tower.
            StageKit.Box("CerealBox_Red", props, A(14f, CounterTop + 20f, 104f), new Vector3(16f, 40f, 6f), cerealA);
            StageKit.Box("CerealBox_Yellow", props, A(32f, CounterTop + 16f, 103f), new Vector3(14f, 32f, 6f), cerealB, euler: new Vector3(0f, -12f, 0f));
            StageKit.Box("CerealBox_Blue", props, A(-112f, CounterTop + 14f, 104f), new Vector3(12f, 28f, 5f), cerealC, euler: new Vector3(0f, 8f, 0f));
            for (int i = 0; i < 5; i++)
                StageKit.Box("SugarCube", props, A(50f + (i % 2) * 0.8f, CounterTop + 2.5f + i * 5f, 104f), new Vector3(5f, 5f, 5f), sugar, euler: new Vector3(0f, i * 17f, 0f));

            // ---- Air ----
            var air = StageKit.Group(root, "Air");
            var events = StageKit.Group(root, "Events");

            // TOASTER: steady warm column through the cabinet gap; TOAST POPS bursts.
            var toasterAt = A(-95f, CounterTop, 106f);
            StageKit.Box("Toaster", props, toasterAt + new Vector3(0f, 7f, 0f), new Vector3(20f, 14f, 12f), steel);
            StageKit.Box("ToasterSlotL", props, toasterAt + new Vector3(-4f, 14.05f, 0f), new Vector3(3f, 0.2f, 9f), darkSteel, collider: false);
            StageKit.Box("ToasterSlotR", props, toasterAt + new Vector3(4f, 14.05f, 0f), new Vector3(3f, 0.2f, 9f), darkSteel, collider: false);
            StageKit.Box("ToasterLever", props, toasterAt + new Vector3(10.6f, 9f, 0f), new Vector3(1.2f, 2f, 3f), darkSteel, collider: false);
            var toasterAir = StageKit.ThermalAsset("Thermal_Toaster", 9f, 10f, 70f, 16f);
            StageKit.Thermal("ToasterHeat", air, toasterAt + new Vector3(0f, 14f, 0f), toasterAir);
            var toastL = StageKit.Box("ToastL", events, toasterAt + new Vector3(-4f, 10f, 0f), new Vector3(2f, 9f, 8f), toast, collider: false);
            var toastR = StageKit.Box("ToastR", events, toasterAt + new Vector3(4f, 10f, 0f), new Vector3(2f, 9f, 8f), toast, collider: false);
            var popHeat = StageKit.Column("ToastPopHeat", events, toasterAt + new Vector3(0f, 14f, 0f), 15f, 95f, 12f, heat: true, strength: 0f);
            popHeat.ramp = 2f;
            popHeat.coreFraction = 0.55f;
            var crumbs = StageKit29.Burst(events, "Crumbs", toasterAt - o + new Vector3(0f, 16f, 0f), 30, 10f, 0.5f);
            crumbs.transform.position = toasterAt + new Vector3(0f, 16f, 0f);
            var pop = StageKit29.Event(events, "ToastPops", "TOAST POPS!", "A blast of heat roars up off the toaster: climb in it!",
                                       new Color(1f, 0.6f, 0.2f), 14f, 2.5f, 6f, 22f);
            pop.air = new AirSource[] { popHeat };
            pop.movers = new[] { toastL.transform, toastR.transform };
            pop.openEuler = new[] { Vector3.zero, Vector3.zero };
            pop.openOffset = new[] { new Vector3(0f, 9f, 0f), new Vector3(0f, 9f, 0f) };
            pop.bursts = new[] { crumbs };
            pop.warnSound = Audio.Sfx.LockBeep;
            pop.activeSound = Audio.Sfx.Pop;
            pop.openTime = 0.12f;

            // KETTLE: steam that lifts you and soaks you (the tea-steam source, bigger).
            var kettleAt = A(-20f, CounterTop, 106f);
            StageKit.Cyl("Kettle", props, kettleAt + new Vector3(0f, 8f, 0f), new Vector3(13f, 16f, 13f), kettleMat);
            StageKit.Cyl("KettleLid", props, kettleAt + new Vector3(0f, 16.5f, 0f), new Vector3(8f, 1.2f, 8f), darkSteel, collider: false);
            StageKit.Box("KettleSpout", props, kettleAt + new Vector3(-7.5f, 11f, 0f), new Vector3(5f, 2.4f, 2.4f), kettleMat, collider: false, euler: new Vector3(0f, 0f, -30f));
            StageKit.Box("KettleHandle", props, kettleAt + new Vector3(7.5f, 12f, 0f), new Vector3(2f, 10f, 3f), darkSteel, collider: false);
            StageKit.Cyl("KettleBase", props, kettleAt + new Vector3(0f, 0.6f, 0f), new Vector3(15f, 1.2f, 15f), darkSteel, collider: false);
            var kettleSteam = Stage2Builder.LoadOrCreate<SteamSettings>(StageKit.Root + "Settings/Steam_Kettle.asset");
            kettleSteam.liftSpeed = 14f;
            kettleSteam.radius = 11f;
            kettleSteam.height = 72f;
            kettleSteam.steamRate = 30f;
            kettleSteam.steamSpread = 0.2f;
            kettleSteam.steamSize = new Vector2(1f, 2.4f);
            kettleSteam.swayAmount = 5f;
            kettleSteam.steamColor = AirPalette.Of(AirLook.Steam, 0.16f);
            EditorUtility.SetDirty(kettleSteam);
            var teaPrefab = StageKit.Load<GameObject>("Prefabs/TeaSteam.prefab");
            if (teaPrefab != null)
            {
                var steamGo = (GameObject)PrefabUtility.InstantiatePrefab(teaPrefab, air);
                PrefabUtility.UnpackPrefabInstance(steamGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                steamGo.name = "KettleSteam";
                steamGo.transform.SetPositionAndRotation(kettleAt + new Vector3(-10f, 13f, 0f), Quaternion.identity);
                steamGo.transform.localScale = Vector3.one;
                // The tea prefab carries a cup model: keep only the steam.
                foreach (var r in steamGo.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(r.gameObject);
                var src = steamGo.GetComponentInChildren<SteamSource>(true);
                if (src != null) BuilderUtil.SetRef(src, "settings", kettleSteam);
            }

            // FRIDGE: tall, against the north wall east of the counter; the door swings open and spills cold air.
            var fridgeX = 100f;
            StageKit.Box("Fridge", props, A(fridgeX, 46f, 105f), new Vector3(46f, 92f, 30f), white);
            StageKit.Box("FridgeTopGrille", props, A(fridgeX, 92.1f, 105f), new Vector3(40f, 0.2f, 24f), darkSteel, collider: false);
            var hinge = new GameObject("FridgeDoorHinge").transform;
            hinge.SetParent(events, false);
            hinge.position = A(fridgeX + 23f, 46f, 89.2f);
            var fdoor = StageKit.Box("FridgeDoor", hinge, A(fridgeX, 46f, 89.2f), new Vector3(45f, 90f, 1.6f), white);
            StageKit.Box("FridgeDoorHandle", hinge, A(fridgeX - 18f, 52f, 88f), new Vector3(1.2f, 22f, 1.2f), handle, collider: false);
            StageKit29.Kinematic(fdoor);
            var spill = StageKit.Beam("FridgeSpill", events, A(fridgeX, 60f, 88f), Quaternion.Euler(55f, 180f, 0f), new Vector2(40f, 50f), 80f, 10f, strength: 0f);
            var fridgeLight = StageKit.PointLight("FridgeLight", events, A(fridgeX, 50f, 84f), new Color(0.75f, 0.9f, 1f), 4f, 55f);
            var mist = StageKit.Emitter(events, "ColdMist", Vector3.zero, new Vector3(40f, 180f, 0f), StageKit.ShimmerMat, false, 2f, 4f);
            mist.transform.position = A(fridgeX, 36f, 86f);
            { var m = mist.main; m.startColor = new Color(0.85f, 0.95f, 1f, 0.35f); m.startSpeed = 5f; m.startLifetime = 4f; m.simulationSpace = ParticleSystemSimulationSpace.World;
              var em = mist.emission; em.rateOverTime = 30f; em.enabled = false;
              var sh = mist.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36f, 60f, 1f); }
            var fridge = StageKit29.Event(events, "FridgeDoor", "FRIDGE DOOR!", "Somebody wants a snack: the door swings out and cold air spills down.",
                                          new Color(0.55f, 0.85f, 1f), 26f, 2f, 6f, 26f);
            fridge.air = new AirSource[] { spill };
            fridge.movers = new[] { hinge };
            fridge.openEuler = new[] { new Vector3(0f, -100f, 0f) };
            fridge.openOffset = new[] { Vector3.zero };
            fridge.eventLights = new[] { fridgeLight };
            fridge.loops = new[] { mist };
            fridge.warnSound = Audio.Sfx.BossClank;
            fridge.activeSound = Audio.Sfx.BossClack;

            // RADIATOR under the night window on the west wall.
            StageKit.Box("WindowFrame", shell, A(-W + 0.6f, 70f, 10f), new Vector3(0.8f, 46f, 70f), frame, collider: false);
            StageKit.Box("NightWindow", shell, A(-W + 1.1f, 70f, 10f), new Vector3(0.4f, 40f, 64f), night, collider: false);
            StageKit.Box("WindowBar", shell, A(-W + 1.3f, 70f, 10f), new Vector3(0.4f, 40f, 1.2f), frame, collider: false);
            StageKit.Box("WindowSill", shell, A(-W + 3f, 49f, 10f), new Vector3(6f, 1.5f, 72f), frame);
            StageKit.Radiator("Radiator_Window", air, A(-W + 1.5f, 0f, 10f), 90f, 60f, StageKit.RadiatorAsset());

            // ---- The kitchen table, chairs and Blue's blanket fort ----
            var dining = StageKit.Group(root, "Table");
            var tableAt = A(10f, 0f, -30f);
            StageKit.Box("TableTop", dining, tableAt + new Vector3(0f, 32f, 0f), new Vector3(92f, 3f, 60f), table);
            foreach (var (dx, dz) in new[] { (-42f, -26f), (42f, -26f), (-42f, 26f), (42f, 26f) })
                StageKit.Box("TableLeg", dining, tableAt + new Vector3(dx, 15.25f, dz), new Vector3(3f, 30.5f, 3f), table);
            foreach (var (dx, dz, yaw) in new[] { (-22f, -46f, 0f), (26f, -46f, 0f), (-22f, 46f, 180f), (60f, 4f, -90f) })
            {
                var c = StageKit.Group(dining, "Chair");
                Vector3 cp = tableAt + new Vector3(dx, 0f, dz);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                StageKit.Box("Seat", c, cp + new Vector3(0f, 19f, 0f), new Vector3(18f, 2f, 18f), chair, euler: new Vector3(0f, yaw, 0f));
                StageKit.Box("Back", c, cp + q * new Vector3(0f, 32f, -8.5f), new Vector3(18f, 24f, 1.6f), chair, euler: new Vector3(0f, yaw, 0f));
                foreach (var (lx, lz) in new[] { (-8f, -8f), (8f, -8f), (-8f, 8f), (8f, 8f) })
                    StageKit.Box("ChairLeg", c, cp + q * new Vector3(lx, 9f, lz), new Vector3(1.4f, 18f, 1.4f), chair, collider: false);
            }
            // The fort: a blanket draped over the east end and north side, open to the south-west.
            StageKit.Box("FortBlanketEast", dining, tableAt + new Vector3(46.5f, 16f, 0f), new Vector3(1.2f, 30f, 62f), blanket);
            StageKit.Box("FortBlanketNorth", dining, tableAt + new Vector3(18f, 16f, 30.5f), new Vector3(58f, 30f, 1.2f), blanket);
            StageKit.Cyl("FortFlagPole", dining, tableAt + new Vector3(40f, 41f, 22f), new Vector3(0.6f, 16f, 0.6f), handle, collider: false);
            StageKit.Box("FortFlag", dining, tableAt + new Vector3(44f, 46f, 22f), new Vector3(8f, 5f, 0.3f), flagRed, collider: false);
            // Fruit bowl and a mug on the table.
            StageKit.Cyl("FruitBowl", dining, tableAt + new Vector3(-14f, 35f, 4f), new Vector3(16f, 3f, 16f), white, collider: false);
            foreach (var (fx, fz, m) in new[] { (-17f, 2f, fruitA), (-11f, 6f, fruitB), (-14f, 8f, fruitA) })
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Fruit";
                Object.DestroyImmediate(f.GetComponent<Collider>());
                f.transform.SetParent(dining, false);
                f.transform.position = tableAt + new Vector3(fx, 39f, fz);
                f.transform.localScale = Vector3.one * 5f;
                f.GetComponent<Renderer>().sharedMaterial = m;
            }
            StageKit.Cyl("Mug", dining, tableAt + new Vector3(24f, 38f, -12f), new Vector3(6f, 7f, 6f), cerealC, collider: false);

            // ---- Biscuit the cat, sitting in front of the counter, facing the room ----
            var catRoot = StageKit.Group(root, "Biscuit");
            Vector3 catAt = A(-40f, 0f, 70f);
            Sphere("Body", catRoot, catAt + new Vector3(0f, 15f, 0f), new Vector3(24f, 30f, 22f), cat, collider: true);
            Sphere("Head", catRoot, catAt + new Vector3(0f, 36f, -6f), new Vector3(18f, 16f, 16f), cat, collider: true);
            StageKit.Box("EarL", catRoot, catAt + new Vector3(-5.5f, 45f, -6f), new Vector3(5f, 7f, 2f), cat, collider: false, euler: new Vector3(0f, 0f, 18f));
            StageKit.Box("EarR", catRoot, catAt + new Vector3(5.5f, 45f, -6f), new Vector3(5f, 7f, 2f), cat, collider: false, euler: new Vector3(0f, 0f, -18f));
            Sphere("EyeL", catRoot, catAt + new Vector3(-4f, 37.5f, -13.6f), new Vector3(3.2f, 2.6f, 1.6f), catEye, collider: false);
            Sphere("EyeR", catRoot, catAt + new Vector3(4f, 37.5f, -13.6f), new Vector3(3.2f, 2.6f, 1.6f), catEye, collider: false);
            StageKit.Box("Nose", catRoot, catAt + new Vector3(0f, 34.5f, -14f), new Vector3(1.6f, 1.2f, 0.8f), catPink, collider: false);
            StageKit.Cyl("Tail", catRoot, catAt + new Vector3(14f, 3f, 4f), new Vector3(3.5f, 14f, 3.5f), cat, collider: false, euler: new Vector3(0f, 30f, 80f));
            Sphere("PawRest", catRoot, catAt + new Vector3(-6f, 2.5f, -10f), new Vector3(6f, 5f, 8f), cat, collider: false);

            // THE SWAT: her right paw sweeps up in front of her and a downdraft slams low flyers down.
            var shoulder = new GameObject("PawHinge").transform;
            shoulder.SetParent(events, false);
            shoulder.position = catAt + new Vector3(7f, 22f, -8f);
            shoulder.localRotation = Quaternion.Euler(-60f, 0f, 0f);          // rest: arm hanging down and forward
            var arm = StageKit.Box("PawArm", shoulder, Vector3.zero, new Vector3(6f, 6f, 26f), cat);
            arm.transform.localPosition = new Vector3(0f, 0f, -13f);
            arm.transform.localRotation = Quaternion.identity;
            var paw = StageKit.Box("Paw", shoulder, Vector3.zero, new Vector3(8f, 7f, 8f), cat);
            paw.transform.localPosition = new Vector3(0f, 0f, -28f);
            paw.transform.localRotation = Quaternion.identity;
            StageKit29.Kinematic(arm);
            StageKit29.Kinematic(paw);
            var swat = StageKit.Beam("SwatDowndraft", events, catAt + new Vector3(0f, 72f, -30f), Quaternion.Euler(90f, 0f, 0f), new Vector2(60f, 44f), 72f, 22f, strength: 0f);
            var eyeLight = StageKit.PointLight("EyeGlow", events, catAt + new Vector3(0f, 38f, -20f), new Color(0.8f, 1f, 0.35f), 3f, 40f);
            var swish = StageKit.Emitter(events, "SwatDust", Vector3.zero, new Vector3(90f, 0f, 0f), StageKit.ShimmerMat, false, 1.5f, 3f);
            swish.transform.position = catAt + new Vector3(0f, 60f, -30f);
            { var m = swish.main; m.startColor = new Color(1f, 1f, 1f, 0.3f); m.startSpeed = 18f; m.startLifetime = 1.2f; m.simulationSpace = ParticleSystemSimulationSpace.World;
              var em = swish.emission; em.rateOverTime = 40f; em.enabled = false;
              var sh = swish.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(50f, 36f, 1f); }
            var swatEvent = StageKit29.Event(events, "CatSwat", "BISCUIT IS WATCHING", "Her paw is coming up: get above the counter edge!",
                                             new Color(0.8f, 1f, 0.35f), 9f, 2f, 1.6f, 14f);
            swatEvent.air = new AirSource[] { swat };
            swatEvent.movers = new[] { shoulder };
            swatEvent.openEuler = new[] { new Vector3(115f, 0f, 0f) };    // swings up to 55 degrees above level
            swatEvent.openOffset = new[] { Vector3.zero };
            swatEvent.eventLights = new[] { eyeLight };
            swatEvent.loops = new[] { swish };
            swatEvent.warnSound = Audio.Sfx.LockBeep;
            swatEvent.activeSound = Audio.Sfx.Bump;
            swatEvent.openTime = 0.18f;
            swatEvent.closeTime = 0.8f;
            swatEvent.restJitter = 8f;

            // ---- Lights ----
            var lights = StageKit.Group(root, "Lights");
            StageKit.Box("CeilingPanel", lights, A(0f, H - 0.4f, 20f), new Vector3(46f, 0.6f, 46f), panel, collider: false);
            StageKit.PointLight("CeilingLight", lights, A(0f, H - 14f, 20f), new Color(1f, 0.95f, 0.85f), 3.4f, 230f, shadows: true);
            StageKit.Cyl("PendantCord", lights, A(10f, (H + 96f) * 0.5f, -30f), new Vector3(0.4f, H - 96f, 0.4f), darkSteel, collider: false);
            StageKit.Cyl("PendantShade", lights, A(10f, 92f, -30f), new Vector3(18f, 8f, 18f), lamp);
            StageKit.PointLight("PendantLight", lights, A(10f, 86f, -30f), new Color(1f, 0.82f, 0.55f), 3f, 100f);
            StageKit.PointLight("UnderCabinetGlow", lights, A(-40f, 66f, 100f), new Color(1f, 0.9f, 0.7f), 2f, 90f);
            StageKit.PointLight("MoonThroughWindow", lights, A(-W + 12f, 70f, 10f), new Color(0.55f, 0.65f, 1f), 1.6f, 80f);

            // ---- Throw points ----
            var spawn = StageKit.Spawn("FridgeSpawn", root, A(fridgeX, 97f, 104f), -150f);          // on top of the fridge
            var tableSpawn = StageKit.Spawn("TableSpawn", root, tableAt + new Vector3(-30f, 40f, -10f), 30f);   // on the table, facing the counter

            // ---- Ring route: over the table, up the radiator, along the counter through both cabinet gaps, under the table ----
            var route = StageKit.Route("KitchenRoute", root, "Kitchen");
            StageKit.Ring(route.transform, "Ring1_OverTheTable", tableAt + new Vector3(0f, 58f, 8f), -110f);
            StageKit.Ring(route.transform, "Ring2_RadiatorLane", A(-140f, 50f, 20f), 0f);
            StageKit.Ring(route.transform, "Ring3_ToasterGap", A(-95f, 88f, 100f), 90f);
            StageKit.Ring(route.transform, "Ring4_KettleGap", A(-20f, 86f, 100f), 90f);
            StageKit.Ring(route.transform, "Ring5_CerealBunkers", A(40f, 70f, 80f), 160f);
            StageKit.Ring(route.transform, "Ring6_UnderTheTable", tableAt + new Vector3(0f, 16f, 0f), -90f);
            route.gameObject.SetActive(false);

            // ---- Rivals (Dogfight): Red Squadron raiders ----
            var combat = StageKit.Group(root, "KitchenCombat");
            StageKit.RivalSpawner("RivalSpawner_1", combat, A(-110f, 92f, -70f), 45f, 2f);
            StageKit.RivalSpawner("RivalSpawner_2", combat, A(120f, 96f, -80f), -45f, 6f);
            combat.gameObject.SetActive(false);

            // ---- Boss slot (The Dreadnought, a later stage) ----
            var boss = StageKit.Group(root, "KitchenBoss");
            boss.gameObject.SetActive(false);

            var room = root.gameObject.AddComponent<Room>();
            room.displayName = "Kitchen";
            room.description = "Toaster heat, kettle steam, a fridge that opens, and a cat who swats low flyers.";
            room.spawnPoint = spawn;
            room.routeGroup = route.gameObject;
            room.combatGroup = combat.gameObject;
            room.extraSpawns = new[] { tableSpawn };
            room.ringRunMedals = new Vector3(70f, 52f, 40f);
            return room;
        }

        /// <summary>A scaled sphere (cat parts), optionally with a collider.</summary>
        static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
