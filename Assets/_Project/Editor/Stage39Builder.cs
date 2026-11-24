using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 39: Break Room look pass, the second room in the "animated film" style (after the
    /// office in Stage 38). Menu: PaperWings > Room Looks > Break Room (Stage 39)
    ///
    ///  - Colour: a 1950s diner palette (mint, salmon, butter yellow, cherry red, chrome) on the break
    ///    room's own copies of its materials ("M_BR_..."), so the other rooms don't change.
    ///    Cream and mint checkerboard floor, speckled butter-yellow counter tops, pastel balloons.
    ///  - Shape: every plain box in the room gets soft rounded edges (same size and collider).
    ///  - Set: a mint subway-tile band with a chrome rail around the walls, chrome cabinet handles,
    ///    a night window with a city view, a starburst wall clock, a pink neon coffee-mug sign, a
    ///    salmon pendant lamp over the party table, fairy lights strung round the walls, and a
    ///    salmon swing door with a lit porthole and light underneath.
    ///  - Light: the pendant is a key light with soft shadows over the table, window-shaped
    ///    moonlight on the floor, warmer ceiling lights, neon and fairy-light glows, bounce lights.
    ///  - Grade: a break-room-only post-processing volume (peach highlights, mint shadows, pastel colour).
    /// Nothing new has a collider: the room flies exactly as before. Safe to re-run.
    /// Re-running Stage 21/22 rebuilds the room plainly: run this again afterwards.
    /// </summary>
    public static class Stage39Builder
    {
        const string Art = StageKit.Root + "Art/";
        const string MeshPath = Art + "Meshes/RoundedBoxes_BreakRoom.asset";
        const string GradePath = Art + "PostFX_BreakRoomLook.asset";
        const string Prefix = "M_BR_";

        // Break room shell (Stage 21), relative to the room's origin.
        const float Left = -131f, Right = 131f, Back = 42f, Front = -111f, CeilingY = 125f;
        const float DoorX = -70f, DoorHalf = 17f;

        // Palette.
        static readonly Color Mint = new Color(0.55f, 0.82f, 0.72f);
        static readonly Color Salmon = new Color(0.95f, 0.52f, 0.44f);
        static readonly Color Butter = new Color(0.98f, 0.85f, 0.50f);
        static readonly Color Cherry = new Color(0.80f, 0.13f, 0.16f);
        static readonly Color Cream = new Color(0.95f, 0.91f, 0.81f);
        static readonly Color Ink = new Color(0.13f, 0.12f, 0.13f);

        static Vector3 origin;
        static Vector3 A(float x, float y, float z) => origin + new Vector3(x, y, z);

        [MenuItem("PaperWings/Room Looks/Break Room (Stage 39)", priority = 52)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report;
            try { report = Run(scene); }
            finally { EditorUtility.ClearProgressBar(); }
            if (report == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No break room. Run Build Stage 21/22 (and 30 for the party) first.", "OK");
                return;
            }
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 39 built: Break Room look pass.\n\n" + report +
                "\n\nLook at it in the Game view: Free Flight in the Break Room.", "Lovely");
        }

        /// <summary>Builds the pass into the open scene (no save, no dialog). Returns a short report, or null if there's no break room.</summary>
        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            var room = StageKit.RoomAt(StageKit.BreakRoom);
            if (room == null) return null;
            origin = room.transform.position;

            {
                EditorUtility.DisplayProgressBar("PaperWings", "Mixing the diner palette...", 0.1f);
                var map = Palette(out var pal);
                foreach (var part in Parts(room.transform)) StageLook.Remap(part, map);
                var door = room.transform.Find("Shell/Door");
                if (door != null && door.TryGetComponent(out Renderer doorRenderer)) doorRenderer.sharedMaterial = pal.salmon;

                EditorUtility.DisplayProgressBar("PaperWings", "Dressing the set...", 0.35f);
                var look = StageKit29.FreshGroup(room.transform, "BreakRoomLook");
                TileBand(StageKit.Group(look, "TileBand"), pal);
                CabinetHardware(StageKit.Group(look, "CabinetHardware"), pal);
                Window(StageKit.Group(look, "Window"), pal);
                Clock(StageKit.Group(look, "Clock"), pal);
                NeonSign(StageKit.Group(look, "NeonSign"), pal);
                Pendant(StageKit.Group(look, "Pendant"), pal);
                Door(StageKit.Group(look, "Door"), pal);

                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.55f);
                var store = new StageLook.MeshStore(MeshPath);
                int rounded = 0;
                foreach (var part in Parts(room.transform)) rounded += StageLook.RoundCubes(part, store, Prefix);

                // After the rounding: the fairy-light cords are a hundred slightly different lengths,
                // and a rounded mesh for each would bloat the mesh file for no visible gain.
                FairyLights(StageKit.Group(look, "FairyLights"), pal);

                EditorUtility.DisplayProgressBar("PaperWings", "Lighting...", 0.75f);
                Lights(room.transform, StageKit.Group(look, "Lights"));
                Grade(look);

                return $"- {rounded} boxes now have rounded edges.\n" +
                       "- Diner palette: mint, salmon, butter yellow, cherry, chrome; checkerboard floor.\n" +
                       "- Subway tiles, night window, starburst clock, neon mug, pendant lamp, fairy lights, porthole door.\n" +
                       "- Pendant key light with shadows, window moonlight, neon and bounce glows.\n" +
                       "- Break room colour grade (its own volume).";
            }
        }

        /// <summary>The room's groups, minus the boss (it keeps the same look in every room it visits).</summary>
        static IEnumerable<Transform> Parts(Transform room)
        {
            foreach (Transform child in room)
                if (child.name != "BreakRoomBoss") yield return child;
        }

        // ------------------------------------------------------------------ palette

        sealed class Pal
        {
            public Material mint, salmon, cherry, chrome, brass, ink, cream, neonPink, neonMint, warmGlow, nightView;
            public Material[] bulbs;
            public Texture2D subway;
        }

        /// <summary>Break-room-only copies of the materials ("M_BR_..."), keyed by the old material's name.</summary>
        static Dictionary<string, Material> Palette(out Pal pal)
        {
            pal = new Pal
            {
                mint = StageLook.Mat("M_BR_Mint", Mint, 0.45f),
                salmon = StageLook.Mat("M_BR_Salmon", Salmon, 0.6f),
                cherry = StageLook.Mat("M_BR_Cherry", Cherry, 0.7f),
                chrome = StageLook.Mat("M_BR_Chrome", new Color(0.86f, 0.87f, 0.9f), 0.88f, 1f),
                brass = StageLook.Mat("M_BR_Brass", new Color(0.88f, 0.68f, 0.34f), 0.75f, 0.9f),
                ink = StageLook.Mat("M_BR_Ink", Ink, 0.4f),
                cream = StageLook.Mat("M_BR_Cream", Cream, 0.65f),
                neonPink = Emissive("M_BR_NeonPink", new Color(1f, 0.55f, 0.8f), new Color(4.2f, 0.9f, 2.2f)),
                neonMint = Emissive("M_BR_NeonMint", new Color(0.6f, 1f, 0.85f), new Color(0.9f, 3.6f, 2.4f)),
                warmGlow = Emissive("M_BR_WarmGlow", new Color(1f, 0.88f, 0.68f), new Color(2.6f, 1.9f, 1.0f)),
                nightView = NightViewMaterial(),
                subway = SubwayTexture(),
                bulbs = new[]
                {
                    Emissive("M_BR_BulbWarm", new Color(1f, 0.9f, 0.7f), new Color(3.2f, 2.4f, 1.3f)),
                    Emissive("M_BR_BulbPeach", new Color(1f, 0.7f, 0.5f), new Color(3.4f, 1.6f, 0.8f)),
                    Emissive("M_BR_BulbPink", new Color(1f, 0.6f, 0.75f), new Color(3.2f, 1.1f, 1.8f)),
                    Emissive("M_BR_BulbMint", new Color(0.7f, 1f, 0.85f), new Color(1.1f, 2.8f, 1.9f)),
                },
            };

            var formica = StageLook.Mat("M_BR_Formica", Color.white, 0.55f, 0f, FormicaTexture(), new Vector2(0.5f, 0.5f));
            var vinyl = StageLook.Mat("M_BR_Vinyl", new Color(0.70f, 0.13f, 0.15f), 0.55f);

            return new Dictionary<string, Material>
            {
                // Shell
                { "M_Lino", StageLook.Mat("M_BR_Floor", Color.white, 0.35f, 0f, FloorTexture(), new Vector2(16.5f, 9.625f)) },   // ~8-unit checks on the 264 x 154 floor
                { "M_WallTile", StageLook.Mat("M_BR_Wall", new Color(0.96f, 0.88f, 0.72f), 0.1f) },     // butter-cream plaster
                { "M_BreakCeiling", StageLook.Mat("M_BR_Ceiling", new Color(0.93f, 0.89f, 0.82f), 0.05f) },
                { "M_FluorescentPanel", Emissive("M_BR_Panel", new Color(1f, 0.97f, 0.9f), new Color(1.7f, 1.55f, 1.3f)) },   // softer, warmer

                // Kitchen and furniture
                { "M_Cabinet", pal.mint },
                { "M_Laminate", formica },
                { "M_TableTop", formica },
                { "M_BrushedSteel", pal.chrome },
                { "M_Steel", pal.chrome },
                { "M_ApplianceWhite", pal.cream },                 // glossy retro cream appliances
                { "M_ApplianceBlack", pal.ink },
                { "M_DarkMetal", pal.ink },
                { "M_VendingRed", pal.cherry },
                { "M_Chair", pal.salmon },
                { "M_Couch", vinyl },                              // a cherry diner booth

                // The party, in pastels
                { "M_BalloonRed", StageLook.Mat("M_BR_BalloonCherry", new Color(0.95f, 0.30f, 0.33f), 0.8f) },
                { "M_BalloonYellow", StageLook.Mat("M_BR_BalloonButter", new Color(1f, 0.86f, 0.42f), 0.8f) },
                { "M_BalloonBlue", StageLook.Mat("M_BR_BalloonPowder", new Color(0.52f, 0.74f, 0.96f), 0.8f) },
                { "M_BalloonGreen", StageLook.Mat("M_BR_BalloonMint", new Color(0.50f, 0.86f, 0.70f), 0.8f) },
                { "M_BalloonPink", StageLook.Mat("M_BR_BalloonPink", new Color(1f, 0.60f, 0.64f), 0.8f) },
            };
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = StageKit.Emissive(name, baseColor, emission);
            m.SetFloat("_Smoothness", 0.3f);
            return m;
        }

        /// <summary>Repeatable hash noise in 0..1 for painting speckles.</summary>
        static float Hash(int x, int y, int seed)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>A diner floor: cream and mint checks, thin grout, a little wear.</summary>
        static Texture2D FloorTexture() => StageLook.Paint(Art + "T_BR_Floor.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            bool odd = (u < 0.5f) ^ (v < 0.5f);
            Color c = odd ? new Color(0.50f, 0.76f, 0.68f) : new Color(0.94f, 0.90f, 0.81f);
            float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f);
            float grout = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
            c = Color.Lerp(new Color(0.62f, 0.62f, 0.58f), c, StageLook.Smooth(0.004f, 0.012f, grout));
            float wear = 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            return c * wear;
        });

        /// <summary>
        /// Speckled butter-yellow formica (counter and table tops): small mint, salmon and ink flecks
        /// scattered in a jittered grid so the pattern never lines up.
        /// </summary>
        static Texture2D FormicaTexture() => StageLook.Paint(Art + "T_BR_Formica.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            Color c = Butter;
            const int cell = 9;
            int cx = x / cell, cy = y / cell;
            for (int k = 0; k < 2; k++)
            {
                float px = cx * cell + Hash(cx, cy, k * 2) * cell, py = cy * cell + Hash(cx, cy, k * 2 + 1) * cell;
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(px, py));
                float pick = Hash(cx, cy, 7 + k);
                Color fleck = pick < 0.4f ? new Color(0.36f, 0.66f, 0.58f) : pick < 0.75f ? Salmon : Ink;
                c = Color.Lerp(fleck, c, StageLook.Smooth(0.9f, 1.6f, d));
            }
            return c * (0.96f + 0.04f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f));
        });

        /// <summary>Mint subway tiles: two tiles across, four staggered rows, a soft glazed bevel, cream grout.</summary>
        static Texture2D SubwayTexture() => StageLook.Paint(Art + "T_BR_Subway.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float rows = v * 4f;
            int row = Mathf.FloorToInt(rows);
            float fu = Mathf.Repeat(u * 2f + (row % 2) * 0.5f, 1f), fv = rows - row;
            float edge = Mathf.Min(Mathf.Min(fu, 1f - fu) * 2f, Mathf.Min(fv, 1f - fv));   // tiles are twice as wide as tall
            Color tile = Mint * (0.94f + 0.06f * Hash(Mathf.FloorToInt(u * 2f + (row % 2) * 0.5f), row, 3));
            tile *= Mathf.Lerp(0.86f, 1.04f, StageLook.Smooth(0.02f, 0.09f, edge)) * (1f + 0.05f * fv);   // bevel, lit from above
            return Color.Lerp(new Color(0.86f, 0.84f, 0.78f), tile, StageLook.Smooth(0.012f, 0.022f, edge));
        });

        /// <summary>One material per run length, so every wall's tiles come out the same size (6 x 3 units).</summary>
        static Material TileMat(Pal pal, float length, float height)
        {
            int len = Mathf.RoundToInt(length);
            return StageLook.Mat($"M_BR_Tile_{len}", Color.white, 0.6f, 0f, pal.subway, new Vector2(len / 12f, height / 12f));
        }

        /// <summary>The view out of the window: a night sky, a moon, and a skyline with lit windows. It glows.</summary>
        static Material NightViewMaterial()
        {
            var tex = StageLook.Paint(Art + "T_BR_NightView.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
            {
                Color sky = Color.Lerp(new Color(0.10f, 0.16f, 0.32f), new Color(0.02f, 0.03f, 0.10f), v);
                float moon = Vector2.Distance(new Vector2(u, v), new Vector2(0.74f, 0.76f));
                sky = Color.Lerp(new Color(1f, 0.96f, 0.85f), sky, StageLook.Smooth(0.07f, 0.08f, moon));
                sky += new Color(0.10f, 0.12f, 0.2f) * (1f - StageLook.Smooth(0.08f, 0.22f, moon));   // halo

                // Buildings: blocks of random height, each 16-30 pixels wide.
                int b = 0, left = 0;
                while (left + 16 + (int)(Hash(b, 0, 11) * 14) <= x) { left += 16 + (int)(Hash(b, 0, 11) * 14); b++; }
                float top = 0.22f + Hash(b, 1, 12) * 0.34f;
                if (v > top) return sky;
                Color wall = new Color(0.04f, 0.04f, 0.08f);
                int wx = (x - left) / 4, wy = y / 5;
                bool inWindow = (x - left) % 4 >= 1 && (x - left) % 4 <= 2 && y % 5 >= 2 && y % 5 <= 3 && x - left >= 2;
                if (inWindow && Hash(b * 31 + wx, wy, 13) > 0.62f) return new Color(1f, 0.78f, 0.42f) * (0.7f + 0.3f * Hash(wx, wy, 14));
                return wall;
            });
            var m = StageLook.Mat("M_BR_NightView", Color.white, 0.9f, 0f, tex, Vector2.one);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", new Color(1.1f, 1.1f, 1.1f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ set dressing

        /// <summary>Mint subway tiles on the lower walls, a chrome rail on top and a salmon skirting (no colliders).</summary>
        static void TileBand(Transform parent, Pal pal)
        {
            const float top = 32f;

            void Run(Vector3 from, Vector3 to, Vector3 inward)
            {
                var mid = (from + to) * 0.5f;
                float len = Vector3.Distance(from, to);
                bool alongX = Mathf.Abs((to - from).x) > 0.5f;
                StageKit.Quad("Tiles", parent, A(mid.x, top * 0.5f, mid.z) + inward * 0.08f, inward, len, top, TileMat(pal, len, top));
                Vector3 Size(float h, float d) => alongX ? new Vector3(len, h, d) : new Vector3(d, h, len);
                StageKit.Box("ChromeRail", parent, A(mid.x, top + 0.6f, mid.z) + inward * 0.6f, Size(1.3f, 1.2f), pal.chrome, collider: false);
                StageKit.Box("Skirting", parent, A(mid.x, 1.5f, mid.z) + inward * 0.45f, Size(3f, 0.9f), pal.salmon, collider: false);
            }

            Run(new Vector3(Left, 0f, Back), new Vector3(Right, 0f, Back), Vector3.back);
            Run(new Vector3(Left, 0f, Front), new Vector3(Left, 0f, Back), Vector3.right);
            Run(new Vector3(Right, 0f, Front), new Vector3(Right, 0f, Back), Vector3.left);
            // The front wall has the door in it: tiles either side.
            Run(new Vector3(Left, 0f, Front), new Vector3(DoorX - DoorHalf, 0f, Front), Vector3.forward);
            Run(new Vector3(DoorX + DoorHalf, 0f, Front), new Vector3(Right, 0f, Front), Vector3.forward);
        }

        /// <summary>Chrome bar handles and door seams on the counter cupboards, and a chrome edge on the counter top.</summary>
        static void CabinetHardware(Transform parent, Pal pal)
        {
            // Counter base (Stage 21): centre (-20, 14.5, 29), 100 x 29 x 22, so its front is at z = 18.
            const float face = 18f;
            foreach (float x in new[] { -45f, -20f, 5f })
                StageKit.Box("DoorSeam", parent, A(x, 14.5f, face - 0.08f), new Vector3(0.35f, 25f, 0.2f), pal.ink, collider: false);
            float[] doors = { -57.5f, -32.5f, -7.5f, 17.5f };
            for (int i = 0; i < doors.Length; i++)
            {
                float hx = doors[i] + (i % 2 == 0 ? 9f : -9f);   // pairs of doors open from the middle
                StageKit.Box("Handle", parent, A(hx, 22f, face - 0.7f), new Vector3(0.8f, 6f, 0.8f), pal.chrome, collider: false);
            }
            StageKit.Box("CounterEdge", parent, A(-20f, 29.5f, 16.9f), new Vector3(102f, 1.1f, 0.5f), pal.chrome, collider: false);

            // Upper cabinets: centre (-55, 63.5, 37.5), 30 x 17 x 9, front at z = 33.
            StageKit.Box("UpperSeam", parent, A(-55f, 63.5f, 32.92f), new Vector3(0.35f, 15f, 0.2f), pal.ink, collider: false);
            foreach (float x in new[] { -57.5f, -52.5f })
                StageKit.Box("UpperHandle", parent, A(x, 58f, 32.3f), new Vector3(0.8f, 4.5f, 0.8f), pal.chrome, collider: false);
        }

        /// <summary>
        /// A window in the right wall, looking out on the city at night. Moonlight comes in through it,
        /// shaped by the office's window cookie, so its panes show on the floor.
        /// </summary>
        static void Window(Transform parent, Pal pal)
        {
            const float z = -20f, y = 72f, w = 56f, h = 38f;
            float x = Right;
            StageKit.Quad("NightView", parent, A(x - 0.1f, y, z), Vector3.left, w, h, pal.nightView);

            // Chrome frame, a mullion, and a mint sill.
            StageKit.Box("FrameTop", parent, A(x - 0.8f, y + h * 0.5f + 1f, z), new Vector3(1.6f, 2f, w + 4f), pal.chrome, collider: false);
            StageKit.Box("FrameBottom", parent, A(x - 0.8f, y - h * 0.5f - 1f, z), new Vector3(1.6f, 2f, w + 4f), pal.chrome, collider: false);
            StageKit.Box("FrameLeft", parent, A(x - 0.8f, y, z - w * 0.5f - 1f), new Vector3(1.6f, h, 2f), pal.chrome, collider: false);
            StageKit.Box("FrameRight", parent, A(x - 0.8f, y, z + w * 0.5f + 1f), new Vector3(1.6f, h, 2f), pal.chrome, collider: false);
            StageKit.Box("Mullion", parent, A(x - 0.6f, y, z), new Vector3(1.2f, h, 1.2f), pal.chrome, collider: false);
            StageKit.Box("Sill", parent, A(x - 2.2f, y - h * 0.5f - 2.6f, z), new Vector3(4.4f, 1.6f, w + 8f), pal.mint, collider: false);

            var moon = new GameObject("WindowMoonlight").AddComponent<Light>();
            moon.transform.SetParent(parent, false);
            moon.transform.position = A(x - 3f, y + 14f, z);
            moon.transform.LookAt(A(30f, 0f, z - 8f));
            moon.type = LightType.Spot;
            moon.color = new Color(0.55f, 0.68f, 1f);
            moon.intensity = 8000f;   // ~120 units to the floor: intensity = brightness x distance^2 (see Stage 38)
            moon.range = 230f;
            moon.spotAngle = 62f;
            moon.innerSpotAngle = 44f;
            moon.shadows = LightShadows.None;
            var cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_O_WindowCookie.asset");
            if (cookie != null) moon.cookie = cookie;
        }

        /// <summary>A starburst wall clock above the coffee corner (decoration only).</summary>
        static void Clock(Transform parent, Pal pal)
        {
            var c = A(10f, 95f, Back - 0.6f);
            var faceOn = new Vector3(90f, 0f, 0f);   // a cylinder's axis is Y: tip it so the face looks into the room
            StageKit.Cyl("Rim", parent, c + Vector3.forward * 0.15f, new Vector3(15.5f, 0.5f, 15.5f), pal.chrome, collider: false, euler: faceOn);
            StageKit.Cyl("Face", parent, c - Vector3.forward * 0.1f, new Vector3(14f, 0.5f, 14f), pal.cream, collider: false, euler: faceOn);
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f;
                var dir = Quaternion.Euler(0f, 0f, ang) * Vector3.up;
                float len = i % 3 == 0 ? 8f : 5f;
                StageKit.Box("Ray", parent, c + dir * (8f + len * 0.5f) + Vector3.forward * 0.2f, new Vector3(0.7f, len, 0.5f),
                             i % 2 == 0 ? pal.brass : pal.chrome, collider: false, euler: new Vector3(0f, 0f, ang));
                if (i % 3 == 0)
                    StageKit.Box("Mark", parent, c + dir * 5.6f - Vector3.forward * 0.4f, new Vector3(0.6f, 1.4f, 0.2f), pal.ink, collider: false, euler: new Vector3(0f, 0f, ang));
            }
            // Ten past ten, like every clock in a catalogue.
            StageKit.Box("HourHand", parent, c + Quaternion.Euler(0f, 0f, 60f) * Vector3.up * 2f - Vector3.forward * 0.5f, new Vector3(0.6f, 4f, 0.2f), pal.ink, collider: false, euler: new Vector3(0f, 0f, 60f));
            StageKit.Box("MinuteHand", parent, c + Quaternion.Euler(0f, 0f, -60f) * Vector3.up * 2.8f - Vector3.forward * 0.6f, new Vector3(0.45f, 5.6f, 0.2f), pal.ink, collider: false, euler: new Vector3(0f, 0f, -60f));
            StageKit.Cyl("Cap", parent, c - Vector3.forward * 0.7f, new Vector3(0.9f, 0.3f, 0.9f), pal.brass, collider: false, euler: faceOn);
        }

        /// <summary>A pink neon coffee mug with mint neon steam on the left wall, and the glow it throws.</summary>
        static void NeonSign(Transform parent, Pal pal)
        {
            // Lay the sign out flat on the wall: local x runs along the wall, local y up, local -z into the room.
            parent.position = A(Left, 72f, 8f);
            parent.rotation = Quaternion.LookRotation(Vector3.left);   // forward into the wall, so right = world +z
            const float t = 0.7f, off = -0.8f;

            void Tube(Vector2 a, Vector2 b, Material mat)
            {
                var d = b - a;
                var go = StageKit.Box("Neon", parent, Vector3.zero, Vector3.one, mat, collider: false);
                go.transform.localPosition = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, off);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                go.transform.localScale = new Vector3(d.magnitude + t, t, t);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            void Line(Material mat, params Vector2[] pts)
            {
                for (int i = 0; i < pts.Length - 1; i++) Tube(pts[i], pts[i + 1], mat);
            }

            // The mug and its handle.
            Line(pal.neonPink, new Vector2(-6f, 5f), new Vector2(-5f, -7f), new Vector2(5f, -7f), new Vector2(6f, 5f), new Vector2(-6f, 5f));
            Line(pal.neonPink, new Vector2(5.8f, 3f), new Vector2(9.5f, 3f), new Vector2(9.5f, -3f), new Vector2(5.3f, -3f));
            // A saucer.
            Line(pal.neonPink, new Vector2(-9f, -9f), new Vector2(9f, -9f));
            // Three wisps of steam.
            foreach (float sx in new[] { -3f, 0f, 3f })
                Line(pal.neonMint, new Vector2(sx, 7f), new Vector2(sx + 1.2f, 9.5f), new Vector2(sx - 0.6f, 12f), new Vector2(sx + 0.6f, 14.5f));

            StageKit.PointLight("NeonGlow", parent, parent.position + Vector3.right * 7f, new Color(1f, 0.45f, 0.72f), 70f, 42f);
        }

        /// <summary>A salmon enamel pendant over the party table: the room's warm key light.</summary>
        static void Pendant(Transform parent, Pal pal)
        {
            float x = -5f, z = -22f;   // the table (Stage 21)
            StageKit.Cyl("Cord", parent, A(x, (CeilingY + 106f) * 0.5f, z), new Vector3(0.35f, CeilingY - 106f, 0.35f), pal.ink, collider: false);
            StageKit.Cyl("Canopy", parent, A(x, CeilingY - 0.6f, z), new Vector3(4f, 1.2f, 4f), pal.chrome, collider: false);
            StageKit.Cyl("Cap", parent, A(x, 106f, z), new Vector3(3f, 1.6f, 3f), pal.chrome, collider: false);
            StageKit.Cyl("ShadeTop", parent, A(x, 103.8f, z), new Vector3(8f, 2.6f, 8f), pal.salmon, collider: false);
            StageKit.Cyl("Shade", parent, A(x, 101.3f, z), new Vector3(14f, 2.6f, 14f), pal.salmon, collider: false);
            StageKit.Cyl("Glow", parent, A(x, 99.95f, z), new Vector3(12.6f, 0.1f, 12.6f), pal.warmGlow, collider: false);
            foreach (var r in parent.GetComponentsInChildren<Renderer>()) if (r.name == "Glow") r.shadowCastingMode = ShadowCastingMode.Off;

            var key = new GameObject("PendantLight").AddComponent<Light>();
            key.transform.SetParent(parent, false);
            key.transform.position = A(x, 99.2f, z);
            key.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.8f, 0.58f);
            key.intensity = 3800f;   // ~75 units to the table top
            key.range = 140f;
            key.spotAngle = 100f;
            key.innerSpotAngle = 50f;
            key.shadows = LightShadows.Soft;
        }

        /// <summary>The door becomes a salmon diner swing door: a lit porthole, a push plate, light underneath.</summary>
        static void Door(Transform parent, Pal pal)
        {
            float z = Front + 0.35f;
            var faceOn = new Vector3(90f, 0f, 0f);
            StageKit.Cyl("PortholeRim", parent, A(DoorX, 62f, z + 0.1f), new Vector3(11f, 0.4f, 11f), pal.chrome, collider: false, euler: faceOn);
            StageKit.Cyl("Porthole", parent, A(DoorX, 62f, z + 0.2f), new Vector3(9f, 0.4f, 9f), pal.warmGlow, collider: false, euler: faceOn);
            StageKit.Box("PushPlate", parent, A(DoorX + 11f, 44f, z + 0.1f), new Vector3(2f, 9f, 0.3f), pal.chrome, collider: false);
            StageKit.Box("KickPlate", parent, A(DoorX, 4.6f, z + 0.1f), new Vector3(32f, 7f, 0.3f), pal.chrome, collider: false);
            // In front of the door slab (it is 0.2 thick at the wall): the hallway light leaking under it.
            StageKit.Quad("UnderDoorLight", parent, A(DoorX, 0.45f, Front + 0.7f), Vector3.forward, DoorHalf * 2f - 1f, 0.9f, pal.warmGlow);

            StageKit.PointLight("PortholeSpill", parent, A(DoorX, 62f, z + 6f), new Color(1f, 0.78f, 0.5f), 50f, 30f);
            StageKit.PointLight("DoorSpillLight", parent, A(DoorX, 2.5f, Front + 5f), new Color(1f, 0.78f, 0.5f), 40f, 32f);
        }

        /// <summary>Strings of coloured bulbs swagged along the tops of all four walls (no colliders, no shadows).</summary>
        static void FairyLights(Transform parent, Pal pal)
        {
            int n = 0;
            void Swags(Vector3 from, Vector3 to, int swags, Vector3 inward)
            {
                const float top = 113f, sag = 7f, spacing = 7f;
                float len = Vector3.Distance(from, to);
                int count = Mathf.Max(2, Mathf.RoundToInt(len / spacing));
                Vector3 prev = default;
                for (int i = 0; i <= count; i++)
                {
                    float t = (float)i / count;
                    float local = Mathf.Repeat(t * swags, 1f);
                    if (i == count) local = 0f;
                    var flat = Vector3.Lerp(from, to, t) + inward * 1.2f;
                    var p = A(flat.x, top - sag * Mathf.Sin(local * Mathf.PI), flat.z);
                    if (i > 0)
                    {
                        var cord = StageKit.Box("Cord", parent, (prev + p) * 0.5f, new Vector3(0.18f, 0.18f, Vector3.Distance(prev, p)), pal.ink, collider: false);
                        cord.transform.rotation = Quaternion.LookRotation(p - prev);
                        cord.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bulb.name = "Bulb";
                    Object.DestroyImmediate(bulb.GetComponent<Collider>());
                    bulb.transform.SetParent(parent, false);
                    bulb.transform.position = p + Vector3.down * 1f;
                    bulb.transform.localScale = new Vector3(1.4f, 1.8f, 1.4f);
                    var r = bulb.GetComponent<Renderer>();
                    r.sharedMaterial = pal.bulbs[n++ % pal.bulbs.Length];
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    prev = p;
                }
            }

            Swags(new Vector3(Left, 0f, Back), new Vector3(Right, 0f, Back), 4, Vector3.back);
            Swags(new Vector3(Left, 0f, Front), new Vector3(Right, 0f, Front), 4, Vector3.forward);
            Swags(new Vector3(Left, 0f, Back), new Vector3(Left, 0f, Front), 2, Vector3.right);
            Swags(new Vector3(Right, 0f, Back), new Vector3(Right, 0f, Front), 2, Vector3.left);

            // Real bulbs this small give no light worth casting: two faint glows sell it on the walls.
            StageKit.PointLight("FairyGlowBack", parent, A(0f, 104f, Back - 8f), new Color(1f, 0.74f, 0.6f), 160f, 110f);
            StageKit.PointLight("FairyGlowFront", parent, A(0f, 104f, Front + 8f), new Color(1f, 0.7f, 0.66f), 160f, 110f);
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>
        /// The room was lit almost entirely by ambient light: its ceiling lights were intensity 7 at
        /// 115 units up (see the distance-squared note in Stage 38), so they did nothing. Now the warm
        /// pendant is the key, the ceiling lights are a soft warm fill, the window is a cool rim, and
        /// the appliances glow for real.
        /// </summary>
        static void Lights(Transform room, Transform parent)
        {
            foreach (var l in room.GetComponentsInChildren<Light>(true))
            {
                switch (l.name)
                {
                    case "CeilingLight":
                        l.color = new Color(1f, 0.93f, 0.82f);
                        l.intensity = 1100f;
                        l.range = 170f;
                        break;
                    case "VendingGlow":
                        l.color = new Color(0.45f, 0.85f, 1f);
                        l.intensity = 140f;
                        l.range = 45f;
                        break;
                    case "MicrowaveGlow":
                        l.intensity = 50f;
                        l.range = 26f;
                        break;
                    case "CandleGlow":
                        l.intensity = 40f;
                        l.range = 26f;
                        break;
                    case "FridgeLight":   // only on while the fridge door is open (Stage 30)
                        l.intensity = 260f;
                        l.range = 55f;
                        break;
                }
            }

            // Bounce cards: the peach ceiling and the mint floor, as faint shadowless lights.
            StageKit.PointLight("CeilingBounce", parent, A(0f, 112f, -34f), new Color(1f, 0.84f, 0.72f), 150f, 140f);
            StageKit.PointLight("FloorBounce", parent, A(0f, 6f, -34f), new Color(0.72f, 1f, 0.86f), 70f, 100f);
            StageKit.PointLight("CounterBounce", parent, A(-20f, 34f, 12f), new Color(1f, 0.86f, 0.55f), 60f, 45f);
        }

        // ------------------------------------------------------------------ colour grade

        /// <summary>A LOCAL post-processing volume around the break room, like the office's (Stage 38).</summary>
        static void Grade(Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(10f);
            color.saturation.Override(18f);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.36f, 0.58f, 0.56f));      // mint shadows
            split.highlights.Override(new Color(1f, 0.80f, 0.68f));      // peach highlights
            split.balance.Override(0f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.97f, 1.02f, 1.02f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1f, 0.96f, 0f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.86f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.27f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("BreakRoomGrade");
            go.transform.SetParent(parent, false);
            go.layer = 2;   // Ignore Raycast: the volume's box must never get in the way of aim or camera rays
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 62f, (Back + Front) * 0.5f);
            box.size = new Vector3(Right - Left + 10f, 135f, Back - Front + 10f);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 5f;
            volume.blendDistance = 20f;
            volume.sharedProfile = profile;
        }
    }
}
