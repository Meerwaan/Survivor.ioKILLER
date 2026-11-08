using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>One decor model the dresser can place (filled by the builder from Tools/Blender/assets/decor_manifest.json).</summary>
    [Serializable]
    public class DecorItem
    {
        public GameObject prefab;
        [Tooltip("obstacle (collider) | small | decal | tall")]
        public string kind = "small";
        public float weight = 1f;
        [Tooltip("Footprint radius in meters (spacing between obstacles).")]
        public float radius = 0.5f;
        [Tooltip("Theme it belongs to: street, checkpoint, evacuation, flood...")]
        public string cluster = "street";
        [Tooltip("Free yaw (cars, debris) or snapped to 90 degrees (walls, barriers).")]
        public bool freeYaw = true;
    }

    /// <summary>
    /// Dresses the endless arena with Ostrov's ruins around the player (CORDON art pass): the ground is cut in
    /// chunks, every chunk gets a theme (street, Cordon checkpoint, evacuation, flood) and a few props, always the
    /// SAME ones for a given chunk (seeded by the chunk coordinates, never by the run), so every Passeur crosses the
    /// same streets — fair for the Registre. Obstacles have colliders: hordes slide around them.
    /// Chunks far from the player are recycled (simple per-prefab pools).
    /// </summary>
    public class WorldDresser : MonoBehaviour
    {
        public DecorItem[] items = Array.Empty<DecorItem>();
        public float chunkSize = 18f;
        [Tooltip("Chunks kept around the player's chunk (2 = a 5x5 block).")]
        public int radius = 2;
        public int worldSeed = 17;
        [Tooltip("No obstacle this close to the map center (where every run starts).")]
        public float clearRadius = 8f;
        [Tooltip("Never place anything beyond this (the player is clamped at 190 m; the decor goes a bit further so the edge isn't empty).")]
        public float worldHalfSize = 216f;

        [Header("Density per chunk")]
        public float obstacles = 1.6f;
        public float smalls = 4f;
        public float decals = 5f;
        public float talls = 0.7f;

        static readonly string[] Themes = { "street", "street", "street", "checkpoint", "evacuation", "flood", "flood", "strike" };

        readonly Dictionary<Vector2Int, List<GameObject>> live = new();
        readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
        readonly Dictionary<GameObject, GameObject> sourceOf = new();
        readonly List<Vector2Int> toDrop = new();
        readonly List<Vector3> placed = new();
        Vector2Int center = new(int.MinValue, int.MinValue);

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            Vector3 p = player ? player.Position : Vector3.zero;
            var c = new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));
            if (c == center) return;
            center = c;

            toDrop.Clear();
            foreach (var k in live.Keys)
                if (Mathf.Abs(k.x - c.x) > radius + 1 || Mathf.Abs(k.y - c.y) > radius + 1) toDrop.Add(k);
            foreach (var k in toDrop) Unload(k);

            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                var k = new Vector2Int(c.x + dx, c.y + dz);
                if (!live.ContainsKey(k)) Load(k);
            }
        }

        void Load(Vector2Int k)
        {
            var list = new List<GameObject>();
            live[k] = list;
            Vector3 origin = new(k.x * chunkSize, 0f, k.y * chunkSize);
            if (Mathf.Abs(origin.x + chunkSize * 0.5f) > worldHalfSize || Mathf.Abs(origin.z + chunkSize * 0.5f) > worldHalfSize) return;

            var rng = new System.Random(Hash(k.x, k.y, worldSeed));
            string theme = Themes[rng.Next(Themes.Length)];
            placed.Clear();
            Place(list, rng, origin, "decal", Count(rng, decals), theme);
            Place(list, rng, origin, "obstacle", Count(rng, obstacles * (theme == "checkpoint" ? 1.6f : 1f)), theme);
            Place(list, rng, origin, "small", Count(rng, smalls), theme);
            Place(list, rng, origin, "tall", Count(rng, talls), theme);
            PlaceBorder(list, origin);
        }

        /// <summary>
        /// The edge of the playable map is the Cordon's own line: a row of concrete barriers, so the player sees
        /// why he can't go further (instead of an invisible wall). Visual only (no colliders): the player is
        /// clamped by PlayerController, and an enemy that wandered outside can still walk back in.
        /// </summary>
        void PlaceBorder(List<GameObject> list, Vector3 origin)
        {
            float line = BorderLine;
            var barrier = FindItem("Barrier_Jersey") ?? FindItem("Fence") ?? FindItem("Barrier");
            if (barrier == null || !barrier.prefab) return;
            float step = Mathf.Max(1.6f, barrier.radius * 2f + 0.15f);
            for (int axis = 0; axis < 2; axis++)
            for (int side = -1; side <= 1; side += 2)
            {
                float fixedCoord = side * line;
                float lo = axis == 0 ? origin.x : origin.z;
                if (fixedCoord < (axis == 0 ? origin.z : origin.x) || fixedCoord >= (axis == 0 ? origin.z : origin.x) + chunkSize) continue;
                for (float t = Mathf.Ceil(lo / step) * step; t < lo + chunkSize; t += step)
                {
                    if (Mathf.Abs(t) > line + 0.5f) continue;
                    var pos = axis == 0 ? new Vector3(t, 0f, fixedCoord) : new Vector3(fixedCoord, 0f, t);
                    var go = Take(barrier.prefab);
                    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, axis == 0 ? 0f : 90f, 0f));
                    if (go.TryGetComponent<Collider>(out var col)) col.enabled = false;
                    list.Add(go);
                }
            }
        }

        static float BorderLine => (PlayerController.Instance ? PlayerController.Instance.mapHalfSize : 190f) + 1.1f;

        static bool NearBorder(Vector3 p, float margin)
        {
            float line = BorderLine;
            return Mathf.Abs(Mathf.Abs(p.x) - line) < margin || Mathf.Abs(Mathf.Abs(p.z) - line) < margin;
        }

        DecorItem FindItem(string word)
        {
            foreach (var it in items)
                if (it.prefab && it.prefab.name.Contains(word)) return it;
            return null;
        }

        static int Count(System.Random rng, float mean)
        {
            int n = Mathf.FloorToInt(mean);
            if (rng.NextDouble() < mean - n) n++;
            return n;
        }

        void Place(List<GameObject> list, System.Random rng, Vector3 origin, string kind, int count, string theme)
        {
            for (int i = 0; i < count; i++)
            {
                var item = Pick(rng, kind, theme);
                if (item == null || !item.prefab) return;
                bool blocks = kind == "obstacle" || kind == "tall";
                for (int tries = 0; tries < 6; tries++)
                {
                    // 1.1 m from the chunk edge: two obstacles in neighbouring chunks keep the same 2.2 m gap as inside one.
                    float m = Mathf.Min(item.radius + (blocks ? 1.1f : 0.5f), chunkSize * 0.3f);
                    var pos = origin + new Vector3(m + (float)rng.NextDouble() * (chunkSize - 2f * m), 0f, m + (float)rng.NextDouble() * (chunkSize - 2f * m));
                    if (blocks && new Vector2(pos.x, pos.z).magnitude < clearRadius + item.radius) continue;
                    if (blocks && Crowded(pos, item.radius)) continue;
                    // Keep the border line clear (the barrier row) so obstacles never pinch the edge of the map.
                    if (blocks && NearBorder(pos, item.radius + 2.5f)) continue;
                    float yaw = item.freeYaw ? (float)rng.NextDouble() * 360f : rng.Next(4) * 90f + ((float)rng.NextDouble() - 0.5f) * 14f;
                    var go = Take(item.prefab);
                    if (go.TryGetComponent<Collider>(out var col)) col.enabled = true; // pooled border pieces have it off
                    go.transform.SetPositionAndRotation(pos + Vector3.up * (kind == "decal" ? 0.012f + i * 0.002f : 0f), Quaternion.Euler(0f, yaw, 0f));
                    list.Add(go);
                    if (blocks) placed.Add(new Vector3(pos.x, item.radius, pos.z));
                    break;
                }
            }
        }

        /// <summary>Keeps obstacles apart so the player and the hordes always have room to pass.</summary>
        bool Crowded(Vector3 pos, float r)
        {
            foreach (var q in placed)
            {
                float dx = q.x - pos.x, dz = q.z - pos.z, gap = q.y + r + 2.2f;
                if (dx * dx + dz * dz < gap * gap) return true;
            }
            return false;
        }

        DecorItem Pick(System.Random rng, string kind, string theme)
        {
            float total = 0f;
            foreach (var it in items)
                if (it.kind == kind) total += it.weight * (it.cluster == theme ? 3f : 1f);
            if (total <= 0f) return null;
            float r = (float)rng.NextDouble() * total;
            foreach (var it in items)
            {
                if (it.kind != kind) continue;
                r -= it.weight * (it.cluster == theme ? 3f : 1f);
                if (r <= 0f) return it;
            }
            return null;
        }

        GameObject Take(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var stack) && stack.Count > 0)
            {
                var g = stack.Pop();
                g.SetActive(true);
                return g;
            }
            var go = Instantiate(prefab, transform);
            sourceOf[go] = prefab;
            return go;
        }

        void Unload(Vector2Int k)
        {
            if (!live.TryGetValue(k, out var list)) return;
            foreach (var go in list)
            {
                if (!go) continue;
                go.SetActive(false);
                var src = sourceOf[go];
                if (!pools.TryGetValue(src, out var stack)) pools[src] = stack = new Stack<GameObject>();
                stack.Push(go);
            }
            live.Remove(k);
        }

        static int Hash(int x, int z, int seed)
        {
            unchecked
            {
                int h = seed * 73856093 ^ x * 19349663 ^ z * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return h ^ (h >> 15);
            }
        }
    }
}
