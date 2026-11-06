using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    public enum MissionType { Kills, Elites, Bosses, Evolve, Runs, GoldPicked, Survive, Combo, Level, Harvest, StageWin, DailyChallenge, Ranked }

    /// <summary>One daily mission (saved).</summary>
    [Serializable]
    public class MissionState
    {
        public int type;
        public int target;
        public int progress;
        public bool claimed;
        public int gold;

        public MissionType Type => (MissionType)type;
        public bool Done => progress >= target;
    }

    /// <summary>A long-term achievement with several tiers.</summary>
    public class AchievementDef
    {
        public string id;
        public string name;
        /// <summary>Description for a tier target, e.g. "Tue 5 000 zombies".</summary>
        public Func<long, string> describe;
        public Func<long> progress;
        public long[] tiers;
    }

    /// <summary>
    /// Daily missions (new set every local day, 3 to 5 depending on the Radio) and tiered achievements.
    /// Progress comes from the run stats at the end of each run (see RunRewards) and from the camp.
    /// </summary>
    public static class Missions
    {
        // ---------- Daily ----------

        /// <summary>Kinds where the best single run counts (others add up over the day).</summary>
        static bool IsBestOf(MissionType t) => t == MissionType.Survive || t == MissionType.Combo || t == MissionType.Level;

        /// <summary>0..3, raises targets and rewards as the campaign progresses.</summary>
        public static int Tier => Mathf.Clamp(Campaign.ClearedCount / 3, 0, 3);

        static int Target(MissionType t, int tier)
        {
            switch (t)
            {
                case MissionType.Kills: return new[] { 250, 500, 800, 1200 }[tier];
                case MissionType.Elites: return new[] { 3, 5, 8, 12 }[tier];
                case MissionType.Bosses: return new[] { 1, 1, 2, 3 }[tier];
                case MissionType.Evolve: return new[] { 1, 1, 2, 2 }[tier];
                case MissionType.Runs: return new[] { 2, 3, 3, 4 }[tier];
                case MissionType.GoldPicked: return new[] { 80, 150, 250, 400 }[tier];
                case MissionType.Survive: return new[] { 180, 300, 420, 600 }[tier];
                case MissionType.Combo: return new[] { 50, 100, 200, 300 }[tier];
                case MissionType.Level: return new[] { 10, 14, 18, 22 }[tier];
                case MissionType.StageWin: return new[] { 1, 1, 2, 2 }[tier];
                case MissionType.DailyChallenge: return 1;
                case MissionType.Ranked: return new[] { 2, 2, 3, 3 }[tier];
                default: return 1;
            }
        }

        public static string Describe(MissionState m)
        {
            int n = m.target;
            switch (m.Type)
            {
                case MissionType.Kills: return $"Tue {n} zombies";
                case MissionType.Elites: return $"Tue {n} élites";
                case MissionType.Bosses: return n > 1 ? $"Bats {n} boss" : "Bats un boss";
                case MissionType.Evolve: return n > 1 ? $"Fais évoluer {n} armes" : "Fais évoluer une arme";
                case MissionType.Runs: return $"Joue {n} parties";
                case MissionType.GoldPicked: return $"Gagne {n} or en une journée";
                case MissionType.Survive: return $"Survis {n / 60} min en une partie";
                case MissionType.Combo: return $"Atteins un combo x{n}";
                case MissionType.Level: return $"Atteins le niveau {n} en une partie";
                case MissionType.Harvest: return "Récolte le camp";
                case MissionType.StageWin: return n > 1 ? $"Gagne {n} étapes de campagne" : "Gagne une étape de campagne";
                case MissionType.DailyChallenge: return "Joue le défi du jour";
                case MissionType.Ranked: return $"Joue {n} parties classées";
                default: return "?";
            }
        }

        public static string ProgressText(MissionState m)
        {
            int p = Mathf.Min(m.progress, m.target);
            return m.Type == MissionType.Survive ? $"{UIKit.FormatTime(p)} / {UIKit.FormatTime(m.target)}" : $"{p} / {m.target}";
        }

        static string Today => DateTime.Now.ToString("yyyyMMdd");

        /// <summary>Time left before the next daily reset (local midnight).</summary>
        public static TimeSpan TimeToReset => DateTime.Today.AddDays(1) - DateTime.Now;

        /// <summary>Today's missions (generated on first access of the day, grows with the Radio).</summary>
        public static List<MissionState> Daily
        {
            get
            {
                Refresh();
                return MetaSave.Data.daily;
            }
        }

        static void Refresh()
        {
            var d = MetaSave.Data;
            string today = Today;
            bool changed = false;
            if (d.dailyDate != today)
            {
                d.dailyDate = today;
                d.daily.Clear();
                d.dailyChestClaimed = false;
                changed = true;
            }

            int slots = Camp.MissionSlots;
            if (d.daily.Count < slots)
            {
                // Same shuffled order all day, so a new Radio slot just reveals the next mission.
                int seed = int.TryParse(today, out int s) ? s : 0;
                var rng = new System.Random(seed);
                var order = new List<MissionType>((MissionType[])Enum.GetValues(typeof(MissionType)));
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                int tier = Tier;
                float mult = Camp.MissionRewardMult;
                foreach (var type in order)
                {
                    if (d.daily.Count >= slots) break;
                    if (d.daily.Exists(m => m.type == (int)type)) continue;
                    bool small = type == MissionType.Harvest;
                    d.daily.Add(new MissionState
                    {
                        type = (int)type,
                        target = Target(type, tier),
                        gold = Mathf.RoundToInt((small ? 40 : 60 + 25 * tier) * mult),
                    });
                }
                changed = true;
            }
            if (changed) MetaSave.Save();
        }

        /// <summary>Adds progress to today's missions of that kind (does not save; callers do).</summary>
        public static void Report(MissionType type, int amount)
        {
            if (amount <= 0) return;
            foreach (var m in Daily)
            {
                if (m.type != (int)type || m.claimed) continue;
                m.progress = IsBestOf(type) ? Mathf.Max(m.progress, amount) : m.progress + amount;
            }
        }

        public static bool Claim(MissionState m)
        {
            if (m == null || m.claimed || !m.Done) return false;
            m.claimed = true;
            MetaSave.Save();
            Progression.AddGold(m.gold);
            return true;
        }

        public static bool AllDailyClaimed => Daily.TrueForAll(m => m.claimed);
        public static bool ChestReady => AllDailyClaimed && !MetaSave.Data.dailyChestClaimed;
        public static int ChestGold => 120 + 50 * Tier;

        /// <summary>Daily chest: a gold bonus once every mission of the day is claimed.</summary>
        public static bool ClaimChest()
        {
            if (!ChestReady) return false;
            MetaSave.Data.dailyChestClaimed = true;
            MetaSave.Save();
            Progression.AddGold(ChestGold);
            return true;
        }

        // ---------- Achievements ----------

        static LifetimeStats Life => MetaSave.Data.life;

        /// <summary>"75 000" (French thousands separator, culture-independent).</summary>
        static string Thousands(long n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");

        public static readonly List<AchievementDef> Achievements = new()
        {
            new AchievementDef { id = "kills", name = "Boucher", describe = n => $"Tue {Thousands(n)} zombies", progress = () => Life.kills, tiers = new long[] { 1000, 5000, 20000, 75000 } },
            new AchievementDef { id = "survive", name = "Survivant", describe = n => $"Survis {n / 60} min en survie infinie", progress = () => Life.bestSeconds, tiers = new long[] { 300, 600, 900, 1200 } },
            new AchievementDef { id = "bosses", name = "Chasseur de boss", describe = n => $"Bats {n} boss", progress = () => Life.bosses, tiers = new long[] { 1, 10, 40, 100 } },
            new AchievementDef { id = "elites", name = "Tueur d'élites", describe = n => $"Tue {n} élites", progress = () => Life.elites, tiers = new long[] { 10, 50, 200, 500 } },
            new AchievementDef { id = "evolutions", name = "Savant fou", describe = n => $"Fais évoluer {n} armes", progress = () => Life.evolutions, tiers = new long[] { 1, 10, 30, 75 } },
            new AchievementDef { id = "stars", name = "Stratège", describe = n => $"Gagne {n} étoiles de campagne", progress = () => Campaign.TotalStars, tiers = new long[] { 3, 12, 24, 36 } },
            new AchievementDef { id = "camp", name = "Bâtisseur", describe = n => $"Atteins {n} niveaux de bâtiments", progress = () => Camp.TotalLevels, tiers = new long[] { 5, 15, 30, 43 } },
            new AchievementDef { id = "runs", name = "Acharné", describe = n => $"Joue {n} parties", progress = () => Life.runs, tiers = new long[] { 10, 50, 150, 400 } },
            new AchievementDef { id = "combo", name = "Furie", describe = n => $"Atteins un combo x{n}", progress = () => Life.bestCombo, tiers = new long[] { 100, 250, 500, 1000 } },
        };

        static AchievementSave SaveOf(AchievementDef a, bool create)
        {
            var list = MetaSave.Data.achievements;
            var s = list.Find(x => x.id == a.id);
            if (s == null && create) list.Add(s = new AchievementSave { id = a.id });
            return s;
        }

        public static int ClaimedTiers(AchievementDef a)
        {
            var s = SaveOf(a, false);
            return s != null ? Mathf.Clamp(s.claimedTiers, 0, a.tiers.Length) : 0;
        }

        public static bool IsComplete(AchievementDef a) => ClaimedTiers(a) >= a.tiers.Length;

        /// <summary>Target of the tier currently being worked on (the last one when complete).</summary>
        public static long CurrentTarget(AchievementDef a) => a.tiers[Mathf.Min(ClaimedTiers(a), a.tiers.Length - 1)];

        public static bool CanClaim(AchievementDef a) => !IsComplete(a) && a.progress() >= CurrentTarget(a);

        /// <summary>Gold for claiming an achievement tier (0-based).</summary>
        public static int TierGold(int tier) => new[] { 200, 350, 550, 800 }[Mathf.Clamp(tier, 0, 3)];

        public static string RewardText(int gold) => $"+{gold} or";

        public static bool ClaimAchievement(AchievementDef a)
        {
            if (!CanClaim(a)) return false;
            int tier = ClaimedTiers(a);
            SaveOf(a, true).claimedTiers = tier + 1;
            MetaSave.Save();
            Progression.AddGold(TierGold(tier));
            return true;
        }

        /// <summary>Things waiting for a tap (for the menu badge).</summary>
        public static int ClaimableCount
        {
            get
            {
                int n = 0;
                foreach (var m in Daily) if (m.Done && !m.claimed) n++;
                if (ChestReady) n++;
                foreach (var a in Achievements) if (CanClaim(a)) n++;
                return n;
            }
        }
    }
}
