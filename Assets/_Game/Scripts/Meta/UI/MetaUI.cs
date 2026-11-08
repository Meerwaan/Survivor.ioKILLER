using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>Extra UI helpers for the meta screens, on top of UIKit (scroll lists, cards, bars, stars).</summary>
    public static class MetaUI
    {
        public static readonly Color Green = new(0.25f, 0.6f, 0.25f);
        public static readonly Color LightGreen = new(0.5f, 1f, 0.5f);
        public static readonly Color Brown = new(0.55f, 0.4f, 0.1f);
        public static readonly Color Locked = new(0.06f, 0.06f, 0.07f, 0.95f);
        public static readonly Color Red = new(0.95f, 0.4f, 0.35f);
        public const string GoldHex = "FFD94D";
        public const string GreenHex = "7CFF7C";

        static Sprite star;

        /// <summary>Five-pointed star, generated once.</summary>
        public static Sprite Star => star ? star : star = MakeStar();

        public static Text Text(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 position, Vector2 box, FontStyle style = FontStyle.Normal)
        {
            var t = UIKit.Label(parent, text, size, color, align, style);
            UIKit.Place(t.rectTransform, anchor, position, box);
            return t;
        }

        public static RectTransform Card(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var r = UIKit.Rect(name, parent);
            UIKit.Place(r, anchor, position, size);
            UIKit.Image(r, color).raycastTarget = false;
            return r;
        }

        /// <summary>Small button that can be disabled (greyed).</summary>
        public static Button Button(Transform parent, string label, Color color, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick, int fontSize, bool enabled = true)
        {
            var b = UIKit.Button(parent, label, enabled ? color : UIKit.Dark, anchor, position, size, onClick, fontSize);
            b.interactable = enabled;
            if (!enabled)
            {
                var label2 = b.GetComponentInChildren<Text>();
                if (label2) label2.color = new Color(0.6f, 0.6f, 0.6f);
            }
            return b;
        }

        /// <summary>Progress bar with a background, returns the fill image.</summary>
        public static Image Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, float value)
        {
            var bar = UIKit.Rect("Bar", parent);
            UIKit.Place(bar, anchor, position, size);
            UIKit.Image(bar, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = false;
            var fill = UIKit.Fill(bar, color, 4f);
            fill.fillAmount = Mathf.Clamp01(value);
            return fill;
        }

        /// <summary>Three stars, filled according to the bitmask.</summary>
        public static void Stars(Transform parent, Vector2 anchor, Vector2 position, float size, int mask)
        {
            var row = UIKit.Rect("Stars", parent);
            UIKit.Place(row, anchor, position, new Vector2(size * 3.2f, size));
            for (int i = 0; i < 3; i++)
            {
                var s = UIKit.Rect("Star", row);
                UIKit.Place(s, UIKit.Center, new Vector2((i - 1) * size * 1.08f, 0f), new Vector2(size, size));
                bool on = (mask & (1 << i)) != 0;
                UIKit.Image(s, on ? UIKit.Gold : new Color(1f, 1f, 1f, 0.15f), Star).raycastTarget = false;
            }
        }

        public static RectTransform StarIcon(Transform parent, Vector2 anchor, Vector2 position, float size, bool on)
        {
            var s = UIKit.Rect("Star", parent);
            UIKit.Place(s, anchor, position, new Vector2(size, size));
            UIKit.Image(s, on ? UIKit.Gold : new Color(1f, 1f, 1f, 0.15f), Star).raycastTarget = false;
            return s;
        }

        /// <summary>Red dot with a number (menu badges).</summary>
        public static RectTransform Badge(Transform parent, Vector2 position, string text)
        {
            var b = UIKit.Rect("Badge", parent);
            UIKit.Place(b, UIKit.TopRight, position, new Vector2(56, 56));
            UIKit.Image(b, new Color(0.9f, 0.15f, 0.1f), UIKit.Circle).raycastTarget = false;
            var t = UIKit.Label(b, text, 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(t.rectTransform);
            return b;
        }

        /// <summary>
        /// Vertical scroll list. Returns the content (top-anchored, pivot at the top):
        /// place rows with UIKit.Place(row, UIKit.Top, new Vector2(0, -y), size) and set the height with SetHeight.
        /// </summary>
        public static RectTransform Scroll(Transform parent, Vector2 position, Vector2 size, out ScrollRect scroll)
        {
            var view = UIKit.Rect("Scroll", parent);
            UIKit.Place(view, UIKit.Center, position, size);
            UIKit.Image(view, new Color(0f, 0f, 0f, 0f)); // catches drags between rows
            view.gameObject.AddComponent<RectMask2D>();

            var content = UIKit.Rect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, size.y);

            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        public static void SetHeight(RectTransform content, float height)
        {
            content.sizeDelta = new Vector2(0f, height);
        }

        static Sprite MakeStar()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0 ? 0.5f : 0.21f) * size;
                pts[i] = new Vector2(size / 2f + Mathf.Cos(a) * rad, size / 2f + Mathf.Sin(a) * rad - size * 0.03f);
            }

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 2x2 supersampling for soft edges.
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                    if (Inside(pts, new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f))) hits++;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }
    }
}
