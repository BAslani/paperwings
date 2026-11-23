using System;
using UnityEngine;

namespace PaperWings.Gameplay
{
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
        [Tooltip("Which mode this night is built on. Free Flight nights are won by landing on the mug.")]
        public GameMode mode = GameMode.RingRun;

        [Header("Twist (0 / off = the mode's normal rules)")]
        [Tooltip("Dogfight / Squadron: kills needed. 0 = the mode's usual target.")]
        public int killTarget = 0;
        [Tooltip("Dogfight / Squadron: how many of the regular rival spawners are active. 0 = all.")]
        public int maxRivals = 0;
        [Tooltip("Fail the night if the clock passes this (seconds). 0 = no limit.")]
        public float timeLimit = 0f;
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
                GameMode.FreeFlight => "Land on the coffee mug.",
                GameMode.RingRun => "Fly through every ring, then land on the mug.",
                GameMode.Dogfight => $"Shoot down {(killTarget > 0 ? killTarget : defaultKillTarget)} rivals.",
                GameMode.Squadron => $"Your squadron needs {(killTarget > 0 ? killTarget : defaultSquadronTarget)} kills.",
                _ => "Defeat THE STAPLER."
            };
            if (timeLimit > 0f) g += $" Under {timeLimit:0}s.";
            if (noPencils) g += " No pencils.";
            if (windScale > 1.05f) g += " Rough air.";
            return g;
        }
    }

    /// <summary>
    /// The Night Shift campaign: an ordered list of nights. PaperWings > Settings > Campaign.
    /// Add, reorder or retune nights here. Saved stars are stored per night index, so
    /// inserting a night in the middle shifts which saved stars belong to which night.
    /// </summary>
    [CreateAssetMenu(fileName = "Campaign", menuName = "PaperWings/Campaign Settings")]
    public class CampaignSettings : ScriptableObject
    {
        [Tooltip("Extra paper clips for each NEW star beyond the first (2nd star, 3rd star).")]
        public int[] starBonus = { 15, 30 };

        public CampaignNight[] nights =
        {
            new CampaignNight
            {
                title = "First Flight", mode = GameMode.FreeFlight, rewardClips = 30,
                twoStarTime = 20f, threeStarTime = 12f,
                memo = "MEMO from Marketing: Welcome to the night shift, rookie. Hold Throw to wind up, let go to fly. " +
                       "Dive for speed, climb to slow down. Bring it down gently on the coffee mug."
            },
            new CampaignNight
            {
                title = "The Ring Route", mode = GameMode.RingRun, rewardClips = 40,
                twoStarTime = 70f, threeStarTime = 50f,
                memo = "MEMO: The sticky-note rings mark our patrol route. Ride the laptop's heat to climb, " +
                       "boost when you need speed, and finish on the mug."
            },
            new CampaignNight
            {
                title = "Trespasser", mode = GameMode.Dogfight, killTarget = 2, maxRivals = 1,
                twoStarTime = 60f, threeStarTime = 35f,
                rewardClips = 20, rewardUnlockId = "dart:" + UnlockIds.StapleMagazine,
                memo = "MEMO: One of Accounting's planes is snooping round our desks. It keeps coming back for more, " +
                       "so shoot it down twice. Supply is sending your Dart a bigger staple magazine."
            },
            new CampaignNight
            {
                title = "Rough Air", mode = GameMode.RingRun, windScale = 1.6f, timeLimit = 120f,
                twoStarTime = 80f, threeStarTime = 60f, rewardClips = 50,
                memo = "MEMO: Someone set every fan and vent to MAX. Fly the ring route anyway. " +
                       "The air pushes harder tonight, so use it, don't fight it."
            },
            new CampaignNight
            {
                title = "Pencil Pushers", mode = GameMode.Dogfight, killTarget = 4,
                twoStarTime = 110f, threeStarTime = 75f,
                rewardClips = 20, rewardUnlockId = "dart:" + UnlockIds.Pencils,
                memo = "MEMO: Accounting is out in force. Down four of them and we'll requisition you " +
                       "a pair of lock-on pencil missiles for your Dart."
            },
            new CampaignNight
            {
                title = "Against the Clock", mode = GameMode.RingRun, timeLimit = 60f,
                twoStarTime = 50f, threeStarTime = 40f,
                rewardClips = 30, rewardUnlockId = UnlockIds.GliderFold,
                memo = "MEMO: The cleaners come at dawn. Run the whole route and land in under a minute. " +
                       "Do it and R&D will teach you the Glider fold."
            },
            new CampaignNight
            {
                title = "Backup", mode = GameMode.Squadron, killTarget = 6,
                twoStarTime = 130f, threeStarTime = 90f, rewardClips = 60,
                memo = "MEMO: You're not alone any more. Two Marketing wingmen fly with you tonight. " +
                       "Together, down six of Accounting's planes."
            },
            new CampaignNight
            {
                title = "Staples Only", mode = GameMode.Dogfight, killTarget = 5, noPencils = true, timeLimit = 160f,
                twoStarTime = 120f, threeStarTime = 85f,
                rewardClips = 30, rewardUnlockId = "dart:" + UnlockIds.HeavyPaper,
                memo = "MEMO: Pencil audit tonight, so they stay in the drawer. Five kills with staples alone, " +
                       "before the clock runs out. Survive it and your Dart gets heavy paper."
            },
            new CampaignNight
            {
                title = "Full Squadron", mode = GameMode.Squadron, killTarget = 10, windScale = 1.3f,
                twoStarTime = 190f, threeStarTime = 130f, rewardClips = 80,
                memo = "MEMO: Accounting's whole squadron is up, and the air is rough. " +
                       "Ten kills between you and your wingmen takes the office back."
            },
            new CampaignNight
            {
                title = "The Stapler", mode = GameMode.BossFight,
                twoStarTime = 150f, threeStarTime = 100f,
                rewardClips = 150, rewardUnlockId = UnlockIds.CanardFold,
                memo = "FINAL MEMO: Accounting's secret weapon has woken up on the mug desk. Dodge its staple fans " +
                       "and slams, and hit the glowing mouth when the jaw opens. End the Night Shift War."
            },
        };

        public int Count => nights != null ? nights.Length : 0;
        public CampaignNight Get(int i) => nights != null && i >= 0 && i < nights.Length ? nights[i] : null;
    }
}
