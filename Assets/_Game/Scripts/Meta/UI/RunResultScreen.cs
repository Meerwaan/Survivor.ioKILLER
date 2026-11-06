using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// End-of-run meta feedback, on its own canvas above the HUD (GameHUD's game-over panel stays underneath).
    /// Campaign: full victory/defeat screen with stars, rewards and next-stage button.
    /// Endless: a small rewards card at the top of the game-over screen.
    /// </summary>
    public class RunResultScreen : MonoBehaviour
    {
        public static RunResultScreen Instance { get; private set; }

        RectTransform canvas, content;

        void Awake()
        {
            Instance = this;
            canvas = UIKit.Canvas("Run Result Canvas", transform, 30);
            content = UIKit.Rect("Content", canvas);
            UIKit.Stretch(content);
            canvas.gameObject.SetActive(false);
        }

        void Start()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (GameManager.Instance) GameManager.Instance.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }

        void OnStateChanged(GameState state)
        {
            if (state != GameState.GameOver) Hide();
        }

        public void Hide() => canvas.gameObject.SetActive(false);

        /// <summary>Campaign: the stage screen. Ranked modes: the score screen (r may be null if the rewards failed).</summary>
        public void Show(RunReport r)
        {
            foreach (Transform child in content) Destroy(child.gameObject);
            canvas.gameObject.SetActive(true);
            if (r != null && r.campaign) BuildCampaign(r);
            else BuildEndless(r);
        }

        // ---------- Campaign ----------

        void BuildCampaign(RunReport r)
        {
            var panel = UIKit.Overlay("Result", content, 1f);
            var gm = GameManager.Instance;
            int stage = Mathf.Clamp(r.stage, 0, Campaign.Count - 1);

            var title = MetaUI.Text(panel, r.won ? "VICTOIRE !" : "ÉCHEC", 130, r.won ? UIKit.Gold : UIKit.Blood,
                TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 760), new Vector2(1060, 170), FontStyle.Bold);
            UIAnim.SlideIn(title.rectTransform, new Vector2(0, 120), 0f, 0.5f, 2f);

            MetaUI.Text(panel, Campaign.Title(stage), 46, Color.white, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 640), new Vector2(1000, 70), FontStyle.Bold);
            MetaUI.Text(panel, Campaign.ChapterTitle(Campaign.Stages[stage].chapter), 34, UIKit.Muted, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 585), new Vector2(1000, 50));

            // Big stars: what this run achieved; the new ones pop in.
            int saved = Campaign.StarMask(stage);
            for (int i = 0; i < 3; i++)
            {
                bool now = (r.starMask & (1 << i)) != 0;
                bool before = (saved & (1 << i)) != 0;
                var star = MetaUI.StarIcon(panel, UIKit.Center, new Vector2((i - 1) * 190, 445), 170, now);
                if (!now && before) star.GetComponent<Image>().color = new Color(1f, 0.85f, 0.3f, 0.35f);
                if (now) UIAnim.SlideIn(star, new Vector2(0, 80), 0.3f + i * 0.2f, 0.4f, 2.2f);
            }

            // Star conditions
            for (int i = 0; i < 3; i++)
            {
                bool now = (r.starMask & (1 << i)) != 0;
                float y = 290 - i * 64;
                MetaUI.StarIcon(panel, UIKit.Center, new Vector2(-420, y), 48, now || (saved & (1 << i)) != 0);
                string text = Campaign.StarText(stage, i);
                if ((r.newStars & (1 << i)) != 0) text += $"  <color=#{MetaUI.GreenHex}>NOUVEAU</color>";
                MetaUI.Text(panel, text, 34, now ? Color.white : UIKit.Muted, TextAnchor.MiddleLeft,
                    UIKit.Center, new Vector2(70, y), new Vector2(880, 60));
            }

            // Run summary
            string summary = $"Temps {UIKit.FormatTime(Mathf.FloorToInt(gm.RunTime))}   ·   Tués {gm.Kills}   ·   Boss {gm.BossKills}   ·   Niveau {(PlayerXP.Instance ? PlayerXP.Instance.Level : 1)}";
            MetaUI.Text(panel, summary, 32, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 75), new Vector2(1040, 50));

            // Rewards
            var rewards = MetaUI.Card(panel, "Rewards", UIKit.Center, new Vector2(0, -175), new Vector2(980, 400), UIKit.Card);
            MetaUI.Text(rewards, r.firstClear ? "RÉCOMPENSES  ·  1ER PASSAGE !" : "RÉCOMPENSES", 38, UIKit.Gold, TextAnchor.UpperCenter,
                UIKit.Top, new Vector2(0, -18), new Vector2(940, 50), FontStyle.Bold);
            var lines = MetaUI.Text(rewards, RewardLines(r), 36, Color.white, TextAnchor.UpperCenter,
                UIKit.Top, new Vector2(0, -80), new Vector2(940, 310));
            lines.lineSpacing = 1.05f;
            AdOffers.DoubleGoldButton(rewards, UIKit.Bottom, new Vector2(0, 22), r, new Vector2(520, 110)); // "x2 or (pub)", Monetization/AdOffers.cs
            UIAnim.SlideIn(rewards, new Vector2(0, -60), 0.5f, 0.35f, 0.9f);

            // Buttons
            bool hasNext = r.won && stage + 1 < Campaign.Count && Campaign.IsUnlocked(stage + 1);
            if (hasNext)
            {
                var next = UIKit.Button(panel, $"SUIVANT : {Campaign.Code(stage + 1)}", UIKit.Blood, UIKit.Center, new Vector2(0, -490), new Vector2(700, 160), () =>
                {
                    Campaign.Configure(stage + 1);
                    GameManager.Instance.Replay();
                }, 58);
                UIAnim.Pulse((RectTransform)next.transform, 3f, 0.03f);
                UIKit.Button(panel, "REJOUER", UIKit.Dark, UIKit.Center, new Vector2(-180, -670), new Vector2(340, 130), Replay(stage), 46);
                UIKit.Button(panel, "MENU", UIKit.Dark, UIKit.Center, new Vector2(180, -670), new Vector2(340, 130), Menu, 46);
            }
            else
            {
                var again = UIKit.Button(panel, r.won ? "REJOUER" : "RÉESSAYER", UIKit.Blood, UIKit.Center, new Vector2(0, -490), new Vector2(700, 160), Replay(stage), 58);
                if (!r.won) UIAnim.Pulse((RectTransform)again.transform, 3f, 0.03f);
                UIKit.Button(panel, "MENU", UIKit.Dark, UIKit.Center, new Vector2(0, -670), new Vector2(700, 130), Menu, 48);
            }

            if (r.claimable > 0)
                MetaUI.Text(panel, $"{r.claimable} récompense{(r.claimable > 1 ? "s" : "")} à récupérer dans Missions", 32, UIKit.Gold,
                    TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -800), new Vector2(1000, 50));
        }

        static UnityEngine.Events.UnityAction Replay(int stage) => () =>
        {
            Campaign.Configure(stage);
            GameManager.Instance.Replay();
        };

        static void Menu() => GameManager.Instance.BackToMenu();

        static string RewardLines(RunReport r)
        {
            var sb = new StringBuilder();
            int gold = r.runGold + r.bonusGold;
            sb.Append($"<color=#{MetaUI.GoldHex}>+{gold} or</color>");
            if (r.bonusGold > 0) sb.Append($" <size=28>(dont {r.bonusGold} bonus)</size>");
            sb.Append('\n');
            int stars = Campaign.CountStars(r.newStars);
            if (stars > 0) sb.Append($"<size=30>{stars} nouvelle{(stars > 1 ? "s" : "")} étoile{(stars > 1 ? "s" : "")}</size>\n");
            if (r.missionsDone.Count == 1) sb.Append($"<size=30>Mission accomplie : {r.missionsDone[0]}</size>\n");
            else if (r.missionsDone.Count > 1) sb.Append($"<size=30>{r.missionsDone.Count} missions accomplies !</size>\n");
            return sb.ToString().TrimEnd('\n');
        }

        // ---------- Ranked modes (Classique, Défi du jour, Défi de la semaine, Menaces) ----------

        static readonly Color PracticeColor = new(1f, 0.55f, 0.45f);

        /// <summary>
        /// Full score screen over GameHUD's game-over panel: mode, base score x multiplier, final score, record, rank on
        /// the mode's board, medal + next tier, run gold / missions, then REJOUER (same mode) / CLASSEMENT / MENU.
        /// </summary>
        void BuildEndless(RunReport r)
        {
            var gm = GameManager.Instance;
            var res = gm ? gm.LastResult : null;
            if (!gm || res == null) return;

            var panel = UIKit.Overlay("Result", content, 0.97f);

            var title = MetaUI.Text(panel, gm.Abandoned ? "ABANDON" : "TU ES MORT", 120, UIKit.Blood, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 800), new Vector2(1060, 160), FontStyle.Bold);
            UIAnim.SlideIn(title.rectTransform, new Vector2(0, 120), 0f, 0.5f, 2f);

            // Mode
            MetaUI.Text(panel, res.label, 44, res.submitted ? Color.white : PracticeColor, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 690), new Vector2(1040, 60), FontStyle.Bold);
            if (!res.submitted)
                MetaUI.Text(panel, res.mode == GameMode.Daily ? "Entraînement : score non classé (1 essai classé par jour)" : "Score non classé",
                    30, PracticeColor, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 648), new Vector2(1040, 40));

            // Run stats, then the "beat yourself" line: danger tier vs best, longest run, last scores on this board.
            int level = PlayerXP.Instance ? PlayerXP.Instance.Level : 1;
            MetaUI.Text(panel, $"Temps {UIKit.FormatTime(Mathf.FloorToInt(gm.RunTime))}   ·   Tués {gm.Kills}   ·   Niveau {level}   ·   Boss {gm.BossKills}\n" + ProgressLine(res),
                30, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 584), new Vector2(1040, 76));

            // Score: base x multiplier = final
            if (res.mult > 1.001f)
                MetaUI.Text(panel, $"Score {Medals.FormatScore(res.baseScore)}   <color=#{MetaUI.GoldHex}>{Threats.FormatMult(res.mult)}</color>", 40, Color.white,
                    TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 515), new Vector2(1040, 56));
            var final = MetaUI.Text(panel, Medals.FormatScore(res.finalScore), 124, UIKit.Gold, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 410), new Vector2(1040, 150), FontStyle.Bold);
            UIAnim.SlideIn(final.rectTransform, new Vector2(0, -40), 0.2f, 0.4f, 1.3f);

            string record = res.newBest
                ? res.previousBest > 0 ? $"NOUVEAU RECORD !  +{Medals.FormatScore(res.finalScore - res.previousBest)}" : "NOUVEAU RECORD !"
                : res.previousBest > 0 ? $"Record : {Medals.FormatScore(res.previousBest)}  <size=30>·  il t'a manqué {Medals.FormatScore(res.previousBest - res.finalScore)}</size>" : "";
            var recordText = MetaUI.Text(panel, record, res.newBest ? 50 : 36, res.newBest ? UIKit.Gold : UIKit.Muted, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 298), new Vector2(1040, 60), FontStyle.Bold);
            if (res.newBest) UIAnim.Pulse(recordText.rectTransform, 2.5f, 0.05f);

            var rankText = MetaUI.Text(panel, RankLine(res), 40, UIKit.Gold, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 243), new Vector2(1040, 56), FontStyle.Bold);
            res.RankChanged = () =>
            {
                if (rankText) rankText.text = RankLine(res);
            };

            BuildMedalCard(panel, res);

            // Run gold and missions (gold is banked by GameManager.EndRun).
            var rewards = MetaUI.Card(panel, "Rewards", UIKit.Center, new Vector2(0, -175), new Vector2(980, 150), UIKit.Card);
            MetaUI.Text(rewards, $"<color=#{MetaUI.GoldHex}>+{gm.GoldEarned} or</color>   <size=30>(total {Progression.Gold})</size>", 44, Color.white,
                TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -16), new Vector2(940, 60), FontStyle.Bold);
            MetaUI.Text(rewards, MissionLine(r), 30, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -84), new Vector2(940, 50));
            UIAnim.SlideIn(rewards, new Vector2(0, -60), 0.5f, 0.35f, 0.9f);
            AdOffers.DoubleGoldButton(panel, UIKit.Center, new Vector2(0, -320), r, new Vector2(520, 96)); // "x2 or (pub)", Monetization/AdOffers.cs

            // Buttons: replay the SAME mode (the daily turns into practice once its ranked attempt is used).
            bool practiceNext = res.mode == GameMode.Daily && GameModes.DailyRankedUsed;
            var again = UIKit.Button(panel, practiceNext ? "S'ENTRAÎNER" : "REJOUER", UIKit.Blood, UIKit.Center, new Vector2(0, -490), new Vector2(700, 150),
                () => GameManager.Instance.Replay(), 58);
            UIAnim.Pulse((RectTransform)again.transform, 3f, 0.03f);
            var mode = res.mode;
            UIKit.Button(panel, "CLASSEMENT", UIKit.Dark, UIKit.Center, new Vector2(-180, -660), new Vector2(340, 120), () =>
            {
                MenuScreen.RequestBoard(mode);
                GameManager.Instance.BackToMenu();
            }, 42);
            UIKit.Button(panel, "MENU", UIKit.Dark, UIKit.Center, new Vector2(180, -660), new Vector2(340, 120), Menu, 46);

            // The build of the run (weapons, evolutions, bonuses): what made this score.
            if (UpgradeManager.Instance)
            {
                var um = UpgradeManager.Instance;
                string stuff = $"TON STUFF {Mathf.Min(um.BuildLevels, um.BuildCap)}/{um.BuildCap}";
                if (gm.LevelBursts > 0) stuff += $"   ·   {gm.LevelBursts} prime{(gm.LevelBursts > 1 ? "s" : "")} de niveau (+{Medals.FormatScore(gm.LevelBurstScore)})";
                MetaUI.Text(panel, stuff, 28, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -752), new Vector2(1040, 40), FontStyle.Bold);
                var build = UIKit.Rect("Build", panel);
                UIKit.Place(build, UIKit.Center, new Vector2(0, -815), new Vector2(1000, 90));
                GameHUD.FillBuildRow(build);
                string absent = um.AbsentNames();
                if (absent.Length > 0)
                    MetaUI.Text(panel, $"Armes absentes : {absent}", 24, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -878), new Vector2(1040, 34));
            }
        }

        static string RankLine(RankedResult res)
        {
            if (!res.submitted) return "";
            if (res.rank == 0) return "Classement…";
            return res.rank > 0 ? $"#{res.rank} au classement" : "Hors du top 50";
        }

        static string MissionLine(RunReport r)
        {
            if (r == null) return "Le camp et les missions t'attendent au menu";
            var line = new StringBuilder();
            if (r.missionsDone.Count == 1) line.Append($"Mission accomplie : {r.missionsDone[0]}");
            else if (r.missionsDone.Count > 1) line.Append($"Missions accomplies : {r.missionsDone.Count}");
            if (r.claimable > 0)
            {
                if (line.Length > 0) line.Append("   ·   ");
                line.Append($"<color=#{MetaUI.GoldHex}>{r.claimable} à récupérer</color>");
            }
            if (line.Length == 0) line.Append("Le camp et les missions t'attendent au menu");
            return line.ToString();
        }

        /// <summary>"Danger 5 (record !)  ·  Plus longue : 16:56  ·  Dernières : 45 k · 61 k · 217 k".</summary>
        static string ProgressLine(RankedResult res)
        {
            string hex = ColorUtility.ToHtmlStringRGB(RunDirector.DangerColor(res.danger));
            var line = new StringBuilder($"<color=#{hex}>Danger {res.danger}</color>");
            if (res.submitted && res.danger > res.previousBestDanger && res.previousBestDanger > 0)
                line.Append($" <color=#{MetaUI.GreenHex}>(record !)</color>");
            else if (res.previousBestDanger > 0) line.Append($" (record {res.previousBestDanger})");
            if (res.submitted && res.seconds > res.previousBestSeconds && res.previousBestSeconds > 0)
                line.Append($"   ·   <color=#{MetaUI.GreenHex}>Partie la plus longue !</color>");
            else if (res.previousBestSeconds > 0)
                line.Append($"   ·   Plus longue : {UIKit.FormatTime(Mathf.FloorToInt(res.previousBestSeconds))}");
            // The last scores are on the mode's rules page (menu): two lines max here.
            return line.ToString();
        }

        /// <summary>Medal of this score, the next tier and a progress bar toward it.</summary>
        static void BuildMedalCard(RectTransform panel, RankedResult res)
        {
            var card = MetaUI.Card(panel, "Medal", UIKit.Center, new Vector2(0, 60), new Vector2(980, 230), UIKit.Card);
            var icon = Medals.Icon(card, UIKit.Left, new Vector2(30, 0), 170, res.medal);
            UIAnim.SlideIn(icon, new Vector2(-60, 0), 0.35f, 0.4f, 1.4f);

            string head = res.medal == Medal.None ? "Pas encore de médaille" : $"Médaille : {Medals.Name(res.medal)}";
            if (!res.submitted && res.medal != Medal.None) head += "  <size=30>(non classée)</size>";
            MetaUI.Text(card, head, 48, Medals.ColorOf(res.medal), TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(230, -22), new Vector2(720, 62), FontStyle.Bold);

            string sub = res.NewMedal ? $"<color=#{MetaUI.GreenHex}>NOUVEAU PALIER !</color>" : $"Meilleure : {Medals.Name(Medals.For(res.boardId, res.Best))}";
            MetaUI.Text(card, sub, 32, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(230, -88), new Vector2(720, 44));
            MetaUI.Text(card, Medals.NextText(res.boardId, res.finalScore), 32, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft,
                new Vector2(230, -134), new Vector2(720, 44));
            Medals.Next(res.boardId, res.finalScore, out var next, out _);
            MetaUI.Bar(card, UIKit.TopLeft, new Vector2(230, -188), new Vector2(700, 22), Medals.ColorOf(next), Medals.Progress(res.boardId, res.finalScore));
        }
    }
}
