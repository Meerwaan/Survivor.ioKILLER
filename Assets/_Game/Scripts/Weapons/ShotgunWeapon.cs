using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>Short-range cone of pellets with heavy knockback. Evolves into exploding meat-cannon shells.</summary>
    public class ShotgunWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public float damage;
            public int pellets;
            public float cooldown;
            public float knockback;
            public int pierce;
        }

        public GameObject pelletPrefab;
        public float range = 7.5f;
        public float pelletSpeed = 22f;
        public float spreadDegrees = 55f;

        [Header("Evolution: Canon à viande")]
        public float evoDamage = 24f;
        public int evoPellets = 14;
        public float evoCooldown = 0.7f;
        public float evoExplodeRadius = 1.4f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Rafale de plombs en cône, gros recul.", damage = 8, pellets = 6, cooldown = 1.25f, knockback = 7, pierce = 0 },
            new() { text = "+2 plombs", damage = 8, pellets = 8, cooldown = 1.15f, knockback = 7, pierce = 0 },
            new() { text = "Dégâts +50%, les plombs traversent 1 ennemi", damage = 12, pellets = 8, cooldown = 1.0f, knockback = 8, pierce = 1 },
            new() { text = "+2 plombs, recharge rapide", damage = 13, pellets = 10, cooldown = 0.9f, knockback = 8, pierce = 1 },
            new() { text = "+2 plombs, dégâts +40%, traverse 2 ennemis", damage = 18, pellets = 12, cooldown = 0.75f, knockback = 10, pierce = 2 },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Cône de plombs à bout portant, repousse tout.";
        public override string EvolutionName => "Canon à viande";
        public override string EvolutionPassive => "damage";
        public override string EvolutionText => "Chaque plomb explose à l'impact. Plus rien ne t'approche.";
        public override ItemTag Tags => ItemTag.Balle;

        float timer;

        void Update()
        {
            if (Level == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            Vector3 origin = Origin;
            var target = FindNearest(origin, range);
            if (!target)
            {
                timer = 0.1f;
                return;
            }

            var s = levels[Level - 1];
            float damage = Evolved ? evoDamage : s.damage;
            int pellets = (Evolved ? evoPellets : s.pellets) + 2 * Stats.extraProjectiles;
            timer = (Evolved ? evoCooldown : s.cooldown) * Stats.cooldownMult;
            float explode = Evolved ? evoExplodeRadius * Stats.areaMult : 0f;
            int pierce = Evolved ? 1 : s.pierce;
            int ricochets = Stats.ricochets + Synergies.ExtraRicochets;

            Vector3 aim = Flat(target.transform.position - transform.position);
            float lifetime = range / pelletSpeed;
            for (int i = 0; i < pellets; i++)
            {
                float t = pellets > 1 ? i / (pellets - 1f) : 0.5f;
                float angle = Mathf.Lerp(-spreadDegrees / 2f, spreadDegrees / 2f, t) + Random.Range(-3f, 3f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * aim;
                float speed = pelletSpeed * Random.Range(0.85f, 1.1f);
                var pellet = Pool.Instance.Spawn<Projectile>(pelletPrefab, origin, Quaternion.LookRotation(dir));
                pellet.Launch(dir, speed, damage * Stats.damageMult, pierce, s.knockback, lifetime, ricochets, explode,
                    ItemTag.Balle, Synergies.NextSpecialBullet());
            }

            if (GameFX.Instance)
            {
                GameFX.Instance.Flash(origin + aim * 0.8f, Evolved ? 0.9f : 0.6f, new Color(3f, 1.6f, 0.5f), 0.08f);
                GameFX.Instance.Sparks(origin + aim * 0.8f, new Color(2f, 1.4f, 0.5f), 8, 1.2f);
            }
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(Evolved ? 0.1f : 0.06f);
            SFX.Play(SfxId.Shotgun, 1f, Evolved ? 0.85f : 1f);
        }
    }
}
