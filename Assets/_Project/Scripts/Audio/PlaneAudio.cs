using UnityEngine;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.Audio
{
    /// <summary>
    /// The player plane's sound:
    ///   Loops (always playing, volume follows the flight):
    ///     wind     - louder, higher and BRIGHTER with speed (a low-pass filter opens up as you go faster)
    ///     flutter  - paper flapping; swells when stalling or in rough air
    ///     thermal  - a low hum while rising air carries you
    ///     draft    - a rushing band of noise in fan drafts and downdrafts
    ///     lock     - steady tone while a pencil lock is held
    ///   One-shots on events: throw, boost, bumps, rips, crash, landing, pencil launch, lock-on beeps.
    /// </summary>
    [RequireComponent(typeof(PlaneController))]
    public class PlaneAudio : MonoBehaviour
    {
        PlaneController plane;
        PaperDamage paper;
        PencilLauncher pencils;

        AudioSource wind, flutter, thermal, draft, lockTone;
        AudioLowPassFilter windFilter;
        PlaneState lastState;
        float nextBeepTime;

        void Awake()
        {
            plane = GetComponent<PlaneController>();
            paper = GetComponent<PaperDamage>();
            pencils = GetComponent<PencilLauncher>();

            wind = MakeLoop("WindLoop", SoundBank.WindLoop);
            windFilter = wind.gameObject.AddComponent<AudioLowPassFilter>();   // brightness follows speed
            flutter = MakeLoop("FlutterLoop", SoundBank.FlutterLoop);
            thermal = MakeLoop("ThermalLoop", SoundBank.RumbleLoop);
            draft = MakeLoop("DraftLoop", SoundBank.DraftLoop);
            lockTone = MakeLoop("LockTone", SoundBank.LockToneLoop);
            lastState = plane.State;
        }

        AudioSource MakeLoop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;   // the player's own plane: always "in your ears"
            s.volume = 0f;
            s.playOnAwake = false;
            s.time = Random.value * clip.length * 0.9f;   // don't start every loop at the same spot
            s.Play();
            return s;
        }

        void OnEnable()
        {
            plane.Boosted += OnBoost;
            plane.Bumped += OnBump;
            if (paper != null) paper.Damaged += OnDamaged;
            if (pencils != null) { pencils.Fired += OnPencilFired; pencils.FireRefused += OnNoLock; }
        }

        void OnDisable()
        {
            plane.Boosted -= OnBoost;
            plane.Bumped -= OnBump;
            if (paper != null) paper.Damaged -= OnDamaged;
            if (pencils != null) { pencils.Fired -= OnPencilFired; pencils.FireRefused -= OnNoLock; }
        }

        void OnBoost() => AudioFx.Play(Sfx.Throw, 0.9f, 1.1f);   // same swish as the hand throw, a touch higher
        void OnBump(float impact) => AudioFx.Play(Sfx.Bump, Mathf.Lerp(0.4f, 1f, impact / 6f));
        void OnDamaged(float amount) => AudioFx.Play(Sfx.PaperRip, Mathf.Lerp(0.4f, 1f, amount / 30f));
        void OnPencilFired(PencilMissile p) => AudioFx.Play(Sfx.Throw, 0.45f, 1.7f);   // the pencil un-clipping
        void OnNoLock() => AudioFx.Play(Sfx.NoLock, 0.6f, 1f, 0f);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool flying = plane.State == PlaneState.Flying;
            float amb = AudioFx.AmbienceVolume;

            // ---- Wind: speed drives volume, pitch and brightness ----
            float speed01 = flying ? Mathf.Clamp01(plane.Speed / plane.Settings.maxSpeed) : 0f;
            Fade(wind, Mathf.Lerp(0.04f, 0.9f, Mathf.Pow(speed01, 1.3f)) * amb, dt, 4f);
            wind.pitch = Mathf.Lerp(0.8f, 1.3f, speed01);
            windFilter.cutoffFrequency = Mathf.Lerp(500f, 9000f, speed01);

            // ---- Flutter: stalls and rough air ----
            float turbulence = flying ? plane.AirVelocity.magnitude * 0.03f : 0f;
            float flutterVol = flying ? Mathf.Clamp01(plane.StallAmount * 0.8f + turbulence + speed01 * 0.1f) : 0f;
            Fade(flutter, flutterVol * 0.6f * amb, dt, 6f);
            flutter.pitch = 1f + plane.StallAmount * 0.3f;

            // ---- Air zones ----
            var air = plane.Air;
            Fade(thermal, (flying ? air.thermal : 0f) * 0.7f * amb, dt, 3f);
            Fade(draft, (flying ? Mathf.Max(air.draft, air.downdraft) : 0f) * 0.6f * amb, dt, 3f);
            draft.pitch = air.downdraft > air.draft ? 0.8f : 1.1f;   // downdrafts sound deeper

            UpdateLockAudio(dt);
            UpdateStateSounds();
        }

        void UpdateLockAudio(float dt)
        {
            bool locked = pencils != null && plane.State == PlaneState.Flying && pencils.IsLocked;
            Fade(lockTone, locked ? 0.35f * AudioFx.EffectsVolume : 0f, dt, 20f);

            // While locking: beeps that speed up as the lock builds.
            if (pencils != null && !locked && pencils.Candidate != null && Time.time >= nextBeepTime)
            {
                AudioFx.Play(Sfx.LockBeep, 0.4f, 1f, 0f);
                nextBeepTime = Time.time + Mathf.Lerp(0.3f, 0.08f, pencils.LockProgress);
            }
        }

        void UpdateStateSounds()
        {
            PlaneState s = plane.State;
            if (s == lastState) return;

            if (lastState == PlaneState.Held && s == PlaneState.Flying) AudioFx.Play(Sfx.Throw, 0.9f);
            else if (s == PlaneState.Down) AudioFx.Play(plane.LastDownWasCrash ? Sfx.Crumple : Sfx.Bump, plane.LastDownWasCrash ? 1f : 0.6f);
            else if (s == PlaneState.Landed) AudioFx.Play(Sfx.LandJingle, 0.8f, 1f, 0f);

            lastState = s;
        }

        /// <summary>Eases a loop's volume toward a target (no clicks or sudden jumps).</summary>
        static void Fade(AudioSource s, float target, float dt, float speed)
            => s.volume = Mathf.Lerp(s.volume, target, 1f - Mathf.Exp(-speed * dt));
    }
}
