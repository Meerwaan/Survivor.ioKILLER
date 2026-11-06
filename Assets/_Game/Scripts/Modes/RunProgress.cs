using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// "Beat yourself": per board, the score timeline of the personal-best run (the HUD compares the live score with it,
    /// like a racing ghost), the best danger tier, the longest survival and the last scores.
    /// Only ranked runs that were submitted count. Own PlayerPrefs key, local to the device.
    /// </summary>
    public static class RunProgress
    {
        /// <summary>Seconds between two samples of the score timeline.</summary>
        public const float Step = 15f;
        const string Key = "progress_v1";
        const int RecentMax = 10;
        /// <summary>At most 40 min of timeline (160 samples) per board.</summary>
        const int MaxSamples = 160;

        [Serializable]
        public class Board
        {
            public string id = "";
            /// <summary>Score of the record run every <see cref="Step"/> seconds (index 0 = 0:00).</summary>
            public List<int> pace = new();
            /// <summary>Final score and length of the record run.</summary>
            public int paceFinal;
            public float paceSeconds;
            public int bestDanger;
            public float bestSeconds;
            public int runs;
            /// <summary>Last scores, oldest first.</summary>
            public List<int> recent = new();
        }

        [Serializable]
        class Data
        {
            public List<Board> boards = new();
        }

        static Data data;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => data = null;

        static Data D
        {
            get
            {
                if (data != null) return data;
                string json = PlayerPrefs.GetString(Key, "");
                try
                {
                    data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Data>(json);
                }
                catch (Exception)
                {
                    data = null;
                }
                data ??= new Data();
                return data;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
        }

        /// <summary>The board's progress, or null if no ranked run ended on it yet.</summary>
        public static Board Find(string boardId)
        {
            if (string.IsNullOrEmpty(boardId)) return null;
            foreach (var b in D.boards)
                if (b.id == boardId) return b;
            return null;
        }

        /// <summary>
        /// Score the record run had at this moment (interpolated), or -1 when there is no record timeline yet
        /// or the record run was already over at that time.
        /// </summary>
        public static int RecordAt(string boardId, float seconds)
        {
            var b = Find(boardId);
            if (b == null || b.pace.Count == 0 || seconds > b.paceSeconds) return -1;
            float f = seconds / Step;
            int i = Mathf.FloorToInt(f);
            int a = b.pace[Mathf.Clamp(i, 0, b.pace.Count - 1)];
            int next = i + 1 < b.pace.Count ? b.pace[i + 1] : b.paceFinal;
            return Mathf.RoundToInt(Mathf.Lerp(a, next, f - i));
        }

        /// <summary>
        /// Records a finished ranked run. A new personal best replaces the record timeline.
        /// Returns the board state from BEFORE this run (for the "record battu de..." lines), as a copy.
        /// </summary>
        public static Board Finish(string boardId, List<int> timeline, int finalScore, int danger, float seconds, bool newBest)
        {
            if (string.IsNullOrEmpty(boardId)) return null;
            var b = Find(boardId);
            if (b == null)
            {
                b = new Board { id = boardId };
                D.boards.Add(b);
            }
            var before = new Board
            {
                id = b.id, paceFinal = b.paceFinal, paceSeconds = b.paceSeconds, bestDanger = b.bestDanger,
                bestSeconds = b.bestSeconds, runs = b.runs, recent = new List<int>(b.recent),
            };

            b.runs++;
            b.bestDanger = Mathf.Max(b.bestDanger, danger);
            b.bestSeconds = Mathf.Max(b.bestSeconds, seconds);
            b.recent.Add(finalScore);
            if (b.recent.Count > RecentMax) b.recent.RemoveRange(0, b.recent.Count - RecentMax);
            if (newBest || b.pace.Count == 0 && finalScore >= b.paceFinal)
            {
                b.pace = new List<int>(timeline.Count > MaxSamples ? timeline.GetRange(0, MaxSamples) : timeline);
                b.paceFinal = finalScore;
                b.paceSeconds = seconds;
            }
            Save();
            return before;
        }

        /// <summary>"12 k · 45 k · 61 k" (last scores, oldest first), empty if none.</summary>
        public static string RecentText(Board b, int count)
        {
            if (b == null || b.recent.Count == 0) return "";
            int from = Mathf.Max(0, b.recent.Count - count);
            var parts = new List<string>();
            for (int i = from; i < b.recent.Count; i++) parts.Add(Short(b.recent[i]));
            return string.Join("  ·  ", parts);
        }

        /// <summary>61 907 -> "62 k", 9 406 -> "9,4 k", 940 -> "940".</summary>
        public static string Short(int score) =>
            score >= 10000 ? $"{Mathf.RoundToInt(score / 1000f)} k"
            : score >= 1000 ? (score / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " k"
            : score.ToString();
    }
}
