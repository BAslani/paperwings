using System.Reflection;
using UnityEngine;
using PaperWings.Gameplay;

public static class CatTest
{
    static CatBrain Cat => Object.FindAnyObjectByType<CatBrain>();

    /// <summary>Play mode: start Free Flight in the kitchen, close the menu, add a camera to watch her with.</summary>
    public static string Setup()
    {
        var d = ModeDirector.Current;
        if (d == null) return "no director (not in play mode?)";
        d.StartMode(GameMode.FreeFlight, 5);
        var menu = Object.FindAnyObjectByType<PaperWings.UI.ModeMenu>();
        if (menu != null)
        {
            var screenType = menu.GetType().GetNestedType("Screen", BindingFlags.NonPublic);
            var show = menu.GetType().GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
            show.Invoke(menu, new[] { System.Enum.Parse(screenType, "None") });
        }
        var cam = GameObject.Find("AgentCam");
        if (cam == null)
        {
            cam = new GameObject("AgentCam");
            var c = cam.AddComponent<Camera>();
            c.fieldOfView = 40f;
            c.nearClipPlane = 0.3f;
            c.depth = 50;
        }
        return Cat != null ? Cat.DebugState : "no cat";
    }

    /// <summary>Points the agent camera at her: distance, yaw around her (0 = in front), height above her, look height.</summary>
    public static string Look(float dist, float yaw, float height, float lookY)
    {
        var cat = Cat;
        var cam = GameObject.Find("AgentCam");
        if (cat == null || cam == null) return "missing";
        var c = cat.transform.position + Vector3.up * lookY;
        var dir = Quaternion.AngleAxis(yaw, Vector3.up) * cat.transform.forward;
        cam.transform.position = c + dir * dist + Vector3.up * height;
        cam.transform.LookAt(c);
        return cat.DebugState;
    }

    /// <summary>Camera at a kitchen-local point (room origin 800,0,800), looking at her (lookY above her feet).</summary>
    public static string From(float x, float y, float z, float lookY)
    {
        var cat = Cat;
        var cam = GameObject.Find("AgentCam");
        if (cat == null || cam == null) return "missing";
        cam.transform.position = new Vector3(800f + x, y, 800f + z);
        cam.transform.LookAt(cat.transform.position + Vector3.up * lookY);
        return cat.DebugState;
    }

    /// <summary>A toy for her to hunt at a kitchen-local point (or remove it with y below 0).</summary>
    public static string Toy(float x, float y, float z)
    {
        var cat = Cat;
        var toy = GameObject.Find("CatToy");
        if (y < 0f) { if (toy != null) Object.Destroy(toy); cat.debugTarget = null; return "toy gone"; }
        if (toy == null)
        {
            toy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            toy.name = "CatToy";
            Object.Destroy(toy.GetComponent<Collider>());
            toy.transform.localScale = Vector3.one * 2f;
        }
        toy.transform.position = new Vector3(800f + x, y, 800f + z);
        cat.debugTarget = toy.transform;
        return cat.DebugState;
    }

    public static string Mode(int mode)
    {
        ModeDirector.Current.StartMode((GameMode)mode, 5);
        return Cat != null ? Cat.DebugState : "no cat";
    }

    public static string Do(string what) => Cat != null ? Cat.DebugDo(what) + " | " + Cat.DebugState : "no cat";
    public static string State() => Cat != null ? Cat.DebugState : "no cat";
    /// <summary>Sets pose tuning fields live: "standHip=14,frontFootFwd=-2".</summary>
    public static string Tune(string pairs)
    {
        var cat = Cat; if (cat == null) return "no cat";
        foreach (var pair in pairs.Split(','))
        {
            var kv = pair.Split('=');
            var f = cat.pose.GetType().GetField(kv[0].Trim());
            if (f == null) return "no field " + kv[0];
            f.SetValue(cat.pose, float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture));
        }
        return "ok";
    }

    public static string Speed(float s) { Time.timeScale = s; return "timeScale " + s; }
}
