using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Burning patch of ground left by a Molotov. Sets enemies standing in it on fire (scaled by Huile / Pyromane).
    /// Look: soft flickering flame layers on the ground (children "Flames*"), little tongues of fire and smoke rising.
    /// </summary>
    public class FirePool : MonoBehaviour
    {
        float damage, radius, life, tick, timer, age;
        float flameBudget, smokeBudget, spin;
        /// <summary>Hit candidates scratch. Shared: only used inside Update, which never runs nested.</summary>
        static readonly List<Enemy> Candidates = new(32);

        static readonly Color EmberColor = new(2.5f, 0.9f, 0.15f);
        static readonly Color GroundFire = new(1.3f, 0.55f, 0.16f, 0.7f);
        static readonly Color FireSmoke = new(0.227f, 0.227f, 0.227f, 0.45f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] layers;
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            var all = GetComponentsInChildren<Renderer>(true);
            int n = 0;
            foreach (var r in all) if (r.transform != transform) n++;
            layers = new Renderer[n];
            n = 0;
            foreach (var r in all) if (r.transform != transform) layers[n++] = r;
        }

        public void Ignite(float damage, float radius, float duration, float tick)
        {
            this.damage = damage;
            this.radius = radius;
            this.tick = tick;
            life = duration;
            timer = 0f;
            age = 0f;
            flameBudget = smokeBudget = 0f;
            spin = Random.Range(0f, 360f);
            Resize(0.2f);
        }

        void Resize(float k) => transform.localScale = new Vector3(radius * 2f * k, 0.01f, radius * 2f * k);

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            age += dt;
            if (life <= 0f)
            {
                Pool.Instance.Despawn(gameObject);
                return;
            }

            // Bursts open, flickers, then shrinks during the last half second.
            float open = Mathf.Min(1f, 0.2f + age * 6f);
            float flicker = 1f + Mathf.Sin(Time.time * 25f + transform.position.x) * 0.04f;
            Resize(Mathf.Min(open, life * 2f) * flicker);
            AnimateFire(dt);

            timer -= dt;
            if (timer > 0f) return;
            timer = tick;

            Vector3 center = transform.position;
            if (GameFX.Instance) GameFX.Instance.Embers(center, radius * 0.8f, 1 + Mathf.RoundToInt(radius * 0.5f), EmberColor);

            // Only the enemies around the pool (snapshot of this frame, same order as walking Enemy.Active backwards).
            var list = Candidates;
            EnemyGrid.Query(center, radius, list);
            for (int i = 0; i < list.Count; i++)
            {
                var enemy = list[i];
                if (!EnemyGrid.IsLive(enemy)) continue; // died or got recycled earlier this frame
                Vector3 d = enemy.transform.position - center;
                d.y = 0f;
                float r = radius + enemy.Radius;
                if (d.sqrMagnitude > r * r) continue;

                Synergies.Ignite(enemy, damage * 0.8f, 1.5f);
                Synergies.Hit(enemy, damage, Vector3.zero, 0f, ItemTag.Feu);
            }
        }

        /// <summary>Fire is always animated, always with smoke (Univers 9.4).</summary>
        void AnimateFire(float dt)
        {
            if (dt <= 0f) return;
            float t = Time.time + transform.position.z;
            spin += dt * 18f;
            float fade = Mathf.Clamp01(life * 2f);
            for (int i = 0; i < layers.Length; i++)
            {
                var r = layers[i];
                if (!r) continue;
                r.transform.localRotation = Quaternion.Euler(90f, (i % 2 == 0 ? spin : -spin * 1.3f) + i * 71f, 0f);
                Color c = GroundFire;
                c.a *= fade * (0.75f + 0.25f * Mathf.Sin(t * (11f + i * 5f)) * Mathf.Sin(t * 7.3f + i));
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block);
            }

            var fx = GameFX.Instance;
            if (!fx) return;
            Vector3 center = transform.position;
            flameBudget += dt * (5f + radius * 5f) * fade;
            while (flameBudget >= 1f)
            {
                flameBudget -= 1f;
                Vector2 c = Random.insideUnitCircle * radius * 0.8f;
                fx.Flame(center + new Vector3(c.x, 0.15f, c.y), new Vector3(0f, Random.Range(1.2f, 2.2f), 0f),
                    Random.Range(0.35f, 0.55f), Random.Range(0.45f, 0.85f));
            }
            smokeBudget += dt * (1.2f + radius * 0.8f) * fade;
            while (smokeBudget >= 1f)
            {
                smokeBudget -= 1f;
                fx.Smoke(center, radius * 0.6f, 1, 0.9f + radius * 0.2f, FireSmoke, 0.9f);
            }
        }
    }
}
