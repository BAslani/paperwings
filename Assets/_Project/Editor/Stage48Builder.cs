using System.Collections.Generic;
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
    /// Stage 48: Tiny War, Act 3, "Lights Out". Menu: PaperWings > Stage Builders > Stage 48 (Tiny War: Bedroom)
    ///
    /// The children's bedroom on Saturday night (room 7, at 0 / +900): dark, lit by fairy lights, a
    /// nightlight, a lava lamp and the moon in the window. Sam's Green Gang holds the top bunk.
    ///   - the LAVA LAMP on the nightstand: a fat warm thermal that lifts you toward the ceiling
    ///   - the RADIATOR under the window: a warm curtain of air
    ///   - the CEILING FAN: pulses on and off, chopping down on whatever flies under it
    ///   - the PILLOW FORT under the window (Blue Wing's base) and the wardrobe: SHADOW to hide in
    ///   - the BUNK BED along the north wall, Sam's gooseneck lamp on the top bunk
    ///   - the toy box (the throw point), fairy lights, glow-in-the-dark stars
    ///   Levels: 7 Lights Out (ring run), 8 Pillow Fort (escort three medics from the toy box to the
    ///   fort while the Green Gang hunts them), 9 THE NIGHT WATCH (boss: a desk lamp that turns into a
    ///   searchlight). Needs Stage 47. Safe to re-run (rebuilds the bedroom).
    /// </summary>
    public static class Stage48Builder
    {
        public static readonly Vector3 Origin = new Vector3(0f, 0f, 900f);
        public const int Bedroom = 7;
        const float W = 120f, D = 90f, H = 120f;
        const string Art = StageKit.Root + "Art/";
        const string Prefix = "M_NT_";
        const string GradePath = Art + "PostFX_BedroomLook.asset";

        static Vector3 o;
        static Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);

        static readonly Color Navy = new Color(0.13f, 0.17f, 0.34f);
        static readonly Color NavyDeep = new Color(0.08f, 0.10f, 0.22f);
        static readonly Color Wood = new Color(0.62f, 0.45f, 0.30f);
        static readonly Color WoodPale = new Color(0.82f, 0.68f, 0.50f);
        static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
        static readonly Color Rose = new Color(0.93f, 0.55f, 0.62f);
        static readonly Color Sky = new Color(0.50f, 0.72f, 0.90f);
        static readonly Color Butter = new Color(0.98f, 0.86f, 0.50f);
        static readonly Color Green = new Color(0.31f, 0.60f, 0.29f);
        static readonly Color Ink = new Color(0.17f, 0.17f, 0.17f);

        [MenuItem("PaperWings/Stage Builders/Stage 48 (Tiny War: Bedroom)", priority = 159)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report = Run(scene);
            if (report == null) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 48 built: the Bedroom (Tiny War, Act 3).\n\n" + report +
                "\n\nMain menu: TINY WAR, levels 7 to 9 (or pick BEDROOM with Left/Right for free play).", "Lights out");
        }

        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            if (StageKit.Director() == null) { EditorUtility.DisplayDialog("PaperWings", "No ModeDirector. Run the earlier stages first.", "OK"); return null; }
            if (AssetDatabase.LoadAssetAtPath<CampaignSettings>(StageKit.Root + "Settings/Campaign_TinyWar.asset") == null)
            { EditorUtility.DisplayDialog("PaperWings", "Run Stages 46 and 47 (Tiny War: Acts 1 and 2) first.", "OK"); return null; }
            o = Origin;
            EditorUtility.DisplayProgressBar("PaperWings", "Turning the lights out...", 0.1f);
            try
            {
                var root = StageKit.FreshRoot(scene, "Bedroom", o);
                var pal = Palette();
                Shell(root, pal);
                var props = StageKit.Group(root, "Props");
                var air = StageKit.Group(root, "Air");
                Bunk(props, pal);
                Wardrobe(props, pal);
                PillowFort(props, pal);
                ToyBox(props, pal);
                Nightstand(props, air, pal);
                Window(props, air, pal);
                Stars(root, pal);
                FairyLights(root, pal);
                var fan = StageKit.CeilingFan("CeilingFan", air, A(10f, H - 28f, 10f), o.y + H,
                                              StageKit.CeilingFanAsset("Fan_Bedroom", 16f, 70f, 6f, 5f), 0f);
                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.5f);
                var store = new StageLook.MeshStore(Art + "Meshes/RoundedBoxes_Bedroom.asset");
                int rounded = StageLook.RoundCubes(root, store, Prefix);
                EditorUtility.DisplayProgressBar("PaperWings", "Moonlight...", 0.65f);
                Lights(StageKit.Group(root, "Lights"));
                Grade(root);

                var room = root.gameObject.AddComponent<Room>();
                room.displayName = "Bedroom";
                room.description = "Fairy lights, a lava lamp thermal, a radiator, a chopping ceiling fan, a pillow fort to hide in and a searchlight on the top bunk.";
                room.spawnPoint = StageKit.Spawn("ToyBoxSpawn", root, A(92f, 40f, -60f), -57f);
                room.extraSpawns = new[] { StageKit.Spawn("DoorSpawn", root, A(100f, 40f, 10f), -90f) };
                room.ringRunMedals = new Vector3(72f, 54f, 44f);
                room.routeGroup = Route(root).gameObject;
                room.combatGroup = Combat(root).gameObject;
                StageKit.SetRoom(Bedroom, room);

                EditorUtility.DisplayProgressBar("PaperWings", "Bolting a lamp to the bunk...", 0.8f);
                FortConvoy(room);
                var boss = NightWatch(root);
                room.bossGroup = boss.transform.parent.gameObject;
                EditorUtility.SetDirty(room);

                var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(StageKit.Root + "Settings/Campaign_TinyWar.asset");
                campaign.nights = CampaignSettings.TinyWar();
                campaign.comingSoon = CampaignSettings.TinyWarComingSoon();
                campaign.completeLine = "TO BE CONTINUED: SUNDAY MORNING...";
                EditorUtility.SetDirty(campaign);
                AssetDatabase.SaveAssets();

                return "- The bedroom: bunk bed, wardrobe, pillow fort, toy box, lava lamp thermal, radiator, chopping ceiling fan, fairy lights, glow stars.\n" +
                       $"- Night look: {rounded} rounded boxes, moonlight, its own colour grade.\n" +
                       $"- Tiny War now has {campaign.Count} levels: Lights Out, Pillow Fort, THE NIGHT WATCH.";
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // ------------------------------------------------------------------ palette and textures

        sealed class Pal
        {
            public Material floor, wallpaper, ceiling, trim, wood, woodPale, rug, sheet, duvet, pillowA, pillowB, pillowC, mattress, metal, glass,
                            lava, ink, green, toyRed, toyBlue, toyYellow, cardboard, glowStar, bulbA, bulbB, bulbC, bulbD, window, nightlight;
        }

        static Pal Palette()
        {
            return new Pal
            {
                floor = StageLook.Mat(Prefix + "Floor", Color.white, 0.4f, 0f, FloorTexture(), new Vector2(3.5f, 2.6f)),
                wallpaper = StageLook.Mat(Prefix + "Wallpaper", Color.white, 0.1f, 0f, WallpaperTexture(), new Vector2(0.25f, 0.25f)),
                ceiling = StageLook.Mat(Prefix + "Ceiling", NavyDeep, 0.05f),
                trim = StageLook.Mat(Prefix + "Trim", Cream, 0.4f),
                wood = StageLook.Mat(Prefix + "Wood", Wood, 0.35f),
                woodPale = StageLook.Mat(Prefix + "WoodPale", WoodPale, 0.35f),
                rug = StageLook.Mat(Prefix + "Rug", Color.white, 0.05f, 0f, RugTexture(), Vector2.one),
                sheet = StageLook.Mat(Prefix + "Sheet", Sky, 0.1f),
                duvet = StageLook.Mat(Prefix + "Duvet", Rose, 0.1f),
                pillowA = StageLook.Mat(Prefix + "PillowCream", Cream, 0.1f),
                pillowB = StageLook.Mat(Prefix + "PillowSky", new Color(0.62f, 0.80f, 0.92f), 0.1f),
                pillowC = StageLook.Mat(Prefix + "PillowButter", Butter, 0.1f),
                mattress = StageLook.Mat(Prefix + "Mattress", new Color(0.88f, 0.90f, 0.95f), 0.1f),
                metal = StageLook.Mat(Prefix + "Metal", new Color(0.55f, 0.57f, 0.62f), 0.7f, 0.6f),
                glass = StageKit29.Glass(Prefix + "Glass", new Color(0.95f, 0.55f, 0.70f, 0.45f)),
                lava = Emissive(Prefix + "Lava", new Color(1f, 0.4f, 0.55f), new Color(2.4f, 0.5f, 0.8f)),
                ink = StageLook.Mat(Prefix + "Ink", Ink, 0.4f),
                green = StageLook.Mat(Prefix + "GangGreen", Green, 0.3f),
                toyRed = StageLook.Mat(Prefix + "ToyRed", new Color(0.88f, 0.3f, 0.25f), 0.3f),
                toyBlue = StageLook.Mat(Prefix + "ToyBlue", new Color(0.25f, 0.45f, 0.82f), 0.3f),
                toyYellow = StageLook.Mat(Prefix + "ToyYellow", new Color(0.98f, 0.82f, 0.22f), 0.3f),
                cardboard = StageLook.Mat(Prefix + "Cardboard", new Color(0.74f, 0.58f, 0.38f), 0.1f),
                glowStar = Emissive(Prefix + "GlowStar", new Color(0.8f, 1f, 0.7f), new Color(0.9f, 2.2f, 0.9f)),
                bulbA = Emissive(Prefix + "FairyWarm", new Color(1f, 0.85f, 0.5f), new Color(3f, 2.2f, 0.9f)),
                bulbB = Emissive(Prefix + "FairyPink", new Color(1f, 0.6f, 0.7f), new Color(3f, 1.1f, 1.6f)),
                bulbC = Emissive(Prefix + "FairyBlue", new Color(0.6f, 0.8f, 1f), new Color(1.1f, 2f, 3f)),
                bulbD = Emissive(Prefix + "FairyGreen", new Color(0.6f, 1f, 0.7f), new Color(1.1f, 3f, 1.4f)),
                window = Emissive(Prefix + "Moon", new Color(0.8f, 0.88f, 1f), new Color(1.3f, 1.7f, 2.6f)),
                nightlight = Emissive(Prefix + "Nightlight", new Color(1f, 0.85f, 0.55f), new Color(3.4f, 2.4f, 1f)),
            };
        }

        static Material Emissive(string name, Color c, Color e)
        {
            var m = StageKit.Emissive(name, c, e);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }

        static Texture2D FloorTexture() => StageLook.Paint(Art + "T_NT_Floor.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float rows = 10f, rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float gv = Mathf.Repeat(rv, 1f);
            float cu = Mathf.Repeat(u * 3f + row * 0.37f, 1f);
            float seam = Mathf.Min(StageLook.Smooth(0f, 0.05f, gv) * StageLook.Smooth(1f, 0.95f, gv), StageLook.Smooth(0f, 0.02f, cu) * StageLook.Smooth(1f, 0.98f, cu));
            float tone = 0.88f + 0.12f * Mathf.Sin(row * 12.9f + Mathf.Floor(u * 3f + row * 0.37f) * 7.1f);
            return Color.Lerp(new Color(0.24f, 0.15f, 0.09f), Wood * tone, seam);
        });

        /// <summary>Navy wallpaper with little cream stars and moons.</summary>
        static Texture2D WallpaperTexture() => StageLook.Paint(Art + "T_NT_Wallpaper.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float cu = Mathf.Repeat(u * 4f, 1f) - 0.5f, cv = Mathf.Repeat(v * 4f + (Mathf.FloorToInt(u * 4f) % 2) * 0.5f, 1f) - 0.5f;
            float d = Mathf.Sqrt(cu * cu + cv * cv);
            bool moon = d < 0.16f && Mathf.Sqrt((cu - 0.07f) * (cu - 0.07f) + cv * cv) > 0.14f;
            bool star = Mathf.Abs(cu) + Mathf.Abs(cv) < 0.05f;
            return moon || star ? new Color(0.93f, 0.86f, 0.55f) : Color.Lerp(Navy, NavyDeep, v);
        });

        static Texture2D RugTexture() => StageLook.Paint(Art + "T_NT_Rug.asset", 128, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            int ring = Mathf.FloorToInt(d * 6f);
            Color[] c = { Rose, Cream, Sky, Butter, Rose, Cream };
            return d > 1f ? new Color(0f, 0f, 0f, 0f) : c[Mathf.Clamp(ring, 0, 5)];
        });

        // ------------------------------------------------------------------ the room

        static void Shell(Transform root, Pal pal)
        {
            var shell = StageKit.Group(root, "Shell");
            StageKit.Slab("Floor", shell, A(0f, 0f, 0f), W * 2f, D * 2f, pal.floor, true);
            StageKit.Slab("Ceiling", shell, A(0f, H, 0f), W * 2f, D * 2f, pal.ceiling, false);
            StageKit.Wall("WestWall", shell, A(-W, H * 0.5f, 0f), Vector3.right, D * 2f, H, pal.wallpaper);
            StageKit.Wall("EastWall", shell, A(W, H * 0.5f, 0f), Vector3.left, D * 2f, H, pal.wallpaper);
            StageKit.Wall("NorthWall", shell, A(0f, H * 0.5f, D), Vector3.back, W * 2f, H, pal.wallpaper);
            StageKit.Wall("SouthWall", shell, A(0f, H * 0.5f, -D), Vector3.forward, W * 2f, H, pal.wallpaper);
            // Skirting boards.
            StageKit.Box("SkirtW", shell, A(-W + 0.8f, 3f, 0f), new Vector3(1.6f, 6f, D * 2f), pal.trim, collider: false);
            StageKit.Box("SkirtE", shell, A(W - 0.8f, 3f, 0f), new Vector3(1.6f, 6f, D * 2f), pal.trim, collider: false);
            StageKit.Box("SkirtN", shell, A(0f, 3f, D - 0.8f), new Vector3(W * 2f, 6f, 1.6f), pal.trim, collider: false);
            StageKit.Box("SkirtS", shell, A(0f, 3f, -D + 0.8f), new Vector3(W * 2f, 6f, 1.6f), pal.trim, collider: false);

            // Door on the east wall, a stripe of landing light under it, and the nightlight beside it.
            StageKit.Box("Door", shell, A(W - 0.8f, 40f, -22f), new Vector3(1.2f, 80f, 34f), pal.wood, collider: false);
            StageKit.Box("DoorKnob", shell, A(W - 1.8f, 40f, -10f), new Vector3(1.6f, 2.2f, 2.2f), pal.metal, collider: false);
            StageKit.Box("LandingLight", shell, A(W - 1.2f, 0.7f, -22f), new Vector3(0.4f, 1.2f, 32f), pal.nightlight, collider: false);
            StageKit.Box("KeepOutSign", shell, A(W - 1.6f, 62f, -22f), new Vector3(0.3f, 9f, 14f), pal.green, collider: false);
            StageKit.Box("Nightlight", shell, A(W - 2.5f, 14f, 8f), new Vector3(4f, 5f, 5f), pal.nightlight, collider: false);

            // A round rug in the middle of the floor.
            StageKit.Cyl("Rug", shell, A(-10f, 0.3f, 0f), new Vector3(90f, 0.6f, 90f), pal.rug, collider: false);
        }

        /// <summary>The bunk bed along the north-east wall: bottom bunk, top bunk, ladder. Sam's lamp goes on top.</summary>
        static void Bunk(Transform props, Pal pal)
        {
            var b = StageKit.Group(props, "BunkBed");
            const float x0 = 30f, x1 = 112f, z0 = 56f, z1 = 88f;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, len = x1 - x0, wid = z1 - z0;
            foreach (var (x, z) in new[] { (x0 + 2f, z0 + 2f), (x1 - 2f, z0 + 2f), (x0 + 2f, z1 - 2f), (x1 - 2f, z1 - 2f) })
                StageKit.Box("Post", b, A(x, 41f, z), new Vector3(4f, 82f, 4f), pal.wood);
            foreach (float y in new[] { 22f, 62f })
            {
                StageKit.Box("BedBase", b, A(cx, y, cz), new Vector3(len, 3f, wid), pal.woodPale);
                StageKit.Box("Mattress", b, A(cx, y + 4f, cz), new Vector3(len - 4f, 5f, wid - 4f), pal.mattress);
                StageKit.Box("Sheet", b, A(cx, y + 6.7f, cz), new Vector3(len - 3f, 0.6f, wid - 3f), y < 40f ? pal.duvet : pal.sheet, collider: false);
            }
            // Pillows and the guard rail on the top bunk.
            StageKit.Box("TopPillow", b, A(x1 - 12f, 70f, cz), new Vector3(14f, 5f, 20f), pal.pillowA);
            StageKit.Box("BottomPillow", b, A(x1 - 12f, 30f, cz), new Vector3(14f, 5f, 20f), pal.pillowB);
            StageKit.Box("GuardRail", b, A(cx, 76f, z0 + 1.5f), new Vector3(len, 2.2f, 2.2f), pal.wood, collider: false);
            foreach (float x in new[] { 40f, 56f, 72f, 88f })
                StageKit.Box("RailBar", b, A(x, 70f, z0 + 1.5f), new Vector3(1.6f, 14f, 1.6f), pal.wood, collider: false);
            // Ladder on the west end.
            for (int i = 0; i < 6; i++)
                StageKit.Box("Rung", b, A(x0 - 1f, 10f + i * 11f, z0 + 14f), new Vector3(2f, 1.6f, 12f), pal.wood, collider: false);
            // Sam's flag on the bunk.
            StageKit.Cyl("GangPole", b, A(x1 - 6f, 92f, z0 + 4f), new Vector3(0.5f, 20f, 0.5f), pal.ink, collider: false);
            StageKit.Box("GangFlag", b, A(x1 - 2.5f, 98f, z0 + 4f), new Vector3(7f, 4.4f, 0.3f), pal.green, collider: false);
        }

        static void Wardrobe(Transform props, Pal pal)
        {
            var w = StageKit.Group(props, "Wardrobe");
            StageKit.Box("Body", w, A(-106f, 52f, 15f), new Vector3(26f, 104f, 56f), pal.woodPale);
            foreach (float z in new[] { 1f, 29f })
            {
                StageKit.Box("Door", w, A(-92.6f, 50f, z), new Vector3(0.8f, 92f, 26f), pal.wood, collider: false);
                StageKit.Box("Handle", w, A(-91.8f, 50f, z + (z < 15f ? 10f : -10f)), new Vector3(1.2f, 10f, 1.4f), pal.metal, collider: false);
            }
            StageKit.Box("Crown", w, A(-106f, 105f, 15f), new Vector3(28f, 3f, 58f), pal.wood, collider: false);
        }

        /// <summary>The Blue Wing's base: pillows stacked into walls under the window, a duvet roof over the back half. Open to the north.</summary>
        static void PillowFort(Transform props, Pal pal)
        {
            var f = StageKit.Group(props, "PillowFort");
            Material[] m = { pal.pillowA, pal.pillowB, pal.pillowC, pal.duvet };
            int n = 0;
            Material Next() => m[n++ % m.Length];
            // West wall, east wall and back wall, two courses high.
            for (int course = 0; course < 2; course++)
            {
                float y = 8f + course * 14f;
                for (int i = 0; i < 3; i++)
                {
                    StageKit.Box("PillowW", f, A(-42f + course * 1.5f, y, -34f - i * 11f), new Vector3(10f, 12f, 12f), Next());
                    StageKit.Box("PillowE", f, A(0f - course * 1.5f, y, -34f - i * 11f), new Vector3(10f, 12f, 12f), Next());
                }
                for (int i = 0; i < 3; i++)
                    StageKit.Box("PillowBack", f, A(-30f + i * 12f, y, -62f), new Vector3(12f, 12f, 10f), Next());
            }
            // Duvet roof over the back half, sagging a little.
            StageKit.Box("DuvetRoof", f, A(-20f, 37f, -48f), new Vector3(50f, 5f, 34f), pal.duvet);
            StageKit.Box("DuvetDrape", f, A(-20f, 33f, -31f), new Vector3(50f, 8f, 2f), pal.duvet, collider: false);
            // Cushions on the floor inside, and a Blue Wing flag on the roof.
            StageKit.Box("Cushion", f, A(-20f, 2f, -46f), new Vector3(20f, 3f, 20f), pal.pillowB, collider: false);
            StageKit.Cyl("BlueFlagPole", f, A(-20f, 48f, -56f), new Vector3(0.5f, 20f, 0.5f), pal.ink, collider: false);
            StageKit.Box("BlueFlag", f, A(-16.5f, 54f, -56f), new Vector3(7f, 4.4f, 0.3f), StageLook.Mat(Prefix + "BlueWing", new Color(0.23f, 0.41f, 0.71f), 0.2f), collider: false);
        }

        static void ToyBox(Transform props, Pal pal)
        {
            var t = StageKit.Group(props, "ToyBox");
            StageKit.Box("Box", t, A(92f, 11f, -60f), new Vector3(30f, 22f, 26f), pal.toyRed);
            StageKit.Box("Lid", t, A(92f, 23f, -60f), new Vector3(31f, 2.4f, 27f), pal.toyBlue);
            StageKit.Box("BlockA", t, A(86f, 28f, -56f), new Vector3(6f, 6f, 6f), pal.toyYellow, collider: false, euler: new Vector3(0f, 20f, 0f));
            StageKit.Box("BlockB", t, A(97f, 27f, -64f), new Vector3(5f, 5f, 5f), pal.pillowB, collider: false, euler: new Vector3(0f, -30f, 0f));
            StageKit.Box("Letter", t, A(106f, 1.5f, -30f), new Vector3(7f, 3f, 5f), pal.toyBlue, collider: false, euler: new Vector3(0f, 40f, 0f));
            StageKit.Box("Block", t, A(70f, 3f, -20f), new Vector3(6f, 6f, 6f), pal.toyRed, collider: false, euler: new Vector3(0f, 15f, 0f));
        }

        /// <summary>Nightstand on the west wall with a lava lamp: a warm thermal you can ride to the ceiling.</summary>
        static void Nightstand(Transform props, Transform air, Pal pal)
        {
            var n = StageKit.Group(props, "Nightstand");
            StageKit.Box("Stand", n, A(-104f, 15f, -50f), new Vector3(24f, 30f, 24f), pal.wood);
            StageKit.Box("Drawer", n, A(-91.8f, 20f, -50f), new Vector3(0.6f, 10f, 18f), pal.woodPale, collider: false);
            StageKit.Cyl("LampBase", n, A(-104f, 32f, -50f), new Vector3(9f, 5f, 9f), pal.metal, collider: false);
            StageKit.Cyl("LavaGlass", n, A(-104f, 46f, -50f), new Vector3(7f, 22f, 7f), pal.glass, collider: false);
            StageKit.Cyl("LavaCore", n, A(-104f, 45f, -50f), new Vector3(4.5f, 14f, 4.5f), pal.lava, collider: false);
            for (int i = 0; i < 3; i++)
                StageKit.Cyl("Blob", n, A(-104f + (i - 1) * 1.2f, 40f + i * 5f, -50f), new Vector3(3f, 3f, 3f), pal.lava, collider: false);
            StageKit.Cyl("LampCap", n, A(-104f, 58f, -50f), new Vector3(7f, 3f, 7f), pal.metal, collider: false);
            StageKit.PointLight("LavaGlow", n, A(-98f, 46f, -50f), new Color(1f, 0.4f, 0.55f), 260f, 60f);
            var heat = StageKit.ThermalAsset("Thermal_LavaLamp", 9f, 12f, 85f, 12f);
            StageKit.Thermal("LavaLampHeat", air, A(-104f, 56f, -50f), heat);
        }

        /// <summary>The moonlit window on the south wall, the radiator under it, and the curtains.</summary>
        static void Window(Transform props, Transform air, Pal pal)
        {
            var w = StageKit.Group(props, "Window");
            StageKit.Box("Frame", w, A(0f, 82f, -D + 0.6f), new Vector3(60f, 52f, 1f), pal.trim, collider: false);
            StageKit.Box("Moonlight", w, A(0f, 82f, -D + 1.2f), new Vector3(54f, 46f, 0.4f), pal.window, collider: false);
            StageKit.Box("BarV", w, A(0f, 82f, -D + 1.6f), new Vector3(1.4f, 46f, 0.6f), pal.trim, collider: false);
            StageKit.Box("BarH", w, A(0f, 82f, -D + 1.6f), new Vector3(54f, 1.4f, 0.6f), pal.trim, collider: false);
            StageKit.Box("Sill", w, A(0f, 56f, -D + 4f), new Vector3(66f, 2f, 8f), pal.trim);
            foreach (float x in new[] { -36f, 36f })
                StageKit.Box("Curtain", w, A(x, 80f, -D + 3f), new Vector3(14f, 66f, 2f), pal.duvet, collider: false);
            StageKit.Box("CurtainRod", w, A(0f, 113f, -D + 3f), new Vector3(90f, 1.2f, 1.2f), pal.metal, collider: false);
            // A radiator under it: a warm curtain of rising air.
            StageKit.Radiator("Radiator", air, A(60f, 0f, -D + 1f), 0f, 30f, StageKit.RadiatorAsset());
        }

        static void Stars(Transform root, Pal pal)
        {
            var g = StageKit.Group(root, "GlowStars");
            var rnd = new System.Random(48);
            for (int i = 0; i < 46; i++)
            {
                float s = 1.6f + (float)rnd.NextDouble() * 1.6f;
                StageKit.Box("Star", g, A((float)(rnd.NextDouble() * 2 - 1) * (W - 8f), H - 0.8f, (float)(rnd.NextDouble() * 2 - 1) * (D - 8f)),
                             new Vector3(s, 0.4f, s), pal.glowStar, collider: false, euler: new Vector3(0f, (float)rnd.NextDouble() * 90f, 0f));
            }
        }

        /// <summary>A string of fairy lights sagging along the west and north walls.</summary>
        static void FairyLights(Transform root, Pal pal)
        {
            var g = StageKit.Group(root, "FairyLights");
            Material[] mats = { pal.bulbA, pal.bulbB, pal.bulbC, pal.bulbD };
            const int n = 22;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float x = Mathf.Lerp(-W + 3f, 20f, t);
                float y = 100f - Mathf.Sin(t * Mathf.PI) * 9f - (i % 2) * 2f;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulb";
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(g, false);
                bulb.transform.position = A(x, y, D - 3f);
                bulb.transform.localScale = Vector3.one * 2.2f;
                bulb.GetComponent<Renderer>().sharedMaterial = mats[i % 4];
            }
            StageKit.Box("Wire", g, A(-50f, 94f, D - 3f), new Vector3(140f, 0.3f, 0.3f), pal.ink, collider: false);
            StageKit.PointLight("FairyGlowA", g, A(-80f, 96f, D - 8f), new Color(1f, 0.8f, 0.55f), 330f, 90f);
            StageKit.PointLight("FairyGlowB", g, A(-20f, 96f, D - 8f), new Color(1f, 0.65f, 0.8f), 330f, 90f);
        }

        // ------------------------------------------------------------------ light and grade

        static void Lights(Transform parent)
        {
            // The moon through the window, falling across the floor and the pillow fort.
            var moon = new GameObject("MoonBeam").AddComponent<Light>();
            moon.transform.SetParent(parent, false);
            moon.transform.position = A(0f, 90f, -D + 4f);
            moon.transform.LookAt(A(-5f, 0f, 10f));
            moon.type = LightType.Spot;
            moon.color = new Color(0.62f, 0.76f, 1f);
            moon.intensity = 9000f;
            moon.range = 280f;
            moon.spotAngle = 70f;
            moon.innerSpotAngle = 35f;
            moon.shadows = LightShadows.Soft;
            StageKit.PointLight("Nightlight", parent, A(W - 10f, 14f, 8f), new Color(1f, 0.82f, 0.5f), 220f, 80f);
            StageKit.PointLight("DoorGlow", parent, A(W - 10f, 4f, -22f), new Color(1f, 0.9f, 0.7f), 90f, 50f);
            StageKit.PointLight("CentreFill", parent, A(0f, 70f, 0f), new Color(0.55f, 0.6f, 0.9f), 520f, 200f);
            StageKit.PointLight("FloorBounce", parent, A(0f, 8f, -10f), new Color(0.8f, 0.7f, 0.9f), 90f, 130f);
        }

        static void Grade(Transform root)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(12f);
            color.saturation.Override(6f);
            color.postExposure.Override(-0.15f);
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.30f, 0.34f, 0.70f));   // deep blue shadows
            split.highlights.Override(new Color(1f, 0.82f, 0.62f));   // warm nightlight highlights
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.6f);
            bloom.scatter.Override(0.8f);
            bloom.tint.Override(new Color(0.95f, 0.9f, 1f));
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.2f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("BedroomGrade");
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

        /// <summary>Level 7's rings: the radiator, over the fort, the lava lamp, the wardrobe side, the fairy lights, the door.</summary>
        static RaceRoute Route(Transform root)
        {
            var route = StageKit.Route("BedroomRoute", root, "Bedroom");
            var pts = new[] { A(92f, 40f, -60f), A(50f, 50f, -30f), A(-22f, 62f, -52f), A(-96f, 74f, -50f), A(-60f, 76f, 30f), A(-10f, 62f, 66f), A(94f, 50f, -5f) };
            string[] names = { "", "Ring1_Radiator", "Ring2_PillowFort", "Ring3_LavaLamp", "Ring4_Wardrobe", "Ring5_FairyLights", "Ring6_Door" };
            for (int i = 1; i < pts.Length; i++)
            {
                var d = pts[i] - pts[i - 1];
                StageKit.Ring(route.transform, names[i], pts[i], Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            }
            route.gameObject.SetActive(false);
            return route;
        }

        static Transform Combat(Transform root)
        {
            var g = StageKit.Group(root, "BedroomCombat");
            StageKit.RivalSpawner("RivalSpawner_1", g, A(-80f, 85f, 60f), -135f, 2f);
            StageKit.RivalSpawner("RivalSpawner_2", g, A(70f, 100f, -50f), 135f, 6f);
            g.gameObject.SetActive(false);
            return g;
        }

        /// <summary>Level 8: three Blue Wing medics fly from the toy box, over the rug, into the pillow fort.</summary>
        static void FortConvoy(Room room)
        {
            var g = StageKit29.FreshGroup(room.transform, "FortConvoy");
            var prefab = AssetDatabase.LoadAssetAtPath<PathFlyer>(StageKit29.P + "CourierBlue.prefab");
            var pts = new[]
            {
                A(86f, 38f, -56f), A(60f, 46f, -26f), A(34f, 52f, 14f), A(8f, 56f, 30f), A(-24f, 52f, 18f),
                A(-34f, 44f, -4f), A(-20f, 30f, -16f), A(-20f, 25f, -30f), A(-20f, 22f, -44f)
            };
            StageKit29.Convoy(g, "FortConvoyRoute", ConvoySpawner.Kind.Escort, prefab, pts, 3, 1, 4f, 8f, 140f, "Medic", 4f);
            StageKit29.AddExtraGroup(room, g.gameObject);
        }

        // ------------------------------------------------------------------ THE NIGHT WATCH

        static AirBossSettings NightWatchSettings()
        {
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(StageKit29.S + "Boss_NightWatch.asset");
            s.displayName = "THE NIGHT WATCH";
            s.title = "Sam's searchlight";
            s.maxHealth = 540f;
            s.armoredMultiplier = 0.25f;
            s.weakSpotMultiplier = 3f;
            s.aggroRange = 320f;
            s.engageDelay = 3f;
            s.wakeUpTime = 2.2f;
            s.phase2At = 0.6f;
            s.phase3At = 0.3f;
            s.telegraph = 1.4f;
            s.attackDuration = 7f;
            s.vulnerableTime = 6f;
            s.restTime = 1.5f;
            s.contactDamagePerSecond = 0f;
            s.shotsPerVolley = 4;
            s.spreadAngle = 26f;
            s.projectileSpeed = 24f;
            s.projectileDamage = 6f;
            s.projectileLifetime = 4f;
            s.projectileScale = 1.8f;
            s.respawnDelay = 25f;
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A marble: The Night Watch's ammunition.</summary>
        static StapleProjectile Marble()
        {
            var root = new GameObject("Marble");
            root.AddComponent<StapleProjectile>();
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 1.1f;
            var r = ball.GetComponent<Renderer>();
            r.sharedMaterial = StageKit.Emissive("M_NT_MarbleShot", new Color(0.35f, 0.7f, 0.95f), new Color(0.6f, 1.6f, 3f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + "Marble.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<StapleProjectile>();
        }

        static GameObject L(string kind, string name, Transform parent, Vector3 local, Vector3 size, Material mat, Vector3 euler = default)
        {
            var go = kind == "cyl" ? StageKit.Cyl(name, parent, Vector3.zero, size, mat, collider: false)
                                   : StageKit.Box(name, parent, Vector3.zero, size, mat, collider: false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static SearchlightBoss NightWatch(Transform root)
        {
            var settings = NightWatchSettings();
            var marble = Marble();
            var lampMetal = StageLook.Mat("M_NT_LampMetal", new Color(0.72f, 0.74f, 0.78f), 0.75f, 0.7f);
            var lampRed = StageLook.Mat("M_NT_LampRed", new Color(0.82f, 0.22f, 0.20f), 0.5f);
            var bulbMat = StageKit.Emissive("M_NT_LampBulb", new Color(1f, 0.95f, 0.8f), new Color(2.5f, 2.3f, 1.8f));
            var weak = Stage26Builder.WeakSpotMat();
            var partMat = Stage26Builder.PartMat();
            var bolt = StageLook.Mat("M_NT_Bolt", new Color(0.55f, 0.56f, 0.6f), 0.8f, 0.8f);

            var group = StageKit.Group(root, "BedroomBoss");
            group.gameObject.SetActive(false);
            var bossRoot = new GameObject("NightWatch").transform;
            bossRoot.SetParent(group, false);
            bossRoot.position = o;

            Transform Point(string n, Vector3 world) { var t = new GameObject(n).transform; t.SetParent(bossRoot, false); t.position = world; return t; }
            var centre = Point("SweepCenter", A(-20f, 40f, -10f));

            // ---- the gooseneck lamp on the top bunk ----
            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(bossRoot, false);
            lamp.position = A(100f, 68f, 74f);
            L("cyl", "Base", lamp, new Vector3(0f, 1f, 0f), new Vector3(16f, 2f, 16f), lampRed);
            L("cyl", "LowerArm", lamp, new Vector3(0f, 12f, 0f), new Vector3(2.4f, 22f, 2.4f), lampMetal);
            var elbow = new GameObject("Elbow").transform;
            elbow.SetParent(lamp, false);
            elbow.localPosition = new Vector3(0f, 24f, 0f);
            L("cyl", "ElbowJoint", elbow, Vector3.zero, new Vector3(5f, 4f, 5f), lampRed, new Vector3(0f, 0f, 90f));
            L("cyl", "UpperArm", elbow, new Vector3(-9f, 7f, -3f), new Vector3(2.2f, 22f, 2.2f), lampMetal, new Vector3(-14f, 0f, 38f));

            // ---- the head: aims the beam ----
            var head = new GameObject("Head").transform;
            head.SetParent(lamp, false);
            head.localPosition = new Vector3(-20f, 38f, -6f);
            head.rotation = Quaternion.LookRotation(centre.position - head.position);
            var shell = L("cyl", "Shell", head, new Vector3(0f, 0f, 2f), new Vector3(13f, 11f, 13f), lampRed, new Vector3(90f, 0f, 0f));
            var shellCol = shell.AddComponent<SphereCollider>();   // so the armored lamp can be hit anywhere on the head
            shellCol.radius = 0.5f;
            L("cyl", "Rim", head, new Vector3(0f, 0f, 7.6f), new Vector3(14.5f, 1.2f, 14.5f), lampMetal, new Vector3(90f, 0f, 0f));
            var bulb = L("cyl", "Bulb_WeakSpot", head, new Vector3(0f, 0f, 6.2f), new Vector3(6f, 3f, 6f), bulbMat, new Vector3(90f, 0f, 0f));
            bulb.GetComponent<Renderer>().sharedMaterial = bulbMat;
            var bulbCol = bulb.AddComponent<SphereCollider>();
            bulbCol.radius = 0.55f;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(head, false);
            muzzle.localPosition = new Vector3(0f, 0f, 9f);
            // The shade: a flap on the back that flips up when it overheats.
            var shade = new GameObject("ShadeFlap").transform;
            shade.SetParent(head, false);
            shade.localPosition = new Vector3(0f, 6.4f, -4f);
            L("box", "Flap", shade, new Vector3(0f, 0.4f, 6f), new Vector3(10f, 0.8f, 12f), lampMetal);
            var spot = new GameObject("BeamLight").AddComponent<Light>();
            spot.transform.SetParent(head, false);
            spot.transform.localPosition = new Vector3(0f, 0f, 8f);
            spot.transform.localRotation = Quaternion.identity;
            spot.type = LightType.Spot;
            spot.spotAngle = 28f;
            spot.innerSpotAngle = 18f;
            spot.range = 280f;
            spot.intensity = 0f;
            spot.color = new Color(1f, 0.95f, 0.8f);
            spot.shadows = LightShadows.Soft;

            // ---- three hinge bolts (parts) on the elbow ----
            foreach (var (n, x, z) in new[] { ("Bolt_1", -3.2f, 3f), ("Bolt_2", 3.2f, 3f), ("Bolt_3", 0f, -3.6f) })
            {
                var part = Stage26Builder.Part(elbow, n, "HINGE BOLT", new Vector3(x, 0f, z), new Vector3(3.4f, 3.4f, 3.4f), 55f, 0.08f);
                var look = part.transform.Find("Look");
                var head1 = L("cyl", "BoltHead", look, Vector3.zero, new Vector3(3f, 1.6f, 3f), partMat, new Vector3(90f, 0f, 0f));
                L("cyl", "Washer", look, new Vector3(0f, 0f, -0.9f), new Vector3(4f, 0.4f, 4f), bolt, new Vector3(90f, 0f, 0f));
                Stage26Builder.FinishPart(part, new[] { head1.GetComponent<Renderer>() }, new GameObject[0]);
            }

            // ---- smoke, drones, shield, defeat ----
            var burst = StageKit29.Burst(lamp, "DefeatBurst", new Vector3(-20f, 38f, -6f), 260, 22f, 0.9f);
            var drones = Stage26Builder.Drones(bossRoot, new[] { A(60f, 90f, 50f) - o, A(-60f, 80f, 40f) - o });
            var shield = Stage26Builder.Shield(head, Vector3.zero, new Vector3(26f, 26f, 26f));

            var boss = bossRoot.gameObject.AddComponent<SearchlightBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", muzzle);
            BuilderUtil.SetRef(boss, "projectilePrefab", marble);
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            BuilderUtil.SetRef(boss, "head", head);
            BuilderUtil.SetRef(boss, "beam", spot);
            BuilderUtil.SetRef(boss, "sweepCenter", centre);
            BuilderUtil.SetRef(boss, "shade", shade);
            StageKit29.SetArray(boss, "bulbGlow", new[] { bulb.GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "weakSpotGlow", new[] { bulb.GetComponent<Renderer>() });

            var target = bossRoot.gameObject.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = "THE NIGHT WATCH";
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = bulb.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }
    }
}
