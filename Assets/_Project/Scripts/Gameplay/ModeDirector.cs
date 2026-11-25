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
        /// <summary>Flight School was finished (the results screen offers Night 1 next).</summary>
        public bool training;
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

        [Header("Rooms (Stage 21). Each has its own throw point and mode groups. Empty = the groups below.")]
        [SerializeField] Room[] rooms;

        [Header("Scene groups (used only if there are no Rooms)")]
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
        [Tooltip("Stage 46: more story campaigns (Tiny War...). Each has its own save id and stars.")]
        [SerializeField] CampaignSettings[] moreCampaigns = new CampaignSettings[0];
        CampaignSettings nightShift;
        /// <summary>The campaign being played or browsed (the Night Shift unless another was picked).</summary>
        public CampaignSettings Campaign => campaign;

        /// <summary>Every campaign: the Night Shift first, then the others.</summary>
        public int CampaignCount => (nightShift != null ? 1 : 0) + (moreCampaigns != null ? moreCampaigns.Length : 0);
        public CampaignSettings CampaignAt(int i)
        {
            if (nightShift != null) { if (i == 0) return nightShift; i--; }
            return moreCampaigns != null && i >= 0 && i < moreCampaigns.Length ? moreCampaigns[i] : null;
        }

        /// <summary>Makes a campaign the current one (its nights, its stars). Not during a run.</summary>
        public void SelectCampaign(CampaignSettings c)
        {
            if (c == null || c == campaign) return;
            campaign = c;
            NightIndex = -1;
            Gameplay.Progression.Scope = c.id ?? "";
            Gameplay.Progression.SyncCampaign(c.id ?? "", c.version);
        }

        [Header("Missions (Stage 29)")]
        [Tooltip("Pilot settings for an Ace night's rival (sharper, more aggressive).")]
        [SerializeField] RivalPilotSettings acePilot;

        /// <summary>The director in the scene (rooms and HUD read it).</summary>
        public static ModeDirector Current { get; private set; }
        /// <summary>Tonight's mission objective (Standard in free play).</summary>
        public NightObjective Objective => InCampaign && Mode == GameMode.Dogfight ? Night.objective : NightObjective.Standard;
        public ConvoySpawner Convoy => ConvoySpawner.Active;

        public GameMode Mode { get; private set; }
        /// <summary>Flight School is running (a guided free flight in the office with a fully upgraded plane).</summary>
        public bool InTraining { get; private set; }
        /// <summary>The tutorial driving Flight School (lives in the office's FlightSchool group).</summary>
        public Tutorial Training { get; private set; }
        /// <summary>The office group holding Flight School (drones, a training rival, the tutorial).</summary>
        public const string TrainingGroup = "FlightSchool";
        public bool HasTraining => RoomAt(0) != null && RoomAt(0).GroupNamed(TrainingGroup) != null;
        const string TrainingDoneKey = "PaperWings.FlightSchoolDone";
        public static bool TrainingDone => PlayerPrefs.GetInt(TrainingDoneKey, 0) == 1;
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
        public ModeResult LastResult { get; private set; }
        public bool PlayerFlying => player != null && player.State == PlaneState.Flying;
        public float PlayerFlightTime => player != null ? player.FlightTime : 0f;

        public event Action<ModeResult> Completed;

        int killBaseline;
        int teamKillBaseline;
        ProgressionApplier applier;
        RaceRoute[] routes;          // per room (its main route)
        GameObject activeRouteGroup; // the route switched on for this run (a room can have several, Stage 27)
        RaceRoute activeRoute;
        Transform lastSpawn;
        BossBase[] bosses;           // per room
        GameObject activeExtraGroup; // tonight's mission group (Stage 29)

        // ------------------------------------------------------------------ rooms

        /// <summary>The room being played (or picked last).</summary>
        public int RoomIndex { get; private set; }
        public int RoomCount => rooms != null && rooms.Length > 0 ? rooms.Length : 1;
        public Room RoomAt(int i) => rooms != null && i >= 0 && i < rooms.Length ? rooms[i] : null;
        /// <summary>
        /// False for a retired room's empty slot (Stage 42: the stairwell, slot 3). Slots keep their
        /// numbers because campaign nights and saved medals refer to rooms by number.
        /// </summary>
        public bool HasRoom(int i) => i == 0 || RoomAt(i) != null;
        public Room CurrentRoom => RoomAt(RoomIndex);
        public string RoomName(int i) => RoomAt(i) != null ? RoomAt(i).displayName : "Office";

        GameObject RouteGroupOf(int i) => RoomAt(i) != null ? RoomAt(i).routeGroup : routeGroup;
        GameObject CombatGroupOf(int i) => RoomAt(i) != null ? RoomAt(i).combatGroup : combatGroup;
        GameObject BossGroupOf(int i) => RoomAt(i) != null ? RoomAt(i).bossGroup : bossGroup;
        GameObject SquadronGroupOf(int i) => RoomAt(i) != null ? RoomAt(i).squadronGroup : squadronGroup;

        public bool RoomSupports(int room, GameMode m) => m switch
        {
            GameMode.FreeFlight => true,
            GameMode.RingRun => RouteGroupOf(room) != null,
            GameMode.Dogfight => CombatGroupOf(room) != null,
            GameMode.Squadron => CombatGroupOf(room) != null && SquadronGroupOf(room) != null,
            _ => BossGroupOf(room) != null
        };

        public RaceRoute RouteOf(int room) => routes != null && room >= 0 && room < routes.Length ? routes[room] : null;
        /// <summary>The ring route of this run (a campaign night may pick one of the room's extra routes).</summary>
        public RaceRoute Route => activeRoute != null ? activeRoute : RouteOf(RoomIndex);
        public BossBase Boss => bosses != null && RoomIndex < bosses.Length ? bosses[RoomIndex] : null;

        /// <summary>Medals are kept per mode and room (the office keeps the plain mode number, so old medals stay).</summary>
        public static int MedalKey(GameMode m, int room) => (int)m + 100 * room;

        public static string ModeName(GameMode m) => m switch
        {
            GameMode.FreeFlight => "Free Flight",
            GameMode.RingRun => "Ring Run",
            GameMode.Dogfight => "Dogfight",
            GameMode.Squadron => "Squadron Battle",
            _ => "Boss Fight"
        };

        static string BestKey(GameMode m, int room) => room == 0 ? $"PaperWings.Best.{m}" : $"PaperWings.Best.R{room}.{m}";
        public float BestTime(GameMode m) => BestTime(m, RoomIndex);
        public float BestTime(GameMode m, int room) => PlayerPrefs.GetFloat(BestKey(m, room), 0f);

        void Awake()
        {
            Current = this;
            routes = new RaceRoute[RoomCount];
            bosses = new BossBase[RoomCount];
            for (int i = 0; i < RoomCount; i++)
            {
                if (RouteGroupOf(i) != null) routes[i] = RouteGroupOf(i).GetComponentInChildren<RaceRoute>(true);
                if (BossGroupOf(i) != null) bosses[i] = BossGroupOf(i).GetComponentInChildren<BossBase>(true);
            }
            RoomIndex = Mathf.Clamp(PlayerPrefs.GetInt("PaperWings.LastRoom", 0), 0, RoomCount - 1);
            if (!HasRoom(RoomIndex)) RoomIndex = 0;   // last flown in a room that's been removed
            nightShift = campaign;
            if (campaign != null) Gameplay.Progression.SyncCampaign(campaign.version);
            Gameplay.Progression.Scope = campaign != null ? campaign.id ?? "" : "";
            if (launcher != null) applier = launcher.GetComponent<ProgressionApplier>();
            // Until a mode is chosen, nothing hostile is running.
            AllGroupsOff();
        }

        void Start()
        {
            // Put the plane in the remembered room's hand (LaunchAndReset.Start holds it at its spawn point).
            if (launcher != null && CurrentRoom != null)
            {
                launcher.SetSpawnPoint(CurrentRoom.spawnPoint);
                if (RoomIndex != 0) { launcher.ResetToStart(); SnapCameras(); }
            }
        }

        void OnEnable()
        {
            Current = this;
            ConvoySpawner.HostileDowned += OnFlyerDowned;
            CombatScore.KillScored += OnPlayerKill;
            if (bosses != null) foreach (var b in bosses) if (b != null) b.Defeated += OnBossDefeated;
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            ConvoySpawner.HostileDowned -= OnFlyerDowned;
            CombatScore.KillScored -= OnPlayerKill;
            if (bosses != null) foreach (var b in bosses) if (b != null) b.Defeated -= OnBossDefeated;
            AirField.WindScale = 1f;
        }

        // Small rewards for things you do during any mode.
        void OnPlayerKill(int kills)
        {
            if (progression != null && HasMode && !InTraining) Gameplay.Progression.AddClips(progression.clipsPerKill, "shot down");
        }

        void OnFlyerDowned(PathFlyer flyer, PlaneController by)
        {
            if (progression != null && HasMode && !InTraining && by != null && by.IsPlayer && Objective != NightObjective.Practice)
                Gameplay.Progression.AddClips(progression.clipsPerKill, "courier down");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>Starts (or restarts) a free-play mode from scratch, in a room (-1 = the current one).</summary>
        public void StartMode(GameMode mode, int room = -1)
        {
            NightIndex = -1;
            Begin(mode, room < 0 ? RoomIndex : room);
            PlayerPrefs.SetInt("PaperWings.LastMode", (int)mode);
        }

        /// <summary>
        /// Flight School: a guided flight in the office with every fold and upgrade. Its tutorial
        /// switches drones and a training rival on step by step, then calls <see cref="CompleteTraining"/>.
        /// </summary>
        public void StartTraining()
        {
            NightIndex = -1;
            Begin(GameMode.FreeFlight, 0);
            if (applier != null) applier.FullLoadout = true;
            InTraining = true;
            activeExtraGroup = RoomAt(0) != null ? RoomAt(0).GroupNamed(TrainingGroup) : null;
            Toggle(activeExtraGroup, true);
            Training = activeExtraGroup != null ? activeExtraGroup.GetComponentInChildren<Tutorial>(true) : null;
            if (Training != null) Training.Begin(player);
            if (launcher != null) launcher.ResetToStart();   // in hand with the full loadout
        }

        /// <summary>The tutorial's last step is done.</summary>
        public void CompleteTraining()
        {
            if (!InTraining || RunComplete) return;
            bool first = !TrainingDone;
            PlayerPrefs.SetInt(TrainingDoneKey, 1);
            PlayerPrefs.Save();
            var result = new ModeResult
            {
                mode = Mode,
                training = true,
                title = "FLIGHT SCHOOL COMPLETE!",
                detail = "You're cleared for the Night Shift. Your own plane starts as a plain sheet: upgrades come from the campaign.",
                time = RunTime
            };
            if (first && progression != null)
            {
                result.clipsEarned = 30;
                Gameplay.Progression.AddClips(30, "flight school");
            }
            RunComplete = true;
            result.night = -1;
            LastResult = result;
            Completed?.Invoke(result);
        }

        /// <summary>Starts (or restarts) a campaign night: its mode and room plus tonight's twist.</summary>
        public void StartNight(int index)
        {
            if (campaign == null || campaign.Get(index) == null) return;
            NightIndex = index;
            Begin(Night.mode, Night.room);
            PlayerPrefs.SetInt("PaperWings.LastNight", index);
        }

        /// <summary>Right room, right groups on, twists applied, plane back in hand, timer reset.</summary>
        void Begin(GameMode mode, int room)
        {
            InTraining = false;
            Training = null;
            if (applier != null) applier.FullLoadout = false;
            room = Mathf.Clamp(room, 0, RoomCount - 1);
            if (!HasRoom(room) || !RoomSupports(room, mode)) room = 0;   // e.g. a Boss Fight asked for in a room without a boss
            bool moved = room != RoomIndex;
            RoomIndex = room;
            PlayerPrefs.SetInt("PaperWings.LastRoom", room);

            // Which route and throw point: a night can name one of the room's places.
            var place = Night;
            GameObject areaRoute = place != null && CurrentRoom != null ? CurrentRoom.RouteNamed(place.area) : null;
            activeRouteGroup = areaRoute != null ? areaRoute : RouteGroupOf(room);
            activeRoute = activeRouteGroup != null ? activeRouteGroup.GetComponentInChildren<RaceRoute>(true) : null;
            Transform spawn = place != null && CurrentRoom != null ? CurrentRoom.SpawnNamed(place.spawn) : null;
            if (spawn == null && areaRoute != null) spawn = areaRoute.transform.Find("Spawn");
            if (spawn == null && CurrentRoom != null) spawn = CurrentRoom.spawnPoint;
            if (launcher != null && spawn != null) launcher.SetSpawnPoint(spawn);
            if (spawn != lastSpawn) moved = true;   // a different throw point in the same room still needs a camera cut
            lastSpawn = spawn;

            Mode = mode;
            HasMode = true;
            RunStarted = false;
            RunComplete = false;
            RunTime = 0f;
            LastResult = null;

            // Toggling a group off and on again gives a clean slate (spawners respawn their rivals).
            AllGroupsOff();
            LimitRivals(0);   // all regular spawners back on (while their group is off, so nothing spawns yet)
            // Squadron = the solo-dogfight rivals PLUS the squadron group (extra rivals and your wingmen).
            SetGroups(route: mode == GameMode.RingRun,
                      combat: mode == GameMode.Dogfight || mode == GameMode.Squadron,
                      boss: mode == GameMode.BossFight,
                      squadron: mode == GameMode.Squadron);

            // Tonight's twist (free play = normal rules).
            var night = Night;
            if (night != null && night.maxRivals != 0) LimitRivals(night.maxRivals);

            // Tonight's mission group (a convoy route...) and ace.
            activeExtraGroup = night != null && CurrentRoom != null ? CurrentRoom.GroupNamed(night.group) : null;
            Toggle(activeExtraGroup, true);
            if (night != null && mode == GameMode.Dogfight && night.objective == NightObjective.Ace) MakeAce(night);
            if (night != null && night.rivalPilot != null) SetRivalPilots(night.rivalPilot);
            AirField.WindScale = night != null ? Mathf.Max(0f, night.windScale) : 1f;
            if (applier != null) applier.PencilsBanned = night != null && night.noPencils;

            if (mode == GameMode.BossFight && Boss != null) Boss.ResetFight();
            if (mode == GameMode.RingRun && Route != null) Route.ResetRoute();

            // The room's cat starts her day again: asleep on her nap spot, or awake if tonight says so (Stage 45).
            if (CurrentRoom != null)
                foreach (var cat in CatBrain.All.ToArray())
                    if (cat != null && cat.transform.IsChildOf(CurrentRoom.transform)) cat.Restart(night != null && night.catAwake);

            killBaseline = CombatScore.Kills;
            teamKillBaseline = CombatScore.TeamKills;
            if (launcher != null) launcher.ResetToStart();   // in hand: the pencil launcher re-arms to the new limit
            if (moved) SnapCameras();
        }

        /// <summary>
        /// The plane just jumped to another room: tell the cameras not to glide all the way there.
        /// UNITY NOTE: Cinemachine cameras smooth their movement using last frame's state;
        /// PreviousStateIsValid = false makes them cut straight to the new spot instead.
        /// </summary>
        static void SnapCameras()
        {
            foreach (var cam in FindObjectsByType<Unity.Cinemachine.CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
                cam.PreviousStateIsValid = false;
        }

        /// <summary>Keeps only the first <paramref name="max"/> regular rival spawners running (0 = all, -1 = none).</summary>
        void LimitRivals(int max)
        {
            var group = CombatGroupOf(RoomIndex);
            if (group == null) return;
            var spawners = group.GetComponentsInChildren<EnemySpawner>(true);
            for (int i = 0; i < spawners.Length; i++)
                spawners[i].enabled = max == 0 || (max > 0 && i < max);   // disabling a spawner removes its rival
        }

        /// <summary>Tonight's rivals (the room's regulars and the mission group's) fly with other pilot settings.</summary>
        void SetRivalPilots(RivalPilotSettings pilot)
        {
            foreach (var group in new[] { CombatGroupOf(RoomIndex), activeExtraGroup })
                if (group != null)
                    foreach (var sp in group.GetComponentsInChildren<EnemySpawner>(false))
                        if (sp.enabled) sp.PilotOverride = pilot;
        }

        /// <summary>Ace night: the first running rival spawner sends a named ace instead.</summary>
        void MakeAce(CampaignNight night)
        {
            var group = CombatGroupOf(RoomIndex);
            if (group == null) return;
            foreach (var sp in group.GetComponentsInChildren<EnemySpawner>(false))
            {
                if (!sp.enabled) continue;
                sp.MakeAce(new EnemySpawner.AceStyle
                {
                    name = string.IsNullOrEmpty(night.aceName) ? "THE ACE" : night.aceName.ToUpperInvariant(),
                    pilot = acePilot
                });
                return;
            }
        }

        /// <summary>The objective line for the in-flight strip (null = let the menu describe the mode).</summary>
        public string ObjectiveText()
        {
            if (InTraining) return Training != null ? Training.StripText : "Flight School";
            var c = Convoy;
            switch (Objective)
            {
                case NightObjective.Intercept:
                    return c == null ? "Intercept the couriers" :
                        $"Couriers down  <b>{c.Downed} / {Night.Target(DefaultDogfightTarget)}</b>   <color=#FF8060>escaped {c.Arrived} / {Night.allowedLosses}</color>";
                case NightObjective.Escort:
                    if (c == null) return "Protect the courier";
                    string hp = c.Alive.Count > 0 && c.Alive[0] != null ? $"   courier paper <b>{c.Alive[0].Health01 * 100f:0}%</b>" : (c.Started ? "   <color=#AAAAAA>next courier coming</color>" : "   <color=#AAAAAA>throw to start</color>");
                    return $"Couriers through  <b>{c.Arrived} / {Night.Target(1)}</b>   <color=#FF8060>lost {c.Lost} / {Night.allowedLosses}</color>{hp}";
                case NightObjective.Practice:
                    return c == null ? "Shoot the drones" : $"Drones down  <b>{c.Downed} / {Night.Target(DefaultDogfightTarget)}</b>";
                case NightObjective.Ace:
                    return $"{(string.IsNullOrEmpty(Night.aceName) ? "The ace" : Night.aceName)} shot down  <b>{KillsThisRun} / {Night.Target(1)}</b>   <color=#FFD040>gold trail</color>";
            }
            return null;
        }

        void AllGroupsOff()
        {
            for (int i = 0; i < RoomCount; i++)
            {
                Toggle(RouteGroupOf(i), false);
                Toggle(CombatGroupOf(i), false);
                Toggle(BossGroupOf(i), false);
                Toggle(SquadronGroupOf(i), false);
                var r = RoomAt(i);
                if (r != null && r.extraRoutes != null) foreach (var g in r.extraRoutes) Toggle(g, false);
                if (r != null && r.extraGroups != null) foreach (var g in r.extraGroups) Toggle(g, false);
            }
        }

        /// <summary>Switches the current room's groups.</summary>
        void SetGroups(bool route, bool combat, bool boss, bool squadron = false)
        {
            Toggle(activeRouteGroup != null ? activeRouteGroup : RouteGroupOf(RoomIndex), route);
            Toggle(CombatGroupOf(RoomIndex), combat);
            Toggle(BossGroupOf(RoomIndex), boss);
            Toggle(SquadronGroupOf(RoomIndex), squadron);
        }

        static void Toggle(GameObject go, bool on) { if (go != null) go.SetActive(on); }

        void Update()
        {
            if (!HasMode || RunComplete || player == null) return;

            if (!RunStarted && player.State == PlaneState.Flying) RunStarted = true;
            if (RunStarted) RunTime += Time.deltaTime;

            if (Mode == GameMode.Squadron && TeamKillsThisRun >= SquadronTarget)
                CompleteTimed("SQUADRON VICTORY!", $"Your squadron downed {SquadronTarget} rivals in");

            switch (Objective)
            {
                case NightObjective.Standard:
                    if (Mode == GameMode.Dogfight && KillsThisRun >= DogfightTarget)
                        CompleteTimed("DOGFIGHT WON!", $"{DogfightTarget} rivals shot down in");
                    break;
                case NightObjective.Ace:
                    if (KillsThisRun >= Night.Target(1))
                        CompleteTimed($"{(string.IsNullOrEmpty(Night.aceName) ? "THE ACE" : Night.aceName.ToUpperInvariant())} GROUNDED!", "Ace shot down in");
                    break;
                default:
                    CheckConvoy();
                    break;
            }

            // Ring Run: the last ring stops the clock (the route records the time and best).
            if (Mode == GameMode.RingRun && Route != null && Route.AllRingsPassed)
            {
                float t = Route.LastTime;
                bool best = !InCampaign && Route.LastWasNewBest;
                Finish(new ModeResult
                {
                    mode = Mode,
                    title = "RING RUN COMPLETE!",
                    detail = $"All {Route.RingCount} rings in {t:0.00}s" + (!InCampaign && !best && Route.BestTime > 0f ? $"   (best {Route.BestTime:0.00}s)" : ""),
                    newBest = best,
                    time = t
                });
            }

            // Free Flight nights: stay in the air long enough in one flight. Stars go by the total clock,
            // so fewer failed throws = more stars.
            if (Mode == GameMode.FreeFlight && InCampaign && Night.airTimeTarget > 0f
                && player.State == PlaneState.Flying && player.FlightTime >= Night.airTimeTarget)
            {
                Finish(new ModeResult
                {
                    mode = Mode,
                    title = "STILL FLYING!",
                    detail = $"{Night.airTimeTarget:0}s in the air in one flight ({RunTime:0.0}s on the clock)",
                    time = RunTime
                });
            }

            // Out of time? (Checked last so a win on the final frame still counts.)
            if (!RunComplete && TimeLeft == 0f)
                Fail("OUT OF TIME", $"The clock ran out at {Night.timeLimit:0}s.");
        }

        void CheckConvoy()
        {
            var c = Convoy;
            if (c == null) return;
            var night = Night;
            int target = night.Target(night.objective == NightObjective.Escort ? 1 : DefaultDogfightTarget);
            switch (night.objective)
            {
                case NightObjective.Intercept:
                    if (c.Downed >= target) CompleteTimed("INTERCEPTED!", $"{target} couriers shot down in");
                    else if (c.Arrived >= night.allowedLosses) Fail("THEY GOT AWAY", $"{c.Arrived} couriers made it out. Catch them earlier: they always fly the same line.");
                    break;
                case NightObjective.Escort:
                    if (c.Arrived >= target) CompleteTimed("DELIVERED!", target > 1 ? $"All {target} couriers through in" : "The courier got through in");
                    else if (c.Lost >= night.allowedLosses) Fail("COURIER DOWN", "Stay close to it and shoot whatever chases it.");
                    break;
                case NightObjective.Practice:
                    if (c.Downed >= target) CompleteTimed("TARGET PRACTICE DONE!", $"{target} drones down in");
                    break;
            }
        }

        void OnBossDefeated()
        {
            if (HasMode && Mode == GameMode.BossFight && !RunComplete)
                CompleteTimed($"{(Boss != null ? Boss.DisplayName : "THE BOSS")} IS DEFEATED!", "Boss down in");
        }

        void CompleteTimed(string title, string lead)
        {
            // Campaign twists change the rules, so nights don't touch the free-play best times.
            float best = BestTime(Mode);
            bool newBest = !InCampaign && (best <= 0f || RunTime < best);
            if (newBest) { PlayerPrefs.SetFloat(BestKey(Mode, RoomIndex), RunTime); PlayerPrefs.Save(); }
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
            result.medal = progression.MedalFor(result.mode, result.time, CurrentRoom != null && result.mode == GameMode.RingRun ? CurrentRoom.ringRunMedals : Vector3.zero);
            int previous = Gameplay.Progression.GetMedal(MedalKey(result.mode, RoomIndex));
            int clips = progression.clipsPerCompletedRun;
            for (int tier = previous + 1; tier <= result.medal; tier++)
                clips += progression.medalBonus[Mathf.Clamp(tier - 1, 0, progression.medalBonus.Length - 1)];
            result.newMedal = Gameplay.Progression.RecordMedal(MedalKey(result.mode, RoomIndex), result.medal);
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
                // Hangar blueprints belong to the Night Shift's nights.
                var blueprints = progression != null && string.IsNullOrEmpty(campaign.id) ? progression.BlueprintsFor(NightIndex) : null;
                if (blueprints != null && blueprints.Count > 0)
                    result.reward = $"New in the hangar: {string.Join(", ", blueprints)}";
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
