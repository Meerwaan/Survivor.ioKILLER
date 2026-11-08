using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Floating damage numbers. Crits are big, orange and bounce; burn ticks are small and orange.
    /// A fixed pool of labels: when it runs out, the oldest one is reused.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        public enum Kind { Normal, Crit, Burn }

        public static DamageNumbers Instance { get; private set; }

        public bool show = true;
        public int poolSize = 80;
        public float lifetime = 0.6f;

        static readonly Color CritColor = new(1f, 0.55f, 0.1f);
        static readonly Color BurnColor = new(1f, 0.6f, 0.25f);
        static readonly Color BigColor = new(1f, 0.92f, 0.45f);

        class Item
        {
            public Text text;
            public RectTransform rect;
            public Vector3 world;
            public Vector2 drift;
            public float age, life, scale;
            public Color color;
            public bool live;
        }

        Item[] items;
        int next;
        Camera cam;
        RectTransform canvasRect;
        static readonly string[] Cache = new string[2000];
        static readonly string[] CritCache = new string[2000];

        void Awake()
        {
            Instance = this;
            canvasRect = UIKit.Canvas("Damage Numbers", transform, 5);
            canvasRect.GetComponent<GraphicRaycaster>().enabled = false;

            items = new Item[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var t = UIKit.Label(canvasRect, "", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.sizeDelta = new Vector2(260, 80);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.gameObject.SetActive(false);
                items[i] = new Item { text = t, rect = t.rectTransform };
            }
        }

        void Start() => cam = Camera.main;

        public void Show(Vector3 worldPosition, float amount, Kind kind = Kind.Normal)
        {
            if (!show || amount < 0.5f) return;

            var item = items[next];
            next = (next + 1) % items.Length;

            int n = Mathf.RoundToInt(amount);
            item.world = worldPosition + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            item.age = 0f;
            item.live = true;
            item.drift = new Vector2(Random.Range(-60f, 60f), 0f);

            switch (kind)
            {
                case Kind.Crit:
                    item.text.text = n < CritCache.Length ? CritCache[n] ??= n + "!" : n + "!";
                    item.text.fontSize = 58;
                    item.color = CritColor;
                    item.scale = 1.8f;
                    item.life = lifetime * 1.4f;
                    break;
                case Kind.Burn:
                    item.text.text = ToText(n);
                    item.text.fontSize = 30;
                    item.color = BurnColor;
                    item.scale = 1f;
                    item.life = lifetime * 0.8f;
                    break;
                default:
                    item.text.text = ToText(n);
                    item.text.fontSize = amount >= 40f ? 46 : 38;
                    item.color = amount >= 40f ? BigColor : Color.white;
                    item.scale = 1.35f;
                    item.life = lifetime;
                    break;
            }
            item.text.color = item.color;
            item.text.gameObject.SetActive(true);
        }

        static string ToText(int n)
        {
            if (n < 0 || n >= Cache.Length) return n.ToString();
            return Cache[n] ??= n.ToString();
        }

        void LateUpdate()
        {
            if (!cam) return;
            // Real time, so numbers still fade out while the game is paused.
            float dt = Time.unscaledDeltaTime;

            foreach (var item in items)
            {
                if (!item.live) continue;

                item.age += dt;
                if (item.age >= item.life)
                {
                    item.live = false;
                    item.text.gameObject.SetActive(false);
                    continue;
                }

                float k = item.age / item.life;
                // Jump up quickly then settle, like a little bounce.
                float rise = 1.8f + (1f - (1f - k) * (1f - k)) * 1.3f;
                Vector3 screen = cam.WorldToScreenPoint(item.world + Vector3.up * rise);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                item.rect.anchoredPosition = local + item.drift * k;

                float pop = k < 0.12f ? Mathf.Lerp(item.scale, 1f, k / 0.12f) : 1f;
                item.rect.localScale = Vector3.one * pop;

                var c = item.color;
                c.a = 1f - k * k * k;
                item.text.color = c;
            }
        }
    }
}
