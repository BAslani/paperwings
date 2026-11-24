using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 17: progression and unlocks.
    /// Menu: PaperWings > Stage Builders > Stage 17 (Progression)
    ///
    ///  - Settings/Progression: unlock shop, rewards, medal times.
    ///  - Tags the folds with unlock ids (Glider/Stunt/Canard folds).
    ///  - Adds a ProgressionApplier to GameFlow in both scenes; hooks rewards into the ModeDirector.
    /// New players start on the raw Dart with only the staple gun.
    /// Dev tools: PaperWings > Dev (reset progress, unlock everything, free paper clips).
    /// </summary>
    public static class Stage17Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string SettingsPath = S + "Progression.asset";
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 17 (Progression)", priority = 117)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Stage2Builder.LoadOrCreate<ProgressionSettings>(SettingsPath);
            TagFold("Fold_Dart", "");
            TagFold("Fold_Glider", UnlockIds.GliderFold);
            TagFold("Fold_Stunt", UnlockIds.StuntFold);
            TagFold("Fold_Canard", UnlockIds.CanardFold);
            AssetDatabase.SaveAssets();

            int wired = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var settings = AssetDatabase.LoadAssetAtPath<ProgressionSettings>(SettingsPath);   // after opening the scene

                PlaneController player = null;
                foreach (var p in Object.FindObjectsByType<PlaneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (p.GetComponent<PlaneInput>() != null) player = p;
                var folds = Object.FindAnyObjectByType<FoldSelector>();
                var launcher = Object.FindAnyObjectByType<LaunchAndReset>();

                if (player != null && launcher != null)
                {
                    var applier = launcher.GetComponent<ProgressionApplier>();
                    if (applier == null) applier = launcher.gameObject.AddComponent<ProgressionApplier>();
                    BuilderUtil.SetRef(applier, "player", player);
                    BuilderUtil.SetRef(applier, "folds", folds);
                    BuilderUtil.SetRef(applier, "settings", settings);
                    wired++;
                }

                var director = Object.FindAnyObjectByType<ModeDirector>();
                if (director != null) BuilderUtil.SetRef(director, "progression", settings);

                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ProgressionSettings>(SettingsPath);
            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 17 built (progression wired into {wired} scene(s)).\n\n" +
                "You now start on the raw Dart with just staples. Earn paper clips by finishing modes, " +
                "shooting down rivals and landing on the mug, then spend them in the mode menu: U / (Y) = UNLOCKS.\n\n" +
                "Testing: PaperWings > Dev > Reset Progress / Unlock Everything / +500 Paper Clips.", "Earn it!");
        }

        static void TagFold(string foldName, string unlockId)
        {
            var fold = AssetDatabase.LoadAssetAtPath<PlaneFold>(S + foldName + ".asset");
            if (fold == null) return;
            fold.unlockId = unlockId;
            EditorUtility.SetDirty(fold);
        }

        // ------------------------------------------------------------------ dev tools

        [MenuItem("PaperWings/Dev/Reset Progress", priority = 200)]
        static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("PaperWings", "Delete all paper clips, unlocks and medals?", "Reset", "Cancel")) return;
            Progression.ResetAll();
            Debug.Log("PaperWings: progress reset. You're back on the raw Dart.");
        }

        [MenuItem("PaperWings/Dev/Unlock Everything", priority = 201)]
        static void UnlockEverything()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ProgressionSettings>(SettingsPath);
            if (settings == null) { Debug.LogWarning("Run Build Stage 17 first."); return; }
            Progression.UnlockAll(settings.AllItems().Select(u => u.id).Append(UnlockIds.Staples));
            Debug.Log("PaperWings: everything unlocked.");
        }

        [MenuItem("PaperWings/Dev/+500 Paper Clips", priority = 202)]
        static void GiveClips()
        {
            Progression.AddClips(500, "dev");
            Debug.Log($"PaperWings: now {Progression.PaperClips} paper clips.");
        }

        [MenuItem("PaperWings/Dev/Open Save Folder", priority = 203)]
        static void OpenSaveFolder() => EditorUtility.RevealInFinder(Progression.SavePath);
    }
}
