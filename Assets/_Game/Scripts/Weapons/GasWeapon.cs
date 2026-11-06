using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Toxic cloud that hurts, slows and poisons every enemy around the player. Evolves into a plague that heals you.
    /// Poisoned enemies take +10% damage, conduct lightning, and explode when set on fire (Synergies).
    /// </summary>
    public class GasWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            [Tooltip("Damage per tick.")]
            public float damage;
            public float radius;
            [Tooltip("Seconds between ticks.")]
            public float tick;
        }

        [Tooltip("Flat disc showing the cloud's area.")]
        public Transform visual;
        public float slowFactor = 0.75f;

        [Header("Evolution: Peste noire")]
        public float evoDamage = 24f;
        public float evoRadius = 5f;
        public float evoTick = 0.3f;
        public float evoSlow = 0.45f;
        [Tooltip("HP healed per enemy hit per tick (capped by evoHealCap).")]
        public float evoHealPerEnemy = 0.12f;
        [Tooltip("Max HP healed per tick (was 3: ~10 HP/s kept the player full).")]
        public float evoHealCap = 1f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Un nuage toxique empoisonne et ralentit les ennemis proches.", damage = 6, radius = 2.2f, tick = 0.5f },
            new() { text = "Zone plus large", damage = 6, radius = 2.7f, tick = 0.5f },
            new() { text = "Dégâts +65%", damage = 10, radius = 2.7f, tick = 0.45f },
            new() { text = "Zone et cadence augmentées", damage = 10, radius = 3.2f, tick = 0.4f },
            new() { text = "Nuage mortel : dégâts +50%, zone +15%", damage = 15, radius = 3.7f, tick = 0.35f },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Aura toxique : tout ce qui t'entoure fond.";
        public override string EvolutionName => "Peste noire";
        public override string EvolutionPassive => "regen";
        public override string EvolutionText => "Nuage géant qui englue les ennemis et te soigne pour chaque victime.";
        public override ItemTag Tags => ItemTag.Poison;

        float timer, pulse;
        static readonly Color GasColor = new(0.6f, 2f, 0.3f);
        static readonly Color PlagueColor = new(1.4f, 0.4f, 2f);

        protected override void Awake()
        {
            base.Awake();
            if (visual) visual.gameObject.SetActive(false);
        }

        protected override void OnLevelChanged()
        {
            if (!visual) return;
            visual.gameObject.SetActive(true);
            if (Evolved && visual.TryGetComponent<Renderer>(out var r))
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(0.5f, 0.15f, 0.7f, 0.3f));
                r.SetPropertyBlock(block);
            }
        }

        void Update()
        {
            if (Level == 0) return;

            var s = levels[Level - 1];
            float radius = (Evolved ? evoRadius : s.radius) * Stats.areaMult;

            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 4f);
            if (visual)
            {
                float breathe = 1f + Mathf.Sin(Time.time * 3f) * 0.03f + pulse * 0.08f;
                visual.SetPositionAndRotation(new Vector3(transform.position.x, 0.05f, transform.position.z), Quaternion.identity);
                visual.localScale = new Vector3(radius * 2f * breathe, 0.01f, radius * 2f * breathe);
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = (Evolved ? evoTick : s.tick) * Stats.cooldownMult;
            pulse = 1f;

            Vector3 center = transform.position;
            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            float slow = Evolved ? evoSlow : slowFactor;
            int hits = 0;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var enemy = list[i];
                Vector3 d = enemy.transform.position - center;
                d.y = 0f;
                float r = radius + enemy.Radius;
                if (d.sqrMagnitude > r * r) continue;

                hits++;
                Synergies.Slow(enemy, slow, 0.6f * Stats.durationMult);
                // Light poison while inside (and after leaving, with Alchimiste).
                Synergies.Poison(enemy, damage * 0.5f, 1f * Stats.durationMult);
                Synergies.Hit(enemy, damage, d, 0.5f, ItemTag.Poison);
            }

            if (Evolved && hits > 0) PlayerHealth.Instance.Heal(Mathf.Min(evoHealCap, hits * evoHealPerEnemy));
            if (GameFX.Instance) GameFX.Instance.Embers(center, radius * 0.8f, Evolved ? 5 : 3, Evolved ? PlagueColor : GasColor);
        }
    }
}
