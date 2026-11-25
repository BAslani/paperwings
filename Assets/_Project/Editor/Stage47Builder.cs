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
    /// Stage 47: Tiny War, Act 2, "The Waterworks". Menu: PaperWings > Stage Builders > Stage 47 (Tiny War: Bathroom)
    ///
    /// The family bathroom on a Saturday morning (room 6, at -800 / +800), built straight in the
    /// animated-film look of Stages 38-43: rounded edges, a designed palette, painted tile textures,
    /// morning light and its own colour grade. Sam's Green Gang holds it.
    ///   - the BATHTUB along the north wall, full of water, with Sam's RUBBER-DUCK NAVY bobbing on it
    ///     (the flagship flies a green flag from a toothbrush mast)
    ///   - the SHOWER at its east end: a big column of steam (lifts you, soaks your paper)
    ///   - the EXTRACTOR FAN in the ceiling: an updraft that sucks you up toward it
    ///   - the HOT TOWEL RAIL on the east wall: a warm thermal, and the soap convoy's road
    ///   - DRIPPING TAPS (bath and sink): thin wet columns that soak paper
    ///   - the vanity with a mirror and Sam's toilet-roll castle; a toilet; a laundry basket (the throw
    ///     point); a frosted window full of morning sun
    ///   Levels: 4 Bathtub Landing Day (ring run), 5 Soap Convoy (intercept), 6 THE TYPHOON (boss: a
    ///   hairdryer taped to a toy truck). Needs Stage 46. Safe to re-run (rebuilds the bathroom).
    /// </summary>
    public static class Stage47Builder
    {
        public static readonly Vector3 Origin = new Vector3(-800f, 0f, 800f);
        public const int Bathroom = 6;
        const float W = 120f, D = 90f, H = 120f;
        const string Art = StageKit.Root + "Art/";
        const string Prefix = "M_BW_";
        const string GradePath = Art + "PostFX_BathroomLook.asset";

        static Vector3 o;
        static Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);

        // Palette: morning bathroom, pastel and clean, with the war's colours on the props.
        static readonly Color Mint = new Color(0.74f, 0.90f, 0.85f);
        static readonly Color MintDeep = new Color(0.38f, 0.66f, 0.60f);
        static readonly Color Butter = new Color(0.98f, 0.93f, 0.76f);
        static readonly Color Enamel = new Color(0.97f, 0.97f, 0.95f);
        static readonly Color Chrome = new Color(0.82f, 0.85f, 0.88f);
        static readonly Color Coral = new Color(0.95f, 0.50f, 0.42f);
        static readonly Color Teal = new Color(0.18f, 0.52f, 0.58f);
        static readonly Color DuckYellow = new Color(1f, 0.82f, 0.18f);
        static readonly Color Green = new Color(0.31f, 0.60f, 0.29f);
        static readonly Color Ink = new Color(0.17f, 0.17f, 0.17f);

        [MenuItem("PaperWings/Stage Builders/Stage 47 (Tiny War: Bathroom)", priority = 158)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report = Run(scene);
            if (report == null) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 47 built: the Bathroom (Tiny War, Act 2).\n\n" + report +
                "\n\nMain menu: TINY WAR, levels 4 to 6 (or pick BATHROOM with Left/Right for free play).", "Splash");
        }

        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            if (StageKit.Director() == null) { EditorUtility.DisplayDialog("PaperWings", "No ModeDirector. Run the earlier stages first.", "OK"); return null; }
            if (AssetDatabase.LoadAssetAtPath<CampaignSettings>(StageKit.Root + "Settings/Campaign_TinyWar.asset") == null)
            { EditorUtility.DisplayDialog("PaperWings", "Run Stage 46 (Tiny War: Act 1) first.", "OK"); return null; }
            o = Origin;
            EditorUtility.DisplayProgressBar("PaperWings", "Tiling the bathroom...", 0.1f);
            try
            {
                var root = StageKit.FreshRoot(scene, "Bathroom", o);
                var pal = Palette();
                Shell(root, pal);
                var props = StageKit.Group(root, "Props");
                var air = StageKit.Group(root, "Air");
                Bathtub(props, air, pal);
                Vanity(props, air, pal);
                TowelRail(props, air, pal);
                Fixtures(root, props, air, pal);
                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.5f);
                var store = new StageLook.MeshStore(Art + "Meshes/RoundedBoxes_Bathroom.asset");
                int rounded = StageLook.RoundCubes(root, store, Prefix);
                EditorUtility.DisplayProgressBar("PaperWings", "Morning light...", 0.65f);
                Lights(StageKit.Group(root, "Lights"));
                Grade(root);

                var room = root.gameObject.AddComponent<Room>();
                room.displayName = "Bathroom";
                room.description = "Shower steam, an extractor fan that sucks you up, a hot towel rail, dripping taps and a rubber-duck navy.";
                room.spawnPoint = StageKit.Spawn("LaundrySpawn", root, A(96f, 34f, -70f), -40f);
                room.extraSpawns = new[] { StageKit.Spawn("DoorSpawn", root, A(46f, 40f, -80f), 0f) };
                room.ringRunMedals = new Vector3(70f, 52f, 42f);
                room.routeGroup = Route(root).gameObject;
                room.combatGroup = Combat(root).gameObject;
                StageKit.SetRoom(Bathroom, room);

                EditorUtility.DisplayProgressBar("PaperWings", "Taping a hairdryer to a truck...", 0.8f);
                SoapConvoy(room);
                var boss = Typhoon(root);
                room.bossGroup = boss.transform.parent.gameObject;
                EditorUtility.SetDirty(room);

                var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(StageKit.Root + "Settings/Campaign_TinyWar.asset");
                campaign.nights = CampaignSettings.TinyWar();
                campaign.comingSoon = CampaignSettings.TinyWarComingSoon();
                campaign.completeLine = "TO BE CONTINUED: SATURDAY NIGHT...";
                EditorUtility.SetDirty(campaign);
                AssetDatabase.SaveAssets();

                return $"- The bathroom: bathtub and rubber-duck navy, shower steam, extractor fan updraft, hot towel rail, dripping taps.\n" +
                       $"- Animated-film look: {rounded} rounded boxes, painted tiles, morning sun, its own colour grade.\n" +
                       $"- Tiny War now has {campaign.Count} levels: Bathtub Landing Day, Soap Convoy, THE TYPHOON.";
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // ------------------------------------------------------------------ palette and textures

        sealed class Pal
        {
            public Material floor, wallTile, wallPaint, ceiling, trim, enamel, chrome, water, duck, beak, duckGreen, mint, mintDeep, coral, teal,
                            mirror, wicker, wood, curtain, mat, window, glowStrip, ink, cardboard, tube, rubber, soap;
        }

        static Pal Palette()
        {
            return new Pal
            {
                floor = StageLook.Mat(Prefix + "Floor", Color.white, 0.45f, 0f, FloorTexture(), new Vector2(3.5f, 2.6f)),
                wallTile = StageLook.Mat(Prefix + "WallTile", Color.white, 0.6f, 0f, SubwayTexture(), new Vector2(0.35f, 0.35f)),
                wallPaint = StageLook.Mat(Prefix + "WallPaint", Butter, 0.15f),
                ceiling = StageLook.Mat(Prefix + "Ceiling", new Color(0.98f, 0.97f, 0.94f), 0.1f),
                trim = StageLook.Mat(Prefix + "Trim", Enamel, 0.5f),
                enamel = StageLook.Mat(Prefix + "Enamel", Enamel, 0.85f),
                chrome = StageLook.Mat(Prefix + "Chrome", Chrome, 0.92f, 0.95f),
                water = StageKit29.Glass(Prefix + "Water", new Color(0.45f, 0.78f, 0.88f, 0.55f)),
                duck = StageLook.Mat(Prefix + "Duck", DuckYellow, 0.75f),
                beak = StageLook.Mat(Prefix + "Beak", new Color(1f, 0.52f, 0.12f), 0.6f),
                duckGreen = StageLook.Mat(Prefix + "GangGreen", Green, 0.3f),
                mint = StageLook.Mat(Prefix + "Mint", Mint, 0.35f),
                mintDeep = StageLook.Mat(Prefix + "MintDeep", MintDeep, 0.35f),
                coral = StageLook.Mat(Prefix + "Coral", Coral, 0.1f),
                teal = StageLook.Mat(Prefix + "Teal", Teal, 0.1f),
                mirror = StageLook.Mat(Prefix + "Mirror", new Color(0.72f, 0.80f, 0.86f), 0.98f, 1f),
                wicker = StageLook.Mat(Prefix + "Wicker", Color.white, 0.2f, 0f, WickerTexture(), new Vector2(0.5f, 0.5f)),
                wood = StageLook.Mat(Prefix + "Wood", new Color(0.78f, 0.6f, 0.42f), 0.35f),
                curtain = StageLook.Mat(Prefix + "Curtain", Color.white, 0.2f, 0f, CurtainTexture(), new Vector2(0.25f, 0.25f)),
                mat = StageLook.Mat(Prefix + "BathMat", Color.white, 0.05f, 0f, MatTexture(), new Vector2(1f / 60f, 1f / 26f)),
                window = Emissive(Prefix + "FrostedSun", new Color(1f, 0.97f, 0.88f), new Color(2.6f, 2.4f, 2.0f)),
                glowStrip = Emissive(Prefix + "VanityLight", new Color(1f, 0.95f, 0.85f), new Color(2.2f, 2.0f, 1.6f)),
                ink = StageLook.Mat(Prefix + "Ink", Ink, 0.4f),
                cardboard = StageLook.Mat(Prefix + "Cardboard", new Color(0.74f, 0.58f, 0.38f), 0.1f),
                tube = StageLook.Mat(Prefix + "Tube", new Color(0.86f, 0.76f, 0.58f), 0.1f),
                rubber = StageLook.Mat(Prefix + "Rubber", new Color(0.13f, 0.13f, 0.14f), 0.3f),
                soap = StageLook.Mat(Prefix + "Soap", new Color(0.98f, 0.72f, 0.80f), 0.6f),
            };
        }

        static Material Emissive(string name, Color c, Color e)
        {
            var m = StageKit.Emissive(name, c, e);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }

        /// <summary>Small white floor tiles with mint grout and the odd mint tile.</summary>
        static Texture2D FloorTexture() => StageLook.Paint(Art + "T_BW_Floor.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float gu = Mathf.Repeat(u * 8f, 1f), gv = Mathf.Repeat(v * 8f, 1f);
            float grout = Mathf.Min(StageLook.Smooth(0f, 0.06f, gu) * StageLook.Smooth(1f, 0.94f, gu), StageLook.Smooth(0f, 0.06f, gv) * StageLook.Smooth(1f, 0.94f, gv));
            int tx = Mathf.FloorToInt(u * 8f), ty = Mathf.FloorToInt(v * 8f);
            bool accent = (tx * 7 + ty * 3) % 11 == 0;
            var tile = accent ? Mint : new Color(0.96f, 0.96f, 0.94f);
            return Color.Lerp(new Color(0.70f, 0.82f, 0.78f), tile, grout);
        });

        /// <summary>Mint subway tiles in a running bond.</summary>
        static Texture2D SubwayTexture() => StageLook.Paint(Art + "T_BW_Subway.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float rows = 8f, cols = 4f;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float cu = u * cols + (row % 2 == 0 ? 0f : 0.5f);
            float gu = Mathf.Repeat(cu, 1f), gv = Mathf.Repeat(rv, 1f);
            float grout = Mathf.Min(StageLook.Smooth(0f, 0.04f, gu) * StageLook.Smooth(1f, 0.96f, gu), StageLook.Smooth(0f, 0.08f, gv) * StageLook.Smooth(1f, 0.92f, gv));
            float sheen = 0.04f * Mathf.Sin(gv * Mathf.PI);
            return Color.Lerp(new Color(0.92f, 0.94f, 0.9f), Mint * (1f + sheen), grout);
        });

        static Texture2D WickerTexture() => StageLook.Paint(Art + "T_BW_Wicker.asset", 128, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float a = Mathf.Sin(u * Mathf.PI * 16f) * Mathf.Sin(v * Mathf.PI * 16f);
            return Color.Lerp(new Color(0.62f, 0.46f, 0.28f), new Color(0.86f, 0.70f, 0.46f), 0.5f + 0.5f * a);
        });

        /// <summary>White shower curtain with blue and yellow fish.</summary>
        static Texture2D CurtainTexture() => StageLook.Paint(Art + "T_BW_Curtain.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float cu = Mathf.Repeat(u * 4f, 1f) - 0.5f, cv = Mathf.Repeat(v * 4f + (Mathf.FloorToInt(u * 4f) % 2) * 0.5f, 1f) - 0.5f;
            float body = (cu * cu) / 0.06f + (cv * cv) / 0.015f;
            float tailX = cu - 0.28f;
            bool tail = tailX > 0f && tailX < 0.12f && Mathf.Abs(cv) < tailX * 1.2f;
            bool fish = body < 1f || tail;
            bool yellow = (Mathf.FloorToInt(u * 4f) + Mathf.FloorToInt(v * 4f)) % 2 == 0;
            return fish ? (yellow ? DuckYellow : new Color(0.35f, 0.62f, 0.86f)) : new Color(0.97f, 0.97f, 0.96f);
        });

        /// <summary>A striped bath mat, coral and cream.</summary>
        static Texture2D MatTexture() => StageLook.Paint(Art + "T_BW_BathMat.asset", 128, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            bool border = u < 0.06f || u > 0.94f || v < 0.1f || v > 0.9f;
            bool stripe = Mathf.Repeat(u * 7f, 1f) < 0.45f;
            return border ? Teal : stripe ? Coral : new Color(0.97f, 0.92f, 0.82f);
        });

        // ------------------------------------------------------------------ the room

        static void Shell(Transform root, Pal pal)
        {
            var shell = StageKit.Group(root, "Shell");
            StageKit.Slab("Floor", shell, A(0f, 0f, 0f), W * 2f, D * 2f, pal.floor, true);
            StageKit.Slab("Ceiling", shell, A(0f, H, 0f), W * 2f, D * 2f, pal.ceiling, false);
            // Painted walls above, tiles below (a tiled skin 48 high, just proud of each wall).
            StageKit.Wall("WestWall", shell, A(-W, H * 0.5f, 0f), Vector3.right, D * 2f, H, pal.wallPaint);
            StageKit.Wall("EastWall", shell, A(W, H * 0.5f, 0f), Vector3.left, D * 2f, H, pal.wallPaint);
            StageKit.Wall("NorthWall", shell, A(0f, H * 0.5f, D), Vector3.back, W * 2f, H, pal.wallPaint);
            StageKit.Wall("SouthWall", shell, A(0f, H * 0.5f, -D), Vector3.forward, W * 2f, H, pal.wallPaint);
            const float th = 48f;
            StageKit.Box("TilesWest", shell, A(-W + 0.3f, th * 0.5f, 0f), new Vector3(0.6f, th, D * 2f), pal.wallTile, collider: false);
            StageKit.Box("TilesEast", shell, A(W - 0.3f, th * 0.5f, 0f), new Vector3(0.6f, th, D * 2f), pal.wallTile, collider: false);
            StageKit.Box("TilesNorth", shell, A(0f, th * 0.5f, D - 0.3f), new Vector3(W * 2f, th, 0.6f), pal.wallTile, collider: false);
            StageKit.Box("TilesSouth", shell, A(0f, th * 0.5f, -D + 0.3f), new Vector3(W * 2f, th, 0.6f), pal.wallTile, collider: false);
            // Above the bath the tiles go to the ceiling (it's the shower wall).
            StageKit.Box("ShowerTiles", shell, A(5f, (th + H) * 0.5f, D - 0.4f), new Vector3(132f, H - th, 0.6f), pal.wallTile, collider: false);
            foreach (var (n, p, s) in new[]
            {
                ("TileCapW", A(-W + 0.9f, th, 0f), new Vector3(1.2f, 1.6f, D * 2f)), ("TileCapE", A(W - 0.9f, th, 0f), new Vector3(1.2f, 1.6f, D * 2f)),
                ("TileCapS", A(0f, th, -D + 0.9f), new Vector3(W * 2f, 1.6f, 1.2f)),
            })
                StageKit.Box(n, shell, p, s, pal.trim, collider: false);

            // Door (south wall, a little ajar is too much at this scale: closed, with Theo's "KEEP OUT" sign).
            StageKit.Box("Door", shell, A(46f, 40f, -D + 0.8f), new Vector3(34f, 80f, 1.2f), pal.trim, collider: false);
            StageKit.Box("DoorKnob", shell, A(32f, 40f, -D + 1.8f), new Vector3(2.2f, 2.2f, 1.6f), pal.chrome, collider: false);
            StageKit.Box("KeepOutSign", shell, A(46f, 58f, -D + 1.6f), new Vector3(14f, 9f, 0.3f), pal.duckGreen, collider: false);

            // A frosted window high on the east wall, bright with morning sun.
            StageKit.Box("WindowFrame", shell, A(W - 0.6f, 86f, 45f), new Vector3(1f, 34f, 40f), pal.trim, collider: false);
            StageKit.Box("FrostedGlass", shell, A(W - 1.1f, 86f, 45f), new Vector3(0.4f, 30f, 36f), pal.window, collider: false);
            StageKit.Box("WindowBar", shell, A(W - 1.3f, 86f, 45f), new Vector3(0.4f, 30f, 1.2f), pal.trim, collider: false);
            StageKit.Box("WindowSill", shell, A(W - 3f, 70f, 45f), new Vector3(6f, 1.4f, 42f), pal.trim);

            // Bath mat in front of the tub.
            StageKit.Box("BathMat", shell, A(5f, 0.25f, 46f), new Vector3(60f, 0.5f, 26f), pal.mat, collider: false);
        }

        static void Bathtub(Transform props, Transform air, Pal pal)
        {
            var tub = StageKit.Group(props, "Bathtub");
            // Tub from x -60 to 70 along the north wall, 30 deep, rim at 24.
            StageKit.Box("TubFront", tub, A(5f, 12f, 62f), new Vector3(130f, 24f, 4f), pal.enamel);
            StageKit.Box("TubBack", tub, A(5f, 12f, 88f), new Vector3(130f, 24f, 4f), pal.enamel);
            StageKit.Box("TubEndW", tub, A(-58f, 12f, 75f), new Vector3(4f, 24f, 30f), pal.enamel);
            StageKit.Box("TubEndE", tub, A(68f, 12f, 75f), new Vector3(4f, 24f, 30f), pal.enamel);
            StageKit.Box("TubFloor", tub, A(5f, 4f, 75f), new Vector3(122f, 8f, 22f), pal.enamel);
            StageKit.Box("Water", tub, A(5f, 17f, 75f), new Vector3(122f, 0.8f, 22f), pal.water, collider: false);
            foreach (float x in new[] { -52f, 62f })
                StageKit.Box("TubFoot", tub, A(x, 1.5f, 62.5f), new Vector3(5f, 3f, 4f), pal.chrome, collider: false);
            // Taps at the east end, dripping.
            StageKit.Cyl("BathTapPost", tub, A(64f, 30f, 87f), new Vector3(2.4f, 12f, 2.4f), pal.chrome);
            StageKit.Box("BathSpout", tub, A(64f, 35f, 83.5f), new Vector3(2f, 2f, 7f), pal.chrome, collider: false);
            foreach (float x in new[] { 58f, 70f })
                StageKit.Cyl("TapHandle", tub, A(x, 34f, 87f), new Vector3(3f, 1.5f, 3f), pal.chrome, collider: false);
            Drip(air, "BathTapDrip", A(64f, 34f, 80.5f), 16f, 0f);
            // Soap dish (the convoy's start) and a bottle.
            StageKit.Box("SoapDish", tub, A(66f, 24.6f, 66f), new Vector3(7f, 1.2f, 5f), pal.chrome, collider: false);
            StageKit.Box("SoapBar", tub, A(66f, 26f, 66f), new Vector3(5f, 2f, 3.4f), pal.soap, collider: false);
            StageKit.Cyl("Shampoo", tub, A(-50f, 31f, 86f), new Vector3(5f, 14f, 5f), pal.coral);

            // ---- the rubber-duck navy ----
            var navy = StageKit.Group(tub, "DuckNavy");
            var spots = new[] { (-40f, 70f, 0.9f), (-22f, 80f, 0.8f), (-4f, 69f, 0.85f), (16f, 79f, 0.8f), (34f, 71f, 0.9f), (6f, 75f, 1.35f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var (x, z, s) = spots[i];
                bool flagship = i == spots.Length - 1;
                var duck = Duck(navy, A(x, 17.4f, z), s, pal, flagship);
                var bob = duck.AddComponent<Bobber>();
                bob.bob = 0.35f * s; bob.rock = 7f; bob.period = 2.4f + i * 0.3f; bob.phase = i * 0.17f;
                bob.drift = flagship ? 0f : 2.5f; bob.driftPeriod = 26f + i * 4f;
            }

            // ---- the shower: head, rail, curtain, steam over the east half ----
            StageKit.Box("ShowerPipe", tub, A(55f, 70f, 89f), new Vector3(2f, 50f, 2f), pal.chrome, collider: false);
            StageKit.Box("ShowerArm", tub, A(55f, 95f, 85f), new Vector3(2f, 2f, 8f), pal.chrome, collider: false);
            StageKit.Cyl("ShowerHead", tub, A(55f, 93f, 80.5f), new Vector3(9f, 2.4f, 9f), pal.chrome, euler: new Vector3(20f, 0f, 0f));
            StageKit.Cyl("CurtainRail", tub, A(5f, 104f, 60.5f), new Vector3(1.2f, 132f, 1.2f), pal.chrome, collider: false, euler: new Vector3(0f, 0f, 90f));
            StageKit.Box("ShowerCurtain", tub, A(-46f, 63f, 60.5f), new Vector3(24f, 82f, 1.4f), pal.curtain, collider: false);
            var steam = Stage2Builder.LoadOrCreate<SteamSettings>(StageKit.Root + "Settings/Steam_Shower.asset");
            steam.liftSpeed = 11f;
            steam.radius = 22f;
            steam.height = 88f;
            steam.steamRate = 55f;
            steam.steamSpread = 0.6f;
            steam.steamSize = new Vector2(3f, 7f);
            steam.swayAmount = 7f;
            steam.steamColor = AirPalette.Of(AirLook.Steam, 0.14f);
            EditorUtility.SetDirty(steam);
            var teaPrefab = StageKit.Load<GameObject>("Prefabs/TeaSteam.prefab");
            if (teaPrefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(teaPrefab, air);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = "ShowerSteam";
                go.transform.SetPositionAndRotation(A(40f, 18f, 75f), Quaternion.identity);
                go.transform.localScale = Vector3.one;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(r.gameObject);   // keep only the steam
                var src = go.GetComponentInChildren<SteamSource>(true);
                if (src != null) BuilderUtil.SetRef(src, "settings", steam);
            }
        }

        /// <summary>A rubber duck facing west (the flagship carries a toothbrush mast and Sam's green flag).</summary>
        static GameObject Duck(Transform parent, Vector3 at, float s, Pal pal, bool flagship)
        {
            var d = new GameObject(flagship ? "Flagship" : "Duck");
            d.transform.SetParent(parent, false);
            d.transform.position = at;
            d.transform.rotation = Quaternion.Euler(0f, -90f + Random.Range(-25f, 25f), 0f);
            GameObject Part(PrimitiveType type, string name, Vector3 local, Vector3 size, Material m)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = name;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(d.transform, false);
                p.transform.localPosition = local * s;
                p.transform.localScale = size * s;
                p.GetComponent<Renderer>().sharedMaterial = m;
                return p;
            }
            Part(PrimitiveType.Sphere, "Body", new Vector3(0f, 1.6f, 0f), new Vector3(8f, 5.2f, 10f), pal.duck);
            Part(PrimitiveType.Sphere, "Tail", new Vector3(0f, 3f, -4.4f), new Vector3(3.4f, 3f, 3f), pal.duck);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 6.2f, 2.6f), new Vector3(5f, 5f, 5f), pal.duck);
            Part(PrimitiveType.Sphere, "Beak", new Vector3(0f, 5.6f, 5.4f), new Vector3(2.8f, 1.2f, 2.6f), pal.beak);
            foreach (float x in new[] { -1.3f, 1.3f }) Part(PrimitiveType.Sphere, "Eye", new Vector3(x, 7f, 4.4f), new Vector3(0.9f, 1.1f, 0.6f), pal.ink);
            var col = d.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 2.5f * s, 0f);
            col.radius = 4.5f * s;
            if (flagship)
            {
                Part(PrimitiveType.Cube, "ToothbrushMast", new Vector3(0f, 9f, -1f), new Vector3(0.6f, 10f, 0.6f), pal.coral);
                Part(PrimitiveType.Cube, "Bristles", new Vector3(0f, 14.4f, -0.4f), new Vector3(0.9f, 1.6f, 1.6f), pal.trim);
                Part(PrimitiveType.Cube, "GreenFlag", new Vector3(0f, 12.4f, -3.2f), new Vector3(0.2f, 2.6f, 4f), pal.duckGreen);
            }
            return d;
        }

        static void Drip(Transform air, string name, Vector3 spout, float height, float offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(air, false);
            go.transform.position = spout;
            var drip = go.AddComponent<DripAir>();
            drip.height = height;
            drip.radius = 3.5f;
            drip.cycleOffset = offset;
            var ps = StageKit.Emitter(go.transform, "Drops", Vector3.zero, new Vector3(90f, 0f, 0f), StageKit.ShimmerMat, true, 0.25f, 0.45f);
            var main = ps.main;
            main.startColor = new Color(0.75f, 0.9f, 1f, 0.8f);
            main.startSpeed = 2f;
            main.startLifetime = Mathf.Sqrt(2f * height / 60f);
            main.gravityModifier = 6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            BuilderUtil.SetRef(drip, "drops", ps);
        }

        static void Vanity(Transform props, Transform air, Pal pal)
        {
            var v = StageKit.Group(props, "Vanity");
            // Cabinet and top on the west wall, z -10 to 40.
            StageKit.Box("Cabinet", v, A(-107f, 17f, 15f), new Vector3(26f, 34f, 50f), pal.mintDeep);
            foreach (float z in new[] { 3f, 27f })
            {
                StageKit.Box("CabinetDoor", v, A(-93.8f, 17f, z), new Vector3(0.6f, 28f, 22f), pal.mint, collider: false);
                StageKit.Box("DoorKnob", v, A(-93.2f, 22f, z + (z < 15f ? 8f : -8f)), new Vector3(0.8f, 1.6f, 1.6f), pal.chrome, collider: false);
            }
            StageKit.Box("Countertop", v, A(-106f, 35f, 15f), new Vector3(29f, 2f, 53f), pal.enamel);
            StageKit.Cyl("Basin", v, A(-104f, 36.05f, 12f), new Vector3(16f, 0.2f, 16f), pal.chrome, collider: false);
            StageKit.Cyl("TapPost", v, A(-116f, 40f, 12f), new Vector3(2.2f, 8f, 2.2f), pal.chrome);
            StageKit.Box("TapSpout", v, A(-112.5f, 43.5f, 12f), new Vector3(7f, 1.6f, 1.6f), pal.chrome, collider: false);
            Drip(air, "SinkTapDrip", A(-109.5f, 42.5f, 12f), 6f, 1.1f);
            StageKit.Box("Toothmug", v, A(-115f, 40f, -2f), new Vector3(4f, 8f, 4f), pal.teal, collider: false);
            StageKit.Box("Toothbrush", v, A(-115f, 46f, -2f), new Vector3(0.6f, 8f, 0.6f), pal.coral, collider: false, euler: new Vector3(0f, 0f, 12f));
            // Mirror and a light bar over it.
            StageKit.Box("MirrorFrame", v, A(-119.4f, 72f, 15f), new Vector3(0.8f, 42f, 48f), pal.trim, collider: false);
            StageKit.Box("Mirror", v, A(-119f, 72f, 15f), new Vector3(0.4f, 38f, 44f), pal.mirror, collider: false);
            StageKit.Box("VanityLight", v, A(-118f, 96f, 15f), new Vector3(3f, 3f, 40f), pal.glowStrip, collider: false);

            // Sam's toilet-roll castle on the counter: the Green Gang's base (the soap's destination).
            var castle = StageKit.Group(v, "TubeCastle");
            foreach (var (x, z, h) in new[] { (-112f, 30f, 10f), (-100f, 30f, 10f), (-112f, 38f, 10f), (-100f, 38f, 10f), (-106f, 34f, 15f) })
                StageKit.Cyl("Tube", castle, A(x, 36f + h * 0.5f, z), new Vector3(5f, h, 5f), pal.tube);
            StageKit.Box("Wall", castle, A(-106f, 39f, 30f), new Vector3(12f, 6f, 0.6f), pal.cardboard, collider: false);
            StageKit.Cyl("FlagPole", castle, A(-106f, 56f, 34f), new Vector3(0.4f, 10f, 0.4f), pal.ink, collider: false);
            StageKit.Box("GreenFlag", castle, A(-103.5f, 59f, 34f), new Vector3(5f, 3.4f, 0.2f), pal.duckGreen, collider: false);
        }

        static void TowelRail(Transform props, Transform air, Pal pal)
        {
            var t = StageKit.Group(props, "TowelRail");
            const float x = W - 3f;
            foreach (float z in new[] { -42f, -2f })
                StageKit.Cyl("Upright", t, A(x, 46f, z), new Vector3(1.8f, 52f, 1.8f), pal.chrome);
            for (int i = 0; i < 6; i++)
                StageKit.Cyl("Rung", t, A(x - 0.4f, 24f + i * 9f, -22f), new Vector3(1.2f, 40f, 1.2f), pal.chrome, collider: false, euler: new Vector3(90f, 0f, 0f));
            StageKit.Box("TowelBig", t, A(x - 2.2f, 54f, -22f), new Vector3(2.4f, 26f, 30f), pal.coral, collider: false);
            StageKit.Box("TowelSmall", t, A(x - 3.4f, 36f, -18f), new Vector3(2.2f, 14f, 18f), pal.teal, collider: false);
            // It's a HOT towel rail: a warm column up the east wall.
            var heat = StageKit.ThermalAsset("Thermal_TowelRail", 8f, 13f, 70f, 12f);
            StageKit.Thermal("TowelRailHeat", air, A(x - 8f, 20f, -22f), heat);
        }

        static void Fixtures(Transform root, Transform props, Transform air, Pal pal)
        {
            // Toilet in the south-west corner, lid down.
            var wc = StageKit.Group(props, "Toilet");
            StageKit.Cyl("Pedestal", wc, A(-100f, 8f, -70f), new Vector3(12f, 16f, 14f), pal.enamel);
            StageKit.Box("Seat", wc, A(-100f, 17f, -70f), new Vector3(18f, 2f, 22f), pal.enamel);
            StageKit.Box("Lid", wc, A(-100f, 18.6f, -70f), new Vector3(17f, 1.2f, 20f), pal.trim);
            StageKit.Box("Cistern", wc, A(-114f, 30f, -70f), new Vector3(10f, 24f, 22f), pal.enamel);
            StageKit.Box("Flush", wc, A(-114f, 42.4f, -70f), new Vector3(3f, 0.8f, 3f), pal.chrome, collider: false);
            StageKit.Cyl("LooRoll", wc, A(-118f, 24f, -52f), new Vector3(5f, 5f, 5f), pal.trim, collider: false, euler: new Vector3(0f, 0f, 90f));

            // Laundry basket in the south-east corner (the throw point is on its lid).
            var basket = StageKit.Group(props, "LaundryBasket");
            StageKit.Cyl("Basket", basket, A(96f, 15f, -70f), new Vector3(24f, 30f, 24f), pal.wicker);
            StageKit.Cyl("BasketLid", basket, A(96f, 31f, -70f), new Vector3(25f, 2f, 25f), pal.wicker);
            StageKit.Box("SockFlag", basket, A(88f, 34f, -62f), new Vector3(6f, 1.2f, 3f), pal.teal, collider: false, euler: new Vector3(0f, 30f, 10f));
            // Blue Wing's beachhead: a crayon flag on the basket.
            StageKit.Cyl("BlueFlagPole", basket, A(104f, 39f, -76f), new Vector3(0.4f, 12f, 0.4f), pal.ink, collider: false);
            StageKit.Box("BlueFlag", basket, A(106.5f, 43f, -76f), new Vector3(5f, 3.4f, 0.2f), StageLook.Mat(Prefix + "BlueWing", new Color(0.23f, 0.41f, 0.71f), 0.2f), collider: false);

            // The extractor fan in the ceiling: a grille, and an updraft that pulls you toward it.
            var fan = StageKit.Group(props, "ExtractorFan");
            StageKit.Box("FanHousing", fan, A(-20f, H - 0.8f, 20f), new Vector3(18f, 1.6f, 18f), pal.trim, collider: false);
            for (int i = 0; i < 6; i++)
                StageKit.Box("Slat", fan, A(-20f, H - 1.8f, 13f + i * 2.8f), new Vector3(15f, 0.6f, 0.8f), pal.ink, collider: false);
            var pull = StageKit.Column("ExtractorUpdraft", air, A(-20f, 40f, 20f), 16f, H - 42f, 9f, 0f, 2.5f);
            pull.coreFraction = 0.5f;
        }

        // ------------------------------------------------------------------ light and grade

        static void Lights(Transform parent)
        {
            var key = StageKit.PointLight("CeilingLight", parent, A(10f, H - 12f, 0f), new Color(1f, 0.95f, 0.86f), 1500f, 220f, shadows: true);
            key.shadows = LightShadows.Soft;
            StageKit.Box("CeilingLamp", parent, A(10f, H - 1.5f, 0f), new Vector3(20f, 3f, 20f), Emissive(Prefix + "Lamp", new Color(1f, 0.97f, 0.9f), new Color(2.2f, 2.1f, 1.8f)), collider: false);
            StageKit.PointLight("VanityGlow", parent, A(-108f, 92f, 15f), new Color(1f, 0.93f, 0.8f), 260f, 70f);
            // Morning sun through the frosted window, falling across the bath.
            var sun = new GameObject("MorningSun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.transform.position = A(W - 4f, 92f, 45f);
            sun.transform.LookAt(A(-20f, 0f, 40f));
            sun.type = LightType.Spot;
            sun.color = new Color(1f, 0.86f, 0.62f);
            sun.intensity = 9000f;
            sun.range = 260f;
            sun.spotAngle = 55f;
            sun.innerSpotAngle = 30f;
            sun.shadows = LightShadows.Soft;
            StageKit.PointLight("TubBounce", parent, A(5f, 30f, 60f), new Color(0.7f, 0.95f, 1f), 140f, 90f);
            StageKit.PointLight("FloorBounce", parent, A(0f, 8f, -20f), new Color(1f, 0.92f, 0.82f), 110f, 130f);
            StageKit.PointLight("CeilingBounce", parent, A(0f, H - 15f, -30f), new Color(0.9f, 0.97f, 1f), 160f, 160f);
        }

        static void Grade(Transform root)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(8f);
            color.saturation.Override(10f);
            color.postExposure.Override(0.15f);
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.40f, 0.62f, 0.66f));   // aqua shadows
            split.highlights.Override(new Color(1f, 0.88f, 0.70f));   // warm sunny highlights
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.0f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("BathroomGrade");
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

        /// <summary>Level 4's rings: steam, the duck navy, the tub's end, the fan, the towel rail, home.</summary>
        static RaceRoute Route(Transform root)
        {
            var route = StageKit.Route("BathroomRoute", root, "Bathroom");
            var pts = new[] { A(96f, 34f, -70f), A(45f, 50f, 70f), A(-15f, 30f, 74f), A(-75f, 45f, 40f), A(-20f, 82f, 18f), A(105f, 55f, -15f), A(20f, 40f, -55f) };
            string[] names = { "", "Ring1_ShowerSteam", "Ring2_DuckNavy", "Ring3_TubEnd", "Ring4_ExtractorFan", "Ring5_TowelRail", "Ring6_HomeBeach" };
            for (int i = 1; i < pts.Length; i++)
            {
                var d = pts[i] - pts[i - 1];
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                StageKit.Ring(route.transform, names[i], pts[i], yaw);
            }
            route.gameObject.SetActive(false);
            return route;
        }

        static Transform Combat(Transform root)
        {
            var g = StageKit.Group(root, "BathroomCombat");
            StageKit.RivalSpawner("RivalSpawner_1", g, A(-90f, 85f, -60f), 45f, 2f);
            StageKit.RivalSpawner("RivalSpawner_2", g, A(90f, 90f, 50f), -135f, 6f);
            g.gameObject.SetActive(false);
            return g;
        }

        /// <summary>Level 5: green couriers carry soap from the bath, along the towel rail, round to the castle.</summary>
        static void SoapConvoy(Room room)
        {
            var g = StageKit29.FreshGroup(room.transform, "SoapConvoy");
            var pts = new[]
            {
                A(66f, 30f, 64f), A(90f, 45f, 40f), A(108f, 55f, 0f), A(108f, 52f, -40f), A(80f, 50f, -62f),
                A(20f, 55f, -68f), A(-40f, 50f, -40f), A(-80f, 46f, 8f), A(-100f, 46f, 30f), A(-106f, 45f, 34f)
            };
            StageKit29.Convoy(g, "SoapConvoyRoute", ConvoySpawner.Kind.Intercept, GreenCourier(), pts, 12, 2, 7f, 9f, 45f, "Soap courier", 4f);
            StageKit29.AddExtraGroup(room, g.gameObject);
        }

        /// <summary>The red courier recoloured in the Green Gang's green (made once from CourierRed).</summary>
        static PathFlyer GreenCourier()
        {
            string redPath = StageKit29.P + "CourierRed.prefab", greenPath = StageKit29.P + "CourierGreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(redPath);
            try
            {
                var green = StageKit.Mat("M_CourierGreen", Green, 0.25f);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null || !mats[i].HasProperty("_BaseColor")) continue;
                        var c = mats[i].GetColor("_BaseColor");
                        if (c.r > c.g + 0.2f && c.r > c.b + 0.2f) mats[i] = green;   // the red parts
                    }
                    r.sharedMaterials = mats;
                }
                foreach (var tr in root.GetComponentsInChildren<TrailRenderer>(true))
                {
                    tr.startColor = new Color(0.45f, 0.9f, 0.4f, 0.8f);
                    tr.endColor = new Color(0.45f, 0.9f, 0.4f, 0f);
                }
                root.name = "CourierGreen";
                PrefabUtility.SaveAsPrefabAsset(root, greenPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<PathFlyer>(greenPath);
        }

        // ------------------------------------------------------------------ THE TYPHOON

        static AirBossSettings TyphoonSettings()
        {
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(StageKit29.S + "Boss_Typhoon.asset");
            s.displayName = "THE TYPHOON";
            s.title = "Sam's secret weapon";
            s.maxHealth = 520f;
            s.armoredMultiplier = 0.25f;
            s.weakSpotMultiplier = 3f;
            s.aggroRange = 320f;
            s.engageDelay = 2.5f;
            s.wakeUpTime = 2f;
            s.phase2At = 0.6f;
            s.phase3At = 0.3f;
            s.telegraph = 1.3f;
            s.attackDuration = 4.5f;
            s.vulnerableTime = 5.5f;
            s.restTime = 1.5f;
            s.airSpeed = 26f;            // hot blast
            s.secondaryAirSpeed = 30f;   // cold blast
            s.contactDamagePerSecond = 0f;
            s.shotsPerVolley = 4;
            s.spreadAngle = 30f;
            s.projectileSpeed = 18f;
            s.projectileDamage = 5f;
            s.projectileLifetime = 4f;
            s.projectileScale = 2.2f;
            s.respawnDelay = 25f;
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A soap bubble: The Typhoon's ammunition (slow, big, see-through).</summary>
        static StapleProjectile Bubble()
        {
            var root = new GameObject("SoapBubble");
            root.AddComponent<StapleProjectile>();
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Bubble";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 1.3f;
            var r = ball.GetComponent<Renderer>();
            r.sharedMaterial = StageKit29.Glass("M_BW_Bubble", new Color(0.85f, 0.95f, 1f, 0.35f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + "SoapBubble.prefab");
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

        static TyphoonBoss Typhoon(Transform root)
        {
            var settings = TyphoonSettings();
            var bubble = Bubble();
            var truckGreen = StageLook.Mat("M_BW_TruckGreen", Green, 0.6f, 0.1f);
            var tyre = StageLook.Mat("M_BW_Tyre", new Color(0.1f, 0.1f, 0.11f), 0.25f);
            var hub = StageLook.Mat("M_BW_Hub", DuckYellow, 0.6f);
            var dryerPink = StageLook.Mat("M_BW_Dryer", new Color(0.93f, 0.52f, 0.66f), 0.7f);
            var dark = StageLook.Mat("M_BW_DryerGrille", new Color(0.12f, 0.12f, 0.13f), 0.4f);
            var nozzleMat = StageKit.Emissive("M_BW_Nozzle", new Color(0.3f, 0.28f, 0.26f), new Color(0.2f, 0.18f, 0.15f));
            var weak = Stage26Builder.WeakSpotMat();
            var partMat = Stage26Builder.PartMat();
            var cord = StageLook.Mat("M_BW_Cord", new Color(0.92f, 0.92f, 0.9f), 0.4f);

            var group = StageKit.Group(root, "BathroomBoss");
            group.gameObject.SetActive(false);
            var bossRoot = new GameObject("Typhoon").transform;
            bossRoot.SetParent(group, false);
            bossRoot.position = o;

            Transform Point(string n, float x, float z) { var t = new GameObject(n).transform; t.SetParent(bossRoot, false); t.position = A(x, 0f, z); return t; }
            var laneA = Point("LaneWest", -60f, -20f);
            var laneB = Point("LaneEast", 70f, -20f);

            // ---- the toy monster truck ----
            var truck = new GameObject("Truck").transform;
            truck.SetParent(bossRoot, false);
            truck.position = A(5f, 0f, -20f);
            truck.rotation = Quaternion.LookRotation(Vector3.right);   // drives along x
            L("box", "Chassis", truck, new Vector3(0f, 9f, 0f), new Vector3(14f, 5f, 24f), truckGreen);
            L("box", "Cab", truck, new Vector3(0f, 14f, 5f), new Vector3(12f, 6f, 9f), truckGreen);
            L("box", "Windscreen", truck, new Vector3(0f, 14.5f, 9.6f), new Vector3(10f, 4f, 0.4f), dark);
            L("box", "Stripe", truck, new Vector3(7.05f, 9f, 0f), new Vector3(0.2f, 1.2f, 22f), hub);
            var wheels = new List<Transform>();
            foreach (var (x, z) in new[] { (-8.5f, -8f), (8.5f, -8f), (-8.5f, 8f), (8.5f, 8f) })
            {
                var w = new GameObject("Wheel").transform;
                w.SetParent(truck, false);
                w.localPosition = new Vector3(x, 6f, z);
                L("cyl", "Tyre", w, Vector3.zero, new Vector3(12f, 4f, 12f), tyre, new Vector3(0f, 0f, 90f));
                L("cyl", "Hub", w, new Vector3(Mathf.Sign(x) * 2.1f, 0f, 0f), new Vector3(5f, 0.4f, 5f), hub, new Vector3(0f, 0f, 90f));
                wheels.Add(w);
            }
            var col = truck.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(20f, 16f, 26f);
            col.center = new Vector3(0f, 9f, 0f);
            StageKit29.Kinematic(truck.gameObject);

            // ---- the hairdryer on top, aimable ----
            var dryer = new GameObject("Dryer").transform;
            dryer.SetParent(truck, false);
            dryer.localPosition = new Vector3(0f, 20f, -3f);
            L("cyl", "Barrel", dryer, new Vector3(0f, 0f, 0f), new Vector3(10f, 18f, 10f), dryerPink, new Vector3(90f, 0f, 0f));
            L("cyl", "BackGrille", dryer, new Vector3(0f, 0f, -9.2f), new Vector3(9f, 0.6f, 9f), dark, new Vector3(90f, 0f, 0f));
            L("cyl", "Nozzle", dryer, new Vector3(0f, 0f, 11.5f), new Vector3(7f, 5f, 7f), dryerPink, new Vector3(90f, 0f, 0f));
            var glowDisc = L("cyl", "NozzleGlow", dryer, new Vector3(0f, 0f, 14.1f), new Vector3(5.6f, 0.3f, 5.6f), nozzleMat, new Vector3(90f, 0f, 0f));
            L("box", "Handle", dryer, new Vector3(0f, -9f, -3f), new Vector3(4.6f, 13f, 5.4f), dryerPink, new Vector3(-12f, 0f, 0f));
            var sw = L("box", "Switch_WeakSpot", dryer, new Vector3(0f, -6f, 0.6f), new Vector3(2.6f, 3.6f, 1.4f), weak, new Vector3(-12f, 0f, 0f));
            // The cord trails off behind to the wall socket.
            for (int i = 0; i < 4; i++)
                L("cyl", "Cord", dryer, new Vector3(0f, -15f - i * 0.5f, -6f - i * 7f), new Vector3(1f, 7f, 1f), cord, new Vector3(90f, 0f, 0f));
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(dryer, false);
            muzzle.localPosition = new Vector3(0f, 0f, 16f);

            // ---- three tape straps (parts) hold it on ----
            var tapeMat = StageLook.Mat("M_BW_GaffaTape", new Color(0.55f, 0.57f, 0.6f), 0.5f);
            foreach (var (n, z) in new[] { ("Strap_Front", 5f), ("Strap_Middle", -1f), ("Strap_Back", -7f) })
            {
                var part = Stage26Builder.Part(truck, n, "TAPE STRAP", new Vector3(0f, 16f, z - 3f), new Vector3(13f, 10f, 3.5f), 60f, 0.08f);
                var look = part.transform.Find("Look");
                var band = L("box", "Band", look, new Vector3(0f, 4f, 0f), new Vector3(11.4f, 1.6f, 2.6f), partMat);
                L("box", "SideL", look, new Vector3(-5.6f, -0.5f, 0f), new Vector3(0.6f, 9f, 2.6f), tapeMat);
                L("box", "SideR", look, new Vector3(5.6f, -0.5f, 0f), new Vector3(0.6f, 9f, 2.6f), tapeMat);
                Stage26Builder.FinishPart(part, new[] { band.GetComponent<Renderer>() }, new GameObject[0]);
            }

            // ---- air, light, smoke, drones ----
            var blast = StageKit.Beam("Blast", dryer, muzzle.position, dryer.rotation, new Vector2(16f, 16f), 95f, settings.airSpeed, strength: 0f);
            blast.transform.localPosition = muzzle.localPosition;
            blast.transform.localRotation = Quaternion.identity;
            blast.ramp = 6f;
            var light = StageKit.PointLight("NozzleLight", dryer, muzzle.position, new Color(1f, 0.45f, 0.15f), 0f, 45f);
            light.transform.localPosition = muzzle.localPosition;
            var smoke = StageKit29.Smoke(dryer, new Vector3(0f, 2f, -8f));
            var burst = StageKit29.Burst(truck, "DefeatBurst", new Vector3(0f, 14f, 0f), 260, 22f, 0.9f);
            var drones = Stage26Builder.Drones(bossRoot, new[] { new Vector3(-40f, 60f, 30f), new Vector3(40f, 60f, -40f) });
            var shield = Stage26Builder.Shield(truck, new Vector3(0f, 14f, 0f), new Vector3(34f, 30f, 38f));

            var boss = bossRoot.gameObject.AddComponent<TyphoonBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", muzzle);
            BuilderUtil.SetRef(boss, "projectilePrefab", bubble);
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            BuilderUtil.SetRef(boss, "truck", truck);
            BuilderUtil.SetRef(boss, "laneA", laneA);
            BuilderUtil.SetRef(boss, "laneB", laneB);
            BuilderUtil.SetRef(boss, "dryer", dryer);
            BuilderUtil.SetRef(boss, "blast", blast);
            BuilderUtil.SetRef(boss, "nozzleLight", light);
            BuilderUtil.SetRef(boss, "overheatSmoke", smoke);
            StageKit29.SetArray(boss, "weakSpotGlow", new[] { sw.GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "nozzleGlow", new[] { glowDisc.GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "wheels", wheels.ToArray());

            var target = bossRoot.gameObject.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = "THE TYPHOON";
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = sw.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }
    }
}
