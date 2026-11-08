using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Throws bottles at crowds; each one bursts (small explosion) and sets the ground on fire. Evolves into a ring of napalm.
    /// Counts as Feu and Explosif: the bridge between Pyromane and Artificier builds.
    /// </summary>
    public class MolotovWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public int bottles;
            [Tooltip("Fire damage per tick.")]
            public float damage;
            public float radius;
            public float duration;
            public float cooldown;
        }

        public GameObject bottlePrefab;
        public GameObject firePrefab;
        public float range = 9f;
        public float flightTime = 0.55f;
        public float fireTick = 0.35f;
        [Tooltip("Impact burst damage, as a multiple of the fire tick damage.")]
        public float impactDamage = 1.5f;

        [Header("Evolution: Napalm")]
        public int evoBottles = 8;
        public float evoDamage = 22f;
        public float evoRadius = 3f;
        public float evoDuration = 6f;
        public float evoCooldown = 2.4f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Lance un cocktail qui embrase le sol et les ennemis.", bottles = 1, damage = 8, radius = 1.8f, duration = 3f, cooldown = 2.5f },
            new() { text = "+1 bouteille", bottles = 2, damage = 8, radius = 1.8f, duration = 3f, cooldown = 2.4f },
            new() { text = "Feu plus large, plus long, dégâts +40%", bottles = 2, damage = 11, radius = 2.1f, duration = 3.5f, cooldown = 2.2f },
            new() { text = "+1 bouteille, recharge rapide", bottles = 3, damage = 11, radius = 2.2f, duration = 3.5f, cooldown = 2.0f },
            new() { text = "+1 bouteille, dégâts +25%, feu géant", bottles = 4, damage = 14, radius = 2.5f, duration = 4f, cooldown = 1.9f },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "La recette des Charognards : rien ne fait mieux fondre la croûte. Bouteilles lancées sur les foules.";
        public override string EvolutionName => "Napalm";
        public override string EvolutionPassive => "duration";
        public override string EvolutionText => "8 bouteilles en cercle autour de toi : un mur de flammes.";
        public override ItemTag Tags => ItemTag.Feu | ItemTag.Explosif;

        float timer;
        float ringAngle;

        void Update()
        {
            if (Level == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            var s = levels[Level - 1];
            timer = (Evolved ? evoCooldown : s.cooldown) * Stats.cooldownMult;

            Vector3 from = transform.position + Vector3.up * 1.2f;
            int bottles = (Evolved ? evoBottles : s.bottles) + Stats.extraProjectiles;
            float radius = (Evolved ? evoRadius : s.radius) * Stats.areaMult;
            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            float duration = (Evolved ? evoDuration : s.duration) * Stats.durationMult;

            ringAngle += 23f;
            SFX.Play(SfxId.Throw);
            for (int i = 0; i < bottles; i++)
            {
                Vector3 to;
                if (Evolved)
                {
                    float a = (ringAngle + 360f * i / bottles) * Mathf.Deg2Rad;
                    float dist = i % 2 == 0 ? 4.5f : 7f;
                    to = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
                    to.y = 0.05f;
                }
                else
                {
                    to = PickTarget(i);
                }

                var bottle = Pool.Instance.Spawn<MolotovBottle>(bottlePrefab, from, Quaternion.identity);
                bottle.Throw(from, to, flightTime + i * 0.04f, landed =>
                {
                    // Glass bursts: small fiery explosion (Artificier, fusion), then the pool of fire.
                    Synergies.Explosion(landed, radius * 0.6f, damage * impactDamage, 3f, Synergies.FireColor, ItemTag.Feu);
                    if (GameFX.Instance) GameFX.Instance.Sparks(landed + Vector3.up * 0.4f, new Color(2.5f, 1.2f, 0.3f), 10);
                    SFX.Play(SfxId.Ignite, 0.7f);
                    var fire = Pool.Instance.Spawn<FirePool>(firePrefab, landed, Quaternion.identity);
                    fire.Ignite(damage, radius, duration, fireTick);
                });
            }
        }

        /// <summary>The first bottle goes where the crowd is, the others at random enemies or spots nearby.</summary>
        Vector3 PickTarget(int index)
        {
            Vector3 me = transform.position;
            var e = index == 0 ? FindCrowd(me, range, 2.5f) : FindRandom(me, range);
            if (e) return e.transform.position.WithY(0.05f);
            Vector2 r = Random.insideUnitCircle * range * 0.6f;
            return new Vector3(me.x + r.x, 0.05f, me.z + r.y);
        }
    }
}
