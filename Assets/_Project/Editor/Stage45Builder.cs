using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 45: Biscuit lives in the kitchen. Menu: PaperWings > Stage Builders > Stage 45 (Biscuit Roams)
    ///
    ///  - The Biscuit prefab gets the rest of her rig wired up (the whole body, the top of the spine,
    ///    neck, jaw, front paws, hind legs), so CatBrain can stand her up, walk, trot, stalk, jump,
    ///    lie down, sleep and stretch, all posed procedurally (her model only has a sitting clip).
    ///  - A WINDOW SEAT under the night window with a gingham cushion: her favourite nap spot, warm
    ///    from the radiator underneath.
    ///  - Her map of the kitchen (CatSpots): floor spots in front of the counter, by the window, by the
    ///    fridge, at the hall door, under the table, beside a chair; perches on the counter, the chair
    ///    and the window seat (jumps). Plus the boxes she must never walk into.
    ///  - She starts each mode asleep on a nap spot. Buzz her and she wakes in stages.
    ///
    /// Needs Stage 37 (kitchen), 43 (its look) and 44 (Biscuit). Safe to re-run.
    /// </summary>
    public static class Stage45Builder
    {
        const string PrefabPath = StageKit.Root + "Prefabs/Biscuit.prefab";

        [MenuItem("PaperWings/Stage Builders/Stage 45 (Biscuit Roams)", priority = 156)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report = Run();
            if (report == null) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 45 built: Biscuit roams the kitchen.\n\n" + report +
                "\n\nFree Flight in the Kitchen: she starts asleep on the window seat. Buzz her to wake her, fly low to get hunted.", "Purr");
        }

        /// <summary>Builds into the open scene (no save, no dialog). Null if something's missing.</summary>
        internal static string Run()
        {
            var room = StageKit.RoomAt(StageKit.Kitchen);
            if (room == null) { EditorUtility.DisplayDialog("PaperWings", "No kitchen. Run Stage 37 first.", "OK"); return null; }
            var cat = room.transform.Find("Biscuit");
            if (cat == null || cat.GetComponent<CatBrain>() == null) { EditorUtility.DisplayDialog("PaperWings", "No Biscuit in the kitchen. Run Stage 44 first.", "OK"); return null; }
            int wired = WirePrefab();
            var o = room.transform.position;
            WindowSeat(room.transform, o);
            var spots = Spots(room.transform, o);
            var keepOut = KeepOut(o);

            var brain = cat.GetComponent<CatBrain>();
            brain.spots = spots;
            brain.keepOut = keepOut;
            brain.startAsleep = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
            EditorUtility.SetDirty(brain);
            return $"- {wired} more of her bones wired up: she stands, walks, trots, stalks, jumps, lies, sleeps and stretches.\n" +
                   $"- {spots.Length} places she knows (3 perches, 2 nap spots), {keepOut.Length} boxes she keeps out of.\n" +
                   "- A window seat with a gingham cushion over the radiator: her favourite nap.";
        }

        // ------------------------------------------------------------------ the prefab

        static int WirePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            int n = 0;
            try
            {
                var brain = root.GetComponent<CatBrain>();
                var all = root.GetComponentsInChildren<Transform>(true);
                Transform B(string prefix)
                {
                    foreach (var t in all) if (t.name.StartsWith(prefix)) { n++; return t; }
                    Debug.LogWarning($"Biscuit: no bone starting '{prefix}'.");
                    return null;
                }
                brain.body = B("j_body_");
                brain.spineTop = B("j_spine_4_");
                brain.neckBase = B("j_neck_base_");
                brain.jaw = B("j_jaw_");
                brain.palmL = B("j_l_palm_"); brain.fingerL = B("j_l_finger_");
                brain.palmR = B("j_r_palm_"); brain.fingerR = B("j_r_finger_");
                brain.femurL = B("j_l_femur_"); brain.kneeL = B("j_l_knee_"); brain.ankleL = B("j_l_ankle_"); brain.ballL = B("j_l_ball_"); brain.toeL = B("j_l_toe_");
                brain.femurR = B("j_r_femur_"); brain.kneeR = B("j_r_knee_"); brain.ankleR = B("j_r_ankle_"); brain.ballR = B("j_r_ball_"); brain.toeR = B("j_r_toe_");
                brain.pose = new CatBrain.PoseTuning();   // the tuned defaults
                brain.awakeFor = 150f;
                brain.napFor = 60f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return n;
        }

        // ------------------------------------------------------------------ the window seat

        static void WindowSeat(Transform room, Vector3 o)
        {
            var old = room.Find("WindowSeat");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var g = StageKit.Group(room, "WindowSeat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_KL_Cupboard.mat")
                       ?? StageKit.Mat("M_KL_Cupboard", new Color(0.55f, 0.68f, 0.78f), 0.3f);
            var cushion = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_KL_Gingham.mat")
                          ?? StageKit.Mat("M_KL_Gingham", new Color(0.95f, 0.8f, 0.4f), 0.1f);
            var trim = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_KL_Cream.mat")
                       ?? StageKit.Mat("M_KL_Cream", new Color(0.96f, 0.92f, 0.84f), 0.5f);
            // A deep board on brackets over the radiator, flush with the sill.
            StageKit.Box("SeatBoard", g, o + new Vector3(-148f, 48.9f, 10f), new Vector3(24f, 2f, 44f), wood);
            StageKit.Box("SeatLip", g, o + new Vector3(-136.4f, 48.2f, 10f), new Vector3(1.2f, 3.4f, 44.4f), trim, collider: false);
            foreach (float z in new[] { -8f, 28f })
                StageKit.Box("Bracket", g, o + new Vector3(-154f, 43f, z), new Vector3(10f, 10f, 1.4f), trim, collider: false, euler: new Vector3(0f, 0f, 0f));
            // The cushion, slightly squashed where she sleeps.
            StageKit.Box("Cushion", g, o + new Vector3(-148f, 51.1f, 10f), new Vector3(19f, 2.4f, 28f), cushion);
            StageKit.Box("CushionPiping", g, o + new Vector3(-148f, 50.0f, 10f), new Vector3(19.6f, 0.6f, 28.6f), trim, collider: false);
            // Round the edges like the rest of the kitchen.
            var store = new StageLook.MeshStore(StageKit.Root + "Art/Meshes/RoundedBoxes_Kitchen.asset");
            StageLook.RoundCubes(g, store, "M_KL_");
        }

        // ------------------------------------------------------------------ her map

        static CatSpot[] Spots(Transform room, Vector3 o)
        {
            var old = room.Find("BiscuitSpots");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var g = StageKit.Group(room, "BiscuitSpots");
            CatSpot S(string name, float x, float y, float z, Vector3 facing, CatSpot.Kind kind, float liking, bool nap = false)
            {
                var go = new GameObject(name);
                go.transform.SetParent(g, false);
                go.transform.position = o + new Vector3(x, y, z);
                go.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                var s = go.AddComponent<CatSpot>();
                s.kind = kind;
                s.liking = liking;
                s.napSpot = nap;
                return s;
            }
            var east = Vector3.right; var west = Vector3.left; var north = Vector3.forward; var south = Vector3.back;
            var F = CatSpot.Kind.Floor; var P = CatSpot.Kind.Perch;

            var counterFront = S("CounterFront", -40f, 0f, 74f, south, F, 1f);
            var counter = S("Counter", -36f, 36f, 101f, south, P, 1.3f);
            var middle = S("Middle", -60f, 0f, 30f, east, F, 0.8f);
            var windowFloor = S("UnderWindow", -116f, 0f, 10f, west, F, 0.6f);
            var windowSeat = S("WindowSeat", -144f, 52.3f, 10f, east, P, 2.6f, nap: true);
            var chairFloor = S("BesideChair", -38f, 0f, 16f, east, F, 0.5f);
            var chair = S("Chair", -12f, 20f, 16f, south, P, 1.1f, nap: true);
            var fridge = S("Fridge", 60f, 0f, 66f, north, F, 1.2f);      // food comes out of there
            var eastSide = S("EastSide", 100f, 0f, 10f, south, F, 0.5f);
            var hall = S("HallDoor", 85f, 0f, -92f, south, F, 0.8f);   // Grandad's TV is through there
            var south_ = S("South", -40f, 0f, -95f, north, F, 0.4f);
            var tableWest = S("TableWest", -62f, 0f, -38f, east, F, 0.5f);
            var underTable = S("UnderTable", -14f, 0f, -34f, south, F, 0.8f, nap: false);

            void Link(CatSpot a, params CatSpot[] b) { var l = new List<CatSpot>(a.links ?? new CatSpot[0]); l.AddRange(b); a.links = l.ToArray(); }
            Link(counterFront, counter, middle, chairFloor, fridge);
            Link(middle, windowFloor, chairFloor, tableWest);
            Link(windowFloor, windowSeat);
            Link(chairFloor, chair);
            Link(fridge, eastSide);
            Link(eastSide, hall);
            Link(hall, south_);
            Link(south_, tableWest);
            Link(tableWest, underTable);
            return g.GetComponentsInChildren<CatSpot>();
        }

        static Bounds[] KeepOut(Vector3 o)
        {
            Bounds Box(float x0, float x1, float y0, float y1, float z0, float z1)
            {
                var b = new Bounds();
                b.SetMinMax(o + new Vector3(x0, y0, z0), o + new Vector3(x1, y1, z1));
                return b;
            }
            const float tx = 10f, tz = -30f;   // the table (Stage 37)
            return new[]
            {
                Box(-200f, -160f, 0f, 130f, -200f, 200f), Box(160f, 200f, 0f, 130f, -200f, 200f),   // walls
                Box(-200f, 200f, 0f, 130f, 120f, 160f), Box(-200f, 200f, 0f, 130f, -160f, -120f),
                Box(-157f, 63f, 0f, 36f, 91.5f, 120f),          // the counter
                Box(77f, 123f, 0f, 92f, 89f, 120f),              // the fridge
                Box(-158.5f, -155.5f, 0f, 21f, -20f, 40f),       // the radiator
                Box(-148f, -132f, 0f, 14f, -120f, -108f),         // the vacuum's dock
                Box(tx + 46f, tx + 47.2f, 0f, 31f, tz - 31f, tz + 31f),   // the fort's blanket (east)
                Box(tx - 11f, tx + 47f, 0f, 31f, tz + 30f, tz + 31.2f),   // and north
                Box(tx - 43.5f, tx - 40.5f, 0f, 31f, tz - 27.5f, tz - 24.5f), Box(tx + 40.5f, tx + 43.5f, 0f, 31f, tz - 27.5f, tz - 24.5f),   // table legs
                Box(tx - 43.5f, tx - 40.5f, 0f, 31f, tz + 24.5f, tz + 27.5f), Box(tx + 40.5f, tx + 43.5f, 0f, 31f, tz + 24.5f, tz + 27.5f),
            };
        }
    }
}
