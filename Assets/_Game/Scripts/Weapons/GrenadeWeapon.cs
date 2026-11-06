using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Frag grenades lobbed into the thickest crowd: big explosion and knockback.
    /// Evolves into cluster bombs that burst into bomblets.
    /// </summary>
    public class GrenadeWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public int grenades;
            public float damage;
            public float radius;
            public float cooldown;
        }

        public GameObject grenadePrefab;
        public float range = 10f;
        public float flightTime = 0.6f;
        public float knockback = 10f;

        [Header("Evolution: Bombe à sous-munitions")]
        public int evoGrenades = 3;
        public float evoDamage = 75f;
        public float evoRadius = 3.4f;
        public float evoCooldown = 1.6f;
        public int evoBomblets = 5;
        public float evoBombletDamage = 0.45f;
        public float evoBombletRadius = 1.9f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Grenade lancée au cœur de la foule.", grenades = 1, damage = 26, radius = 2.3f, cooldown = 2.3f },
            new() { text = "Dégâts +40%, explosion plus large", grenades = 1, damage = 36, radius = 2.6f, cooldown = 2.1f },
            new() { text = "+1 grenade", grenades = 2, damage = 38, radius = 2.7f, cooldown = 1.95f },
            new() { text = "Dégâts +35%, recharge rapide", grenades = 2, damage = 52, radius = 2.9f, cooldown = 1.8f },
            new() { text = "+1 grenade, dégâts +25%, zone +15%", grenades = 3, damage = 65, radius = 3.3f, cooldown = 1.6f },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Explosions dévastatrices là où la foule est dense.";
        public override string EvolutionName => "Bombe à sous-munitions";
        public override string EvolutionPassive => "corpse";
        public override string EvolutionText => "Chaque explosion projette 5 mini-bombes. Réaction en chaîne.";
        public override ItemTag Tags => ItemTag.Explosif;

        float timer;

        void Update()
        {
            if (Level == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            if (!FindNearest(transform.position, range))
            {
                timer = 0.1f;
                return;
            }

            var s = levels[Level - 1];
            timer = (Evolved ? evoCooldown : s.cooldown) * Stats.cooldownMult;
            int count = (Evolved ? evoGrenades : s.grenades) + Stats.extraProjectiles;
            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            float radius = (Evolved ? evoRadius : s.radius) * Stats.areaMult;

            Vector3 from = transform.position + Vector3.up * 1.2f;
            SFX.Play(SfxId.Throw, 1f, 0.9f);
            for (int i = 0; i < count; i++)
            {
                var target = i == 0 ? FindCrowd(transform.position, range, radius) : FindRandom(transform.position, range);
                Vector3 to = target ? target.transform.position : transform.position + Random.insideUnitSphere.WithY(0f) * 5f;
                to.y = 0.1f;

                var g = Pool.Instance.Spawn<MolotovBottle>(grenadePrefab, from, Quaternion.identity);
                g.transform.localScale = Vector3.one;
                g.Throw(from, to, flightTime + i * 0.08f, landed => Boom(landed, damage, radius, true));
            }
        }

        void Boom(Vector3 at, float damage, float radius, bool main)
        {
            Synergies.Explosion(at, radius, damage, main ? knockback : knockback * 0.5f, Combat.ExplosionColor, ItemTag.Explosif);
            if (GameFX.Instance) GameFX.Instance.Splat(at, radius * 0.5f);
            if (!main || !Evolved) return;

            for (int i = 0; i < evoBomblets; i++)
            {
                float a = (i + Random.value * 0.5f) / evoBomblets * Mathf.PI * 2f;
                Vector3 to = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(2.2f, 4f) * Stats.areaMult;
                to.y = 0.1f;
                var b = Pool.Instance.Spawn<MolotovBottle>(grenadePrefab, at + Vector3.up * 0.5f, Quaternion.identity);
                b.transform.localScale = Vector3.one * 0.55f;
                float bombletDamage = damage * evoBombletDamage;
                float bombletRadius = evoBombletRadius * Stats.areaMult;
                b.Throw(at + Vector3.up * 0.5f, to, Random.Range(0.35f, 0.5f), landed => Boom(landed, bombletDamage, bombletRadius, false));
            }
        }
    }
}
