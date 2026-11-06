using System;

namespace Survivor
{
    /// <summary>Score side of a finished non-campaign run (built by GameManager.EndRun, shown by RunResultScreen).</summary>
    public class RankedResult
    {
        public GameMode mode;
        public string boardId = "";
        /// <summary>"DÉFI DU JOUR  ·  x1.45" (see GameModes.RunLabel).</summary>
        public string label = "";
        /// <summary>Kill points + 2 per second.</summary>
        public int baseScore;
        public float mult = 1f;
        /// <summary>round(baseScore x mult): what goes to the board.</summary>
        public int finalScore;
        /// <summary>False for a practice attempt (daily after the ranked one): nothing was saved.</summary>
        public bool submitted;
        /// <summary>Personal best on this board before this run.</summary>
        public int previousBest;
        public bool newBest;
        /// <summary>1-based rank on the board; 0 = still waiting for the service, -1 = off the board / not submitted.</summary>
        public int rank;
        public Medal medal;
        /// <summary>Medal of the personal best before this run (to celebrate a new tier).</summary>
        public Medal previousMedal;

        public int Best => Math.Max(previousBest, submitted ? finalScore : 0);
        public bool NewMedal => submitted && medal > previousMedal;

        /// <summary>Fired when the rank arrives (the leaderboard answers through a callback, maybe later when online).</summary>
        public Action RankChanged;

        public void SetRank(int r)
        {
            rank = r;
            RankChanged?.Invoke();
        }
    }
}
