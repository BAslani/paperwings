using System.Reflection;
using UnityEngine;
using PaperWings.Gameplay;
public static class SaveFix
{
    /// <summary>Removes test progress: Tiny War stars and the clips earned while testing.</summary>
    public static string Run(int clipsToRemove)
    {
        var t = typeof(Progression);
        var data = t.GetProperty("Data", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var campaigns = (System.Collections.IList)data.GetType().GetField("campaigns").GetValue(data);
        campaigns.Clear();
        var clips = data.GetType().GetField("paperClips");
        clips.SetValue(data, (int)clips.GetValue(data) - clipsToRemove);
        var total = data.GetType().GetField("totalClipsEarned");
        total.SetValue(data, (int)total.GetValue(data) - clipsToRemove);
        t.GetMethod("SaveAndNotify", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        return "clips now " + Progression.PaperClips + ", tiny war stars " + Progression.TotalStars("tinywar");
    }
}
