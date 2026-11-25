using UnityEngine;
using PaperWings.Gameplay;
public static class Rings
{
    public static string Info()
    {
        var r = ModeDirector.Current.Route;
        return r == null ? "no route" : $"rings {r.RingCount} active {r.gameObject.activeInHierarchy} done {r.AllRingsPassed}";
    }
}
