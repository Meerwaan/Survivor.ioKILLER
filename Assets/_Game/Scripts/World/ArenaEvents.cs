using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Little surprises on top of the enemy waves:
    /// - explosive barrels scattered ahead of the player (shoot them in a crowd: chain explosions),
    /// - supply crates falling from the sky (break them for meat, magnet or gold),
    /// - FRENZY: a big combo doubles the fire rate for a few seconds,
    /// - timed challenges ("kill 50 in 20 s") that pay out a chest.
    /// Everything is announced with a banner and a sound.
    /// Also boots the run spice: RunModifiers (relics, pacts), ComfortMeter, WorldEvents (spice + pressure events), RunHUD.
    /// </summary>
    public class ArenaEvents : MonoBehaviour
    {
        public static ArenaEvents Instance { get; private set; }

        [Header("Explosive barrels")]
        public EnemyData barrel;
        public float barrelInterval = 7f;
        public int maxBarrels = 7;
        public int startBarrels = 3;

        [Header("Supply drops")]
        public EnemyData crate;
        [Tooltip("Crates fall at the start of a lull (RunDirector waves), at most this often.")]
        public float firstSupplyTime = 30f;
        public float supplyEvery = 70f;
        public float dropHeight = 14f;

        [Header("Frenzy")]
        [Tooltip("Kill streak needed for the first frenzy of the run. A wave is ~350 kills at 2 min, ~700 at 5, ~1400 at 10.")]
        public int frenzyFirstCombo = 400;
        [Tooltip("Each frenzy of the run raises the next threshold by this much (the streak itself breaks every lull).")]
        public int frenzyComboGrowth = 600;
        [Tooltip("Multiplier on the thresholds (relic Sablier fêlé lowers it).")]
        public float frenzyComboScale = 1f;
        public float frenzyDuration = 8f;
        [Tooltip("Weapon cooldowns are multiplied by this during the frenzy (0.5 = twice the fire rate).")]
        public float frenzyCooldownMult = 0.5f;
        public float frenzySpeedMult = 1.2f;

        [Header("Timed challenge")]
        public float firstChallengeTime = 85f;
        public float challengeInterval = 110f;
        public float challengeDuration = 20f;
        [Tooltip("Target = this x the kills the stream brings in the challenge time (from RunDirector's rate).")]
        public float challengeStreamFactor = 1.1f;
        public int challengeMinKills = 30;

        public static readonly Color FrenzyColor = new(1f, 0.35f, 0.1f);

        public bool FrenzyActive => frenzyLeft > 0f;
        public float FrenzyLeft => frenzyLeft;
        public float FrenzyFraction => frenzyDuration > 0f ? Mathf.Clamp01(frenzyLeft / frenzyDuration) : 0f;

        public bool ChallengeActive { get; private set; }
        public int ChallengeKills { get; private set; }
        public int ChallengeTarget { get; private set; }
        public float ChallengeTimeLeft { get; private set; }

        float nextBarrel, nextSupply, nextChallenge;
        bool started;

        float frenzyLeft;
        bool frenzyApplied;
        int frenzyCount;
        int challengeStartKills;

        void Awake()
        {
            Instance = this;
            // Run spice lives next to the arena events: relics/pacts, the bigger random events and their HUD.
            if (!GetComponent<RunModifiers>()) gameObject.AddComponent<RunModifiers>();
            if (!GetComponent<ComfortMeter>()) gameObject.AddComponent<ComfortMeter>();
            if (!GetComponent<WorldEvents>()) gameObject.AddComponent<WorldEvents>();
            if (!GetComponent<RunHUD>()) gameObject.AddComponent<RunHUD>();
        }

        void Start()
        {
            nextBarrel = barrelInterval;
            nextSupply = firstSupplyTime;
            nextChallenge = firstChallengeTime;
        }

        void OnDestroy() => EndFrenzy();

        /// <summary>
        /// Supply crates are a lull reward: WorldEvents calls this at the start of each lull (when no pact altar
        /// takes the moment). Returns true if a crate fell.
        /// </summary>
        public bool TrySupplyDrop()
        {
            var gm = GameManager.Instance;
            if (!crate || !gm || gm.State != GameState.Playing || !PlayerController.Instance || gm.RunTime < nextSupply) return false;
            int before = CountAlive(crate);
            if (before >= 2) return false;
            nextSupply = gm.RunTime + supplyEvery;
            SupplyDrop();
            return true;
        }

        /// <summary>Kill streak needed for the next frenzy.</summary>
        public int NextFrenzyCombo => Mathf.RoundToInt((frenzyFirstCombo + frenzyComboGrowth * frenzyCount) * frenzyComboScale);

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm || gm.State != GameState.Playing || !PlayerController.Instance || !RunDirector.Instance) return;
            float time = gm.RunTime;

            if (!started)
            {
                started = true;
                for (int i = 0; i < startBarrels; i++) SpawnBarrelOnScreen();
            }

            if (barrel && time >= nextBarrel)
            {
                nextBarrel = time + barrelInterval;
                SpawnBarrelsAhead();
            }


            UpdateFrenzy();
            UpdateChallenge(gm, time);
        }

        // ---------- Barrels ----------

        int CountAlive(EnemyData data)
        {
            int n = 0;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++) if (list[i].Data == data) n++;
            return n;
        }

        void SpawnBarrelOnScreen()
        {
            if (!barrel) return;
            var player = PlayerController.Instance;
            var r = RunDirector.ViewRect(-1.5f);
            for (int tries = 0; tries < 10; tries++)
            {
                var p = new Vector3(Random.Range(r.xMin, r.xMax), 0f, Random.Range(r.yMin, r.yMax));
                if ((p - player.Position).sqrMagnitude < 20f) continue;
                RunDirector.Instance.SpawnAt(barrel, p);
                return;
            }
        }

        /// <summary>A small cluster of barrels just off-screen, roughly where the player is heading.</summary>
        void SpawnBarrelsAhead()
        {
            if (CountAlive(barrel) >= maxBarrels) return;
            var player = PlayerController.Instance;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * player.Facing;
            Vector3 at = RunDirector.Instance.EdgePointToward(dir, 1f);
            int count = Random.Range(1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector2 c = Random.insideUnitCircle * 1.6f;
                RunDirector.Instance.SpawnAt(barrel, at + new Vector3(c.x, 0f, c.y));
            }
        }

        // ---------- Supply drop ----------

        void SupplyDrop()
        {
            if (CountAlive(crate) >= 2) return;
            var player = PlayerController.Instance;
            var r = RunDirector.ViewRect(-2f);
            Vector3 at = player.Position + Vector3.forward * 4f;
            for (int tries = 0; tries < 10; tries++)
            {
                var p = new Vector3(Random.Range(r.xMin, r.xMax), 0f, Random.Range(r.yMin, r.yMax));
                float d = (p - player.Position).sqrMagnitude;
                if (d < 12f || d > 49f) continue;
                at = p;
                break;
            }

            var box = RunDirector.Instance.SpawnAt(crate, at);
            if (!box) return;
            box.DropIn(dropHeight);
            if (GameFX.Instance) GameFX.Instance.Ring(at, 1.8f, new Color(1.2f, 1.1f, 0.8f), 0.8f);
            GameHUD.Instance.ShowBanner("RAVITAILLEMENT !", new Color(0.6f, 0.9f, 1f), 1.3f);
            SFX.Play(SfxId.Throw, 1f, 0.6f);
        }

        // ---------- Frenzy ----------

        void UpdateFrenzy()
        {
            // The threshold climbs with every frenzy of the run (not reset by a broken streak): a few per run, earned.
            int combo = Combat.ComboTimeLeft > 0f ? Combat.Combo : 0;
            if (combo >= NextFrenzyCombo && frenzyLeft <= 0f)
            {
                frenzyCount++;
                StartFrenzy();
            }

            if (frenzyLeft > 0f)
            {
                frenzyLeft -= Time.deltaTime;
                var player = PlayerController.Instance;
                if (GameFX.Instance && Random.value < 0.5f)
                    GameFX.Instance.Embers(player.Position, 0.6f, 1, FrenzyColor * 2.5f);
                if (frenzyLeft <= 0f) EndFrenzy();
            }
        }

        void StartFrenzy()
        {
            bool wasActive = frenzyLeft > 0f;
            frenzyLeft = frenzyDuration;
            var player = PlayerController.Instance;

            if (!frenzyApplied && player && player.Stats)
            {
                player.Stats.cooldownMult *= frenzyCooldownMult;
                player.Stats.moveSpeed *= frenzySpeedMult;
                frenzyApplied = true;
            }

            GameHUD.Instance.ShowBanner(wasActive ? "FRÉNÉSIE PROLONGÉE !" : "FRÉNÉSIE !\n<size=50>Cadence x2</size>", FrenzyColor, 1.6f);
            GameHUD.Instance.ScreenFlash(FrenzyColor, 0.5f);
            SFX.Play(SfxId.Frenzy);
            if (GameFX.Instance)
            {
                Vector3 p = player.Position;
                GameFX.Instance.Ring(p, 6f, FrenzyColor * 3f, 0.5f);
                GameFX.Instance.Sparks(p + Vector3.up, FrenzyColor * 2.5f, 40, 2f);
            }
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
        }

        void EndFrenzy()
        {
            frenzyLeft = 0f;
            if (!frenzyApplied) return;
            frenzyApplied = false;
            var player = PlayerController.Instance;
            if (!player || !player.Stats) return;
            player.Stats.cooldownMult /= frenzyCooldownMult;
            player.Stats.moveSpeed /= frenzySpeedMult;
        }

        // ---------- Timed challenge ----------

        /// <summary>Bodies the stream brings in <paramref name="seconds"/> right now (RunDirector's own formula, groups ~1.3).</summary>
        static float ExpectedKills(float seconds)
        {
            var d = RunDirector.Instance;
            float m = d.Minutes;
            float rate = Mathf.Min(d.maxRate, d.baseRate + d.ratePerMinute * m + d.earlyRamp * Mathf.Min(m, 2f));
            return rate * 1.3f * RunConfig.SpawnRateMult * seconds;
        }

        void UpdateChallenge(GameManager gm, float time)
        {
            var boss = RunDirector.Instance.CurrentBoss;
            bool bossAlive = boss && boss.gameObject.activeSelf && !boss.IsDead;

            if (!ChallengeActive)
            {
                if (time < nextChallenge) return;
                // Starts in a wave's build phase with enough time left, never with a boss around.
                var director = RunDirector.Instance;
                bool buildOk = director.Phase == WavePhase.Build && director.PhaseTimeLeft >= challengeDuration * 0.75f;
                if (bossAlive || !buildOk)
                {
                    nextChallenge = time + (bossAlive ? 10f : 1f);
                    return;
                }
                ChallengeActive = true;
                challengeStartKills = gm.Kills;
                ChallengeKills = 0;
                ChallengeTimeLeft = challengeDuration;
                ChallengeTarget = Mathf.Max(challengeMinKills, Mathf.RoundToInt(ExpectedKills(challengeDuration) * challengeStreamFactor / 5f) * 5);
                GameHUD.Instance.ShowBanner($"DÉFI !\n<size=50>Tue {ChallengeTarget} ennemis en {challengeDuration:0} s</size>", UIKit.Gold, 2f);
                SFX.Play(SfxId.Alert);
                return;
            }

            ChallengeKills = gm.Kills - challengeStartKills;
            ChallengeTimeLeft -= Time.deltaTime;

            if (ChallengeKills >= ChallengeTarget)
            {
                ChallengeActive = false;
                nextChallenge = time + challengeInterval;
                Vector3 p = PlayerController.Instance.Position;
                Vector3 drop = p + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 2.5f;
                if (Loot.Instance)
                {
                    Loot.Instance.DropRewardChest(drop + Vector3.up * 0.4f);
                    Loot.Instance.DropCoins(drop + Vector3.up * 0.4f, 8, 2);
                }
                GameHUD.Instance.ShowBanner("DÉFI RÉUSSI !\n<size=50>Un coffre t'attend</size>", UIKit.Gold, 2f);
                GameHUD.Instance.ScreenFlash(new Color(1f, 0.85f, 0.4f), 0.4f);
                SFX.Play(SfxId.Success);
                if (GameFX.Instance) GameFX.Instance.Ring(drop, 3f, new Color(3f, 2.4f, 0.8f), 0.6f);
            }
            else if (ChallengeTimeLeft <= 0f)
            {
                ChallengeActive = false;
                nextChallenge = time + challengeInterval;
                GameHUD.Instance.ShowBanner("DÉFI RATÉ...", UIKit.Muted, 1.5f);
                SFX.Play(SfxId.Fail);
            }
        }
    }
}
