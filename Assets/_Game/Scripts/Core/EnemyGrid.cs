using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Uniform grid over <see cref="Enemy.Active"/>, rebuilt lazily once per frame (on the first query of the frame).
    /// Per-frame hit checks (bullets, saws, fire pools) only look at the enemies around them instead of all 300+,
    /// and each enemy's position is read once per frame here instead of once per projectile.
    /// No allocations after warm-up (arrays only grow).
    ///
    /// Rules for callers (the grid only prunes, the caller's own test still decides):
    /// - <see cref="Query"/> returns the enemies whose SNAPSHOT circle overlaps the query circle (plus a small slack).
    ///   Callers check <see cref="IsLive"/> and then redo their exact test on the live transform position, as before.
    /// - Enemies that died or were despawned since the snapshot are still returned: <see cref="IsLive"/> filters them.
    /// - A pooled enemy respawned since the snapshot is found (or not) by its OLD position, and the caller's live test
    ///   then judges its real position: it can be missed for that one frame, never hit where it isn't.
    /// - Enemies spawned after the snapshot are missed until the next frame.
    /// - Results come in snapshot order, which is <see cref="Enemy.Active"/> walked backwards (the order the old loops used).
    /// Enemies don't move during the Update phase (physics moves them in FixedUpdate, Relocate runs in FixedUpdate,
    /// Pool.Spawn only places new ones), so for every enemy present at snapshot time the snapshot position IS the live one.
    /// Query from Update / coroutines only: not from FixedUpdate (physics would move enemies after the snapshot).
    /// </summary>
    public static class EnemyGrid
    {
        const float CellSize = 3f;
        const float InvCell = 1f / CellSize;
        /// <summary>Hash buckets (power of two): far more than the cells a horde covers, so few cells share one.</summary>
        const int BucketCount = 1024;
        const int BucketMask = BucketCount - 1;
        /// <summary>
        /// Extra reach of the pruning test: absorbs float rounding so it never rejects what the caller's exact test
        /// (radius + enemy radius, live position) would accept.
        /// </summary>
        const float Slack = 0.05f;
        /// <summary>Queries spanning more cells than this per axis scan the whole snapshot instead (cheaper).</summary>
        const int MaxCellsPerAxis = 8;

        struct Entry
        {
            public Enemy enemy;
            public float x, z, radius;
            public int cx, cz, bucket;
        }

        // Snapshot, in Enemy.Active order walked backwards.
        static Entry[] entries = new Entry[512];
        // Snapshot indices grouped by bucket (counting sort): bucket b is sorted[bucketStart[b] .. bucketStart[b + 1]).
        static int[] sorted = new int[512];
        static readonly int[] bucketStart = new int[BucketCount + 1];
        // Query scratch: snapshot indices of the matches.
        static int[] found = new int[64];
        static int count, builtFrame = -1;
        static float maxRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            System.Array.Clear(entries, 0, entries.Length);
            count = 0;
            builtFrame = -1;
            maxRadius = 0f;
        }

        /// <summary>Still in <see cref="Enemy.Active"/> and alive (snapshot entries can have died or been recycled since).</summary>
        public static bool IsLive(Enemy e) => e && !e.IsDead && e.isActiveAndEnabled;

        /// <summary>
        /// Clears <paramref name="results"/> and fills it with the enemies whose snapshot circle overlaps the circle
        /// (<paramref name="center"/>, <paramref name="radius"/>) on the ground plane (x/z), in snapshot order.
        /// Candidates only: check <see cref="IsLive"/> and redo the exact distance test on the live position.
        /// </summary>
        public static void Query(Vector3 center, float radius, List<Enemy> results)
        {
            results.Clear();
            if (builtFrame != Time.frameCount) Build();
            if (count == 0) return;

            float px = center.x, pz = center.z;
            float reach = radius + maxRadius + Slack; // enemies are bucketed by center: widen by the biggest radius
            int n = 0;
            if (reach * 2f * InvCell >= MaxCellsPerAxis)
            {
                for (int k = 0; k < count; k++)
                    if (Overlaps(in entries[k], px, pz, radius)) Add(ref n, k);
            }
            else
            {
                int x0 = Mathf.FloorToInt((px - reach) * InvCell), z0 = Mathf.FloorToInt((pz - reach) * InvCell);
                int w = Mathf.FloorToInt((px + reach) * InvCell) - x0, h = Mathf.FloorToInt((pz + reach) * InvCell) - z0;
                for (int iz = 0; iz <= h; iz++)
                {
                    int cz = z0 + iz;
                    for (int ix = 0; ix <= w; ix++)
                    {
                        int cx = x0 + ix;
                        int b = Bucket(cx, cz);
                        for (int j = bucketStart[b], end = bucketStart[b + 1]; j < end; j++)
                        {
                            int k = sorted[j];
                            ref Entry e = ref entries[k];
                            if (e.cx != cx || e.cz != cz) continue; // another cell hashed to the same bucket
                            if (Overlaps(in e, px, pz, radius)) Add(ref n, k);
                        }
                    }
                }
                // Matches came cell by cell: back to snapshot order (a handful of them, insertion sort).
                for (int i = 1; i < n; i++)
                {
                    int v = found[i], j = i - 1;
                    while (j >= 0 && found[j] > v)
                    {
                        found[j + 1] = found[j];
                        j--;
                    }
                    found[j + 1] = v;
                }
            }
            for (int i = 0; i < n; i++) results.Add(entries[found[i]].enemy);
        }

        static bool Overlaps(in Entry e, float px, float pz, float radius)
        {
            float dx = e.x - px, dz = e.z - pz, r = radius + e.radius + Slack;
            return dx * dx + dz * dz <= r * r;
        }

        static void Add(ref int n, int k)
        {
            if (n == found.Length) System.Array.Resize(ref found, n * 2);
            found[n++] = k;
        }

        static int Bucket(int cx, int cz) => unchecked(cx * 73856093 ^ cz * 19349663) & BucketMask;

        static void Build()
        {
            builtFrame = Time.frameCount;
            var list = Enemy.Active;
            int total = list.Count;
            if (entries.Length < total)
            {
                int size = Mathf.Max(total, entries.Length * 2);
                System.Array.Clear(entries, 0, entries.Length);
                entries = new Entry[size];
                sorted = new int[size];
            }

            int previous = count;
            count = 0;
            maxRadius = 0f;
            System.Array.Clear(bucketStart, 0, bucketStart.Length);
            // Backwards: snapshot order = the order the per-weapon loops used to visit enemies.
            for (int i = total - 1; i >= 0; i--)
            {
                var enemy = list[i];
                if (!enemy) continue;
                Vector3 p = enemy.transform.position;
                ref Entry e = ref entries[count];
                e.enemy = enemy;
                e.x = p.x;
                e.z = p.z;
                e.radius = enemy.Radius;
                e.cx = Mathf.FloorToInt(p.x * InvCell);
                e.cz = Mathf.FloorToInt(p.z * InvCell);
                e.bucket = Bucket(e.cx, e.cz);
                if (e.radius > maxRadius) maxRadius = e.radius;
                bucketStart[e.bucket]++;
                count++;
            }
            // Don't keep references to enemies from a bigger previous frame.
            if (previous > count) System.Array.Clear(entries, count, previous - count);

            // Counting sort by bucket. Inclusive prefix sums: bucketStart[b] = end of bucket b (bucketStart[BucketCount] = count).
            for (int b = 1; b <= BucketCount; b++) bucketStart[b] += bucketStart[b - 1];
            // Scatter backwards: each end pointer walks down to its bucket's start, keeping snapshot order inside a bucket.
            // Afterwards bucket b is [bucketStart[b], bucketStart[b + 1]).
            for (int k = count - 1; k >= 0; k--) sorted[--bucketStart[entries[k].bucket]] = k;
        }
    }
}
