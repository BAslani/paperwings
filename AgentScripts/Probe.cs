using System.Text;
using UnityEngine;
using UnityEditor;

public static class Probe
{
    /// <summary>Prints the cat's bone tree with world positions (relative to the cat root, in the cat's own frame) in the prefab's saved pose.</summary>
    public static string CatBones()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Biscuit.prefab");
        var go = (GameObject)Object.Instantiate(prefab);
        try
        {
            var brain = go.GetComponent<PaperWings.Gameplay.CatBrain>();
            if (brain != null && brain.idleClip != null) brain.idleClip.SampleAnimation(go, brain.restTime);
            var sb = new StringBuilder();
            Walk(go.transform, go.transform, 0, sb);
            return sb.ToString();
        }
        finally { Object.DestroyImmediate(go); }
    }

    static void Walk(Transform root, Transform t, int depth, StringBuilder sb)
    {
        var p = root.InverseTransformPoint(t.position);
        if (t.name.StartsWith("j_") || depth < 3)
            sb.AppendLine($"{new string(' ', depth)}{t.name}  ({p.x:0.0},{p.y:0.0},{p.z:0.0})");
        foreach (Transform c in t) Walk(root, c, depth + 1, sb);
    }
}
