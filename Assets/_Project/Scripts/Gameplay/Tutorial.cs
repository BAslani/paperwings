using System;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// FLIGHT SCHOOL (Stage 33, air lessons Stage 35): a guided first flight in the office, on a fully
    /// upgraded plane, so new players learn every control AND how the office's air works before the
    /// campaign hands them a plain sheet of paper.
    ///   1 Throw   2 Steer (a full turn)
    ///   3 Heat: climb inside warm rising air        4 Drafts: ride the desk fan's stream
    ///   5 Downdrafts: feel the AC vent push you down, then escape it
    ///   6 Steam: rises like heat, but soaks your paper
    ///   7 Boost   8 Staples: practice drones   9 Pencil missiles   10 Dogfight a (gentle) training rival
    /// The air lessons put a marker on the nearest source of that kind (DebugHUD draws it); a room
    /// without one simply skips that lesson. Crashing never fails a step: you're back in hand and carry on.
    /// Lives on the office's FlightSchool group; the ModeDirector switches the group on and calls <see cref="Begin"/>.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        [Tooltip("Practice drones (a looping ConvoySpawner). Switched on for the shooting steps.")]
        [SerializeField] GameObject drones;
        [Tooltip("A training rival (EnemySpawner with a gentle pilot). Switched on for the last step.")]
        [SerializeField] GameObject rival;
        [SerializeField] int dronesToShoot = 3;
        [SerializeField] float turnDegrees = 270f;
        [Tooltip("Height to gain while INSIDE warm air (diving and pulling up doesn't count).")]
        [SerializeField] float climbHeight = 15f;
        [Tooltip("Seconds to ride a fan's stream.")]
        [SerializeField] float draftSeconds = 2f;
        [Tooltip("Seconds under the AC vent before you're told to get out.")]
        [SerializeField] float downdraftSeconds = 0.6f;
        [Tooltip("Seconds in steam.")]
        [SerializeField] float steamSeconds = 1.5f;

        public enum Step { Throw, Steer, Climb, Draft, Downdraft, Steam, Boost, Shoot, Missile, Dogfight, Done }
        public Step Current { get; private set; }
        public int StepNumber => (int)Current + 1;
        public const int StepCount = 10;
        /// <summary>When the current step began (the HUD fades its card in).</summary>
        public float StepStarted { get; private set; }

        /// <summary>Air lessons: where to fly (the nearest source of the kind being taught).</summary>
        public bool HasTarget => target != null && target.isActiveAndEnabled;
        public Vector3 TargetPoint => target != null ? TargetCentre(target) : Vector3.zero;
        public string TargetLabel => Current switch
        {
            Step.Climb => "WARM AIR",
            Step.Draft => "FAN",
            Step.Downdraft => escaping ? "" : "AC VENT",
            Step.Steam => "STEAM",
            _ => ""
        };

        PlaneController player;
        PencilLauncher pencils;
        PaperDamage paper;
        ConvoySpawner convoy;
        AirSource target;
        int downedAtStart, killsAtStart;
        float turned, lastYaw, lastY, gained, airTime, escapeTime, doneTimer;
        bool fired, escaping;

        public string Title => Current switch
        {
            Step.Throw => "THROW",
            Step.Steer => "STEER",
            Step.Climb => "HEAT: CLIMB IN WARM AIR",
            Step.Draft => "DRAFTS: RIDE THE FAN",
            Step.Downdraft => "DOWNDRAFTS: THE AC VENT",
            Step.Steam => "STEAM: LIFT, BUT WET",
            Step.Boost => "BOOST",
            Step.Shoot => "STAPLES",
            Step.Missile => "PENCIL MISSILES",
            Step.Dogfight => "DOGFIGHT",
            _ => "GRADUATION"
        };

        public string Body => Current switch
        {
            Step.Throw => "Aim with the mouse (or WASD). Hold SPACE (gamepad A) to wind up, let go to throw.",
            Step.Steer => "Move the MOUSE (or WASD / left stick). The plane banks, and banking turns it. Fly a full turn. (C swaps mouse aim and classic controls.)",
            Step.Climb => "Paper gliders always sink, but warm air RISES. Orange shimmer marks it: the laptop, the radiators, the space heater, the lamps. " +
                          "Fly into it (follow the marker) and CIRCLE to stay inside while it lifts you. Diving and pulling up doesn't count.",
            Step.Draft => "Pale streaks are a DRAFT: the desk fan blows air sideways and carries you with it. Fly into its stream and let it push you along. " +
                          "Ride with it for speed, fly across it and it drifts you off your line.",
            Step.Downdraft => escaping
                ? "Feel that? Now GET OUT: turn away from the falling air. Under a vent you sink fast, so never fight a rival there (but you can chase one in)."
                : "Blue streaks falling from the ceiling are the AC VENT: air pushing DOWN. Fly under it (follow the marker) and feel it drag you down.",
            Step.Steam => "Steam from hot drinks LIFTS you like heat, but it's WET: soggy paper is heavier and sinks faster. " +
                          "Fly through the tea's steam, then dry out again in warm DRY air (laptop, radiators).",
            Step.Boost => "Press SHIFT (gamepad right trigger) for a burst of speed. It needs a moment to recharge.",
            Step.Shoot => "Hold LEFT MOUSE (or F, gamepad X) to fire staples. The reticle snaps onto whatever is near your nose. Shoot down the yellow practice drones.",
            Step.Missile => "Keep a drone in front of you until the pencil LOCKS, then RIGHT MOUSE (or G, gamepad left trigger). Pencils home in, and refill when you're back in hand.",
            Step.Dogfight => "An Accounting rival (red trail) is in the office. Get on its tail and shoot it down. It climbs in warm air when it's low: wait for it there.",
            _ => "Well flown!"
        };

        public string Progress => Current switch
        {
            Step.Steer => $"turned {Mathf.Min(turned, turnDegrees):0}° / {turnDegrees:0}°",
            Step.Climb => $"climbed {Mathf.Min(gained, climbHeight):0.0} / {climbHeight:0} in warm air" + (InAir(a => a.InThermal) ? "  <color=#FFD040>lifting!</color>" : ""),
            Step.Draft => $"in the fan's stream {Mathf.Min(airTime, draftSeconds):0.0} / {draftSeconds:0.0}s",
            Step.Downdraft => escaping ? $"get clear {Mathf.Min(escapeTime, 1f) * 100f:0}%" : $"under the vent {Mathf.Min(airTime, downdraftSeconds):0.0} / {downdraftSeconds:0.0}s",
            Step.Steam => $"in steam {Mathf.Min(airTime, steamSeconds):0.0} / {steamSeconds:0.0}s" + (paper != null ? $"   paper wet {paper.Wetness01 * 100f:0}%" : ""),
            Step.Shoot => $"drones down {Mathf.Min(DronesDown, dronesToShoot)} / {dronesToShoot}",
            Step.Missile => fired ? "pencil away!" : (pencils != null && pencils.IsLocked ? "<color=#FFD040>LOCKED: fire!</color>" : "lock on to a drone"),
            Step.Dogfight => "rival shot down " + (Kills > 0 ? "1" : "0") + " / 1",
            _ => ""
        };

        /// <summary>One line for the objective strip at the bottom.</summary>
        public string StripText => $"Step <b>{Mathf.Min(StepNumber, StepCount)} / {StepCount}</b>  {Title}" + (Progress.Length > 0 ? $"   <b>{Progress}</b>" : "");

        int DronesDown => convoy != null ? convoy.Downed - downedAtStart : 0;
        int Kills => CombatScore.Kills - killsAtStart;
        bool Flying => player != null && player.State == PlaneState.Flying;
        bool InAir(Func<AirSample, bool> test) => Flying && test(player.Air);

        public void Begin(PlaneController playerPlane)
        {
            player = playerPlane;
            pencils = player != null ? player.GetComponent<PencilLauncher>() : null;
            paper = player != null ? player.GetComponent<PaperDamage>() : null;
            if (drones != null) drones.SetActive(false);
            if (rival != null) rival.SetActive(false);
            Go(Step.Throw);
        }

        void OnEnable()
        {
            if (pencils != null) pencils.Fired += OnPencil;
        }

        void OnDisable()
        {
            if (pencils != null) pencils.Fired -= OnPencil;
            if (player != null) player.Boosted -= OnBoost;
        }

        void OnPencil(PencilMissile m) { if (Current == Step.Missile) fired = true; }
        void OnBoost() { if (Current == Step.Boost) Go(Step.Shoot); }

        // ------------------------------------------------------------------ the air lessons' targets

        /// <summary>The air source a lesson teaches (null = the room has none: the lesson is skipped).</summary>
        static Func<AirSource, bool> SourceTest(Step step) => step switch
        {
            Step.Climb => s => s.Kind == AirKind.Thermal && s.IsHeat,
            Step.Draft => s => s.Kind == AirKind.Draft,
            Step.Downdraft => s => s.Kind == AirKind.Downdraft,
            Step.Steam => s => s.Moisture >= 0.5f,
            _ => null
        };

        AirSource Nearest(Func<AirSource, bool> test)
        {
            if (test == null || player == null) return null;
            AirSource best = null;
            float bestSq = float.MaxValue;
            foreach (var s in AirField.Sources)
            {
                if (s == null || !s.isActiveAndEnabled || !test(s)) continue;
                float d = (TargetCentre(s) - player.transform.position).sqrMagnitude;
                if (d < bestSq && d < 400f * 400f) { bestSq = d; best = s; }   // only this room's air
            }
            return best;
        }

        /// <summary>A good point to fly to: the lower third of a rising column, the middle of anything else.</summary>
        static Vector3 TargetCentre(AirSource s)
        {
            Bounds b = s.GetBounds();
            return s.Kind == AirKind.Thermal ? new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z) : b.center;
        }

        // ------------------------------------------------------------------ steps

        void Go(Step step)
        {
            // Air lessons need that kind of air in the room: skip the ones it lacks.
            var test = SourceTest(step);
            target = Nearest(test);
            if (test != null && target == null && step != Step.Done) { Go(step + 1); return; }

            Current = step;
            StepStarted = Time.time;
            if (step != Step.Throw) Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);

            if (player != null)
            {
                player.Boosted -= OnBoost;
                if (step == Step.Boost) player.Boosted += OnBoost;
                lastYaw = player.transform.eulerAngles.y;
                lastY = player.transform.position.y;
            }
            if (pencils != null) { pencils.Fired -= OnPencil; pencils.Fired += OnPencil; }
            turned = gained = airTime = escapeTime = 0f;
            fired = escaping = false;
            doneTimer = 0f;

            // Drones for the two shooting steps, the rival for the last one.
            bool wantDrones = step == Step.Shoot || step == Step.Missile;
            if (drones != null && drones.activeSelf != wantDrones) drones.SetActive(wantDrones);
            convoy = drones != null ? drones.GetComponentInChildren<ConvoySpawner>(true) : null;
            downedAtStart = convoy != null ? convoy.Downed : 0;
            if (rival != null) rival.SetActive(step == Step.Dogfight);
            killsAtStart = CombatScore.Kills;

            if (step == Step.Done && ModeDirector.Current != null) ModeDirector.Current.CompleteTraining();
        }

        void Update()
        {
            if (player == null || Current == Step.Done) return;
            bool flying = Flying;
            float dt = Time.deltaTime;

            // Keep pointing at the nearest source as you fly around (there may be several radiators).
            if (SourceTest(Current) != null && Time.frameCount % 20 == 0)
            {
                var nearer = Nearest(SourceTest(Current));
                if (nearer != null) target = nearer;
            }

            switch (Current)
            {
                case Step.Throw:
                    if (flying && player.FlightTime > 0.8f) Go(Step.Steer);
                    break;

                case Step.Steer:
                    if (!flying) { lastYaw = player.transform.eulerAngles.y; break; }
                    float yaw = player.transform.eulerAngles.y;
                    turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
                    lastYaw = yaw;
                    if (turned >= turnDegrees) Go(Step.Climb);
                    break;

                case Step.Climb:
                    {
                        // Only height gained INSIDE warm air counts.
                        float y = player.transform.position.y;
                        if (flying && player.Air.InThermal) gained += Mathf.Max(0f, y - lastY);
                        lastY = y;
                        if (gained >= climbHeight) Go(Step.Draft);
                    }
                    break;

                case Step.Draft:
                    if (InAir(a => a.InDraft)) airTime += dt;
                    if (airTime >= draftSeconds) Go(Step.Downdraft);
                    break;

                case Step.Downdraft:
                    if (!escaping)
                    {
                        if (InAir(a => a.InDowndraft)) airTime += dt;
                        if (airTime >= downdraftSeconds) { escaping = true; StepStarted = Time.time; Audio.AudioFx.PlayUi(Audio.Sfx.UiMove); }
                    }
                    else
                    {
                        // Out of the falling air for a second (and still flying) = escaped.
                        if (flying && !player.Air.InDowndraft) escapeTime += dt; else escapeTime = 0f;
                        if (escapeTime >= 1f) Go(Step.Steam);
                    }
                    break;

                case Step.Steam:
                    if (InAir(a => a.moisture > 0.25f)) airTime += dt;
                    if (airTime >= steamSeconds) Go(Step.Boost);
                    break;

                case Step.Shoot:
                    if (DronesDown >= dronesToShoot) Go(Step.Missile);
                    break;

                case Step.Missile:
                    // A couple of seconds to watch the pencil fly.
                    if (fired && (doneTimer += dt) > 2.5f) Go(Step.Dogfight);
                    break;

                case Step.Dogfight:
                    if (Kills >= 1 && (doneTimer += dt) > 1.5f) Go(Step.Done);
                    break;
            }
        }
    }
}
