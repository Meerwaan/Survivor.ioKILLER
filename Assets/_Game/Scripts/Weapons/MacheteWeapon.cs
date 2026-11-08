using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>Blades spinning around the player. Evolves into a pulsing cyclone of cleavers.</summary>
    public class MacheteWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            public float damage;
            public int blades;
            public float radius;
            [Tooltip("Degrees per second.")]
            public float spinSpeed;
        }

        public GameObject bladePrefab;
        [Tooltip("Seconds before the same enemy can be hit again.")]
        public float hitCooldown = 0.45f;
        public float knockback = 7f;
        public float bladeHitRadius = 0.7f;

        [Header("Evolution: Hachoir cyclone")]
        public float evoDamage = 34f;
        public int evoBlades = 8;
        public float evoMinRadius = 2.2f;
        public float evoMaxRadius = 5f;
        public float evoSpin = 380f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Deux machettes tournent autour de toi.", damage = 10, blades = 2, radius = 2.2f, spinSpeed = 230 },
            new() { text = "+1 machette", damage = 11, blades = 3, radius = 2.3f, spinSpeed = 240 },
            new() { text = "Dégâts +45%, zone plus large", damage = 16, blades = 3, radius = 2.5f, spinSpeed = 260 },
            new() { text = "+1 machette, rotation rapide", damage = 17, blades = 4, radius = 2.7f, spinSpeed = 290 },
            new() { text = "+1 machette, dégâts +40%", damage = 24, blades = 5, radius = 3.0f, spinSpeed = 310 },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Outils de débroussaillage de la voirie. Lames en orbite qui hachent tout ce qui approche.";
        public override string EvolutionName => "Hachoir cyclone";
        public override string EvolutionPassive => "area";
        public override string EvolutionText => "8 hachoirs qui s'écartent et reviennent en tornade.";
        public override ItemTag Tags => ItemTag.Lame;

        readonly List<Transform> blades = new();
        readonly Dictionary<Enemy, float> nextHit = new();
        float angle;

        protected override void OnLevelChanged()
        {
            if (!Evolved) return;
            foreach (var b in blades) b.localScale = Vector3.one * 1.3f;
        }

        void Update()
        {
            if (Level == 0) return;

            var s = levels[Level - 1];
            int count = (Evolved ? evoBlades : s.blades) + Stats.extraProjectiles;
            while (blades.Count < count)
            {
                var b = Instantiate(bladePrefab, transform).transform;
                if (Evolved) b.localScale = Vector3.one * 1.3f;
                blades.Add(b);
            }

            float spin = Evolved ? evoSpin : s.spinSpeed;
            angle += spin * Time.deltaTime;
            float radius;
            if (Evolved)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
                radius = Mathf.Lerp(evoMinRadius, evoMaxRadius, wave) * Stats.areaMult;
            }
            else
            {
                radius = s.radius * Stats.areaMult;
            }
            Vector3 center = transform.position + Vector3.up * 0.8f;

            for (int b = 0; b < blades.Count; b++)
            {
                float a = (angle + 360f * b / blades.Count) * Mathf.Deg2Rad;
                Vector3 outward = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // Blades point along the swing so the trail reads as a slash.
                Vector3 tangent = new(-outward.z, 0f, outward.x);
                blades[b].SetPositionAndRotation(center + outward * radius, Quaternion.LookRotation(tangent));
            }

            float now = Time.time;
            float hitR = bladeHitRadius * Stats.areaMult * (Evolved ? 1.3f : 1f);
            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            // Adrénaline ("armes plus rapides") speeds up every weapon, blades included.
            float cooldown = (Evolved ? hitCooldown * 0.65f : hitCooldown) * Stats.cooldownMult;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var enemy = list[i];
                if (nextHit.TryGetValue(enemy, out float t) && now < t) continue;

                Vector3 ep = enemy.transform.position;
                float r = enemy.Radius + hitR;
                for (int b = 0; b < blades.Count; b++)
                {
                    Vector3 d = ep - blades[b].position;
                    d.y = 0f;
                    if (d.sqrMagnitude > r * r) continue;

                    nextHit[enemy] = now + cooldown;
                    Synergies.Hit(enemy, damage, ep - center, knockback, ItemTag.Lame);
                    SFX.Play(SfxId.Slash, 0.7f, Evolved ? 0.85f : 1f);
                    if (GameFX.Instance) GameFX.Instance.Sparks(ep + Vector3.up, new Color(2f, 2f, 2.2f), 2);
                    break;
                }
            }

            // Forget dead/recycled enemies now and then so the dictionary doesn't grow forever.
            if (nextHit.Count > 600) nextHit.Clear();
        }
    }
}
