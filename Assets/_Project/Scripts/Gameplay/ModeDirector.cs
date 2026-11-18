using System;
using UnityEngine;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    public enum GameMode
    {
        FreeFlight,   // just the desk and the air
        RingRun,      // fly the ring route, land on the mug
        Dogfight,     // shoot down N rivals
        BossFight     // defeat The Stapler
    }

    /// <summary>What to show on the results screen.</summary>
    public class ModeResult
    {
        public GameMode mode;
        public string title;
        public string detail;
        public bool newBest;
    }

    /// <summary>
    /// Runs the desk as separate game modes. Each mode switches the right parts of the scene on
    /// (ring route, rival spawners, boss) and the rest off, tracks its objective and a run timer,
    /// and saves a best time per mode.
    ///
    /// The run timer starts on your first throw and keeps going across crashes and resets,
    /// so in Dogfight and Boss Fight a crash costs you time, but never ends the run.
    /// </summary>
    public class ModeDirector : MonoBehaviour
    {
        [SerializeField] LaunchAndReset launcher;
        [SerializeField] PlaneController player;

        [Header("Scene groups (switched on/off per mode)")]
        [SerializeField] GameObject routeGroup;
        [SerializeField] GameObject combatGroup;
        [SerializeField] GameObject bossGroup;

        [Header("Objectives")]
        [Tooltip("Rivals to shoot down to win a Dogfight.")]
        [SerializeField] int dogfightKillTarget = 5;

        public GameMode Mode { get; private set; }
        public bool HasMode { get; private set; }
        public bool RunStarted { get; private set; }
        public bool RunComplete { get; private set; }
        public float RunTime { get; private set; }
        public int DogfightTarget => dogfightKillTarget;
        public int KillsThisRun => CombatScore.Kills - killBaseline;
        public RaceRoute Route { get; private set; }
        public StaplerBoss Boss { get; private set; }
        public ModeResult LastResult { get; private set; }

        public event Action<ModeResult> Completed;

        int killBaseline;
        bool ringLandingPending;

        public static string ModeName(GameMode m) => m switch
        {
            GameMode.FreeFlight => "Free Flight",
            GameMode.RingRun => "Ring Run",
            GameMode.Dogfight => "Dogfight",
            _ => "Boss Fight"
        };

        static string BestKey(GameMode m) => $"PaperWings.Best.{m}";
        public float BestTime(GameMode m) => PlayerPrefs.GetFloat(BestKey(m), 0f);

        void Awake()
        {
            if (routeGroup != null) Route = routeGroup.GetComponentInChildren<RaceRoute>(true);
            if (bossGroup != null) Boss = bossGroup.GetComponentInChildren<StaplerBoss>(true);
            // Until a mode is chosen, nothing hostile is running.
            SetGroups(route: false, combat: false, boss: false);
        }

        void OnEnable()
        {
            LandingZone.AnyLanded += OnLanded;
            if (Boss != null) Boss.Defeated += OnBossDefeated;
        }

        void OnDisable()
        {
            LandingZone.AnyLanded -= OnLanded;
            if (Boss != null) Boss.Defeated -= OnBossDefeated;
        }

        /// <summary>Starts (or restarts) a mode from scratch: right groups on, plane back in hand, timer reset.</summary>
        public void StartMode(GameMode mode)
        {
            Mode = mode;
            HasMode = true;
            RunStarted = false;
            RunComplete = false;
            RunTime = 0f;
            LastResult = null;
            ringLandingPending = false;

            // Toggling a group off and on again gives a clean slate (spawners respawn their rivals).
            SetGroups(route: false, combat: false, boss: false);
            SetGroups(route: mode == GameMode.RingRun, combat: mode == GameMode.Dogfight, boss: mode == GameMode.BossFight);

            if (mode == GameMode.BossFight && Boss != null) Boss.ResetFight();
            if (mode == GameMode.RingRun && Route != null) Route.ResetRoute();

            killBaseline = CombatScore.Kills;
            if (launcher != null) launcher.ResetToStart();
            PlayerPrefs.SetInt("PaperWings.LastMode", (int)mode);
        }

        void SetGroups(bool route, bool combat, bool boss)
        {
            if (routeGroup != null) routeGroup.SetActive(route);
            if (combatGroup != null) combatGroup.SetActive(combat);
            if (bossGroup != null) bossGroup.SetActive(boss);
        }

        void Update()
        {
            if (!HasMode || RunComplete || player == null) return;

            if (!RunStarted && player.State == PlaneState.Flying) RunStarted = true;
            if (RunStarted) RunTime += Time.deltaTime;

            if (Mode == GameMode.Dogfight && KillsThisRun >= dogfightKillTarget)
                CompleteTimed($"DOGFIGHT WON!", $"{dogfightKillTarget} rivals shot down in");

            // Ring Run: checked a frame after landing so the route has finished its own bookkeeping.
            if (Mode == GameMode.RingRun && ringLandingPending)
            {
                ringLandingPending = false;
                if (Route != null && Route.AllRingsPassed)
                {
                    float t = player.FlightTime;
                    Finish(new ModeResult
                    {
                        mode = Mode,
                        title = "RING RUN COMPLETE!",
                        detail = $"All rings and a mug landing in {t:0.00}s" + (Route.BestTime > 0f ? $"   (best {Route.BestTime:0.00}s)" : ""),
                        newBest = Route.LastWasNewBest
                    });
                }
            }
        }

        void OnLanded(PlaneController plane, string message)
        {
            if (HasMode && Mode == GameMode.RingRun && plane == player) ringLandingPending = true;
        }

        void OnBossDefeated()
        {
            if (HasMode && Mode == GameMode.BossFight && !RunComplete)
                CompleteTimed("THE STAPLER IS DEFEATED!", "Boss down in");
        }

        void CompleteTimed(string title, string lead)
        {
            float best = BestTime(Mode);
            bool newBest = best <= 0f || RunTime < best;
            if (newBest) { PlayerPrefs.SetFloat(BestKey(Mode), RunTime); PlayerPrefs.Save(); }
            Finish(new ModeResult
            {
                mode = Mode,
                title = title,
                detail = $"{lead} {RunTime:0.0}s" + (newBest ? "" : $"   (best {best:0.0}s)"),
                newBest = newBest
            });
        }

        void Finish(ModeResult result)
        {
            RunComplete = true;
            LastResult = result;
            Completed?.Invoke(result);
        }
    }
}
