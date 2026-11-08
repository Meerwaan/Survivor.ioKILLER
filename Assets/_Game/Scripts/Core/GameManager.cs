using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survivor
{
    /// <summary>Revive: frozen on the "Seconde chance" offer after a campaign death (Monetization/ReviveOffer.cs).</summary>
    public enum GameState { Menu, Playing, Paused, LevelUp, GameOver, Revive }

    /// <summary>
    /// Runs the flow: menu, run (endless or campaign stage), pauses for level-ups,
    /// victory/game over, meta rewards and score submission.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Set by "Rejouer" so the reloaded scene skips the menu.</summary>
        static bool startRunOnLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            startRunOnLoad = false;
            RunConfig.SetEndless();
        }

        public event Action<GameState> StateChanged;

        [Header("Level-up nova (push wave after the last queued level-up)")]
        [Tooltip("Radius in meters (times the area bonus). Was 5: with fewer level-ups it must stay a breather, not a screen wipe.")]
        public float levelUpNovaRadius = 4.5f;
        [Tooltip("Damage (times the damage bonus). Was 15: one-shot every zombie for the first 3 minutes.")]
        public float levelUpNovaDamage = 10f;

        [Header("Level burst (level-ups once the stuff is full: no cards, instant reward)")]
        [Tooltip("Radius in meters (times the area bonus).")]
        public float levelBurstRadius = 6.5f;
        [Tooltip("Damage x the enemy health scaling of the moment x the damage bonus: it always clears normal enemies around you.")]
        public float levelBurstDamage = 14f;
        [Tooltip("Share of max HP healed per level (at most 30% for a batch).")]
        public float levelBurstHeal = 0.1f;
        [Tooltip("Score bonus per level = this x the player's level.")]
        public int levelBurstScorePerLevel = 40;

        public GameState State { get; private set; } = GameState.Menu;
        public float RunTime { get; private set; }
        public int Kills { get; private set; }
        public int BossKills { get; private set; }
        /// <summary>Gold picked up during this run (banked at the end).</summary>
        public int Gold { get; private set; }
        /// <summary>Gold banked at the end of the run, survival bonus included.</summary>
        public int GoldEarned { get; private set; }
        /// <summary>Kill points + 2 per second + level bursts (levels gained once the stuff is full).</summary>
        public int Score => killScore + Mathf.FloorToInt(RunTime) * 2 + LevelBurstScore;
        /// <summary>Score from level bursts this run.</summary>
        public int LevelBurstScore { get; private set; }
        /// <summary>Level-ups turned into bursts this run (stuff full).</summary>
        public int LevelBursts { get; private set; }
        /// <summary>True when a campaign stage was survived to the end.</summary>
        public bool Won { get; private set; }
        /// <summary>Lowest health fraction reached this run (campaign "health" star).</summary>
        public float LowestHealth { get; private set; } = 1f;
        /// <summary>Meta rewards of the run that just ended (null before that).</summary>
        public RunReport LastReport { get; private set; }

        /// <summary>Record of the classic board (historic "best_score" key). Other boards: <see cref="BestScoreFor"/>.</summary>
        public static int BestScore => Leaderboard.PersonalBest(Leaderboard.ClassicBoard);
        /// <summary>Personal best on a board ("classic", "daily_20261106", "weekly_202645", "menaces").</summary>
        public static int BestScoreFor(string boardId) => Leaderboard.PersonalBest(boardId);
        /// <summary>Score with the threat multiplier: what goes to the board.</summary>
        public int FinalScore => Mathf.RoundToInt(Score * Mathf.Max(1f, RunConfig.ScoreMult));
        /// <summary>Score side of the run that just ended (null in the campaign / before the end).</summary>
        public RankedResult LastResult { get; private set; }

        int killScore;
        int pendingLevelUps;
        /// <summary>Displayed score every RunProgress.Step seconds (the record "ghost" of this run if it becomes the best).</summary>
        readonly System.Collections.Generic.List<int> paceSamples = new(64);
        float nextPaceSample;
        Coroutine hitStop;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            // Meta UI built in code (no scene setup needed).
            if (!GetComponent<RunResultScreen>()) gameObject.AddComponent<RunResultScreen>();
            if (!GetComponent<ModeHUD>()) gameObject.AddComponent<ModeHUD>();
        }

        void Start()
        {
            if (startRunOnLoad)
            {
                startRunOnLoad = false;
                StartRun();
            }
            else
            {
                SetState(GameState.Menu);
            }
        }

        void SetState(GameState state)
        {
            if (hitStop != null)
            {
                StopCoroutine(hitStop);
                hitStop = null;
            }
            State = state;
            Time.timeScale = state == GameState.Playing ? 1f : 0f;
            StateChanged?.Invoke(state);
        }

        public void StartRun()
        {
            if (State != GameState.Menu || Ads.Busy) return;
            // At most one forced ad a day, announced before the day's first run (Monetization/Ads.cs rules).
            Ads.BeforeRun(() =>
            {
                if (State != GameState.Menu) return;
                GameModes.OnRunStarted(); // the daily ranked attempt is used now (quitting doesn't refund it)
                UpgradeManager.Instance.BeginRun();
                SetState(GameState.Playing);
            });
        }

        /// <summary>Freezes the action for a split second: makes big kills feel heavy.</summary>
        public void HitStop(float seconds)
        {
            if (State != GameState.Playing || hitStop != null) return;
            hitStop = StartCoroutine(HitStopRoutine(seconds));
        }

        IEnumerator HitStopRoutine(float seconds)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (State == GameState.Playing) Time.timeScale = 1f;
            hitStop = null;
        }

        public void AddGold(int amount) => Gold += Mathf.Max(0, amount);

        void Update()
        {
            if (State != GameState.Playing) return;

            RunTime += Time.deltaTime;
            if (PlayerHealth.Instance) LowestHealth = Mathf.Min(LowestHealth, PlayerHealth.Instance.Fraction);
            while (RunTime >= nextPaceSample)
            {
                paceSamples.Add(FinalScore);
                nextPaceSample += RunProgress.Step;
            }

            // Campaign stage: surviving the whole duration wins.
            if (RunConfig.Duration > 0f && RunTime >= RunConfig.Duration)
            {
                Won = true;
                EndRun();
                return;
            }

            if (pendingLevelUps > 0)
            {
                // Full stuff and nothing to evolve: no card screen, the level pays out right away.
                if (UpgradeManager.Instance.HasChoices) OpenLevelUp();
                else LevelBurst();
            }
        }

        /// <summary>
        /// Every queued level-up at once, once the stuff is full: a shockwave that clears normal enemies around the player,
        /// a small heal and a score bonus. Keeps level-ups exciting without making the build stronger.
        /// </summary>
        void LevelBurst()
        {
            int count = pendingLevelUps;
            pendingLevelUps = 0;
            int level = PlayerXP.Instance ? PlayerXP.Instance.Level : 1;
            int bonus = levelBurstScorePerLevel * level * count;
            LevelBurstScore += bonus;
            LevelBursts += count;

            var pc = PlayerController.Instance;
            if (pc)
            {
                var s = pc.Stats;
                Vector3 p = pc.Position;
                float radius = levelBurstRadius * s.areaMult;
                Combat.Nova(p, radius, levelBurstDamage * RunModifiers.WorldScale * s.damageMult);
                if (GameFX.Instance)
                {
                    GameFX.Instance.Ring(p, radius * 1.15f, UpgradeManager.EvolutionColor * 2.5f, 0.55f);
                    GameFX.Instance.Flash(p + Vector3.up * 0.5f, radius * 0.45f, UIKit.Gold * 2f, 0.15f);
                }
            }
            if (PlayerHealth.Instance) PlayerHealth.Instance.Heal(PlayerHealth.Instance.Max * Mathf.Min(0.3f, levelBurstHeal * count));
            SFX.Play(SfxId.LevelUp);
            if (GameHUD.Instance) GameHUD.Instance.ShowLevelBurst(level, bonus);
        }

        public int EliteKills { get; private set; }

        /// <summary>A save from 0 HP (Plume de phénix): counts as having dropped to 0 for the campaign health star.</summary>
        public void RecordNearDeath() => LowestHealth = 0f;

        public void RegisterKill(int score, bool boss, bool elite = false)
        {
            Kills++;
            killScore += score;
            if (boss) BossKills++;
            if (elite) EliteKills++;
        }

        public void QueueLevelUp(int count = 1) => pendingLevelUps += count;

        void OpenLevelUp()
        {
            SetState(GameState.LevelUp);
            GameHUD.Instance.ShowLevelUp(UpgradeManager.Instance.RollOptions(3));
        }

        public void ChooseUpgrade(UpgradeOption option)
        {
            if (State != GameState.LevelUp) return;

            option.Apply();
            pendingLevelUps--;
            SetState(GameState.Playing);
            if (pendingLevelUps <= 0 && PlayerController.Instance)
            {
                var stats = PlayerController.Instance.Stats;
                Combat.Nova(PlayerController.Instance.Position, levelUpNovaRadius * stats.areaMult, levelUpNovaDamage * stats.damageMult);
            }
            // If more level-ups are queued, Update() opens the next one on the next frame.
        }

        public void Pause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        /// <summary>The run was ended from the pause menu (result screen title).</summary>
        public bool Abandoned { get; private set; }

        public void Abandon()
        {
            if (State != GameState.Paused) return;
            Abandoned = true;
            EndRun();
        }

        /// <summary>
        /// Campaign only (never ranked), about once every 2-3 games: "Seconde chance" rewarded-ad offer first.
        /// Accepted = back to Playing (ReviveOffer heals and pushes the horde); declined or timed out = EndRun.
        /// </summary>
        public void PlayerDied()
        {
            LowestHealth = 0f; // the lethal hit lands outside Update: a revive must not keep the "health" star

            if (State == GameState.Playing && ReviveOffer.TryOffer(ResumeAfterRevive, EndRun))
            {
                SetState(GameState.Revive);
                return;
            }
            EndRun();
        }

        /// <summary>The revive ad was watched: the run goes on.</summary>
        public void ResumeAfterRevive()
        {
            if (State == GameState.Revive) SetState(GameState.Playing);
        }

        void EndRun()
        {
            if (State == GameState.GameOver || State == GameState.Menu) return;

            bool campaign = RunConfig.IsCampaign;
            // Records and leaderboards are for the ranked modes only (stages are short and have their own stars).
            // Score submitted = round(base score x threat multiplier), on the mode's board; practice runs save nothing.
            string board = RunConfig.BoardId;
            bool submit = !campaign && RunConfig.Ranked && RunConfig.SubmitScore && !string.IsNullOrEmpty(board);
            int score = FinalScore;
            int previousBest = string.IsNullOrEmpty(board) ? 0 : BestScoreFor(board);
            bool newBest = submit && Leaderboard.TrySetPersonalBest(board, score);
            int danger = RunDirector.Instance ? RunDirector.Instance.Danger : 1;
            // Personal progress (record ghost, best danger, best time, last scores): submitted ranked runs only.
            var progressBefore = submit ? RunProgress.Finish(board, paceSamples, score, danger, RunTime, newBest) : RunProgress.Find(board);
            LastResult = campaign ? null : new RankedResult
            {
                mode = RunConfig.Mode,
                boardId = board ?? "",
                label = GameModes.RunLabel,
                baseScore = Score,
                mult = Mathf.Max(1f, RunConfig.ScoreMult),
                finalScore = score,
                submitted = submit,
                previousBest = previousBest,
                newBest = newBest,
                rank = submit ? 0 : -1,
                medal = Medals.For(board, score),
                previousMedal = Medals.For(board, previousBest),
                danger = danger,
                previousBestDanger = progressBefore != null ? progressBefore.bestDanger : 0,
                seconds = RunTime,
                previousBestSeconds = progressBefore != null ? progressBefore.bestSeconds : 0f,
            };

            // Bank the gold: what was picked up plus a survival bonus.
            var playerStats = PlayerController.Instance ? PlayerController.Instance.Stats : null;
            float goldMult = playerStats ? playerStats.goldMult : 1f;
            int bonus = Mathf.FloorToInt(RunTime / 60f * 10f) + BossKills * 40;
            GoldEarned = Mathf.RoundToInt((Gold + bonus) * goldMult);
            Progression.AddGold(GoldEarned);

            // Long-term progress: lifetime stats, missions, campaign stars (gold only, no run power).
            try
            {
                LastReport = RunRewards.Process(this, Won);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                LastReport = null;
            }

            SetState(GameState.GameOver);
            GameHUD.Instance.ShowGameOver(newBest);
            if ((LastReport != null || LastResult != null) && RunResultScreen.Instance) RunResultScreen.Instance.Show(LastReport);

            if (!submit) return;

            var entry = new LeaderboardEntry
            {
                name = PlayerProfile.Name,
                score = score,
                seconds = Mathf.FloorToInt(RunTime),
                kills = Kills,
                level = PlayerXP.Instance ? PlayerXP.Instance.Level : 1,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                mult = Mathf.Max(1f, RunConfig.ScoreMult),
                title = PlayerProfile.Title,
                skin = PlayerProfile.Skin,
            };
            var result = LastResult;
            Leaderboard.Service.Submit(board, entry, rank =>
            {
                if (GameHUD.Instance) GameHUD.Instance.ShowRank(rank);
                result?.SetRank(rank);
            });
        }

        /// <summary>Plays the same mode again (campaign: same stage; daily: becomes practice once the ranked attempt is used).</summary>
        public void Replay()
        {
            if (RunConfig.Mode != GameMode.Campaign) GameModes.Configure(RunConfig.Mode);
            startRunOnLoad = true;
            Reload();
        }

        public void BackToMenu()
        {
            startRunOnLoad = false;
            Reload();
        }

        static void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // On a phone, leaving the app (call, notification...) pauses the run.
        void OnApplicationPause(bool paused)
        {
            if (paused && !Application.isEditor) Pause();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !Application.isEditor) Pause();
        }
    }
}
