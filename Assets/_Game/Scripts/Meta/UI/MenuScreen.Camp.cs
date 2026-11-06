using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>"Le Camp" tab: the survivors, storage, harvest and building upgrades (gold only, no run power).</summary>
    public partial class MenuScreen
    {
        void BuildCamp()
        {
            Header("LE CAMP", $"{Camp.Survivors} survivants  ·  Confort {Camp.Comfort}/{Camp.MaxComfort}  ·  réserve max {Camp.CapHours:0} h");

            // Storage + harvest
            var card = MetaUI.Card(page, "Storage", UIKit.Center, new Vector2(0, 530), new Vector2(980, 250), UIKit.Card);
            MetaUI.Text(card, "RÉSERVE", 38, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(30, -18), new Vector2(600, 50), FontStyle.Bold);
            var stock = MetaUI.Text(card, "", 34, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(30, -74), new Vector2(630, 50));
            var fill = MetaUI.Bar(card, UIKit.TopLeft, new Vector2(30, -134), new Vector2(620, 34), UIKit.Gold, 0f);
            var timer = MetaUI.Text(card, "", 28, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(30, -180), new Vector2(630, 44));
            Button harvest = null;
            harvest = UIKit.Button(card, "RÉCOLTER", MetaUI.Green, UIKit.Right, new Vector2(-25, 0), new Vector2(290, 180),
                () => HarvestFrom((RectTransform)harvest.transform), 44);

            var boostBadge = MetaUI.Text(card, "", 30, MetaUI.LightGreen, TextAnchor.UpperRight, UIKit.TopLeft, new Vector2(330, -22), new Vector2(320, 44), FontStyle.Bold);

            float campaign = (Camp.ProgressFactor - 1f) * 100f;
            float comfort = (Camp.ComfortFactor - 1f) * 100f;
            var production = MetaUI.Text(page, "", 28, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 372), new Vector2(1040, 44));

            // Rewarded-ad boosts (optional, gold only)
            var boosts = BuildCampBoosts();

            liveUpdate = () =>
            {
                stock.text = $"<color=#{MetaUI.GoldHex}>{Camp.StoredGold} / {Camp.GoldCapacity:0} or</color>";
                fill.fillAmount = Camp.Fill;
                float seconds = Camp.SecondsToFull;
                timer.text = seconds <= 1f ? $"<color=#{MetaUI.GoldHex}>Réserve pleine : récolte vite !</color>" : $"Pleine dans {FormatDuration(seconds)}";
                harvest.interactable = Camp.CanHarvest;
                bool boosted = Camp.IsBoosted;
                boostBadge.text = boosted ? $"x2 : {MonetizationUI.Duration(Camp.BoostSecondsLeft)}" : "";
                production.text = boosted
                    ? $"Production : <color=#{MetaUI.GreenHex}>{Camp.CurrentGoldPerHour:0} or/h (x2)</color>, même jeu fermé  <size=26>(+{campaign:0}% campagne, +{comfort:0}% confort)</size>"
                    : $"Production : {Camp.GoldPerHour:0} or/h, même jeu fermé  <size=26>(+{campaign:0}% campagne, +{comfort:0}% confort)</size>";
                boosts();
            };
            liveUpdate();

            // Buildings
            var content = MetaUI.Scroll(page, new Vector2(0, -320), new Vector2(1000, 940), out pageScroll);
            float y = 0f;
            for (int i = 0; i < Camp.Buildings.Count; i++)
            {
                BuildingRow(content, Camp.Buildings[i], y);
                y += 256f;
            }

            // Long-term goal. The New Game+ itself doesn't exist yet: just a teaser for now.
            string goal = Camp.IsMaxed
                ? $"<color=#{MetaUI.GoldHex}>Camp au max : mode MENACES (NG+) débloqué !</color>"
                : $"Camp : {Camp.TotalLevels}/{Camp.MaxTotalLevels} niveaux  ·  <size=26>un camp au max débloque les Menaces (NG+)</size>";
            MetaUI.Text(content, goal, 32, UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y - 10f), new Vector2(960, 70));
            y += 100f;
            MetaUI.SetHeight(content, y);
        }

        void BuildingRow(RectTransform content, BuildingDef b, float y)
        {
            int level = Camp.Level(b);
            bool maxed = level >= b.maxLevel;
            int next = level + 1;

            var row = MetaUI.Card(content, b.name, UIKit.Top, new Vector2(0, -y), new Vector2(960, 240), level > 0 ? UIKit.Card : MetaUI.Locked);
            Color c = level > 0 ? b.color : new Color(b.color.r * 0.45f, b.color.g * 0.45f, b.color.b * 0.45f);
            var icon = UIKit.Icon(row, b.name, c, false, 110, level > 0 ? level.ToString() : "");
            UIKit.Place(icon, UIKit.Left, new Vector2(18, 0), new Vector2(110, 110));

            MetaUI.Text(row, $"{b.name}  <size=30><color=#BBBBBB>Nv {level}/{b.maxLevel}</color></size>", 40, Color.white,
                TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(148, -14), new Vector2(550, 52), FontStyle.Bold);
            MetaUI.Text(row, b.description, 26, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(148, -64), new Vector2(550, 36));
            MetaUI.Text(row, level > 0 ? "Actuel : " + b.effect(level) : "Pas encore construit", 26, Color.white,
                TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(148, -102), new Vector2(550, 60));
            if (!maxed)
                MetaUI.Text(row, $"Niv. {next} : {b.effect(next)}", 26, MetaUI.LightGreen,
                    TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(148, -164), new Vector2(550, 64));

            string blocker = Camp.UpgradeBlocker(b);
            if (maxed)
            {
                MetaUI.Button(row, "MAX", UIKit.Dark, UIKit.Right, new Vector2(-18, 0), new Vector2(240, 150), () => { }, 44, false);
            }
            else if (blocker != null)
            {
                var lockBox = MetaUI.Card(row, "Locked", UIKit.Right, new Vector2(-18, 0), new Vector2(240, 150), new Color(0f, 0f, 0f, 0.35f));
                MetaUI.Text(lockBox, blocker, 28, MetaUI.Red, TextAnchor.MiddleCenter, UIKit.Center, Vector2.zero, new Vector2(220, 140));
            }
            else
            {
                int gold = b.goldCost(next);
                string label = (level == 0 ? "CONSTRUIRE" : "AMÉLIORER") + $"\n<size=30>{gold} or</size>";
                Button button = null;
                button = MetaUI.Button(row, label, MetaUI.Brown, UIKit.Right, new Vector2(-18, 0), new Vector2(240, 150),
                    () => UpgradeBuilding(b, (RectTransform)button.transform), 34, Progression.CanAfford(gold));
            }
        }

        /// <summary>
        /// "Production x2 pendant 2 h" (extendable to 4 h) and "Livraison express" (+1 h of gold, 2 a day), both rewarded ads.
        /// Returns the live refresh of the two buttons.
        /// </summary>
        System.Action BuildCampBoosts()
        {
            var card = MetaUI.Card(page, "Boosts", UIKit.Center, new Vector2(0, 250), new Vector2(980, 170), UIKit.Card);
            Button boost = null, express = null;
            boost = MonetizationUI.AdButtonUI(card, "", UIKit.Left, new Vector2(12, 0), new Vector2(472, 146), () =>
            {
                if (!Camp.CanBoost) return;
                Vector3 at = boost.transform.position;
                Ads.ShowRewarded(AdPlacement.CampBoost, ok =>
                {
                    if (!ok) return;
                    Camp.AddBoost();
                    if (fxLayer) MetaFX.FloatText(fxLayer, at, $"Production x2 : {MonetizationUI.Duration(Camp.BoostSecondsLeft)} !", MetaUI.LightGreen, 50);
                    liveUpdate?.Invoke();
                });
            }, 34);
            express = MonetizationUI.AdButtonUI(card, "", UIKit.Right, new Vector2(-12, 0), new Vector2(472, 146), () =>
            {
                if (Camp.ExpressLeftToday <= 0) return;
                Vector3 at = express.transform.position;
                Ads.ShowRewarded(AdPlacement.CampExpress, ok =>
                {
                    if (!ok || !Camp.Express(out int gold) || !fxLayer) return;
                    MetaFX.Burst(fxLayer, at, goldChip.position, UIKit.Gold, Mathf.Clamp(4 + gold / 25, 5, 16));
                    MetaFX.FloatText(fxLayer, at, $"Livraison : +{gold} or", UIKit.Gold, 56);
                    MetaFX.Punch(goldChip, 0.55f);
                    liveUpdate?.Invoke();
                });
            }, 34);

            return () =>
            {
                bool adReady = Ads.IsRewardedReady(AdPlacement.CampBoost);
                string test = Ads.IsTest ? " (test)" : "";
                float left = Camp.BoostSecondsLeft;
                if (left <= 0f && Camp.Fill >= 1f)
                    MonetizationUI.SetLabel(boost, $"PRODUCTION x2\n<size=28>réserve pleine : récolte d'abord</size>");
                else if (left <= 0f)
                    MonetizationUI.SetLabel(boost, $"PRODUCTION x2\n<size=28>pendant {Camp.BoostHours:0} h{test}</size>");
                else if (Camp.CanBoost)
                    MonetizationUI.SetLabel(boost, $"x2 ACTIF : {MonetizationUI.Duration(left)}\n<size=28>Prolonger +{Camp.BoostHours:0} h{test}</size>");
                else
                    MonetizationUI.SetLabel(boost, $"x2 ACTIF : {MonetizationUI.Duration(left)}\n<size=28>max {Camp.BoostMaxHours:0} h d'avance</size>");
                MonetizationUI.SetEnabled(boost, adReady && Camp.CanBoost, MonetizationUI.AdButton);

                int n = Camp.ExpressLeftToday;
                MonetizationUI.SetLabel(express, n > 0
                    ? $"LIVRAISON EXPRESS\n<size=28>+{Camp.ExpressGold} or  ·  {n}/{Camp.ExpressPerDay} aujourd'hui</size>"
                    : "LIVRAISON EXPRESS\n<size=28>revient demain</size>");
                MonetizationUI.SetEnabled(express, adReady && n > 0, MonetizationUI.AdButton);
            };
        }

        void UpgradeBuilding(BuildingDef b, RectTransform source)
        {
            Vector3 at = source.position;
            if (!Camp.Upgrade(b)) return;
            Refresh();
            MetaFX.FloatText(fxLayer, at, $"{b.name} niveau {Camp.Level(b)} !", UIKit.Gold, 52);
        }
    }
}
