using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Precise shots, each bullet at a different nearby enemy. Evolves into a bullet hose that ricochets.
    /// </summary>
    public class RevolverWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public float damage;
            public float cooldown;
            public int bullets;
            public int pierce;
        }

        public GameObject bulletPrefab;
        public float range = 14f;
        public float bulletSpeed = 26f;
        public float knockback = 3f;

        [Header("Evolution: Gatling du Diable")]
        public float evoDamage = 34f;
        public float evoCooldown = 0.1f;
        public int evoPierce = 2;
        public int evoRicochet = 2;

        public List<LevelStats> levels = new()
        {
            new() { text = "Tire sur l'ennemi le plus proche.", damage = 13, cooldown = 0.7f, bullets = 1, pierce = 0 },
            new() { text = "+1 balle (vise un autre ennemi)", damage = 13, cooldown = 0.65f, bullets = 2, pierce = 0 },
            new() { text = "Dégâts +40%, les balles traversent 1 ennemi", damage = 18, cooldown = 0.6f, bullets = 2, pierce = 1 },
            new() { text = "+1 balle, cadence +15%", damage = 18, cooldown = 0.52f, bullets = 3, pierce = 1 },
            new() { text = "+1 balle, dégâts +40%, traverse 2 ennemis", damage = 25, cooldown = 0.42f, bullets = 4, pierce = 2 },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "L'arme de service d'un policier d'Ostrov. Tirs précis, chaque balle vise un ennemi différent.";
        public override string EvolutionName => "Gatling du Diable";
        public override string EvolutionPassive => "cooldown";
        public override string EvolutionText => "Un déluge de balles qui traversent et ricochent.";
        public override ItemTag Tags => ItemTag.Balle;

        float timer;
        int shotIndex;
        readonly List<Enemy> targets = new(8);

        void Update()
        {
            if (Level == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            Vector3 origin = Origin;
            var first = FindNearest(origin, range);
            if (!first)
            {
                timer = 0.1f;
                return;
            }

            if (Evolved) FireGatling(origin);
            else FireVolley(origin, first);
        }

        void FireVolley(Vector3 origin, Enemy first)
        {
            var s = levels[Level - 1];
            timer = s.cooldown * Stats.cooldownMult;

            int bullets = s.bullets + Stats.extraProjectiles;
            targets.Clear();
            targets.Add(first);
            while (targets.Count < bullets)
            {
                var next = FindNearest(origin, range, targets);
                if (!next) break;
                targets.Add(next);
            }

            for (int i = 0; i < bullets; i++)
            {
                var target = targets[i % targets.Count];
                Vector3 dir = Flat(target.transform.position - transform.position);
                if (i >= targets.Count) dir = Quaternion.Euler(0f, (i - targets.Count + 1) * 7f, 0f) * dir;
                Shoot(origin, dir, s.damage, s.pierce);
            }
            Muzzle(origin, Flat(first.transform.position - transform.position), 0.35f);
            SFX.Play(SfxId.Gunshot);
        }

        void FireGatling(Vector3 origin)
        {
            // One bullet at a time: extra projectiles (Chargeur étendu, Pacte du chaos) make the stream faster instead.
            timer = evoCooldown * Stats.cooldownMult / (1f + 0.35f * Stats.extraProjectiles);

            // Rotate between the closest enemies so the stream sprays the whole crowd.
            targets.Clear();
            int pool = 3 + Stats.extraProjectiles;
            while (targets.Count < pool)
            {
                var next = FindNearest(origin, range, targets);
                if (!next) break;
                targets.Add(next);
            }
            var target = targets[shotIndex++ % targets.Count];
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f) * Flat(target.transform.position - transform.position);
            Shoot(origin, dir, evoDamage, evoPierce, evoRicochet);
            Muzzle(origin, dir, 0.3f);
            SFX.Play(SfxId.Gunshot, 0.6f, 1.25f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.01f);
        }

        void Shoot(Vector3 origin, Vector3 dir, float damage, int pierce, int ricochet = 0)
        {
            var bullet = Pool.Instance.Spawn<Projectile>(bulletPrefab, origin, Quaternion.LookRotation(dir));
            bullet.Launch(dir, bulletSpeed, damage * Stats.damageMult, pierce, knockback, range / bulletSpeed + 0.2f,
                ricochet + Stats.ricochets + Synergies.ExtraRicochets, 0f, ItemTag.Balle, Synergies.NextSpecialBullet());
        }

        void Muzzle(Vector3 origin, Vector3 dir, float size)
        {
            if (!GameFX.Instance) return;
            Color c = Evolved ? new Color(3f, 0.6f, 0.2f) : new Color(3f, 2.2f, 0.9f);
            GameFX.Instance.Flash(origin + dir * 0.7f, size, c, 0.06f);
        }
    }
}
