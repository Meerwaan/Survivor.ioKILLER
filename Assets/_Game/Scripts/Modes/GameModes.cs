using System;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// The ranked modes and how each one sets up <see cref="RunConfig"/> (same rules for everyone in a mode):
    /// - Classique: pure equal endless. Board "classic".
    /// - Défi du jour: seed from the UTC date, fixed start weapon + 2 fixed threats, seeded cards. Only the FIRST attempt
    ///   of the day is ranked; later ones are practice (nothing submitted). Board "daily_yyyyMMdd".
    /// - Défi de la semaine: element from the ISO week (UTC), start weapon of that element, cards of that element x2,
    ///   1-2 fixed threats, seeded cards. Every attempt counts (best score). Board "weekly_yyyyWW".
    /// - Menaces (NG+): unlocked when the camp is maxed (or the test unlock); the player turns threats on,
    ///   each one adds to the score multiplier. Board "menaces".
    /// Call <see cref="Configure"/> right before GameManager.StartRun (GameManager.Replay calls it again).
    /// </summary>
    public static class GameModes
    {
        public const string ClassicBoard = "classic", MenacesBoard = "menaces";
        public const string DailyPrefix = "daily_", WeeklyPrefix = "weekly_";
        const string DevUnlockKey = "dev_unlock_all";
        const int DailyThreatCount = 2;

        // ---------- Unlocks ----------

        /// <summary>Test switch (PlayerPrefs "dev_unlock_all"): unlocks everything that is gated. Clearly labeled in the UI.</summary>
        public static bool DevUnlockAll
        {
            get => PlayerPrefs.GetInt(DevUnlockKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(DevUnlockKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Owner's rule: the NG+ threats open once every camp building is maxed.</summary>
        public static bool MenacesUnlocked => Camp.IsMaxed || DevUnlockAll;

        public static string MenacesLockText => $"Améliore tout le Relais au maximum ({Camp.TotalLevels}/{Camp.MaxTotalLevels} niveaux)";

        // ---------- Seeds ----------

        /// <summary>Deterministic mix of a few ints (same result on every device). Never 0.</summary>
        public static int Hash(int a, int b, int c = 0)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h ^= h >> 15;
                h = (h ^ (uint)b) * 2246822519u;
                h ^= h >> 13;
                h = (h ^ (uint)c) * 3266489917u;
                h ^= h >> 16;
                int r = (int)(h & 0x7FFFFFFF);
                return r == 0 ? 1 : r;
            }
        }

        static DateTime Now => DateTime.UtcNow;

        // ---------- Daily ----------

        public static int Day(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;
        public static int Today => Day(Now);
        public static string DailyBoardId(int day) => DailyPrefix + day;
        public static string DailyBoard => DailyBoardId(Today);
        public static int DailySeed(int day) => Hash(day, 0x0DA11);
        public static string DailyWeapon(int day) => PickWeapon(AllWeapons, Hash(day, 0x3EA9));
        public static int DailyThreats(int day) => Threats.PickFromSeed(Hash(day, 0x7EA7), DailyThreatCount);
        public static float DailyMult(int day) => Threats.ScoreMult(DailyThreats(day));
        public static bool DailyRankedUsed => ModeSave.Data.dailyRankedDay == Today;
        public static int DailyAttemptsToday => ModeSave.Data.dailyAttemptsDay == Today ? ModeSave.Data.dailyAttempts : 0;
        public static TimeSpan UntilNextDaily => Now.Date.AddDays(1) - Now;
        /// <summary>"06/11" for the UI.</summary>
        public static string DayLabel(int day) => $"{day % 100:00}/{day / 100 % 100:00}";

        // ---------- Weekly ----------

        /// <summary>ISO 8601 week (weeks start on Monday, week 1 holds the first Thursday).</summary>
        public static void IsoWeek(DateTime d, out int year, out int week)
        {
            int dow = ((int)d.DayOfWeek + 6) % 7; // Monday = 0
            DateTime thursday = d.Date.AddDays(3 - dow);
            year = thursday.Year;
            week = (thursday.DayOfYear - 1) / 7 + 1;
        }

        /// <summary>yyyyWW, e.g. 202645.</summary>
        public static int WeekKey(DateTime utc)
        {
            IsoWeek(utc, out int y, out int w);
            return y * 100 + w;
        }

        public static int ThisWeek => WeekKey(Now);
        public static string WeeklyBoardId(int week) => WeeklyPrefix + week;
        public static string WeeklyBoard => WeeklyBoardId(ThisWeek);
        public static int WeeklySeed(int week) => Hash(week, 0x3EEC1);

        /// <summary>Elements of the weekly rotation (every one has at least one weapon).</summary>
        public static readonly ItemTag[] Elements =
            { ItemTag.Feu, ItemTag.Poison, ItemTag.Electrique, ItemTag.Explosif, ItemTag.Lame, ItemTag.Balle };

        /// <summary>Rotates through every element (no repeat two weeks in a row).</summary>
        public static ItemTag WeeklyElement(int week) => Elements[((week / 100) * 53 + week % 100) % Elements.Length];
        public static string WeeklyWeapon(int week) => PickWeapon(WeaponsOf(WeeklyElement(week)), Hash(week, 0x3EA9));
        public static int WeeklyThreats(int week) => Threats.PickFromSeed(Hash(week, 0x7EEC), 1 + (Hash(week, 0xC0) & 1));
        public static float WeeklyMult(int week) => Threats.ScoreMult(WeeklyThreats(week));
        public static int WeeklyAttempts => ModeSave.Data.weeklyAttemptsWeek == ThisWeek ? ModeSave.Data.weeklyAttempts : 0;

        public static TimeSpan UntilNextWeek
        {
            get
            {
                var now = Now;
                int dow = ((int)now.DayOfWeek + 6) % 7;
                return now.Date.AddDays(7 - dow) - now;
            }
        }

        public static Color ElementColor(ItemTag t) => t switch
        {
            ItemTag.Feu => new Color(1f, 0.5f, 0.2f),
            ItemTag.Poison => new Color(0.55f, 1f, 0.35f),
            ItemTag.Electrique => new Color(0.45f, 0.8f, 1f),
            ItemTag.Explosif => new Color(1f, 0.8f, 0.3f),
            ItemTag.Lame => new Color(1f, 0.4f, 0.4f),
            ItemTag.Balle => new Color(0.9f, 0.9f, 0.75f),
            _ => Color.white,
        };

        public static string ElementHex(ItemTag t) => ColorUtility.ToHtmlStringRGB(ElementColor(t));

        // ---------- Weapons ----------

        /// <summary>Every weapon id in a fixed order (the seeds pick by index: same weapon for everyone).</summary>
        static readonly string[] AllWeapons =
        {
            "RevolverWeapon", "ShotgunWeapon", "MacheteWeapon", "MolotovWeapon", "GasWeapon",
            "FlamethrowerWeapon", "TeslaWeapon", "GrenadeWeapon", "SawWeapon", "DartWeapon",
        };

        static string[] WeaponsOf(ItemTag t) => t switch
        {
            ItemTag.Feu => new[] { "FlamethrowerWeapon", "MolotovWeapon" },
            ItemTag.Poison => new[] { "GasWeapon", "DartWeapon" },
            ItemTag.Electrique => new[] { "TeslaWeapon" },
            ItemTag.Explosif => new[] { "GrenadeWeapon", "MolotovWeapon" },
            ItemTag.Lame => new[] { "MacheteWeapon", "SawWeapon" },
            ItemTag.Balle => new[] { "RevolverWeapon", "ShotgunWeapon", "DartWeapon" },
            _ => AllWeapons,
        };

        /// <summary>Picks among the candidates that exist in this build (falls back to the full list).</summary>
        static string PickWeapon(string[] candidates, int seed)
        {
            var um = UpgradeManager.Instance;
            var present = new System.Collections.Generic.List<string>();
            foreach (var id in candidates)
            {
                if (!um) { present.Add(id); continue; }
                foreach (var w in um.Weapons)
                    if (w && w.Id == id) { present.Add(id); break; }
            }
            if (present.Count == 0) return "";
            return present[seed % present.Count];
        }

        static readonly (string id, string name)[] WeaponNames =
        {
            ("RevolverWeapon", "Revolver"), ("ShotgunWeapon", "Fusil à pompe"), ("MacheteWeapon", "Machettes"),
            ("MolotovWeapon", "Cocktail Molotov"), ("GasWeapon", "Gaz toxique"), ("FlamethrowerWeapon", "Lance-flammes"),
            ("TeslaWeapon", "Batterie Tesla"), ("GrenadeWeapon", "Grenades"), ("SawWeapon", "Scie circulaire"),
            ("DartWeapon", "Fléchettes"),
        };

        public static string WeaponName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "au choix";
            var um = UpgradeManager.Instance;
            if (um)
                foreach (var w in um.Weapons)
                    if (w && w.Id == id) return w.displayName;
            foreach (var (wid, name) in WeaponNames)
                if (wid == id) return name;
            return id;
        }

        // ---------- Menaces selection ----------

        public static int SelectedThreats => Threats.MaskOf(ModeSave.Data.threats);

        public static bool IsSelected(string id) => ModeSave.Data.threats.Contains(id);

        public static void ToggleThreat(string id)
        {
            var list = ModeSave.Data.threats;
            if (!list.Remove(id)) list.Add(id);
            ModeSave.Save();
        }

        // ---------- Run setup ----------

        /// <summary>Writes RunConfig for a ranked mode (Campaign: use Campaign.Configure).</summary>
        public static void Configure(GameMode mode)
        {
            RunConfig.SetEndless();
            switch (mode)
            {
                case GameMode.Daily:
                {
                    int day = Today;
                    RunConfig.Mode = GameMode.Daily;
                    RunConfig.Seed = DailySeed(day);
                    RunConfig.ForcedStartWeapon = DailyWeapon(day);
                    Threats.ApplyToConfig(DailyThreats(day));
                    RunConfig.BoardId = DailyBoardId(day);
                    RunConfig.SubmitScore = !DailyRankedUsed;
                    RunConfig.Label = "APPEL DU JOUR";
                    break;
                }
                case GameMode.Weekly:
                {
                    int week = ThisWeek;
                    RunConfig.Mode = GameMode.Weekly;
                    RunConfig.Seed = WeeklySeed(week);
                    RunConfig.BoostedTag = WeeklyElement(week);
                    RunConfig.ForcedStartWeapon = WeeklyWeapon(week);
                    Threats.ApplyToConfig(WeeklyThreats(week));
                    RunConfig.BoardId = WeeklyBoardId(week);
                    RunConfig.SubmitScore = true;
                    RunConfig.Label = "SEMAINE : " + Synergies.TagName(WeeklyElement(week)).ToUpperInvariant();
                    break;
                }
                case GameMode.Menaces:
                    RunConfig.Mode = GameMode.Menaces;
                    Threats.ApplyToConfig(SelectedThreats);
                    RunConfig.BoardId = MenacesBoard;
                    RunConfig.SubmitScore = MenacesUnlocked;
                    RunConfig.Label = "CONDITIONS";
                    break;
                default:
                    RunConfig.Label = "CLASSIQUE";
                    break;
            }
        }

        /// <summary>Called by GameManager.StartRun: the daily ranked attempt is used as soon as the run starts (quitting doesn't refund it).</summary>
        public static void OnRunStarted()
        {
            var d = ModeSave.Data;
            if (RunConfig.Mode == GameMode.Daily)
            {
                int day = Today;
                if (d.dailyAttemptsDay != day)
                {
                    d.dailyAttemptsDay = day;
                    d.dailyAttempts = 0;
                }
                d.dailyAttempts++;
                if (RunConfig.SubmitScore) d.dailyRankedDay = day;
                ModeSave.Save();
            }
            else if (RunConfig.Mode == GameMode.Weekly)
            {
                int week = ThisWeek;
                if (d.weeklyAttemptsWeek != week)
                {
                    d.weeklyAttemptsWeek = week;
                    d.weeklyAttempts = 0;
                }
                d.weeklyAttempts++;
                ModeSave.Save();
            }
        }

        // ---------- Text ----------

        /// <summary>Player-facing mode names (Docs/Univers.md 6.4): Défi du jour = l'Appel du jour, Menaces = conditions de sortie.</summary>
        public static string ModeName(GameMode mode) => mode switch
        {
            GameMode.Campaign => "OSTROV",
            GameMode.Daily => "APPEL DU JOUR",
            GameMode.Weekly => "DÉFI DE LA SEMAINE",
            GameMode.Menaces => "CONDITIONS",
            _ => "CLASSIQUE",
        };

        /// <summary>Short label for the current run (HUD / result screen). Empty for the campaign.</summary>
        public static string RunLabel
        {
            get
            {
                if (RunConfig.Mode == GameMode.Campaign) return "";
                string name = RunConfig.Mode == GameMode.Weekly && RunConfig.BoostedTag != ItemTag.None
                    ? "SEMAINE : " + Synergies.TagName(RunConfig.BoostedTag).ToUpperInvariant()
                    : ModeName(RunConfig.Mode);
                if (!RunConfig.SubmitScore) return name + "  ·  ENTRAÎNEMENT";
                return Mathf.Approximately(RunConfig.ScoreMult, 1f) ? name : $"{name}  ·  {Threats.FormatMult(RunConfig.ScoreMult)}";
            }
        }

        /// <summary>Board of a mode (for the menu): today's daily, this week's weekly...</summary>
        public static string BoardOf(GameMode mode) => mode switch
        {
            GameMode.Daily => DailyBoard,
            GameMode.Weekly => WeeklyBoard,
            GameMode.Menaces => MenacesBoard,
            _ => ClassicBoard,
        };

        /// <summary>"05:12:33" under a day, "3 j 04 h 12" above.</summary>
        public static string FormatSpan(TimeSpan t)
        {
            if (t.Ticks < 0) t = TimeSpan.Zero;
            return t.Days > 0 ? $"{t.Days} j {t.Hours:00} h {t.Minutes:00}" : $"{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
