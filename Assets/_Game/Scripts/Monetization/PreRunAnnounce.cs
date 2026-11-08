using System;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// "Petite pub avant la partie… 3": the forced ad is always announced before it plays (owner's rule),
    /// with a way out ("Sans pub"). Runs on unscaled time; the countdown waits while the store popup is open.
    /// </summary>
    public class PreRunAnnounce : MonoBehaviour
    {
        public const float Seconds = 3f;

        RectTransform canvas;
        Text text;
        float left = Seconds;
        bool done;
        Action<bool> onDone;

        /// <summary>onDone(false) = play the ad now, onDone(true) = "Sans pub" was bought, skip it.</summary>
        public static void Show(Action<bool> onDone)
        {
            var canvas = MonetizationUI.NewCanvas("Pre-run Ad Announce", MonetizationUI.AnnounceOrder);
            var a = canvas.gameObject.AddComponent<PreRunAnnounce>();
            a.canvas = canvas;
            a.onDone = onDone;
            a.Build();
        }

        void Build()
        {
            var bg = UIKit.Overlay("Announce", canvas, 0.75f);
            // Wrapper so the shadow slides in with the card.
            var holder = UIKit.Rect("Holder", bg);
            UIKit.Place(holder, UIKit.Center, Vector2.zero, new Vector2(900, 620));
            var card = MetaUI.Card(holder, "Card", UIKit.Center, Vector2.zero, new Vector2(900, 620), UIKit.Card);
            UIKit.DropShadow(card, 22f, 0.6f, -12f);
            UIAnim.SlideIn(holder, new Vector2(0, -60), 0f, 0.25f, 0.85f);
            var stripe = UIKit.Rect("Stripe", card);
            UIKit.Place(stripe, UIKit.TopRight, new Vector2(-2, -2), new Vector2(878, 6));
            UIKit.Image(stripe, MonetizationUI.AdYellow).raycastTarget = false;
            MonetizationUI.AdTag(card, UIKit.TopLeft, new Vector2(30, -30), 50f);

            text = MetaUI.Text(card, "", 58, Color.white, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -110), new Vector2(840, 200), FontStyle.Bold);
            MetaUI.Text(card, "Une pub de quelques secondes, puis la partie démarre.\nMerci : elles financent un jeu gratuit et équitable.", 30, UIKit.Muted,
                TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -320), new Vector2(840, 100));

            var p = Store.Get(Store.NoAdsId);
            string label = $"Plus de pubs forcées : {p.price}" + (Store.IsTest ? " (test)" : "");
            UIKit.Button(card, label, MonetizationUI.Premium, UIKit.Bottom, new Vector2(0, 40), new Vector2(760, 110), BuyNoAds, 36);
            Refresh();
        }

        void BuyNoAds()
        {
            if (done || Store.Busy) return;
            Store.Purchase(Store.NoAdsId, ok =>
            {
                if (ok) Finish(true);
            });
        }

        void Update()
        {
            if (done || Store.Busy) return;
            left -= Time.unscaledDeltaTime;
            Refresh();
            if (left <= 0f) Finish(false);
        }

        void Refresh()
        {
            if (text) text.text = $"Petite pub avant la partie…\n<size=110><color=#{MetaUI.GoldHex}>{Mathf.Max(1, Mathf.CeilToInt(left))}</color></size>";
        }

        void Finish(bool skipped)
        {
            if (done) return;
            done = true;
            var callback = onDone;
            onDone = null;
            Destroy(canvas.gameObject);
            callback?.Invoke(skipped);
        }
    }
}
