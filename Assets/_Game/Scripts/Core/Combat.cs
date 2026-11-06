using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Survivor
{
    /// <summary>
    /// Shared damage rules: crits, burning, area hits, explosions, and what happens on every kill
    /// (combo, multi-kill callouts, life steal, exploding corpses).
    /// Corpse explosions are delayed a little, which gives nice visible chain reactions and keeps
    /// the enemy list safe while a weapon is still looping over it.
    /// </summary>
    public class Combat : MonoBehaviour
    {
        public static Combat Instance { get; private set; }

        public static readonly Color ExplosionColor = new(1f, 0.55f, 0.15f);
        public static readonly Color CorpseColor = new(0.9f, 0.15f, 0.08f);

        /// <summary>Kills in a row without a <see cref="ComboWindow"/> s break (breaks in the wave lulls, see RunDirector).</summary>
        public static int Combo { get; private set; }
        public static int BestCombo { get; private set; }
        public static float ComboTimeLeft => Mathf.Max(0f, comboUntil - Time.time);

        /// <summary>Fired with a callout text and color when many enemies die at once.</summary>
        public static event Action<string, Color> MultiKill;

        /// <summary>Seconds without a kill that break the combo. 2.5 -> 2: the wave lulls (5 s of silence) can now break it.</summary>
        public const float ComboWindow = 2f;
        const float BurstWindow = 0.6f;

        static readonly (int kills, string text, Color color)[] Tiers =
        {
            (8, "CARNAGE !", new Color(1f, 0.8f, 0.3f)),
            (15, "BOUCHERIE !", new Color(1f, 0.5f, 0.15f)),
            (25, "MASSACRE !", new Color(1f, 0.2f, 0.1f)),
            (40, "APOCALYPSE !", new Color(0.85f, 0.3f, 1f)),
        };

        /// <summary>Life steal: heal per proc, and a token bucket so it can't pin HP at max in a horde (max ~2 HP/s sustained).</summary>
        public const float LifeStealHeal = 3f;
        const float LifeStealBucket = 6f, LifeStealRefillPerSecond = 2f;

        /// <summary>Corpse explosion: flat + share of the dead enemy's max HP, radius 1.6 m + its body.</summary>
        const float CorpseFlatDamage = 8f, CorpseHealthShare = 0.35f, CorpseRadius = 1.6f;

        static float comboUntil, burstStart, lifeStealTokens, lifeStealTime;
        static int burstKills, burstTier;

        struct Pending
        {
            public Vector3 position;
            public float radius, damage, time, playerDamage;
            public Color color;
            public bool corpse;
        }

        readonly List<Pending> pending = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Combo = BestCombo = 0;
            comboUntil = burstStart = 0f;
            lifeStealTokens = LifeStealBucket;
            lifeStealTime = 0f;
            burstKills = burstTier = 0;
            MultiKill = null;
        }

        void Awake()
        {
            Instance = this;
            ResetStatics();
        }

        static PlayerStats Stats => PlayerController.Instance ? PlayerController.Instance.Stats : null;

        // ---------- Hits ----------

        /// <summary>
        /// One hit from a player weapon. Rolls crits and burning (unless <paramref name="rollProcs"/> is false: Synergies.Hit
        /// rolls them itself to react to them). Returns true if it killed.
        /// </summary>
        public static bool Hit(Enemy enemy, float damage, Vector3 direction, float knockback, bool alwaysCrit = false, bool rollProcs = true)
        {
            if (!enemy || enemy.IsDead) return false;

            var s = Stats;
            bool crit = alwaysCrit || (rollProcs && s && Random.value < s.critChance);
            if (crit) damage *= s ? s.critMult : 2f;

            // Through Synergies: Huile / Pyromane scale it, and reactions (toxic burst, fusion, spread) see the fire.
            if (rollProcs && s && s.burnChance > 0f && Random.value < s.burnChance)
                Synergies.Ignite(enemy, damage * 0.4f + 2f, 3f * s.durationMult);

            enemy.TakeDamage(damage, direction, knockback, crit);
            return enemy.IsDead;
        }

        /// <summary>Hits every enemy in a circle. Knockback pushes away from the center. Returns the number of enemies hit.</summary>
        public static int Area(Vector3 center, float radius, float damage, float knockback, bool canCrit = true)
        {
            int hits = 0;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 d = e.transform.position - center;
                d.y = 0f;
                float r = radius + e.Radius;
                if (d.sqrMagnitude > r * r) continue;

                hits++;
                if (canCrit) Hit(e, damage, d, knockback);
                else e.TakeDamage(damage, d, knockback, false);
            }
            return hits;
        }

        /// <summary>Area damage with the full boom: flash, shockwave, sparks, embers and a bit of shake.</summary>
        public static int Explosion(Vector3 center, float radius, float damage, float knockback, Color color)
        {
            center.y = 0f;
            var fx = GameFX.Instance;
            if (fx) fx.Explosion(center, radius, color);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(Mathf.Min(0.25f, 0.04f * radius));
            return Area(center, radius, damage, knockback);
        }

        /// <summary>
        /// Explosion a little later (barrels, kamikazes, chain reactions). Safe to call while a weapon is
        /// looping over the enemy list. <paramref name="playerDamage"/> &gt; 0 also hurts the player if caught in it.
        /// </summary>
        public static void QueueExplosion(Vector3 position, float radius, float damage, Color color, float delay = 0.08f, float playerDamage = 0f)
        {
            if (!Instance) return;
            Instance.pending.Add(new Pending
            {
                position = position,
                radius = radius,
                damage = damage,
                time = Time.time + delay,
                playerDamage = playerDamage,
                color = color,
            });
        }

        /// <summary>Push wave when the player levels up: gives breathing room and feels great.</summary>
        public static void Nova(Vector3 center, float radius, float damage)
        {
            center.y = 0f;
            if (GameFX.Instance)
            {
                GameFX.Instance.Ring(center, radius, UIKit.Gold * 2.5f, 0.45f);
                GameFX.Instance.Ring(center, radius * 0.6f, Color.white * 2f, 0.3f);
                GameFX.Instance.Sparks(center + Vector3.up, UIKit.Gold * 2f, 40, 2f);
            }
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.15f);
            SFX.Play(SfxId.Thud, 0.8f, 0.8f);
            Area(center, radius, damage, 16f, false);
        }

        // ---------- Kills ----------

        /// <summary>Called by every enemy as it dies.</summary>
        public static void OnKill(Enemy enemy)
        {
            float now = Time.time;

            Combo = now <= comboUntil ? Combo + 1 : 1;
            comboUntil = now + ComboWindow;
            if (Combo > BestCombo) BestCombo = Combo;

            if (now - burstStart > BurstWindow)
            {
                burstStart = now;
                burstKills = 0;
                burstTier = 0;
            }
            burstKills++;
            while (burstTier < Tiers.Length && burstKills >= Tiers[burstTier].kills)
            {
                var tier = Tiers[burstTier];
                burstTier++;
                MultiKill?.Invoke(tier.text, tier.color);
                if (burstTier >= 3 && CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
            }

            var s = Stats;
            if (!s) return;

            if (s.lifeStealChance > 0f && Random.value < s.lifeStealChance)
            {
                lifeStealTokens = Mathf.Min(LifeStealBucket, lifeStealTokens + (now - lifeStealTime) * LifeStealRefillPerSecond);
                lifeStealTime = now;
                if (lifeStealTokens >= LifeStealHeal)
                {
                    lifeStealTokens -= LifeStealHeal;
                    PlayerHealth.Instance.Heal(LifeStealHeal);
                }
            }

            if (s.corpseExplodeChance > 0f && Instance && Random.value < s.corpseExplodeChance)
            {
                float damage = (CorpseFlatDamage + enemy.MaxHealth * CorpseHealthShare) * s.damageMult;
                Instance.pending.Add(new Pending
                {
                    position = enemy.transform.position,
                    radius = (CorpseRadius + enemy.Radius) * s.areaMult,
                    damage = damage,
                    time = now + Random.Range(0.08f, 0.16f),
                    color = CorpseColor,
                    corpse = true,
                });
            }
        }

        void Update()
        {
            if (pending.Count == 0) return;

            float now = Time.time;
            // Copy out the ones that are due: exploding can queue new ones (chain reactions).
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (i >= pending.Count || pending[i].time > now) continue;
                var p = pending[i];
                pending.RemoveAt(i);
                Explosion(p.position, p.radius, p.damage, 7f, p.color);
                if (p.corpse && GameFX.Instance) GameFX.Instance.Blood(p.position + Vector3.up * 0.5f, 12);
                if (p.playerDamage > 0f) HurtPlayer(p.position, p.radius, p.playerDamage);
            }

            // Mobile safety: never let a chain reaction pile up hundreds of explosions.
            if (pending.Count > 40) pending.RemoveRange(0, pending.Count - 40);
        }

        static void HurtPlayer(Vector3 center, float radius, float damage)
        {
            var player = PlayerController.Instance;
            if (!player || !PlayerHealth.Instance) return;
            Vector3 d = player.Position - center;
            d.y = 0f;
            float r = radius + player.Radius;
            if (d.sqrMagnitude <= r * r) PlayerHealth.Instance.TakeDamage(damage);
        }
    }
}
