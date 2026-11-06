using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>A camp building, upgraded with gold. Buildings never touch run stats (fair play).</summary>
    public class BuildingDef
    {
        public string id;
        public string name;
        public string description;
        public Color color;
        public int maxLevel;
        public int startLevel;
        /// <summary>Confort points per level (living conditions of the survivors). 0 for the utility buildings.</summary>
        public int comfort;
        /// <summary>What the building does at a level ("12 or / h").</summary>
        public Func<int, string> effect;
        /// <summary>Gold cost to reach a level.</summary>
        public Func<int, int> goldCost;
        /// <summary>Campaign stages that must be cleared to reach a level: progress gates the camp.</summary>
        public Func<int, int> requiredClears;
    }

    /// <summary>
    /// "Le Camp": the survivors' base. Gold buys better living conditions for them; it is the long-term
    /// gold sink (maxing everything takes many runs and the whole campaign). It also produces gold over
    /// real time, even while the app is closed (capped at <see cref="CapHours"/> hours, raised by the Entrepôt).
    /// Fair play: nothing here changes how strong a run is. Confort only nudges the Potager's gold.
    /// Later: a fully maxed camp (<see cref="IsMaxed"/>) will lead to a New Game+.
    /// </summary>
    public static class Camp
    {
        public const float BaseCapHours = 10f;
        public const float CapHoursPerLevel = 2f;
        /// <summary>Potager bonus per Confort point (+1%).</summary>
        const float GoldPerComfort = 0.01f;

        static int Round10(float v) => Mathf.RoundToInt(v / 10f) * 10;

        static float CapHoursAt(int warehouseLevel) => BaseCapHours + CapHoursPerLevel * warehouseLevel;

        /// <summary>Cost curve shared by most buildings: base x level^1.7.</summary>
        static Func<int, int> Cost(float baseCost) => l => Round10(baseCost * Mathf.Pow(l, 1.7f));

        static string ComfortText(int level, string extra) =>
            level <= 0 ? "Rien pour l'instant" : $"Confort +{level}  ·  {extra}";

        public static readonly List<BuildingDef> Buildings = new()
        {
            // ----- Utility -----
            new BuildingDef
            {
                id = "garden", name = "Potager", description = "Des légumes à troquer : produit de l'or.",
                color = new Color(0.45f, 0.8f, 0.35f), maxLevel = 10, startLevel = 1,
                effect = l => $"{GoldRate(l):0} or / h",
                goldCost = l => Round10(60f * Mathf.Pow(l, 1.6f)),
                requiredClears = l => Mathf.Max(0, l - 2),
            },
            new BuildingDef
            {
                id = "warehouse", name = "Entrepôt", description = "Plus de place : le camp stocke plus longtemps.",
                color = new Color(0.65f, 0.5f, 0.3f), maxLevel = 5,
                effect = l => $"Réserve max {CapHoursAt(l):0} h",
                goldCost = Cost(150f),
                requiredClears = l => 2 * l - 1,
            },
            new BuildingDef
            {
                id = "radio", name = "Radio", description = "Capte les appels : plus de missions.",
                color = new Color(1f, 0.8f, 0.3f), maxLevel = 3,
                effect = l => $"{MissionSlotsAt(l)} missions par jour, récompenses +{15 * l}%",
                goldCost = l => Round10(300f * Mathf.Pow(l, 1.8f)),
                requiredClears = l => 3 * l - 1,
            },

            // ----- Living conditions (Confort, 1 point per level) -----
            new BuildingDef
            {
                id = "campfire", name = "Feu de camp", description = "Là où tout le monde se retrouve le soir.",
                color = new Color(1f, 0.55f, 0.2f), maxLevel = 3, startLevel = 1, comfort = 1,
                effect = l => ComfortText(l, l >= 3 ? "on y chante même" : l == 2 ? "des bancs autour" : "quelques bûches"),
                goldCost = Cost(120f),
                requiredClears = l => Mathf.Max(0, 2 * l - 3),
            },
            new BuildingDef
            {
                id = "kitchen", name = "Cuisine", description = "Fini les conserves froides.",
                color = new Color(0.95f, 0.75f, 0.45f), maxLevel = 5, comfort = 1,
                effect = l => ComfortText(l, l >= 4 ? "de vrais repas" : "des repas chauds"),
                goldCost = Cost(200f),
                requiredClears = l => Mathf.Max(0, 2 * l - 2),
            },
            new BuildingDef
            {
                id = "dorms", name = "Dortoirs", description = "Des lits pour accueillir plus de survivants.",
                color = new Color(0.55f, 0.6f, 0.85f), maxLevel = 5, comfort = 1,
                effect = l => ComfortText(l, $"{2 * l} lits"),
                goldCost = Cost(250f),
                requiredClears = l => 2 * l - 1,
            },
            new BuildingDef
            {
                id = "showers", name = "Douches", description = "De l'eau chaude. Le luxe absolu.",
                color = new Color(0.4f, 0.8f, 0.9f), maxLevel = 3, comfort = 1,
                effect = l => ComfortText(l, l >= 3 ? "eau chaude à volonté" : "eau tiède"),
                goldCost = Cost(300f),
                requiredClears = l => 3 * l,
            },
            new BuildingDef
            {
                id = "clinic", name = "Dispensaire", description = "Un coin calme pour soigner les bobos du camp.",
                color = new Color(0.95f, 0.5f, 0.6f), maxLevel = 4, comfort = 1,
                effect = l => ComfortText(l, "le camp se porte mieux"),
                goldCost = Cost(350f),
                requiredClears = l => 3 * l - 1,
            },
            new BuildingDef
            {
                id = "walls", name = "Palissade", description = "Des murs solides : tout le monde dort mieux.",
                color = new Color(0.6f, 0.45f, 0.3f), maxLevel = 5, comfort = 1,
                effect = l => ComfortText(l, l >= 5 ? "une vraie forteresse" : "on se sent en sécurité"),
                goldCost = Cost(300f),
                requiredClears = l => 2 * l + 1,
            },
        };

        public static BuildingDef Get(string id) => Buildings.Find(b => b.id == id);

        public static int Level(BuildingDef b)
        {
            if (b == null) return 0;
            var save = MetaSave.Data.buildings.Find(s => s.id == b.id);
            return Mathf.Clamp(save != null ? save.level : b.startLevel, 0, b.maxLevel);
        }

        public static int Level(string id) => Level(Get(id));

        static void SetLevel(BuildingDef b, int level)
        {
            var list = MetaSave.Data.buildings;
            var save = list.Find(s => s.id == b.id);
            if (save == null) list.Add(save = new BuildingSave { id = b.id });
            save.level = level;
        }

        public static int TotalLevels
        {
            get
            {
                int n = 0;
                foreach (var b in Buildings) n += Level(b);
                return n;
            }
        }

        public static int MaxTotalLevels
        {
            get
            {
                int n = 0;
                foreach (var b in Buildings) n += b.maxLevel;
                return n;
            }
        }

        /// <summary>Every building at its max level (hook for the future New Game+).</summary>
        public static bool IsMaxed
        {
            get
            {
                foreach (var b in Buildings) if (Level(b) < b.maxLevel) return false;
                return true;
            }
        }

        // ---------- Survivors ----------

        /// <summary>The survivors' living conditions: Confort points of every building.</summary>
        public static int Comfort
        {
            get
            {
                int n = 0;
                foreach (var b in Buildings) n += b.comfort * Level(b);
                return n;
            }
        }

        public static int MaxComfort
        {
            get
            {
                int n = 0;
                foreach (var b in Buildings) n += b.comfort * b.maxLevel;
                return n;
            }
        }

        /// <summary>How many people live at the camp (flavour only): beds, food, and more as the campaign goes.</summary>
        public static int Survivors => 4 + 2 * Level("dorms") + Level("kitchen") + Campaign.ClearedCount / 2;

        // ---------- Production ----------

        /// <summary>+8% production per campaign stage cleared.</summary>
        public static float ProgressFactor => 1f + 0.08f * Campaign.ClearedCount;

        /// <summary>+1% Potager production per Confort point (gold only, never run power).</summary>
        public static float ComfortFactor => 1f + GoldPerComfort * Comfort;

        static float GoldRate(int level) => 8f * level * ProgressFactor * ComfortFactor;

        public static float GoldPerHour => GoldRate(Level("garden"));
        /// <summary>Hours of production the storage holds (Entrepôt).</summary>
        public static float CapHours => CapHoursAt(Level("warehouse"));
        public static float GoldCapacity => GoldPerHour * CapHours;

        /// <summary>Adds what was produced since the last tick (real UTC time, capped storage).</summary>
        public static void Tick()
        {
            var d = MetaSave.Data;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // First launch, or a last tick absurdly far in the future (broken clock): restart from now.
            if (d.campLastTick <= 0 || d.campLastTick - now > 30L * 24 * 3600)
            {
                d.campLastTick = now;
                return;
            }
            // Clock moved backwards (or no time passed): produce nothing until real time catches up,
            // so moving the clock forward and back again can't farm the camp.
            if (now <= d.campLastTick) return;

            float hours = (now - d.campLastTick) / 3600f;
            // Ad boost (x2): only the part of the elapsed time inside the boost window counts double, so offline
            // time is exact (the boost may have ended while the app was closed). Activating a boost ticks first.
            long boostEnd = ClampedBoostEnd(now);
            // long math (not Mathf/float): unix seconds don't fit a float's precision.
            float boosted = boostEnd > d.campLastTick ? (Math.Min(now, boostEnd) - d.campLastTick) / 3600f : 0f;
            float produced = GoldPerHour * (hours + (BoostMultiplier - 1f) * boosted);
            d.campGold = Mathf.Min(d.campGold + produced, Mathf.Max(d.campGold, GoldCapacity));
            d.campLastTick = now;
        }

        public static int StoredGold
        {
            get
            {
                Tick();
                return Mathf.FloorToInt(MetaSave.Data.campGold);
            }
        }

        /// <summary>0..1, how full the storage is.</summary>
        public static float Fill
        {
            get
            {
                Tick();
                return GoldCapacity > 0f ? Mathf.Clamp01(MetaSave.Data.campGold / GoldCapacity) : 0f;
            }
        }

        /// <summary>Seconds until the storage is full (0 when it already is), boost included.</summary>
        public static float SecondsToFull
        {
            get
            {
                Tick();
                float missing = GoldCapacity - MetaSave.Data.campGold;
                float perSecond = GoldPerHour / 3600f;
                if (missing <= 0f || perSecond <= 0f) return 0f;
                float boostLeft = BoostSecondsLeft;
                float boostedGold = perSecond * BoostMultiplier * boostLeft;
                if (missing <= boostedGold) return missing / (perSecond * BoostMultiplier);
                return boostLeft + (missing - boostedGold) / perSecond;
            }
        }

        // ---------- Ad boosts (rewarded ads, gold only: never run power) ----------

        public const float BoostMultiplier = 2f;
        /// <summary>Hours of x2 production per ad.</summary>
        public const float BoostHours = 2f;
        /// <summary>The boost can be stacked up to this many hours ahead.</summary>
        public const float BoostMaxHours = 4f;
        /// <summary>"Livraison express": hours of production given at once.</summary>
        public const float ExpressHours = 1f;
        public const int ExpressPerDay = 2;

        /// <summary>End of the boost, never more than BoostMaxHours ahead (a clock moved backwards can't stretch it).</summary>
        static long ClampedBoostEnd(long now)
        {
            var shop = Monetization.Data;
            long max = now + (long)(BoostMaxHours * 3600f);
            if (shop.campBoostUntil > max) shop.campBoostUntil = max;
            return shop.campBoostUntil;
        }

        public static float BoostSecondsLeft
        {
            get
            {
                long now = Monetization.Now;
                return Mathf.Max(0f, ClampedBoostEnd(now) - now);
            }
        }

        public static bool IsBoosted => BoostSecondsLeft > 0f;

        /// <summary>Production right now (x2 while boosted).</summary>
        public static float CurrentGoldPerHour => GoldPerHour * (IsBoosted ? BoostMultiplier : 1f);

        /// <summary>One more ad adds a full BoostHours only while at most BoostMaxHours - BoostHours are left.</summary>
        public static bool CanBoost => BoostSecondsLeft <= (BoostMaxHours - BoostHours) * 3600f + 1f;

        /// <summary>Starts or extends the x2 boost (call after the rewarded ad was watched).</summary>
        public static void AddBoost()
        {
            Tick(); // bank what was produced before at the old rate
            var shop = Monetization.Data;
            long now = Monetization.Now;
            long start = Math.Max(now, shop.campBoostUntil);
            shop.campBoostUntil = Math.Min(start + (long)(BoostHours * 3600f), now + (long)(BoostMaxHours * 3600f));
            MetaSave.Save();
        }

        public static int ExpressUsedToday
        {
            get
            {
                var shop = Monetization.Data;
                return shop.expressDay == Monetization.Today ? shop.expressCount : 0;
            }
        }

        public static int ExpressLeftToday => Mathf.Max(0, ExpressPerDay - ExpressUsedToday);

        /// <summary>Gold of one express delivery: an hour of (unboosted) production.</summary>
        public static int ExpressGold => Mathf.Max(1, Mathf.RoundToInt(GoldPerHour * ExpressHours));

        /// <summary>Gives an hour of production at once (call after the rewarded ad was watched). Max ExpressPerDay a day.</summary>
        public static bool Express(out int gold)
        {
            gold = 0;
            if (ExpressLeftToday <= 0) return false;
            var shop = Monetization.Data;
            string today = Monetization.Today;
            if (shop.expressDay != today)
            {
                shop.expressDay = today;
                shop.expressCount = 0;
            }
            shop.expressCount++;
            gold = ExpressGold;
            MetaSave.Save();
            Progression.AddGold(gold);
            return true;
        }

        public static bool CanHarvest => StoredGold > 0;

        /// <summary>Moves the stored gold to the player (keeps the fraction for next time).</summary>
        public static bool Harvest(out int gold)
        {
            Tick();
            var d = MetaSave.Data;
            gold = Mathf.FloorToInt(d.campGold);
            if (gold <= 0) return false;

            d.campGold -= gold;
            d.life.harvests++;
            Missions.Report(MissionType.Harvest, 1);
            MetaSave.Save();
            Progression.AddGold(gold);
            return true;
        }

        // ---------- Upgrades ----------

        /// <summary>Why the next level can't be bought right now (null = it can, if affordable).</summary>
        public static string UpgradeBlocker(BuildingDef b)
        {
            int next = Level(b) + 1;
            if (next > b.maxLevel) return "Niveau max";
            int req = b.requiredClears(next);
            if (Campaign.ClearedCount < req) return $"Termine {req} étape{(req > 1 ? "s" : "")} de campagne";
            return null;
        }

        public static bool Upgrade(BuildingDef b)
        {
            if (UpgradeBlocker(b) != null) return false;
            int next = Level(b) + 1;
            // Bank what was produced at the old rate first.
            Tick();
            if (!Progression.TrySpend(b.goldCost(next))) return false;
            SetLevel(b, next);
            MetaSave.Save();
            return true;
        }

        // ---------- Effects used by the other systems ----------

        static int MissionSlotsAt(int radioLevel) => 3 + Mathf.Min(2, radioLevel);
        public static int MissionSlots => MissionSlotsAt(Level("radio"));
        public static float MissionRewardMult => 1f + 0.15f * Level("radio");
    }
}
