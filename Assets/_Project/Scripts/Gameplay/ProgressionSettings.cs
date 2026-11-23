using System;
using System.Collections.Generic;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// The ids that code checks for.
    /// Fold unlocks are global ("fold_glider"). Upgrades belong to one fold and are written
    /// "foldId:upgrade", e.g. "dart:pencils" or "canard:propeller": buying pencils for the Dart
    /// doesn't give them to the Glider.
    /// </summary>
    public static class UnlockIds
    {
        // Per-fold upgrade keys (combine with ForFold).
        public const string Pencils = "pencils";
        public const string PencilRack = "pencils_plus";
        public const string StapleMagazine = "staples_plus";
        public const string HeavyPaper = "paper_tough";
        public const string Propeller = "propeller";

        // Global unlocks.
        public const string GliderFold = "fold_glider";
        public const string StuntFold = "fold_stunt";
        public const string CanardFold = "fold_canard";
        public const string Staples = "staples";   // only used if staples are set to be an unlock

        public static string ForFold(string foldKey, string upgrade) => $"{foldKey}:{upgrade}";
        public static string ForFold(PlaneFold fold, string upgrade) => ForFold(fold != null ? fold.Key : "dart", upgrade);

        /// <summary>The upgrade key's ids from before upgrades were per fold (save migration).</summary>
        public static readonly string[] LegacyUpgrades = { Pencils, PencilRack, StapleMagazine, HeavyPaper, Propeller };
    }

    [Serializable]
    public class UnlockItem
    {
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string description;
        [Tooltip("Paper clips to buy it.")]
        public int cost = 50;
        [Tooltip("Another unlock that must be owned first (empty = none). In the fold upgrade list this is another upgrade key.")]
        public string requires = "";

        /// <summary>Which fold a per-fold upgrade belongs to (empty for global unlocks). Filled in at runtime.</summary>
        [NonSerialized] public string foldName = "";
        /// <summary>"Pencil Missiles (Glider)" for upgrades, just the name for everything else.</summary>
        public string LongName => string.IsNullOrEmpty(foldName) ? displayName : $"{displayName} ({foldName})";
    }

    /// <summary>
    /// Everything about progression in one asset: the folds you can unlock, the upgrades every
    /// fold can buy (each fold separately), what you earn, and the medal times.
    /// PaperWings > Settings > Progression.
    /// </summary>
    [CreateAssetMenu(fileName = "Progression", menuName = "PaperWings/Progression Settings")]
    public class ProgressionSettings : ScriptableObject
    {
        [Tooltip("All folds, in the order their tabs appear in the Unlocks screen (filled in by Build Stage 19).")]
        public PlaneFold[] folds;

        [Header("Fold unlocks (ids must match each fold's Unlock Id)")]
        public UnlockItem[] foldUnlocks =
        {
            new UnlockItem { id = UnlockIds.GliderFold, displayName = "Glider Fold", cost = 80,
                description = "Wide wings that ride thermals and glide forever." },
            new UnlockItem { id = UnlockIds.StuntFold, displayName = "Stunt Fold", cost = 100,
                description = "A snappy delta for tight turns and big dives." },
            new UnlockItem { id = UnlockIds.CanardFold, displayName = "Canard Fold", cost = 150,
                description = "Little nose wings: glides far and climbs easily." },
        };

        [Header("Upgrades every fold can buy (each fold separately)")]
        public UnlockItem[] foldUpgrades =
        {
            new UnlockItem { id = UnlockIds.Pencils, displayName = "Pencil Missiles", cost = 60,
                description = "Two lock-on homing pencils under the wings." },
            new UnlockItem { id = UnlockIds.PencilRack, displayName = "Pencil Rack", cost = 120, requires = UnlockIds.Pencils,
                description = "Carry four pencils instead of two." },
            new UnlockItem { id = UnlockIds.StapleMagazine, displayName = "Staple Magazine", cost = 50,
                description = "+15 staples per throw." },
            new UnlockItem { id = UnlockIds.HeavyPaper, displayName = "Heavy Paper", cost = 70,
                description = "+25% paper integrity: more bumps and hits before you crumple." },
            new UnlockItem { id = UnlockIds.Propeller, displayName = "Pusher Propeller", cost = 90,
                description = "A spinning prop on the tail: boosting lifts you up as well as speeding you up." },
        };

        [Header("Starting loadout")]
        [Tooltip("If ON, the staple gun must be unlocked too (once, for every fold). Default: you start with it.")]
        public bool staplesAreAnUnlock = false;
        public int staplesCost = 40;

        [Header("Earning paper clips")]
        public int clipsPerKill = 3;
        [Tooltip("For finishing a Ring Run, Dogfight, Squadron Battle or Boss Fight.")]
        public int clipsPerCompletedRun = 20;
        [Tooltip("Extra clips for a NEW best medal in a mode (bronze, silver, gold). Paid once per medal tier.")]
        public int[] medalBonus = { 15, 30, 60 };

        [Header("Medal times (seconds; beat them for bronze / silver / gold)")]
        public Vector3 ringRunMedals = new Vector3(60f, 45f, 32f);
        public Vector3 dogfightMedals = new Vector3(140f, 100f, 70f);
        public Vector3 squadronMedals = new Vector3(200f, 140f, 100f);
        public Vector3 bossMedals = new Vector3(180f, 130f, 90f);

        UnlockItem StaplesItem => new UnlockItem
        {
            id = UnlockIds.Staples, displayName = "Staple Gun", cost = staplesCost,
            description = "A nose-mounted staple gun for every fold. Hold fire to shoot."
        };

        /// <summary>One fold's copy of an upgrade: its own id, and it needs the fold (or the fold's copy of the prerequisite).</summary>
        public UnlockItem UpgradeFor(PlaneFold fold, UnlockItem template) => new UnlockItem
        {
            id = UnlockIds.ForFold(fold, template.id),
            displayName = template.displayName,
            description = template.description,
            cost = template.cost,
            requires = string.IsNullOrEmpty(template.requires) ? fold.unlockId : UnlockIds.ForFold(fold, template.requires),
            foldName = fold.displayName
        };

        /// <summary>
        /// The Unlocks screen rows for one fold tab: the staple gun (if it's an unlock), the fold
        /// itself (if it has to be unlocked), then that fold's upgrades.
        /// </summary>
        public List<UnlockItem> ShopFor(PlaneFold fold)
        {
            var rows = new List<UnlockItem>();
            if (staplesAreAnUnlock) rows.Add(StaplesItem);
            if (fold == null) return rows;
            if (!string.IsNullOrEmpty(fold.unlockId))
            {
                var foldItem = Array.Find(foldUnlocks, u => u.id == fold.unlockId);
                rows.Add(foldItem ?? new UnlockItem { id = fold.unlockId, displayName = fold.displayName + " Fold", cost = 100, description = fold.description });
            }
            foreach (var up in foldUpgrades) rows.Add(UpgradeFor(fold, up));
            return rows;
        }

        /// <summary>Every unlock there is (staple gun, folds, and every fold's upgrades).</summary>
        public IEnumerable<UnlockItem> AllItems()
        {
            if (staplesAreAnUnlock) yield return StaplesItem;
            foreach (var f in foldUnlocks) yield return f;
            if (folds == null) yield break;
            foreach (var fold in folds)
                if (fold != null)
                    foreach (var up in foldUpgrades) yield return UpgradeFor(fold, up);
        }

        public UnlockItem Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var item in AllItems()) if (item.id == id) return item;
            return null;
        }

        /// <summary>0 = none, 1 = bronze, 2 = silver, 3 = gold.</summary>
        public int MedalFor(GameMode mode, float time) => MedalFor(mode, time, Vector3.zero);

        /// <summary>Same, with room-specific times (zero = the defaults above).</summary>
        public int MedalFor(GameMode mode, float time, Vector3 overrideTimes)
        {
            Vector3 t = mode switch
            {
                GameMode.RingRun => ringRunMedals,
                GameMode.Dogfight => dogfightMedals,
                GameMode.Squadron => squadronMedals,
                GameMode.BossFight => bossMedals,
                _ => Vector3.zero
            };
            if (overrideTimes != Vector3.zero) t = overrideTimes;
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
