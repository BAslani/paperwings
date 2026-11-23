using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using PaperWings.Gameplay;
using PaperWings.UI;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 20: options and a playable build.
    /// Menu: PaperWings > Build Stage 20 (Options, Brighter Office)
    ///
    ///  - Brightens the night office: stronger moon and lamp, lighter ambient, a soft ceiling fill
    ///    light and more exposure. (Brightness is also an in-game option now.)
    ///  - Adds the F1 dev panel to DeskPrototype (clips, unlocks, nights, reset; works in builds).
    ///  - The Options screen itself is part of the mode menu: O / (Select).
    /// Separate menu item: PaperWings > Build Windows Player.
    /// </summary>
    public static class Stage20Builder
    {
        const string DeskScene = "Assets/_Project/Scenes/DeskPrototype.unity";
        const string NightProfile = "Assets/_Project/Art/PostFX_Night.asset";
        const string BuildPath = "Builds/Windows/PaperWings.exe";

        [MenuItem("PaperWings/Build Stage 20 (Options, Brighter Office)", priority = 120)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "DeskPrototype scene not found. Run the earlier stages first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BrightenProfile();
            var scene = EditorSceneManager.OpenScene(DeskScene);
            BrightenLights(scene);

            var director = Object.FindAnyObjectByType<ModeDirector>();
            if (director != null)
            {
                var panel = director.GetComponent<DevPanel>();
                if (panel == null) panel = director.gameObject.AddComponent<DevPanel>();
                BuilderUtil.SetRef(panel, "director", director);
            }
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 20 built.\n\n" +
                "The office is brighter (and Brightness is in Options).\n" +
                "Mode menu: O / (Select) = OPTIONS: volume, controls, invert, mouse sensitivity, brightness, " +
                "graphics quality, fullscreen, resolution, quit.\n" +
                "F1 = dev panel (also in builds).\n\n" +
                "To make a standalone game: PaperWings > Build Windows Player.", "Lights on");
        }

        // ------------------------------------------------------------------ lighting

        static void BrightenProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NightProfile);
            if (profile == null) return;
            if (profile.TryGet(out ColorAdjustments color)) { color.postExposure.Override(0.95f); color.contrast.Override(10f); }
            if (profile.TryGet(out Vignette vignette)) vignette.intensity.Override(0.2f);
            if (profile.TryGet(out SplitToning split)) split.balance.Override(10f);   // less blue in the shadows
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        static void BrightenLights(Scene scene)
        {
            // Still night time, but you can see the room: brighter moon, lamp and screen, lighter ambient.
            SetLight(scene, "Moonlight", new Color(0.7f, 0.78f, 1f), 0.9f, null);
            SetLight(scene, "DeskLampLight", new Color(1f, 0.82f, 0.6f), 14f, 140f);
            SetLight(scene, "MonitorGlow", new Color(0.5f, 0.65f, 1f), 5f, 40f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.36f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.23f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.12f);
            RenderSettings.fogColor = new Color(0.1f, 0.11f, 0.16f);

            // Soft "ceiling lights left on down the hall" fill from above, no shadows.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "CeilingFill") Object.DestroyImmediate(root);
            var fill = new GameObject("CeilingFill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.96f, 0.88f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(80f, 30f, 0f);
        }

        static void SetLight(Scene scene, string name, Color color, float intensity, float? range)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.name != name) continue;
                    l.color = color;
                    l.intensity = intensity;
                    if (range.HasValue) l.range = range.Value;
                }
        }

        // ------------------------------------------------------------------ build

        /// <summary>
        /// Builds a standalone Windows game into Builds/Windows (git-ignored).
        /// UNITY NOTE: BuildPipeline.BuildPlayer is what File > Build Profiles > Build does; scenes in
        /// the list are included, and the first one loads when the game starts.
        /// </summary>
        [MenuItem("PaperWings/Build Windows Player", priority = 300)]
        public static void BuildWindowsPlayer()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "DeskPrototype scene not found.", "OK");
                return;
            }

            PlayerSettings.productName = "Paper Wings";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DeskScene, true) };   // the game is the desk scene

            var options = new BuildPlayerOptions
            {
                scenes = new[] { DeskScene },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                if (EditorUtility.DisplayDialog("PaperWings",
                        $"Build done: {BuildPath}\n{summary.totalSize / (1024f * 1024f):0} MB in {summary.totalTime.TotalSeconds:0}s.\n\n" +
                        "Zip the whole Builds/Windows folder to share it (the .exe needs the _Data folder next to it).",
                        "Show folder", "OK"))
                    EditorUtility.RevealInFinder(BuildPath);
            }
            else
            {
                EditorUtility.DisplayDialog("PaperWings", $"Build {summary.result} with {summary.totalErrors} error(s). See the Console.", "OK");
            }
        }
    }
}
