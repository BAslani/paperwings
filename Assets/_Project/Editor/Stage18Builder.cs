using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 18: the Night Shift campaign.
    /// Menu: PaperWings > Stage Builders > Stage 18 (Campaign)
    ///
    ///  - Settings/Campaign: ten nights, each a mode plus a twist, a memo and a reward.
    ///  - Hooks the campaign into the ModeDirector in both scenes (the menu gets a Campaign card).
    /// Dev tools: PaperWings > Dev > Open All Nights (Reset Progress also resets the campaign).
    /// </summary>
    public static class Stage18Builder
    {
        const string CampaignPath = "Assets/_Project/Settings/Campaign.asset";
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 18 (Campaign)", priority = 118)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Stage2Builder.LoadOrCreate<CampaignSettings>(CampaignPath);
            AssetDatabase.SaveAssets();

            int wired = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(CampaignPath);   // after opening the scene

                var director = Object.FindAnyObjectByType<ModeDirector>();
                if (director != null)
                {
                    BuilderUtil.SetRef(director, "campaign", campaign);
                    wired++;
                }
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<CampaignSettings>(CampaignPath);
            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 18 built (campaign wired into {wired} scene(s)).\n\n" +
                "The menu now opens with CAMPAIGN: THE NIGHT SHIFT. Ten nights, each a mode with a twist " +
                "(fewer rivals, a time limit, rough air, no pencils) and a first-clear reward.\n\n" +
                "Tune the nights in PaperWings > Settings > Campaign.\n" +
                "Testing: PaperWings > Dev > Open All Nights / Reset Progress.", "Clock in");
        }

        [MenuItem("PaperWings/Dev/Open All Nights", priority = 204)]
        static void OpenAllNights()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(CampaignPath);
            if (campaign == null) { Debug.LogWarning("Run Build Stage 18 first."); return; }
            Progression.ClearNights(campaign.Count - 1);   // every night open, the last one still unbeaten
            Debug.Log("PaperWings: every campaign night is open (rewards for the nights marked cleared are skipped).");
        }
    }
}
