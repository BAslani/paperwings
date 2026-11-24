using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 41: Rooftop look pass, the fourth room in the "animated film" style.
    /// Menu: PaperWings > Room Looks > Rooftop (Stage 41)
    ///
    ///  - Colour: a moonlit blue night with sodium-orange street glow ("M_RT_..." materials): damp
    ///    blue-grey roof, brick parapets with concrete coping, a wooden water tower with iron hoops,
    ///    galvanised metal, billboards with real artwork, warm lit windows across the city.
    ///  - Shape: every plain box in the room gets soft rounded edges (same size and collider).
    ///  - Set: a moon in the sky, an orange haze rising from the streets all round, puddles that
    ///    catch the moonlight, a caged lamp over the stair-hut door, festoon bulbs from the hut to the
    ///    billboard over a couple of folding chairs, blinking red aircraft beacons.
    ///  - Light: the scene's moonlight stays the key; sodium uplight from the streets rims anything
    ///    flying near the edges, the hut lamp pools orange with shadows, the billboards and skylights
    ///    spill colour on the deck.
    ///  - Grade: a rooftop-only volume (cool blue shadows, sodium-orange highlights).
    /// Nothing new has a collider: the roof flies exactly as before. Safe to re-run.
    /// Re-running Stage 25 rebuilds the roof plainly: run this again afterwards.
    /// </summary>
    public static class Stage41Builder
    {
        const string Art = StageKit.Root + "Art/";
        const string MeshPath = Art + "Meshes/RoundedBoxes_Rooftop.asset";
        const string GradePath = Art + "PostFX_RooftopLook.asset";
        const string Prefix = "M_RT_";

        // Rooftop layout (Stage 25), relative to the room's origin.
        const float Half = 150f, ParapetH = 12f, Bounds = 168f;

        static readonly Color Sodium = new Color(1f, 0.58f, 0.24f);
        static readonly Color Moon = new Color(0.62f, 0.72f, 1f);

        static Vector3 origin;
        static Vector3 A(float x, float y, float z) => origin + new Vector3(x, y, z);

        [MenuItem("PaperWings/Room Looks/Rooftop (Stage 41)", priority = 54)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report;
            try { report = Run(scene); }
            finally { EditorUtility.ClearProgressBar(); }
            if (report == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No rooftop. Run Build Stage 25 first.", "OK");
                return;
            }
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 41 built: Rooftop look pass.\n\n" + report +
                "\n\nLook at it in the Game view: Free Flight on the Rooftop.", "Lovely");
        }

        /// <summary>Builds the pass into the open scene (no save, no dialog). Returns a short report, or null if there's no rooftop.</summary>
        internal static string Run(UnityEngine.SceneManagement.Scene scene)
        {
            var room = StageKit.RoomAt(StageKit.Rooftop);
            if (room == null) return null;
            origin = room.transform.position;

            {
                EditorUtility.DisplayProgressBar("PaperWings", "Mixing the night palette...", 0.1f);
                var map = Palette(out var pal);
                foreach (var part in Parts(room.transform)) StageLook.Remap(part, map);
                var hutDoor = FindDeep(room.transform, "HutDoor");
                if (hutDoor != null && hutDoor.TryGetComponent(out Renderer doorRenderer)) doorRenderer.sharedMaterial = pal.doorGreen;

                EditorUtility.DisplayProgressBar("PaperWings", "Dressing the set...", 0.35f);
                var look = StageKit29.FreshGroup(room.transform, "RooftopLook");
                Coping(StageKit.Group(look, "Coping"), pal);
                WaterTower(StageKit.Group(look, "WaterTower"), pal);
                HutLamp(StageKit.Group(look, "HutLamp"), pal);
                Hangout(StageKit.Group(look, "Hangout"), pal);
                Puddles(StageKit.Group(look, "Puddles"), pal);
                Beacons(room.transform, StageKit.Group(look, "Beacons"), pal);

                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.55f);
                var store = new StageLook.MeshStore(MeshPath);
                int rounded = 0;
                foreach (var part in Parts(room.transform)) rounded += StageLook.RoundCubes(part, store, Prefix);
                FixParapetBricks(room.transform, pal);

                // After the rounding: cords and sky planes need no rounded meshes.
                Festoon(StageKit.Group(look, "Festoon"), pal);
                Sky(scene, StageKit.Group(look, "Sky"), pal);

                EditorUtility.DisplayProgressBar("PaperWings", "Lighting...", 0.75f);
                Lights(room.transform, StageKit.Group(look, "Lights"));
                Grade(look);

                return $"- {rounded} boxes now have rounded edges.\n" +
                       "- Night palette: damp roof, brick parapets, wooden water tower, billboard artwork.\n" +
                       "- Moon, street haze, puddles, hut lamp, festoon bulbs and chairs, blinking beacons.\n" +
                       "- Sodium uplight from the streets, hut lamp with shadows, billboard and skylight spill.\n" +
                       "- Rooftop colour grade (its own volume).";
            }
        }

        /// <summary>The roof's groups, minus the boss (the Condenser prefab keeps its own look).</summary>
        static IEnumerable<Transform> Parts(Transform room)
        {
            foreach (Transform child in room)
                if (child.name != "RooftopBoss") yield return child;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ palette

        sealed class Pal
        {
            public Material brick, brickRot, coping, iron, galv, doorGreen, warmGlow, beaconGlow, beaconLens, puddle, canvas, cord, haze, moon;
            public Material[] bulbs;
        }

        static Dictionary<string, Material> Palette(out Pal pal)
        {
            var brickTiling = new Vector2(10f / 16f, 10f / 12f);   // rounded boxes: ~10 units per UV; bricks 8 x 3 units
            pal = new Pal
            {
                brick = StageLook.Mat("M_RT_Brick", Color.white, 0.2f, 0f, BrickTexture(false), brickTiling),
                brickRot = StageLook.Mat("M_RT_BrickSide", Color.white, 0.2f, 0f, BrickTexture(true), new Vector2(brickTiling.y, brickTiling.x)),
                coping = StageLook.Mat("M_RT_Coping", new Color(0.62f, 0.62f, 0.64f), 0.3f),
                iron = StageLook.Mat("M_RT_Iron", new Color(0.10f, 0.11f, 0.13f), 0.45f, 0.6f),
                galv = StageLook.Mat("M_RT_Galvanised", new Color(0.56f, 0.59f, 0.63f), 0.55f, 0.7f),
                doorGreen = StageLook.Mat("M_RT_DoorGreen", new Color(0.14f, 0.30f, 0.24f), 0.45f, 0.3f),
                warmGlow = Emissive("M_RT_SodiumGlow", new Color(1f, 0.75f, 0.45f), new Color(3.2f, 1.7f, 0.6f)),
                beaconGlow = Emissive("M_RT_BeaconGlow", new Color(1f, 0.15f, 0.12f), new Color(4f, 0.3f, 0.25f)),
                beaconLens = StageLook.Mat("M_RT_BeaconLens", new Color(0.35f, 0.05f, 0.05f), 0.8f),
                puddle = StageLook.Mat("M_RT_Puddle", new Color(0.03f, 0.04f, 0.06f), 0.96f),
                canvas = StageLook.Mat("M_RT_Canvas", new Color(0.78f, 0.36f, 0.20f), 0.15f),
                cord = StageLook.Mat("M_RT_Cord", new Color(0.05f, 0.05f, 0.06f), 0.3f),
                haze = Glow("M_RT_StreetHaze", "T_RT_StreetHaze", new Color(1f, 0.55f, 0.25f, 0.2f), (u, v) => Mathf.Pow(1f - v, 1.6f)),
                moon = Glow("M_RT_Moon", "T_RT_Moon", new Color(0.9f, 0.94f, 1f, 1f), MoonAlpha),
                bulbs = new[]
                {
                    Emissive("M_RT_BulbWarm", new Color(1f, 0.88f, 0.65f), new Color(3.4f, 2.4f, 1.1f)),
                    Emissive("M_RT_BulbAmber", new Color(1f, 0.7f, 0.4f), new Color(3.4f, 1.7f, 0.6f)),
                },
            };

            return new Dictionary<string, Material>
            {
                { "M_RoofConcrete", StageLook.Mat("M_RT_Roof", Color.white, 0.3f, 0f, RoofTexture(), new Vector2(12f, 12f)) },
                { "M_Parapet", pal.brick },
                { "M_StairHut", StageLook.Mat("M_RT_Stucco", new Color(0.52f, 0.47f, 0.42f), 0.15f) },
                { "M_RoofMetal", pal.galv },
                { "M_DarkMetal", pal.iron },
                { "M_WaterTank", StageLook.Mat("M_RT_TankWood", Color.white, 0.15f, 0f, StaveTexture(), new Vector2(10f, 1f)) },
                { "M_Building", StageLook.Mat("M_RT_Building", new Color(0.07f, 0.08f, 0.12f), 0.25f) },
                { "M_CityLights", Emissive("M_RT_Windows", new Color(1f, 0.8f, 0.5f), new Color(2.3f, 1.55f, 0.7f)) },
                { "M_Skylight", Emissive("M_RT_Skylight", new Color(0.9f, 0.75f, 0.5f), new Color(1.5f, 1.05f, 0.55f)) },   // lit from the stairs below
                { "M_BillboardA", Billboard("M_RT_BillboardSunrise", SunriseAd(), new Color(1.5f, 1.5f, 1.5f)) },
                { "M_BillboardB", Billboard("M_RT_BillboardSoda", SodaAd(), new Color(1.4f, 1.4f, 1.4f)) },
            };
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = StageKit.Emissive(name, baseColor, emission);
            m.SetFloat("_Smoothness", 0.4f);
            return m;
        }

        /// <summary>An additive, unlit glow (haze, moon) whose alpha is painted by a function.</summary>
        static Material Glow(string name, string texName, Color tint, System.Func<float, float, float> alpha)
        {
            var tex = StageLook.Paint(Art + texName + ".asset", 256, TextureWrapMode.Clamp, (u, v, x, y) => new Color(1f, 1f, 1f, alpha(u, v)));
            var m = Stage2Builder.ParticleMaterial(name, tex, additive: true);
            m.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A moon disc with soft grey maria, and a wide halo round it.</summary>
        static float MoonAlpha(float u, float v)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
            float disc = 1f - StageLook.Smooth(0.19f, 0.2f, r);
            float maria = Mathf.PerlinNoise(u * 9f + 3f, v * 9f + 7f);
            disc *= Mathf.Lerp(0.72f, 1f, StageLook.Smooth(0.35f, 0.6f, maria));
            float halo = Mathf.Exp(-Mathf.Max(0f, r - 0.2f) * 14f) * 0.35f;
            return Mathf.Clamp01(disc + halo * (1f - disc));
        }

        static float Hash(int x, int y, int seed)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Damp roof concrete: cool grey, mottled, joints, darker wet patches.</summary>
        static Texture2D RoofTexture() => StageLook.Paint(Art + "T_RT_Roof.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.6f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.4f;
            Color c = new Color(0.38f, 0.40f, 0.44f) * (0.85f + 0.25f * n);
            float wet = StageLook.Smooth(0.55f, 0.7f, Mathf.PerlinNoise(x * 0.025f + 11f, y * 0.025f + 5f));
            c = Color.Lerp(c, c * 0.62f, wet);
            if (x % 128 < 2 || y % 128 < 2) c *= 0.55f;   // expansion joints
            return c;
        });

        /// <summary>Weathered red-brown brick: four courses, staggered, mortar lines. Rotated for side-on faces.</summary>
        static Texture2D BrickTexture(bool rotated) => StageLook.Paint(Art + (rotated ? "T_RT_BrickSide.asset" : "T_RT_Brick.asset"), 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            if (rotated) { float t = u; u = v; v = t; int ti = x; x = y; y = ti; }
            float rows = v * 4f;
            int row = Mathf.FloorToInt(rows);
            float cols = u * 2f + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(cols);
            float fu = cols - col, fv = rows - row;
            float edge = Mathf.Min(Mathf.Min(fu, 1f - fu) * (8f / 3f), Mathf.Min(fv, 1f - fv));   // bricks are 8 x 3
            Color brick = Color.Lerp(new Color(0.46f, 0.24f, 0.19f), new Color(0.36f, 0.22f, 0.20f), Hash(col, row, 4));
            brick *= 0.9f + 0.15f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
            return Color.Lerp(new Color(0.42f, 0.41f, 0.40f), brick, StageLook.Smooth(0.04f, 0.07f, edge));
        });

        /// <summary>Water-tank staves: four boards per tile, dark gaps, grain, darker and greener toward the bottom.</summary>
        static Texture2D StaveTexture() => StageLook.Paint(Art + "T_RT_Staves.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float s = u * 4f;
            int board = Mathf.FloorToInt(s);
            float f = s - board;
            Color wood = new Color(0.45f, 0.30f, 0.19f) * (0.85f + 0.2f * Hash(board, 0, 6));
            wood *= 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.6f, y * 0.04f + board * 10f);   // grain runs up the board
            wood = Color.Lerp(new Color(0.22f, 0.24f, 0.18f), wood, StageLook.Smooth(0f, 0.45f, v));   // weathered foot
            return Color.Lerp(new Color(0.08f, 0.06f, 0.05f), wood, StageLook.Smooth(0.02f, 0.06f, Mathf.Min(f, 1f - f)));
        });

        /// <summary>A billboard: its artwork is both colour and glow. Tiled once across a 52 x 36 rounded panel.</summary>
        static Material Billboard(string name, Texture2D tex, Color glow)
        {
            var m = StageLook.Mat(name, Color.white, 0.4f, 0f, tex, new Vector2(10f / 52f, 10f / 36f));
            m.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0.5f));   // rounded-box UVs are centred on the box
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", glow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Billboard A: a sunrise, and a paper plane flying out of it. No words.</summary>
        static Texture2D SunriseAd() => StageLook.Paint(Art + "T_RT_AdSunrise.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            float frame = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (frame < 0.03f) return new Color(0.95f, 0.92f, 0.85f);
            Color c = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(0.55f, 0.15f, 0.35f), v);
            float sun = Vector2.Distance(new Vector2(u * 1.44f, v), new Vector2(0.38f * 1.44f, 0.3f));
            if (sun < 0.2f) c = Color.Lerp(new Color(1f, 0.92f, 0.5f), new Color(1f, 0.75f, 0.3f), sun / 0.2f);
            if (v < 0.22f) c = (Mathf.Repeat(v * 30f, 1f) < 0.5f) ? new Color(0.35f, 0.1f, 0.25f) : c * 0.8f;   // sea stripes
            var p = new Vector2(u, v);
            Vector2 nose = new Vector2(0.86f, 0.72f), tip = new Vector2(0.48f, 0.86f), tail = new Vector2(0.55f, 0.6f), keel = new Vector2(0.6f, 0.5f);
            if (InTriangle(p, nose, tip, tail)) return new Color(1f, 0.98f, 0.94f);
            if (InTriangle(p, nose, tail, keel)) return new Color(0.82f, 0.8f, 0.78f);
            return c;
        });

        /// <summary>Billboard B: a big cold soda can shape in bubbles on blue. No words.</summary>
        static Texture2D SodaAd() => StageLook.Paint(Art + "T_RT_AdSoda.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            float frame = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (frame < 0.03f) return new Color(0.95f, 0.92f, 0.85f);
            Color c = Color.Lerp(new Color(0.1f, 0.45f, 0.95f), new Color(0.05f, 0.15f, 0.45f), v);
            // Bubbles.
            int cx = x / 32, cy = y / 32;
            var centre = new Vector2(cx * 32 + 8 + Hash(cx, cy, 1) * 16, cy * 32 + 8 + Hash(cx, cy, 2) * 16);
            float r = 3f + Hash(cx, cy, 3) * 7f, d = Vector2.Distance(new Vector2(x, y), centre);
            if (Mathf.Abs(d - r) < 1.2f) c = Color.Lerp(c, new Color(0.7f, 0.95f, 1f), 0.8f);
            // The can, standing right of centre, tilted a little.
            float cu = (u - 0.68f) * 1.44f + (v - 0.5f) * 0.12f, cv = v - 0.5f;
            if (Mathf.Abs(cu) < 0.13f && Mathf.Abs(cv) < 0.34f)
            {
                c = Color.Lerp(new Color(0.95f, 0.15f, 0.2f), new Color(1f, 0.45f, 0.4f), 1f - Mathf.Abs(cu) / 0.13f);
                if (Mathf.Abs(cv) > 0.3f) c = new Color(0.75f, 0.78f, 0.82f);                               // lid and base
                if (Mathf.Abs(cv - 0.02f) < 0.05f) c = new Color(1f, 0.97f, 0.9f);                           // label band
            }
            // A white wave across the bottom.
            if (Mathf.Abs(v - (0.18f + 0.05f * Mathf.Sin(u * 12f))) < 0.025f) c = Color.white;
            return c;
        });

        // ------------------------------------------------------------------ set dressing

        /// <summary>Concrete coping along the parapet tops.</summary>
        static void Coping(Transform parent, Pal pal)
        {
            float y = ParapetH + 0.4f;
            StageKit.Box("Coping", parent, A(0f, y, Half), new Vector3(Half * 2f + 6f, 0.8f, 5.4f), pal.coping, collider: false);
            StageKit.Box("Coping", parent, A(0f, y, -Half), new Vector3(Half * 2f + 6f, 0.8f, 5.4f), pal.coping, collider: false);
            StageKit.Box("Coping", parent, A(Half, y, 0f), new Vector3(5.4f, 0.8f, Half * 2f - 5f), pal.coping, collider: false);
            StageKit.Box("Coping", parent, A(-Half, y, 0f), new Vector3(5.4f, 0.8f, Half * 2f - 5f), pal.coping, collider: false);
        }

        /// <summary>Iron hoops round the tank, a stepped conical roof with a finial, and a ladder up one leg.</summary>
        static void WaterTower(Transform parent, Pal pal)
        {
            // Tank (Stage 25): centre (110, 66, -110), 40 wide, 32 tall; roof cap at 84.
            foreach (float y in new[] { 53f, 62f, 71f, 79f })
                StageKit.Cyl("Hoop", parent, A(110f, y, -110f), new Vector3(40.8f, 0.8f, 40.8f), pal.iron, collider: false);
            StageKit.Cyl("RoofStep1", parent, A(110f, 87f, -110f), new Vector3(22f, 2.5f, 22f), pal.iron, collider: false);
            StageKit.Cyl("RoofStep2", parent, A(110f, 89.2f, -110f), new Vector3(13f, 2f, 13f), pal.iron, collider: false);
            StageKit.Cyl("Finial", parent, A(110f, 91.5f, -110f), new Vector3(1.2f, 3f, 1.2f), pal.iron, collider: false);
            // Ladder up the south-west leg to the tank.
            var leg = A(97f, 0f, -97f) + new Vector3(-1.6f, 0f, 1.6f);
            foreach (float s in new[] { -1.4f, 1.4f })
                StageKit.Box("LadderRail", parent, leg + new Vector3(s * 0.7f, 25f, s * 0.7f), new Vector3(0.35f, 50f, 0.35f), pal.galv, collider: false, euler: new Vector3(0f, 45f, 0f));
            for (float y = 3f; y < 50f; y += 3f)
                StageKit.Box("Rung", parent, leg + new Vector3(0f, y, 0f), new Vector3(2.8f, 0.25f, 0.25f), pal.galv, collider: false, euler: new Vector3(0f, -45f, 0f));
        }

        /// <summary>A caged bulkhead lamp over the stair-hut door. The hut's old lamp light becomes its sodium spot.</summary>
        static void HutLamp(Transform parent, Pal pal)
        {
            // Hut door (Stage 25): centre (-110, 22, 89.9), the hut's south face is at z = 90.
            var c = A(-110f, 48f, 88.6f);
            StageKit.Box("Backplate", parent, c + new Vector3(0f, 0f, 1f), new Vector3(4f, 4f, 0.6f), pal.iron, collider: false);
            StageKit.Cyl("Glass", parent, c - new Vector3(0f, 0f, 0.6f), new Vector3(2.8f, 2.4f, 2.8f), pal.warmGlow, collider: false, euler: new Vector3(90f, 0f, 0f));
            foreach (float a in new[] { 0f, 60f, 120f })
                StageKit.Box("Cage", parent, c - new Vector3(0f, 0f, 0.6f), new Vector3(0.2f, 3.4f, 2.6f), pal.iron, collider: false, euler: new Vector3(0f, 0f, a));
            StageKit.Box("Hood", parent, c + new Vector3(0f, 2.2f, -0.6f), new Vector3(4.5f, 0.4f, 3.6f), pal.iron, collider: false);
        }

        /// <summary>Somebody's break spot: two folding chairs and a cool box near the stair hut.</summary>
        static void Hangout(Transform parent, Pal pal)
        {
            void Chair(Vector3 at, float yaw)
            {
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 P(float x, float y, float z) => at + rot * new Vector3(x, y, z);
                StageKit.Box("Seat", parent, P(0f, 5f, 0f), new Vector3(7f, 0.5f, 6f), pal.canvas, collider: false, euler: new Vector3(-6f, yaw, 0f));
                StageKit.Box("Back", parent, P(0f, 9.5f, -3.4f), new Vector3(7f, 8f, 0.5f), pal.canvas, collider: false, euler: new Vector3(-12f, yaw, 0f));
                foreach (float s in new[] { -3.4f, 3.4f })
                {
                    StageKit.Box("Leg", parent, P(s, 4.5f, 0f), new Vector3(0.4f, 9.5f, 0.4f), pal.galv, collider: false, euler: new Vector3(35f, yaw, 0f));
                    StageKit.Box("Leg", parent, P(s, 4.5f, 0f), new Vector3(0.4f, 9.5f, 0.4f), pal.galv, collider: false, euler: new Vector3(-35f, yaw, 0f));
                }
            }
            Chair(A(-80f, 0f, 70f), 200f);
            Chair(A(-66f, 0f, 74f), 160f);
            StageKit.Box("CoolBox", parent, A(-73f, 3f, 62f), new Vector3(8f, 6f, 5f), StageLook.Mat("M_RT_CoolBox", new Color(0.2f, 0.45f, 0.75f), 0.45f), collider: false);
            StageKit.Box("CoolBoxLid", parent, A(-73f, 6.3f, 62f), new Vector3(8.4f, 0.8f, 5.4f), pal.coping, collider: false);
        }

        /// <summary>Shallow puddles: near-black and mirror-smooth, so they catch the moon and the billboards.</summary>
        static void Puddles(Transform parent, Pal pal)
        {
            foreach (var (x, z, w, d, yaw) in new[] { (-20f, 20f, 26f, 14f, 20f), (50f, 80f, 18f, 10f, -30f), (-90f, -40f, 30f, 12f, 70f), (80f, -20f, 16f, 22f, 10f), (10f, -70f, 22f, 12f, -15f) })
            {
                var p = StageKit.Cyl("Puddle", parent, A(x, 0.06f, z), new Vector3(w, 0.04f, d), pal.puddle, collider: false, euler: new Vector3(0f, yaw, 0f));
                p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// Red aircraft-warning beacons: the mast's (Stage 25) now blinks, and the water tower and the
        /// billboard ends get their own, each on its own rhythm.
        /// </summary>
        static void Beacons(Transform room, Transform parent, Pal pal)
        {
            var mastGlow = FindDeep(room, "Beacon");
            var mastLight = FindDeep(room, "BeaconLight");
            if (mastGlow != null)
            {
                StageKit.Cyl("MastLens", parent, mastGlow.position, new Vector3(2.7f, 1.8f, 2.7f), pal.beaconLens, collider: false);
                Blink(parent, "MastBlinker", mastLight != null ? mastLight.GetComponent<Light>() : null, mastGlow.GetComponent<Renderer>(), 1.8f, 0f);
                if (mastLight != null && mastLight.TryGetComponent(out Light ml)) { ml.intensity = 400f; ml.range = 60f; }
            }
            foreach (var (pos, phase) in new[] { (A(110f, 94f, -110f), 0.35f), (A(-60f, 66f, 100f), 0.6f), (A(60f, 66f, 100f), 0.85f) })
            {
                StageKit.Cyl("BeaconLens", parent, pos, new Vector3(1.8f, 1.2f, 1.8f), pal.beaconLens, collider: false);
                var glow = StageKit.Cyl("BeaconGlow", parent, pos, new Vector3(2f, 1.4f, 2f), pal.beaconGlow, collider: false);
                var light = StageKit.PointLight("BeaconLight", parent, pos + Vector3.up * 1.5f, new Color(1f, 0.15f, 0.1f), 250f, 45f);
                Blink(parent, "Blinker", light, glow.GetComponent<Renderer>(), 1.8f, phase);
            }
        }

        static void Blink(Transform parent, string name, Light light, Renderer glow, float period, float phase)
        {
            var b = new GameObject(name).AddComponent<Blinker>();
            b.transform.SetParent(parent, false);
            b.period = period;
            b.onFraction = 0.28f;
            b.phase = phase;
            b.lights = light != null ? new[] { light } : new Light[0];
            b.glows = glow != null ? new[] { glow } : new Renderer[0];
        }

        /// <summary>
        /// The rounded boxes map a box's side faces (the ones facing east or west) with their texture
        /// turned a quarter: the east and west parapets get the turned brick so their courses lie flat.
        /// </summary>
        static void FixParapetBricks(Transform room, Pal pal)
        {
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "Parapet") continue;
                var s = r.transform.lossyScale;
                r.sharedMaterial = Mathf.Abs(s.z) > Mathf.Abs(s.x) ? pal.brickRot : pal.brick;
            }
        }

        /// <summary>Festoon bulbs from the stair hut's corner down to the billboard's first post, over the chairs.</summary>
        static void Festoon(Transform parent, Pal pal)
        {
            Vector3 from = A(-90.5f, 47f, 90.5f), to = A(-50f, 27.5f, 101.5f);
            const int count = 11;
            const float sag = 6f;
            Vector3 prev = from;
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                var p = Vector3.Lerp(from, to, t) + Vector3.down * sag * Mathf.Sin(t * Mathf.PI);
                if (i > 0)
                {
                    var cord = StageKit.Box("Cord", parent, (prev + p) * 0.5f, new Vector3(0.15f, 0.15f, Vector3.Distance(prev, p)), pal.cord, collider: false);
                    cord.transform.rotation = Quaternion.LookRotation(p - prev);
                    cord.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                if (i > 0 && i < count)
                {
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bulb.name = "Bulb";
                    Object.DestroyImmediate(bulb.GetComponent<Collider>());
                    bulb.transform.SetParent(parent, false);
                    bulb.transform.position = p + Vector3.down * 1f;
                    bulb.transform.localScale = new Vector3(1.3f, 1.7f, 1.3f);
                    var r = bulb.GetComponent<Renderer>();
                    r.sharedMaterial = pal.bulbs[i % pal.bulbs.Length];
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                prev = p;
            }
            StageKit.PointLight("FestoonGlow", parent, A(-72f, 30f, 82f), new Color(1f, 0.75f, 0.45f), 220f, 50f);
        }

        /// <summary>
        /// The sky: the camera clears to a flat colour and the fog swallows anything past ~450 units,
        /// so there's no skybox to paint. Instead: a moon (placed where the scene's moonlight comes
        /// from, clamped to a nice height) and a ring of orange street haze rising behind the parapets.
        /// Both sit inside the fog's reach, beyond the invisible walls.
        /// </summary>
        static void Sky(UnityEngine.SceneManagement.Scene scene, Transform parent, Pal pal)
        {
            Vector3 toMoon = new Vector3(-0.5f, 0.6f, 0.6f).normalized;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (l.name == "Moonlight" && l.type == LightType.Directional) toMoon = -l.transform.forward;
            var flat = new Vector3(toMoon.x, 0f, toMoon.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            float elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(toMoon.y, -1f, 1f)) * Mathf.Rad2Deg, 22f, 42f);
            float e = elevation * Mathf.Deg2Rad;
            var dir = flat.normalized * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
            var centre = A(0f, 40f, 0f);
            var moonPos = centre + dir * 230f;
            StageKit.Quad("Moon", parent, moonPos, (centre - moonPos).normalized, 70f, 70f, pal.moon);

            const int panels = 24;
            const float radius = 225f, bottom = -90f, top = 75f;
            float width = 2f * radius * Mathf.Tan(Mathf.PI / panels) * 1.08f;
            for (int i = 0; i < panels; i++)
            {
                float a = i * Mathf.PI * 2f / panels;
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var q = StageKit.Quad("StreetHaze", parent, A(0f, (bottom + top) * 0.5f, 0f) + outward * radius, -outward, width, top - bottom, pal.haze);
                q.GetComponent<Renderer>().receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>
        /// The scene's moonlight (a directional light shared by every room) stays the key. Added:
        /// sodium uplight from the streets outside each edge (it lights anything flying near the
        /// parapets from below), the hut lamp as a shadowed sodium spot, and colour spill from the
        /// billboards and skylights. Stage 25's lamps were intensity 2-3: far too dim at this scale.
        /// </summary>
        static void Lights(Transform room, Transform parent)
        {
            var hut = FindDeep(room, "HutLamp");
            if (hut != null && hut.TryGetComponent(out Light hl))
            {
                hl.type = LightType.Spot;
                hl.transform.position = A(-110f, 46.5f, 87.5f);
                hl.transform.LookAt(A(-104f, 0f, 62f));
                hl.color = Sodium;
                hl.intensity = 2600f;
                hl.range = 110f;
                hl.spotAngle = 115f;
                hl.innerSpotAngle = 55f;
                hl.shadows = LightShadows.Soft;
            }

            foreach (var (x, z) in new[] { (0f, Bounds + 18f), (0f, -Bounds - 18f), (Bounds + 18f, 0f), (-Bounds - 18f, 0f) })
                StageKit.PointLight("StreetUplight", parent, A(x, -20f, z), Sodium, 4500f, 240f);

            StageKit.PointLight("BillboardSpillA", parent, A(-34f, 38f, 90f), new Color(1f, 0.6f, 0.35f), 600f, 75f);
            StageKit.PointLight("BillboardSpillB", parent, A(34f, 38f, 90f), new Color(0.4f, 0.65f, 1f), 600f, 75f);
            foreach (var (x, z) in new[] { (-60f, -90f), (20f, 60f) })   // skylights (Stage 25)
                StageKit.PointLight("SkylightGlow", parent, A(x, 6f, z), new Color(1f, 0.78f, 0.5f), 140f, 40f);
            StageKit.PointLight("MoonBounce", parent, A(0f, 140f, 0f), Moon, 900f, 260f);
        }

        // ------------------------------------------------------------------ colour grade

        /// <summary>A LOCAL post-processing volume over the roof, like the office's (Stage 38).</summary>
        static void Grade(Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(14f);
            color.saturation.Override(8f);

            var white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(-14f);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.26f, 0.36f, 0.70f));      // moonlit blue shadows
            split.highlights.Override(new Color(1f, 0.72f, 0.46f));      // sodium-orange highlights
            split.balance.Override(5f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.1f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.85f, 0.75f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("RooftopGrade");
            go.transform.SetParent(parent, false);
            go.layer = 2;   // Ignore Raycast: the volume's box must never get in the way of aim or camera rays
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 110f, 0f);
            box.size = new Vector3(Bounds * 2f + 20f, 280f, Bounds * 2f + 20f);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 5f;
            volume.blendDistance = 20f;
            volume.sharedProfile = profile;
        }
    }
}
