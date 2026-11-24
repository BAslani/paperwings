using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 40: Server Room look pass, the third room in the "animated film" style.
    /// Menu: PaperWings > Room Looks > Server Room (Stage 40)
    ///
    ///  - Colour: cyan and magenta tech-noir on the server room's own materials ("M_SR_..."):
    ///    navy-black racks covered in cyan, magenta and white status lights, a dark raised floor with
    ///    perforated tiles, panelled walls, cyan cold-aisle and magenta hot-aisle floor vents.
    ///  - Shape: every plain box in the room gets soft rounded edges (same size and collider).
    ///  - Set: glowing light bars along the rack tops, neon edge lines down the aisles, a haze of
    ///    light hanging in each aisle (fake volumetric light), LED skirting round the walls, a big
    ///    dashboard on the north wall and a scrolling-code screen on the west wall, status lights
    ///    on the cooling units.
    ///  - Light: a cool blue fill from the ceiling, cyan and magenta glow rising out of the aisles,
    ///    screen spill on the walls, a shadowed key light over the Mainframe's arena.
    ///    The new fill lights dim in a BLACKOUT and strobe red in a HALON PURGE like the old ones;
    ///    the aisle and screen glows stay on in a blackout ("only the racks glow").
    ///  - Grade: a server-room-only volume (deep blue shadows, crisp contrast, strong bloom).
    /// Nothing new has a collider: the room flies exactly as before. Safe to re-run.
    /// Re-running Stage 25 rebuilds the room plainly (and Stage 31 its machinery): run this again afterwards.
    /// </summary>
    public static class Stage40Builder
    {
        const string Art = StageKit.Root + "Art/";
        const string MeshPath = Art + "Meshes/RoundedBoxes_ServerRoom.asset";
        const string GradePath = Art + "PostFX_ServerRoomLook.asset";
        const string Prefix = "M_SR_";

        // Server room layout (Stage 25), relative to the room's origin.
        const float W = 130f, D = 100f, H = 110f;
        const float RackX0 = -95f, RackX1 = 40f, RackH = 60f, RackDepth = 12f;
        const float RackMid = (RackX0 + RackX1) * 0.5f, RackLen = RackX1 - RackX0;
        const float AisleW = 28f;                       // 44 - 12 - 4
        static readonly float[] Rows = { -66f, -22f, 22f, 66f };
        static readonly float[] ColdAisles = { -44f, 44f };
        const float HotAisle = 0f;

        // Palette.
        static readonly Color Cyan = new Color(0.20f, 0.85f, 1f);
        static readonly Color Magenta = new Color(1f, 0.22f, 0.70f);
        static readonly Color Navy = new Color(0.05f, 0.06f, 0.10f);

        static Vector3 origin;
        static Vector3 A(float x, float y, float z) => origin + new Vector3(x, y, z);

        [MenuItem("PaperWings/Room Looks/Server Room (Stage 40)", priority = 53)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report;
            try { report = Run(scene); }
            finally { EditorUtility.ClearProgressBar(); }
            if (report == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No server room. Run Build Stage 25 (and 31 for its machinery) first.", "OK");
                return;
            }
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 40 built: Server Room look pass.\n\n" + report +
                "\n\nLook at it in the Game view: Free Flight in the Server Room (and wait for a Blackout).", "Boot it up");
        }

        /// <summary>Builds the pass into the open scene (no save, no dialog). Returns a short report, or null if there's no server room.</summary>
        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            var room = StageKit.RoomAt(StageKit.ServerRoom);
            if (room == null) return null;
            origin = room.transform.position;

            {
                EditorUtility.DisplayProgressBar("PaperWings", "Mixing the tech-noir palette...", 0.1f);
                var map = Palette(out var pal);
                foreach (var part in Parts(room.transform)) StageLook.Remap(part, map);
                WallPanels(room.transform, pal);

                EditorUtility.DisplayProgressBar("PaperWings", "Dressing the set...", 0.35f);
                var look = StageKit29.FreshGroup(room.transform, "ServerRoomLook");
                RackBars(StageKit.Group(look, "RackBars"), pal);
                AisleLines(StageKit.Group(look, "AisleLines"), pal);
                Skirting(StageKit.Group(look, "LedSkirting"), pal);
                CoolingStatus(StageKit.Group(look, "CoolingStatus"), pal);
                var glows = new List<Light>();
                Screens(StageKit.Group(look, "Screens"), pal, glows);

                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.55f);
                var store = new StageLook.MeshStore(MeshPath);
                int rounded = 0;
                foreach (var part in Parts(room.transform)) rounded += StageLook.RoundCubes(part, store, Prefix);

                // The haze is soft transparent planes: added after the rounding (it has no boxes anyway).
                Haze(StageKit.Group(look, "AisleHaze"), pal, glows);

                EditorUtility.DisplayProgressBar("PaperWings", "Lighting...", 0.75f);
                var fills = Lights(room.transform, StageKit.Group(look, "Lights"));
                WireEvents(room.transform, fills);
                Grade(look);

                return $"- {rounded} boxes now have rounded edges.\n" +
                       "- Tech-noir palette: navy racks with cyan/magenta status lights, raised floor, panelled walls.\n" +
                       "- Rack light bars, neon aisle lines, aisle haze, LED skirting, wall dashboard and code screen.\n" +
                       "- Cool blue fill, aisle glows, screen spill, shadowed key over the Mainframe's arena.\n" +
                       "- Server room colour grade (its own volume).";
            }
        }

        /// <summary>The room's groups, minus the boss (the Mainframe prefab keeps its own look).</summary>
        static IEnumerable<Transform> Parts(Transform room)
        {
            foreach (Transform child in room)
                if (child.name != "ServerBoss") yield return child;
        }

        // ------------------------------------------------------------------ palette

        sealed class Pal
        {
            public Material neonCyan, neonMagenta, neonWhite, gunmetal, amber, dashboard, codeScreen, hazeCyan, hazeMagenta;
            public Texture2D wallPanels;
        }

        static Dictionary<string, Material> Palette(out Pal pal)
        {
            pal = new Pal
            {
                neonCyan = Emissive("M_SR_NeonCyan", Cyan, new Color(0.5f, 2.6f, 3.4f)),
                neonMagenta = Emissive("M_SR_NeonMagenta", Magenta, new Color(3.4f, 0.6f, 2.2f)),
                neonWhite = Emissive("M_SR_NeonWhite", new Color(0.9f, 0.95f, 1f), new Color(2.2f, 2.5f, 3f)),
                amber = Emissive("M_SR_Amber", new Color(1f, 0.6f, 0.15f), new Color(3f, 1.4f, 0.2f)),
                gunmetal = StageLook.Mat("M_SR_Gunmetal", new Color(0.22f, 0.25f, 0.31f), 0.55f, 0.6f),
                dashboard = ScreenMaterial("M_SR_Dashboard", DashboardTexture(), new Color(1.3f, 1.3f, 1.3f)),
                codeScreen = ScreenMaterial("M_SR_CodeScreen", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_ScreenCode.asset") ?? DashboardTexture(), new Color(0.6f, 1.6f, 1.9f)),
                hazeCyan = HazeMaterial("M_SR_HazeCyan", new Color(0.35f, 0.9f, 1f, 0.16f)),
                hazeMagenta = HazeMaterial("M_SR_HazeMagenta", new Color(1f, 0.35f, 0.8f, 0.13f)),
                wallPanels = WallTexture(),
            };

            return new Dictionary<string, Material>
            {
                // Shell (the walls get per-size panel materials in WallPanels)
                { "M_ServerFloor", StageLook.Mat("M_SR_Floor", Color.white, 0.55f, 0.2f, FloorTexture(), new Vector2(10.8f, 8.3f)) },   // 12-unit tiles on 260 x 200
                { "M_ServerCeiling", StageLook.Mat("M_SR_Ceiling", new Color(0.07f, 0.08f, 0.11f), 0.2f) },

                // Racks: a new face, tiled for the rounded boxes (~10 units per UV): one texture per 22 x 30 units, like before
                { "M_Rack", RackMaterial() },
                { "M_RackSide", StageLook.Mat("M_SR_RackMetal", new Color(0.05f, 0.055f, 0.08f), 0.55f, 0.6f) },
                { "M_CableTray", StageLook.Mat("M_SR_Tray", new Color(0.32f, 0.35f, 0.42f), 0.5f, 0.7f) },

                // Aisle vents and machines
                { "M_ColdTile", Emissive("M_SR_ColdVent", new Color(0.08f, 0.30f, 0.42f), new Color(0.12f, 0.75f, 1.15f)) },
                { "M_HotTile", Emissive("M_SR_HotVent", new Color(0.40f, 0.08f, 0.26f), new Color(1.05f, 0.14f, 0.6f)) },
                { "M_CoolingUnit", pal.gunmetal },
                { "M_Grille", StageLook.Mat("M_SR_Grille", new Color(0.04f, 0.045f, 0.06f), 0.4f, 0.6f) },
                { "M_ServerStrip", Emissive("M_SR_CeilingStrip", new Color(0.85f, 0.95f, 1f), new Color(1.2f, 1.7f, 2.2f)) },
                { "M_CoolantPipe", Emissive("M_SR_Coolant", new Color(0.15f, 0.6f, 0.85f), new Color(0.3f, 1.6f, 2.4f)) },
                { "M_DarkMetal", StageLook.Mat("M_SR_DarkMetal", new Color(0.08f, 0.09f, 0.12f), 0.5f, 0.5f) },
                { "M_GantryMetal", StageLook.Mat("M_SR_Hazard", new Color(0.95f, 0.70f, 0.08f), 0.5f, 0.5f) },
            };
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = StageKit.Emissive(name, baseColor, emission);
            m.SetFloat("_Smoothness", 0.4f);
            return m;
        }

        /// <summary>A screen: its picture is both the colour and the glow.</summary>
        static Material ScreenMaterial(string name, Texture2D tex, Color glow)
        {
            var m = StageLook.Mat(name, Color.white, 0.85f, 0f, tex, Vector2.one);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// The rack faces: navy server fronts with vents and drive bays (colour) and only their status
        /// lights in the glow map, so the lights bloom and the metal stays dark.
        /// </summary>
        static Material RackMaterial()
        {
            var face = StageLook.Paint(Art + "T_SR_RackFront.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) => RackPixel(x, y, glow: false));
            var glowMap = StageLook.Paint(Art + "T_SR_RackGlow.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) => RackPixel(x, y, glow: true));
            var m = StageLook.Mat("M_SR_Rack", Color.white, 0.5f, 0.3f, face, new Vector2(0.45f, 0.34f));
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", glowMap);
            m.SetTextureScale("_EmissionMap", new Vector2(0.45f, 0.34f));
            m.SetColor("_EmissionColor", new Color(1.8f, 1.8f, 1.8f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color RackPixel(int x, int y, bool glow)
        {
            Color off = glow ? Color.black : Navy;
            int unit = y / 32, uy = y % 32;
            int bay = x / 64, bx = x % 64;   // four servers across, eight units high
            if (uy < 2 || bx < 2) return glow ? Color.black : new Color(0.12f, 0.14f, 0.19f);   // rack rails and unit lines

            float kind = Hash(bay, unit, 1);
            // Status lights in the right of each server.
            if (uy > 12 && uy < 17 && bx > 44 && bx < 62 && (bx - 44) % 6 < 3)
            {
                float h = Hash(bay * 7 + (bx - 44) / 6, unit, 2);
                if (h > 0.35f)
                {
                    Color led = h > 0.93f ? new Color(1f, 0.6f, 0.15f) : h > 0.72f ? Magenta : h > 0.6f ? new Color(0.9f, 0.95f, 1f) : Cyan;
                    return glow ? led : led * 0.8f;
                }
            }
            // Some servers have a thin cyan status bar along the bottom.
            if (kind > 0.7f && uy > 25 && uy < 27 && bx > 6 && bx < 40) return glow ? Cyan * 0.7f : Cyan * 0.5f;
            if (glow) return Color.black;
            // Vents on the left, drive bays in the middle.
            if (bx < 26 && uy > 5 && uy < 26 && uy % 4 < 2) return new Color(0.025f, 0.03f, 0.05f);
            if (kind > 0.4f && bx >= 28 && bx < 42 && uy > 5 && uy < 26 && (bx - 28) % 5 != 0) return new Color(0.09f, 0.10f, 0.14f);
            return off;
        }

        static float Hash(int x, int y, int seed)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Raised floor: dark graphite tiles with bevelled seams; every few tiles a perforated one.</summary>
        static Texture2D FloorTexture() => StageLook.Paint(Art + "T_SR_Floor.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            int tx = x / 128, ty = y / 128;
            int lx = x % 128, ly = y % 128;
            float edge = Mathf.Min(Mathf.Min(lx, 127 - lx), Mathf.Min(ly, 127 - ly));
            Color c = new Color(0.12f, 0.13f, 0.17f) * (0.95f + 0.05f * Mathf.PerlinNoise(x * 0.04f, y * 0.04f));
            if ((tx + ty) % 2 == 0 && lx % 8 < 3 && ly % 8 < 3 && edge > 10) c = new Color(0.04f, 0.045f, 0.06f);   // perforations
            if (edge < 2) c = new Color(0.03f, 0.03f, 0.04f);
            else if (edge < 4) c *= 1.35f;   // bevel catches the light
            return c;
        });

        /// <summary>Wall panels: blue-black, a seam every panel, a thin groove a third of the way up.</summary>
        static Texture2D WallTexture() => StageLook.Paint(Art + "T_SR_WallPanel.asset", 128, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            Color c = new Color(0.09f, 0.10f, 0.15f) * (0.96f + 0.04f * Mathf.PerlinNoise(x * 0.1f, y * 0.1f));
            if (x < 2 || x > 125) return new Color(0.03f, 0.03f, 0.05f);
            if (x < 4 || x > 123) c *= 1.4f;
            if (Mathf.Abs(y - 42) < 1) c = new Color(0.04f, 0.04f, 0.06f);
            return c;
        });

        /// <summary>
        /// A control-room dashboard: six panels of line graphs, bar charts and ring gauges in cyan and
        /// magenta on navy. Drawn for a 3:1 screen.
        /// </summary>
        static Texture2D DashboardTexture() => StageLook.Paint(Art + "T_SR_Dashboard.asset", 512, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            Color bg = new Color(0.02f, 0.03f, 0.07f);
            int px = Mathf.Min(2, (int)(u * 3f)), py = Mathf.Min(1, (int)(v * 2f));
            float lu = u * 3f - px, lv = v * 2f - py;
            float border = Mathf.Min(Mathf.Min(lu, 1f - lu) * 2f, Mathf.Min(lv, 1f - lv));   // panels are 2:1
            if (border < 0.025f) return border > 0.012f ? Cyan * 0.45f : bg;
            Color c = bg;
            if ((Mathf.Repeat(lu * 10f, 1f) < 0.03f) || (Mathf.Repeat(lv * 5f, 1f) < 0.03f)) c += new Color(0.03f, 0.06f, 0.1f);   // grid

            switch ((px + py * 3) % 3)
            {
                case 0:   // line graph
                {
                    float curve = 0.5f + 0.22f * Mathf.Sin(lu * 11f + px * 2f) + 0.08f * Mathf.Sin(lu * 37f + py);
                    float d = Mathf.Abs(lv - curve);
                    if (d < 0.02f) return Cyan * 1.1f;
                    if (lv < curve) c += Cyan * 0.12f * lv;
                    break;
                }
                case 1:   // bar chart
                {
                    int bar = (int)(lu * 9f);
                    float f = lu * 9f - bar;
                    float h = 0.15f + 0.7f * Hash(bar, px + py * 3, 5);
                    if (f > 0.18f && f < 0.82f && lv > 0.1f && lv < h) return lv > h - 0.04f ? Color.white * 0.9f : Magenta * (0.6f + 0.5f * lv);
                    break;
                }
                default:  // ring gauge
                {
                    var p = new Vector2((lu - 0.5f) * 2f, lv - 0.5f);
                    float r = p.magnitude;
                    float ang = Mathf.Repeat(Mathf.Atan2(p.x, p.y) / (2f * Mathf.PI), 1f);
                    if (r > 0.26f && r < 0.36f) return ang < 0.25f + 0.6f * Hash(px, py, 9) ? (py == 0 ? Magenta : Cyan) : new Color(0.08f, 0.1f, 0.16f);
                    if (r < 0.12f) c += Cyan * 0.25f;
                    break;
                }
            }
            return c;
        });

        /// <summary>Soft light hanging in the air: bright at the floor, fading up and at both sides. Two-sided, additive.</summary>
        static Material HazeMaterial(string name, Color tint)
        {
            var tex = StageLook.Paint(Art + "T_SR_Haze.asset", 128, TextureWrapMode.Clamp, (u, v, x, y) =>
            {
                float a = Mathf.Pow(1f - v, 1.8f) * Mathf.Sin(u * Mathf.PI);
                return new Color(1f, 1f, 1f, a);
            });
            var m = Stage2Builder.ParticleMaterial(name, tex, additive: true);
            m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ walls

        /// <summary>Panelled walls: one material per wall size so the panels come out the same width (16 units).</summary>
        static void WallPanels(Transform room, Pal pal)
        {
            foreach (var (name, width) in new[] { ("WestWall", D * 2f), ("EastWall", D * 2f), ("NorthWall", W * 2f), ("SouthWall", W * 2f) })
            {
                var face = room.Find($"Shell/{name}/Face");
                if (face == null || !face.TryGetComponent(out Renderer r)) continue;
                int w = Mathf.RoundToInt(width);
                r.sharedMaterial = StageLook.Mat($"M_SR_Wall_{w}", Color.white, 0.35f, 0.3f, pal.wallPanels, new Vector2(w / 16f, 1f));
            }
        }

        // ------------------------------------------------------------------ set dressing

        /// <summary>A glowing bar along the top of every rack face: cyan on the cold-aisle side, magenta on the hot.</summary>
        static void RackBars(Transform parent, Pal pal)
        {
            foreach (float z in Rows)
                foreach (float side in new[] { -1f, 1f })
                {
                    float faceZ = z + side * (RackDepth * 0.5f + 0.25f);
                    float aisle = z + side * 22f;
                    Material mat = Mathf.Abs(aisle - HotAisle) < 1f ? pal.neonMagenta : Mathf.Abs(Mathf.Abs(aisle) - 44f) < 1f ? pal.neonCyan : null;
                    if (mat == null) continue;   // the outer backs face the walls
                    StageKit.Box("RackBar", parent, A(RackMid, RackH - 1.5f, faceZ), new Vector3(RackLen - 1f, 0.7f, 0.4f), mat, collider: false);
                    StageKit.Box("RackFoot", parent, A(RackMid, 0.9f, faceZ), new Vector3(RackLen - 1f, 0.35f, 0.3f), mat, collider: false);
                }
        }

        /// <summary>Neon lines on the floor along both edges of every aisle.</summary>
        static void AisleLines(Transform parent, Pal pal)
        {
            void Edges(float z, Material mat)
            {
                foreach (float s in new[] { -1f, 1f })
                    StageKit.Box("AisleLine", parent, A(RackMid, 0.12f, z + s * (AisleW * 0.5f + 0.8f)), new Vector3(RackLen, 0.12f, 0.5f), mat, collider: false);
            }
            foreach (float z in ColdAisles) Edges(z, pal.neonCyan);
            Edges(HotAisle, pal.neonMagenta);
        }

        /// <summary>Cyan LED skirting round the bottom of every wall.</summary>
        static void Skirting(Transform parent, Pal pal)
        {
            StageKit.Box("Skirting", parent, A(0f, 1.2f, D - 0.4f), new Vector3(W * 2f, 0.5f, 0.5f), pal.neonCyan, collider: false);
            StageKit.Box("Skirting", parent, A(0f, 1.2f, -D + 0.4f), new Vector3(W * 2f, 0.5f, 0.5f), pal.neonCyan, collider: false);
            StageKit.Box("Skirting", parent, A(-W + 0.4f, 1.2f, 0f), new Vector3(0.5f, 0.5f, D * 2f), pal.neonCyan, collider: false);
            StageKit.Box("Skirting", parent, A(W - 0.4f, 1.2f, 0f), new Vector3(0.5f, 0.5f, D * 2f), pal.neonCyan, collider: false);
        }

        /// <summary>A status panel on each cooling unit: a cyan bar and a row of lights.</summary>
        static void CoolingStatus(Transform parent, Pal pal)
        {
            // Cooling units (Stage 25): (-W + 6, 27.5, ±44), 12 x 55 x 24, so their room face is at x = -W + 12.
            foreach (float z in ColdAisles)
            {
                float x = -W + 12.15f;
                StageKit.Box("StatusBar", parent, A(x, 50f, z), new Vector3(0.3f, 1f, 18f), pal.neonCyan, collider: false);
                for (int i = 0; i < 4; i++)
                    StageKit.Box("StatusLight", parent, A(x, 12f, z - 6f + i * 4f), new Vector3(0.3f, 1.2f, 1.2f), i == 3 ? pal.amber : pal.neonWhite, collider: false);
            }
        }

        /// <summary>The north-wall dashboard and the west-wall code screen, with the light they throw.</summary>
        static void Screens(Transform parent, Pal pal, List<Light> glows)
        {
            // North wall, centred over the racks, under the gantry rail (y 104).
            const float nw = 96f, nh = 32f, ny = 82f;
            StageKit.Box("DashboardBezel", parent, A(RackMid, ny, D - 0.8f), new Vector3(nw + 3f, nh + 3f, 1.2f), pal.gunmetal, collider: false);
            StageKit.Quad("Dashboard", parent, A(RackMid, ny, D - 1.5f), Vector3.back, nw, nh, pal.dashboard);
            glows.Add(StageKit.PointLight("DashboardSpill", parent, A(RackMid, ny - 4f, D - 14f), new Color(0.45f, 0.75f, 1f), 260f, 70f));

            // West wall, between the two cooling units.
            const float ww = 50f, wh = 30f, wy = 70f;
            StageKit.Box("CodeBezel", parent, A(-W + 0.8f, wy, 0f), new Vector3(1.2f, wh + 3f, ww + 3f), pal.gunmetal, collider: false);
            StageKit.Quad("CodeScreen", parent, A(-W + 1.5f, wy, 0f), Vector3.right, ww, wh, pal.codeScreen);
            glows.Add(StageKit.PointLight("CodeSpill", parent, A(-W + 14f, wy - 4f, 0f), new Color(0.4f, 1f, 0.9f), 200f, 60f));
        }

        /// <summary>
        /// Fake volumetric light: a few soft additive planes standing in each aisle, along it and
        /// across it, so from any angle you look through layers of glow rising off the floor vents.
        /// </summary>
        static void Haze(Transform parent, Pal pal, List<Light> glows)
        {
            void Aisle(float z, Material mat, Color glowColor, float height)
            {
                foreach (float dz in new[] { -AisleW * 0.3f, 0f, AisleW * 0.3f })
                    Plane(A(RackMid, height * 0.5f, z + dz), Vector3.forward, RackLen, height, mat);
                for (float x = RackX0 + 12f; x < RackX1; x += 22f)
                    Plane(A(x, height * 0.5f, z), Vector3.right, AisleW, height, mat);
                // The glow coming up out of the vents.
                for (float x = RackX0 + 20f; x < RackX1; x += 45f)
                    glows.Add(StageKit.PointLight("AisleGlow", parent, A(x, 8f, z), glowColor, 160f, 45f));
            }
            void Plane(Vector3 pos, Vector3 facing, float w, float h, Material mat)
            {
                var q = StageKit.Quad("Haze", parent, pos, facing, w, h, mat);
                q.GetComponent<Renderer>().receiveShadows = false;
            }

            foreach (float z in ColdAisles) Aisle(z, pal.hazeCyan, new Color(0.3f, 0.85f, 1f), 46f);
            Aisle(HotAisle, pal.hazeMagenta, new Color(1f, 0.3f, 0.75f), 40f);
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>
        /// Stage 25's lights were intensity 4 at ~100 units: invisible (see Stage 38's distance-squared
        /// note). Now: a cool fill from the ceiling, a key light with shadows over the Mainframe's
        /// arena, and faint bounce. Returns the new FILL lights (the glows are not in this list).
        /// </summary>
        static List<Light> Lights(Transform room, Transform parent)
        {
            foreach (var l in room.GetComponentsInChildren<Light>(true))
            {
                if (l.name != "ServerLight") continue;
                bool arena = l.transform.position.x - origin.x > 80f;
                l.color = arena ? new Color(0.7f, 0.82f, 1f) : new Color(0.55f, 0.72f, 1f);
                l.intensity = arena ? 1800f : 700f;
                l.range = arena ? 160f : 130f;
                if (arena) l.shadows = LightShadows.Soft;
            }

            var fills = new List<Light>
            {
                StageKit.PointLight("CeilingBounce", parent, A(-10f, 100f, 0f), new Color(0.5f, 0.65f, 1f), 160f, 150f),
                StageKit.PointLight("FloorBounce", parent, A(-10f, 5f, 0f), new Color(0.4f, 0.8f, 1f), 70f, 110f),
            };

            // A cool top light on the cable curtain and the Mainframe wall, so the arena reads from across the room.
            var key = new GameObject("ArenaKey").AddComponent<Light>();
            key.transform.SetParent(parent, false);
            key.transform.position = A(70f, H - 4f, 0f);
            key.transform.LookAt(A(105f, 0f, 0f));
            key.type = LightType.Spot;
            key.color = new Color(0.75f, 0.85f, 1f);
            key.intensity = 6000f;   // ~110 units to the floor
            key.range = 200f;
            key.spotAngle = 85f;
            key.innerSpotAngle = 50f;
            key.shadows = LightShadows.Soft;
            fills.Add(key);
            return fills;
        }

        /// <summary>
        /// The Halon Purge and Blackout events (Stage 31) drive a list of lights: add the new fills to
        /// it so they go red or dark with the rest. Lights deleted by a re-run drop out of the list.
        /// </summary>
        static void WireEvents(Transform room, List<Light> fills)
        {
            foreach (var ev in room.GetComponentsInChildren<RoomEvent>(true))
            {
                if (ev.name != "HalonPurge" && ev.name != "Blackout") continue;
                var list = new List<Light>();
                if (ev.lights != null) foreach (var l in ev.lights) if (l != null && !list.Contains(l)) list.Add(l);
                foreach (var l in fills) if (!list.Contains(l)) list.Add(l);
                ev.lights = list.ToArray();
                EditorUtility.SetDirty(ev);
            }
        }

        // ------------------------------------------------------------------ colour grade

        /// <summary>A LOCAL post-processing volume around the server room, like the office's (Stage 38).</summary>
        static void Grade(Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(22f);
            color.saturation.Override(12f);

            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(-12f);
            white.tint.Override(6f);   // a touch toward magenta

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.25f, 0.35f, 0.72f));      // deep blue shadows
            split.highlights.Override(new Color(0.85f, 0.95f, 1f));      // crisp cool highlights
            split.balance.Override(-10f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.94f, 0.98f, 1.12f, 0f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(0.8f, 0.92f, 1f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("ServerRoomGrade");
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
