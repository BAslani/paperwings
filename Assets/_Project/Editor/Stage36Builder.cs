using UnityEditor;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 36: Fire Escape is cut from the campaign (eleven nights now).
    /// Menu: PaperWings > Stage Builders > Stage 36 (Remove Fire Escape)
    ///
    /// Takes the night out of the saved Campaign asset and moves it to campaign version 5. Players'
    /// saves follow automatically on the next Play (Progression.SyncCampaign): Squall and The Condenser
    /// keep their stars. The Pencil Rack blueprint (night index 9) now comes from Squall.
    /// The stairwell room itself stays for free play. Assets only. Safe to re-run.
    /// </summary>
    public static class Stage36Builder
    {
        const string CampaignPath = "Assets/_Project/Settings/Campaign.asset";

        [MenuItem("PaperWings/Stage Builders/Stage 36 (Remove Fire Escape)", priority = 148)]
        public static void Build()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(CampaignPath);
            if (campaign == null || campaign.nights == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Settings/Campaign.asset not found.", "OK");
                return;
            }

            int before = campaign.nights.Length;
            campaign.nights = System.Array.FindAll(campaign.nights, n => n != null && n.title != "Fire Escape");
            campaign.version = CampaignSettings.CurrentVersion;
            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                before != campaign.nights.Length
                    ? $"Stage 36 built: Fire Escape removed. The campaign has {campaign.nights.Length} nights.\n\nSaved stars move over on the next Play."
                    : $"Fire Escape was already gone ({campaign.nights.Length} nights). Campaign version set to {campaign.version}.",
                "OK");
        }
    }
}
