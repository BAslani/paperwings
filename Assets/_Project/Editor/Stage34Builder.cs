using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 34: bolder pencil missiles, an easier office party.
    /// Menu: PaperWings > Stage Builders > Stage 34 (Pencil Trails, Easier Party)
    ///
    ///  - Pencil missiles: a thick, long-lingering smoke trail, a short hot flame trail, sparks and a
    ///    small glow, all lit when the pencil ignites. When it hits, the trails stay behind and fade.
    ///  - Act 2 (the break room party) eased for the upgrades you have by then:
    ///      Office Party  3 kills (was 4), two rivals at most, flown by TRAINEE pilots
    ///      Coffee Run    one rival (was 2), a trainee
    ///      The Auditor   less health, weaker clip bombs, longer stall windows
    ///    and gentler star times on all three.
    /// Assets only (no scene changes). Safe to re-run.
    /// </summary>
    public static class Stage34Builder
    {
        const string Root = "Assets/_Project/";
        const string PencilPrefab = Root + "Prefabs/PencilMissile.prefab";

        [MenuItem("PaperWings/Stage Builders/Stage 34 (Pencil Trails, Easier Party)", priority = 146)]
        public static void Build()
        {
            bool pencil = BolderPencil();
            int nights = EaseParty();
            bool boss = EaseAuditor();
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 34 built.\n\n" +
                (pencil ? "- Pencil missiles: thick smoke, a flame trail, sparks and a glow.\n" : "- PencilMissile prefab not found (run Stage 9).\n") +
                $"- Office party: {nights} night(s) eased" + (boss ? ", the Auditor softened.\n" : ".\n") +
                "\nNothing to rebuild in the scene: just press Play.", "Nice");
        }

        // ------------------------------------------------------------------ pencil trails

        static bool BolderPencil()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PencilPrefab) == null) return false;
            var root = PrefabUtility.LoadPrefabContents(PencilPrefab);
            var missile = root.GetComponent<PencilMissile>();
            var dot = Stage2Builder.SoftDotTexture(Root + "Art/T_SoftDot.asset");

            // Remove what an earlier run added, so re-running doesn't stack them.
            foreach (var name in new[] { "FlameTrail", "Sparks", "Glow" })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // ---- Smoke: thicker, lighter and longer than the original, billowing out (toned down 50% after the first try) ----
            var smoke = root.transform.Find("SmokeTrail")?.GetComponent<TrailRenderer>();
            if (smoke != null)
            {
                smoke.time = 0.8f;
                smoke.minVertexDistance = 0.15f;
                smoke.widthMultiplier = 0.35f;
                smoke.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.3f, 0.9f), new Keyframe(1f, 1.8f));
                smoke.colorGradient = Gradient(
                    new[] { (new Color(1f, 0.96f, 0.88f), 0f), (new Color(0.85f, 0.85f, 0.88f), 0.25f), (new Color(0.6f, 0.6f, 0.65f), 1f) },
                    new[] { (0.5f, 0f), (0.3f, 0.4f), (0f, 1f) });
            }
            Vector3 tail = smoke != null ? smoke.transform.localPosition : new Vector3(0f, 0f, -0.58f);

            // ---- Flame: a short, bright, additive trail right behind the eraser ----
            var flameGo = new GameObject("FlameTrail");
            flameGo.transform.SetParent(root.transform, false);
            flameGo.transform.localPosition = tail;
            var flame = flameGo.AddComponent<TrailRenderer>();
            flame.sharedMaterial = Stage2Builder.ParticleMaterial("M_PencilFlame", dot, additive: true);
            flame.time = 0.15f;
            flame.minVertexDistance = 0.1f;
            flame.widthMultiplier = 0.25f;
            flame.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);
            flame.colorGradient = Gradient(
                new[] { (new Color(1f, 0.97f, 0.8f), 0f), (new Color(1f, 0.6f, 0.2f), 0.4f), (new Color(1f, 0.3f, 0.1f), 1f) },
                new[] { (0.5f, 0f), (0.4f, 0.4f), (0f, 1f) });
            flame.shadowCastingMode = ShadowCastingMode.Off;
            flame.emitting = false;

            // ---- Sparks: little embers spat out backwards ----
            var sparksGo = new GameObject("Sparks");
            sparksGo.transform.SetParent(root.transform, false);
            sparksGo.transform.localPosition = tail;
            sparksGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // emit backwards
            var sparks = Stage2Builder.AddParticles(sparksGo, Stage2Builder.ParticleMaterial("M_PencilSparks", dot, additive: true), stretched: false, sizeMin: 0.12f, sizeMax: 0.25f);
            var main = sparks.main;
            main.prewarm = false;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.15f));
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = sparks.emission;
            emission.rateOverTime = 15f;
            emission.rateOverDistance = 2f;
            emission.enabled = false;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.05f;
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            fade.color = Gradient(new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (1f, 0f), (0f, 1f) });

            // ---- Glow: a small warm light so a pencil is easy to follow across a dark room ----
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(root.transform, false);
            glowGo.transform.localPosition = tail;
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.25f);
            glow.intensity = 1.25f;
            glow.range = 5f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;

            var so = new SerializedObject(missile);
            var trails = so.FindProperty("extraTrails");
            trails.arraySize = 1;
            trails.GetArrayElementAtIndex(0).objectReferenceValue = flame;
            so.FindProperty("exhaust").objectReferenceValue = sparks;
            so.FindProperty("glow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PencilPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            return true;
        }

        static Gradient Gradient((Color c, float t)[] colors, (float a, float t)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return g;
        }

        // ------------------------------------------------------------------ the office party (Act 2)

        /// <summary>Copies the eased Act 2 nights onto the saved campaign and gives the first two trainee rivals.</summary>
        static int EaseParty()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(Root + "Settings/Campaign.asset");
            if (campaign == null || campaign.nights == null) return 0;
            var trainee = AssetDatabase.LoadAssetAtPath<RivalPilotSettings>(Root + "Settings/Pilot_Trainee.asset");
            var defaults = ScriptableObject.CreateInstance<CampaignSettings>();
            int eased = 0;
            foreach (var title in new[] { "Office Party", "Coffee Run", "The Auditor" })
            {
                var night = System.Array.Find(campaign.nights, n => n.title == title);
                var d = System.Array.Find(defaults.nights, n => n.title == title);
                if (night == null || d == null) continue;
                night.killTarget = d.killTarget;
                night.maxRivals = d.maxRivals;
                night.allowedLosses = d.allowedLosses;
                night.twoStarTime = d.twoStarTime;
                night.threeStarTime = d.threeStarTime;
                night.memo = d.memo;
                if (title != "The Auditor") night.rivalPilot = trainee;   // rookies, not regulars
                eased++;
            }
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(campaign);
            return eased;
        }

        static bool EaseAuditor()
        {
            var s = AssetDatabase.LoadAssetAtPath<AirBossSettings>(Root + "Settings/Boss_Auditor.asset");
            if (s == null) return false;
            s.maxHealth = 400f;          // was 480
            s.projectileDamage = 4f;     // clip bombs: was 5
            s.vulnerableTime = 7.5f;     // engine stall window: was 6.5
            EditorUtility.SetDirty(s);
            return true;
        }
    }
}
