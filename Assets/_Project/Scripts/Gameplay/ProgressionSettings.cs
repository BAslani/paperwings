using System;
using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>The ids that code checks for. Unlock entries in the settings asset use these.</summary>
    public static class UnlockIds
    {
        public const string Pencils = "pencils";
        public const string PencilRack = "pencils_plus";
        public const string StapleMagazine = "staples_plus";
        public const string HeavyPaper = "paper_tough";
        public const string GliderFold = "fold_glider";
        public const string StuntFold = "fold_stunt";
        public const string CanardFold = "fold_canard";
        public const string Propeller = "propeller";
        public const string Staples = "staples";   // only used if staples are set to be an unlock
    }

    [Serializable]
    public class UnlockItem
    {
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string description;
        [Tooltip("Paper clips to buy it.")]
        public int cost = 50;
        [Tooltip("Another unlock id that must be owned first (empty = none).")]
        public string requires = "";
    }

    /// <summary>
    /// Everything about progression in one asset: the unlock shop, what you earn, and the medal times.
    /// PaperWings > Settings > Progression.
    /// </summary>
    [CreateAssetMenu(fileName = "Progression", menuName = "PaperWings/Progression Settings")]
    public class ProgressionSettings : ScriptableObject
    {
        [Header("Unlock shop (in order shown)")]
        public UnlockItem[] unlocks =
        {
            new UnlockItem { id = UnlockIds.Pencils, displayName = "Pencil Missiles", cost = 60,
                description = "Two lock-on homing pencils under your wings." },
            new UnlockItem { id = UnlockIds.PencilRack, displayName = "Pencil Rack", cost = 120, requires = UnlockIds.Pencils,
                description = "Carry four pencils instead of two." },
            new UnlockItem { id = UnlockIds.StapleMagazine, displayName = "Staple Magazine", cost = 50,
                description = "+15 staples per throw." },
            new UnlockItem { id = UnlockIds.HeavyPaper, displayName = "Heavy Paper", cost = 70,
                description = "+25% paper integrity: more bumps and hits before you crumple." },
            new UnlockItem { id = UnlockIds.GliderFold, displayName = "Glider Fold", cost = 80,
                description = "Wide wings that ride thermals and glide forever." },
            new UnlockItem { id = UnlockIds.StuntFold, displayName = "Stunt Fold", cost = 100,
                description = "A snappy delta for tight turns and big dives." },
            new UnlockItem { id = UnlockIds.CanardFold, displayName = "Canard Fold", cost = 150,
                description = "Little nose wings: glides far and climbs easily." },
            new UnlockItem { id = UnlockIds.Propeller, displayName = "Pusher Propeller", cost = 90, requires = UnlockIds.CanardFold,
                description = "A spinning prop for the Canard: its boost lifts you up." },
        };

        [Header("Starting loadout")]
        [Tooltip("If ON, the staple gun must be unlocked too (and is added to the shop automatically). Default: you start with it.")]
        public bool staplesAreAnUnlock = false;
        public int staplesCost = 40;

        [Header("Earning paper clips")]
        public int clipsPerKill = 3;
        public int clipsPerMugLanding = 5;
        [Tooltip("For finishing a Ring Run, Dogfight, Squadron Battle or Boss Fight.")]
        public int clipsPerCompletedRun = 20;
        [Tooltip("Extra clips for a NEW best medal in a mode (bronze, silver, gold). Paid once per medal tier.")]
        public int[] medalBonus = { 15, 30, 60 };

        [Header("Medal times (seconds; beat them for bronze / silver / gold)")]
        public Vector3 ringRunMedals = new Vector3(75f, 55f, 40f);
        public Vector3 dogfightMedals = new Vector3(140f, 100f, 70f);
        public Vector3 squadronMedals = new Vector3(200f, 140f, 100f);
        public Vector3 bossMedals = new Vector3(180f, 130f, 90f);

        /// <summary>The shop as shown in the menu (adds the staple gun first if it's set to be an unlock).</summary>
        public UnlockItem[] Shop()
        {
            if (!staplesAreAnUnlock) return unlocks;
            var list = new System.Collections.Generic.List<UnlockItem>
            {
                new UnlockItem { id = UnlockIds.Staples, displayName = "Staple Gun", cost = staplesCost,
                                 description = "A nose-mounted staple gun. Hold fire to shoot." }
            };
            list.AddRange(unlocks);
            return list.ToArray();
        }

        public UnlockItem Find(string id) => Array.Find(Shop(), u => u.id == id);

        /// <summary>0 = none, 1 = bronze, 2 = silver, 3 = gold.</summary>
        public int MedalFor(GameMode mode, float time)
        {
            Vector3 t = mode switch
            {
                GameMode.RingRun => ringRunMedals,
                GameMode.Dogfight => dogfightMedals,
                GameMode.Squadron => squadronMedals,
                GameMode.BossFight => bossMedals,
                _ => Vector3.zero
            };
            if (t == Vector3.zero) return 0;
            if (time <= t.z) return 3;
            if (time <= t.y) return 2;
            if (time <= t.x) return 1;
            return 0;
        }

        public static string MedalName(int medal) => medal switch { 1 => "Bronze", 2 => "Silver", 3 => "Gold", _ => "" };
        public static string MedalColor(int medal) => medal switch { 1 => "#D08A50", 2 => "#C8D0D8", 3 => "#FFD040", _ => "#888888" };
    }
}
