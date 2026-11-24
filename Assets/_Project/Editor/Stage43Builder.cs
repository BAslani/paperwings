using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PaperWings.Air;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 43: Kitchen look pass, Tiny War Act 1 ("The Kitchen Front") in the "animated film" style,
    /// matching the Tiny War concept frame and story bible: a family kitchen on a Friday night while
    /// Grandad watches TV in the next room. Menu: PaperWings > Room Looks > Kitchen (Stage 43)
    ///
    ///  - Colour ("M_KL_..." copies): terracotta quarry-tile floor, cream subway tiles behind the
    ///    counter, a warm wooden worktop, dusty-blue cupboards, striped cream wallpaper, a grey fridge,
    ///    the teal kettle, cereal boxes with real (wordless) box art, a patchwork quilt for Blue's fort.
    ///  - Shape: every plain box gets soft rounded edges (same size and collider).
    ///  - Set: Theo's crayon war maps on the fridge door, a garden at night through the window with
    ///    gingham curtains and herb pots, the hall door ajar on flickering TV light, the robot vacuum
    ///    asleep in its dock (the Dreadnought, level 3), a torch sweeping out of Blue's fort, pillows,
    ///    Theo's crayon flag, a kitchen clock at five past seven, a calendar with Friday circled, a
    ///    runner rug, utensils over the sink, glowing toaster coils.
    ///  - Light: warm ceiling and pendant light with shadows, pools under the cupboards, window-shaped
    ///    moonlight, TV light flickering in from the hall, the torch beam, bounce lights.
    ///  - The fridge: its cold air now also sinks and spreads across the floor as fog (a gentle
    ///    downdraft near the floor while the door is open), so it isn't a copy of the break room's.
    ///  - Grade: a kitchen-only volume (warm highlights, cool shadows, a little film grain).
    /// Biscuit the cat is left alone: she's being replaced with a proper model. Safe to re-run.
    /// Re-running Stage 37 rebuilds the kitchen plainly: run this again afterwards.
    /// </summary>
    public static class Stage43Builder
    {
        const string Art = StageKit.Root + "Art/";
        const string MeshPath = Art + "Meshes/RoundedBoxes_Kitchen.asset";
        const string GradePath = Art + "PostFX_KitchenLook.asset";
        const string Prefix = "M_KL_";

        // Kitchen layout (Stage 37), relative to the room's origin.
        const float W = 160f, D = 120f, H = 130f, CounterTop = 36f;
        static readonly Vector3 TableAt = new Vector3(10f, 0f, -30f);
        const float FridgeX = 100f;

        // Palette (from the Tiny War concept frame).
        static readonly Color Cream = new Color(0.96f, 0.92f, 0.84f);
        static readonly Color Grout = new Color(0.84f, 0.78f, 0.64f);
        static readonly Color Wood = new Color(0.73f, 0.54f, 0.36f);
        static readonly Color Blue = new Color(0.23f, 0.41f, 0.71f);
        static readonly Color Red = new Color(0.76f, 0.25f, 0.18f);
        static readonly Color Mustard = new Color(0.91f, 0.70f, 0.23f);
        static readonly Color Teal = new Color(0.18f, 0.43f, 0.56f);
        static readonly Color Ink = new Color(0.17f, 0.17f, 0.17f);

        static Vector3 origin;
        static Vector3 A(float x, float y, float z) => origin + new Vector3(x, y, z);
        static Vector3 T(float x, float y, float z) => A(TableAt.x + x, TableAt.y + y, TableAt.z + z);

        [MenuItem("PaperWings/Room Looks/Kitchen (Stage 43)", priority = 55)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report;
            try { report = Run(scene); }
            finally { EditorUtility.ClearProgressBar(); }
            if (report == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No kitchen. Run Stage Builders > Stage 37 first.", "OK");
                return;
            }
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 43 built: Kitchen look pass.\n\n" + report +
                "\n\nLook at it in the Game view: Free Flight in the Kitchen (and wait for the fridge).", "Lovely");
        }

        /// <summary>Builds the pass into the open scene (no save, no dialog). Returns a short report, or null if there's no kitchen.</summary>
        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            var room = StageKit.RoomAt(StageKit.Kitchen);
            if (room == null) return null;
            origin = room.transform.position;

            EditorUtility.DisplayProgressBar("PaperWings", "Mixing the kitchen palette...", 0.1f);
            var map = Palette(out var pal);
            foreach (var part in Parts(room.transform)) StageLook.Remap(part, map);
            PerObjectMaterials(room.transform, pal);

            EditorUtility.DisplayProgressBar("PaperWings", "Dressing the set...", 0.35f);
            var look = StageKit29.FreshGroup(room.transform, "KitchenLook");
            var hinge = FindDeep(room.transform, "FridgeDoorHinge");
            WarMaps(hinge, pal);
            Window(StageKit.Group(look, "Window"), pal);
            Hallway(room.transform, StageKit.Group(look, "Hallway"), pal);
            Vacuum(StageKit.Group(look, "DockedVacuum"), pal);
            Fort(StageKit.Group(look, "Fort"), pal);
            Clock(StageKit.Group(look, "Clock"), pal);
            Details(StageKit.Group(look, "Details"), pal);

            EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.55f);
            var store = new StageLook.MeshStore(MeshPath);
            int rounded = 0;
            foreach (var part in Parts(room.transform)) rounded += StageLook.RoundCubes(part, store, Prefix);

            EditorUtility.DisplayProgressBar("PaperWings", "Chilling the fridge...", 0.65f);
            FridgeFog(room.transform, StageKit.Group(look, "FridgeFog"));

            EditorUtility.DisplayProgressBar("PaperWings", "Lighting...", 0.75f);
            Lights(room.transform, StageKit.Group(look, "Lights"), pal);
            Grade(look);

            return $"- {rounded} boxes now have rounded edges.\n" +
                   "- Concept-frame palette: quarry tiles, cream subway tiles, wood worktop, blue cupboards, box art.\n" +
                   "- Theo's war maps, night garden window, hall door ajar on TV light, docked vacuum, fort torch.\n" +
                   "- Warm key lights with shadows, moonlight, flickering TV light, the torch beam.\n" +
                   "- The fridge's cold air now spreads across the floor as fog.\n" +
                   "- Kitchen colour grade (its own volume).";
        }

        /// <summary>The room's groups, minus the boss slot and Biscuit (she's being replaced with a proper model).</summary>
        static IEnumerable<Transform> Parts(Transform room)
        {
            foreach (Transform child in room)
                if (child.name != "KitchenBoss" && child.name != "Biscuit") yield return child;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static void SetMat(Transform room, string name, Material mat)
        {
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
                if (r.name == name) r.sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ palette

        sealed class Pal
        {
            public Material cream, steel, brass, ink, terracotta, leaves, coil, glow, white, rubber, graphite, ledRed, nightView, hallway, curtain, quilt, flag, rug, paper;
            public Material[] magnets, pillows;
            public Material[] maps;
        }

        static Dictionary<string, Material> Palette(out Pal pal)
        {
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_WoodGrain.asset");
            if (wood == null) wood = TextureFactory.WoodGrain();
            var woodTile = new Vector2(0.6f, 0.6f);   // rounded boxes map ~10 units per UV

            pal = new Pal
            {
                cream = StageLook.Mat("M_KL_Cream", Cream, 0.5f),
                steel = StageLook.Mat("M_KL_Steel", new Color(0.78f, 0.80f, 0.82f), 0.75f, 0.85f),
                brass = StageLook.Mat("M_KL_Brass", new Color(0.85f, 0.66f, 0.32f), 0.7f, 0.9f),
                ink = StageLook.Mat("M_KL_Ink", Ink, 0.4f),
                terracotta = StageLook.Mat("M_KL_Terracotta", new Color(0.78f, 0.42f, 0.26f), 0.2f),
                leaves = StageLook.Mat("M_KL_Leaves", new Color(0.30f, 0.55f, 0.28f), 0.3f),
                coil = Emissive("M_KL_ToasterCoil", new Color(1f, 0.45f, 0.15f), new Color(3.2f, 1.1f, 0.25f)),
                glow = Emissive("M_KL_WarmGlow", new Color(1f, 0.88f, 0.68f), new Color(2.4f, 1.8f, 1.0f)),
                white = StageLook.Mat("M_KL_White", new Color(0.95f, 0.95f, 0.93f), 0.5f),
                rubber = StageLook.Mat("M_KL_Rubber", new Color(0.12f, 0.12f, 0.13f), 0.3f),
                graphite = StageLook.Mat("M_KL_Graphite", new Color(0.24f, 0.25f, 0.27f), 0.6f, 0.3f),
                ledRed = Emissive("M_KL_LedRed", new Color(1f, 0.2f, 0.2f), new Color(3.5f, 0.4f, 0.35f)),
                nightView = GlowPicture("M_KL_GardenView", GardenTexture(), new Color(1f, 1f, 1f), Vector2.one, Vector2.zero),
                hallway = GlowPicture("M_KL_Hallway", HallwayTexture(), new Color(0.9f, 0.9f, 0.9f), Vector2.one, Vector2.zero),
                curtain = StageLook.Mat("M_KL_Gingham", Color.white, 0.1f, 0f, GinghamTexture(), new Vector2(2.5f, 2.5f)),
                quilt = StageLook.Mat("M_KL_Quilt", Color.white, 0.1f, 0f, QuiltTexture(), new Vector2(10f / 24f, 10f / 24f)),
                flag = Centred("M_KL_FortFlag", FlagTexture(), new Vector2(8f, 5f), 0.2f),
                rug = StageLook.Mat("M_KL_Rug", Color.white, 0.05f, 0f, RugTexture(), Vector2.one),
                paper = StageLook.Mat("M_KL_Calendar", Color.white, 0.2f, 0f, CalendarTexture(), Vector2.one),
                magnets = new[]
                {
                    StageLook.Mat("M_KL_MagnetRed", Red, 0.6f),
                    StageLook.Mat("M_KL_MagnetBlue", Blue, 0.6f),
                    StageLook.Mat("M_KL_MagnetYellow", Mustard, 0.6f),
                },
                pillows = new[]
                {
                    StageLook.Mat("M_KL_PillowCream", new Color(0.94f, 0.90f, 0.82f), 0.1f),
                    StageLook.Mat("M_KL_PillowBlue", new Color(0.45f, 0.60f, 0.82f), 0.1f),
                },
                maps = new[]
                {
                    StageLook.Mat("M_KL_MapBattle", Color.white, 0.15f, 0f, MapTexture(0), Vector2.one),
                    StageLook.Mat("M_KL_MapCat", Color.white, 0.15f, 0f, MapTexture(1), Vector2.one),
                    StageLook.Mat("M_KL_MapRemote", Color.white, 0.15f, 0f, MapTexture(2), Vector2.one),
                },
            };

            return new Dictionary<string, Material>
            {
                // Shell (the walls get per-size wallpaper in PerObjectMaterials)
                { "M_K_Floor", StageLook.Mat("M_KL_Floor", Color.white, 0.3f, 0f, FloorTexture(), new Vector2(10f, 7.5f)) },   // 16-unit tiles on 320 x 240
                { "M_K_Ceiling", StageLook.Mat("M_KL_Ceiling", new Color(0.95f, 0.93f, 0.88f), 0.05f) },
                { "M_K_Backsplash", StageLook.Mat("M_KL_Subway", Color.white, 0.55f, 0f, SubwayTexture(), new Vector2(0.625f, 0.625f)) },

                // Cupboards and counter
                { "M_K_Cabinet", StageLook.Mat("M_KL_Cupboard", new Color(0.50f, 0.63f, 0.72f), 0.35f) },
                { "M_K_Worktop", StageLook.Mat("M_KL_Worktop", Wood, 0.45f, 0f, wood, woodTile) },
                { "M_K_Handle", pal.brass },
                { "M_K_Steel", pal.steel },
                { "M_K_DarkSteel", StageLook.Mat("M_KL_DarkSteel", new Color(0.20f, 0.20f, 0.22f), 0.55f, 0.6f) },
                { "M_K_Appliance", StageLook.Mat("M_KL_Fridge", new Color(0.85f, 0.87f, 0.89f), 0.6f, 0.15f) },
                { "M_K_Kettle", StageLook.Mat("M_KL_Kettle", Teal, 0.7f, 0.2f) },

                // Table, chairs, the fort
                { "M_K_Table", StageLook.Mat("M_KL_Table", new Color(0.79f, 0.65f, 0.48f), 0.4f, 0f, wood, woodTile) },
                { "M_K_Chair", StageLook.Mat("M_KL_Chair", new Color(0.55f, 0.41f, 0.29f), 0.4f, 0f, wood, woodTile) },
                { "M_K_Blanket", pal.quilt },

                // Window, door
                { "M_K_Frame", pal.white },
                { "M_K_Door", StageLook.Mat("M_KL_Door", new Color(0.90f, 0.86f, 0.78f), 0.35f) },
                { "M_K_UnderCabinet", Emissive("M_KL_UnderCabinet", new Color(1f, 0.92f, 0.75f), new Color(2.2f, 1.7f, 0.9f)) },
                { "M_K_CeilingLight", Emissive("M_KL_CeilingLight", new Color(1f, 0.95f, 0.85f), new Color(1.6f, 1.4f, 1.05f)) },
                { "M_K_LampShade", Emissive("M_KL_Pendant", new Color(1f, 0.85f, 0.6f), new Color(2.4f, 1.6f, 0.75f)) },
            };
        }

        /// <summary>Materials that depend on the object, not just its old material: walls by size, cereal-box art, toaster coils, the fort flag.</summary>
        static void PerObjectMaterials(Transform room, Pal pal)
        {
            var paper = WallpaperTexture();
            foreach (var (name, width) in new[] { ("WestWall", D * 2f), ("EastWall", D * 2f), ("NorthWall", W * 2f), ("SouthWall", W * 2f) })
            {
                var face = room.Find($"Shell/{name}/Face");
                if (face == null || !face.TryGetComponent(out Renderer r)) continue;
                int w = Mathf.RoundToInt(width);
                r.sharedMaterial = StageLook.Mat($"M_KL_Wall_{w}", Color.white, 0.1f, 0f, paper, new Vector2(w / 16f, H / 16f));
            }
            SetMat(room, "CerealBox_Yellow", Centred("M_KL_BoxSunny", CerealTexture(0), new Vector2(14f, 32f), 0.3f));
            SetMat(room, "CerealBox_Red", Centred("M_KL_BoxChoco", CerealTexture(1), new Vector2(16f, 40f), 0.3f));
            SetMat(room, "CerealBox_Blue", Centred("M_KL_BoxFrosty", CerealTexture(2), new Vector2(12f, 28f), 0.3f));
            SetMat(room, "ToasterSlotL", pal.coil);
            SetMat(room, "ToasterSlotR", pal.coil);
            SetMat(room, "FortFlag", pal.flag);
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = StageKit.Emissive(name, baseColor, emission);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }

        /// <summary>A picture that glows (a view, a lit hallway): the texture is both colour and emission.</summary>
        static Material GlowPicture(string name, Texture2D tex, Color glow, Vector2 tiling, Vector2 offset)
        {
            var m = StageLook.Mat(name, Color.white, 0.6f, 0f, tex, tiling);
            m.SetTextureOffset("_BaseMap", offset);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A picture laid once across the front of a rounded box of the given width and height (rounded-box UVs are centred).</summary>
        static Material Centred(string name, Texture2D tex, Vector2 size, float smoothness)
        {
            var m = StageLook.Mat(name, Color.white, smoothness, 0f, tex, new Vector2(10f / size.x, 10f / size.y));
            m.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0.5f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ painting helpers

        static float Hash(int x, int y, int seed)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>One crayon stroke along a polyline (in UV units): waxy, with gaps where the paper's grain shows through.</summary>
        sealed class Stroke
        {
            public Color color; public float width; public Vector2[] pts;
            public Stroke(Color c, float w, params Vector2[] p) { color = c; width = w; pts = p; }
        }

        static Color Crayon(Color paper, float u, float v, int x, int y, Stroke[] strokes)
        {
            var p = new Vector2(u, v);
            Color c = paper;
            float grain = 0.55f + 0.45f * Mathf.PerlinNoise(x * 0.9f, y * 0.35f);
            foreach (var s in strokes)
            {
                float d = float.MaxValue;
                for (int i = 0; i < s.pts.Length - 1; i++) d = Mathf.Min(d, SegDist(p, s.pts[i], s.pts[i + 1]));
                float a = (1f - StageLook.Smooth(s.width * 0.55f, s.width, d)) * grain;
                c = Color.Lerp(c, s.color, a);
            }
            return c;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        static Vector2[] Circle(Vector2 c, float r, int n = 20, float squash = 1f)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2f / n; pts[i] = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * squash); }
            return pts;
        }

        // ------------------------------------------------------------------ textures

        /// <summary>Terracotta quarry tiles: each tile its own shade, cream grout, a few flecks.</summary>
        static Texture2D FloorTexture() => StageLook.Paint(Art + "T_KL_Floor.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            int tx = x / 128, ty = y / 128, lx = x % 128, ly = y % 128;
            float edge = Mathf.Min(Mathf.Min(lx, 127 - lx), Mathf.Min(ly, 127 - ly));
            Color tile = Color.Lerp(new Color(0.74f, 0.40f, 0.26f), new Color(0.66f, 0.34f, 0.22f), Hash(tx, ty, 1));
            tile *= 0.92f + 0.12f * Mathf.PerlinNoise(x * 0.05f + tx * 9f, y * 0.05f + ty * 7f);
            if (Hash(x, y, 2) > 0.985f) tile *= 0.8f;
            Color c = Color.Lerp(new Color(0.80f, 0.74f, 0.62f), tile, StageLook.Smooth(2f, 4f, edge));
            return c;
        });

        /// <summary>Cream subway tiles behind the counter (the concept frame's wall): two across, four staggered rows.</summary>
        static Texture2D SubwayTexture() => StageLook.Paint(Art + "T_KL_Subway.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float rows = v * 4f;
            int row = Mathf.FloorToInt(rows);
            float fu = Mathf.Repeat(u * 2f + (row % 2) * 0.5f, 1f), fv = rows - row;
            float edge = Mathf.Min(Mathf.Min(fu, 1f - fu) * 2f, Mathf.Min(fv, 1f - fv));
            Color tile = Cream * Mathf.Lerp(0.9f, 1.03f, StageLook.Smooth(0.02f, 0.1f, edge)) * (1f + 0.04f * fv);
            return Color.Lerp(Grout, tile, StageLook.Smooth(0.012f, 0.024f, edge));
        });

        /// <summary>Striped cream wallpaper with a tiny sprig in the light stripes. One tile = 16 units.</summary>
        static Texture2D WallpaperTexture() => StageLook.Paint(Art + "T_KL_Wallpaper.asset", 128, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float s = Mathf.Repeat(u * 4f, 1f);
            bool light = s < 0.5f;
            Color c = light ? new Color(0.93f, 0.86f, 0.70f) : new Color(0.89f, 0.81f, 0.64f);
            c = Color.Lerp(c, new Color(0.86f, 0.77f, 0.60f), 1f - StageLook.Smooth(0f, 0.04f, Mathf.Min(Mathf.Abs(s - 0.5f), Mathf.Min(s, 1f - s))));
            if (light)
            {
                var sprig = new Vector2(Mathf.Repeat(u * 4f, 1f) - 0.25f, Mathf.Repeat(v * 4f + (Mathf.Floor(u * 4f) % 2) * 0.5f, 1f) - 0.5f);
                if (sprig.magnitude < 0.06f) c = Color.Lerp(c, new Color(0.55f, 0.62f, 0.45f), 0.7f);
            }
            return c;
        });

        /// <summary>Patchwork for Blue's fort: blue and cream squares, some with a white star, stitched seams.</summary>
        static Texture2D QuiltTexture() => StageLook.Paint(Art + "T_KL_Quilt.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            int px = x / 64, py = y / 64;
            float lx = (x % 64) / 64f - 0.5f, ly = (y % 64) / 64f - 0.5f;
            float pick = Hash(px, py, 3);
            Color c = pick < 0.3f ? Blue : pick < 0.55f ? new Color(0.40f, 0.56f, 0.80f) : pick < 0.8f ? new Color(0.93f, 0.89f, 0.80f) : new Color(0.17f, 0.30f, 0.55f);
            // A five-point star on the darker patches.
            if (pick < 0.3f || pick >= 0.8f)
            {
                float ang = Mathf.Atan2(ly, lx), r = new Vector2(lx, ly).magnitude;
                float star = 0.16f + 0.08f * Mathf.Cos(ang * 5f + Mathf.PI * 0.5f);
                if (r < star) c = new Color(0.97f, 0.96f, 0.92f);
            }
            float edge = Mathf.Min(0.5f - Mathf.Abs(lx), 0.5f - Mathf.Abs(ly));
            if (edge < 0.035f) c *= 0.82f;
            if (edge > 0.05f && edge < 0.065f && ((x + y) % 6 < 3)) c = Color.Lerp(c, new Color(0.95f, 0.93f, 0.85f), 0.6f);   // stitches
            return c;
        });

        /// <summary>Mustard gingham for the curtains.</summary>
        static Texture2D GinghamTexture() => StageLook.Paint(Art + "T_KL_Gingham.asset", 64, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float a = (Mathf.Repeat(u * 2f, 1f) < 0.5f ? 0.5f : 0f) + (Mathf.Repeat(v * 2f, 1f) < 0.5f ? 0.5f : 0f);
            return Color.Lerp(new Color(0.97f, 0.94f, 0.86f), Mustard, a);
        });

        /// <summary>Theo's flag: crayon on paper, a blue border and a blue paper plane.</summary>
        static Texture2D FlagTexture() => StageLook.Paint(Art + "T_KL_Flag.asset", 128, TextureWrapMode.Clamp, (u, v, x, y) =>
            Crayon(new Color(0.97f, 0.96f, 0.92f), u, v, x, y, new[]
            {
                new Stroke(Blue, 0.05f, V(0.06f, 0.08f), V(0.94f, 0.1f), V(0.93f, 0.9f), V(0.07f, 0.92f), V(0.06f, 0.08f)),
                new Stroke(Blue, 0.045f, V(0.22f, 0.35f), V(0.8f, 0.62f), V(0.34f, 0.72f), V(0.22f, 0.35f)),
                new Stroke(Blue, 0.035f, V(0.8f, 0.62f), V(0.42f, 0.5f)),
            }));

        /// <summary>The fridge maps: 0 Theo's battle plan, 1 a drawing of Biscuit with a warning, 2 the Golden Remote ("the prize").</summary>
        static Texture2D MapTexture(int kind) => StageLook.Paint(Art + $"T_KL_Map{kind}.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            Color paper = new Color(0.97f, 0.95f, 0.89f);
            if (Mathf.Repeat(v * 14f, 1f) < 0.04f) paper = Color.Lerp(paper, new Color(0.6f, 0.75f, 0.9f), 0.5f);   // ruled lines
            if (Mathf.Abs(u - 0.1f) < 0.004f) paper = Color.Lerp(paper, new Color(0.9f, 0.45f, 0.45f), 0.6f);           // margin
            Color grey = new Color(0.25f, 0.25f, 0.27f), gold = new Color(0.92f, 0.72f, 0.18f);
            Stroke[] s;
            switch (kind)
            {
                case 0:   // the counter, the fort, attack arrows, red X's at the hall door
                    s = new[]
                    {
                        new Stroke(Blue, 0.025f, V(0.18f, 0.78f), V(0.85f, 0.78f), V(0.85f, 0.68f), V(0.18f, 0.68f), V(0.18f, 0.78f)),
                        new Stroke(Blue, 0.025f, V(0.3f, 0.2f), V(0.45f, 0.42f), V(0.6f, 0.2f), V(0.3f, 0.2f)),
                        new Stroke(Blue, 0.02f, V(0.47f, 0.44f), V(0.52f, 0.58f), V(0.58f, 0.64f)),
                        new Stroke(Blue, 0.02f, V(0.54f, 0.58f), V(0.58f, 0.64f), V(0.6f, 0.57f)),
                        new Stroke(Blue, 0.02f, V(0.4f, 0.44f), V(0.3f, 0.56f), V(0.32f, 0.65f)),
                        new Stroke(Blue, 0.02f, V(0.27f, 0.6f), V(0.32f, 0.65f), V(0.36f, 0.6f)),
                        new Stroke(Red, 0.025f, V(0.75f, 0.15f), V(0.85f, 0.27f)),
                        new Stroke(Red, 0.025f, V(0.85f, 0.15f), V(0.75f, 0.27f)),
                        new Stroke(Red, 0.025f, V(0.66f, 0.4f), V(0.74f, 0.5f)),
                        new Stroke(Red, 0.025f, V(0.74f, 0.4f), V(0.66f, 0.5f)),
                    };
                    break;
                case 1:   // Biscuit: round head, ears, eyes, whiskers, and a big red "!"
                    var head = Circle(V(0.45f, 0.48f), 0.22f, 24, 0.9f);
                    s = new[]
                    {
                        new Stroke(grey, 0.03f, head),
                        new Stroke(grey, 0.03f, V(0.28f, 0.6f), V(0.3f, 0.82f), V(0.42f, 0.67f)),
                        new Stroke(grey, 0.03f, V(0.48f, 0.67f), V(0.6f, 0.82f), V(0.62f, 0.6f)),
                        new Stroke(new Color(0.6f, 0.75f, 0.1f), 0.04f, V(0.37f, 0.53f), V(0.39f, 0.53f)),
                        new Stroke(new Color(0.6f, 0.75f, 0.1f), 0.04f, V(0.51f, 0.53f), V(0.53f, 0.53f)),
                        new Stroke(grey, 0.015f, V(0.3f, 0.44f), V(0.12f, 0.47f)),
                        new Stroke(grey, 0.015f, V(0.3f, 0.41f), V(0.13f, 0.38f)),
                        new Stroke(grey, 0.015f, V(0.6f, 0.44f), V(0.78f, 0.47f)),
                        new Stroke(grey, 0.015f, V(0.6f, 0.41f), V(0.77f, 0.38f)),
                        new Stroke(Red, 0.04f, V(0.85f, 0.85f), V(0.85f, 0.3f)),
                        new Stroke(Red, 0.05f, V(0.85f, 0.18f), V(0.85f, 0.19f)),
                    };
                    break;
                default:  // the Golden Remote with rays round it
                    s = new[]
                    {
                        new Stroke(gold, 0.06f, V(0.42f, 0.25f), V(0.42f, 0.7f)),
                        new Stroke(gold, 0.06f, V(0.58f, 0.25f), V(0.58f, 0.7f)),
                        new Stroke(gold, 0.06f, V(0.42f, 0.25f), V(0.58f, 0.25f)),
                        new Stroke(gold, 0.06f, V(0.42f, 0.7f), V(0.58f, 0.7f)),
                        new Stroke(Red, 0.03f, V(0.5f, 0.6f), V(0.5f, 0.61f)),
                        new Stroke(grey, 0.025f, V(0.47f, 0.48f), V(0.47f, 0.49f)),
                        new Stroke(grey, 0.025f, V(0.53f, 0.48f), V(0.53f, 0.49f)),
                        new Stroke(grey, 0.025f, V(0.47f, 0.38f), V(0.47f, 0.39f)),
                        new Stroke(grey, 0.025f, V(0.53f, 0.38f), V(0.53f, 0.39f)),
                        new Stroke(gold, 0.02f, V(0.5f, 0.78f), V(0.5f, 0.9f)),
                        new Stroke(gold, 0.02f, V(0.3f, 0.7f), V(0.2f, 0.8f)),
                        new Stroke(gold, 0.02f, V(0.7f, 0.7f), V(0.8f, 0.8f)),
                        new Stroke(gold, 0.02f, V(0.3f, 0.3f), V(0.18f, 0.25f)),
                        new Stroke(gold, 0.02f, V(0.7f, 0.3f), V(0.82f, 0.25f)),
                    };
                    break;
            }
            return Crayon(paper, u, v, x, y, s);
        });

        /// <summary>Cereal box art, no words: 0 Sunny O's (the concept frame's yellow box), 1 Choco Bombs, 2 Frosty Flakes.</summary>
        static Texture2D CerealTexture(int kind) => StageLook.Paint(Art + $"T_KL_Cereal{kind}.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            Color body = kind == 0 ? Mustard : kind == 1 ? Red : Blue;
            Color band = kind == 0 ? Red : kind == 1 ? Mustard : new Color(0.95f, 0.95f, 0.98f);
            Color c = body;
            if (v > 0.8f) c = band;
            // A wiggly "title" line in the band (reads as lettering from a distance, says nothing).
            if (v > 0.84f && v < 0.94f && Mathf.Abs(Mathf.Sin(u * 40f) * 0.025f + 0.89f - v) < 0.012f && u > 0.12f && u < 0.88f)
                c = kind == 2 ? Blue : Color.white;
            // The bowl.
            var p = new Vector2((u - 0.5f) * 0.45f, v - 0.42f);
            float bowl = p.magnitude;
            if (bowl < 0.17f)
            {
                c = new Color(0.98f, 0.95f, 0.85f);
                if (p.y > -0.02f && p.y < 0.08f)
                {
                    int cx = (int)(u * 30f), cy = (int)(v * 60f);
                    if (Hash(cx, cy, kind) > 0.45f)
                        c = kind == 0 ? new Color(0.95f, 0.6f, 0.15f) : kind == 1 ? new Color(0.35f, 0.2f, 0.12f) : new Color(0.97f, 0.97f, 1f);
                }
            }
            if (Mathf.Abs(bowl - 0.17f) < 0.008f) c *= 0.75f;
            // Choco Bombs: a starburst; Frosty: a snowflake.
            var q = new Vector2((u - 0.75f) * 0.45f, v - 0.68f);
            if (kind == 1 && q.magnitude < 0.06f + 0.025f * Mathf.Cos(Mathf.Atan2(q.y, q.x) * 8f)) c = Mustard;
            if (kind == 2)
            {
                float ang = Mathf.Repeat(Mathf.Atan2(q.y, q.x), Mathf.PI / 3f) - Mathf.PI / 6f;
                if (q.magnitude < 0.08f && Mathf.Abs(Mathf.Sin(ang) * q.magnitude) < 0.006f) c = Color.white;
            }
            return c;
        });

        /// <summary>The garden at night: sky, moon, a tree, the fence and the house next door with one window lit.</summary>
        static Texture2D GardenTexture() => StageLook.Paint(Art + "T_KL_Garden.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            Color c = Color.Lerp(new Color(0.12f, 0.18f, 0.34f), new Color(0.03f, 0.04f, 0.12f), v);
            float moon = Vector2.Distance(new Vector2(u, v), new Vector2(0.72f, 0.78f));
            c = Color.Lerp(new Color(1f, 0.96f, 0.85f), c, StageLook.Smooth(0.06f, 0.07f, moon));
            c += new Color(0.08f, 0.1f, 0.18f) * (1f - StageLook.Smooth(0.07f, 0.2f, moon));
            Color dark = new Color(0.03f, 0.04f, 0.07f);
            // The house next door: a block with a pitched roof, one warm window.
            float roof = 0.5f - Mathf.Abs(u - 0.62f) * 0.5f;
            if (u > 0.4f && u < 0.84f && v < Mathf.Min(0.45f, roof + 0.07f))
            {
                c = dark;
                if (u > 0.55f && u < 0.63f && v > 0.24f && v < 0.33f) c = new Color(1f, 0.75f, 0.4f);
            }
            // The tree: a lumpy crown on a trunk.
            float crown = Vector2.Distance(new Vector2(u * 1.3f, v), new Vector2(0.22f * 1.3f, 0.55f)) - 0.2f * (0.75f + 0.25f * Mathf.PerlinNoise(u * 12f, v * 12f));
            if (crown < 0f || (Mathf.Abs(u - 0.22f) < 0.02f && v < 0.4f)) c = dark;
            // The fence.
            if (v < 0.18f && Mathf.Repeat(u * 22f, 1f) < 0.8f && v < 0.16f + 0.02f * (1f - Mathf.Abs(Mathf.Repeat(u * 22f, 1f) * 2f - 0.8f))) c = new Color(0.06f, 0.06f, 0.09f);
            return c;
        });

        /// <summary>The hall through the open door: dark, with the living-room doorway glowing blue with TV light at the far end.</summary>
        static Texture2D HallwayTexture() => StageLook.Paint(Art + "T_KL_Hallway.asset", 128, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            Color c = new Color(0.06f, 0.05f, 0.05f);
            if (v < 0.12f) c = Color.Lerp(new Color(0.18f, 0.2f, 0.3f), c, v / 0.12f);   // TV light along the floor
            if (u > 0.55f && u < 0.85f && v > 0.05f && v < 0.75f) c = new Color(0.25f, 0.32f, 0.55f) * (0.8f + 0.2f * v);   // the far doorway
            if (u > 0.62f && u < 0.66f && v > 0.05f && v < 0.38f) c = new Color(0.12f, 0.1f, 0.1f);                          // armchair edge
            return c;
        });

        /// <summary>A striped runner rug: cream, blue and mustard bands, fringe at the ends.</summary>
        static Texture2D RugTexture() => StageLook.Paint(Art + "T_KL_Rug.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            if (u < 0.03f || u > 0.97f) return (y % 4 < 2) ? new Color(0.92f, 0.88f, 0.78f) : new Color(0.75f, 0.42f, 0.27f);   // fringe
            float b = Mathf.Repeat(u * 9f, 1f);
            Color c = b < 0.5f ? new Color(0.93f, 0.89f, 0.80f) : b < 0.75f ? Blue : Mustard;
            if (v < 0.08f || v > 0.92f) c = Blue;
            return c * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.6f, y * 0.6f));
        });

        /// <summary>A wall calendar: a photo of a paper plane over the month, Friday circled in red.</summary>
        static Texture2D CalendarTexture() => StageLook.Paint(Art + "T_KL_Calendar.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            Color c = new Color(0.97f, 0.96f, 0.92f);
            if (v > 0.58f && v < 0.95f && u > 0.06f && u < 0.94f)
            {
                c = Color.Lerp(new Color(0.55f, 0.75f, 0.95f), new Color(0.85f, 0.92f, 1f), (v - 0.58f) / 0.37f);
                var p = new Vector2(u, v);
                if (Mathf.Abs(SegDist(p, V(0.35f, 0.72f), V(0.68f, 0.82f))) < 0.02f || (u > 0.38f && u < 0.66f && Mathf.Abs(v - (0.72f + (u - 0.35f) * 0.3f)) < 0.04f)) c = Color.white;
            }
            if (v < 0.52f && v > 0.05f && u > 0.06f && u < 0.94f)
            {
                float cu = (u - 0.06f) / 0.88f * 7f, cv = (v - 0.05f) / 0.47f * 5f;
                if (Mathf.Repeat(cu, 1f) < 0.04f || Mathf.Repeat(cv, 1f) < 0.05f) c = new Color(0.7f, 0.7f, 0.72f);
                var friday = new Vector2((Mathf.Floor(4f) + 0.5f - cu) / 7f * 1.8f, (2.5f - cv) / 5f);
                if (Mathf.Abs(friday.magnitude - 0.055f) < 0.008f) c = Red;
            }
            return c;
        });

        // ------------------------------------------------------------------ set dressing

        /// <summary>
        /// Theo's crayon maps stuck to the fridge door with magnets. They hang on the door's hinge, so
        /// they swing open with it. Old ones are cleared first (they live under Stage 37's hinge, not this pass's group).
        /// </summary>
        static void WarMaps(Transform hinge, Pal pal)
        {
            if (hinge == null) return;
            for (int i = hinge.childCount - 1; i >= 0; i--)
            {
                var c = hinge.GetChild(i);
                if (c.name == "WarMap" || c.name == "Magnet") Object.DestroyImmediate(c.gameObject);
            }
            const float face = 88.3f;   // the door's front (Stage 37: centre z 89.2, 1.6 thick)
            var maps = new (float x, float y, float w, float h, float tilt, int kind)[]
            {
                (FridgeX + 6f, 66f, 18f, 22f, -4f, 0),
                (FridgeX + 12f, 38f, 15f, 15f, 6f, 1),
                (FridgeX - 6f, 24f, 13f, 16f, -8f, 2),
            };
            for (int i = 0; i < maps.Length; i++)
            {
                var m = maps[i];
                var q = StageKit.Quad("WarMap", hinge, A(m.x, m.y, face - 0.05f * (i + 1)), Vector3.back, m.w, m.h, pal.maps[m.kind]);
                q.transform.rotation = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0f, 0f, m.tilt);
                var top = q.transform.position + q.transform.up * (m.h * 0.5f - 1.5f);
                var magnet = StageKit.Cyl("Magnet", hinge, top + Vector3.back * 0.5f, new Vector3(2.4f, 0.8f, 2.4f), pal.magnets[i % pal.magnets.Length], collider: false, euler: new Vector3(90f, 0f, 0f));
                magnet.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>The night garden through the west window, gingham café curtains on a brass rod, herb pots on the sill.</summary>
        static void Window(Transform parent, Pal pal)
        {
            // Window (Stage 37): centre (-W, 70, 10), 64 wide (z) x 40 tall; sill at y 49, 6 deep.
            float x = -W + 1.35f;
            StageKit.Quad("GardenView", parent, A(x, 70f, 10f), Vector3.right, 64f, 40f, pal.nightView);
            foreach (float s in new[] { -1f, 1f })
                StageKit.Box("Curtain", parent, A(-W + 2.6f, 70f, 10f + s * 30f), new Vector3(1.2f, 44f, 16f), pal.curtain, collider: false);
            StageKit.Box("Valance", parent, A(-W + 2.8f, 92f, 10f), new Vector3(1.4f, 8f, 78f), pal.curtain, collider: false);
            StageKit.Cyl("CurtainRod", parent, A(-W + 2.4f, 96.5f, 10f), new Vector3(0.8f, 82f, 0.8f), pal.brass, collider: false, euler: new Vector3(90f, 0f, 0f));
            foreach (float z in new[] { -14f, 28f, 38f })
            {
                StageKit.Cyl("HerbPot", parent, A(-W + 3f, 52f, z), new Vector3(4.4f, 4.4f, 4.4f), pal.terracotta, collider: false);
                for (int k = 0; k < 3; k++)
                {
                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    leaf.name = "Herb";
                    Object.DestroyImmediate(leaf.GetComponent<Collider>());
                    leaf.transform.SetParent(parent, false);
                    leaf.transform.position = A(-W + 3f + (k - 1) * 0.9f, 56f + k * 0.9f, z + ((k * 7) % 3 - 1) * 1.1f);
                    leaf.transform.localScale = new Vector3(3.2f, 3.6f, 3.2f);
                    leaf.GetComponent<Renderer>().sharedMaterial = pal.leaves;
                }
            }
        }

        /// <summary>
        /// The hall door stands ajar: behind it the dark hall, and at its far end the living room,
        /// blue with Grandad's TV. Its light flickers in across the kitchen floor.
        /// </summary>
        static void Hallway(Transform room, Transform parent, Pal pal)
        {
            // Hall door (Stage 37): centre (105, 45, -D + 0.4), 40 wide, knob at x 90, so the hinge is at x 125.
            var hinge = A(125f, 45f, -D + 0.4f);
            var open = Quaternion.Euler(0f, 38f, 0f);   // the free end swings into the room
            var door = FindDeep(room, "HallDoor");
            if (door != null) { door.rotation = open; door.position = hinge + open * new Vector3(-20f, 0f, 0f); }
            var knob = FindDeep(room, "HallDoorKnob");
            if (knob != null) { knob.rotation = open; knob.position = hinge + open * new Vector3(-35f, -1f, 0.6f); }

            StageKit.Quad("HallBeyond", parent, A(105f, 45f, -D + 0.08f), Vector3.forward, 40f, 90f, pal.hallway);
            StageKit.Box("DoorFrameTop", parent, A(105f, 91f, -D + 0.5f), new Vector3(44f, 2f, 1f), pal.white, collider: false);
            foreach (float fx in new[] { 84f, 126f })
                StageKit.Box("DoorFrameSide", parent, A(fx, 45f, -D + 0.5f), new Vector3(2f, 92f, 1f), pal.white, collider: false);

            var glow = StageKit.PointLight("TVGlow", parent, A(105f, 30f, -D + 6f), new Color(0.55f, 0.7f, 1f), 260f, 75f);
            var spill = new GameObject("TVSpill").AddComponent<Light>();
            spill.transform.SetParent(parent, false);
            spill.transform.position = A(108f, 70f, -D + 3f);
            spill.transform.LookAt(A(80f, 0f, -55f));
            spill.type = LightType.Spot;
            spill.color = new Color(0.55f, 0.7f, 1f);
            spill.intensity = 3600f;   // ~95 units to the floor patch
            spill.range = 150f;
            spill.spotAngle = 55f;
            spill.innerSpotAngle = 25f;
            spill.shadows = LightShadows.Soft;

            var flicker = new GameObject("TVFlicker").AddComponent<LightFlicker>();
            flicker.transform.SetParent(parent, false);
            flicker.lights = new[] { glow, spill };
        }

        /// <summary>The robot vacuum asleep in its dock in the south-west corner: the Dreadnought, before it wakes (level 3).</summary>
        static void Vacuum(Transform parent, Pal pal)
        {
            StageKit.Box("Dock", parent, A(-140f, 7f, -D + 4f), new Vector3(16f, 14f, 7f), pal.graphite, collider: false);
            StageKit.Box("DockLight", parent, A(-140f, 11f, -D + 7.6f), new Vector3(3f, 0.8f, 0.3f), pal.ledRed, collider: false);
            StageKit.Cyl("Body", parent, A(-140f, 4.6f, -D + 22f), new Vector3(34f, 8f, 34f), pal.graphite, collider: false);
            StageKit.Cyl("Bumper", parent, A(-140f, 3.2f, -D + 22f), new Vector3(35f, 3f, 35f), pal.rubber, collider: false);
            StageKit.Cyl("Lid", parent, A(-140f, 8.75f, -D + 22f), new Vector3(28f, 0.5f, 28f), pal.ink, collider: false);
            StageKit.Cyl("Sensor", parent, A(-140f, 9.3f, -D + 32f), new Vector3(5f, 1.2f, 5f), pal.rubber, collider: false);
            var eye = StageKit.Cyl("SensorEye", parent, A(-140f, 10f, -D + 32f), new Vector3(2f, 0.4f, 2f), pal.ledRed, collider: false);
            var light = StageKit.PointLight("SensorGlow", parent, A(-140f, 12f, -D + 34f), new Color(1f, 0.2f, 0.15f), 60f, 26f);
            var blink = new GameObject("SleepBlink").AddComponent<Blinker>();
            blink.transform.SetParent(parent, false);
            blink.period = 4f;
            blink.onFraction = 0.55f;
            blink.lights = new[] { light };
            blink.glows = new[] { eye.GetComponent<Renderer>() };
        }

        /// <summary>Blue's fort: pillows at the opening and a torch inside, its beam sweeping out across the floor.</summary>
        static void Fort(Transform parent, Pal pal)
        {
            StageKit.Box("Pillow", parent, T(-16f, 3.6f, -14f), new Vector3(17f, 7f, 12f), pal.pillows[0], collider: false, euler: new Vector3(0f, 18f, 0f));
            StageKit.Box("Pillow", parent, T(4f, 3.6f, -20f), new Vector3(16f, 7f, 11f), pal.pillows[1], collider: false, euler: new Vector3(0f, -12f, 0f));

            var pivot = new GameObject("TorchPivot").transform;
            pivot.SetParent(parent, false);
            pivot.position = T(32f, 3f, 14f);
            pivot.rotation = Quaternion.LookRotation((T(-60f, 0f, -60f) - pivot.position).normalized + Vector3.up * 0.05f);
            var body = StageKit.Cyl("TorchBody", pivot, pivot.position - pivot.forward * 5f, new Vector3(4f, 10f, 4f), StageLook.Mat("M_KL_TorchBlue", Blue, 0.5f), collider: false);
            body.transform.rotation = pivot.rotation * Quaternion.Euler(90f, 0f, 0f);
            var lens = StageKit.Cyl("TorchLens", pivot, pivot.position, new Vector3(4.6f, 0.6f, 4.6f), pal.glow, collider: false);
            lens.transform.rotation = pivot.rotation * Quaternion.Euler(90f, 0f, 0f);

            var beam = new GameObject("TorchBeam").AddComponent<Light>();
            beam.transform.SetParent(pivot, false);
            beam.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.92f, 0.75f);
            beam.intensity = 6000f;
            beam.range = 180f;
            beam.spotAngle = 30f;
            beam.innerSpotAngle = 16f;
            beam.shadows = LightShadows.Soft;
            var sweep = pivot.gameObject.AddComponent<Sweeper>();
            sweep.amplitude = 32f;
            sweep.period = 8f;
        }

        /// <summary>A round kitchen clock on the east wall, at five past seven on a Friday evening.</summary>
        static void Clock(Transform parent, Pal pal)
        {
            var c = A(W - 0.7f, 95f, -30f);
            var faceOn = new Vector3(0f, 0f, 90f);   // a cylinder's axis is Y: tip it to point along X, into the room
            var room = Vector3.left;
            StageKit.Cyl("Rim", parent, c - room * 0.15f, new Vector3(16f, 0.6f, 16f), StageLook.Mat("M_KL_ClockRed", Red, 0.6f), collider: false, euler: faceOn);
            StageKit.Cyl("Face", parent, c + room * 0.15f, new Vector3(14f, 0.4f, 14f), pal.cream, collider: false, euler: faceOn);
            // Seen from the room (looking east), clockwise runs from up toward -z: a rotation of -angle about X.
            Vector3 Dir(float clockwise) => Quaternion.Euler(-clockwise, 0f, 0f) * Vector3.up;
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                StageKit.Box("Mark", parent, c + Dir(a) * 5.8f + room * 0.4f, new Vector3(0.2f, i % 3 == 0 ? 1.6f : 0.8f, 0.5f), pal.ink, collider: false, euler: new Vector3(-a, 0f, 0f));
            }
            float hour = 7f * 30f + 2.5f, minute = 30f;
            StageKit.Box("HourHand", parent, c + Dir(hour) * 2f + room * 0.5f, new Vector3(0.2f, 4f, 0.7f), pal.ink, collider: false, euler: new Vector3(-hour, 0f, 0f));
            StageKit.Box("MinuteHand", parent, c + Dir(minute) * 2.8f + room * 0.6f, new Vector3(0.2f, 5.6f, 0.5f), pal.ink, collider: false, euler: new Vector3(-minute, 0f, 0f));
            StageKit.Cyl("Cap", parent, c + room * 0.7f, new Vector3(1f, 0.3f, 1f), StageLook.Mat("M_KL_ClockRed", Red, 0.6f), collider: false, euler: faceOn);
        }

        /// <summary>The small things: calendar, runner rug at the sink, utensils on a rail, Grandad's cup of tea.</summary>
        static void Details(Transform parent, Pal pal)
        {
            StageKit.Quad("Calendar", parent, A(W - 0.3f, 70f, -2f), Vector3.left, 15f, 21f, pal.paper);
            StageKit.Cyl("CalendarPin", parent, A(W - 0.6f, 80f, -2f), new Vector3(1f, 0.6f, 1f), pal.magnets[0], collider: false, euler: new Vector3(0f, 0f, 90f));

            // The runner rug in front of the sink (a flat quad facing up).
            var rug = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rug.name = "RunnerRug";
            Object.DestroyImmediate(rug.GetComponent<Collider>());
            rug.transform.SetParent(parent, false);
            rug.transform.position = A(-58f, 0.06f, 78f);
            rug.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // a quad faces -Z: tipping it 90 about X makes it face up
            rug.transform.localScale = new Vector3(48f, 17f, 1f);
            rug.GetComponent<Renderer>().sharedMaterial = pal.rug;

            // Utensils hanging from a rail on the tiles over the sink (the middle cupboard's underside is at y 73).
            float z = D - 1.4f;
            StageKit.Cyl("Rail", parent, A(-57f, 64f, z), new Vector3(0.7f, 30f, 0.7f), pal.steel, collider: false, euler: new Vector3(0f, 0f, 90f));
            var woodHandle = StageLook.Mat("M_KL_UtensilWood", new Color(0.62f, 0.44f, 0.28f), 0.4f);
            foreach (var (x, kind) in new[] { (-67f, 0), (-61f, 1), (-54f, 2), (-48f, 0) })
            {
                StageKit.Box("Hook", parent, A(x, 63f, z), new Vector3(0.3f, 1.6f, 0.3f), pal.steel, collider: false);
                StageKit.Box("Handle", parent, A(x, 58.5f, z), new Vector3(0.7f, 8f, 0.5f), kind == 2 ? pal.steel : woodHandle, collider: false);
                if (kind == 0) StageKit.Box("Spatula", parent, A(x, 53f, z), new Vector3(3.2f, 3.6f, 0.3f), pal.steel, collider: false);
                else if (kind == 1) StageKit.Cyl("Ladle", parent, A(x, 53.6f, z - 0.4f), new Vector3(3.6f, 1.6f, 3.6f), pal.steel, collider: false);
                else
                {
                    var whisk = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    whisk.name = "Whisk";
                    Object.DestroyImmediate(whisk.GetComponent<Collider>());
                    whisk.transform.SetParent(parent, false);
                    whisk.transform.position = A(x, 52.5f, z - 0.3f);
                    whisk.transform.localScale = new Vector3(2.8f, 5f, 2.8f);
                    whisk.GetComponent<Renderer>().sharedMaterial = pal.steel;
                }
            }
        }

        // ------------------------------------------------------------------ the fridge

        /// <summary>
        /// The fridge's cold air (Stage 37: a beam spilling down from the open door) now also sinks and
        /// spreads across the floor as fog, with a gentle downdraft over the floor in front of the fridge
        /// while the door is open: flying low there means the fog and the cat at once.
        /// </summary>
        static void FridgeFog(Transform room, Transform parent)
        {
            RoomEvent fridge = null;
            foreach (var ev in room.GetComponentsInChildren<RoomEvent>(true)) if (ev.name == "FridgeDoor") fridge = ev;
            if (fridge == null) return;

            var sink = StageKit.Beam("FloorFogSink", parent, A(FridgeX - 10f, 24f, 45f), Quaternion.Euler(90f, 0f, 0f), new Vector2(110f, 80f), 24f, 4.5f, strength: 0f);
            var fog = StageKit.Emitter(parent, "FloorFog", Vector3.zero, Vector3.zero, StageKit.ShimmerMat, false, 7f, 14f);
            fog.transform.SetPositionAndRotation(A(FridgeX, 3f, 84f), Quaternion.LookRotation(Vector3.back));
            {
                var m = fog.main;
                m.startColor = new Color(0.86f, 0.93f, 1f, 0.22f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                m.startLifetime = 7f;
                m.gravityModifier = 0f;
                m.maxParticles = 600;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var sh = fog.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 55f;
                sh.radius = 14f;
                var em = fog.emission;
                em.rateOverTime = 30f;
                em.enabled = false;
            }

            var air = new List<AirSource>();
            if (fridge.air != null) foreach (var a in fridge.air) if (a != null && !air.Contains(a)) air.Add(a);
            air.Add(sink);
            fridge.air = air.ToArray();
            var loops = new List<ParticleSystem>();
            if (fridge.loops != null) foreach (var l in fridge.loops) if (l != null && !loops.Contains(l)) loops.Add(l);
            loops.Add(fog);
            fridge.loops = loops.ToArray();
            EditorUtility.SetDirty(fridge);
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>
        /// Stage 37's lights were intensity 1.6-3.4: invisible at this scale (see Stage 38's
        /// distance-squared note). Now: a warm ceiling key with shadows, the pendant as a spot over the
        /// table, pools under each run of cupboards, window-shaped moonlight, toaster coils, bounce.
        /// </summary>
        static void Lights(Transform room, Transform parent, Pal pal)
        {
            foreach (var l in room.GetComponentsInChildren<Light>(true))
            {
                switch (l.name)
                {
                    case "CeilingLight":
                        l.color = new Color(1f, 0.88f, 0.70f);
                        l.intensity = 1600f;
                        l.range = 220f;
                        l.shadows = LightShadows.Soft;
                        break;
                    case "PendantLight":
                        l.type = LightType.Spot;
                        l.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                        l.color = new Color(1f, 0.8f, 0.55f);
                        l.intensity = 3400f;   // ~55 units to the table top
                        l.range = 140f;
                        l.spotAngle = 105f;
                        l.innerSpotAngle = 55f;
                        l.shadows = LightShadows.Soft;
                        break;
                    case "UnderCabinetGlow":
                        l.intensity = 130f;
                        l.range = 45f;
                        break;
                    case "MoonThroughWindow":
                        l.intensity = 70f;
                        l.range = 70f;
                        break;
                    case "FridgeLight":   // only on while the fridge door is open (Stage 37)
                        l.intensity = 280f;
                        l.range = 65f;
                        break;
                }
            }

            // A pool of light under each run of upper cupboards (Stage 37 had one weak light for all three).
            foreach (float x in new[] { -136f, 32f })
                StageKit.PointLight("UnderCabinetGlow", parent, A(x, 66f, 100f), new Color(1f, 0.9f, 0.7f), 130f, 45f);

            // Moonlight through the window, shaped by the office's window cookie (two panes and a bar).
            var moon = new GameObject("WindowMoonlight").AddComponent<Light>();
            moon.transform.SetParent(parent, false);
            moon.transform.position = A(-W + 3f, 86f, 10f);
            moon.transform.LookAt(A(-70f, 0f, 0f));
            moon.type = LightType.Spot;
            moon.color = new Color(0.55f, 0.68f, 1f);
            moon.intensity = 7000f;
            moon.range = 220f;
            moon.spotAngle = 62f;
            moon.innerSpotAngle = 44f;
            moon.shadows = LightShadows.None;
            var cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_O_WindowCookie.asset");
            if (cookie != null) moon.cookie = cookie;

            StageKit.PointLight("ToasterCoils", parent, A(-95f, CounterTop + 18f, 106f), new Color(1f, 0.5f, 0.2f), 45f, 22f);
            StageKit.PointLight("FloorBounce", parent, A(0f, 6f, 0f), new Color(1f, 0.7f, 0.5f), 80f, 120f);
            StageKit.PointLight("CeilingBounce", parent, A(0f, H - 12f, 0f), new Color(1f, 0.88f, 0.75f), 160f, 160f);
        }

        // ------------------------------------------------------------------ colour grade

        /// <summary>A LOCAL post-processing volume around the kitchen, like the office's (Stage 38).</summary>
        static void Grade(Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(12f);
            color.saturation.Override(14f);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.30f, 0.42f, 0.62f));      // cool night shadows
            split.highlights.Override(new Color(1f, 0.78f, 0.55f));      // warm lamp-lit highlights
            split.balance.Override(-5f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.96f, 0.99f, 1.08f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));

            var grain = profile.Add<FilmGrain>(true);   // a nod to the newsreel cards that open each level
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.18f);
            grain.response.Override(0.8f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("KitchenGrade");
            go.transform.SetParent(parent, false);
            go.layer = 2;   // Ignore Raycast: the volume's box must never get in the way of aim or camera rays
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
    }
}
