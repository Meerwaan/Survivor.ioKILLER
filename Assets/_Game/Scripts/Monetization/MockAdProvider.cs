using System;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Stand-in ad network so every flow can be tested before a real SDK is plugged in:
    /// a full-screen "PUBLICITÉ (test)" overlay with a countdown (unscaled time), then a close button.
    /// Rewarded ads are granted only when watched to the end; "Passer" cancels (tests the decline path).
    /// Never touches Time.timeScale or audio.
    /// </summary>
    public class MockAdProvider : IAdProvider
    {
        /// <summary>Length of a test ad, in real seconds.</summary>
        public static float Seconds = 3f;

        public bool IsTest => true;
        public bool IsRewardedReady(string placement) => true;
        public bool IsInterstitialReady(string placement) => true;

        public void ShowRewarded(string placement, Action<bool> onDone) => MockAdView.Open(placement, true, Seconds, onDone);

        public void ShowInterstitial(string placement, Action onDone) => MockAdView.Open(placement, false, Seconds, _ => onDone?.Invoke());
    }

    /// <summary>The fake ad itself (one per ad, destroyed when closed).</summary>
    public class MockAdView : MonoBehaviour
    {
        static readonly string[] Pitches =
        {
            "SUPER JEU DE FERME\n<size=48>Tes carottes t'attendent !</size>",
            "EAU DE SOURCE D'OSTROV\n<size=48>Puisée à la résurgence. Pure, promis.</size>",
            "ASSURANCE APOCALYPSE\n<size=48>Parce qu'on ne sait jamais.</size>",
            "TRONÇONNEUSES & CO\n<size=48>-20 % sur toute la gamme.</size>",
        };

        bool rewarded, finished, closed;
        float duration, age;
        Action<bool> onDone;
        Text countdown, status;
        Image progress;
        Button closeButton, skipButton;
        RectTransform canvas;

        public static void Open(string placement, bool rewarded, float seconds, Action<bool> onDone)
        {
            var canvas = MonetizationUI.NewCanvas("Mock Ad (" + placement + ")", MonetizationUI.AdOrder);
            var view = canvas.gameObject.AddComponent<MockAdView>();
            view.canvas = canvas;
            view.rewarded = rewarded;
            view.duration = Mathf.Max(0.5f, seconds);
            view.onDone = onDone;
            view.Build(placement);
        }

        void Build(string placement)
        {
            var bg = UIKit.Overlay("Ad", canvas, 1f);

            // Top bar
            MetaUI.Text(bg, "PUBLICITÉ (test)", 44, MonetizationUI.AdYellow, TextAnchor.MiddleLeft, UIKit.TopLeft, new Vector2(40, -40), new Vector2(700, 80), FontStyle.Bold);
            MetaUI.Text(bg, rewarded ? "Pub récompensée  ·  " + placement : "Pub avant la partie  ·  " + placement, 28, UIKit.Muted,
                TextAnchor.MiddleLeft, UIKit.TopLeft, new Vector2(40, -110), new Vector2(800, 44));

            var circle = UIKit.Rect("Countdown", bg);
            UIKit.Place(circle, UIKit.TopRight, new Vector2(-40, -40), new Vector2(110, 110));
            UIKit.Image(circle, new Color(1f, 1f, 1f, 0.15f), UIKit.Circle).raycastTarget = false;
            countdown = UIKit.Label(circle, "", 60, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(countdown.rectTransform);

            closeButton = UIKit.Button(bg, "FERMER  X", UIKit.Dark, UIKit.TopRight, new Vector2(-40, -40), new Vector2(300, 110), Close, 40);
            closeButton.gameObject.SetActive(false);

            // The "ad": a loud placeholder card.
            var hue = UnityEngine.Random.value;
            var card = MetaUI.Card(bg, "Creative", UIKit.Center, new Vector2(0, 80), new Vector2(900, 1000), Color.HSVToRGB(hue, 0.55f, 0.55f));
            MetaUI.Card(card, "Inner", UIKit.Center, Vector2.zero, new Vector2(860, 960), Color.HSVToRGB(hue, 0.45f, 0.25f));
            var pitch = MetaUI.Text(card, Pitches[UnityEngine.Random.Range(0, Pitches.Length)], 80, Color.white, TextAnchor.MiddleCenter,
                UIKit.Center, new Vector2(0, 120), new Vector2(820, 500), FontStyle.Bold);
            UIAnim.Pulse(pitch.rectTransform, 2.5f, 0.03f);
            MetaUI.Text(card, "Ici, une vraie pub plus tard.\nMerci : c'est ce qui garde le jeu gratuit et équitable.", 34, UIKit.Muted,
                TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -300), new Vector2(820, 140));

            // Bottom: progress and (rewarded) the decline path.
            progress = MetaUI.Bar(bg, UIKit.Bottom, new Vector2(0, 330), new Vector2(900, 30), MonetizationUI.AdYellow, 0f);
            status = MetaUI.Text(bg, "", 36, Color.white, TextAnchor.MiddleCenter, UIKit.Bottom, new Vector2(0, 250), new Vector2(1000, 60));
            if (rewarded)
                skipButton = UIKit.Button(bg, "Passer (sans récompense)", UIKit.Dark, UIKit.Bottom, new Vector2(0, 90), new Vector2(620, 110), Skip, 36);

            Refresh();
        }

        void Update()
        {
            if (closed) return;
            age += Time.unscaledDeltaTime;
            if (!finished && age >= duration)
            {
                finished = true;
                closeButton.gameObject.SetActive(true);
                countdown.transform.parent.gameObject.SetActive(false);
                if (skipButton) skipButton.gameObject.SetActive(false);
                MetaFX.Punch((RectTransform)closeButton.transform);
                if (rewarded) SFX.Play(SfxId.Click);
            }
            Refresh();
        }

        void Refresh()
        {
            float left = Mathf.Max(0f, duration - age);
            countdown.text = Mathf.CeilToInt(left).ToString();
            progress.fillAmount = Mathf.Clamp01(age / duration);
            status.text = rewarded
                ? finished ? $"<color=#{MetaUI.GreenHex}>Récompense obtenue ! Ferme la pub.</color>" : "Regarde jusqu'au bout pour la récompense"
                : finished ? "Tu peux fermer la pub." : "La partie commence juste après.";
        }

        void Skip() => Finish(false);

        void Close() => Finish(rewarded && finished || !rewarded);

        void Finish(bool granted)
        {
            if (closed) return;
            closed = true;
            var callback = onDone;
            onDone = null;
            Destroy(canvas.gameObject);
            callback?.Invoke(granted);
        }
    }
}
