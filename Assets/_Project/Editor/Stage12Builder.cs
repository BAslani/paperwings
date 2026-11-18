using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Flight;
using PaperWings.Gameplay;
using PaperWings.UI;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 12: game modes.
    /// Menu: PaperWings > Build Stage 12 (Game Modes)
    ///
    /// Adds a "GameModes" object to DeskPrototype with a ModeDirector (switches the ring route,
    /// rivals and boss on/off per mode, tracks objectives and best times) and a ModeMenu
    /// (mode select, results, objective strip). Safe to re-run.
    /// </summary>
    public static class Stage12Builder
    {
        const string DeskScene = "Assets/_Project/Scenes/DeskPrototype.unity";

        [MenuItem("PaperWings/Build Stage 12 (Game Modes)", priority = 112)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "DeskPrototype scene not found. Run the earlier stages first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(DeskScene);

            var old = FindByName(scene, "GameModes");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("GameModes");

            var launcher = Object.FindAnyObjectByType<LaunchAndReset>();
            PlaneController player = null;
            foreach (var p in Object.FindObjectsByType<PlaneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.GetComponent<PlaneInput>() != null) player = p;

            // The groups each mode switches. They may currently be inactive, so search including inactive.
            var route = Object.FindAnyObjectByType<RaceRoute>(FindObjectsInactive.Include);
            var combat = FindByName(scene, "Combat");
            var boss = FindByName(scene, "Boss");

            var director = go.AddComponent<ModeDirector>();
            BuilderUtil.SetRef(director, "launcher", launcher);
            BuilderUtil.SetRef(director, "player", player);
            BuilderUtil.SetRef(director, "routeGroup", route != null ? route.gameObject : null);
            BuilderUtil.SetRef(director, "combatGroup", combat);
            BuilderUtil.SetRef(director, "bossGroup", boss);

            var menu = go.AddComponent<ModeMenu>();
            BuilderUtil.SetRef(menu, "director", director);

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;

            string missing = (route == null ? " ring route" : "") + (combat == null ? " rivals(Combat)" : "") + (boss == null ? " boss" : "");
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 12 built. Press Play in DeskPrototype: the mode menu opens.\n\n" +
                "  1 Free Flight   2 Ring Run   3 Dogfight   4 Boss Fight\n" +
                "Esc / M / gamepad Start: back to the menu (pauses).\n" +
                (missing.Length > 0 ? $"\nNot found in the scene (those modes will be empty):{missing}" : ""), "Play!");
        }

        /// <summary>Finds an object by name anywhere in the scene, including inactive ones.</summary>
        static GameObject FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}
