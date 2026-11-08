using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>Burning patch of ground left by a Molotov. Sets enemies standing in it on fire (scaled by Huile / Pyromane).</summary>
    public class FirePool : MonoBehaviour
    {
        float damage, radius, life, tick, timer, age;
        /// <summary>Hit candidates scratch. Shared: only used inside Update, which never runs nested.</summary>
        static readonly List<Enemy> Candidates = new(32);

        static readonly Color EmberColor = new(2.5f, 0.9f, 0.15f);

        public void Ignite(float damage, float radius, float duration, float tick)
        {
            this.damage = damage;
            this.radius = radius;
            this.tick = tick;
            life = duration;
            timer = 0f;
            age = 0f;
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

            timer -= dt;
            if (timer > 0f) return;
            timer = tick;

            Vector3 center = transform.position;
            if (GameFX.Instance) GameFX.Instance.Embers(center, radius * 0.8f, 2 + Mathf.RoundToInt(radius), EmberColor);

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
    }
}
