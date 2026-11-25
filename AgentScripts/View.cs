using System.Reflection;
using UnityEngine;
using PaperWings.Gameplay;
public static class View
{
    /// <summary>Free flight in a room, menu closed, an agent camera ready.</summary>
    public static string Room(int room)
    {
        ModeDirector.Current.StartMode(GameMode.FreeFlight, room);
        var menu = Object.FindAnyObjectByType<PaperWings.UI.ModeMenu>();
        var t = menu.GetType().GetNestedType("Screen", BindingFlags.NonPublic);
        menu.GetType().GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, new[] { System.Enum.Parse(t, "None") });
        if (GameObject.Find("AgentCam") == null) { var c = new GameObject("AgentCam").AddComponent<Camera>(); c.fieldOfView = 55f; c.nearClipPlane = 0.3f; c.depth = 50; }
        return "room " + room;
    }
    /// <summary>Camera at a room-local point (origin ox,oz) looking at another.</summary>
    public static string Cam(float ox, float oz, float x, float y, float z, float lx, float ly, float lz)
    {
        var cam = GameObject.Find("AgentCam");
        cam.transform.position = new Vector3(ox + x, y, oz + z);
        cam.transform.LookAt(new Vector3(ox + lx, ly, oz + lz));
        return "ok";
    }
}
