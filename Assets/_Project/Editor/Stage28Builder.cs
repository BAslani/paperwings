using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.UI;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 28: the hangar, a 3D preview of your plane.
    /// Menu: PaperWings > Stage Builders > Stage 28 (Hangar Preview)
    ///
    /// Adds a PlanePreview (a turntable filmed into a texture) next to the mode menu in both scenes.
    /// The Unlocks screen becomes the HANGAR: the fold you're looking at turns on its stand with every
    /// upgrade it owns fitted (pencils under the wings, the staple magazine under the nose, heavy paper
    /// as a second sheet, the pusher prop on the tail). Select an upgrade you don't own and it appears
    /// as a glowing blue blueprint where it would go. F / (X) flies that fold. The in-hand fold card
    /// (Q / E) shows the turntable too. Safe to re-run.
    ///
    /// Also: PaperWings > Build Stages 23-28 (All New) runs Stages 23 to 28 in order.
    /// </summary>
    public static class Stage28Builder
    {
        static readonly string[] Scenes = { StageKit.DeskScene, StageKit.Root + "Scenes/FlightTest.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 28 (Hangar Preview)", priority = 128)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int n = Run();
            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 28 built: the hangar preview is in {n} scene(s).\n\nOpen the menu and press U (or Y) for the HANGAR, or hold the plane and press Q / E.", "Show me");
        }

        [MenuItem("PaperWings/Stage Builders/Stages 23-28 (All New)", priority = 129)]
        public static void BuildAll()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            Stage23Builder.Run(scene);
            if (!Stage24Builder.Run(scene)) return;
            if (!Stage25Builder.Run(scene)) return;
            if (!Stage26Builder.Run(scene)) return;
            string report = Stage27Builder.Run(scene);
            StageKit.Save(scene);
            Run();
            EditorUtility.DisplayDialog("PaperWings",
                "Stages 23-28 built.\n\n- Scale fixes, one colour per air type, faction paper and trails.\n- Office air: radiators, ceiling fan, heater, pendant lamps.\n" +
                "- New rooms: Server Room, Stairwell, Rooftop.\n- Bosses: The Mainframe, The Condenser.\n- Campaign: twelve nights, twelve places.\n- Hangar: 3D plane preview.\n\n" + report, "Fly");
        }

        /// <summary>Adds / refreshes the preview in every scene that has a mode menu. Returns how many.</summary>
        internal static int Run()
        {
            var ghost = StageKit.Emissive("M_PreviewGhost", new Color(0.35f, 0.65f, 1f), new Color(0.35f, 0.7f, 1.4f));
            ghost.SetFloat("_Smoothness", 0.6f);
            EditorUtility.SetDirty(ghost);
            var paper = StageKit.Load<Material>("Art/M_Paper.mat");
            var pencilBody = StageKit.Load<Material>("Art/M_PencilBody.mat");
            var pencilTip = StageKit.Load<Material>("Art/M_PencilLead.mat");
            var eraser = StageKit.Load<Material>("Art/M_PencilEraser.mat");
            var metal = StageKit.Load<Material>("Art/M_BrushedSteel.mat");
            var prop = StageKit.Load<GameObject>("Prefabs/Propeller.prefab");
            AssetDatabase.SaveAssets();

            int count = 0;
            string start = SceneManager.GetActiveScene().path;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = SceneManager.GetActiveScene().path == path ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(path);
                var menu = Object.FindAnyObjectByType<ModeMenu>(FindObjectsInactive.Include);
                if (menu == null) continue;
                var preview = menu.GetComponent<PlanePreview>();
                if (preview == null) preview = menu.gameObject.AddComponent<PlanePreview>();
                BuilderUtil.SetRef(preview, "paperMaterial", paper);
                BuilderUtil.SetRef(preview, "ghostMaterial", ghost);
                BuilderUtil.SetRef(preview, "pencilBody", pencilBody);
                BuilderUtil.SetRef(preview, "pencilTip", pencilTip);
                BuilderUtil.SetRef(preview, "pencilEraser", eraser);
                BuilderUtil.SetRef(preview, "magazineMetal", metal);
                BuilderUtil.SetRef(preview, "propellerPrefab", prop);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                count++;
            }
            if (!string.IsNullOrEmpty(start) && SceneManager.GetActiveScene().path != start) EditorSceneManager.OpenScene(start);
            return count;
        }
    }
}
