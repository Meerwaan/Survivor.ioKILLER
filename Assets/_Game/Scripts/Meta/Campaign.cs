using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>One campaign stage: survive <see cref="duration"/> seconds against tougher enemies.</summary>
    public class StageDef
    {
        public string name;
        public int chapter;
        public float duration;
        public float health, damage, spawn;
        /// <summary>First-clear reward (gold only: nothing in the meta makes a run stronger).</summary>
        public int gold;
    }

    /// <summary>
    /// The campaign: 4 chapters of 3 stages, 3 stars each.
    /// Star 1 = survive, star 2 = never drop under 30% HP, star 3 = beat the bosses (or elites).
    /// Stars are kept separately, so they can be earned over several attempts.
    /// </summary>
    public static class Campaign
    {
        public const int StarCleared = 1, StarHealth = 2, StarBoss = 4;
        public const float HealthStarThreshold = 0.3f;
        /// <summary>A boss counts for the boss star only if it had at least this long to be killed.</summary>
        const float BossGrace = 75f;

        /// <summary>Chapter names (Docs/Univers.md 2.2 and 8).</summary>
        public static readonly string[] Chapters = { "Ostrov-Sud", "Ostrov-Centre", "Le Combinat", "Hôpital Central" };

        public static readonly Color[] ChapterColors =
        {
            new(0.45f, 0.7f, 0.35f),
            new(0.4f, 0.55f, 0.85f),
            new(0.85f, 0.55f, 0.2f),
            new(0.75f, 0.25f, 0.3f),
        };

        static StageDef S(int chapter, string name, float minutes, float hp, float dmg, float spawn, int gold) =>
            new()
            {
                chapter = chapter, name = name, duration = minutes * 60f, health = hp, damage = dmg, spawn = spawn,
                gold = gold,
            };

        // First-clear gold: ~150 to 600, a bit more on the last stage of each chapter.
        // Health multipliers of chapters 2-4 lowered (3.4 -> 2.7 max) with the slower XP curve: a 10 min stage now ends
        // around level 30 instead of 50 (Docs/Pacing.md).
        public static readonly List<StageDef> Stages = new()
        {
            S(0, "Le lotissement", 5f, 1.0f, 1.0f, 0.9f, 150),
            S(0, "La Galeria", 5.5f, 1.1f, 1.05f, 1.0f, 175),
            S(0, "Le parking souterrain", 6f, 1.2f, 1.1f, 1.05f, 250),
            S(1, "La gare", 6f, 1.3f, 1.15f, 1.1f, 275),
            S(1, "Le métro", 6.5f, 1.4f, 1.2f, 1.15f, 300),
            S(1, "La place de la mairie", 7f, 1.55f, 1.3f, 1.2f, 375),
            S(2, "L'entrepôt", 7f, 1.7f, 1.4f, 1.25f, 400),
            S(2, "La raffinerie", 7.5f, 1.85f, 1.5f, 1.3f, 425),
            S(2, "La centrale", 8f, 2.0f, 1.6f, 1.35f, 500),
            S(3, "Les urgences", 8f, 2.2f, 1.7f, 1.4f, 525),
            S(3, "Le bloc opératoire", 9f, 2.4f, 1.85f, 1.45f, 550),
            S(3, "Le laboratoire", 10f, 2.7f, 2.0f, 1.5f, 600),
        };

        public static int Count => Stages.Count;
        public static int MaxStars => Count * 3;

        public static string Code(int stage) => $"{Stages[stage].chapter + 1}-{stage % 3 + 1}";
        public static string Title(int stage) => $"{Code(stage)}  {Stages[stage].name}";
        public static string ChapterTitle(int chapter) => $"Chapitre {chapter + 1} : {Chapters[chapter]}";

        // ---------- Saved progress ----------

        public static int StarMask(int stage)
        {
            var list = MetaSave.Data.stageStars;
            return stage >= 0 && stage < list.Count ? list[stage] : 0;
        }

        public static void SetStarMask(int stage, int mask)
        {
            var list = MetaSave.Data.stageStars;
            while (list.Count <= stage) list.Add(0);
            list[stage] = mask & 7;
        }

        public static int CountStars(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
        public static int Stars(int stage) => CountStars(StarMask(stage));
        public static bool IsCleared(int stage) => (StarMask(stage) & StarCleared) != 0;
        public static bool IsUnlocked(int stage) => stage == 0 || (stage > 0 && stage < Count && IsCleared(stage - 1));

        public static int ClearedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (IsCleared(i)) n++;
                return n;
            }
        }

        public static int TotalStars
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) n += Stars(i);
                return n;
            }
        }

        /// <summary>The first stage not cleared yet (or the last one when everything is done).</summary>
        public static int NextStage
        {
            get
            {
                for (int i = 0; i < Count; i++) if (!IsCleared(i)) return i;
                return Count - 1;
            }
        }

        public static bool AllCleared => ClearedCount >= Count;

        // ---------- Star rules ----------

        static void BossTimes(out float first, out float interval)
        {
            first = 210f;
            interval = 210f;
            var d = RunDirector.Instance;
            if (d)
            {
                first = d.firstBossTime;
                interval = Mathf.Max(30f, d.bossInterval);
            }
        }

        /// <summary>How many bosses the boss star asks for (only bosses with enough time to be killed).</summary>
        public static int ExpectedBosses(StageDef s)
        {
            BossTimes(out float first, out float interval);
            int n = 0;
            for (float t = first; t + BossGrace <= s.duration && n < 20; t += interval) n++;
            return n;
        }

        /// <summary>Fallback for the third star when no boss shows up in time.</summary>
        public static int ExpectedElites(StageDef s)
        {
            float first = 40f, interval = 35f;
            var d = RunDirector.Instance;
            if (d)
            {
                first = d.firstEliteTime;
                interval = Mathf.Max(5f, d.eliteInterval);
            }
            int spawned = Mathf.Max(0, Mathf.FloorToInt((s.duration - first) / interval) + 1);
            return Mathf.Max(1, Mathf.FloorToInt(spawned * 0.6f));
        }

        public static string StarText(int stage, int star)
        {
            var s = Stages[stage];
            switch (star)
            {
                case 0: return $"Survis {UIKit.FormatTime(Mathf.RoundToInt(s.duration))}";
                case 1: return $"Ne descends jamais sous {Mathf.RoundToInt(HealthStarThreshold * 100f)}% de PV";
                default:
                    int bosses = ExpectedBosses(s);
                    return bosses > 0 ? (bosses > 1 ? $"Abats {bosses} Souches" : "Abats la Souche") : $"Tue {ExpectedElites(s)} Alphas";
            }
        }

        /// <summary>Stars earned by a finished run (all need the victory).</summary>
        public static int EvaluateRun(int stage, bool won, float lowestHealth, int bossKills, int eliteKills)
        {
            if (!won || stage < 0 || stage >= Count) return 0;
            var s = Stages[stage];
            int mask = StarCleared;
            if (lowestHealth >= HealthStarThreshold) mask |= StarHealth;
            int bosses = ExpectedBosses(s);
            bool third = bosses > 0 ? bossKills >= bosses : eliteKills >= ExpectedElites(s);
            if (third) mask |= StarBoss;
            return mask;
        }

        /// <summary>Gold given for each star earned for the first time.</summary>
        public static int StarGold(int stage) => 25 + 15 * stage;

        // ---------- Run setup ----------

        /// <summary>Writes RunConfig for a stage. Call before GameManager.StartRun()/Replay().</summary>
        public static void Configure(int stage)
        {
            stage = Mathf.Clamp(stage, 0, Count - 1);
            var s = Stages[stage];
            RunConfig.SetEndless();
            RunConfig.Mode = GameMode.Campaign;
            RunConfig.BoardId = "";
            RunConfig.SubmitScore = false;
            RunConfig.Stage = stage;
            RunConfig.EnemyHealthMult = s.health;
            RunConfig.EnemyDamageMult = s.damage;
            RunConfig.SpawnRateMult = s.spawn;
            RunConfig.Duration = s.duration;
            RunConfig.Label = Title(stage);
            MetaSave.Data.selectedStage = stage;
        }

        public static string RewardText(StageDef s) => $"{s.gold} or";
    }
}
