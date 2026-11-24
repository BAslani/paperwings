using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 10: the first boss, THE STAPLER.
    /// Menu: PaperWings > Stage Builders > Stage 10 (Stapler Boss)
    ///
    ///  - Adds CombatTarget markers to the player and rival plane prefabs (needed for lock-on/markers).
    ///  - Builds the StaplerBoss prefab (model, jaw, weak-spot glow, slam air, effects, reinforcements).
    ///  - Places it on the back-right corner of the desk in DeskPrototype under a "Boss" object.
    /// Re-running rebuilds the prefab and the "Boss" object; the settings asset is kept.
    /// </summary>
    public static class Stage10Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string P = "Assets/_Project/Prefabs/";
        const string BossSettingsPath = S + "Boss_Stapler.asset";
        const string BossPrefabPath = P + "StaplerBoss.prefab";
        const string DeskScene = "Assets/_Project/Scenes/DeskPrototype.unity";

        [MenuItem("PaperWings/Stage Builders/Stage 10 (Stapler Boss)", priority = 110)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(P + "RivalDart.prefab") == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run 'Build Stage 8' first (RivalDart prefab not found).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Stage2Builder.LoadOrCreate<BossSettings>(BossSettingsPath);
            AddTarget(P + "PaperDart.prefab", "You", false);
            AddTarget(P + "RivalDart.prefab", "Rival", false);
            BuildBossPrefab();
            AssetDatabase.SaveAssets();
            PlaceBoss();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 10 built. THE STAPLER now guards the back-right corner of the desk, near the mug.\n\n" +
                "Its jaw is armored when shut. Dodge the staple fans, then hit the glowing mouth while it's open.\n" +
                "Below 60% it slams (downdraft blast); below 30% it calls reinforcements.\n\n" +
                "To fly without it: disable the 'Boss' object in the scene.", "Fight!");
        }

        static void AddTarget(string prefabPath, string displayName, bool isBoss)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var target = root.GetComponent<CombatTarget>();
            if (target == null) target = root.AddComponent<CombatTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("isBoss").boolValue = isBoss;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ------------------------------------------------------------------ boss model

        static void BuildBossPrefab()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BossSettings>(BossSettingsPath);
            var staple = AssetDatabase.LoadAssetAtPath<GameObject>(P + "Staple.prefab").GetComponent<StapleProjectile>();
            var rivalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(P + "RivalDart.prefab");
            var pilot = AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(S + "Pilot_Rival.asset");

            var black = BuilderUtil.CreateMaterial("M_StaplerBase", new Color(0.08f, 0.08f, 0.09f));
            var red = BuilderUtil.CreateMaterial("M_StaplerArm", new Color(0.7f, 0.08f, 0.08f));
            var darkRed = BuilderUtil.CreateMaterial("M_StaplerGrip", new Color(0.35f, 0.04f, 0.04f));
            var steel = BuilderUtil.CreateMaterial("M_StaplerSteel", new Color(0.7f, 0.72f, 0.75f));
            steel.SetFloat("_Metallic", 0.85f);
            var glow = BuilderUtil.CreateMaterial("M_BossWeakSpot", new Color(1f, 0.85f, 0.3f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", Color.black);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glow);

            // Mouth faces +Z. About 16 long, 4.5 wide, ~3.5 tall when shut: a giant stapler on the desk.
            var root = new GameObject("StaplerBoss");
            Stage2Builder.Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(4.5f, 1.2f, 16f), black, keepCollider: true);
            var anvilGlow = Stage2Builder.Prim(PrimitiveType.Cube, "AnvilGlow", root.transform, new Vector3(0f, 1.27f, 6.2f), Vector3.zero, new Vector3(3f, 0.15f, 3f), glow);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Hinge", root.transform, new Vector3(0f, 1.6f, -7.2f), new Vector3(0f, 0f, 90f), new Vector3(1.4f, 2.4f, 1.4f), steel);

            var jaw = new GameObject("Jaw").transform;
            jaw.SetParent(root.transform, false);
            jaw.localPosition = new Vector3(0f, 1.6f, -7.2f);   // hinge at the back
            Stage2Builder.Prim(PrimitiveType.Cube, "Arm", jaw, new Vector3(0f, 0.9f, 7.6f), Vector3.zero, new Vector3(4f, 1.8f, 15.5f), red, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cube, "Grip", jaw, new Vector3(0f, 1.85f, 6f), Vector3.zero, new Vector3(3f, 0.3f, 10f), darkRed);
            var mouthGlow = Stage2Builder.Prim(PrimitiveType.Cube, "MouthGlow", jaw, new Vector3(0f, -0.05f, 13.4f), Vector3.zero, new Vector3(3f, 0.15f, 3.2f), glow);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 2.2f, 8.6f);

            // Slam air (idle until the boss pulses it).
            var slamGo = new GameObject("SlamAir");
            slamGo.transform.SetParent(root.transform, false);
            var slamAir = slamGo.AddComponent<BossSlamAir>();

            var slamBurst = MakeSlamBurst(root.transform);
            var defeatBurst = MakeDefeatBurst(root.transform);

            // Phase 3 reinforcements: two rival spawners, inactive until the boss needs help.
            var reinforcements = new GameObject("Reinforcements");
            reinforcements.transform.SetParent(root.transform, false);
            for (int i = 0; i < 2; i++)
            {
                var sp = new GameObject($"ReinforcementSpawner_{i + 1}");
                sp.transform.SetParent(reinforcements.transform, false);
                sp.transform.localPosition = new Vector3(i == 0 ? -14f : 14f, 26f, 12f);
                var spawner = sp.AddComponent<EnemySpawner>();
                BuilderUtil.SetRef(spawner, "rivalPrefab", rivalPrefab);
                BuilderUtil.SetRef(spawner, "pilotSettings", pilot);
                var sso = new SerializedObject(spawner);
                sso.FindProperty("firstSpawnDelay").floatValue = 0.5f + i;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }
            reinforcements.SetActive(false);

            var boss = root.AddComponent<StaplerBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "jaw", jaw);
            BuilderUtil.SetRef(boss, "muzzle", muzzle);
            BuilderUtil.SetRef(boss, "staplePrefab", staple);
            BuilderUtil.SetRef(boss, "slamAir", slamAir);
            BuilderUtil.SetRef(boss, "reinforcements", reinforcements);
            BuilderUtil.SetRef(boss, "slamBurst", slamBurst);
            BuilderUtil.SetRef(boss, "defeatBurst", defeatBurst);
            var bso = new SerializedObject(boss);
            var glows = bso.FindProperty("weakSpotGlow");
            glows.arraySize = 2;
            glows.GetArrayElementAtIndex(0).objectReferenceValue = mouthGlow.GetComponent<Renderer>();
            glows.GetArrayElementAtIndex(1).objectReferenceValue = anvilGlow.GetComponent<Renderer>();
            bso.ApplyModifiedPropertiesWithoutUndo();

            // Lock-on / markers aim at the mouth.
            var target = root.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = settings.displayName;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = muzzle;
            tso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Object.DestroyImmediate(root);
        }

        static ParticleSystem MakeSlamBurst(Transform parent)
        {
            // A ring of dust blasting outward along the desk when the jaw slams.
            var go = new GameObject("SlamBurst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1f, 4f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // circle lies flat on the desk
            var mat = Stage2Builder.ParticleMaterial("M_SlamDust", Stage2Builder.SoftDotTexture("Assets/_Project/Art/T_SoftDot.asset"), additive: false);
            var ps = Stage2Builder.AddParticles(go, mat, stretched: true, sizeMin: 0.6f, sizeMax: 1.4f);
            var main = ps.main;
            main.loop = false; main.prewarm = false; main.playOnAwake = false;
            main.duration = 0.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 22f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startColor = new Color(0.85f, 0.85f, 0.9f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2f;
            return ps;
        }

        static ParticleSystem MakeDefeatBurst(Transform parent)
        {
            // A big shower of paper scraps (black, red and white) when it's beaten.
            var go = new GameObject("DefeatBurst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 3f, 4f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var mat = Stage2Builder.ParticleMaterial("M_PaperBits", null, additive: false);
            var ps = Stage2Builder.AddParticles(go, mat, stretched: false, sizeMin: 0.3f, sizeMax: 0.7f);
            var main = ps.main;
            main.loop = false; main.prewarm = false; main.playOnAwake = false;
            main.duration = 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 20f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.gravityModifier = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var colors = new Gradient();
            colors.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.95f, 0.9f), 0f), new GradientColorKey(new Color(0.8f, 0.1f, 0.1f), 0.5f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 250) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 3f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            return ps;
        }

        // ------------------------------------------------------------------ scene

        static void PlaceBoss()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null) return;
            var scene = EditorSceneManager.OpenScene(DeskScene);
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);   // load AFTER opening the scene

            var old = GameObject.Find("Boss");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Boss").transform;

            // Back-right corner of the desk, far from the throw spot, guarding the approach to the mug.
            // Mouth faces the middle of the desk (-X). The base spans x 19..35, z ~7..11.
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, root);
            boss.transform.SetPositionAndRotation(new Vector3(27f, 30f, 9f), Quaternion.Euler(0f, -90f, 0f));

            // Ring 4 (fan stream) used to sit where the open jaw now swings: nudge it forward.
            var ring4 = GameObject.Find("Ring4_FanStream");
            if (ring4 != null) ring4.transform.position = new Vector3(18f, 36f, 1f);

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = boss;
        }
    }
}
