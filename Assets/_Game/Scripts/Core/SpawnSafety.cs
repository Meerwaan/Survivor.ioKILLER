using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Keeps spawns out of the decor (cars, containers, walls on the "Decor" layer): a spawn point inside a collider
    /// would be shoved out by physics, or get stuck. Allocation-free (CheckSphere only).
    /// </summary>
    public static class SpawnSafety
    {
        /// <summary>-1 = not looked up yet; 0 = no "Decor" layer in this project (then every point is free).</summary>
        static int decorMask = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => decorMask = -1;

        public static int DecorMask
        {
            get
            {
                if (decorMask < 0) decorMask = LayerMask.GetMask("Decor");
                return decorMask;
            }
        }

        /// <summary>True when a body of <paramref name="radius"/> standing at <paramref name="p"/> overlaps the decor.</summary>
        public static bool Blocked(Vector3 p, float radius)
        {
            int mask = DecorMask;
            if (mask == 0) return false;
            float r = radius + 0.2f;
            // Centre at least r above the ground, so the sphere never dips into it.
            p.y = Mathf.Max(0.5f, r + 0.02f);
            return Physics.CheckSphere(p, r, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// <paramref name="p"/> if it is free, else the nearest free spot found on rings around it (golden-angle spiral),
        /// else <paramref name="p"/> unchanged. Y is kept.
        /// </summary>
        public static Vector3 FreePoint(Vector3 p, float radius, int tries = 8)
        {
            if (DecorMask == 0 || !Blocked(p, radius)) return p;
            float step = radius * 2f + 0.8f;
            for (int i = 0; i < tries; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float dist = step * (1f + i * 0.5f);
                Vector3 q = new(p.x + Mathf.Cos(angle) * dist, p.y, p.z + Mathf.Sin(angle) * dist);
                if (!Blocked(q, radius)) return q;
            }
            return p;
        }
    }
}
