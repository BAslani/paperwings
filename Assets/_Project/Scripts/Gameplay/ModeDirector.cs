using System;
using UnityEngine;
using PaperWings.Air;
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
        /// <summary>Run time in seconds (what medals and stars are judged on).</summary>
        public float time;
        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int medal;
        public bool newMedal;
        public int clipsEarned;

        // Campaign nights
        /// <summary>Campaign night index, or -1 for a free-play mode.</summary>
        public int night = -1;
        /// <summary>The night was lost (out of time). Nothing is awarded.</summary>
        public bool failed;
        public int stars;
        public bool newStars;
        /// <summary>"Unlocked: Pencil Missiles" etc. for the first clear, or empty.</summary>
        public string reward = "";
        /// <summary>This clear finished the whole campaign for the first time.</summary>
        public bool campaignComplete;
    }

    /// <summary>
    /// Runs the desk as separate game modes. Each mode switches the right parts of the scene on
    /// (ring route, rival spawners, boss) and the rest off, tracks its objective and a run timer,
    /// and saves a best time per mode.
    ///
    /// Campaign nights (Stage 18) are a mode plus a twist from CampaignSettings: a different kill
    /// target, fewer rivals, a time limit, rough air or no pencils. StartNight sets them up.
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

        [Tooltip("The Night Shift campaign. Leave empty to hide the campaign.")]
        [SerializeField] CampaignSettings campaign;
        public CampaignSettings Campaign => campaign;

        public GameMode Mode { get; private set; }
        public bool HasMode { get; private set; }
        public bool RunStarted { get; private set; }
        public bool RunComplete { get; private set; }
        public float RunTime { get; private set; }

        /// <summary>The campaign night being played, or -1 in free play.</summary>
        public int NightIndex { get; private set; } = -1;
        public CampaignNight Night => campaign != null ? campaign.Get(NightIndex) : null;
        public bool InCampaign => Night != null;
        /// <summary>Seconds left on tonight's clock, or -1 if there's no limit.</summary>
        public float TimeLeft => InCampaign && Night.timeLimit > 0f ? Mathf.Max(0f, Night.timeLimit - RunTime) : -1f;

        public int DogfightTarget => InCampaign && Night.killTarget > 0 ? Night.killTarget : dogfightKillTarget;
        public int SquadronTarget => InCampaign && Night.killTarget > 0 ? Night.killTarget : squadronKillTarget;
        public int DefaultDogfightTarget => dogfightKillTarget;
        public int DefaultSquadronTarget => squadronKillTarget;
        public int KillsThisRun => CombatScore.Kills - killBaseline;
        public int TeamKillsThisRun => CombatScore.TeamKills - teamKillBaseline;
        public RaceRoute Route { get; private set; }
        public StaplerBoss Boss { get; private set; }
        public ModeResult LastResult { get; private set; }

        public event Action<ModeResult> Completed;

        int killBaseline;
        int teamKillBaseline;
        bool landingPending;
        ProgressionApplier applier;

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
            if (launcher != null) applier = launcher.GetComponent<ProgressionApplier>();
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
            AirField.WindScale = 1f;
        }

        // Small rewards for things you do during any mode.
        void OnPlayerKill(int kills)
        {
            if (progression != null && HasMode) Gameplay.Progression.AddClips(progression.clipsPerKill, "shot down");
        }

        /// <summary>Starts (or restarts) a free-play mode from scratch.</summary>
        public void StartMode(GameMode mode)
        {
            NightIndex = -1;
            Begin(mode);
            PlayerPrefs.SetInt("PaperWings.LastMode", (int)mode);
        }

        /// <summary>Starts (or restarts) a campaign night: its mode plus tonight's twist.</summary>
        public void StartNight(int index)
        {
            if (campaign == null || campaign.Get(index) == null) return;
            NightIndex = index;
            Begin(Night.mode);
            PlayerPrefs.SetInt("PaperWings.LastNight", index);
        }

        /// <summary>Right groups on, twists applied, plane back in hand, timer reset.</summary>
        void Begin(GameMode mode)
        {
            Mode = mode;
            HasMode = true;
            RunStarted = false;
            RunComplete = false;
            RunTime = 0f;
            LastResult = null;
            landingPending = false;

            // Toggling a group off and on again gives a clean slate (spawners respawn their rivals).
            SetGroups(route: false, combat: false, boss: false);
            LimitRivals(0);   // all regular spawners back on (while their group is off, so nothing spawns yet)
            // Squadron = the solo-dogfight rivals PLUS the squadron group (extra rivals and your wingmen).
            SetGroups(route: mode == GameMode.RingRun,
                      combat: mode == GameMode.Dogfight || mode == GameMode.Squadron,
                      boss: mode == GameMode.BossFight,
                      squadron: mode == GameMode.Squadron);

            // Tonight's twist (free play = normal rules).
            var night = Night;
            if (night != null && night.maxRivals > 0) LimitRivals(night.maxRivals);
            AirField.WindScale = night != null ? Mathf.Max(0f, night.windScale) : 1f;
            if (applier != null) applier.PencilsBanned = night != null && night.noPencils;

            if (mode == GameMode.BossFight && Boss != null) Boss.ResetFight();
            if (mode == GameMode.RingRun && Route != null) Route.ResetRoute();

            killBaseline = CombatScore.Kills;
            teamKillBaseline = CombatScore.TeamKills;
            if (launcher != null) launcher.ResetToStart();   // in hand: the pencil launcher re-arms to the new limit
        }

        /// <summary>Keeps only the first <paramref name="max"/> regular rival spawners running (0 = all).</summary>
        void LimitRivals(int max)
        {
            if (combatGroup == null) return;
            var spawners = combatGroup.GetComponentsInChildren<EnemySpawner>(true);
            for (int i = 0; i < spawners.Length; i++)
                spawners[i].enabled = max <= 0 || i < max;   // disabling a spawner removes its rival
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

            if (Mode == GameMode.Squadron && TeamKillsThisRun >= SquadronTarget)
                CompleteTimed("SQUADRON VICTORY!", $"Your squadron downed {SquadronTarget} rivals in");

            if (Mode == GameMode.Dogfight && KillsThisRun >= DogfightTarget)
                CompleteTimed("DOGFIGHT WON!", $"{DogfightTarget} rivals shot down in");

            // Landings are checked a frame later so the route has finished its own bookkeeping.
            if (landingPending)
            {
                landingPending = false;
                if (Mode == GameMode.RingRun && Route != null && Route.AllRingsPassed)
                {
                    float t = player.FlightTime;
                    Finish(new ModeResult
                    {
                        mode = Mode,
                        title = "RING RUN COMPLETE!",
                        detail = $"All rings and a mug landing in {t:0.00}s" + (!InCampaign && Route.BestTime > 0f ? $"   (best {Route.BestTime:0.00}s)" : ""),
                        newBest = !InCampaign && Route.LastWasNewBest,
                        time = t
                    });
                }
                else if (Mode == GameMode.FreeFlight && InCampaign)
                {
                    // Free Flight nights are won by landing on the mug.
                    float t = player.FlightTime;
                    Finish(new ModeResult { mode = Mode, title = "TOUCHDOWN!", detail = $"Landed on the mug after {t:0.0}s of flight", time = t });
                }
            }

            // Out of time? (Checked last so a win on the final frame still counts.)
            if (!RunComplete && TimeLeft == 0f)
                Fail("OUT OF TIME", $"The clock ran out at {Night.timeLimit:0}s.");
        }

        void OnLanded(PlaneController plane, string message)
        {
            if (plane != player || !HasMode || RunComplete) return;
            if (progression != null) Gameplay.Progression.AddClips(progression.clipsPerMugLanding, "mug landing");
            if (Mode == GameMode.RingRun || (Mode == GameMode.FreeFlight && InCampaign)) landingPending = true;
        }

        void OnBossDefeated()
        {
            if (HasMode && Mode == GameMode.BossFight && !RunComplete)
                CompleteTimed("THE STAPLER IS DEFEATED!", "Boss down in");
        }

        void CompleteTimed(string title, string lead)
        {
            // Campaign twists change the rules, so nights don't touch the free-play best times.
            float best = BestTime(Mode);
            bool newBest = !InCampaign && (best <= 0f || RunTime < best);
            if (newBest) { PlayerPrefs.SetFloat(BestKey(Mode), RunTime); PlayerPrefs.Save(); }
            Finish(new ModeResult
            {
                mode = Mode,
                title = title,
                detail = $"{lead} {RunTime:0.0}s" + (newBest || InCampaign || best <= 0f ? "" : $"   (best {best:0.0}s)"),
                newBest = newBest,
                time = RunTime
            });
        }

        void Fail(string title, string detail)
        {
            RunComplete = true;
            LastResult = new ModeResult { mode = Mode, title = title, detail = detail, time = RunTime, failed = true, night = NightIndex };
            Completed?.Invoke(LastResult);
        }

        void Finish(ModeResult result)
        {
            RunComplete = true;
            result.night = NightIndex;
            if (InCampaign) AwardNight(result);
            else AwardRun(result);
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

        /// <summary>
        /// Campaign reward: stars, the night's first-clear prize (clips and maybe a free unlock),
        /// and a bonus for each new 2nd/3rd star. Replays still pay the normal completion clips.
        /// </summary>
        void AwardNight(ModeResult result)
        {
            var night = Night;
            int previous = Gameplay.Progression.GetNightStars(NightIndex);
            bool firstClear = previous == 0;
            bool wasComplete = Gameplay.Progression.NightCleared(campaign.Count - 1);
            result.stars = night.StarsFor(result.time);

            int clips = progression != null ? progression.clipsPerCompletedRun : 0;
            for (int s = Mathf.Max(previous + 1, 2); s <= result.stars; s++)
                clips += campaign.starBonus[Mathf.Clamp(s - 2, 0, campaign.starBonus.Length - 1)];

            if (firstClear)
            {
                clips += night.rewardClips;
                if (!string.IsNullOrEmpty(night.rewardUnlockId))
                {
                    var item = progression != null ? progression.Find(night.rewardUnlockId) : null;
                    string name = item != null ? item.LongName : night.rewardUnlockId;
                    if (Gameplay.Progression.GrantUnlock(night.rewardUnlockId))
                        result.reward = $"Unlocked: {name}!";
                    else
                    {
                        // Already bought it in the shop: refund its price instead.
                        int refund = item != null ? item.cost : 0;
                        clips += refund;
                        result.reward = $"You already own {name}, so here are {refund} clips instead.";
                    }
                }
            }

            result.newStars = Gameplay.Progression.RecordNightStars(NightIndex, result.stars);
            result.campaignComplete = !wasComplete && Gameplay.Progression.NightCleared(campaign.Count - 1);
            result.clipsEarned = clips;
            Gameplay.Progression.AddClips(clips, firstClear ? "night cleared" : "night replayed");
        }
    }
}
