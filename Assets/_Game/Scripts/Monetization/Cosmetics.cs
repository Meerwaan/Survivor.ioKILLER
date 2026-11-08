using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    public enum CosmeticSource { Free, Gold, Achievement, Premium }

    /// <summary>Look of a skin's accessory (built from primitives by PlayerSkin, drawn in 2D by SkinPreview).</summary>
    public enum SkinAccessory { None, Cap, CowboyHat, Mohawk, Medic, Crown, Viking, Hazmat, Halo, DemonHorns, Neon, Cape }

    /// <summary>Something purely visual the player can own. Never any gameplay effect (fair play).</summary>
    public abstract class CosmeticDef
    {
        public string id;
        /// <summary>Skin name, or the title text itself (shown on the leaderboard).</summary>
        public string name;
        public CosmeticSource source;
        /// <summary>Gold price (source Gold).</summary>
        public int gold;
        /// <summary>Store product that unlocks it (source Premium).</summary>
        public string product;
        /// <summary>Achievement condition (source Achievement).</summary>
        public Func<bool> condition;
        /// <summary>Achievement progress 0..1.</summary>
        public Func<float> progress;
        /// <summary>Achievement description with live progress, e.g. "Tue 10 000 zombies (1 234/10 000)".</summary>
        public Func<string> requirement;
    }

    public class SkinDef : CosmeticDef
    {
        public string description;
        public Color body = new(0.72f, 0.76f, 0.84f);
        public float smoothness = 0.4f;
        public float metallic;
        /// <summary>Body glow (black = none).</summary>
        public Color bodyEmission = Color.black;
        public SkinAccessory accessory;
        public Color accent = Color.white;
        /// <summary>Secondary accessory color (horns, backpack...).</summary>
        public Color accent2 = Color.white;
        /// <summary>Glowing disc on the ground under the player (alpha 0 = none). Very readable from the top-down camera.</summary>
        public Color aura = Color.clear;
    }

    public class TitleDef : CosmeticDef { }

    /// <summary>
    /// Skins and titles: the cosmetics sold by the game (gold, achievements, or the store).
    /// Equipping writes PlayerProfile.Skin (id) / PlayerProfile.Title (text): the leaderboard shows them.
    /// Gold prices sit next to the camp (~58 000 gold to max) as long-term goals.
    /// </summary>
    public static class Cosmetics
    {
        public const string DefaultSkinId = "";

        static LifetimeStats Life => MetaSave.Data.life;

        static Func<string> ReqText(string text, Func<int> current, int target, Func<int, string> format = null)
        {
            format ??= Num;
            return () => $"{text} ({format(Mathf.Min(current(), target))}/{format(target)})";
        }

        static T Stat<T>(T def, string text, Func<int> current, int target, Func<int, string> format = null) where T : CosmeticDef
        {
            def.source = CosmeticSource.Achievement;
            def.condition = () => current() >= target;
            def.progress = () => target > 0 ? Mathf.Clamp01(current() / (float)target) : 1f;
            def.requirement = ReqText(text, current, target, format);
            return def;
        }

        static string Clock(int seconds) => UIKit.FormatTime(seconds);

        /// <summary>"10 000" (French thousands separator, culture-independent).</summary>
        public static string Num(int v)
        {
            string s = Mathf.Abs(v).ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && (s.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return (v < 0 ? "-" : "") + sb;
        }

        // ---------- Skins ----------

        public static readonly List<SkinDef> Skins = new()
        {
            new SkinDef
            {
                id = DefaultSkinId, name = "Survivant", source = CosmeticSource.Free,
                description = "La tenue de base du Passeur. Sobre et efficace.",
            },
            new SkinDef
            {
                id = "skin_camo", name = "Camouflage", source = CosmeticSource.Gold, gold = 2000,
                description = "Treillis et casquette kaki : surplus du Cordon, acheté aux Guichets.",
                body = new Color(0.36f, 0.45f, 0.24f), accessory = SkinAccessory.Cap, accent = new Color(0.2f, 0.28f, 0.13f),
            },
            new SkinDef
            {
                id = "skin_sheriff", name = "Shérif", source = CosmeticSource.Gold, gold = 4000,
                description = "Uniforme d'un flic d'Ostrov, étoile de chez Maskarada. Nouveau shérif en ville.",
                body = new Color(0.8f, 0.62f, 0.4f), accessory = SkinAccessory.CowboyHat, accent = new Color(0.42f, 0.25f, 0.11f),
                accent2 = new Color(0.12f, 0.08f, 0.05f),
            },
            new SkinDef
            {
                id = "skin_punk", name = "Punk", source = CosmeticSource.Gold, gold = 7500,
                description = "Crête rose fluo, rescapée des squats d'Ostrov-Centre. Zéro respect.",
                body = new Color(0.17f, 0.17f, 0.21f), accessory = SkinAccessory.Mohawk, accent = new Color(1f, 0.25f, 0.65f),
            },
            new SkinDef
            {
                id = "skin_medic", name = "Médecin", source = CosmeticSource.Gold, gold = 12000,
                description = "Une blouse de l'aile Nord. Au dispensaire, on n'aime pas te la voir sur le dos.",
                body = new Color(0.93f, 0.94f, 0.97f), smoothness = 0.6f, accessory = SkinAccessory.Medic,
                accent = new Color(0.85f, 0.1f, 0.1f), accent2 = new Color(0.82f, 0.84f, 0.88f),
            },
            new SkinDef
            {
                id = "skin_king", name = "Roi du Relais", source = CosmeticSource.Gold, gold = 25000,
                description = "Costume du Théâtre d'Ostrov, offert au meilleur contributeur du Relais.",
                body = new Color(0.45f, 0.17f, 0.56f), accessory = SkinAccessory.Crown, accent = new Color(1f, 0.78f, 0.22f),
                accent2 = new Color(0.9f, 0.1f, 0.15f), aura = new Color(0.75f, 0.35f, 1f),
            },
            Stat(new SkinDef
            {
                id = "skin_viking", name = "Viking",
                description = "Rayon Halloween de Maskarada. Casque à cornes : les Souches tremblent.",
                body = new Color(0.55f, 0.37f, 0.22f), accessory = SkinAccessory.Viking, accent = new Color(0.62f, 0.64f, 0.68f),
                accent2 = new Color(0.95f, 0.92f, 0.8f), metallic = 0f,
            }, "Abats 50 Souches", () => Life.bosses, 50),
            Stat(new SkinDef
            {
                id = "skin_hazmat", name = "NRBC",
                description = "La vraie combinaison du Cordon, étanche, masque à gaz. Payée très cher en or.",
                body = new Color(0.95f, 0.8f, 0.15f), accessory = SkinAccessory.Hazmat, accent = new Color(0.06f, 0.06f, 0.08f),
                accent2 = new Color(0.6f, 0.62f, 0.66f),
            }, "Tue 200 Alphas", () => Life.elites, 200),
            Stat(new SkinDef
            {
                id = "skin_angel", name = "Ange gardien",
                description = "Maskarada, encore. Une auréole bien méritée.",
                body = new Color(0.98f, 0.97f, 0.92f), smoothness = 0.6f, accessory = SkinAccessory.Halo, accent = new Color(1f, 0.88f, 0.45f),
                aura = new Color(1f, 0.9f, 0.55f),
            }, "Toutes les étoiles d'Ostrov", () => Campaign.TotalStars, Campaign.MaxStars),
            new SkinDef
            {
                id = "skin_demon", name = "Démon", source = CosmeticSource.Premium, product = Store.DemonId,
                description = "Maskarada, rayon adulte. L'aura de braise ? Une lanterne au cou.",
                body = new Color(0.5f, 0.08f, 0.07f), bodyEmission = new Color(0.25f, 0.02f, 0f), accessory = SkinAccessory.DemonHorns,
                accent = new Color(0.12f, 0.05f, 0.05f), aura = new Color(1f, 0.3f, 0.05f),
            },
            new SkinDef
            {
                id = "skin_neon", name = "Greffé", source = CosmeticSource.Premium, product = Store.NeonId,
                description = "Harnais de Régulateur volé : diodes et aura cyan.",
                body = new Color(0.1f, 0.12f, 0.2f), smoothness = 0.8f, bodyEmission = new Color(0f, 0.18f, 0.25f),
                accessory = SkinAccessory.Neon, accent = new Color(0.2f, 1f, 1f), aura = new Color(0.1f, 0.9f, 1f),
            },
            new SkinDef
            {
                id = "skin_founder", name = "Fondateur", source = CosmeticSource.Premium, product = Store.FounderId,
                description = "La tenue de parade de l'Opéra d'Ostrov. Merci d'être là depuis le début !",
                body = new Color(1f, 0.78f, 0.3f), metallic = 0.85f, smoothness = 0.75f, accessory = SkinAccessory.Cape,
                accent = new Color(0.75f, 0.08f, 0.1f), aura = new Color(1f, 0.75f, 0.2f),
            },
        };

        // ---------- Titles (text shown on the leaderboard) ----------

        public static readonly List<TitleDef> Titles = new()
        {
            new TitleDef { id = "title_pilleur", name = "Pilleur", source = CosmeticSource.Gold, gold = 1000 },
            new TitleDef { id = "title_bricoleur", name = "Bricoleur", source = CosmeticSource.Gold, gold = 3000 },
            new TitleDef { id = "title_baron", name = "Baron du Relais", source = CosmeticSource.Gold, gold = 10000 },
            Stat(new TitleDef { id = "title_veteran", name = "Vétéran" }, "Fais 50 sorties", () => Life.runs, 50),
            Stat(new TitleDef { id = "title_chasseur", name = "Chasseur de Souches" }, "Abats 10 Souches", () => Life.bosses, 10),
            Stat(new TitleDef { id = "title_fermier", name = "Fermier" }, "Récolte au Relais 100 fois", () => Life.harvests, 100),
            Stat(new TitleDef { id = "title_boucher", name = "Le Boucher" }, "Tue 10 000 Ambrés", () => Life.kills, 10000),
            Stat(new TitleDef { id = "title_increvable", name = "Increvable" }, "Tiens 15:00 en survie infinie", () => Life.bestSeconds, 900, Clock),
            Stat(new TitleDef { id = "title_inarretable", name = "Inarrêtable" }, "Fais un combo x500", () => Life.bestCombo, 500),
            Stat(new TitleDef { id = "title_heros", name = "Héros d'Ostrov" }, "Termine toute la carte d'Ostrov", () => Campaign.ClearedCount, Campaign.Count),
            new TitleDef { id = "title_mecene", name = "Mécène", source = CosmeticSource.Premium, product = Store.NoAdsId },
            new TitleDef { id = "title_electrique", name = "Électrique", source = CosmeticSource.Premium, product = Store.NeonId },
            new TitleDef { id = "title_fondateur", name = "Fondateur", source = CosmeticSource.Premium, product = Store.FounderId },
        };

        public static event Action Changed;

        // ---------- Ownership ----------

        public static bool IsOwned(CosmeticDef c)
        {
            if (c == null) return false;
            return c.source switch
            {
                CosmeticSource.Free => true,
                CosmeticSource.Gold => Monetization.Data.cosmetics.Contains(c.id),
                CosmeticSource.Achievement => c.condition != null && c.condition(),
                CosmeticSource.Premium => Store.Owns(c.product),
                _ => false,
            };
        }

        /// <summary>Unlocked but never seen in the shop ("NOUVEAU" badge).</summary>
        public static bool IsNew(CosmeticDef c) => c.source != CosmeticSource.Free && IsOwned(c) && !Monetization.Data.seen.Contains(c.id);

        public static int NewCount
        {
            get
            {
                int n = 0;
                foreach (var s in Skins) if (IsNew(s)) n++;
                foreach (var t in Titles) if (IsNew(t)) n++;
                return n;
            }
        }

        /// <summary>Clears the "NOUVEAU" badges (call after the shop was shown).</summary>
        public static void MarkAllSeen()
        {
            var seen = Monetization.Data.seen;
            bool changed = false;
            foreach (var s in Skins)
                if (IsNew(s)) { seen.Add(s.id); changed = true; }
            foreach (var t in Titles)
                if (IsNew(t)) { seen.Add(t.id); changed = true; }
            if (changed) MetaSave.Save();
        }

        /// <summary>Buys a gold cosmetic (nothing happens if it can't be afforded).</summary>
        public static bool TryBuy(CosmeticDef c)
        {
            if (c == null || c.source != CosmeticSource.Gold || IsOwned(c)) return false;
            if (!Progression.TrySpend(c.gold)) return false;
            var d = Monetization.Data;
            d.cosmetics.Add(c.id);
            if (!d.seen.Contains(c.id)) d.seen.Add(c.id);
            MetaSave.Save();
            Changed?.Invoke();
            return true;
        }

        // ---------- Equipped ----------

        public static SkinDef GetSkin(string id) => Skins.Find(s => s.id == (id ?? ""));

        /// <summary>Equipped skin (the default one if the saved id is unknown or no longer owned).</summary>
        public static SkinDef EquippedSkin
        {
            get
            {
                var s = GetSkin(PlayerProfile.Skin);
                return s != null && IsOwned(s) ? s : Skins[0];
            }
        }

        /// <summary>Equipped title, or null.</summary>
        public static TitleDef EquippedTitle
        {
            get
            {
                string text = PlayerProfile.Title;
                if (string.IsNullOrEmpty(text)) return null;
                var t = Titles.Find(x => x.name == text);
                return t != null && IsOwned(t) ? t : null;
            }
        }

        public static void Equip(SkinDef s)
        {
            if (s == null || !IsOwned(s)) return;
            PlayerProfile.Skin = s.id;
            PlayerSkin.RefreshAll();
            Changed?.Invoke();
        }

        /// <summary>Equips a title (null = none).</summary>
        public static void Equip(TitleDef t)
        {
            if (t != null && !IsOwned(t)) return;
            PlayerProfile.Title = t != null ? t.name : "";
            Changed?.Invoke();
        }

        /// <summary>Body color of a skin id (for leaderboard swatches). Unknown ids give the default look.</summary>
        public static Color SkinColor(string id) => (GetSkin(id) ?? Skins[0]).body;

        /// <summary>Drops an equipped cosmetic that isn't owned anymore (e.g. after a progression reset).</summary>
        public static void ValidateEquipped()
        {
            var s = GetSkin(PlayerProfile.Skin);
            if (!string.IsNullOrEmpty(PlayerProfile.Skin) && (s == null || !IsOwned(s))) PlayerProfile.Skin = DefaultSkinId;
            if (!string.IsNullOrEmpty(PlayerProfile.Title) && EquippedTitle == null) PlayerProfile.Title = "";
        }
    }
}
