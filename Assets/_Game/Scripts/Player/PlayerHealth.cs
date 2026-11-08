using System;
using UnityEngine;

namespace Survivor
{
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        public Renderer bodyRenderer;
        public Material hurtMaterial;
        [Tooltip("Seconds of invulnerability after a hit: a swarm can't melt you in one frame.")]
        public float invulnerability = 0.35f;
        [Tooltip("Regeneration pauses this long after taking a hit: HP dips during hordes, recovers in the breathers.")]
        public float regenDelayAfterHit = 2f;

        public float Current { get; private set; }
        public float Max => stats.maxHealth;
        public float Fraction => Max > 0f ? Current / Max : 0f;

        /// <summary>Fired with the damage taken (after armor). The HUD flashes red.</summary>
        public event Action<float> Hurt;

        PlayerStats stats;
        Material baseMaterial;
        float flashUntil, invulnerableUntil, regenResumeAt;
        bool dead;

        void Awake()
        {
            Instance = this;
            stats = GetComponent<PlayerStats>();
            Current = stats.maxHealth;
            if (bodyRenderer) baseMaterial = bodyRenderer.sharedMaterial;
            anim = GetComponent<CharacterAnimator>();
        }

        CharacterAnimator anim;

        void Update()
        {
            if (!dead && stats.regen > 0f && Time.time >= regenResumeAt) Heal(stats.regen * Time.deltaTime);

            if (flashUntil > 0f && Time.time >= flashUntil)
            {
                bodyRenderer.sharedMaterial = baseMaterial;
                flashUntil = 0f;
            }
        }

        public void TakeDamage(float amount)
        {
            if (dead || GameManager.Instance.State != GameState.Playing || Time.time < invulnerableUntil) return;
            invulnerableUntil = Time.time + invulnerability;
            regenResumeAt = Time.time + regenDelayAfterHit;

            amount *= 1f - Mathf.Min(0.6f, stats.armor);
            Current = Mathf.Max(0f, Current - amount);

            if (anim) anim.Hit();
            if (bodyRenderer && hurtMaterial)
            {
                bodyRenderer.sharedMaterial = hurtMaterial;
                flashUntil = Time.time + 0.1f;
            }
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
            if (GameFX.Instance) GameFX.Instance.Blood(transform.position + Vector3.up, 5);
            Hurt?.Invoke(amount);

            if (Current <= 0f)
            {
                dead = true;
                if (anim) anim.SetDead(true);
                GameManager.Instance.PlayerDied();
            }
        }

        public void Heal(float amount)
        {
            if (!dead) Current = Mathf.Min(Max, Current + amount);
        }

        /// <summary>Back from a death ("Seconde chance" ad, campaign only, see Monetization/ReviveOffer.cs).</summary>
        public void Revive(float fraction, float invulnerableSeconds)
        {
            dead = false;
            if (anim) anim.SetDead(false);
            Current = Mathf.Max(1f, Max * Mathf.Clamp01(fraction));
            invulnerableUntil = Time.time + invulnerableSeconds;
        }

        public void AddMaxHealth(float amount)
        {
            stats.maxHealth += amount;
            Heal(amount);
        }
    }
}
