using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// UI helpers shared by the monetization overlays (ads, store, revive) and the ad buttons in the menus.
    /// Overlays get their own canvas that survives scene reloads and sits above everything else.
    /// </summary>
    public static class MonetizationUI
    {
        /// <summary>Sort orders: above the HUD (10), menu (20) and run result (30).</summary>
        public const int ReviveOrder = 40;
        public const int AnnounceOrder = 990;
        public const int AdOrder = 1000;
        public const int StoreOrder = 1010;

        public static readonly Color AdYellow = new(1f, 0.82f, 0.2f);
        public static readonly Color AdButton = new(0.16f, 0.45f, 0.75f);
        public static readonly Color Premium = new(0.55f, 0.25f, 0.75f);

        /// <summary>Full-screen canvas for an overlay (destroy its GameObject when done).</summary>
        public static RectTransform NewCanvas(string name, int order)
        {
            var canvas = UIKit.Canvas(name, null, order);
            Object.DontDestroyOnLoad(canvas.gameObject);
            return canvas;
        }

        /// <summary>Small yellow "PUB" tag, glued to a corner of an ad button so the player always knows.</summary>
        public static RectTransform AdTag(Transform parent, Vector2 anchor, Vector2 position, float height = 44f)
        {
            var tag = UIKit.Rect("Ad Tag", parent);
            UIKit.Place(tag, anchor, position, new Vector2(height * 1.9f, height));
            UIKit.Image(tag, AdYellow).raycastTarget = false;
            var t = UIKit.Label(tag, "PUB", Mathf.RoundToInt(height * 0.62f), Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.GetComponent<Shadow>().enabled = false;
            UIKit.Stretch(t.rectTransform);
            return tag;
        }

        /// <summary>Button that plays a rewarded ad (blue, with the "PUB" tag on its top-left corner).</summary>
        public static Button AdButtonUI(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, UnityAction onClick, int fontSize, bool enabled = true)
        {
            var b = MetaUI.Button(parent, label, AdButton, anchor, position, size, onClick, fontSize, enabled);
            AdTag(b.transform, UIKit.TopLeft, new Vector2(-8, 14), 40f);
            return b;
        }

        public static void SetLabel(Button b, string text)
        {
            if (!b) return;
            var t = b.GetComponentInChildren<Text>();
            if (t) t.text = text;
        }

        /// <summary>Greys a button out (or back) without rebuilding it.</summary>
        public static void SetEnabled(Button b, bool enabled, Color color)
        {
            if (!b) return;
            b.interactable = enabled;
            if (b.targetGraphic) b.targetGraphic.color = enabled ? color : UIKit.Dark;
            var t = b.GetComponentInChildren<Text>();
            if (t) t.color = enabled ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        }

        /// <summary>"1 h 05" / "12 min" / "45 s".</summary>
        public static string Duration(float seconds)
        {
            if (seconds < 60f) return $"{Mathf.CeilToInt(Mathf.Max(0f, seconds))} s";
            int total = Mathf.CeilToInt(seconds / 60f);
            int h = total / 60, m = total % 60;
            return h > 0 ? $"{h} h {m:00}" : $"{m} min";
        }
    }
}
