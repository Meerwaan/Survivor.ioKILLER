using UnityEngine;

namespace Survivor
{
    public enum Medal { None, Bronze, Argent, Or, Diamant, Legende }

    /// <summary>
    /// Honest goals instead of fake players: score tiers per board (Bronze / Argent / Or / Diamant / Légende).
    /// Score = kill points + 2 per second + level bursts once the stuff is full (GameManager), times the threat multiplier. Rough model (Docs/Pacing.md:
    /// kills per 50 s wave ~350 at 2 min, ~700 at 5 min, ~1400 at 10 min, ~11 points per kill):
    /// 3 min ~10 k, 5 min ~27 k, 8 min ~65 k, 10 min ~95 k, 13 min ~160 k, 16 min ~250 k.
    /// A decent player (dies around 8-12 min) lands Argent / Or in Classique. Tune here only.
    /// Measured with the kite bot (skill 0.75), Classique: 14 k at 3:19 (bad build), 55 k at 7:00, 79 k at 8:20, 209 k at 13:45,
    /// death 15:44 with 270 k (3 evolutions). Légende (400 k) needs ~18 min: it must stay rare.
    /// </summary>
    public static class Medals
    {
        static readonly int[] Classic = { 10000, 40000, 100000, 200000, 400000 };
        /// <summary>Daily / weekly: 1-2 fixed threats (x1.1 to x1.5) partly offset by the harder run.</summary>
        static readonly int[] Challenge = { 12000, 45000, 120000, 240000, 480000 };
        /// <summary>Menaces: the multiplier can reach x2.8.</summary>
        static readonly int[] Menaces = { 15000, 60000, 150000, 300000, 600000 };

        public static int[] Thresholds(string boardId)
        {
            if (string.IsNullOrEmpty(boardId)) return Classic;
            if (boardId.StartsWith("daily_") || boardId.StartsWith("weekly_")) return Challenge;
            if (boardId == GameModes.MenacesBoard) return Menaces;
            return Classic;
        }

        public static Medal For(string boardId, int score)
        {
            var t = Thresholds(boardId);
            var m = Medal.None;
            for (int i = 0; i < t.Length; i++)
                if (score >= t[i]) m = (Medal)(i + 1);
            return m;
        }

        /// <summary>The next tier above <paramref name="score"/>; false when Légende is reached.</summary>
        public static bool Next(string boardId, int score, out Medal next, out int at)
        {
            var t = Thresholds(boardId);
            for (int i = 0; i < t.Length; i++)
            {
                if (score >= t[i]) continue;
                next = (Medal)(i + 1);
                at = t[i];
                return true;
            }
            next = Medal.Legende;
            at = t[t.Length - 1];
            return false;
        }

        /// <summary>Progress from the current tier to the next one (1 at Légende).</summary>
        public static float Progress(string boardId, int score)
        {
            var t = Thresholds(boardId);
            int from = 0;
            for (int i = 0; i < t.Length; i++)
            {
                if (score >= t[i])
                {
                    from = t[i];
                    continue;
                }
                return Mathf.Clamp01((score - from) / (float)Mathf.Max(1, t[i] - from));
            }
            return 1f;
        }

        /// <summary>"Prochain palier : Or à 75 000" (rich text), or the top line.</summary>
        public static string NextText(string boardId, int score)
        {
            if (!Next(boardId, score, out var next, out int at)) return $"<color=#{Hex(Medal.Legende)}>Palier maximal : tu es une légende !</color>";
            return $"Prochain palier : <color=#{Hex(next)}>{Name(next)}</color> à {FormatScore(at)}";
        }

        public static string Name(Medal m) => m switch
        {
            Medal.Bronze => "Bronze",
            Medal.Argent => "Argent",
            Medal.Or => "Or",
            Medal.Diamant => "Diamant",
            Medal.Legende => "Légende",
            _ => "Aucune médaille",
        };

        public static Color ColorOf(Medal m) => m switch
        {
            Medal.Bronze => new Color(0.85f, 0.52f, 0.28f),
            Medal.Argent => new Color(0.82f, 0.86f, 0.92f),
            Medal.Or => UIKit.Gold,
            Medal.Diamant => new Color(0.45f, 0.9f, 1f),
            Medal.Legende => new Color(1f, 0.4f, 0.9f),
            _ => new Color(0.45f, 0.45f, 0.48f),
        };

        public static string Hex(Medal m) => ColorUtility.ToHtmlStringRGB(ColorOf(m));

        /// <summary>"75 000" (plain spaces: the UI font has no narrow no-break space).</summary>
        public static string FormatScore(int n)
        {
            string s = Mathf.Abs(n).ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return n < 0 ? "-" + sb : sb.ToString();
        }

        /// <summary>Round medal icon (colored disc, initial in the middle). Greyed for None.</summary>
        public static RectTransform Icon(Transform parent, Vector2 anchor, Vector2 position, float size, Medal m)
        {
            Color c = ColorOf(m);
            var r = UIKit.Rect("Medal", parent);
            UIKit.Place(r, anchor, position, new Vector2(size, size));
            UIKit.Image(r, new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f), UIKit.Circle).raycastTarget = false;
            var inner = UIKit.Rect("Inner", r);
            UIKit.Stretch(inner, size * 0.1f);
            UIKit.Image(inner, c, UIKit.Circle).raycastTarget = false;
            string letter = m == Medal.None ? "-" : Name(m).Substring(0, 1);
            var l = UIKit.Label(r, letter, Mathf.RoundToInt(size * 0.5f), new Color(0.08f, 0.06f, 0.05f), TextAnchor.MiddleCenter, FontStyle.Bold);
            l.GetComponent<UnityEngine.UI.Shadow>().enabled = false;
            UIKit.Stretch(l.rectTransform);
            return r;
        }
    }
}
