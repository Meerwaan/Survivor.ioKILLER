using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>What the competitive modes remember (own PlayerPrefs key, so it never conflicts with the meta blob).</summary>
    [Serializable]
    public class ModeData
    {
        /// <summary>Threat ids turned on in the Menaces page (by id: survives a reordering of the list).</summary>
        public List<string> threats = new();
        /// <summary>UTC day (yyyyMMdd) whose ranked daily attempt was used. 0 = never.</summary>
        public int dailyRankedDay;
        /// <summary>Attempts at the current daily (ranked + practice), and which day they belong to.</summary>
        public int dailyAttempts;
        public int dailyAttemptsDay;
        /// <summary>Attempts at the current weekly challenge (yyyyWW).</summary>
        public int weeklyAttempts;
        public int weeklyAttemptsWeek;
    }

    public static class ModeSave
    {
        const string Key = "modes_save_v1";

        static ModeData data;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => data = null;

        public static ModeData Data
        {
            get
            {
                if (data != null) return data;
                string json = PlayerPrefs.GetString(Key, "");
                try
                {
                    data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<ModeData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ModeSave] Corrupted save, starting fresh: {e.Message}");
                    data = null;
                }
                data ??= new ModeData();
                data.threats ??= new List<string>();
                return data;
            }
        }

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
                Debug.LogWarning($"[ModeSave] Could not save: {e.Message}");
            }
        }
    }
}
