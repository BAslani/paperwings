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
        [Tooltip("Which room: 0 office, 1 break room, 2 server room, (3 was the stairwell, removed), 4 rooftop, 5 kitchen (Tiny War).")]
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
        [Tooltip("Fly tonight's rivals with these pilot settings (e.g. Pilot_Trainee for an early night). Empty = their usual pilot.")]
        public PaperWings.Combat.RivalPilotSettings rivalPilot;

        [Header("Stars (1 for clearing, 2 and 3 for speed)")]
        [Tooltip("Clear at or under this time for 2 stars. 0 = no time stars.")]
        public float twoStarTime = 0f;
        [Tooltip("Clear at or under this time for 3 stars.")]
        public float threeStarTime = 0f;

        [Header("Story (Stage 46, Tiny War)")]
        [Tooltip("When it happens, stamped on the newsreel card and the briefing (e.g. 'FRIDAY · 1900').")]
        public string when = "";
        [Tooltip("Grandad's newsreel narration, shown on a black-and-white title card before the level. Empty = no card.")]
        [TextArea(2, 5)] public string newsreel = "";
        [Tooltip("Biscuit the cat starts this level awake (else asleep on her nap spot).")]
        public bool catAwake = false;

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
                    NightObjective.Escort => Target(1) > 1 ? $"Escort: {Target(1)} couriers must get through. Lose {allowedLosses} and it's over." : $"Keep your courier alive. Lose {allowedLosses} and it's over.",
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
        public const int CurrentVersion = 5;   // 5: Fire Escape removed (Stage 36)

        [Header("Which campaign (Stage 46)")]
        [Tooltip("Save id: empty for the Night Shift (its stars use the original save slots), e.g. 'tinywar' for others.")]
        public string id = "";
        public string displayName = "Night Shift";
        [Tooltip("One line under its name on the main menu.")]
        public string tagline = "Climb the office building, one night at a time.";
        [Tooltip("Heading of the roster clipboard.")]
        public string rosterTitle = "THE NIGHT SHIFT · ROSTER";
        [Tooltip("What one mission is called: 'Night' or 'Level'.")]
        public string levelWord = "Night";
        [Tooltip("What the briefing sheet is called: 'MEMO' or 'WAR DIARY'.")]
        public string briefingWord = "MEMO";
        [Tooltip("Level slots the story has but that aren't built yet (shown as 'coming soon' rows).")]
        public string[] comingSoon = new string[0];
        [Tooltip("Stamped on the results when its last level is cleared for the first time.")]
        public string completeLine = "THE NIGHT SHIFT WAR IS WON!";

        [Tooltip("Extra paper clips for each NEW star beyond the first (2nd star, 3rd star).")]
        public int[] starBonus = { 10, 25 };

        public CampaignNight[] nights = NightShift();

        /// <summary>
        /// The Night Shift (campaign version 5, Stage 36): eleven nights of air combat climbing the
        /// building (target practice moved to Flight School). No two nights in a room ask for the same thing:
        ///   Office      a lone trespasser, intercept the couriers, The Printer
        ///   Break room  a dogfight at the office party, escort the coffee courier, The Auditor (it flies)
        ///   Server room a squadron battle, an ace duel in the dark, The Mainframe
        ///   Rooftop     a dogfight in the wind, The Condenser
        /// Nights pay paper clips and unlock BLUEPRINTS (what the hangar is allowed to sell); the
        /// upgrades themselves are bought. Rooms: 0 office, 1 break room, 2 server room, (3: removed stairwell), 4 rooftop.
        /// </summary>
        public static CampaignNight[] NightShift() => new[]
        {
            // ---------------- Act 1: the office ----------------
            new CampaignNight
            {
                title = "Trespasser", mode = GameMode.Dogfight, room = 0, spawn = "CoworkerSpawn",
                place = "Office: the coworker's corner", killTarget = 2, maxRivals = 1,
                twoStarTime = 60f, threeStarTime = 35f, rewardClips = 60,
                memo = "MEMO from Marketing: Welcome to the night shift, rookie. An Accounting plane (red trail) is snooping round the office. " +
                       "It keeps coming back, so shoot it down twice. New here? Fly FLIGHT SCHOOL from the main menu first. " +
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
                title = "The Printer", mode = GameMode.BossFight, room = 0,
                place = "Office: the printer corner", twoStarTime = 170f, threeStarTime = 115f, rewardClips = 140,
                memo = "MEMO: The big printer in the corner has gone rogue: it's PRINTING Accounting fighters that hunt you, and firing paper at you. " +
                       "While its print job is out it's shielded: shoot the fighters down. Shoot off its paper trays and it prints fewer. " +
                       "When it jams, its lid pops open: ride the heat up and shoot the glowing fuser inside."
            },

            // ---------------- Act 2: the break room ----------------
            new CampaignNight
            {
                title = "Office Party", mode = GameMode.Dogfight, room = 1, spawn = "CouchSpawn", killTarget = 3, maxRivals = 2,
                place = "Break room: the party", twoStarTime = 120f, threeStarTime = 85f, rewardClips = 70,
                memo = "MEMO: Somebody's leaving do, and Accounting crashed it with a pair of trainees. Three kills among the balloons (shoot them to clear a lane). " +
                       "Watch the appliances: the microwave dumps heat when it dings, the fridge door swings open, and the vending machine has gone haywire."
            },
            new CampaignNight
            {
                title = "Coffee Run", mode = GameMode.Dogfight, objective = NightObjective.Escort, room = 1,
                group = "CoffeeCourier", killTarget = 2, allowedLosses = 2, maxRivals = 1,
                place = "Break room: the coffee machine", twoStarTime = 130f, threeStarTime = 95f, rewardClips = 80,
                memo = "MEMO: Two of our couriers (blue) are flying the morning coffee orders from the machine to the door. A rookie rival will hunt them. " +
                       "Stay close and shoot whatever gets on their tail. We can lose one, not two."
            },
            new CampaignNight
            {
                title = "The Auditor", mode = GameMode.BossFight, room = 1,
                place = "Break room: under the ceiling", twoStarTime = 200f, threeStarTime = 140f, rewardClips = 160,
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

            // ---------------- Act 4: the roof ----------------
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

        /// <summary>
        /// Tiny War (Stage 46): a war film in a house over one weekend. Theo's Blue Wing (you) against
        /// Maya's Red Squadron and Sam's Green Gang, narrated by Grandad like a 1940s newsreel. Acts 1 and 2 are
        /// built (Friday night in the kitchen, Saturday morning in the bathroom); the rest are in <see cref="comingSoon"/>.
        /// Room 5 is the kitchen, room 6 the bathroom.
        /// </summary>
        public static CampaignNight[] TinyWar() => new[]
        {
            new CampaignNight
            {
                title = "Ration Run", mode = GameMode.Dogfight, objective = NightObjective.Escort, room = 5,
                group = "CookieRun", killTarget = 2, allowedLosses = 2, maxRivals = 1, spawn = "FridgeSpawn",
                place = "Kitchen: the cookie jar to the fort", when = "FRIDAY · 1900",
                twoStarTime = 120f, threeStarTime = 85f, rewardClips = 70,
                memo = "WAR DIARY, Friday. Mum and Dad are gone till Sunday. Grandad is in charge (asleep in his chair). " +
                       "The fort under the table needs supplies. Three of my couriers are flying cookies from the jar to the fort, one at a time. " +
                       "Maya's Red raiders will try to shoot them down. Stay close. Two have to get through. We can lose one cookie, NOT two. " +
                       "Biscuit is asleep on the window seat. Do NOT wake her up.",
                newsreel = "Friday night. The grown-ups have gone. The house falls silent... " +
                           "and in the kitchen, the Blue Wing prepares for war.\n\n" +
                           "Supplies must get through to the fort. Every cookie counts."
            },
            new CampaignNight
            {
                title = "Battle of the Kitchen", mode = GameMode.Dogfight, room = 5, killTarget = 4, maxRivals = 2, spawn = "TableSpawn",
                place = "Kitchen: over the counter", when = "FRIDAY · 2030", catAwake = true,
                twoStarTime = 110f, threeStarTime = 75f, rewardClips = 90,
                memo = "WAR DIARY, Friday, later. Maya wants the kitchen. She's sent her whole Red Squadron in. " +
                       "Shoot down four. Hide behind the cereal boxes, climb in the toaster heat and the kettle steam. " +
                       "Biscuit is AWAKE and in a playful mood. She goes for anything flying low, Red or Blue. Stay high.",
                newsreel = "Twenty-thirty hours. Red Squadron crosses the counter in force.\n\n" +
                           "Above the toaster, the skies fill with paper. Below, a cat watches. " +
                           "She has chosen no side. She never will."
            },
            new CampaignNight
            {
                title = "The Dreadnought", mode = GameMode.BossFight, room = 5, spawn = "TableSpawn",
                place = "Kitchen: the floor", when = "FRIDAY · 2200",
                twoStarTime = 190f, threeStarTime = 130f, rewardClips = 200,
                memo = "WAR DIARY, Friday, 10 o'clock. The robot vacuum WOKE UP. It's on a timer, and Maya taped a cardboard " +
                       "turret on it. Now it patrols the floor sucking in anything low and firing crumbs. We call it THE DREADNOUGHT. " +
                       "Shoot off its side brushes so it can't suck as hard. When its battery runs low it goes home to charge: " +
                       "its dust bin pops open. HIT THE DUST BIN. Biscuit is terrified of it. So am I.",
                newsreel = "Twenty-two hundred hours. Without warning, the Dreadnought leaves its dock.\n\n" +
                           "It answers to no one. It devours everything in its path. " +
                           "Tonight, the kitchen floor belongs to the machine."
            },

            // ---------------- Act 2: the Waterworks (Saturday morning, the bathroom, room 6) ----------------
            new CampaignNight
            {
                title = "Bathtub Landing Day", mode = GameMode.RingRun, room = 6,
                place = "Bathroom: the beachhead", when = "SATURDAY · 0700",
                twoStarTime = 60f, threeStarTime = 44f, rewardClips = 90,
                memo = "WAR DIARY, Saturday, early. Sam's Green Gang took the bathroom while we slept. Today we land. " +
                       "Fly the rings: through the shower steam, over the bath and the rubber-duck navy, up the extractor fan " +
                       "and round the hot towel rail. Steam lifts you but makes your paper soggy. The fan sucks you UP: use it.",
                newsreel = "Saturday, oh-seven-hundred hours. Fog over the bathtub.\n\n" +
                           "Somewhere in the steam, the rubber-duck navy lies in wait. " +
                           "The Blue Wing goes in at dawn."
            },
            new CampaignNight
            {
                title = "Soap Convoy", mode = GameMode.Dogfight, objective = NightObjective.Intercept, room = 6,
                group = "SoapConvoy", killTarget = 5, allowedLosses = 3, maxRivals = 1,
                place = "Bathroom: the towel rail", when = "SATURDAY · 0830",
                twoStarTime = 100f, threeStarTime = 70f, rewardClips = 110,
                memo = "WAR DIARY, Saturday, breakfast. Sam's couriers (green) are flying soap-bar ammo from the bath along the towel rail " +
                       "to his toilet-roll castle on the sink. Shoot down five before three get through. One of his crayon planes flies cover. " +
                       "Mind the taps: they DRIP, and wet paper flies like a wet sock.",
                newsreel = "Oh-eight-thirty. A supply line runs along the towel rail.\n\n" +
                           "Soap. Enough soap to hold the bathroom for a week. " +
                           "It must not reach the castle."
            },
            new CampaignNight
            {
                title = "The Typhoon", mode = GameMode.BossFight, room = 6,
                place = "Bathroom: the floor", when = "SATURDAY · 1000",
                twoStarTime = 190f, threeStarTime = 130f, rewardClips = 220,
                memo = "WAR DIARY, Saturday, 10 o'clock. Sam has a SECRET WEAPON. He taped Mum's hairdryer to his monster truck. " +
                       "Hot blasts throw you up, cold blasts shove you back, and it fires soap bubbles. Shoot the tape straps off so it " +
                       "can't aim. When it overheats it cuts out: shoot the SWITCH on the handle. Don't tell Mum.",
                newsreel = "Ten hundred hours. A roar from the bathroom.\n\n" +
                           "The Green Gang has unleashed the Typhoon: hot wind, cold wind, and bubbles. " +
                           "Nothing can stand in its path. The Blue Wing must try."
            },
        };

        /// <summary>Tiny War's levels after Act 2, shown as 'coming soon' on the roster.</summary>
        public static string[] TinyWarComingSoon() => new[]
        {
            "Lights Out", "Pillow Fort", "The Top Bunk",
            "Ceasefire", "The Cleanup", "The Kaiju"
        };

        /// <summary>Kept for old builders (Stage 21): the break-room nights are part of <see cref="NightShift"/> now.</summary>
        public static CampaignNight[] OvertimeNights() => new CampaignNight[0];

        public int Count => nights != null ? nights.Length : 0;
        public CampaignNight Get(int i) => nights != null && i >= 0 && i < nights.Length ? nights[i] : null;
    }
}
