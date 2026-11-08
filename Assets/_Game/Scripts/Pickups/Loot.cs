using UnityEngine;

namespace Survivor
{
    /// <summary>Decides what an enemy drops when it dies.</summary>
    public class Loot : MonoBehaviour
    {
        public static Loot Instance { get; private set; }

        public GameObject gemPrefab;
        public GameObject healthPrefab;
        public GameObject magnetPrefab;
        public GameObject chestPrefab;
        public GameObject coinPrefab;
        [Tooltip("Smaller chest (1 free upgrade): thief, timed challenges.")]
        public GameObject rewardChestPrefab;

        [Tooltip("Meat chance per normal kill. Kills grow with the stream, so natural meat is also limited in time (below).")]
        [Range(0f, 0.2f)] public float healthChance = 0.01f;
        [Tooltip("At least this many seconds between two meat drops from normal kills (elites, crates, chests aren't limited).")]
        public float healthMinInterval = 12f;
        [Tooltip("Meat chance on an elite kill.")]
        [Range(0f, 1f)] public float eliteHealthChance = 0.3f;
        [Tooltip("Meat chance on a supply crate (otherwise magnet or coins).")]
        [Range(0f, 1f)] public float crateHealthChance = 0.3f;
        [Range(0f, 0.2f)] public float magnetChance = 0.004f;
        [Range(0f, 0.3f)] public float coinChance = 0.03f;
        [Tooltip("Past this many gems on the ground, new XP merges into existing gems (saves performance).")]
        public int maxGems = 250;

        float nextHealthDrop;

        void Awake() => Instance = this;

        public void Drop(Enemy enemy, Vector3 position)
        {
            Vector3 p = position + Vector3.up * 0.4f;
            var table = enemy.Data ? enemy.Data.loot : LootTable.Normal;

            switch (table)
            {
                case LootTable.None:
                    return;

                case LootTable.Crate:
                    DropGem(enemy.XP, p);
                    float roll = Random.value;
                    if (roll < crateHealthChance) Spawn(healthPrefab, p);
                    else if (roll < crateHealthChance + 0.15f) Spawn(magnetPrefab, p);
                    else DropCoins(p, 6, 2);
                    return;

                case LootTable.Thief:
                    DropGem(enemy.XP, p);
                    DropCoins(p, 14, 3);
                    DropRewardChest(p);
                    return;

                case LootTable.Golden:
                    DropGem(enemy.XP, p);
                    DropCoins(p, 2, 2);
                    return;
            }

            if (enemy.IsBoss)
            {
                Spawn(chestPrefab, p);
                for (int i = 0; i < 6; i++) DropGem(enemy.XP / 6f, p);
                DropCoins(p, 14, 5);
                return;
            }

            if (enemy.IsElite)
            {
                DropGem(enemy.XP, p);
                DropCoins(p, 5, 2);
                if (Random.value < eliteHealthChance) Spawn(healthPrefab, p);
                else if (Random.value < 0.2f) Spawn(magnetPrefab, p);
                return;
            }

            DropGem(enemy.XP, p);

            float r = Random.value;
            if (r < magnetChance) Spawn(magnetPrefab, p);
            else if (r < magnetChance + healthChance)
            {
                float now = Time.time;
                if (now >= nextHealthDrop)
                {
                    nextHealthDrop = now + healthMinInterval;
                    Spawn(healthPrefab, p);
                }
            }
            else if (r < magnetChance + healthChance + coinChance) DropCoins(p, 1, 1);
        }

        void Spawn(GameObject prefab, Vector3 p)
        {
            if (!prefab) return;
            if (Pool.Instance.Spawn(prefab, p, Quaternion.identity).TryGetComponent<Pickup>(out var pickup)) pickup.Pop();
        }

        /// <summary>A little fountain of coins (each one pops out in a random direction).</summary>
        public void DropCoins(Vector3 p, int count, int value)
        {
            if (!coinPrefab) return;
            for (int i = 0; i < count; i++)
            {
                var coin = Pool.Instance.Spawn<Pickup>(coinPrefab, p, Quaternion.identity);
                coin.value = value;
                coin.Pop();
            }
        }

        /// <summary>Small chest: heals, 1 free upgrade and some gold. Falls back to the boss chest.</summary>
        public void DropRewardChest(Vector3 p)
        {
            Spawn(rewardChestPrefab ? rewardChestPrefab : chestPrefab, p);
        }

        public void DropGem(float value, Vector3 position)
        {
            if (!gemPrefab || value <= 0f) return;

            var gems = XPGem.Active;
            if (gems.Count >= maxGems)
            {
                // Merge into the closest gem: XP must stay where the kill happened, not vanish across the map.
                XPGem best = gems[0];
                float bestSqr = float.MaxValue;
                for (int i = 0; i < gems.Count; i++)
                {
                    Vector3 d = gems[i].transform.position - position;
                    float sqr = d.x * d.x + d.z * d.z;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = gems[i];
                    }
                }
                best.AddValue(value);
                return;
            }
            Pool.Instance.Spawn<XPGem>(gemPrefab, position, Quaternion.identity).Init(value);
        }
    }

    static class VectorExtensions
    {
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
    }
}
