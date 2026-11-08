using System;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Anything thrown in an arc (Molotov bottles, grenades, bomblets). Calls back where it lands.
    /// </summary>
    public class MolotovBottle : MonoBehaviour
    {
        public float arcHeight = 3f;
        public float spinSpeed = 540f;

        Vector3 from, to;
        float elapsed, duration;
        Action<Vector3> onLanded;

        public void Throw(Vector3 start, Vector3 end, float flightTime, Action<Vector3> landed)
        {
            from = start;
            to = end;
            duration = Mathf.Max(0.05f, flightTime);
            elapsed = 0f;
            onLanded = landed;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arcHeight);
            transform.Rotate(spinSpeed * Time.deltaTime, 0f, 0f, Space.Self);

            if (k >= 1f)
            {
                var callback = onLanded;
                onLanded = null;
                Pool.Instance.Despawn(gameObject);
                callback?.Invoke(to);
            }
        }
    }
}
