using UnityEngine;

namespace Survivor
{
    public enum PickupType { Health, Magnet, Chest, Coin }

    /// <summary>
    /// Drops: meat heals, magnet pulls every gem, coins are gold for the camp,
    /// chest (boss) grants free upgrades and a pile of gold.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        [Tooltip("Health (meat): share of max HP healed. 0.25 -> 0.2.")]
        public float healFraction = 0.2f;
        [Tooltip("Chest: share of max HP healed. Boss chest 0.5 -> 0.3; the small reward chest is set to 0.15 by the builder.")]
        public float chestHealFraction = 0.3f;
        [Tooltip("Chest only: free level-ups granted.")]
        public int chestUpgrades = 3;
        [Tooltip("Coin: gold given.")]
        public int value = 2;
        [Tooltip("Chest: gold given.")]
        public int chestGold = 50;

        bool attracted;
        float speed;
        float bobPhase;
        Vector3 popVelocity;
        float popTime;

        void OnEnable()
        {
            attracted = false;
            speed = -6f;
            bobPhase = Random.value * 10f;
            popTime = 0f;
        }

        /// <summary>Little jump out of the corpse.</summary>
        public void Pop()
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
            popVelocity = new Vector3(r.x, Random.Range(5f, 8f), r.y);
            popTime = 0.6f;
        }

        void Update()
        {
            var player = PlayerController.Instance;
            if (!player) return;

            float dt = Time.deltaTime;
            bobPhase += dt * 3f;
            transform.Rotate(0f, (type == PickupType.Coin ? 270f : 90f) * dt, 0f, Space.World);

            if (popTime > 0f)
            {
                popTime -= dt;
                popVelocity.y -= 25f * dt;
                Vector3 p = transform.position + popVelocity * dt;
                if (p.y < 0.5f)
                {
                    p.y = 0.5f;
                    popVelocity.y = Mathf.Abs(popVelocity.y) * 0.3f;
                    popVelocity.x *= 0.5f;
                    popVelocity.z *= 0.5f;
                }
                transform.position = p;
                return;
            }

            Vector3 target = player.Position + Vector3.up * 0.6f;
            Vector3 toPlayer = target - transform.position;
            float distance = toPlayer.magnitude;

            float radius = player.Stats.pickupRadius * (type == PickupType.Coin ? 1.3f : 1f);
            if (!attracted && distance <= radius) attracted = true;
            if (attracted)
            {
                // Starts by backing off a little, then whooshes in.
                speed += 45f * dt;
                transform.position += toPlayer.normalized * Mathf.Min(distance, (speed + 6f) * dt);
            }
            else
            {
                Vector3 p = transform.position;
                p.y = 0.5f + Mathf.Sin(bobPhase) * 0.15f;
                transform.position = p;
            }

            if (distance < 0.7f) Collect();
        }

        void Collect()
        {
            var fx = GameFX.Instance;
            Vector3 p = transform.position;
            switch (type)
            {
                case PickupType.Health:
                    float amount = PlayerHealth.Instance.Max * healFraction;
                    PlayerHealth.Instance.Heal(amount);
                    GameHUD.Instance.ShowBanner($"+{amount:0} PV", new Color(0.5f, 0.95f, 0.4f), 0.8f);
                    if (fx) fx.Ring(p, 2f, new Color(0.6f, 2f, 0.5f), 0.4f);
                    SFX.Play(SfxId.Heal);
                    break;
                case PickupType.Magnet:
                    XPGem.AttractAll();
                    GameHUD.Instance.ShowBanner("AIMANT !", new Color(0.4f, 0.75f, 1f), 0.8f);
                    if (fx) fx.Ring(p, 8f, new Color(0.6f, 1.2f, 3f), 0.6f);
                    SFX.Play(SfxId.Magnet);
                    break;
                case PickupType.Coin:
                    GameManager.Instance.AddGold(value);
                    if (fx) fx.Sparks(p, new Color(2.5f, 2f, 0.6f), 4);
                    SFX.Play(SfxId.Coin);
                    break;
                case PickupType.Chest:
                    PlayerHealth.Instance.Heal(PlayerHealth.Instance.Max * chestHealFraction);
                    GameManager.Instance.QueueLevelUp(chestUpgrades);
                    GameManager.Instance.AddGold(chestGold);
                    GameHUD.Instance.ShowBanner($"COFFRE !\n<size=50>+{chestGold} or</size>", UIKit.Gold, 1.2f);
                    GameHUD.Instance.ScreenFlash(new Color(1f, 0.85f, 0.4f), 0.5f);
                    SFX.Play(SfxId.Chest);
                    if (fx)
                    {
                        fx.Ring(p, 7f, new Color(3f, 2.4f, 0.8f), 0.7f);
                        fx.Sparks(p, new Color(3f, 2.4f, 0.8f), 60, 2f);
                    }
                    break;
            }
            Pool.Instance.Despawn(gameObject);
        }
    }
}
