using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Ranked mode reminder during a run: a small label under the timer ("DÉFI DU JOUR · x1.45", "… · ENTRAÎNEMENT")
    /// and an opening banner with the mode's rules. Own canvas just under the main HUD (level-up / pause overlays cover it).
    /// Hidden in the campaign, and while a boss is alive (the boss bar uses that spot).
    /// Added at runtime by GameManager.Awake.
    /// </summary>
    public class ModeHUD : MonoBehaviour
    {
        RectTransform canvasRect, safeRoot, pill;
        Text label;
        Rect appliedSafeArea;
        bool introShown;

        static readonly Color PracticeColor = new(1f, 0.55f, 0.45f);

        void Awake()
        {
            canvasRect = UIKit.Canvas("Mode HUD Canvas", transform, 9);
            safeRoot = UIKit.Rect("Safe Area", canvasRect);
            ApplySafeArea();

            pill = UIKit.Rect("Mode Label", safeRoot);
            UIKit.Place(pill, UIKit.Top, new Vector2(0, -146), new Vector2(520, 38));
            UIKit.Image(pill, new Color(0f, 0f, 0f, 0.45f)).raycastTarget = false;
            label = UIKit.Label(pill, "", 26, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(label.rectTransform);
            pill.gameObject.SetActive(false);
        }

        void Start()
        {
            if (!GameManager.Instance) return;
            GameManager.Instance.StateChanged += OnStateChanged;
            OnStateChanged(GameManager.Instance.State);
        }

        void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged -= OnStateChanged;
        }

        bool Shown => RunConfig.Mode != GameMode.Campaign;

        void OnStateChanged(GameState state)
        {
            bool inRun = state is GameState.Playing or GameState.Paused or GameState.LevelUp;
            canvasRect.gameObject.SetActive(inRun && Shown);
            if (!inRun || !Shown) return;

            RefreshLabel();

            if (state == GameState.Playing && !introShown)
            {
                introShown = true;
                if (GameHUD.Instance) GameHUD.Instance.ShowBanner(IntroText(), RunConfig.SubmitScore ? Color.white : PracticeColor, 2.8f);
            }
        }

        int shownDanger = -1;

        /// <summary>"CLASSIQUE  ·  DANGER 3", the tier colored from yellow to red.</summary>
        void RefreshLabel()
        {
            int danger = RunDirector.Instance ? RunDirector.Instance.Danger : 1;
            shownDanger = danger;
            string hex = ColorUtility.ToHtmlStringRGB(RunDirector.DangerColor(danger));
            label.text = $"{GameModes.RunLabel}   ·   <color=#{hex}>DANGER {danger}</color>";
            label.color = RunConfig.SubmitScore ? UIKit.Gold : PracticeColor;
            float width = Mathf.Clamp(label.preferredWidth + 48f, 260f, 900f);
            pill.sizeDelta = new Vector2(width, 38f);
        }

        static string IntroText()
        {
            string rules = RunConfig.Mode switch
            {
                GameMode.Daily => $"Arme : {GameModes.WeaponName(RunConfig.ForcedStartWeapon)}  ·  {ThreatLine()}",
                GameMode.Weekly => $"Cartes {Synergies.TagName(RunConfig.BoostedTag)} x2  ·  {ThreatLine()}",
                GameMode.Menaces => RunConfig.ThreatMask == 0 ? "Aucune condition : x1.00" : ThreatLine(),
                _ => "Mêmes règles pour tous",
            };
            string title = RunConfig.Mode == GameMode.Weekly
                ? "SEMAINE : " + Synergies.TagName(RunConfig.BoostedTag).ToUpperInvariant()
                : GameModes.ModeName(RunConfig.Mode);
            if (!RunConfig.SubmitScore) rules = "Entraînement : score hors Registre";
            return $"{title}\n<size=40>{rules}</size>";
        }

        static string ThreatLine()
        {
            int n = Threats.Count(RunConfig.ThreatMask);
            return n == 0 ? "sans condition" : $"{n} condition{(n > 1 ? "s" : "")} {Threats.FormatMult(RunConfig.ScoreMult)}";
        }

        void Update()
        {
            if (!canvasRect.gameObject.activeSelf) return;
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea();
            bool boss = RunDirector.Instance && RunDirector.Instance.CurrentBoss;
            if (pill.gameObject.activeSelf == boss) pill.gameObject.SetActive(!boss);
            if (RunDirector.Instance && RunDirector.Instance.Danger != shownDanger) RefreshLabel();
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
    }
}
