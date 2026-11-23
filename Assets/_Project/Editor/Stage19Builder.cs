using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 19: upgrades per fold.
    /// Menu: PaperWings > Build Stage 19 (Per-Fold Upgrades)
    ///
    ///  - Every fold gets an upgrade id ("dart", "glider", ...) and a propeller mount on its tail.
    ///  - The propeller becomes a shared part any fold can buy (no longer built into the Canard),
    ///    and the other folds get a boost lift for when they carry it.
    ///  - The Progression settings learn the fold list (for the Unlocks screen tabs).
    ///  - Campaign rewards for upgrades now go to the Dart ("dart:pencils").
    /// Old saves are converted automatically: upgrades bought before go to the Dart,
    /// the old propeller stays on the Canard.
    /// </summary>
    public static class Stage19Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string PropellerPath = "Assets/_Project/Prefabs/Propeller.prefab";
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        // Fold asset, upgrade id, boost lift with a propeller (if the fold has none set yet).
        static readonly (string asset, string id, float lift)[] FoldSetup =
        {
            ("Fold_Dart", "dart", 7f),
            ("Fold_Glider", "glider", 8f),
            ("Fold_Stunt", "stunt", 6f),
            ("Fold_Canard", "canard", 9f),
        };

        /// <summary>The Canard's prop sits here (hand-placed in Stage 16); other folds sit the same distance behind their tails.</summary>
        static readonly Vector3 CanardMount = new Vector3(0f, 0f, -0.92f);

        [MenuItem("PaperWings/Build Stage 19 (Per-Fold Upgrades)", priority = 119)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var propeller = AssetDatabase.LoadAssetAtPath<GameObject>(PropellerPath);
            if (propeller == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Prefabs/Propeller.prefab not found. Run Build Stage 16 first.", "OK");
                return;
            }

            // ---- Folds: ids, propeller mounts, boost lift with a prop.
            var folds = new PlaneFold[FoldSetup.Length];
            var canard = AssetDatabase.LoadAssetAtPath<PlaneFold>(S + "Fold_Canard.asset");
            float tailGap = canard != null && canard.mesh != null ? CanardMount.z - TailZ(canard.mesh) : -0.05f;

            for (int i = 0; i < FoldSetup.Length; i++)
            {
                var (asset, id, lift) = FoldSetup[i];
                var fold = AssetDatabase.LoadAssetAtPath<PlaneFold>(S + asset + ".asset");
                folds[i] = fold;
                if (fold == null) continue;

                fold.foldId = id;
                fold.propellerMount = fold == canard || fold.mesh == null ? CanardMount : new Vector3(0f, 0f, TailZ(fold.mesh) + tailGap);
                if (fold.attachment == propeller) fold.attachment = null;   // the prop is an upgrade now, not part of the Canard
                EditorUtility.SetDirty(fold);

                if (fold.flight != null && fold.flight.boostLift <= 0f)
                {
                    fold.flight.boostLift = lift;
                    EditorUtility.SetDirty(fold.flight);
                }
            }

            // ---- Progression: the fold list drives the Unlocks screen tabs.
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionSettings>(S + "Progression.asset");
            if (progression != null)
            {
                progression.folds = System.Array.FindAll(folds, f => f != null);
                EditorUtility.SetDirty(progression);
            }

            // ---- Campaign: upgrade rewards become the Dart's upgrades.
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(S + "Campaign.asset");
            if (campaign != null && campaign.nights != null)
            {
                foreach (var night in campaign.nights)
                {
                    string id = night.rewardUnlockId;
                    if (string.IsNullOrEmpty(id) || id.Contains(":") || System.Array.IndexOf(UnlockIds.LegacyUpgrades, id) < 0) continue;
                    night.rewardUnlockId = UnlockIds.ForFold(id == UnlockIds.Propeller ? "canard" : "dart", id);
                }
                EditorUtility.SetDirty(campaign);
            }
            AssetDatabase.SaveAssets();

            // ---- Scenes: the fold picker spawns the propeller on whichever fold owns one.
            int wired = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var prop = AssetDatabase.LoadAssetAtPath<GameObject>(PropellerPath);   // after opening the scene
                foreach (var selector in Object.FindObjectsByType<FoldSelector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    BuilderUtil.SetRef(selector, "propellerPrefab", prop);
                    wired++;
                }
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeObject = progression;
            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 19 built (fold picker wired in {wired} scene(s)).\n\n" +
                "Upgrades are now bought for each fold separately, and every fold can buy the Pusher Propeller.\n" +
                "Unlocks screen: Left/Right picks the fold tab.\n\n" +
                "Upgrades bought before go to the Dart (the old propeller stays on the Canard).", "Fold on");
        }

        /// <summary>The rearmost point along the plane's centreline (falls back to the whole mesh).</summary>
        static float TailZ(Mesh mesh)
        {
            float tail = float.MaxValue;
            foreach (var v in mesh.vertices)
                if (Mathf.Abs(v.x) < 0.12f && v.z < tail) tail = v.z;
            return tail == float.MaxValue ? mesh.bounds.min.z : tail;
        }
    }
}
