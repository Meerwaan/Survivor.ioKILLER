using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>Elite modifiers, rolled when an elite spawns (more of them later in the run). Shown above its head.</summary>
    [System.Flags]
    public enum EliteAffix
    {
        None = 0,
        /// <summary>Much faster.</summary>
        Rapide = 1,
        /// <summary>Takes less damage, barely pushed back.</summary>
        Blinde = 2,
        /// <summary>Blows up shortly after dying (telegraphed): get away from the corpse.</summary>
        Explosif = 4,
        /// <summary>Regenerates, and heals a lot when it hits the player.</summary>
        Vampire = 8,
        /// <summary>Calls zombies to its side every few seconds.</summary>
        Invocateur = 16,
    }

    /// <summary>
    /// Enemy brain: chase (melee), keep distance and spit (ranged), chase, charge and fire rings (boss),
    /// swell up and blow up (kamikaze), run away with the loot (thief), or just sit there (barrels, crates).
    /// Movement is smoothed (velocity eases in, the body turns instead of snapping); chasers fan out on arcs
    /// (<see cref="EnemyData.flank"/>), step aside when something blocks them, and every attack that isn't a plain
    /// contact hit is telegraphed (Bestiaire rule 1). Physics keeps enemies from stacking; hits are distance checks.
    /// Also handles hit feedback (flash + squash + sound), burning, slowing, elites (and their affixes), bounty targets,
    /// splitting and death explosions. In-run pacts and relics plug in through <see cref="RunModifiers"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Enemy : MonoBehaviour, IHudTarget
    {
        /// <summary>
        /// Every living enemy (props included). When looping and possibly killing, iterate BACKWARDS:
        /// a death swaps the last enemy into the dead one's slot.
        /// </summary>
        public static readonly List<Enemy> Active = new(512);

        /// <summary>Living shooters (Cracheurs): RunDirector caps them (Bestiaire 4.5).</summary>
        public static int ShooterCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            ShooterCount = 0;
            playerCacheTime = -1f;
        }

        /// <summary>Enemies this many meters off-screen are moved back in front of the player.</summary>
        const float RelocateMargin = 12f;
        /// <summary>Boss leash: seconds spent this far off-screen before it is moved back in front of the player.</summary>
        const float BossLeashMargin = 8f, BossLeashDelay = 6f;
        float bossAway;
        const float EliteScale = 1.4f;

        // ---- Movement ----
        /// <summary>Acceleration in "speeds per second": full speed in 0.25 s.</summary>
        const float AccelPerSpeed = 4f;
        /// <summary>Turn rates (degrees per second).</summary>
        const float TurnRate = 540f, BossTurnRate = 200f, BossChargeTurnRate = 600f;
        /// <summary>Knockback ceiling (m/s): point-blank shotgun stacks used to tunnel enemies through fences and cars.</summary>
        const float MaxKnockback = 12f;
        /// <summary>The flank curve fades out between these distances (no orbiting at contact).</summary>
        const float FlankFadeNear = 2.5f, FlankFadeFar = 8f;
        /// <summary>Lead: never aims further ahead than this many seconds of the player's run.</summary>
        const float MaxLeadTime = 1.5f;
        // Unstuck: under 30% of the wished progress for this long, farther than contact + UnstuckMinGap from the player
        // (closer, it is just the crowd around him: no sidestep) -> a short sidestep. A car between them is wider than that.
        const float StuckProgress = 0.3f, StuckDelay = 0.35f, SidestepTime = 0.7f, UnstuckMinGap = 1f;

        // ---- Melee ----
        const float ReachSlack = 0.25f, SwingReachBonus = 0.2f;
        /// <summary>A swing that landed during the player's invulnerability tries again this soon (not a full cooldown).</summary>
        const float RetryAfterAbsorbed = 0.25f;
        /// <summary>Slowed for the swing and this long after (the attack clip plays on the legs too).</summary>
        const float SwingSlowTail = 0.3f, SwingSlowMult = 0.3f;
        /// <summary>Masse slam: starts this far beyond contact, hits this much harder than a contact hit (it is rarer and
        /// dodgeable), lands this far in front, then the Masse stays rooted.</summary>
        const float SlamTrigger = 0.6f, SlamDamageMult = 1.3f, SlamOffset = 0.45f, SlamRootTime = 0.6f;
        /// <summary>The Masse clip's fists hit the ground ~0.3 s in: the clip starts that long before the impact.</summary>
        const float SlamAnimLead = 0.3f;

        // ---- Cracheur ----
        /// <summary>Wind-up (stop, face, the sac swells and glows): the clip's spit leaves the mouth at ~0.57 s.</summary>
        const float SpitWindup = 0.55f;
        /// <summary>Aims at where half the player's run over the flight time will take him.</summary>
        const float SpitLead = 0.5f;
        /// <summary>Fires only after this long truly on screen, and within this many preferred ranges.</summary>
        const float ShooterOnScreenDelay = 0.6f, ShooterMaxRange = 1.6f, ShooterScreenMargin = 0.04f;
        const float StrafeMult = 0.45f;

        // ---- Boss ----
        const float ChargeWindupTime = 0.75f, ChargeLockBefore = 0.35f, ChargeTime = 0.8f, ChargeSpeedMult = 4.5f;
        const float ChargeLeadTime = 0.3f, ChargeRecoverTime = 0.7f, RingWindupTime = 0.4f;
        /// <summary>Phase 2 (under 50% HP): more shards per ring, faster rings, an escort of Fiévreux.</summary>
        const int EnragedExtraShards = 6, EnragedEscort = 4;
        const float EnragedFireMult = 0.7f, EnragedChargeMult = 0.85f;

        /// <summary>Seconds of ground telegraph before a summoned enemy crawls out (Invocateur, boss escort).</summary>
        public const float SummonWarn = 0.9f;

        static readonly Color ThiefSparkle = new(2.5f, 2f, 0.6f);
        static readonly Color DustColor = new(1.1f, 1f, 0.85f);
        /// <summary>Amber of the Ambrés (#E58E2B): their telegraphs (Univers 9.3) and elite / boss deaths.</summary>
        public static readonly Color Amber = new(0.898f, 0.557f, 0.169f);

        public Renderer bodyRenderer;
        public Material flashMaterial;
        public Material eliteMaterial;
        [Tooltip("Child holding the meshes; squashed on hits. Falls back to the body.")]
        public Transform visual;

        [Header("Look (CORDON art)")]
        [Tooltip("Drives the model's clips (walk speed, attack, special). Optional.")]
        public CharacterAnimator anim;
        [Tooltip("Blood / ichor colors (particles). Ambrés bleed amber-black ichor, humans a little red.")]
        public Color bloodA = new(0.26f, 0f, 0f), bloodB = new(0.52f, 0.02f, 0.01f);
        [Tooltip("Ground stain color.")]
        public Color splatColor = new(0.28f, 0.01f, 0.01f);
        [Tooltip("Chunk colors on death: crust / body, and the darker inside. Alpha 0 = body material color.")]
        public Color gibColor = Color.clear;
        public Color gibInnerColor = new(0.45f, 0.03f, 0.03f);

        public EnemyData Data { get; private set; }
        public float Radius => (Data ? Data.hitRadius : 0.4f) * (IsElite ? EliteScale : 1f);
        public bool IsDead => health <= 0f;
        public bool IsBoss => Data && Data.behaviour == EnemyBehaviour.Boss;
        public bool IsElite { get; private set; }
        /// <summary>Barrels and crates: don't move, don't count as kills.</summary>
        public bool IsProp => Data && Data.behaviour == EnemyBehaviour.Prop;
        public bool IsThief => Data && Data.behaviour == EnemyBehaviour.Thief;
        /// <summary>Humans (the Pilleur is the only one so far): no crust chunks on death (Univers 9.9 / 9.10).</summary>
        public bool IsHuman => IsThief;
        public float MaxHealth => maxHealth;
        public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        /// <summary>New value on every (re)spawn: lets Synergies tell a pooled enemy's new life from its previous one.</summary>
        public int SpawnId { get; private set; }
        static int spawnCounter;
        /// <summary>Slowed right now, by anything (gas, Alchimiste, frost relic...): lightning gets Surcharge on it.</summary>
        public bool IsSlowed => SlowMult(Time.time) < 1f;
        public float XP { get; private set; }
        public EliteAffix Affixes { get; private set; }
        /// <summary>"Chasse à la prime" target (see WorldEvents).</summary>
        public bool IsBounty { get; private set; }

        static readonly EliteAffix[] AllAffixes = { EliteAffix.Rapide, EliteAffix.Blinde, EliteAffix.Explosif, EliteAffix.Vampire, EliteAffix.Invocateur };
        static readonly Color BountyColor = new(1f, 0.82f, 0.25f);

        public static Color AffixColor(EliteAffix a) => a switch
        {
            EliteAffix.Rapide => new Color(1f, 0.95f, 0.3f),
            EliteAffix.Blinde => new Color(0.65f, 0.8f, 1f),
            EliteAffix.Explosif => new Color(1f, 0.5f, 0.12f),
            EliteAffix.Vampire => new Color(1f, 0.2f, 0.3f),
            EliteAffix.Invocateur => new Color(0.5f, 1f, 0.35f),
            _ => Color.white,
        };

        public static string AffixName(EliteAffix a) => a switch
        {
            EliteAffix.Rapide => "RAPIDE",
            EliteAffix.Blinde => "BLINDÉ",
            EliteAffix.Explosif => "EXPLOSIF",
            EliteAffix.Vampire => "VAMPIRE",
            EliteAffix.Invocateur => "INVOCATEUR",
            _ => "",
        };

        Rigidbody rb;
        CapsuleCollider capsule;
        float capsuleRadius, capsuleHeight;
        Vector3 capsuleCenter;
        Material baseMaterial, currentMaterial;
        Color bodyColor = Color.gray;
        Vector3 visualScale = Vector3.one, visualPos;
        float health, maxHealth, damageMult = 1f;
        // Multipliers given at spawn: passed on to split children and used to scale death explosions.
        float spawnHealthMult = 1f, spawnDamageMult = 1f, spawnXpMult = 1f;
        float nextAttack, nextShot, flashUntil, punch;
        Vector3 knockback;
        int index = -1;

        float burnDps, burnUntil, nextBurnTick;
        // Two slow slots: the strongest running slow, and a weaker one that outlasts it (takes over when it ends).
        float slowFactor = 1f, slowUntil, weakSlowFactor = 1f, weakSlowUntil;

        // Movement: smoothed velocity (knockback stays separate), this spawn's side of the arc and speed.
        Vector3 moveVel;
        float side, speedJitter = 1f;
        // Unstuck
        float stuckTime, sidestepUntil, lastSidestepEnd, sidestepSign = 1f;
        Vector3 sidestepDir;

        // Telegraph glow (amber pulse, then a white flash right before it acts)
        float glowStart, glowUntil;
        bool glowing;

        // Melee swing / Masse slam
        float swingHitAt, swingSlowUntil, rootedUntil, attackAnimAt;
        Vector3 slamCenter, slamFace;

        // Cracheur
        bool backingOff, approaching, countedShooter;
        float strafeSign = 1f, nextStrafeFlip, aimUntil, onScreenSince = -1f, strafeStuck;

        // Kamikaze
        bool fusing, selfDestruct;
        float fuseUntil;
        Vector3 fuseCenter;

        // Thief
        float escapeAt, nextCoin, wobbleSeed;

        // Supply drop falling from the sky (visual only)
        float dropHeight, dropSpeed;

        // Elite affixes / bounty
        string tagLabel;
        Color tagColor, auraColor;
        float nextSummon, nextAura;

        // Boss: chase -> (ring wind-up -> ring) / (charge wind-up -> charge -> recovery: the punish window)
        enum BossState { Chase, RingWindup, ChargeWindup, Charging, Recover }
        BossState bossState;
        float bossTimer, chargeTimer;
        Vector3 chargeDir;
        bool chargeLocked, enraged;

        // Player position / velocity, read once per physics step for every enemy.
        static float playerCacheTime = -1f;
        static Vector3 playerPos, playerVel;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            if (capsule)
            {
                capsuleRadius = capsule.radius;
                capsuleHeight = capsule.height;
                capsuleCenter = capsule.center;
            }
            if (!visual && bodyRenderer) visual = bodyRenderer.transform;
            if (visual)
            {
                visualScale = visual.localScale;
                visualPos = visual.localPosition;
            }
            if (bodyRenderer)
            {
                baseMaterial = bodyRenderer.sharedMaterial;
                if (baseMaterial && baseMaterial.HasProperty("_BaseColor")) bodyColor = baseMaterial.GetColor("_BaseColor");
            }
            if (gibColor.a > 0f) bodyColor = gibColor;
            if (!anim) anim = GetComponent<CharacterAnimator>();
        }

        void OnEnable()
        {
            index = Active.Count;
            Active.Add(this);
        }

        void OnDisable()
        {
            if (tagLabel != null) RunHUD.Unregister(this);
            if (countedShooter)
            {
                countedShooter = false;
                ShooterCount--;
            }
            int last = Active.Count - 1;
            if (index >= 0 && index <= last && Active[index] == this)
            {
                Active[index] = Active[last];
                Active[index].index = index;
                Active.RemoveAt(last);
            }
            index = -1;
        }

        public void Init(EnemyData data, float healthMultiplier, float damageMultiplier, float xpMultiplier, bool elite)
        {
            Data = data;
            SpawnId = ++spawnCounter;
            bossAway = 0f;
            IsElite = elite && data.canBeElite && data.behaviour != EnemyBehaviour.Boss &&
                      data.behaviour != EnemyBehaviour.Prop && data.behaviour != EnemyBehaviour.Thief;
            spawnHealthMult = healthMultiplier;
            spawnDamageMult = damageMultiplier;
            spawnXpMult = xpMultiplier;
            bool prop = data.behaviour == EnemyBehaviour.Prop;
            maxHealth = health = data.maxHealth * healthMultiplier * (IsElite ? 7f * RunModifiers.EliteHealthMult : 1f)
                                 * (prop ? 1f : RunModifiers.EnemyHealthMult);
            damageMult = damageMultiplier * (IsElite ? 1.4f : 1f) * (prop ? 1f : RunModifiers.EnemyDamageMult);
            XP = data.xp * xpMultiplier * (IsElite ? 10f : 1f);
            knockback = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            nextAttack = 0f;
            nextShot = Time.time + Random.Range(0.5f, data.fireInterval);
            flashUntil = 0f;
            punch = 0f;
            burnUntil = 0f;
            slowUntil = weakSlowUntil = 0f;
            slowFactor = weakSlowFactor = 1f;

            // Pooled: every per-life field starts over.
            moveVel = Vector3.zero;
            side = Random.Range(-1f, 1f);
            speedJitter = Random.Range(0.92f, 1.08f);
            stuckTime = 0f;
            sidestepUntil = 0f;
            lastSidestepEnd = -99f;
            sidestepSign = side >= 0f ? 1f : -1f;
            glowUntil = 0f;
            glowing = false;
            swingHitAt = swingSlowUntil = rootedUntil = attackAnimAt = 0f;
            backingOff = approaching = false;
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            nextStrafeFlip = Time.time + Random.Range(1.5f, 3f);
            aimUntil = 0f;
            onScreenSince = -1f;
            strafeStuck = 0f;
            bossState = BossState.Chase;
            bossTimer = 0f;
            chargeTimer = 5f;
            chargeLocked = enraged = false;
            chargeDir = transform.forward;
            if (anim) anim.SetCharging(false);

            bool shooter = data.behaviour == EnemyBehaviour.Ranged;
            if (shooter != countedShooter)
            {
                countedShooter = shooter;
                ShooterCount += shooter ? 1 : -1;
            }

            fusing = selfDestruct = false;
            fuseUntil = 0f;
            escapeAt = Time.time + data.escapeTime;
            nextCoin = 0f;
            wobbleSeed = Random.value * 10f;
            dropHeight = dropSpeed = 0f;

            IsBounty = false;
            Affixes = IsElite ? RollAffixes() : EliteAffix.None;
            nextSummon = Time.time + 3f;
            nextAura = 0f;
            RefreshTag();

            currentMaterial = IsElite && eliteMaterial ? eliteMaterial : baseMaterial;
            if (bodyRenderer) bodyRenderer.sharedMaterial = currentMaterial;
            float size = IsElite ? EliteScale : 1f;
            if (visual)
            {
                visual.localScale = visualScale * size;
                visual.localPosition = visualPos;
            }
            // The capsule grows with the model (an elite used to be 1.4x bigger than its collider).
            if (capsule)
            {
                capsule.radius = capsuleRadius * size;
                capsule.height = capsuleHeight * size;
                capsule.center = capsuleCenter * size;
            }
        }

        // ---------- Elite affixes / bounty ----------

        /// <summary>None in the first 90 s (the start is fragile), then 1, sometimes 2 from 7 min.</summary>
        /// <summary>Elites rolled this run (seeded runs: the Nth elite gets the same affixes for everyone).</summary>
        static int eliteRolls;

        /// <summary>Called by RunDirector.Start: each run counts its elites from 0.</summary>
        public static void ResetAffixRolls() => eliteRolls = 0;

        static EliteAffix RollAffixes()
        {
            float minutes = RunDirector.Instance ? RunDirector.Instance.Minutes : 0f;
            // Daily / weekly: affixes come from the seed and the elite's index, like the cards (fair for everyone).
            System.Random seeded = RunConfig.Seed != 0 ? new System.Random(GameModes.Hash(RunConfig.Seed, 13 * 1000003 + ++eliteRolls, 0)) : null;
            float roll = seeded != null ? (float)seeded.NextDouble() : Random.value;
            int count = minutes < 1.5f ? 0 : minutes < 7f ? 1 : roll < 0.45f ? 2 : 1;
            count = Mathf.Min(3, count + RunModifiers.ExtraEliteAffixes);
            EliteAffix result = EliteAffix.None;
            for (int tries = 0; count > 0 && tries < 20; tries++)
            {
                var a = AllAffixes[seeded != null ? seeded.Next(0, AllAffixes.Length) : Random.Range(0, AllAffixes.Length)];
                if ((result & a) != 0) continue;
                result |= a;
                count--;
            }
            return result;
        }

        /// <summary>Marks this enemy as the bounty target: tougher, gold label, arrow on the screen edge.</summary>
        public void MakeBounty(float healthMult)
        {
            IsBounty = true;
            maxHealth *= healthMult;
            health = maxHealth;
            RefreshTag();
        }

        /// <summary>Leaves the arena without dying (bounty that ran out of time).</summary>
        public void Vanish()
        {
            if (IsDead || !gameObject.activeSelf) return;
            var fx = GameFX.Instance;
            Vector3 p = transform.position;
            if (fx)
            {
                fx.Ring(p, 2.5f, new Color(1.5f, 1.5f, 1.5f), 0.5f);
                fx.Embers(p, 1f, 14, new Color(1.2f, 1.2f, 1.2f));
            }
            Pool.Instance.Despawn(gameObject);
        }

        void RefreshTag()
        {
            string label = null;
            if (Affixes != EliteAffix.None)
            {
                var sb = new System.Text.StringBuilder();
                bool first = true;
                foreach (var a in AllAffixes)
                {
                    if ((Affixes & a) == 0) continue;
                    if (first) auraColor = AffixColor(a);
                    else sb.Append(' ');
                    first = false;
                    sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(AffixColor(a))).Append('>').Append(AffixName(a)).Append("</color>");
                }
                label = sb.ToString();
            }
            if (IsBounty)
            {
                const string bounty = "<size=48>CIBLE</size>";
                label = label == null ? bounty : bounty + "\n" + label;
                if (Affixes == EliteAffix.None) auraColor = BountyColor;
            }

            if (label == null)
            {
                if (tagLabel != null) RunHUD.Unregister(this);
                tagLabel = null;
                return;
            }
            tagLabel = label;
            tagColor = IsBounty ? BountyColor : Color.white;
            RunHUD.Register(this);
        }

        bool Has(EliteAffix a) => (Affixes & a) != 0;

        /// <summary>Affix behaviour and readable cues (embers in the affix color around the body).</summary>
        void UpdateAffixes(float now, float dt, bool onScreen)
        {
            var fx = GameFX.Instance;
            if (fx && now >= nextAura)
            {
                nextAura = now + 0.1f;
                Vector3 p = transform.position;
                fx.Embers(p + Vector3.up * 0.2f, Radius + 0.3f, 1, auraColor * 2f);
                if (IsBounty) fx.Sparks(p + Vector3.up * 1.6f, BountyColor * 2.5f, 1, 0.3f);
            }

            if (Has(EliteAffix.Vampire) && health < maxHealth) health = Mathf.Min(maxHealth, health + maxHealth * 0.025f * dt);

            if (Has(EliteAffix.Invocateur) && onScreen && now >= nextSummon && RunDirector.Instance)
            {
                nextSummon = now + 5f;
                var director = RunDirector.Instance;
                var minion = director.hordeEnemy;
                if (minion && Active.Count < director.maxAlive)
                {
                    float minutes = director.Minutes;
                    float offset = Random.value * 360f;
                    Vector3 p = transform.position;
                    Color c = AffixColor(EliteAffix.Invocateur);
                    // Telegraphed: a ground circle where each one will crawl out, a moment before it does.
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 d = Quaternion.Euler(0f, offset + 120f * i, 0f) * Vector3.forward;
                        director.QueueSpawnWarned(minion, p + d * (Radius + 1f),
                            director.HealthMult(minutes) * RunConfig.EnemyHealthMult,
                            director.DamageMult(minutes) * RunConfig.EnemyDamageMult,
                            director.XPMult(minutes) * 0.5f, d * 4f, SummonWarn, c * 2f);
                    }
                    if (fx)
                    {
                        fx.Ring(p, Radius + 2f, c * 2.5f, 0.5f);
                        fx.Sparks(p + Vector3.up, c * 2f, 14, 1f);
                    }
                    SFX.Play(SfxId.Whoosh, 0.7f, 1.4f);
                }
            }
        }

        // IHudTarget: affix names / bounty label above the head.
        public bool HudActive => tagLabel != null && !IsDead && gameObject.activeSelf;
        public Vector3 HudPosition => transform.position + Vector3.up * (1.4f + Radius * 2.6f);
        public string HudLabel => tagLabel;
        public Color HudColor => tagColor;
        public bool HudArrow => IsBounty;
        public float HudScale => 1f;

        // ---------- Status effects ----------

        public void Ignite(float damagePerSecond, float duration)
        {
            if (IsDead || damagePerSecond <= 0f) return;
            float now = Time.time;
            bool burning = now < burnUntil;
            burnDps = burning ? Mathf.Max(burnDps, damagePerSecond) : damagePerSecond;
            burnUntil = Mathf.Max(burnUntil, now + duration);
            if (!burning) nextBurnTick = now + 0.4f;
        }

        /// <summary>Multiplies move speed by <paramref name="factor"/> (0.5 = half speed) for a while.</summary>
        public void Slow(float factor, float duration)
        {
            if (IsBoss) factor = Mathf.Lerp(factor, 1f, 0.5f);
            float now = Time.time, until = now + duration;
            // A stronger slow takes over for its own duration; a weaker one waits underneath if it lasts longer.
            // (Was one slot: a weak 0.6 s gas slow cut a 1.5 s freeze short, yet kept the freeze factor while refreshed.)
            if (now >= slowUntil)
            {
                // Main slot free: the waiting slow (if still running) moves up first.
                slowFactor = now < weakSlowUntil ? weakSlowFactor : 1f;
                slowUntil = now < weakSlowUntil ? weakSlowUntil : 0f;
                weakSlowFactor = 1f;
                weakSlowUntil = 0f;
            }
            if (now >= slowUntil || factor < slowFactor)
            {
                if (now < slowUntil && slowUntil > until) SetWeakSlow(slowFactor, slowUntil, now);
                slowFactor = factor;
                slowUntil = until;
            }
            else if (factor <= slowFactor) slowUntil = Mathf.Max(slowUntil, until);
            else if (until > slowUntil) SetWeakSlow(factor, until, now);
        }

        void SetWeakSlow(float factor, float until, float now)
        {
            if (now < weakSlowUntil && factor > weakSlowFactor && until <= weakSlowUntil) return; // the waiting one is better
            weakSlowFactor = factor;
            weakSlowUntil = until;
        }

        /// <summary>Speed multiplier from slows right now (1 = not slowed).</summary>
        float SlowMult(float now) => now < slowUntil ? slowFactor : now < weakSlowUntil ? weakSlowFactor : 1f;

        /// <summary>Adds a one-off shove (e.g. split children popping out of their parent).</summary>
        public void Push(Vector3 velocity)
        {
            velocity.y = 0f;
            knockback = Vector3.ClampMagnitude(knockback + velocity, MaxKnockback);
        }

        /// <summary>Supply drop: the visual falls from <paramref name="height"/> meters and lands with a thud.</summary>
        public void DropIn(float height)
        {
            dropHeight = Mathf.Max(0f, height);
            dropSpeed = 0f;
            if (visual) visual.localPosition = visualPos + Vector3.up * dropHeight;
        }

        // ---------- Per frame ----------

        void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            UpdateGlow(now);
            if (flashUntil > 0f && now >= flashUntil)
            {
                flashUntil = 0f;
                if (!glowing && bodyRenderer) bodyRenderer.sharedMaterial = currentMaterial;
            }

            if (dropHeight > 0f) UpdateDrop(dt);
            if (tagLabel != null && !IsDead)
                UpdateAffixes(now, dt, !CameraFollow.Instance || CameraFollow.Instance.IsOnScreen(transform.position, 0.02f));

            if (visual && (punch > 0f || fusing))
            {
                punch = Mathf.MoveTowards(punch, 0f, dt * 7f);
                float p = punch * punch;
                float size = IsElite ? EliteScale : 1f;
                Vector3 squash = new(1f + 0.35f * p, 1f - 0.25f * p, 1f + 0.35f * p);
                if (fusing)
                {
                    // Swells and wobbles faster and faster before blowing up (the glow blinks with it).
                    float k = 1f - Mathf.Clamp01((fuseUntil - now) / Mathf.Max(0.05f, Data.fuseTime));
                    squash *= 1f + 0.5f * k + Mathf.Sin(now * 40f) * 0.06f * k;
                }
                visual.localScale = Vector3.Scale(visualScale, squash) * size;
            }

            if (burnUntil > now && now >= nextBurnTick)
            {
                nextBurnTick = now + 0.4f;
                if (GameFX.Instance) GameFX.Instance.Embers(transform.position + Vector3.up, 0.3f, 2, Combat.ExplosionColor * 2f);
                TakeDamage(burnDps * 0.4f, Vector3.zero, 0f, false, DamageNumbers.Kind.Burn);
            }
        }

        /// <summary>Starts the telegraph glow for <paramref name="duration"/> seconds.</summary>
        void Glow(float duration)
        {
            glowStart = Time.time;
            glowUntil = glowStart + Mathf.Max(0.05f, duration);
        }

        void StopGlow() => glowUntil = 0f;

        /// <summary>Wind-up look: an amber pulse (the elite rim glow) that speeds up, then a white flash for the last
        /// third, "now". Elites (already amber) pulse with the flash.</summary>
        void UpdateGlow(float now)
        {
            if (now >= glowUntil)
            {
                if (glowing)
                {
                    glowing = false;
                    if (bodyRenderer && flashUntil <= 0f) bodyRenderer.sharedMaterial = currentMaterial;
                }
                return;
            }
            glowing = true;
            if (!bodyRenderer || !flashMaterial) return;
            float k = Mathf.Clamp01((now - glowStart) / Mathf.Max(0.05f, glowUntil - glowStart));
            var pulse = eliteMaterial && currentMaterial != eliteMaterial ? eliteMaterial : flashMaterial;
            bool blink = Mathf.Repeat((now - glowStart) * (5f + 15f * k), 1f) < 0.5f;
            bodyRenderer.sharedMaterial = k > 0.67f ? (blink ? flashMaterial : pulse) : (blink ? pulse : currentMaterial);
        }

        void UpdateDrop(float dt)
        {
            dropSpeed += 45f * dt;
            dropHeight -= dropSpeed * dt;
            if (dropHeight <= 0f)
            {
                dropHeight = 0f;
                punch = 1f;
                Vector3 p = transform.position;
                var fx = GameFX.Instance;
                if (fx)
                {
                    fx.Ring(p, 2.6f, DustColor, 0.45f);
                    fx.Sparks(p + Vector3.up * 0.3f, DustColor, 14, 0.8f);
                }
                if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.12f);
                SFX.Play(SfxId.Thud);
            }
            if (visual) visual.localPosition = visualPos + Vector3.up * dropHeight;
        }

        static void CachePlayer(PlayerController player)
        {
            float t = Time.fixedTime;
            if (t == playerCacheTime) return;
            playerCacheTime = t;
            playerPos = player.Position;
            playerVel = player.Velocity;
        }

        void FixedUpdate()
        {
            var player = PlayerController.Instance;
            if (!player || !Data || IsDead) return;

            var view = CameraFollow.Instance;
            var behaviour = Data.behaviour;

            if (behaviour == EnemyBehaviour.Prop)
            {
                // Props stay put; recycle the ones left far behind.
                if (view && !view.IsInView(rb.position, 30f)) Pool.Instance.Despawn(gameObject);
                return;
            }

            float now = Time.time, dt = Time.fixedDeltaTime;
            CachePlayer(player);
            Vector3 actual = rb.linearVelocity; // after last step's collisions: tells when something blocks it
            actual.y = 0f;
            Vector3 toPlayer = playerPos - rb.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 dir = distance > 0.001f ? toPlayer / distance : Flat(transform.forward);

            if (behaviour == EnemyBehaviour.Thief)
            {
                UpdateThief(dir, view, now, dt, actual);
                return;
            }

            // Stragglers far off-screen are moved back in front of the player, except during a lull
            // (they just walk back, so the lull really is quiet).
            if (!IsBoss && view && !view.IsInView(rb.position, RelocateMargin) &&
                (!RunDirector.Instance || RunDirector.Instance.Phase != WavePhase.Lull))
            {
                Relocate(player);
                return;
            }

            // A boss left far behind catches up after a few seconds: while it lives there are no hordes,
            // so outrunning it must never be a safe way to farm the time score.
            if (IsBoss && view)
            {
                if (view.IsInView(rb.position, BossLeashMargin)) bossAway = 0f;
                else if ((bossAway += dt) >= BossLeashDelay)
                {
                    bossAway = 0f;
                    Relocate(player);
                    if (GameHUD.Instance) GameHUD.Instance.ShowBanner("LA SOUCHE TE RATTRAPE !", UIKit.Blood, 1.4f);
                    return;
                }
            }

            float speed = Data.moveSpeed * speedJitter * SlowMult(now) * (IsElite ? 1.1f : 1f) * RunModifiers.EnemySpeedMult;
            if (Has(EliteAffix.Rapide)) speed *= 1.45f;
            float contact = Radius + player.Radius;
            float reach = contact + ReachSlack;

            Vector3 desired; // wished direction, length = fraction of the speed
            Vector3 face = Vector3.zero; // zero = face where it walks
            float turn = IsBoss ? BossTurnRate : TurnRate;
            float accel = AccelPerSpeed * Mathf.Max(1f, speed);
            bool steer = true; // false: a deliberate move (stop, strafe, charge): no unstuck sidestep

            switch (behaviour)
            {
                case EnemyBehaviour.Ranged:
                    desired = UpdateRanged(now, dt, distance, dir, view, speed, actual, ref face, ref steer);
                    break;
                case EnemyBehaviour.Boss:
                    desired = UpdateBoss(now, dt, distance, dir, view, ref speed, ref face, ref accel, ref turn, ref steer);
                    break;
                case EnemyBehaviour.Kamikaze:
                    if (fusing)
                    {
                        if (now >= fuseUntil)
                        {
                            Detonate();
                            return;
                        }
                        desired = Vector3.zero;
                        face = dir;
                        steer = false;
                    }
                    else if (distance <= Data.fuseRange + Radius)
                    {
                        StartFuse();
                        desired = Vector3.zero;
                        face = dir;
                        steer = false;
                    }
                    else desired = ChaseDir(distance, dir, speed);
                    break;
                default:
                    desired = Data.evasive ? EvadeDir(now, distance, dir) : ChaseDir(distance, dir, speed);
                    break;
            }

            // Kamikazes hurt with their explosion instead of contact.
            if (behaviour != EnemyBehaviour.Kamikaze && Data.damage > 0f)
                UpdateMelee(now, distance, dir, contact, reach, ref desired, ref face, ref steer);

            if (steer) desired = Unstick(now, dt, desired, actual, speed, distance, contact);
            if (face == Vector3.zero && !Data.evasive && distance < reach + 0.5f) face = dir; // at the player: look at him, don't spin

            Vector3 target = desired * speed;
            // At contact, stop pushing into the player: the crowd no longer shoves him around or jitters on him
            // (contact hits still reach out to +0.25 m). Same for the boss charge: it hits, it doesn't carry him.
            if (distance < contact + 0.1f)
            {
                target -= dir * Mathf.Max(0f, Vector3.Dot(target, dir));
                moveVel -= dir * Mathf.Max(0f, Vector3.Dot(moveVel, dir));
            }
            Move(target, accel, face, turn, dt);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        /// <summary>Eases the velocity toward <paramref name="targetVel"/>, adds the knockback, turns the body.</summary>
        void Move(Vector3 targetVel, float accel, Vector3 face, float turn, float dt)
        {
            moveVel = Vector3.MoveTowards(moveVel, targetVel, accel * dt);
            rb.linearVelocity = moveVel + knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 25f * dt);
            Vector3 f = face.sqrMagnitude > 0.0001f ? face : moveVel.sqrMagnitude > 0.09f ? moveVel : Vector3.zero;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f)
                rb.rotation = Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(f), turn * dt);
        }

        // ---------- Approach ----------

        /// <summary>
        /// Heads for the player (a little ahead of him for <see cref="EnemyData.lead"/>), curving on this spawn's side of
        /// the arc (<see cref="EnemyData.flank"/>): a crowd fans out around him instead of queueing behind the first one.
        /// The curve fades out close in, so they close the distance instead of orbiting.
        /// </summary>
        Vector3 ChaseDir(float distance, Vector3 dir, float speed)
        {
            Vector3 d = dir;
            if (Data.lead > 0f && distance > 1.5f)
            {
                float t = Mathf.Min(distance / Mathf.Max(0.5f, speed), MaxLeadTime);
                Vector3 to = playerPos + playerVel * (Data.lead * t) - rb.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) d = to.normalized;
            }
            float flank = Data.flank * side * Mathf.Clamp01((distance - FlankFadeNear) / (FlankFadeFar - FlankFadeNear));
            if (flank != 0f) d = (d + new Vector3(-d.z, 0f, d.x) * flank).normalized;
            return d;
        }

        /// <summary>
        /// Cristallisé: the player hunts it (Bestiaire). Walks in while far, drifts sideways at mid range,
        /// slips away (zig-zagging, slower than the player) when he comes close.
        /// </summary>
        Vector3 EvadeDir(float now, float distance, Vector3 dir)
        {
            if (distance > 8f) return ChaseDir(distance, dir, 1f) * 0.8f;
            float sign = side >= 0f ? 1f : -1f;
            Vector3 around = new Vector3(-dir.z, 0f, dir.x) * sign;
            if (distance > 5f) return around * 0.6f;
            Vector3 away = Quaternion.Euler(0f, Mathf.Sin(now * 1.3f + wobbleSeed) * 45f, 0f) * (-dir + around * 0.4f).normalized;
            return StayOnMap(away);
        }

        Vector3 StayOnMap(Vector3 d)
        {
            Vector3 p = rb.position;
            p.y = 0f;
            if (Mathf.Abs(p.x) > 170f || Mathf.Abs(p.z) > 170f) d = (d - p.normalized * 1.5f).normalized;
            return d;
        }

        /// <summary>
        /// Blocked (car, wall, a jam of bodies) while far from the player: steps aside for a moment, then tries again.
        /// The same side as last time if that helped, the other one if it got stuck again right away.
        /// </summary>
        Vector3 Unstick(float now, float dt, Vector3 desired, Vector3 actual, float speed, float distance, float contact)
        {
            if (distance < contact + UnstuckMinGap)
            {
                // At the player (or the crowd around him): no sidestep, just press in.
                stuckTime = 0f;
                sidestepUntil = 0f;
                return desired;
            }
            if (now < sidestepUntil) return sidestepDir * Mathf.Max(0.5f, desired.magnitude);
            float want = desired.magnitude;
            if (want < 0.5f || knockback.sqrMagnitude > 1f)
            {
                stuckTime = 0f;
                return desired;
            }
            Vector3 d = desired / want;
            float expected = speed * want;
            bool pushing = Vector3.Dot(moveVel, d) > 0.7f * expected; // not just accelerating
            if (pushing && Vector3.Dot(actual, d) < StuckProgress * expected) stuckTime += dt;
            else stuckTime = Mathf.Max(0f, stuckTime - dt * 2f);
            if (stuckTime < StuckDelay) return desired;

            stuckTime = 0f;
            if (now - lastSidestepEnd < 1.2f) sidestepSign = -sidestepSign;
            sidestepDir = (new Vector3(-d.z, 0f, d.x) * sidestepSign + d * 0.25f).normalized;
            sidestepUntil = now + SidestepTime;
            lastSidestepEnd = sidestepUntil;
            return sidestepDir * want;
        }

        // ---------- Melee ----------

        /// <summary>
        /// Contact attacks are visible swings: in reach and ready -> the attack clip starts, the hit lands
        /// <see cref="EnemyData.swingTime"/> later if the player is still in reach. Masses slam the ground instead
        /// (circle telegraph in front, then rooted: a punish window).
        /// </summary>
        void UpdateMelee(float now, float distance, Vector3 dir, float contact, float reach, ref Vector3 desired, ref Vector3 face, ref bool steer)
        {
            bool slam = Data.slamRadius > 0f;
            if (attackAnimAt > 0f && now >= attackAnimAt)
            {
                attackAnimAt = 0f;
                if (anim) anim.Attack();
            }

            // The boss charge hits on contact, no swing.
            if (IsBoss && bossState == BossState.Charging)
            {
                if (now >= nextAttack && distance <= reach)
                    nextAttack = HitPlayer(1f) ? now + Data.attackInterval : now + RetryAfterAbsorbed;
                return;
            }

            if (swingHitAt > 0f)
            {
                if (now >= swingHitAt)
                {
                    swingHitAt = 0f;
                    if (slam) Slam();
                    // Swallowed by the player's invulnerability: tries again soon instead of wasting a full cooldown.
                    else if (distance <= reach + SwingReachBonus && !HitPlayer(1f)) nextAttack = now + RetryAfterAbsorbed;
                }
            }
            else if (now >= nextAttack && CanSwing() && distance <= (slam ? contact + SlamTrigger : reach))
            {
                StartSwing(now, dir);
            }

            if (now < rootedUntil)
            {
                desired = Vector3.zero;
                face = slamFace;
                steer = false;
            }
            else if (now < swingSlowUntil)
            {
                desired *= SwingSlowMult;
                face = dir;
                steer = false;
            }
        }

        /// <summary>Not while spitting; the boss only swings while chasing (not in its wind-ups, nor in its recovery window).</summary>
        bool CanSwing() => aimUntil <= 0f && (!IsBoss || bossState == BossState.Chase);

        void StartSwing(float now, Vector3 dir)
        {
            float windup = Mathf.Max(0.02f, Data.swingTime);
            swingHitAt = now + windup;
            nextAttack = now + Data.attackInterval;
            if (Data.slamRadius > 0f)
            {
                // Masse: stops, raises both fists (amber glow), a circle in front fills, the fists land in it.
                slamFace = dir;
                slamCenter = rb.position + dir * (Radius + SlamOffset);
                slamCenter.y = 0f;
                rootedUntil = swingHitAt + SlamRootTime;
                nextAttack = Mathf.Max(nextAttack, rootedUntil);
                moveVel = Vector3.zero;
                attackAnimAt = Mathf.Max(now, swingHitAt - SlamAnimLead);
                if (attackAnimAt <= now)
                {
                    attackAnimAt = 0f;
                    if (anim) anim.Attack();
                }
                Glow(windup);
                if (GameFX.Instance) GameFX.Instance.Telegraph(slamCenter, SlamRadius, Amber * 2f, windup);
                SFX.Play(SfxId.Whoosh, 0.45f, 0.6f);
            }
            else
            {
                swingSlowUntil = swingHitAt + SwingSlowTail;
                if (anim) anim.Attack();
            }
        }

        float SlamRadius => Data.slamRadius * (IsElite ? EliteScale : 1f);

        void Slam()
        {
            var fx = GameFX.Instance;
            float r = SlamRadius;
            if (fx)
            {
                fx.Ring(slamCenter, r, DustColor * 1.4f, 0.3f);
                fx.Sparks(slamCenter + Vector3.up * 0.2f, DustColor, 10, 0.8f);
            }
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.12f);
            SFX.Play(SfxId.Thud, 0.8f, 0.8f);

            var player = PlayerController.Instance;
            if (!player) return;
            Vector3 d = player.Position - slamCenter;
            d.y = 0f;
            float hr = r + player.Radius;
            if (d.sqrMagnitude <= hr * hr) HitPlayer(SlamDamageMult);
        }

        /// <summary>Hurts the player; false when nothing was taken (invulnerable...). VAMPIRE heals on real hits only.</summary>
        bool HitPlayer(float mult)
        {
            var ph = PlayerHealth.Instance;
            if (!ph || !ph.TakeDamage(Data.damage * damageMult * mult)) return false;
            if (Has(EliteAffix.Vampire))
            {
                health = Mathf.Min(maxHealth, health + maxHealth * 0.12f);
                if (GameFX.Instance) GameFX.Instance.Ring(rb.position, Radius + 0.8f, AffixColor(EliteAffix.Vampire) * 2.5f, 0.35f);
            }
            return true;
        }

        // ---------- Cracheur ----------

        /// <summary>
        /// Keeps a distance band (with hysteresis, so it doesn't flicker on a threshold): backs off when the player is
        /// too close, walks in when too far or off screen, strafes slowly in between. Fires after a 0.55 s wind-up
        /// (stops, faces, the sac swells and glows, a hiss), only once it has been really on screen for a moment.
        /// </summary>
        Vector3 UpdateRanged(float now, float dt, float distance, Vector3 dir, CameraFollow view, float speed, Vector3 actual,
            ref Vector3 face, ref bool steer)
        {
            float range = Mathf.Max(1f, Data.preferredRange);
            bool onScreen = !view || view.IsOnScreen(rb.position, ShooterScreenMargin);
            if (!onScreen) onScreenSince = -1f;
            else if (onScreenSince < 0f) onScreenSince = now;

            if (backingOff) { if (distance > range * 0.85f) backingOff = false; }
            else if (distance < range * 0.7f) backingOff = true;
            if (approaching) { if (distance < range * 0.95f && onScreen) approaching = false; }
            else if (distance > range * 1.1f || !onScreen) approaching = true;

            if (aimUntil > 0f)
            {
                face = dir;
                steer = false;
                if (now >= aimUntil)
                {
                    aimUntil = 0f;
                    if (onScreen) Spit(distance);
                    nextShot = now + Mathf.Max(0.5f, Data.fireInterval - SpitWindup); // same rate of fire as before the wind-up
                }
                return Vector3.zero;
            }
            if (now >= nextShot && onScreenSince >= 0f && now - onScreenSince >= ShooterOnScreenDelay &&
                distance <= range * ShooterMaxRange)
            {
                aimUntil = now + SpitWindup;
                if (anim) anim.Special();
                Glow(SpitWindup);
                SFX.Play(SfxId.Fuse, 0.35f, 1.7f);
                face = dir;
                steer = false;
                return Vector3.zero;
            }

            if (backingOff) return -dir;
            if (approaching) return ChaseDir(distance, dir, speed);

            // In the band: a slow strafe that flips now and then (and right away if a wall stops it).
            steer = false;
            Vector3 strafe = new Vector3(-dir.z, 0f, dir.x) * strafeSign;
            if (Vector3.Dot(moveVel, strafe) > 0.7f * StrafeMult * speed && Vector3.Dot(actual, strafe) < 0.3f * StrafeMult * speed)
                strafeStuck += dt;
            else strafeStuck = 0f;
            if (now >= nextStrafeFlip || strafeStuck > 0.3f)
            {
                strafeSign = -strafeSign;
                strafeStuck = 0f;
                nextStrafeFlip = now + Random.Range(1.5f, 3f);
            }
            return strafe * StrafeMult;
        }

        void Spit(float distance)
        {
            if (!Data.projectilePrefab) return;
            float flight = distance / Mathf.Max(1f, Data.projectileSpeed);
            Vector3 aim = playerPos + playerVel * (SpitLead * flight) - rb.position;
            aim.y = 0f;
            Fire(rb.position + Vector3.up, aim.sqrMagnitude > 0.01f ? aim.normalized : Flat(transform.forward));
            SFX.Play(SfxId.EnemyShoot, 0.6f);
        }

        // ---------- Kamikaze ----------

        void StartFuse()
        {
            fusing = true;
            fuseUntil = Time.time + Data.fuseTime;
            // Stops dead (no knockback either), so the blast lands exactly on the circle.
            fuseCenter = rb.position;
            fuseCenter.y = 0f;
            moveVel = Vector3.zero;
            knockback = Vector3.zero;
            if (anim) anim.Special();
            Glow(Data.fuseTime);
            SFX.Play(SfxId.Fuse);
            // The blast radius fills up on the ground (amber: an Ambré's telegraph).
            if (GameFX.Instance) GameFX.Instance.Telegraph(fuseCenter, Data.deathExplosionRadius * (IsElite ? EliteScale : 1f), Amber * 2f, Data.fuseTime);
        }

        void Detonate()
        {
            selfDestruct = true;
            health = 0f;
            Die(false);
        }

        // ---------- Thief ----------

        /// <summary>Runs away from the player, zig-zagging, and escapes if left alone too long.</summary>
        void UpdateThief(Vector3 dir, CameraFollow view, float now, float dt, Vector3 actual)
        {
            if (now >= escapeAt || (view && !view.IsInView(rb.position, 5f)))
            {
                Escape();
                return;
            }

            Vector3 away = -dir;
            away = Quaternion.Euler(0f, Mathf.Sin(now * 1.7f + wobbleSeed) * 40f, 0f) * away;
            away = StayOnMap(away);

            float speed = Data.moveSpeed * SlowMult(now);
            away = Unstick(now, dt, away, actual, speed, 99f, 0f);
            Move(away * speed, AccelPerSpeed * Mathf.Max(1f, speed), Vector3.zero, TurnRate, dt);

            // Glittering trail, so it's easy to spot in the crowd.
            if (GameFX.Instance && Random.value < 0.3f) GameFX.Instance.Sparks(rb.position + Vector3.up * 1.3f, ThiefSparkle, 1, 0.3f);
        }

        void Escape()
        {
            Vector3 p = transform.position;
            if (GameFX.Instance)
            {
                GameFX.Instance.Ring(p, 2f, ThiefSparkle, 0.4f);
                GameFX.Instance.Sparks(p + Vector3.up, ThiefSparkle, 20, 1.5f);
            }
            SFX.Play(SfxId.Fail, 0.8f, 1.2f);
            if (GameHUD.Instance) GameHUD.Instance.ShowBanner("IL S'EST ENFUI...", UIKit.Muted, 1.5f);
            Pool.Instance.Despawn(gameObject);
        }

        // ---------- Boss ----------

        /// <summary>
        /// Boss: chases; every few seconds stops and glows, then fires a ring of shards; every 5.5-7.5 s winds up a charge
        /// (the direction locks 0.35 s before it goes, and the lane fills on the ground), rushes, then stands winded
        /// 0.7 s (the punish window). Under 50% HP it enrages (see <see cref="Enrage"/>).
        /// </summary>
        Vector3 UpdateBoss(float now, float dt, float distance, Vector3 dir, CameraFollow view, ref float speed,
            ref Vector3 face, ref float accel, ref float turn, ref bool steer)
        {
            var fx = GameFX.Instance;
            switch (bossState)
            {
                case BossState.Chase:
                    chargeTimer -= dt;
                    if (swingHitAt > 0f || now < swingSlowUntil) return dir; // finish the swipe first
                    if (chargeTimer <= 0f)
                    {
                        bossState = BossState.ChargeWindup;
                        bossTimer = ChargeWindupTime;
                        chargeLocked = false;
                        if (anim) anim.Windup();
                        Glow(ChargeWindupTime);
                        if (fx) fx.Ring(rb.position, 3.5f, Amber * 2.5f, 0.7f);
                        SFX.Play(SfxId.BossRoar, 0.55f, 1.15f);
                        steer = false;
                        return Vector3.zero;
                    }
                    bool onScreen = !view || view.IsOnScreen(rb.position, 0f);
                    if (now >= nextShot && onScreen && chargeTimer > 1f)
                    {
                        bossState = BossState.RingWindup;
                        bossTimer = RingWindupTime;
                        if (anim) anim.Special();
                        Glow(RingWindupTime);
                        SFX.Play(SfxId.Fuse, 0.6f, 0.55f);
                        steer = false;
                        return Vector3.zero;
                    }
                    return dir;

                case BossState.RingWindup:
                    face = dir;
                    steer = false;
                    if ((bossTimer -= dt) <= 0f)
                    {
                        FireRing();
                        bossState = BossState.Chase;
                        // The wind-up counts in the interval: same ring rate as before it existed (faster once enraged).
                        nextShot = now + Mathf.Max(0.5f, Data.fireInterval * (enraged ? EnragedFireMult : 1f) - RingWindupTime);
                    }
                    return Vector3.zero;

                case BossState.ChargeWindup:
                    steer = false;
                    bossTimer -= dt;
                    if (!chargeLocked)
                    {
                        face = dir;
                        if (bossTimer <= ChargeLockBefore) LockCharge(speed);
                    }
                    else
                    {
                        face = chargeDir;
                        turn = BossChargeTurnRate;
                    }
                    if (bossTimer <= 0f)
                    {
                        if (!chargeLocked) LockCharge(speed);
                        bossState = BossState.Charging;
                        bossTimer = ChargeTime;
                        if (anim) anim.SetCharging(true);
                        StopGlow();
                        if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.15f);
                    }
                    return Vector3.zero;

                case BossState.Charging:
                    steer = false;
                    face = chargeDir;
                    turn = BossChargeTurnRate;
                    speed *= ChargeSpeedMult;
                    accel = 60f;
                    if (fx && Random.value < 0.5f) fx.Blood(rb.position + Vector3.up * 0.3f, 2, bloodA, bloodB);
                    if ((bossTimer -= dt) <= 0f)
                    {
                        bossState = BossState.Recover;
                        bossTimer = ChargeRecoverTime;
                        if (anim) anim.SetCharging(false);
                        if (fx) fx.Ring(rb.position, 2.5f, DustColor, 0.4f);
                    }
                    return chargeDir;

                default: // Recover: winded, barely turns
                    steer = false;
                    accel = 60f;
                    face = dir;
                    turn = 90f;
                    if ((bossTimer -= dt) <= 0f)
                    {
                        bossState = BossState.Chase;
                        chargeTimer = Random.Range(5.5f, 7.5f) * (enraged ? EnragedChargeMult : 1f);
                    }
                    return Vector3.zero;
            }
        }

        /// <summary>Locks the charge (slightly ahead of the player) and draws its lane: circles that fill as it passes.</summary>
        void LockCharge(float speed)
        {
            chargeLocked = true;
            Vector3 to = playerPos + playerVel * ChargeLeadTime - rb.position;
            to.y = 0f;
            chargeDir = to.sqrMagnitude > 0.01f ? to.normalized : Flat(transform.forward);

            var fx = GameFX.Instance;
            if (!fx) return;
            float v = Mathf.Max(1f, speed * ChargeSpeedMult);
            float length = v * ChargeTime;
            float r = Radius + 0.3f;
            int n = Mathf.Clamp(Mathf.CeilToInt(length / (r * 1.6f)), 2, 6);
            for (int i = 1; i <= n; i++)
            {
                float d = length * i / n;
                fx.Telegraph(rb.position + chargeDir * d, r, Amber * 2.2f, Mathf.Max(0.5f, bossTimer + d / v));
            }
        }

        void FireRing()
        {
            if (!Data.projectilePrefab) return;
            Vector3 origin = rb.position + Vector3.up * 1f;
            int n = Mathf.Max(1, Data.ringCount + (enraged ? EnragedExtraShards : 0));
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
                Fire(origin, Quaternion.Euler(0f, offset + 360f * i / n, 0f) * Vector3.forward);
            SFX.Play(SfxId.EnemyShoot, 1f, 0.7f);
        }

        /// <summary>Phase 2 (Bestiaire 3.7): tighter, faster rings, quicker charges, and it calls Fiévreux.</summary>
        void Enrage()
        {
            enraged = true;
            Vector3 p = transform.position;
            string who = Data && !string.IsNullOrEmpty(Data.displayName) ? "LE " + Data.displayName.ToUpperInvariant() : "LA SOUCHE";
            if (GameHUD.Instance) GameHUD.Instance.ShowBanner($"{who} S'ENRAGE !\n<size=50>Il appelle des Fiévreux</size>", UIKit.Blood, 1.8f);
            SFX.Play(SfxId.BossRoar, 0.9f, 0.85f);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.3f);
            if (GameFX.Instance) GameFX.Instance.Ring(p, 5f, Amber * 2.5f, 0.6f);

            var director = RunDirector.Instance;
            if (!director) return;
            var escort = director.hordeFastEnemy ? director.hordeFastEnemy : director.hordeEnemy;
            if (!escort) return;
            float offset = Random.value * 360f;
            for (int i = 0; i < EnragedEscort; i++)
            {
                Vector3 d = Quaternion.Euler(0f, offset + 360f * i / EnragedEscort, 0f) * Vector3.forward;
                director.SpawnWarned(escort, p + d * (Radius + 2.5f), SummonWarn, Amber * 2f);
            }
        }

        void Fire(Vector3 origin, Vector3 dir)
        {
            var p = Pool.Instance.Spawn<EnemyProjectile>(Data.projectilePrefab, origin, Quaternion.LookRotation(dir));
            p.Launch(dir, Data.projectileSpeed, Data.projectileDamage * damageMult);
        }

        void Relocate(PlayerController player)
        {
            Vector3 ahead = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * player.Facing;
            var director = RunDirector.Instance;
            Vector3 pos = director.EdgePointToward(ahead, director.spawnMargin);
            pos = RunDirector.SafeSpawnPoint(pos, Radius);
            rb.position = pos;
            transform.position = pos;
            knockback = Vector3.zero;
            moveVel = Vector3.zero;
            stuckTime = 0f;
            sidestepUntil = 0f;
            if (IsBoss && bossState != BossState.Chase)
            {
                // A charge / wind-up in progress no longer matches its lane: start over.
                bossState = BossState.Chase;
                chargeTimer = Mathf.Max(chargeTimer, 2f);
                if (anim) anim.SetCharging(false);
                StopGlow();
            }
        }

        // ---------- Damage ----------

        public void TakeDamage(float amount, Vector3 hitDirection, float knockbackForce, bool crit = false,
            DamageNumbers.Kind kind = DamageNumbers.Kind.Normal)
        {
            if (IsDead || dropHeight > 0f) return;

            amount *= RunModifiers.DamageTakenMult(this);
            bool armored = Has(EliteAffix.Blinde);
            if (armored)
            {
                amount *= 0.6f;
                knockbackForce *= 0.25f;
            }

            health -= amount;
            if (DamageNumbers.Instance) DamageNumbers.Instance.Show(transform.position, amount, crit ? DamageNumbers.Kind.Crit : kind);

            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude > 0.0001f && bossState != BossState.Charging && !fusing)
                knockback = Vector3.ClampMagnitude(knockback + hitDirection.normalized * knockbackForce * (1f - Data.knockbackResist), MaxKnockback);

            bool burn = kind == DamageNumbers.Kind.Burn;
            if (bodyRenderer && flashMaterial && !burn)
            {
                bodyRenderer.sharedMaterial = flashMaterial;
                flashUntil = Time.time + 0.07f;
            }
            punch = Mathf.Max(punch, burn ? 0.4f : 1f);

            if (!burn) SFX.Play(Data.hitSound, crit ? 0.9f : 0.6f, crit ? 0.85f : 1f);

            // The thief leaks coins with every hit: chasing it pays off right away.
            if (IsThief && !burn && health > 0f && Time.time >= nextCoin && Loot.Instance)
            {
                nextCoin = Time.time + 0.18f;
                Loot.Instance.DropCoins(transform.position + Vector3.up * 0.6f, 1, 1);
            }

            if (IsBoss && !enraged && health > 0f && health <= maxHealth * 0.5f) Enrage();

            if (health <= 0f) Die(crit);
            else if (!burn && GameFX.Instance)
            {
                RunModifiers.OnEnemyHit(this);
                if (armored && Random.value < 0.3f)
                {
                    GameFX.Instance.Sparks(transform.position + Vector3.up, AffixColor(EliteAffix.Blinde) * 2f, 3);
                    SFX.Play(SfxId.Metal, 0.5f, 1.2f);
                }
                if (Data.bleeds) GameFX.Instance.Blood(transform.position + Vector3.up, crit ? 6 : 3, bloodA, bloodB);
                else GameFX.Instance.Sparks(transform.position + Vector3.up * 0.6f, new Color(2f, 1.6f, 0.8f), 3);
            }
        }

        void Die(bool crit)
        {
            Vector3 pos = transform.position;
            var fx = GameFX.Instance;
            float size = Radius;
            bool prop = IsProp;

            if (!prop) GameManager.Instance.RegisterKill(Data.score * (IsElite ? 5 : 1), IsBoss, IsElite);

            if (fx)
            {
                if (Data.bleeds)
                {
                    fx.Blood(pos + Vector3.up * 0.8f, Mathf.RoundToInt(Data.bloodAmount * (IsElite ? 2f : 1f)), bloodA, bloodB);
                    fx.Splat(pos, size * 2f, splatColor);
                }
                // Humans fall, they don't burst into chunks (Univers 9.10).
                if (!IsHuman)
                    fx.Gibs(pos + Vector3.up * 0.8f, Mathf.Clamp(Mathf.RoundToInt(size * 10f), 3, 30) + (crit ? 3 : 0) + (prop ? 8 : 0), bodyColor, gibInnerColor);

                if (IsBoss || IsElite)
                {
                    fx.Explosion(pos, size * 3f, Amber * 2f);
                    if (CameraFollow.Instance) CameraFollow.Instance.Shake(IsBoss ? 0.7f : 0.3f);
                    GameManager.Instance.HitStop(IsBoss ? 0.25f : 0.06f);
                }
            }

            // Kill pops climb in pitch with the combo: long streaks literally sound hotter.
            float comboPitch = 1f + Mathf.Min(Combat.Combo, 150) * 0.003f;
            SFX.Play(Data.deathSound, IsElite ? 1f : 0.75f, Data.deathSound == SfxId.Kill ? comboPitch : 1f);
            if (IsElite || IsBoss) SFX.Play(SfxId.Gore, 1f, 0.8f);
            if (IsBoss)
            {
                SFX.Play(SfxId.Success);
                if (GameHUD.Instance)
                {
                    GameHUD.Instance.ShowBanner("SOUCHE ABATTUE !", UIKit.Gold, 2.5f);
                    GameHUD.Instance.ScreenFlash(new Color(1f, 0.85f, 0.4f), 0.7f);
                }
            }

            if (Data.deathExplosionRadius > 0f)
            {
                float radius = Data.deathExplosionRadius * (IsElite ? EliteScale : 1f);
                float playerDamage = selfDestruct ? Data.damage * damageMult : 0f;
                // A fusing Kyste blows up exactly on its telegraph.
                Vector3 blast = fusing ? fuseCenter : pos;
                Combat.QueueExplosion(blast, radius, Data.deathExplosionDamage * spawnHealthMult, Data.explosionColor,
                    prop ? 0.1f : 0.05f, playerDamage);
            }

            if (Data.splitInto && Data.splitCount > 0 && RunDirector.Instance)
            {
                float offset = Random.value * 360f;
                for (int i = 0; i < Data.splitCount; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, offset + 360f * i / Data.splitCount, 0f) * Vector3.forward;
                    RunDirector.Instance.QueueSpawn(Data.splitInto, pos + d * 0.4f, spawnHealthMult, spawnDamageMult, spawnXpMult, d * 9f);
                }
            }

            if (Has(EliteAffix.Explosif))
            {
                // Telegraphed blast one second after death: get away from the corpse.
                const float blastRadius = 3.4f, blastDelay = 1f;
                var c = AffixColor(EliteAffix.Explosif);
                if (fx) fx.Telegraph(pos, blastRadius, c * 2.5f, blastDelay);
                SFX.Play(SfxId.Fuse, 1f, 0.8f);
                Combat.QueueExplosion(pos, blastRadius, 30f * spawnHealthMult, c, blastDelay, Mathf.Max(Data.damage, 8f) * damageMult * 1.3f);
            }

            if (!prop) Combat.OnKill(this);
            RunModifiers.OnEnemyKilled(this);
            if (Loot.Instance) Loot.Instance.Drop(this, pos);
            Pool.Instance.Despawn(gameObject);
        }
    }
}
