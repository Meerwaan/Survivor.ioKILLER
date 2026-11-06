using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Main menu shown over the arena before a run: top resource bar, bottom tabs
    /// (Jouer / Camp / Codex / Missions / Profil), campaign stage select, endless mode and leaderboard.
    /// Pages are rebuilt from code on every change (simple, and it only runs on taps).
    /// The tab pages live in Scripts/Meta/UI/MenuScreen.*.cs (partial class).
    /// </summary>
    public partial class MenuScreen : MonoBehaviour
    {
        enum Page { Home, Stages, Board, Camp, Codex, Missions, Profile, Threats, ModeInfo }

        static readonly string[] TabNames = { "JOUER", "CAMP", "CODEX", "MISSIONS", "PROFIL" };
        const int BoardSize = 15;

        RectTransform root, page, popupLayer, fxLayer;
        RectTransform goldChip;
        Text goldText, starsText;
        readonly Button[] navButtons = new Button[5];
        readonly RectTransform[] navBadges = new RectTransform[5];

        Page current = Page.Home;
        ScrollRect pageScroll;
        InputField nameField;
        RectTransform boardRows;
        float liveTimer;
        /// <summary>Per-page refresh run twice a second (camp storage, timers).</summary>
        Action liveUpdate;

        void Awake() => Build();

        void Start()
        {
            GameManager.Instance.StateChanged += OnStateChanged;
            Progression.Changed += OnMetaChanged;
            OnStateChanged(GameManager.Instance.State);
        }

        void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged -= OnStateChanged;
            Progression.Changed -= OnMetaChanged;
        }

        /// <summary>Board to open when the menu shows up next (result screen "CLASSEMENT"). Static: survives the scene reload.</summary>
        static GameMode? pendingBoard;
        /// <summary>Tab of the board page.</summary>
        GameMode boardMode = GameMode.Classic;
        bool menuShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => pendingBoard = null;

        public static void RequestBoard(GameMode mode) => pendingBoard = mode == GameMode.Campaign ? GameMode.Classic : mode;

        void OnStateChanged(GameState state)
        {
            bool menu = state == GameState.Menu;
            root.gameObject.SetActive(menu);
            // Start() and GameManager.Start() can both report Menu: build the page only once per visit.
            if (!menu || menuShown)
            {
                menuShown = menu;
                return;
            }
            menuShown = true;
            if (pendingBoard.HasValue)
            {
                boardMode = pendingBoard.Value;
                pendingBoard = null;
                ShowPage(Page.Board);
            }
            else ShowPage(Page.Home);
        }

        void OnMetaChanged()
        {
            if (root && root.gameObject.activeInHierarchy) RefreshTopBar();
        }

        void Update()
        {
            if (!root.gameObject.activeInHierarchy) return;
            liveTimer -= Time.unscaledDeltaTime;
            if (liveTimer > 0f) return;
            liveTimer = 0.5f;
            RefreshTopBar();
            liveUpdate?.Invoke();
        }

        // ---------- Pages ----------

        void ShowPage(Page p, bool keepScroll = false)
        {
            float scrollY = keepScroll && pageScroll ? pageScroll.content.anchoredPosition.y : 0f;
            bool samePage = p == current;
            current = p;
            liveUpdate = null;
            pageScroll = null;
            nameField = null;
            ClosePopup();
            foreach (Transform child in page) Destroy(child.gameObject);

            switch (p)
            {
                case Page.Home: BuildHome(); break;
                case Page.Stages: BuildStages(!(keepScroll && samePage)); break;
                case Page.Board: BuildBoardPage(); break;
                case Page.Camp: BuildCamp(); break;
                case Page.Codex: BuildCodex(); break;
                case Page.Missions: BuildMissions(); break;
                case Page.Profile: BuildProfile(); break;
                case Page.Threats: BuildThreats(); break;
                case Page.ModeInfo: BuildModeInfo(); break;
            }
            liveUpdate?.Invoke(); // fill the live texts (timers) right away

            if (keepScroll && samePage && pageScroll) pageScroll.content.anchoredPosition = new Vector2(0f, scrollY);
            UpdateNav();
            RefreshTopBar();
        }

        /// <summary>Rebuilds the current page, keeping the scroll position.</summary>
        void Refresh() => ShowPage(current, true);

        static int TabOf(Page p) => p switch
        {
            Page.Camp => 1,
            Page.Codex => 2,
            Page.Missions => 3,
            Page.Profile => 4,
            _ => 0,
        };

        void OnTab(int tab)
        {
            var p = tab switch
            {
                1 => Page.Camp,
                2 => Page.Codex,
                3 => Page.Missions,
                4 => Page.Profile,
                _ => Page.Home,
            };
            if (p == current) return;
            ShowPage(p);
        }

        void UpdateNav()
        {
            int tab = TabOf(current);
            for (int i = 0; i < navButtons.Length; i++)
                navButtons[i].targetGraphic.color = i == tab ? UIKit.Blood : UIKit.Dark;
        }

        void RefreshTopBar()
        {
            goldText.text = Progression.Gold.ToString();
            starsText.text = $"{Campaign.TotalStars}/{Campaign.MaxStars}";

            SetBadge(1, Camp.Fill >= 0.25f ? "!" : null);
            int claim = Missions.ClaimableCount;
            SetBadge(3, claim > 0 ? claim.ToString() : null);
            int fresh = Cosmetics.NewCount;
            SetBadge(4, fresh > 0 ? fresh.ToString() : null);
        }

        void SetBadge(int tab, string text)
        {
            var b = navBadges[tab];
            bool on = !string.IsNullOrEmpty(text);
            if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
            if (on) b.GetComponentInChildren<Text>().text = text;
        }

        // ---------- Shell ----------

        void Build()
        {
            var canvas = UIKit.Canvas("Menu Canvas", transform, 20);
            root = UIKit.Overlay("Menu", canvas, 0.6f);

            page = UIKit.Rect("Page", root);
            UIKit.Stretch(page);

            // Top bar: resources
            var bar = MetaUI.Card(root, "Top Bar", UIKit.Center, new Vector2(0, 895), new Vector2(1080, 120), new Color(0f, 0f, 0f, 0.55f));
            goldChip = Chip(bar, -230, 520, UIKit.Gold, UIKit.Circle, out goldText);
            Chip(bar, 330, 300, UIKit.Gold, MetaUI.Star, out starsText);

            // Bottom tabs
            var nav = MetaUI.Card(root, "Nav", UIKit.Center, new Vector2(0, -880), new Vector2(1080, 160), new Color(0.05f, 0.05f, 0.06f, 0.97f));
            for (int i = 0; i < TabNames.Length; i++)
            {
                int tab = i;
                navButtons[i] = UIKit.Button(nav, TabNames[i], UIKit.Dark, UIKit.Center, new Vector2(-432 + i * 216, 0), new Vector2(208, 144), () => OnTab(tab), 32);
                navBadges[i] = MetaUI.Badge(navButtons[i].transform, new Vector2(-4, -4), "!");
                navBadges[i].gameObject.SetActive(false);
            }

            popupLayer = UIKit.Rect("Popup", root);
            UIKit.Stretch(popupLayer);
            popupLayer.gameObject.SetActive(false);

            fxLayer = UIKit.Rect("FX", root);
            UIKit.Stretch(fxLayer);
        }

        static RectTransform Chip(RectTransform bar, float x, float width, Color color, Sprite sprite, out Text text)
        {
            var chip = MetaUI.Card(bar, "Chip", UIKit.Center, new Vector2(x, 0), new Vector2(width, 90), new Color(0f, 0f, 0f, 0.5f));
            var icon = UIKit.Rect("Icon", chip);
            UIKit.Place(icon, UIKit.Left, new Vector2(16, 0), new Vector2(58, 58));
            UIKit.Image(icon, color, sprite).raycastTarget = false;
            text = MetaUI.Text(chip, "", 46, color, TextAnchor.MiddleLeft, UIKit.Left, new Vector2(90, 0), new Vector2(width - 100, 80), FontStyle.Bold);
            return chip;
        }

        /// <summary>Big page title + subtitle under the top bar. Returns the subtitle.</summary>
        Text Header(string title, string subtitle)
        {
            MetaUI.Text(page, title, 74, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 780), new Vector2(640, 100), FontStyle.Bold);
            return MetaUI.Text(page, subtitle, 32, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 708), new Vector2(1040, 50));
        }

        void BackButton(Page target)
        {
            UIKit.Button(page, "RETOUR", UIKit.Dark, UIKit.Center, new Vector2(-420, 780), new Vector2(210, 100), () => ShowPage(target), 36);
        }

        static Text SectionTitle(RectTransform content, string text, float y)
        {
            return MetaUI.Text(content, text, 40, UIKit.Gold, TextAnchor.LowerLeft, UIKit.Top, new Vector2(0, -y), new Vector2(960, 60), FontStyle.Bold);
        }

        static string FormatDuration(float seconds)
        {
            int total = Mathf.CeilToInt(seconds / 60f);
            int h = total / 60, m = total % 60;
            return h > 0 ? $"{h} h {m:00}" : $"{m} min";
        }

        // ---------- Home ----------

        int HomeStage()
        {
            int stage = Mathf.Clamp(MetaSave.Data.selectedStage, 0, Campaign.Count - 1);
            return Campaign.IsUnlocked(stage) ? stage : Campaign.NextStage;
        }

        /// <summary>
        /// Scrolling home: title, nickname, campaign card (onboarding), the 4 ranked modes, leaderboards,
        /// start weapon and the camp shortcut at the bottom.
        /// </summary>
        void BuildHome()
        {
            var content = MetaUI.Scroll(page, new Vector2(0, 15), new Vector2(1080, 1610), out pageScroll);
            float y = 6f;

            var title = MetaUI.Text(content, "SURVIVOR\nKILLER", 100, UIKit.Blood, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y), new Vector2(1060, 220), FontStyle.Bold);
            title.lineSpacing = 0.85f;
            UIAnim.Pulse(title.rectTransform, 1.5f, 0.02f);
            y += 222f;

            MetaUI.Text(content, "Pseudo", 30, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y), new Vector2(600, 40));
            y += 42f;
            nameField = UIKit.Input(content, Vector2.zero, new Vector2(620, 96), PlayerProfile.Name, PlayerProfile.MaxLength,
                value => PlayerProfile.Name = value);
            UIKit.Place((RectTransform)nameField.transform, UIKit.Top, new Vector2(0, -y), new Vector2(620, 96));
            y += 120f;

            // Campaign card (the onboarding)
            int stage = HomeStage();
            var s = Campaign.Stages[stage];
            var card = MetaUI.Card(content, "Campaign", UIKit.Top, new Vector2(0, -y), new Vector2(980, 380), UIKit.Card);
            y += 410f;
            var stripe = MetaUI.Card(card, "Stripe", UIKit.Left, Vector2.zero, new Vector2(12, 380), Campaign.ChapterColors[s.chapter]);
            stripe.SetAsFirstSibling();
            MetaUI.Text(card, "CAMPAGNE", 40, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(36, -20), new Vector2(500, 50), FontStyle.Bold);
            MetaUI.Stars(card, UIKit.TopRight, new Vector2(-30, -20), 54, Campaign.StarMask(stage));
            MetaUI.Text(card, Campaign.ChapterTitle(s.chapter), 32, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(36, -72), new Vector2(900, 44));
            MetaUI.Text(card, Campaign.Title(stage), 50, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(36, -114), new Vector2(920, 64), FontStyle.Bold);
            string info = Campaign.AllCleared ? "Campagne terminée ! Va chercher les 3 étoiles partout." : $"Survis {UIKit.FormatTime(Mathf.RoundToInt(s.duration))}  ·  Ennemis x{s.health:0.0#}";
            MetaUI.Text(card, info, 30, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(36, -180), new Vector2(920, 42));

            var play = UIKit.Button(card, $"JOUER {Campaign.Code(stage)}", UIKit.Blood, Vector2.zero, new Vector2(30, 20), new Vector2(560, 130), () => PlayStage(stage), 58);
            UIAnim.Pulse((RectTransform)play.transform, 3f, 0.03f);
            UIKit.Button(card, "ÉTAPES", UIKit.Dark, new Vector2(1f, 0f), new Vector2(-30, 20), new Vector2(340, 130), () => ShowPage(Page.Stages), 46);

            // Ranked modes: Classique, Défi du jour, Défi de la semaine, Menaces (Scripts/Modes/UI/MenuScreen.Ranked.cs)
            y = BuildRankedSection(content, y);

            var w = StartingWeapon();
            if (w)
            {
                MetaUI.Text(content, $"Arme de départ (Classique, Menaces) : <color=#{MetaUI.GoldHex}>{w.displayName}</color>  <size=26>(Codex)</size>", 30, UIKit.Muted,
                    TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y), new Vector2(1000, 46));
                y += 62f;
            }

            // Camp shortcut: harvest from the home screen.
            var camp = MetaUI.Card(content, "Camp", UIKit.Top, new Vector2(0, -y), new Vector2(980, 170), UIKit.Card);
            y += 190f;
            MetaUI.Text(camp, "LE CAMP", 36, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(36, -18), new Vector2(600, 46), FontStyle.Bold);
            var stock = MetaUI.Text(camp, "", 30, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(36, -70), new Vector2(620, 90));
            Button harvest = null;
            harvest = UIKit.Button(camp, "RÉCOLTER", MetaUI.Green, UIKit.Right, new Vector2(-24, 0), new Vector2(290, 124),
                () => HarvestFrom((RectTransform)harvest.transform), 44);
            liveUpdate += () =>
            {
                if (!stock) return;
                stock.text = $"Réserve : <color=#{MetaUI.GoldHex}>{Camp.StoredGold} or</color>" +
                             $"\n<size=26>{Camp.GoldPerHour:0} or / h, même jeu fermé  ·  {Camp.Survivors} survivants</size>";
                harvest.interactable = Camp.CanHarvest;
            };
            MetaUI.SetHeight(content, y + 10f);
        }

        void SaveName()
        {
            if (nameField) PlayerProfile.Name = nameField.text;
        }

        void PlayStage(int stage)
        {
            if (!Campaign.IsUnlocked(stage)) return;
            SaveName();
            Campaign.Configure(stage);
            MetaSave.Save();
            StartConfiguredRun(); // reloads first if threats from the last ranked run are still in the scene
        }

        static Weapon StartingWeapon()
        {
            var um = UpgradeManager.Instance;
            if (!um) return null;
            foreach (var w in um.Weapons)
                if (w.Id == Progression.StartingWeaponId) return w;
            return um.startingWeapon;
        }

        /// <summary>Collects the camp with a shower of coins flying to the top bar.</summary>
        void HarvestFrom(RectTransform source)
        {
            Vector3 from = source ? source.position : root.position;
            if (!Camp.Harvest(out int gold)) return;

            MetaFX.Burst(fxLayer, from, goldChip.position, UIKit.Gold, Mathf.Clamp(4 + gold / 25, 5, 16));
            MetaFX.FloatText(fxLayer, from, $"+{gold} or", UIKit.Gold, 60);
            MetaFX.Punch(goldChip, 0.55f);
            liveUpdate?.Invoke();
        }

        // ---------- Campaign stage select ----------

        void BuildStages(bool scrollToNext)
        {
            Header("CAMPAGNE", $"{Campaign.TotalStars}/{Campaign.MaxStars} étoiles  ·  {Campaign.ClearedCount}/{Campaign.Count} étapes");
            BackButton(Page.Home);

            const float viewHeight = 1420f;
            var content = MetaUI.Scroll(page, new Vector2(0, -80), new Vector2(1000, viewHeight), out pageScroll);
            float y = 10f, targetY = 0f;
            int chapter = -1, next = Campaign.NextStage;
            for (int i = 0; i < Campaign.Count; i++)
            {
                var s = Campaign.Stages[i];
                if (s.chapter != chapter)
                {
                    chapter = s.chapter;
                    if (i > 0) y += 20f;
                    var h = SectionTitle(content, Campaign.ChapterTitle(chapter), y);
                    h.color = Campaign.ChapterColors[chapter];
                    y += 76f;
                }
                if (i == next) targetY = y;
                var row = StageRow(content, i, y);
                UIAnim.SlideIn(row, new Vector2(0, -50), Mathf.Min(i, 6) * 0.03f, 0.3f, 0.92f);
                y += 212f;
            }
            MetaUI.SetHeight(content, y + 20f);
            if (scrollToNext)
                content.anchoredPosition = new Vector2(0f, Mathf.Clamp(targetY - 200f, 0f, Mathf.Max(0f, y + 20f - viewHeight)));
        }

        RectTransform StageRow(RectTransform content, int i, float y)
        {
            var s = Campaign.Stages[i];
            bool unlocked = Campaign.IsUnlocked(i), cleared = Campaign.IsCleared(i);
            var row = MetaUI.Card(content, "Stage " + Campaign.Code(i), UIKit.Top, new Vector2(0, -y), new Vector2(960, 196), unlocked ? UIKit.Card : MetaUI.Locked);

            Color c = Campaign.ChapterColors[s.chapter];
            var code = MetaUI.Card(row, "Code", UIKit.Left, new Vector2(18, 0), new Vector2(150, 150), unlocked ? new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f) : UIKit.Dark);
            MetaUI.Text(code, Campaign.Code(i), 54, unlocked ? Color.white : UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, Vector2.zero, new Vector2(150, 150), FontStyle.Bold);

            MetaUI.Text(row, s.name, 40, unlocked ? Color.white : UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(190, -16), new Vector2(560, 54), FontStyle.Bold);
            MetaUI.Text(row, $"{UIKit.FormatTime(Mathf.RoundToInt(s.duration))}  ·  Ennemis x{s.health:0.0#}  ·  Dégâts x{s.damage:0.0#}", 27, UIKit.Muted,
                TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(190, -72), new Vector2(560, 40));
            string reward = cleared ? $"<color=#{MetaUI.GreenHex}>Terminée</color>  ·  rejouable pour les étoiles" : "1er passage : " + Campaign.RewardText(s);
            MetaUI.Text(row, reward, 26, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(190, -116), new Vector2(560, 70));

            MetaUI.Stars(row, UIKit.TopRight, new Vector2(-22, -18), 46, Campaign.StarMask(i));
            if (unlocked)
            {
                var b = UIKit.Button(row, "JOUER", cleared ? UIKit.Dark : UIKit.Blood, new Vector2(1f, 0f), new Vector2(-20, 18), new Vector2(190, 104), () => PlayStage(i), 40);
                if (!cleared) UIAnim.Pulse((RectTransform)b.transform, 3f, 0.03f);
            }
            else
            {
                MetaUI.Text(row, "Verrouillée", 30, UIKit.Muted, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-24, 26), new Vector2(220, 50));
            }
            return row;
        }

        // ---------- Popup layer (shared by the tab pages) ----------

        void ClosePopup()
        {
            if (!popupLayer) return;
            foreach (Transform child in popupLayer) Destroy(child.gameObject);
            popupLayer.gameObject.SetActive(false);
        }
    }
}
