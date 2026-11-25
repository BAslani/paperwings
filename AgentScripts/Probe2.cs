using System.Text;
using UnityEngine;
public static class Probe2
{
    public static string Near(float x, float y, float z, float r)
    {
        var o = new Vector3(800f, 0f, 800f);
        var c = o + new Vector3(x, y, z);
        var sb = new StringBuilder();
        foreach (var rend in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var b = rend.bounds;
            if (b.SqrDistance(c) > r * r) continue;
            var col = rend.GetComponent<Collider>() != null ? "C" : " ";
            sb.AppendLine($"{col} {rend.name} [{rend.transform.parent?.name}] min({b.min.x - o.x:0.#},{b.min.y:0.#},{b.min.z - o.z:0.#}) max({b.max.x - o.x:0.#},{b.max.y:0.#},{b.max.z - o.z:0.#})");
        }
        return sb.ToString();
    }
}
