using UnityEngine;

namespace Survivor
{
    public class PlayerXP : MonoBehaviour
    {
        public static PlayerXP Instance { get; private set; }

        [Header("XP curve: required(L) = base + linear*(L-1) + cubic*(L-1)^power (see Docs/Pacing.md)")]
        [Tooltip("XP for the very first level-up (~8-10 s of killing).")]
        public float xpBase = 2f;
        public float xpLinear = 1.1f;
        [Tooltip("Main pacing knob. The cubic term makes the level ~ cube root of the XP gathered, so the curve is robust: " +
                 "twice the XP income only gives ~+25% levels.")]
        public float xpCubic = 0.07f;
        public float xpPower = 3f;

        public int Level { get; private set; } = 1;
        public float Current { get; private set; }
        public float Required => RequiredFor(Level);

        /// <summary>
        /// XP needed to go from this level to the next. Quick early levels (3, 5, 7, 9, 13...), then a steep climb
        /// (L10 ~ 68, L20 ~ 510, L30 ~ 1750) because the XP income grows with the stream.
        /// Targets: L9 at 1:30, L20 at 5:00, L26 at 8:00, L30 at 10:00, L33 at 12:00.
        /// </summary>
        public float RequiredFor(int level)
        {
            float n = Mathf.Max(0, level - 1);
            return xpBase + xpLinear * n + xpCubic * Mathf.Pow(n, xpPower);
        }

        PlayerStats stats;

        void Awake()
        {
            Instance = this;
            stats = GetComponent<PlayerStats>();
        }

        public void Add(float amount)
        {
            if (GameManager.Instance.State == GameState.GameOver) return;

            Current += amount * stats.xpMult;
            while (Current >= Required)
            {
                Current -= Required;
                Level++;
                GameManager.Instance.QueueLevelUp();
            }
        }
    }
}
