using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class RunStage
{
    /// <summary>Calls a builder's internal Run() (no dialogs) and saves the scene.</summary>
    public static string Run(string typeName)
    {
        var t = System.Type.GetType(typeName + ", Assembly-CSharp-Editor");
        if (t == null) return "no type " + typeName;
        var m = t.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var ps = m.GetParameters();
        object r = ps.Length == 0 ? m.Invoke(null, null) : m.Invoke(null, new object[] { EditorSceneManager.GetActiveScene() });
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return r == null ? "null" : r.ToString();
    }
}
