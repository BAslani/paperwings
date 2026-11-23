using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PaperWings.Audio;
using PaperWings.Flight;

namespace PaperWings.UI
{
    /// <summary>
    /// Player options from the in-game Options screen: volume, controls, brightness, graphics.
    /// Saved in PlayerPrefs, so they stick in a built game too (where ScriptableObject edits
    /// are NOT saved), and pushed into the live settings assets at startup and on every change.
    ///
    /// UNITY NOTE: In the Editor, changing a ScriptableObject in Play mode changes the asset on disk.
    /// In a build, assets are read-only snapshots, so anything the player picks must be saved
    /// separately; PlayerPrefs is Unity's small per-user key/value store.
    /// </summary>
    public static class GameOptions
    {
        const string P = "PaperWings.Opt.";

        public static ControlSettings Controls { get; private set; }
        public static GameAudioSettings Audio => AudioFx.Settings;

        static ColorAdjustments colorAdjust;
        static float baseExposure;

        /// <summary>Extra exposure on top of the scene's own grade (EV stops). 0 = as built.</summary>
        public static float Brightness
        {
            get => PlayerPrefs.GetFloat(P + "Brightness", 0f);
            set { PlayerPrefs.SetFloat(P + "Brightness", value); ApplyBrightness(); }
        }

        /// <summary>Finds the live settings and applies whatever the player saved before.</summary>
        public static void Init()
        {
            var input = Object.FindAnyObjectByType<PlaneInput>();
            Controls = input != null ? input.Controls : null;

            // A per-scene copy of the post-processing profile, so brightness changes never touch the asset.
            colorAdjust = null;
            var volume = Object.FindAnyObjectByType<Volume>();
            if (volume != null && volume.isGlobal && volume.profile != null && volume.profile.TryGet(out ColorAdjustments ca))
            {
                colorAdjust = ca;
                baseExposure = ca.postExposure.value;
            }

            if (Audio != null)
            {
                Audio.masterVolume = PlayerPrefs.GetFloat(P + "Master", Audio.masterVolume);
                Audio.effectsVolume = PlayerPrefs.GetFloat(P + "Effects", Audio.effectsVolume);
                Audio.ambienceVolume = PlayerPrefs.GetFloat(P + "Ambience", Audio.ambienceVolume);
                Audio.mute = false;
            }
            if (Controls != null)
            {
                Controls.scheme = (ControlScheme)PlayerPrefs.GetInt(P + "Scheme", (int)Controls.scheme);
                Controls.invertPitch = PlayerPrefs.GetInt(P + "Invert", Controls.invertPitch ? 1 : 0) == 1;
                Controls.mouseSensitivity = PlayerPrefs.GetFloat(P + "MouseSens", Controls.mouseSensitivity);
            }
            ApplyBrightness();
        }

        static void ApplyBrightness()
        {
            if (colorAdjust != null) colorAdjust.postExposure.Override(baseExposure + Brightness);
        }

        // ------------------------------------------------------------------ setters (save as they change)

        public static void SetVolume(string which, float v)
        {
            v = Mathf.Clamp01(v);
            if (Audio != null)
            {
                if (which == "Master") Audio.masterVolume = v;
                else if (which == "Effects") Audio.effectsVolume = v;
                else Audio.ambienceVolume = v;
            }
            PlayerPrefs.SetFloat(P + which, v);
        }

        public static float GetVolume(string which)
        {
            if (Audio == null) return 0f;
            return which == "Master" ? Audio.masterVolume : which == "Effects" ? Audio.effectsVolume : Audio.ambienceVolume;
        }

        public static void SetScheme(ControlScheme s)
        {
            if (Controls != null) Controls.scheme = s;
            PlayerPrefs.SetInt(P + "Scheme", (int)s);
        }

        /// <summary>Called when C switches schemes mid-game, so the choice is remembered.</summary>
        public static void RememberScheme(ControlScheme s) => PlayerPrefs.SetInt(P + "Scheme", (int)s);

        public static void SetInvert(bool on)
        {
            if (Controls != null) Controls.invertPitch = on;
            PlayerPrefs.SetInt(P + "Invert", on ? 1 : 0);
        }

        public static void SetMouseSensitivity(float v)
        {
            v = Mathf.Clamp(v, 0.01f, 0.5f);
            if (Controls != null) Controls.mouseSensitivity = v;
            PlayerPrefs.SetFloat(P + "MouseSens", v);
        }

        // Graphics: Unity saves quality, fullscreen and resolution itself in a build.
        public static void SetQuality(int level)
        {
            level = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(level, true);
        }

        public static void SetFullscreen(bool on)
            => UnityEngine.Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        /// <summary>Distinct screen sizes the monitor supports (refresh rates folded together).</summary>
        public static Vector2Int[] Resolutions()
        {
            var list = new System.Collections.Generic.List<Vector2Int>();
            foreach (var r in UnityEngine.Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v)) list.Add(v);
            }
            if (list.Count == 0) list.Add(new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height));
            return list.ToArray();
        }

        public static void SetResolution(Vector2Int size)
            => UnityEngine.Screen.SetResolution(size.x, size.y, UnityEngine.Screen.fullScreenMode);

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
