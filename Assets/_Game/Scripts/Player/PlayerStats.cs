using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// All the player's numbers in one place. Passives and evolutions modify these (same base for every player); weapons read them.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        public float moveSpeed = 5.2f;
        public float maxHealth = 100f;
        [Tooltip("Health regenerated per second.")]
        public float regen = 0f;
        [Tooltip("Extra health per second once the player hasn't been hit for a few seconds (Sang froid). Applied by Synergies.")]
        public float calmRegen = 0f;
        [Tooltip("Share of incoming damage blocked (0.1 = -10%). Capped at 60%.")]
        public float armor = 0f;
        [Tooltip("Distance at which XP gems start flying to the player.")]
        public float pickupRadius = 2.8f;
        public float xpMult = 1f;
        public float goldMult = 1f;

        [Header("Applied to every weapon")]
        public float damageMult = 1.15f;
        public float cooldownMult = 1f;
        public float areaMult = 1f;
        [Tooltip("Fire, gas, saws... last longer.")]
        public float durationMult = 1f;
        [Tooltip("Extra bullets / blades / bottles for every weapon.")]
        public int extraProjectiles = 0;

        [Header("On hit / on kill")]
        [Range(0f, 1f)] public float critChance = 0.05f;
        public float critMult = 2f;
        [Tooltip("Bullets bounce to this many extra enemies.")]
        public int ricochets = 0;
        [Tooltip("Chance per hit to set the enemy on fire.")]
        [Range(0f, 1f)] public float burnChance = 0f;
        [Tooltip("Chance for a dead enemy to explode and hurt its neighbours.")]
        [Range(0f, 1f)] public float corpseExplodeChance = 0f;
        [Tooltip("Chance to heal 3 HP on each kill (Sangsue). Capped per second in Combat.OnKill.")]
        [Range(0f, 1f)] public float lifeStealChance = 0f;

        [Header("Synergies (see Synergies.cs)")]
        [Tooltip("Multiplier on burning damage.")]
        public float burnPower = 1f;
        [Tooltip("Share of a blade hit dealt again as bleeding over 3 s.")]
        public float bleedPower = 0f;
        [Tooltip("Chance per hit to shock 2 nearby enemies.")]
        [Range(0f, 1f)] public float shockChance = 0f;
        [Tooltip("Chance per hit to poison the enemy.")]
        [Range(0f, 1f)] public float poisonChance = 0f;
        [Tooltip("Chance for a bullet or pellet to explode on impact.")]
        [Range(0f, 1f)] public float bulletExplodeChance = 0f;
        [Tooltip("Lightning poisons: share of the hit dealt as poison per second (Acide de batterie).")]
        public float shockPoison = 0f;
        [Tooltip("Chance for a blade hit to set the enemy on fire (Lame chauffée).")]
        [Range(0f, 1f)] public float bladeIgnite = 0f;
        [Tooltip("Damage bonus at 0 HP, scaled by missing health (Dos au mur).")]
        public float lowHpDamage = 0f;

        [Header("Level-up screen")]
        public int rerolls = 1;
        [Tooltip("Items the player can remove from the card pool for the run.")]
        public int banishes = 3;
        [Tooltip("Rarer cards and more 4-card hands (Patte de lapin). +1 per level.")]
        public float luck = 0f;
    }
}
