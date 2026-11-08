using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>Flat 2D swatch of a skin for the shop (body pill + accessory shapes + aura), built from UI images.</summary>
    public static class SkinPreview
    {
        /// <summary>Draws the skin centered in a box of <paramref name="size"/> (height). Returns the root.</summary>
        public static RectTransform Draw(Transform parent, SkinDef s, Vector2 anchor, Vector2 position, float size, Color background)
        {
            var root = UIKit.Rect("Preview " + s.id, parent);
            UIKit.Place(root, anchor, position, new Vector2(size, size));

            float w = size * 0.34f;          // body width
            float h = size * 0.6f;           // body height
            float bottom = -size * 0.36f;    // feet (from the center)
            float head = bottom + h;         // top of the body
            Color body = s.body + s.bodyEmission * 0.6f;

            if (s.aura.a > 0f)
            {
                Shape(root, UIKit.Circle, new Color(s.aura.r, s.aura.g, s.aura.b, 0.35f), new Vector2(0, bottom), new Vector2(size * 0.9f, size * 0.24f));
                Shape(root, UIKit.Circle, new Color(s.aura.r, s.aura.g, s.aura.b, 0.55f), new Vector2(0, bottom), new Vector2(size * 0.6f, size * 0.15f));
            }
            else
            {
                Shape(root, UIKit.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0, bottom), new Vector2(size * 0.55f, size * 0.12f));
            }

            // The real outfit, photographed by Survivor > Bake Skin Thumbnails (falls back to the drawn shapes).
            var photo = Thumb(s.id);
            if (photo)
            {
                var r = UIKit.Rect("Photo", root);
                UIKit.Place(r, UIKit.Center, new Vector2(0f, size * 0.02f), new Vector2(size * 0.8f, size));
                var img = UIKit.Image(r, Color.white, photo);
                img.preserveAspect = true;
                img.raycastTarget = false;
                return root;
            }

            // Behind the body
            if (s.accessory == SkinAccessory.Cape)
                Shape(root, UIKit.Square, s.accent, new Vector2(0, bottom + h * 0.42f), new Vector2(w * 1.35f, h * 0.7f));
            if (s.accessory == SkinAccessory.Medic || s.accessory == SkinAccessory.Hazmat)
                Shape(root, UIKit.Square, s.accent2, new Vector2(w * 0.62f, bottom + h * 0.5f), new Vector2(w * 0.38f, h * 0.38f));

            // Body: a pill (two circles + a rectangle)
            Shape(root, UIKit.Square, body, new Vector2(0, bottom + h * 0.5f), new Vector2(w, h - w));
            Shape(root, UIKit.Circle, body, new Vector2(0, head - w * 0.5f), new Vector2(w, w));
            Shape(root, UIKit.Circle, body, new Vector2(0, bottom + w * 0.5f), new Vector2(w, w));

            switch (s.accessory)
            {
                case SkinAccessory.Cap:
                    Shape(root, UIKit.Circle, s.accent, new Vector2(0, head - w * 0.12f), new Vector2(w * 1.06f, w * 0.62f));
                    Shape(root, UIKit.Square, s.accent, new Vector2(w * 0.45f, head - w * 0.22f), new Vector2(w * 0.55f, w * 0.12f));
                    break;
                case SkinAccessory.CowboyHat:
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, head + w * 0.28f), new Vector2(w * 0.72f, w * 0.5f));
                    Shape(root, UIKit.Square, s.accent2, new Vector2(0, head + w * 0.1f), new Vector2(w * 0.74f, w * 0.1f));
                    Shape(root, UIKit.Circle, s.accent, new Vector2(0, head + w * 0.02f), new Vector2(w * 1.9f, w * 0.22f));
                    break;
                case SkinAccessory.Mohawk:
                    Shape(root, UIKit.Triangle, s.accent, new Vector2(0, head + w * 0.22f), new Vector2(w * 0.36f, w * 0.7f));
                    Shape(root, UIKit.Triangle, s.accent, new Vector2(-w * 0.2f, head + w * 0.1f), new Vector2(w * 0.3f, w * 0.5f));
                    Shape(root, UIKit.Triangle, s.accent, new Vector2(w * 0.2f, head + w * 0.1f), new Vector2(w * 0.3f, w * 0.5f));
                    break;
                case SkinAccessory.Medic:
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, head - w * 0.42f), new Vector2(w * 1.02f, w * 0.14f));
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, bottom + h * 0.45f), new Vector2(w * 0.42f, w * 0.13f));
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, bottom + h * 0.45f), new Vector2(w * 0.13f, w * 0.42f));
                    break;
                case SkinAccessory.Crown:
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, head + w * 0.02f), new Vector2(w * 0.8f, w * 0.2f));
                    for (int i = -1; i <= 1; i++)
                        Shape(root, UIKit.Triangle, s.accent, new Vector2(i * w * 0.27f, head + w * 0.24f), new Vector2(w * 0.26f, w * 0.3f));
                    Shape(root, UIKit.Circle, s.accent2, new Vector2(0, head + w * 0.02f), new Vector2(w * 0.14f, w * 0.14f));
                    break;
                case SkinAccessory.Viking:
                    Horn(root, s.accent2, new Vector2(w * 0.52f, head + w * 0.05f), new Vector2(w * 0.26f, w * 0.5f), -40f);
                    Horn(root, s.accent2, new Vector2(-w * 0.52f, head + w * 0.05f), new Vector2(w * 0.26f, w * 0.5f), 40f);
                    Shape(root, UIKit.Circle, s.accent, new Vector2(0, head - w * 0.12f), new Vector2(w * 1.08f, w * 0.75f));
                    break;
                case SkinAccessory.Hazmat:
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, head - w * 0.55f), new Vector2(w * 0.72f, w * 0.26f));
                    break;
                case SkinAccessory.Halo:
                    Shape(root, UIKit.Circle, s.accent, new Vector2(0, head + w * 0.35f), new Vector2(w * 1.05f, w * 0.34f));
                    Shape(root, UIKit.Circle, background, new Vector2(0, head + w * 0.35f), new Vector2(w * 0.75f, w * 0.17f));
                    break;
                case SkinAccessory.DemonHorns:
                    Horn(root, s.accent, new Vector2(w * 0.25f, head + w * 0.1f), new Vector2(w * 0.24f, w * 0.45f), -20f);
                    Horn(root, s.accent, new Vector2(-w * 0.25f, head + w * 0.1f), new Vector2(w * 0.24f, w * 0.45f), 20f);
                    break;
                case SkinAccessory.Neon:
                    Shape(root, UIKit.Square, new Color(0.15f, 0.15f, 0.18f), new Vector2(0, head + w * 0.2f), new Vector2(w * 0.06f, w * 0.4f));
                    Shape(root, UIKit.Circle, s.accent, new Vector2(0, head + w * 0.42f), new Vector2(w * 0.24f, w * 0.24f));
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, head - w * 0.55f), new Vector2(w * 0.86f, w * 0.11f));
                    break;
                case SkinAccessory.Cape:
                    Shape(root, UIKit.Square, s.accent, new Vector2(0, head - w * 0.95f), new Vector2(w * 1.15f, w * 0.14f));
                    break;
            }
            return root;
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> thumbs = new();

        static Sprite Thumb(string id)
        {
            string key = string.IsNullOrEmpty(id) ? "skin_default" : id;
            if (thumbs.TryGetValue(key, out var s) && s) return s;
            s = Resources.Load<Sprite>("SkinThumbs/" + key);
            thumbs[key] = s;
            return s;
        }

        static Image Shape(RectTransform root, Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            var r = UIKit.Rect("Shape", root);
            UIKit.Place(r, UIKit.Center, pos, size);
            var img = UIKit.Image(r, color, sprite);
            img.raycastTarget = false;
            return img;
        }

        static void Horn(RectTransform root, Color color, Vector2 pos, Vector2 size, float angle)
        {
            var img = Shape(root, UIKit.Triangle, color, pos, size);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
