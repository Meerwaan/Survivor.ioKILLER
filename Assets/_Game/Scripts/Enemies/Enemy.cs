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
    /// Physics keeps enemies from stacking; hits are simple distance checks.
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        /// <summary>Enemies this many meters off-screen are moved back in front of the player.</summary>
        const float RelocateMargin = 12f;
        /// <summary>Boss leash: seconds spent this far off-screen before it is moved back in front of the player.</summary>
        const float BossLeashMargin = 8f, BossLeashDelay = 6f;
        float bossAway;
        const float EliteScale = 1.4f;

        static readonly Color ThiefSparkle = new(2.5f, 2f, 0.6f);
        static readonly Color DustColor = new(1.1f, 1f, 0.85f);

        public Renderer bodyRenderer;
        public Material flashMaterial;
        public Material eliteMaterial;
        [Tooltip("Child holding the meshes; squashed on hits. Falls back to the body.")]
        public Transform visual;

        public EnemyData Data { get; private set; }
        public float Radius => (Data ? Data.hitRadius : 0.4f) * (IsElite ? EliteScale : 1f);
        public bool IsDead => health <= 0f;
        public bool IsBoss => Data && Data.behaviour == EnemyBehaviour.Boss;
        public bool IsElite { get; private set; }
        /// <summary>Barrels and crates: don't move, don't count as kills.</summary>
        public bool IsProp => Data && Data.behaviour == EnemyBehaviour.Prop;
        public bool IsThief => Data && Data.behaviour == EnemyBehaviour.Thief;
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

        // Kamikaze
        bool fusing, selfDestruct;
        float fuseUntil;

        // Thief
        float escapeAt, nextCoin, wobbleSeed;

        // Supply drop falling from the sky (visual only)
        float dropHeight, dropSpeed;

        // Elite affixes / bounty
        string tagLabel;
        Color tagColor, auraColor;
        float nextSummon, nextAura;

        // Boss charge
        enum ChargeState { None, Windup, Charging }
        ChargeState charge;
        float chargeTimer;
        Vector3 chargeDir;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
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
        }

        void OnEnable()
        {
            index = Active.Count;
            Active.Add(this);
        }

        void OnDisable()
        {
            if (tagLabel != null) RunHUD.Unregister(this);
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
            charge = ChargeState.None;
            chargeTimer = 5f;
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
            if (visual)
            {
                visual.localScale = visualScale * (IsElite ? EliteScale : 1f);
                visual.localPosition = visualPos;
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
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 d = Quaternion.Euler(0f, offset + 120f * i, 0f) * Vector3.forward;
                        director.QueueSpawn(minion, p + d * (Radius + 1f),
                            director.HealthMult(minutes) * RunConfig.EnemyHealthMult,
                            director.DamageMult(minutes) * RunConfig.EnemyDamageMult,
                            director.XPMult(minutes) * 0.5f, d * 4f);
                    }
                    if (fx)
                    {
                        fx.Ring(p, Radius + 2f, AffixColor(EliteAffix.Invocateur) * 2.5f, 0.5f);
                        fx.Sparks(p + Vector3.up, AffixColor(EliteAffix.Invocateur) * 2f, 14, 1f);
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
            knockback += velocity;
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
            if (flashUntil > 0f && now >= flashUntil && charge != ChargeState.Windup && !fusing)
            {
                bodyRenderer.sharedMaterial = currentMaterial;
                flashUntil = 0f;
            }

            if (dropHeight > 0f) UpdateDrop(dt);
            if (tagLabel != null && !IsDead)
                UpdateAffixes(now, dt, !CameraFollow.Instance || CameraFollow.Instance.IsInView(transform.position, -0.5f));

            if (visual && (punch > 0f || fusing))
            {
                punch = Mathf.MoveTowards(punch, 0f, dt * 7f);
                float p = punch * punch;
                float size = IsElite ? EliteScale : 1f;
                Vector3 squash = new(1f + 0.35f * p, 1f - 0.25f * p, 1f + 0.35f * p);
                if (fusing)
                {
                    // Swells, wobbles and blinks faster and faster before blowing up.
                    float k = 1f - Mathf.Clamp01((fuseUntil - now) / Mathf.Max(0.05f, Data.fuseTime));
                    squash *= 1f + 0.5f * k + Mathf.Sin(now * 40f) * 0.06f * k;
                    bool on = Mathf.Repeat(now * (6f + 18f * k), 1f) < 0.5f;
                    if (bodyRenderer && flashMaterial) bodyRenderer.sharedMaterial = on ? flashMaterial : currentMaterial;
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

            Vector3 toPlayer = player.Position - rb.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 dir = distance > 0.001f ? toPlayer / distance : Vector3.zero;

            if (behaviour == EnemyBehaviour.Thief)
            {
                UpdateThief(dir, view);
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
                else if ((bossAway += Time.deltaTime) >= BossLeashDelay)
                {
                    bossAway = 0f;
                    Relocate(player);
                    if (GameHUD.Instance) GameHUD.Instance.ShowBanner("LE BOSS TE RATTRAPE !", UIKit.Blood, 1.4f);
                    return;
                }
            }

            Vector3 move = dir;
            float speed = Data.moveSpeed * SlowMult(Time.time) * (IsElite ? 1.1f : 1f) * RunModifiers.EnemySpeedMult;
            if (Has(EliteAffix.Rapide)) speed *= 1.45f;

            if (behaviour == EnemyBehaviour.Ranged)
            {
                if (distance < Data.preferredRange * 0.75f) move = -dir;
                else if (distance < Data.preferredRange) move = Vector3.zero;
            }
            else if (IsBoss)
            {
                UpdateCharge(dir, ref move, ref speed);
            }
            else if (behaviour == EnemyBehaviour.Kamikaze)
            {
                if (fusing)
                {
                    speed *= 0.35f;
                    if (Time.time >= fuseUntil)
                    {
                        Detonate();
                        return;
                    }
                }
                else if (distance <= Data.fuseRange + Radius)
                {
                    StartFuse();
                }
            }

            rb.linearVelocity = move * speed + knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 25f * Time.fixedDeltaTime);
            Vector3 face = charge == ChargeState.Charging ? chargeDir : dir;
            if (face != Vector3.zero) rb.rotation = Quaternion.LookRotation(face);

            // Shooters only fire while on screen, so the player always sees where shots come from.
            bool onScreen = !view || view.IsInView(rb.position, -0.5f);
            bool shooter = behaviour == EnemyBehaviour.Ranged || IsBoss;
            if (shooter && charge == ChargeState.None && Time.time >= nextShot && onScreen)
            {
                nextShot = Time.time + Data.fireInterval;
                Shoot(dir);
            }

            // Kamikazes hurt with their explosion instead of contact.
            if (behaviour != EnemyBehaviour.Kamikaze && Data.damage > 0f &&
                distance <= Radius + player.Radius + 0.25f && Time.time >= nextAttack)
            {
                nextAttack = Time.time + Data.attackInterval;
                PlayerHealth.Instance.TakeDamage(Data.damage * damageMult);
                if (Has(EliteAffix.Vampire))
                {
                    health = Mathf.Min(maxHealth, health + maxHealth * 0.12f);
                    if (GameFX.Instance) GameFX.Instance.Ring(rb.position, Radius + 0.8f, AffixColor(EliteAffix.Vampire) * 2.5f, 0.35f);
                }
            }
        }

        // ---------- Kamikaze ----------

        void StartFuse()
        {
            fusing = true;
            fuseUntil = Time.time + Data.fuseTime;
            SFX.Play(SfxId.Fuse);
            // Telegraph the blast radius.
            if (GameFX.Instance) GameFX.Instance.Ring(rb.position, Data.deathExplosionRadius * (IsElite ? 1.4f : 1f), Data.explosionColor * 1.6f, Data.fuseTime);
        }

        void Detonate()
        {
            selfDestruct = true;
            health = 0f;
            Die(false);
        }

        // ---------- Thief ----------

        /// <summary>Runs away from the player, zig-zagging, and escapes if left alone too long.</summary>
        void UpdateThief(Vector3 dir, CameraFollow view)
        {
            float now = Time.time;
            if (now >= escapeAt || (view && !view.IsInView(rb.position, 5f)))
            {
                Escape();
                return;
            }

            Vector3 away = dir == Vector3.zero ? transform.forward : -dir;
            away = Quaternion.Euler(0f, Mathf.Sin(now * 1.7f + wobbleSeed) * 40f, 0f) * away;
            Vector3 p = rb.position;
            p.y = 0f;
            if (Mathf.Abs(p.x) > 170f || Mathf.Abs(p.z) > 170f) away = (away - p.normalized * 1.5f).normalized; // stay on the map

            float speed = Data.moveSpeed * SlowMult(now);
            rb.linearVelocity = away * speed + knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 25f * Time.fixedDeltaTime);
            rb.rotation = Quaternion.LookRotation(away);

            // Glittering trail, so it's easy to spot in the crowd.
            if (GameFX.Instance && Random.value < 0.3f) GameFX.Instance.Sparks(p + Vector3.up * 1.3f, ThiefSparkle, 1, 0.3f);
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

        /// <summary>Boss: every few seconds it stops, flashes, then rushes in a straight line.</summary>
        void UpdateCharge(Vector3 dir, ref Vector3 move, ref float speed)
        {
            chargeTimer -= Time.fixedDeltaTime;
            switch (charge)
            {
                case ChargeState.None:
                    if (chargeTimer <= 0f)
                    {
                        charge = ChargeState.Windup;
                        chargeTimer = 0.75f;
                        if (GameFX.Instance) GameFX.Instance.Ring(rb.position, 3.5f, new Color(2.5f, 0.2f, 0.1f), 0.7f);
                        SFX.Play(SfxId.BossRoar, 0.55f, 1.15f);
                    }
                    break;
                case ChargeState.Windup:
                    move = Vector3.zero;
                    bool on = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
                    if (bodyRenderer && flashMaterial) bodyRenderer.sharedMaterial = on ? flashMaterial : currentMaterial;
                    if (chargeTimer <= 0f)
                    {
                        charge = ChargeState.Charging;
                        chargeTimer = 0.8f;
                        chargeDir = dir == Vector3.zero ? transform.forward : dir;
                        if (bodyRenderer) bodyRenderer.sharedMaterial = currentMaterial;
                        if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.15f);
                    }
                    break;
                case ChargeState.Charging:
                    move = chargeDir;
                    speed *= 4.5f;
                    if (GameFX.Instance && Random.value < 0.5f) GameFX.Instance.Blood(rb.position + Vector3.up * 0.3f, 2);
                    if (chargeTimer <= 0f)
                    {
                        charge = ChargeState.None;
                        chargeTimer = Random.Range(5.5f, 7.5f);
                    }
                    break;
            }
        }

        void Shoot(Vector3 dir)
        {
            if (!Data.projectilePrefab) return;

            Vector3 origin = rb.position + Vector3.up * 1f;
            if (Data.behaviour == EnemyBehaviour.Boss)
            {
                int n = Mathf.Max(1, Data.ringCount);
                float offset = Random.Range(0f, 360f);
                for (int i = 0; i < n; i++)
                    Fire(origin, Quaternion.Euler(0f, offset + 360f * i / n, 0f) * Vector3.forward);
                SFX.Play(SfxId.EnemyShoot, 1f, 0.7f);
            }
            else
            {
                Fire(origin, dir);
                SFX.Play(SfxId.EnemyShoot, 0.6f);
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
            Vector3 pos = RunDirector.Instance.EdgePointToward(ahead, RunDirector.Instance.spawnMargin);
            rb.position = pos;
            transform.position = pos;
            knockback = Vector3.zero;
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
            if (hitDirection.sqrMagnitude > 0.0001f && charge != ChargeState.Charging)
                knockback += hitDirection.normalized * knockbackForce * (1f - Data.knockbackResist);

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

            if (health <= 0f) Die(crit);
            else if (!burn && GameFX.Instance)
            {
                RunModifiers.OnEnemyHit(this);
                if (armored && Random.value < 0.3f)
                {
                    GameFX.Instance.Sparks(transform.position + Vector3.up, AffixColor(EliteAffix.Blinde) * 2f, 3);
                    SFX.Play(SfxId.Metal, 0.5f, 1.2f);
                }
                if (Data.bleeds) GameFX.Instance.Blood(transform.position + Vector3.up, crit ? 6 : 3);
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
                    fx.Blood(pos + Vector3.up * 0.8f, Mathf.RoundToInt(Data.bloodAmount * (IsElite ? 2f : 1f)));
                    fx.Splat(pos, size * 2.5f);
                }
                fx.Gibs(pos + Vector3.up * 0.8f, Mathf.Clamp(Mathf.RoundToInt(size * 10f), 3, 30) + (crit ? 3 : 0) + (prop ? 8 : 0), bodyColor);

                if (IsBoss || IsElite)
                {
                    fx.Explosion(pos, size * 3f, new Color(2f, 0.25f, 0.1f));
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
                    GameHUD.Instance.ShowBanner("BOSS VAINCU !", UIKit.Gold, 2.5f);
                    GameHUD.Instance.ScreenFlash(new Color(1f, 0.85f, 0.4f), 0.7f);
                }
            }

            if (Data.deathExplosionRadius > 0f)
            {
                float radius = Data.deathExplosionRadius * (IsElite ? 1.4f : 1f);
                float playerDamage = selfDestruct ? Data.damage * damageMult : 0f;
                Combat.QueueExplosion(pos, radius, Data.deathExplosionDamage * spawnHealthMult, Data.explosionColor,
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
