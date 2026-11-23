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
            public int version = 1;
            public int paperClips;
            public int totalClipsEarned;
            public List<string> unlocked = new List<string>();
            public List<int> medalModes = new List<int>();   // parallel lists: JsonUtility can't do dictionaries
            public List<int> medals = new List<int>();
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

        public static int GetMedal(GameMode mode)
        {
            int i = Data.medalModes.IndexOf((int)mode);
            return i >= 0 ? Data.medals[i] : 0;
        }

        /// <summary>Records a medal if it beats the previous one. Returns true if it's a new best tier.</summary>
        public static bool RecordMedal(GameMode mode, int medal)
        {
            if (medal <= GetMedal(mode)) return false;
            int i = Data.medalModes.IndexOf((int)mode);
            if (i >= 0) Data.medals[i] = medal;
            else { Data.medalModes.Add((int)mode); Data.medals.Add(medal); }
            SaveAndNotify();
            return true;
        }

        public static void AddClips(int amount, string reason)
        {
            if (amount <= 0) return;
            Data.paperClips += amount;
            Data.totalClipsEarned += amount;
            SaveAndNotify();
            ClipsEarned?.Invoke(amount, reason);
        }

        public enum BuyResult { Bought, AlreadyOwned, NeedsRequirement, TooExpensive }

        public static BuyResult CanBuy(UnlockItem item)
        {
            if (IsUnlocked(item.id)) return BuyResult.AlreadyOwned;
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
            Changed = null;
            ClipsEarned = null;
        }
    }
}
