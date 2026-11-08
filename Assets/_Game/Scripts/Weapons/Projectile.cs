using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Straight-flying bullet. Checks distance to the enemies around it (EnemyGrid) instead of using physics (cheaper with many enemies).
    /// Can pierce, bounce to the next enemy (ricochet) and explode on impact.
    /// Synergies: once it has bounced it always ignites (if the player has incendiary bullets),
    /// Poudre noire can make it explode, and Pistolero II "special" bullets are exploding crits.
    /// Darts (Fléchettes) also poison what they hit.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public float radius = 0.3f;
        [Tooltip("Impact sparks color (above 1 glows).")]
        public Color sparkColor = new(2f, 1.6f, 0.6f);

        static readonly Color PowderColor = new(1f, 0.7f, 0.3f);

        Vector3 direction;
        float speed, damage, knockback, life, explodeRadius, poison;
        int pierceLeft, ricochetLeft;
        ItemTag tags;
        bool special, bounced;
        readonly List<Enemy> alreadyHit = new(8);
        /// <summary>Hit candidates scratch. Shared: only used inside Update, which never runs nested.</summary>
        static readonly List<Enemy> Candidates = new(32);
        TrailRenderer trail;

        void Awake() => trail = GetComponentInChildren<TrailRenderer>();

        public void Launch(Vector3 dir, float speed, float damage, int pierce, float knockback, float lifetime,
            int ricochet = 0, float explodeRadius = 0f, ItemTag tags = ItemTag.Balle, bool special = false, float poison = 0f)
        {
            direction = dir;
            this.speed = speed;
            this.damage = damage;
            this.knockback = knockback;
            this.explodeRadius = explodeRadius;
            this.tags = tags;
            this.special = special;
            this.poison = poison;
            bounced = false;
            pierceLeft = pierce;
            ricochetLeft = ricochet;
            life = lifetime;
            alreadyHit.Clear();
            transform.localScale = special ? Vector3.one * 1.8f : Vector3.one;
            if (trail) trail.Clear();
        }

        /// <summary>Enemies touched by this frame's segment, sorted by distance along it. Shared scratch (Update never nests).</summary>
        static Enemy[] sweptHits = new Enemy[32];
        static float[] sweptT = new float[32];

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 from = transform.position;
            Vector3 step = direction * speed * dt;
            transform.position = from + step;
            life -= dt;
            if (life <= 0f)
            {
                Pool.Instance.Despawn(gameObject);
                return;
            }

            var stats = PlayerController.Instance ? PlayerController.Instance.Stats : null;
            int hitCount = Sweep(from, step);
            for (int h = 0; h < hitCount; h++)
            {
                var enemy = sweptHits[h];
                sweptHits[h] = null;
                if (!EnemyGrid.IsLive(enemy)) continue; // died or got recycled by an earlier hit of this segment

                alreadyHit.Add(enemy);
                Vector3 hitPoint = enemy.transform.position + Vector3.up;

                var flags = HitFlags.None;
                if (special) flags |= HitFlags.Crit;
                if (bounced && stats && stats.burnChance > 0f) flags |= HitFlags.Ignite; // tracer rounds: ricochets carry fire
                bool killed = Synergies.Hit(enemy, damage, direction, knockback, tags, flags);
                // Poison darts: poison dps for 3 s (x duration), refreshed by every dart.
                if (poison > 0f && !killed) Synergies.Poison(enemy, poison, 3f * (stats ? stats.durationMult : 1f));
                if (GameFX.Instance) GameFX.Instance.Sparks(hitPoint, sparkColor, 3);

                // Explosions are queued: we're looping over the enemy list right now.
                float area = stats ? stats.areaMult : 1f;
                if (special)
                {
                    Synergies.QueueExplosion(hitPoint, 1.6f * area, damage * 1.5f, 6f, Synergies.PistoleroColor, tags);
                    special = false;
                }
                if (explodeRadius > 0f)
                {
                    Synergies.QueueExplosion(hitPoint, explodeRadius, damage * 0.6f, 5f, Combat.ExplosionColor, tags);
                    explodeRadius = 0f; // once per pellet
                }
                else if (stats && stats.bulletExplodeChance > 0f && Random.value < stats.bulletExplodeChance)
                {
                    Synergies.QueueExplosion(hitPoint, 1.4f * area, damage * 0.7f, 4f, PowderColor, tags);
                }

                if (ricochetLeft > 0)
                {
                    // Ricochet from where the segment touched this enemy, not from the end of a long (hitch) step.
                    Vector3 end = transform.position;
                    transform.position = from + step * sweptT[h];
                    if (Bounce(hitPoint)) return;
                    transform.position = end;
                }

                if (pierceLeft-- <= 0)
                {
                    Pool.Instance.Despawn(gameObject);
                    return;
                }
            }
        }

        /// <summary>
        /// Swept hit test: every live, not-yet-hit enemy whose circle touches this frame's segment
        /// [from, from + step] on the ground plane, sorted along it (a frame hitch can't tunnel through a crowd).
        /// </summary>
        int Sweep(Vector3 from, Vector3 step)
        {
            float sx = step.x, sz = step.z, len2 = sx * sx + sz * sz;
            // Only the enemies around the segment (snapshot of this frame, same order as walking Enemy.Active backwards).
            var list = Candidates;
            EnemyGrid.Query(from + step * 0.5f, Mathf.Sqrt(len2) * 0.5f + radius, list);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var enemy = list[i];
                if (!EnemyGrid.IsLive(enemy)) continue; // died or got recycled earlier this frame
                Vector3 c = enemy.transform.position;
                float cx = c.x - from.x, cz = c.z - from.z;
                float t = len2 > 1e-8f ? Mathf.Clamp01((cx * sx + cz * sz) / len2) : 1f;
                float dx = cx - sx * t, dz = cz - sz * t;
                float r = enemy.Radius + radius;
                if (dx * dx + dz * dz > r * r || alreadyHit.Contains(enemy)) continue; // distance first: cheaper

                if (n == sweptHits.Length)
                {
                    System.Array.Resize(ref sweptHits, n * 2);
                    System.Array.Resize(ref sweptT, n * 2);
                }
                // Insertion by distance along the segment (ties keep snapshot order).
                int j = n - 1;
                while (j >= 0 && sweptT[j] > t)
                {
                    sweptT[j + 1] = sweptT[j];
                    sweptHits[j + 1] = sweptHits[j];
                    j--;
                }
                sweptT[j + 1] = t;
                sweptHits[j + 1] = enemy;
                n++;
            }
            return n;
        }

        bool Bounce(Vector3 from)
        {
            var next = Weapon.FindNearest(from, 7f, alreadyHit);
            if (!next) return false;

            ricochetLeft--;
            bounced = true;
            Vector3 to = next.transform.position - transform.position;
            to.y = 0f;
            direction = to.normalized;
            transform.rotation = Quaternion.LookRotation(direction);
            life = to.magnitude / speed + 0.3f;
            return true;
        }
    }
}
