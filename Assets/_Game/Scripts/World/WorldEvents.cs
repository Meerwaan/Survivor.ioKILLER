using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// The bigger events, on top of the RunDirector's waves (build 30 s, peak 12 s, lull 8 s). Two layers:
    ///
    /// SPICE (random, tied to the wave phases, same odds for everyone):
    /// - AUTEL DES PACTES at the start of a lull (from ~1:30, then every 2-3 waves): two altars, each a pact
    ///   (big bonus + a curse for the rest of the run). Walk into one, or let them crumble after 25 s.
    /// - At the start of a build phase, alternating (from ~2:30, then every ~90 s):
    ///   ZONE MAUDITE (stand in a circle 10 s while zombies crawl out of its edge: a relic) and
    ///   CHASSE À LA PRIME (a tough marked elite to kill in 35 s: a relic and gold).
    /// - Other lulls show a small "ACCALMIE" cue, or the supply crate (ArenaEvents).
    ///
    /// PRESSURE (the owner's rule: "if it's too easy, if he doesn't even need to move, make him move"):
    /// when <see cref="ComfortMeter.Value"/> stays at or above <see cref="comfortHigh"/> for <see cref="comfortHold"/> s
    /// (not during a lull, from 1:00), an event that forces movement is sent. Each one sent while the player stays
    /// comfortable raises the pressure level (1..3: stronger, then combined); comfort below <see cref="comfortLow"/>
    /// for <see cref="relaxTime"/> s lowers it again. A struggling player gets no pressure at all.
    /// - Camping (barely moving): SOL BRÛLANT (fire patches drop on you), ZONE SÛRE (shrinking, drifting circle, outside hurts),
    ///   level 3: MALÉDICTION DU CAMPEUR (damage ramps while you stand still) + another one.
    /// - Moving but dominating: MÉTÉORES (aimed where you are going), EMBUSCADE (pack from behind), FUYARD (thief to chase),
    ///   ZONE SÛRE. Everything is telegraphed (circle, sound, banner) and dodgeable.
    /// One event at a time. Spawns go through RunDirector.SpawnAt (its numbers are untouched).
    /// Added at runtime by ArenaEvents.
    /// </summary>
    public class WorldEvents : MonoBehaviour
    {
        public static WorldEvents Instance { get; private set; }

        enum Kind { None, Altar, Zone, Bounty, Meteors, Burning, SafeZone, Ambush, Runner, Curse }

        [Header("Spice")]
        public float altarFirst = 90f;
        public float altarEvery = 110f;
        public float altarLifetime = 25f;
        public float buildEventFirst = 140f;
        public float buildEventEvery = 90f;
        [Tooltip("Waits until no announcement banner has been shown for this long.")]
        public float quietGap = 5f;

        [Header("Pressure (comfort driven)")]
        public float pressureUnlock = 150f;
        [Range(0f, 1f)] public float comfortHigh = 0.66f;
        [Tooltip("Seconds of high comfort before a pressure event.")]
        public float comfortHold = 14f;
        [Range(0f, 1f)] public float comfortLow = 0.35f;
        [Tooltip("Seconds of low comfort to drop one pressure level.")]
        public float relaxTime = 12f;
        public float pressureCooldown = 20f;

        [Header("Meteors")]
        public float meteorDuration = 9f;
        public float meteorsPerSecond = 1.8f;
        public float meteorRadius = 2.1f;
        public float meteorTelegraph = 1.4f;
        [Tooltip("Times the enemy damage scaling.")]
        public float meteorPlayerDamage = 9f;
        [Tooltip("Times the enemy health scaling: kills normal enemies outright.")]
        public float meteorEnemyDamage = 40f;

        [Header("Burning ground")]
        public float burnDuration = 12f;
        public float burnPatchEvery = 1.1f;
        public float burnPatchRadius = 1.7f;
        public float burnPatchLife = 6f;
        public float burnTelegraph = 0.9f;
        [Tooltip("Per tick (0.5 s), times the enemy damage scaling.")]
        public float burnDamage = 3f;

        [Header("Safe zone")]
        public float safeDuration = 15f;
        public float safeGrace = 3f;
        public float safeStartRadius = 6.5f;
        public float safeEndRadius = 2.6f;
        public float safeDrift = 0.6f;
        [Tooltip("Per tick (0.5 s) outside, times the enemy damage scaling.")]
        public float safeDamage = 3f;

        [Header("Ambush / runner / curse")]
        public int ambushBase = 6;
        public float ambushPerMinute = 4f;
        public float ambushDelay = 1.4f;
        public float curseDuration = 18f;
        [Tooltip("Per tick (0.5 s) per stack, times the enemy damage scaling.")]
        public float curseDamagePerStack = 1.2f;

        [Header("Cursed zone (spice)")]
        public float zoneRadius = 4.2f;
        public float zoneHoldTime = 10f;
        public float zoneTimeout = 40f;
        public float zoneSummonInterval = 2f;

        [Header("Bounty (spice)")]
        public float bountyTime = 35f;
        public float bountyHealthMult = 1.5f;

        /// <summary>Tir du Cordon: white aim circles (#F2F2EE), never orange (Univers 9.9: no lava).</summary>
        public static readonly Color MeteorColor = new(0.949f, 0.949f, 0.933f);
        static readonly Color ShellColor = new(1.6f, 1.55f, 1.45f);
        static readonly Color ShellSmoke = new(0.45f, 0.44f, 0.42f, 0.35f);
        /// <summary>Where a shell comes from, relative to its impact point (steep, slightly from the side).</summary>
        static readonly Vector3 ShellFall = new(2f, 13f, 0f);
        public static readonly Color ZoneColor = new(0.7f, 0.25f, 1f);
        public static readonly Color AltarColor = new(1f, 0.15f, 0.3f);
        public static readonly Color FireColor = new(1f, 0.35f, 0.05f);
        public static readonly Color SafeColor = new(0.35f, 1f, 0.75f);
        public static readonly Color CurseColor = new(0.8f, 0.2f, 1f);

        /// <summary>Line under the top HUD while an event runs (null when nothing runs).</summary>
        public string StatusText { get; private set; }
        public Color StatusColor { get; private set; } = Color.white;
        public bool StatusUrgent { get; private set; }
        /// <summary>Current pressure level 0..3 (for tuning / the debug readout).</summary>
        public int PressureLevel { get; private set; }

        // ---------- Read-only queries (test bot, future AI helpers). They never change the events. ----------

        /// <summary>The "ZONE SÛRE" circle the player must stay in, if one is up.</summary>
        public bool TryGetSafeZone(out Vector3 center, out float radius)
        {
            center = safe.center;
            radius = safeRadius;
            return safe.active;
        }

        /// <summary>The "ZONE MAUDITE" circle (stand in it for a relic), if one is up.</summary>
        public bool TryGetCursedZone(out Vector3 center)
        {
            center = zone.center;
            return zone.active;
        }

        /// <summary>Telegraphed ground hazards (incoming meteors, live fire patches): xyz = position, w = radius.</summary>
        public void GetHazards(List<Vector4> into)
        {
            into.Clear();
            for (int i = 0; i < meteors.Count; i++)
                into.Add(new Vector4(meteors[i].position.x, 0f, meteors[i].position.z, meteors[i].radius));
            if (patches == null) return;
            foreach (var p in patches)
                if (p != null && p.live) into.Add(new Vector4(p.position.x, 0f, p.position.z, p.radius));
        }

        Kind active, second;
        float eventEnd, nextAltar, nextBuildEvent, nextPressure, highTimer, lowTimer;
        bool zoneNext = true;
        int shownA = -1, shownB = -1;
        float tick;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (RunDirector.Instance) RunDirector.Instance.PhaseChanged -= OnPhaseChanged;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            nextAltar = altarFirst;
            nextBuildEvent = buildEventFirst;
            BuildMeteorPool();
            if (RunDirector.Instance) RunDirector.Instance.PhaseChanged += OnPhaseChanged;
        }

        static float Now => GameManager.Instance ? GameManager.Instance.RunTime : 0f;
        static bool Playing => GameManager.Instance && GameManager.Instance.State == GameState.Playing && PlayerController.Instance && RunDirector.Instance;

        static bool BossAlive
        {
            get
            {
                var boss = RunDirector.Instance ? RunDirector.Instance.CurrentBoss : null;
                return boss && boss.gameObject.activeSelf && !boss.IsDead;
            }
        }

        /// <summary>Enemy damage scaling, for the hazards that hurt the player.</summary>
        static float DamageScale
        {
            get
            {
                var d = RunDirector.Instance;
                return (d ? d.DamageMult(d.Minutes) : 1f) * RunConfig.EnemyDamageMult;
            }
        }

        // ================= Scheduling =================

        void OnPhaseChanged(int wave, WavePhase phase)
        {
            if (!Playing) return;
            float time = Now;
            bool quiet = !BossAlive && (!GameHUD.Instance || Time.time - GameHUD.Instance.LastAnnouncementTime >= quietGap);

            if (phase == WavePhase.Lull)
            {
                if (active == Kind.None && quiet && time >= nextAltar && StartAltar())
                {
                    Begin(Kind.Altar);
                    nextAltar = time + altarEvery;
                    return;
                }
                if (ArenaEvents.Instance && ArenaEvents.Instance.TrySupplyDrop()) return;
                if (wave >= 1 && !BossAlive && active == Kind.None && GameHUD.Instance)
                    GameHUD.Instance.ShowBanner("ACCALMIE", new Color(0.6f, 0.85f, 1f), 1.2f);
                return;
            }

            if (phase == WavePhase.Build && active == Kind.None && quiet && time >= nextBuildEvent &&
                (!ArenaEvents.Instance || !ArenaEvents.Instance.ChallengeActive))
            {
                bool ok = zoneNext ? StartZone() : StartBounty();
                if (!ok) ok = zoneNext ? StartBounty() : StartZone();
                if (!ok) return;
                Begin(zoneActive ? Kind.Zone : Kind.Bounty);
                zoneNext = !zoneActive;
                nextBuildEvent = time + buildEventEvery;
            }
        }

        void Update()
        {
            if (!Playing) return;
            float time = Now;
            float dt = Time.deltaTime;

            UpdateMeteors(dt);
            UpdatePatches(dt);
            UpdateComfort(time, dt);

            UpdateEvent(active, time, dt, true);
            if (second != Kind.None) UpdateEvent(second, time, dt, false);
        }

        void UpdateEvent(Kind kind, float time, float dt, bool main)
        {
            switch (kind)
            {
                case Kind.Altar: UpdateAltar(time); break;
                case Kind.Zone: UpdateZone(time, dt); break;
                case Kind.Bounty: UpdateBounty(time); break;
                case Kind.Meteors: UpdateMeteorShower(time, dt, main); break;
                case Kind.Burning: UpdateBurning(time, main); break;
                case Kind.SafeZone: UpdateSafeZone(time, dt, main); break;
                case Kind.Ambush: UpdateAmbush(time); break;
                case Kind.Runner: if (main) EndEvent(); break; // one-shot
                case Kind.Curse: UpdateCurse(time, dt, main); break;
            }
        }

        void UpdateComfort(float time, float dt)
        {
            float comfort = ComfortMeter.Value;
            bool lull = RunDirector.Instance.Phase == WavePhase.Lull; // the lull is a real breather: comfort there doesn't count
            if (comfort >= comfortHigh) { if (!lull) highTimer += dt; }
            else highTimer = Mathf.Max(0f, highTimer - dt * 2f);

            if (comfort < comfortLow)
            {
                lowTimer += dt;
                if (lowTimer >= relaxTime)
                {
                    lowTimer = 0f;
                    PressureLevel = Mathf.Max(0, PressureLevel - 1);
                }
            }
            else lowTimer = 0f;

            if (active != Kind.None || highTimer < comfortHold || time < nextPressure || time < pressureUnlock) return;
            var director = RunDirector.Instance;
            if (director.Phase == WavePhase.Lull || director.Phase == WavePhase.Peak) return;
            if (director.CurrentBoss) return; // the boss is already the pressure
            if (ArenaEvents.Instance && ArenaEvents.Instance.ChallengeActive) return;

            int level = Mathf.Min(3, PressureLevel + 1);
            if (!StartPressure(level)) return;
            PressureLevel = level;
            highTimer = 0f;
            nextPressure = time + pressureCooldown;
        }

        // Reused: StartPressure can be retried every frame while an event refuses to start (e.g. ambush at the enemy cap).
        readonly List<Kind> pressurePool = new(4);

        /// <summary>Picks a movement-forcing event for this player's way of being comfortable (uniform among the fitting ones).</summary>
        bool StartPressure(int level)
        {
            float intensity = 1f + 0.35f * (level - 1);
            var pool = pressurePool;
            pool.Clear();
            if (ComfortMeter.Camping)
            {
                pool.Add(Kind.Burning);
                pool.Add(Kind.SafeZone);
                if (level >= 2) pool.Add(Kind.Meteors);
            }
            else
            {
                pool.Add(Kind.Meteors);
                pool.Add(Kind.Ambush);
                if (RunDirector.Instance.thiefEnemy && level <= 2) pool.Add(Kind.Runner);
                if (level >= 2) pool.Add(Kind.SafeZone);
            }

            var kind = pool[Random.Range(0, pool.Count)];
            if (!StartKind(kind, intensity, true)) return false;
            Begin(kind);

            // Level 3: a camper also gets the curse on top; a dominating runner gets meteors on top of the rest.
            if (level >= 3)
            {
                var extra = ComfortMeter.Camping ? Kind.Curse : kind == Kind.Meteors ? Kind.Ambush : Kind.Meteors;
                if (StartKind(extra, intensity, false)) second = extra;
            }
            return true;
        }

        bool StartKind(Kind kind, float intensity, bool announce) => kind switch
        {
            Kind.Meteors => StartMeteors(intensity, announce),
            Kind.Burning => StartBurning(intensity, announce),
            Kind.SafeZone => StartSafeZone(intensity, announce),
            Kind.Ambush => StartAmbush(intensity, announce),
            Kind.Runner => StartRunner(),
            Kind.Curse => StartCurse(intensity, announce),
            _ => false,
        };

        void Begin(Kind kind)
        {
            active = kind;
            shownA = shownB = -1;
            tick = 0f;
        }

        /// <summary>
        /// Ends the main event. The extra one riding along (pressure level 3) keeps its own duration: it becomes the main
        /// event instead of being cut short (the 1.4 s ambush used to cancel its meteors, the 12 s fire its 18 s curse).
        /// </summary>
        void EndEvent()
        {
            if (second != Kind.None)
            {
                active = second;
                second = Kind.None;
                shownA = shownB = -1;
                return;
            }
            active = Kind.None;
            StatusText = null;
            StatusUrgent = false;
        }

        /// <summary>Cleans up an event's visuals.</summary>
        void StopKind(Kind kind)
        {
            switch (kind)
            {
                case Kind.SafeZone: CloseSafeZone(); break;
                case Kind.Curse: CloseCurse(); break;
            }
        }

        /// <summary>Rebuilds the status string only when the shown numbers change (no per-frame garbage).</summary>
        void SetStatus(int a, int b, string format, Color color, bool urgent)
        {
            StatusColor = color;
            StatusUrgent = urgent;
            if (a == shownA && b == shownB && StatusText != null) return;
            shownA = a;
            shownB = b;
            StatusText = string.Format(format, a, b);
        }

        static void Banner(string text, Color color, float duration)
        {
            if (GameHUD.Instance) GameHUD.Instance.ShowBanner(text, color, duration);
        }

        /// <summary>Random point on screen, between the two distances from the player.</summary>
        static Vector3 OnScreenPoint(float inset, float minDistance, float maxDistance)
        {
            var player = PlayerController.Instance;
            var r = RunDirector.ViewRect(-inset);
            for (int tries = 0; tries < 16; tries++)
            {
                var p = new Vector3(Random.Range(r.xMin, r.xMax), 0f, Random.Range(r.yMin, r.yMax));
                float d = (p - player.Position).magnitude;
                if (d < minDistance || d > maxDistance) continue;
                return p;
            }
            Vector3 best = player.Position + Vector3.forward * minDistance;
            best.y = 0f;
            return best;
        }

        static bool PlayerWithin(Vector3 center, float radius)
        {
            var player = PlayerController.Instance;
            Vector3 d = player.Position - center;
            d.y = 0f;
            float r = radius + player.Radius;
            return d.sqrMagnitude <= r * r;
        }

        static void HurtPlayer(float amount)
        {
            if (PlayerHealth.Instance && amount > 0f) PlayerHealth.Instance.TakeDamage(amount);
        }

        // ================= PACT ALTARS (spice, lull) =================

        readonly List<PactAltar> altars = new(2);

        bool StartAltar()
        {
            var mods = RunModifiers.Instance;
            if (!mods) return false;
            var pacts = mods.RollPacts(2);
            if (pacts.Count == 0) return false;

            var player = PlayerController.Instance;
            var r = RunDirector.ViewRect(0f);
            float side = Mathf.Min(3.2f, r.width * 0.3f);
            float ahead = Mathf.Min(2.6f, r.height * 0.15f);
            Vector3 c = player.Position + new Vector3(0f, 0f, ahead);
            altars.Clear();
            if (pacts.Count == 1) altars.Add(PactAltar.Create(pacts[0], SpawnSafety.FreePoint(ClampToMap(c + new Vector3(0f, 0f, 1.5f), 1f), 1.2f), AltarColor));
            else
            {
                altars.Add(PactAltar.Create(pacts[0], SpawnSafety.FreePoint(ClampToMap(c + new Vector3(-side, 0f, 0f), 1f), 1.2f), AltarColor));
                altars.Add(PactAltar.Create(pacts[1], SpawnSafety.FreePoint(ClampToMap(c + new Vector3(side, 0f, 0f), 1f), 1.2f), new Color(0.75f, 0.2f, 1f)));
            }
            eventEnd = Now + altarLifetime;

            var fx = GameFX.Instance;
            foreach (var a in altars)
            {
                if (!fx) break;
                fx.Ring(a.transform.position, 3f, AltarColor * 2.5f, 0.6f);
                fx.Sparks(a.transform.position + Vector3.up * 2f, AltarColor * 2f, 30, 1.5f);
            }
            Banner("AUTELS DES FERVENTS\n<size=48>Marche sur une offrande pour sceller un pacte</size>", new Color(1f, 0.4f, 0.45f), 2.6f);
            SFX.Play(SfxId.Bell, 1f, 0.85f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.15f);
            return true;
        }

        void UpdateAltar(float time)
        {
            for (int i = 0; i < altars.Count; i++)
            {
                var a = altars[i];
                if (!a || !a.Armed || !PlayerWithin(a.transform.position, a.TouchRadius)) continue;
                if (RunModifiers.Instance) RunModifiers.Instance.SealPact(a.Pact, a.transform.position);
                ClearAltars(a);
                EndEvent();
                return;
            }

            float left = eventEnd - time;
            if (left <= 0f)
            {
                ClearAltars(null);
                Banner("Les offrandes s'effondrent...", UIKit.Muted, 1.3f);
                SFX.Play(SfxId.Fail, 0.6f, 0.8f);
                EndEvent();
                return;
            }
            SetStatus(Mathf.CeilToInt(left), 0, "AUTELS DES FERVENTS  {0} s", new Color(1f, 0.45f, 0.5f), left < 6f);
        }

        void ClearAltars(PactAltar chosen)
        {
            foreach (var a in altars)
                if (a) a.Crumble(a == chosen);
            altars.Clear();
        }

        // ================= CURSED ZONE (spice, build) =================

        class ZoneTarget : IHudTarget
        {
            public Vector3 center;
            public bool active, inside;
            public string outsideLabel = "<size=40>CERCLE DES FERVENTS</size>\nentre dans le cercle";
            public string insideLabel = "<size=40>TIENS BON !</size>";
            public Color color = new(0.85f, 0.55f, 1f);
            public bool HudActive => active && (!inside || insideLabel.Length > 0);
            public Vector3 HudPosition => center + Vector3.up * 1.5f;
            public string HudLabel => inside ? insideLabel : outsideLabel;
            public Color HudColor => color;
            public bool HudArrow => true;
            public float HudScale => 1f;
        }

        readonly ZoneTarget zone = new();
        GroundRing zoneRing, zoneFill;
        float zoneProgress, nextZoneSummon;
        bool zoneActive;

        bool StartZone()
        {
            if (zoneRing == null)
            {
                zoneRing = new GroundRing("Zone Ring", transform);
                zoneFill = new GroundRing("Zone Fill", transform);
            }
            float time = Now;
            zone.center = OnScreenPoint(zoneRadius * 0.6f, 5f, 9f);
            zone.active = true;
            zone.inside = false;
            zoneActive = true;
            zoneProgress = 0f;
            nextZoneSummon = time + 1f;
            eventEnd = time + zoneTimeout;
            zoneRing.Show(zone.center, zoneRadius, ZoneColor * 1.6f);
            zoneFill.Show(zone.center, 0.01f, ZoneColor * 1.2f, 0.055f);
            RunHUD.Register(zone);

            if (GameFX.Instance)
            {
                GameFX.Instance.Ring(zone.center, zoneRadius * 1.5f, ZoneColor * 2.5f, 0.7f);
                GameFX.Instance.Embers(zone.center, zoneRadius * 0.8f, 20, ZoneColor * 2f);
            }
            Banner($"CERCLE DES FERVENTS !\n<size=48>Tiens le cercle {zoneHoldTime:0} s : vole leur échantillon</size>", new Color(0.8f, 0.5f, 1f), 2.6f);
            SFX.Play(SfxId.Alert, 1f, 0.7f);
            SFX.Play(SfxId.Bell, 0.7f, 1.2f);
            return true;
        }

        void UpdateZone(float time, float dt)
        {
            zone.inside = PlayerWithin(zone.center, zoneRadius - PlayerController.Instance.Radius);
            if (zone.inside)
            {
                zoneProgress += dt / Mathf.Max(1f, zoneHoldTime);
                // The curse fights back: zombies keep crawling out of the circle's edge.
                if (time >= nextZoneSummon)
                {
                    nextZoneSummon = time + zoneSummonInterval;
                    SummonAround(zone.center, zoneRadius + 1.2f);
                }
            }

            float pulse = 1f + Mathf.Sin(Time.time * (zone.inside ? 9f : 3f)) * 0.15f;
            zoneRing.SetColor(ZoneColor * (zone.inside ? 2.4f : 1.3f) * pulse);
            zoneFill.SetRadius(Mathf.Max(0.01f, zoneRadius * Mathf.Clamp01(zoneProgress)));
            if (GameFX.Instance && Random.value < (zone.inside ? 0.6f : 0.25f))
            {
                Vector2 c = Random.insideUnitCircle.normalized * zoneRadius;
                GameFX.Instance.Embers(zone.center + new Vector3(c.x, 0f, c.y), 0.3f, 1, ZoneColor * 2.2f);
            }

            float left = eventEnd - time;
            if (zoneProgress >= 1f)
            {
                CloseZone();
                RunModifiers.DropRelic(zone.center);
                if (Loot.Instance) Loot.Instance.DropCoins(zone.center + Vector3.up * 0.5f, 8, 2);
                Banner("RITUEL ROMPU !\n<size=50>Ramasse l'échantillon</size>", UIKit.Gold, 2f);
                if (GameHUD.Instance) GameHUD.Instance.ScreenFlash(new Color(0.8f, 0.6f, 1f), 0.4f);
                SFX.Play(SfxId.Success);
                if (GameFX.Instance) GameFX.Instance.Ring(zone.center, zoneRadius * 1.6f, new Color(2.5f, 2f, 3f), 0.6f);
                EndEvent();
                return;
            }
            if (left <= 0f)
            {
                CloseZone();
                Banner("CERCLE PERDU...", UIKit.Muted, 1.5f);
                SFX.Play(SfxId.Fail);
                EndEvent();
                return;
            }
            SetStatus(Mathf.FloorToInt(zoneProgress * 100f), Mathf.CeilToInt(left), "CERCLE DES FERVENTS  {0}%   {1} s", new Color(0.85f, 0.6f, 1f), left < 8f);
        }

        /// <summary>A ring of zombies (runners mixed in after 4 min) crawling out around a point, each one telegraphed
        /// by a violet ground circle a moment before (Bestiaire rule 1: nothing pops out of thin air).</summary>
        static void SummonAround(Vector3 center, float radius)
        {
            var director = RunDirector.Instance;
            if (!director || Enemy.Active.Count >= director.maxAlive - 10) return;
            float minutes = director.Minutes;
            int count = Mathf.Min(8, 3 + Mathf.FloorToInt(minutes * 0.4f));
            float offset = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                var data = director.hordeFastEnemy && minutes >= 4f && i % 2 == 1 ? director.hordeFastEnemy : director.hordeEnemy;
                if (!data) continue;
                Vector3 dir = Quaternion.Euler(0f, offset + 360f * i / count, 0f) * Vector3.forward;
                Vector3 p = center + dir * radius;
                director.SpawnWarned(data, p, Enemy.SummonWarn, ZoneColor * 2f);
                if (GameFX.Instance && i % 2 == 0) GameFX.Instance.Sparks(p + Vector3.up * 0.5f, ZoneColor * 2f, 4, 0.6f);
            }
        }

        void CloseZone()
        {
            zone.active = false;
            zoneActive = false;
            RunHUD.Unregister(zone);
            zoneRing.Hide();
            zoneFill.Hide();
        }

        // ================= BOUNTY (spice, build) =================

        Enemy bounty;

        bool StartBounty()
        {
            var director = RunDirector.Instance;
            float time = Now;
            EnemyData best = null;
            foreach (var e in director.entries)
            {
                if (!e.enemy || !e.enemy.canBeElite || time < e.startTime || time >= e.endTime) continue;
                if (e.enemy.behaviour != EnemyBehaviour.Melee) continue;
                if (!best || e.enemy.maxHealth > best.maxHealth) best = e.enemy;
            }
            if (!best) best = director.hordeEnemy;
            if (!best) return false;

            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var target = director.SpawnAt(best, director.EdgePointToward(dir, director.spawnMargin + 1f), true);
            if (!target) return false;
            target.MakeBounty(bountyHealthMult);
            bounty = target;
            eventEnd = time + bountyTime;

            Banner($"PRIME REGEN !\n<size=50>Abats l'Alpha marqué en {bountyTime:0} s</size>", UIKit.Gold, 2.4f);
            SFX.Play(SfxId.Alert, 1f, 1.1f);
            SFX.Play(SfxId.Bell, 0.6f, 1.5f);
            return true;
        }

        void UpdateBounty(float time)
        {
            if (!bounty || !bounty.gameObject.activeSelf || !bounty.IsBounty)
            {
                bounty = null;
                EndEvent();
                return;
            }
            float left = eventEnd - time;
            if (left <= 0f)
            {
                bounty.Vanish();
                bounty = null;
                Banner("LA CIBLE A FILÉ...", UIKit.Muted, 1.5f);
                SFX.Play(SfxId.Fail);
                EndEvent();
                return;
            }
            SetStatus(Mathf.CeilToInt(left), 0, "PRIME  {0} s", UIKit.Gold, left < 8f);
        }

        /// <summary>Called for every death (see RunModifiers.OnEnemyKilled).</summary>
        public void OnEnemyKilled(Enemy e)
        {
            if (active != Kind.Bounty || e != bounty) return;
            Vector3 p = e.transform.position;
            bounty = null;
            RunModifiers.DropRelic(p);
            if (Loot.Instance) Loot.Instance.DropCoins(p + Vector3.up * 0.5f, 12, 2);
            Banner("PRIME ENCAISSÉE !\n<size=50>Ramasse l'échantillon</size>", UIKit.Gold, 2f);
            if (GameHUD.Instance) GameHUD.Instance.ScreenFlash(new Color(1f, 0.85f, 0.4f), 0.4f);
            SFX.Play(SfxId.Success);
            EndEvent();
        }

        // ================= METEORS (pressure) =================

        struct Meteor
        {
            public Vector3 position;
            public float impactAt, radius;
            public int visual;
        }

        const int MeteorVisuals = 12;
        const float FallTime = 0.5f, FallHeight = 13f;
        readonly List<Meteor> meteors = new(32);
        Renderer[] meteorRenderers;
        bool[] visualBusy;
        float meteorSpawn, meteorEnd, meteorRate, meteorAimed;
        Vector3 lastPlayerPos, playerVelocity;

        void BuildMeteorPool()
        {
            meteorRenderers = new Renderer[MeteorVisuals];
            visualBusy = new bool[MeteorVisuals];
            for (int i = 0; i < MeteorVisuals; i++)
            {
                // A falling shell: a thin bright streak along its path.
                var r = RunProps.Primitive(PrimitiveType.Cylinder, "Shell", transform, RunProps.GlowMaterial);
                r.transform.localScale = new Vector3(0.12f, 0.9f, 0.12f);
                r.transform.rotation = Quaternion.FromToRotation(Vector3.up, ShellFall.normalized);
                RunProps.Tint(r, ShellColor);
                r.gameObject.SetActive(false);
                meteorRenderers[i] = r;
            }
        }

        bool StartMeteors(float intensity, bool announce)
        {
            meteorEnd = Now + meteorDuration;
            meteorRate = meteorsPerSecond * intensity;
            meteorAimed = Mathf.Clamp01(0.35f + 0.1f * (intensity - 1f) / 0.35f);
            meteorSpawn = 0.6f; // first circles almost right away
            lastPlayerPos = PlayerController.Instance.Position;
            playerVelocity = Vector3.zero;
            if (announce)
            {
                Banner("TIR DU CORDON !\n<size=50>Bouge ! Évite les cercles de visée</size>", new Color(1f, 0.55f, 0.2f), 2.2f);
                SFX.Play(SfxId.Alert, 1f, 0.8f);
                if (GameHUD.Instance) GameHUD.Instance.ScreenFlash(new Color(1f, 0.4f, 0.1f), 0.25f);
            }
            SFX.Play(SfxId.Whoosh, 1f, 0.7f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.2f);
            return true;
        }

        void UpdateMeteorShower(float time, float dt, bool main)
        {
            var player = PlayerController.Instance;
            if (dt > 0f)
            {
                Vector3 v = (player.Position - lastPlayerPos) / dt;
                playerVelocity = Vector3.Lerp(playerVelocity, v, Mathf.Clamp01(dt * 6f));
                lastPlayerPos = player.Position;
            }

            float left = meteorEnd - time;
            if (left > 0f)
            {
                meteorSpawn += meteorRate * dt;
                while (meteorSpawn >= 1f)
                {
                    meteorSpawn -= 1f;
                    SpawnMeteor(player);
                }
                if (main) SetStatus(Mathf.CeilToInt(left), 0, "TIR DU CORDON  {0} s", new Color(1f, 0.6f, 0.25f), false);
            }
            else if (main && meteors.Count == 0)
            {
                EndEvent();
            }
        }

        void SpawnMeteor(PlayerController player)
        {
            if (meteors.Count >= 30) return;
            Vector3 at;
            if (Random.value < meteorAimed)
            {
                // Aimed where the player will be: keep moving, and change direction!
                Vector3 lead = playerVelocity;
                lead.y = 0f;
                at = player.Position + Vector3.ClampMagnitude(lead, 6f) * (meteorTelegraph * 0.7f);
                Vector2 j = Random.insideUnitCircle * 0.8f;
                at += new Vector3(j.x, 0f, j.y);
            }
            else
            {
                var r = RunDirector.ViewRect(-0.5f);
                at = new Vector3(Random.Range(r.xMin, r.xMax), 0f, Random.Range(r.yMin, r.yMax));
            }
            at.y = 0f;
            float radius = meteorRadius * Random.Range(0.85f, 1.2f);
            meteors.Add(new Meteor { position = at, impactAt = Time.time + meteorTelegraph, radius = radius, visual = -1 });
            if (GameFX.Instance) GameFX.Instance.Telegraph(at, radius, MeteorColor * 1.3f, meteorTelegraph);
        }

        /// <summary>Runs even after the shower ends, so every telegraphed meteor lands.</summary>
        void UpdateMeteors(float dt)
        {
            if (meteors.Count == 0) return;
            float now = Time.time;
            var fx = GameFX.Instance;
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                var m = meteors[i];
                float until = m.impactAt - now;

                if (until <= FallTime && m.visual < 0)
                {
                    m.visual = TakeVisual();
                    if (m.visual >= 0) meteorRenderers[m.visual].gameObject.SetActive(true);
                    SFX.Play(SfxId.Whoosh, 0.6f, Random.Range(0.9f, 1.2f));
                }
                if (m.visual >= 0)
                {
                    float k = Mathf.Clamp01(1f - until / FallTime);
                    // Accelerates down a straight line; the streak's lower tip is the shell.
                    Vector3 p = m.position + ShellFall * (FallHeight / ShellFall.y) * (1f - k * k);
                    var t = meteorRenderers[m.visual].transform;
                    t.position = p + ShellFall.normalized * 0.9f;
                    if (fx && dt > 0f && Random.value < 0.4f) fx.Smoke(p + ShellFall.normalized * 1.4f, 0.05f, 1, 0.45f, ShellSmoke, 0.2f);
                }

                if (until > 0f)
                {
                    meteors[i] = m;
                    continue;
                }
                if (m.visual >= 0)
                {
                    meteorRenderers[m.visual].gameObject.SetActive(false);
                    visualBusy[m.visual] = false;
                }
                meteors.RemoveAt(i);
                Impact(m.position, m.radius);
            }
        }

        int TakeVisual()
        {
            for (int i = 0; i < visualBusy.Length; i++)
            {
                if (visualBusy[i]) continue;
                visualBusy[i] = true;
                return i;
            }
            return -1;
        }

        void Impact(Vector3 at, float radius)
        {
            var fx = GameFX.Instance;
            if (fx)
            {
                fx.DustBurst(at, radius); // grey dust, white pop, soot: no lava
            }
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.12f);
            // Meteors wreck enemies too: lure them into the circles.
            Combat.Area(at, radius, meteorEnemyDamage * RunModifiers.WorldScale, 10f, false);
            if (PlayerWithin(at, radius)) HurtPlayer(meteorPlayerDamage * DamageScale);
            if (Loot.Instance && Random.value < 0.25f) Loot.Instance.DropCoins(at + Vector3.up * 0.5f, 1, 2);
        }

        // ================= BURNING GROUND (pressure, campers) =================

        class Patch
        {
            public GroundRing ring, core;
            public Vector3 position;
            public float igniteAt, endAt, radius;
            public bool live, burning;
        }

        const int PatchCount = 14;
        Patch[] patches;
        float burnEnd, nextPatch, burnEvery, patchTick;

        Patch FreePatch()
        {
            if (patches == null)
            {
                patches = new Patch[PatchCount];
                for (int i = 0; i < PatchCount; i++)
                    patches[i] = new Patch { ring = new GroundRing("Fire Patch", transform), core = new GroundRing("Fire Core", transform) };
            }
            Patch oldest = patches[0];
            foreach (var p in patches)
            {
                if (!p.live) return p;
                if (p.endAt < oldest.endAt) oldest = p;
            }
            return oldest;
        }

        bool StartBurning(float intensity, bool announce)
        {
            float time = Now;
            burnEnd = time + burnDuration;
            burnEvery = burnPatchEvery / intensity;
            nextPatch = time + 0.3f;
            if (announce)
            {
                Banner("LARGAGE INCENDIAIRE !\n<size=50>L'hélico arrose là où tu restes</size>", new Color(1f, 0.5f, 0.15f), 2.2f);
                SFX.Play(SfxId.Alert, 1f, 0.9f);
            }
            SFX.Play(SfxId.Ignite, 1f, 0.7f);
            return true;
        }

        void UpdateBurning(float time, bool main)
        {
            float left = burnEnd - time;
            if (left <= 0f)
            {
                if (main) EndEvent();
                else if (second == Kind.Burning) second = Kind.None;
                return;
            }
            if (time >= nextPatch)
            {
                nextPatch = time + burnEvery;
                // Drops on the player, a little ahead: standing still = standing in fire.
                var player = PlayerController.Instance;
                Vector3 at = player.Position + player.Facing * 0.6f + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                SpawnPatch(at, burnPatchRadius * Random.Range(0.9f, 1.15f));
            }
            if (main) SetStatus(Mathf.CeilToInt(left), 0, "LARGAGE INCENDIAIRE  {0} s", new Color(1f, 0.55f, 0.2f), false);
        }

        void SpawnPatch(Vector3 at, float radius)
        {
            at.y = 0f;
            var p = FreePatch();
            p.position = at;
            p.radius = radius;
            p.igniteAt = Time.time + burnTelegraph;
            p.endAt = p.igniteAt + burnPatchLife;
            p.live = true;
            p.burning = false;
            p.ring.Hide();
            p.core.Hide();
            if (GameFX.Instance) GameFX.Instance.Telegraph(at, radius, FireColor * 2.2f, burnTelegraph);
        }

        /// <summary>Fire patches keep burning after the event ends.</summary>
        void UpdatePatches(float dt)
        {
            if (patches == null) return;
            float now = Time.time;
            patchTick += dt;
            bool doTick = patchTick >= 0.5f;
            if (doTick) patchTick = 0f;
            bool hurt = false;
            var fx = GameFX.Instance;

            foreach (var p in patches)
            {
                if (!p.live) continue;
                if (now >= p.endAt)
                {
                    p.live = false;
                    p.ring.Hide();
                    p.core.Hide();
                    continue;
                }
                if (now < p.igniteAt) continue;
                if (!p.burning)
                {
                    p.burning = true;
                    p.ring.Show(p.position, p.radius, FireColor * 2f);
                    p.core.Show(p.position, p.radius * 0.6f, FireColor * 1.5f, 0.055f);
                    if (fx) fx.Flash(p.position + Vector3.up * 0.3f, p.radius * 0.6f, FireColor * 2f, 0.2f);
                    SFX.Play(SfxId.Ignite, 0.5f, 1.2f);
                }
                float flicker = 1.6f + Mathf.Sin(now * 13f + p.radius * 7f) * 0.4f;
                p.ring.SetColor(FireColor * flicker);
                if (fx && Random.value < 0.35f) fx.Embers(p.position, p.radius * 0.8f, 1, FireColor * 2.2f);
                // Fire is always animated and always smokes (Univers 9.4).
                if (fx && Random.value < 0.35f)
                {
                    Vector2 c = Random.insideUnitCircle * p.radius * 0.75f;
                    fx.Flame(p.position + new Vector3(c.x, 0.15f, c.y), new Vector3(0f, Random.Range(1.2f, 2.2f), 0f),
                        Random.Range(0.35f, 0.55f), Random.Range(0.5f, 0.9f));
                }
                if (fx && Random.value < 0.05f) fx.Smoke(p.position, p.radius * 0.6f, 1, 1f + p.radius * 0.2f);
                if (!doTick) continue;
                if (!hurt && PlayerWithin(p.position, p.radius * 0.9f))
                {
                    hurt = true;
                    HurtPlayer(burnDamage * DamageScale);
                }
                Combat.Area(p.position, p.radius, 3f * RunModifiers.WorldScale, 0f, false);
            }
        }

        // ================= SAFE ZONE (pressure) =================

        readonly ZoneTarget safe = new()
        {
            outsideLabel = "<size=42>POCHE D'AIR</size>\nva dans le cercle !",
            insideLabel = "",
            color = new Color(0.45f, 1f, 0.8f),
        };
        GroundRing safeRing, safeInner;
        float safeStart, safeEnd, safeIntensity, safeRadius, safeTick;
        Vector3 safeVelocity;

        bool StartSafeZone(float intensity, bool announce)
        {
            if (safeRing == null)
            {
                safeRing = new GroundRing("Safe Ring", transform);
                safeInner = new GroundRing("Safe Inner", transform);
            }
            float time = Now;
            var player = PlayerController.Instance;
            // Never centered on the player: you have to walk to it.
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            safe.center = player.Position + dir * (safeStartRadius * 0.95f); // just outside: step in during the grace time
            safe.center = ClampToMap(safe.center, safeStartRadius);
            safe.active = true;
            safeStart = time;
            safeEnd = time + safeDuration;
            safeIntensity = intensity;
            safeRadius = safeStartRadius;
            safeTick = 0f;
            safeVelocity = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * safeDrift * intensity;
            safeRing.Show(safe.center, safeRadius, SafeColor * 2f);
            safeInner.Show(safe.center, safeRadius * 0.97f, SafeColor * 0.5f, 0.055f);
            RunHUD.Register(safe);
            if (announce)
            {
                Banner($"NUAGE DE SÈVE !\n<size=50>Réfugie-toi dans le cercle vert ({safeGrace:0} s)</size>", new Color(0.5f, 1f, 0.8f), 2.2f);
                SFX.Play(SfxId.Alert, 1f, 1.2f);
            }
            if (GameFX.Instance) GameFX.Instance.Ring(safe.center, safeRadius * 1.3f, SafeColor * 2.5f, 0.6f);
            return true;
        }

        void UpdateSafeZone(float time, float dt, bool main)
        {
            float left = safeEnd - time;
            if (left <= 0f)
            {
                CloseSafeZone();
                if (main)
                {
                    Banner("BRUME LEVÉE", new Color(0.5f, 1f, 0.8f), 1.2f);
                    EndEvent();
                }
                else if (second == Kind.SafeZone) second = Kind.None;
                return;
            }

            float k = Mathf.Clamp01((time - safeStart - safeGrace) / Mathf.Max(1f, safeDuration - safeGrace));
            safeRadius = Mathf.Lerp(safeStartRadius, safeEndRadius, k);
            if (time - safeStart > safeGrace) safe.center = ClampToMap(safe.center + safeVelocity * dt, safeRadius);
            safe.inside = PlayerWithin(safe.center, safeRadius - PlayerController.Instance.Radius * 2f);

            bool grace = time - safeStart < safeGrace;
            float blink = grace ? (Mathf.Repeat(time * 4f, 1f) < 0.5f ? 2.6f : 1.2f) : 2f;
            safeRing.Show(safe.center, safeRadius, (safe.inside ? SafeColor : new Color(1f, 0.3f, 0.25f)) * blink);
            safeInner.Show(safe.center, safeRadius * 0.97f, SafeColor * (safe.inside ? 0.6f : 0.25f), 0.055f);

            if (!grace && !safe.inside)
            {
                safeTick += dt;
                if (safeTick >= 0.5f)
                {
                    safeTick = 0f;
                    HurtPlayer(safeDamage * safeIntensity * DamageScale);
                }
            }
            else safeTick = 0f;

            if (main)
                SetStatus(Mathf.CeilToInt(left), 0, safe.inside ? "POCHE D'AIR  {0} s" : "DANS LA BRUME !  {0} s",
                    safe.inside ? new Color(0.5f, 1f, 0.8f) : new Color(1f, 0.4f, 0.3f), !safe.inside && !grace);
        }

        void CloseSafeZone()
        {
            safe.active = false;
            RunHUD.Unregister(safe);
            if (safeRing != null)
            {
                safeRing.Hide();
                safeInner.Hide();
            }
        }

        // ================= AMBUSH (pressure, dominating runner) =================

        float ambushAt;
        int ambushCount;
        Vector3 ambushDir;

        bool StartAmbush(float intensity, bool announce)
        {
            var director = RunDirector.Instance;
            if (!director.hordeEnemy || Enemy.Active.Count >= director.maxAlive - 15) return false;
            var player = PlayerController.Instance;
            // From where the player is NOT going.
            ambushDir = -player.Facing;
            ambushCount = Mathf.Min(50, Mathf.RoundToInt((ambushBase + ambushPerMinute * director.Minutes) * intensity));
            ambushAt = Now + ambushDelay;
            if (announce)
            {
                Banner("EMBUSCADE !\n<size=50>Ils arrivent dans ton dos</size>", UIKit.Blood, 1.8f);
                SFX.Play(SfxId.Alert, 1f, 0.75f);
            }
            if (GameFX.Instance)
            {
                Vector3 edge = director.EdgePointToward(ambushDir, -1.5f);
                GameFX.Instance.Telegraph(edge, 3f, new Color(2.5f, 0.3f, 0.2f), ambushDelay);
            }
            return true;
        }

        void UpdateAmbush(float time)
        {
            if (ambushCount <= 0)
            {
                if (active == Kind.Ambush) EndEvent();
                else if (second == Kind.Ambush) second = Kind.None;
                return;
            }
            if (time < ambushAt) return;

            var director = RunDirector.Instance;
            float minutes = director.Minutes;
            Vector3 side = Vector3.Cross(Vector3.up, ambushDir).normalized;
            // Two thirds from behind, the rest split on the flanks: a pincer.
            for (int i = 0; i < ambushCount; i++)
            {
                if (Enemy.Active.Count >= director.hardCap) break;
                int group = i % 3;
                Vector3 dir = group == 0 || group == 1 ? ambushDir : (i % 2 == 0 ? side : -side) * 0.8f + ambushDir * 0.6f;
                Vector3 at = director.EdgePointToward(dir, director.spawnMargin);
                Vector2 j = Random.insideUnitCircle * 2.2f;
                var data = director.hordeFastEnemy && (minutes >= 1.5f && i % 2 == 0) ? director.hordeFastEnemy : director.hordeEnemy;
                director.SpawnAt(data, at + new Vector3(j.x, 0f, j.y));
            }
            ambushCount = 0;
            SFX.Play(SfxId.Horde, 0.6f, 1.15f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.15f);
        }

        /// <summary>Keeps a zone center inside the walkable map (PlayerController.mapHalfSize), radius included.</summary>
        static Vector3 ClampToMap(Vector3 p, float radius)
        {
            float half = PlayerController.Instance ? PlayerController.Instance.mapHalfSize : 190f;
            float m = Mathf.Max(0f, half - radius - 0.5f);
            return new Vector3(Mathf.Clamp(p.x, -m, m), 0f, Mathf.Clamp(p.z, -m, m));
        }

        // ================= RUNNER (pressure: a reward to chase) =================

        bool StartRunner()
        {
            var director = RunDirector.Instance;
            if (!director.thiefEnemy) return false;
            Vector3 at = OnScreenPoint(1.5f, 4f, 8f);
            var thief = director.SpawnAt(director.thiefEnemy, at);
            if (!thief) return false;
            Banner("UN PORTEUR !\n<size=50>Cours-lui après, il porte une caisse</size>", UIKit.Gold, 2f);
            SFX.Play(SfxId.Alert, 1f, 1.25f);
            if (GameFX.Instance) GameFX.Instance.Ring(at, 2.5f, new Color(2.5f, 2f, 0.6f), 0.5f);
            return true;
        }

        // ================= CAMPER CURSE (pressure level 3, campers) =================

        readonly ZoneTarget curseTag = new() { insideLabel = "<size=46>BOUGE !</size>", outsideLabel = "", color = new Color(0.9f, 0.5f, 1f) };
        GroundRing curseRing;
        float curseEnd, curseStacks, curseTick, curseIntensity;
        readonly Vector3[] curseTrail = new Vector3[8];
        int curseHead;
        float curseSample;

        bool StartCurse(float intensity, bool announce)
        {
            if (curseRing == null) curseRing = new GroundRing("Curse Ring", transform);
            curseEnd = Now + curseDuration;
            curseStacks = 0f;
            curseTick = 0f;
            curseIntensity = intensity;
            var p = PlayerController.Instance.Position;
            for (int i = 0; i < curseTrail.Length; i++) curseTrail[i] = p;
            curseHead = 0;
            curseSample = 0f;
            curseTag.active = true;
            curseTag.inside = false;
            RunHUD.Register(curseTag);
            Banner("DRONE TRAQUEUR !\n<size=50>Il te verrouille : reste immobile et il frappe</size>", new Color(0.85f, 0.4f, 1f), 2.4f);
            SFX.Play(SfxId.Pact, 0.8f, 1.2f);
            return true;
        }

        void UpdateCurse(float time, float dt, bool main)
        {
            var player = PlayerController.Instance;
            float left = curseEnd - time;
            if (left <= 0f)
            {
                CloseCurse();
                if (main) EndEvent();
                else if (second == Kind.Curse) second = Kind.None;
                return;
            }

            // Distance covered over the last ~1.6 s (8 samples, 0.2 s apart).
            curseSample += dt;
            if (curseSample >= 0.2f)
            {
                curseSample = 0f;
                curseTrail[curseHead] = player.Position;
                curseHead = (curseHead + 1) % curseTrail.Length;
            }
            Vector3 d = player.Position - curseTrail[curseHead];
            d.y = 0f;
            bool still = d.sqrMagnitude < 2.5f * 2.5f;
            curseStacks = still ? Mathf.Min(6f, curseStacks + dt * 1.2f) : Mathf.Max(0f, curseStacks - dt * 4f);

            int stacks = Mathf.FloorToInt(curseStacks);
            curseTag.inside = stacks > 0; // "BOUGE !" above the player's head
            curseTag.center = player.Position + Vector3.up * 1.2f;
            float pulse = 1f + Mathf.Sin(time * (6f + stacks * 3f)) * 0.15f;
            curseRing.Show(player.Position, 1.1f + stacks * 0.15f, CurseColor * (0.8f + stacks * 0.4f) * pulse);
            if (stacks > 0)
            {
                curseTick += dt;
                if (curseTick >= 0.5f)
                {
                    curseTick = 0f;
                    HurtPlayer(curseDamagePerStack * stacks * curseIntensity * DamageScale);
                    if (GameFX.Instance) GameFX.Instance.Embers(player.Position, 0.6f, 2 + stacks, CurseColor * 2.5f);
                }
            }
            else curseTick = 0f;

            if (main)
                SetStatus(stacks, Mathf.CeilToInt(left), stacks > 0 ? "BOUGE !  x{0}   {1} s" : "DRONE TRAQUEUR  {1} s", CurseColor, stacks > 0);
        }

        void CloseCurse()
        {
            if (curseRing != null) curseRing.Hide();
            curseTag.active = false;
            RunHUD.Unregister(curseTag);
        }
    }
}
