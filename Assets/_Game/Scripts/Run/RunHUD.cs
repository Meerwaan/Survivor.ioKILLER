using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>Something in the world the HUD should point out: a label above it, an arrow when off-screen.</summary>
    public interface IHudTarget
    {
        bool HudActive { get; }
        Vector3 HudPosition { get; }
        /// <summary>Rich text allowed. Return a cached string (called every frame).</summary>
        string HudLabel { get; }
        Color HudColor { get; }
        bool HudArrow { get; }
        float HudScale { get; }
    }

    /// <summary>
    /// HUD for the run spice, on its own canvas just under the main HUD (so level-up / pause overlays cover it):
    /// icon column of owned relics and pacts, world labels (elite affixes, altars, relics, bounty, zone),
    /// edge arrows toward important things off-screen, and a status line for the running event.
    /// Added at runtime by ArenaEvents.
    /// </summary>
    public class RunHUD : MonoBehaviour
    {
        public static RunHUD Instance { get; private set; }

        /// <summary>Everything that wants a label / arrow. Register on spawn, unregister on despawn.</summary>
        public static readonly List<IHudTarget> Targets = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Targets.Clear();

        public static void Register(IHudTarget t)
        {
            if (t != null && !Targets.Contains(t)) Targets.Add(t);
        }

        public static void Unregister(IHudTarget t) => Targets.Remove(t);

        const int LabelCount = 10, ArrowCount = 6;
        const float IconSize = 66f, IconGap = 10f;

        Camera cam;
        RectTransform canvasRect, safeRoot, root, perkColumn;
        Rect appliedSafeArea;
        Text statusText, debugText;
        float nextDebug;
        Text[] labels;
        RectTransform[] arrows;
        Image[] arrowImages;
        Text[] arrowTexts;
        int shownVersion = -1, shownPerks;
        string shownStatus;

        void Awake()
        {
            Instance = this;
            Targets.Clear();
            Build();
        }

        void Start()
        {
            cam = Camera.main;
            if (GameManager.Instance)
            {
                GameManager.Instance.StateChanged += OnStateChanged;
                OnStateChanged(GameManager.Instance.State);
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }

        void OnStateChanged(GameState state)
        {
            bool inRun = state is GameState.Playing or GameState.Paused or GameState.LevelUp;
            root.gameObject.SetActive(inRun);
        }

        // ---------- Build ----------

        void Build()
        {
            canvasRect = UIKit.Canvas("Run HUD Canvas", transform, 9);
            root = UIKit.Rect("Run HUD", canvasRect);
            UIKit.Stretch(root);

            labels = new Text[LabelCount];
            for (int i = 0; i < LabelCount; i++)
            {
                var l = UIKit.Label(root, "", 36, Color.white, TextAnchor.LowerCenter, FontStyle.Bold);
                l.rectTransform.sizeDelta = new Vector2(460, 200);
                l.rectTransform.pivot = new Vector2(0.5f, 0f);
                l.lineSpacing = 0.9f;
                l.gameObject.SetActive(false);
                labels[i] = l;
            }

            arrows = new RectTransform[ArrowCount];
            arrowImages = new Image[ArrowCount];
            arrowTexts = new Text[ArrowCount];
            for (int i = 0; i < ArrowCount; i++)
            {
                var a = UIKit.Rect("Arrow", root);
                a.sizeDelta = new Vector2(64, 64);
                arrowImages[i] = UIKit.Image(a, Color.white, UIKit.Triangle);
                arrowImages[i].raycastTarget = false;
                var t = UIKit.Label(root, "", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.sizeDelta = new Vector2(260, 40);
                t.gameObject.SetActive(false);
                arrowTexts[i] = t;
                a.gameObject.SetActive(false);
                arrows[i] = a;
            }

            safeRoot = UIKit.Rect("Run Safe Area", root);
            ApplySafeArea();

            perkColumn = UIKit.Rect("Perks", safeRoot);
            UIKit.Place(perkColumn, UIKit.TopLeft, new Vector2(30, -372), new Vector2(2 * IconSize + IconGap, 800));

            statusText = UIKit.Label(safeRoot, "", 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(statusText.rectTransform, UIKit.Top, new Vector2(0, -352), new Vector2(860, 64));
            statusText.gameObject.SetActive(false);

            // Tuning readout (ComfortMeter.ShowDebug).
            debugText = UIKit.Label(safeRoot, "", 26, new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerLeft);
            UIKit.Place(debugText.rectTransform, new Vector2(0f, 0f), new Vector2(20, 20), new Vector2(1040, 40));
            debugText.gameObject.SetActive(false);
        }

        void ApplySafeArea()
        {
            appliedSafeArea = Screen.safeArea;
            if (!safeRoot || Screen.width <= 0 || Screen.height <= 0) return;
            var size = new Vector2(Screen.width, Screen.height);
            safeRoot.anchorMin = appliedSafeArea.position / size;
            safeRoot.anchorMax = (appliedSafeArea.position + appliedSafeArea.size) / size;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        // ---------- Per frame ----------

        void Update()
        {
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea();
            if (!root.gameObject.activeSelf) return;

            var mods = RunModifiers.Instance;
            if (mods && mods.Version != shownVersion) RebuildPerks(mods);

            bool debug = ComfortMeter.ShowDebug && ComfortMeter.Instance;
            if (debugText.gameObject.activeSelf != debug) debugText.gameObject.SetActive(debug);
            if (debug && Time.unscaledTime >= nextDebug)
            {
                nextDebug = Time.unscaledTime + 0.25f;
                int level = WorldEvents.Instance ? WorldEvents.Instance.PressureLevel : 0;
                debugText.text = ComfortMeter.Instance.DebugLine() + $"  pression {level}";
            }

            string status = WorldEvents.Instance ? WorldEvents.Instance.StatusText : null;
            bool showStatus = !string.IsNullOrEmpty(status);
            if (statusText.gameObject.activeSelf != showStatus) statusText.gameObject.SetActive(showStatus);
            if (showStatus)
            {
                if (!ReferenceEquals(status, shownStatus))
                {
                    shownStatus = status;
                    statusText.text = status;
                }
                var ev = WorldEvents.Instance;
                statusText.color = ev.StatusUrgent && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? new Color(1f, 0.35f, 0.25f) : ev.StatusColor;
            }
        }

        void RebuildPerks(RunModifiers mods)
        {
            shownVersion = mods.Version;
            foreach (Transform child in perkColumn) Destroy(child.gameObject);
            var owned = mods.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var p = owned[i];
                bool spent = mods.IsSpent(p);
                Color c = spent ? new Color(0.35f, 0.35f, 0.35f) : p.color;
                int col = i % 2, row = i / 2;
                var holder = UIKit.Rect("Perk " + p.name, perkColumn);
                holder.anchorMin = holder.anchorMax = holder.pivot = UIKit.TopLeft;
                holder.sizeDelta = new Vector2(IconSize, IconSize);
                holder.anchoredPosition = new Vector2(col * (IconSize + IconGap), -row * (IconSize + IconGap));
                if (p.IsPact)
                {
                    // Pacts: blood-red frame, so the curse is never forgotten.
                    var frame = UIKit.Rect("Frame", holder);
                    UIKit.Stretch(frame, -5f);
                    UIKit.Image(frame, RunModifiers.PactColor).raycastTarget = false;
                }
                var icon = UIKit.Icon(holder, p.name, c, !p.IsPact, IconSize, "");
                icon.anchorMin = icon.anchorMax = icon.pivot = UIKit.Center;
                icon.anchoredPosition = Vector2.zero;
                if (i >= shownPerks) UIAnim.SlideIn(holder, new Vector2(120, 0), 0.05f, 0.4f, 2f);
            }
            shownPerks = owned.Count;
        }

        void LateUpdate()
        {
            if (!root.gameObject.activeSelf) return;
            if (!cam) cam = Camera.main;
            if (!cam) return;

            int label = 0, arrow = 0;
            float m = 80f * UIKit.PixelsPerUnit;
            var targets = Targets;
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var t = targets[i];
                if (t == null || (t is Object o && !o))
                {
                    targets.RemoveAt(i);
                    continue;
                }
                if (!t.HudActive) continue;

                Vector3 screen = cam.WorldToScreenPoint(t.HudPosition);
                bool onScreen = screen.z > 0f && screen.x > -m && screen.x < Screen.width + m && screen.y > -m && screen.y < Screen.height + m;
                if (onScreen)
                {
                    if (label >= labels.Length) continue;
                    var l = labels[label++];
                    if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);
                    string text = t.HudLabel;
                    if (!ReferenceEquals(l.text, text)) l.text = text;
                    l.color = t.HudColor;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                    l.rectTransform.anchoredPosition = local;
                    l.rectTransform.localScale = Vector3.one * t.HudScale;
                }
                else if (t.HudArrow && arrow < arrows.Length)
                {
                    PlaceArrow(arrow++, screen, t);
                }
            }

            for (int i = label; i < labels.Length; i++)
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
            for (int i = arrow; i < arrows.Length; i++)
            {
                if (arrows[i].gameObject.activeSelf) arrows[i].gameObject.SetActive(false);
                if (arrowTexts[i].gameObject.activeSelf) arrowTexts[i].gameObject.SetActive(false);
            }
        }

        static readonly Dictionary<string, string> ArrowNames = new();

        /// <summary>First line of the label without rich text tags (cached).</summary>
        static string ShortName(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            if (ArrowNames.TryGetValue(label, out var s)) return s;
            var sb = new System.Text.StringBuilder();
            bool tag = false;
            foreach (char ch in label)
            {
                if (ch == '\n') break;
                if (ch == '<') tag = true;
                else if (ch == '>') tag = false;
                else if (!tag) sb.Append(ch);
            }
            s = sb.ToString();
            if (s.Length > 16) s = s.Substring(0, 16);
            ArrowNames[label] = s;
            return s;
        }

        void PlaceArrow(int index, Vector3 screen, IHudTarget t)
        {
            var a = arrows[index];
            var txt = arrowTexts[index];
            if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
            if (!txt.gameObject.activeSelf) txt.gameObject.SetActive(true);

            float m = 70f * UIKit.PixelsPerUnit;
            Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = (Vector2)screen - center;
            if (screen.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            float sx = (center.x - m) / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
            float sy = (center.y - m) / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
            Vector2 edge = center + dir * Mathf.Min(sx, sy);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, edge, null, out var local);
            a.anchoredPosition = local;
            a.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            a.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 8f + index) * 0.1f);

            Color c = t.HudColor;
            if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f) c = RunModifiers.PactColor; // white labels (altars): tint the arrow
            arrowImages[index].color = c;

            // Name tucked just inside the arrow, toward the screen center.
            Vector2 inward = -dir.normalized * 62f;
            txt.rectTransform.anchoredPosition = local + inward;
            string name = ShortName(t.HudLabel);
            if (!ReferenceEquals(txt.text, name)) txt.text = name;
            txt.color = c;
        }
    }
}
