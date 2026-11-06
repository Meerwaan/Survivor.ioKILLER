using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>Totals over every run ever played (achievements and the profile read them).</summary>
    [Serializable]
    public class LifetimeStats
    {
        public int runs;
        public int kills;
        public int bosses;
        public int elites;
        public int evolutions;
        public int bestSeconds;
        public int bestCombo;
        public int bestLevel;
        /// <summary>Unused since gear was removed (v2), kept so old saves load cleanly.</summary>
        public int itemsFound;
        public int harvests;
        public int stageWins;
        public long goldEarned;
    }

    [Serializable]
    public class BuildingSave
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class AchievementSave
    {
        public string id;
        public int claimedTiers;
    }

    /// <summary>
    /// Everything the long-term meta needs (campaign, camp, missions), as one JSON blob.
    /// Gold and the starting weapon stay in their own old PlayerPrefs keys (see Progression).
    /// v2: gear items, scrap and training were removed (fair play); their old fields are simply ignored on load.
    /// </summary>
    [Serializable]
    public class MetaData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;

        // Campaign: one star bitmask per stage (bit 0 = cleared, bit 1 = health star, bit 2 = boss star).
        public List<int> stageStars = new();
        public int selectedStage;

        // Camp
        public List<BuildingSave> buildings = new();
        public long campLastTick;
        public float campGold;

        // Missions
        public string dailyDate = "";
        public List<MissionState> daily = new();
        public bool dailyChestClaimed;
        public List<AchievementSave> achievements = new();

        public LifetimeStats life = new();

        /// <summary>Ads caps, camp boosts, purchases and cosmetics (Monetization/MonetizationSave.cs). Old saves get the defaults.</summary>
        public MonetizationSave shop = new();

        /// <summary>Fixes nulls left by old or hand-edited saves, and migrates older versions.</summary>
        public void Sanitize()
        {
            stageStars ??= new List<int>();
            buildings ??= new List<BuildingSave>();
            daily ??= new List<MissionState>();
            achievements ??= new List<AchievementSave>();
            life ??= new LifetimeStats();
            dailyDate ??= "";
            buildings.RemoveAll(b => b == null);

            // v2: the power buildings (Atelier, Armurerie, Infirmerie) are gone, drop their levels.
            if (version < 2) buildings.RemoveAll(b => Camp.Get(b.id) == null);

            // Future migrations go here: if (version < 3) { ... }
            version = CurrentVersion;
        }
    }

    /// <summary>Loads/saves <see cref="MetaData"/> in PlayerPrefs (versioned key, never throws).</summary>
    public static class MetaSave
    {
        const string Key = "meta_save_v1";

        static MetaData data;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => data = null;

        public static MetaData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        static void Load()
        {
            MetaData loaded = null;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    loaded = JsonUtility.FromJson<MetaData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MetaSave] Corrupted save, starting fresh: {e.Message}");
                    // Keep a copy so a broken save can still be inspected/recovered.
                    PlayerPrefs.SetString(Key + "_backup", json);
                }
            }

            bool fresh = loaded == null;
            data = loaded ?? new MetaData();
            data.Sanitize();
            if (fresh) data.campLastTick = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>Writes to disk and tells the UI something changed.</summary>
        public static void Save()
        {
            if (data == null) return;
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MetaSave] Could not save: {e.Message}");
            }
            Progression.NotifyChanged();
        }

        /// <summary>Wipes the meta blob (editor debug only, see Progression.ResetAll).</summary>
        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            data = null;
        }
    }
}
