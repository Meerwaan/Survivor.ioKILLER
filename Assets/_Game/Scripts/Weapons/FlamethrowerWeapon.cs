using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Short cone of fire aimed at the nearest enemies: burns everything in front of the player.
    /// The Pyromane core weapon (with the Molotov). Evolves into three jets spinning around the player.
    /// </summary>
    public class FlamethrowerWeapon : Weapon
    {
        [System.Serializable]
        public class LevelStats
        {
            public string text;
            [Tooltip("Damage per tick to every enemy in the cone.")]
            public float damage;
            public float range;
            [Tooltip("Full cone angle in degrees.")]
            public float angle;
            public int jets;
        }

        [Tooltip("Visual blob (FlamePuff). Optional.")]
        public GameObject puffPrefab;
        public float tick = 0.2f;
        public float knockback = 0.8f;
        [Tooltip("Burn dps applied, as a multiple of the tick damage.")]
        public float burnFactor = 1.2f;

        [Header("Evolution: Souffle du dragon")]
        public float evoDamage = 14f;
        public float evoRange = 6.5f;
        public float evoAngle = 60f;
        public int evoJets = 3;
        [Tooltip("Degrees per second.")]
        public float evoSpin = 150f;

        public List<LevelStats> levels = new()
        {
            new() { text = "Un jet de flammes qui embrase tout devant toi.", damage = 4.5f, range = 4f, angle = 50, jets = 1 },
            new() { text = "Jet plus long, dégâts +20%", damage = 5.5f, range = 4.6f, angle = 50, jets = 1 },
            new() { text = "+1 jet (vise un autre ennemi)", damage = 5.5f, range = 4.6f, angle = 50, jets = 2 },
            new() { text = "Dégâts +45%, cône plus large", damage = 8f, range = 5f, angle = 60, jets = 2 },
            new() { text = "Dégâts +40%, jet plus long", damage = 11, range = 5.6f, angle = 60, jets = 2 },
        };

        public override int MaxLevel => levels.Count;
        public override string DescribeLevel(int level) => levels[level - 1].text;
        public override string Summary => "Désherbeur thermique du Combinat, bricolé au garage. Courte portée, embrase les foules.";
        public override string EvolutionName => "Souffle du dragon";
        public override string EvolutionPassive => "huile";
        public override string EvolutionText => "3 jets de feu tournent autour de toi et calcinent tout.";
        public override ItemTag Tags => ItemTag.Feu;

        static readonly Color NozzleColor = new(3f, 1.3f, 0.3f);
        static readonly Color EmberColor = new(2.5f, 0.9f, 0.15f);
        const float PuffLife = 0.32f;
        const float PuffInterval = 0.04f;
        const int MaxJets = 8;

        readonly Vector3[] aims = new Vector3[MaxJets];
        readonly List<Enemy> targets = new(MaxJets);
        int aimCount;
        float timer, puffTimer, spin, firingUntil;

        void Update()
        {
            if (Level == 0) return;

            var s = levels[Level - 1];
            float dt = Time.deltaTime;
            float range = (Evolved ? evoRange : s.range) * Stats.areaMult;
            float angle = Evolved ? evoAngle : s.angle;
            int jets = Mathf.Min(MaxJets, (Evolved ? evoJets : s.jets) + Stats.extraProjectiles);
            Vector3 me = transform.position;

            if (Evolved)
            {
                // Dragon breath: jets spin around the player, but only while something is close.
                spin += evoSpin * dt;
                aimCount = jets;
                for (int j = 0; j < jets; j++)
                    aims[j] = Quaternion.Euler(0f, spin + 360f * j / jets, 0f) * Vector3.forward;
            }

            puffTimer -= dt;
            if (puffTimer <= 0f && Time.time < firingUntil)
            {
                puffTimer = PuffInterval;
                SpawnPuffs(me, range, angle);
            }

            timer -= dt;
            if (timer > 0f) return;

            if (Evolved)
            {
                if (!FindNearest(me, range + 1f))
                {
                    timer = 0.1f;
                    return;
                }
            }
            else if (!AimAtNearest(me, range + 1.5f, jets))
            {
                timer = 0.1f;
                return;
            }

            timer = tick * Stats.cooldownMult;
            firingUntil = Time.time + timer + 0.1f;

            float damage = (Evolved ? evoDamage : s.damage) * Stats.damageMult;
            float cosHalf = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            for (int j = 0; j < aimCount; j++) Burn(me, aims[j], range, cosHalf, damage);
            SFX.Play(SfxId.Ignite, 0.3f, Evolved ? 0.8f : 1.2f);
        }

        /// <summary>Points one jet at each of the nearest enemies. False if nothing is in range.</summary>
        bool AimAtNearest(Vector3 me, float range, int jets)
        {
            targets.Clear();
            while (targets.Count < jets)
            {
                var e = FindNearest(me, range, targets);
                if (!e) break;
                targets.Add(e);
            }
            if (targets.Count == 0) return false;

            aimCount = jets;
            for (int j = 0; j < jets; j++)
            {
                Vector3 dir = Flat(targets[j % targets.Count].transform.position - me);
                // Extra jets with no target of their own fan out a little.
                if (j >= targets.Count) dir = Quaternion.Euler(0f, (j - targets.Count + 1) * 35f, 0f) * dir;
                aims[j] = dir;
            }
            return true;
        }

        void Burn(Vector3 me, Vector3 aim, float range, float cosHalf, float damage)
        {
            float burnDps = damage * burnFactor;
            float burnTime = 2f * Stats.durationMult;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 d = e.transform.position - me;
                d.y = 0f;
                float r = range + e.Radius;
                float sqr = d.sqrMagnitude;
                if (sqr > r * r) continue;
                // Inside the cone (anything touching the player always burns).
                if (sqr > 1f && Vector3.Dot(aim, d) < cosHalf * Mathf.Sqrt(sqr)) continue;

                Synergies.Ignite(e, burnDps, burnTime);
                Synergies.Hit(e, damage, d, knockback, ItemTag.Feu);
            }

            if (GameFX.Instance && Random.value < 0.5f)
                GameFX.Instance.Embers(me + aim * range * 0.7f, range * 0.25f, 2, EmberColor);
        }

        void SpawnPuffs(Vector3 me, float range, float angle)
        {
            if (!puffPrefab) return;
            Vector3 nozzle = me + Vector3.up * 0.9f;
            float speed = range / PuffLife * 0.95f;
            float size = 0.55f + range * 0.12f;
            for (int j = 0; j < aimCount; j++)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-angle, angle) * 0.4f, 0f) * aims[j];
                var puff = Pool.Instance.Spawn<FlamePuff>(puffPrefab, nozzle + aims[j] * 0.5f, Quaternion.identity);
                if (puff) puff.Launch(dir * speed, PuffLife * Random.Range(0.85f, 1.1f), size * Random.Range(0.8f, 1.2f));
            }
            if (GameFX.Instance && Random.value < 0.3f)
                GameFX.Instance.Flash(nozzle + aims[0] * 0.6f, 0.35f, NozzleColor, 0.06f);
        }
    }
}
