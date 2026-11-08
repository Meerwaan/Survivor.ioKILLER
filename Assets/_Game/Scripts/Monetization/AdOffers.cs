using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// Ready-made rewarded-ad offers other screens drop in with one call (so their owners keep their files clean).
    /// Gold has no run power, so these are allowed in every mode, ranked included.
    /// </summary>
    public static class AdOffers
    {
        /// <summary>Default size of the "x2 or" button.</summary>
        public static readonly Vector2 DoubleGoldSize = new(440, 130);

        /// <summary>Center-anchored overload (see the full version).</summary>
        public static Button DoubleGoldButton(Transform parent, Vector2 position, RunReport report) =>
            DoubleGoldButton(parent, UIKit.Center, position, report, DoubleGoldSize);

        /// <summary>
        /// End of run "x2 or (pub)": doubles the gold banked for the run (GameManager.GoldEarned, i.e. report.runGold),
        /// once per run, any mode. Returns null (nothing drawn) when there's nothing to double or no ad to show.
        /// </summary>
        public static Button DoubleGoldButton(Transform parent, Vector2 anchor, Vector2 position, RunReport report, Vector2 size)
        {
            if (!parent || report == null) return null;
            int amount = report.runGold;
            if (amount <= 0) return null;

            if (MonetizationRuntime.DoubleGoldUsedThisRun)
            {
                return MetaUI.Button(parent, $"OR DOUBLÉ\n<size=28>+{amount} or reçus</size>", UIKit.Dark, anchor, position, size, () => { }, 36, false);
            }
            if (!Ads.IsRewardedReady(AdPlacement.DoubleGold)) return null;

            string test = Ads.IsTest ? " (test)" : "";
            Button button = null;
            button = MonetizationUI.AdButtonUI(parent, $"x2 OR\n<size=28>+{amount} or{test}</size>", anchor, position, size, () =>
            {
                if (MonetizationRuntime.DoubleGoldUsedThisRun) return;
                Ads.ShowRewarded(AdPlacement.DoubleGold, ok =>
                {
                    if (!ok || MonetizationRuntime.DoubleGoldUsedThisRun) return;
                    MonetizationRuntime.DoubleGoldUsedThisRun = true;
                    Progression.AddGold(amount);
                    MetaSave.Data.life.goldEarned += amount;
                    MetaSave.Save();
                    if (!button) return;
                    MonetizationUI.SetLabel(button, $"OR DOUBLÉ\n<size=28>+{amount} or reçus</size>");
                    MonetizationUI.SetEnabled(button, false, MonetizationUI.AdButton);
                    var tag = button.transform.Find("Ad Tag");
                    if (tag) tag.gameObject.SetActive(false);
                    var layer = button.transform.parent as RectTransform;
                    if (layer) MetaFX.FloatText(layer, button.transform.position, $"+{amount} or", UIKit.Gold, 64);
                    SFX.Play(SfxId.Click);
                });
            }, 40);
            button.targetGraphic.color = MetaUI.Green;
            UIAnim.Pulse((RectTransform)button.transform, 3f, 0.035f);
            return button;
        }
    }
}
