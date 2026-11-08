using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Tiny UI animations (think CSS transitions): slide-in with overshoot, and looping pulse.
    /// Runs on real time so it works while the game is paused.
    /// </summary>
    public class UIAnim : MonoBehaviour
    {
        RectTransform rect;
        CanvasGroup group;
        Vector2 target, from;
        Vector3 baseScale = Vector3.one;
        float delay, duration, startScale, age;
        bool sliding;

        float pulseSpeed, pulseAmount;

        /// <summary>Slides from <paramref name="offset"/> to the current position, popping in with a slight overshoot.</summary>
        public static UIAnim SlideIn(RectTransform r, Vector2 offset, float delay, float duration = 0.35f, float startScale = 0.7f)
        {
            var a = Get(r);
            a.target = r.anchoredPosition;
            a.from = a.target + offset;
            a.delay = delay;
            a.duration = duration;
            a.startScale = startScale;
            a.age = 0f;
            a.sliding = true;
            if (!a.group && !r.TryGetComponent(out a.group)) a.group = r.gameObject.AddComponent<CanvasGroup>();
            a.Apply(0f);
            return a;
        }

        /// <summary>Endless gentle scale pulse (for things that deserve attention).</summary>
        public static UIAnim Pulse(RectTransform r, float speed = 4f, float amount = 0.04f)
        {
            var a = Get(r);
            a.pulseSpeed = speed;
            a.pulseAmount = amount;
            return a;
        }

        static UIAnim Get(RectTransform r)
        {
            if (!r.TryGetComponent<UIAnim>(out var a))
            {
                a = r.gameObject.AddComponent<UIAnim>();
                a.rect = r;
                a.baseScale = r.localScale;
            }
            return a;
        }

        public bool Done => !sliding;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (sliding)
            {
                age += dt;
                float k = Mathf.Clamp01((age - delay) / duration);
                Apply(k);
                if (k >= 1f) sliding = false;
            }
            else if (pulseAmount > 0f)
            {
                rect.localScale = baseScale * (1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount);
            }
        }

        void Apply(float k)
        {
            float e = EaseOutBack(k);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, target, e);
            rect.localScale = baseScale * Mathf.LerpUnclamped(startScale, 1f, e);
            if (group) group.alpha = Mathf.Clamp01(k * 3f);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
