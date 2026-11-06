using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>"Missions" tab: daily missions, daily chest and achievements.</summary>
    public partial class MenuScreen
    {
        static readonly Color ClaimedCard = new(0.07f, 0.07f, 0.08f, 0.9f);

        void BuildMissions()
        {
            var timer = Header("MISSIONS", "");
            liveUpdate = () =>
            {
                var left = Missions.TimeToReset;
                timer.text = $"Nouvelles missions dans {left.Hours} h {left.Minutes:00}";
            };
            liveUpdate();

            var content = MetaUI.Scroll(page, new Vector2(0, -80), new Vector2(1000, 1420), out pageScroll);
            float y = 0f;

            SectionTitle(content, $"DU JOUR  <size=28><color=#BBBBBB>({Missions.Daily.Count} missions, plus avec la Radio du camp)</color></size>", y);
            y += 72f;
            foreach (var m in Missions.Daily)
            {
                MissionRow(content, m, y);
                y += 180f;
            }
            ChestRow(content, y);
            y += 196f;

            SectionTitle(content, "SUCCÈS", y);
            y += 72f;
            foreach (var a in Missions.Achievements)
            {
                AchievementRow(content, a, y);
                y += 180f;
            }
            MetaUI.SetHeight(content, y + 20f);
        }

        void MissionRow(RectTransform content, MissionState m, float y)
        {
            var row = MetaUI.Card(content, "Mission", UIKit.Top, new Vector2(0, -y), new Vector2(960, 165), m.claimed ? ClaimedCard : UIKit.Card);
            MetaUI.Text(row, Missions.Describe(m), 36, m.claimed ? UIKit.Muted : Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(28, -16), new Vector2(660, 50), FontStyle.Bold);
            float progress = m.target > 0 ? (float)m.progress / m.target : 1f;
            MetaUI.Bar(row, UIKit.TopLeft, new Vector2(28, -76), new Vector2(450, 30), m.Done ? MetaUI.LightGreen : UIKit.Gold, progress);
            MetaUI.Text(row, Missions.ProgressText(m), 28, UIKit.Muted, TextAnchor.MiddleLeft, UIKit.TopLeft, new Vector2(494, -68), new Vector2(200, 46));
            MetaUI.Text(row, Missions.RewardText(m.gold), 28, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(28, -118), new Vector2(660, 40));

            ClaimButton(row, m.claimed ? "FAIT" : m.Done ? "RÉCUPÉRER" : "EN COURS", m.Done && !m.claimed, source =>
            {
                if (!Missions.Claim(m)) return;
                RewardFX(source, m.gold);
                Refresh();
            });
        }

        void ChestRow(RectTransform content, float y)
        {
            bool claimed = MetaSave.Data.dailyChestClaimed;
            bool ready = Missions.ChestReady;
            var row = MetaUI.Card(content, "Chest", UIKit.Top, new Vector2(0, -y), new Vector2(960, 180), ready ? new Color(0.22f, 0.16f, 0.05f, 0.95f) : claimed ? ClaimedCard : UIKit.Card);
            var icon = UIKit.Icon(row, "Coffre", UIKit.Gold, false, 110, "");
            UIKit.Place(icon, UIKit.Left, new Vector2(20, 0), new Vector2(110, 110));
            MetaUI.Text(row, "COFFRE DU JOUR", 38, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(150, -22), new Vector2(520, 50), FontStyle.Bold);
            MetaUI.Text(row, $"Termine toutes les missions du jour :\n{Missions.ChestGold} or", 28, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(150, -78), new Vector2(540, 90));

            ClaimButton(row, claimed ? "OUVERT" : "OUVRIR", ready, source =>
            {
                int gold = Missions.ChestGold;
                if (!Missions.ClaimChest()) return;
                RewardFX(source, gold);
                Refresh();
            });
        }

        void AchievementRow(RectTransform content, AchievementDef a, float y)
        {
            int tier = Missions.ClaimedTiers(a);
            bool complete = Missions.IsComplete(a);
            bool canClaim = Missions.CanClaim(a);
            long target = Missions.CurrentTarget(a);
            long progress = a.progress();

            var row = MetaUI.Card(content, "Achievement " + a.id, UIKit.Top, new Vector2(0, -y), new Vector2(960, 165), complete ? ClaimedCard : UIKit.Card);
            MetaUI.Text(row, $"{a.name}  <size=28><color=#BBBBBB>{tier}/{a.tiers.Length}</color></size>", 36, complete ? UIKit.Muted : Color.white,
                TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(28, -14), new Vector2(660, 50), FontStyle.Bold);
            for (int i = 0; i < a.tiers.Length; i++)
                MetaUI.StarIcon(row, UIKit.TopLeft, new Vector2(500 + i * 44, -18), 38, i < tier);

            MetaUI.Text(row, a.describe(target), 28, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(28, -62), new Vector2(660, 40));
            MetaUI.Bar(row, UIKit.TopLeft, new Vector2(28, -106), new Vector2(380, 26), canClaim || complete ? MetaUI.LightGreen : UIKit.Gold, target > 0 ? (float)progress / target : 1f);
            string shown = a.id == "survive"
                ? $"{UIKit.FormatTime((int)System.Math.Min(progress, target))} / {UIKit.FormatTime((int)target)}"
                : $"{System.Math.Min(progress, target)} / {target}";
            MetaUI.Text(row, shown, 26, UIKit.Muted, TextAnchor.MiddleLeft, UIKit.TopLeft, new Vector2(420, -98), new Vector2(270, 42));

            if (!complete)
            {
                int gold = Missions.TierGold(tier);
                MetaUI.Text(row, Missions.RewardText(gold), 24, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(28, -136), new Vector2(680, 30));
                ClaimButton(row, canClaim ? "RÉCUPÉRER" : "EN COURS", canClaim, source =>
                {
                    if (!Missions.ClaimAchievement(a)) return;
                    RewardFX(source, gold);
                    Refresh();
                });
            }
            else
            {
                ClaimButton(row, "TERMINÉ", false, null);
            }
        }

        void ClaimButton(RectTransform row, string label, bool enabled, System.Action<RectTransform> onClaim)
        {
            Button button = null;
            button = MetaUI.Button(row, label, MetaUI.Green, UIKit.Right, new Vector2(-18, 0), new Vector2(240, 112), () =>
            {
                if (onClaim != null) onClaim((RectTransform)button.transform);
            }, 34, enabled);
            if (enabled) UIAnim.Pulse((RectTransform)button.transform, 4f, 0.04f);
        }

        /// <summary>Coins flying from a claim button to the top bar.</summary>
        void RewardFX(RectTransform source, int gold)
        {
            Vector3 at = source.position;
            if (gold > 0) MetaFX.Burst(fxLayer, at, goldChip.position, UIKit.Gold, Mathf.Clamp(4 + gold / 30, 5, 14));
            MetaFX.FloatText(fxLayer, at, $"+{gold} or", UIKit.Gold, 52);
            MetaFX.Punch(goldChip, 0.55f);
        }
    }
}
