using UnityEngine;

namespace Survivor
{
    public enum EnemyBehaviour
    {
        /// <summary>Walks at the player and hits on contact.</summary>
        Melee,
        /// <summary>Keeps its distance and spits projectiles.</summary>
        Ranged,
        /// <summary>Walks at the player and fires rings of projectiles.</summary>
        Boss,
        /// <summary>Walks at the player, swells up when close, then blows up (hurting other enemies too).</summary>
        Kamikaze,
        /// <summary>Runs AWAY from the player with loot, and escapes after a while. Drops coins when hit.</summary>
        Thief,
        /// <summary>Doesn't move or attack: explosive barrels, breakable crates. Not counted as a kill.</summary>
        Prop,
    }

    /// <summary>What an enemy drops when it dies (see Loot).</summary>
    public enum LootTable
    {
        /// <summary>XP gem, plus small chances of meat / magnet / coins. Elites and bosses drop more.</summary>
        Normal,
        None,
        /// <summary>Supply crate: meat, magnet or a pile of coins.</summary>
        Crate,
        /// <summary>Thief: a shower of gold and a reward chest.</summary>
        Thief,
        /// <summary>Golden wave: big XP and coins.</summary>
        Golden,
    }

    /// <summary>
    /// One enemy type. Create new ones with right-click > Create > Survivor > Enemy.
    /// </summary>
    [CreateAssetMenu(menuName = "Survivor/Enemy", fileName = "Enemy")]
    public class EnemyData : ScriptableObject
    {
        public string displayName = "Ambré";
        public GameObject prefab;
        public EnemyBehaviour behaviour = EnemyBehaviour.Melee;

        [Header("Stats")]
        public float maxHealth = 10f;
        public float moveSpeed = 2.2f;
        public float damage = 8f;
        [Tooltip("Seconds between two contact hits on the player.")]
        public float attackInterval = 0.8f;
        [Tooltip("Body radius, used for hits and contact.")]
        public float hitRadius = 0.4f;
        [Range(0f, 1f)] public float knockbackResist = 0f;
        public bool canBeElite = true;

        [Header("Shooting (Ranged / Boss)")]
        public GameObject projectilePrefab;
        public float projectileDamage = 8f;
        public float projectileSpeed = 7f;
        public float fireInterval = 2.5f;
        [Tooltip("Ranged enemies try to stay this far from the player.")]
        public float preferredRange = 7f;
        [Tooltip("Boss: projectiles per ring.")]
        public int ringCount = 12;

        [Header("Kamikaze")]
        [Tooltip("Starts its fuse when the player is this close.")]
        public float fuseRange = 2.2f;
        public float fuseTime = 0.7f;

        [Header("Death effects")]
        [Tooltip("> 0: explodes on death and hurts nearby enemies (and the player too if it blew itself up).")]
        public float deathExplosionRadius = 0f;
        [Tooltip("Damage dealt to enemies by that explosion (scales like enemy health over the run).")]
        public float deathExplosionDamage = 0f;
        public Color explosionColor = new(1f, 0.55f, 0.15f);
        [Tooltip("Splitter: spawns these when it dies.")]
        public EnemyData splitInto;
        public int splitCount = 0;

        [Header("Thief")]
        [Tooltip("Seconds before it escapes with the loot.")]
        public float escapeTime = 12f;

        [Header("Rewards")]
        public float xp = 1f;
        public int score = 10;
        public LootTable loot = LootTable.Normal;

        [Header("Gore and sound")]
        public int bloodAmount = 18;
        [Tooltip("Off for props: they throw sparks / debris instead of blood.")]
        public bool bleeds = true;
        public SfxId hitSound = SfxId.Hit;
        public SfxId deathSound = SfxId.Kill;
    }
}
