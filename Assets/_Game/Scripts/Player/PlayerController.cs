using UnityEngine;
using UnityEngine.InputSystem;

namespace Survivor
{
    /// <summary>
    /// Moves the player. Keyboard (WASD / arrows) or a floating joystick: press anywhere and drag
    /// (works with a finger on phone and with the mouse on PC).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Tooltip("Joystick radius in UI units (1080x1920 reference screen).")]
        public float joystickRadius = 130f;
        public float radius = 0.4f;
        public float mapHalfSize = 190f;
        [Tooltip("Child holding the meshes: bobs and squashes while running.")]
        public Transform visual;
        [Tooltip("Greybox hop / squash / tilt. Off when the model has its own run cycle (CORDON art).")]
        public bool proceduralBob = true;

        public PlayerStats Stats { get; private set; }
        public Vector3 Position => rb.position;
        public float Radius => radius;
        public Vector3 Facing { get; private set; } = Vector3.forward;

        public bool JoystickActive { get; private set; }
        public Vector2 JoystickOrigin { get; private set; }
        /// <summary>Joystick offset, length 0..1.</summary>
        public Vector2 JoystickVector { get; private set; }

        Rigidbody rb;
        Vector3 moveDir;
        Vector3 visualPos, visualScale = Vector3.one;
        float stride, moving;

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody>();
            Stats = GetComponent<PlayerStats>();
            if (visual)
            {
                visualPos = visual.localPosition;
                visualScale = visual.localScale;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: lets test bots steer the player.</summary>
        public static Vector2 DebugInput;
#endif

        void Update()
        {
            bool playing = GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;
            Vector2 input = playing ? ReadKeyboard() : Vector2.zero;

            ReadJoystick(playing);
            input += JoystickVector;
#if UNITY_EDITOR
            if (playing) input += DebugInput;
#endif

            input = Vector2.ClampMagnitude(input, 1f);
            moveDir = new Vector3(input.x, 0f, input.y);
            if (moveDir.sqrMagnitude > 0.01f) Facing = moveDir.normalized;

            AnimateVisual();
        }

        /// <summary>Hop and squash while running, gentle breathing while idle.</summary>
        void AnimateVisual()
        {
            if (!visual || !proceduralBob) return;
            float dt = Time.deltaTime;
            moving = Mathf.MoveTowards(moving, moveDir.sqrMagnitude > 0.01f ? 1f : 0f, dt * 6f);
            stride += dt * Mathf.Lerp(2f, 13f, moving);

            float hop = Mathf.Abs(Mathf.Sin(stride)) * 0.18f * moving;
            float squash = Mathf.Sin(stride * 2f) * Mathf.Lerp(0.02f, 0.07f, moving);
            visual.localPosition = visualPos + Vector3.up * hop;
            visual.localScale = Vector3.Scale(visualScale, new Vector3(1f - squash, 1f + squash, 1f - squash));
            visual.localRotation = Quaternion.Euler(moving * 8f, 0f, Mathf.Sin(stride) * 4f * moving);
        }

        static Vector2 ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            // Input System keys are physical positions, so WASD is also ZQSD on an AZERTY keyboard.
            Vector2 v = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            return v;
        }

        void ReadJoystick(bool playing)
        {
            var pointer = Pointer.current;
            if (pointer == null || !playing || !pointer.press.isPressed)
            {
                JoystickActive = false;
                JoystickVector = Vector2.zero;
                return;
            }

            Vector2 pos = pointer.position.ReadValue();
            if (!JoystickActive)
            {
                // Only start on a fresh press, so the tap that picks an upgrade doesn't move the player.
                if (!pointer.press.wasPressedThisFrame) return;
                JoystickActive = true;
                JoystickOrigin = pos;
            }

            float r = joystickRadius * UIKit.PixelsPerUnit;
            Vector2 delta = pos - JoystickOrigin;
            if (delta.magnitude > r)
            {
                // Drag the joystick base along so it never feels stuck.
                JoystickOrigin = pos - delta.normalized * r;
                delta = pos - JoystickOrigin;
            }
            JoystickVector = delta / r;
        }

        void FixedUpdate()
        {
            rb.linearVelocity = moveDir * Stats.moveSpeed;
            if (moveDir.sqrMagnitude > 0.01f) rb.rotation = Quaternion.LookRotation(Facing);

            Vector3 p = rb.position;
            if (Mathf.Abs(p.x) > mapHalfSize || Mathf.Abs(p.z) > mapHalfSize)
            {
                p.x = Mathf.Clamp(p.x, -mapHalfSize, mapHalfSize);
                p.z = Mathf.Clamp(p.z, -mapHalfSize, mapHalfSize);
                rb.position = p;
            }
        }
    }
}
