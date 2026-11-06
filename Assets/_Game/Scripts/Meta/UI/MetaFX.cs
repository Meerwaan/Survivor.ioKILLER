using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Fire-and-forget UI effects on real time (the menu runs at timeScale 0):
    /// floating text, flying coins, punch scale.
    /// </summary>
    public class MetaFX : MonoBehaviour
    {
        enum Mode { Float, Fly, Punch }

        Mode mode;
        RectTransform rect;
        Graphic graphic;
        Vector3 from, to, control, baseScale;
        float delay, duration, age;

        /// <summary>Text that rises and fades out ("+120 or").</summary>
        public static void FloatText(RectTransform layer, Vector3 worldPosition, string text, Color color, int size = 60)
        {
            var t = UIKit.Label(layer, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.sizeDelta = new Vector2(900, 120);
            t.rectTransform.position = worldPosition;
            var fx = t.gameObject.AddComponent<MetaFX>();
            fx.mode = Mode.Float;
            fx.rect = t.rectTransform;
            fx.graphic = t;
            fx.from = worldPosition;
            fx.to = worldPosition + Vector3.up * 220f * layer.lossyScale.y;
            fx.duration = 1.3f;
        }

        /// <summary>A burst of small discs flying from one point to another (harvest, rewards).</summary>
        public static void Burst(RectTransform layer, Vector3 fromWorld, Vector3 toWorld, Color color, int count, Sprite sprite = null)
        {
            float scale = layer.lossyScale.x;
            for (int i = 0; i < count; i++)
            {
                var r = UIKit.Rect("Coin", layer);
                r.sizeDelta = new Vector2(46, 46);
                var img = UIKit.Image(r, color, sprite ? sprite : UIKit.Circle);
                img.raycastTarget = false;
                var fx = r.gameObject.AddComponent<MetaFX>();
                fx.mode = Mode.Fly;
                fx.rect = r;
                fx.graphic = img;
                Vector3 jitter = (Vector3)(Random.insideUnitCircle * 70f * scale);
                fx.from = fromWorld + jitter;
                fx.to = toWorld;
                Vector3 mid = (fx.from + toWorld) * 0.5f;
                fx.control = mid + (Vector3)(Random.insideUnitCircle * 300f * scale) + Vector3.up * 150f * scale;
                fx.delay = i * 0.035f;
                fx.duration = 0.55f + Random.value * 0.2f;
                r.position = fx.from;
                r.localScale = Vector3.zero;
            }
        }

        /// <summary>Quick scale bump (does nothing if the target already pulses).</summary>
        public static void Punch(RectTransform target, float delay = 0f)
        {
            if (!target || target.GetComponent<UIAnim>()) return;
            var fx = target.GetComponent<MetaFX>();
            if (!fx)
            {
                fx = target.gameObject.AddComponent<MetaFX>();
                fx.baseScale = target.localScale;
            }
            fx.mode = Mode.Punch;
            fx.rect = target;
            fx.delay = delay;
            fx.duration = 0.35f;
            fx.age = 0f;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((age - delay) / duration);
            switch (mode)
            {
                case Mode.Float:
                    rect.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
                    SetAlpha(k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                    if (k >= 1f) Destroy(gameObject);
                    break;

                case Mode.Fly:
                    if (age < delay) return;
                    float u = 1f - k;
                    rect.position = u * u * from + 2f * u * k * control + k * k * to;
                    rect.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.6f, k) * Mathf.Clamp01(k * 6f);
                    if (k >= 1f) Destroy(gameObject);
                    break;

                case Mode.Punch:
                    rect.localScale = baseScale * (1f + 0.22f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                    if (k >= 1f)
                    {
                        rect.localScale = baseScale;
                        Destroy(this);
                    }
                    break;
            }
        }

        void SetAlpha(float a)
        {
            if (!graphic) return;
            var c = graphic.color;
            c.a = a;
            graphic.color = c;
        }
    }
}
