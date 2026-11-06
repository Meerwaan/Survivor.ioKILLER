using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Survivor
{
    /// <summary>One threat ("Menace", Hades-heat style): makes the run harder, adds to the score multiplier.</summary>
    public sealed class ThreatDef
    {
        public string id;
        public string name;
        public string description;
        /// <summary>Added to the score multiplier (0.25 = +25 %).</summary>
        public float bonus;
        /// <summary>Mutator: trades a boost for a restriction (smaller bonus, never picked for the daily / weekly).</summary>
        public bool mutator;

        public int Percent => Mathf.RoundToInt(bonus * 100f);
    }

    /// <summary>
    /// The threats of the competitive modes (NG+ "Menaces", and the fixed ones of the daily / weekly challenges).
    /// Fair play: nothing here is permanent; a threat only makes THIS run harder (or, for the mutator, trades a boost
    /// for a weakness) and the same rules apply to everyone who turns it on.
    /// Score multiplier = 1 + sum of the bonuses (shown as "x1.45").
    /// Active threats = <see cref="RunConfig.ThreatMask"/> (bit i = <see cref="All"/>[i]); the hooks read it:
    /// RunConfig multipliers (set by GameModes.Configure), RunModifiers statics (enemy speed, elites),
    /// RunDirector timers and Loot drops (<see cref="ApplyToDirector"/>), player stats (<see cref="ApplyToPlayer"/>),
    /// rerolls / banishes (<see cref="NoSafetyNet"/>).
    /// Never reorder <see cref="All"/>: the daily / weekly picks use the index (same for everyone on the same build).
    /// </summary>
    public static class Threats
    {
        public const string Horde = "horde", Flesh = "chair", Fangs = "crocs", Sprint = "sprint", Elites = "elites",
            BossRush = "boss", Famine = "famine", NoNet = "filet", Fragile = "fragile", GlassCannon = "verre";

        static ThreatDef T(string id, string name, string desc, float bonus, bool mutator = false) =>
            new() { id = id, name = name, description = desc, bonus = bonus, mutator = mutator };

        public static readonly ThreatDef[] All =
        {
            T(Horde, "Horde dense", "+30 % d'ennemis (flot, hordes, rats)", 0.25f),
            T(Flesh, "Chair épaisse", "Ennemis +35 % de PV", 0.25f),
            T(Fangs, "Crocs", "Ennemis +30 % de dégâts", 0.20f),
            T(Sprint, "Sprinteurs", "Ennemis +15 % de vitesse", 0.20f),
            T(Elites, "Élites enragées", "Élites plus fréquentes, +30 % PV, +1 affixe", 0.15f),
            T(BossRush, "Boss pressé", "Un boss toutes les 2:30 au lieu de 3:20", 0.20f),
            T(Famine, "Famine", "Aucune viande ne tombe (coffres seulement)", 0.15f),
            T(NoNet, "Sans filet", "Ni relance, ni bannissement", 0.10f),
            T(Fragile, "Fragile", "-30 % de PV max", 0.20f),
            T(GlassCannon, "Verre canon", "+30 % de dégâts, mais -50 % de PV max", 0.10f, true),
        };

        // ---------- Tuning (what each threat does) ----------

        const float HordeSpawn = 1.3f, FleshHealth = 1.35f, FangsDamage = 1.3f, SprintSpeed = 1.15f;
        const float EliteHealth = 1.3f, EliteIntervalMult = 0.7f, EliteFirstMult = 0.8f;
        const float BossTimeMult = 0.75f; // 200 s -> 150 s (= 3 waves of 50 s: still lands right after a lull)
        const float FragileHealth = 0.7f, GlassHealth = 0.5f, GlassDamage = 1.3f;

        // ---------- Masks ----------

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
            return -1;
        }

        public static ThreatDef Get(string id)
        {
            int i = IndexOf(id);
            return i >= 0 ? All[i] : null;
        }

        public static bool InMask(int mask, string id)
        {
            int i = IndexOf(id);
            return i >= 0 && (mask & (1 << i)) != 0;
        }

        /// <summary>Is this threat on for the current run?</summary>
        public static bool Has(string id) => InMask(RunConfig.ThreatMask, id);

        public static int MaskOf(IEnumerable<string> ids)
        {
            int mask = 0;
            if (ids == null) return 0;
            foreach (var id in ids)
            {
                int i = IndexOf(id);
                if (i >= 0) mask |= 1 << i;
            }
            return mask;
        }

        public static List<ThreatDef> InMask(int mask)
        {
            var list = new List<ThreatDef>();
            for (int i = 0; i < All.Length; i++)
                if ((mask & (1 << i)) != 0) list.Add(All[i]);
            return list;
        }

        public static int Count(int mask)
        {
            int n = 0;
            for (int i = 0; i < All.Length; i++) if ((mask & (1 << i)) != 0) n++;
            return n;
        }

        /// <summary>Score multiplier of a set of threats: 1 + sum of the bonuses.</summary>
        public static float ScoreMult(int mask)
        {
            float m = 1f;
            for (int i = 0; i < All.Length; i++)
                if ((mask & (1 << i)) != 0) m += All[i].bonus;
            return m;
        }

        public static string FormatMult(float mult) => "x" + mult.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>"Horde dense, Famine" (plain text).</summary>
        public static string Names(int mask)
        {
            var sb = new StringBuilder();
            foreach (var t in InMask(mask))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(t.name);
            }
            return sb.ToString();
        }

        /// <summary>
        /// <paramref name="count"/> different non-mutator threats picked from a seed (daily / weekly: same for everyone).
        /// </summary>
        public static int PickFromSeed(int seed, int count)
        {
            var pool = new List<int>();
            for (int i = 0; i < All.Length; i++) if (!All[i].mutator) pool.Add(i);
            var rng = new System.Random(seed);
            int mask = 0;
            for (int n = 0; n < count && pool.Count > 0; n++)
            {
                int k = rng.Next(0, pool.Count);
                mask |= 1 << pool[k];
                pool.RemoveAt(k);
            }
            return mask;
        }

        // ---------- Hooks ----------

        /// <summary>RunConfig multipliers read by RunDirector (called by GameModes.Configure, after SetEndless).</summary>
        public static void ApplyToConfig(int mask)
        {
            RunConfig.ThreatMask = mask;
            RunConfig.ScoreMult = ScoreMult(mask);
            if (InMask(mask, Horde)) RunConfig.SpawnRateMult *= HordeSpawn;
            if (InMask(mask, Flesh)) RunConfig.EnemyHealthMult *= FleshHealth;
            if (InMask(mask, Fangs)) RunConfig.EnemyDamageMult *= FangsDamage;
        }

        /// <summary>Base values of the RunModifiers statics (read by Enemy). Called by RunModifiers.ResetStatics every run.</summary>
        public static float EnemySpeedMult => Has(Sprint) ? SprintSpeed : 1f;
        public static float EliteHealthMult => Has(Elites) ? EliteHealth : 1f;
        public static int ExtraEliteAffixes => Has(Elites) ? 1 : 0;

        /// <summary>"Sans filet": no reroll and no banish this run.</summary>
        public static bool NoSafetyNet => Has(NoNet);

        /// <summary>
        /// Threats that were active when the scene loaded (RunModifiers.Awake and RunDirector.Start read them then).
        /// The menu compares it with the chosen mode: if they differ, the run starts from a reloaded scene (see MenuScreen.StartConfiguredRun).
        /// </summary>
        public static int SceneMask { get; private set; }

        /// <summary>Boss / elite timers and meat drops, on this run's instances only (the tuned defaults are untouched). Called by RunDirector.Start.</summary>
        public static void ApplyToDirector(RunDirector d)
        {
            SceneMask = RunConfig.ThreatMask;
            if (RunConfig.ThreatMask == 0 || !d) return;
            if (Has(BossRush))
            {
                d.firstBossTime *= BossTimeMult;
                d.bossInterval *= BossTimeMult;
            }
            if (Has(Elites))
            {
                d.firstEliteTime *= EliteFirstMult;
                d.eliteInterval *= EliteIntervalMult;
            }
            if (Has(Famine) && Loot.Instance)
            {
                Loot.Instance.healthChance = 0f;
                Loot.Instance.eliteHealthChance = 0f;
                Loot.Instance.crateHealthChance = 0f;
            }
        }

        /// <summary>Player-side threats (max HP, mutator damage). Called once by UpgradeManager.BeginRun.</summary>
        public static void ApplyToPlayer(PlayerStats s)
        {
            if (RunConfig.ThreatMask == 0 || !s) return;
            float hp = 1f;
            if (Has(Fragile)) hp *= FragileHealth;
            if (Has(GlassCannon))
            {
                hp *= GlassHealth;
                s.damageMult *= GlassDamage;
            }
            if (hp < 1f)
            {
                s.maxHealth = Mathf.Max(20f, s.maxHealth * hp);
                if (PlayerHealth.Instance) PlayerHealth.Instance.Heal(0f); // clamps current health to the new max
            }
        }
    }
}
