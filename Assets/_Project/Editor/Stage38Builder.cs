using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 38: Office look pass, the first room in the "animated film" style.
    /// Menu: PaperWings > Build Stage 38 (Office Look Pass)
    ///
    ///  - Shape: every plain box in the office gets soft rounded edges (same size and collider).
    ///  - Colour: a mid-century palette (walnut, teal, burnt orange, mustard, cream, olive) on the
    ///    office's own copies of its materials, so the other rooms don't change.
    ///  - Set: walnut wainscot panelling and a chair rail, a patterned rug, light under the door,
    ///    and a banker's lamp on the coworker's desk.
    ///  - Light: moonlight shaped like the windows on the floor (light cookies), a shadowed floor lamp,
    ///    warm bounce lights where the lamps hit the rug and desk.
    ///  - Grade: an office-only post-processing volume (warm highlights, teal shadows, more colour).
    ///  - Project-wide: shadows reach across the whole room (were cut off 50 units from the camera),
    ///    and ambient occlusion is sized for this scale (it was too small to see).
    /// Safe to re-run.
    /// </summary>
    public static class Stage38Builder
    {
        const string Art = StageKit.Root + "Art/";
        const string MeshPath = Art + "Meshes/RoundedBoxes_Office.asset";
        const string GradePath = Art + "PostFX_OfficeLook.asset";

        // Office walls (Stage 22).
        const float Left = -170f, Right = 170f, Back = 90f, Front = -150f;
        const float DoorZ = Front + 30f, DoorHalf = 19f;

        // Palette.
        static readonly Color Walnut = new Color(0.72f, 0.45f, 0.26f);
        static readonly Color Teal = new Color(0.10f, 0.42f, 0.44f);
        static readonly Color Orange = new Color(0.95f, 0.45f, 0.12f);
        static readonly Color Mustard = new Color(0.88f, 0.64f, 0.16f);
        static readonly Color Cream = new Color(0.90f, 0.85f, 0.72f);

        [MenuItem("PaperWings/Build Stage 38 (Office Look Pass)", priority = 150)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;

            try
            {
                EditorUtility.DisplayProgressBar("PaperWings", "Mixing the office palette...", 0.1f);
                var map = Palette(out var pal);
                foreach (var root in OfficeRoots(scene, includeShell: true)) StageLook.Remap(root, map);

                EditorUtility.DisplayProgressBar("PaperWings", "Sanding the edges...", 0.35f);
                var store = new StageLook.MeshStore(MeshPath);
                int rounded = 0;
                foreach (var root in OfficeRoots(scene, includeShell: false)) rounded += StageLook.RoundCubes(root, store, "M_O_");

                EditorUtility.DisplayProgressBar("PaperWings", "Dressing the set...", 0.55f);
                var look = StageKit.FreshRoot(scene, "OfficeLook", Vector3.zero);
                Wainscot(StageKit.Group(look, "Wainscot"), pal);
                CoworkerLamp(StageKit.Group(look, "CoworkerLamp"), pal);
                DoorSpill(StageKit.Group(look, "DoorSpill"), pal);
                rounded += StageLook.RoundCubes(look, store, "M_O_");

                EditorUtility.DisplayProgressBar("PaperWings", "Lighting...", 0.75f);
                Lights(scene, StageKit.Group(look, "Lights"));
                Grade(look);
                ProjectRendering();

                StageKit.Save(scene);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("PaperWings",
                    "Stage 38 built: Office look pass.\n\n" +
                    $"- {rounded} boxes now have rounded edges.\n" +
                    "- Mid-century palette, wainscot, patterned rug, banker's lamp, light under the door.\n" +
                    "- Window-shaped moonlight on the floor, shadowed floor lamp, warm bounce light.\n" +
                    "- Office colour grade (its own volume).\n" +
                    "- All rooms: longer shadow distance and ambient occlusion sized for the scene.\n\n" +
                    "Look at it in the Game view, Ring Run or Free Flight in the Office.", "Lovely");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ roots

        /// <summary>The office's top-level objects. The shell (floor, ceiling) only gets new colours.</summary>
        static IEnumerable<Transform> OfficeRoots(Scene scene, bool includeShell)
        {
            foreach (var name in new[] { "OfficeRoom", "Desk", "Props", "OfficeAir" })
            {
                var t = StageKit.FindRoot(scene, name);
                if (t != null) yield return t;
            }
            if (!includeShell) yield break;
            var floor = StageKit.FindRoot(scene, "Floor");
            if (floor != null) yield return floor;
            var stage5 = StageKit.FindRoot(scene, "Stage5");
            var ceiling = stage5 != null ? stage5.Find("Ceiling") : null;
            if (ceiling != null) yield return ceiling;
        }

        // ------------------------------------------------------------------ palette

        sealed class Pal
        {
            public Material walnut, darkWalnut, wainscot, trim, teal, orange, brass, enamel, lampShade, glow, doorLight, charcoal;
        }

        /// <summary>Office-only copies of the materials ("M_O_..."), keyed by the old material's name.</summary>
        static Dictionary<string, Material> Palette(out Pal pal)
        {
            // Reuse the existing wood grain as it is; only paint it if it's missing.
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "T_WoodGrain.asset");
            if (wood == null) wood = TextureFactory.WoodGrain();
            var carpetTex = CarpetTexture();
            var woodTile = new Vector2(0.6f, 0.6f);   // rounded boxes map ~10 units per tile: ~16 grain lines per 10 units

            pal = new Pal
            {
                walnut = StageLook.Mat("M_O_Walnut", Walnut, 0.45f, 0f, wood, woodTile),
                darkWalnut = StageLook.Mat("M_O_DarkWalnut", new Color(0.42f, 0.25f, 0.14f), 0.5f, 0f, wood, woodTile),
                wainscot = StageLook.Mat("M_O_Wainscot", new Color(0.55f, 0.33f, 0.19f), 0.4f, 0f, wood, new Vector2(10f, 1f)),
                trim = StageLook.Mat("M_O_Trim", new Color(0.22f, 0.13f, 0.08f), 0.45f),
                teal = StageLook.Mat("M_O_Teal", Teal, 0.12f),
                orange = StageLook.Mat("M_O_Orange", Orange, 0.15f),
                brass = StageLook.Mat("M_O_Brass", new Color(0.86f, 0.64f, 0.30f), 0.72f, 0.9f),
                enamel = StageLook.Mat("M_O_Enamel", new Color(0.08f, 0.36f, 0.30f), 0.75f),
                charcoal = StageLook.Mat("M_O_Charcoal", new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.5f),
                lampShade = Emissive("M_O_LampShade", new Color(1f, 0.92f, 0.75f), new Color(2.2f, 1.6f, 0.9f)),
                glow = Emissive("M_O_LampGlow", new Color(1f, 0.9f, 0.7f), new Color(3f, 2.2f, 1.2f)),
                doorLight = Emissive("M_O_DoorLight", new Color(1f, 0.85f, 0.6f), new Color(2.4f, 1.7f, 0.9f)),
            };

            var rug = StageLook.Mat("M_O_Rug", Color.white, 0.05f, 0f, RugTexture(), Vector2.one);

            return new Dictionary<string, Material>
            {
                // Shell
                { "M_OfficeWall", StageLook.Mat("M_O_Wall", new Color(0.60f, 0.62f, 0.53f), 0.1f) },     // warm sage
                { "M_OfficeTrim", pal.trim },
                { "M_Ceiling", StageLook.Mat("M_O_Ceiling", new Color(0.56f, 0.54f, 0.49f), 0.05f) },
                { "M_Carpet", StageLook.Mat("M_O_Carpet", Color.white, 0.05f, 0f, carpetTex, new Vector2(60f, 60f)) },
                { "M_Whiteboard", StageLook.Mat("M_O_Whiteboard", new Color(0.94f, 0.93f, 0.89f), 0.75f) },

                // Wood
                { "M_DoorWood", pal.walnut },
                { "M_DeskWood", StageLook.Mat("M_O_DeskWalnut", new Color(0.56f, 0.34f, 0.2f), 0.5f, 0f, wood, new Vector2(0.6f, 0.6f)) },
                { "M_DarkWood", pal.darkWalnut },

                // Soft furnishings
                { "M_OfficeCouch", pal.teal },
                { "M_Cushion", pal.orange },
                { "M_Rug", rug },
                { "M_PenCup", pal.teal },

                // Metal and plastic: cream "retro office machine" plastic, brass knobs, olive cabinets
                { "M_DarkMetal", pal.charcoal },
                { "M_Plastic", StageLook.Mat("M_O_Cream", new Color(0.86f, 0.81f, 0.70f), 0.4f) },
                { "M_PrinterGrey", StageLook.Mat("M_O_Putty", new Color(0.74f, 0.72f, 0.66f), 0.35f) },
                { "M_BrushedSteel", pal.brass },
                { "M_FilingCabinet", StageLook.Mat("M_O_Olive", new Color(0.40f, 0.47f, 0.33f), 0.5f, 0.25f) },
                { "M_Lamp", pal.enamel },
                { "M_LampShadeWarm", pal.lampShade },

                // Plant
                { "M_PlantPot", StageLook.Mat("M_O_Terracotta", new Color(0.80f, 0.40f, 0.20f), 0.2f) },
                { "M_PlantLeaves", StageLook.Mat("M_O_Leaves", new Color(0.22f, 0.52f, 0.30f), 0.3f) },

                // Books and binders
                { "M_BookRed", StageLook.Mat("M_O_BookRed", new Color(0.78f, 0.20f, 0.14f), 0.3f) },
                { "M_BookBlue", StageLook.Mat("M_O_BookTeal", Teal, 0.3f) },
                { "M_BookGreen", StageLook.Mat("M_O_BookMustard", Mustard, 0.3f) },
                { "M_BookYellow", StageLook.Mat("M_O_BookCream", Cream, 0.3f) },
            };
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = StageKit.Emissive(name, baseColor, emission);
            m.SetFloat("_Smoothness", 0.2f);
            return m;
        }

        /// <summary>A mid-century rug: mustard field, orange and cream border, teal band, a diamond in the middle.</summary>
        static Texture2D RugTexture() => StageLook.Paint(Art + "T_O_Rug.asset", 512, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float diamond = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
            Color c = Mustard;
            c = Color.Lerp(c, Teal, 1f - StageLook.Smooth(0.105f, 0.11f, edge));
            c = Color.Lerp(c, Cream, 1f - StageLook.Smooth(0.075f, 0.08f, edge));
            c = Color.Lerp(c, new Color(0.80f, 0.33f, 0.12f), 1f - StageLook.Smooth(0.06f, 0.065f, edge));
            if (edge > 0.11f)
            {
                float ring = Mathf.Abs(diamond - 0.2f);
                c = Color.Lerp(c, Cream, 1f - StageLook.Smooth(0.008f, 0.012f, ring));
                c = Color.Lerp(c, Orange, 1f - StageLook.Smooth(0.07f, 0.075f, diamond));
            }
            float fibre = 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
            return c * fibre;
        });

        /// <summary>
        /// Office carpet: a warm mid-tone weave. The shared carpet (T_CarpetWeave) is painted at ~20%
        /// brightness with a blue cast, so it swallowed every lamp and read as blue whatever the tint.
        /// </summary>
        static Texture2D CarpetTexture() => StageLook.Paint(Art + "T_O_Carpet.asset", 256, TextureWrapMode.Repeat, (u, v, x, y) =>
        {
            float weave = ((x / 2 + y / 2) % 2 == 0) ? 0.03f : -0.03f;
            float n = Mathf.PerlinNoise(x * 0.9f, y * 0.9f) * 0.06f;
            var baseColor = new Color(0.40f, 0.34f, 0.28f);   // warm taupe
            return baseColor * (0.92f + weave + n);
        });

        /// <summary>A light cookie shaped like the office windows: two panes split by a mullion, soft edges.</summary>
        static Texture2D WindowCookie() => StageLook.Paint(Art + "T_O_WindowCookie.asset", 256, TextureWrapMode.Clamp, (u, v, x, y) =>
        {
            float frame = StageLook.Smooth(0.18f, 0.2f, Mathf.Min(u, 1f - u)) * StageLook.Smooth(0.22f, 0.24f, Mathf.Min(v, 1f - v));
            float mullion = StageLook.Smooth(0.008f, 0.014f, Mathf.Abs(u - 0.5f));
            float k = frame * mullion;
            return new Color(k, k, k, k);
        });

        // ------------------------------------------------------------------ set dressing

        /// <summary>Walnut panelling on the lower walls with a rounded chair rail (decoration only, no colliders).</summary>
        static void Wainscot(Transform parent, Pal pal)
        {
            const float bottom = 4f, top = 34f, inset = 0.07f;
            float yMid = (bottom + top) * 0.5f, h = top - bottom;

            void Run(Vector3 from, Vector3 to, Vector3 inward)
            {
                var mid = (from + to) * 0.5f;
                float len = Vector3.Distance(from, to);
                Panel("Wainscot", parent, new Vector3(mid.x, yMid, mid.z) + inward * inset, inward, len, h, pal.wainscot);
                var along = (to - from).normalized;
                var size = Mathf.Abs(along.x) > 0.5f ? new Vector3(len, 1.6f, 1.4f) : new Vector3(1.4f, 1.6f, len);
                StageKit.Box("ChairRail", parent, new Vector3(mid.x, top + 0.8f, mid.z) + inward * 0.7f, size, pal.trim, collider: false);
            }

            Run(new Vector3(Left, 0f, Back), new Vector3(Right, 0f, Back), Vector3.back);
            Run(new Vector3(Left, 0f, Front), new Vector3(Right, 0f, Front), Vector3.forward);
            Run(new Vector3(Right, 0f, Front), new Vector3(Right, 0f, Back), Vector3.left);
            // The left wall has the door in it: panel either side.
            Run(new Vector3(Left, 0f, Front), new Vector3(Left, 0f, DoorZ - DoorHalf), Vector3.right);
            Run(new Vector3(Left, 0f, DoorZ + DoorHalf), new Vector3(Left, 0f, Back), Vector3.right);
        }

        /// <summary>A green-enamel banker's lamp on the coworker's desk: a second warm pool on the right side of the room.</summary>
        static void CoworkerLamp(Transform lamp, Pal pal)
        {
            lamp.position = new Vector3(88f, 30f, -108f);   // on the coworker's desk top (Stage 22), left of the monitor
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Base", lamp, new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(5f, 0.4f, 5f), pal.brass, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Pole", lamp, new Vector3(0f, 5.4f, 0f), Vector3.zero, new Vector3(0.6f, 5f, 0.6f), pal.brass, keepCollider: true);
            StageKit.Box("Shade", lamp, lamp.position + new Vector3(0f, 11.5f, 0f), new Vector3(11f, 3f, 5f), pal.enamel);
            var glow = Panel("ShadeGlow", lamp, lamp.position + new Vector3(0f, 9.95f, 0f), Vector3.down, 10f, 4f, pal.glow);
            glow.transform.rotation = Quaternion.LookRotation(Vector3.up);   // a quad faces -forward: this faces down

            var light = new GameObject("CoworkerLampLight").AddComponent<Light>();
            light.transform.SetParent(lamp, false);
            light.transform.position = lamp.position + new Vector3(0f, 9.4f, 0f);
            light.transform.LookAt(lamp.position + new Vector3(4f, 0f, 8f));
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.82f, 0.58f);
            light.intensity = 150f;
            light.range = 55f;
            light.spotAngle = 115f;
            light.innerSpotAngle = 60f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>A sliver of warm hallway light under the door, and the glow it throws on the carpet.</summary>
        static void DoorSpill(Transform parent, Pal pal)
        {
            Panel("UnderDoorLight", parent, new Vector3(Left + 0.15f, 0.6f, DoorZ), Vector3.right, 33f, 1.1f, pal.doorLight);
            var l = new GameObject("DoorSpillLight").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = new Vector3(Left + 5f, 2.5f, DoorZ);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.5f);
            l.intensity = 40f;
            l.range = 32f;
            l.shadows = LightShadows.None;
        }

        /// <summary>A flat, collider-free quad facing into the room.</summary>
        static GameObject Panel(string name, Transform parent, Vector3 pos, Vector3 inward, float w, float h, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.LookRotation(-inward);
            q.transform.localScale = new Vector3(w, h, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>
        /// Film lighting is about contrast between pools of warm light and a cool room, plus the soft
        /// "bounce" light that real light picks up from coloured surfaces. Realtime lights don't bounce,
        /// so we place a few faint, shadowless lights where the bounce would come from (what film
        /// lighters call bounce cards).
        ///
        /// UNITY NOTE: point and spot light fades with distance squared, measured in units. This scene
        /// is built at ~2.3 cm per unit, so a lamp 40 units away is "40 m" away to the renderer: the old
        /// intensities (3-14) barely reached the furniture. Rule of thumb: intensity = brightness x distance^2.
        /// </summary>
        static void Lights(Scene scene, Transform parent)
        {
            // Moonlight through each window, shaped by a cookie so the panes and mullion show on the floor.
            var cookie = WindowCookie();
            foreach (float x in new[] { -110f, 0f, 110f })
            {
                var moon = AddLight(parent, "WindowMoonlight", LightType.Spot, new Vector3(x, 84f, Back - 3f), new Color(0.55f, 0.68f, 1f), 9000f, 230f);
                moon.transform.LookAt(new Vector3(x * 1.1f, 0f, Back - 105f));
                moon.spotAngle = 62f;
                moon.innerSpotAngle = 45f;
                moon.cookie = cookie;
            }

            // Bounce cards.
            AddLight(parent, "RugBounce", LightType.Point, new Vector3(-128f, 25f, -80f), new Color(1f, 0.68f, 0.32f), 120f, 70f);
            AddLight(parent, "CeilingBounce", LightType.Point, new Vector3(0f, 105f, -30f), new Color(1f, 0.86f, 0.7f), 150f, 130f);

            // The floor lamp becomes a proper key light for the lounge corner, with soft shadows.
            var office = StageKit.FindRoot(scene, "OfficeRoom");
            var floorLamp = office != null ? office.Find("Furniture/Lounge/FloorLampLight") : null;
            if (floorLamp != null && floorLamp.TryGetComponent(out Light fl))
            {
                fl.color = new Color(1f, 0.78f, 0.5f);
                fl.intensity = 400f;
                fl.range = 95f;
                fl.shadows = LightShadows.Soft;
            }

            // The desk lamp (Stage 3, brightened in Stage 20 to 14): a real pool of light on the desk now.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.name == "DeskLampLight" && l.transform.position.sqrMagnitude < 200f * 200f)   // the office's, not another room's
                {
                    l.color = new Color(1f, 0.8f, 0.55f);
                    l.intensity = 500f;
                    l.range = 140f;
                }
        }

        static Light AddLight(Transform parent, string name, LightType type, Vector3 pos, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ colour grade

        /// <summary>
        /// A LOCAL post-processing volume around the office: it only applies while the camera is in
        /// the office, on top of the global night grade. It leaves exposure alone, so the Brightness
        /// option (which adjusts the global grade's exposure) still works here.
        /// </summary>
        static void Grade(Transform parent)
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath) != null) AssetDatabase.DeleteAsset(GradePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, GradePath);

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(16f);
            color.saturation.Override(14f);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.30f, 0.48f, 0.62f));      // teal shadows
            split.highlights.Override(new Color(1f, 0.78f, 0.52f));      // warm lamp-lit highlights
            split.balance.Override(-5f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.96f, 1f, 1.08f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0f));

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.8f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("OfficeGrade");
            go.transform.SetParent(parent, false);
            go.layer = 2;   // Ignore Raycast: the volume's box must never get in the way of aim or camera rays
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;   // a volume's box only marks where it applies; planes fly through it
            box.center = new Vector3(0f, 62f, (Back + Front) * 0.5f);
            box.size = new Vector3(Right - Left + 10f, 135f, Back - Front + 10f);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 5f;
            volume.blendDistance = 20f;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ project-wide rendering

        /// <summary>
        /// Two URP settings that held every room back:
        ///  - Shadow distance was 50 units, so anything further than a desk-length away had no shadow.
        ///  - Ambient occlusion's radius was 0.3 units (under a centimetre at this scale): invisible.
        /// </summary>
        static void ProjectRendering()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (urp != null)
            {
                urp.shadowDistance = 260f;
                // The lamps' shadows already overflowed the 2048 atlas (Unity was shrinking them): give them room.
                var so = new SerializedObject(urp);
                var atlas = so.FindProperty("m_AdditionalLightsShadowmapResolution");
                if (atlas != null && atlas.intValue < 4096) atlas.intValue = 4096;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urp);
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer != null)
                foreach (var feature in renderer.rendererFeatures)
                {
                    if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    var so = new SerializedObject(feature);
                    void Set(string prop, float value)
                    {
                        var p = so.FindProperty("m_Settings." + prop);
                        if (p != null) p.floatValue = value;
                    }
                    Set("Radius", 3.5f);
                    Set("Intensity", 1.1f);
                    Set("DirectLightingStrength", 0.35f);
                    Set("Falloff", 400f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(feature);
                }
            AssetDatabase.SaveAssets();
        }
    }
}
