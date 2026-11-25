using System.Reflection;
using UnityEngine;
using PaperWings.Gameplay;
using PaperWings.UI;

public static class MenuTest
{
    static ModeMenu Menu => Object.FindAnyObjectByType<ModeMenu>();
    static object Call(string method, params object[] args)
    {
        var m = typeof(ModeMenu).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        return m.Invoke(Menu, args);
    }
    static void Show(string screen)
    {
        var t = typeof(ModeMenu).GetNestedType("Screen", BindingFlags.NonPublic);
        Call("Show", System.Enum.Parse(t, screen));
    }

    public static string MainMenu() { Show("Menu"); return "menu"; }

    /// <summary>Highlights a main-menu entry (0-based) without choosing it.</summary>
    public static string Select(int entry)
    {
        typeof(ModeMenu).GetField("selected", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Menu, entry);
        return "selected " + entry;
    }

    /// <summary>Opens a campaign's roster (0 = Night Shift, 1 = Tiny War).</summary>
    public static string Roster(int campaign, int level)
    {
        var d = ModeDirector.Current;
        d.SelectCampaign(d.CampaignAt(campaign));
        typeof(ModeMenu).GetField("nightSelected", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Menu, level);
        Show("Campaign");
        return d.Campaign.displayName + " " + d.Campaign.Count;
    }

    public static string Play(int campaign, int level)
    {
        var d = ModeDirector.Current;
        d.SelectCampaign(d.CampaignAt(campaign));
        Call("PlayNight", level);
        return $"{d.Campaign.displayName} night {d.NightIndex} mode {d.Mode} room {d.RoomIndex}";
    }

    public static string Fly() { Show("None"); return "flying"; }

    /// <summary>Test only: marks the first n levels of a campaign cleared (1 star).</summary>
    public static string Unlock(int campaign, int n)
    {
        var d = ModeDirector.Current;
        d.SelectCampaign(d.CampaignAt(campaign));
        Progression.ClearNights(n);
        return "unlocked " + n;
    }

    public static string Status()
    {
        var d = ModeDirector.Current;
        var b = d.Boss;
        string boss = b != null ? $" boss {b.DisplayName} hp {b.Health01:0.00} awake {b.IsAwake} weak {b.WeakSpotOpen} warn {b.WarningText}" : "";
        var cat = Object.FindAnyObjectByType<CatBrain>();
        return $"mode {d.Mode} night {d.NightIndex} run {d.RunTime:0.0} complete {d.RunComplete} kills {d.KillsThisRun} obj '{d.ObjectiveText()}'{boss} cat {(cat != null ? cat.DebugState : "-")}";
    }

    /// <summary>Throws the player's plane (as if Space was held and released).</summary>
    public static string Throw()
    {
        var l = Object.FindAnyObjectByType<LaunchAndReset>();
        var m = typeof(LaunchAndReset).GetMethod("Throw", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (m == null) return "no Launch method";
        var ps = m.GetParameters();
        m.Invoke(l, ps.Length == 0 ? null : new object[] { 1f });
        return "thrown";
    }
}
