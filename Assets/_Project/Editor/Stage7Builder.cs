using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 7: the folding system.
    /// Menu: PaperWings > Build Stage 7 (Folds)
    ///
    ///  - Creates the global Controls asset and moves your Invert Pitch choice into it.
    ///  - Creates Glider and Stunt folds: meshes, FlightSettings and PaperSettings (copied from
    ///    your tuned Dart, then given their own character) and a PlaneFold asset for each.
    ///  - Adds a FoldSelector to GameFlow in FlightTest and DeskPrototype.
    /// Re-running never overwrites existing settings or fold assets, so your tuning is safe.
    /// </summary>
    public static class Stage7Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string Art = "Assets/_Project/Art/";
        const string ControlsPath = S + "Controls.asset";
        const string PlanePrefabPath = "Assets/_Project/Prefabs/PaperDart.prefab";
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Build Stage 7 (Folds)", priority = 107)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var controls = EnsureControls();

            // The plane prefab's input now reads the global Controls asset.
            var root = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
            BuilderUtil.SetRef(root.GetComponent<PlaneInput>(), "controls", controls);
            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            EnsureFoldAssets();
            AssetDatabase.SaveAssets();   // write the new assets to disk before switching scenes

            int added = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);

                // UNITY GOTCHA: opening a scene unloads assets nothing in the scene references,
                // which turns objects we created a moment ago into "fake nulls". Reload them
                // by path AFTER opening the scene, or we'd assign empty references.
                var folds = LoadFolds();

                var launcher = Object.FindAnyObjectByType<LaunchAndReset>();
                var plane = Object.FindAnyObjectByType<PlaneController>();
                if (launcher != null && plane != null)
                {
                    AddFoldSelector(launcher.gameObject, plane, folds);
                    added++;
                }
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 7 built. Fold picker added to {added} scene(s).\n\n" +
                "While the plane is in hand: Q / E (or LB / RB) to switch between Dart, Glider and Stunt.\n\n" +
                "Invert Pitch now lives in PaperWings > Settings > Controls (your setting was carried over: " +
                (controls.invertPitch ? "ON" : "OFF") + ").", "Fly!");
        }

        // ------------------------------------------------------------------ controls

        public static ControlSettings EnsureControls()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ControlSettings>(ControlsPath);
            if (existing != null) return existing;

            var controls = ScriptableObject.CreateInstance<ControlSettings>();
            // Migrate: Invert Pitch used to live in the Dart's FlightSettings. The field is gone from
            // the script, but the old value is still in the asset file, so read it straight from the text.
            string dartPath = S + "FlightSettings_Dart.asset";
            if (File.Exists(dartPath) && File.ReadAllText(dartPath).Contains("invertPitch: 1"))
                controls.invertPitch = true;
            AssetDatabase.CreateAsset(controls, ControlsPath);
            return controls;
        }

        // ------------------------------------------------------------------ folds

        /// <summary>Creates any missing fold assets and returns all folds in picker order.</summary>
        public static PlaneFold[] EnsureFoldAssets()
        {
            var dartFlight = AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Dart.asset");
            var dartPaper = Stage2Builder.LoadOrCreate<PaperSettings>(PlaneFxBuilder.PaperSettingsPath);
            var dartMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "PaperDart_Mesh.asset");

            var gliderMesh = BuilderUtil.SaveMesh(FoldMeshes.BuildGlider(), Art + "PaperGlider_Mesh.asset");
            var stuntMesh = BuilderUtil.SaveMesh(FoldMeshes.BuildStunt(), Art + "PaperStunt_Mesh.asset");

            var gliderFlight = CopyIfMissing(dartFlight, S + "FlightSettings_Glider.asset", f =>
            {
                // Floaty: glides far, catches air hard, but slow and wide-turning.
                f.cruiseSpeed = 11f; f.maxSpeed = 34f;
                f.diveSpeedGain = 14f; f.climbSpeedCost = 16f;
                f.glideRatio = 14f; f.slowSinkExtra = 1.8f; f.turnSinkExtra = 0.8f; f.airGrip = 3.5f;
                f.pitchRate = 55f; f.turnRate = 60f; f.maxBank = 50f; f.bankResponse = 3.5f;
                f.stallSpeed = 5.5f; f.stallRecoverSpeed = 8f;
                f.airInfluence = 1.35f; f.tailwindSpeedGain = 0.7f;
                f.boostSpeed = 9f; f.throwSpeedMin = 10f; f.throwSpeedMax = 16f;
                f.wobbleAmount = 3.5f; f.trailMinSpeed = 14f; f.trailFullSpeed = 28f;
            });

            var stuntFlight = CopyIfMissing(dartFlight, S + "FlightSettings_Stunt.asset", f =>
            {
                // Snappy: fast and agile, but sinks quickly. Keep the speed up.
                f.cruiseSpeed = 16f; f.maxSpeed = 50f;
                f.diveSpeedGain = 22f; f.climbSpeedCost = 22f;
                f.glideRatio = 7f; f.slowSinkExtra = 3f; f.airGrip = 8f;
                f.pitchRate = 100f; f.turnRate = 115f; f.maxBank = 70f; f.bankResponse = 8f; f.rollAutoLevel = 4f;
                f.lowSpeedAuthority = 0.6f;
                f.stallSpeed = 8.5f; f.stallRecoverSpeed = 11.5f;
                f.airInfluence = 0.8f;
                f.boostSpeed = 13f; f.boostCooldown = 2.2f; f.throwSpeedMin = 14f; f.throwSpeedMax = 22f;
                f.wobbleAmount = 1.5f;
            });

            var gliderPaper = CopyIfMissing(dartPaper, S + "Paper_Glider.asset", p =>
            {
                // Big thin wings: flimsy and soak up steam.
                p.maxIntegrity = 75f; p.bumpBaseDamage = 9f; p.wetFromSteam = 0.9f; p.crumpleAmount = 0.22f;
            });

            var stuntPaper = CopyIfMissing(dartPaper, S + "Paper_Stunt.asset", p =>
            {
                // Folded cardstock: tough and slow to soak.
                p.maxIntegrity = 140f; p.bumpBaseDamage = 6f; p.bumpDamagePerImpact = 2.5f;
                p.wetFromSteam = 0.5f; p.crumpleAmount = 0.12f;
            });

            return new[]
            {
                Fold("Fold_Dart", "Dart", "The classic. Fast, steady and forgiving. Good at everything, best at nothing.",
                     dartFlight, dartPaper, dartMesh, new Color(0.96f, 0.95f, 0.9f)),
                Fold("Fold_Glider", "Glider", "Wide wings ride thermals like an elevator and glide forever. Slow, wide turns, flimsy paper.",
                     gliderFlight, gliderPaper, gliderMesh, new Color(0.86f, 0.93f, 1f)),
                Fold("Fold_Stunt", "Stunt", "Snappy delta for tight lines and big dives. Sinks fast, so keep the speed up. Tough cardstock.",
                     stuntFlight, stuntPaper, stuntMesh, new Color(1f, 0.9f, 0.82f)),
            };
        }

        static readonly string[] FoldFiles = { "Fold_Dart", "Fold_Glider", "Fold_Stunt" };

        /// <summary>Loads the fold assets fresh from disk, in picker order.</summary>
        static PlaneFold[] LoadFolds()
        {
            var folds = new PlaneFold[FoldFiles.Length];
            for (int i = 0; i < FoldFiles.Length; i++)
                folds[i] = AssetDatabase.LoadAssetAtPath<PlaneFold>(S + FoldFiles[i] + ".asset");
            return folds;
        }

        /// <summary>Adds (or refreshes) a FoldSelector on the GameFlow object. Shared with the Stage 1/3 builders.</summary>
        public static void AddFoldSelector(GameObject gameFlow, PlaneController plane, PlaneFold[] folds)
        {
            var selector = gameFlow.GetComponent<FoldSelector>();
            if (selector == null) selector = gameFlow.AddComponent<FoldSelector>();
            BuilderUtil.SetRef(selector, "plane", plane);
            BuilderUtil.SetRef(selector, "input", plane.GetComponent<PlaneInput>());

            var so = new SerializedObject(selector);
            var arr = so.FindProperty("folds");
            arr.arraySize = folds.Length;
            for (int i = 0; i < folds.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = folds[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T CopyIfMissing<T>(T source, string path, System.Action<T> customize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var copy = Object.Instantiate(source);   // starts from your current Dart tuning
            customize(copy);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        static PlaneFold Fold(string file, string displayName, string description,
                              FlightSettings flight, PaperSettings paper, Mesh mesh, Color color)
        {
            string path = S + file + ".asset";
            var fold = AssetDatabase.LoadAssetAtPath<PlaneFold>(path);
            if (fold != null)
            {
                // Keep your edits, but fill any reference that went missing.
                if (fold.flight == null) fold.flight = flight;
                if (fold.paper == null) fold.paper = paper;
                if (fold.mesh == null) fold.mesh = mesh;
                EditorUtility.SetDirty(fold);
                return fold;
            }
            fold = ScriptableObject.CreateInstance<PlaneFold>();
            fold.displayName = displayName;
            fold.description = description;
            fold.flight = flight;
            fold.paper = paper;
            fold.mesh = mesh;
            fold.paperColor = color;
            AssetDatabase.CreateAsset(fold, path);
            return fold;
        }
    }
}
