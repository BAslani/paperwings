using UnityEditor;
using UnityEngine;
using PaperWings.Flight;
using PaperWings.Combat;
using PaperWings.Gameplay;

public static class BossTest
{
    static EditorApplication.CallbackFunction tick;
    static Vector3 hoverAt;

    /// <summary>Keeps the player's plane flying in a slow circle at a kitchen-local point (radius r).</summary>
    public static string Hover(float x, float y, float z, float r)
    {
        Unhover();
        hoverAt = new Vector3(800f + x, y, 800f + z);
        float start = Time.time;
        tick = () =>
        {
            if (!Application.isPlaying) { Unhover(); return; }
            PlaneController p = null;
            foreach (var pc in PlaneController.All) if (pc.IsPlayer) p = pc;
            if (p == null) return;
            if (p.State != PlaneState.Flying) { p.Launch(12f, 0f); }
            float a = (Time.time - start) * 0.5f;
            var pos = hoverAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            var vel = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * r * 0.5f;
            var rb = p.GetComponent<Rigidbody>();
            rb.position = pos;
            p.transform.position = pos;
            rb.linearVelocity = vel.sqrMagnitude > 0.01f ? vel : Vector3.forward * 8f;
            p.transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized, Vector3.up);
            var paper = p.GetComponent<PaperDamage>();
        };
        EditorApplication.update += tick;
        return "hovering";
    }

    public static string Unhover() { if (tick != null) EditorApplication.update -= tick; tick = null; return "free"; }

    public static string Hit(float amount)
    {
        var b = BossBase.Active;
        if (b == null) return "no boss";
        b.ApplyDamage(amount, null);
        return $"{b.DisplayName} hp {b.Health01:0.00} weak {b.WeakSpotOpen}";
    }

    public static string BreakParts()
    {
        var b = BossBase.Active;
        if (b == null) return "no boss";
        int n = 0;
        foreach (var part in b.GetComponentsInChildren<BossPart>()) { part.ApplyDamage(9999f, null); n++; }
        return $"broke {n}; hp {b.Health01:0.00}";
    }
}
