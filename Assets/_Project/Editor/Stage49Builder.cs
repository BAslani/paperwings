using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 49: Tiny War, Act 4, "Sunday Morning". Menu: PaperWings > Stage Builders > Stage 49 (Tiny War: Living Room)
    ///
    /// The living room on Sunday morning (room 8, at 900 / -800). The war is ending: Maya's Red Squadron and
    /// the Blue Wing fly together against the Green Gang, Mum starts tidying, and the two sides build a monster.
    ///   - the FIREPLACE on the south wall: a strong thermal (the room's elevator)
    ///   - the FLOOR LAMP: a gentle warm thermal
    ///   - the SOFA, the bookshelf, the TV unit and Grandad's armchair: cover and things to weave round
    ///   - ROOM EVENTS: MUM'S VACUUM (a canister vacuum Mum pushes across the rug: a wide suction funnel) and THE FRONT DOOR
    ///     (a gust through the room)
    ///   Levels: 10 Ceasefire (squadron battle), 11 The Cleanup (stay in the air while Mum tidies),
    ///   12 THE KAIJU (boss: a cardboard giant whose shockwaves stay low). Needs Stage 48.
    ///   Safe to re-run (rebuilds the living room).
    /// </summary>
    public static class Stage49Builder
    {
        public static readonly Vector3 Origin = new Vector3(900f, 0f, -800f);
        public const int LivingRoom = 8;
        const float W = 150f, D = 110f, H = 130f;
        const string Art = StageKit.Root + "Art/";
        const string Prefix = "M_LR_";
        const string GradePath = Art + "PostFX_LivingRoomLook.asset";

        static Vector3 o;
        static Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);

        static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
        static readonly Color Teal = new Color(0.22f, 0.55f, 0.58f);
        static readonly Color Mustard = new Color(0.88f, 0.68f, 0.24f);
        static readonly Color Brick = new Color(0.66f, 0.34f, 0.26f);
        static readonly Color Ink = new Color(0.17f, 0.17f, 0.17f);

        [MenuItem("PaperWings/Stage Builders/Stage 49 (Tiny War: Living Room)", priority = 160)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report = Run(scene);
            if (report == null) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 49 built: the Living Room (Tiny War, Act 4).\n\n" + report +
                "\n\nMain menu: TINY WAR, levels 10 to 12 (or pick LIVING ROOM with Left/Right for free play).", "Ceasefire");
        }

        sealed class Pal
        {
            public Material floor, wall, ceiling, trim, sofa, cushionA, cushionB, wood, woodDark, rug, brick, screen, metal, lampShade,
                            book1, book2, book3, plant, pot, ink, cardboard, cardboardDark, eye, fuse, tape, fire, curtain, vacuum, vacuumDark, sun, redFlag, blueFlag;
        }

        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            if (StageKit.Director() == null) { EditorUtility.DisplayDialog("PaperWings", "No ModeDirector. Run the earlier stages first.", "OK"); return null; }
            if (AssetDatabase.LoadAssetAtPath<CampaignSettings>(StageKit.Root + "Settings/Campaign_TinyWar.asset") == null)
            { EditorUtility.DisplayDialog("PaperWings", "Run Stages 46 to 48 (Tiny War: Acts 1 to 3) first.", "OK"); return null; }
            o = Origin;
            EditorUtility.DisplayProgressBar("PaperWings", "Tidying the living room...", 0.1f);
            try
            {
                var root = StageKit.FreshRoot(scene, "LivingRoom", o);
                var pal = Palette();
                Shell(root, pal);
                var props = StageKit.Group(root, "Props");
                var air = StageKit.Group(root, "Air");
                Furniture(props, air, pal);
                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.45f);
                var store = new StageLook.MeshStore(Art + "Meshes/RoundedBoxes_LivingRoom.asset");
                int rounded = StageLook.RoundCubes(root, store, Prefix);
                Lights(StageKit.Group(root, "Lights"));
                Grade(root);

                var room = root.gameObject.AddComponent<Room>();
                room.displayName = "Living Room";
                room.description = "A fireplace elevator, Mum's vacuum, a gust from the front door, a sofa to hide behind and a cardboard giant.";
                room.spawnPoint = StageKit.Spawn("SofaSpawn", root, A(-95f, 54f, 88f), 130f);
                room.extraSpawns = new[] { StageKit.Spawn("ArmchairSpawn", root, A(110f, 50f, 70f), -130f) };
                room.combatGroup = Combat(root).gameObject;
                StageKit.SetRoom(LivingRoom, room);
                Squadron(room);
                Events(room, pal);

                EditorUtility.DisplayProgressBar("PaperWings", "Building a monster out of boxes...", 0.8f);
                var boss = Kaiju(root, pal);
                room.bossGroup = boss.transform.parent.gameObject;
                EditorUtility.SetDirty(room);

                var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(StageKit.Root + "Settings/Campaign_TinyWar.asset");
                campaign.nights = CampaignSettings.TinyWar();
                campaign.comingSoon = CampaignSettings.TinyWarComingSoon();
                campaign.completeLine = "THE WAR IS OVER. TEA?";
                EditorUtility.SetDirty(campaign);
                AssetDatabase.SaveAssets();

                return "- The living room: sofa, armchair, bookshelf, TV unit, fireplace thermal, floor lamp, window sun.\n" +
                       "- Room events: Mum's vacuum (suction) and the front door (gust).\n" +
                       $"- {rounded} rounded boxes, its own colour grade.\n" +
                       $"- Tiny War now has {campaign.Count} levels: Ceasefire, The Cleanup, THE KAIJU.";
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static Pal Palette()
        {
            return new Pal
            {
                floor = StageLook.Mat(Prefix + "Floor", Color.white, 0.4f, 0f, FloorTexture(), new Vector2(3.5f, 2.6f)),
                wall = StageLook.Mat(Prefix + "Wall", Color.white, 0.1f, 0f, WallTexture(), new Vector2(0.3f, 0.3f)),
                ceiling = StageLook.Mat(Prefix + "Ceiling", new Color(0.97f, 0.95f, 0.9f), 0.1f),
                trim = StageLook.Mat(Prefix + "Trim", Cream, 0.4f),
                sofa = StageLook.Mat(Prefix + "Sofa", Teal, 0.1f),
                cushionA = StageLook.Mat(Prefix + "CushionMustard", Mustard, 0.1f),
                cushionB = StageLook.Mat(Prefix + "CushionBrick", Brick, 0.1f),
                wood = StageLook.Mat(Prefix + "Wood", new Color(0.72f, 0.52f, 0.34f), 0.35f),
                woodDark = StageLook.Mat(Prefix + "WoodDark", new Color(0.42f, 0.28f, 0.18f), 0.35f),
                rug = StageLook.Mat(Prefix + "Rug", Color.white, 0.05f, 0f, RugTexture(), Vector2.one),
                brick = StageLook.Mat(Prefix + "Brick", Brick, 0.15f),
                screen = StageKit.Emissive(Prefix + "TV", new Color(0.1f, 0.12f, 0.15f), new Color(0.5f, 1.1f, 1.7f)),
                metal = StageLook.Mat(Prefix + "Metal", new Color(0.6f, 0.62f, 0.66f), 0.7f, 0.6f),
                lampShade = StageKit.Emissive(Prefix + "LampShade", new Color(1f, 0.92f, 0.7f), new Color(1.6f, 1.2f, 0.6f)),
                book1 = StageLook.Mat(Prefix + "BookRed", new Color(0.78f, 0.25f, 0.2f), 0.2f),
                book2 = StageLook.Mat(Prefix + "BookBlue", new Color(0.25f, 0.4f, 0.75f), 0.2f),
                book3 = StageLook.Mat(Prefix + "BookGreen", new Color(0.3f, 0.6f, 0.3f), 0.2f),
                plant = StageLook.Mat(Prefix + "Plant", new Color(0.25f, 0.55f, 0.28f), 0.3f),
                pot = StageLook.Mat(Prefix + "Pot", new Color(0.82f, 0.45f, 0.3f), 0.3f),
                ink = StageLook.Mat(Prefix + "Ink", Ink, 0.4f),
                cardboard = StageLook.Mat(Prefix + "Cardboard", new Color(0.74f, 0.58f, 0.38f), 0.1f),
                cardboardDark = StageLook.Mat(Prefix + "CardboardDark", new Color(0.58f, 0.44f, 0.28f), 0.1f),
                eye = StageKit.Emissive(Prefix + "KaijuEye", new Color(1f, 0.8f, 0.3f), new Color(2.4f, 1.8f, 0.6f)),
                fuse = StageKit.Emissive(Prefix + "KaijuFuse", new Color(0.4f, 0.1f, 0.05f), new Color(0.3f, 0.1f, 0.05f)),
                tape = StageLook.Mat(Prefix + "Tape", new Color(0.86f, 0.82f, 0.62f), 0.5f),
                fire = StageKit.Emissive(Prefix + "Fire", new Color(1f, 0.5f, 0.1f), new Color(3f, 1.2f, 0.3f)),
                curtain = StageLook.Mat(Prefix + "Curtain", new Color(0.9f, 0.72f, 0.4f), 0.1f),
                vacuum = StageLook.Mat(Prefix + "Vacuum", new Color(0.8f, 0.22f, 0.2f), 0.5f),
                vacuumDark = StageLook.Mat(Prefix + "VacuumDark", new Color(0.18f, 0.18f, 0.2f), 0.4f),
                sun = StageKit.Emissive(Prefix + "Sunlight", new Color(1f, 0.97f, 0.85f), new Color(2.6f, 2.4f, 1.9f)),
                redFlag = StageLook.Mat(Prefix + "RedFlag", new Color(0.76f, 0.25f, 0.18f), 0.2f),
                blueFlag = StageLook.Mat(Prefix + "BlueWing", new Color(0.23f, 0.41f, 0.71f), 0.2f),
            };
        }

        static Texture2D FloorTexture() => StageLook.Paint(Art + "T_LR_Floor.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            // Herringbone-ish parquet: alternating plank direction in 2x2 blocks.
            float bu = u * 6f, bv = v * 6f;
            bool horiz = (Mathf.FloorToInt(bu) + Mathf.FloorToInt(bv)) % 2 == 0;
            float a = Mathf.Repeat(horiz ? bv * 4f : bu * 4f, 1f);
            float edge = Mathf.Min(StageLook.Smooth(0f, 0.08f, a), StageLook.Smooth(1f, 0.92f, a));
            float tone = 0.85f + 0.15f * Mathf.Sin(Mathf.Floor(bu * 4f) * 3.1f + Mathf.Floor(bv * 4f) * 5.7f);
            return Color.Lerp(new Color(0.25f, 0.16f, 0.1f), new Color(0.7f, 0.5f, 0.32f) * tone, edge);
        });

        static Texture2D WallTexture() => StageLook.Paint(Art + "T_LR_Wall.asset", 128, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            bool stripe = Mathf.Repeat(u * 6f, 1f) < 0.5f;
            return stripe ? new Color(0.93f, 0.88f, 0.74f) : new Color(0.89f, 0.83f, 0.68f);
        });

        static Texture2D RugTexture() => StageLook.Paint(Art + "T_LR_Rug.asset", 128, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            float d = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
            Color[] c = { Brick, Cream, Teal, Mustard, Cream, Brick };
            return c[Mathf.Clamp(Mathf.FloorToInt(d * 6f), 0, 5)];
        });

        // ------------------------------------------------------------------ the room

        static void Shell(Transform root, Pal pal)
        {
            var shell = StageKit.Group(root, "Shell");
            StageKit.Slab("Floor", shell, A(0f, 0f, 0f), W * 2f, D * 2f, pal.floor, true);
            StageKit.Slab("Ceiling", shell, A(0f, H, 0f), W * 2f, D * 2f, pal.ceiling, false);
            StageKit.Wall("WestWall", shell, A(-W, H * 0.5f, 0f), Vector3.right, D * 2f, H, pal.wall);
            StageKit.Wall("EastWall", shell, A(W, H * 0.5f, 0f), Vector3.left, D * 2f, H, pal.wall);
            StageKit.Wall("NorthWall", shell, A(0f, H * 0.5f, D), Vector3.back, W * 2f, H, pal.wall);
            StageKit.Wall("SouthWall", shell, A(0f, H * 0.5f, -D), Vector3.forward, W * 2f, H, pal.wall);
            StageKit.Box("SkirtW", shell, A(-W + 0.8f, 3.5f, 0f), new Vector3(1.6f, 7f, D * 2f), pal.trim, collider: false);
            StageKit.Box("SkirtE", shell, A(W - 0.8f, 3.5f, 0f), new Vector3(1.6f, 7f, D * 2f), pal.trim, collider: false);
            StageKit.Box("SkirtN", shell, A(0f, 3.5f, D - 0.8f), new Vector3(W * 2f, 7f, 1.6f), pal.trim, collider: false);
            StageKit.Box("SkirtS", shell, A(0f, 3.5f, -D + 0.8f), new Vector3(W * 2f, 7f, 1.6f), pal.trim, collider: false);
            StageKit.Box("Rug", shell, A(0f, 0.3f, -5f), new Vector3(170f, 0.6f, 120f), pal.rug, collider: false);
            // Big window on the north wall: Sunday morning sun.
            StageKit.Box("WindowFrame", shell, A(60f, 80f, D - 0.6f), new Vector3(70f, 60f, 1f), pal.trim, collider: false);
            StageKit.Box("WindowSun", shell, A(60f, 80f, D - 1.2f), new Vector3(64f, 54f, 0.4f), pal.sun, collider: false);
            StageKit.Box("WindowBar", shell, A(60f, 80f, D - 1.6f), new Vector3(1.4f, 54f, 0.6f), pal.trim, collider: false);
            StageKit.Box("CurtainL", shell, A(18f, 80f, D - 3f), new Vector3(12f, 74f, 2f), pal.curtain, collider: false);
            StageKit.Box("CurtainR", shell, A(102f, 80f, D - 3f), new Vector3(12f, 74f, 2f), pal.curtain, collider: false);
            // Front door on the east wall (south end).
            StageKit.Box("FrontDoor", shell, A(W - 0.8f, 44f, -62f), new Vector3(1.2f, 88f, 36f), pal.wood, collider: false);
            StageKit.Box("DoorKnob", shell, A(W - 1.9f, 42f, -50f), new Vector3(1.6f, 2.4f, 2.4f), pal.metal, collider: false);
        }

        static void Furniture(Transform props, Transform air, Pal pal)
        {
            // ---- sofa on the north wall ----
            var sofa = StageKit.Group(props, "Sofa");
            StageKit.Box("Seat", sofa, A(-50f, 14f, 84f), new Vector3(100f, 28f, 36f), pal.sofa);
            StageKit.Box("Back", sofa, A(-50f, 46f, 100f), new Vector3(100f, 36f, 10f), pal.sofa);
            StageKit.Box("ArmL", sofa, A(-104f, 28f, 86f), new Vector3(10f, 40f, 36f), pal.sofa);
            StageKit.Box("ArmR", sofa, A(4f, 28f, 86f), new Vector3(10f, 40f, 36f), pal.sofa);
            StageKit.Box("CushionA", sofa, A(-80f, 32f, 84f), new Vector3(24f, 8f, 24f), pal.cushionA);
            StageKit.Box("CushionB", sofa, A(-20f, 32f, 84f), new Vector3(24f, 8f, 24f), pal.cushionB);
            StageKit.Box("Blanket", sofa, A(-50f, 30f, 78f), new Vector3(30f, 3f, 20f), pal.curtain, collider: false);
            // Maya's red flag and Sam's green one on the sofa arms: the two armies' camps.
            StageKit.Cyl("RedPole", sofa, A(-104f, 56f, 86f), new Vector3(0.5f, 20f, 0.5f), pal.ink, collider: false);
            StageKit.Box("RedFlag", sofa, A(-100.5f, 62f, 86f), new Vector3(7f, 4.4f, 0.3f), pal.redFlag, collider: false);
            StageKit.Cyl("BluePole", sofa, A(4f, 56f, 86f), new Vector3(0.5f, 20f, 0.5f), pal.ink, collider: false);
            StageKit.Box("BlueFlag", sofa, A(7.5f, 62f, 86f), new Vector3(7f, 4.4f, 0.3f), pal.blueFlag, collider: false);

            // ---- Grandad's armchair on the east side (he's watching the TV, not the war) ----
            var chair = StageKit.Group(props, "GrandadsArmchair");
            StageKit.Box("Seat", chair, A(112f, 14f, 76f), new Vector3(34f, 28f, 34f), pal.cushionB);
            StageKit.Box("Back", chair, A(112f, 44f, 90f), new Vector3(34f, 36f, 8f), pal.cushionB);
            StageKit.Box("ArmA", chair, A(96f, 24f, 76f), new Vector3(7f, 32f, 34f), pal.cushionB);
            StageKit.Box("ArmB", chair, A(128f, 24f, 76f), new Vector3(7f, 32f, 34f), pal.cushionB);
            StageKit.Box("Grandad", chair, A(112f, 40f, 78f), new Vector3(16f, 26f, 12f), pal.curtain, collider: false);
            StageKit.Box("GrandadHead", chair, A(112f, 58f, 78f), new Vector3(9f, 9f, 9f), pal.cardboard, collider: false);

            // ---- coffee table in the middle ----
            var table = StageKit.Group(props, "CoffeeTable");
            StageKit.Box("Top", table, A(-20f, 17f, 10f), new Vector3(56f, 3f, 28f), pal.wood);
            foreach (var (x, z) in new[] { (-44f, -2f), (4f, -2f), (-44f, 22f), (4f, 22f) })
                StageKit.Box("Leg", table, A(x, 8f, z), new Vector3(3.4f, 16f, 3.4f), pal.woodDark);
            StageKit.Box("Remote", table, A(-8f, 19.4f, 8f), new Vector3(3f, 1.6f, 9f), pal.ink, collider: false);
            StageKit.Box("TeaCup", table, A(-30f, 21f, 12f), new Vector3(5f, 6f, 5f), pal.trim, collider: false);

            // ---- TV unit on the west wall ----
            var tv = StageKit.Group(props, "TVUnit");
            StageKit.Box("Unit", tv, A(-134f, 14f, 0f), new Vector3(26f, 28f, 70f), pal.woodDark);
            StageKit.Box("Set", tv, A(-140f, 44f, 0f), new Vector3(8f, 34f, 62f), pal.ink);
            StageKit.Box("Screen", tv, A(-135.6f, 44f, 0f), new Vector3(0.6f, 30f, 56f), pal.screen, collider: false);

            // ---- bookshelf on the east wall ----
            var shelf = StageKit.Group(props, "Bookshelf");
            StageKit.Box("Frame", shelf, A(142f, 56f, -4f), new Vector3(14f, 112f, 60f), pal.woodDark);
            Material[] books = { pal.book1, pal.book2, pal.book3, pal.cushionA };
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 6; i++)
                    StageKit.Box("Book", shelf, A(134.4f, 14f + row * 26f, -28f + i * 9f), new Vector3(0.8f, 18f, 6.4f), books[(row + i) % 4], collider: false);

            // ---- fireplace on the south wall: the room's elevator ----
            var fp = StageKit.Group(props, "Fireplace");
            StageKit.Box("SurroundL", fp, A(-34f, 35f, -D + 8f), new Vector3(14f, 70f, 16f), pal.brick);
            StageKit.Box("SurroundR", fp, A(34f, 35f, -D + 8f), new Vector3(14f, 70f, 16f), pal.brick);
            StageKit.Box("Lintel", fp, A(0f, 62f, -D + 8f), new Vector3(82f, 16f, 16f), pal.brick);
            StageKit.Box("Mantel", fp, A(0f, 74f, -D + 9f), new Vector3(96f, 4f, 20f), pal.woodDark);
            StageKit.Box("Hearth", fp, A(0f, 1.5f, -D + 14f), new Vector3(90f, 3f, 30f), pal.brick, collider: false);
            StageKit.Box("FireBack", fp, A(0f, 24f, -D + 2f), new Vector3(52f, 46f, 1f), pal.ink, collider: false);
            StageKit.Box("Fire", fp, A(0f, 12f, -D + 6f), new Vector3(26f, 14f, 6f), pal.fire, collider: false);
            StageKit.Box("Clock", fp, A(0f, 84f, -D + 6f), new Vector3(12f, 12f, 6f), pal.trim, collider: false);
            StageKit.PointLight("FireGlow", fp, A(0f, 18f, -D + 22f), new Color(1f, 0.55f, 0.2f), 520f, 110f);
            var hot = StageKit.ThermalAsset("Thermal_Fireplace", 12f, 16f, 100f, 16f);
            StageKit.Thermal("FireplaceHeat", air, A(0f, 22f, -D + 28f), hot);

            // ---- floor lamp near the sofa: a gentle thermal ----
            var lamp = StageKit.Group(props, "FloorLamp");
            StageKit.Cyl("Pole", lamp, A(60f, 30f, 60f), new Vector3(1.6f, 60f, 1.6f), pal.metal);
            StageKit.Cyl("Foot", lamp, A(60f, 1.5f, 60f), new Vector3(12f, 3f, 12f), pal.metal, collider: false);
            StageKit.Cyl("Shade", lamp, A(60f, 68f, 60f), new Vector3(18f, 16f, 18f), pal.lampShade, collider: false);
            StageKit.PointLight("LampGlow", lamp, A(60f, 66f, 60f), new Color(1f, 0.85f, 0.55f), 260f, 90f);
            StageKit.Thermal("LampHeat", air, A(60f, 74f, 60f), StageKit.ThermalAsset("Thermal_FloorLamp", 6f, 9f, 60f, 10f));

            // ---- plant and a toy tower on the rug ----
            StageKit.Cyl("Pot", props, A(-132f, 10f, -86f), new Vector3(16f, 20f, 16f), pal.pot);
            StageKit.Box("Leaves", props, A(-132f, 34f, -86f), new Vector3(24f, 28f, 24f), pal.plant, euler: new Vector3(0f, 25f, 0f));
        }

        // ------------------------------------------------------------------ light and grade

        static void Lights(Transform parent)
        {
            StageKit.PointLight("CeilingLight", parent, A(0f, H - 14f, 0f), new Color(1f, 0.94f, 0.82f), 1500f, 260f, shadows: true);
            var sun = new GameObject("MorningSun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.transform.position = A(60f, 90f, D - 4f);
            sun.transform.LookAt(A(20f, 0f, -20f));
            sun.type = LightType.Spot;
            sun.color = new Color(1f, 0.88f, 0.66f);
            sun.intensity = 11000f;
            sun.range = 300f;
            sun.spotAngle = 65f;
            sun.innerSpotAngle = 35f;
            sun.shadows = LightShadows.Soft;
            StageKit.PointLight("FloorBounce", parent, A(0f, 8f, 0f), new Color(1f, 0.88f, 0.72f), 130f, 160f);
            StageKit.PointLight("CeilingBounce", parent, A(0f, H - 16f, -30f), new Color(0.95f, 0.96f, 1f), 180f, 180f);
        }

        static void Grade(Transform root)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8f);
            color.saturation.Override(12f);
            color.postExposure.Override(0.2f);
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.55f, 0.50f, 0.62f));
            split.highlights.Override(new Color(1f, 0.86f, 0.62f));
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.75f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("LivingRoomGrade");
            go.transform.SetParent(root, false);
            go.layer = 2;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, H * 0.5f + 2f, 0f);
            box.size = new Vector3(W * 2f + 10f, H + 15f, D * 2f + 10f);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 5f;
            volume.blendDistance = 20f;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ modes

        static Transform Combat(Transform root)
        {
            var g = StageKit.Group(root, "LivingRoomCombat");
            StageKit.RivalSpawner("RivalSpawner_1", g, A(100f, 90f, -60f), -120f, 2f);
            StageKit.RivalSpawner("RivalSpawner_2", g, A(-100f, 95f, -70f), 60f, 6f);
            g.gameObject.SetActive(false);
            return g;
        }

        /// <summary>Level 10: you and two wingmen against the Green Gang's squadron.</summary>
        static void Squadron(Room room)
        {
            var g = StageKit29.FreshGroup(room.transform, "LivingRoomSquadron");
            var wingman = StageKit.Load<GameObject>("Prefabs/WingmanDart.prefab");
            var player = StageKit.Player();
            foreach (var (name, offset, hand) in new[] { ("Wingman_Left", new Vector3(-7f, 1f, -6f), new Vector3(-3f, 0.6f, -1.5f)),
                                                         ("Wingman_Right", new Vector3(7f, 1f, -6f), new Vector3(3f, 0.6f, -1.5f)) })
            {
                var go = new GameObject(name);
                go.transform.SetParent(g, false);
                var a = go.AddComponent<AllySpawner>();
                BuilderUtil.SetRef(a, "wingmanPrefab", wingman);
                BuilderUtil.SetRef(a, "leader", player);
                var so = new SerializedObject(a);
                so.FindProperty("formationOffset").vector3Value = offset;
                so.FindProperty("handOffset").vector3Value = hand;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            StageKit.RivalSpawner("SquadronRival_1", g, A(90f, 96f, -80f), -120f, 4f);
            StageKit.RivalSpawner("SquadronRival_2", g, A(-90f, 98f, -80f), 60f, 8f);
            g.gameObject.SetActive(false);
            room.squadronGroup = g.gameObject;
        }

        /// <summary>Mum tidies: her vacuum drags everything toward its nozzle, then the front door lets a gust through.</summary>
        static void Events(Room room, Pal pal)
        {
            var events = StageKit29.FreshGroup(room.transform, "Events");

            // MUM'S VACUUM: a canister cleaner she pushes across the rug. A wide funnel of suction pulls everything
            // in and down toward the floor head; right at the nozzle the pull is brutal. Fly high (or hug the ceiling).
            var vacuum = Vacuum(events, pal);
            // THE FRONT DOOR: it swings open and a gust crosses the room.
            var gust = StageKit.Beam("DoorGust", events, A(W - 6f, 44f, -62f), Quaternion.Euler(0f, -90f, 0f), new Vector2(50f, 70f), 260f, 12f, strength: 0f);
            var door = StageKit29.Event(events, "FrontDoorGust", "FRONT DOOR!", "A gust is blowing in from the hall: it'll push you across the room.",
                                        new Color(0.7f, 0.9f, 1f), 24f, 2.5f, 6f, 24f);
            door.air = new AirSource[] { gust };
            door.warnSound = Audio.Sfx.LockBeep;
            door.activeSound = Audio.Sfx.BossClack;
        }

        const float ModelScale = 0.45f;

        static GameObject Ball(string name, Transform parent, Vector3 local, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A cylinder from point a to point b (local to parent): always connected, whatever the angle.</summary>
        static GameObject Between(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var go = StageKit.Cyl(name, parent, Vector3.zero, new Vector3(radius * 2f, (b - a).magnitude, radius * 2f), mat, collider: false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return go;
        }

        /// <summary>
        /// The cleaner, built like a toy: a glossy red capsule of a canister on two big wheels with chrome hubcaps,
        /// a cream lid with a badge and a status light, a smooth ribbed hose looping up to a grip, a chrome wand and a
        /// rounded floor head with headlights and a brush. Front is +z. Returns the event that runs it.
        /// </summary>
        static RoomEvent Vacuum(Transform events, Pal pal)
        {
            var vac = StageKit.Group(events, "Vacuum");
            vac.position = A(-10f, 0f, -68f);
            // The model is built at the old size and shown at 45%: the air around it stays big.
            var model = new GameObject("Model").transform;
            model.SetParent(vac, false);
            model.localScale = Vector3.one * ModelScale;

            var red = StageLook.Mat(Prefix + "VacRed", new Color(0.86f, 0.18f, 0.16f), 0.85f, 0.05f);
            var redDeep = StageLook.Mat(Prefix + "VacRedDeep", new Color(0.55f, 0.10f, 0.10f), 0.7f);
            var cream = StageLook.Mat(Prefix + "VacCream", new Color(0.97f, 0.93f, 0.82f), 0.8f);
            var chrome = StageLook.Mat(Prefix + "VacChrome", new Color(0.86f, 0.88f, 0.92f), 0.95f, 1f);
            var tyre = StageLook.Mat(Prefix + "VacTyre", new Color(0.10f, 0.10f, 0.11f), 0.3f);
            var hoseA = StageLook.Mat(Prefix + "VacHoseDark", new Color(0.22f, 0.24f, 0.30f), 0.5f);
            var hoseB = StageLook.Mat(Prefix + "VacHoseLight", new Color(0.46f, 0.50f, 0.60f), 0.5f);
            var bristle = StageLook.Mat(Prefix + "VacBristle", new Color(0.35f, 0.27f, 0.18f), 0.2f);
            var led = StageKit.Emissive(Prefix + "VacLed", new Color(0.3f, 1f, 0.5f), new Color(0.6f, 3f, 1f));
            var lamp = StageKit.Emissive(Prefix + "VacHeadlight", new Color(1f, 0.95f, 0.7f), new Color(3f, 2.6f, 1.2f));

            // ---- canister: a fat red capsule with a cream lid ----
            Ball("Body", model, new Vector3(0f, 14f, -26f), new Vector3(24f, 18f, 38f), red);
            Ball("Belly", model, new Vector3(0f, 8f, -26f), new Vector3(22f, 8f, 34f), redDeep);
            Ball("Lid", model, new Vector3(0f, 21f, -28f), new Vector3(17f, 7f, 28f), cream);
            Ball("Nose", model, new Vector3(0f, 13f, -8f), new Vector3(15f, 12f, 8f), redDeep);
            Ball("Badge", model, new Vector3(0f, 17f, -9.6f), new Vector3(6f, 6f, 1.6f), chrome);
            Ball("StatusLight", model, new Vector3(0f, 24.4f, -21f), new Vector3(3.2f, 1.6f, 3.2f), led);
            Ball("PowerButton", model, new Vector3(0f, 24.8f, -33f), new Vector3(5f, 1.8f, 5f), chrome);
            // Carry handle arching over the back of the lid.
            Between("HandleA", model, new Vector3(0f, 24f, -38f), new Vector3(0f, 30f, -36f), 1.1f, chrome);
            Between("HandleB", model, new Vector3(0f, 30f, -36f), new Vector3(0f, 30f, -26f), 1.1f, chrome);
            Between("HandleC", model, new Vector3(0f, 30f, -26f), new Vector3(0f, 24f, -24f), 1.1f, chrome);

            // ---- wheels: big tyres, chrome hubcaps, a red cap in the middle ----
            var wheels = new System.Collections.Generic.List<Transform>();
            foreach (float x in new[] { -13.5f, 13.5f })
            {
                var w = new GameObject("Wheel").transform;
                w.SetParent(model, false);
                w.localPosition = new Vector3(x, 8f, -33f);
                float side = Mathf.Sign(x);
                L("cyl", "Tyre", w, Vector3.zero, new Vector3(16f, 4.2f, 16f), tyre, new Vector3(0f, 0f, 90f));
                L("cyl", "Hubcap", w, new Vector3(side * 2.2f, 0f, 0f), new Vector3(10f, 0.8f, 10f), chrome, new Vector3(0f, 0f, 90f));
                L("cyl", "HubRed", w, new Vector3(side * 2.7f, 0f, 0f), new Vector3(4.5f, 0.8f, 4.5f), red, new Vector3(0f, 0f, 90f));
                wheels.Add(w);
            }
            Ball("Caster", model, new Vector3(0f, 3f, -13f), new Vector3(6f, 6f, 6f), tyre);

            // ---- hose: a smooth ribbed tube from the nose, looping up and over to the grip ----
            Vector3 p0 = new Vector3(0f, 14f, -5f), p1 = new Vector3(0f, 44f, 6f), p2 = new Vector3(0f, 46f, 24f), p3 = new Vector3(0f, 30f, 36f);
            Between("HoseSocket", model, new Vector3(0f, 14f, -7f), new Vector3(0f, 15.5f, -1f), 3.4f, chrome);
            const int rings = 26;
            for (int i = 0; i < rings; i++)
            {
                float t = i / (rings - 1f), u = 1f - t;
                Vector3 pos = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                bool rib = i % 2 == 0;
                Ball("HoseRing", model, pos, Vector3.one * (rib ? 5.6f : 4.6f), rib ? hoseB : hoseA);
            }

            // ---- grip and wand ----
            Vector3 gripTop = new Vector3(0f, 31f, 37f), gripBottom = new Vector3(0f, 24f, 33f);
            Between("Grip", model, gripTop, gripBottom, 2.4f, red);
            Ball("GripCap", model, gripTop, Vector3.one * 5.2f, redDeep);
            Ball("Trigger", model, new Vector3(0f, 26f, 36.2f), new Vector3(3f, 3f, 2f), chrome);
            Vector3 wandTop = new Vector3(0f, 24f, 34f), wandBottom = new Vector3(0f, 6f, 54f);
            Between("Wand", model, wandTop, wandBottom, 1.5f, chrome);
            Between("WandCollar", model, Vector3.Lerp(wandTop, wandBottom, 0.45f), Vector3.Lerp(wandTop, wandBottom, 0.55f), 2.2f, red);

            // ---- floor head: rounded, with headlights, a bristle strip and a swivel ball ----
            Ball("Swivel", model, new Vector3(0f, 7f, 53f), Vector3.one * 5f, chrome);
            Ball("Head", model, new Vector3(0f, 3.4f, 59f), new Vector3(26f, 6f, 14f), red);
            Ball("HeadTop", model, new Vector3(0f, 5.2f, 58f), new Vector3(20f, 3.4f, 9f), cream);
            L("box", "Bristles", model, new Vector3(0f, 1f, 66.2f), new Vector3(24f, 1.8f, 1.6f), bristle);
            foreach (float x in new[] { -8f, 8f }) Ball("Headlight", model, new Vector3(x, 3.6f, 65.4f), new Vector3(3.6f, 3.2f, 1.8f), lamp);
            var glow = StageKit.PointLight("HeadGlow", model, vac.position + new Vector3(0f, 6f, 66f), new Color(1f, 0.92f, 0.65f), 90f, 30f);

            // ---- suction: a wide funnel that pulls in and down, and a brutal mouth at the head ----
            var funnel = StageKit.Column("VacuumFunnel", vac, Vector3.zero, 70f, 105f, -5f, -28f, 5f, strength: 0f);
            funnel.transform.localPosition = new Vector3(0f, 0f, 60f * ModelScale);
            funnel.coreFraction = 0.12f;
            funnel.ramp = 1.5f;
            var mouth = StageKit.Column("VacuumMouth", vac, Vector3.zero, 16f, 34f, -24f, -16f, 8f, strength: 0f);
            mouth.transform.localPosition = new Vector3(0f, 0f, 60f * ModelScale);
            mouth.coreFraction = 0.4f;
            mouth.ramp = 1.5f;

            // Dust and fluff streaming in toward the nozzle.
            var dust = StageKit.Emitter(vac, "Dust", new Vector3(0f, 22f * ModelScale, 60f * ModelScale), new Vector3(-90f, 0f, 0f), StageKit.ShimmerMat, false, 0.5f, 1.1f);
            {
                var m = dust.main; m.startColor = new Color(0.82f, 0.76f, 0.62f, 0.7f); m.startSpeed = 0f; m.startLifetime = 1.8f;
                m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 400;
                var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 45f; sh.radiusThickness = 0.15f;
                var vel = dust.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
                vel.radial = new ParticleSystem.MinMaxCurve(-32f);
                var em = dust.emission; em.rateOverTime = 90f; em.enabled = false;
            }

            // Mum pushes it back and forth while the suction runs.
            var sweep = vac.gameObject.AddComponent<SuctionSweeper>();
            BuilderUtil.SetRef(sweep, "suction", funnel);
            var sso = new SerializedObject(sweep);
            sso.FindProperty("from").vector3Value = new Vector3(-70f, 0f, 0f);
            sso.FindProperty("to").vector3Value = new Vector3(70f, 0f, 0f);
            sso.FindProperty("speed").floatValue = 15f;
            sso.ApplyModifiedPropertiesWithoutUndo();
            StageKit29.SetArray(sweep, "spinners", wheels.ToArray());

            var ev = StageKit29.Event(events, "MumsVacuum", "MUM'S VACUUM!", "Everything is being sucked toward the nozzle and down to the floor: climb high, hug the ceiling!",
                                      new Color(1f, 0.45f, 0.35f), 10f, 3f, 9f, 16f);
            ev.air = new AirSource[] { funnel, mouth };
            ev.loops = new[] { dust };
            ev.warnSound = Audio.Sfx.Alarm;
            ev.activeSound = Audio.Sfx.BossSlam;
            return ev;
        }

        // ------------------------------------------------------------------ THE KAIJU

        static AirBossSettings KaijuSettings()
        {
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(StageKit29.S + "Boss_Kaiju.asset");
            s.displayName = "THE KAIJU";
            s.title = "Built by Maya and Sam, together";
            s.maxHealth = 620f;
            s.armoredMultiplier = 0.25f;
            s.weakSpotMultiplier = 3f;
            s.aggroRange = 360f;
            s.engageDelay = 3f;
            s.wakeUpTime = 2.6f;
            s.phase2At = 0.6f;
            s.phase3At = 0.3f;
            s.telegraph = 1.4f;
            s.attackDuration = 5f;
            s.vulnerableTime = 6.5f;
            s.restTime = 1.3f;
            s.contactDamagePerSecond = 0f;
            s.shotsPerVolley = 4;
            s.spreadAngle = 26f;
            s.projectileSpeed = 22f;
            s.projectileDamage = 6f;
            s.projectileLifetime = 5f;
            s.projectileScale = 1.9f;
            s.respawnDelay = 25f;
            EditorUtility.SetDirty(s);
            return s;
        }

        static StapleProjectile Shock()
        {
            var root = new GameObject("ShockShot");
            root.AddComponent<StapleProjectile>();
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 1.2f;
            var r = ball.GetComponent<Renderer>();
            r.sharedMaterial = StageKit.Emissive("M_LR_ShockShot", new Color(1f, 0.7f, 0.3f), new Color(3f, 1.8f, 0.6f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + "ShockShot.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<StapleProjectile>();
        }

        static GameObject L(string kind, string name, Transform parent, Vector3 local, Vector3 size, Material mat, Vector3 euler = default, bool collider = false)
        {
            var go = kind == "cyl" ? StageKit.Cyl(name, parent, Vector3.zero, size, mat, collider: collider)
                                   : StageKit.Box(name, parent, Vector3.zero, size, mat, collider: collider);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static KaijuBoss Kaiju(Transform root, Pal pal)
        {
            var settings = KaijuSettings();
            var shock = Shock();
            var weak = Stage26Builder.WeakSpotMat();
            var partMat = Stage26Builder.PartMat();

            var group = StageKit.Group(root, "LivingRoomBoss");
            group.gameObject.SetActive(false);
            var bossRoot = new GameObject("Kaiju").transform;
            bossRoot.SetParent(group, false);
            bossRoot.position = o;

            Transform Point(string n, Vector3 world) { var t = new GameObject(n).transform; t.SetParent(bossRoot, false); t.position = world; return t; }
            var laneA = Point("LaneWest", A(-85f, 0f, -35f));
            var laneB = Point("LaneEast", A(85f, 0f, -35f));

            // ---- the giant: a stack of boxes, facing north ----
            var giant = new GameObject("Giant").transform;
            giant.SetParent(bossRoot, false);
            giant.position = A(0f, 0f, -35f);
            StageKit29.Kinematic(giant.gameObject);

            var legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var leg = new GameObject(i == 0 ? "LegL" : "LegR").transform;
                leg.SetParent(giant, false);
                leg.localPosition = new Vector3(i == 0 ? -8f : 8f, 24f, 0f);
                L("box", "Thigh", leg, new Vector3(0f, -12f, 0f), new Vector3(10f, 24f, 11f), pal.cardboardDark, collider: true);
                L("box", "Foot", leg, new Vector3(0f, -22f, 3f), new Vector3(11f, 4f, 17f), pal.cardboard, collider: true);
                legs[i] = leg;
            }
            L("box", "Torso", giant, new Vector3(0f, 40f, 0f), new Vector3(32f, 32f, 19f), pal.cardboard, collider: true);
            L("box", "Belt", giant, new Vector3(0f, 26f, 0f), new Vector3(33f, 3f, 20f), pal.tape);
            L("box", "Head", giant, new Vector3(0f, 63f, 1f), new Vector3(20f, 17f, 17f), pal.cardboardDark, collider: true);
            var eyes = new[] { L("box", "EyeL", giant, new Vector3(-5f, 64f, 9.4f), new Vector3(5f, 3f, 1f), pal.eye), L("box", "EyeR", giant, new Vector3(5f, 64f, 9.4f), new Vector3(5f, 3f, 1f), pal.eye) };
            L("cyl", "Antenna", giant, new Vector3(0f, 76f, 0f), new Vector3(1f, 10f, 1f), pal.ink);
            L("box", "Flag", giant, new Vector3(2.5f, 79f, 0f), new Vector3(5f, 3f, 0.3f), pal.redFlag);
            L("box", "FlagBlue", giant, new Vector3(2.5f, 76f, 0f), new Vector3(5f, 3f, 0.3f), pal.blueFlag);

            // Chest hatch and the fuse behind it (the weak spot).
            var fuse = L("box", "Fuse_WeakSpot", giant, new Vector3(0f, 42f, 9.2f), new Vector3(10f, 10f, 1.4f), weak);
            fuse.AddComponent<BoxCollider>();
            var hatch = new GameObject("Hatch").transform;
            hatch.SetParent(giant, false);
            hatch.localPosition = new Vector3(0f, 50f, 10f);
            L("box", "Flap", hatch, new Vector3(0f, -7f, 0f), new Vector3(16f, 14f, 1f), pal.cardboardDark, collider: true);
            L("box", "FlapTape", hatch, new Vector3(0f, -7f, 0.6f), new Vector3(16.4f, 2f, 0.4f), pal.tape);

            // Arms: shoulder pivots, with the shoulder bolts (parts) and a fist muzzle each.
            var fists = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var arm = new GameObject(i == 0 ? "ArmL" : "ArmR").transform;
                arm.SetParent(giant, false);
                arm.localPosition = new Vector3(side * 20f, 50f, 0f);
                L("box", "Upper", arm, new Vector3(side * 2f, -12f, 0f), new Vector3(8f, 26f, 8f), pal.cardboardDark, collider: true);
                L("box", "Fist", arm, new Vector3(side * 2f, -28f, 2f), new Vector3(13f, 12f, 13f), pal.cardboard, collider: true);
                var muzzle = new GameObject("FistMuzzle").transform;
                muzzle.SetParent(arm, false);
                muzzle.localPosition = new Vector3(side * 2f, -28f, 9f);
                fists[i] = muzzle;

                var part = Stage26Builder.Part(giant, i == 0 ? "Bolt_L" : "Bolt_R", "SHOULDER BOLT", new Vector3(side * 17f, 52f, 6f), new Vector3(6f, 6f, 6f), 60f, 0.08f);
                var look = part.transform.Find("Look");
                var head = L("cyl", "BoltHead", look, Vector3.zero, new Vector3(5f, 2.4f, 5f), partMat, new Vector3(90f, 0f, 0f));
                Stage26Builder.FinishPart(part, new[] { head.GetComponent<Renderer>() }, new GameObject[0]);
            }

            var feetMuzzle = new GameObject("FeetMuzzle").transform;
            feetMuzzle.SetParent(giant, false);
            feetMuzzle.localPosition = new Vector3(0f, 3f, 6f);

            var burst = StageKit29.Burst(giant, "DefeatBurst", new Vector3(0f, 40f, 0f), 300, 24f, 1f);
            var drones = Stage26Builder.Drones(bossRoot, new[] { A(70f, 85f, 40f) - o, A(-70f, 85f, 30f) - o });
            var shield = Stage26Builder.Shield(giant, new Vector3(0f, 40f, 0f), new Vector3(52f, 70f, 40f));

            var boss = bossRoot.gameObject.AddComponent<KaijuBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", feetMuzzle);
            BuilderUtil.SetRef(boss, "projectilePrefab", shock);
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            BuilderUtil.SetRef(boss, "giant", giant);
            BuilderUtil.SetRef(boss, "laneA", laneA);
            BuilderUtil.SetRef(boss, "laneB", laneB);
            BuilderUtil.SetRef(boss, "hatch", hatch);
            StageKit29.SetArray(boss, "legs", legs);
            StageKit29.SetArray(boss, "fists", fists);
            StageKit29.SetArray(boss, "eyes", new[] { eyes[0].GetComponent<Renderer>(), eyes[1].GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "weakSpotGlow", new[] { fuse.GetComponent<Renderer>() });

            var target = bossRoot.gameObject.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = "THE KAIJU";
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = fuse.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }
    }
}
