using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Base for every weapon. Weapons sit on the player and do nothing until Level is at least 1.
    /// Each weapon has 5 levels, then an EVOLUTION once it is maxed and its partner bonus is owned
    /// (e.g. Revolver max + Adrénaline = Gatling du Diable).
    /// </summary>
    public abstract class Weapon : MonoBehaviour
    {
        public string displayName = "Arme";
        public Color color = Color.white;
        [Tooltip("Unused: every weapon is available to everyone (fair play). Kept for old scenes.")]
        public int unlockCost = 0;

        /// <summary>Stable save id.</summary>
        public string Id => GetType().Name;

        public int Level { get; private set; }
        public bool Evolved { get; private set; }
        public abstract int MaxLevel { get; }
        public bool IsMaxed => Level >= MaxLevel;
        public string CurrentName => Evolved ? EvolutionName : displayName;

        /// <summary>One line for the Codex and level-up cards.</summary>
        public abstract string Summary { get; }
        public abstract string EvolutionName { get; }
        /// <summary>Id of the passive (see UpgradeManager) needed to evolve.</summary>
        public abstract string EvolutionPassive { get; }
        public abstract string EvolutionText { get; }
        /// <summary>Families this weapon counts for in the set bonuses (Synergies).</summary>
        public virtual ItemTag Tags => ItemTag.None;

        protected PlayerStats Stats { get; private set; }
        protected Vector3 Origin => transform.position + Vector3.up;

        protected virtual void Awake() => Stats = GetComponent<PlayerStats>();

        /// <summary>Card text for reaching <paramref name="level"/> (1-based).</summary>
        public abstract string DescribeLevel(int level);

        public void LevelUp()
        {
            if (IsMaxed) return;
            Level++;
            OnLevelChanged();
        }

        public void Evolve()
        {
            if (!IsMaxed || Evolved) return;
            Evolved = true;
            OnLevelChanged();
            if (GameFX.Instance)
            {
                Vector3 p = transform.position;
                // Readable but never screen-covering: two thin rings, a short pop, sparks.
                GameFX.Instance.Ring(p, 3.5f, color * 1.5f, 0.55f);
                GameFX.Instance.Ring(p, 2f, UpgradeManager.EvolutionColor * 1.3f, 0.35f);
                GameFX.Instance.Flash(p + Vector3.up, 0.9f, UpgradeManager.EvolutionColor * 1.5f, 0.15f);
                GameFX.Instance.Sparks(p + Vector3.up, color * 2f, 30, 1.6f);
            }
        }

        protected virtual void OnLevelChanged() { }

        // ---------- Targeting helpers ----------

        public static Enemy FindNearest(Vector3 from, float range, List<Enemy> exclude = null)
        {
            Enemy best = null;
            float bestSqr = range * range;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.IsProp) continue;
                Vector3 d = e.transform.position - from;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr && (exclude == null || !exclude.Contains(e)))
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>Random living enemy within range (and on screen), or null.</summary>
        public static Enemy FindRandom(Vector3 from, float range)
        {
            var list = Enemy.Active;
            var view = CameraFollow.Instance;
            for (int tries = 0; tries < 10 && list.Count > 0; tries++)
            {
                var e = list[Random.Range(0, list.Count)];
                if (e.IsProp) continue;
                Vector3 d = e.transform.position - from;
                d.y = 0f;
                if (d.sqrMagnitude > range * range) continue;
                if (view && !view.IsInView(e.transform.position, 0f)) continue;
                return e;
            }
            return null;
        }

        /// <summary>
        /// Picks, among a few random enemies in range, the one with the most neighbours:
        /// explosives land where they kill the most.
        /// </summary>
        public static Enemy FindCrowd(Vector3 from, float range, float crowdRadius)
        {
            var list = Enemy.Active;
            Enemy best = null;
            int bestCount = -1;
            float r2 = crowdRadius * crowdRadius;
            for (int s = 0; s < 10; s++)
            {
                var c = FindRandom(from, range);
                if (!c) continue;
                Vector3 cp = c.transform.position;
                int count = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    Vector3 d = list[i].transform.position - cp;
                    if (d.x * d.x + d.z * d.z <= r2) count++;
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    best = c;
                }
            }
            return best ? best : FindNearest(from, range);
        }

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}
