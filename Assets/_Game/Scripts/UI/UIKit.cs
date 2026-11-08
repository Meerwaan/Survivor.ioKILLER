using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Tiny helpers to build uGUI from code (a bit like document.createElement + a shared stylesheet).
    /// All layouts assume a 1080x1920 portrait reference screen.
    /// Look: "military utility" (Univers.md 9.11): dark panels with cut corners, thin lines, condensed bold titles.
    /// </summary>
    public static class UIKit
    {
        // ---------- Palette (Univers.md 9.4 / 9.11) ----------

        public static readonly Color Blood = Hex(0xB8322A);
        public static readonly Color Gold = Hex(0xF4C84A);
        public static readonly Color Muted = Hex(0x8C949C);
        public static readonly Color Card = new(0.122f, 0.141f, 0.165f, 0.96f);   // #1F242A
        public static readonly Color Dark = Hex(0x2A3038);
        /// <summary>Screen background behind panels.</summary>
        public static readonly Color Bg = Hex(0x14171B);
        /// <summary>Thin separators and panel outlines.</summary>
        public static readonly Color Line = Hex(0x3A424B);
        /// <summary>Main text color (instead of pure white).</summary>
        public static readonly Color Ink = Hex(0xE6E9EC);
        /// <summary>Interface accent: REGEN cyan (the Passeur's screens are REGEN hardware).</summary>
        public static readonly Color Accent = Hex(0x2CB1C4);
        public static readonly Color Safe = Hex(0x5FD08A);
        public static readonly Color Amber = Hex(0xE58E2B);

        public static Color Hex(int rgb, float a = 1f) =>
            new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        // ---------- Fonts ----------

        static Font legacy, body, bodyBold, heading, display;

        static Font Legacy => legacy ? legacy : legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        static Font Load(string file)
        {
            var f = Resources.Load<Font>("Fonts/" + file);
            return f ? f : Legacy;
        }

        /// <summary>Body text (Inter Regular).</summary>
        public static Font Font => body ? body : body = Load("Inter-Regular");
        /// <summary>Small bold text (Inter SemiBold).</summary>
        public static Font BodyBold => bodyBold ? bodyBold : bodyBold = Load("Inter-SemiBold");
        /// <summary>Buttons, card titles (Roboto Bold).</summary>
        public static Font Heading => heading ? heading : heading = Load("Roboto-Bold");
        /// <summary>Big titles and banners (Roboto Black).</summary>
        public static Font Display => display ? display : display = Load("Roboto-Black");

        /// <summary>
        /// Picks the face for a role: bold + big = Roboto Black, bold = Roboto Bold / Inter SemiBold, else Inter.
        /// Sizes are scaled so text keeps about the width the layouts were made for (they were made with Arial).
        /// </summary>
        public static void ApplyFont(Text t, int size, FontStyle style)
        {
            bool bold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            Font f;
            float k;
            if (bold && size >= 44) { f = Display; k = 0.9f; }
            else if (bold && size >= 30) { f = Heading; k = 0.95f; }
            else if (bold) { f = BodyBold; k = 0.92f; }
            else { f = Font; k = 0.93f; }

            if (f == Legacy) { t.font = f; t.fontSize = size; t.fontStyle = style; return; }
            t.font = f;
            t.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * k));
            // The face is already bold: no fake bold on top.
            t.fontStyle = bold ? (style == FontStyle.BoldAndItalic ? FontStyle.Italic : FontStyle.Normal) : style;
        }

        // ---------- Sprites (generated once, cached) ----------

        static Sprite square, circle, triangle, vignette, panel, panelLine, button, chip, chipLine, bar, glow;

        public static Sprite Square => square ? square : square = MakeSprite(false);
        public static Sprite Circle => circle ? circle : circle = MakeSprite(true);
        /// <summary>Points up.</summary>
        public static Sprite Triangle => triangle ? triangle : triangle = MakeTriangle();
        /// <summary>Transparent center, opaque edges: full-screen damage flash.</summary>
        public static Sprite Vignette => vignette ? vignette : vignette = MakeVignette();
        /// <summary>Sliced panel with two cut corners (top-left, bottom-right) and a soft vertical shade.</summary>
        public static Sprite PanelSprite => panel ? panel : panel = MakeChamfer(64, 14, 20, Shade.Panel, 0f);
        /// <summary>Outline of <see cref="PanelSprite"/> only (draw it over a panel, tinted with a line color).</summary>
        public static Sprite PanelLineSprite => panelLine ? panelLine : panelLine = MakeChamfer(64, 14, 20, Shade.Flat, 2.5f);
        /// <summary>Sliced button: cut corners, light top edge, gradient, darker bottom lip (a pressable slab).</summary>
        public static Sprite ButtonSprite => button ? button : button = MakeChamfer(64, 14, 20, Shade.Button, 0f);
        /// <summary>Small sliced shape for chips, tags and bars.</summary>
        public static Sprite ChipSprite => chip ? chip : chip = MakeChamfer(32, 6, 9, Shade.Panel, 0f);
        public static Sprite ChipLineSprite => chipLine ? chipLine : chipLine = MakeChamfer(32, 6, 9, Shade.Flat, 2f);
        /// <summary>Vertical gradient with a highlight, for bar fills (works with Image.Type.Filled).</summary>
        public static Sprite BarSprite => bar ? bar : bar = MakeBar();
        /// <summary>Soft blurred square, for drop shadows and glows (sliced).</summary>
        public static Sprite GlowSprite => glow ? glow : glow = MakeGlow();

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

        /// <summary>
        /// Cut-corner panel: the fill (tinted <paramref name="color"/>) plus a thin outline child.
        /// <paramref name="corner"/> scales the cut corners (1 = 14 units, 0.5 for small boxes).
        /// </summary>
        public static Image Panel(RectTransform r, Color color, bool outline = true, float corner = 1f)
        {
            float minSide = Mathf.Min(r.rect.width > 0 ? r.rect.width : r.sizeDelta.x, r.rect.height > 0 ? r.rect.height : r.sizeDelta.y);
            bool small = minSide > 0f && minSide < 64f;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = small ? ChipSprite : PanelSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.2f, corner);
            img.color = color;
            if (outline) Outline(r, LineFor(color), small, corner);
            return img;
        }

        /// <summary>Thin cut-corner outline drawn over a panel (first child, so content stays on top).</summary>
        public static Image Outline(RectTransform r, Color color, bool small = false, float corner = 1f)
        {
            var line = Rect("Line", r);
            Stretch(line);
            line.SetAsFirstSibling();
            var img = line.gameObject.AddComponent<Image>();
            img.sprite = small ? ChipLineSprite : PanelLineSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.2f, corner);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A lighter rim for a panel color (transparent panels get a faint grey rim).</summary>
        public static Color LineFor(Color c)
        {
            float k = c.a < 0.7f ? 0.2f : 0.16f;
            return new Color(c.r + (1f - c.r) * k, c.g + (1f - c.g) * k, c.b + (1f - c.b) * k, Mathf.Max(c.a, 0.45f));
        }

        /// <summary>Soft drop shadow behind a rect (a sibling placed just before it).</summary>
        public static Image DropShadow(RectTransform target, float spread = 18f, float alpha = 0.5f, float offsetY = -10f)
        {
            if (!target.parent) return null;
            var s = Rect("Shadow", target.parent);
            s.anchorMin = target.anchorMin;
            s.anchorMax = target.anchorMax;
            s.pivot = target.pivot;
            s.anchoredPosition = target.anchoredPosition + new Vector2(0f, offsetY);
            s.sizeDelta = target.sizeDelta + new Vector2(spread * 2f, spread * 2f);
            s.SetSiblingIndex(target.GetSiblingIndex());
            var img = s.gameObject.AddComponent<Image>();
            img.sprite = GlowSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = new Color(0f, 0f, 0f, alpha);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Dark full-screen layer that also blocks taps on what's behind.</summary>
        public static RectTransform Overlay(string name, Transform parent, float alpha)
        {
            var r = Rect(name, parent);
            Stretch(r);
            Image(r, new Color(0.035f, 0.04f, 0.05f, alpha));
            return r;
        }

        /// <summary>Horizontal progress bar fill inside <paramref name="bar"/>.</summary>
        public static Image Fill(RectTransform bar, Color color, float padding)
        {
            var fill = Rect("Fill", bar);
            Stretch(fill, padding);
            var img = Image(fill, color, BarSprite);
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
            ApplyFont(t, size, style);
            t.text = text;
            t.color = color == Color.white ? Ink : color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            // Shadow stays the FIRST effect: several screens tweak GetComponent<Shadow>().
            var shadow = r.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -3f);
            if (size >= 56 && (style == FontStyle.Bold || style == FontStyle.BoldAndItalic))
            {
                // Big titles and banners get a thin dark contour so they read over the game.
                var outline = r.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        /// <summary>Relative luminance (to pick dark text on light buttons).</summary>
        public static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        public static Button Button(Transform parent, string label, Color background, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick, int fontSize = 56)
        {
            var r = Rect("Button " + label, parent);
            Place(r, anchor, position, size);
            var bg = r.gameObject.AddComponent<Image>();
            bg.sprite = ButtonSprite;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            float minSide = Mathf.Min(size.x, size.y);
            bg.pixelsPerUnitMultiplier = minSide < 90f ? 1.6f : 1f;
            bg.color = background;

            // Neutral rim so code that recolors targetGraphic keeps working.
            var rim = Outline(r, new Color(1f, 1f, 1f, 0.13f));
            rim.pixelsPerUnitMultiplier = bg.pixelsPerUnitMultiplier;

            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.75f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(PlayClick);
            r.gameObject.AddComponent<UIPress>();

            bool light = Luma(background) > 0.55f;
            var text = Label(r, label, fontSize, light ? Bg : Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 6f);
            text.rectTransform.offsetMin += new Vector2(0f, 3f); // optical center above the bottom lip
            if (light) text.GetComponent<Shadow>().effectColor = new Color(1f, 1f, 1f, 0.25f);
            // Long labels shrink instead of spilling out of the button.
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(text.fontSize * 0.6f));
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return button;
        }

        public static InputField Input(Transform parent, Vector2 position, Vector2 size, string value, int characterLimit, UnityAction<string> onEndEdit)
        {
            var r = Rect("Input", parent);
            Place(r, Center, position, size);
            var bg = Panel(r, new Color(0.03f, 0.035f, 0.045f, 0.85f), false);
            Outline(r, new Color(Accent.r, Accent.g, Accent.b, 0.55f));

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

        /// <summary>Tilted rubber-stamp text in a frame ("ZONE INTERDITE", "NOUVEAU").</summary>
        public static RectTransform Stamp(Transform parent, string text, Color color, int size, float angle)
        {
            var r = Rect("Stamp", parent);
            var t = Label(r, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.GetComponent<Shadow>().enabled = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = t.preferredWidth + size * 0.9f, h = size * 1.45f;
            r.sizeDelta = new Vector2(w, h);
            Stretch(t.rectTransform);
            var frame = Outline(r, new Color(color.r, color.g, color.b, 0.85f), true, 1f);
            frame.pixelsPerUnitMultiplier = 0.6f;
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
            return r;
        }

        public static string FormatTime(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        /// <summary>Every UIKit button clicks (see SFX).</summary>
        static void PlayClick() => SFX.Play(SfxId.Click);

        /// <summary>
        /// Item badge: a framed tile with a drawn pictogram (IconPainter), or the first letter when no glyph exists.
        /// Round = bonuses, square = weapons and the rest.
        /// </summary>
        public static RectTransform Icon(Transform parent, string name, Color color, bool round, float size, string badge)
        {
            var r = Rect("Icon " + name, parent);
            r.sizeDelta = new Vector2(size, size);
            var rim = Lighten(color, 0.1f);
            if (round)
            {
                Image(r, rim, Circle).raycastTarget = false;
                var inner = Rect("Inner", r);
                Stretch(inner, size * 0.07f);
                Image(inner, Tile(color), Circle).raycastTarget = false;
            }
            else
            {
                var bg = Image(r, rim, ChipSprite);
                bg.type = UnityEngine.UI.Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 32f / Mathf.Max(32f, size * 0.9f);
                bg.raycastTarget = false;
                var inner = Rect("Inner", r);
                Stretch(inner, size * 0.06f);
                var ii = Image(inner, Tile(color), ButtonSprite);
                ii.type = UnityEngine.UI.Image.Type.Sliced;
                ii.pixelsPerUnitMultiplier = 64f / Mathf.Max(24f, size * 0.55f);
                ii.raycastTarget = false;
            }

            var glyph = IconPainter.For(name);
            if (glyph)
            {
                var g = Rect("Glyph", r);
                Stretch(g, size * (round ? 0.17f : 0.13f));
                var gi = Image(g, Lighten(color, 0.45f), glyph);
                gi.preserveAspect = true;
                gi.raycastTarget = false;
            }
            else
            {
                string letter = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                var l = Label(r, letter, Mathf.RoundToInt(size * 0.55f), Lighten(color, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
                Stretch(l.rectTransform);
            }
            if (!string.IsNullOrEmpty(badge))
            {
                var b = Label(r, badge, Mathf.RoundToInt(size * 0.32f), Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                Stretch(b.rectTransform, -size * 0.08f);
            }
            return r;
        }

        static Color Lighten(Color c, float k) => new(c.r + (1f - c.r) * k, c.g + (1f - c.g) * k, c.b + (1f - c.b) * k, Mathf.Max(0.9f, c.a));

        /// <summary>Dark tile tinted by the item color, so the light glyph pops.</summary>
        static Color Tile(Color c) => new(0.07f + c.r * 0.2f, 0.08f + c.g * 0.2f, 0.09f + c.b * 0.2f, 0.98f);

        // ---------- sprite factories ----------

        enum Shade { Flat, Panel, Button }

        /// <summary>
        /// White shape with the top-left and bottom-right corners cut at 45°, as a sliced sprite.
        /// <paramref name="stroke"/> &gt; 0 draws only the outline that thick.
        /// </summary>
        static Sprite MakeChamfer(int size, int cut, int border, Shade shade, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            const float r2 = 1.41421356f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                // Signed distance (negative inside): box edges, then the two 45° cuts.
                float d = Mathf.Max(Mathf.Max(-fx, fx - size), Mathf.Max(-fy, fy - size));
                d = Mathf.Max(d, (cut - (fx + (size - fy))) / r2);   // top-left
                d = Mathf.Max(d, (cut - ((size - fx) + fy)) / r2);   // bottom-right
                // Straight edges sit on pixel borders: keep them crisp, only the diagonals are smoothed.
                float a = Mathf.Clamp01(0.5f - d);
                if (stroke > 0f) a *= Mathf.Clamp01(0.5f + d + stroke);

                float v = 1f;
                float t = fy / size; // 0 bottom .. 1 top
                switch (shade)
                {
                    case Shade.Panel:
                        v = Mathf.Lerp(0.86f, 1f, t);
                        break;
                    case Shade.Button:
                        if (y < 5) v = 0.5f;                        // bottom lip (the slab's thickness)
                        else if (y == 5) v = 0.62f;
                        else if (y >= size - 3) v = 1f;            // top highlight
                        else v = Mathf.Lerp(0.72f, 0.93f, (fy - 6f) / (size - 9f));
                        break;
                }
                byte c = (byte)(Mathf.Clamp01(v) * 255f);
                px[y * size + x] = new Color32(c, c, c, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        static Sprite MakeBar()
        {
            const int w = 4, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = (y + 0.5f) / h;
                float v = y >= h - 4 ? 1f : Mathf.Lerp(0.62f, 0.92f, t);
                byte c = (byte)(v * 255f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(c, c, c, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeGlow()
        {
            const int size = 64, border = 30;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size / 2f) - 2f) / (size / 2f - 2f);
                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size / 2f) - 2f) / (size / 2f - 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.SmoothStep(0f, 1f, d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

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

        /// <summary>Edges only (a frame around the screen), so effects tint the borders, not the game.</summary>
        static Sprite MakeVignette()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                // Rounded-rectangle distance (superellipse): stays clear in the middle, rises near every edge.
                float d = Mathf.Pow(Mathf.Pow(dx, 5f) + Mathf.Pow(dy, 5f), 0.2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, d));
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

    /// <summary>Pressed buttons sink a little (like a physical key). Cooperates with UIAnim's pulse.</summary>
    public class UIPress : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        const float Down = 0.94f, Speed = 12f;
        public float Factor { get; private set; } = 1f;
        bool held;
        Selectable selectable;
        UIAnim anim;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => held = !selectable || selectable.interactable;
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => held = false;
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => held = false;

        void OnDisable()
        {
            if (Factor != 1f && !(anim && anim.DrivesScale)) transform.localScale /= Factor;
            Factor = 1f;
            held = false;
        }

        void Update()
        {
            float target = held ? Down : 1f;
            if (Mathf.Approximately(Factor, target)) return;
            float next = Mathf.MoveTowards(Factor, target, Time.unscaledDeltaTime * Speed * (1f - Down) * 4f);
            if (!anim) TryGetComponent(out anim);
            // UIAnim multiplies the factor in itself while it animates; otherwise scale relative to the current scale.
            if (!(anim && anim.DrivesScale)) transform.localScale = transform.localScale / Factor * next;
            Factor = next;
        }
    }
}
