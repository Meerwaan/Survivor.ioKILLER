using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// In-run UI, built from code: XP bar, timer, kills/score/gold, combo counter, multi-kill callouts,
    /// damage flash, pause button, health bar, joystick, boss bar, banners,
    /// plus the level-up (animated cards, reroll, build), pause and game-over screens.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        Camera cam;
        RectTransform canvasRect, safeRoot, hudRoot;
        Rect appliedSafeArea;

        Image xpFill, hpFill, bossFill, vignette, frenzyVignette, flashOverlay;
        Text levelText, timerText, killText, scoreText, goldText, bannerText, comboText, calloutText, challengeText, frenzyText;
        Text muteLabel;
        Color flashColor;
        float flashAlpha;
        int shownChallengeKills = -1, shownChallengeSeconds = -1, shownFrenzySeconds = -1;
        RectTransform xpBar, hpBar, joyBase, joyKnob, bossBar, bossArrow;
        float bannerUntil, bannerDuration, calloutAge = 99f, hurtFlash, xpPunch, comboPunch;

        GameObject levelUpPanel, pausePanel, gameOverPanel;
        RectTransform cardList, levelUpBuild, pauseBuildRow;
        Text levelUpTitle, pauseBuild, pausePerks, gameOverTitle, gameOverStats, gameOverRank;
        Button rerollButton;
        Text rerollLabel;
        CanvasGroup cardsGroup;
        float cardsReadyAt;

        int shownSeconds = -1, shownKills = -1, shownLevel = -1, shownScore = -1, shownGold = -1, shownCombo = -1, shownBuild = -1;

        static readonly Color EvoCard = new(0.2f, 0.08f, 0.26f, 0.97f);

        void Awake()
        {
            Instance = this;
            Build();
            if (!GetComponent<TutorialHints>()) gameObject.AddComponent<TutorialHints>();
        }

        void Start()
        {
            cam = Camera.main;
            GameManager.Instance.StateChanged += OnStateChanged;
            OnStateChanged(GameManager.Instance.State);
            Combat.MultiKill += ShowCallout;
            if (PlayerHealth.Instance) PlayerHealth.Instance.Hurt += OnHurt;
        }

        void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged -= OnStateChanged;
            Combat.MultiKill -= ShowCallout;
            if (PlayerHealth.Instance) PlayerHealth.Instance.Hurt -= OnHurt;
        }

        void OnStateChanged(GameState state)
        {
            bool inRun = state is GameState.Playing or GameState.Paused or GameState.LevelUp;
            hudRoot.gameObject.SetActive(inRun);
            hpBar.gameObject.SetActive(inRun);
            vignette.gameObject.SetActive(inRun);
            if (state != GameState.Playing) joyBase.gameObject.SetActive(false);
            if (!inRun) bossArrow.gameObject.SetActive(false);
            if (!inRun) bannerText.gameObject.SetActive(false);
            if (!inRun) calloutText.gameObject.SetActive(false);
            frenzyVignette.gameObject.SetActive(inRun);
            if (!inRun)
            {
                flashAlpha = 0f;
                flashOverlay.gameObject.SetActive(false);
            }

            levelUpPanel.SetActive(state == GameState.LevelUp);
            pausePanel.SetActive(state == GameState.Paused);
            gameOverPanel.SetActive(state == GameState.GameOver);

            if (state == GameState.Paused)
            {
                pauseBuild.text = DescribeBuild();
                // Set bonuses first (they come from the stuff), then relics and pacts.
                string sets = Synergies.DescribeActive();
                string perks = RunModifiers.Instance ? RunModifiers.Instance.Describe() : "";
                pausePerks.text = sets.Length > 0 ? sets + "\n" + perks : perks;
                FillBuildRow(pauseBuildRow);
                RefreshMuteLabel();
            }

            // Campaign stage: its name as the opening banner.
            if (state == GameState.Playing && !introShown)
            {
                introShown = true;
                if (RunConfig.IsCampaign && !string.IsNullOrEmpty(RunConfig.Label))
                    ShowBanner($"{RunConfig.Label}\n<size=50>Survis {UIKit.FormatTime(Mathf.RoundToInt(RunConfig.Duration))}</size>", Color.white, 2.5f);
            }
        }

        bool introShown;

        // ---------- Run goals (ranked modes): personal best + next medal, announced when crossed ----------

        Text goalText;
        bool goalsReady, recordDone;
        int goalBest;
        Medal goalMedal;
        readonly Queue<(string text, Color color)> goalAnnounces = new();
        float goalAnnounceAt;

        void InitGoals()
        {
            goalsReady = true;
            string board = RunConfig.BoardId;
            bool ranked = RunConfig.Ranked && !string.IsNullOrEmpty(board);
            goalBest = ranked ? GameManager.BestScoreFor(board) : 0;
            // No "record" fanfare on the very first run of a board (every score would be one), nor in practice runs.
            recordDone = !ranked || !RunConfig.SubmitScore || goalBest <= 0;
            goalMedal = Medal.None;
            goalText.gameObject.SetActive(ranked);
        }

        void UpdateGoals(int score)
        {
            if (!goalsReady) InitGoals();
            if (!goalText.gameObject.activeSelf) return;
            string board = RunConfig.BoardId;

            var medal = Medals.For(board, score);
            if (medal > goalMedal)
            {
                if (score > 0) goalAnnounces.Enqueue(($"MÉDAILLE {Medals.Name(medal).ToUpperInvariant()} !", Medals.ColorOf(medal)));
                goalMedal = medal;
            }
            if (!recordDone && score > goalBest)
            {
                recordDone = true;
                goalAnnounces.Enqueue(("NOUVEAU RECORD !", UIKit.Gold));
            }

            // Next goal: the record first (if close), else the next medal.
            string line;
            if (!recordDone) line = $"Record : {Medals.FormatScore(goalBest)}";
            else if (Medals.Next(board, score, out var next, out int at)) line = $"<color=#{ColorUtility.ToHtmlStringRGB(Medals.ColorOf(next))}>{Medals.Name(next)}</color> à {at}";
            else line = $"<color=#{ColorUtility.ToHtmlStringRGB(Medals.ColorOf(Medal.Legende))}>Légende !</color>";
            if (goalMedal != Medal.None) line = $"<color=#{ColorUtility.ToHtmlStringRGB(Medals.ColorOf(goalMedal))}>● {Medals.Name(goalMedal)}</color>   " + line;
            goalText.text = line + PaceLine(board, score);
        }

        /// <summary>
        /// Live comparison with the personal-best run at the same moment (a "ghost"): "+3 200 vs record" in green,
        /// "-1 100 vs record" in red; once past the record run's length, how far from its final score.
        /// </summary>
        static string PaceLine(string board, int score)
        {
            var prog = RunProgress.Find(board);
            if (prog == null || prog.pace.Count == 0) return "";
            int rec = RunProgress.RecordAt(board, GameManager.Instance.RunTime);
            int d = rec >= 0 ? score - rec : score - prog.paceFinal;
            string amount = Medals.FormatScore(Mathf.Abs(d));
            if (rec < 0)
                return d >= 0 ? $"\n<color=#F4C84A>RECORD BATTU  +{amount}</color>" : $"\n<color=#FF9A70>Encore {amount} pour le record</color>";
            return d >= 0 ? $"\n<color=#8CFF7A>+{amount} vs record</color>" : $"\n<color=#FF7070>-{amount} vs record</color>";
        }

        /// <summary>One goal banner at a time, and never on top of an event banner.</summary>
        void UpdateGoalAnnounces()
        {
            if (goalAnnounces.Count == 0 || Time.unscaledTime < goalAnnounceAt) return;
            if (GameManager.Instance.State != GameState.Playing || bannerText.gameObject.activeSelf) return;
            var (text, color) = goalAnnounces.Dequeue();
            ShowBanner($"<size=90>{text}</size>", color, 1.6f);
            ScreenFlash(color, 0.25f);
            SFX.Play(SfxId.Success);
            goalAnnounceAt = Time.unscaledTime + 2.2f;
        }

        // ---------- Public API ----------

        public void ShowLevelUp(List<UpgradeOption> options)
        {
            levelUpTitle.text = $"NIVEAU {PlayerXP.Instance.Level} !";
            SFX.Play(SfxId.LevelUp);
            UIAnim.SlideIn(levelUpTitle.rectTransform, new Vector2(0, 60), 0f, 0.3f, 1.6f);
            ShowCards(options);
            FillBuildRow(levelUpBuild);
            RefreshStuff();
            levelUpPanel.SetActive(true);
        }

        void ShowCards(List<UpgradeOption> options)
        {
            foreach (Transform child in cardList) Destroy(child.gameObject);
            cardCount = options.Count;
            for (int i = 0; i < options.Count; i++) AddCard(i, options[i]);

            // A short delay so a finger still on the joystick can't pick a card by accident.
            cardsReadyAt = Time.unscaledTime + 0.35f;
            cardsGroup.interactable = false;

            // Full stuff (only evolutions left): rerolling or banishing can't change anything.
            var um = UpgradeManager.Instance;
            int rerolls = um.RerollsLeft;
            rerollButton.gameObject.SetActive(rerolls > 0 && !um.BuildComplete);
            rerollLabel.text = $"RELANCER ({rerolls})";
            SetBanishMode(false);
        }

        /// <summary>Stuff gauge and its title on the level-up screen.</summary>
        void RefreshStuff()
        {
            var um = UpgradeManager.Instance;
            int used = um.BuildLevels, cap = um.BuildCap;
            stuffTitle.text = um.BuildComplete
                ? $"STUFF COMPLET  {Mathf.Min(used, cap)}/{cap}"
                : $"TON STUFF  {used}/{cap}  <color=#{ColorUtility.ToHtmlStringRGB(UIKit.Muted)}><size=26>· {cap - used} niveau{(cap - used > 1 ? "x" : "")} libre{(cap - used > 1 ? "s" : "")}</size></color>";
            FillStuffGauge(stuffGauge);
        }

        /// <summary>
        /// One segment per stuff level (20 by default), colored by the item that uses it (weapons first), empty ones dark.
        /// Shows at a glance how much room is left and where it went.
        /// </summary>
        public static void FillStuffGauge(RectTransform row)
        {
            foreach (Transform child in row) Destroy(child.gameObject);
            var um = UpgradeManager.Instance;
            if (!um) return;
            var colors = new List<Color>();
            foreach (var w in um.Weapons)
                for (int i = 0; i < w.Level; i++) colors.Add(w.Evolved ? UpgradeManager.EvolutionColor : w.color);
            foreach (var p in um.Passives)
                for (int i = 0; i < p.level; i++) colors.Add(p.color);

            int cap = um.BuildCap;
            float width = row.rect.width > 1f ? row.rect.width : 900f, height = row.rect.height > 1f ? row.rect.height : 18f;
            const float gap = 6f;
            float seg = (width - gap * (cap - 1)) / cap;
            for (int i = 0; i < cap; i++)
            {
                var r = UIKit.Rect("Seg", row);
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2(i * (seg + gap), 0f);
                r.sizeDelta = new Vector2(seg, height);
                var img = UIKit.Image(r, i < colors.Count ? colors[i] : new Color(1f, 1f, 1f, 0.12f), UIKit.BarSprite);
                img.raycastTarget = false;
            }
        }

        /// <summary>
        /// Family labels stuck on the top edge of a card ("FEU", "EXPLOSIF"...): what set bonuses the item counts for.
        /// Bonuses without a family say "UTILITAIRE" (they never count for a set).
        /// </summary>
        static void AddTagLabels(RectTransform card, UpgradeOption option)
        {
            if (option.kind == UpgradeKind.Bonus) return;
            var list = new List<(string name, Color color)>();
            foreach (var syn in Synergies.All)
                if ((option.tags & syn.tag) != 0) list.Add((Synergies.TagName(syn.tag).ToUpperInvariant(), syn.color));
            if (list.Count == 0)
            {
                if (option.kind != UpgradeKind.Passive) return;
                list.Add(("UTILITAIRE", new Color(0.62f, 0.64f, 0.68f)));
            }

            float x = -26f;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var (name, color) = list[i];
                var chip = UIKit.Rect("Tag " + name, card);
                chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
                chip.pivot = new Vector2(1f, 0.5f);
                var chipImg = UIKit.Image(chip, color, UIKit.ChipSprite);
                chipImg.type = Image.Type.Sliced;
                chipImg.raycastTarget = false;
                var label = UIKit.Label(chip, name, 24, new Color(0.06f, 0.06f, 0.07f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Stretch(label.rectTransform);
                var shadow = label.GetComponent<Shadow>();
                if (shadow) shadow.enabled = false;
                float w = Mathf.Max(80f, label.preferredWidth + 26f);
                chip.sizeDelta = new Vector2(w, 34f);
                chip.anchoredPosition = new Vector2(x, 0f);
                x -= w + 8f;
            }
        }

        // ---------- Queued banners (wait for a quiet moment instead of replacing an event banner) ----------

        readonly Queue<(string text, Color color, float duration)> bannerQueue = new();
        float nextQueuedBanner;

        /// <summary>Shows this banner as soon as no other banner is up and the run is playing.</summary>
        public void QueueBanner(string text, Color color, float duration) => bannerQueue.Enqueue((text, color, duration));

        void UpdateBannerQueue()
        {
            if (bannerQueue.Count == 0 || Time.unscaledTime < nextQueuedBanner) return;
            if (GameManager.Instance.State != GameState.Playing || bannerText.gameObject.activeSelf) return;
            var (text, color, duration) = bannerQueue.Dequeue();
            ShowBanner(text, color, duration);
            nextQueuedBanner = Time.unscaledTime + duration + 0.6f;
        }

        /// <summary>A level-up once the stuff is full (GameManager.LevelBurst): a callout instead of the card screen.</summary>
        public void ShowLevelBurst(int level, int scoreBonus)
        {
            // Own label under the timer: the multi-kill callouts the burst itself causes would overwrite a callout.
            burstText.text = $"NIVEAU {level}  ·  <color=#FFFFFF>+{scoreBonus} pts</color>";
            burstAge = 0f;
            burstText.gameObject.SetActive(true);
            UIAnim.SlideIn(burstText.rectTransform, new Vector2(0, 30), 0f, 0.25f, 1.5f);
            ScreenFlash(UIKit.Gold, 0.25f);
            xpPunch = 1f;
        }

        Text burstText;
        float burstAge = 99f;
        const float BurstShow = 1.8f;

        void UpdateBurst(float dt)
        {
            if (!burstText.gameObject.activeSelf) return;
            burstAge += dt;
            var c = burstText.color;
            c.a = Mathf.Clamp01((BurstShow - burstAge) / 0.5f);
            burstText.color = c;
            if (burstAge >= BurstShow) burstText.gameObject.SetActive(false);
        }

        void Reroll()
        {
            if (Time.unscaledTime < cardsReadyAt) return;
            if (UpgradeManager.Instance.TryReroll(3, out var options)) ShowCards(options);
        }

        // Banish (BUILDS): tap BANNIR, then a card: its item leaves the pool for the run and only that card is replaced.
        void ToggleBanish()
        {
            if (Time.unscaledTime < cardsReadyAt) return;
            SetBanishMode(!banishMode);
        }

        void SetBanishMode(bool on)
        {
            var um = UpgradeManager.Instance;
            int left = um.BanishesLeft;
            banishMode = on && left > 0 && !um.BuildComplete;
            banishButton.gameObject.SetActive(left > 0 && !um.BuildComplete);
            banishLabel.text = banishMode ? "ANNULER" : $"BANNIR ({left})";
            int room = um.BuildRoom;
            levelUpSub.text = banishMode ? "Touche la carte à bannir pour toute la partie"
                : room <= 0 ? "Stuff complet : l'évolution est gratuite !"
                : room == 1 ? "<color=#F4C84A>Dernier niveau de stuff : choisis bien !</color>"
                : "Choisis une amélioration";
            levelUpSub.color = banishMode ? UIKit.Blood : UIKit.Muted;
        }

        void BanishCard(UpgradeOption option)
        {
            if (!option.CanBanish) return;
            if (!UpgradeManager.Instance.TryBanish(option, out var options)) return;
            SFX.Play(SfxId.Pick);
            ScreenFlash(UIKit.Blood, 0.35f);
            ShowCards(options);
        }

        public void ShowGameOver(bool newBest)
        {
            var gm = GameManager.Instance;
            // The full result screen (RunResultScreen) covers both modes: keep this panel only as a fallback,
            // otherwise its text shows through behind the result screen.
            if ((gm.LastReport != null || gm.LastResult != null) && RunResultScreen.Instance)
            {
                gameOverPanel.SetActive(false);
                SFX.Play(gm.Won ? SfxId.Success : SfxId.GameOver);
                return;
            }
            gameOverStats.text =
                $"Temps   {UIKit.FormatTime(Mathf.FloorToInt(gm.RunTime))}\n" +
                $"Tués   {gm.Kills}\n" +
                $"Souches   {gm.BossKills}\n" +
                $"Meilleur combo   x{Combat.BestCombo}\n" +
                $"Niveau   {PlayerXP.Instance.Level}\n\n" +
                $"<size=80><b>{gm.Score}</b></size>\n" +
                (newBest ? "<color=#F4C84A>NOUVEAU RECORD !</color>" : $"Record   {GameManager.BestScore}") +
                $"\n\n<color=#F4C84A>+{gm.GoldEarned} or</color>   <size=36>(total {Progression.Gold})</size>";
            gameOverRank.text = "";
            gameOverTitle.text = gm.Won ? "VICTOIRE !" : "TU ES MORT";
            gameOverTitle.color = gm.Won ? UIKit.Gold : UIKit.Blood;
            SFX.Play(gm.Won ? SfxId.Success : SfxId.GameOver);
            UIAnim.SlideIn(gameOverTitle.rectTransform, new Vector2(0, 120), 0f, 0.5f, 2f);
            UIAnim.SlideIn(gameOverStats.rectTransform, new Vector2(0, -80), 0.2f, 0.4f, 0.9f);
        }

        public void ShowRank(int rank)
        {
            gameOverRank.text = rank > 0 ? $"#{rank} au Registre" : "";
        }

        /// <summary>Time.time of the last event-sized banner (1.4 s or longer): WorldEvents waits for a quiet moment.</summary>
        public float LastAnnouncementTime { get; private set; } = -99f;

        public void ShowBanner(string text, Color color, float duration = 2f)
        {
            if (duration >= 1.4f) LastAnnouncementTime = Time.time;
            bannerText.text = text;
            bannerText.color = color;
            bannerDuration = duration;
            bannerUntil = Time.time + duration;
            bannerText.gameObject.SetActive(true);
        }

        void ShowCallout(string text, Color color)
        {
            calloutText.text = text;
            calloutText.color = color;
            calloutAge = 0f;
            calloutText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
            calloutText.gameObject.SetActive(true);
            SFX.Play(SfxId.Callout);
        }

        void OnHurt(float amount)
        {
            hurtFlash = Mathf.Min(1f, hurtFlash + 0.35f + amount / 60f);
            SFX.Play(SfxId.Hurt, Mathf.Clamp(0.6f + amount / 40f, 0.6f, 1f));
        }

        /// <summary>Full-screen colored flash that fades out quickly (boss, evolution, frenzy, chests...).</summary>
        public void ScreenFlash(Color color, float strength = 0.6f)
        {
            flashColor = color;
            // Edge-only flash (vignette sprite), capped: the fight in the middle must stay readable.
            flashAlpha = Mathf.Max(flashAlpha, Mathf.Clamp(strength, 0f, 0.65f));
            flashOverlay.gameObject.SetActive(true);
        }

        // ---------- Per-frame updates ----------

        void Update()
        {
            if (Screen.safeArea != appliedSafeArea) ApplySafeArea();

            if (levelUpPanel.activeSelf && !cardsGroup.interactable && Time.unscaledTime >= cardsReadyAt)
                cardsGroup.interactable = true;

            var gm = GameManager.Instance;
            var xp = PlayerXP.Instance;
            if (!gm || !xp || !hudRoot.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime;

            xpFill.fillAmount = xp.Current / xp.Required;

            var um = UpgradeManager.Instance;
            int build = um ? um.BuildLevels : 0;
            if (xp.Level != shownLevel || build != shownBuild)
            {
                if (shownLevel > 0 && xp.Level != shownLevel) xpPunch = 1f;
                shownLevel = xp.Level;
                shownBuild = build;
                bool full = um && um.BuildComplete;
                levelText.text = !um ? $"Nv {shownLevel}"
                    : full ? $"Nv {shownLevel}   ·   <color=#F4C84A>STUFF COMPLET</color>"
                    : $"Nv {shownLevel}   ·   Stuff {build}/{um.BuildCap}";
                // Gold XP bar once the stuff is full: from now on every level is a burst.
                // Amber like the éclats the injector burns; gold once the stuff is full.
                xpFill.color = full ? UIKit.Gold : UIKit.Amber;
            }
            xpPunch = Mathf.MoveTowards(xpPunch, 0f, dt * 3f);
            xpBar.localScale = new Vector3(1f, 1f + xpPunch * 0.5f, 1f);

            // Campaign stages count down to the win; endless counts up.
            int seconds = RunConfig.Duration > 0f
                ? Mathf.Max(0, Mathf.CeilToInt(RunConfig.Duration - gm.RunTime))
                : Mathf.FloorToInt(gm.RunTime);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timerText.text = UIKit.FormatTime(seconds);
            }

            if (gm.Kills != shownKills)
            {
                shownKills = gm.Kills;
                killText.text = $"{shownKills} tués";
            }

            // Ranked modes show the final score (threat multiplier included), like the result screen.
            int score = RunConfig.ScoreMult != 1f ? Mathf.RoundToInt(gm.Score * RunConfig.ScoreMult) : gm.Score;
            if (score != shownScore)
            {
                shownScore = score;
                scoreText.text = shownScore.ToString();
                UpdateGoals(score);
            }

            if (gm.Gold != shownGold)
            {
                shownGold = gm.Gold;
                goldText.text = $"{shownGold} or";
            }

            UpdateGoalAnnounces();
            UpdateBannerQueue();
            UpdateBurst(dt);
            UpdateCombo(dt);
            UpdateCallout(dt);
            UpdateVignette(dt);
            UpdateFlash(dt);
            UpdateFrenzy();
            UpdateChallenge();

            var boss = RunDirector.Instance ? RunDirector.Instance.CurrentBoss : null;
            bool showBoss = boss && boss.gameObject.activeSelf && !boss.IsDead;
            if (bossBar.gameObject.activeSelf != showBoss) bossBar.gameObject.SetActive(showBoss);
            if (showBoss) bossFill.fillAmount = boss.HealthFraction;

            if (bannerText.gameObject.activeSelf)
            {
                float left = bannerUntil - Time.time;
                if (left <= 0f) bannerText.gameObject.SetActive(false);
                else
                {
                    var c = bannerText.color;
                    c.a = Mathf.Clamp01(left / Mathf.Min(0.5f, bannerDuration));
                    bannerText.color = c;
                    float since = bannerDuration - left;
                    float pop = since < 0.2f ? Mathf.Lerp(1.6f, 1f, since / 0.2f) : 1f;
                    bannerText.rectTransform.localScale = Vector3.one * pop;
                }
            }
        }

        void UpdateCombo(float dt)
        {
            int combo = Combat.ComboTimeLeft > 0f ? Combat.Combo : 0;
            if (combo != shownCombo)
            {
                if (combo > shownCombo) comboPunch = 1f;
                shownCombo = combo;
                comboText.text = combo >= 5 ? $"COMBO\n<size=86>x{combo}</size>" : "";
            }
            if (combo < 5) return;

            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 6f);
            float heat = Mathf.Clamp01(combo / 150f);
            Color c = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.1f), heat);
            c.a = Mathf.Clamp01(Combat.ComboTimeLeft / 0.6f);
            comboText.color = c;
            comboText.rectTransform.localScale = Vector3.one * (1f + comboPunch * 0.25f);
        }

        void UpdateCallout(float dt)
        {
            if (!calloutText.gameObject.activeSelf) return;
            calloutAge += dt;
            const float life = 1.1f;
            if (calloutAge >= life)
            {
                calloutText.gameObject.SetActive(false);
                return;
            }
            float pop = calloutAge < 0.12f ? Mathf.Lerp(1.55f, 1f, calloutAge / 0.12f) : 1f + (calloutAge - 0.12f) * 0.1f;
            calloutText.rectTransform.localScale = Vector3.one * pop;
            var c = calloutText.color;
            c.a = Mathf.Clamp01((life - calloutAge) / 0.3f);
            calloutText.color = c;
        }

        void UpdateVignette(float dt)
        {
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt * 2.5f);
            float hp = PlayerHealth.Instance ? PlayerHealth.Instance.Fraction : 1f;
            float danger = hp < 0.3f ? (0.3f - hp) / 0.3f * (0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
            var c = vignette.color;
            c.a = Mathf.Clamp01(Mathf.Max(hurtFlash * 0.8f, danger));
            vignette.color = c;
        }

        void UpdateFlash(float dt)
        {
            if (!flashOverlay.gameObject.activeSelf) return;
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 2.2f);
            var c = flashColor;
            c.a = flashAlpha;
            flashOverlay.color = c;
            if (flashAlpha <= 0f) flashOverlay.gameObject.SetActive(false);
        }

        /// <summary>Burning screen edges and a countdown while the frenzy lasts.</summary>
        void UpdateFrenzy()
        {
            var events = ArenaEvents.Instance;
            bool on = events && events.FrenzyActive;
            var c = ArenaEvents.FrenzyColor;
            c.a = on ? (0.22f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f)) * Mathf.Clamp01(events.FrenzyLeft / 0.5f) : 0f;
            frenzyVignette.color = c;

            if (frenzyText.gameObject.activeSelf != on) frenzyText.gameObject.SetActive(on);
            if (!on) return;
            int seconds = Mathf.CeilToInt(events.FrenzyLeft);
            if (seconds != shownFrenzySeconds)
            {
                shownFrenzySeconds = seconds;
                frenzyText.text = $"FRÉNÉSIE  {seconds}";
            }
            frenzyText.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 12f));
        }

        void UpdateChallenge()
        {
            var events = ArenaEvents.Instance;
            bool on = events && events.ChallengeActive;
            if (challengeText.gameObject.activeSelf != on) challengeText.gameObject.SetActive(on);
            if (!on) return;

            int kills = Mathf.Min(events.ChallengeKills, events.ChallengeTarget);
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, events.ChallengeTimeLeft));
            if (kills != shownChallengeKills || seconds != shownChallengeSeconds)
            {
                shownChallengeKills = kills;
                shownChallengeSeconds = seconds;
                challengeText.text = $"DÉFI  {kills}/{events.ChallengeTarget}   {seconds} s";
            }
            bool hurry = seconds <= 5;
            challengeText.color = hurry && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? new Color(1f, 0.35f, 0.25f) : UIKit.Gold;
        }

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            if (!player || !cam || !hpBar.gameObject.activeSelf) return;

            // Health bar under the player's feet.
            Vector3 screen = cam.WorldToScreenPoint(player.transform.position + new Vector3(0f, 0f, -0.9f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            hpBar.anchoredPosition = local;
            float fraction = PlayerHealth.Instance.Fraction;
            hpFill.fillAmount = fraction;
            hpFill.color = fraction < 0.3f && Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.5f ? new Color(1f, 0.6f, 0.6f) : new Color(0.85f, 0.1f, 0.1f);

            // Floating joystick.
            bool showJoystick = player.JoystickActive;
            if (joyBase.gameObject.activeSelf != showJoystick) joyBase.gameObject.SetActive(showJoystick);
            if (showJoystick)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, player.JoystickOrigin, null, out var origin);
                joyBase.anchoredPosition = origin;
                joyKnob.anchoredPosition = player.JoystickVector * 80f;
            }

            UpdateBossArrow();
        }

        /// <summary>Arrow on the screen edge pointing at the boss while it's off-screen.</summary>
        void UpdateBossArrow()
        {
            var boss = RunDirector.Instance ? RunDirector.Instance.CurrentBoss : null;
            bool alive = boss && boss.gameObject.activeSelf && !boss.IsDead;

            Vector3 screen = alive ? cam.WorldToScreenPoint(boss.transform.position + Vector3.up) : Vector3.zero;
            const float margin = 70f;
            float m = margin * UIKit.PixelsPerUnit;
            bool offScreen = alive && (screen.z < 0f || screen.x < m || screen.x > Screen.width - m || screen.y < m || screen.y > Screen.height - m);

            if (bossArrow.gameObject.activeSelf != offScreen) bossArrow.gameObject.SetActive(offScreen);
            if (!offScreen) return;

            Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = (Vector2)screen - center;
            if (screen.z < 0f) dir = -dir; // behind the camera: flip
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;

            // Push the arrow out to the screen border along that direction.
            float sx = (center.x - m) / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
            float sy = (center.y - m) / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
            Vector2 edge = center + dir * Mathf.Min(sx, sy);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, edge, null, out var local);
            bossArrow.anchoredPosition = local;
            bossArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.1f;
            bossArrow.localScale = Vector3.one * pulse;
        }

        static string DescribeBuild()
        {
            var um = UpgradeManager.Instance;
            var sb = new System.Text.StringBuilder();
            int used = um.BuildLevels, cap = um.BuildCap;
            sb.Append(um.BuildComplete
                ? $"<color=#F4C84A><b>STUFF COMPLET {Mathf.Min(used, cap)}/{cap}</b></color>\n"
                : $"<color=#F4C84A><b>STUFF {used}/{cap}</b></color>  <size=30>({cap - used} libre{(cap - used > 1 ? "s" : "")})</size>\n");
            foreach (var w in um.Weapons)
            {
                if (w.Level == 0) continue;
                sb.Append(w.Evolved ? $"<size=38><color=#D9A6FF>{w.CurrentName}  ÉVO</color></size>\n" : $"<size=38>{w.displayName}  Nv {w.Level}/{w.MaxLevel}</size>\n");
            }
            int shown = 0;
            foreach (var p in um.Passives)
            {
                if (p.level == 0) continue;
                sb.Append($"<size=32>{p.name} {p.level}/{p.maxLevel}</size>");
                sb.Append(++shown % 2 == 0 ? "\n" : "     ");
            }
            if (shown % 2 == 1) sb.Append('\n');
            string absent = um.AbsentNames();
            if (absent.Length > 0) sb.Append($"<size=28><color=#999999>Absentes cette partie : {absent}</color></size>");
            return sb.ToString();
        }

        /// <summary>Row of icons: weapons (squares) then bonuses (circles), with their level.</summary>
        /// <summary>Icons of the current build (weapons, then passives). Also used by the result screen.</summary>
        public static void FillBuildRow(RectTransform row)
        {
            foreach (Transform child in row) Destroy(child.gameObject);
            var um = UpgradeManager.Instance;
            var items = new List<(string name, Color color, bool round, string badge)>();
            foreach (var w in um.Weapons)
                if (w.Level > 0) items.Add((w.CurrentName, w.Evolved ? UpgradeManager.EvolutionColor : w.color, false, w.Evolved ? "ÉVO" : w.Level.ToString()));
            foreach (var p in um.Passives)
                if (p.level > 0) items.Add((p.name, p.color, true, p.level.ToString()));

            const float size = 76f, gap = 10f;
            float width = items.Count * size + (items.Count - 1) * gap;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var icon = UIKit.Icon(row, it.name, it.color, it.round, size, it.badge);
                icon.anchoredPosition = new Vector2(-width / 2f + size / 2f + i * (size + gap), 0f);
            }
        }

        // ---------- Construction ----------

        void Build()
        {
            canvasRect = UIKit.Canvas("HUD Canvas", transform, 10);

            // Damage flash first so it sits behind everything else.
            var vig = UIKit.Rect("Damage Vignette", canvasRect);
            UIKit.Stretch(vig);
            vignette = UIKit.Image(vig, new Color(0.85f, 0f, 0f, 0f), UIKit.Vignette);
            vignette.raycastTarget = false;

            var fv = UIKit.Rect("Frenzy Vignette", canvasRect);
            UIKit.Stretch(fv);
            frenzyVignette = UIKit.Image(fv, new Color(1f, 0.35f, 0.1f, 0f), UIKit.Vignette);
            frenzyVignette.raycastTarget = false;

            var fl = UIKit.Rect("Screen Flash", canvasRect);
            UIKit.Stretch(fl);
            flashOverlay = UIKit.Image(fl, new Color(1f, 1f, 1f, 0f), UIKit.Vignette);
            flashOverlay.raycastTarget = false;
            fl.gameObject.SetActive(false);

            // Widgets that follow world/screen positions sit directly on the canvas.
            hpBar = UIKit.Rect("HP Bar", canvasRect);
            hpBar.sizeDelta = new Vector2(136, 20);
            var hpBg = UIKit.Image(hpBar, new Color(0.02f, 0.02f, 0.03f, 0.8f), UIKit.ChipSprite);
            hpBg.type = Image.Type.Sliced;
            hpBg.pixelsPerUnitMultiplier = 2f;
            hpBg.raycastTarget = false;
            hpFill = UIKit.Fill(hpBar, new Color(0.85f, 0.1f, 0.1f), 3f);

            // Joystick: thin light ring + dark disc, solid knob with a rim.
            joyBase = UIKit.Rect("Joystick", canvasRect);
            joyBase.sizeDelta = new Vector2(260, 260);
            UIKit.Image(joyBase, new Color(1f, 1f, 1f, 0.22f), UIKit.Circle).raycastTarget = false;
            var joyInner = UIKit.Rect("Disc", joyBase);
            UIKit.Stretch(joyInner, 5f);
            UIKit.Image(joyInner, new Color(0.02f, 0.03f, 0.04f, 0.32f), UIKit.Circle).raycastTarget = false;
            joyKnob = UIKit.Rect("Knob", joyBase);
            joyKnob.sizeDelta = new Vector2(116, 116);
            UIKit.Image(joyKnob, new Color(1f, 1f, 1f, 0.35f), UIKit.Circle).raycastTarget = false;
            var knobCore = UIKit.Rect("Core", joyKnob);
            UIKit.Stretch(knobCore, 8f);
            UIKit.Image(knobCore, new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.6f), UIKit.Circle).raycastTarget = false;
            joyBase.gameObject.SetActive(false);

            bossArrow = UIKit.Rect("Boss Arrow", canvasRect);
            bossArrow.sizeDelta = new Vector2(70, 70);
            UIKit.Image(bossArrow, UIKit.Amber, UIKit.Triangle).raycastTarget = false;
            bossArrow.gameObject.SetActive(false);

            // Everything else respects the phone's notch / rounded corners.
            safeRoot = UIKit.Rect("Safe Area", canvasRect);
            ApplySafeArea();

            hudRoot = UIKit.Rect("HUD", safeRoot);
            UIKit.Stretch(hudRoot);
            BuildTopBar();

            bannerText = UIKit.Label(safeRoot, "", 110, UIKit.Blood, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(bannerText.rectTransform, UIKit.Center, new Vector2(0, 380), new Vector2(1000, 160));
            bannerText.gameObject.SetActive(false);

            calloutText = UIKit.Label(safeRoot, "", 104, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(calloutText.rectTransform, UIKit.Center, new Vector2(0, 560), new Vector2(1060, 160));
            calloutText.GetComponent<Shadow>().effectDistance = new Vector2(6, -6);
            calloutText.gameObject.SetActive(false);

            BuildLevelUpPanel();
            BuildPausePanel();
            BuildGameOverPanel();
        }

        void BuildTopBar()
        {
            xpBar = UIKit.Rect("XP Bar", hudRoot);
            xpBar.anchorMin = new Vector2(0, 1);
            xpBar.anchorMax = new Vector2(1, 1);
            xpBar.pivot = new Vector2(0.5f, 1);
            xpBar.anchoredPosition = new Vector2(0, -16);
            xpBar.sizeDelta = new Vector2(-32, 36);
            var xpBg = UIKit.Image(xpBar, new Color(0.02f, 0.025f, 0.03f, 0.75f), UIKit.ChipSprite);
            xpBg.type = Image.Type.Sliced;
            xpBg.raycastTarget = false;
            UIKit.Outline(xpBar, new Color(1f, 1f, 1f, 0.12f), true);
            xpFill = UIKit.Fill(xpBar, UIKit.Amber, 5f);
            xpFill.fillAmount = 0f; // an Image starts full: don't flash a full bar before the first update
            levelText = UIKit.Label(xpBar, "Nv 1", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(levelText.rectTransform);

            UIKit.Button(hudRoot, "II", new Color(0.08f, 0.09f, 0.11f, 0.7f), UIKit.TopLeft, new Vector2(24, -66), new Vector2(110, 110),
                () => GameManager.Instance.Pause(), 52);

            comboText = UIKit.Label(hudRoot, "", 40, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(comboText.rectTransform, UIKit.TopLeft, new Vector2(30, -200), new Vector2(400, 160));
            comboText.lineSpacing = 0.8f;

            timerText = UIKit.Label(hudRoot, "00:00", 64, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Place(timerText.rectTransform, UIKit.Top, new Vector2(0, -64), new Vector2(400, 80));

            killText = UIKit.Label(hudRoot, "0 tués", 38, UIKit.Muted, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Place(killText.rectTransform, UIKit.TopRight, new Vector2(-32, -70), new Vector2(400, 50));

            scoreText = UIKit.Label(hudRoot, "0", 38, Color.white, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Place(scoreText.rectTransform, UIKit.TopRight, new Vector2(-32, -118), new Vector2(400, 50));

            goldText = UIKit.Label(hudRoot, "0 or", 38, UIKit.Gold, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Place(goldText.rectTransform, UIKit.TopRight, new Vector2(-32, -166), new Vector2(400, 50));

            goalText = UIKit.Label(hudRoot, "", 28, UIKit.Muted, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Place(goalText.rectTransform, UIKit.TopRight, new Vector2(-32, -214), new Vector2(460, 80));

            bossBar = UIKit.Rect("Boss Bar", hudRoot);
            UIKit.Place(bossBar, UIKit.Top, new Vector2(0, -170), new Vector2(560, 36));
            var bossBg = UIKit.Image(bossBar, new Color(0.02f, 0.02f, 0.03f, 0.8f), UIKit.ChipSprite);
            bossBg.type = Image.Type.Sliced;
            bossBg.raycastTarget = false;
            UIKit.Outline(bossBar, new Color(UIKit.Amber.r, UIKit.Amber.g, UIKit.Amber.b, 0.6f), true);
            bossFill = UIKit.Fill(bossBar, UIKit.Amber, 5f);
            var bossLabel = UIKit.Label(bossBar, "SOUCHE", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(bossLabel.rectTransform);
            bossBar.gameObject.SetActive(false);

            challengeText = UIKit.Label(hudRoot, "", 44, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(challengeText.rectTransform, UIKit.Top, new Vector2(0, -228), new Vector2(800, 60));
            challengeText.gameObject.SetActive(false);

            burstText = UIKit.Label(hudRoot, "", 44, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            // Below the timer, mode label, boss bar, challenge and frenzy lines; above the event banners.
            UIKit.Place(burstText.rectTransform, UIKit.Top, new Vector2(0, -362), new Vector2(800, 56));
            burstText.gameObject.SetActive(false);

            frenzyText = UIKit.Label(hudRoot, "", 48, ArenaEvents.FrenzyColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(frenzyText.rectTransform, UIKit.Top, new Vector2(0, -290), new Vector2(800, 64));
            frenzyText.gameObject.SetActive(false);
        }

        // Level-up cards (BUILDS): 3 or 4 cards per hand, banish mode.
        Button banishButton;
        Text banishLabel, levelUpSub, stuffTitle;
        RectTransform stuffGauge;
        bool banishMode;
        int cardCount = 3;

        void BuildLevelUpPanel()
        {
            var panel = UIKit.Overlay("Level Up", canvasRect, 0.82f);
            levelUpPanel = panel.gameObject;

            levelUpTitle = UIKit.Label(panel, "NIVEAU SUPÉRIEUR", 84, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(levelUpTitle.rectTransform, UIKit.Center, new Vector2(0, 600), new Vector2(1000, 130));
            levelUpSub = UIKit.Label(panel, "Choisis une amélioration", 38, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Place(levelUpSub.rectTransform, UIKit.Center, new Vector2(0, 515), new Vector2(1000, 60));

            cardList = UIKit.Rect("Cards", panel);
            UIKit.Place(cardList, UIKit.Center, new Vector2(0, 30), new Vector2(940, 860));
            cardsGroup = cardList.gameObject.AddComponent<CanvasGroup>();

            rerollButton = UIKit.Button(panel, "RELANCER", UIKit.Dark, UIKit.Center, new Vector2(-235, -510), new Vector2(450, 110), Reroll, 44);
            rerollLabel = rerollButton.GetComponentInChildren<Text>();
            banishButton = UIKit.Button(panel, "BANNIR", UIKit.Dark, UIKit.Center, new Vector2(235, -510), new Vector2(450, 110), ToggleBanish, 44);
            banishLabel = banishButton.GetComponentInChildren<Text>();

            // Stuff: "TON STUFF 12/20" + one segment per level, then the item icons.
            stuffTitle = UIKit.Label(panel, "TON STUFF", 32, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(stuffTitle.rectTransform, UIKit.Center, new Vector2(0, -608), new Vector2(1000, 46));
            stuffGauge = UIKit.Rect("Stuff Gauge", panel);
            UIKit.Place(stuffGauge, UIKit.Center, new Vector2(0, -648), new Vector2(940, 18));
            levelUpBuild = UIKit.Rect("Build", panel);
            UIKit.Place(levelUpBuild, UIKit.Center, new Vector2(0, -716), new Vector2(1000, 90));

            levelUpPanel.SetActive(false);
        }

        void BuildPausePanel()
        {
            var panel = UIKit.Overlay("Pause", canvasRect, 0.85f);
            pausePanel = panel.gameObject;

            var title = UIKit.Label(panel, "PAUSE", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, UIKit.Center, new Vector2(0, 660), new Vector2(1000, 160));

            pauseBuildRow = UIKit.Rect("Build", panel);
            UIKit.Place(pauseBuildRow, UIKit.Center, new Vector2(0, 520), new Vector2(1000, 90));

            pauseBuild = UIKit.Label(panel, "", 42, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Place(pauseBuild.rectTransform, UIKit.Center, new Vector2(0, 220), new Vector2(980, 460));

            UIKit.Button(panel, "REPRENDRE", UIKit.Blood, UIKit.Center, new Vector2(0, -160), new Vector2(640, 160),
                () => GameManager.Instance.Resume(), 60);
            UIKit.Button(panel, "ABANDONNER", UIKit.Dark, UIKit.Center, new Vector2(0, -360), new Vector2(640, 130),
                () => GameManager.Instance.Abandon(), 48);

            var mute = UIKit.Button(panel, "SON", UIKit.Dark, UIKit.Center, new Vector2(0, -530), new Vector2(640, 110), ToggleSound, 42);
            muteLabel = mute.GetComponentInChildren<Text>();
            RefreshMuteLabel();

            // Relics and pacts of this run (see RunModifiers).
            // Below the SON button (it ends at -585): synergies first, then relics and pacts.
            pausePerks = UIKit.Label(panel, "", 30, Color.white, TextAnchor.UpperCenter);
            UIKit.Place(pausePerks.rectTransform, UIKit.Center, new Vector2(0, -775), new Vector2(1040, 330));
            pausePerks.lineSpacing = 1.05f;

            pausePanel.SetActive(false);
        }

        void BuildGameOverPanel()
        {
            var panel = UIKit.Overlay("Game Over", canvasRect, 0.85f);
            gameOverPanel = panel.gameObject;

            gameOverTitle = UIKit.Label(panel, "TU ES MORT", 120, UIKit.Blood, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(gameOverTitle.rectTransform, UIKit.Center, new Vector2(0, 680), new Vector2(1060, 170));

            gameOverStats = UIKit.Label(panel, "", 48, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place(gameOverStats.rectTransform, UIKit.Center, new Vector2(0, 160), new Vector2(900, 800));

            gameOverRank = UIKit.Label(panel, "", 48, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(gameOverRank.rectTransform, UIKit.Center, new Vector2(0, -320), new Vector2(900, 70));

            UIKit.Button(panel, "REJOUER", UIKit.Blood, UIKit.Center, new Vector2(0, -470), new Vector2(640, 160),
                () => GameManager.Instance.Replay(), 60);
            UIKit.Button(panel, "MENU", UIKit.Dark, UIKit.Center, new Vector2(0, -660), new Vector2(640, 130),
                () => GameManager.Instance.BackToMenu(), 48);

            gameOverPanel.SetActive(false);
        }

        void AddCard(int index, UpgradeOption option)
        {
            bool evo = option.kind == UpgradeKind.Evolution;
            bool isNew = option.kind == UpgradeKind.NewWeapon;

            // A 4-card hand uses shorter cards so it still fits above the buttons.
            bool compact = cardCount > 3;
            var card = UIKit.Rect("Card", cardList);
            UIKit.Place(card, UIKit.Top, new Vector2(0, -index * (compact ? 232 : 290)), new Vector2(920, compact ? 214 : 260));

            var shadowRect = UIKit.Rect("Shadow", card);
            UIKit.Stretch(shadowRect, -16f);
            shadowRect.anchoredPosition = new Vector2(0f, -10f);
            var shadowImg = UIKit.Image(shadowRect, new Color(0f, 0f, 0f, 0.55f), UIKit.GlowSprite);
            shadowImg.type = Image.Type.Sliced;
            shadowImg.raycastTarget = false;

            if (evo)
            {
                // Glowing frame behind the card.
                var glow = UIKit.Rect("Glow", card);
                UIKit.Stretch(glow, -10f);
                UIKit.Panel(glow, option.color, false).raycastTarget = false;
                UIAnim.Pulse(glow, 6f, 0.02f);
            }
            else if (option.rarity != UpgradeRarity.Commun)
            {
                // Rare / epic: thick frame in the rarity color (the level label says how many levels).
                var frame = UIKit.Rect("Rarity", card);
                UIKit.Stretch(frame, option.rarity == UpgradeRarity.Epique ? -9f : -6f);
                UIKit.Panel(frame, option.RarityColor, false).raycastTarget = false;
                if (option.rarity == UpgradeRarity.Epique) UIAnim.Pulse(frame, 5f, 0.015f);
            }
            else if (isNew)
            {
                var frame = UIKit.Rect("Frame", card);
                UIKit.Stretch(frame, -4f);
                UIKit.Panel(frame, new Color(option.color.r, option.color.g, option.color.b, 0.75f), false).raycastTarget = false;
            }

            var bgRect = UIKit.Rect("Background", card);
            UIKit.Stretch(bgRect);
            var bg = UIKit.Panel(bgRect, evo ? EvoCard : UIKit.Card);
            // Color stripe on the left edge: the item's color, so the hand reads at a glance.
            var accent = UIKit.Rect("Accent", bgRect);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.anchoredPosition = new Vector2(5f, 0f);
            accent.sizeDelta = new Vector2(7f, -44f);
            UIKit.Image(accent, option.color).raycastTarget = false;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            card.gameObject.AddComponent<UIPress>();
            button.onClick.AddListener(() =>
            {
                if (Time.unscaledTime < cardsReadyAt) return;
                if (banishMode)
                {
                    BanishCard(option);
                    return;
                }
                if (evo)
                {
                    SFX.Play(SfxId.Evolution);
                    ScreenFlash(UpgradeManager.EvolutionColor, 0.8f);
                    if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.3f);
                }
                else
                {
                    SFX.Play(SfxId.Pick);
                }
                GameManager.Instance.ChooseUpgrade(option);
            });

            bool round = option.kind == UpgradeKind.Passive;
            var icon = UIKit.Icon(card, option.title, option.color, round, 130, "");
            UIKit.Place(icon, UIKit.Left, new Vector2(30, 10), new Vector2(130, 130));

            var title = UIKit.Label(card, option.title, 48, option.color, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(title.rectTransform, UIKit.TopLeft, new Vector2(190, -24), new Vector2(460, 64));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;

            var level = UIKit.Label(card, option.levelLabel, 30, evo ? option.color : isNew ? UIKit.Gold : UIKit.Muted, TextAnchor.UpperRight, FontStyle.Bold);
            UIKit.Place(level.rectTransform, UIKit.TopRight, new Vector2(-28, -32), new Vector2(300, 50));

            var desc = UIKit.Label(card, option.description, compact ? 31 : 35, Color.white, TextAnchor.UpperLeft);
            var dr = desc.rectTransform;
            dr.anchorMin = Vector2.zero;
            dr.anchorMax = Vector2.one;
            dr.offsetMin = new Vector2(190, 58);
            dr.offsetMax = new Vector2(-28, -96);

            if (!string.IsNullOrEmpty(option.hint))
            {
                var hint = UIKit.Label(card, option.hint, 28, UpgradeManager.EvolutionColor, TextAnchor.LowerLeft, FontStyle.Bold);
                var hr = hint.rectTransform;
                hr.anchorMin = new Vector2(0, 0);
                hr.anchorMax = new Vector2(1, 0);
                hr.pivot = new Vector2(0.5f, 0);
                hr.offsetMin = new Vector2(190, 18);
                hr.offsetMax = new Vector2(-28, 58);
            }

            // Part of the active Codex preset: just a reminder under the icon (the draw itself is untouched).
            if (option.inPreset) PresetBadge.Create(card, Vector2.zero, new Vector2(22, 16), 40f);

            AddTagLabels(card, option);

            UIAnim.SlideIn(card, new Vector2(0, -260), 0.06f + index * 0.08f, 0.35f, 0.75f);
            if (evo) UIAnim.Pulse(card, 4f, 0.015f);
        }

        void ToggleSound()
        {
            SFX.Muted = !SFX.Muted;
            RefreshMuteLabel();
        }

        void RefreshMuteLabel()
        {
            if (muteLabel) muteLabel.text = SFX.Muted ? "SON : NON" : "SON : OUI";
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
