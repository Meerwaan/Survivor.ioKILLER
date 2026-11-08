using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Plays a character model's clips from what its body does (CORDON art pass):
    /// - the locomotion clip runs at the real ground speed, so feet don't slide;
    /// - every spawn starts at a random point of the cycle, so a horde never marches in step;
    /// - every spawn shows one of the model's look variants (clothes), and tells <see cref="Enemy"/> which
    ///   renderer to flash.
    /// Gameplay code calls <see cref="Attack"/>, <see cref="Special"/>, <see cref="Windup"/>, <see cref="SetCharging"/>.
    /// Purely visual: it never changes gameplay, and it has its own random generator (seeded runs stay identical).
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Look variants: one is shown per spawn.")]
        public Renderer[] variants;
        [Tooltip("Ground speed (m/s) of the locomotion clip at playback speed 1.")]
        public float naturalSpeed = 2f;
        public float minSpeed = 0.35f, maxSpeed = 2.6f;
        [Tooltip("Below this ground speed (m/s) the Idle clip plays (when the controller has one).")]
        public float idleBelow = 0.3f;
        [Tooltip("Back to the locomotion clip above this ground speed (m/s): the gap keeps Move/Idle from flickering.")]
        public float moveAbove = 0.45f;
        /// <summary>Hard ceiling on the locomotion playback speed, whatever the enemy's own speed.</summary>
        const float PlaybackCeiling = 8f;

        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int MovingId = Animator.StringToHash("Moving");
        public static readonly int AttackId = Animator.StringToHash("Attack");
        public static readonly int SpecialId = Animator.StringToHash("Special");
        public static readonly int WindupId = Animator.StringToHash("Windup");
        public static readonly int HitId = Animator.StringToHash("Hit");
        public static readonly int ChargingId = Animator.StringToHash("Charging");
        public static readonly int DeadId = Animator.StringToHash("Dead");
        static readonly int MoveState = Animator.StringToHash("Move");

        static readonly System.Random LookRng = new();

        Rigidbody rb;
        Enemy enemy;
        Vector3 lastPos;
        bool moving = true;

        /// <summary>The variant shown right now.</summary>
        public Renderer ActiveRenderer { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            enemy = GetComponent<Enemy>();
            if (variants != null && variants.Length > 0) ActiveRenderer = variants[0];
        }

        void OnEnable()
        {
            if (variants != null && variants.Length > 1)
            {
                int pick = LookRng.Next(variants.Length);
                for (int i = 0; i < variants.Length; i++)
                    if (variants[i]) variants[i].gameObject.SetActive(i == pick);
                ActiveRenderer = variants[pick];
                if (enemy) enemy.bodyRenderer = ActiveRenderer;
            }
            if (animator && animator.isActiveAndEnabled && animator.runtimeAnimatorController)
            {
                animator.SetBool(MovingId, true);
                animator.Play(MoveState, 0, (float)LookRng.NextDouble());
            }
            moving = true;
            lastPos = transform.position;
        }

        void Update()
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            float dt = Time.deltaTime;
            Vector3 v;
            if (rb && !rb.isKinematic) v = rb.linearVelocity;
            else v = dt > 0f ? (transform.position - lastPos) / dt : Vector3.zero;
            lastPos = transform.position;
            v.y = 0f;
            float speed = v.magnitude;
            float natural = Mathf.Max(0.1f, naturalSpeed);
            // Small clips (the rat's walks 0.96 m/s, it runs 5): never clamp below the enemy's own design speed,
            // or its feet glide.
            float max = maxSpeed;
            if (enemy && enemy.Data) max = Mathf.Clamp(enemy.Data.moveSpeed * 1.3f / natural, maxSpeed, PlaybackCeiling);
            animator.SetFloat(SpeedId, Mathf.Clamp(speed / natural, minSpeed, max));
            // Hysteresis: one threshold flickered Move/Idle on enemies hovering around it.
            if (moving ? speed < idleBelow : speed > Mathf.Max(moveAbove, idleBelow + 0.05f)) moving = !moving;
            animator.SetBool(MovingId, moving);
        }

        public void Attack() { if (animator) animator.SetTrigger(AttackId); }
        public void Special() { if (animator) animator.SetTrigger(SpecialId); }
        public void Windup() { if (animator) animator.SetTrigger(WindupId); }
        public void Hit() { if (animator) animator.SetTrigger(HitId); }
        public void SetCharging(bool on) { if (animator) animator.SetBool(ChargingId, on); }

        /// <summary>The Passeur falls (the game freezes time on the result screen, so the fall plays in real time).</summary>
        public void SetDead(bool dead)
        {
            if (!animator) return;
            animator.SetBool(DeadId, dead);
            animator.updateMode = dead ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
        }
    }
}
