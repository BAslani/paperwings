using UnityEditor;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 6: paper damage and sogginess.
    /// Menu: PaperWings > Build Stage 6 (Paper Damage)
    ///
    /// Adds a PaperDamage component and a "TearBits" particle burst to the PaperDart prefab,
    /// and creates Settings/Paper_Dart. Every scene uses the prefab, so they all pick it up;
    /// no scene changes needed. Safe to re-run (the settings asset is kept).
    /// </summary>
    public static class Stage6Builder
    {
        const string PlanePrefabPath = "Assets/_Project/Prefabs/PaperDart.prefab";

        [MenuItem("PaperWings/Build Stage 6 (Paper Damage)")]
        public static void Build()
        {
            // UNITY NOTE: LoadPrefabContents opens the prefab in a hidden scene for editing;
            // SaveAsPrefabAsset writes it back, and every scene instance updates automatically.
            var root = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
            PlaneFxBuilder.AddPaperDamage(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<PaperSettings>(PlaneFxBuilder.PaperSettingsPath);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 6 built. The paper dart now takes damage from bumps and gets soggy in steam.\n\n" +
                "Tuning: PaperWings > Settings > Paper (Dart).", "Fly!");
        }
    }
}
