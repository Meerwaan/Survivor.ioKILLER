using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Straight-flying bullet. Checks distance to enemies instead of using physics (cheaper with many enemies).
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

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += direction * speed * dt;
            life -= dt;
            if (life <= 0f)
            {
                Pool.Instance.Despawn(gameObject);
                return;
            }

            Vector3 p = transform.position;
            var stats = PlayerController.Instance ? PlayerController.Instance.Stats : null;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var enemy = list[i];
                Vector3 d = enemy.transform.position - p;
                d.y = 0f;
                float r = enemy.Radius + radius;
                if (d.sqrMagnitude > r * r || alreadyHit.Contains(enemy)) continue; // distance first: cheaper

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

                if (ricochetLeft > 0 && Bounce(hitPoint)) return;

                if (pierceLeft-- <= 0)
                {
                    Pool.Instance.Despawn(gameObject);
                    return;
                }
            }
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
