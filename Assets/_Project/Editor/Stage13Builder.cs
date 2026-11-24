using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Audio;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 13: sound.
    /// Menu: PaperWings > Stage Builders > Stage 13 (Sound)
    ///
    /// All sounds are synthesized in code (SoundBank), so this only wires things up:
    ///  - Settings/Audio (volume mix)
    ///  - PlaneAudio on the PaperDart prefab (wind, flutter, air, plane events)
    ///  - an "Audio" object with an AudioDirector in FlightTest and DeskPrototype
    /// Safe to re-run.
    /// </summary>
    public static class Stage13Builder
    {
        const string SettingsPath = "Assets/_Project/Settings/Audio.asset";
        const string PlanePrefabPath = "Assets/_Project/Prefabs/PaperDart.prefab";
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 13 (Sound)", priority = 113)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Stage2Builder.LoadOrCreate<GameAudioSettings>(SettingsPath);

            var root = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
            if (root.GetComponent<PlaneAudio>() == null) root.AddComponent<PlaneAudio>();
            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(SettingsPath);   // after opening the scene

                GameObject go = null;
                foreach (var r in scene.GetRootGameObjects()) if (r.name == "Audio") go = r;
                if (go == null) go = new GameObject("Audio");
                var director = go.GetComponent<AudioDirector>();
                if (director == null) director = go.AddComponent<AudioDirector>();
                BuilderUtil.SetRef(director, "settings", settings);

                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(SettingsPath);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 13 built. Every sound is synthesized in code, no audio files needed.\n\n" +
                "Volume mix: PaperWings > Settings > Audio (master, effects, ambience, UI, mute).", "Listen!");
        }
    }
}
