using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// The player's saved progress: paper clips, unlocks and best medals.
    /// Stored as a small JSON file in Application.persistentDataPath, so it survives between sessions
    /// (dev tools to reset it are under PaperWings > Dev in the Editor).
    ///
    /// UNITY NOTE: persistentDataPath is a per-user folder Unity provides for save data
    /// (on Windows: %USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;). JsonUtility turns a
    /// [Serializable] class into JSON text and back.
    /// </summary>
    public static class Progression
    {
        [Serializable]
        class SaveData
        {
            public int version = 2;   // 2 = upgrades are per fold ("dart:pencils")
            public int paperClips;
            public int totalClipsEarned;
            public List<string> unlocked = new List<string>();
            public List<int> medalModes = new List<int>();   // parallel lists: JsonUtility can't do dictionaries
            public List<int> medals = new List<int>();
            public List<int> nightStars = new List<int>();   // campaign: stars per night index (0 = not cleared)
            public int campaignVersion;                       // which campaign layout the stars belong to
            public List<CampaignSave> campaigns = new List<CampaignSave>();   // Stage 46: other campaigns (Tiny War)
        }

        /// <summary>Stars for a campaign other than the Night Shift (which keeps the original slots).</summary>
        [Serializable]
        class CampaignSave
        {
            public string id;
            public int version;
            public List<int> stars = new List<int>();
        }

        /// <summary>
        /// Which campaign the night functions below talk about (Stage 46): "" = the Night Shift,
        /// "tinywar" = Tiny War. The ModeDirector sets it when a campaign is picked.
        /// </summary>
        public static string Scope { get; set; } = "";

        static List<int> Stars(string scope)
        {
            if (string.IsNullOrEmpty(scope)) return Data.nightStars;
            foreach (var c in Data.campaigns) if (c.id == scope) return c.stars;
            var made = new CampaignSave { id = scope };
            Data.campaigns.Add(made);
            return made.stars;
        }

        static SaveData data;

        /// <summary>Fired whenever progress changes (clips, unlocks, medals).</summary>
        public static event Action Changed;
        /// <summary>Fired when clips are earned. Args: amount, reason (for HUD pops).</summary>
        public static event Action<int, string> ClipsEarned;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "paperwings_save.json");

        static SaveData Data
        {
            get { if (data == null) Load(); return data; }
        }

        public static int PaperClips => Data.paperClips;
        public static bool IsUnlocked(string id) => string.IsNullOrEmpty(id) || Data.unlocked.Contains(id);

        public static int GetMedal(GameMode mode) => GetMedal((int)mode);
        public static bool RecordMedal(GameMode mode, int medal) => RecordMedal((int)mode, medal);

        /// <summary>Medal for a key (ModeDirector.MedalKey: mode + 100 x room).</summary>
        public static int GetMedal(int mode)
        {
            int i = Data.medalModes.IndexOf(mode);
            return i >= 0 ? Data.medals[i] : 0;
        }

        /// <summary>Records a medal if it beats the previous one. Returns true if it's a new best tier.</summary>
        public static bool RecordMedal(int mode, int medal)
        {
            if (medal <= GetMedal(mode)) return false;
            int i = Data.medalModes.IndexOf(mode);
            if (i >= 0) Data.medals[i] = medal;
            else { Data.medalModes.Add(mode); Data.medals.Add(medal); }
            SaveAndNotify();
            return true;
        }

        // ------------------------------------------------------------------ campaign

        /// <summary>Stars earned on a campaign night (0 = not cleared yet).</summary>
        public static int GetNightStars(int night) => GetNightStars(night, Scope);

        public static int GetNightStars(int night, string scope)
        {
            var list = Stars(scope);
            return night >= 0 && night < list.Count ? list[night] : 0;
        }

        public static bool NightCleared(int night) => GetNightStars(night) > 0;
        public static bool NightCleared(int night, string scope) => GetNightStars(night, scope) > 0;

        /// <summary>Night 0 is always open; every other night opens once the one before it is cleared.</summary>
        public static bool NightOpen(int night) => night == 0 || NightCleared(night - 1);

        /// <summary>Records stars if they beat the previous best. Returns true if improved.</summary>
        public static bool RecordNightStars(int night, int stars)
        {
            if (night < 0 || stars <= GetNightStars(night)) return false;
            var list = Stars(Scope);
            while (list.Count <= night) list.Add(0);
            list[night] = stars;
            SaveAndNotify();
            return true;
        }

        public static int TotalStars() => TotalStars(Scope);

        public static int TotalStars(string scope)
        {
            int n = 0;
            foreach (int s in Stars(scope)) n += s;
            return n;
        }

        /// <summary>A campaign other than the Night Shift changed its levels: its stars start over (Stage 46).</summary>
        public static void SyncCampaign(string scope, int version)
        {
            if (string.IsNullOrEmpty(scope)) { SyncCampaign(version); return; }
            Stars(scope);
            foreach (var c in Data.campaigns)
                if (c.id == scope && c.version != version)
                {
                    c.stars.Clear();
                    c.version = version;
                    SaveAndNotify();
                }
        }

        /// <summary>Gives an unlock for free (a campaign reward). False if it was already owned.</summary>
        public static bool GrantUnlock(string id)
        {
            if (IsUnlocked(id)) return false;
            Data.unlocked.Add(id);
            SaveAndNotify();
            return true;
        }

        // ------------------------------------------------------------------ clips and shop

        public static void AddClips(int amount, string reason)
        {
            if (amount <= 0) return;
            Data.paperClips += amount;
            Data.totalClipsEarned += amount;
            SaveAndNotify();
            ClipsEarned?.Invoke(amount, reason);
        }

        public enum BuyResult { Bought, AlreadyOwned, NeedsRequirement, TooExpensive, NeedsBlueprint }

        public static BuyResult CanBuy(UnlockItem item)
        {
            if (IsUnlocked(item.id)) return BuyResult.AlreadyOwned;
            if (item.requiresNight >= 0 && !NightCleared(item.requiresNight, "")) return BuyResult.NeedsBlueprint;   // blueprints come from the Night Shift
            if (!IsUnlocked(item.requires)) return BuyResult.NeedsRequirement;
            if (Data.paperClips < item.cost) return BuyResult.TooExpensive;
            return BuyResult.Bought;
        }

        public static BuyResult TryBuy(UnlockItem item)
        {
            var result = CanBuy(item);
            if (result != BuyResult.Bought) return result;
            Data.paperClips -= item.cost;
            Data.unlocked.Add(item.id);
            SaveAndNotify();
            return BuyResult.Bought;
        }

        // ------------------------------------------------------------------ dev helpers

        /// <summary>
        /// Stars are saved per night index, so when the campaign is rebuilt (new nights, new order)
        /// the old stars would land on the wrong nights. A new campaign version starts the stars over;
        /// paper clips and unlocks are kept.
        /// </summary>
        public static void SyncCampaign(int version)
        {
            if (Data.campaignVersion == version) return;
            if (Data.campaignVersion == 3 && version >= 4)
            {
                // Night Shift v3 -> v4 only dropped the first night (Target Practice is Flight School now):
                // keep everyone's stars, one night earlier.
                if (Data.nightStars.Count > 0) Data.nightStars.RemoveAt(0);
                Data.campaignVersion = 4;
            }
            if (Data.campaignVersion == 4 && version == 5)
            {
                // v4 -> v5 only dropped night 10, Fire Escape (index 9): Squall and The Condenser keep their stars.
                if (Data.nightStars.Count > 9) Data.nightStars.RemoveAt(9);
                Data.campaignVersion = version;
                SaveAndNotify();
                return;
            }
            Data.nightStars.Clear();
            Data.campaignVersion = version;
            SaveAndNotify();
        }

        public static void ResetAll()
        {
            data = new SaveData();
            SaveAndNotify();
        }

        public static void UnlockAll(IEnumerable<string> ids)
        {
            foreach (var id in ids) if (!Data.unlocked.Contains(id)) Data.unlocked.Add(id);
            SaveAndNotify();
        }

        /// <summary>Marks the first <paramref name="count"/> nights cleared (1 star) so every night is open.</summary>
        public static void ClearNights(int count)
        {
            var list = Stars(Scope);
            while (list.Count < count) list.Add(0);
            for (int i = 0; i < count; i++) list[i] = Mathf.Max(1, list[i]);
            SaveAndNotify();
        }

        // ------------------------------------------------------------------ file

        public static void Load()
        {
            try
            {
                data = File.Exists(SavePath) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Progression: couldn't read save ({e.Message}); starting fresh.");
                data = null;
            }
            data ??= new SaveData();
            data.nightStars ??= new List<int>();   // saves from before the campaign existed
            data.unlocked ??= new List<string>();
            data.campaigns ??= new List<CampaignSave>();
            if (data.version < 2) MigrateToPerFoldUpgrades();
        }

        /// <summary>
        /// Version 1 saves had one set of upgrades for every fold. They become the Dart's upgrades
        /// (the fold everyone flew), and the old Canard-only propeller stays on the Canard.
        /// </summary>
        static void MigrateToPerFoldUpgrades()
        {
            for (int i = 0; i < data.unlocked.Count; i++)
            {
                string id = data.unlocked[i];
                if (Array.IndexOf(UnlockIds.LegacyUpgrades, id) < 0) continue;
                data.unlocked[i] = UnlockIds.ForFold(id == UnlockIds.Propeller ? "canard" : "dart", id);
            }
            data.version = 2;
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); } catch (Exception) { }
        }

        static void SaveAndNotify()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); }
            catch (Exception e) { Debug.LogWarning($"Progression: couldn't write save ({e.Message})."); }
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            data = null;   // re-read from disk each Play session
            Scope = "";
            Changed = null;
            ClipsEarned = null;
        }
    }
}
