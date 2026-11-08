using UnityEngine;

namespace Survivor
{
    /// <summary>Slow projectile fired by spitters and bosses. Hurts the player on contact.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        public float radius = 0.25f;

        Vector3 direction;
        float speed, damage, life;

        public void Launch(Vector3 dir, float speed, float damage, float lifetime = 5f)
        {
            direction = dir;
            this.speed = speed;
            this.damage = damage;
            life = lifetime;
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

            var player = PlayerController.Instance;
            if (!player) return;

            Vector3 d = player.Position - transform.position;
            d.y = 0f;
            float r = radius + player.Radius;
            if (d.sqrMagnitude <= r * r)
            {
                PlayerHealth.Instance.TakeDamage(damage);
                Pool.Instance.Despawn(gameObject);
            }
        }
    }
}
