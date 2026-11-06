using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Competitive modes in the menu: the "MODES CLASSÉS" tiles of the home page, the rules page of each mode,
    /// the Menaces (NG+) selection page and the leaderboard page with one tab per board.
    /// </summary>
    public partial class MenuScreen
    {
        static readonly Color DailyColor = new(0.3f, 0.7f, 1f);
        static readonly Color MenacesColor = new(0.65f, 0.3f, 1f);

        /// <summary>Mode shown by the rules page.</summary>
        GameMode infoMode = GameMode.Classic;

        static Color ModeColor(GameMode m) => m switch
        {
            GameMode.Daily => DailyColor,
            GameMode.Weekly => GameModes.ElementColor(GameModes.WeeklyElement(GameModes.ThisWeek)),
            GameMode.Menaces => MenacesColor,
            _ => UIKit.Blood,
        };

        // ---------- Play ----------

        void PlayMode(GameMode mode)
        {
            if (mode == GameMode.Menaces && !GameModes.MenacesUnlocked) return;
            SaveName();
            GameModes.Configure(mode);
            StartConfiguredRun();
        }

        /// <summary>
        /// Starts the run RunConfig describes. Some threat hooks are read when the scene loads (RunModifiers.Awake,
        /// RunDirector.Start): if the threats changed since then (e.g. back from a Menaces run), start from a fresh scene.
        /// </summary>
        static void StartConfiguredRun()
        {
            if (RunConfig.ThreatMask != Threats.SceneMask) GameManager.Instance.Replay();
            else GameManager.Instance.StartRun();
        }

        // ---------- Home: ranked tiles ----------

        /// <summary>"MODES CLASSÉS" title + 2x2 tiles + leaderboard button, from <paramref name="y"/> in a top-anchored scroll content. Returns the new y.</summary>
        float BuildRankedSection(RectTransform content, float y)
        {
            MetaUI.Text(content, "MODES CLASSÉS", 46, UIKit.Gold, TextAnchor.LowerLeft, UIKit.Top, new Vector2(0, -y), new Vector2(980, 60), FontStyle.Bold);
            MetaUI.Text(content, "Mêmes règles pour tous", 28, UIKit.Muted, TextAnchor.LowerRight, UIKit.Top, new Vector2(0, -y - 4), new Vector2(980, 50));
            y += 72f;

            const float w = 480f, h = 340f;
            ModeTile(content, GameMode.Classic, new Vector2(-250, -y), new Vector2(w, h));
            ModeTile(content, GameMode.Daily, new Vector2(250, -y), new Vector2(w, h));
            y += h + 20f;
            ModeTile(content, GameMode.Weekly, new Vector2(-250, -y), new Vector2(w, h));
            ModeTile(content, GameMode.Menaces, new Vector2(250, -y), new Vector2(w, h));
            y += h + 20f;

            UIKit.Button(content, "CLASSEMENTS", UIKit.Dark, UIKit.Top, new Vector2(0, -y), new Vector2(980, 110), () =>
            {
                boardMode = GameMode.Classic;
                ShowPage(Page.Board);
            }, 44);
            y += 130f;
            return y;
        }

        void ModeTile(RectTransform content, GameMode mode, Vector2 position, Vector2 size)
        {
            bool locked = mode == GameMode.Menaces && !GameModes.MenacesUnlocked;
            var tile = MetaUI.Card(content, "Mode " + mode, UIKit.Top, position, size, locked ? MetaUI.Locked : UIKit.Card);
            var stripe = MetaUI.Card(tile, "Stripe", UIKit.Top, Vector2.zero, new Vector2(size.x, 8), ModeColor(mode));
            stripe.SetAsFirstSibling();

            string title = mode switch
            {
                GameMode.Daily => "DÉFI DU JOUR",
                GameMode.Weekly => "DÉFI HEBDO",
                GameMode.Menaces => "MENACES  <size=28>NG+</size>",
                _ => "CLASSIQUE",
            };
            MetaUI.Text(tile, title, 40, locked ? UIKit.Muted : UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(24, -22), new Vector2(size.x - 48, 50), FontStyle.Bold);

            var lines = new string[3];
            string board = GameModes.BoardOf(mode);
            switch (mode)
            {
                case GameMode.Classic:
                    lines[0] = "Survie infinie, règles pures";
                    lines[1] = BestLine(board);
                    lines[2] = MedalLine(board);
                    break;
                case GameMode.Daily:
                {
                    int day = GameModes.Today;
                    lines[0] = $"Arme : {GameModes.WeaponName(GameModes.DailyWeapon(day))}";
                    lines[1] = $"{Threats.Count(GameModes.DailyThreats(day))} menaces  ·  {Threats.FormatMult(GameModes.DailyMult(day))}";
                    lines[2] = GameModes.DailyRankedUsed
                        ? $"<color=#FFA070>Essai classé utilisé</color>"
                        : $"<color=#{MetaUI.GreenHex}>Essai classé disponible</color>";
                    break;
                }
                case GameMode.Weekly:
                {
                    int week = GameModes.ThisWeek;
                    var el = GameModes.WeeklyElement(week);
                    lines[0] = $"Élément : <color=#{GameModes.ElementHex(el)}>{Synergies.TagName(el)}</color>  ·  {Threats.FormatMult(GameModes.WeeklyMult(week))}";
                    lines[1] = $"Arme : {GameModes.WeaponName(GameModes.WeeklyWeapon(week))}";
                    lines[2] = BestLine(board);
                    break;
                }
                default:
                    if (locked)
                    {
                        lines[0] = "Verrouillé";
                        lines[1] = "Améliore tout le camp";
                        lines[2] = $"Camp : {Camp.TotalLevels}/{Camp.MaxTotalLevels} niveaux";
                    }
                    else
                    {
                        int mask = GameModes.SelectedThreats;
                        int n = Threats.Count(mask);
                        lines[0] = n == 0 ? "Aucune menace active" : $"{n} menace{(n > 1 ? "s" : "")}  ·  {Threats.FormatMult(Threats.ScoreMult(mask))}";
                        lines[1] = BestLine(board);
                        lines[2] = MedalLine(board);
                    }
                    break;
            }

            for (int i = 0; i < 3; i++)
                MetaUI.Text(tile, lines[i], 28, i == 0 ? Color.white : UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft,
                    new Vector2(24, -76 - i * 38), new Vector2(size.x - 40, 38));
            var timer = MetaUI.Text(tile, "", 26, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(24, -190), new Vector2(size.x - 40, 36));
            if (mode == GameMode.Daily)
                liveUpdate += () => { if (timer) timer.text = $"Nouveau défi dans {GameModes.FormatSpan(GameModes.UntilNextDaily)}"; };
            else if (mode == GameMode.Weekly)
                liveUpdate += () => { if (timer) timer.text = $"Fin dans {GameModes.FormatSpan(GameModes.UntilNextWeek)}"; };

            // Buttons
            if (locked)
            {
                UIKit.Button(tile, "DÉTAILS", UIKit.Dark, UIKit.Bottom, new Vector2(0, 18), new Vector2(size.x - 40, 92), () => ShowPage(Page.Threats), 38);
                return;
            }
            string playLabel = mode == GameMode.Daily && GameModes.DailyRankedUsed ? "S'ENTRAÎNER" : "JOUER";
            var play = UIKit.Button(tile, playLabel, mode == GameMode.Classic ? new Color(0.45f, 0.08f, 0.06f) : UIKit.Blood,
                new Vector2(0f, 0f), new Vector2(20, 18), new Vector2(290, 92), () => PlayMode(mode), playLabel.Length > 6 ? 36 : 44);
            if (mode == GameMode.Daily && !GameModes.DailyRankedUsed) UIAnim.Pulse((RectTransform)play.transform, 3f, 0.03f);
            string second = mode == GameMode.Menaces ? "CHOIX" : "INFOS";
            UIKit.Button(tile, second, UIKit.Dark, new Vector2(1f, 0f), new Vector2(-20, 18), new Vector2(140, 92), () =>
            {
                if (mode == GameMode.Menaces)
                {
                    ShowPage(Page.Threats);
                    return;
                }
                infoMode = mode;
                ShowPage(Page.ModeInfo);
            }, 32);
        }

        static string BestLine(string board)
        {
            int best = Leaderboard.PersonalBest(board);
            return best > 0 ? $"Record : {Medals.FormatScore(best)}" : "Pas encore de record";
        }

        /// <summary>"Argent · Or à 75 000" for the personal best of a board.</summary>
        static string MedalLine(string board)
        {
            int best = Leaderboard.PersonalBest(board);
            var m = Medals.For(board, best);
            if (!Medals.Next(board, best, out var next, out int at)) return $"<color=#{Medals.Hex(m)}>{Medals.Name(m)} !</color>";
            if (m == Medal.None) return $"Objectif : {Medals.Name(next)} à {Medals.FormatScore(at)}";
            return $"<color=#{Medals.Hex(m)}>{Medals.Name(m)}</color>  ·  {Medals.Name(next)} à {Medals.FormatScore(at)}";
        }

        // ---------- Rules page (Classique / Défi du jour / Défi de la semaine) ----------

        void BuildModeInfo()
        {
            var mode = infoMode;
            string board = GameModes.BoardOf(mode);
            var subtitle = Header(mode == GameMode.Weekly ? "DÉFI HEBDO" : GameModes.ModeName(mode), ""); // the header box is 640 wide
            BackButton(Page.Home);
            subtitle.color = ModeColor(mode);
            if (mode == GameMode.Daily)
                liveUpdate += () => { if (subtitle) subtitle.text = $"Défi du {GameModes.DayLabel(GameModes.Today)}  ·  nouveau défi dans {GameModes.FormatSpan(GameModes.UntilNextDaily)}"; };
            else if (mode == GameMode.Weekly)
                liveUpdate += () => { if (subtitle) subtitle.text = $"Semaine {GameModes.ThisWeek % 100}  ·  fin dans {GameModes.FormatSpan(GameModes.UntilNextWeek)}"; };
            else subtitle.text = "Survie infinie  ·  le hasard fait ton set";

            var content = MetaUI.Scroll(page, new Vector2(0, -10), new Vector2(1000, 1260), out pageScroll);
            float y = 0f;

            SectionTitle(content, "RÈGLES", y);
            y += 70f;
            foreach (var line in RuleLines(mode)) y = Bullet(content, line, y);

            y += 24f;
            SectionTitle(content, "TON RÉSULTAT", y);
            y += 70f;
            y = MedalPanel(content, board, y);

            y += 24f;
            SectionTitle(content, "PALIERS", y);
            y += 70f;
            y = TierList(content, board, y);
            MetaUI.SetHeight(content, y + 20f);

            string playLabel = mode == GameMode.Daily && GameModes.DailyRankedUsed ? "S'ENTRAÎNER" : "JOUER";
            var play = UIKit.Button(page, playLabel, UIKit.Blood, UIKit.Center, new Vector2(-170, -720), new Vector2(620, 130), () => PlayMode(mode), 54);
            UIAnim.Pulse((RectTransform)play.transform, 3f, 0.03f);
            UIKit.Button(page, "CLASSEMENT", UIKit.Dark, UIKit.Center, new Vector2(330, -720), new Vector2(320, 130), () =>
            {
                boardMode = mode;
                ShowPage(Page.Board);
            }, 38);
        }

        static List<string> RuleLines(GameMode mode)
        {
            var list = new List<string>();
            switch (mode)
            {
                case GameMode.Daily:
                {
                    int day = GameModes.Today;
                    list.Add("La même partie pour tout le monde aujourd'hui");
                    list.Add($"Arme imposée : <color=#{MetaUI.GoldHex}>{GameModes.WeaponName(GameModes.DailyWeapon(day))}</color>");
                    foreach (var t in Threats.InMask(GameModes.DailyThreats(day)))
                        list.Add($"Menace : <color=#FF8080>{t.name}</color>  <size=28>{t.description} (+{t.Percent} %)</size>");
                    list.Add("Cartes tirées dans le même ordre pour tous (mêmes choix = mêmes cartes)");
                    list.Add($"Score {Threats.FormatMult(GameModes.DailyMult(day))}  ·  <color=#{MetaUI.GoldHex}>1 seul essai classé par jour</color>, les suivants sont de l'entraînement");
                    list.Add(GameModes.DailyRankedUsed
                        ? $"<color=#FFA070>Ton essai classé du jour est utilisé ({GameModes.DailyAttemptsToday} essai{(GameModes.DailyAttemptsToday > 1 ? "s" : "")})</color>"
                        : $"<color=#{MetaUI.GreenHex}>Ton essai classé est disponible</color>");
                    break;
                }
                case GameMode.Weekly:
                {
                    int week = GameModes.ThisWeek;
                    var el = GameModes.WeeklyElement(week);
                    string name = Synergies.TagName(el);
                    list.Add($"Élément de la semaine : <color=#{GameModes.ElementHex(el)}>{name}</color>, le même pour tout le monde");
                    list.Add($"Arme imposée : <color=#{MetaUI.GoldHex}>{GameModes.WeaponName(GameModes.WeeklyWeapon(week))}</color>");
                    list.Add($"Les cartes {name} sortent 2 fois plus souvent");
                    foreach (var t in Threats.InMask(GameModes.WeeklyThreats(week)))
                        list.Add($"Menace : <color=#FF8080>{t.name}</color>  <size=28>{t.description} (+{t.Percent} %)</size>");
                    list.Add("Cartes tirées dans le même ordre pour tous (mêmes choix = mêmes cartes)");
                    list.Add($"Score {Threats.FormatMult(GameModes.WeeklyMult(week))}  ·  essais illimités, ton meilleur score compte");
                    break;
                }
                default:
                    list.Add("Survie infinie, sans menace : les règles pures");
                    list.Add("Tout le monde commence pareil, aucun bonus permanent");
                    list.Add("C'est le hasard (et tes choix) qui fait ton set en jeu");
                    list.Add("Arme de départ : celle choisie dans le Codex");
                    list.Add("Score = points des ennemis tués + 2 par seconde survécue");
                    break;
            }
            return list;
        }

        static float Bullet(RectTransform content, string text, float y)
        {
            var t = MetaUI.Text(content, "•  " + text, 32, Color.white, TextAnchor.UpperLeft, UIKit.Top, new Vector2(0, -y), new Vector2(960, 90));
            float h = Mathf.Max(46f, t.preferredHeight + 10f);
            t.rectTransform.sizeDelta = new Vector2(960, h);
            return y + h;
        }

        /// <summary>Medal icon of the personal best, record, next tier and progress bar.</summary>
        static float MedalPanel(RectTransform content, string board, float y)
        {
            int best = Leaderboard.PersonalBest(board);
            var m = Medals.For(board, best);
            var card = MetaUI.Card(content, "Medal", UIKit.Top, new Vector2(0, -y), new Vector2(960, 200), new Color(1f, 1f, 1f, 0.05f));
            Medals.Icon(card, UIKit.Left, new Vector2(24, 0), 150, m);
            MetaUI.Text(card, m == Medal.None ? "Pas encore de médaille" : Medals.Name(m), 46, Medals.ColorOf(m), TextAnchor.UpperLeft,
                UIKit.TopLeft, new Vector2(200, -20), new Vector2(740, 58), FontStyle.Bold);
            MetaUI.Text(card, best > 0 ? $"Ton record : {Medals.FormatScore(best)}" : "Pas encore de record sur ce classement", 32, UIKit.Muted,
                TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(200, -82), new Vector2(740, 44));
            MetaUI.Text(card, Medals.NextText(board, best), 30, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(200, -124), new Vector2(740, 40));
            Medals.Next(board, best, out var next, out _);
            MetaUI.Bar(card, UIKit.TopLeft, new Vector2(200, -170), new Vector2(720, 18), Medals.ColorOf(next), Medals.Progress(board, best));
            return y + 220f;
        }

        static float TierList(RectTransform content, string board, float y)
        {
            int best = Leaderboard.PersonalBest(board);
            var t = Medals.Thresholds(board);
            for (int i = 0; i < t.Length; i++)
            {
                var m = (Medal)(i + 1);
                bool got = best >= t[i];
                var row = MetaUI.Card(content, "Tier", UIKit.Top, new Vector2(0, -y), new Vector2(960, 70), new Color(1f, 1f, 1f, got ? 0.08f : 0.03f));
                Medals.Icon(row, UIKit.Left, new Vector2(16, 0), 54, got ? m : Medal.None);
                MetaUI.Text(row, Medals.Name(m), 34, got ? Medals.ColorOf(m) : UIKit.Muted, TextAnchor.MiddleLeft, UIKit.Left, new Vector2(90, 0), new Vector2(400, 60), FontStyle.Bold);
                MetaUI.Text(row, Medals.FormatScore(t[i]) + (got ? $"  <color=#{MetaUI.GreenHex}>atteint</color>" : ""), 34, got ? Color.white : UIKit.Muted,
                    TextAnchor.MiddleRight, UIKit.Right, new Vector2(-24, 0), new Vector2(400, 60), FontStyle.Bold);
                y += 78f;
            }
            return y;
        }

        // ---------- Menaces (NG+) ----------

        void BuildThreats()
        {
            int best = Leaderboard.PersonalBest(GameModes.MenacesBoard);
            var m = Medals.For(GameModes.MenacesBoard, best);
            Header("MENACES", best > 0 ? $"NG+  ·  Record {Medals.FormatScore(best)}  ·  {Medals.Name(m)}" : "NG+  ·  chaque menace multiplie ton score");
            BackButton(Page.Home);

            if (!GameModes.MenacesUnlocked)
            {
                BuildThreatsLocked();
                return;
            }

            // Test unlock is on but the camp isn't maxed: offer to lock again.
            if (GameModes.DevUnlockAll && !Camp.IsMaxed)
                UIKit.Button(page, "Reverrouiller (test)", UIKit.Dark, UIKit.Center, new Vector2(400, 780), new Vector2(240, 80), () =>
                {
                    GameModes.DevUnlockAll = false;
                    Refresh();
                }, 24);

            var content = MetaUI.Scroll(page, new Vector2(0, 50), new Vector2(1000, 1160), out pageScroll);
            float y = 0f;
            for (int i = 0; i < Threats.All.Length; i++)
            {
                ThreatRow(content, Threats.All[i], y);
                y += 128f;
            }
            MetaUI.Text(content, "Aucun bonus permanent : les Menaces ne durent qu'une partie, mêmes règles pour tous ceux qui les choisissent.",
                28, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -y - 10), new Vector2(940, 80));
            MetaUI.SetHeight(content, y + 100f);

            int mask = GameModes.SelectedThreats;
            int n = Threats.Count(mask);
            MetaUI.Text(page, $"Multiplicateur : <color=#{MetaUI.GoldHex}>{Threats.FormatMult(Threats.ScoreMult(mask))}</color>  <size=32>·  {n} menace{(n > 1 ? "s" : "")}</size>",
                46, Color.white, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -585), new Vector2(1000, 60), FontStyle.Bold);
            var play = UIKit.Button(page, "JOUER", UIKit.Blood, UIKit.Center, new Vector2(-170, -715), new Vector2(620, 130), () => PlayMode(GameMode.Menaces), 56);
            UIAnim.Pulse((RectTransform)play.transform, 3f, 0.03f);
            UIKit.Button(page, "CLASSEMENT", UIKit.Dark, UIKit.Center, new Vector2(330, -715), new Vector2(320, 130), () =>
            {
                boardMode = GameMode.Menaces;
                ShowPage(Page.Board);
            }, 38);
        }

        void ThreatRow(RectTransform content, ThreatDef t, float y)
        {
            bool on = GameModes.IsSelected(t.id);
            var row = MetaUI.Card(content, "Threat " + t.id, UIKit.Top, new Vector2(0, -y), new Vector2(960, 116),
                on ? new Color(0.28f, 0.08f, 0.1f, 0.97f) : UIKit.Card);
            string bonus = $"<color=#{(on ? MetaUI.GoldHex : "BFBFBF")}>+{t.Percent} %</color>";
            MetaUI.Text(row, $"{t.name}   {bonus}" + (t.mutator ? "  <size=26><color=#9FD8FF>MUTATEUR</color></size>" : ""), 38, on ? Color.white : UIKit.Muted,
                TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(24, -14), new Vector2(710, 50), FontStyle.Bold);
            MetaUI.Text(row, t.description, 27, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(24, -66), new Vector2(710, 44));
            string id = t.id;
            UIKit.Button(row, on ? "ACTIVE" : "OFF", on ? UIKit.Blood : UIKit.Dark, UIKit.Right, new Vector2(-18, 0), new Vector2(190, 84), () =>
            {
                GameModes.ToggleThreat(id);
                Refresh();
            }, 34);
        }

        void BuildThreatsLocked()
        {
            var card = MetaUI.Card(page, "Locked", UIKit.Center, new Vector2(0, 160), new Vector2(980, 760), MetaUI.Locked);
            MetaUI.Text(card, "VERROUILLÉ", 64, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -40), new Vector2(940, 80), FontStyle.Bold);
            MetaUI.Text(card, GameModes.MenacesLockText, 34, Color.white, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -140), new Vector2(920, 100));
            float fill = Camp.MaxTotalLevels > 0 ? Camp.TotalLevels / (float)Camp.MaxTotalLevels : 0f;
            MetaUI.Bar(card, UIKit.Top, new Vector2(0, -250), new Vector2(800, 30), UIKit.Gold, fill);

            var text = MetaUI.Text(card,
                "Les Menaces (NG+) rendent la partie plus dure : plus d'ennemis, plus de PV, boss pressé, famine...\n" +
                "Chaque menace activée augmente ton multiplicateur de score.\n" +
                "Aucun bonus permanent : tout le monde joue avec les mêmes règles.",
                30, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -310), new Vector2(900, 260));
            text.lineSpacing = 1.1f;

            UIKit.Button(card, "ALLER AU CAMP", UIKit.Dark, UIKit.Bottom, new Vector2(0, 40), new Vector2(560, 120), () => ShowPage(Page.Camp), 44);

            // Developer unlock so the owner can test tonight. Clearly labeled.
            UIKit.Button(page, "Débloquer (test)", new Color(0.25f, 0.2f, 0.35f), UIKit.Center, new Vector2(0, -400), new Vector2(460, 100), () =>
            {
                GameModes.DevUnlockAll = true;
                Refresh();
            }, 34);
            MetaUI.Text(page, "Mode test : débloque tout ce qui est verrouillé, pour essayer", 26, UIKit.Muted, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, -480), new Vector2(1000, 40));
        }

        // ---------- Leaderboards ----------

        static readonly GameMode[] BoardTabs = { GameMode.Classic, GameMode.Daily, GameMode.Weekly, GameMode.Menaces };
        static readonly string[] BoardTabNames = { "CLASSIQUE", "DU JOUR", "HEBDO", "MENACES" };

        void BuildBoardPage()
        {
            var mode = boardMode == GameMode.Campaign ? GameMode.Classic : boardMode;
            string board = GameModes.BoardOf(mode);
            var subtitle = Header("CLASSEMENT", BoardSubtitle(mode));
            BackButton(Page.Home);
            if (mode == GameMode.Daily)
                liveUpdate += () => { if (subtitle) subtitle.text = BoardSubtitle(GameMode.Daily); };
            else if (mode == GameMode.Weekly)
                liveUpdate += () => { if (subtitle) subtitle.text = BoardSubtitle(GameMode.Weekly); };

            for (int i = 0; i < BoardTabs.Length; i++)
            {
                var tabMode = BoardTabs[i];
                UIKit.Button(page, BoardTabNames[i], tabMode == mode ? UIKit.Blood : UIKit.Dark, UIKit.Center,
                    new Vector2(-372 + i * 248, 625), new Vector2(236, 90), () =>
                    {
                        if (boardMode == tabMode) return;
                        boardMode = tabMode;
                        ShowPage(Page.Board);
                    }, 30);
            }

            // Personal best, medal and next tier
            int best = Leaderboard.PersonalBest(board);
            var m = Medals.For(board, best);
            var info = MetaUI.Card(page, "Board Info", UIKit.Center, new Vector2(0, 495), new Vector2(980, 140), UIKit.Card);
            Medals.Icon(info, UIKit.Left, new Vector2(20, 0), 110, m);
            MetaUI.Text(info, best > 0 ? $"Ton record : {Medals.FormatScore(best)}  ·  <color=#{Medals.Hex(m)}>{Medals.Name(m)}</color>" : "Pas encore de score ici",
                36, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(150, -20), new Vector2(810, 48), FontStyle.Bold);
            MetaUI.Text(info, Medals.NextText(board, best), 30, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(150, -78), new Vector2(810, 44));

            bool multColumn = mode == GameMode.Menaces;
            RankRow(page, UIKit.Center, new Vector2(0, 385), "#", "Pseudo", "", "Score", multColumn ? "Mult." : "Temps", UIKit.Muted, false);
            boardRows = UIKit.Rect("Rows", page);
            UIKit.Place(boardRows, UIKit.Center, new Vector2(0, 345), new Vector2(980, 0));
            boardRows.pivot = new Vector2(0.5f, 1f);
            var rows = boardRows;
            Leaderboard.Service.GetTop(board, BoardSize, entries =>
            {
                if (rows) FillBoardRows(rows, entries, mode); // the page may have changed meanwhile
            });
        }

        static string BoardSubtitle(GameMode mode) => mode switch
        {
            GameMode.Daily => $"Défi du {GameModes.DayLabel(GameModes.Today)}  ·  nouveau dans {GameModes.FormatSpan(GameModes.UntilNextDaily)}",
            GameMode.Weekly => $"Semaine : {Synergies.TagName(GameModes.WeeklyElement(GameModes.ThisWeek))}  ·  fin dans {GameModes.FormatSpan(GameModes.UntilNextWeek)}",
            GameMode.Menaces => "NG+  ·  score x multiplicateur des menaces",
            _ => "Survie infinie  ·  sur cet appareil",
        };

        static void FillBoardRows(RectTransform rows, List<LeaderboardEntry> entries, GameMode mode)
        {
            if (entries.Count == 0)
            {
                string text = mode == GameMode.Daily ? "Personne n'a encore joué le défi du jour.\nÀ toi d'ouvrir le bal !" : "Aucune partie pour l'instant.\nÀ toi de jouer !";
                var empty = UIKit.Label(rows, text, 42, UIKit.Muted, TextAnchor.UpperCenter);
                UIKit.Place(empty.rectTransform, UIKit.Top, new Vector2(0, -60), new Vector2(900, 200));
                return;
            }

            bool multColumn = mode == GameMode.Menaces;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                Color color = i == 0 ? UIKit.Gold : Color.white;
                string last = multColumn ? Threats.FormatMult(Mathf.Max(1f, e.mult)) : UIKit.FormatTime(e.seconds);
                RankRow(rows, UIKit.Top, new Vector2(0, -i * 74), (i + 1).ToString(), e.name, e.title, Medals.FormatScore(e.score), last, color, i % 2 == 0);
            }
        }

        /// <summary>One leaderboard line. Nickname and title are user text: rich text off.</summary>
        static void RankRow(RectTransform parent, Vector2 anchor, Vector2 position, string rank, string name, string title, string score, string last, Color color, bool shaded)
        {
            var row = UIKit.Rect("Row", parent);
            UIKit.Place(row, anchor, position, new Vector2(980, 70));
            UIKit.Image(row, new Color(1f, 1f, 1f, shaded ? 0.06f : 0f)).raycastTarget = false;

            bool hasTitle = !string.IsNullOrEmpty(title);
            RowCell(row, rank, 20, 90, 0f, 38, color, TextAnchor.MiddleLeft);
            RowCell(row, name, 110, 470, hasTitle ? 12f : 0f, hasTitle ? 34 : 38, color, TextAnchor.MiddleLeft);
            if (hasTitle) RowCell(row, title, 112, 470, -20f, 22, new Color(1f, 0.8f, 0.45f), TextAnchor.MiddleLeft);
            RowCell(row, score, 580, 230, 0f, 38, color, TextAnchor.MiddleRight);
            RowCell(row, last, 830, 130, 0f, 34, color, TextAnchor.MiddleRight);
        }

        static void RowCell(RectTransform row, string text, float x, float width, float yOffset, int size, Color color, TextAnchor alignment)
        {
            var t = UIKit.Label(row, text ?? "", size, color, alignment, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.supportRichText = false; // nicknames and titles are user text
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 0);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 0.5f);
            r.anchoredPosition = new Vector2(x, yOffset);
            r.sizeDelta = new Vector2(width, 0);
        }
    }
}
