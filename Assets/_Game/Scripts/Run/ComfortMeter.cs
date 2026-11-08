using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// How comfortable the player is right now, 0 (struggling) .. 1 (standing still, full HP, nothing touches them),
    /// measured over a sliding window. WorldEvents uses it to send "pressure" events that force movement when
    /// the run gets too easy. It reacts only to how you play (same thresholds for everyone), never to who you are.
    ///
    /// Four parts, each 0..1, combined with the public weights:
    /// - Stillness: 1 - average speed / <see cref="stillSpeed"/> (standing still = 1, running around = 0).
    /// - Health:    average HP fraction, mapped so 50% HP = 0 and 95%+ = 1.
    /// - Untouched: 1 - damage taken in the window / (<see cref="hurtShare"/> x max HP).
    /// - Clear:     1 - average enemies within <see cref="crowdRadius"/> m / <see cref="crowdCount"/> (enemies die on arrival = 1).
    /// Examples (weights 0.3 / 0.2 / 0.3 / 0.2, WorldEvents threshold 0.62):
    /// - standing still, full HP, nothing reaches you: ~1.0 -> pressure;
    /// - standing still at 70% HP, taking regular hits, 3 enemies close: ~0.6 -> no pressure (already fighting);
    /// - the test bot circling at full HP, untouched, clear screen: 0.7 -> pressure (kiting in circles is "too easy" too);
    /// - moving and getting scratched (90% HP, a few hits): ~0.55 -> no pressure;
    /// - fighting a horde at 60% HP: ~0.2.
    /// Added at runtime by ArenaEvents.
    /// </summary>
    public class ComfortMeter : MonoBehaviour
    {
        public static ComfortMeter Instance { get; private set; }

        /// <summary>Current comfort 0..1 (0 when no run is going on).</summary>
        public static float Value => Instance ? Instance.value : 0f;
        /// <summary>True when the player barely moved over the window (anti-camping events are picked then).</summary>
        public static bool Camping => Instance && Instance.Stillness >= Instance.campingStillness;
        /// <summary>Shows a small readout on the HUD (for tuning with the test bot).</summary>
        public static bool ShowDebug;

        [Header("Window")]
        public float window = 12f;
        public float sampleInterval = 0.25f;

        [Header("Weights (normalized)")]
        public float stillnessWeight = 0.3f;
        public float healthWeight = 0.2f;
        public float untouchedWeight = 0.3f;
        public float clearWeight = 0.2f;

        [Header("References")]
        [Tooltip("Average speed (m/s) counted as 'really moving'. Base move speed is 5.2.")]
        public float stillSpeed = 2.5f;
        [Tooltip("Damage taken in the window, as a share of max HP, that counts as 'under pressure'.")]
        public float hurtShare = 0.25f;
        public float crowdRadius = 4f;
        public float crowdCount = 6f;
        [Tooltip("Stillness above this = camping.")]
        public float campingStillness = 0.6f;

        public float Stillness { get; private set; }
        public float Health { get; private set; }
        public float Untouched { get; private set; }
        public float Clear { get; private set; }

        float value;
        float[] moved, hp, hurt, crowd;
        int head, filled;
        float sampleTimer, hurtAccum;
        Vector3 lastPos;
        bool hasLast;

        void Awake()
        {
            Instance = this;
            int n = Mathf.Max(4, Mathf.RoundToInt(window / sampleInterval));
            moved = new float[n];
            hp = new float[n];
            hurt = new float[n];
            crowd = new float[n];
        }

        void Start()
        {
            if (PlayerHealth.Instance) PlayerHealth.Instance.Hurt += OnHurt;
        }

        void OnDestroy()
        {
            if (PlayerHealth.Instance) PlayerHealth.Instance.Hurt -= OnHurt;
            if (Instance == this) Instance = null;
        }

        void OnHurt(float amount) => hurtAccum += amount;

        void Update()
        {
            var gm = GameManager.Instance;
            var player = PlayerController.Instance;
            if (!gm || gm.State != GameState.Playing || !player || !PlayerHealth.Instance)
            {
                if (gm && gm.State is GameState.Menu or GameState.GameOver) value = 0f;
                return;
            }

            sampleTimer += Time.deltaTime;
            if (sampleTimer < sampleInterval) return;
            sampleTimer -= sampleInterval;

            Vector3 p = player.Position;
            p.y = 0f;
            float d = hasLast ? (p - lastPos).magnitude : 0f;
            lastPos = p;
            hasLast = true;

            int near = 0;
            float r2 = crowdRadius * crowdRadius;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.IsProp) continue;
                Vector3 v = e.transform.position - p;
                v.y = 0f;
                if (v.sqrMagnitude <= r2) near++;
            }

            moved[head] = Mathf.Min(d, 3f); // ignore teleports
            hp[head] = PlayerHealth.Instance.Fraction;
            hurt[head] = hurtAccum;
            crowd[head] = near;
            hurtAccum = 0f;
            head = (head + 1) % moved.Length;
            if (filled < moved.Length) filled++;

            Recompute();
        }

        void Recompute()
        {
            if (filled == 0) return;
            float sumMoved = 0f, sumHp = 0f, sumHurt = 0f, sumCrowd = 0f;
            for (int i = 0; i < filled; i++)
            {
                sumMoved += moved[i];
                sumHp += hp[i];
                sumHurt += hurt[i];
                sumCrowd += crowd[i];
            }
            float seconds = filled * sampleInterval;
            float speed = sumMoved / seconds;
            float maxHp = PlayerHealth.Instance ? PlayerHealth.Instance.Max : 100f;

            Stillness = 1f - Mathf.Clamp01(speed / Mathf.Max(0.1f, stillSpeed));
            Health = Mathf.Clamp01((sumHp / filled - 0.5f) / 0.45f);
            // A partly filled window counts proportionally, so a fresh run doesn't read as "untouched" too fast.
            Untouched = 1f - Mathf.Clamp01(sumHurt / Mathf.Max(1f, hurtShare * maxHp * seconds / window));
            Clear = 1f - Mathf.Clamp01(sumCrowd / filled / Mathf.Max(1f, crowdCount));

            float total = stillnessWeight + healthWeight + untouchedWeight + clearWeight;
            float v = Stillness * stillnessWeight + Health * healthWeight + Untouched * untouchedWeight + Clear * clearWeight;
            value = total > 0f ? Mathf.Clamp01(v / total) : 0f;
            // Not trusted until most of the window is filled.
            if (filled < moved.Length / 2) value *= (float)filled / (moved.Length / 2);
        }

        /// <summary>One-line readout for tuning.</summary>
        public string DebugLine() =>
            $"confort {value:0.00}  immobile {Stillness:0.00}  pv {Health:0.00}  intact {Untouched:0.00}  dégagé {Clear:0.00}";
    }
}
