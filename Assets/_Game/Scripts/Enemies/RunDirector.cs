using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Survivor
{
    [Serializable]
    public class SpawnEntry
    {
        public EnemyData enemy;
        [Tooltip("Seconds into the run when this enemy starts appearing.")]
        public float startTime;
        public float endTime = 99999f;
        [Tooltip("Relative chance compared to the other active entries.")]
        public float weight = 1f;
        [Tooltip("Weight added per minute of run (negative = gets rarer). The mix gets nastier over time.")]
        public float weightPerMinute = 0f;
        [Tooltip("Floor for the weight once weightPerMinute is applied.")]
        public float minWeight = 0.05f;
        [Tooltip("Ceiling for the weight (0 = no ceiling).")]
        public float maxWeight = 0f;
        [Tooltip("How many come together each time this entry is picked (rats come in packs).")]
        public int groupSize = 1;

        public float WeightAt(float minutes)
        {
            float w = Mathf.Max(minWeight, weight + weightPerMinute * minutes);
            return maxWeight > 0f ? Mathf.Min(w, maxWeight) : w;
        }
    }

    /// <summary>Where the current wave is: pressure builds up, peaks (horde / assault), then a short breather.</summary>
    public enum WavePhase { Build, Peak, Lull }

    /// <summary>
    /// Paces the run in waves of <see cref="waveLength"/> seconds:
    /// BUILD (the stream grows from 0.7x to 1.3x) -> PEAK (1.5x + a horde ring or a frontal assault) -> LULL
    /// (almost nothing spawns for a few seconds, so the screen clears, HP recovers and the combo can break).
    /// On top of that: glowing elites (loot piñatas), rat swarms, golden waves, a thief to chase and a boss every few minutes.
    /// The mix of the stream gets nastier over time (more runners, brutes and spitters), and a third of the stream comes
    /// from where the player is heading, so it can't simply be outrun.
    /// Enemy health and damage scale up forever (times the campaign multipliers in RunConfig).
    /// Numbers and reasoning: Docs/Pacing.md.
    /// </summary>
    public class RunDirector : MonoBehaviour
    {
        public static RunDirector Instance { get; private set; }

        public List<SpawnEntry> entries = new();

        [Header("Placement")]
        [Tooltip("Enemies appear this many meters outside the visible screen area.")]
        public float spawnMargin = 2f;
        [Tooltip("Share of the stream that spawns where the player is heading (±60°) instead of anywhere around the screen.")]
        [Range(0f, 1f)] public float aheadChance = 0.15f;
        [Tooltip("Extra margin on the far (top) side of the screen, and for the boss (tall bodies poke into view).")]
        public float farSpawnExtra = 3f, bossSpawnExtra = 5f;
        [Tooltip("Most shooters (Cracheurs) alive at once (Bestiaire 4.5); past it, the pick is rerolled / swapped for a runner.")]
        public int maxShooters = 6;

        [Header("Stream (picks per second; a pick is one enemy, or a whole pack for grouped entries)")]
        [Tooltip("Picks per second at 0:00.")]
        public float baseRate = 1f;
        [Tooltip("Extra picks per second added each minute.")]
        public float ratePerMinute = 1.7f;
        [Tooltip("Extra ramp per minute during the first 2 minutes, so the action builds up quickly.")]
        public float earlyRamp = 0.5f;
        [Tooltip("Ceiling on the base rate (before the wave multiplier), for very long endless runs.")]
        public float maxRate = 45f;
        [Tooltip("Mobile safety cap on living enemies for the stream.")]
        public int maxAlive = 320;
        [Tooltip("Absolute cap, events included.")]
        public int hardCap = 380;

        [Header("Waves: build -> peak -> lull")]
        public float waveLength = 50f;
        [Tooltip("Seconds of build-up at the start of each wave.")]
        public float buildTime = 30f;
        [Tooltip("Seconds of peak right after (the horde / assault lands at its start). The rest of the wave is the lull.")]
        public float peakTime = 12f;
        public float buildStartMult = 0.7f;
        public float buildEndMult = 1.3f;
        public float peakMult = 1.35f;
        [Tooltip("First seconds of the lull: nothing spawns at all (the screen clears, the combo can break).")]
        public float lullSilence = 5f;
        [Tooltip("Stream multiplier for the rest of the lull.")]
        [Range(0f, 1f)] public float lullMult = 0.3f;
        [Tooltip("The rhythm fades in over this many minutes (the first waves are flatter).")]
        public float rhythmRampMinutes = 1.5f;
        [Tooltip("Stream multiplier while a boss is alive (the boss is the peak).")]
        [Range(0f, 1f)] public float bossStreamMult = 0.6f;

        [Header("Scaling (per minute)")]
        public float healthPerMinute = 0.15f;
        public float healthPerMinuteSquared = 0.03f;
        [Tooltip("Endless only: extra toughness so a strong build still falls around 15-20 min. 0.04 -> 0.01 with the 20-level " +
                 "stuff: builds stop growing around 5 min, the base curve does the rest.")]
        public float endlessHealthPerMinuteSquared = 0.01f;
        public float endlessDamagePerMinute = 0.03f;
        public float damagePerMinute = 0.07f;
        [Tooltip("XP per enemy grows this much per minute. Kept low: the stream already grows, the XP curve (PlayerXP) does the pacing.")]
        public float xpPerMinute = 0.03f;

        [Header("Elites")]
        public float firstEliteTime = 55f;
        public float eliteInterval = 40f;

        [Header("Hordes (peak of odd waves): a ring all around the screen")]
        public EnemyData hordeEnemy;
        [Tooltip("Mixed into hordes and assaults after 2 minutes.")]
        public EnemyData hordeFastEnemy;
        [Tooltip("A few of these in hordes after 3 minutes: shoot them for chain reactions.")]
        public EnemyData hordeKamikaze;
        [Tooltip("Brutes: in hordes after hordeHeavyFromMinute, and in assaults.")]
        public EnemyData hordeHeavyEnemy;
        public float hordeHeavyFromMinute = 5f;
        public int hordeBaseCount = 12;
        public float hordeCountPerMinute = 6f;
        public int hordeMaxCount = 100;
        [Tooltip("Width (m) of each of the two breaches left in a horde ring (Bestiaire: choisir une brèche).")]
        public float ringGap = 4f;
        [Tooltip("Peaks (horde / assault / pincer) and rat swarms: the edge glows where they come from this long before they spawn.")]
        public float crueWarnTime = 1.2f;
        [Tooltip("The peak lands up to this many seconds earlier or later each wave (never at the same second: Bestiaire 4.3 e).")]
        public float peakJitter = 6f;
        [Tooltip("Pincer (two columns from opposite sides) joins the formation draw from this minute.")]
        public float pincerFromMinute = 5f;

        [Header("Assaults (peak of even waves): a dense mixed column from where the player is heading")]
        [Tooltip("Spitters mixed into assaults.")]
        public EnemyData assaultRangedEnemy;
        [Tooltip("Brutes and spitters join assaults after this minute (zombies / runners before).")]
        public float assaultMixFromMinute = 2.5f;
        public int assaultBaseCount = 8;
        public float assaultCountPerMinute = 3.5f;
        public int assaultMaxCount = 60;

        [Header("Rat swarm (build phase only)")]
        public EnemyData ratEnemy;
        public float firstRatTime = 62f;
        public float ratInterval = 80f;
        public int ratBaseCount = 28;
        public float ratCountPerMinute = 4f;

        [Header("Golden wave (build phase only)")]
        public EnemyData goldenEnemy;
        public float firstGoldenTime = 105f;
        public float goldenInterval = 120f;
        public int goldenCount = 12;

        [Header("Thief (build phase only)")]
        public EnemyData thiefEnemy;
        public float firstThiefTime = 72f;
        public float thiefInterval = 95f;

        [Header("Boss (lands at the start of a wave, right after a lull)")]
        public EnemyData boss;
        public float firstBossTime = 200f;
        public float bossInterval = 200f;
        [Tooltip("Each new boss gets this much more health (0.6 = +60%).")]
        public float bossHealthGrowth = 0.6f;

        [Header("Danger tiers (endless): every dangerStep seconds the arena adds a rule, announced (Docs/Pacing.md)")]
        [Tooltip("Seconds per danger tier. 150 = a new tier at 2:30, 5:00, 7:30...")]
        public float dangerStep = 150f;
        [Tooltip("From tier 8 (Apocalypse): extra enemy health and damage per tier.")]
        public float apocalypsePerTier = 0.12f;

        /// <summary>Danger tier of the run (1 at the start; endless only, always 1 in the campaign).</summary>
        public int Danger { get; private set; } = 1;
        /// <summary>Fired when the danger tier goes up (new tier).</summary>
        public event Action<int> DangerChanged;

        /// <summary>Name and rule of each tier (index = tier - 1); tier 8 and up are all "Apocalypse" (names: Docs/Univers.md 6.5).</summary>
        public static readonly string[,] DangerTiers =
        {
            { "Suintement", "L'eau suinte. Ça va monter." },
            { "Montée", "Plus de Fiévreux" },
            { "Crue", "Les Cracheurs se multiplient" },
            { "Débordement", "Alphas plus fréquents, +1 affixe" },
            { "Déferlante", "Crues et assauts 30% plus gros" },
            { "Submersion", "Les Masses déferlent" },
            { "Noyade", "Ennemis 10% plus rapides" },
            { "Apocalypse", "+12% PV et dégâts à chaque palier" },
        };

        public static string DangerName(int tier)
        {
            int i = Mathf.Clamp(tier, 1, DangerTiers.GetLength(0)) - 1;
            int extra = tier - DangerTiers.GetLength(0);
            return extra > 0 ? $"{DangerTiers[i, 0]} {extra + 1}" : DangerTiers[i, 0];
        }

        public static string DangerRule(int tier) => DangerTiers[Mathf.Clamp(tier, 1, DangerTiers.GetLength(0)) - 1, 1];

        /// <summary>Yellow at tier 1, deep red from tier 7.</summary>
        public static Color DangerColor(int tier) => Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.15f, 0.1f), Mathf.Clamp01((tier - 1) / 6f));

        bool DangerOn => !RunConfig.IsCampaign;
        float DangerHealthMult => 1f + apocalypsePerTier * Mathf.Max(0, Danger - DangerTiers.GetLength(0) + 1);

        /// <summary>Stream weight multiplier of an enemy type at the current tier.</summary>
        float DangerWeight(EnemyData e)
        {
            if (!DangerOn || !e) return 1f;
            // Tiers 2-3 stay gentle: they land around the 3:00 horde and the first boss (a 1.6x first try killed
            // focused builds at 4:44); the late tiers can hit harder.
            if (e == hordeFastEnemy && Danger >= 2) return 1.35f;
            if (e == assaultRangedEnemy && Danger >= 3) return 1.45f;
            if (e == hordeHeavyEnemy && Danger >= 6) return 1.7f;
            return 1f;
        }

        float DangerEliteInterval => DangerOn && Danger >= 4 ? 0.7f : 1f;
        float DangerHordeMult => DangerOn && Danger >= 5 ? 1.3f : 1f;

        void UpdateDanger(float time)
        {
            if (!DangerOn || dangerStep <= 0f) return;
            int tier = 1 + Mathf.FloorToInt(time / dangerStep);
            if (tier <= Danger) return;
            Danger = tier;
            // One-shot rules (statics reset with each run by RunModifiers.ResetStatics).
            if (tier == 4) RunModifiers.ExtraEliteAffixes += 1;
            if (tier == 7) RunModifiers.EnemySpeedMult *= 1.1f;
            DangerChanged?.Invoke(tier);

            Color c = DangerColor(tier);
            if (GameHUD.Instance)
            {
                GameHUD.Instance.QueueBanner($"<size=96>DANGER {tier}</size>\n<size=52>{DangerName(tier)}</size>\n<size=36>{DangerRule(tier)}</size>", c, 2.8f);
                GameHUD.Instance.ScreenFlash(new Color(c.r * 0.6f, c.g * 0.15f, 0.05f), 0.35f);
            }
            SFX.Play(SfxId.Alert, 0.9f, 0.75f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
        }

        public Enemy CurrentBoss { get; private set; }
        public float Minutes => GameManager.Instance ? GameManager.Instance.RunTime / 60f : 0f;

        /// <summary>Current wave (0 = the first 50 s).</summary>
        public int WaveIndex { get; private set; }
        public WavePhase Phase { get; private set; } = WavePhase.Build;
        /// <summary>Seconds left in the current phase.</summary>
        public float PhaseTimeLeft { get; private set; }
        /// <summary>Fired when the wave phase changes (wave index, new phase). Arena events / HUD can hook on it.</summary>
        public event Action<int, WavePhase> PhaseChanged;

        /// <summary>Two announced events never start closer than this.</summary>
        const float EventGap = 8f;

        struct QueuedSpawn
        {
            public EnemyData data;
            /// <summary>World position, or an offset from the player when <see cref="relative"/> (warned crues: they must
            /// still land off-screen if the player ran during the warning).</summary>
            public Vector3 position, push;
            public float health, damage, xp, at;
            public bool relative;
        }

        readonly List<QueuedSpawn> queued = new(128);

        /// <summary>Peak formations (Bestiaire 4.3 a): drawn each peak, never the same twice in a row.</summary>
        enum Formation { Ring, Column, Pincer }
        Formation lastFormation = Formation.Column; // the first peak is a ring, as before

        const float CrueWarnRadius = 1.5f, CrueWarnMargin = 0.1f, MinLull = 4f;
        /// <summary>Hash streams of this file's draws (seeded runs: same peaks for everyone).</summary>
        const int JitterStream = 71, FormationStream = 73;

        float pending;
        float nextBoss, nextElite, nextRats, nextGolden, nextThief;
        float lastEvent = -99f;
        int bossCount, lastPeakWave;
        /// <summary>Unseeded runs: a per-run salt for the peak draws.</summary>
        int runSalt = 1;

        void Awake() => Instance = this;

        void Start()
        {
            Threats.ApplyToDirector(this); // ranked threats: scales this run's boss / elite timers (defaults untouched)
            Enemy.ResetAffixRolls();
            nextBoss = firstBossTime;
            nextElite = firstEliteTime;
            nextRats = firstRatTime;
            nextGolden = firstGoldenTime;
            nextThief = firstThiefTime;
            lastPeakWave = 0; // wave 0's peak is stream only (no horde in the first minute)
            lastFormation = Formation.Column;
            runSalt = Random.Range(1, int.MaxValue);
        }

        /// <summary>Seed of a draw: the mode's seed in daily / weekly runs (same for everyone), else this run's salt.</summary>
        int DrawSeed(int stream, int index) => GameModes.Hash(RunConfig.Seed != 0 ? RunConfig.Seed : runSalt, stream * 1000003 + index, 0);

        /// <summary>How early (-) or late (+) this wave's peak lands. Wave 0 keeps the usual opening.</summary>
        float PeakOffset(int wave)
        {
            if (wave <= 0 || peakJitter <= 0f) return 0f;
            int h = DrawSeed(JitterStream, wave);
            return ((h & 0xFFFF) / 65535f * 2f - 1f) * peakJitter;
        }

        public float HealthMult(float minutes) => (1f + healthPerMinute * minutes
            + (healthPerMinuteSquared + (RunConfig.IsCampaign ? 0f : endlessHealthPerMinuteSquared)) * minutes * minutes) * DangerHealthMult;
        public float DamageMult(float minutes) => (1f + (damagePerMinute + (RunConfig.IsCampaign ? 0f : endlessDamagePerMinute)) * minutes) * DangerHealthMult;
        public float XPMult(float minutes) => 1f + xpPerMinute * minutes;

        /// <summary>Base stream (picks per second) before the wave multiplier.</summary>
        public float BaseStreamRate(float minutes) =>
            Mathf.Min(maxRate, baseRate + ratePerMinute * minutes + earlyRamp * Mathf.Min(minutes, 2f));

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm.State != GameState.Playing || !PlayerController.Instance) return;

            if (CurrentBoss && (!CurrentBoss.gameObject.activeSelf || CurrentBoss.IsDead)) CurrentBoss = null;

            SpawnQueued();

            float time = gm.RunTime;
            float minutes = time / 60f;
            UpdateDanger(time);

            float waveMult = UpdateWave(time, minutes);
            bool bossAlive = CurrentBoss;

            // ---- Stream ----
            float rate = BaseStreamRate(minutes) * waveMult;
            if (bossAlive) rate *= bossStreamMult;
            pending += rate * RunConfig.SpawnRateMult * Time.deltaTime;
            while (pending >= 1f)
            {
                pending -= 1f;
                if (Enemy.Active.Count >= maxAlive) continue;
                var entry = Pick(time);
                if (entry == null) continue;
                Vector3 at = Random.value < aheadChance
                    ? EdgePointToward(Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * PlayerController.Instance.Facing, spawnMargin + Random.Range(0f, 2f))
                    : RandomEdgePoint(spawnMargin + Random.Range(0f, 2f));
                int group = Mathf.Max(1, entry.groupSize);
                for (int i = 0; i < group; i++)
                {
                    Vector2 jitter = group > 1 ? Random.insideUnitCircle * 1.5f : Vector2.zero;
                    Spawn(entry.enemy, at + new Vector3(jitter.x, 0f, jitter.y), minutes, 1f);
                }
            }

            // ---- Peak event: horde (odd waves) or assault (even waves). The boss replaces it while alive. ----
            if (Phase == WavePhase.Peak && WaveIndex > lastPeakWave)
            {
                lastPeakWave = WaveIndex;
                if (!bossAlive)
                {
                    lastEvent = time;
                    SpawnPeak(minutes);
                }
            }

            // ---- Elites: any phase but the peak (never stacked on a horde), pushed to the lull if needed. ----
            if (time >= nextElite && Phase != WavePhase.Peak)
            {
                float interval = eliteInterval * DangerEliteInterval;
                nextElite = Mathf.Max(nextElite + interval, time + interval * 0.5f);
                SpawnElite(time, minutes);
            }

            // ---- Boss: on its timer (Campaign reads firstBossTime / bossInterval for the boss star). ----
            // Never two bosses at once: a boss still alive at its successor's time delays it until it dies.
            if (time >= nextBoss && !bossAlive)
            {
                nextBoss = Mathf.Max(nextBoss + bossInterval, time + bossInterval * 0.5f);
                lastEvent = time;
                SpawnBoss(minutes);
                return;
            }

            // ---- Side events: build phase only, and not right before the peak. ----
            bool sideOk = Phase == WavePhase.Build && PhaseTimeLeft > EventGap;
            if (!sideOk) return;
            if (ratEnemy && EventDue(ref nextRats, ratInterval, time)) SpawnRats(minutes);
            else if (goldenEnemy && EventDue(ref nextGolden, goldenInterval, time)) SpawnGoldenWave(minutes);
            else if (thiefEnemy && EventDue(ref nextThief, thiefInterval, time)) SpawnThief(minutes);
        }

        /// <summary>Updates the wave phase and returns the stream multiplier for this moment.</summary>
        float UpdateWave(float time, float minutes)
        {
            float length = Mathf.Max(10f, waveLength);
            int index = Mathf.FloorToInt(time / length);
            float t = time - index * length;
            // The peak moves a few seconds each wave (never on the same second), keeping at least MinLull of breather.
            float build = Mathf.Min(buildTime + PeakOffset(index), Mathf.Max(buildTime, length - peakTime - MinLull));
            build = Mathf.Clamp(build, 1f, length);
            float peakEnd = Mathf.Clamp(build + peakTime, build, length);

            WavePhase phase;
            float mult;
            if (t < build)
            {
                phase = WavePhase.Build;
                mult = Mathf.Lerp(buildStartMult, buildEndMult, t / build);
                PhaseTimeLeft = build - t;
            }
            else if (t < peakEnd)
            {
                phase = WavePhase.Peak;
                mult = peakMult;
                PhaseTimeLeft = peakEnd - t;
            }
            else
            {
                phase = WavePhase.Lull;
                mult = t - peakEnd < lullSilence ? 0f : lullMult;
                PhaseTimeLeft = length - t;
            }

            if (index != WaveIndex || phase != Phase)
            {
                WaveIndex = index;
                Phase = phase;
                PhaseChanged?.Invoke(index, phase);
            }

            // The rhythm fades in: the very first waves are flatter (no hard lull at 0:42, no big peak at 0:30).
            float amount = rhythmRampMinutes > 0f ? Mathf.Clamp01(minutes / rhythmRampMinutes) : 1f;
            return Mathf.Lerp(1f, mult, amount);
        }

        /// <summary>True when an event should start now; pushes it back a little if another one just happened.</summary>
        bool EventDue(ref float next, float interval, float time)
        {
            if (time < next) return false;
            if (time < lastEvent + EventGap)
            {
                next = lastEvent + EventGap;
                return false;
            }
            next = Mathf.Max(next + interval, time + interval * 0.5f);
            lastEvent = time;
            return true;
        }

        // ---------- Events ----------

        void SpawnElite(float time, float minutes)
        {
            EnemyData data = null;
            for (int tries = 0; tries < 6 && !data; tries++)
            {
                var entry = Pick(time);
                if (entry != null && entry.enemy.canBeElite) data = entry.enemy;
            }
            if (!data) return;
            Spawn(data, EdgePointToward(PlayerController.Instance.Facing, spawnMargin + 1f), minutes, 1f, true);
            GameHUD.Instance.ShowBanner("ALPHA !\n<size=50>Butin en approche</size>", UIKit.Gold, 1.4f);
            SFX.Play(SfxId.Alert, 0.7f, 0.85f);
        }

        /// <summary>
        /// The peak event: a formation drawn among the unlocked ones, never the same twice in a row (Bestiaire 4.3).
        /// Daily / weekly runs draw from the mode's seed. Every formation is announced: banner, sound, and an amber glow
        /// on the screen edge where it comes from, <see cref="crueWarnTime"/> before the enemies spawn.
        /// </summary>
        void SpawnPeak(float minutes)
        {
            if (!hordeEnemy) return;
            var rng = new System.Random(DrawSeed(FormationStream, WaveIndex));
            float ring = lastFormation == Formation.Ring ? 0f : 3f;
            float column = lastFormation == Formation.Column ? 0f : 3f;
            float pincer = lastFormation == Formation.Pincer || minutes < pincerFromMinute ? 0f : 2f;
            float roll = (float)rng.NextDouble() * (ring + column + pincer);
            var f = roll < ring ? Formation.Ring : roll < ring + column ? Formation.Column : Formation.Pincer;
            lastFormation = f;
            switch (f)
            {
                case Formation.Ring: SpawnHorde(minutes, rng); break;
                case Formation.Column: SpawnAssault(minutes, rng); break;
                default: SpawnPincer(minutes, rng); break;
            }
        }

        /// <summary>A ring all around the screen, with two breaches to choose from.</summary>
        void SpawnHorde(float minutes, System.Random rng)
        {
            // Evenly spread all around the screen edges: the player is surrounded whatever the orientation.
            int count = Mathf.RoundToInt((hordeBaseCount + hordeCountPerMinute * minutes) * RunConfig.SpawnRateMult * DangerHordeMult);
            count = Mathf.Min(count, hordeMaxCount, hardCap - Enemy.Active.Count);
            if (count <= 0) return;

            var r = SpawnRect(spawnMargin);
            float perimeter = 2f * (r.width + r.height);
            float gap = Mathf.Min(ringGap, perimeter * 0.08f);
            float usable = perimeter - 2f * gap;
            float offset = (float)rng.NextDouble() * perimeter;
            // First breach at [0, gap), the second one somewhere across the ring.
            float gap2 = gap + usable * Mathf.Lerp(0.35f, 0.65f, (float)rng.NextDouble());
            int warnEvery = Mathf.Max(1, Mathf.CeilToInt(count / 8f)); // ~8 glows around the edge
            for (int i = 0; i < count; i++)
            {
                var data = hordeEnemy;
                if (hordeFastEnemy && minutes >= 2f && i % 3 == 0) data = hordeFastEnemy;
                if (hordeHeavyEnemy && minutes >= hordeHeavyFromMinute && i % 9 == 4) data = hordeHeavyEnemy;
                if (hordeKamikaze && minutes >= 3f && i % 11 == 5) data = hordeKamikaze;
                float d = gap + usable * (i + 0.5f) / count;
                if (d >= gap2) d += gap;
                Vector3 at = PerimeterPoint(r, offset + d);
                QueueCrue(data, at, minutes, crueWarnTime);
                if (i % warnEvery == warnEvery / 2) WarnFrom(at, crueWarnTime);
            }

            GameHUD.Instance.ShowBanner("CRUE !\n<size=50>Trouve la brèche</size>", UIKit.Blood, 1.8f);
            SFX.Play(SfxId.Horde);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.25f);
        }

        int AssaultCount(float minutes)
        {
            int count = Mathf.RoundToInt((assaultBaseCount + assaultCountPerMinute * minutes) * RunConfig.SpawnRateMult * DangerHordeMult);
            return Mathf.Min(count, assaultMaxCount, hardCap - Enemy.Active.Count);
        }

        /// <summary>
        /// A dense mixed column: runners in front, brutes and spitters behind them. From where the player is heading
        /// (60%), from a side (25%) or from behind (15%). Has to be cut through.
        /// </summary>
        void SpawnAssault(float minutes, System.Random rng)
        {
            int count = AssaultCount(minutes);
            if (count <= 0) return;

            float roll = (float)rng.NextDouble();
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            float angle = roll < 0.6f ? 0f : roll < 0.85f ? 90f * side : 180f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * PlayerController.Instance.Facing;
            SpawnColumn(dir, count, 50f, minutes, 0);

            string from = angle == 0f ? "Ils arrivent devant" : angle == 180f ? "Ils arrivent DERRIÈRE" : "Ils arrivent sur le côté";
            GameHUD.Instance.ShowBanner($"ASSAUT !\n<size=50>{from}</size>", new Color(1f, 0.5f, 0.15f), 1.6f);
            SFX.Play(SfxId.Horde, 0.8f, 1.15f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
        }

        /// <summary>Two columns from opposite sides of the player's heading: get out ahead or behind.</summary>
        void SpawnPincer(float minutes, System.Random rng)
        {
            int count = AssaultCount(minutes);
            if (count <= 1) return;

            float angle = 90f + Mathf.Lerp(-20f, 20f, (float)rng.NextDouble());
            Vector3 facing = PlayerController.Instance.Facing;
            int half = count / 2;
            SpawnColumn(Quaternion.Euler(0f, angle, 0f) * facing, half, 35f, minutes, 0);
            SpawnColumn(Quaternion.Euler(0f, angle + 180f, 0f) * facing, count - half, 35f, minutes, 1);

            GameHUD.Instance.ShowBanner("TENAILLE !\n<size=50>Ils arrivent des deux côtés</size>", new Color(1f, 0.5f, 0.15f), 1.6f);
            SFX.Play(SfxId.Horde, 0.85f, 1.05f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.22f);
        }

        /// <summary>Queues a warned column spread over ±<paramref name="spread"/>° around <paramref name="dir"/>.</summary>
        void SpawnColumn(Vector3 dir, int count, float spread, float minutes, int mixOffset)
        {
            bool mixed = minutes >= assaultMixFromMinute;
            for (int i = 0; i < count; i++)
            {
                EnemyData data;
                switch ((i + mixOffset) % 4)
                {
                    case 2: data = mixed && hordeHeavyEnemy ? hordeHeavyEnemy : hordeEnemy; break;
                    case 3: data = mixed && assaultRangedEnemy ? assaultRangedEnemy : hordeFastEnemy ? hordeFastEnemy : hordeEnemy; break;
                    default: data = hordeFastEnemy ? hordeFastEnemy : hordeEnemy; break;
                }
                float angle = count > 1 ? Mathf.Lerp(-spread, spread, (float)i / (count - 1)) : 0f;
                Vector3 d = Quaternion.Euler(0f, angle, 0f) * dir;
                Vector2 jitter = Random.insideUnitCircle * 0.8f;
                Vector3 at = EdgePointToward(d, spawnMargin + Random.Range(0f, 2.5f)) + new Vector3(jitter.x, 0f, jitter.y);
                QueueCrue(data, at, minutes, crueWarnTime);
            }
            // The glow: the front of the column, left / centre / right.
            for (int k = -1; k <= 1; k++)
                WarnFrom(EdgePointToward(Quaternion.Euler(0f, spread * 0.6f * k, 0f) * dir, spawnMargin), crueWarnTime);
        }

        /// <summary>A tight pack of rats pouring in from one side: tiny, fast, one hit each. Multi-kill heaven.</summary>
        void SpawnRats(float minutes)
        {
            int count = Mathf.RoundToInt((ratBaseCount + ratCountPerMinute * minutes) * RunConfig.SpawnRateMult);
            count = Mathf.Min(count, 70, hardCap - Enemy.Active.Count);
            if (count <= 0) return;

            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 center = EdgePointToward(dir, spawnMargin + 2f);
            for (int i = 0; i < count; i++)
            {
                Vector2 c = Random.insideUnitCircle * 3f;
                QueueCrue(ratEnemy, center + new Vector3(c.x, 0f, c.y), minutes, crueWarnTime);
            }
            WarnFrom(center, crueWarnTime);

            GameHUD.Instance.ShowBanner("NUÉE !", new Color(0.85f, 0.75f, 0.6f), 1.6f);
            SFX.Play(SfxId.Squeak);
            SFX.Play(SfxId.Alert, 0.6f, 1.3f);
        }

        /// <summary>Queues a crue enemy: spawns after <paramref name="delay"/>, at the same place RELATIVE to the player
        /// (so it still lands off-screen if he ran meanwhile), with the scaling of <paramref name="minutes"/>.</summary>
        void QueueCrue(EnemyData data, Vector3 at, float minutes, float delay)
        {
            if (!data) return;
            Vector3 offset = at - PlayerController.Instance.Position;
            offset.y = 0f;
            queued.Add(new QueuedSpawn
            {
                data = data, position = offset, relative = true, at = Time.time + delay,
                health = HealthMult(minutes) * RunConfig.EnemyHealthMult,
                damage = DamageMult(minutes) * RunConfig.EnemyDamageMult,
                xp = XPMult(minutes),
            });
        }

        /// <summary>Amber glow on the screen edge, on the line from the player to an off-screen spawn point.</summary>
        void WarnFrom(Vector3 at, float duration)
        {
            var fx = GameFX.Instance;
            var player = PlayerController.Instance;
            if (!fx || !player) return;
            Vector3 inside = player.Position, outside = at;
            inside.y = outside.y = 0f;
            var view = CameraFollow.Instance;
            if (view)
            {
                if (view.IsOnScreen(outside, CrueWarnMargin)) inside = outside;
                else
                {
                    // Binary search of the last point still well on screen.
                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 m = (inside + outside) * 0.5f;
                        if (view.IsOnScreen(m, CrueWarnMargin)) inside = m;
                        else outside = m;
                    }
                }
            }
            fx.Telegraph(inside, CrueWarnRadius, Enemy.Amber * 2f, duration);
        }

        /// <summary>A line of glowing golden zombies: big XP and gold if you catch them all.</summary>
        void SpawnGoldenWave(float minutes)
        {
            int count = Mathf.Min(goldenCount, hardCap - Enemy.Active.Count);
            if (count <= 0) return;

            var r = SpawnRect(spawnMargin);
            float perimeter = 2f * (r.width + r.height);
            float start = Random.value * perimeter;
            for (int i = 0; i < count; i++)
                Spawn(goldenEnemy, PerimeterPoint(r, start + i * 1.2f), minutes, 1f);

            GameHUD.Instance.ShowBanner("VEINE D'AMBRE !", UIKit.Gold, 1.8f);
            SFX.Play(SfxId.Alert, 0.9f, 1.1f);
        }

        /// <summary>Spawns on screen, then runs away with the loot. Catch it before it escapes!</summary>
        void SpawnThief(float minutes)
        {
            var player = PlayerController.Instance;
            var view = CameraFollow.Instance;
            var r = ViewRect(-1.5f);
            Vector3 at = player.Position + Vector3.forward * 5f;
            for (int tries = 0; tries < 16; tries++)
            {
                var p = new Vector3(Random.Range(r.xMin, r.xMax), 0f, Random.Range(r.yMin, r.yMax));
                if ((p - player.Position).sqrMagnitude < 16f) continue;
                // The ground rect's bottom corners are off the picture: really on screen, or try again.
                if (view && !view.IsOnScreen(p, 0.1f)) continue;
                if (SpawnSafety.Blocked(p, thiefEnemy.hitRadius)) continue;
                at = p;
                break;
            }
            at.y = 0f;

            var thief = Spawn(thiefEnemy, at, minutes, 1f);
            if (thief) at = thief.transform.position;
            GameHUD.Instance.ShowBanner("PILLEUR !\n<size=50>Attrape-le avant qu'il file</size>", UIKit.Gold, 2f);
            SFX.Play(SfxId.Alert, 1f, 1.2f);
            if (GameFX.Instance) GameFX.Instance.Ring(at, 2.5f, new Color(2.5f, 2f, 0.6f), 0.5f);
        }

        void SpawnBoss(float minutes)
        {
            if (!boss) return;

            bossCount++;
            float healthBonus = 1f + bossHealthGrowth * (bossCount - 1);
            CurrentBoss = Spawn(boss, EdgePointToward(PlayerController.Instance.Facing, spawnMargin + bossSpawnExtra), minutes, healthBonus);

            string name = string.IsNullOrEmpty(boss.displayName) ? "Une Souche" : "Le " + boss.displayName;
            GameHUD.Instance.ShowBanner($"SOUCHE !\n<size=56>{name} arrive</size>", UIKit.Gold, 2.5f);
            GameHUD.Instance.ScreenFlash(new Color(0.9f, 0.05f, 0.05f), 0.45f);
            SFX.Play(SfxId.BossRoar);
            SFX.Play(SfxId.Horde, 0.6f, 0.8f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.5f);
        }

        // ---------- Spawning ----------

        Enemy Spawn(EnemyData data, Vector3 position, float minutes, float extraHealth, bool elite = false)
        {
            data = CapShooters(data);
            if (!data || !data.prefab) return null;
            position = SafeSpawnPoint(position, data.hitRadius);
            var enemy = Pool.Instance.Spawn<Enemy>(data.prefab, position, Quaternion.identity);
            enemy.Init(data,
                HealthMult(minutes) * extraHealth * RunConfig.EnemyHealthMult,
                DamageMult(minutes) * RunConfig.EnemyDamageMult,
                XPMult(minutes), elite);
            return enemy;
        }

        /// <summary>Spawns an enemy right now with the current run scaling (used by arena events).</summary>
        public Enemy SpawnAt(EnemyData data, Vector3 position, bool elite = false)
        {
            position.y = 0f;
            return Spawn(data, position, Minutes, 1f, elite);
        }

        /// <summary>
        /// Spawns next frame with the given (already scaled) multipliers. Used by splitters, so it is safe
        /// to call while weapons are looping over the enemy list.
        /// </summary>
        public void QueueSpawn(EnemyData data, Vector3 position, float healthMult, float damageMult, float xpMult, Vector3 push)
        {
            if (!data) return;
            position.y = 0f;
            queued.Add(new QueuedSpawn { data = data, position = position, push = push, health = healthMult, damage = damageMult, xp = xpMult });
        }

        /// <summary>
        /// Like <see cref="QueueSpawn"/>, but telegraphed: a ground circle in <paramref name="color"/> fills where it
        /// will crawl out, and it spawns <paramref name="delay"/> seconds later (summons: nothing pops out of thin air).
        /// </summary>
        public void QueueSpawnWarned(EnemyData data, Vector3 position, float healthMult, float damageMult, float xpMult,
            Vector3 push, float delay, Color color)
        {
            if (!data) return;
            position = SafeSpawnPoint(position, data.hitRadius);
            if (GameFX.Instance) GameFX.Instance.Telegraph(position, Mathf.Max(0.7f, data.hitRadius + 0.4f), color, delay);
            queued.Add(new QueuedSpawn
            {
                data = data, position = position, push = push, health = healthMult, damage = damageMult, xp = xpMult,
                at = Time.time + delay,
            });
        }

        /// <summary><see cref="QueueSpawnWarned"/> with the current run scaling (ritual summons, boss escort).</summary>
        public void SpawnWarned(EnemyData data, Vector3 position, float delay, Color color)
        {
            float m = Minutes;
            QueueSpawnWarned(data, position, HealthMult(m) * RunConfig.EnemyHealthMult, DamageMult(m) * RunConfig.EnemyDamageMult,
                XPMult(m), Vector3.zero, delay, color);
        }

        void SpawnQueued()
        {
            if (queued.Count == 0) return;
            float now = Time.time;
            var player = PlayerController.Instance;
            int keep = 0;
            for (int i = 0; i < queued.Count; i++)
            {
                var q = queued[i];
                if (q.at > now)
                {
                    queued[keep++] = q;
                    continue;
                }
                var data = CapShooters(q.data);
                if (!data || !data.prefab || Enemy.Active.Count >= hardCap) continue;
                Vector3 pos = q.relative && player ? player.Position + q.position : q.position;
                pos = SafeSpawnPoint(pos, data.hitRadius);
                var enemy = Pool.Instance.Spawn<Enemy>(data.prefab, pos, Quaternion.identity);
                enemy.Init(data, q.health, q.damage, q.xp, false);
                if (q.push != Vector3.zero) enemy.Push(q.push);
            }
            queued.RemoveRange(keep, queued.Count - keep);
        }

        bool ShootersCapped => Enemy.ShooterCount >= maxShooters;

        /// <summary>Past the shooter cap, a spitter becomes a runner (or a zombie): six on the field is readable, more is noise.</summary>
        EnemyData CapShooters(EnemyData data)
        {
            if (!data || data.behaviour != EnemyBehaviour.Ranged || !ShootersCapped) return data;
            return hordeFastEnemy ? hordeFastEnemy : hordeEnemy ? hordeEnemy : data;
        }

        /// <summary>
        /// A spawn point inside the playable map (the border barriers have no collider) and out of the decor
        /// (cars, containers, walls: see <see cref="SpawnSafety"/>).
        /// </summary>
        public static Vector3 SafeSpawnPoint(Vector3 p, float radius)
        {
            p.y = 0f;
            p = ClampToMap(p);
            return ClampToMap(SpawnSafety.FreePoint(p, radius));
        }

        static Vector3 ClampToMap(Vector3 p)
        {
            var player = PlayerController.Instance;
            float m = (player ? player.mapHalfSize : 190f) - 1f;
            p.x = Mathf.Clamp(p.x, -m, m);
            p.z = Mathf.Clamp(p.z, -m, m);
            return p;
        }

        // ---------- Positions around the visible screen area ----------

        /// <summary>
        /// <see cref="ViewRect"/> for spawning off-screen: positive margins get <see cref="farSpawnExtra"/> more on the far
        /// (top) side. Negative margins (points on screen) are left alone.
        /// </summary>
        Rect SpawnRect(float margin)
        {
            var r = ViewRect(margin);
            if (margin > 0f) r.yMax += farSpawnExtra;
            return r;
        }

        /// <summary>Ground area on screen, grown by <paramref name="margin"/> meters on every side (negative shrinks it).</summary>
        public static Rect ViewRect(float margin)
        {
            Rect r;
            if (CameraFollow.Instance && CameraFollow.Instance.GroundRect.width > 0f)
            {
                r = CameraFollow.Instance.GroundRect;
            }
            else
            {
                // No camera info yet: assume a 12x20 m portrait view around the player.
                Vector3 p = PlayerController.Instance ? PlayerController.Instance.Position : Vector3.zero;
                r = new Rect(p.x - 6f, p.z - 10f, 12f, 20f);
            }
            return new Rect(r.xMin - margin, r.yMin - margin, r.width + 2f * margin, r.height + 2f * margin);
        }

        /// <summary>Walks <paramref name="distance"/> meters clockwise along the rect's edge, starting top-left.</summary>
        static Vector3 PerimeterPoint(Rect r, float distance)
        {
            float w = r.width, h = r.height;
            float d = Mathf.Repeat(distance, 2f * (w + h));
            if (d < w) return new Vector3(r.xMin + d, 0f, r.yMax);
            d -= w;
            if (d < h) return new Vector3(r.xMax, 0f, r.yMax - d);
            d -= h;
            if (d < w) return new Vector3(r.xMax - d, 0f, r.yMin);
            d -= w;
            return new Vector3(r.xMin, 0f, r.yMin + d);
        }

        /// <summary>Random spot just off-screen (any side).</summary>
        public Vector3 RandomEdgePoint(float margin)
        {
            var r = SpawnRect(margin);
            return PerimeterPoint(r, Random.value * 2f * (r.width + r.height));
        }

        /// <summary>Spot just off-screen in a given direction from the screen center (negative margin: on screen).</summary>
        public Vector3 EdgePointToward(Vector3 direction, float margin)
        {
            var r = SpawnRect(margin);
            Vector2 c = r.center;
            Vector2 d = new(direction.x, direction.z);
            if (d.sqrMagnitude < 0.0001f) d = Vector2.up;
            d.Normalize();

            float tx = Mathf.Abs(d.x) > 0.0001f ? (d.x > 0f ? r.xMax - c.x : r.xMin - c.x) / d.x : float.MaxValue;
            float ty = Mathf.Abs(d.y) > 0.0001f ? (d.y > 0f ? r.yMax - c.y : r.yMin - c.y) / d.y : float.MaxValue;
            Vector2 hit = c + d * Mathf.Min(tx, ty);
            return new Vector3(hit.x, 0f, hit.y);
        }

        /// <summary>Weighted pick among the active entries; shooters are left out of the draw while capped.</summary>
        SpawnEntry Pick(float time)
        {
            float minutes = time / 60f;
            bool noShooters = ShootersCapped;
            float total = 0f;
            foreach (var e in entries)
                if (Eligible(e, time, noShooters)) total += e.WeightAt(minutes) * DangerWeight(e.enemy);
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (var e in entries)
            {
                if (!Eligible(e, time, noShooters)) continue;
                roll -= e.WeightAt(minutes) * DangerWeight(e.enemy);
                if (roll <= 0f) return e;
            }
            return null;
        }

        static bool Eligible(SpawnEntry e, float time, bool noShooters) =>
            e.enemy && time >= e.startTime && time < e.endTime && !(noShooters && e.enemy.behaviour == EnemyBehaviour.Ranged);
    }
}
