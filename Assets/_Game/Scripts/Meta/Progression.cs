using System;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// What survives between runs (saved on the device): gold and the chosen starting weapon.
    /// Fair play: nothing here makes a run stronger. Every player starts with the same stats and the
    /// whole weapon/passive pool; gold only buys camp buildings (cosmetics later).
    /// The long-term meta (campaign, camp, missions) lives in MetaSave.
    /// </summary>
    public static class Progression
    {
        const string GoldKey = "meta_gold";
        const string WeaponKey = "meta_start_weapon";

        public static event Action Changed;

        /// <summary>Lets the other meta systems refresh the UI after saving.</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        public static int Gold
        {
            get => PlayerPrefs.GetInt(GoldKey, 0);
            private set
            {
                PlayerPrefs.SetInt(GoldKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static void AddGold(int amount)
        {
            if (amount > 0) Gold += amount;
        }

        public static bool CanAfford(int gold) => Gold >= gold;

        /// <summary>Spends the gold, or nothing at all if there isn't enough.</summary>
        public static bool TrySpend(int gold)
        {
            if (gold < 0 || !CanAfford(gold)) return false;
            if (gold > 0) Gold -= gold;
            return true;
        }

        // ---------- Weapons ----------

        /// <summary>Every weapon is available to everyone (kept so callers stay simple).</summary>
        public static bool IsUnlocked(Weapon w) => true;

        /// <summary>Weapon given at the start of a run (any weapon can be picked, see the Codex tab).</summary>
        public static string StartingWeaponId
        {
            get => PlayerPrefs.GetString(WeaponKey, "");
            set
            {
                PlayerPrefs.SetString(WeaponKey, value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        // ---------- Debug ----------

        /// <summary>Editor-only debug: wipes all progression (keeps nickname, leaderboard and record).</summary>
        public static void ResetAll()
        {
            if (!Application.isEditor) return;
            PlayerPrefs.DeleteKey(GoldKey);
            PlayerPrefs.DeleteKey(WeaponKey);
            MetaSave.Delete();
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
