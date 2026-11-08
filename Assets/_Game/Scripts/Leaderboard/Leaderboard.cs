using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string name;
        /// <summary>Final score (base score x mult, rounded).</summary>
        public int score;
        public int seconds;
        public int kills;
        public int level;
        public long timestamp;
        /// <summary>Score multiplier of the run (threats). 1 in classic.</summary>
        public float mult = 1f;
        /// <summary>Cosmetics shown next to the name (sold in the shop): title text and skin id.</summary>
        public string title = "";
        public string skin = "";
    }

    /// <summary>
    /// Where scores are stored, one board per id ("classic", "daily_20261106", "weekly_202645", "menaces").
    /// Callbacks (instead of return values) so an online version (Supabase, etc.) can plug in later
    /// without touching the game code.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>Saves a score on a board. onRank receives its 1-based position, or -1 if it didn't make the board.</summary>
        void Submit(string boardId, LeaderboardEntry entry, Action<int> onRank);

        /// <summary>Best <paramref name="count"/> entries of a board, best first.</summary>
        void GetTop(string boardId, int count, Action<List<LeaderboardEntry>> onResult);
    }

    /// <summary>Old single-board calls, kept so callers written before the boards still compile (= classic board).</summary>
    public static class LeaderboardServiceExtensions
    {
        public static void Submit(this ILeaderboardService s, LeaderboardEntry entry, Action<int> onRank) =>
            s.Submit(Leaderboard.ClassicBoard, entry, onRank);

        public static void GetTop(this ILeaderboardService s, int count, Action<List<LeaderboardEntry>> onResult) =>
            s.GetTop(Leaderboard.ClassicBoard, count, onResult);
    }

    public static class Leaderboard
    {
        public const string ClassicBoard = "classic";

        static ILeaderboardService service;

        /// <summary>The active leaderboard. Local for now; swap in an online service here later.</summary>
        public static ILeaderboardService Service
        {
            get => service ??= new LocalLeaderboard();
            set => service = value;
        }

        // ---------- Personal best (this device, per board) ----------

        /// <summary>The classic board keeps the historic "best_score" key so old records survive.</summary>
        public static string BestKey(string boardId) =>
            string.IsNullOrEmpty(boardId) || boardId == ClassicBoard ? "best_score" : "best_score_" + boardId;

        public static int PersonalBest(string boardId) => PlayerPrefs.GetInt(BestKey(boardId), 0);

        /// <summary>Saves the score if it beats the record. Returns true for a new record.</summary>
        public static bool TrySetPersonalBest(string boardId, int score)
        {
            if (score <= PersonalBest(boardId)) return false;
            PlayerPrefs.SetInt(BestKey(boardId), score);
            PlayerPrefs.Save();
            return true;
        }
    }

    /// <summary>
    /// Top scores saved on this device, one PlayerPrefs key per board.
    /// Daily and weekly boards are pruned (only the most recent ones are kept, with their personal bests).
    /// </summary>
    public class LocalLeaderboard : ILeaderboardService
    {
        /// <summary>Classic keeps the historic key; other boards add their id.</summary>
        const string Key = "leaderboard_local_v1";
        const string IndexKey = "leaderboard_boards_v1";
        const int MaxEntries = 50;
        const int KeepDaily = 7, KeepWeekly = 6;

        [Serializable]
        class Data
        {
            public List<LeaderboardEntry> entries = new();
        }

        [Serializable]
        class Index
        {
            public List<string> boards = new();
        }

        static string KeyOf(string boardId) =>
            string.IsNullOrEmpty(boardId) || boardId == Leaderboard.ClassicBoard ? Key : Key + "_" + boardId;

        public void Submit(string boardId, LeaderboardEntry entry, Action<int> onRank)
        {
            if (entry == null)
            {
                onRank?.Invoke(-1);
                return;
            }
            Remember(boardId);

            var data = Load(boardId);
            data.entries.Add(entry);
            data.entries.Sort((a, b) => b.score.CompareTo(a.score));

            int rank = data.entries.IndexOf(entry) + 1;
            if (data.entries.Count > MaxEntries)
                data.entries.RemoveRange(MaxEntries, data.entries.Count - MaxEntries);
            Save(boardId, data);

            onRank?.Invoke(rank <= MaxEntries ? rank : -1);
        }

        public void GetTop(string boardId, int count, Action<List<LeaderboardEntry>> onResult)
        {
            var entries = Load(boardId).entries;
            onResult?.Invoke(entries.GetRange(0, Mathf.Clamp(count, 0, entries.Count)));
        }

        static Data Load(string boardId)
        {
            string json = PlayerPrefs.GetString(KeyOf(boardId), "");
            if (string.IsNullOrEmpty(json)) return new Data();
            try
            {
                var d = JsonUtility.FromJson<Data>(json) ?? new Data();
                d.entries ??= new List<LeaderboardEntry>();
                d.entries.RemoveAll(e => e == null);
                return d;
            }
            catch (Exception)
            {
                return new Data();
            }
        }

        static void Save(string boardId, Data data)
        {
            PlayerPrefs.SetString(KeyOf(boardId), JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Adds the board to the index and drops the oldest daily / weekly boards.</summary>
        static void Remember(string boardId)
        {
            if (string.IsNullOrEmpty(boardId)) return;
            Index index;
            try
            {
                index = JsonUtility.FromJson<Index>(PlayerPrefs.GetString(IndexKey, "")) ?? new Index();
            }
            catch (Exception)
            {
                index = new Index();
            }
            index.boards ??= new List<string>();
            if (!index.boards.Contains(boardId)) index.boards.Add(boardId);

            Prune(index.boards, GameModes.DailyPrefix, KeepDaily);
            Prune(index.boards, GameModes.WeeklyPrefix, KeepWeekly);

            PlayerPrefs.SetString(IndexKey, JsonUtility.ToJson(index));
        }

        static void Prune(List<string> boards, string prefix, int keep)
        {
            var dated = boards.FindAll(b => b.StartsWith(prefix));
            if (dated.Count <= keep) return;
            dated.Sort(string.CompareOrdinal); // ids are yyyyMMdd / yyyyWW: oldest first
            for (int i = 0; i < dated.Count - keep; i++)
            {
                PlayerPrefs.DeleteKey(KeyOf(dated[i]));
                PlayerPrefs.DeleteKey(Leaderboard.BestKey(dated[i]));
                boards.Remove(dated[i]);
            }
        }
    }

    /// <summary>The player's nickname and equipped cosmetics, remembered between sessions.</summary>
    public static class PlayerProfile
    {
        const string Key = "player_name";
        const string TitleKey = "player_title";
        const string SkinKey = "player_skin";

        /// <summary>Equipped title shown on the leaderboard (cosmetic only). Empty = none.</summary>
        public static string Title
        {
            get => PlayerPrefs.GetString(TitleKey, "");
            set { PlayerPrefs.SetString(TitleKey, value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>Equipped skin id (cosmetic only). Empty = default look.</summary>
        public static string Skin
        {
            get => PlayerPrefs.GetString(SkinKey, "");
            set { PlayerPrefs.SetString(SkinKey, value ?? ""); PlayerPrefs.Save(); }
        }
        public const int MaxLength = 16;

        public static string Name
        {
            get
            {
                string n = PlayerPrefs.GetString(Key, "");
                return string.IsNullOrWhiteSpace(n) ? "Passeur" : n;
            }
            set
            {
                string n = (value ?? "").Trim();
                if (n.Length > MaxLength) n = n.Substring(0, MaxLength);
                PlayerPrefs.SetString(Key, n);
                PlayerPrefs.Save();
            }
        }
    }
}
