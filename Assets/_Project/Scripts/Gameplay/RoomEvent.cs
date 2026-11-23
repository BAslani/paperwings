using UnityEngine;
using PaperWings.Air;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// A set piece that changes the room in the middle of a fight (Stage 29): the microwave dings
    /// and dumps heat, the fridge door swings open and spills cold air, the server room's halon
    /// purge slams everyone toward the floor, the lights go out...
    ///
    /// It runs on a loop while a mode is being played in its room: a WARNING (the HUD names it and
    /// counts down), then ACTIVE (air on, parts moved, lights changed), then a rest. Everything it
    /// touches is listed in the inspector, so one component covers every event.
    /// </summary>
    public class RoomEvent : MonoBehaviour
    {
        [Header("What the HUD says")]
        public string title = "MICROWAVE: DING!";
        [TextArea(1, 2)] public string hint = "Heat is pouring out of the microwave: climb in it!";
        public Color color = new Color(1f, 0.6f, 0.2f);

        [Header("Rhythm (seconds)")]
        public float firstDelay = 10f;
        public float warnTime = 2.5f;
        public float activeTime = 6f;
        public float restTime = 20f;
        [Tooltip("Random extra rest, so two events in a room drift apart.")]
        public float restJitter = 6f;
        [Tooltip("Seconds for moving parts to open / close.")]
        public float openTime = 0.5f;
        public float closeTime = 1f;

        [Header("Air switched on while active (ColumnAir / BeamAir strength)")]
        public AirSource[] air;
        public float airStrength = 1f;
        [Tooltip("Air switched OFF while active (e.g. the cooling stops during a blackout).")]
        public AirSource[] airOff;

        [Header("Moving parts (rotate / slide to their open pose while active)")]
        public Transform[] movers;
        public Vector3[] openEuler;
        public Vector3[] openOffset;

        [Header("Lights")]
        public Light[] lights;
        [Tooltip("Intensity multiplier while active (0 = blackout).")]
        public float lightScale = 1f;
        [Tooltip("Colour while active (alpha 0 = keep their own colour).")]
        public Color lightColor = new Color(1f, 0.2f, 0.1f, 0f);
        public bool strobe;
        [Tooltip("Lights switched ON only while active (emergency lights, a light inside the fridge).")]
        public Light[] eventLights;

        [Header("Effects")]
        [Tooltip("Played once when it goes active.")]
        public ParticleSystem[] bursts;
        [Tooltip("Emitting only while active.")]
        public ParticleSystem[] loops;
        [Tooltip("Objects (hazard launchers, extra props) switched on only while active.")]
        public GameObject[] enableWhileActive;
        public Audio.Sfx warnSound = Audio.Sfx.Alarm;
        public Audio.Sfx activeSound = Audio.Sfx.BossSlam;

        public enum Stage { Idle, Warning, Active }
        public Stage Current { get; private set; }
        /// <summary>Seconds until the warning turns into the event.</summary>
        public float SecondsToGo => Current == Stage.Warning ? Mathf.Max(0f, stageEnd - Time.time) : 0f;

        /// <summary>The event the HUD should show right now (the latest one warning or active), or null.</summary>
        public static RoomEvent Announcing { get; private set; }
        static readonly System.Collections.Generic.List<RoomEvent> all = new System.Collections.Generic.List<RoomEvent>();

        Room room;
        bool live;
        float stageEnd, open01;
        Quaternion[] restRot;
        Vector3[] restPos;
        float[] baseIntensity, eventIntensity;
        Color[] baseColor;

        void Awake()
        {
            room = GetComponentInParent<Room>();
            if (movers != null)
            {
                restRot = new Quaternion[movers.Length];
                restPos = new Vector3[movers.Length];
                for (int i = 0; i < movers.Length; i++)
                    if (movers[i] != null) { restRot[i] = movers[i].localRotation; restPos[i] = movers[i].localPosition; }
            }
            if (lights != null)
            {
                baseIntensity = new float[lights.Length];
                baseColor = new Color[lights.Length];
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null) { baseIntensity[i] = lights[i].intensity; baseColor[i] = lights[i].color; }
            }
            if (eventLights != null)
            {
                eventIntensity = new float[eventLights.Length];
                for (int i = 0; i < eventLights.Length; i++)
                    if (eventLights[i] != null) { eventIntensity[i] = eventLights[i].intensity; eventLights[i].intensity = 0f; }
            }
            SetActiveStuff(false);
        }

        void OnEnable() => all.Add(this);

        void OnDisable()
        {
            all.Remove(this);
            if (Announcing == this) Announcing = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Announcing = null; all.Clear(); }

        /// <summary>Another event in this room is warning or running (one at a time: they'd fight over lights and air).</summary>
        bool RoomBusy()
        {
            foreach (var e in all) if (e != this && e.room == room && e.Current != Stage.Idle) return true;
            return false;
        }

        bool RoomIsLive()
        {
            var d = ModeDirector.Current;
            if (d == null || !d.HasMode || d.RunComplete) return false;
            return room == null || d.CurrentRoom == room;
        }

        void Update()
        {
            bool nowLive = RoomIsLive();
            if (nowLive != live)
            {
                live = nowLive;
                Enter(Stage.Idle, firstDelay);
                if (!live) { SetActiveStuff(false); if (Announcing == this) Announcing = null; }
            }

            if (live && Time.time >= stageEnd)
            {
                switch (Current)
                {
                    case Stage.Idle:
                        if (RoomBusy()) { stageEnd = Time.time + 4f; break; }
                        Enter(Stage.Warning, warnTime);
                        Announcing = this;
                        Audio.AudioFx.PlayAt(warnSound, transform.position, 0.8f);
                        break;
                    case Stage.Warning:
                        Enter(Stage.Active, activeTime);
                        Announcing = this;
                        SetActiveStuff(true);
                        if (bursts != null) foreach (var b in bursts) if (b != null) b.Play();
                        Audio.AudioFx.PlayAt(activeSound, transform.position, 0.9f);
                        break;
                    case Stage.Active:
                        Enter(Stage.Idle, restTime + Random.value * restJitter);
                        SetActiveStuff(false);
                        if (Announcing == this) Announcing = null;
                        break;
                }
            }

            // Moving parts ease open while active, closed otherwise.
            bool opening = Current == Stage.Active && live;
            open01 = Mathf.MoveTowards(open01, opening ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, opening ? openTime : closeTime));
            float e = Mathf.SmoothStep(0f, 1f, open01);
            if (movers != null)
                for (int i = 0; i < movers.Length; i++)
                {
                    if (movers[i] == null) continue;
                    Vector3 euler = openEuler != null && i < openEuler.Length ? openEuler[i] : Vector3.zero;
                    Vector3 offset = openOffset != null && i < openOffset.Length ? openOffset[i] : Vector3.zero;
                    movers[i].localRotation = restRot[i] * Quaternion.Euler(euler * e);
                    movers[i].localPosition = restPos[i] + offset * e;
                }

            UpdateLights(opening);
        }

        void Enter(Stage s, float seconds)
        {
            Current = s;
            stageEnd = Time.time + seconds;
        }

        void SetActiveStuff(bool on)
        {
            if (air != null) foreach (var a in air) SetStrength(a, on ? airStrength : 0f);
            if (airOff != null) foreach (var a in airOff) SetStrength(a, on ? 0f : 1f);
            if (loops != null)
                foreach (var p in loops)
                {
                    if (p == null) continue;
                    var em = p.emission;
                    em.enabled = on;
                    if (on && !p.isPlaying) p.Play();
                }
            if (enableWhileActive != null) foreach (var g in enableWhileActive) if (g != null) g.SetActive(on);
        }

        static void SetStrength(AirSource a, float s)
        {
            if (a is ColumnAir c) c.targetStrength = s;
            else if (a is BeamAir b) b.targetStrength = s;
            else if (a != null) a.enabled = s > 0f;
        }

        void UpdateLights(bool active)
        {
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            if (lights != null)
                for (int i = 0; i < lights.Length; i++)
                {
                    var l = lights[i];
                    if (l == null) continue;
                    float target = baseIntensity[i];
                    Color col = baseColor[i];
                    if (active)
                    {
                        target *= lightScale;
                        if (strobe) target *= Mathf.Repeat(Time.time * 3f, 1f) < 0.5f ? 1f : 0.15f;
                        if (lightColor.a > 0f) col = lightColor;
                    }
                    l.intensity = strobe && active ? target : Mathf.Lerp(l.intensity, target, k);
                    l.color = Color.Lerp(l.color, col, k);
                }
            if (eventLights != null)
                for (int i = 0; i < eventLights.Length; i++)
                {
                    var l = eventLights[i];
                    if (l == null) continue;
                    float target = active ? eventIntensity[i] * (strobe ? (Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? 1f : 0.2f) : 1f) : 0f;
                    l.intensity = Mathf.Lerp(l.intensity, target, k * 2f);
                }
        }
    }
}
