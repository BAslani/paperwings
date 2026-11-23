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
        BossFight,    // defeat The Stapler
        Squadron      // you + wingmen vs a bigger rival squadron (added last so saved mode numbers stay valid)
    }

    /// <summary>What to show on the results screen.</summary>
    public class ModeResult
    {
        public GameMode mode;
        public string title;
        public string detail;
        public bool newBest;
        /// <summary>Run time in seconds (what medals are judged on).</summary>
        public float time;
        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int medal;
        public bool newMedal;
        public int clipsEarned;
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
        [Tooltip("Extra rivals plus your wingmen; switched on only in Squadron Battle.")]
        [SerializeField] GameObject squadronGroup;

        [Header("Objectives")]
        [Tooltip("Rivals to shoot down to win a Dogfight.")]
        [SerializeField] int dogfightKillTarget = 5;
        [Tooltip("Rivals your squadron (you + wingmen) must shoot down to win a Squadron Battle.")]
        [SerializeField] int squadronKillTarget = 8;

        [Header("Progression")]
        [Tooltip("Rewards and medal times. Leave empty to play without progression.")]
        [SerializeField] ProgressionSettings progression;
        public ProgressionSettings ProgressionConfig => progression;

        public GameMode Mode { get; private set; }
        public bool HasMode { get; private set; }
        public bool RunStarted { get; private set; }
        public bool RunComplete { get; private set; }
        public float RunTime { get; private set; }
        public int DogfightTarget => dogfightKillTarget;
        public int KillsThisRun => CombatScore.Kills - killBaseline;
        public int TeamKillsThisRun => CombatScore.TeamKills - teamKillBaseline;
        public int SquadronTarget => squadronKillTarget;
        public RaceRoute Route { get; private set; }
        public StaplerBoss Boss { get; private set; }
        public ModeResult LastResult { get; private set; }

        public event Action<ModeResult> Completed;

        int killBaseline;
        int teamKillBaseline;
        bool ringLandingPending;

        public static string ModeName(GameMode m) => m switch
        {
            GameMode.FreeFlight => "Free Flight",
            GameMode.RingRun => "Ring Run",
            GameMode.Dogfight => "Dogfight",
            GameMode.Squadron => "Squadron Battle",
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
            CombatScore.KillScored += OnPlayerKill;
            if (Boss != null) Boss.Defeated += OnBossDefeated;
        }

        void OnDisable()
        {
            LandingZone.AnyLanded -= OnLanded;
            CombatScore.KillScored -= OnPlayerKill;
            if (Boss != null) Boss.Defeated -= OnBossDefeated;
        }

        // Small rewards for things you do during any mode.
        void OnPlayerKill(int kills)
        {
            if (progression != null && HasMode) Gameplay.Progression.AddClips(progression.clipsPerKill, "shot down");
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
            // Squadron = the solo-dogfight rivals PLUS the squadron group (extra rivals and your wingmen).
            SetGroups(route: mode == GameMode.RingRun,
                      combat: mode == GameMode.Dogfight || mode == GameMode.Squadron,
                      boss: mode == GameMode.BossFight,
                      squadron: mode == GameMode.Squadron);

            if (mode == GameMode.BossFight && Boss != null) Boss.ResetFight();
            if (mode == GameMode.RingRun && Route != null) Route.ResetRoute();

            killBaseline = CombatScore.Kills;
            teamKillBaseline = CombatScore.TeamKills;
            if (launcher != null) launcher.ResetToStart();
            PlayerPrefs.SetInt("PaperWings.LastMode", (int)mode);
        }

        void SetGroups(bool route, bool combat, bool boss, bool squadron = false)
        {
            if (routeGroup != null) routeGroup.SetActive(route);
            if (combatGroup != null) combatGroup.SetActive(combat);
            if (bossGroup != null) bossGroup.SetActive(boss);
            if (squadronGroup != null) squadronGroup.SetActive(squadron);
        }

        void Update()
        {
            if (!HasMode || RunComplete || player == null) return;

            if (!RunStarted && player.State == PlaneState.Flying) RunStarted = true;
            if (RunStarted) RunTime += Time.deltaTime;

            if (Mode == GameMode.Squadron && TeamKillsThisRun >= squadronKillTarget)
                CompleteTimed("SQUADRON VICTORY!", $"Your squadron downed {squadronKillTarget} rivals in");

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
                        newBest = Route.LastWasNewBest,
                        time = t
                    });
                }
            }
        }

        void OnLanded(PlaneController plane, string message)
        {
            if (plane != player || !HasMode) return;
            if (progression != null) Gameplay.Progression.AddClips(progression.clipsPerMugLanding, "mug landing");
            if (Mode == GameMode.RingRun) ringLandingPending = true;
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
                newBest = newBest,
                time = RunTime
            });
        }

        void Finish(ModeResult result)
        {
            RunComplete = true;
            AwardRun(result);
            LastResult = result;
            Completed?.Invoke(result);
        }

        /// <summary>
        /// Paper clips for finishing, plus a bonus only when you beat your best medal in this mode
        /// (so the big payouts can't be farmed by repeating an easy run).
        /// </summary>
        void AwardRun(ModeResult result)
        {
            if (progression == null) return;
            result.medal = progression.MedalFor(result.mode, result.time);
            int previous = Gameplay.Progression.GetMedal(result.mode);
            int clips = progression.clipsPerCompletedRun;
            for (int tier = previous + 1; tier <= result.medal; tier++)
                clips += progression.medalBonus[Mathf.Clamp(tier - 1, 0, progression.medalBonus.Length - 1)];
            result.newMedal = Gameplay.Progression.RecordMedal(result.mode, result.medal);
            result.clipsEarned = clips;
            Gameplay.Progression.AddClips(clips, "run complete");
        }
    }
}
