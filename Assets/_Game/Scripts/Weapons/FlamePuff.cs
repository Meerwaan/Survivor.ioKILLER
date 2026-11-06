using UnityEngine;

namespace Survivor
{
    /// <summary>One blob of the flamethrower jet: flies forward, swells, then shrinks away. Purely visual.</summary>
    public class FlamePuff : MonoBehaviour
    {
        Vector3 velocity;
        float age, life, size;

        public void Launch(Vector3 velocity, float life, float size)
        {
            this.velocity = velocity;
            this.life = Mathf.Max(0.05f, life);
            this.size = size;
            age = 0f;
            transform.localScale = Vector3.one * size * 0.3f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life)
            {
                Pool.Instance.Despawn(gameObject);
                return;
            }

            float k = age / life;
            transform.position += velocity * dt;
            float grow = Mathf.Lerp(0.3f, 1.25f, Mathf.Sqrt(k));
            if (k > 0.7f) grow *= (1f - k) / 0.3f;
            transform.localScale = Vector3.one * size * grow;
        }
    }
}
