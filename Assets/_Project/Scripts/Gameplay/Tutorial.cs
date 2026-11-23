using UnityEngine;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// FLIGHT SCHOOL (Stage 33): a guided first flight in the office, on a fully upgraded plane, so
    /// new players learn every control before the campaign hands them a plain sheet of paper.
    ///   1 Throw   2 Steer (a full turn)   3 Climb in warm air   4 Boost
    ///   5 Staples: shoot down practice drones   6 Pencil missiles: lock on and fire
    ///   7 Dogfight: shoot down a (gentle) training rival
    /// Lives on the office's FlightSchool group; the ModeDirector switches the group on and calls
    /// <see cref="Begin"/>. Crashing never fails a step: you're back in hand and carry on.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        [Tooltip("Practice drones (a looping ConvoySpawner). Switched on for the shooting steps.")]
        [SerializeField] GameObject drones;
        [Tooltip("A training rival (EnemySpawner with a gentle pilot). Switched on for the last step.")]
        [SerializeField] GameObject rival;
        [SerializeField] int dronesToShoot = 3;
        [SerializeField] float turnDegrees = 270f;
        [SerializeField] float climbHeight = 15f;

        public enum Step { Throw, Steer, Climb, Boost, Shoot, Missile, Dogfight, Done }
        public Step Current { get; private set; }
        public int StepNumber => (int)Current + 1;
        public const int StepCount = 7;
        /// <summary>When the current step began (the HUD fades its card in).</summary>
        public float StepStarted { get; private set; }

        PlaneController player;
        PencilLauncher pencils;
        ConvoySpawner convoy;
        int downedAtStart, killsAtStart;
        float turned, lastYaw, lowest, gained, doneTimer;
        bool fired;

        public string Title => Current switch
        {
            Step.Throw => "THROW",
            Step.Steer => "STEER",
            Step.Climb => "CLIMB IN WARM AIR",
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
            Step.Climb => "Paper gliders sink. Orange shimmering air RISES: the radiators under the windows, the space heater by the couch, the lamps. Fly into it and circle to climb.",
            Step.Boost => "Press SHIFT (gamepad right trigger) for a burst of speed. It needs a moment to recharge.",
            Step.Shoot => "Hold LEFT MOUSE (or F, gamepad X) to fire staples. The reticle snaps onto whatever is near your nose. Shoot down the yellow practice drones.",
            Step.Missile => "Keep a drone in front of you until the pencil LOCKS, then RIGHT MOUSE (or G, gamepad left trigger). Pencils home in, and refill when you're back in hand.",
            Step.Dogfight => "An Accounting rival (red trail) is in the office. Get on its tail and shoot it down. It climbs in warm air when it's low: wait for it there.",
            _ => "Well flown!"
        };

        public string Progress => Current switch
        {
            Step.Steer => $"turned {Mathf.Min(turned, turnDegrees):0}° / {turnDegrees:0}°",
            Step.Climb => $"climbed {Mathf.Min(gained, climbHeight):0.0} / {climbHeight:0}",
            Step.Shoot => $"drones down {Mathf.Min(DronesDown, dronesToShoot)} / {dronesToShoot}",
            Step.Missile => fired ? "pencil away!" : (pencils != null && pencils.IsLocked ? "<color=#FFD040>LOCKED: fire!</color>" : "lock on to a drone"),
            Step.Dogfight => "rival shot down " + (Kills > 0 ? "1" : "0") + " / 1",
            _ => ""
        };

        /// <summary>One line for the objective strip at the bottom.</summary>
        public string StripText => $"Step <b>{Mathf.Min(StepNumber, StepCount)} / {StepCount}</b>  {Title}" + (Progress.Length > 0 ? $"   <b>{Progress}</b>" : "");

        int DronesDown => convoy != null ? convoy.Downed - downedAtStart : 0;
        int Kills => CombatScore.Kills - killsAtStart;

        public void Begin(PlaneController playerPlane)
        {
            player = playerPlane;
            pencils = player != null ? player.GetComponent<PencilLauncher>() : null;
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

        void Go(Step step)
        {
            Current = step;
            StepStarted = Time.time;
            if (step != Step.Throw) Audio.AudioFx.PlayUi(Audio.Sfx.UiConfirm);

            if (player != null)
            {
                player.Boosted -= OnBoost;
                if (step == Step.Boost) player.Boosted += OnBoost;
                lastYaw = player.transform.eulerAngles.y;
                lowest = player.transform.position.y;
            }
            if (pencils != null) { pencils.Fired -= OnPencil; pencils.Fired += OnPencil; }
            turned = gained = 0f;
            fired = false;
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
            bool flying = player.State == PlaneState.Flying;

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
                    if (!flying) { lowest = float.MaxValue; break; }
                    float y = player.transform.position.y;
                    lowest = Mathf.Min(lowest, y);
                    gained = Mathf.Max(gained, y - lowest);
                    if (gained >= climbHeight) Go(Step.Boost);
                    break;

                case Step.Shoot:
                    if (DronesDown >= dronesToShoot) Go(Step.Missile);
                    break;

                case Step.Missile:
                    // A couple of seconds to watch the pencil fly.
                    if (fired && (doneTimer += Time.deltaTime) > 2.5f) Go(Step.Dogfight);
                    break;

                case Step.Dogfight:
                    if (Kills >= 1 && (doneTimer += Time.deltaTime) > 1.5f) Go(Step.Done);
                    break;
            }
        }
    }
}
