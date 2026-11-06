using UnityEngine;

namespace Survivor
{
    public class PlayerXP : MonoBehaviour
    {
        public static PlayerXP Instance { get; private set; }

        [Header("XP curve: required(L) = base + linear*n + cubic*n^power + late*max(0, n - knee)^latePower, n = L-1 (Docs/Pacing.md)")]
        [Tooltip("XP for the very first level-up (~8-10 s of killing).")]
        public float xpBase = 2f;
        public float xpLinear = 1.1f;
        [Tooltip("Main pacing knob. The cubic term makes the level ~ cube root of the XP gathered, so the curve is robust: " +
                 "twice the XP income only gives ~+25% levels.")]
        public float xpCubic = 0.07f;
        public float xpPower = 3f;
        [Tooltip("Late slowdown starts after this many level-ups (14 = from L15, the last third of the 20-level stuff).")]
        public float xpKnee = 14f;
        [Tooltip("Late slowdown: the last stuff levels take longer (full around 4:30-5:30) and the bursts after it come every ~45 s instead of ~25 s.")]
        public float xpLate = 45f;
        public float xpLatePower = 2f;

        public int Level { get; private set; } = 1;
        public float Current { get; private set; }
        public float Required => RequiredFor(Level);

        /// <summary>
        /// XP needed to go from this level to the next. Quick early levels (3, 5, 7, 9, 13...), unchanged up to L15
        /// (the first minutes and the 3:00 horde need those level-ups), then the late term slows things down:
        /// L17 486, L18 770, L20 1630, L22 2880, L25 5500 (was 306 / 365 / 503 / 675 / 996 before the stuff cap).
        /// Measured with knee 16 / late 40: a focused bot filled its 20-level stuff at 3:28 and got a burst every ~28 s,
        /// too early and too often (a human who picks up gems levels even faster).
        /// </summary>
        public float RequiredFor(int level)
        {
            float n = Mathf.Max(0, level - 1);
            return xpBase + xpLinear * n + xpCubic * Mathf.Pow(n, xpPower) + xpLate * Mathf.Pow(Mathf.Max(0f, n - xpKnee), xpLatePower);
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
