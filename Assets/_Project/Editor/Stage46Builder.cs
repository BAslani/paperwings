using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 46: Tiny War, Act 1 is playable. Menu: PaperWings > Stage Builders > Stage 46 (Tiny War: Act 1)
    ///
    ///  - A second campaign, TINY WAR (Settings/Campaign_TinyWar.asset), on the main menu under the Night
    ///    Shift with its own stars. Three levels, Friday night in the kitchen:
    ///      1 Ration Run        escort three cookie couriers from the jar to the fort under the table
    ///      2 Battle of the Kitchen  four kills over the counter, Biscuit awake and hunting low flyers
    ///      3 The Dreadnought   the robot vacuum boss
    ///    The other nine levels show on the roster as "coming soon".
    ///  - Each level opens on Grandad's newsreel card (black and white, typed narration); briefings are
    ///    Theo's war diary.
    ///  - THE DREADNOUGHT: the family's robot vacuum, with Maya's cardboard turret taped on. It patrols the
    ///    floor sucking low flyers down and firing crumbs, loses suction as you shoot off its side brushes,
    ///    and docks to charge with its dust bin open (the weak spot). Biscuit flees it.
    ///
    /// Needs Stages 37, 43, 44 and 45. Safe to re-run.
    /// </summary>
    public static class Stage46Builder
    {
        const string CampaignPath = StageKit.Root + "Settings/Campaign_TinyWar.asset";
        const float D = 120f;

        [MenuItem("PaperWings/Stage Builders/Stage 46 (Tiny War: Act 1)", priority = 157)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            string report = Run();
            if (report == null) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 46 built: Tiny War, Act 1.\n\n" + report +
                "\n\nMain menu: STORY: TINY WAR. Level 1 opens with Grandad's newsreel.", "To war");
        }

        internal static string Run()
        {
            var room = StageKit.RoomAt(StageKit.Kitchen);
            var director = StageKit.Director();
            if (room == null || director == null) { EditorUtility.DisplayDialog("PaperWings", "No kitchen or director. Run Stage 37 first.", "OK"); return null; }
            var o = room.transform.position;

            var campaign = Campaign();
            Register(director, campaign);
            CookieRun(room, o);
            var boss = Dreadnought(room, o);
            room.bossGroup = boss.transform.parent.gameObject;
            room.description = "Toaster heat, kettle steam, a fridge that opens, a robot vacuum, and a cat who hunts low flyers.";
            EditorUtility.SetDirty(room);
            AssetDatabase.SaveAssets();
            return $"- Tiny War campaign: {campaign.Count} levels built, {campaign.comingSoon.Length} coming soon. Newsreel cards and war-diary briefings.\n" +
                   "- Ration Run: three cookie couriers fly from the jar to the fort.\n" +
                   "- THE DREADNOUGHT: patrols the floor with its suction on, side brushes to shoot off, dust bin weak spot when it docks.";
        }

        // ------------------------------------------------------------------ the campaign

        static CampaignSettings Campaign()
        {
            var c = Stage2Builder.LoadOrCreate<CampaignSettings>(CampaignPath);
            c.id = "tinywar";
            c.version = 1;
            c.displayName = "Tiny War";
            c.tagline = "A war film. In a house. Over one weekend.";
            c.rosterTitle = "TINY WAR · ORDER OF BATTLE";
            c.levelWord = "Level";
            c.briefingWord = "WAR DIARY";
            c.starBonus = new[] { 10, 25 };
            c.nights = CampaignSettings.TinyWar();
            c.comingSoon = CampaignSettings.TinyWarComingSoon();
            EditorUtility.SetDirty(c);
            return c;
        }

        static void Register(ModeDirector director, CampaignSettings c)
        {
            var so = new SerializedObject(director);
            var list = so.FindProperty("moreCampaigns");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == c) return;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        // ------------------------------------------------------------------ level 1: Ration Run

        static void CookieRun(Room room, Vector3 o)
        {
            var g = StageKit29.FreshGroup(room.transform, "CookieRun");
            var prefab = AssetDatabase.LoadAssetAtPath<PathFlyer>(StageKit29.P + "CourierBlue.prefab");
            Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);
            // From the cookie jar, down past the window, under the table's west edge and into the fort.
            var pts = new[]
            {
                A(-136f, 62f, 100f), A(-118f, 66f, 76f), A(-92f, 58f, 52f), A(-68f, 44f, 26f), A(-54f, 26f, -6f),
                A(-44f, 18f, -30f), A(-20f, 15f, -44f), A(12f, 12f, -36f), A(24f, 11f, -30f)
            };
            StageKit29.Convoy(g, "CookieConvoy", ConvoySpawner.Kind.Escort, prefab, pts, 3, 1, 4f, 7.5f, 140f, "Cookie courier", 4f);
            StageKit29.AddExtraGroup(room, g.gameObject);
        }

        // ------------------------------------------------------------------ level 3: THE DREADNOUGHT

        static AirBossSettings Settings()
        {
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(StageKit29.S + "Boss_Dreadnought.asset");
            s.displayName = "THE DREADNOUGHT";
            s.title = "Robot vacuum, armed by Red Squadron";
            s.maxHealth = 480f;
            s.armoredMultiplier = 0.25f;
            s.weakSpotMultiplier = 3f;
            s.aggroRange = 330f;
            s.engageDelay = 2.5f;
            s.wakeUpTime = 2.4f;
            s.phase2At = 0.6f;
            s.phase3At = 0.3f;
            s.telegraph = 1.4f;
            s.attackDuration = 5f;
            s.vulnerableTime = 6f;
            s.restTime = 1.2f;
            s.airSpeed = 14f;               // the suction's pull
            s.secondaryAirSpeed = 8f;
            s.contactDamagePerSecond = 35f;
            s.shotsPerVolley = 3;
            s.spreadAngle = 18f;
            s.projectileSpeed = 26f;
            s.projectileDamage = 5f;
            s.projectileLifetime = 3f;
            s.projectileScale = 1.6f;
            s.respawnDelay = 25f;
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A toast crumb: the Dreadnought's ammunition.</summary>
        static StapleProjectile Crumb()
        {
            var mat = StageKit.Mat("M_KL_Crumb", new Color(0.78f, 0.55f, 0.28f), 0.15f);
            var root = new GameObject("Crumb");
            root.AddComponent<StapleProjectile>();
            var bit = StageKit.Box("Bit", root.transform, Vector3.zero, new Vector3(1.1f, 0.8f, 0.9f), mat, collider: false);
            bit.transform.localPosition = Vector3.zero;
            bit.transform.localRotation = Quaternion.Euler(20f, 35f, 10f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + "Crumb.prefab");
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

        static Material M(string name, Color c, float smooth = 0.4f, float metal = 0f)
            => AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + $"Art/{name}.mat") ?? StageKit.Mat(name, c, smooth, metal);

        static DreadnoughtBoss Dreadnought(Room room, Vector3 o)
        {
            var settings = Settings();
            var crumb = Crumb();
            var graphite = M("M_KL_Graphite", new Color(0.24f, 0.25f, 0.27f), 0.6f, 0.3f);
            var rubber = M("M_KL_Rubber", new Color(0.12f, 0.12f, 0.13f), 0.3f);
            var ink = M("M_KL_Ink", new Color(0.17f, 0.17f, 0.17f), 0.4f);
            var bristle = StageKit.Mat("M_KL_Bristle", new Color(0.82f, 0.8f, 0.74f), 0.2f);
            var cardboard = StageKit.Mat("M_KL_Cardboard", new Color(0.72f, 0.56f, 0.36f), 0.1f);
            var tape = StageKit.Mat("M_KL_Tape", new Color(0.86f, 0.82f, 0.62f), 0.5f);
            var redFlag = M("M_KL_FortFlag", new Color(0.76f, 0.25f, 0.18f), 0.2f);
            var led = StageKit.Emissive("M_KL_VacuumEye", new Color(1f, 0.2f, 0.15f), new Color(3.2f, 0.4f, 0.3f));
            var weak = Stage26Builder.WeakSpotMat();
            var partMat = Stage26Builder.PartMat();

            // The boss slot (Stage 37 left it empty).
            var group = room.transform.Find("KitchenBoss");
            if (group == null) { group = StageKit.Group(room.transform, "KitchenBoss"); }
            group.gameObject.SetActive(false);
            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);

            var root = new GameObject("Dreadnought").transform;
            root.SetParent(group, false);
            root.position = o;

            // Its dock (Stage 43 put the charger in the south-west corner); it parks facing out of it, north.
            var dock = new GameObject("DockPoint").transform;
            dock.SetParent(root, false);
            dock.position = o + new Vector3(-140f, 0f, -D + 4f);
            dock.rotation = Quaternion.LookRotation(Vector3.forward);

            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.position = o + new Vector3(-140f, 0f, -D + 22f);
            body.rotation = Quaternion.LookRotation(Vector3.forward);

            // ---- the puck ----
            L("cyl", "Shell", body, new Vector3(0f, 4.6f, 0f), new Vector3(34f, 8f, 34f), graphite);
            L("cyl", "Bumper", body, new Vector3(0f, 3.2f, 0f), new Vector3(35.2f, 3f, 35.2f), rubber);
            L("cyl", "Top", body, new Vector3(0f, 8.75f, 2f), new Vector3(27f, 0.5f, 27f), ink);
            L("cyl", "Lidar", body, new Vector3(0f, 9.8f, 9f), new Vector3(6f, 2f, 6f), rubber);
            L("cyl", "LidarEye", body, new Vector3(0f, 10.9f, 9f), new Vector3(2.2f, 0.3f, 2.2f), led);
            // Angry eyes on the bumper.
            foreach (float x in new[] { -4.2f, 4.2f })
            {
                L("box", "Eye", body, new Vector3(x, 5.2f, 17.2f), new Vector3(3f, 1.4f, 0.4f), led);
                L("box", "Brow", body, new Vector3(x, 6.6f, 17.3f), new Vector3(3.6f, 0.5f, 0.4f), ink, new Vector3(0f, 0f, x < 0f ? -16f : 16f));
            }
            var col = body.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(32f, 10f, 32f);
            col.center = new Vector3(0f, 5f, 0f);
            StageKit29.Kinematic(body.gameObject);

            // ---- the dust bin at the back: its lid lifts from the front edge ----
            L("box", "Bin", body, new Vector3(0f, 9.2f, -8f), new Vector3(15f, 1.6f, 10f), rubber);
            var binWeak = L("box", "DustBin_WeakSpot", body, new Vector3(0f, 9.6f, -8f), new Vector3(13f, 1f, 8.4f), weak);
            var lid = new GameObject("BinLid").transform;
            lid.SetParent(body, false);
            lid.localPosition = new Vector3(0f, 10.2f, -3f);
            L("box", "Lid", lid, new Vector3(0f, 0f, -5f), new Vector3(15.4f, 0.7f, 10.4f), graphite);

            // ---- Maya's cardboard turret, taped on ----
            var turret = new GameObject("Turret").transform;
            turret.SetParent(body, false);
            turret.localPosition = new Vector3(0f, 9f, 2.5f);
            L("box", "Box", turret, new Vector3(0f, 2.8f, 0f), new Vector3(9f, 5.6f, 8f), cardboard);
            L("box", "TapeX", turret, new Vector3(0f, 5.65f, 0f), new Vector3(9.4f, 0.12f, 1.6f), tape, new Vector3(0f, 35f, 0f));
            L("box", "TapeY", turret, new Vector3(0f, 5.66f, 0f), new Vector3(9.4f, 0.12f, 1.6f), tape, new Vector3(0f, -35f, 0f));
            L("box", "TapeDown", turret, new Vector3(4.55f, 1.4f, 0f), new Vector3(0.12f, 3f, 1.6f), tape);
            L("cyl", "Barrel", turret, new Vector3(0f, 3.4f, 8.5f), new Vector3(2.2f, 9f, 2.2f), cardboard, new Vector3(90f, 0f, 0f));
            L("cyl", "FlagPole", turret, new Vector3(-3.6f, 9.5f, -3f), new Vector3(0.3f, 8f, 0.3f), ink);
            L("box", "RedFlag", turret, new Vector3(-1.9f, 12.3f, -3f), new Vector3(3.4f, 2.2f, 0.15f), redFlag);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turret, false);
            muzzle.localPosition = new Vector3(0f, 3.4f, 13.5f);

            // ---- the side brushes (parts) at the front corners ----
            var spinners = new List<Transform>();
            foreach (float side in new[] { -1f, 1f })
            {
                var part = Stage26Builder.Part(body, side < 0f ? "Brush_Left" : "Brush_Right", "SIDE BRUSH", new Vector3(side * 12.5f, 1.2f, 12.5f), new Vector3(9f, 3f, 9f), 70f, 0.1f);
                var look = part.transform.Find("Look");
                var hub = L("cyl", "Hub", look, Vector3.zero, new Vector3(2.4f, 1.2f, 2.4f), partMat);
                var spin = new GameObject("Spinner").transform;
                spin.SetParent(look, false);
                for (int k = 0; k < 3; k++)
                    L("box", "Bristles", spin, Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(0f, -0.2f, 3.4f), new Vector3(0.7f, 0.3f, 6.6f), bristle, new Vector3(4f, k * 120f, 0f));
                spinners.Add(spin);
                Stage26Builder.FinishPart(part, new[] { hub.GetComponent<Renderer>() }, new GameObject[0]);
            }

            // ---- air, effects, light ----
            var suction = StageKit.Column("Suction", body, body.position + Vector3.up * 9f, 30f, 42f, -settings.airSpeed, -settings.airSpeed * 0.45f, 0f, heat: false, strength: 0f);
            suction.ramp = 3f;
            var trail = StageKit29.Smoke(body, new Vector3(0f, 1f, -16f));
            var binDust = StageKit29.Smoke(body, new Vector3(0f, 10f, -8f));
            var burst = StageKit29.Burst(body, "DefeatBurst", new Vector3(0f, 8f, 0f), 260, 22f, 0.9f);
            var light = StageKit.PointLight("StatusLight", body, body.position + new Vector3(0f, 13f, 9f), new Color(1f, 0.15f, 0.1f), 80f, 40f);

            // ---- patrol loop round the floor ----
            var loop = new GameObject("Patrol").transform;
            loop.SetParent(root, false);
            var pts = new[] { (-100f, -70f), (-110f, 40f), (-20f, 62f), (60f, 60f), (125f, 30f), (125f, -75f), (20f, -100f), (-60f, -100f) };
            var wps = new List<Transform>();
            for (int i = 0; i < pts.Length; i++)
            {
                var wp = new GameObject($"P{i + 1}").transform;
                wp.SetParent(loop, false);
                wp.position = o + new Vector3(pts[i].Item1, 0f, pts[i].Item2);
                wps.Add(wp);
            }

            var boss = root.gameObject.AddComponent<DreadnoughtBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", muzzle);
            BuilderUtil.SetRef(boss, "projectilePrefab", crumb);
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            BuilderUtil.SetRef(boss, "body", body);
            BuilderUtil.SetRef(boss, "dock", dock);
            BuilderUtil.SetRef(boss, "suction", suction);
            BuilderUtil.SetRef(boss, "binLid", lid);
            BuilderUtil.SetRef(boss, "turret", turret);
            BuilderUtil.SetRef(boss, "dustTrail", trail);
            BuilderUtil.SetRef(boss, "binDust", binDust);
            BuilderUtil.SetRef(boss, "statusLight", light);
            var so = new SerializedObject(boss);
            so.FindProperty("binOpenAngle").floatValue = 80f;   // the lid lies toward the back: positive lifts it
            so.ApplyModifiedPropertiesWithoutUndo();
            StageKit29.SetArray(boss, "weakSpotGlow", new[] { binWeak.GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "patrol", wps.ToArray());
            StageKit29.SetArray(boss, "brushSpinners", spinners.ToArray());

            // The sleeping vacuum (Stage 43's prop) hides while the boss is out; its dock stays.
            var hide = new List<GameObject>();
            var docked = FindDeep(room.transform, "DockedVacuum");
            if (docked != null)
                foreach (Transform t in docked)
                    if (t.name != "Dock" && t.name != "DockLight") hide.Add(t.gameObject);
            StageKit29.SetArray(boss, "hideWhileActive", hide.ToArray());

            var target = root.gameObject.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = "THE DREADNOUGHT";
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = binWeak.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}
