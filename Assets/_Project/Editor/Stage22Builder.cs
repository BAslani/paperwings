using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 22: walls, no landings, a friendlier break room.
    /// Menu: PaperWings > Build Stage 22 (Walls, No Landing)
    ///
    ///  - Landing is gone: the coffee mugs are just props (no landing zone, beam or glow), Ring Run
    ///    ends at the last ring, and the two landing nights became "stay in the air" nights.
    ///  - The office gets walls (with night windows, a door, a whiteboard, filing cabinets, a plant)
    ///    and a ceiling that covers the whole room, so you can't fly off to the break room.
    ///  - The break room is rebuilt: the microwave moves to the middle of the counter with open air
    ///    above it, and the room gets its fourth wall.
    /// Safe to re-run.
    /// </summary>
    public static class Stage22Builder
    {
        const string Root = "Assets/_Project/";
        const string DeskScene = Root + "Scenes/DeskPrototype.unity";
        const float CeilingY = 125f;   // raised from 95 (Stage 5)

        // Office walls: x -170..170, z -150..90 (the desk is at the centre, 70 x 34).
        const float Left = -170f, Right = 170f, Back = 90f, Front = -150f;

        [MenuItem("PaperWings/Build Stage 22 (Walls, No Landing)", priority = 122)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "DeskPrototype scene not found. Run the earlier stages first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            StripMugPrefab();
            UpdateSettings();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(DeskScene);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MugGlow") Object.DestroyImmediate(root);   // the office mug's green glow

            BuildOfficeShell(scene);
            ScatterHeatSources(scene);

            // Rebuild the break room with its new layout, and make sure both rooms are wired.
            var director = Object.FindAnyObjectByType<ModeDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                var rooms = so.FindProperty("rooms");
                Room office = rooms.arraySize > 0 ? rooms.GetArrayElementAtIndex(0).objectReferenceValue as Room : null;
                if (office == null) office = Stage21Builder.MakeOfficeRoom(scene, director);
                var breakRoom = Stage21Builder.BuildBreakRoom(scene);
                if (rooms.arraySize < 2) rooms.arraySize = 2;   // never shrink: rooms 2-4 come from Stage 25
                rooms.GetArrayElementAtIndex(0).objectReferenceValue = office;
                rooms.GetArrayElementAtIndex(1).objectReferenceValue = breakRoom;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 22 built.\n\n" +
                "- No more landing: Ring Run ends at the last ring; landing nights are now 'stay in the air' nights.\n" +
                "- The office has walls and a full ceiling.\n" +
                "- Break room: the microwave is in the middle of the counter with open air above it, and the room is closed.", "Walls up");
        }

        // ------------------------------------------------------------------ landing removal

        /// <summary>The coffee mug becomes a plain prop: no landing zone, target beam, rim markers or confetti.</summary>
        static void StripMugPrefab()
        {
            const string path = Root + "Prefabs/CoffeeMug.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            var zone = root.GetComponent<LandingZone>();
            if (zone != null) Object.DestroyImmediate(zone);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == root.transform) continue;
                if (t.name == "Beam" || t.name == "RimMarker" || t.name == "Confetti") Object.DestroyImmediate(t.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void UpdateSettings()
        {
            // Ceilings are higher now (125): the AC vents need a longer reach to still push down near the desk.
            var vent = AssetDatabase.LoadAssetAtPath<PaperWings.Air.VentSettings>(Root + "Settings/Vent_Ceiling.asset");
            if (vent != null)
            {
                vent.range = 85f;
                EditorUtility.SetDirty(vent);
            }

            // Ring Run is shorter without the landing.
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionSettings>(Root + "Settings/Progression.asset");
            if (progression != null)
            {
                progression.ringRunMedals = new Vector3(60f, 45f, 32f);
                EditorUtility.SetDirty(progression);
            }

            // Campaign: copy the new goals, star times and memos onto the saved nights (matched by title).
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(Root + "Settings/Campaign.asset");
            if (campaign != null && campaign.nights != null)
            {
                var defaults = ScriptableObject.CreateInstance<CampaignSettings>();
                foreach (var night in campaign.nights)
                {
                    var d = System.Array.Find(defaults.nights, n => n.title == night.title);
                    if (d == null) continue;
                    night.memo = d.memo;
                    night.airTimeTarget = d.airTimeTarget;
                    night.timeLimit = d.timeLimit;
                    night.twoStarTime = d.twoStarTime;
                    night.threeStarTime = d.threeStarTime;
                }
                Object.DestroyImmediate(defaults);
                EditorUtility.SetDirty(campaign);
            }
        }

        // ------------------------------------------------------------------ office walls

        static void BuildOfficeShell(Scene scene)
        {
            Stage21Builder.Remove(scene, "OfficeRoom");
            var root = new GameObject("OfficeRoom").transform;

            var wallMat = BuilderUtil.CreateMaterial("M_OfficeWall", new Color(0.46f, 0.48f, 0.54f));
            var trim = BuilderUtil.CreateMaterial("M_OfficeTrim", new Color(0.2f, 0.2f, 0.23f));
            var wood = BuilderUtil.CreateMaterial("M_DoorWood", new Color(0.45f, 0.3f, 0.2f));
            var board = BuilderUtil.CreateMaterial("M_Whiteboard", new Color(0.95f, 0.95f, 0.93f));
            board.SetFloat("_Smoothness", 0.7f);
            var cabinetMat = BuilderUtil.CreateMaterial("M_FilingCabinet", new Color(0.55f, 0.57f, 0.6f));
            cabinetMat.SetFloat("_Metallic", 0.5f);
            var pot = BuilderUtil.CreateMaterial("M_PlantPot", new Color(0.6f, 0.3f, 0.2f));
            var leaves = BuilderUtil.CreateMaterial("M_PlantLeaves", new Color(0.2f, 0.45f, 0.22f));
            var night = BuilderUtil.CreateMaterial("M_NightWindow", new Color(0.04f, 0.06f, 0.14f));
            night.EnableKeyword("_EMISSION");
            night.SetColor("_EmissionColor", new Color(0.06f, 0.1f, 0.25f));
            night.SetFloat("_Smoothness", 0.9f);
            night.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // tells Unity it glows (otherwise it strips the glow on re-save)
            EditorUtility.SetDirty(night);
            var city = BuilderUtil.CreateMaterial("M_CityLights", new Color(1f, 0.85f, 0.5f));
            city.EnableKeyword("_EMISSION");
            city.SetColor("_EmissionColor", new Color(1.6f, 1.2f, 0.6f));
            city.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // tells Unity it glows (otherwise it strips the glow on re-save)
            EditorUtility.SetDirty(city);

            float midZ = (Back + Front) * 0.5f, depth = Back - Front, width = Right - Left, y = CeilingY * 0.5f;

            // ---- Walls ----
            var walls = Group(root, "Walls");
            Stage21Builder.Wall("BackWall", walls, new Vector3(0f, y, Back), Vector3.back, width, CeilingY, wallMat);
            Stage21Builder.Wall("FrontWall", walls, new Vector3(0f, y, Front), Vector3.forward, width, CeilingY, wallMat);
            Stage21Builder.Wall("LeftWall", walls, new Vector3(Left, y, midZ), Vector3.right, depth, CeilingY, wallMat);
            Stage21Builder.Wall("RightWall", walls, new Vector3(Right, y, midZ), Vector3.left, depth, CeilingY, wallMat);

            // Skirting boards (decoration).
            Picture("SkirtingBack", walls, new Vector3(0f, 2f, Back - 0.05f), Vector3.back, width, 4f, trim);
            Picture("SkirtingFront", walls, new Vector3(0f, 2f, Front + 0.05f), Vector3.forward, width, 4f, trim);
            Picture("SkirtingLeft", walls, new Vector3(Left + 0.05f, 2f, midZ), Vector3.right, depth, 4f, trim);
            Picture("SkirtingRight", walls, new Vector3(Right - 0.05f, 2f, midZ), Vector3.left, depth, 4f, trim);

            // ---- Ceiling: stretch the existing one (Stage 5) over the whole room ----
            var ceiling = FindChild(scene, "Stage5", "Ceiling");
            if (ceiling != null)
            {
                ceiling.position = new Vector3(0f, CeilingY, midZ);
                ceiling.localScale = new Vector3(width / 10f + 0.4f, 1f, depth / 10f + 0.4f);   // a Plane is 10 x 10
            }
            // The AC vent hangs from the (now higher) ceiling.
            var vent = FindChild(scene, "Stage5", "CeilingVent");
            if (vent != null) vent.position = new Vector3(vent.position.x, CeilingY - 0.2f, vent.position.z);

            // A bigger room needs a little light near the walls (soft, no shadows).
            foreach (float x in new[] { -115f, 115f })
            {
                var l = new GameObject("WallFillLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = new Vector3(x, CeilingY - 20f, midZ);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.9f, 0.75f);
                l.intensity = 2.5f;
                l.range = 140f;
                l.shadows = LightShadows.None;
            }

            // ---- Back wall: night windows with a few lit office windows across the street ----
            var dressing = Group(root, "Dressing");
            foreach (float x in new[] { -110f, 0f, 110f })
            {
                Picture("WindowFrame", dressing, new Vector3(x, 72f, Back - 0.05f), Vector3.back, 56f, 42f, trim);
                Picture("Window", dressing, new Vector3(x, 72f, Back - 0.1f), Vector3.back, 52f, 38f, night);
                Picture("Mullion", dressing, new Vector3(x, 72f, Back - 0.15f), Vector3.back, 1f, 38f, trim);
                for (int i = 0; i < 5; i++)
                {
                    // Little warm squares: other buildings' lights, pseudo-random but fixed.
                    float lx = x - 22f + ((i * 37 + (int)x) % 44 + 44) % 44;
                    float ly = 58f + (i * 23 % 26);
                    Picture("CityLight", dressing, new Vector3(lx, ly, Back - 0.12f), Vector3.back, 1.6f, 1.2f, city);
                }
            }

            // ---- Left wall: door and a whiteboard ----
            // A 2 m door at the props' scale (~2.3 cm per unit), fixed in Stage 23.
            Picture("DoorFrame", dressing, new Vector3(Left + 0.05f, 44f, Front + 30f), Vector3.right, 38f, 88f, trim);
            Picture("Door", dressing, new Vector3(Left + 0.1f, 43f, Front + 30f), Vector3.right, 34f, 86f, wood);
            Picture("WhiteboardFrame", dressing, new Vector3(Left + 0.05f, 62f, 5f), Vector3.right, 70f, 34f, trim);
            Picture("Whiteboard", dressing, new Vector3(Left + 0.1f, 62f, 5f), Vector3.right, 68f, 32f, board);

            // ---- Right wall: filing cabinets ----
            for (int i = 0; i < 3; i++)   // four-drawer cabinets, taller than the desk (Stage 23)
                Box("FilingCabinet", dressing, new Vector3(Right - 11f, 26f, -52f + i * 22f), new Vector3(20f, 52f, 21f), cabinetMat);

            // ---- Front corner: a potted plant ----
            Stage2Builder.Prim(PrimitiveType.Cylinder, "PlantPot", dressing, Vector3.zero, Vector3.zero, new Vector3(9f, 5f, 9f), pot, keepCollider: true)
                .transform.position = new Vector3(Left + 10f, 5f, Front + 10f);
            Stage2Builder.Prim(PrimitiveType.Sphere, "PlantLeaves", dressing, Vector3.zero, Vector3.zero, new Vector3(16f, 22f, 16f), leaves, keepCollider: true)
                .transform.position = new Vector3(Left + 10f, 20f, Front + 10f);

            // ---- Wall clock on the front wall ----
            Stage2Builder.Prim(PrimitiveType.Cylinder, "WallClock", dressing, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(10f, 0.5f, 10f), board)
                .transform.position = new Vector3(0f, 90f, Front + 0.6f);

            Furnish(Group(root, "Furniture"));
        }

        // ------------------------------------------------------------------ furniture

        /// <summary>
        /// Office furniture, all on the floor or against the walls, away from the desk-top ring route:
        /// a lounge corner (couch, coffee table, rug, floor lamp), a chest of drawers under the windows,
        /// a printer stand, a bookshelf, a coworker's desk and the desk chairs.
        /// </summary>
        static void Furnish(Transform f)
        {
            var wood = BuilderUtil.CreateMaterial("M_DoorWood", new Color(0.45f, 0.3f, 0.2f));
            var darkWood = BuilderUtil.CreateMaterial("M_DarkWood", new Color(0.28f, 0.19f, 0.13f));
            var fabric = BuilderUtil.CreateMaterial("M_OfficeCouch", new Color(0.24f, 0.33f, 0.42f));
            var cushion = BuilderUtil.CreateMaterial("M_Cushion", new Color(0.85f, 0.6f, 0.25f));
            var rug = BuilderUtil.CreateMaterial("M_Rug", new Color(0.5f, 0.22f, 0.2f));
            var metal = BuilderUtil.CreateMaterial("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var plastic = BuilderUtil.CreateMaterial("M_Plastic", new Color(0.2f, 0.21f, 0.24f));
            var lightPlastic = BuilderUtil.CreateMaterial("M_PrinterGrey", new Color(0.78f, 0.78f, 0.76f));
            var knob = BuilderUtil.CreateMaterial("M_BrushedSteel", new Color(0.7f, 0.72f, 0.74f));
            var paper = BuilderUtil.CreateMaterial("M_PaperStack", new Color(0.9f, 0.9f, 0.85f));
            var shade = BuilderUtil.CreateMaterial("M_LampShadeWarm", new Color(1f, 0.9f, 0.7f));
            shade.EnableKeyword("_EMISSION");
            shade.SetColor("_EmissionColor", new Color(0.9f, 0.7f, 0.4f));
            shade.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // tells Unity it glows (otherwise it strips the glow on re-save)
            EditorUtility.SetDirty(shade);
            var screen = BuilderUtil.CreateMaterial("M_SleepScreen", new Color(0.05f, 0.08f, 0.15f));
            screen.EnableKeyword("_EMISSION");
            screen.SetColor("_EmissionColor", new Color(0.05f, 0.12f, 0.3f));
            screen.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // tells Unity it glows (otherwise it strips the glow on re-save)
            EditorUtility.SetDirty(screen);
            Material[] bookColors =
            {
                BuilderUtil.CreateMaterial("M_BookRed", new Color(0.6f, 0.15f, 0.15f)),
                BuilderUtil.CreateMaterial("M_BookBlue", new Color(0.15f, 0.25f, 0.55f)),
                BuilderUtil.CreateMaterial("M_BookGreen", new Color(0.2f, 0.45f, 0.25f)),
                BuilderUtil.CreateMaterial("M_BookYellow", new Color(0.8f, 0.65f, 0.2f)),
            };

            // ---- Lounge corner (front-left): couch against the left wall, coffee table, rug, floor lamp ----
            var lounge = Group(f, "Lounge");
            Rug(lounge, new Vector3(-128f, 0.05f, -80f), 64f, 84f, rug);
            Box("CouchSeat", lounge, new Vector3(-152f, 7f, -80f), new Vector3(18f, 14f, 64f), fabric);
            Box("CouchBack", lounge, new Vector3(-163.5f, 20f, -80f), new Vector3(5f, 26f, 64f), fabric);
            Box("CouchArm", lounge, new Vector3(-152f, 12f, -46f), new Vector3(18f, 10f, 4f), fabric);
            Box("CouchArm", lounge, new Vector3(-152f, 12f, -114f), new Vector3(18f, 10f, 4f), fabric);
            Deco("Cushion", lounge, new Vector3(-158f, 18f, -62f), new Vector3(4f, 10f, 12f), new Vector3(0f, 0f, 12f), cushion);
            Deco("Cushion", lounge, new Vector3(-158f, 18f, -98f), new Vector3(4f, 10f, 12f), new Vector3(0f, 0f, 12f), cushion);
            Box("CoffeeTableTop", lounge, new Vector3(-122f, 10f, -80f), new Vector3(20f, 2f, 36f), darkWood);
            foreach (var (dx, dz) in new[] { (-8f, -15f), (8f, -15f), (-8f, 15f), (8f, 15f) })
                Box("CoffeeTableLeg", lounge, new Vector3(-122f + dx, 4.5f, -80f + dz), new Vector3(1.5f, 9f, 1.5f), darkWood);
            Deco("Magazines", lounge, new Vector3(-121f, 11.4f, -86f), new Vector3(7f, 0.8f, 9f), new Vector3(0f, 18f, 0f), paper);
            Deco("CoffeeCup", lounge, new Vector3(-124f, 12.5f, -72f), new Vector3(2f, 3f, 2f), Vector3.zero, cushion, PrimitiveType.Cylinder);
            // Floor lamp: pole, glowing shade and a warm light over the couch.
            Stage2Builder.Prim(PrimitiveType.Cylinder, "FloorLampPole", lounge, Vector3.zero, Vector3.zero, new Vector3(1.2f, 25f, 1.2f), metal, keepCollider: true)
                .transform.position = new Vector3(-158f, 25f, -36f);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "FloorLampBase", lounge, Vector3.zero, Vector3.zero, new Vector3(8f, 0.6f, 8f), metal, keepCollider: true)
                .transform.position = new Vector3(-158f, 0.6f, -36f);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "FloorLampShade", lounge, Vector3.zero, Vector3.zero, new Vector3(10f, 4f, 10f), shade, keepCollider: true)
                .transform.position = new Vector3(-158f, 52f, -36f);
            var lampLight = new GameObject("FloorLampLight").AddComponent<Light>();
            lampLight.transform.SetParent(lounge, false);
            lampLight.transform.position = new Vector3(-155f, 46f, -40f);
            lampLight.type = LightType.Point;
            lampLight.color = new Color(1f, 0.8f, 0.55f);
            lampLight.intensity = 3f;
            lampLight.range = 60f;

            // ---- Chest of drawers under the middle window, with a printer and binders on top ----
            var drawers = Group(f, "Drawers");
            Box("Chest", drawers, new Vector3(0f, 13f, 82f), new Vector3(72f, 26f, 14f), wood);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    float x = -24f + col * 24f, y = 4.5f + row * 8.2f;
                    Deco("DrawerFront", drawers, new Vector3(x, y, 74.9f), new Vector3(22f, 7.4f, 0.4f), Vector3.zero, darkWood);
                    Deco("DrawerKnob", drawers, new Vector3(x, y, 74.4f), new Vector3(3f, 0.8f, 0.8f), Vector3.zero, knob);
                }
            Box("Printer", drawers, new Vector3(-18f, 31f, 82f), new Vector3(18f, 10f, 12f), lightPlastic);
            Deco("PrinterPaper", drawers, new Vector3(-18f, 36.2f, 84f), new Vector3(12f, 0.6f, 6f), Vector3.zero, paper);
            for (int i = 0; i < 5; i++)
                Box("Binder", drawers, new Vector3(14f + i * 3.2f, 33f, 82f), new Vector3(2.8f, 14f, 10f), bookColors[i % bookColors.Length]);

            // Tall drawer cabinet against the front wall, front-right (moved out of the back-left corner in
            // Stage 24, so the window-radiator route can turn there). Its drawers face into the room (+Z).
            Box("TallDrawers", drawers, new Vector3(133f, 30f, -141f), new Vector3(26f, 60f, 18f), wood);
            for (int row = 0; row < 6; row++)
            {
                float y = 5f + row * 9.6f;
                Deco("DrawerFront", drawers, new Vector3(133f, y, -131.9f), new Vector3(24f, 8.6f, 0.4f), Vector3.zero, darkWood);
                Deco("DrawerKnob", drawers, new Vector3(133f, y, -131.4f), new Vector3(4f, 0.8f, 0.8f), Vector3.zero, knob);
            }

            // ---- Bookshelf against the right wall ----
            var shelf = Group(f, "Bookshelf");
            const float sx = 162f, sz = 45f;
            Box("ShelfBack", shelf, new Vector3(sx + 6f, 40f, sz), new Vector3(2f, 80f, 50f), darkWood);
            Box("ShelfSideA", shelf, new Vector3(sx, 40f, sz - 25f), new Vector3(14f, 80f, 2f), darkWood);
            Box("ShelfSideB", shelf, new Vector3(sx, 40f, sz + 25f), new Vector3(14f, 80f, 2f), darkWood);
            for (int s = 0; s < 5; s++)
            {
                float y = 1f + s * 19.5f;
                Box("Shelf", shelf, new Vector3(sx, y, sz), new Vector3(14f, 2f, 48f), darkWood);
                if (s == 4) continue;
                // A row of books on each shelf, varied heights and colours (fixed pattern).
                float z = sz - 22f;
                for (int b = 0; z < sz + 20f; b++)
                {
                    float thick = 2f + (b * 7 % 3);
                    float h = 11f + (b * 5 % 6);
                    Deco("Book", shelf, new Vector3(sx + 1f, y + 1f + h * 0.5f, z + thick * 0.5f), new Vector3(10f, h, thick), Vector3.zero, bookColors[(b + s) % bookColors.Length]);
                    z += thick + 0.3f;
                    if (b % 7 == 6) z += 4f;   // little gaps
                }
            }

            // ---- A coworker's desk in the front-right, with a sleeping monitor and a chair ----
            var desk2 = Group(f, "CoworkerDesk");
            Box("Desk2Top", desk2, new Vector3(110f, 29f, -100f), new Vector3(60f, 2f, 30f), wood);
            foreach (var (dx, dz) in new[] { (-28f, -13f), (28f, -13f), (-28f, 13f), (28f, 13f) })
                Box("Desk2Leg", desk2, new Vector3(110f + dx, 14f, -100f + dz), new Vector3(2f, 28f, 2f), metal);
            Box("Desk2Monitor", desk2, new Vector3(110f, 39f, -110f), new Vector3(22f, 14f, 1f), plastic);
            Deco("Desk2Screen", desk2, new Vector3(110f, 39f, -109.4f), new Vector3(20f, 12f, 0.2f), Vector3.zero, screen);
            Box("Desk2MonitorNeck", desk2, new Vector3(110f, 32f, -110.5f), new Vector3(2f, 4f, 1f), plastic);
            Box("Desk2Keyboard", desk2, new Vector3(110f, 30.4f, -98f), new Vector3(14f, 0.8f, 4f), plastic);
            Box("Desk2Papers", desk2, new Vector3(126f, 30.6f, -96f), new Vector3(8f, 1.2f, 10f), paper);
            OfficeChair(desk2, new Vector3(110f, 0f, -80f), 180f, fabric, metal);

            // ---- Your desk's chair (pushed back, below the desk top) ----
            OfficeChair(f, new Vector3(0f, 0f, -32f), 0f, fabric, metal);

            // ---- Printer stand in the back-right corner, and a bin by the desk ----
            // In the corner, clear of the right window's radiator (Stage 24).
            Box("PrinterStand", f, new Vector3(152f, 15f, 79f), new Vector3(22f, 30f, 18f), plastic);
            Box("BigPrinter", f, new Vector3(152f, 36f, 79f), new Vector3(20f, 12f, 16f), lightPlastic);
            Deco("BigPrinterTray", f, new Vector3(152f, 42.4f, 76f), new Vector3(14f, 0.8f, 8f), Vector3.zero, paper);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Bin", f, Vector3.zero, Vector3.zero, new Vector3(7f, 6f, 7f), metal, keepCollider: true)
                .transform.position = new Vector3(42f, 6f, -26f);
        }

        /// <summary>A simple swivel chair: five-legged base (as a disc), post, seat and back. Low enough to fly over.</summary>
        static void OfficeChair(Transform parent, Vector3 floorPos, float yaw, Material fabric, Material metal)
        {
            var chair = Group(parent, "OfficeChair");
            chair.position = floorPos;
            chair.rotation = Quaternion.Euler(0f, yaw, 0f);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Base", chair, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(12f, 0.6f, 12f), metal, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Post", chair, new Vector3(0f, 8f, 0f), Vector3.zero, new Vector3(1.6f, 7f, 1.6f), metal, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cube, "Seat", chair, new Vector3(0f, 16f, 0f), Vector3.zero, new Vector3(13f, 2.5f, 13f), fabric, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cube, "Back", chair, new Vector3(0f, 24f, -6f), Vector3.zero, new Vector3(13f, 14f, 2f), fabric, keepCollider: true);
        }

        static void Rug(Transform parent, Vector3 pos, float w, float d, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Rug";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lying flat, facing up
            q.transform.localScale = new Vector3(w, d, 1f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Decoration only (no collider), e.g. drawer fronts and books.</summary>
        static void Deco(string name, Transform parent, Vector3 pos, Vector3 size, Vector3 euler, Material mat, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = Stage2Builder.Prim(type, name, parent, Vector3.zero, euler, size, mat);
            go.transform.position = pos;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A flat decoration on a wall, facing into the room (no collider).</summary>
        static void Picture(string name, Transform parent, Vector3 pos, Vector3 inward, float w, float h, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.LookRotation(-inward);
            q.transform.localScale = new Vector3(w, h, 1f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ------------------------------------------------------------------ heat sources

        static readonly Vector3 TeaTable = new Vector3(70f, 0f, -50f);
        const float TeaTableHeight = 30f;   // same as the desk, so the steam column reaches the same heights

        /// <summary>
        /// A desk-sized laptop (its visible parts only: the thermal keeps its size), and the tea moved
        /// off the desk to its own side table, so the office's heat sources are spread out.
        /// Absolute values, so re-running doesn't keep growing anything.
        /// </summary>
        static void ScatterHeatSources(Scene scene)
        {
            // ---- Laptop: about 4x bigger (was 34 x 24 cm real size, tiny next to the giant desk) ----
            var laptop = FindChild(scene, "Gameplay", "LaptopThermal");
            if (laptop != null)
            {
                Set(laptop, "Deck", new Vector3(0f, 0.3f, 0f), new Vector3(13.6f, 0.6f, 9.6f));
                Set(laptop, "HeatStrip", new Vector3(0f, 0.62f, 2.4f), new Vector3(10.4f, 0.04f, 1.6f));
                Set(laptop, "ScreenHinge", new Vector3(0f, 0.6f, 4.6f), Vector3.one * 4f);   // scales the screen with it
                Set(laptop, "HeatShimmer", new Vector3(0f, 1.2f, 0f), null);                 // just above the bigger deck
            }
            // Make room for it: slide the keyboard and mouse right.
            var keyboard = FindChild(scene, "Props", "Keyboard");
            if (keyboard != null) keyboard.position = new Vector3(4f, keyboard.position.y, keyboard.position.z);
            var mouse = FindChild(scene, "Props", "Mouse");
            if (mouse != null) mouse.position = new Vector3(16f, mouse.position.y, mouse.position.z);

            // ---- Tea: onto a round side table out in the room (front-right of the desk) ----
            var officeRoot = FindRoot(scene, "OfficeRoom");
            var wood = BuilderUtil.CreateMaterial("M_DarkWood", new Color(0.28f, 0.19f, 0.13f));
            var metal = BuilderUtil.CreateMaterial("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var table = Group(officeRoot != null ? officeRoot : null, "TeaTable");
            table.position = TeaTable;
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Top", table, new Vector3(0f, TeaTableHeight - 0.75f, 0f), Vector3.zero, new Vector3(18f, 0.75f, 18f), wood, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Pedestal", table, new Vector3(0f, TeaTableHeight * 0.5f, 0f), Vector3.zero, new Vector3(2.4f, TeaTableHeight * 0.5f, 2.4f), metal, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Foot", table, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(12f, 0.5f, 12f), metal, keepCollider: true);

            var tea = FindChild(scene, "Stage5", "TeaSteam");
            if (tea != null) tea.position = TeaTable + Vector3.up * TeaTableHeight;

            // The last ring of the office route sits over the steam, so it moves with the tea.
            foreach (var route in Object.FindObjectsByType<RaceRoute>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var ring = route.transform.Find("Ring5_SteamTop");
                if (ring != null) ring.SetPositionAndRotation(TeaTable + Vector3.up * (TeaTableHeight + 28f), Quaternion.Euler(0f, 135f, 0f));
            }
        }

        static void Set(Transform parent, string child, Vector3 localPos, Vector3? localScale)
        {
            var t = parent.Find(child);
            if (t == null) return;
            t.localPosition = localPos;
            if (localScale.HasValue) t.localScale = localScale.Value;
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }

        static Transform FindChild(Scene scene, string rootName, string child)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == rootName) return root.transform.Find(child);
            return null;
        }
    }
}
