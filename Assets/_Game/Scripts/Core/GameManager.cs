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

        public GameState State { get; private set; } = GameState.Menu;
        public float RunTime { get; private set; }
        public int Kills { get; private set; }
        public int BossKills { get; private set; }
        /// <summary>Gold picked up during this run (banked at the end).</summary>
        public int Gold { get; private set; }
        /// <summary>Gold banked at the end of the run, survival bonus included.</summary>
        public int GoldEarned { get; private set; }
        public int Score => killScore + Mathf.FloorToInt(RunTime) * 2;
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

            // Campaign stage: surviving the whole duration wins.
            if (RunConfig.Duration > 0f && RunTime >= RunConfig.Duration)
            {
                Won = true;
                EndRun();
                return;
            }

            if (pendingLevelUps > 0) OpenLevelUp();
        }

        public int EliteKills { get; private set; }

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

        public void Abandon()
        {
            if (State == GameState.Paused) EndRun();
        }

        /// <summary>
        /// Campaign only (never ranked), about once every 2-3 games: "Seconde chance" rewarded-ad offer first.
        /// Accepted = back to Playing (ReviveOffer heals and pushes the horde); declined or timed out = EndRun.
        /// </summary>
        public void PlayerDied()
        {
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
