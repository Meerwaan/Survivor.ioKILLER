using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// "Profil" tab: wardrobe and shop (skins, titles, premium packs) plus lifetime stats and the ad test options.
    /// Fair play: everything sold here is cosmetic (or "Sans pub"); no permanent bonus exists, every run starts equal.
    /// </summary>
    public partial class MenuScreen
    {
#if UNITY_EDITOR
        bool resetConfirm;
#endif
        static readonly string[] ProfileTabNames = { "TENUES", "TITRES", "GUICHET", "STATS" };
        static readonly Color EquippedCard = UIKit.Hex(0x1A2B23, 0.97f);
        /// <summary>Outline of what is worn (green, like the "ÉQUIPÉ" label).</summary>
        static readonly Color EquippedRim = new(0.37f, 0.82f, 0.54f, 0.75f);
        static int statRow;

        int profileTab;
        /// <summary>Gold cosmetic waiting for a second tap ("SÛR ?") before being bought.</summary>
        string profileConfirmId;

        void BuildProfile()
        {
            var title = Cosmetics.EquippedTitle;
            string who = title != null ? $"{PlayerProfile.Name}  <color=#{MetaUI.GoldHex}>« {title.name} »</color>" : PlayerProfile.Name;
            Header("LA PLAQUE", $"{who}  ·  {Campaign.TotalStars}/{Campaign.MaxStars} étoiles  ·  {Campaign.ClearedCount}/{Campaign.Count} étapes");

            ProfileTabs();
            var content = MetaUI.Scroll(page, new Vector2(0, -112), new Vector2(1000, 1360), out pageScroll);
            float y;
            switch (profileTab)
            {
                case 0: y = ProfileSkins(content); break;
                case 1: y = ProfileTitles(content); break;
                case 2: y = ProfileShop(content); break;
                default: y = ProfileStats(content); break;
            }
            MetaUI.SetHeight(content, y + 20f);
        }

        void ProfileTabs()
        {
            int newSkins = 0, newTitles = 0;
            foreach (var s in Cosmetics.Skins) if (Cosmetics.IsNew(s)) newSkins++;
            foreach (var t in Cosmetics.Titles) if (Cosmetics.IsNew(t)) newTitles++;

            for (int i = 0; i < ProfileTabNames.Length; i++)
            {
                int tab = i;
                var b = PageTab(page, ProfileTabNames[i], i == profileTab,
                    new Vector2(-363 + i * 242, 628), new Vector2(232, 84), () =>
                    {
                        if (profileTab == tab) return;
                        profileTab = tab;
                        profileConfirmId = null;
                        ShowPage(Page.Profile);
                    }, 34);
                int badge = i == 0 ? newSkins : i == 1 ? newTitles : 0;
                if (badge > 0 && i != profileTab) MetaUI.Badge(b.transform, new Vector2(10, 14), badge.ToString());
            }
        }

        // ---------- Skins ----------

        float ProfileSkins(RectTransform content)
        {
            float y = 0f;
            var equipped = Cosmetics.EquippedSkin;

            // What the others see: the equipped look, name and title.
            var hero = MetaUI.Card(content, "Equipped", UIKit.Top, new Vector2(0, -y), new Vector2(960, 300), UIKit.Card);
            UIKit.DropShadow(hero, 16f, 0.45f, -8f);
            PanelRim(hero, new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f));
            MetaUI.Strip(hero, UIKit.Accent);
            // Plate backing behind the figure: a darker inset, like the stamped part of a dog tag.
            MetaUI.Card(hero, "Plate", UIKit.Left, new Vector2(22, 0), new Vector2(266, 266), new Color(0.07f, 0.08f, 0.095f, 0.9f));
            SkinPreview.Draw(hero, equipped, UIKit.Left, new Vector2(20, 0), 270f, new Color(0.07f, 0.08f, 0.095f, 1f));
            MetaUI.Text(hero, "ÉQUIPÉ", 28, UIKit.Accent, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(310, -28), new Vector2(620, 40), FontStyle.Bold);
            FitText(MetaUI.Text(hero, equipped.name, 56, Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(310, -66), new Vector2(620, 70), FontStyle.Bold), 36);
            MetaUI.Text(hero, equipped.description, 28, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(310, -142), new Vector2(620, 80));
            var title = Cosmetics.EquippedTitle;
            MetaUI.Text(hero, title != null ? $"Au Registre : {PlayerProfile.Name}  « {title.name} »" : $"Au Registre : {PlayerProfile.Name}",
                28, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(310, -232), new Vector2(620, 44));
            y += 320f;

            MetaUI.Text(content, "Ta tenue en sortie et au Registre. Aucun effet sur le jeu : que du style.", 28, UIKit.Muted,
                TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y), new Vector2(960, 44));
            y += 60f;

            var list = Cosmetics.Skins;
            for (int i = 0; i < list.Count; i++)
            {
                int col = i % 3, row = i / 3;
                ProfileSkinCard(content, list[i], list[i] == equipped, new Vector2((col - 1) * 324f, -(y + row * 466f)));
            }
            y += Mathf.CeilToInt(list.Count / 3f) * 466f;
            MarkSeen(list);
            return y;
        }

        void ProfileSkinCard(RectTransform content, SkinDef s, bool equipped, Vector2 pos)
        {
            bool owned = Cosmetics.IsOwned(s);
            Color bg = equipped ? EquippedCard : owned ? UIKit.Card : MetaUI.Locked;
            var card = MetaUI.Card(content, "Skin " + s.name, UIKit.Top, pos, new Vector2(312, 450), bg);
            if (equipped) PanelRim(card, EquippedRim);

            SkinPreview.Draw(card, s, UIKit.Top, new Vector2(0, -14), 200f, bg);
            if (Cosmetics.IsNew(s)) NewTag(card);
            SourceTag(card, s);

            FitText(MetaUI.Text(card, s.name, 36, owned ? Color.white : UIKit.Muted, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -218), new Vector2(300, 46), FontStyle.Bold), 24);
            MetaUI.Text(card, ProfileInfo(s, owned), 23, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -264), new Vector2(296, 60));
            if (!owned && s.source == CosmeticSource.Achievement && s.progress != null)
                MetaUI.Bar(card, UIKit.Top, new Vector2(0, -328), new Vector2(260, 14), UIKit.Gold, s.progress());

            ProfileActionButton(card, s, owned, equipped, UIKit.Bottom, new Vector2(0, 14), new Vector2(284, 84), 30, () => Cosmetics.Equip(s));
        }

        // ---------- Titles ----------

        float ProfileTitles(RectTransform content)
        {
            float y = 0f;
            var equipped = Cosmetics.EquippedTitle;
            string shown = equipped != null ? $"{PlayerProfile.Name}  <color=#{MetaUI.GoldHex}>« {equipped.name} »</color>" : PlayerProfile.Name;
            MetaUI.Text(content, $"Au Registre : {shown}", 32, Color.white, TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y), new Vector2(960, 50));
            y += 70f;

            // "No title" row
            var none = MetaUI.Card(content, "No title", UIKit.Top, new Vector2(0, -y), new Vector2(960, 110), equipped == null ? EquippedCard : UIKit.Card);
            if (equipped == null) PanelRim(none, EquippedRim);
            MetaUI.Text(none, "Aucun titre", 36, Color.white, TextAnchor.MiddleLeft, UIKit.Left, new Vector2(30, 0), new Vector2(600, 60), FontStyle.Bold);
            if (equipped == null)
                WornButton(MetaUI.Button(none, "ÉQUIPÉ", UIKit.Dark, UIKit.Right, new Vector2(-18, 0), new Vector2(260, 86), () => { }, 32, false));
            else
                MetaUI.Button(none, "RETIRER", UIKit.Dark, UIKit.Right, new Vector2(-18, 0), new Vector2(260, 86), () =>
                {
                    Cosmetics.Equip((TitleDef)null);
                    Refresh();
                }, 32);
            y += 124f;

            foreach (var t in Cosmetics.Titles)
            {
                bool owned = Cosmetics.IsOwned(t), isEquipped = t == equipped;
                Color bg = isEquipped ? EquippedCard : owned ? UIKit.Card : MetaUI.Locked;
                var row = MetaUI.Card(content, "Title " + t.name, UIKit.Top, new Vector2(0, -y), new Vector2(960, 150), bg);
                if (isEquipped) PanelRim(row, EquippedRim);
                FitText(MetaUI.Text(row, $"« {t.name} »", 40, owned ? UIKit.Gold : UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(30, -16), new Vector2(620, 54), FontStyle.Bold), 28);
                MetaUI.Text(row, ProfileInfo(t, owned), 26, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(30, -74), new Vector2(620, 40));
                if (!owned && t.source == CosmeticSource.Achievement && t.progress != null)
                    MetaUI.Bar(row, UIKit.TopLeft, new Vector2(30, -118), new Vector2(560, 14), UIKit.Gold, t.progress());
                if (Cosmetics.IsNew(t)) NewTag(row);
                var tt = t;
                ProfileActionButton(row, t, owned, isEquipped, UIKit.Right, new Vector2(-18, 0), new Vector2(260, 110), 30, () => Cosmetics.Equip(tt));
                y += 164f;
            }
            MarkSeen(Cosmetics.Titles);
            return y;
        }

        // ---------- Shared bits ----------

        static string ProfileInfo(CosmeticDef c, bool owned)
        {
            if (c.source == CosmeticSource.Free) return "Offert";
            if (owned)
            {
                return c.source switch
                {
                    CosmeticSource.Achievement => "Débloqué !",
                    CosmeticSource.Premium => "Acheté",
                    _ => "Possédé",
                };
            }
            switch (c.source)
            {
                case CosmeticSource.Gold: return $"{Cosmetics.Num(c.gold)} or";
                case CosmeticSource.Achievement: return c.requirement != null ? c.requirement() : "Succès";
                case CosmeticSource.Premium:
                    var p = Store.Get(c.product);
                    return p != null ? $"{p.name}  ·  {p.price}" : "Guichet";
            }
            return "";
        }

        /// <summary>ÉQUIPÉ / ÉQUIPER / ACHETER (gold, two taps) / VERROUILLÉ / price (store).</summary>
        void ProfileActionButton(RectTransform parent, CosmeticDef c, bool owned, bool equipped, Vector2 anchor, Vector2 pos, Vector2 size, int font, System.Action equip)
        {
            if (equipped)
            {
                WornButton(MetaUI.Button(parent, "ÉQUIPÉ", UIKit.Dark, anchor, pos, size, () => { }, font, false));
                return;
            }
            if (owned)
            {
                MetaUI.Button(parent, "ÉQUIPER", MetaUI.Green, anchor, pos, size, () =>
                {
                    equip();
                    SFX.Play(SfxId.Click);
                    Refresh();
                }, font);
                return;
            }

            switch (c.source)
            {
                case CosmeticSource.Gold:
                {
                    bool confirm = profileConfirmId == c.id;
                    bool affordable = Progression.CanAfford(c.gold);
                    string label = confirm ? $"SÛR ?\n<size=24>{Cosmetics.Num(c.gold)} or</size>" : $"ACHETER\n<size=24>{Cosmetics.Num(c.gold)} or</size>";
                    Button b = null;
                    b = MetaUI.Button(parent, label, confirm ? UIKit.Blood : MetaUI.Brown, anchor, pos, size, () =>
                    {
                        if (profileConfirmId != c.id)
                        {
                            profileConfirmId = c.id;
                            Refresh();
                            return;
                        }
                        profileConfirmId = null;
                        Vector3 at = b.transform.position;
                        if (!Cosmetics.TryBuy(c)) return;
                        equip();
                        Refresh();
                        MetaFX.FloatText(fxLayer, at, $"{c.name} !", UIKit.Gold, 52);
                        MetaFX.Punch(goldChip);
                    }, font - 4, affordable);
                    break;
                }
                case CosmeticSource.Achievement:
                    MetaUI.Button(parent, "VERROUILLÉ", UIKit.Dark, anchor, pos, size, () => { }, font - 4, false);
                    break;
                case CosmeticSource.Premium:
                {
                    var p = Store.Get(c.product);
                    string price = p != null ? p.price : "?";
                    Button b = null;
                    b = MetaUI.Button(parent, price + (Store.IsTest ? "\n<size=22>(test)</size>" : ""), MonetizationUI.Premium, anchor, pos, size, () =>
                    {
                        Vector3 at = b.transform.position;
                        Store.Purchase(c.product, ok =>
                        {
                            if (!ok || !this) return;
                            if (Cosmetics.IsOwned(c)) equip();
                            Refresh();
                            if (fxLayer) MetaFX.FloatText(fxLayer, at, "Merci !", UIKit.Gold, 60);
                        });
                    }, font - 2);
                    break;
                }
            }
        }

        /// <summary>The disabled "ÉQUIPÉ" button reads as a state, not a greyed action: green label.</summary>
        static void WornButton(Button b)
        {
            var t = b ? b.GetComponentInChildren<Text>() : null;
            if (t) t.color = MetaUI.LightGreen;
        }

        /// <summary>Red rubber stamp on a dark backing, tilted ("NOUVEAU").</summary>
        static void NewTag(RectTransform card)
        {
            var red = new Color(0.93f, 0.3f, 0.24f);
            var tag = UIKit.Stamp(card, "NOUVEAU", red, 22, -5f);
            tag.name = "New";
            tag.anchorMin = tag.anchorMax = tag.pivot = UIKit.TopLeft;
            tag.anchoredPosition = new Vector2(12, -16);
            var back = UIKit.Image(tag, new Color(0.16f, 0.04f, 0.035f, 0.88f), UIKit.ChipSprite);
            back.type = Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = 0.6f; // same cut as the stamp frame
            back.raycastTarget = false;
        }

        static void SourceTag(RectTransform card, CosmeticDef c)
        {
            string text;
            Color color;
            switch (c.source)
            {
                case CosmeticSource.Gold: text = "OR"; color = UIKit.Gold; break;
                case CosmeticSource.Achievement: text = "SUCCÈS"; color = MetaUI.LightGreen; break;
                case CosmeticSource.Premium: text = "PREMIUM"; color = new Color(0.85f, 0.6f, 1f); break;
                default: return;
            }
            MetaUI.Text(card, text, 22, color, TextAnchor.UpperRight, UIKit.TopRight, new Vector2(-12, -12), new Vector2(160, 30), FontStyle.Bold);
        }

        /// <summary>Clears the "NOUVEAU" badges of a list once it was shown.</summary>
        static void MarkSeen<T>(List<T> list) where T : CosmeticDef
        {
            var seen = Monetization.Data.seen;
            bool changed = false;
            foreach (var c in list)
                if (Cosmetics.IsNew(c))
                {
                    seen.Add(c.id);
                    changed = true;
                }
            if (changed) MetaSave.Save();
        }

        // ---------- Premium shop ----------

        float ProfileShop(RectTransform content)
        {
            float y = 0f;
            string note = Store.IsTest ? "Guichet de test : aucun paiement réel.\n" : "";
            MetaUI.Text(content, note + "Rien à acheter qui rende plus fort : uniquement du style et le confort sans pub.", 28, UIKit.Muted,
                TextAnchor.MiddleCenter, UIKit.Top, new Vector2(0, -y), new Vector2(960, 90));
            y += 110f;

            foreach (var p in Store.Products)
            {
                ProfileProductCard(content, p, y);
                y += 300f;
            }

            UIKit.Button(content, "Restaurer les achats" + (Store.IsTest ? " (test)" : ""), UIKit.Dark, UIKit.Top, new Vector2(0, -y - 10f), new Vector2(640, 100), () =>
            {
                Store.Restore(n =>
                {
                    if (!this) return;
                    Refresh();
                    if (fxLayer) MetaFX.FloatText(fxLayer, root.position, n > 0 ? $"{n} achat{(n > 1 ? "s" : "")} restauré{(n > 1 ? "s" : "")}" : "Achats à jour", UIKit.Gold, 54);
                });
            }, 36);
            y += 130f;
            return y;
        }

        void ProfileProductCard(RectTransform content, ProductDef p, float y)
        {
            bool owned = Store.Owns(p.id);
            var card = MetaUI.Card(content, p.name, UIKit.Top, new Vector2(0, -y), new Vector2(960, 284), owned ? EquippedCard : UIKit.Card);
            if (owned) PanelRim(card, EquippedRim);
            else MetaUI.Strip(card, MonetizationUI.Premium);

            // Preview: the pack's skin, or a "no ads" sign.
            var skin = Cosmetics.Skins.Find(s => s.source == CosmeticSource.Premium && s.product == p.id);
            float textX = 30f;
            if (skin != null)
            {
                SkinPreview.Draw(card, skin, UIKit.Left, new Vector2(10, 0), 230f, owned ? EquippedCard : UIKit.Card);
                textX = 250f;
            }
            else if (p.id == Store.NoAdsId)
            {
                var sign = UIKit.Rect("No Ads", card);
                UIKit.Place(sign, UIKit.Left, new Vector2(40, 0), new Vector2(170, 170));
                UIKit.Image(sign, MetaUI.Red, UIKit.Circle).raycastTarget = false;
                var inner = UIKit.Rect("Inner", sign);
                UIKit.Stretch(inner, 18f);
                UIKit.Image(inner, owned ? EquippedCard : UIKit.Card, UIKit.Circle).raycastTarget = false;
                MonetizationUI.AdTag(sign, UIKit.Center, Vector2.zero, 56f);
                var bar = UIKit.Rect("Bar", sign);
                UIKit.Place(bar, UIKit.Center, Vector2.zero, new Vector2(150, 18));
                bar.localRotation = Quaternion.Euler(0f, 0f, 45f);
                UIKit.Image(bar, MetaUI.Red).raycastTarget = false;
                textX = 250f;
            }

            float width = 960f - textX - 300f;
            FitText(MetaUI.Text(card, p.name, 44, UIKit.Gold, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(textX, -22), new Vector2(width, 56), FontStyle.Bold), 30);
            FitText(MetaUI.Text(card, p.description, 26, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(textX, -86), new Vector2(width, 180)), 20);

            if (owned)
            {
                WornButton(MetaUI.Button(card, "ACHETÉ", UIKit.Dark, UIKit.Right, new Vector2(-20, 0), new Vector2(270, 130), () => { }, 38, false));
                return;
            }
            Button buy = null;
            buy = MetaUI.Button(card, p.price + (Store.IsTest ? "\n<size=24>(test)</size>" : ""), MonetizationUI.Premium, UIKit.Right,
                new Vector2(-20, 0), new Vector2(270, 130), () =>
                {
                    Vector3 at = buy.transform.position;
                    Store.Purchase(p.id, ok =>
                    {
                        if (!ok || !this) return;
                        // Packs equip their skin right away: the player sees what they got.
                        if (skin != null && Cosmetics.IsOwned(skin)) Cosmetics.Equip(skin);
                        Refresh();
                        if (fxLayer) MetaFX.FloatText(fxLayer, at, "Merci !", UIKit.Gold, 60);
                    });
                }, 44);
            UIAnim.Pulse((RectTransform)buy.transform, 2.5f, 0.025f);
        }

        // ---------- Stats + ad test options ----------

        float ProfileStats(RectTransform content)
        {
            float y = 0f;
            SectionTitle(content, "STATISTIQUES", y);
            y += 72f;
            statRow = 0;
            var life = MetaSave.Data.life;
            StatLine(content, "Sorties", life.runs.ToString(), ref y);
            StatLine(content, "Ambrés tués", life.kills.ToString(), ref y);
            StatLine(content, "Souches abattues", life.bosses.ToString(), ref y);
            StatLine(content, "Alphas tués", life.elites.ToString(), ref y);
            StatLine(content, "Armes évoluées", life.evolutions.ToString(), ref y);
            StatLine(content, "Meilleur temps (survie infinie)", UIKit.FormatTime(life.bestSeconds), ref y);
            StatLine(content, "Record (survie infinie)", GameManager.BestScore.ToString(), ref y);
            StatLine(content, "Meilleur combo", $"x{life.bestCombo}", ref y);
            StatLine(content, "Meilleur niveau", life.bestLevel.ToString(), ref y);
            StatLine(content, "Étapes gagnées", life.stageWins.ToString(), ref y);
            StatLine(content, "Récoltes au Relais", life.harvests.ToString(), ref y);
            StatLine(content, "Survivants au Relais", Camp.Survivors.ToString(), ref y);
            StatLine(content, "Confort du Relais", $"{Camp.Comfort}/{Camp.MaxComfort}", ref y);
            StatLine(content, "Or gagné au total", life.goldEarned.ToString(), ref y);

            var shop = Monetization.Data;
            int skins = 0, titles = 0;
            foreach (var s in Cosmetics.Skins) if (Cosmetics.IsOwned(s)) skins++;
            foreach (var t in Cosmetics.Titles) if (Cosmetics.IsOwned(t)) titles++;
            StatLine(content, "Tenues", $"{skins}/{Cosmetics.Skins.Count}", ref y);
            StatLine(content, "Titres", $"{titles}/{Cosmetics.Titles.Count}", ref y);
            StatLine(content, "Pubs récompensées regardées", shop.rewardedWatched.ToString(), ref y);
            StatLine(content, "Sans pub", Ads.NoAds ? "Oui, merci !" : "Non", ref y);

            if (Ads.IsTest)
            {
                y += 24f;
                SectionTitle(content, "PUBS DE TEST", y);
                y += 72f;
                ProfileToggle(content, "Pubs de test avant partie", Ads.TestInterstitials, v => Ads.TestInterstitials = v, ref y);
                ProfileToggle(content, "Simuler : aucune pub disponible", Ads.TestUnavailable, v => Ads.TestUnavailable = v, ref y);
                string pre = Ads.InterstitialBlocker();
                statRow = 0;
                StatLine(content, "Pub avant la prochaine partie", pre == null ? "oui" : $"non ({pre})", ref y);
                StatLine(content, "Seconde chance (carte d'Ostrov)", ReviveOffer.RunsUntilNextOffer == 0 ? "à la prochaine mort" : $"dans {ReviveOffer.RunsUntilNextOffer} partie(s)", ref y);
                StatLine(content, "Parties aujourd'hui", Monetization.RunsToday.ToString(), ref y);
                y += 10f;
                UIKit.Button(content, "Forcer les pubs dès la prochaine partie (test)", UIKit.Dark, UIKit.Top, new Vector2(0, -y), new Vector2(900, 100), () =>
                {
                    var d = Monetization.Data;
                    d.runDay = Monetization.Today;
                    Ads.ForceNextInterstitial = true;
                    d.runsSinceReviveOffer = 99;
                    d.expressCount = 0;
                    d.campBoostUntil = 0;
                    MetaSave.Save();
                    Refresh();
                }, 32);
                y += 120f;
            }

#if UNITY_EDITOR
            // Debug only, never compiled into a build.
            y += 30f;
            UIKit.Button(content, resetConfirm ? "SÛR ? TOUT EFFACER" : "RESET PROGRESSION (éditeur)", resetConfirm ? UIKit.Blood : UIKit.Dark,
                UIKit.Top, new Vector2(0, -y), new Vector2(700, 110), () =>
                {
                    if (!resetConfirm)
                    {
                        resetConfirm = true;
                        Refresh();
                        return;
                    }
                    resetConfirm = false;
                    Progression.ResetAll();
                    GameManager.Instance.BackToMenu();
                }, 36);
            y += 130f;
#endif
            return y;
        }

        void ProfileToggle(RectTransform content, string label, bool value, System.Action<bool> set, ref float y)
        {
            var row = MetaUI.Card(content, "Toggle", UIKit.Top, new Vector2(0, -y), new Vector2(960, 86), UIKit.Card);
            if (value) MetaUI.Strip(row, MetaUI.LightGreen, 6f);
            MetaUI.Text(row, label, 32, value ? Color.white : UIKit.Muted, TextAnchor.MiddleLeft, UIKit.Left, new Vector2(24, 0), new Vector2(660, 80));
            UIKit.Button(row, value ? "OUI" : "NON", value ? MetaUI.Green : UIKit.Dark, UIKit.Right, new Vector2(-14, 0), new Vector2(200, 72), () =>
            {
                set(!value);
                Refresh();
            }, 34);
            y += 94f;
        }

        /// <summary>Ledger line: label left, value right, every other row lightly shaded, a thin rule under each.</summary>
        static void StatLine(RectTransform content, string label, string value, ref float y)
        {
            var row = UIKit.Rect("Stat", content);
            UIKit.Place(row, UIKit.Top, new Vector2(0, -y), new Vector2(960, 62));
            UIKit.Image(row, new Color(1f, 1f, 1f, statRow++ % 2 == 0 ? 0.045f : 0.012f)).raycastTarget = false;
            LedgerRule(row, 960f, -4f);
            FitText(MetaUI.Text(row, label, 32, UIKit.Muted, TextAnchor.MiddleLeft, UIKit.Left, new Vector2(24, 0), new Vector2(560, 60)), 22);
            FitText(MetaUI.Text(row, value, 34, Color.white, TextAnchor.MiddleRight, UIKit.Right, new Vector2(-24, 0), new Vector2(380, 60), FontStyle.Bold), 22);
            y += 68f;
        }
    }
}
