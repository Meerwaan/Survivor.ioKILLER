using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// A car battery wired to the player: lightning jumps from enemy to enemy.
    /// Evolves into a storm that also calls bolts down from the sky.
    /// Lightning jumps 50% further from slowed or poisoned enemies and hits them 30% harder (Synergies).
    /// </summary>
    public class TeslaWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public float damage;
            [Tooltip("Enemies hit by one bolt.")]
            public int chains;
            public float cooldown;
        }

        public float range = 9f;
        public float chainRange = 4.5f;
        public float knockback = 1.5f;

        [Header("Evolution: Tempête électrique")]
        public float evoDamage = 38f;
        public int evoChains = 14;
        public int evoBolts = 3;
        public float evoCooldown = 0.75f;
        public int evoSkyStrikes = 3;
        public float evoStrikeRadius = 2.2f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Un éclair qui saute d'ennemi en ennemi (3 cibles).", damage = 14, chains = 3, cooldown = 1.35f },
            new() { text = "+1 cible", damage = 14, chains = 4, cooldown = 1.25f },
            new() { text = "Dégâts +55%, +1 cible", damage = 22, chains = 5, cooldown = 1.1f },
            new() { text = "+2 cibles, recharge rapide", damage = 25, chains = 7, cooldown = 0.95f },
            new() { text = "+3 cibles, dégâts +35%", damage = 34, chains = 10, cooldown = 0.8f },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Éclairs en chaîne : idéal contre les hordes.";
        public override string EvolutionName => "Tempête électrique";
        public override string EvolutionPassive => "projectiles";
        public override string EvolutionText => "3 éclairs géants en chaîne et la foudre qui tombe du ciel.";
        public override ItemTag Tags => ItemTag.Electrique;

        static readonly Color BoltColor = new(0.7f, 1.4f, 3f);
        static readonly Color StormColor = new(1.6f, 1f, 3f);

        float timer;
        readonly List<Enemy> hit = new(32);

        void Update()
        {
            if (Level == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            Vector3 origin = Origin;
            if (!FindNearest(origin, range))
            {
                timer = 0.1f;
                return;
            }

            var s = levels[Level - 1];
            timer = (Evolved ? evoCooldown : s.cooldown) * Stats.cooldownMult;
            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            int chains = (Evolved ? evoChains : s.chains) + Stats.ricochets + Synergies.ExtraChains;
            int bolts = (Evolved ? evoBolts : 1) + Stats.extraProjectiles;

            hit.Clear();
            for (int b = 0; b < bolts; b++) Chain(origin, damage, chains);

            if (Evolved) SkyStrikes(damage);
            SFX.Play(SfxId.Zap, 1f, Evolved ? 0.8f : 1f);
            if (GameFX.Instance) GameFX.Instance.Flash(origin, 0.4f, BoltColor, 0.08f);
        }

        void Chain(Vector3 origin, float damage, int chains)
        {
            Vector3 from = origin;
            float jump = range;
            Color color = Evolved ? StormColor : BoltColor;
            for (int c = 0; c < chains; c++)
            {
                var target = FindNearest(from, jump, hit);
                if (!target) break;
                hit.Add(target);

                Vector3 to = target.transform.position + Vector3.up;
                if (GameFX.Instance)
                {
                    GameFX.Instance.Lightning(from, to, color, Evolved ? 0.28f : 0.18f);
                    GameFX.Instance.Sparks(to, color, 4);
                }
                // Read before the hit: a kill recycles the enemy.
                float conduct = Synergies.ChainRange(target);
                Synergies.Hit(target, damage, to - from, knockback, ItemTag.Electrique);
                from = to;
                jump = chainRange * Stats.areaMult * conduct;
            }
        }

        void SkyStrikes(float damage)
        {
            Vector3 me = transform.position;
            for (int i = 0; i < evoSkyStrikes + Stats.extraProjectiles; i++)
            {
                var e = FindRandom(me, range + 4f);
                if (!e) break;
                Vector3 p = e.transform.position;
                if (GameFX.Instance)
                    GameFX.Instance.Lightning(p + new Vector3(Random.Range(-2f, 2f), 14f, Random.Range(-1f, 1f)), p, StormColor, 0.45f, 0.25f);
                Synergies.Explosion(p, evoStrikeRadius * Stats.areaMult, damage, 6f, new Color(0.6f, 0.7f, 1.6f), ItemTag.Electrique);
            }
        }
    }
}
