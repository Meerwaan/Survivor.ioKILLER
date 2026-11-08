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

        [Tooltip("Root of the cloud visual: soft cloud layers (\"Cloud*\", slowly counter-rotating) and a thin \"Edge\" ring.")]
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
        public override string Summary => "Neutralisant REGEN : censé dissoudre l'ambre, il empoisonne tout le reste. Aura toxique autour de toi.";
        public override string EvolutionName => "Peste noire";
        public override string EvolutionPassive => "regen";
        public override string EvolutionText => "Nuage géant qui englue les ennemis et te soigne un peu pour chaque ennemi dedans.";
        public override ItemTag Tags => ItemTag.Poison;

        float timer, pulse, spin;

        // Toxic #A6D23C fading into dark olive; the Peste noire is darker, almost black-green (never violet).
        static readonly Color CloudColor = new(0.5f, 0.62f, 0.17f, 0.3f);
        static readonly Color CloudColorB = new(0.36f, 0.45f, 0.12f, 0.24f);
        static readonly Color PlagueCloud = new(0.17f, 0.23f, 0.07f, 0.42f);
        static readonly Color PlagueCloudB = new(0.12f, 0.16f, 0.05f, 0.34f);
        static readonly Color EdgeColor = new(0.65f, 0.82f, 0.24f, 0.32f);
        static readonly Color PlagueEdge = new(0.42f, 0.55f, 0.14f, 0.3f);
        static readonly Color PuffColor = new(0.42f, 0.52f, 0.15f, 0.2f);
        static readonly Color PlaguePuff = new(0.13f, 0.17f, 0.06f, 0.3f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] layers;
        Transform[] spinners;
        MaterialPropertyBlock block;

        protected override void Awake()
        {
            base.Awake();
            if (!visual) return;
            visual.gameObject.SetActive(false);
            block = new MaterialPropertyBlock();
            layers = visual.GetComponentsInChildren<Renderer>(true);
            int n = 0;
            foreach (var r in layers) if (r.transform != visual && r.name.StartsWith("Cloud")) n++;
            spinners = new Transform[n];
            n = 0;
            foreach (var r in layers) if (r.transform != visual && r.name.StartsWith("Cloud")) spinners[n++] = r.transform;
        }

        protected override void OnLevelChanged()
        {
            if (!visual) return;
            visual.gameObject.SetActive(true);
        }

        /// <summary>Tints every layer (old single-disc visual included), a little denser right after a tick.</summary>
        void PaintCloud()
        {
            if (layers == null) return;
            float boost = 1f + pulse * 0.25f;
            foreach (var r in layers)
            {
                if (!r) continue;
                Color c = r.name.StartsWith("Edge") ? (Evolved ? PlagueEdge : EdgeColor)
                    : r.name.EndsWith("B") ? (Evolved ? PlagueCloudB : CloudColorB)
                    : (Evolved ? PlagueCloud : CloudColor);
                c.a = Mathf.Clamp01(c.a * boost);
                block.SetColor(BaseColorId, c);
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
                float breathe = 1f + Mathf.Sin(Time.time * 3f) * 0.03f + pulse * 0.05f;
                visual.SetPositionAndRotation(new Vector3(transform.position.x, 0.05f, transform.position.z), Quaternion.identity);
                visual.localScale = new Vector3(radius * 2f * breathe, 0.01f, radius * 2f * breathe);
                // Two cloud layers turning slowly in opposite directions: the gas swirls instead of sitting still.
                spin += Time.deltaTime * 14f;
                for (int i = 0; i < spinners.Length; i++)
                    if (spinners[i]) spinners[i].localRotation = Quaternion.Euler(90f, (i % 2 == 0 ? spin : -spin * 0.7f) + i * 67f, 0f);
                PaintCloud();
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
            // A few soft puffs drifting low over the cloud.
            if (GameFX.Instance)
                GameFX.Instance.Smoke(center, radius * 0.75f, Evolved ? 3 : 2, 1.1f + radius * 0.15f, Evolved ? PlaguePuff : PuffColor, 0.12f);
        }
    }
}
