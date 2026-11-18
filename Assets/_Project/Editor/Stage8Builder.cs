using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 8: first combat.
    /// Menu: PaperWings > Build Stage 8 (Combat)
    ///
    ///  - Settings: player and rival staple guns, rival AI pilot, rival flight + paper.
    ///  - Prefabs: Staple (projectile) and RivalDart (AI plane). Adds a staple gun to PaperDart.
    ///  - Scenes: a "Combat" group with two rival spawners in DeskPrototype and FlightTest.
    /// Re-running replaces prefabs and the "Combat" groups; settings assets are kept.
    /// </summary>
    public static class Stage8Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string P = "Assets/_Project/Prefabs/";
        const string Art = "Assets/_Project/Art/";

        const string PlayerWeaponPath = S + "Weapon_PlayerStaples.asset";
        const string RivalWeaponPath = S + "Weapon_RivalStaples.asset";
        const string PilotPath = S + "Pilot_Rival.asset";
        const string RivalFlightPath = S + "FlightSettings_Rival.asset";
        const string RivalPaperPath = S + "Paper_Rival.asset";
        const string StaplePrefabPath = P + "Staple.prefab";
        const string RivalPrefabPath = P + "RivalDart.prefab";
        const string PlanePrefabPath = P + "PaperDart.prefab";

        [MenuItem("PaperWings/Build Stage 8 (Combat)", priority = 108)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateSettings();
            BuildStaplePrefab();
            AddGunToPlayer();
            BuildRivalPrefab();
            AssetDatabase.SaveAssets();

            // Desk: one rival over the middle of the desk, one patrolling above the laptop thermal.
            PlaceSpawners("Assets/_Project/Scenes/DeskPrototype.unity",
                new[] { new Vector3(10f, 60f, 0f), new Vector3(-12f, 72f, 2f) });
            // Open field: two rivals ahead of the launch tower.
            PlaceSpawners("Assets/_Project/Scenes/FlightTest.unity",
                new[] { new Vector3(0f, 45f, 80f), new Vector3(40f, 40f, 130f) });
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 8 built.\n\nFire staples: F / Left Mouse / gamepad X (12 per throw).\n" +
                "Two red rival planes patrol each scene. Shoot them down; they shoot back!\n\n" +
                "To turn rivals off: disable the 'Combat' object in the scene.", "Fight!");
        }

        // ------------------------------------------------------------------ settings

        static void CreateSettings()
        {
            Stage2Builder.LoadOrCreate<WeaponSettings>(PlayerWeaponPath);   // defaults are the player's gun

            if (AssetDatabase.LoadAssetAtPath<WeaponSettings>(RivalWeaponPath) == null)
            {
                // Rivals: unlimited ammo, but weaker, slower and less accurate than you.
                var w = ScriptableObject.CreateInstance<WeaponSettings>();
                w.clipSize = 0;
                w.fireInterval = 0.3f;
                w.damage = 10f;
                w.spread = 2.5f;
                w.aimAssistAngle = 4f;
                w.projectileSpeed = 65f;
                AssetDatabase.CreateAsset(w, RivalWeaponPath);
            }

            Stage2Builder.LoadOrCreate<RivalPilotSettings>(PilotPath);

            if (AssetDatabase.LoadAssetAtPath<FlightSettings>(RivalFlightPath) == null)
            {
                var dart = AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Dart.asset");
                var f = Object.Instantiate(dart);
                // A bit slower and less agile than the player's dart, so you can win a turning fight.
                f.cruiseSpeed = 13f; f.maxSpeed = 40f; f.turnRate = 62f; f.pitchRate = 60f;
                f.trailsEnabled = false;
                AssetDatabase.CreateAsset(f, RivalFlightPath);
            }

            if (AssetDatabase.LoadAssetAtPath<PaperSettings>(RivalPaperPath) == null)
            {
                var basePaper = AssetDatabase.LoadAssetAtPath<PaperSettings>(PlaneFxBuilder.PaperSettingsPath);
                var p = basePaper != null ? Object.Instantiate(basePaper) : ScriptableObject.CreateInstance<PaperSettings>();
                p.maxIntegrity = 60f;   // ~4 staples
                p.damagedColor = new Color(0.55f, 0.25f, 0.22f);
                AssetDatabase.CreateAsset(p, RivalPaperPath);
            }
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildStaplePrefab()
        {
            var metal = BuilderUtil.CreateMaterial("M_Staple", new Color(0.75f, 0.77f, 0.8f));
            metal.SetFloat("_Metallic", 0.8f);
            metal.SetFloat("_Smoothness", 0.7f);

            var root = new GameObject("Staple");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.08f, 0.08f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());   // staples raycast; no collider needed
            var r = body.GetComponent<Renderer>();
            r.sharedMaterial = metal;
            r.shadowCastingMode = ShadowCastingMode.Off;

            // A short glint trail so fast staples are easy to see.
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Stage2Builder.ParticleMaterial("M_Trail", null, additive: true);
            trail.time = 0.08f;
            trail.widthMultiplier = 0.06f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;

            root.AddComponent<StapleProjectile>();
            PrefabUtility.SaveAsPrefabAsset(root, StaplePrefabPath);
            Object.DestroyImmediate(root);
        }

        static void AddGunToPlayer()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponSettings>(PlayerWeaponPath);
            var staple = AssetDatabase.LoadAssetAtPath<GameObject>(StaplePrefabPath).GetComponent<StapleProjectile>();

            var root = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
            var plane = root.GetComponent<PlaneController>();
            var gun = root.GetComponent<StapleGun>();
            if (gun == null) gun = root.AddComponent<StapleGun>();
            BuilderUtil.SetRef(gun, "plane", plane);
            BuilderUtil.SetRef(gun, "settings", weapon);
            BuilderUtil.SetRef(gun, "projectilePrefab", staple);

            var shooter = root.GetComponent<PlayerShooter>();
            if (shooter == null) shooter = root.AddComponent<PlayerShooter>();
            BuilderUtil.SetRef(shooter, "input", root.GetComponent<PlaneInput>());
            BuilderUtil.SetRef(shooter, "gun", gun);

            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void BuildRivalPrefab()
        {
            var flight = AssetDatabase.LoadAssetAtPath<FlightSettings>(RivalFlightPath);
            var paper = AssetDatabase.LoadAssetAtPath<PaperSettings>(RivalPaperPath);
            var pilotSettings = AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(PilotPath);
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponSettings>(RivalWeaponPath);
            var staple = AssetDatabase.LoadAssetAtPath<GameObject>(StaplePrefabPath).GetComponent<StapleProjectile>();
            var dartMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "PaperDart_Mesh.asset");
            var redPaper = BuilderUtil.CreateMaterial("M_PaperRival", new Color(0.88f, 0.36f, 0.3f));

            var root = new GameObject("RivalDart");
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.05f, 0f);
            col.size = new Vector3(1.4f, 0.3f, 2f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = dartMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = redPaper;

            // Same flight model as the player; the RivalPilot "holds the stick" instead of PlaneInput.
            var controller = root.AddComponent<PlaneController>();
            BuilderUtil.SetRef(controller, "settings", flight);

            var gun = root.AddComponent<StapleGun>();
            BuilderUtil.SetRef(gun, "plane", controller);
            BuilderUtil.SetRef(gun, "settings", weapon);
            BuilderUtil.SetRef(gun, "projectilePrefab", staple);

            var pilot = root.AddComponent<RivalPilot>();
            BuilderUtil.SetRef(pilot, "settings", pilotSettings);
            BuilderUtil.SetRef(pilot, "gun", gun);

            var wobble = root.AddComponent<PaperWobble>();
            BuilderUtil.SetRef(wobble, "plane", controller);
            BuilderUtil.SetRef(wobble, "visual", visual.transform);

            PlaneFxBuilder.AddPaperDamage(root, paper);

            PrefabUtility.SaveAsPrefabAsset(root, RivalPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ scenes

        static void PlaceSpawners(string scenePath, Vector3[] positions)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) return;
            var scene = EditorSceneManager.OpenScene(scenePath);

            // Load AFTER opening the scene: opening a scene can unload assets we were holding.
            var rivalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RivalPrefabPath);
            var pilotSettings = AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(PilotPath);

            var old = GameObject.Find("Combat");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Combat").transform;

            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"RivalSpawner_{i + 1}");
                go.transform.SetParent(root, false);
                go.transform.position = positions[i];
                go.transform.rotation = Quaternion.Euler(0f, 180f + i * 90f, 0f);
                var spawner = go.AddComponent<EnemySpawner>();
                BuilderUtil.SetRef(spawner, "rivalPrefab", rivalPrefab);
                BuilderUtil.SetRef(spawner, "pilotSettings", pilotSettings);
                var so = new SerializedObject(spawner);
                so.FindProperty("firstSpawnDelay").floatValue = 2f + i * 5f;   // stagger arrivals
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene);
        }
    }
}
