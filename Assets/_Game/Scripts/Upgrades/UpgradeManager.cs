using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Survivor
{
    public enum UpgradeKind { Weapon, NewWeapon, Passive, Evolution, Bonus }

    /// <summary>Card quality: a rare card gives 2 levels at once, an epic one 3. Rolled after the item is drawn (never changes which item comes up).</summary>
    public enum UpgradeRarity { Commun, Rare, Epique }

    /// <summary>One card on the level-up screen.</summary>
    public class UpgradeOption
    {
        public UpgradeKind kind;
        /// <summary>Weapon Id or passive id. Empty for evolutions and rewards (heal, gold).</summary>
        public string id = "";
        public string title;
        public string levelLabel;
        public string description;
        /// <summary>Small extra line (evolution hint), may be empty.</summary>
        public string hint = "";
        public Color color;
        /// <summary>Families of the item on the card (for the HUD).</summary>
        public ItemTag tags;
        /// <summary>The item (or the weapon that evolves) is in the active Codex preset: the HUD shows a reminder badge. Never changes the draw.</summary>
        public bool inPreset;
        /// <summary>Commun = +1 level, Rare = +2, Épique = +3 (see <see cref="levels"/>).</summary>
        public UpgradeRarity rarity;
        /// <summary>Levels gained when picked.</summary>
        public int levels = 1;
        public Action apply;

        public void Apply() => apply?.Invoke();

        /// <summary>Weapons and bonuses can be banished for the rest of the run; evolutions and rewards can't.</summary>
        public bool CanBanish => !string.IsNullOrEmpty(id) &&
                                 (kind == UpgradeKind.Weapon || kind == UpgradeKind.NewWeapon || kind == UpgradeKind.Passive);

        /// <summary>Frame / label color of the rarity (white for common).</summary>
        public Color RarityColor => UpgradeManager.RarityColor(rarity);
    }

    /// <summary>A bonus picked during a run (Archero-style skill).</summary>
    public class Passive
    {
        public string id;
        public string name;
        public string description;
        public int maxLevel = 5;
        public int level;
        public Color color;
        /// <summary>Families this bonus counts for in the set bonuses (Synergies).</summary>
        public ItemTag tags;
        public Action<PlayerStats> apply;
    }

    /// <summary>
    /// Builds the level-up choices: new weapons, weapon levels, bonuses and evolutions.
    /// Limited slots (like Vampire Survivors) so every pick is a real choice.
    /// Cards have a rarity (bigger level jumps, rarer early), a hand is sometimes 4 cards instead of 3,
    /// and the player can banish an item from the pool for the run (theorycraft: steer the draw).
    /// Also gives the chosen starting weapon when a run begins.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        public Weapon startingWeapon;
        [Tooltip("How many different weapons the player can carry.")]
        public int maxWeapons = 5;
        [Tooltip("How many different bonuses the player can carry.")]
        public int maxPassives = 6;
        [Tooltip("STUFF: total levels a build can hold (weapon levels + bonus levels, the starting weapon's level 1 included). " +
                 "Evolutions are free. Once it's full, level-ups give a level burst instead of cards (GameManager).")]
        public int buildCap = 20;
        [Tooltip("Weapons left out of the card pool each run (never the starting weapon, never the weekly element's).")]
        public int absentWeapons = 3;

        /// <summary>Pale gold-white (violet belongs to the cult only, Univers 9.4).</summary>
        public static readonly Color EvolutionColor = new(1f, 0.84f, 0.55f);
        public static readonly Color RareColor = new(0.3f, 0.7f, 1f);
        public static readonly Color EpicColor = new(1f, 0.55f, 0.1f);
        static readonly Color HealColor = new(0.4f, 0.9f, 0.4f);

        // ---------- Tuning (card draw) ----------

        /// <summary>Draw weights: weapons stay about half of the draws.</summary>
        const float NewWeaponWeight = 2.8f, OwnedWeaponWeight = 3f, PassiveWeight = 1f, EvolutionPartnerWeight = 2f;
        /// <summary>A new weapon is forced into every hand while the player has fewer weapons than this.</summary>
        const int ForcedNewWeaponBelow = 2;
        /// <summary>Once the build has a shape, new items come up a bit less (slots fill around ~10 min instead of ~5).</summary>
        const int DampNewWeaponsFrom = 3, DampNewPassivesFrom = 4;
        const float LateNewItemMult = 0.8f;

        /// <summary>Rarity chance per card: 0 before the start level, then grows per level up to the cap. Luck adds on top.</summary>
        const int RareStartLevel = 6, EpicStartLevel = 15;
        const float RarePerLevel = 0.006f, RareMax = 0.08f, EpicPerLevel = 0.003f, EpicMax = 0.03f;
        const float LuckRare = 0.04f, LuckEpic = 0.015f;

        /// <summary>Every Nth level the hand has one more card; otherwise a small chance (+ luck).</summary>
        const int ExtraCardEvery = 10;
        const float ExtraCardChance = 0.05f, LuckExtraCard = 0.06f;

        Weapon[] weapons;
        PlayerStats stats;
        List<Passive> passives;
        bool runStarted;

        readonly HashSet<string> banished = new();
        readonly List<Weapon> absent = new();
        List<UpgradeOption> hand;
        int handSize = 3;
        int lastMilestone;
        bool announcedComplete;

        public IReadOnlyList<Weapon> Weapons => weapons;
        public IReadOnlyList<Passive> Passives => passives;
        public int RerollsLeft { get; private set; }
        public int BanishesLeft { get; private set; }
        /// <summary>Items removed from the pool for this run (weapon Ids / passive ids).</summary>
        public IReadOnlyCollection<string> Banished => banished;
        public bool IsBanished(string id) => banished.Contains(id);
        /// <summary>The hand on screen (after rerolls / banishes). Null before the first level-up.</summary>
        public IReadOnlyList<UpgradeOption> CurrentHand => hand;

        // ---------- Stuff (build budget) ----------

        /// <summary>Levels the build can hold this run (threat "Bagage léger" lowers it).</summary>
        public int BuildCap => Mathf.Max(5, buildCap - Threats.BuildCapCut);

        /// <summary>Levels in the build now: every weapon level plus every bonus level (evolutions are free).</summary>
        public int BuildLevels
        {
            get
            {
                int n = 0;
                if (weapons != null) foreach (var w in weapons) n += w.Level;
                if (passives != null) foreach (var p in passives) n += p.level;
                return n;
            }
        }

        /// <summary>Levels still free in the build.</summary>
        public int BuildRoom => Mathf.Max(0, BuildCap - BuildLevels);
        /// <summary>The stuff is full: no more weapon or bonus levels this run.</summary>
        public bool BuildComplete => BuildRoom <= 0;

        /// <summary>A maxed weapon whose partner bonus is owned can evolve (evolutions don't cost stuff levels).</summary>
        public bool EvolutionReady
        {
            get
            {
                if (weapons != null) foreach (var w in weapons) if (CanEvolve(w)) return true;
                return false;
            }
        }

        /// <summary>A level-up should open the card screen (otherwise GameManager plays a level burst).</summary>
        public bool HasChoices => !BuildComplete || EvolutionReady;

        /// <summary>Weapons left out of this run's card pool (chosen at the start, same rule for everyone).</summary>
        public IReadOnlyList<Weapon> AbsentWeapons => absent;
        public bool IsAbsent(Weapon w) => absent.Contains(w);

        /// <summary>"Batterie Tesla, Scie circulaire et Gaz toxique" (empty if none).</summary>
        public string AbsentNames()
        {
            if (absent.Count == 0) return "";
            var names = new List<string>();
            foreach (var w in absent) names.Add(w.displayName);
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " et " + names[names.Count - 1];
        }

        void Awake()
        {
            Instance = this;
            weapons = GetComponents<Weapon>();
            stats = GetComponent<PlayerStats>();
            passives = CreatePassives();
            // Synergy runtime (statuses, chain reactions). Added here so old scenes get it too.
            if (!GetComponent<Synergies>()) gameObject.AddComponent<Synergies>();
        }

        /// <summary>Set bonuses currently active (Pyromane I, Artificier II...).</summary>
        public List<Synergy> ActiveSynergies => Synergies.Active();

        static Passive P(string id, string name, string desc, Color color, ItemTag tags, Action<PlayerStats> apply, int max = 5) =>
            new() { id = id, name = name, description = desc, color = color, tags = tags, apply = apply, maxLevel = max };

        static List<Passive> CreatePassives()
        {
            Color red = new(1f, 0.35f, 0.3f), orange = new(1f, 0.6f, 0.25f), yellow = new(1f, 0.85f, 0.35f);
            Color green = new(0.5f, 0.95f, 0.45f), blue = new(0.45f, 0.75f, 1f), grey = new(0.75f, 0.78f, 0.82f);
            Color teal = new(0.35f, 0.95f, 0.8f), clover = new(0.55f, 1f, 0.55f);
            const ItemTag feu = ItemTag.Feu, explosif = ItemTag.Explosif, elec = ItemTag.Electrique, lame = ItemTag.Lame;
            const ItemTag balle = ItemTag.Balle, poison = ItemTag.Poison, survie = ItemTag.Survie;
            return new List<Passive>
            {
                // Evolution partners
                // +0.14 on the 1.15 base = +12% of the base damage per level (5/5: +61%).
                P("damage", "Rage", "+12% dégâts", red, lame, s => s.damageMult += 0.14f),
                P("cooldown", "Adrénaline", "Armes 8% plus rapides", yellow, balle, s => s.cooldownMult *= 0.92f),
                P("area", "Gros calibre", "+12% zone d'effet", orange, explosif, s => s.areaMult += 0.12f),
                P("regen", "Sang froid", "+0,4 PV/s (pause 2 s après un coup), +0,5 de plus après 4 s sans être touché", green, survie | poison, s =>
                {
                    s.regen += 0.4f;
                    s.calmRegen += 0.5f;
                }),
                P("duration", "Bidon d'essence", "+15% durée des effets et portée des scies", orange, feu | poison, s => s.durationMult += 0.15f),
                P("projectiles", "Chargeur étendu", "+1 projectile pour toutes les armes (sauf le gaz)", yellow, balle, s => s.extraProjectiles += 1, 2),
                P("corpse", "Cadavres explosifs", "+10% de chance qu'un ennemi tué explose", red, explosif, s => s.corpseExplodeChance += 0.1f),
                P("crit", "Œil de lynx", "+8% de critique, +15% dégâts critiques", yellow, lame | balle, s => { s.critChance += 0.08f; s.critMult += 0.15f; }),
                P("huile", "Huile de vidange", "+25% dégâts de brûlure", orange, feu, s => s.burnPower += 0.25f),
                // Archero-style tricks
                P("ricochet", "Ricochet", "Les balles rebondissent sur +1 ennemi (et +1 cible pour l'éclair)", blue, balle | elec, s => s.ricochets += 1, 3),
                P("burn", "Balles incendiaires", "Tous tes coups : +12% de chance d'enflammer. Les ricochets enflamment toujours", orange, feu, s => s.burnChance += 0.12f, 4),
                P("vampire", "Sangsue", "+5% de chance de rendre 3 PV par ennemi tué (2 PV/s max)", red, survie, s => s.lifeStealChance += 0.05f),
                P("powder", "Poudre noire", "+8% de chance qu'une balle explose à l'impact", yellow, explosif | balle, s => s.bulletExplodeChance += 0.08f),
                P("copper", "Fil de cuivre", "+8% de chance qu'un coup lance un arc sur 2 ennemis proches", blue, elec, s => s.shockChance += 0.08f),
                P("battery", "Acide de batterie", "Tes éclairs empoisonnent (+15% du coup par seconde). Éclair + poison = Surcharge", teal, elec | poison,
                    s => s.shockPoison += 0.15f),
                P("serrated", "Lame dentelée", "Les lames font saigner (+40% du coup en 3 s). Tes critiques aussi (moitié)", red, lame, s => s.bleedPower += 0.4f),
                P("hotblade", "Lame chauffée", "+12% de chance que tes lames enflamment. Lame sur ennemi en feu : Fer rouge", orange, lame | feu,
                    s => s.bladeIgnite += 0.12f, 4),
                P("acid", "Fiole d'acide", "+10% de chance d'empoisonner. Feu + poison = explosion toxique", green, poison, s => s.poisonChance += 0.1f),
                // Survival
                P("health", "Ration de combat", "+25 PV max (et soigne 25 PV)", green, survie, _ => PlayerHealth.Instance.AddMaxHealth(25f)),
                P("armor", "Gilet pare-balles", "-5% dégâts reçus", grey, survie, s => s.armor += 0.05f),
                P("laststand", "Dos au mur", "Plus tu es blessé, plus tu frappes fort : jusqu'à +12% dégâts", red, survie, s => s.lowHpDamage += 0.12f),
                P("speed", "Bottes de course", "+8% vitesse de déplacement", grey, ItemTag.None, s => s.moveSpeed *= 1.08f),
                P("magnet", "Collecteur rouillé", "+35% rayon de ramassage", blue, elec, s => s.pickupRadius *= 1.35f),
                P("xp", "Injecteur réglé", "+10% d'éclats : stuff rempli plus vite, puis plus de primes de niveau", blue, ItemTag.None, s => s.xpMult += 0.1f),
                P("luck", "Patte de lapin", Threats.NoSafetyNet ? "Plus de cartes rares et épiques, et de 4e choix"
                    : "Plus de cartes rares et épiques, et de 4e choix. +1 relance", clover, ItemTag.None, s =>
                {
                    s.luck += 1f;
                    if (Instance && !Threats.NoSafetyNet) Instance.RerollsLeft++; // threat "Sans filet": no reroll at all
                }, 3),
            };
        }

        public Passive FindPassive(string id) => passives.Find(p => p.id == id);

        // ---------- Run start ----------

        /// <summary>Gives the starting weapon (same base stats for everyone). Called once when the run starts.</summary>
        public void BeginRun()
        {
            if (runStarted) return;
            runStarted = true;

            RerollsLeft = stats.rerolls;
            BanishesLeft = stats.banishes;
            banished.Clear();
            handCount = exactCount = rerollCount = banishCount = 0;
            seeded = null;

            // Threats (Menaces / daily / weekly): fewer HP, mutator damage, no safety net. Same for everyone who has them.
            Threats.ApplyToPlayer(stats);
            if (Threats.NoSafetyNet) RerollsLeft = BanishesLeft = 0;

            // Daily / weekly force the start weapon (same for everyone); otherwise the one picked in the Codex.
            Weapon start = null;
            string id = string.IsNullOrEmpty(RunConfig.ForcedStartWeapon) ? Progression.StartingWeaponId : RunConfig.ForcedStartWeapon;
            foreach (var w in weapons)
                if (w.Id == id && Progression.IsUnlocked(w)) start = w;
            if (!start) start = startingWeapon ? startingWeapon : weapons.Length > 0 ? weapons[0] : null;
            if (start) start.LevelUp();
            PickAbsentWeapons(start);
            announcedComplete = false;
            Synergies.Refresh(false);

            if (absent.Count > 0 && GameHUD.Instance)
                GameHUD.Instance.QueueBanner($"<size=72>ARSENAL DE LA PARTIE</size>\n<size=38>Absentes : {AbsentNames()}</size>",
                    new Color(0.85f, 0.85f, 0.9f), 2.6f);
        }

        /// <summary>
        /// Arsenal of the run: a few weapons are left out of the card pool so two runs don't play the same.
        /// Never the starting weapon, never a weapon of the weekly element. Daily / weekly: from the seed (same for everyone).
        /// </summary>
        void PickAbsentWeapons(Weapon start)
        {
            absent.Clear();
            absent.AddRange(AbsentFor(start, RunConfig.Seed, RunConfig.BoostedTag));
        }

        /// <summary>
        /// The weapons a run leaves out: seeded (daily / weekly, same for everyone) when <paramref name="seed"/> isn't 0.
        /// Also used by the ranked rules page to show them before the run.
        /// </summary>
        public List<Weapon> AbsentFor(Weapon start, int seed, ItemTag boosted)
        {
            var result = new List<Weapon>();
            var candidates = new List<Weapon>();
            foreach (var w in weapons)
            {
                if (w == start || !Progression.IsUnlocked(w)) continue;
                if (boosted != ItemTag.None && (w.Tags & boosted) != 0) continue;
                candidates.Add(w);
            }
            // Keep at least 5 weapons in play (start included) whatever the setting.
            int count = Mathf.Clamp(absentWeapons, 0, Mathf.Max(0, weapons.Length - 5));
            System.Random seededPick = seed != 0 ? new System.Random(GameModes.Hash(seed, StreamArsenal * 1000003, 0)) : null;
            while (result.Count < count && candidates.Count > 0)
            {
                int i = seededPick != null ? seededPick.Next(0, candidates.Count) : Random.Range(0, candidates.Count);
                result.Add(candidates[i]);
                candidates.RemoveAt(i);
            }
            return result;
        }

        /// <summary>"Gaz toxique, Scie et Tesla" for a seeded run that starts with <paramref name="startId"/> (rules page).</summary>
        public string AbsentNamesFor(string startId, int seed, ItemTag boosted)
        {
            Weapon start = null;
            foreach (var w in weapons) if (w.Id == startId) start = w;
            var list = AbsentFor(start, seed, boosted);
            var names = new List<string>();
            foreach (var w in list) names.Add(w.displayName);
            if (names.Count <= 1) return names.Count == 1 ? names[0] : "";
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " et " + names[names.Count - 1];
        }

        // ---------- Seeded draws (daily / weekly) ----------

        /// <summary>
        /// When <see cref="RunConfig.Seed"/> is set, every random decision of the card draw (items, rarity, 4th card,
        /// shuffle, reroll and banish replacements) comes from a System.Random built from the seed and a draw counter:
        /// two players making the same picks see the same cards. Everything else in the run stays UnityEngine.Random.
        /// </summary>
        System.Random seeded;
        int handCount, exactCount, rerollCount, banishCount;

        const int StreamHand = 1, StreamExact = 2, StreamReroll = 3, StreamBanish = 4, StreamArsenal = 5;

        void SeedDraw(int stream, int index, int sub = 0)
        {
            seeded = RunConfig.Seed != 0 ? new System.Random(GameModes.Hash(RunConfig.Seed, stream * 1000003 + index, sub)) : null;
        }

        float Rand01() => seeded != null ? (float)seeded.NextDouble() : Random.value;
        int RandRange(int min, int maxExclusive) => seeded != null ? seeded.Next(min, maxExclusive) : Random.Range(min, maxExclusive);

        /// <summary>Weekly challenge: cards of the week's element come up twice as often (same for everyone in that mode).</summary>
        static float ElementBoost(ItemTag tags) => RunConfig.BoostedTag != ItemTag.None && (tags & RunConfig.BoostedTag) != 0 ? 2f : 1f;

        // ---------- Level-up cards ----------

        int OwnedWeapons()
        {
            int n = 0;
            foreach (var w in weapons) if (w.Level > 0) n++;
            return n;
        }

        int OwnedPassives()
        {
            int n = 0;
            foreach (var p in passives) if (p.level > 0) n++;
            return n;
        }

        bool CanEvolve(Weapon w)
        {
            // A banished weapon keeps its levels but never evolves (see TryBanish).
            if (w.Level == 0 || !w.IsMaxed || w.Evolved || banished.Contains(w.Id)) return false;
            var p = FindPassive(w.EvolutionPassive);
            return p != null && p.level > 0;
        }

        /// <summary>Green = condition already met (on the evolution hints).</summary>
        public const string DoneHex = "8CFF7A";

        /// <summary>"Évolution : Nv 5 + Adrénaline", each condition already met in green.</summary>
        string EvolutionHint(Weapon w)
        {
            if (w.Evolved) return "";
            var p = FindPassive(w.EvolutionPassive);
            if (p == null) return "";
            string lv = w.IsMaxed ? $"<color=#{DoneHex}>Nv {w.MaxLevel}</color>" : $"Nv {w.MaxLevel}";
            string partner = p.level > 0 ? $"<color=#{DoneHex}>{p.name}</color>" : p.name;
            return $"Évolution : {lv} + {partner}";
        }

        /// <summary>Joins two card hints with " · " when both fit on one line, else keeps the first non-empty one.</summary>
        static string JoinHint(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            if (string.IsNullOrEmpty(b)) return a;
            return VisibleLength(a) + VisibleLength(b) + 3 <= 44 ? $"{a} · {b}" : a;
        }

        /// <summary>Length without rich-text tags.</summary>
        static int VisibleLength(string s)
        {
            int n = 0;
            bool inTag = false;
            foreach (char c in s)
            {
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag) n++;
            }
            return n;
        }

        // ---------- Rarity / hand size ----------

        float Luck => stats ? stats.luck : 0f;
        static int PlayerLevel => PlayerXP.Instance ? PlayerXP.Instance.Level : 1;

        /// <summary>Chance for a card to be rare (2 levels), epic included.</summary>
        public float RareChance => Mathf.Clamp((PlayerLevel - RareStartLevel) * RarePerLevel, 0f, RareMax) + Luck * LuckRare;
        /// <summary>Chance for a card to be epic (3 levels).</summary>
        public float EpicChance => Mathf.Clamp((PlayerLevel - EpicStartLevel) * EpicPerLevel, 0f, EpicMax) + Luck * LuckEpic;

        public static Color RarityColor(UpgradeRarity r) => r switch
        {
            UpgradeRarity.Rare => RareColor,
            UpgradeRarity.Epique => EpicColor,
            _ => Color.white,
        };

        public static string RarityName(UpgradeRarity r) => r switch
        {
            UpgradeRarity.Rare => "RARE",
            UpgradeRarity.Epique => "ÉPIQUE",
            _ => "",
        };

        /// <summary>1, 2 or 3 levels, never more than the item has left nor than the stuff has room for.</summary>
        int RollJump(int room)
        {
            float epic = EpicChance, rare = RareChance;
            float r = Rand01();
            int jump = r < epic ? 3 : r < epic + rare ? 2 : 1;
            return Mathf.Clamp(jump, 1, Mathf.Max(1, Mathf.Min(room, BuildRoom)));
        }

        bool RollExtraCard()
        {
            int level = PlayerLevel;
            // Always roll first: a seeded draw consumes the same numbers whatever the level (daily / weekly stay in sync).
            float roll = Rand01();
            // Once per milestone level (chest hands at the same level don't all get it).
            if (level > 1 && level % ExtraCardEvery == 0 && level != lastMilestone)
            {
                lastMilestone = level;
                return true;
            }
            return roll < ExtraCardChance + Luck * LuckExtraCard;
        }

        // ---------- Draw ----------

        /// <summary>Something that can come up on a card (a weapon or a bonus) and its draw weight.</summary>
        class Candidate
        {
            public Weapon weapon;
            public Passive passive;
            public float weight;
            public string Id => weapon ? weapon.Id : passive.id;
            public bool IsNewWeapon => weapon && weapon.Level == 0;
        }

        /// <summary>Every weapon / bonus that can be offered now, minus banished ones and <paramref name="exclude"/>.</summary>
        List<Candidate> BuildPool(HashSet<string> exclude)
        {
            var pool = new List<Candidate>();
            // Full stuff: nothing can level up any more (evolutions are added by Roll, they're free).
            if (BuildComplete) return pool;

            int ownedWeapons = OwnedWeapons();
            foreach (var w in weapons)
            {
                if (w.IsMaxed || banished.Contains(w.Id) || (exclude != null && exclude.Contains(w.Id))) continue;
                bool isNew = w.Level == 0;
                if (isNew && (ownedWeapons >= maxWeapons || !Progression.IsUnlocked(w) || absent.Contains(w))) continue;
                // Weapons are the fun part: they come up about half the time. New ones that fit the build a bit more.
                float weight = isNew ? NewWeaponWeight * Synergies.CardWeight(w.Tags) : OwnedWeaponWeight;
                if (isNew && ownedWeapons >= DampNewWeaponsFrom) weight *= LateNewItemMult;
                weight *= ElementBoost(w.Tags);
                pool.Add(new Candidate { weapon = w, weight = weight });
            }

            int ownedPassives = OwnedPassives();
            foreach (var p in passives)
            {
                if (p.level >= p.maxLevel || (p.level == 0 && ownedPassives >= maxPassives)) continue;
                if (banished.Contains(p.id) || (exclude != null && exclude.Contains(p.id))) continue;
                float weight = PassiveWeight;
                foreach (var w in weapons)
                    if (w.Level > 0 && !w.Evolved && w.EvolutionPassive == p.id) weight = EvolutionPartnerWeight;
                // Bonuses that fit the build come up a bit more often.
                if (p.level == 0) weight *= Synergies.CardWeight(p.tags) * (ownedPassives >= DampNewPassivesFrom ? LateNewItemMult : 1f);
                weight *= ElementBoost(p.tags);
                pool.Add(new Candidate { passive = p, weight = weight });
            }
            return pool;
        }

        Candidate TakeWeighted(List<Candidate> pool)
        {
            float total = 0f;
            foreach (var c in pool) total += c.weight;
            float roll = Rand01() * total;
            int pick = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].weight;
                if (roll <= 0f)
                {
                    pick = i;
                    break;
                }
            }
            var chosen = pool[pick];
            pool.RemoveAt(pick);
            return chosen;
        }

        /// <summary>Card for a drawn candidate, with its rarity rolled now (the draw itself never looks at rarity).</summary>
        UpgradeOption MakeOption(Candidate c)
        {
            if (c.weapon) return WeaponOption(c.weapon, RollJump(c.weapon.MaxLevel - c.weapon.Level));
            return PassiveOption(c.passive, RollJump(c.passive.maxLevel - c.passive.level));
        }

        static string LevelLabel(bool isNew, string newLabel, int from, int to, UpgradeRarity rarity)
        {
            if (rarity == UpgradeRarity.Commun) return isNew ? newLabel : $"Nv {to}";
            string hex = ColorUtility.ToHtmlStringRGB(RarityColor(rarity));
            string tag = $"<color=#{hex}>{RarityName(rarity)}</color>";
            return isNew ? $"{tag} · Nv {to}" : $"{tag} · Nv {from}→{to}";
        }

        UpgradeOption WeaponOption(Weapon w, int jump)
        {
            var weapon = w;
            bool isNew = w.Level == 0;
            int to = w.Level + jump;
            var rarity = (UpgradeRarity)(jump - 1);

            string description = w.DescribeLevel(w.Level + 1);
            if (jump > 1)
            {
                var parts = new List<string>();
                for (int lv = w.Level + 1; lv <= to; lv++) parts.Add(w.DescribeLevel(lv));
                string joined = string.Join(" · ", parts);
                description = joined.Length <= 84 ? joined : $"{jump} niveaux d'un coup : {w.DescribeLevel(to)}";
            }

            return new UpgradeOption
            {
                kind = isNew ? UpgradeKind.NewWeapon : UpgradeKind.Weapon,
                id = w.Id,
                title = w.displayName,
                levelLabel = LevelLabel(isNew, "NOUVELLE ARME", w.Level, to, rarity),
                description = description,
                // New weapon: which set bonus it pushes first (that's what makes players chase combos).
                hint = isNew ? JoinHint(Synergies.CardHint(w.Tags), EvolutionHint(w)) : EvolutionHint(w),
                color = w.color,
                tags = w.Tags,
                inPreset = Presets.InActive(w.Id),
                rarity = rarity,
                levels = jump,
                apply = () =>
                {
                    bool wasNew = weapon.Level == 0;
                    for (int i = 0; i < jump && BuildRoom > 0; i++) weapon.LevelUp();
                    AfterPick(wasNew);
                },
            };
        }

        UpgradeOption PassiveOption(Passive p, int jump)
        {
            var passive = p;
            var rarity = (UpgradeRarity)(jump - 1);

            // Tell the player when a bonus is the key to one of their weapons' evolution (and how far that weapon is).
            string hint = "";
            Weapon partnerOf = null;
            foreach (var w in weapons)
            {
                if (w.Evolved || w.EvolutionPassive != p.id) continue;
                if (w.Level > 0)
                {
                    hint = p.level > 0 ? "" // already the partner: more levels don't change the evolution
                        : w.IsMaxed ? $"Fait évoluer : {w.displayName} <color=#{DoneHex}>tout de suite !</color>"
                        : $"Fait évoluer : {w.displayName} (Nv {w.Level}/{w.MaxLevel})";
                }
                else if (!absent.Contains(w)) partnerOf = w;
            }
            // ...and which set bonus a new one pushes.
            if (p.level == 0) hint = JoinHint(hint, Synergies.CardHint(p.tags));
            // Nothing better to say: the weapon it evolves (teaches the pairs).
            if (hint.Length == 0 && partnerOf) hint = $"Partenaire d'évolution : {partnerOf.displayName}";

            return new UpgradeOption
            {
                kind = UpgradeKind.Passive,
                id = p.id,
                title = p.name,
                levelLabel = LevelLabel(p.level == 0, "NOUVEAU BONUS", p.level, p.level + jump, rarity),
                description = jump > 1 ? $"<b>x{jump}</b>  {p.description}" : p.description,
                hint = hint,
                color = p.color,
                tags = p.tags,
                inPreset = Presets.InActive(p.id),
                rarity = rarity,
                levels = jump,
                apply = () =>
                {
                    bool wasNew = passive.level == 0;
                    for (int i = 0; i < jump && passive.level < passive.maxLevel && BuildRoom > 0; i++)
                    {
                        passive.level++;
                        passive.apply(stats);
                    }
                    AfterPick(wasNew);
                },
            };
        }

        /// <summary>Set bonus banners for a new item, then the "stuff complet" announce the first time the build is full.</summary>
        void AfterPick(bool wasNew)
        {
            if (wasNew) Synergies.Refresh(true);
            if (!BuildComplete || announcedComplete) return;
            announcedComplete = true;
            if (GameHUD.Instance)
                GameHUD.Instance.QueueBanner($"STUFF COMPLET !\n<size=42>{BuildCap} niveaux. Chaque niveau suivant :\nonde de choc + soin + prime de score</size>",
                    UIKit.Gold, 3.2f);
        }

        /// <summary>
        /// The level-up hand (called by GameManager with 3): usually <paramref name="count"/> cards,
        /// one more every 10th level or by luck. Remembered for reroll / banish.
        /// For a fixed number of cards (chest, event...), use <see cref="RollExact"/>.
        /// </summary>
        public List<UpgradeOption> RollOptions(int count)
        {
            SeedDraw(StreamHand, ++handCount);
            handSize = count + (RollExtraCard() ? 1 : 0);
            hand = Roll(handSize);
            return hand;
        }

        /// <summary>Exactly <paramref name="count"/> cards, without touching the level-up hand.</summary>
        public List<UpgradeOption> RollExact(int count)
        {
            SeedDraw(StreamExact, ++exactCount);
            return Roll(count);
        }

        public bool TryReroll(int count, out List<UpgradeOption> options)
        {
            options = null;
            if (RerollsLeft <= 0 || Threats.NoSafetyNet) return false;
            RerollsLeft--;
            SeedDraw(StreamReroll, handCount, ++rerollCount);
            // Keeps the size of the current hand (a 4-card hand stays 4 cards).
            handSize = Mathf.Max(count, handSize);
            hand = Roll(handSize);
            options = hand;
            return true;
        }

        /// <summary>
        /// Removes this card's item from the pool for the rest of the run and replaces only that card.
        /// An owned item keeps its levels but won't be offered again (so no evolution for a banished weapon).
        /// </summary>
        public bool TryBanish(UpgradeOption option, out List<UpgradeOption> options)
        {
            options = hand;
            if (BanishesLeft <= 0 || Threats.NoSafetyNet || option == null || !option.CanBanish || hand == null) return false;
            int index = hand.IndexOf(option);
            if (index < 0) return false;

            BanishesLeft--;
            banished.Add(option.id);
            SeedDraw(StreamBanish, handCount, ++banishCount);

            var exclude = new HashSet<string>();
            foreach (var o in hand)
                if (!string.IsNullOrEmpty(o.id)) exclude.Add(o.id);
            var pool = BuildPool(exclude);

            UpgradeOption replacement = pool.Count > 0 ? MakeOption(TakeWeighted(pool)) : null;
            if (replacement == null)
            {
                bool hasHeal = hand.Exists(o => o.kind == UpgradeKind.Bonus && o.title == HealTitle);
                replacement = hasHeal ? GoldOption() : HealOption();
            }

            hand = new List<UpgradeOption>(hand) { [index] = replacement };
            options = hand;
            return true;
        }

        List<UpgradeOption> Roll(int count)
        {
            var result = new List<UpgradeOption>();

            // Evolutions are the jackpot: always shown when possible.
            foreach (var w in weapons)
                if (CanEvolve(w)) result.Add(EvolutionOption(w));
            if (result.Count > count) result.RemoveRange(count, result.Count - count);
            int evolutions = result.Count;
            // Full stuff: the hand is the evolution(s) only. Offering a heal next to it would let the player keep the
            // evolution pending and farm a 50% heal on every later level.
            if (BuildComplete && evolutions > 0) return result;

            var pool = BuildPool(null);

            // Early on, always offer a new weapon so the build gets going (the 3rd one comes from the normal draw).
            if (OwnedWeapons() < ForcedNewWeaponBelow && result.Count < count)
            {
                var fresh = pool.FindAll(c => c.IsNewWeapon);
                if (fresh.Count > 0)
                {
                    var pick = fresh[RandRange(0, fresh.Count)];
                    pool.Remove(pick);
                    result.Add(MakeOption(pick));
                }
            }

            // A weapon at Nv 5 always gets its partner bonus offered: a focused stuff must be able to reach its evolution
            // (with ~25 bonuses in the pool, the partner could otherwise never show up before the stuff is full).
            foreach (var w in weapons)
            {
                if (result.Count >= count) break;
                if (w.Level == 0 || !w.IsMaxed || w.Evolved || banished.Contains(w.Id)) continue;
                var partner = pool.Find(c => c.passive != null && c.passive.id == w.EvolutionPassive && c.passive.level == 0);
                if (partner == null) continue;
                pool.Remove(partner);
                result.Add(MakeOption(partner));
            }

            // With a 20-level stuff, focusing must always be possible: every hand has a card that levels up a weapon
            // the player owns (as long as one can still level up).
            if (result.Count < count)
            {
                var owned = pool.FindAll(c => c.weapon && c.weapon.Level > 0);
                if (owned.Count > 0)
                {
                    var pick = TakeWeighted(owned);
                    pool.Remove(pick);
                    result.Add(MakeOption(pick));
                }
            }

            // Weighted draw without repeats.
            while (result.Count < count && pool.Count > 0) result.Add(MakeOption(TakeWeighted(pool)));

            // Shuffle everything after the evolutions so the forced weapon isn't always in the same spot.
            for (int i = result.Count - 1; i > evolutions; i--)
            {
                int j = RandRange(evolutions, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            // Everything maxed: offer rewards so level-ups still feel good.
            if (result.Count < count) result.Add(HealOption());
            if (result.Count < count) result.Add(GoldOption());
            return result;
        }

        UpgradeOption EvolutionOption(Weapon w)
        {
            var weapon = w;
            return new UpgradeOption
            {
                kind = UpgradeKind.Evolution,
                title = w.EvolutionName,
                levelLabel = "ÉVOLUTION !",
                description = w.EvolutionText,
                hint = $"{w.displayName} évolue · gratuit, hors stuff",
                color = EvolutionColor,
                tags = w.Tags,
                inPreset = Presets.InActive(w.Id),
                apply = () => weapon.Evolve(),
            };
        }

        const string HealTitle = "Trousse de soin";

        static UpgradeOption HealOption() => new()
        {
            kind = UpgradeKind.Bonus,
            title = HealTitle,
            levelLabel = "",
            description = "Rend 50% de tes PV",
            color = HealColor,
            apply = () => PlayerHealth.Instance.Heal(PlayerHealth.Instance.Max * 0.5f),
        };

        static UpgradeOption GoldOption() => new()
        {
            kind = UpgradeKind.Bonus,
            title = "Bourse",
            levelLabel = "",
            description = "+25 or",
            color = UIKit.Gold,
            apply = () => GameManager.Instance.AddGold(25),
        };
    }
}
