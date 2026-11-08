using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Tiny helpers to build uGUI from code (a bit like document.createElement + inline styles).
    /// All layouts assume a 1080x1920 portrait reference screen.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Blood = new(0.8f, 0.12f, 0.08f);
        public static readonly Color Gold = new(1f, 0.85f, 0.3f);
        public static readonly Color Muted = new(0.75f, 0.75f, 0.75f);
        public static readonly Color Card = new(0.1f, 0.1f, 0.11f, 0.95f);
        public static readonly Color Dark = new(0.18f, 0.18f, 0.2f, 1f);

        static Font font;
        static Sprite square, circle, triangle, vignette;

        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Sprite Square => square ? square : square = MakeSprite(false);
        public static Sprite Circle => circle ? circle : circle = MakeSprite(true);
        /// <summary>Points up.</summary>
        public static Sprite Triangle => triangle ? triangle : triangle = MakeTriangle();
        /// <summary>Transparent center, opaque edges: full-screen damage flash.</summary>
        public static Sprite Vignette => vignette ? vignette : vignette = MakeVignette();

        /// <summary>Screen pixels per UI unit, matching the canvas "Expand" scaling.</summary>
        public static float PixelsPerUnit => Mathf.Min(Screen.width / 1080f, Screen.height / 1920f);

        public static RectTransform Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            // "Expand" keeps the whole 1080x1920 layout visible on any screen shape:
            // tall iPhones, iPads and landscape all show everything (extra space goes to the sides/edges).
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return (RectTransform)go.transform;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float padding = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(padding, padding);
            r.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Anchor + pivot at the same normalized point, then offset and size.</summary>
        public static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = position;
            r.sizeDelta = size;
        }

        public static readonly Vector2 Center = new(0.5f, 0.5f);
        public static readonly Vector2 Top = new(0.5f, 1f);
        public static readonly Vector2 TopLeft = new(0f, 1f);
        public static readonly Vector2 TopRight = new(1f, 1f);
        public static readonly Vector2 Bottom = new(0.5f, 0f);
        public static readonly Vector2 Left = new(0f, 0.5f);
        public static readonly Vector2 Right = new(1f, 0.5f);

        public static Image Image(RectTransform r, Color color, Sprite sprite = null)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite ? sprite : Square;
            img.color = color;
            return img;
        }

        /// <summary>Dark full-screen layer that also blocks taps on what's behind.</summary>
        public static RectTransform Overlay(string name, Transform parent, float alpha)
        {
            var r = Rect(name, parent);
            Stretch(r);
            Image(r, new Color(0f, 0f, 0f, alpha));
            return r;
        }

        /// <summary>Horizontal progress bar fill inside <paramref name="bar"/>.</summary>
        public static Image Fill(RectTransform bar, Color color, float padding)
        {
            var fill = Rect("Fill", bar);
            Stretch(fill, padding);
            var img = Image(fill, color);
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            var r = Rect("Label", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = r.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(3, -3);
            return t;
        }

        public static Button Button(Transform parent, string label, Color background, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick, int fontSize = 56)
        {
            var r = Rect("Button " + label, parent);
            Place(r, anchor, position, size);
            var bg = Image(r, background);
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(PlayClick);
            var text = Label(r, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        public static InputField Input(Transform parent, Vector2 position, Vector2 size, string value, int characterLimit, UnityAction<string> onEndEdit)
        {
            var r = Rect("Input", parent);
            Place(r, Center, position, size);
            var bg = Image(r, new Color(0f, 0f, 0f, 0.6f));

            var text = Label(r, "", 52, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 12f);
            text.supportRichText = false;

            var field = r.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.characterLimit = characterLimit;
            field.lineType = InputField.LineType.SingleLine;
            field.text = value;
            field.onEndEdit.AddListener(onEndEdit);
            return field;
        }

        public static string FormatTime(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        /// <summary>Every UIKit button clicks (see SFX).</summary>
        static void PlayClick() => SFX.Play(SfxId.Click);

        static Sprite MakeTriangle()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float halfWidth = (1f - (y + 0.5f) / size) * size / 2f;
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - size / 2f);
                    byte a = (byte)(Mathf.Clamp01(halfWidth - dx + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Colored square with the first letter: a placeholder icon for weapons and bonuses.</summary>
        public static RectTransform Icon(Transform parent, string name, Color color, bool round, float size, string badge)
        {
            var r = Rect("Icon " + name, parent);
            r.sizeDelta = new Vector2(size, size);
            Image(r, new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.95f), round ? Circle : Square).raycastTarget = false;
            var inner = Rect("Inner", r);
            Stretch(inner, size * 0.08f);
            Image(inner, color, round ? Circle : Square).raycastTarget = false;
            string letter = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            var l = Label(r, letter, Mathf.RoundToInt(size * 0.55f), Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            l.GetComponent<Shadow>().enabled = false;
            Stretch(l.rectTransform);
            if (!string.IsNullOrEmpty(badge))
            {
                var b = Label(r, badge, Mathf.RoundToInt(size * 0.32f), Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                Stretch(b.rectTransform, -size * 0.08f);
            }
            return r;
        }

        static Sprite MakeVignette()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeSprite(bool round)
        {
            int size = round ? 128 : 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                byte a = 255;
                if (round)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    a = (byte)(Mathf.Clamp01(r - d) * 255f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
