using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Survivor
{
    public enum PerkKind { Relic, Pact }

    /// <summary>
    /// Something that changes the rest of THIS run only: a relic (small passive bonus, doesn't use the 6 passive
    /// slots) or a pact (a big bonus paid for with a curse). Same pool and same odds for every player.
    /// </summary>
    public sealed class Perk
    {
        public string id;
        public string name;
        /// <summary>The good part (relics: the whole effect).</summary>
        public string boon;
        /// <summary>Pacts only: the price.</summary>
        public string bane = "";
        public Color color;
        public PerkKind kind;
        public Action<RunModifiers, PlayerStats> apply;

        public bool IsPact => kind == PerkKind.Pact;
    }

    /// <summary>
    /// In-run relics and pacts, and the hooks they need. Enemy.cs reads the static multipliers and calls
    /// <see cref="OnEnemyHit"/> / <see cref="OnEnemyKilled"/>; player-side effects go through PlayerStats
    /// fields or PlayerHealth.Hurt. Everything resets with the scene (each run).
    /// Added at runtime by ArenaEvents (no scene setup needed).
    /// </summary>
    public class RunModifiers : MonoBehaviour
    {
        public static RunModifiers Instance { get; private set; }

        // ---------- Read by Enemy (pacts and relics) ----------

        /// <summary>Pacts: multipliers on every new enemy (props excluded).</summary>
        public static float EnemySpeedMult = 1f, EnemyHealthMult = 1f, EnemyDamageMult = 1f;
        /// <summary>Pact "Chasseur": elites are tougher and get an extra affix.</summary>
        public static float EliteHealthMult = 1f;
        public static int ExtraEliteAffixes;
        /// <summary>Relic "Talisman de givre": chance per hit to freeze.</summary>
        public static float FreezeChance;
        /// <summary>Relic "Marque du chasseur": extra damage taken by elites and bosses.</summary>
        public static float EliteDamageBonus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            EnemyHealthMult = EnemyDamageMult = 1f;
            // Base values come from the run's threats (Modes/Threats.cs): 1 / 0 when none is on.
            EnemySpeedMult = Threats.EnemySpeedMult;
            EliteHealthMult = Threats.EliteHealthMult;
            ExtraEliteAffixes = Threats.ExtraEliteAffixes;
            FreezeChance = 0f;
            EliteDamageBonus = 0f;
        }

        public static readonly Color PactColor = new(0.85f, 0.15f, 0.25f);
        public static readonly Color RelicColor = new(1f, 0.8f, 0.35f);
        public static readonly Color FreezeColor = new(0.5f, 1.6f, 3f);
        const string BoonHex = "#8CFF7A", BaneHex = "#FF7070";

        /// <summary>Owned relics and pacts, in the order they were taken.</summary>
        public readonly List<Perk> Owned = new(16);
        /// <summary>Bumped on every change (the HUD rebuilds its icon row).</summary>
        public int Version { get; private set; }

        /// <summary>Affixed elites drop a relic this often (pact "Chasseur": always).</summary>
        public float eliteRelicChance = 0.1f;

        // Relic state
        int vampireKills, vampireEvery;
        float vampireHeal;
        float bellInterval, bellTimer;
        bool thorns, phoenix;
        float nextFreezeFx;

        void Awake()
        {
            Instance = this;
            ResetStatics();
        }

        void Start()
        {
            if (PlayerHealth.Instance) PlayerHealth.Instance.Hurt += OnHurt;
        }

        void OnDestroy()
        {
            if (PlayerHealth.Instance) PlayerHealth.Instance.Hurt -= OnHurt;
            if (Instance == this) Instance = null;
        }

        static PlayerStats Stats => PlayerController.Instance ? PlayerController.Instance.Stats : null;

        /// <summary>Damage of world effects (bell, thorns, meteors) scales like enemy health, so they stay relevant.</summary>
        public static float WorldScale
        {
            get
            {
                float minutes = RunDirector.Instance ? RunDirector.Instance.Minutes : 0f;
                return (RunDirector.Instance ? RunDirector.Instance.HealthMult(minutes) : 1f) * RunConfig.EnemyHealthMult;
            }
        }

        // ---------- Catalog ----------

        static Perk Relic(string id, string name, string boon, Color color, Action<RunModifiers, PlayerStats> apply) =>
            new() { id = id, name = name, boon = boon, color = color, kind = PerkKind.Relic, apply = apply };

        static Perk Pact(string id, string name, string boon, string bane, Action<RunModifiers, PlayerStats> apply) =>
            new() { id = id, name = name, boon = boon, bane = bane, color = PactColor, kind = PerkKind.Pact, apply = apply };

        /// <summary>12 relics, all equally likely. Each can be found once per run.</summary>
        public static readonly Perk[] Relics =
        {
            Relic("frost", "Talisman de givre", "10% de chance de geler un ennemi touché", new Color(0.5f, 0.85f, 1f),
                (m, s) => FreezeChance += 0.1f),
            Relic("lead", "Bottes de plomb", "-15% de dégâts reçus, -8% de vitesse", new Color(0.6f, 0.62f, 0.7f),
                (m, s) => { s.armor += 0.15f; s.moveSpeed *= 0.92f; }),
            Relic("fang", "Croc de vampire", "Tous les 30 ennemis tués : +5 PV", new Color(0.9f, 0.2f, 0.3f),
                (m, s) => { m.vampireEvery = 30; m.vampireHeal = 5f; }),
            Relic("bell", "Cloche du glas", "Toutes les 10 s, une onde repousse et blesse les ennemis proches", new Color(0.8f, 0.6f, 0.3f),
                (m, s) => { m.bellInterval = 10f; m.bellTimer = 3f; }),
            Relic("mark", "Marque du chasseur", "+40% de dégâts contre les élites et les boss", new Color(1f, 0.45f, 0.2f),
                (m, s) => EliteDamageBonus += 0.4f),
            Relic("horseshoe", "Fer à cheval", "+8% de chance de critique, +30% d'or", new Color(0.95f, 0.85f, 0.4f),
                (m, s) => { s.critChance += 0.08f; s.goldMult += 0.3f; }),
            Relic("stoneheart", "Cœur de pierre", "+30 PV max", new Color(0.75f, 0.55f, 0.5f),
                (m, s) => { if (PlayerHealth.Instance) PlayerHealth.Instance.AddMaxHealth(30f); else s.maxHealth += 30f; }),
            Relic("hourglass", "Sablier fêlé", "FRÉNÉSIE : +4 s, et il faut 30% de combo en moins", new Color(1f, 0.5f, 0.15f),
                (m, s) =>
                {
                    var ev = ArenaEvents.Instance;
                    if (!ev) return;
                    ev.frenzyDuration += 4f;
                    ev.frenzyComboScale *= 0.7f;
                }),
            Relic("fuse", "Mèche courte", "+8% de chance qu'un ennemi tué explose", new Color(1f, 0.35f, 0.15f),
                (m, s) => s.corpseExplodeChance += 0.08f),
            Relic("compass", "Boussole", "+40% de rayon de ramassage, +10% d'XP", new Color(0.4f, 0.75f, 1f),
                (m, s) => { s.pickupRadius *= 1.4f; s.xpMult += 0.1f; }),
            Relic("thorns", "Couronne d'épines", "Quand tu es touché, une onde blesse les ennemis autour", new Color(0.55f, 0.85f, 0.35f),
                (m, s) => m.thorns = true),
            Relic("phoenix", "Plume de phénix", "Une fois : à 0 PV, tu renais avec 50% de PV", new Color(1f, 0.6f, 0.2f),
                (m, s) => m.phoenix = true),
        };

        /// <summary>Pacts offered on the altars (2 at a time). Each can be sealed once per run.</summary>
        public static readonly Perk[] Pacts =
        {
            Pact("blood", "Pacte du sang", "+30% de dégâts", "Ennemis +15% de vitesse",
                (m, s) => { s.damageMult *= 1.3f; EnemySpeedMult *= 1.15f; }),
            Pact("glass", "Pacte de verre", "+12% de critique, +50% de dégâts critiques", "-25% de PV max",
                (m, s) => { s.critChance += 0.12f; s.critMult += 0.5f; ShrinkMaxHealth(0.25f); }),
            Pact("chaos", "Pacte du chaos", "+1 projectile pour toutes les armes (sauf le gaz)", "Ennemis +30% de PV",
                (m, s) => { s.extraProjectiles += 1; EnemyHealthMult *= 1.3f; }),
            Pact("greed", "Pacte de l'avare", "+60% d'or, +20% d'XP", "Ennemis +20% de dégâts",
                (m, s) => { s.goldMult += 0.6f; s.xpMult += 0.2f; EnemyDamageMult *= 1.2f; }),
            Pact("iron", "Pacte de fer", "-20% de dégâts reçus", "Armes 12% plus lentes",
                (m, s) => { s.armor += 0.2f; s.cooldownMult *= 1.12f; }),
            Pact("sacrifice", "Pacte du sacrifice", "Soin complet + un coffre", "-20% de PV max",
                (m, s) =>
                {
                    ShrinkMaxHealth(0.2f);
                    if (PlayerHealth.Instance) PlayerHealth.Instance.Heal(PlayerHealth.Instance.Max);
                    var player = PlayerController.Instance;
                    if (Loot.Instance && player)
                        Loot.Instance.DropRewardChest(player.Position + player.Facing * 2f + Vector3.up * 0.4f);
                }),
            Pact("hunter", "Pacte du chasseur", "Chaque élite à affixe lâche une relique", "Élites +50% de PV et +1 affixe",
                (m, s) => { m.eliteRelicChance = 1f; EliteHealthMult *= 1.5f; ExtraEliteAffixes += 1; }),
            Pact("storm", "Pacte de la tempête", "+25% de zone, +25% de durée", "Ennemis +20% de PV",
                (m, s) => { s.areaMult *= 1.25f; s.durationMult *= 1.25f; EnemyHealthMult *= 1.2f; }),
        };

        static void ShrinkMaxHealth(float fraction)
        {
            var s = Stats;
            if (!s) return;
            s.maxHealth = Mathf.Max(20f, s.maxHealth * (1f - fraction));
            if (PlayerHealth.Instance) PlayerHealth.Instance.Heal(0f); // clamps current health to the new max
        }

        // ---------- Seeded picks (daily / weekly: the Nth relic and the Nth altar are the same for everyone) ----------

        const int StreamRelics = 11, StreamPacts = 12;
        int relicGrants, pactRolls;

        static System.Random Seeded(int stream, int index) =>
            RunConfig.Seed != 0 ? new System.Random(GameModes.Hash(RunConfig.Seed, stream * 1000003 + index, 0)) : null;

        // ---------- Queries ----------

        public bool Has(string id)
        {
            for (int i = 0; i < Owned.Count; i++) if (Owned[i].id == id) return true;
            return false;
        }

        public bool RelicsLeft
        {
            get
            {
                for (int i = 0; i < Relics.Length; i++) if (!Has(Relics[i].id)) return true;
                return false;
            }
        }

        /// <summary>Up to <paramref name="count"/> different pacts not sealed yet, in random order.</summary>
        public List<Perk> RollPacts(int count)
        {
            var pool = new List<Perk>(Pacts.Length);
            foreach (var p in Pacts) if (!Has(p.id)) pool.Add(p);
            var result = new List<Perk>(count);
            var seeded = Seeded(StreamPacts, ++pactRolls);
            while (result.Count < count && pool.Count > 0)
            {
                int i = seeded != null ? seeded.Next(0, pool.Count) : Random.Range(0, pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return result;
        }

        // ---------- Granting ----------

        /// <summary>Picks a relic not owned yet, applies it and announces it. Returns null if all are owned.</summary>
        public Perk GrantRandomRelic(Vector3 at)
        {
            int left = 0;
            foreach (var r in Relics) if (!Has(r.id)) left++;
            if (left == 0) return null;
            var seeded = Seeded(StreamRelics, ++relicGrants);
            int pick = seeded != null ? seeded.Next(0, left) : Random.Range(0, left);
            foreach (var r in Relics)
            {
                if (Has(r.id)) continue;
                if (pick-- > 0) continue;
                Add(r);
                if (GameHUD.Instance)
                {
                    GameHUD.Instance.ShowBanner($"RELIQUE !\n<size=56>{r.name}</size>\n<size=40>{r.boon}</size>", RelicColor, 2.6f);
                    GameHUD.Instance.ScreenFlash(new Color(1f, 0.85f, 0.45f), 0.35f);
                }
                SFX.Play(SfxId.Relic);
                var fx = GameFX.Instance;
                if (fx)
                {
                    Color c = r.color * 2.5f;
                    fx.Ring(at, 4f, c, 0.6f);
                    fx.Sparks(at + Vector3.up, c, 40, 1.8f);
                }
                return r;
            }
            return null;
        }

        /// <summary>Seals a pact for the rest of the run.</summary>
        public void SealPact(Perk pact, Vector3 at)
        {
            if (pact == null || Has(pact.id)) return;
            Add(pact);
            if (GameHUD.Instance)
            {
                GameHUD.Instance.ShowBanner(
                    $"{pact.name.ToUpperInvariant()}\n<size=44><color={BoonHex}>{pact.boon}</color>\n<color={BaneHex}>{pact.bane}</color></size>",
                    new Color(1f, 0.35f, 0.4f), 3f);
                GameHUD.Instance.ScreenFlash(new Color(0.5f, 0f, 0.05f), 0.6f);
            }
            SFX.Play(SfxId.Pact);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(0.3f);
            var fx = GameFX.Instance;
            if (fx)
            {
                fx.Ring(at, 7f, new Color(2.5f, 0.2f, 0.4f), 0.8f);
                fx.Sparks(at + Vector3.up, new Color(2.5f, 0.3f, 0.4f), 50, 2f);
                fx.Embers(at, 1.5f, 14, new Color(2f, 0.2f, 0.3f));
            }
        }

        void Add(Perk p)
        {
            Owned.Add(p);
            Version++;
            var s = Stats;
            if (s) p.apply?.Invoke(this, s);
        }

        /// <summary>Drops a relic in the world (falls back to gold when every relic is owned).</summary>
        public static void DropRelic(Vector3 position)
        {
            position.y = 0f;
            if (Instance && Instance.RelicsLeft)
            {
                RelicPickup.Spawn(position);
                return;
            }
            if (Loot.Instance) Loot.Instance.DropCoins(position + Vector3.up * 0.5f, 10, 3);
        }

        // ---------- Hooks called by Enemy ----------

        /// <summary>Damage multiplier for one hit on this enemy (relic "Marque du chasseur").</summary>
        public static float DamageTakenMult(Enemy e) => EliteDamageBonus > 0f && (e.IsElite || e.IsBoss) ? 1f + EliteDamageBonus : 1f;

        /// <summary>Every non-burn hit that didn't kill.</summary>
        public static void OnEnemyHit(Enemy e)
        {
            if (FreezeChance <= 0f || e.IsProp || Random.value >= FreezeChance) return;
            e.Slow(0.05f, 1.5f);
            if (Instance && Time.time >= Instance.nextFreezeFx && GameFX.Instance)
            {
                Instance.nextFreezeFx = Time.time + 0.05f;
                Vector3 p = e.transform.position;
                GameFX.Instance.Sparks(p + Vector3.up, FreezeColor, 6, 0.6f);
                GameFX.Instance.Ring(p, e.Radius + 0.6f, FreezeColor, 0.3f);
            }
        }

        /// <summary>Every death (props included), before the enemy is recycled.</summary>
        public static void OnEnemyKilled(Enemy e)
        {
            if (!Instance) return;
            Instance.Killed(e);
            if (WorldEvents.Instance) WorldEvents.Instance.OnEnemyKilled(e);
        }

        void Killed(Enemy e)
        {
            if (e.IsProp) return;

            if (vampireEvery > 0 && ++vampireKills >= vampireEvery)
            {
                vampireKills = 0;
                if (PlayerHealth.Instance && PlayerHealth.Instance.Fraction < 1f)
                {
                    PlayerHealth.Instance.Heal(vampireHeal);
                    var player = PlayerController.Instance;
                    if (GameFX.Instance && player) GameFX.Instance.Ring(player.Position, 1.5f, new Color(2.2f, 0.3f, 0.4f), 0.35f);
                }
            }

            if (e.IsElite && e.Affixes != EliteAffix.None && !e.IsBounty && Random.value < eliteRelicChance)
                DropRelic(e.transform.position);
        }

        // ---------- Player-side relics ----------

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm || gm.State != GameState.Playing || bellInterval <= 0f) return;
            bellTimer -= Time.deltaTime;
            if (bellTimer > 0f) return;
            bellTimer = bellInterval;

            var player = PlayerController.Instance;
            var s = Stats;
            if (!player || !s) return;
            Vector3 p = player.Position;
            float radius = 3.5f * s.areaMult;
            if (GameFX.Instance)
            {
                GameFX.Instance.Ring(p, radius, new Color(2.2f, 1.6f, 0.6f), 0.5f);
                GameFX.Instance.Ring(p, radius * 0.6f, new Color(1.5f, 1.1f, 0.4f), 0.35f);
            }
            SFX.Play(SfxId.Bell, 0.8f);
            Combat.Area(p, radius, 12f * WorldScale * s.damageMult, 14f, false);
        }

        void OnHurt(float amount)
        {
            var player = PlayerController.Instance;
            var health = PlayerHealth.Instance;
            if (!player || !health) return;

            if (phoenix && health.Current <= 0f)
            {
                // Called before PlayerHealth checks for death: healing here saves the run.
                phoenix = false;
                if (GameManager.Instance) GameManager.Instance.RecordNearDeath();
                health.Heal(health.Max * 0.5f);
                Version++;
                var s = Stats;
                Combat.Nova(player.Position, 7f, 30f * WorldScale * (s ? s.damageMult : 1f));
                if (GameHUD.Instance)
                {
                    GameHUD.Instance.ShowBanner("RENAISSANCE !\n<size=50>La plume de phénix brûle</size>", new Color(1f, 0.6f, 0.2f), 2.5f);
                    GameHUD.Instance.ScreenFlash(new Color(1f, 0.55f, 0.15f), 0.9f);
                }
                if (GameFX.Instance) GameFX.Instance.Embers(player.Position, 1.5f, 30, new Color(3f, 1.5f, 0.3f));
                SFX.Play(SfxId.Frenzy);
                return;
            }

            if (thorns && health.Current > 0f)
            {
                var s = Stats;
                float radius = 2.6f * (s ? s.areaMult : 1f);
                Combat.Area(player.Position, radius, (6f + amount) * WorldScale * (s ? s.damageMult : 1f), 12f, false);
                if (GameFX.Instance) GameFX.Instance.Ring(player.Position, radius, new Color(0.9f, 2f, 0.5f), 0.3f);
            }
        }

        /// <summary>Phoenix used: the HUD greys its icon.</summary>
        public bool IsSpent(Perk p) => p.id == "phoenix" && !phoenix;

        // ---------- Text ----------

        /// <summary>Lines for the pause screen.</summary>
        public string Describe()
        {
            if (Owned.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var p in Owned)
            {
                if (p.IsPact)
                    sb.Append($"<color=#FF6B7A><b>{p.name}</b></color>  <color={BoonHex}>{p.boon}</color> / <color={BaneHex}>{p.bane}</color>\n");
                else
                    sb.Append($"<color=#FFD06B><b>{p.name}</b></color>  {p.boon}{(IsSpent(p) ? " (utilisée)" : "")}\n");
            }
            return sb.ToString();
        }

        public static string BoonColor => BoonHex;
        public static string BaneColor => BaneHex;
    }
}
