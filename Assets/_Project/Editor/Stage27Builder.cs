using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 27: the campaign, rebuilt so every night is a new place.
    /// Menu: PaperWings > Stage Builders > Stage 27 (Campaign: One Place per Night)
    ///
    /// Twelve nights climbing the building: four in the office (the lounge, the window row, the
    /// coworker's corner, your desk for The Stapler), two in the break room, two in the server room
    /// (ending with The Mainframe), the stairwell chimney, and three on the roof (ending with The
    /// Condenser). Twists like "rough air" and "no pencils" are gone: the new spaces and their air
    /// are the variety. See CampaignSettings.NightShift() for the list.
    ///
    /// Writes the nights into Settings/Campaign (campaign version 2: saved stars from the old campaign
    /// start over, clips and unlocks stay), adds the break room's couch throw point, and checks that
    /// every room, route and throw point a night asks for exists. Run Stages 24-26 first.
    /// </summary>
    public static class Stage27Builder
    {
        [MenuItem("PaperWings/Stage Builders/Stage 27 (Campaign: One Place per Night)", priority = 127)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            string report = Run(scene);
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 27 built: the Night Shift is twelve nights in twelve places.\n\n" + report, "OK");
        }

        internal static string Run(Scene scene)
        {
            var campaign = StageKit.Load<CampaignSettings>("Settings/Campaign.asset");
            if (campaign == null) return "No Settings/Campaign asset (run Stage 18).";
            campaign.nights = CampaignSettings.NightShift();
            campaign.version = CampaignSettings.CurrentVersion;
            EditorUtility.SetDirty(campaign);
            AssetDatabase.SaveAssets();

            AddCouchSpawn();
            return Check(campaign);
        }

        /// <summary>The break room's couch end: a throw point on the couch back, facing the kitchen.</summary>
        static void AddCouchSpawn()
        {
            var room = StageKit.RoomAt(StageKit.BreakRoom);
            if (room == null) return;
            var old = room.transform.Find("CouchSpawn");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var spawn = StageKit.Spawn("CouchSpawn", room.transform, new Vector3(640f, 31f, -106f), 0f);
            room.extraSpawns = new[] { spawn };
            EditorUtility.SetDirty(room);
        }

        /// <summary>Lists any night whose room, route or throw point is missing (it would fall back to the office).</summary>
        static string Check(CampaignSettings campaign)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < campaign.Count; i++)
            {
                var n = campaign.Get(i);
                var room = StageKit.RoomAt(n.room);
                if (room == null) { sb.AppendLine($"Night {i + 1} '{n.title}': room {n.room} missing (run Stage 25)."); continue; }
                if (!room.Supports(n.mode)) sb.AppendLine($"Night {i + 1} '{n.title}': {room.displayName} has no {ModeDirector.ModeName(n.mode)} (run Stage 25/26).");
                if (!string.IsNullOrEmpty(n.area) && room.RouteNamed(n.area) == null) sb.AppendLine($"Night {i + 1} '{n.title}': route '{n.area}' missing (run Stage 24).");
                if (!string.IsNullOrEmpty(n.spawn) && room.SpawnNamed(n.spawn) == null) sb.AppendLine($"Night {i + 1} '{n.title}': throw point '{n.spawn}' missing.");
                if (!string.IsNullOrEmpty(n.group) && room.GroupNamed(n.group) == null) sb.AppendLine($"Night {i + 1} '{n.title}': mission group '{n.group}' missing (run Stages 29-32).");
            }
            return sb.Length == 0 ? "Every night's room, route and throw point is in place." : "Missing pieces:\n" + sb;
        }
    }
}
