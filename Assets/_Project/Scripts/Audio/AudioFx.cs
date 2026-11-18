using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Audio
{
    /// <summary>
    /// Fire-and-forget sound playback from anywhere:
    ///   AudioFx.Play(Sfx.Boost)                 2D (the player's own sounds)
    ///   AudioFx.PlayAt(Sfx.StapleHit, position) 3D (quieter with distance, panned left/right)
    ///   AudioFx.PlayUi(Sfx.UiMove)              2D, still plays while the game is paused
    /// Uses a small pool of AudioSources on a hidden object, created on first use.
    ///
    /// UNITY NOTE: an AudioSource plays clips; the AudioListener (on the Main Camera) "hears" them.
    /// spatialBlend = 0 means 2D (same everywhere), 1 means fully 3D.
    /// </summary>
    public static class AudioFx
    {
        /// <summary>The active volume mix (set by the scene's AudioDirector). Null = defaults.</summary>
        public static GameAudioSettings Settings { get; set; }

        const int PoolSize = 24;
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static int next;

        public static float EffectsVolume => Settings == null ? 1f : (Settings.mute ? 0f : Settings.effectsVolume);
        public static float AmbienceVolume => Settings == null ? 0.7f : (Settings.mute ? 0f : Settings.ambienceVolume);
        public static float UiVolume => Settings == null ? 0.7f : (Settings.mute ? 0f : Settings.uiVolume);

        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchJitter = 0.05f)
            => PlayInternal(sfx, Vector3.zero, false, volume * EffectsVolume, pitch, pitchJitter, false);

        public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f)
            => PlayInternal(sfx, position, true, volume * EffectsVolume, pitch, pitchJitter, false);

        public static void PlayUi(Sfx sfx, float volume = 1f)
            => PlayInternal(sfx, Vector3.zero, false, volume * UiVolume, 1f, 0f, true);

        static void PlayInternal(Sfx sfx, Vector3 pos, bool spatial, float volume, float pitch, float jitter, bool ignorePause)
        {
            if (volume <= 0.001f) return;
            AudioSource src = NextSource();
            src.transform.position = pos;
            src.spatialBlend = spatial ? 1f : 0f;
            src.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            src.volume = volume;
            src.ignoreListenerPause = ignorePause;
            src.clip = SoundBank.Get(sfx);
            src.Play();
        }

        static AudioSource NextSource()
        {
            if (pool.Count == 0 || pool[0] == null)
            {
                pool.Clear();
                var host = new GameObject("AudioFx (pool)") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(host);
                for (int i = 0; i < PoolSize; i++)
                {
                    var child = new GameObject("Voice");
                    child.transform.SetParent(host.transform, false);
                    var s = child.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.rolloffMode = AudioRolloffMode.Logarithmic;
                    s.minDistance = 6f;      // full volume inside this distance
                    s.maxDistance = 150f;
                    s.dopplerLevel = 0f;     // no pitch wobble from movement (it sounds odd with synth sounds)
                    pool.Add(s);
                }
            }
            // Prefer a free voice; otherwise steal the oldest.
            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[(next + i) % pool.Count];
                if (!s.isPlaying) { next = (next + i + 1) % pool.Count; return s; }
            }
            var stolen = pool[next];
            next = (next + 1) % pool.Count;
            return stolen;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool.Clear();
            next = 0;
            Settings = null;
        }
    }
}
