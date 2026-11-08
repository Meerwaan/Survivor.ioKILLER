using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Poison darts thrown at random enemies on screen: low direct damage, but they spread poison through the crowd
    /// (Corrosion, Surcharge, toxic bursts with fire). Counts as Poison and Balle: the bridge between Alchimiste and Pistolero.
    /// Weak single target (poison doesn't stack). Evolves into a ring of exploding needles (with Fiole d'acide).
    /// </summary>
    public class DartWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public float damage;
            [Tooltip("Poison damage per second given to every enemy hit (3 s).")]
            public float poison;
            public int darts;
            public float cooldown;
            public int pierce;
        }

        public GameObject bulletPrefab;
        public float range = 11f;
        public float speed = 22f;
        public float knockback = 1f;

        [Header("Evolution: Nuée d'aiguilles")]
        public int evoDarts = 10;
        public float evoDamage = 16f;
        public float evoPoison = 18f;
        public float evoCooldown = 0.9f;
        public int evoPierce = 2;
        [Tooltip("Each needle bursts on its first hit (60% damage): explosions spread poison (Dispersion).")]
        public float evoBurstRadius = 1.3f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Une fléchette empoisonnée vers un ennemi au hasard.", damage = 6, poison = 4, darts = 1, cooldown = 0.8f, pierce = 0 },
            new() { text = "+1 fléchette", damage = 6, poison = 4, darts = 2, cooldown = 0.75f, pierce = 0 },
            new() { text = "Poison +75%", damage = 7, poison = 7, darts = 2, cooldown = 0.7f, pierce = 0 },
            new() { text = "+1 fléchette, elles traversent 1 ennemi", damage = 7, poison = 7, darts = 3, cooldown = 0.65f, pierce = 1 },
            new() { text = "+1 fléchette, dégâts +40%, traverse 2 ennemis", damage = 10, poison = 9, darts = 4, cooldown = 0.55f, pierce = 2 },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Fléchettes qui empoisonnent toute la foule, faibles contre les Souches.";
        public override string EvolutionName => "Nuée d'aiguilles";
        public override string EvolutionPassive => "acid";
        public override string EvolutionText => "10 aiguilles en cercle qui explosent et répandent le poison partout.";
        public override ItemTag Tags => ItemTag.Poison | ItemTag.Balle;

        static readonly Color DartFlash = new(0.8f, 2.2f, 0.6f);

        float timer, ringAngle;

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
            float poison = (Evolved ? evoPoison : s.poison) * Stats.damageMult;
            int pierce = Evolved ? evoPierce : s.pierce;
            int ricochets = Stats.ricochets + Synergies.ExtraRicochets;
            float lifetime = range / speed + 0.2f;

            if (Evolved)
            {
                int count = evoDarts + 2 * Stats.extraProjectiles;
                float burst = evoBurstRadius * Stats.areaMult;
                ringAngle += 17f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, ringAngle + 360f * i / count, 0f) * Vector3.forward;
                    Throw(origin, dir, damage, pierce, lifetime, ricochets, burst, poison);
                }
                if (GameFX.Instance) GameFX.Instance.Ring(transform.position, 1.5f, DartFlash, 0.25f);
            }
            else
            {
                int count = s.darts + Stats.extraProjectiles;
                for (int i = 0; i < count; i++)
                {
                    // Random targets on screen: the poison goes everywhere instead of stacking on one enemy.
                    var target = FindRandom(origin, range);
                    if (!target) target = FindNearest(origin, range);
                    if (!target) break;
                    Vector3 dir = Quaternion.Euler(0f, Random.Range(-3f, 3f), 0f) * Flat(target.transform.position - transform.position);
                    Throw(origin, dir, damage, pierce, lifetime, ricochets, 0f, poison);
                }
            }

            SFX.Play(SfxId.Gunshot, Evolved ? 0.5f : 0.35f, 1.7f);
        }

        void Throw(Vector3 origin, Vector3 dir, float damage, int pierce, float lifetime, int ricochets, float burst, float poison)
        {
            var dart = Pool.Instance.Spawn<Projectile>(bulletPrefab, origin, Quaternion.LookRotation(dir));
            dart.Launch(dir, speed, damage, pierce, knockback, lifetime, ricochets, burst, ItemTag.Poison | ItemTag.Balle,
                Synergies.NextSpecialBullet(), poison);
            if (GameFX.Instance) GameFX.Instance.Flash(origin + dir * 0.6f, 0.2f, DartFlash, 0.05f);
        }
    }
}
