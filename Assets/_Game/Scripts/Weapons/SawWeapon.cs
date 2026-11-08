using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Circular saw blades thrown like boomerangs: they slice through everything on the way out and back.
    /// Evolves into a star of huge saws that always crit.
    /// </summary>
    public class SawWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public int saws;
            public float damage;
            public float range;
            public float size;
            public float cooldown;
        }

        public GameObject sawPrefab;
        public float speed = 15f;
        public float knockback = 4f;

        [Header("Evolution: Scie sanglante")]
        public int evoSaws = 5;
        public float evoDamage = 32f;
        public float evoRange = 10f;
        public float evoSize = 1.5f;
        public float evoCooldown = 1.1f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Une scie qui part et revient en tranchant tout.", saws = 1, damage = 14, range = 7f, size = 0.7f, cooldown = 1.55f },
            new() { text = "+1 scie", saws = 2, damage = 14, range = 7f, size = 0.7f, cooldown = 1.5f },
            new() { text = "Dégâts +45%, scies plus grandes", saws = 2, damage = 20, range = 8f, size = 0.85f, cooldown = 1.35f },
            new() { text = "+1 scie, recharge rapide", saws = 3, damage = 21, range = 8f, size = 0.85f, cooldown = 1.2f },
            new() { text = "Dégâts +40%, portée et taille max", saws = 3, damage = 30, range = 9f, size = 1f, cooldown = 1.1f },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Lames du Combinat, lancées par un moteur de meuleuse. Elles traversent des lignes entières.";
        public override string EvolutionName => "Scie sanglante";
        public override string EvolutionPassive => "crit";
        public override string EvolutionText => "5 scies géantes en étoile. Chaque coup est critique.";
        public override ItemTag Tags => ItemTag.Lame;

        float timer;
        float starAngle;

        void Update()
        {
            if (Level == 0) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            var s = levels[Level - 1];
            float range = Evolved ? evoRange : s.range;
            var target = FindNearest(transform.position, range + 2f);
            if (!target && !Evolved)
            {
                timer = 0.1f;
                return;
            }

            timer = (Evolved ? evoCooldown : s.cooldown) * Stats.cooldownMult;
            int count = (Evolved ? evoSaws : s.saws) + Stats.extraProjectiles;
            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            float size = (Evolved ? evoSize : s.size) * Stats.areaMult;
            Vector3 aim = target ? Flat(target.transform.position - transform.position) : transform.forward;

            starAngle += 36f;
            SFX.Play(SfxId.Saw, 1f, Evolved ? 0.8f : 1f);
            for (int i = 0; i < count; i++)
            {
                float angle = Evolved ? starAngle + 360f * i / count : (i - (count - 1) / 2f) * 25f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * aim;
                var saw = Pool.Instance.Spawn<SawBlade>(sawPrefab, Origin, Quaternion.identity);
                saw.Launch(this, dir, damage, range * Stats.durationMult, speed, size, knockback, Evolved);
            }
        }
    }
}
