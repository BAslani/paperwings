using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using PaperWings.CameraRig;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 11: mouse-aim controls.
    /// Menu: PaperWings > Stage Builders > Stage 11 (Mouse Aim Controls)
    ///
    /// Adds a MouseAimCamera to the chase camera in FlightTest and DeskPrototype (only the
    /// camera object changes). The controls themselves live in PlaneInput + the Controls asset,
    /// so no prefab changes are needed. Safe to re-run.
    /// </summary>
    public static class Stage11Builder
    {
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 11 (Mouse Aim Controls)", priority = 111)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int added = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);

                var vcam = Object.FindAnyObjectByType<CinemachineCamera>();
                var plane = Object.FindAnyObjectByType<PlaneController>();
                // The player's plane is the one with PlaneInput (rivals spawn at runtime, but be safe).
                foreach (var p in Object.FindObjectsByType<PlaneController>(FindObjectsSortMode.None))
                    if (p.GetComponent<PlaneInput>() != null) plane = p;

                if (vcam != null && plane != null)
                {
                    var mouseCam = vcam.GetComponent<MouseAimCamera>();
                    if (mouseCam == null) mouseCam = vcam.gameObject.AddComponent<MouseAimCamera>();
                    BuilderUtil.SetRef(mouseCam, "plane", plane);
                    BuilderUtil.SetRef(mouseCam, "input", plane.GetComponent<PlaneInput>());
                    added++;
                }
                EditorSceneManager.SaveScene(scene);
            }

            var controls = AssetDatabase.LoadAssetAtPath<ControlSettings>("Assets/_Project/Settings/Controls.asset");
            if (controls != null)
            {
                controls.scheme = ControlScheme.Classic;   // keyboard by default; press C in play for mouse aim
                EditorUtility.SetDirty(controls);
                AssetDatabase.SaveAssets();
                Selection.activeObject = controls;
            }

            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 11 built (mouse-aim camera added to {added} scene(s)).\n\n" +
                "Classic (keyboard) stays the default. Press C in play to switch to MOUSE AIM:\n" +
                "  Mouse: move the aim circle, the plane steers toward it\n" +
                "  W/S A/D: manual override while held\n" +
                "  Left click: staples    Right click: pencil (after lock-on)\n" +
                "  Space: throw / boost    C: switch to Classic    Esc: free the cursor\n\n" +
                "Sensitivity and feel: PaperWings > Settings > Controls.", "Fly!");
        }
    }
}
