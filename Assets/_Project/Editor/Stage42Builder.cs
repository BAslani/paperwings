using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 42: the stairwell is removed from the game.
    /// Menu: PaperWings > Stage Builders > Stage 42 (Remove Stairwell)
    ///
    ///  - Deletes the Stairwell from DeskPrototype and empties its room slot (3). The slot stays,
    ///    empty, so the rooms after it keep their numbers: campaign nights and players' saved best
    ///    times and medals refer to rooms by number. The room picker skips empty slots, and a player
    ///    whose last room was the stairwell starts in the office.
    ///  - Takes any campaign night still set in room 3 out of the campaign (there are none since
    ///    Stage 36, but older campaign assets may have one).
    ///  - Deletes the stairwell-only assets once nothing in the project uses them.
    /// Stage 25 no longer builds the stairwell, so re-running it won't bring it back. Safe to re-run.
    /// </summary>
    public static class Stage42Builder
    {
        const string CampaignPath = StageKit.Root + "Settings/Campaign.asset";

        static readonly string[] StairwellAssets =
        {
            StageKit.Root + "Art/M_StairWall.mat",
            StageKit.Root + "Art/M_FireDoor.mat",
            StageKit.Root + "Art/M_Handrail.mat",
            StageKit.Root + "Art/M_Concrete.mat",
            StageKit.Root + "Settings/Thermal_Chimney.asset",
        };

        [MenuItem("PaperWings/Stage Builders/Stage 42 (Remove Stairwell)", priority = 154)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out var scene)) return;

            var root = StageKit.FindRoot(scene, "Stairwell");
            bool hadRoom = root != null;
            if (root != null) Object.DestroyImmediate(root.gameObject);
            if (HasSlot()) StageKit.SetRoom(StageKit.Stairwell, null);
            StageKit.Save(scene);

            int nights = RemoveNights();
            int deleted = DeleteUnusedAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 42 built: the stairwell is gone.\n\n" +
                (hadRoom ? "- Removed the Stairwell from the scene; its room slot is now empty.\n" : "- The Stairwell was already gone from the scene.\n") +
                (nights > 0 ? $"- Took {nights} stairwell night(s) out of the campaign.\n" : "- No campaign nights were set in the stairwell.\n") +
                $"- Deleted {deleted} stairwell-only asset(s).\n\n" +
                "The room picker skips the empty slot; the other rooms keep their numbers.", "OK");
        }

        static bool HasSlot()
        {
            var director = StageKit.Director();
            if (director == null) return false;
            return new SerializedObject(director).FindProperty("rooms").arraySize > StageKit.Stairwell;
        }

        static int RemoveNights()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignSettings>(CampaignPath);
            if (campaign == null || campaign.nights == null) return 0;
            int before = campaign.nights.Length;
            campaign.nights = System.Array.FindAll(campaign.nights, n => n != null && n.room != StageKit.Stairwell);
            int removed = before - campaign.nights.Length;
            if (removed > 0)
            {
                EditorUtility.SetDirty(campaign);
                AssetDatabase.SaveAssets();
            }
            return removed;
        }

        /// <summary>Deletes each stairwell asset unless a scene or prefab still depends on it.</summary>
        static int DeleteUnusedAssets()
        {
            var used = new HashSet<string>();
            var users = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" })) users.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var dep in AssetDatabase.GetDependencies(users.ToArray(), true)) used.Add(dep);

            int deleted = 0;
            foreach (var path in StairwellAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null || used.Contains(path)) continue;
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }
            return deleted;
        }
    }
}
