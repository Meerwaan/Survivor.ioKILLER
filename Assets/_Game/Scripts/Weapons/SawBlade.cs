using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>One thrown saw: flies out, slows down, comes back to the player. Hits each enemy once per way.</summary>
    public class SawBlade : MonoBehaviour
    {
        Transform owner;
        Vector3 direction;
        float damage, range, speed, size, knockback, traveled, returnSpeed;
        bool returning, alwaysCrit;
        readonly List<Enemy> hit = new(32);
        TrailRenderer trail;

        void Awake() => trail = GetComponentInChildren<TrailRenderer>();

        public void Launch(Weapon weapon, Vector3 dir, float damage, float range, float speed, float size, float knockback, bool alwaysCrit)
        {
            owner = weapon.transform;
            direction = dir;
            this.damage = damage;
            this.range = range;
            this.speed = speed;
            this.size = size;
            this.knockback = knockback;
            this.alwaysCrit = alwaysCrit;
            traveled = 0f;
            returnSpeed = 0f;
            returning = false;
            hit.Clear();
            transform.localScale = Vector3.one * size * 1.25f;
            if (trail)
            {
                trail.Clear();
                trail.widthMultiplier = size * 1.4f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.Rotate(0f, 1080f * dt, 0f, Space.World);

            if (!owner)
            {
                Pool.Instance.Despawn(gameObject);
                return;
            }

            Vector3 p = transform.position;
            if (!returning)
            {
                // Eases out near the end of the throw.
                float k = traveled / range;
                float v = speed * Mathf.Lerp(1f, 0.25f, k * k);
                p += direction * v * dt;
                traveled += v * dt;
                if (traveled >= range)
                {
                    returning = true;
                    hit.Clear();
                }
            }
            else
            {
                Vector3 home = owner.position + Vector3.up;
                Vector3 to = home - p;
                float d = to.magnitude;
                returnSpeed = Mathf.Min(speed * 1.6f, returnSpeed + speed * 3f * dt);
                if (d < 0.6f || d < returnSpeed * dt)
                {
                    Pool.Instance.Despawn(gameObject);
                    return;
                }
                p += to / d * returnSpeed * dt;
            }
            transform.position = p;

            float hitR = size * 0.85f;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 d = e.transform.position - p;
                d.y = 0f;
                float r = hitR + e.Radius;
                if (d.sqrMagnitude > r * r || hit.Contains(e)) continue; // distance first: the hit list gets long

                hit.Add(e);
                Synergies.Hit(e, damage, d, knockback, ItemTag.Lame, alwaysCrit ? HitFlags.Crit : HitFlags.None);
                if (GameFX.Instance)
                {
                    GameFX.Instance.Sparks(e.transform.position + Vector3.up, new Color(2.5f, 2.2f, 1.6f), 3);
                    GameFX.Instance.Blood(e.transform.position + Vector3.up, 4);
                }
            }
        }
    }
}
