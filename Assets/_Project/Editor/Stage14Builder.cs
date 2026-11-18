using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 14: factions and wingmen.
    /// Menu: PaperWings > Build Stage 14 (Factions & Wingmen)
    ///
    ///  - Sets factions: the player = Marketing; rivals and The Stapler = Accounting.
    ///  - Builds a blue WingmanDart prefab (Marketing AI) with its own pilot/flight/gun settings.
    ///  - DeskPrototype: a "Squadron" group (2 extra rival spawners + 2 wingman spawners), used by
    ///    the new Squadron Battle mode.
    /// Re-running rebuilds the wingman prefab and the Squadron group; settings assets are kept.
    /// </summary>
    public static class Stage14Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string P = "Assets/_Project/Prefabs/";
        const string DeskScene = "Assets/_Project/Scenes/DeskPrototype.unity";
        const string WingmanPrefabPath = P + "WingmanDart.prefab";

        [MenuItem("PaperWings/Build Stage 14 (Factions & Wingmen)", priority = 114)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(P + "RivalDart.prefab") == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run 'Build Stage 8' (and 10) first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SetFaction(P + "PaperDart.prefab", Faction.Marketing, "You");
            SetFaction(P + "RivalDart.prefab", Faction.Accounting, "Rival");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(P + "StaplerBoss.prefab") != null)
                SetFaction(P + "StaplerBoss.prefab", Faction.Accounting, null);

            CreateWingmanSettings();
            BuildWingmanPrefab();
            AssetDatabase.SaveAssets();
            PlaceSquadron();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 14 built.\n\nYou fly for MARKETING (blue); rivals and The Stapler are ACCOUNTING (red).\n" +
                "New mode: SQUADRON BATTLE. Two blue wingmen wait in hand beside you, launch when you throw, " +
                "fly formation and attack nearby rivals. Your squadron needs 8 kills.", "Fly together!");
        }

        static void SetFaction(string prefabPath, Faction faction, string displayName)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var target = root.GetComponent<CombatTarget>();
            if (target == null) target = root.AddComponent<CombatTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("faction").enumValueIndex = (int)faction;
            if (displayName != null) so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------ wingman

        static void CreateWingmanSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(S + "Pilot_Wingman.asset") == null)
            {
                var p = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(S + "Pilot_Rival.asset"));
                // A bit sharper than the rivals: they're on your side, after all.
                p.engageRange = 50f; p.disengageRange = 75f;
                p.aggressiveness = 0.85f; p.fireAngle = 12f;
                p.burstLength = 4; p.burstPause = 1.2f;
                AssetDatabase.CreateAsset(p, S + "Pilot_Wingman.asset");
            }
            if (AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Wingman.asset") == null)
            {
                var f = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Dart.asset"));
                f.trailsEnabled = false;   // keep the screen readable; only your plane trails
                AssetDatabase.CreateAsset(f, S + "FlightSettings_Wingman.asset");
            }
            if (AssetDatabase.LoadAssetAtPath<WeaponSettings>(S + "Weapon_WingmanStaples.asset") == null)
            {
                var w = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponSettings>(S + "Weapon_RivalStaples.asset"));
                w.damage = 12f; w.fireInterval = 0.28f;
                AssetDatabase.CreateAsset(w, S + "Weapon_WingmanStaples.asset");
            }
            if (AssetDatabase.LoadAssetAtPath<PaperSettings>(S + "Paper_Wingman.asset") == null)
            {
                var paper = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PaperSettings>(S + "Paper_Rival.asset"));
                paper.maxIntegrity = 80f;
                paper.damagedColor = new Color(0.45f, 0.55f, 0.7f);
                AssetDatabase.CreateAsset(paper, S + "Paper_Wingman.asset");
            }
        }

        static void BuildWingmanPrefab()
        {
            var bluePaper = BuilderUtil.CreateMaterial("M_PaperWingman", new Color(0.45f, 0.65f, 0.95f));

            // Start from the rival plane and re-badge it as a Marketing wingman.
            var root = PrefabUtility.LoadPrefabContents(P + "RivalDart.prefab");
            root.name = "WingmanDart";

            var visual = root.transform.Find("Visual");
            if (visual != null) visual.GetComponent<MeshRenderer>().sharedMaterial = bluePaper;

            var target = root.GetComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Marketing;
            tso.FindProperty("displayName").stringValue = "Wingman";
            tso.ApplyModifiedPropertiesWithoutUndo();

            BuilderUtil.SetRef(root.GetComponent<PlaneController>(), "settings", AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Wingman.asset"));
            BuilderUtil.SetRef(root.GetComponent<RivalPilot>(), "settings", AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(S + "Pilot_Wingman.asset"));
            BuilderUtil.SetRef(root.GetComponent<StapleGun>(), "settings", AssetDatabase.LoadAssetAtPath<WeaponSettings>(S + "Weapon_WingmanStaples.asset"));
            BuilderUtil.SetRef(root.GetComponent<PaperDamage>(), "settings", AssetDatabase.LoadAssetAtPath<PaperSettings>(S + "Paper_Wingman.asset"));

            PrefabUtility.SaveAsPrefabAsset(root, WingmanPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------ scene

        static void PlaceSquadron()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null) return;
            var scene = EditorSceneManager.OpenScene(DeskScene);

            // Load AFTER opening the scene (opening a scene can unload assets we were holding).
            var rivalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(P + "RivalDart.prefab");
            var wingmanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WingmanPrefabPath);
            var rivalPilot = AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(S + "Pilot_Rival.asset");

            PlaneController player = null;
            foreach (var p in Object.FindObjectsByType<PlaneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.GetComponent<PlaneInput>() != null) player = p;

            foreach (var r in scene.GetRootGameObjects())
                if (r.name == "Squadron") Object.DestroyImmediate(r);
            var group = new GameObject("Squadron");

            // Two extra rival spawners (on top of the two in "Combat"), away from your throw spot.
            Vector3[] rivalSpots = { new Vector3(0f, 68f, 12f), new Vector3(22f, 62f, -10f) };
            for (int i = 0; i < rivalSpots.Length; i++)
            {
                var go = new GameObject($"SquadronRivalSpawner_{i + 1}");
                go.transform.SetParent(group.transform, false);
                go.transform.SetPositionAndRotation(rivalSpots[i], Quaternion.Euler(0f, 220f + i * 60f, 0f));
                var spawner = go.AddComponent<EnemySpawner>();
                BuilderUtil.SetRef(spawner, "rivalPrefab", rivalPrefab);
                BuilderUtil.SetRef(spawner, "pilotSettings", rivalPilot);
                var so = new SerializedObject(spawner);
                so.FindProperty("firstSpawnDelay").floatValue = 4f + i * 3f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Two wingmen: right and left of you.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                var go = new GameObject(i == 0 ? "WingmanSpawner_Right" : "WingmanSpawner_Left");
                go.transform.SetParent(group.transform, false);
                var ally = go.AddComponent<AllySpawner>();
                BuilderUtil.SetRef(ally, "wingmanPrefab", wingmanPrefab);
                BuilderUtil.SetRef(ally, "leader", player);
                var so = new SerializedObject(ally);
                so.FindProperty("formationOffset").vector3Value = new Vector3(7f * side, 1f, -6f);
                so.FindProperty("handOffset").vector3Value = new Vector3(3f * side, 0.6f, -1.5f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Tell the mode director about the new group.
            var director = Object.FindAnyObjectByType<ModeDirector>();
            if (director != null) BuilderUtil.SetRef(director, "squadronGroup", group);
            group.SetActive(false);   // the director switches it on for Squadron Battle

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = group;
        }
    }
}
