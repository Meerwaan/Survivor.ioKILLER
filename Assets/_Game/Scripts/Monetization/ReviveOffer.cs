using System;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// "Seconde chance": on death in a CAMPAIGN stage only (never in ranked modes), about once every 2-3 games,
    /// a 5 s offer to watch a rewarded ad and come back with 50% HP, a shockwave and a short invulnerability.
    /// Called from GameManager.PlayerDied(). Runs on unscaled time (the game is frozen meanwhile).
    /// </summary>
    public class ReviveOffer : MonoBehaviour
    {
        /// <summary>Runs started between two offers (offer on run N, earliest next one on run N+2).</summary>
        public const int EveryRuns = 2;
        public const float Seconds = 5f;
        public const float HealthFraction = 0.5f;
        public const float InvulnerableSeconds = 2.5f;
        const float NovaRadius = 8f;
        const float NovaDamage = 40f;
        const float NovaKnockback = 30f;

        RectTransform canvas;
        Image ring;
        Text count;
        float left = Seconds;
        bool waiting, done;
        Action revive, decline;

        /// <summary>Why no offer would be made right now (null = it would). Shown in the Profile test section.</summary>
        public static string Blocker()
        {
            if (!RunConfig.IsCampaign || RunConfig.Ranked) return "campagne uniquement";
            if (Ads.BotActive) return "bot de test";
            if (MonetizationRuntime.ReviveUsedThisRun) return "déjà proposée cette partie";
            int wait = EveryRuns - Monetization.Data.runsSinceReviveOffer;
            if (wait > 0) return $"dans {wait} partie{(wait > 1 ? "s" : "")}";
            if (!Ads.IsRewardedReady(AdPlacement.Revive)) return "aucune pub disponible";
            return null;
        }

        /// <summary>Runs before the next offer is allowed (0 = next campaign death).</summary>
        public static int RunsUntilNextOffer => Mathf.Max(0, EveryRuns - Monetization.Data.runsSinceReviveOffer);

        /// <summary>
        /// Shows the offer if allowed. <paramref name="revive"/> must put the game back in Playing (the revive
        /// effects are applied right after it); <paramref name="decline"/> ends the run. Returns false if no offer.
        /// </summary>
        public static bool TryOffer(Action revive, Action decline)
        {
            if (Blocker() != null || !PlayerHealth.Instance || !PlayerController.Instance) return false;

            MonetizationRuntime.ReviveUsedThisRun = true;
            Monetization.Data.runsSinceReviveOffer = 0;
            MetaSave.Save();

            var canvas = MonetizationUI.NewCanvas("Revive Offer", MonetizationUI.ReviveOrder);
            var offer = canvas.gameObject.AddComponent<ReviveOffer>();
            offer.canvas = canvas;
            offer.revive = revive;
            offer.decline = decline;
            offer.Build();
            return true;
        }

        void Build()
        {
            var bg = UIKit.Overlay("Revive", canvas, 0.72f);
            var title = MetaUI.Text(bg, "SECONDE CHANCE ?", 96, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 520), new Vector2(1060, 140), FontStyle.Bold);
            UIAnim.SlideIn(title.rectTransform, new Vector2(0, 80), 0f, 0.35f, 1.4f);

            var gm = GameManager.Instance;
            string progress = RunConfig.Duration > 0f && gm
                ? $"{RunConfig.Label}\nTu as tenu {UIKit.FormatTime(Mathf.FloorToInt(gm.RunTime))} sur {UIKit.FormatTime(Mathf.RoundToInt(RunConfig.Duration))}"
                : RunConfig.Label;
            MetaUI.Text(bg, progress, 38, Color.white, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 385), new Vector2(1000, 110));

            // Countdown ring
            var circle = UIKit.Rect("Countdown", bg);
            UIKit.Place(circle, UIKit.Center, new Vector2(0, 130), new Vector2(300, 300));
            UIKit.Image(circle, new Color(1f, 1f, 1f, 0.12f), UIKit.Circle).raycastTarget = false;
            var fill = UIKit.Rect("Fill", circle);
            UIKit.Stretch(fill);
            ring = UIKit.Image(fill, UIKit.Gold, UIKit.Circle);
            ring.raycastTarget = false;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            var inner = UIKit.Rect("Inner", circle);
            UIKit.Stretch(inner, 22f);
            UIKit.Image(inner, new Color(0.08f, 0.08f, 0.09f, 1f), UIKit.Circle).raycastTarget = false;
            count = UIKit.Label(circle, "", 130, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(count.rectTransform);

            MetaUI.Text(bg, $"Reviens avec {HealthFraction * 100f:0} % de vie, une onde de choc repousse la horde.", 34, UIKit.Muted,
                TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -90), new Vector2(980, 100));

            var yes = MonetizationUI.AdButtonUI(bg, "REVIVRE", UIKit.Center, new Vector2(0, -260), new Vector2(640, 170), OnYes, 64);
            yes.targetGraphic.color = MetaUI.Green;
            UIAnim.Pulse((RectTransform)yes.transform, 3.5f, 0.04f);
            UIKit.Button(bg, "Non merci", UIKit.Dark, UIKit.Center, new Vector2(0, -450), new Vector2(400, 110), OnNo, 40);
            if (Ads.IsTest)
                MetaUI.Text(bg, "(pub de test)", 28, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, -560), new Vector2(600, 40));
            Refresh();
        }

        void Update()
        {
            if (done || waiting) return;
            left -= Time.unscaledDeltaTime;
            Refresh();
            if (left <= 0f) OnNo();
        }

        void Refresh()
        {
            ring.fillAmount = Mathf.Clamp01(left / Seconds);
            count.text = Mathf.Max(0, Mathf.CeilToInt(left)).ToString();
        }

        void OnYes()
        {
            if (done || waiting) return;
            waiting = true;
            Ads.ShowRewarded(AdPlacement.Revive, ok =>
            {
                if (ok) Finish(true);
                else Finish(false);
            });
        }

        void OnNo()
        {
            if (done || waiting) return;
            Finish(false);
        }

        void Finish(bool revived)
        {
            if (done) return;
            done = true;
            Destroy(canvas.gameObject);
            if (revived)
            {
                revive?.Invoke();
                ApplyRevive();
            }
            else
            {
                decline?.Invoke();
            }
        }

        /// <summary>50% HP, short invulnerability, shockwave that pushes and hurts what's around.</summary>
        static void ApplyRevive()
        {
            var health = PlayerHealth.Instance;
            if (health) health.Revive(HealthFraction, InvulnerableSeconds);

            var player = PlayerController.Instance;
            if (player)
            {
                Vector3 p = player.Position;
                float area = player.Stats ? player.Stats.areaMult : 1f;
                float damage = player.Stats ? player.Stats.damageMult : 1f;
                Combat.Nova(p, NovaRadius * area, NovaDamage * damage);
                Combat.Area(p, NovaRadius * 1.6f * area, NovaDamage * 0.25f * damage, NovaKnockback, false);
                if (GameFX.Instance) GameFX.Instance.Ring(p, NovaRadius * 1.6f * area, Color.white * 2f, 0.6f);
            }
            if (GameHUD.Instance) GameHUD.Instance.ShowBanner("SECONDE CHANCE !", UIKit.Gold, 1.5f);
        }
    }
}
