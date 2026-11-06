using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Survivor
{
    /// <summary>Families of weapons and bonuses. Owning several items of one family unlocks its set bonus.</summary>
    [Flags]
    public enum ItemTag
    {
        None = 0,
        Feu = 1 << 0,
        Explosif = 1 << 1,
        Electrique = 1 << 2,
        Lame = 1 << 3,
        Balle = 1 << 4,
        Poison = 1 << 5,
        Survie = 1 << 6,
    }

    [Flags]
    public enum HitFlags
    {
        None = 0,
        /// <summary>Always a critical hit.</summary>
        Crit = 1,
        /// <summary>Always sets the enemy on fire.</summary>
        Ignite = 2,
        /// <summary>Secondary hit (arc, burst...): no procs, so chain reactions stay finite.</summary>
        NoProc = 4,
    }

    /// <summary>A named set bonus (archetype): tier I at 2 items of its tag, tier II at 4.</summary>
    public class Synergy
    {
        public string name;
        public ItemTag tag;
        public Color color;
        public string tier1, tier2;

        public int Count { get; internal set; }
        public int Tier => Count >= Synergies.Tier2Count ? 2 : Count >= Synergies.Tier1Count ? 1 : 0;
        public string Hex => ColorUtility.ToHtmlStringRGB(color);
        public string TierName => Tier switch { 2 => name + " II", 1 => name + " I", _ => name };
        /// <summary>What is active right now (both lines at tier II).</summary>
        public string ActiveText => Tier >= 2 ? $"{tier1}. {tier2}." : Tier == 1 ? tier1 + "." : "";
    }

    /// <summary>
    /// Everything that makes items work together:
    /// - set bonuses (Pyromane, Artificier, Électricien, Boucher, Pistolero, Alchimiste, Increvable),
    /// - elemental reactions (fire + poison = toxic burst, explosion on a burning enemy = fusion, crit on burning = spread,
    ///   lightning on slowed/poisoned = overload, ricochets carry fire...),
    /// - bleed and poison damage over time, and status tracking (Enemy has no getters for burning/slowed, so we keep our own).
    /// Weapons call Synergies.Hit / Explosion / Ignite / Poison / Slow instead of Combat directly.
    /// Every chain reaction is rate-limited and secondary hits never proc, so nothing can loop forever.
    /// </summary>
    [DisallowMultipleComponent]
    public class Synergies : MonoBehaviour
    {
        public static Synergies Instance { get; private set; }

        public const int Tier1Count = 2, Tier2Count = 4;

        // ---------- Set bonuses ----------

        public static readonly Synergy Pyromane = new()
        {
            name = "Pyromane", tag = ItemTag.Feu, color = new Color(1f, 0.55f, 0.15f),
            tier1 = "Brûlure +40% dégâts", tier2 = "Les brûlés qui meurent enflamment leurs voisins",
        };
        public static readonly Synergy Artificier = new()
        {
            name = "Artificier", tag = ItemTag.Explosif, color = new Color(1f, 0.8f, 0.3f),
            tier1 = "Explosions +25% de zone", tier2 = "Les explosions mortelles font une réplique",
        };
        public static readonly Synergy Electricien = new()
        {
            name = "Électricien", tag = ItemTag.Electrique, color = new Color(0.45f, 0.75f, 1f),
            tier1 = "Éclairs : +2 cibles et sauts +30%", tier2 = "Chaque critique libère un arc électrique",
        };
        public static readonly Synergy Boucher = new()
        {
            name = "Boucher", tag = ItemTag.Lame, color = new Color(1f, 0.3f, 0.3f),
            tier1 = "Les lames font saigner", tier2 = "Ennemis qui saignent : +30% dégâts subis",
        };
        public static readonly Synergy Pistolero = new()
        {
            name = "Pistolero", tag = ItemTag.Balle, color = new Color(1f, 0.85f, 0.35f),
            tier1 = "Balles : +1 ricochet", tier2 = "Une balle sur 6 : critique explosif",
        };
        public static readonly Synergy Alchimiste = new()
        {
            name = "Alchimiste", tag = ItemTag.Poison, color = new Color(0.5f, 0.95f, 0.3f),
            tier1 = "Poison 2x plus long, il ralentit", tier2 = "Les empoisonnés qui meurent libèrent un nuage toxique",
        };
        public static readonly Synergy Increvable = new()
        {
            name = "Increvable", tag = ItemTag.Survie, color = new Color(0.4f, 0.9f, 0.6f),
            tier1 = "Riposte : être touché crée une onde de choc", tier2 = "Riposte x2, soigne 0,5 PV par ennemi touché (4 max)",
        };

        public static readonly Synergy[] All = { Pyromane, Artificier, Electricien, Boucher, Pistolero, Alchimiste, Increvable };

        /// <summary>
        /// Always-on elemental reactions, as shown in the Codex (name, rule). Keep in sync with Hit / Explosion / CheckToxic below.
        /// </summary>
        public static readonly string[][] Reactions =
        {
            new[] { "Explosion toxique", "Feu + poison sur le même ennemi : il explose en nuage toxique" },
            new[] { "Fusion", "Une explosion qui tue un ennemi en feu provoque une explosion de flammes" },
            new[] { "Propagation", "Un critique sur un ennemi en feu enflamme 2 voisins" },
            new[] { "Surcharge", "Électricité sur un ennemi ralenti ou empoisonné : +30% dégâts" },
            new[] { "Corrosion", "Les ennemis empoisonnés subissent +10% dégâts" },
            new[] { "Balles traçantes", "Avec une chance d'enflammer, les ricochets enflamment toujours" },
            new[] { "Hémorragie", "Avec Lame dentelée, les critiques de n'importe quelle arme font saigner" },
            new[] { "Électrocution", "Électricité sur un ennemi qui saigne : tout le saignement restant éclate d'un coup" },
            new[] { "Plasma", "Électricité sur un ennemi en feu : il explose en plasma" },
            new[] { "Dispersion", "Une explosion qui touche un ennemi empoisonné empoisonne toute la zone" },
            new[] { "Fer rouge", "Les lames font +25% dégâts aux ennemis en feu" },
        };

        // ---------- Tuning ----------

        const float DotTick = 0.5f;
        const int MaxTracked = 512;
        const int MaxPending = 48;
        const float ReactionCooldown = 1.5f;

        // Reactions / survival numbers (easy to tweak).
        const float CorrosionMult = 1.1f, OverloadMult = 1.3f, BoucherIIMult = 1.3f, FerRougeMult = 1.25f;
        /// <summary>Électrocution: remaining bleed dealt at once, times this.</summary>
        const float ElectrocutionMult = 1.25f;
        const float PlasmaRadius = 1.6f, PlasmaDamage = 0.5f;
        /// <summary>Dispersion: share of the strongest poison in the blast given to every enemy hit.</summary>
        const float DispersionShare = 0.7f;
        /// <summary>Sang froid: calm regen starts after this long without being hit.</summary>
        const float CalmDelay = 4f;
        /// <summary>Increvable II: HP healed per enemy hit by the riposte, and the cap per riposte.</summary>
        const float RiposteHealPerHit = 0.5f, RiposteHealMax = 4f;
        public static readonly Color PlasmaColor = new(1.6f, 0.6f, 2.6f);

        public static readonly Color FireColor = new(1f, 0.45f, 0.1f);
        public static readonly Color ToxicColor = new(0.45f, 1f, 0.2f);
        public static readonly Color ArcColor = new(0.7f, 1.3f, 3f);
        public static readonly Color FusionColor = new(1f, 0.3f, 0.05f);
        public static readonly Color PistoleroColor = new(1f, 0.85f, 0.3f);

        // ---------- Runtime state ----------

        class Status
        {
            public Enemy enemy;
            /// <summary>enemy.SpawnId when recorded: a pooled enemy starting a new life has another one (-1 = dead, invalid).</summary>
            public int spawn;
            /// <summary>enemy.MaxHealth when recorded (toxic cloud damage).</summary>
            public float life;
            public Vector3 pos;
            public float burnUntil, burnDps, poisonUntil, poisonDps, bleedUntil, bleedDps, slowUntil;
            public float nextTick, nextReaction;
            public bool tracked;

            public void Reset(int newSpawn, float newLife)
            {
                spawn = newSpawn;
                life = newLife;
                burnUntil = poisonUntil = bleedUntil = slowUntil = 0f;
                burnDps = poisonDps = bleedDps = 0f;
                nextTick = nextReaction = 0f;
            }

            public bool Active(float now) => burnUntil > now || poisonUntil > now || bleedUntil > now;
        }

        enum Fx { Explosion, Aftershock, Fusion, Toxic, Arc, Riposte }

        struct Pending
        {
            public Fx kind;
            public Vector3 pos;
            public float radius, damage, knockback, time;
            public Color color;
            public ItemTag source;
            public Enemy origin;
        }

        /// <summary>Token bucket: at most `perSecond` events per second, with a small burst.</summary>
        class Limiter
        {
            readonly float rate, cap;
            float tokens, last;

            public Limiter(float perSecond, float burst)
            {
                rate = perSecond;
                cap = burst;
                tokens = burst;
            }

            public bool Take()
            {
                float now = Time.time;
                tokens = Mathf.Min(cap, tokens + (now - last) * rate);
                last = now;
                if (tokens < 1f) return false;
                tokens -= 1f;
                return true;
            }
        }

        readonly Dictionary<Enemy, Status> statuses = new(512);
        readonly List<Status> tracked = new(256);
        readonly List<Pending> pending = new(MaxPending);
        readonly List<Enemy> scratch = new(8);

        readonly Limiter spreadLimiter = new(14f, 10f);
        readonly Limiter toxicLimiter = new(6f, 4f);
        readonly Limiter fusionLimiter = new(5f, 3f);
        readonly Limiter aftershockLimiter = new(6f, 4f);
        readonly Limiter arcLimiter = new(14f, 8f);
        readonly Limiter cloudLimiter = new(4f, 3f);
        readonly Limiter plasmaLimiter = new(5f, 3f);
        readonly Limiter dispersionLimiter = new(4f, 3f);

        static int bulletCount;
        float nextRiposte, lastHurt = -99f;
        PlayerHealth hookedHealth;

        static PlayerStats S => PlayerController.Instance ? PlayerController.Instance.Stats : null;

        void Awake()
        {
            Instance = this;
            bulletCount = 0;
            foreach (var syn in All) syn.Count = 0;
        }

        void Start()
        {
            hookedHealth = PlayerHealth.Instance;
            if (hookedHealth) hookedHealth.Hurt += OnHurt;
        }

        void OnDestroy()
        {
            if (hookedHealth) hookedHealth.Hurt -= OnHurt;
            if (Instance == this) Instance = null;
        }

        // ---------- Set bonus bookkeeping ----------

        /// <summary>Recounts items per tag. With <paramref name="announce"/>, shows a banner for each newly reached tier.</summary>
        public static void Refresh(bool announce)
        {
            var um = UpgradeManager.Instance;
            if (!um) return;

            StringBuilder banner = null;
            Color bannerColor = Color.white;
            foreach (var syn in All)
            {
                int before = syn.Tier;
                int n = 0;
                var weapons = um.Weapons;
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i].Level > 0 && (weapons[i].Tags & syn.tag) != 0) n++;
                var passives = um.Passives;
                for (int i = 0; i < passives.Count; i++)
                    if (passives[i].level > 0 && (passives[i].tags & syn.tag) != 0) n++;
                syn.Count = n;

                if (!announce || syn.Tier <= before) continue;
                banner ??= new StringBuilder();
                if (banner.Length > 0) banner.Append('\n');
                banner.Append(syn.TierName.ToUpperInvariant()).Append(" !\n<size=44>")
                    .Append(syn.Tier >= 2 ? syn.tier2 : syn.tier1).Append("</size>");
                bannerColor = syn.color;
                Celebrate(syn.color);
            }

            if (banner != null && GameHUD.Instance) GameHUD.Instance.ShowBanner(banner.ToString(), bannerColor, 3f);
        }

        static void Celebrate(Color color)
        {
            var pc = PlayerController.Instance;
            if (!pc || !GameFX.Instance) return;
            Vector3 p = pc.Position;
            GameFX.Instance.Ring(p, 5f, color * 2.5f, 0.6f);
            GameFX.Instance.Sparks(p + Vector3.up, color * 2f, 40, 1.6f);
        }

        /// <summary>Set bonuses currently active (for the HUD / pause screen).</summary>
        public static List<Synergy> Active()
        {
            var list = new List<Synergy>();
            foreach (var syn in All)
                if (syn.Tier > 0) list.Add(syn);
            return list;
        }

        /// <summary>Rich-text lines describing the active set bonuses, empty if none.</summary>
        public static string DescribeActive()
        {
            var sb = new StringBuilder();
            foreach (var syn in All)
            {
                if (syn.Tier == 0) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<color=#").Append(syn.Hex).Append('>').Append(syn.TierName).Append("</color>  ").Append(syn.ActiveText);
            }
            return sb.ToString();
        }

        public static string TagName(ItemTag t) => t switch
        {
            ItemTag.Feu => "Feu",
            ItemTag.Explosif => "Explosif",
            ItemTag.Electrique => "Électrique",
            ItemTag.Lame => "Lame",
            ItemTag.Balle => "Balle",
            ItemTag.Poison => "Poison",
            ItemTag.Survie => "Survie",
            _ => "",
        };

        /// <summary>"Feu · Explosif" (for cards).</summary>
        public static string TagNames(ItemTag tags)
        {
            var sb = new StringBuilder();
            foreach (var syn in All)
            {
                if ((tags & syn.tag) == 0) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append("<color=#").Append(syn.Hex).Append('>').Append(TagName(syn.tag)).Append("</color>");
            }
            return sb.ToString();
        }

        /// <summary>Card line for an item not owned yet: which set bonus it pushes, e.g. "Débloque Pyromane I !".</summary>
        public static string CardHint(ItemTag tags)
        {
            Synergy best = null, second = null;
            int bestScore = -1;
            foreach (var syn in All)
            {
                if ((tags & syn.tag) == 0) continue;
                int after = syn.Count + 1;
                if (after > Tier2Count) continue; // already maxed, nothing to gain
                int score = after == Tier1Count || after == Tier2Count ? 100 + after : after;
                if (score > bestScore)
                {
                    second = best;
                    best = syn;
                    bestScore = score;
                }
                else if (second == null) second = syn;
            }
            if (best == null) return "";

            int a = best.Count + 1;
            string name = $"<color=#{best.Hex}>{best.name}</color>";
            string text = a == Tier2Count ? $"Débloque {name} II !"
                : a == Tier1Count ? $"Débloque {name} I !"
                : $"+1 {name} ({a}/{(a < Tier1Count ? Tier1Count : Tier2Count)})";
            if (second != null) text += $" · +<color=#{second.Hex}>{second.name}</color>";
            return text;
        }

        /// <summary>Draw weight multiplier for a new item that fits what the player already owns (x1 to x1.8).</summary>
        public static float CardWeight(ItemTag tags)
        {
            float w = 1f;
            foreach (var syn in All)
            {
                if ((tags & syn.tag) == 0 || syn.Count == 0 || syn.Count >= Tier2Count) continue;
                w += 0.2f * Mathf.Min(syn.Count, 3);
                if (syn.Count + 1 == Tier1Count || syn.Count + 1 == Tier2Count) w += 0.15f;
            }
            return Mathf.Min(w, 1.8f);
        }

        // ---------- Read by weapons ----------

        /// <summary>Burn damage multiplier (Huile de vidange, Pyromane I).</summary>
        public static float BurnMult
        {
            get
            {
                var s = S;
                return (s ? s.burnPower : 1f) * (Pyromane.Tier >= 1 ? 1.4f : 1f);
            }
        }

        /// <summary>Extra enemies per lightning chain.</summary>
        public static int ExtraChains => Electricien.Tier >= 1 ? 2 : 0;

        /// <summary>Lightning jump distance multiplier from this enemy: wet (poisoned) or slowed enemies conduct better.</summary>
        public static float ChainRange(Enemy from) => (Electricien.Tier >= 1 ? 1.3f : 1f) * (IsConductive(from) ? 1.5f : 1f);

        public static int ExtraRicochets => Pistolero.Tier >= 1 ? 1 : 0;

        /// <summary>Pistolero II: every 6th bullet fired is an exploding crit.</summary>
        public static bool NextSpecialBullet()
        {
            if (Pistolero.Tier < 2) return false;
            bulletCount++;
            return bulletCount % 6 == 0;
        }

        public static bool IsBurning(Enemy e) => Instance && e && Instance.TryPeek(e, out var st) && st.burnUntil > Time.time;
        public static bool IsPoisoned(Enemy e) => Instance && e && Instance.TryPeek(e, out var st) && st.poisonUntil > Time.time;

        public static bool IsConductive(Enemy e)
        {
            if (!e) return false;
            if (e.IsSlowed) return true; // any slow counts (gas, Alchimiste, frost relic...)
            return Instance && Instance.TryPeek(e, out var st) && st.poisonUntil > Time.time;
        }

        // ---------- Hits ----------

        /// <summary>
        /// A hit from a player weapon: Combat.Hit plus synergy procs (burn, bleed, poison, shock, fire spread)
        /// and damage taken bonuses (poisoned, bleeding, overload). Returns true if it killed.
        /// </summary>
        public static bool Hit(Enemy e, float damage, Vector3 dir, float knockback, ItemTag source, HitFlags flags = HitFlags.None)
        {
            if (!e || e.IsDead) return false;
            bool forcedCrit = (flags & HitFlags.Crit) != 0;
            var I = Instance;
            var s = S;
            if (!I || !s) return Combat.Hit(e, damage, dir, knockback, forcedCrit);

            float now = Time.time;
            var st = I.Get(e);
            bool burning = st.burnUntil > now, poisoned = st.poisonUntil > now;
            bool bleeding = st.bleedUntil > now, slowed = st.slowUntil > now || e.IsSlowed;
            bool electric = (source & ItemTag.Electrique) != 0, blade = (source & ItemTag.Lame) != 0;

            if (poisoned) damage *= CorrosionMult;                            // corrosion
            if (bleeding && Boucher.Tier >= 2) damage *= BoucherIIMult;
            if (electric && (slowed || poisoned)) damage *= OverloadMult;     // overload
            if (blade && burning) damage *= FerRougeMult;                     // fer rouge
            // Dos au mur: the more HP is missing, the harder every hit.
            if (s.lowHpDamage > 0f && PlayerHealth.Instance) damage *= 1f + s.lowHpDamage * (1f - PlayerHealth.Instance.Fraction);
            // Électrocution: lightning makes the remaining bleed burst at once.
            if (electric && bleeding)
            {
                damage += st.bleedDps * (st.bleedUntil - now) * ElectrocutionMult;
                st.bleedUntil = now;
                if (GameFX.Instance) GameFX.Instance.Blood(e.transform.position + Vector3.up, 6);
            }

            bool crit = forcedCrit || Random.value < s.critChance;

            // Combat.Hit would roll crit and burn itself. We roll them here (to react to them), so its own rolls are off.
            // (Was done by zeroing the stats for the call: anything changing them during a kill was then overwritten.)
            float burnChance = s.burnChance;
            bool killed = Combat.Hit(e, damage, dir, knockback, crit, false);

            float dealt = crit ? damage * s.critMult : damage;

            // Death effects (fire spread, toxic cloud) happen where it died, not where the status was last seen.
            if (killed) st.pos = e.transform.position;

            if ((flags & HitFlags.NoProc) != 0)
            {
                if (killed)
                {
                    I.Killed(st);
                }
                // Acide de batterie also works on arcs (no proc chain: poison never hits by itself).
                else if (electric && s.shockPoison > 0f) Poison(e, dealt * s.shockPoison + 1f, 3f * s.durationMult);
                return killed;
            }

            Vector3 p = e.transform.position;

            // Shock: Fil de cuivre on any hit, or every crit with Électricien II.
            if ((s.shockChance > 0f && Random.value < s.shockChance) || (crit && Electricien.Tier >= 2))
                I.QueueArc(e, p, dealt * 0.5f);

            // Crit on a burning enemy: the fire jumps to its neighbours.
            if (crit && burning) I.SpreadFire(p, Mathf.Max(st.burnDps, dealt * 0.3f), 2, 2.5f, e);

            // Plasma: lightning on a burning enemy bursts (queued: we may be inside an enemy loop).
            if (electric && burning && I.plasmaLimiter.Take())
            {
                float area = s.areaMult;
                I.Queue(Fx.Fusion, p, PlasmaRadius * area, dealt * PlasmaDamage, 0.05f, null, 4f, PlasmaColor, ItemTag.Feu | ItemTag.Electrique);
            }

            if (killed)
            {
                I.Killed(st);
                return true;
            }

            float igniteChance = burnChance + (blade ? s.bladeIgnite : 0f);
            if ((flags & HitFlags.Ignite) != 0 || (igniteChance > 0f && Random.value < igniteChance))
                Ignite(e, dealt * 0.4f + 2f, 3f * s.durationMult);

            if (electric && s.shockPoison > 0f) Poison(e, dealt * s.shockPoison + 1f, 3f * s.durationMult);

            float bleed = BleedPower(source, crit, s);
            if (bleed > 0f) Bleed(e, dealt * bleed / 3f, 3f * s.durationMult);

            if (s.poisonChance > 0f && Random.value < s.poisonChance)
                Poison(e, dealt * 0.2f + 1f, 3f * s.durationMult);

            return false;
        }

        static float BleedPower(ItemTag source, bool crit, PlayerStats s)
        {
            float power = s.bleedPower + (Boucher.Tier >= 1 ? 0.5f : 0f);
            if ((source & ItemTag.Lame) != 0) return power;
            // Lame dentelée: crits from any weapon bleed too (half as much).
            return crit && s.bleedPower > 0f ? power * 0.5f : 0f;
        }

        /// <summary>Hits every enemy in a circle (no FX). Returns the number hit.</summary>
        public static int Area(Vector3 center, float radius, float damage, float knockback, ItemTag source, HitFlags flags = HitFlags.None)
        {
            int hits = 0;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 d = e.transform.position - center;
                d.y = 0f;
                float r = radius + e.Radius;
                if (d.sqrMagnitude > r * r) continue;
                hits++;
                Hit(e, damage, d, knockback, source, flags);
            }
            return hits;
        }

        /// <summary>Explosion from a player weapon, with Artificier, fusion (kills a burning enemy) and aftershocks.</summary>
        public static int Explosion(Vector3 center, float radius, float damage, float knockback, Color color, ItemTag source)
        {
            if (!Instance) return Combat.Explosion(center, radius, damage, knockback, color);
            return Instance.Explode(center, radius, damage, knockback, color, source | ItemTag.Explosif, Fx.Explosion);
        }

        /// <summary>Same, a frame later. Use it while looping over Enemy.Active (e.g. from a bullet).</summary>
        public static void QueueExplosion(Vector3 center, float radius, float damage, float knockback, Color color, ItemTag source)
        {
            if (!Instance)
            {
                Combat.Explosion(center, radius, damage, knockback, color);
                return;
            }
            Instance.Queue(Fx.Explosion, center, radius, damage, 0f, null, knockback, color, source | ItemTag.Explosif);
        }

        int Explode(Vector3 center, float radius, float damage, float knockback, Color color, ItemTag source, Fx kind)
        {
            center.y = 0f;
            if (kind == Fx.Explosion && Artificier.Tier >= 1) radius *= 1.25f;

            var fx = GameFX.Instance;
            if (fx) fx.Explosion(center, radius, color);
            if (CameraFollow.Instance) CameraFollow.Instance.Shake(Mathf.Min(0.25f, 0.04f * radius));

            float now = Time.time;
            int hits = 0, kills = 0;
            bool fusion = false;
            float spreadPoison = 0f;
            Vector3 fusionAt = default, killAt = default;
            HitFlags flags = kind == Fx.Explosion ? HitFlags.None : HitFlags.NoProc;

            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                Vector3 ep = e.transform.position;
                Vector3 d = ep - center;
                d.y = 0f;
                float r = radius + e.Radius;
                if (d.sqrMagnitude > r * r) continue;

                hits++;
                bool known = TryPeek(e, out var st);
                bool burning = known && st.burnUntil > now;
                if (known && st.poisonUntil > now) spreadPoison = Mathf.Max(spreadPoison, st.poisonDps);
                if (kind == Fx.Toxic) Poison(e, damage * 0.15f + 1f, 2f);
                if (!Hit(e, damage, d, knockback, source, flags)) continue;

                if (kills++ == 0) killAt = ep;
                if (burning && !fusion)
                {
                    fusion = true;
                    fusionAt = ep;
                }
            }

            // Only primary explosions make secondary ones: no endless chains.
            if (kind == Fx.Explosion)
            {
                if (fusion && fusionLimiter.Take())
                    Queue(Fx.Fusion, fusionAt, radius * 0.75f, damage * 0.6f, 0.1f, null, 6f, FusionColor, ItemTag.Feu);
                if (kills > 0 && Artificier.Tier >= 2 && aftershockLimiter.Take())
                    Queue(Fx.Aftershock, killAt, radius * 0.8f, damage * 0.6f, 0.2f, null, knockback * 0.6f, color, ItemTag.Explosif);
                // Dispersion: the blast sprays the poison it found on everyone it touched.
                if (spreadPoison > 0f && hits > 1 && dispersionLimiter.Take()) Disperse(center, radius, spreadPoison * DispersionShare);
            }
            return hits;
        }

        /// <summary>Poisons every living enemy in the circle (no hit, so nothing can chain from here).</summary>
        void Disperse(Vector3 center, float radius, float dps)
        {
            var s = S;
            float duration = 3f * (s ? s.durationMult : 1f);
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.IsDead || e.IsProp) continue;
                Vector3 d = e.transform.position - center;
                d.y = 0f;
                float r = radius + e.Radius;
                if (d.sqrMagnitude <= r * r) Poison(e, dps, duration);
            }
            if (GameFX.Instance) GameFX.Instance.Ring(center, radius, ToxicColor * 2f, 0.35f);
        }

        // ---------- Statuses ----------

        /// <summary>Sets an enemy on fire (dps is scaled by Huile de vidange and Pyromane).</summary>
        public static void Ignite(Enemy e, float dps, float duration) => IgniteRaw(e, dps * BurnMult, duration);

        static void IgniteRaw(Enemy e, float dps, float duration)
        {
            if (!e || e.IsDead || dps <= 0f) return;
            e.Ignite(dps, duration);
            var I = Instance;
            if (!I) return;
            float now = Time.time;
            var st = I.Get(e);
            st.burnDps = st.burnUntil > now ? Mathf.Max(st.burnDps, dps) : dps;
            st.burnUntil = Mathf.Max(st.burnUntil, now + duration);
            I.Track(st);
            I.CheckToxic(st);
        }

        /// <summary>Poison: damage over time, and poisoned enemies take +10% damage and conduct lightning.</summary>
        public static void Poison(Enemy e, float dps, float duration)
        {
            if (!e || e.IsDead) return;
            var I = Instance;
            if (!I) return;
            if (Alchimiste.Tier >= 1)
            {
                duration *= 2f;
                Slow(e, 0.75f, duration);
            }
            float now = Time.time;
            var st = I.Get(e);
            st.poisonDps = st.poisonUntil > now ? Mathf.Max(st.poisonDps, dps) : dps;
            st.poisonUntil = Mathf.Max(st.poisonUntil, now + duration);
            I.Track(st);
            I.CheckToxic(st);
        }

        /// <summary>Bleeding: damage over time (blades). Boucher II makes bleeding enemies take +30% damage.</summary>
        public static void Bleed(Enemy e, float dps, float duration)
        {
            if (!e || e.IsDead || dps <= 0f) return;
            var I = Instance;
            if (!I) return;
            float now = Time.time;
            var st = I.Get(e);
            st.bleedDps = st.bleedUntil > now ? Mathf.Max(st.bleedDps, dps) : dps;
            st.bleedUntil = Mathf.Max(st.bleedUntil, now + duration);
            I.Track(st);
        }

        /// <summary>Enemy.Slow, remembered so lightning knows the enemy is slowed.</summary>
        public static void Slow(Enemy e, float factor, float duration)
        {
            if (!e || e.IsDead) return;
            e.Slow(factor, duration);
            var I = Instance;
            if (!I) return;
            var st = I.Get(e);
            st.slowUntil = Mathf.Max(st.slowUntil, Time.time + duration);
        }

        /// <summary>Fire + poison on the same enemy: toxic burst (once per enemy every 1.5 s).</summary>
        void CheckToxic(Status st)
        {
            float now = Time.time;
            if (st.burnUntil <= now || st.poisonUntil <= now || now < st.nextReaction) return;
            if (!toxicLimiter.Take()) return;
            st.nextReaction = now + ReactionCooldown;
            var s = S;
            float area = s ? s.areaMult : 1f;
            float damage = 8f + 3f * Mathf.Max(st.burnDps, st.poisonDps);
            Queue(Fx.Toxic, st.enemy.transform.position, 2f * area, damage, 0.05f, null, 4f, ToxicColor, ItemTag.Poison | ItemTag.Feu);
        }

        /// <summary>Sets up to <paramref name="count"/> nearby enemies that aren't burning yet on fire.</summary>
        void SpreadFire(Vector3 p, float dps, int count, float radius, Enemy exclude)
        {
            if (dps <= 0f || !spreadLimiter.Take()) return;
            var s = S;
            float duration = 2.5f * (s ? s.durationMult : 1f);
            float r2 = radius * radius;
            float now = Time.time;
            int done = 0;
            var fx = GameFX.Instance;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count && done < count; i++)
            {
                var e = list[i];
                if (e == exclude || e.IsDead) continue;
                Vector3 ep = e.transform.position;
                Vector3 d = ep - p;
                d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                if (TryPeek(e, out var st) && st.burnUntil > now) continue;

                IgniteRaw(e, dps, duration);
                if (fx) fx.Embers(ep + Vector3.up * 0.5f, 0.3f, 3, FireColor * 2.5f);
                done++;
            }
            if (done > 0 && fx) fx.Ring(p, radius * 0.7f, FireColor * 2f, 0.3f);
        }

        void QueueArc(Enemy origin, Vector3 p, float damage)
        {
            if (!arcLimiter.Take()) return;
            Queue(Fx.Arc, p, 0f, Mathf.Max(4f, damage), 0.05f, origin, 1f, ArcColor, ItemTag.Electrique);
        }

        void DoArc(in Pending p)
        {
            int targets = 2 + (Electricien.Tier >= 1 ? 1 : 0);
            float range = 3.5f * (Electricien.Tier >= 1 ? 1.3f : 1f);
            scratch.Clear();
            if (p.origin) scratch.Add(p.origin);
            Vector3 from = p.pos + Vector3.up;
            var fx = GameFX.Instance;
            for (int t = 0; t < targets; t++)
            {
                var e = Weapon.FindNearest(p.pos, range, scratch);
                if (!e) break;
                scratch.Add(e);
                Vector3 to = e.transform.position + Vector3.up;
                if (fx)
                {
                    fx.Lightning(from, to, ArcColor, 0.12f, 0.12f);
                    fx.Sparks(to, ArcColor, 3);
                }
                // Plasma on arcs too (Fil de cuivre / Électricien II + fire): arc hits are NoProc, so Hit skips it.
                // The burst is a NoProc Fusion explosion: it can't make arcs or plasma again.
                bool burning = IsBurning(e);
                Hit(e, p.damage, to - from, 1f, ItemTag.Electrique, HitFlags.NoProc);
                if (burning && plasmaLimiter.Take())
                {
                    var s = S;
                    Queue(Fx.Fusion, to - Vector3.up, PlasmaRadius * (s ? s.areaMult : 1f), p.damage * PlasmaDamage, 0.05f, null, 4f,
                        PlasmaColor, ItemTag.Feu | ItemTag.Electrique);
                }
            }
        }

        // ---------- Increvable: riposte ----------

        void OnHurt(float amount)
        {
            lastHurt = Time.time;
            if (Increvable.Tier < 1 || Time.time < nextRiposte) return;
            var pc = PlayerController.Instance;
            if (!pc) return;
            nextRiposte = Time.time + 1.2f;
            // Deferred: we're inside an enemy's attack, don't kill enemies from there.
            Queue(Fx.Riposte, pc.Position, 0f, 0f, 0f, null, 18f, Increvable.color, ItemTag.Survie);
        }

        void DoRiposte(Vector3 center)
        {
            var s = S;
            if (!s) return;
            center.y = 0f;
            bool big = Increvable.Tier >= 2;
            float minutes = GameManager.Instance ? GameManager.Instance.RunTime / 60f : 0f;
            float damage = (15f + 6f * minutes) * s.damageMult * (big ? 2f : 1f);
            float radius = 3f * s.areaMult * (big ? 1.2f : 1f);
            if (GameFX.Instance)
            {
                GameFX.Instance.Ring(center, radius, Increvable.color * 2.5f, 0.4f);
                GameFX.Instance.Flash(center + Vector3.up * 0.5f, radius * 0.5f, Increvable.color * 2f, 0.15f);
            }
            int hits = Area(center, radius, damage, 18f, ItemTag.Survie, HitFlags.NoProc);
            if (big && hits > 0 && PlayerHealth.Instance) PlayerHealth.Instance.Heal(Mathf.Min(RiposteHealMax, hits * RiposteHealPerHit));
        }

        // ---------- Healing (Sang froid; Sangsue is capped in Combat.OnKill) ----------

        /// <summary>Sang froid: extra regen once the player hasn't been hit for a while (recover between waves, not during).</summary>
        void CalmRegen()
        {
            var s = S;
            if (!s || s.calmRegen <= 0f || Time.time - lastHurt < CalmDelay || !PlayerHealth.Instance) return;
            PlayerHealth.Instance.Heal(s.calmRegen * Time.deltaTime);
        }

        /// <summary>True while Sang froid's calm regen is running (for a HUD hint, if wanted).</summary>
        public static bool IsCalm => Instance && Time.time - Instance.lastHurt >= CalmDelay;

        // ---------- Per frame ----------

        void Update()
        {
            float now = Time.time;

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                if (i >= tracked.Count) continue;
                var st = tracked[i];
                var e = st.enemy;
                // A pooled enemy can die and be respawned within one frame: a new SpawnId means a new life.
                // (No position-jump test: stragglers relocated in front of the player keep their statuses.)
                bool gone = !e || !e.gameObject.activeInHierarchy || e.IsDead || e.SpawnId != st.spawn;
                Vector3 ep = gone ? st.pos : e.transform.position;
                if (gone)
                {
                    Untrack(i);
                    Killed(st);
                    continue;
                }
                st.pos = ep;

                if (now >= st.nextTick)
                {
                    st.nextTick = now + DotTick;
                    float damage = 0f;
                    if (st.bleedUntil > now) damage += st.bleedDps * DotTick;
                    if (st.poisonUntil > now) damage += st.poisonDps * DotTick;
                    if (damage > 0f)
                    {
                        e.TakeDamage(damage, Vector3.zero, 0f, false, DamageNumbers.Kind.Burn);
                        if (e.IsDead)
                        {
                            Untrack(i);
                            Killed(st);
                            continue;
                        }
                    }
                }

                if (!st.Active(now)) Untrack(i);
            }

            RunPending(now);
            CalmRegen();
        }

        /// <summary>
        /// The enemy behind this status died: death effects, then the status is invalidated so the pooled
        /// object's next life starts clean (Get sees a different life). Safe to call twice.
        /// </summary>
        void Killed(Status st)
        {
            Died(st);
            st.Reset(-1, st.life);
        }

        /// <summary>Death effects of the statuses an enemy had (fire spread, toxic cloud). Clears them.</summary>
        void Died(Status st)
        {
            float now = Time.time;
            if (st.burnUntil > now && Pyromane.Tier >= 2) SpreadFire(st.pos, st.burnDps, 3, 2.8f, st.enemy);
            if (st.poisonUntil > now && Alchimiste.Tier >= 2 && cloudLimiter.Take())
            {
                var s = S;
                float area = s ? s.areaMult : 1f;
                float damage = 6f + 2f * st.poisonDps + 0.1f * st.life;
                Queue(Fx.Toxic, st.pos, 2.2f * area, damage, 0.05f, null, 3f, ToxicColor, ItemTag.Poison);
            }
            st.burnUntil = st.poisonUntil = st.bleedUntil = 0f;
        }

        void RunPending(float now)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (i >= pending.Count || pending[i].time > now) continue;
                var p = pending[i];
                pending.RemoveAt(i);
                switch (p.kind)
                {
                    case Fx.Arc:
                        DoArc(p);
                        break;
                    case Fx.Riposte:
                        DoRiposte(PlayerController.Instance ? PlayerController.Instance.Position : p.pos);
                        break;
                    default:
                        Explode(p.pos, p.radius, p.damage, p.knockback, p.color, p.source, p.kind);
                        break;
                }
            }
        }

        void Queue(Fx kind, Vector3 pos, float radius, float damage, float delay, Enemy origin, float knockback, Color color, ItemTag source)
        {
            if (pending.Count >= MaxPending) return;
            pending.Add(new Pending
            {
                kind = kind, pos = pos, radius = radius, damage = damage, time = Time.time + delay,
                origin = origin, knockback = knockback, color = color, source = source,
            });
        }

        // ---------- Status storage ----------

        Status Get(Enemy e)
        {
            if (!statuses.TryGetValue(e, out var st))
            {
                st = new Status { enemy = e };
                st.Reset(e.SpawnId, e.MaxHealth);
                statuses.Add(e, st);
            }
            else if (st.spawn != e.SpawnId)
            {
                // Pooled enemy on a new life: the old one died without us noticing.
                if (st.tracked) Died(st);
                st.Reset(e.SpawnId, e.MaxHealth);
                st.pos = e.transform.position;
            }
            return st;
        }

        bool TryPeek(Enemy e, out Status st)
        {
            if (statuses.TryGetValue(e, out st) && st.spawn == e.SpawnId) return true;
            st = null;
            return false;
        }

        void Track(Status st)
        {
            if (st.tracked || tracked.Count >= MaxTracked) return;
            st.tracked = true;
            st.pos = st.enemy.transform.position;
            st.nextTick = Time.time + DotTick;
            tracked.Add(st);
        }

        void Untrack(int index)
        {
            tracked[index].tracked = false;
            int last = tracked.Count - 1;
            tracked[index] = tracked[last];
            tracked.RemoveAt(last);
        }
    }
}
