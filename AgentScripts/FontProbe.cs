using System.Reflection;
using UnityEngine;
public static class FontProbe
{
    public static string Run()
    {
        var t = typeof(PaperWings.UI.ModeMenu);
        var f = (Font)t.GetField("heavy", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var w = (Font)t.GetField("typewriter", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        string os = string.Join(",", System.Array.FindAll(Font.GetOSInstalledFontNames(), n => n.Contains("Arial") || n.Contains("Impact")));
        return $"heavy={(f ? f.name + " dyn=" + f.dynamic + " names=" + string.Join("/", f.fontNames) : "null")} type={(w ? w.name : "null")} os={os}";
    }
}
