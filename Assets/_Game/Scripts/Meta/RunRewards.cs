using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>What a finished run gave, for the result screen.</summary>
    public class RunReport
    {
        public bool campaign;
        public bool won;
        public int stage = -1;
        /// <summary>Stars met by this run, and the ones earned for the first time.</summary>
        public int starMask, newStars;
        public bool firstClear;
        /// <summary>Gold banked by GameManager (pickups + survival bonus).</summary>
        public int runGold;
        /// <summary>Extra gold: new stars and first clear.</summary>
        public int bonusGold;
        public readonly List<string> missionsDone = new();
        public int claimable;
    }

    /// <summary>
    /// Turns the stats of a finished run into long-term progress:
    /// lifetime stats, missions and campaign stars/first-clear gold. Never anything that makes a run stronger.
    /// Called once by GameManager.EndRun(), after the run gold is banked.
    /// </summary>
    public static class RunRewards
    {
        public static RunReport Process(GameManager gm, bool won)
        {
            var d = MetaSave.Data;
            var r = new RunReport
            {
                campaign = RunConfig.IsCampaign && RunConfig.Stage < Campaign.Count,
                won = won,
                stage = RunConfig.Stage,
                runGold = gm.GoldEarned,
            };

            int seconds = Mathf.FloorToInt(gm.RunTime);
            int level = PlayerXP.Instance ? PlayerXP.Instance.Level : 1;
            int evolutions = 0;
            if (UpgradeManager.Instance)
                foreach (var w in UpgradeManager.Instance.Weapons)
                    if (w.Evolved) evolutions++;

            // Lifetime stats
            var life = d.life;
            life.runs++;
            life.kills += gm.Kills;
            life.bosses += gm.BossKills;
            life.elites += gm.EliteKills;
            life.evolutions += evolutions;
            life.bestCombo = Mathf.Max(life.bestCombo, Combat.BestCombo);
            life.bestLevel = Mathf.Max(life.bestLevel, level);
            life.goldEarned += gm.GoldEarned;
            if (!r.campaign) life.bestSeconds = Mathf.Max(life.bestSeconds, seconds);

            // Campaign stars first, so "win a stage" missions and star achievements see them.
            if (r.campaign && won) ProcessCampaign(gm, r);

            // Daily missions: note which ones this run completes.
            var daily = Missions.Daily;
            var wasDone = new bool[daily.Count];
            for (int i = 0; i < daily.Count; i++) wasDone[i] = daily[i].Done;

            Missions.Report(MissionType.Runs, 1);
            Missions.Report(MissionType.Kills, gm.Kills);
            Missions.Report(MissionType.Elites, gm.EliteKills);
            Missions.Report(MissionType.Bosses, gm.BossKills);
            Missions.Report(MissionType.Evolve, evolutions);
            Missions.Report(MissionType.GoldPicked, gm.Gold);
            Missions.Report(MissionType.Survive, seconds);
            Missions.Report(MissionType.Combo, Combat.BestCombo);
            Missions.Report(MissionType.Level, level);
            if (r.campaign && won) Missions.Report(MissionType.StageWin, 1);
            if (RunConfig.Mode == GameMode.Daily) Missions.Report(MissionType.DailyChallenge, 1);
            if (RunConfig.Ranked) Missions.Report(MissionType.Ranked, 1);

            for (int i = 0; i < daily.Count; i++)
                if (!wasDone[i] && daily[i].Done) r.missionsDone.Add(Missions.Describe(daily[i]));

            MetaSave.Save();
            Progression.AddGold(r.bonusGold);

            r.claimable = Missions.ClaimableCount;
            return r;
        }

        static void ProcessCampaign(GameManager gm, RunReport r)
        {
            var d = MetaSave.Data;
            var stage = Campaign.Stages[r.stage];
            d.life.stageWins++;

            int old = Campaign.StarMask(r.stage);
            r.starMask = Campaign.EvaluateRun(r.stage, true, gm.LowestHealth, gm.BossKills, gm.EliteKills);
            r.newStars = r.starMask & ~old;
            Campaign.SetStarMask(r.stage, old | r.starMask);
            r.bonusGold += Campaign.CountStars(r.newStars) * Campaign.StarGold(r.stage);

            if ((r.newStars & Campaign.StarCleared) != 0)
            {
                r.firstClear = true;
                r.bonusGold += stage.gold;
            }

            if (r.stage + 1 < Campaign.Count) d.selectedStage = r.stage + 1;
        }
    }
}
