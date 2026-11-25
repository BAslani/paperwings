using UnityEngine;
public static class Cam2
{
    public static string At(float x, float y, float z, string target)
    {
        var cam = GameObject.Find("AgentCam");
        if (cam == null) { var c = new GameObject("AgentCam").AddComponent<Camera>(); c.fieldOfView = 50f; c.depth = 50; cam = c.gameObject; }
        var t = GameObject.Find(target);
        cam.transform.position = new Vector3(x, y, z);
        cam.transform.LookAt(t != null ? t.transform.position + Vector3.up * 12f : Vector3.zero);
        return t != null ? t.transform.position.ToString() : "no " + target;
    }
}
