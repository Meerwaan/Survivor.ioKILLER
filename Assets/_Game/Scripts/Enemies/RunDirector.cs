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
        [Tooltip("Endless only: extra toughness so a strong build still falls around 15-20 min.")]
        public float endlessHealthPerMinuteSquared = 0.04f;
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
            public Vector3 position, push;
            public float health, damage, xp;
        }

        readonly List<QueuedSpawn> queued = new(32);

        float pending;
        float nextBoss, nextElite, nextRats, nextGolden, nextThief;
        float lastEvent = -99f;
        int bossCount, lastPeakWave;

        void Awake() => Instance = this;

        void Start()
        {
            Threats.ApplyToDirector(this); // ranked threats: scales this run's boss / elite timers (defaults untouched)
            nextBoss = firstBossTime;
            nextElite = firstEliteTime;
            nextRats = firstRatTime;
            nextGolden = firstGoldenTime;
            nextThief = firstThiefTime;
            lastPeakWave = 0; // wave 0's peak is stream only (no horde in the first minute)
        }

        public float HealthMult(float minutes) => 1f + healthPerMinute * minutes
            + (healthPerMinuteSquared + (RunConfig.IsCampaign ? 0f : endlessHealthPerMinuteSquared)) * minutes * minutes;
        public float DamageMult(float minutes) => 1f + (damagePerMinute + (RunConfig.IsCampaign ? 0f : endlessDamagePerMinute)) * minutes;
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
                    if (WaveIndex % 2 == 1) SpawnHorde(minutes);
                    else SpawnAssault(minutes);
                }
            }

            // ---- Elites: any phase but the peak (never stacked on a horde), pushed to the lull if needed. ----
            if (time >= nextElite && Phase != WavePhase.Peak)
            {
                nextElite = Mathf.Max(nextElite + eliteInterval, time + eliteInterval * 0.5f);
                SpawnElite(time, minutes);
            }

            // ---- Boss: on its timer (Campaign reads firstBossTime / bossInterval for the boss star). ----
            if (time >= nextBoss)
            {
                nextBoss += bossInterval;
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
            float build = Mathf.Clamp(buildTime, 1f, length);
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
            GameHUD.Instance.ShowBanner("ÉLITE !\n<size=50>Butin en approche</size>", UIKit.Gold, 1.4f);
            SFX.Play(SfxId.Alert, 0.7f, 0.85f);
        }

        void SpawnHorde(float minutes)
        {
            if (!hordeEnemy) return;

            // Evenly spread all around the screen edges: the player is surrounded whatever the orientation.
            int count = Mathf.RoundToInt((hordeBaseCount + hordeCountPerMinute * minutes) * RunConfig.SpawnRateMult);
            count = Mathf.Min(count, hordeMaxCount, hardCap - Enemy.Active.Count);
            if (count <= 0) return;

            var r = ViewRect(spawnMargin);
            float perimeter = 2f * (r.width + r.height);
            float offset = Random.value * perimeter;
            for (int i = 0; i < count; i++)
            {
                var data = hordeEnemy;
                if (hordeFastEnemy && minutes >= 2f && i % 3 == 0) data = hordeFastEnemy;
                if (hordeHeavyEnemy && minutes >= hordeHeavyFromMinute && i % 9 == 4) data = hordeHeavyEnemy;
                if (hordeKamikaze && minutes >= 3f && i % 11 == 5) data = hordeKamikaze;
                Spawn(data, PerimeterPoint(r, offset + perimeter * i / count), minutes, 1f);
            }

            GameHUD.Instance.ShowBanner("HORDE !", UIKit.Blood);
            SFX.Play(SfxId.Horde);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.25f);
        }

        /// <summary>
        /// A dense mixed column charging from where the player is heading: runners in front, brutes and spitters
        /// behind them. Can't be dodged by running away, has to be cut through.
        /// </summary>
        void SpawnAssault(float minutes)
        {
            if (!hordeEnemy) return;

            int count = Mathf.RoundToInt((assaultBaseCount + assaultCountPerMinute * minutes) * RunConfig.SpawnRateMult);
            count = Mathf.Min(count, assaultMaxCount, hardCap - Enemy.Active.Count);
            if (count <= 0) return;

            bool mixed = minutes >= assaultMixFromMinute;
            Vector3 facing = PlayerController.Instance.Facing;
            for (int i = 0; i < count; i++)
            {
                EnemyData data;
                switch (i % 4)
                {
                    case 2: data = mixed && hordeHeavyEnemy ? hordeHeavyEnemy : hordeEnemy; break;
                    case 3: data = mixed && assaultRangedEnemy ? assaultRangedEnemy : hordeFastEnemy ? hordeFastEnemy : hordeEnemy; break;
                    default: data = hordeFastEnemy ? hordeFastEnemy : hordeEnemy; break;
                }
                float angle = count > 1 ? Mathf.Lerp(-50f, 50f, (float)i / (count - 1)) : 0f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * facing;
                Vector2 jitter = Random.insideUnitCircle * 0.8f;
                Vector3 at = EdgePointToward(dir, spawnMargin + Random.Range(0f, 2.5f)) + new Vector3(jitter.x, 0f, jitter.y);
                Spawn(data, at, minutes, 1f);
            }

            GameHUD.Instance.ShowBanner("ASSAUT !\n<size=50>Ils arrivent devant</size>", new Color(1f, 0.5f, 0.15f), 1.6f);
            SFX.Play(SfxId.Horde, 0.8f, 1.15f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
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
                Spawn(ratEnemy, center + new Vector3(c.x, 0f, c.y), minutes, 1f);
            }

            GameHUD.Instance.ShowBanner("LES RATS !", new Color(0.85f, 0.75f, 0.6f), 1.6f);
            SFX.Play(SfxId.Squeak);
            SFX.Play(SfxId.Alert, 0.6f, 1.3f);
        }

        /// <summary>A line of glowing golden zombies: big XP and gold if you catch them all.</summary>
        void SpawnGoldenWave(float minutes)
        {
            int count = Mathf.Min(goldenCount, hardCap - Enemy.Active.Count);
            if (count <= 0) return;

            var r = ViewRect(spawnMargin);
            float perimeter = 2f * (r.width + r.height);
            float start = Random.value * perimeter;
            for (int i = 0; i < count; i++)
                Spawn(goldenEnemy, PerimeterPoint(r, start + i * 1.2f), minutes, 1f);

            GameHUD.Instance.ShowBanner("VAGUE DORÉE !", UIKit.Gold, 1.8f);
            SFX.Play(SfxId.Alert, 0.9f, 1.1f);
        }

        /// <summary>Spawns on screen, then runs away with the loot. Catch it before it escapes!</summary>
        void SpawnThief(float minutes)
        {
            var player = PlayerController.Instance;
            var r = ViewRect(-1.5f);
            Vector3 at = player.Position + Vector3.forward * 5f;
            for (int tries = 0; tries < 10; tries++)
            {
                var p = new Vector3(Random.Range(r.xMin, r.xMax), 0f, Random.Range(r.yMin, r.yMax));
                if ((p - player.Position).sqrMagnitude < 16f) continue;
                at = p;
                break;
            }
            at.y = 0f;

            Spawn(thiefEnemy, at, minutes, 1f);
            GameHUD.Instance.ShowBanner("PILLEUR !\n<size=50>Attrape-le avant qu'il file</size>", UIKit.Gold, 2f);
            SFX.Play(SfxId.Alert, 1f, 1.2f);
            if (GameFX.Instance) GameFX.Instance.Ring(at, 2.5f, new Color(2.5f, 2f, 0.6f), 0.5f);
        }

        void SpawnBoss(float minutes)
        {
            if (!boss) return;

            bossCount++;
            float healthBonus = 1f + bossHealthGrowth * (bossCount - 1);
            CurrentBoss = Spawn(boss, EdgePointToward(PlayerController.Instance.Facing, spawnMargin + 2f), minutes, healthBonus);

            string name = string.IsNullOrEmpty(boss.displayName) ? "Le boss" : "Le " + boss.displayName;
            GameHUD.Instance.ShowBanner($"BOSS !\n<size=56>{name} arrive</size>", UIKit.Gold, 2.5f);
            GameHUD.Instance.ScreenFlash(new Color(0.9f, 0.05f, 0.05f), 0.45f);
            SFX.Play(SfxId.BossRoar);
            SFX.Play(SfxId.Horde, 0.6f, 0.8f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.5f);
        }

        // ---------- Spawning ----------

        Enemy Spawn(EnemyData data, Vector3 position, float minutes, float extraHealth, bool elite = false)
        {
            if (!data || !data.prefab) return null;
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

        void SpawnQueued()
        {
            if (queued.Count == 0) return;
            for (int i = 0; i < queued.Count; i++)
            {
                var q = queued[i];
                if (!q.data.prefab || Enemy.Active.Count >= hardCap) continue;
                var enemy = Pool.Instance.Spawn<Enemy>(q.data.prefab, q.position, Quaternion.identity);
                enemy.Init(q.data, q.health, q.damage, q.xp, false);
                enemy.Push(q.push);
            }
            queued.Clear();
        }

        // ---------- Positions around the visible screen area ----------

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
            var r = ViewRect(margin);
            return PerimeterPoint(r, Random.value * 2f * (r.width + r.height));
        }

        /// <summary>Spot just off-screen in a given direction from the screen center.</summary>
        public Vector3 EdgePointToward(Vector3 direction, float margin)
        {
            var r = ViewRect(margin);
            Vector2 c = r.center;
            Vector2 d = new(direction.x, direction.z);
            if (d.sqrMagnitude < 0.0001f) d = Vector2.up;
            d.Normalize();

            float tx = Mathf.Abs(d.x) > 0.0001f ? (d.x > 0f ? r.xMax - c.x : r.xMin - c.x) / d.x : float.MaxValue;
            float ty = Mathf.Abs(d.y) > 0.0001f ? (d.y > 0f ? r.yMax - c.y : r.yMin - c.y) / d.y : float.MaxValue;
            Vector2 hit = c + d * Mathf.Min(tx, ty);
            return new Vector3(hit.x, 0f, hit.y);
        }

        SpawnEntry Pick(float time)
        {
            float minutes = time / 60f;
            float total = 0f;
            foreach (var e in entries)
                if (e.enemy && time >= e.startTime && time < e.endTime) total += e.WeightAt(minutes);
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (var e in entries)
            {
                if (!e.enemy || time < e.startTime || time >= e.endTime) continue;
                roll -= e.WeightAt(minutes);
                if (roll <= 0f) return e;
            }
            return null;
        }
    }
}
