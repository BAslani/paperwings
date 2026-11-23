using System;
using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>What a Dogfight-based night asks of you (Stage 29).</summary>
    public enum NightObjective
    {
        Standard,    // shoot down N rivals (or the mode's normal goal)
        Intercept,   // shoot down N Accounting couriers before too many get away
        Escort,      // keep your courier(s) alive until N arrive
        Practice,    // shoot N practice drones (no rivals)
        Ace          // one named ace: tougher, sharper, gold trail. Shoot it down N times
    }

    /// <summary>
    /// One campaign mission ("night"): a game mode plus a twist, a memo to brief you, and a reward.
    /// Nights unlock in order; clearing one opens the next.
    /// </summary>
    [Serializable]
    public class CampaignNight
    {
        public string title = "New Night";
        [Tooltip("The briefing memo shown on the campaign screen.")]
        [TextArea(2, 4)] public string memo = "";
        [Tooltip("Which mode this night is built on. Free Flight nights are won by staying in the air (Air Time Target).")]
        public GameMode mode = GameMode.RingRun;
        [Tooltip("Which room: 0 office, 1 break room, 2 server room, 3 stairwell, 4 rooftop.")]
        public int room = 0;
        [Tooltip("Where in the room, shown on the campaign screen (e.g. 'Office: the window row').")]
        public string place = "";
        [Tooltip("Ring Run only: which of the room's routes to fly (a Room Extra Route name). Empty = the room's main route.")]
        public string area = "";
        [Tooltip("Which throw point (a Room Extra Spawn name). Empty = the area route's own 'Spawn', else the room's.")]
        public string spawn = "";

        [Header("Mission (Stage 29; Dogfight nights)")]
        public NightObjective objective = NightObjective.Standard;
        [Tooltip("A group under the room switched on for this night only (a convoy route, extra spawners). Room Extra Groups, by name.")]
        public string group = "";
        [Tooltip("Intercept: couriers allowed to get away. Escort: couriers you may lose. The night fails when it's reached.")]
        public int allowedLosses = 2;
        [Tooltip("Ace nights: the ace's name on the HUD.")]
        public string aceName = "";

        [Header("Twist (0 / off = the mode's normal rules)")]
        [Tooltip("Dogfight / Squadron: kills needed (Intercept / Practice: flyers to down, Escort: couriers to deliver). 0 = the mode's usual target.")]
        public int killTarget = 0;
        [Tooltip("Dogfight / Squadron: how many of the regular rival spawners are active. 0 = all, -1 = none.")]
        public int maxRivals = 0;
        [Tooltip("Fail the night if the clock passes this (seconds). 0 = no limit.")]
        public float timeLimit = 0f;
        [Tooltip("Free Flight nights: seconds to stay in the air in ONE flight to win.")]
        public float airTimeTarget = 0f;
        [Tooltip("Strength of every fan, thermal and vent. 1 = normal, 1.6 = rough air.")]
        public float windScale = 1f;
        [Tooltip("Pencils stay in the drawer tonight, even if unlocked.")]
        public bool noPencils = false;

        [Header("Stars (1 for clearing, 2 and 3 for speed)")]
        [Tooltip("Clear at or under this time for 2 stars. 0 = no time stars.")]
        public float twoStarTime = 0f;
        [Tooltip("Clear at or under this time for 3 stars.")]
        public float threeStarTime = 0f;

        [Header("Reward (first clear only)")]
        [Tooltip("Paper clips for the first clear.")]
        public int rewardClips = 30;
        [Tooltip("Unlock id granted for free on the first clear (empty = none), e.g. 'fold_glider' or 'dart:pencils'. If you already own it, you get its price in clips instead.")]
        public string rewardUnlockId = "";

        public int Target(int fallback) => killTarget > 0 ? killTarget : fallback;

        public int StarsFor(float time)
        {
            if (threeStarTime > 0f && time <= threeStarTime) return 3;
            if (twoStarTime > 0f && time <= twoStarTime) return 2;
            return 1;
        }

        /// <summary>One line of what you have to do, for the menu and the in-flight strip.</summary>
        public string Goal(int defaultKillTarget, int defaultSquadronTarget)
        {
            string g = mode switch
            {
                GameMode.FreeFlight => $"Stay in the air for {airTimeTarget:0}s in one flight.",
                GameMode.RingRun => "Fly through every ring.",
                GameMode.Dogfight => objective switch
                {
                    NightObjective.Intercept => $"Shoot down {Target(defaultKillTarget)} couriers before {allowedLosses} get away.",
                    NightObjective.Escort => $"Keep your courier alive: {Target(1)} must get through. Lose {allowedLosses} and it's over.",
                    NightObjective.Practice => $"Shoot down {Target(defaultKillTarget)} practice drones.",
                    NightObjective.Ace => $"Shoot down {(string.IsNullOrEmpty(aceName) ? "the ace" : aceName)}{(Target(1) > 1 ? $" {Target(1)} times" : "")}.",
                    _ => $"Shoot down {Target(defaultKillTarget)} rivals."
                },
                GameMode.Squadron => $"Your squadron needs {(killTarget > 0 ? killTarget : defaultSquadronTarget)} kills.",
                _ => "Defeat the boss."
            };
            if (timeLimit > 0f) g += $" Under {timeLimit:0}s.";
            if (noPencils) g += " No pencils.";
            if (windScale > 1.05f) g += " Rough air.";
            return g;
        }
    }

    /// <summary>
    /// The Night Shift campaign: an ordered list of nights. PaperWings > Settings > Campaign.
    /// Changing the nights? Bump Version so saved stars don't land on the wrong nights.
    /// Add, reorder or retune nights here. Saved stars are stored per night index, so
    /// inserting a night in the middle shifts which saved stars belong to which night.
    /// </summary>
    [CreateAssetMenu(fileName = "Campaign", menuName = "PaperWings/Campaign Settings")]
    public class CampaignSettings : ScriptableObject
    {
        [Tooltip("Bump this when the nights change: saved stars from another version are cleared (clips and unlocks stay).")]
        public int version = CurrentVersion;
        public const int CurrentVersion = 3;

        [Tooltip("Extra paper clips for each NEW star beyond the first (2nd star, 3rd star).")]
        public int[] starBonus = { 10, 25 };

        public CampaignNight[] nights = NightShift();

        /// <summary>
        /// The Night Shift (campaign version 3, Stage 29): thirteen nights of air combat climbing the
        /// building. No two nights in a room ask for the same thing:
        ///   Office      target practice, a lone trespasser, intercept the couriers, The Stapler
        ///   Break room  a dogfight at the office party, escort the coffee courier, The Auditor (it flies)
        ///   Server room a squadron battle, an ace duel in the dark, The Mainframe
        ///   Stairwell   intercept couriers climbing the chimney
        ///   Rooftop     a dogfight in the wind, The Condenser
        /// Nights pay paper clips and unlock BLUEPRINTS (what the hangar is allowed to sell); the
        /// upgrades themselves are bought. Rooms: 0 office, 1 break room, 2 server room, 3 stairwell, 4 rooftop.
        /// </summary>
        public static CampaignNight[] NightShift() => new[]
        {
            // ---------------- Act 1: the office ----------------
            new CampaignNight
            {
                title = "Target Practice", mode = GameMode.Dogfight, objective = NightObjective.Practice, room = 0,
                spawn = "LoungeSpawn", group = "PracticeDrones", maxRivals = -1, killTarget = 6,
                place = "Office: the lounge", twoStarTime = 75f, threeStarTime = 50f, rewardClips = 40,
                memo = "MEMO from Marketing: Welcome to the night shift, rookie. Hold Throw to wind up, let go to fly, hold Fire for staples. " +
                       "Six practice drones are circling the office. The space heater by the couch throws up warm air (orange): circle in it to climb."
            },
            new CampaignNight
            {
                title = "Trespasser", mode = GameMode.Dogfight, room = 0, spawn = "CoworkerSpawn",
                place = "Office: the coworker's corner", killTarget = 2, maxRivals = 1,
                twoStarTime = 60f, threeStarTime = 35f, rewardClips = 50,
                memo = "MEMO: An Accounting plane (red trail) is snooping round the office. It keeps coming back, so shoot it down twice. " +
                       "Their pilots climb in warm air to recover: the radiators under the windows are where they'll go."
            },
            new CampaignNight
            {
                title = "Paper Trail", mode = GameMode.Dogfight, objective = NightObjective.Intercept, room = 0,
                group = "WindowCouriers", killTarget = 4, allowedLosses = 3, maxRivals = 1,
                place = "Office: the window row", twoStarTime = 90f, threeStarTime = 60f, rewardClips = 60,
                memo = "MEMO: Accounting couriers are carrying our campaign plans along the window row to the door. Shoot down four before three get out. " +
                       "They always fly the same line, and one rival flies cover. Ride the radiators to stay above them."
            },
            new CampaignNight
            {
                title = "The Stapler", mode = GameMode.BossFight, room = 0,
                place = "Office: your desk", twoStarTime = 150f, threeStarTime = 100f, rewardClips = 120,
                memo = "MEMO: Accounting's desk guardian has woken up on YOUR desk. Its jaw is armored; hit the glowing mouth when it " +
                       "opens. When the jaw rises, it slams and blasts the air down: get clear."
            },

            // ---------------- Act 2: the break room ----------------
            new CampaignNight
            {
                title = "Office Party", mode = GameMode.Dogfight, room = 1, spawn = "CouchSpawn", killTarget = 4,
                place = "Break room: the party", twoStarTime = 100f, threeStarTime = 70f, rewardClips = 70,
                memo = "MEMO: Somebody's leaving do, and Accounting crashed it. Four kills among the balloons (shoot them to clear a lane). " +
                       "Watch the appliances: the microwave dumps heat when it dings, the fridge door swings open, and the vending machine has gone haywire."
            },
            new CampaignNight
            {
                title = "Coffee Run", mode = GameMode.Dogfight, objective = NightObjective.Escort, room = 1,
                group = "CoffeeCourier", killTarget = 2, allowedLosses = 2, maxRivals = 2,
                place = "Break room: the coffee machine", twoStarTime = 110f, threeStarTime = 80f, rewardClips = 80,
                memo = "MEMO: Two of our couriers (blue) are flying the morning coffee orders from the machine to the door. Rivals will hunt them. " +
                       "Stay close and shoot whatever gets on their tail. We can lose one, not two."
            },
            new CampaignNight
            {
                title = "The Auditor", mode = GameMode.BossFight, room = 1,
                place = "Break room: under the ceiling", twoStarTime = 170f, threeStarTime = 120f, rewardClips = 160,
                memo = "MEMO: Accounting folded a bomber out of a whole ledger and it's circling the break room. Shoot off its wing turrets, dodge its " +
                       "clip bombs, and when its engine stalls get on its tail: the glowing engine is the weak spot."
            },

            // ---------------- Act 3: the server room ----------------
            new CampaignNight
            {
                title = "Cold Boot", mode = GameMode.Squadron, room = 2, killTarget = 8,
                place = "Server room: the aisles", twoStarTime = 150f, threeStarTime = 105f, rewardClips = 90,
                memo = "MEMO: Two Marketing wingmen (blue) fly with you tonight against a whole squadron. Eight kills between you. " +
                       "The tape robot rides its gantry overhead and the halon alarm slams everyone down: listen for the siren."
            },
            new CampaignNight
            {
                title = "Sysadmin", mode = GameMode.Dogfight, objective = NightObjective.Ace, room = 2,
                aceName = "The Sysadmin", killTarget = 2, maxRivals = 1,
                place = "Server room: lights out", twoStarTime = 120f, threeStarTime = 80f, rewardClips = 100,
                memo = "MEMO: IT's best pilot (gold trail) is hunting you: tougher paper, sharper turns, longer bursts. Shoot it down twice. " +
                       "The lights keep cutting out; the racks still glow, and so does its trail."
            },
            new CampaignNight
            {
                title = "The Mainframe", mode = GameMode.BossFight, room = 2,
                place = "Server room: the Mainframe's wall", twoStarTime = 170f, threeStarTime = 120f, rewardClips = 200,
                memo = "MEMO: The Mainframe has joined Accounting. Shoot off its two fans and its sweep loses its bite. When it raises a firewall, " +
                       "shoot down its drones. When it overheats, ride its hot exhaust up and shoot the hatch."
            },

            // ---------------- Act 4: up and out ----------------
            new CampaignNight
            {
                title = "Fire Escape", mode = GameMode.Dogfight, objective = NightObjective.Intercept, room = 3,
                group = "StairCouriers", killTarget = 5, allowedLosses = 3, maxRivals = 1,
                place = "Stairwell: the chimney", twoStarTime = 110f, threeStarTime = 75f, rewardClips = 110,
                memo = "MEMO: Accounting is running its files up the stairwell to the roof. The couriers spiral up the warm chimney: climb with them " +
                       "and shoot down five before three reach the top. The doors on every floor gust open: they shove you, and them."
            },
            new CampaignNight
            {
                title = "Squall", mode = GameMode.Dogfight, room = 4, spawn = "WaterTowerSpawn", killTarget = 5,
                place = "Rooftop: the water tower", twoStarTime = 130f, threeStarTime = 90f, rewardClips = 120,
                memo = "MEMO: Accounting's last squadron is up on the roof. Five kills in the wind. Their planes drift with the gusts " +
                       "just like yours: lead your shots downwind."
            },
            new CampaignNight
            {
                title = "The Condenser", mode = GameMode.BossFight, room = 4,
                place = "Rooftop: the AC unit", twoStarTime = 200f, threeStarTime = 140f, rewardClips = 300,
                memo = "FINAL MEMO: The building's giant AC unit is Accounting's last weapon. Its fan blows you sky-high and throws rings of debris, " +
                       "then reverses and sucks you into the grille. Shoot off its coolant tanks, hit the hub when the fan stalls. End the Night Shift War."
            },
        };

        /// <summary>Kept for old builders (Stage 21): the break-room nights are part of <see cref="NightShift"/> now.</summary>
        public static CampaignNight[] OvertimeNights() => new CampaignNight[0];

        public int Count => nights != null ? nights.Length : 0;
        public CampaignNight Get(int i) => nights != null && i >= 0 && i < nights.Length ? nights[i] : null;
    }
}
