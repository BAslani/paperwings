using UnityEngine;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.Audio
{
    /// <summary>
    /// One per scene. Applies the volume mix, mutes everything while the game is paused, and plays
    /// "big moment" sounds that don't belong to one object: rings, kills, mode victories.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        [SerializeField] GameAudioSettings settings;

        RaceRoute route;
        ModeDirector modes;

        void Awake() => AudioFx.Settings = settings;

        void Start()
        {
            // Synthesize every sound now (a few milliseconds), so nothing hitches mid-fight later.
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) SoundBank.Get(s);

            route = FindAnyObjectByType<RaceRoute>(FindObjectsInactive.Include);
            modes = FindAnyObjectByType<ModeDirector>();
            if (route != null) route.RingPassed += OnRing;
            if (modes != null) modes.Completed += OnModeComplete;
            CombatScore.KillScored += OnKill;
        }

        void OnDestroy()
        {
            if (route != null) route.RingPassed -= OnRing;
            if (modes != null) modes.Completed -= OnModeComplete;
            CombatScore.KillScored -= OnKill;
            AudioListener.pause = false;
        }

        void OnRing(Checkpoint ring) => AudioFx.Play(Sfx.RingChime, 0.8f, 1f + 0.06f * ring.Index, 0f);   // rises with each ring
        void OnKill(int kills) => AudioFx.Play(Sfx.ShotDown, 0.9f);
        void OnModeComplete(ModeResult r) => AudioFx.PlayUi(Sfx.Victory, 1f);

        void Update()
        {
            AudioFx.Settings = settings;   // keeps live edits to the asset working
            AudioListener.volume = settings == null ? 0.8f : (settings.mute ? 0f : settings.masterVolume);

            // Paused (menus set timeScale to 0): silence the world. UI sounds ignore the pause.
            AudioListener.pause = Time.timeScale == 0f;
        }
    }
}
