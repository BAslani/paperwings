using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 9: pencil missiles.
    /// Menu: PaperWings > Stage Builders > Stage 9 (Pencil Missiles)
    ///
    /// Builds the PencilMissile prefab and Settings/Pencils, then mounts four pencils under the
    /// PaperDart's wings with a PencilLauncher. Prefab-only: every scene picks it up.
    /// Re-running rebuilds the pencil prefab and slots; the settings asset is kept.
    /// </summary>
    public static class Stage9Builder
    {
        const string SettingsPath = "Assets/_Project/Settings/Pencils.asset";
        const string PencilPrefabPath = "Assets/_Project/Prefabs/PencilMissile.prefab";
        const string PlanePrefabPath = "Assets/_Project/Prefabs/PaperDart.prefab";

        /// <summary>Overall pencil size. 0.55 = ~0.65 units long, about a third of the dart.</summary>
        const float PencilScale = 0.55f;

        // Mount points under the dart's wings (local to the Visual child), in firing order:
        // outer-left, outer-right, inner-left, inner-right, so the plane stays balanced as they go.
        static readonly Vector3[] SlotPositions =
        {
            new Vector3(-0.48f, -0.1f, -0.45f),
            new Vector3( 0.48f, -0.1f, -0.45f),
            new Vector3(-0.26f, -0.12f, -0.2f),
            new Vector3( 0.26f, -0.12f, -0.2f),
        };

        [MenuItem("PaperWings/Stage Builders/Stage 9 (Pencil Missiles)", priority = 109)]
        public static void Build()
        {
            var settings = Stage2Builder.LoadOrCreate<PencilSettings>(SettingsPath);
            var pencil = BuildPencilPrefab();
            MountOnPlayer(settings, pencil);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 9 built. Four pencil missiles now hang under the dart's wings.\n\n" +
                "Keep a rival in front of you until the box turns red (LOCKED), then press G / Right Mouse / LT.\n" +
                "Pencils refill when the plane is back in hand.", "Fire!");
        }

        static PencilMissile BuildPencilPrefab()
        {
            var yellow = BuilderUtil.CreateMaterial("M_PencilBody", new Color(1f, 0.78f, 0.15f));
            var wood = BuilderUtil.CreateMaterial("M_PencilWood", new Color(0.9f, 0.72f, 0.5f));
            var graphite = BuilderUtil.CreateMaterial("M_PencilLead", new Color(0.15f, 0.15f, 0.17f));
            var ferrule = BuilderUtil.CreateMaterial("M_PencilFerrule", new Color(0.75f, 0.76f, 0.78f));
            ferrule.SetFloat("_Metallic", 0.8f);
            var eraser = BuilderUtil.CreateMaterial("M_PencilEraser", new Color(1f, 0.55f, 0.6f));

            // ~1.1 units long (a short pencil at 10x scale), pointing along +Z like the plane.
            var root = new GameObject("PencilMissile");
            Part(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.16f, 0.4f, 0.16f), yellow);
            Part(PrimitiveType.Cylinder, "Wood", root.transform, new Vector3(0f, 0f, 0.47f), new Vector3(0.11f, 0.07f, 0.11f), wood);
            Part(PrimitiveType.Cylinder, "Lead", root.transform, new Vector3(0f, 0f, 0.56f), new Vector3(0.05f, 0.04f, 0.05f), graphite);
            Part(PrimitiveType.Cylinder, "Ferrule", root.transform, new Vector3(0f, 0f, -0.43f), new Vector3(0.17f, 0.04f, 0.17f), ferrule);
            Part(PrimitiveType.Cylinder, "Eraser", root.transform, new Vector3(0f, 0f, -0.52f), new Vector3(0.16f, 0.05f, 0.16f), eraser);

            // Graphite-grey smoke trail from the back, switched on when the pencil ignites.
            var trailGo = new GameObject("SmokeTrail");
            trailGo.transform.SetParent(root.transform, false);
            trailGo.transform.localPosition = new Vector3(0f, 0f, -0.58f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            var smoke = Stage2Builder.ParticleMaterial("M_PencilSmoke", Stage2Builder.SoftDotTexture("Assets/_Project/Art/T_SoftDot.asset"), additive: false);
            trail.sharedMaterial = smoke;
            trail.time = 0.6f;
            trail.minVertexDistance = 0.2f;
            trail.widthMultiplier = 0.2f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.4f, 1f, 1.2f);   // puffs out as it lingers
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.55f, 0.55f, 0.6f), 0f), new GradientColorKey(new Color(0.35f, 0.35f, 0.4f), 1f) },
                      new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;

            root.transform.localScale = Vector3.one * PencilScale;   // parts are modelled at full size, then scaled

            var missile = root.AddComponent<PencilMissile>();
            BuilderUtil.SetRef(missile, "smokeTrail", trail);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PencilPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PencilMissile>();
        }

        static void MountOnPlayer(PencilSettings settings, PencilMissile pencilPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
            var visual = root.transform.Find("Visual");
            if (visual == null) visual = root.transform;

            // Rebuild slots from scratch.
            for (int i = visual.childCount - 1; i >= 0; i--)
                if (visual.GetChild(i).name.StartsWith("PencilSlot")) Object.DestroyImmediate(visual.GetChild(i).gameObject);

            var slots = new Transform[SlotPositions.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"PencilSlot_{i + 1}").transform;
                slot.SetParent(visual, false);   // under Visual, so pencils wobble with the paper
                slot.localPosition = SlotPositions[i];
                slots[i] = slot;
            }

            var launcher = root.GetComponent<PencilLauncher>();
            if (launcher == null) launcher = root.AddComponent<PencilLauncher>();
            BuilderUtil.SetRef(launcher, "plane", root.GetComponent<PlaneController>());
            BuilderUtil.SetRef(launcher, "input", root.GetComponent<PlaneInput>());
            BuilderUtil.SetRef(launcher, "settings", settings);
            BuilderUtil.SetRef(launcher, "pencilPrefab", pencilPrefab);
            var so = new SerializedObject(launcher);
            var arr = so.FindProperty("slots");
            arr.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>A cylinder lying along +Z (cylinders stand on Y by default, so rotate 90 on X).</summary>
        static void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = Stage2Builder.Prim(type, name, parent, pos, new Vector3(90f, 0f, 0f), scale, mat);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
