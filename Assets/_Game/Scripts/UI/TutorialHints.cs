using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// First-time player hints: short contextual tips shown once each (saved in PlayerPrefs), during the run only,
    /// one at a time, never on top of each other. Teaches the core loop in the first two runs without a tutorial level:
    /// move / auto-fire, gems, level-up choices and evolutions, set bonuses, healing, bosses, banish / reroll.
    /// Added at runtime by GameHUD.
    /// </summary>
    public class TutorialHints : MonoBehaviour
    {
        const string Key = "tuto_seen_v1";
        /// <summary>Seconds between two hints.</summary>
        const float Spacing = 5f;

        enum Hint
        {
            Move = 0,
            Gems = 1,
            Choices = 2,
            Evolution = 3,
            SetBonus = 4,
            LowHealth = 5,
            Boss = 6,
            Reroll = 7,
            Pause = 8,
            Stuff = 9,
            Danger = 10,
        }

        static readonly Dictionary<Hint, string> Texts = new()
        {
            [Hint.Move] = "Glisse ton doigt n'importe où pour bouger.\n<color=#FFD94D>Tes armes tirent toutes seules !</color>",
            [Hint.Gems] = "Ramasse les <color=#E58E2B>éclats</color> d'ambre :\nton injecteur les change en niveaux.",
            [Hint.Choices] = "Chaque niveau = un choix (jusqu'au stuff complet). Mêmes chances pour tous :\n<color=#FFD94D>c'est ton stuff qui fait la différence.</color>",
            [Hint.Evolution] = "Arme au <color=#FFD94D>Nv 5</color> + son bonus partenaire (indiqué sur la carte)\n= <color=#D973FF>ÉVOLUTION</color> surpuissante !",
            [Hint.SetBonus] = "<color=#FFD94D>Synergie !</color> 2 objets de la même famille (Feu, Lame, Poison…)\n= palier I. Va jusqu'à 4 pour le palier II.",
            [Hint.LowHealth] = "<color=#FF5544>Vie basse !</color> Bouge, ne reste pas encerclé.\nLes <color=#FF8866>Rations</color> et les caisses soignent.",
            [Hint.Boss] = "<color=#FF5544>SOUCHE !</color> Suis la flèche rouge.\nAbats-la : sa caisse rapporte gros.",
            [Hint.Reroll] = "Pas de bonne carte ? <color=#FFD94D>RELANCER</color> tire une nouvelle main,\n<color=#FFD94D>BANNIR</color> retire un objet pour toute la partie.",
            [Hint.Pause] = "Le bouton pause montre ton stuff complet\net tes synergies actives.",
            [Hint.Stuff] = "Ton <color=#FFD94D>STUFF</color> a 20 niveaux max (jauge sous les cartes).\nSpécialise-toi : <color=#FFD94D>armes Nv 5 + partenaires</color> !",
            [Hint.Danger] = "Le <color=#FF7A50>DANGER</color> monte toutes les 2:30 (sous le chrono).\nEn haut à droite : ton score <color=#8CFF7A>contre ton record</color>.",
        };

        int seen;
        float cooldown = 1.2f, showFor, age, check;
        RectTransform box;
        Text label;
        CanvasGroup group;
        int lastLevel = 1;

        void Awake()
        {
            seen = PlayerPrefs.GetInt(Key, 0);
            Build();
        }

        /// <summary>Clears the seen hints (Profile "revoir les astuces" or tests).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        bool Seen(Hint h) => (seen & (1 << (int)h)) != 0;

        void Build()
        {
            var canvas = UIKit.Canvas("Hints Canvas", transform, 11);
            box = UIKit.Rect("Hint", canvas);
            // Lower half: the event banners and multi-kill callouts use the upper half.
            UIKit.Place(box, UIKit.Center, new Vector2(0, -300), new Vector2(980, 190));
            UIKit.Image(box, new Color(0f, 0f, 0f, 0.72f)).raycastTarget = false;
            var stripe = UIKit.Rect("Stripe", box);
            UIKit.Place(stripe, UIKit.Left, new Vector2(6, 0), new Vector2(12, 190));
            UIKit.Image(stripe, UIKit.Gold).raycastTarget = false;
            label = UIKit.Label(box, "", 40, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(label.rectTransform, 24f);
            group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            box.gameObject.SetActive(false);
        }

        void Show(Hint h, float duration = 5.5f)
        {
            seen |= 1 << (int)h;
            PlayerPrefs.SetInt(Key, seen);
            PlayerPrefs.Save();
            label.text = Texts[h];
            showFor = duration;
            age = 0f;
            box.gameObject.SetActive(true);
            UIAnim.SlideIn(box, new Vector2(0, 40), 0f, 0.3f, 0.9f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool playing = gm && gm.State == GameState.Playing;

            if (box.gameObject.activeSelf)
            {
                // Hidden while a card screen / pause is up, and the timer only runs while playing.
                group.alpha = playing ? Mathf.Clamp01(Mathf.Min(age / 0.25f, (showFor - age) / 0.4f)) : 0f;
                if (playing) age += Time.unscaledDeltaTime;
                if (age >= showFor)
                {
                    box.gameObject.SetActive(false);
                    cooldown = Spacing;
                }
                return;
            }

            if (!playing) return;
            cooldown -= Time.unscaledDeltaTime;
            if (cooldown > 0f) return;
            check -= Time.unscaledDeltaTime;
            if (check > 0f) return;
            check = 0.25f;

            var next = NextHint(gm);
            if (next.HasValue) Show(next.Value);
        }

        Hint? NextHint(GameManager gm)
        {
            float t = gm.RunTime;
            var xp = PlayerXP.Instance;
            int level = xp ? xp.Level : 1;
            bool leveled = level > lastLevel;
            lastLevel = level;

            if (!Seen(Hint.Move)) return Hint.Move;

            var hp = PlayerHealth.Instance;
            if (!Seen(Hint.LowHealth) && hp && hp.Fraction < 0.35f) return Hint.LowHealth;
            if (!Seen(Hint.Boss) && BossAlive()) return Hint.Boss;

            if (!Seen(Hint.Gems) && level <= 1 && t > 4f && XPGem.Active.Count > 0) return Hint.Gems;
            if (!Seen(Hint.Choices) && level >= 2 && leveled) return Hint.Choices;
            if (!Seen(Hint.Stuff) && Seen(Hint.Choices) && level >= 4 && leveled) return Hint.Stuff;
            if (!Seen(Hint.Danger) && RunDirector.Instance && RunDirector.Instance.Danger >= 2) return Hint.Danger;
            if (!Seen(Hint.SetBonus) && Synergies.Active().Count > 0) return Hint.SetBonus;
            if (!Seen(Hint.Evolution) && level >= 5 && t > 60f) return Hint.Evolution;
            if (!Seen(Hint.Reroll) && level >= 8 && UpgradeManager.Instance && UpgradeManager.Instance.RerollsLeft > 0) return Hint.Reroll;
            if (!Seen(Hint.Pause) && t > 150f) return Hint.Pause;
            return null;
        }

        static bool BossAlive()
        {
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] && list[i].IsBoss && !list[i].IsDead) return true;
            return false;
        }
    }
}
