using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 33: Flight School, THE PRINTER, and a round of fixes.
    /// Menu: PaperWings > Stage Builders > Stage 33 (Flight School, The Printer)
    ///
    ///  - FLIGHT SCHOOL: a seven-lesson tutorial in the office on a fully upgraded plane (practice
    ///    drones and a gentle training rival live in the office's FlightSchool group).
    ///  - THE PRINTER replaces The Stapler as the office boss: the corner printer stays on its stand,
    ///    prints fighters that hunt you and shoots paper sheets (prefab, settings, projectile, pilots).
    ///  - CAMPAIGN v4: Target Practice left the campaign (it's lesson 5 now). Saved stars move with it.
    ///  - ECONOMY: everything 10% cheaper. THE AUDITOR: eased (settings and turrets).
    ///  - FIX: the Trespasser throw point sat against the coworker's keyboard.
    /// Run Stages 29-32 first. Safe to re-run.
    /// </summary>
    public static class Stage33Builder
    {
        const string P = StageKit.Root + "Prefabs/";
        const string S = StageKit.Root + "Settings/";

        [MenuItem("PaperWings/Stage Builders/Stage 33 (Flight School, The Printer)", priority = 145)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            var office = StageKit.RoomAt(StageKit.Office);
            if (office == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No office room found. Run the earlier stages (21 onwards) first.", "OK");
                return;
            }
            string report = Run(scene, office);
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 33 built.\n\n" + report, "Fly");
        }

        internal static string Run(Scene scene, Room office)
        {
            MoveCoworkerSpawn(office);
            Economy();
            Campaign();
            EaseAuditor();
            var printed = PilotSettings("Pilot_Printed", engage: 320f, fireRange: 28f, fireAngle: 8f, burst: 2, pause: 2.1f, aggression: 0.6f, respawn: 5f);
            var trainee = PilotSettings("Pilot_Trainee", engage: 55f, fireRange: 22f, fireAngle: 6f, burst: 2, pause: 2.8f, aggression: 0.45f, respawn: 4f);
            var sheet = PaperSheet();
            var printer = BuildPrinter(PrinterSettings(), sheet, printed);
            PlacePrinter(office, printer);
            FlightSchool(office, trainee);
            int cyl = StageKit.FixCylinderColliders();
            AssetDatabase.SaveAssets();
            string check = Stage27Builder.Run(scene);
            return "- FLIGHT SCHOOL is the first card on the main menu.\n- THE PRINTER is the office boss (Night 3).\n" +
                   "- Campaign v4: 12 nights; old stars move down one night.\n- Upgrades 10% cheaper; The Auditor eased.\n" +
                   $"- Trespasser throw point moved off the keyboard. ({cyl} cylinder collider(s) fixed)\n\n" + check;
        }

        // ------------------------------------------------------------------ fixes, economy, campaign

        static void MoveCoworkerSpawn(Room office)
        {
            var spawn = office.SpawnNamed("CoworkerSpawn");
            if (spawn == null) return;
            // Front edge of the coworker's desk: the keyboard is behind, the room in front.
            spawn.SetPositionAndRotation(office.transform.position + new Vector3(88f, 33.5f, -91f), Quaternion.Euler(0f, -90f, 0f));
            EditorUtility.SetDirty(spawn);
        }

        static void Economy()
        {
            var p = StageKit.Load<ProgressionSettings>("Settings/Progression.asset");
            if (p == null) return;
            var d = ScriptableObject.CreateInstance<ProgressionSettings>();
            p.foldUnlocks = d.foldUnlocks;
            p.foldUpgrades = d.foldUpgrades;
            p.staplesCost = d.staplesCost;
            Object.DestroyImmediate(d);
            EditorUtility.SetDirty(p);
        }

        static void Campaign()
        {
            var c = StageKit.Load<CampaignSettings>("Settings/Campaign.asset");
            if (c == null) return;
            c.nights = CampaignSettings.NightShift();
            c.version = CampaignSettings.CurrentVersion;
            EditorUtility.SetDirty(c);
        }

        /// <summary>The Auditor is Night 5's boss: tuned for a plane with a magazine and a couple of pencils.</summary>
        static void EaseAuditor()
        {
            var s = AssetDatabase.LoadAssetAtPath<AirBossSettings>(S + "Boss_Auditor.asset");
            if (s != null)
            {
                s.maxHealth = 480f;
                s.armoredMultiplier = 0.3f;
                s.telegraph = 1.6f;
                s.attackDuration = 3.5f;
                s.vulnerableTime = 6.5f;
                s.restTime = 2.5f;
                s.shotsPerVolley = 3;
                s.spreadAngle = 24f;
                s.projectileSpeed = 30f;
                s.projectileDamage = 5f;
                EditorUtility.SetDirty(s);
            }

            string path = P + "Auditor.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            var boss = root.GetComponent<AuditorBoss>();
            if (boss != null)
            {
                var so = new SerializedObject(boss);
                so.FindProperty("turretInterval").floatValue = 2.6f;
                so.FindProperty("turretBurst").intValue = 2;
                so.FindProperty("turretRange").floatValue = 70f;
                so.FindProperty("cruiseSpeed").floatValue = 13f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var part in root.GetComponentsInChildren<BossPart>(true))
            {
                var pso = new SerializedObject(part);
                pso.FindProperty("maxHealth").floatValue = 90f;
                pso.FindProperty("bossDamageOnDestroy").floatValue = 0.1f;
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static RivalPilotSettings PilotSettings(string name, float engage, float fireRange, float fireAngle, int burst, float pause, float aggression, float respawn)
        {
            var s = Stage2Builder.LoadOrCreate<RivalPilotSettings>(S + name + ".asset");
            s.engageRange = engage;
            s.disengageRange = engage * 1.6f;
            s.fireRange = fireRange;
            s.fireAngle = fireAngle;
            s.burstLength = burst;
            s.burstPause = pause;
            s.patrolRadius = 28f;
            s.preferredHeight = 24f;
            s.lowHeight = 10f;
            s.aggressiveness = aggression;
            s.avoidDistance = 16f;
            s.respawnDelay = respawn;
            EditorUtility.SetDirty(s);
            return s;
        }

        // ------------------------------------------------------------------ THE PRINTER

        static AirBossSettings PrinterSettings()
        {
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(S + "Boss_Printer.asset");
            s.displayName = "THE PRINTER";
            s.title = "Shared Device, 3rd Floor";
            s.maxHealth = 420f;
            s.armoredMultiplier = 0.25f;
            s.weakSpotMultiplier = 3f;
            s.aggroRange = 260f;
            s.engageDelay = 2.5f;
            s.wakeUpTime = 2f;
            s.phase2At = 0.6f;
            s.phase3At = 0.3f;
            s.telegraph = 1.5f;
            s.attackDuration = 4.5f;
            s.vulnerableTime = 6f;
            s.restTime = 2f;
            s.secondaryAirSpeed = 10f;   // the fuser's heat column
            s.shotsPerVolley = 3;
            s.spreadAngle = 22f;
            s.projectileSpeed = 24f;
            s.projectileDamage = 5f;
            s.projectileLifetime = 3.2f;
            s.projectileScale = 2.2f;
            s.respawnDelay = 25f;
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A flying sheet of A4: The Printer's ammunition (slow, big, easy to read).</summary>
        static StapleProjectile PaperSheet()
        {
            var paper = StageKit.Mat("M_SheetPaper", new Color(0.97f, 0.97f, 0.94f), 0.1f);
            var root = new GameObject("PaperSheet");
            root.AddComponent<StapleProjectile>();
            var leaf = StageKit.Box("Sheet", root.transform, Vector3.zero, new Vector3(1.6f, 0.06f, 2.2f), paper, collider: false);
            leaf.transform.localPosition = Vector3.zero;
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, P + "PaperSheet.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<StapleProjectile>();
        }

        static GameObject LBox(string name, Transform parent, Vector3 local, Vector3 size, Material mat, Vector3 euler = default)
        {
            var go = StageKit.Box(name, parent, Vector3.zero, size, mat, collider: false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static GameObject BuildPrinter(AirBossSettings settings, StapleProjectile sheet, RivalPilotSettings printedPilot)
        {
            var shell = StageKit.Mat("M_PrinterShell", new Color(0.86f, 0.86f, 0.83f), 0.35f);
            var lid = StageKit.Mat("M_PrinterLid", new Color(0.62f, 0.63f, 0.64f), 0.3f);
            var dark = StageKit.Mat("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var paper = StageKit.Mat("M_SheetPaper", new Color(0.97f, 0.97f, 0.94f), 0.1f);
            var lcd = StageKit.Emissive("M_PrinterLCD", new Color(0.3f, 0.8f, 0.4f), new Color(0.4f, 1.6f, 0.5f));
            var eye = StageKit.Emissive("M_PrinterEye", new Color(1f, 0.15f, 0.1f), new Color(2.4f, 0.25f, 0.1f));
            var weak = Stage26Builder.WeakSpotMat();
            var partMat = Stage26Builder.PartMat();

            var root = new GameObject("Printer");
            var t = root.transform;
            var body = new GameObject("Body").transform;
            body.SetParent(t, false);   // built at the origin facing +Z (front = output slot), moved onto the stand at the end

            // ---- the machine ----
            LBox("Chassis", body, new Vector3(0f, -0.5f, 0f), new Vector3(20f, 10f, 18f), shell);
            // The lid swings up on a hinge at the back when the printer jams.
            var hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(body, false);
            hinge.localPosition = new Vector3(0f, 6.8f, -9f);
            LBox("Lid", hinge, new Vector3(0f, -1.2f, 8.5f), new Vector3(20.4f, 2.4f, 17f), lid);
            LBox("ScannerGlass", hinge, new Vector3(0f, 0.05f, 8.5f), new Vector3(16f, 0.15f, 13f), dark);
            LBox("OutputSlot", body, new Vector3(0f, 3.4f, 8.9f), new Vector3(14f, 1.2f, 0.4f), dark);
            for (int i = 0; i < 3; i++)
                LBox("PrintedSheet", body, new Vector3(0f, 3.5f + i * 0.25f, 10.5f + i * 0.3f), new Vector3(11f, 0.08f, 4f), paper, new Vector3(-8f - i * 4f, 0f, 0f));
            LBox("LCD", body, new Vector3(6.5f, 5.2f, 9.05f), new Vector3(5f, 1.8f, 0.2f), lcd);
            // Angry status LEDs for eyes, with brows.
            foreach (float x in new[] { -4.5f, 0.5f })
            {
                LBox("Eye", body, new Vector3(x, 1.4f, 9.05f), new Vector3(2.2f, 1.3f, 0.25f), eye);
                LBox("Brow", body, new Vector3(x, 2.5f, 9.1f), new Vector3(2.8f, 0.4f, 0.3f), dark, new Vector3(0f, 0f, x < 0f ? -18f : 18f));
            }
            var col = body.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(20f, 13f, 18f);
            col.center = new Vector3(0f, 0.5f, 0f);
            StageKit29.Kinematic(body.gameObject);

            // ---- the fuser inside the top, under the lid: the weak spot when it jams ----
            var fuser = LBox("Fuser_WeakSpot", body, new Vector3(0f, 4.6f, -1f), new Vector3(16f, 1.2f, 9f), weak);
            for (int i = 0; i < 5; i++)
                LBox("Vent", body, new Vector3(-6f + i * 3f, 0.5f, -9.15f), new Vector3(0.5f, 5.4f, 0.4f), dark);

            // ---- two paper trays (parts): each prints a fighter ----
            var trays = new BossPart[2];
            for (int i = 0; i < 2; i++)
            {
                float y = i == 0 ? -2.2f : -5f;
                var part = Stage26Builder.Part(body, i == 0 ? "Tray_Upper" : "Tray_Lower", "PAPER TRAY", new Vector3(0f, y, 9.4f), new Vector3(18f, 2.4f, 2.2f), 80f, 0.1f);
                var look = part.transform.Find("Look");
                var front = LBox("TrayFront", look, Vector3.zero, new Vector3(18f, 2.3f, 1.2f), partMat);
                LBox("Handle", look, new Vector3(0f, 0f, 0.7f), new Vector3(5f, 0.6f, 0.4f), dark);
                LBox("Paper", look, new Vector3(0f, 1.25f, -1.5f), new Vector3(15f, 0.3f, 3f), paper);
                Stage26Builder.FinishPart(part, new[] { front.GetComponent<Renderer>() }, new GameObject[0]);
                trays[i] = part;
            }

            // ---- weapons, fighters, effects ----
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(body, false);
            muzzle.localPosition = new Vector3(0f, 3.6f, 12f);

            var drones = new GameObject("PrintedFighters");
            drones.transform.SetParent(body, false);
            var spots = new[] { new Vector3(-7f, 1f, 17f), new Vector3(7f, 1f, 17f), new Vector3(-12f, 5f, 19f), new Vector3(12f, 5f, 19f) };
            var spawners = new EnemySpawner[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var sp = StageKit.RivalSpawner($"PrintSpawner_{i + 1}", drones.transform, Vector3.zero, 0f, 0.6f + i * 0.5f);
                sp.transform.localPosition = spots[i];
                sp.transform.localRotation = Quaternion.Euler(-6f, (i % 2 == 0 ? -12f : 12f), 0f);
                BuilderUtil.SetRef(sp, "pilotSettings", printedPilot);
                spawners[i] = sp;
            }
            drones.SetActive(false);

            var shield = Stage26Builder.Shield(body, new Vector3(0f, 0.5f, 0f), new Vector3(30f, 22f, 28f));
            var printStream = StageKit29.Burst(body, "PrintStream", new Vector3(0f, 3.6f, 10f), 70, 12f, 0.9f);
            var smoke = StageKit29.Smoke(body, new Vector3(0f, 2f, -10f));
            var heat = StageKit.Column("FuserHeat", t, Vector3.zero, 9f, 65f, settings.secondaryAirSpeed, heat: true, strength: 0f);
            var burst = StageKit29.Burst(body, "DefeatBurst", Vector3.zero, 300, 24f, 1f);

            var boss = root.AddComponent<PrinterBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", muzzle);
            BuilderUtil.SetRef(boss, "projectilePrefab", sheet);
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            BuilderUtil.SetRef(boss, "body", body);
            BuilderUtil.SetRef(boss, "printStream", printStream);
            BuilderUtil.SetRef(boss, "jamSmoke", smoke);
            BuilderUtil.SetRef(boss, "fuserHeat", heat);
            BuilderUtil.SetRef(boss, "lidHinge", hinge);
            StageKit29.SetArray(boss, "weakSpotGlow", new[] { fuser.GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "trays", trays);
            StageKit29.SetArray(boss, "printSpawners", spawners);

            var target = root.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = "THE PRINTER";
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = fuser.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();

            // On the printer stand in the back-right corner, facing into the office. It never leaves it.
            body.localPosition = new Vector3(150f, 37f, 79f);
            body.localRotation = Quaternion.Euler(0f, -90f, 0f);
            heat.transform.localPosition = body.localPosition + Vector3.up * 5f;   // rises out of the open lid

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, P + "Printer.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Into the office's boss group (the old Stapler leaves it), at the room's origin.</summary>
        static void PlacePrinter(Room office, GameObject prefab)
        {
            var group = office.bossGroup != null ? office.bossGroup.transform : null;
            if (group == null)
            {
                group = StageKit.Group(office.transform, "OfficeBoss");
                group.gameObject.SetActive(false);
                office.bossGroup = group.gameObject;
            }
            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            inst.transform.SetPositionAndRotation(office.transform.position, Quaternion.identity);

            // The ordinary printer on the stand hides while the boss is out (it IS the boss).
            var hide = new List<GameObject>();
            foreach (var tr in office.GetComponentsInChildren<Transform>(true))
                if (tr.name == "BigPrinter" || tr.name == "BigPrinterTray") hide.Add(tr.gameObject);
            StageKit29.SetArray(inst.GetComponent<PrinterBoss>(), "hideWhileActive", hide.ToArray());
            EditorUtility.SetDirty(office);
        }

        // ------------------------------------------------------------------ FLIGHT SCHOOL

        static void FlightSchool(Room office, RivalPilotSettings trainee)
        {
            var g = StageKit29.FreshGroup(office.transform, ModeDirector.TrainingGroup);
            Vector3 o = office.transform.position;

            // Practice drones: sticky-note gliders looping round the lounge, slow and easy.
            var loop = new Vector3[10];
            for (int i = 0; i < loop.Length; i++)
            {
                float a = i * Mathf.PI * 2f / loop.Length;
                loop[i] = o + new Vector3(-50f + Mathf.Cos(a) * 62f, 62f + Mathf.Sin(a * 2f) * 10f, -35f + Mathf.Sin(a) * 45f);
            }
            var drones = StageKit29.Convoy(g, "Drones", ConvoySpawner.Kind.Practice,
                AssetDatabase.LoadAssetAtPath<PathFlyer>(StageKit29.P + "PracticeDrone.prefab"), loop, 99, 3, 2.5f, 6f, 10f, "Drone", 1.5f);
            drones.gameObject.SetActive(false);

            // A gentle training rival over the lounge.
            var rival = new GameObject("TrainingRival").transform;
            rival.SetParent(g, false);
            var sp = StageKit.RivalSpawner("TraineeSpawner", rival, o + new Vector3(-40f, 72f, -30f), 90f, 1.5f);
            BuilderUtil.SetRef(sp, "pilotSettings", trainee);
            rival.gameObject.SetActive(false);

            var tutorial = g.gameObject.AddComponent<Tutorial>();
            BuilderUtil.SetRef(tutorial, "drones", drones.gameObject);
            BuilderUtil.SetRef(tutorial, "rival", rival.gameObject);

            g.gameObject.SetActive(false);
            StageKit29.AddExtraGroup(office, g.gameObject);
            EditorUtility.SetDirty(office);
        }
    }
}
