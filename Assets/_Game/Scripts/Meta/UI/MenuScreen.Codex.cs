using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>
    /// "Codex" tab: every weapon, bonus and synergy of the game, the starting weapon picker,
    /// and build presets for theorycrafting. Everyone plays with the same tools: luck makes the set in a run,
    /// a preset only puts a reminder badge on its cards (it never changes the draws).
    /// Texts come from the same sources as the level-up cards (Weapon, UpgradeManager passives, Synergies).
    /// </summary>
    public partial class MenuScreen
    {
        static readonly string[] CodexTabs = { "ARMES", "BONUS", "SYNERGIES", "PRESETS" };
        static readonly Color CodexSelected = new(0.2f, 0.17f, 0.07f, 0.97f);
        static readonly Color CodexEvoBlock = new(0.85f, 0.45f, 1f, 0.12f);
        static readonly Color CodexLine = new(0.86f, 0.86f, 0.86f);
        static readonly Color NoFrame = new(0f, 0f, 0f, 0f);
        const string MutedHex = "BBBBBB";
        const string DimHex = "8A8A8A";
        const float CodexWidth = 960f;
        const float TileHeight = 120f, TileStep = 134f;

        int codexTab;
        /// <summary>Preset shown in the editor (-1 = the active one, else the first).</summary>
        int codexEdit = -1;
        int codexDeleteConfirm = -1;
        /// <summary>Set by in-page refreshes so cards only slide in when the tab is opened.</summary>
        bool codexQuiet;

        void BuildCodex()
        {
            bool animate = !codexQuiet;
            codexQuiet = false;

            MetaUI.Text(page, "LE CARNET", 70, UIKit.Gold, TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 785), new Vector2(640, 90), FontStyle.Bold);
            MetaUI.Text(page, "Tout le monde à armes égales : c'est le hasard qui fait ton stuff.", 28, UIKit.Muted,
                TextAnchor.MiddleCenter, UIKit.Center, new Vector2(0, 712), new Vector2(1040, 50));
            for (int i = 0; i < CodexTabs.Length; i++) CodexTabButton(i);

            var content = MetaUI.Scroll(page, new Vector2(0, -118), new Vector2(1000, 1340), out pageScroll);
            float y = 10f;
            var um = UpgradeManager.Instance;
            if (!um)
            {
                MetaUI.Text(content, "Carnet indisponible (pas de joueur dans la scène).", 34, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -40), new Vector2(900, 120));
                MetaUI.SetHeight(content, 200f);
                return;
            }

            switch (codexTab)
            {
                case 0: y = CodexWeapons(content, um, y, animate); break;
                case 1: y = CodexPassives(content, um, y, animate); break;
                case 2: y = CodexSynergies(content, um, y, animate); break;
                default: y = CodexPresets(content, um, y, animate); break;
            }
            MetaUI.SetHeight(content, y + 30f);
        }

        void CodexTabButton(int tab)
        {
            bool on = codexTab == tab;
            UIKit.Button(page, CodexTabs[tab], on ? UIKit.Blood : UIKit.Dark, UIKit.Center, new Vector2(-375 + tab * 250, 620), new Vector2(240, 100), () =>
            {
                if (codexTab == tab) return;
                codexTab = tab;
                codexDeleteConfirm = -1;
                ShowPage(current); // fresh page: scroll back to the top
            }, 36);
        }

        /// <summary>Rebuilds the page after a tap, keeping the scroll and without the slide-in.</summary>
        void CodexRefresh()
        {
            codexQuiet = true;
            Refresh();
        }

        // ---------- Weapons ----------

        float CodexWeapons(RectTransform content, UpgradeManager um, float y, bool animate)
        {
            y = CodexIntro(content, CodexStuffRules(um), y);
            y = CodexIntro(content, "Touche une arme pour en faire ton <color=#" + MetaUI.GoldHex + ">arme de départ</color>. Toutes sont disponibles, pour tout le monde.", y);
            var selected = CodexStartingWeapon(um);
            var weapons = um.Weapons;
            for (int i = 0; i < weapons.Count; i++)
                y = CodexWeaponCard(content, um, weapons[i], weapons[i] == selected, y, i, animate) + 16f;
            return y;
        }

        static Weapon CodexStartingWeapon(UpgradeManager um)
        {
            string id = Progression.StartingWeaponId;
            foreach (var w in um.Weapons)
                if (w.Id == id) return w;
            if (um.startingWeapon) return um.startingWeapon;
            return um.Weapons.Count > 0 ? um.Weapons[0] : null;
        }

        float CodexWeaponCard(RectTransform content, UpgradeManager um, Weapon w, bool selected, float y, int index, bool animate)
        {
            var weapon = w;
            RectTransform card = null;
            card = CodexCard(content, "Weapon " + w.displayName, y, selected ? CodexSelected : UIKit.Card, selected ? UIKit.Gold : NoFrame, () =>
            {
                codexDeleteConfirm = -1;
                if (Progression.StartingWeaponId == weapon.Id && CodexStartingWeapon(um) == weapon) return;
                Vector3 at = card.position;
                Progression.StartingWeaponId = weapon.Id;
                CodexRefresh();
                MetaFX.FloatText(fxLayer, at, $"Arme de départ : {weapon.displayName}", UIKit.Gold, 48);
            });
            CodexStripe(card, w.color);

            var icon = UIKit.Icon(card, w.displayName, w.color, false, 110, "");
            UIKit.Place(icon, UIKit.TopLeft, new Vector2(30, -24), new Vector2(110, 110));

            const float x = 164f, width = CodexWidth - x - 26f;
            float cy = 20f;
            MetaUI.Text(card, selected ? "ARME DE DÉPART" : "CHOISIR", 26, selected ? UIKit.Gold : UIKit.Muted, TextAnchor.UpperRight,
                UIKit.TopRight, new Vector2(-26, -32), new Vector2(270, 40), FontStyle.Bold);
            CodexFlow(card, w.displayName, 44, selected ? UIKit.Gold : Color.white, x, ref cy, width - 280f, FontStyle.Bold, 2f);
            CodexFlow(card, CodexTags(w.Tags), 28, Color.white, x, ref cy, width, FontStyle.Normal, 6f);
            CodexFlow(card, w.Summary, 30, UIKit.Muted, x, ref cy, width, FontStyle.Normal, 12f);

            var sb = new StringBuilder();
            for (int lv = 1; lv <= w.MaxLevel; lv++)
            {
                if (lv > 1) sb.Append('\n');
                sb.Append("<color=#").Append(MetaUI.GoldHex).Append(">Nv ").Append(lv).Append("</color>  ").Append(w.DescribeLevel(lv));
            }
            CodexFlow(card, sb.ToString(), 27, CodexLine, x, ref cy, width, FontStyle.Normal, 14f);

            cy = CodexEvolution(card, um, w, x, cy, width);
            CodexFinish(card, Mathf.Max(cy, 24f + 110f) + 22f);
            CodexAnim(card, index, animate);
            return y + card.sizeDelta.y;
        }

        /// <summary>Purple block: evolution name, recipe and effect.</summary>
        static float CodexEvolution(RectTransform card, UpgradeManager um, Weapon w, float x, float cy, float width)
        {
            var block = UIKit.Rect("Evolution", card); // created first: drawn behind the texts
            float top = cy;
            cy += 12f;
            var partner = um.FindPassive(w.EvolutionPassive);
            string recipe = partner != null
                ? $"Niveau max + <color=#{ColorUtility.ToHtmlStringRGB(partner.color)}>{partner.name}</color>"
                : "Niveau max";
            CodexFlow(card, "ÉVOLUTION : " + w.EvolutionName, 32, UpgradeManager.EvolutionColor, x + 18f, ref cy, width - 36f, FontStyle.Bold, 2f);
            CodexFlow(card, recipe, 27, UIKit.Muted, x + 18f, ref cy, width - 36f, FontStyle.Normal, 4f);
            CodexFlow(card, w.EvolutionText, 27, Color.white, x + 18f, ref cy, width - 36f, FontStyle.Normal, 0f);
            cy += 12f;
            UIKit.Place(block, UIKit.TopLeft, new Vector2(x, -top), new Vector2(width, cy - top));
            UIKit.Image(block, CodexEvoBlock).raycastTarget = false;
            return cy;
        }

        // ---------- Passives ----------

        float CodexPassives(RectTransform content, UpgradeManager um, float y, bool animate)
        {
            y = CodexIntro(content, $"{um.Passives.Count} bonus à gagner en montant de niveau pendant la partie. Chacun compte pour ses familles (synergies), certains font évoluer une arme.", y);
            var passives = um.Passives;
            for (int i = 0; i < passives.Count; i++)
                y = CodexPassiveCard(content, um, passives[i], y, i, animate) + 16f;
            return y;
        }

        float CodexPassiveCard(RectTransform content, UpgradeManager um, Passive p, float y, int index, bool animate)
        {
            var card = CodexCard(content, "Passive " + p.id, y, UIKit.Card, NoFrame, null);
            CodexStripe(card, p.color);
            var icon = UIKit.Icon(card, p.name, p.color, true, 100, "");
            UIKit.Place(icon, UIKit.TopLeft, new Vector2(34, -22), new Vector2(100, 100));

            const float x = 164f, width = CodexWidth - x - 26f;
            float cy = 18f;
            MetaUI.Text(card, $"Nv max {p.maxLevel}", 27, UIKit.Muted, TextAnchor.UpperRight, UIKit.TopRight, new Vector2(-26, -28), new Vector2(200, 40), FontStyle.Bold);
            CodexFlow(card, p.name, 40, Color.white, x, ref cy, width - 210f, FontStyle.Bold, 2f);
            CodexFlow(card, CodexTags(p.tags), 27, Color.white, x, ref cy, width, FontStyle.Normal, 6f);
            CodexFlow(card, p.description, 30, CodexLine, x, ref cy, width, FontStyle.Normal, 6f);

            foreach (var w in um.Weapons)
            {
                if (w.EvolutionPassive != p.id) continue;
                string evoHex = ColorUtility.ToHtmlStringRGB(UpgradeManager.EvolutionColor);
                CodexFlow(card, $"Fait évoluer : <color=#{ColorUtility.ToHtmlStringRGB(w.color)}>{w.displayName}</color>  →  <color=#{evoHex}><b>{w.EvolutionName}</b></color>",
                    27, UIKit.Muted, x, ref cy, width, FontStyle.Normal, 4f);
            }

            CodexFinish(card, Mathf.Max(cy, 22f + 100f) + 18f);
            CodexAnim(card, index, animate);
            return y + card.sizeDelta.y;
        }

        // ---------- Synergies ----------

        float CodexSynergies(RectTransform content, UpgradeManager um, float y, bool animate)
        {
            y = CodexIntro(content, $"Chaque arme et chaque bonus compte pour ses familles. {Synergies.Tier1Count} objets d'une famille : palier I. " +
                                    $"{Synergies.Tier2Count} objets : palier II (les deux effets).", y);
            int index = 0;
            foreach (var syn in Synergies.All)
            {
                y = CodexSynergyCard(content, um, syn, y, index, animate) + 16f;
                index++;
            }

            y += 10f;
            SectionTitle(content, $"RÉACTIONS  <size=28><color=#{MutedHex}>toujours actives, dès que les effets se croisent</color></size>", y);
            y += 74f;
            foreach (var r in Synergies.Reactions)
            {
                var card = CodexCard(content, "Reaction " + r[0], y, UIKit.Card, NoFrame, null);
                CodexStripe(card, UIKit.Gold);
                float cy = 16f;
                CodexFlow(card, r[0], 34, UIKit.Gold, 34f, ref cy, CodexWidth - 60f, FontStyle.Bold, 2f);
                CodexFlow(card, r.Length > 1 ? r[1] : "", 28, CodexLine, 34f, ref cy, CodexWidth - 60f, FontStyle.Normal, 0f);
                CodexFinish(card, cy + 18f);
                CodexAnim(card, index++, animate);
                y += card.sizeDelta.y + 12f;
            }
            return y;
        }

        float CodexSynergyCard(RectTransform content, UpgradeManager um, Synergy syn, float y, int index, bool animate)
        {
            var card = CodexCard(content, "Synergy " + syn.name, y, UIKit.Card, NoFrame, null);
            CodexStripe(card, syn.color);
            var icon = UIKit.Icon(card, syn.name, syn.color, false, 100, "");
            UIKit.Place(icon, UIKit.TopLeft, new Vector2(34, -22), new Vector2(100, 100));

            const float x = 164f, width = CodexWidth - x - 26f;
            float cy = 18f;
            CodexFlow(card, syn.name, 42, syn.color, x, ref cy, width, FontStyle.Bold, 0f);
            CodexFlow(card, "Famille " + Synergies.TagName(syn.tag), 27, UIKit.Muted, x, ref cy, width, FontStyle.Normal, 10f);
            CodexFlow(card, $"<b>I</b>  <color=#{MutedHex}>({Synergies.Tier1Count} objets)</color>  {syn.tier1}", 30, Color.white, x, ref cy, width, FontStyle.Normal, 4f);
            CodexFlow(card, $"<b>II</b>  <color=#{MutedHex}>({Synergies.Tier2Count} objets)</color>  {syn.tier2}", 30, Color.white, x, ref cy, width, FontStyle.Normal, 10f);

            var weapons = new List<string>();
            foreach (var w in um.Weapons)
                if ((w.Tags & syn.tag) != 0) weapons.Add(w.displayName);
            var passives = new List<string>();
            foreach (var p in um.Passives)
                if ((p.tags & syn.tag) != 0) passives.Add(p.name);
            if (weapons.Count > 0)
                CodexFlow(card, $"<color=#{MutedHex}>Armes :</color> {string.Join(", ", weapons)}", 26, CodexLine, x, ref cy, width, FontStyle.Normal, 2f);
            if (passives.Count > 0)
                CodexFlow(card, $"<color=#{MutedHex}>Bonus :</color> {string.Join(", ", passives)}", 26, CodexLine, x, ref cy, width, FontStyle.Normal, 2f);

            CodexFinish(card, Mathf.Max(cy, 22f + 100f) + 16f);
            CodexAnim(card, index, animate);
            return y + card.sizeDelta.y;
        }

        // ---------- Presets ----------

        float CodexPresets(RectTransform content, UpgradeManager um, float y, bool animate)
        {
            Presets.RemoveUnknown(id => CodexFindWeapon(um, id) != null, id => um.FindPassive(id) != null);

            y = CodexIntro(content, "Prépare tes stuffs à l'avance. Un preset ne choisit rien à ta place et ne change pas les tirages : " +
                                    "en partie, les cartes de ton preset <b>actif</b> portent juste ce badge. " +
                                    $"Rappel : {um.buildCap} niveaux de stuff (3 armes au Nv 5 + leurs 3 partenaires = 18).", y);
            PresetBadge.Create(content, UIKit.TopLeft, new Vector2(20, -y), 44f);
            y += 64f;

            var all = Presets.All;
            int edit = codexEdit >= 0 && codexEdit < all.Count ? codexEdit : Presets.ActiveIndex >= 0 ? Presets.ActiveIndex : all.Count > 0 ? 0 : -1;
            Text editName = null;

            SectionTitle(content, $"MES PRESETS  <size=30><color=#{MutedHex}>{all.Count}/{Presets.MaxPresets}</color></size>", y);
            y += 74f;
            if (all.Count == 0)
            {
                MetaUI.Text(content, "Aucun preset pour l'instant.", 32, UIKit.Muted, TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -y), new Vector2(960, 50));
                y += 64f;
            }
            for (int i = 0; i < all.Count; i++)
            {
                var row = CodexPresetRow(content, um, i, i == edit, y, out var nameText);
                if (i == edit) editName = nameText;
                CodexAnim(row, i, animate);
                y += 130f + 14f;
            }

            if (all.Count < Presets.MaxPresets)
            {
                UIKit.Button(content, "+ NOUVEAU PRESET", MetaUI.Green, UIKit.Top, new Vector2(0, -y), new Vector2(CodexWidth, 112), () =>
                {
                    codexDeleteConfirm = -1;
                    var created = Presets.Create();
                    if (created != null) codexEdit = Presets.IndexOf(created);
                    CodexRefresh();
                }, 42);
                y += 112f + 20f;
            }
            else
            {
                MetaUI.Text(content, $"{Presets.MaxPresets} presets maximum : supprimes-en un pour en créer un autre.", 28, UIKit.Muted,
                    TextAnchor.UpperCenter, UIKit.Top, new Vector2(0, -y), new Vector2(960, 44));
                y += 60f;
            }

            if (edit < 0) return y;
            return CodexPresetEditor(content, um, all[edit], editName, y + 10f);
        }

        RectTransform CodexPresetRow(RectTransform content, UpgradeManager um, int i, bool editing, float y, out Text nameText)
        {
            var p = Presets.All[i];
            bool active = Presets.ActiveIndex == i;
            var row = CodexCard(content, "Preset " + i, y, editing ? CodexSelected : UIKit.Card, editing ? UIKit.Gold : NoFrame, () =>
            {
                codexDeleteConfirm = -1;
                if (codexEdit == i) return;
                codexEdit = i;
                CodexRefresh();
            });
            CodexFinish(row, 130f);
            if (active) CodexStripe(row, PresetBadge.Tint);

            PresetBadge.HeartIcon(row, UIKit.Left, new Vector2(26, 0), 58f, active ? PresetBadge.Tint : new Color(1f, 1f, 1f, 0.12f));
            nameText = MetaUI.Text(row, p.name, 38, editing ? UIKit.Gold : Color.white, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(104, -16), new Vector2(450, 50), FontStyle.Bold);
            nameText.supportRichText = false;

            string info = $"{p.weapons.Count}/{Presets.MaxWeapons} armes · {p.passives.Count}/{Presets.MaxPassives} bonus";
            string tiers = CodexTierSummary(p, um, 2);
            if (tiers.Length > 0) info += "\n" + tiers;
            var infoText = MetaUI.Text(row, info, 24, UIKit.Muted, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(104, -66), new Vector2(450, 60));
            infoText.lineSpacing = 0.95f;

            UIKit.Button(row, active ? "ACTIF" : "ACTIVER", active ? PresetBadge.Tint : UIKit.Dark, UIKit.Right, new Vector2(-190, 0), new Vector2(200, 100), () =>
            {
                codexDeleteConfirm = -1;
                Presets.SetActive(active ? -1 : i);
                codexEdit = i;
                CodexRefresh();
            }, 34);

            bool confirm = codexDeleteConfirm == i;
            UIKit.Button(row, confirm ? "SÛR ?" : "SUPPR.", confirm ? UIKit.Blood : new Color(0.3f, 0.3f, 0.33f), UIKit.Right, new Vector2(-16, 0), new Vector2(160, 100), () =>
            {
                if (codexDeleteConfirm != i)
                {
                    codexDeleteConfirm = i;
                    CodexRefresh();
                    return;
                }
                codexDeleteConfirm = -1;
                Presets.Delete(i);
                if (codexEdit == i) codexEdit = -1;
                else if (codexEdit > i) codexEdit--;
                CodexRefresh();
            }, 32);
            return row;
        }

        float CodexPresetEditor(RectTransform content, UpgradeManager um, Preset p, Text rowName, float y)
        {
            SectionTitle(content, "MODIFIER LE PRESET", y);
            y += 80f;

            // Name: the row above is updated in place, no rebuild (the field may be ending its edit because the page is going away).
            InputField field = null;
            field = UIKit.Input(content, Vector2.zero, new Vector2(600, 100), p.name, Presets.NameLength, value =>
            {
                Presets.Rename(p, value);
                if (rowName) rowName.text = p.name;
                if (field) field.text = p.name;
            });
            UIKit.Place((RectTransform)field.transform, UIKit.TopLeft, new Vector2(20, -y), new Vector2(600, 100));
            MetaUI.Text(content, "Touche le nom pour le changer", 26, UIKit.Muted, TextAnchor.MiddleLeft, UIKit.TopLeft, new Vector2(645, -y), new Vector2(335, 100));
            y += 120f;

            // What this build would reach: that's the theorycraft part.
            var block = MetaUI.Card(content, "Build Summary", UIKit.Top, new Vector2(0, -y), new Vector2(CodexWidth, 100), UIKit.Card);
            float cy = 16f;
            CodexFlow(block, "SYNERGIES DU STUFF", 30, UIKit.Gold, 28f, ref cy, CodexWidth - 56f, FontStyle.Bold, 6f);
            CodexFlow(block, CodexSynergyDetails(p, um), 27, CodexLine, 28f, ref cy, CodexWidth - 56f, FontStyle.Normal, 14f);
            CodexFlow(block, "ÉVOLUTIONS", 30, UpgradeManager.EvolutionColor, 28f, ref cy, CodexWidth - 56f, FontStyle.Bold, 6f);
            CodexFlow(block, CodexEvolutionDetails(p, um), 27, CodexLine, 28f, ref cy, CodexWidth - 56f, FontStyle.Normal, 0f);
            CodexFinish(block, cy + 18f);
            y += block.sizeDelta.y + 24f;

            // Weapon picker
            SectionTitle(content, $"ARMES  <size=30><color=#{MutedHex}>{p.weapons.Count}/{Presets.MaxWeapons}</color></size>", y);
            y += 74f;
            var weapons = um.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                CodexPresetTile(content, p, w.Id, true, w.displayName, w.Tags, w.color, i, y);
            }
            y += Mathf.CeilToInt(weapons.Count / 3f) * TileStep + 16f;

            // Passive picker
            SectionTitle(content, $"BONUS  <size=30><color=#{MutedHex}>{p.passives.Count}/{Presets.MaxPassives}</color></size>", y);
            y += 74f;
            var passives = um.Passives;
            for (int i = 0; i < passives.Count; i++)
            {
                var ps = passives[i];
                CodexPresetTile(content, p, ps.id, false, ps.name, ps.tags, ps.color, i, y);
            }
            y += Mathf.CeilToInt(passives.Count / 3f) * TileStep;
            return y;
        }

        void CodexPresetTile(RectTransform content, Preset p, string id, bool weapon, string label, ItemTag tags, Color color, int index, float y0)
        {
            int col = index % 3, row = index / 3;
            bool on = weapon ? p.weapons.Contains(id) : p.passives.Contains(id);

            var tile = UIKit.Rect("Tile " + label, content);
            UIKit.Place(tile, UIKit.Top, new Vector2(-325 + col * 325, -(y0 + row * TileStep)), new Vector2(310, TileHeight));
            if (on)
            {
                var frame = UIKit.Rect("Frame", tile);
                UIKit.Stretch(frame, -5f);
                UIKit.Image(frame, PresetBadge.Tint).raycastTarget = false;
            }
            var bgRect = UIKit.Rect("Bg", tile);
            UIKit.Stretch(bgRect);
            var bg = UIKit.Image(bgRect, on ? Color.Lerp(UIKit.Card, color, 0.22f) : UIKit.Card);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                codexDeleteConfirm = -1;
                Vector3 at = tile.position;
                SFX.Play(SfxId.Click);
                if (!Presets.Toggle(p, id, weapon))
                {
                    MetaFX.FloatText(fxLayer, at, weapon ? $"{Presets.MaxWeapons} armes max !" : $"{Presets.MaxPassives} bonus max !", MetaUI.Red, 46);
                    return;
                }
                CodexRefresh();
            });

            var icon = UIKit.Icon(tile, label, on ? color : color * 0.6f, !weapon, 72, "");
            UIKit.Place(icon, UIKit.Left, new Vector2(12, 0), new Vector2(72, 72));

            var name = UIKit.Label(tile, label, 25, on ? Color.white : CodexLine, TextAnchor.UpperLeft, FontStyle.Bold);
            name.lineSpacing = 0.9f;
            var nr = name.rectTransform;
            nr.anchorMin = Vector2.zero;
            nr.anchorMax = Vector2.one;
            nr.offsetMin = new Vector2(94, 34);
            nr.offsetMax = new Vector2(-10, -10);

            var tagText = UIKit.Label(tile, tags == ItemTag.None ? $"<color=#{DimHex}>Sans famille</color>" : Synergies.TagNames(tags), 20, Color.white, TextAnchor.LowerLeft);
            var tr = tagText.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.offsetMin = new Vector2(94, 8);
            tr.offsetMax = new Vector2(-6, 34);

            if (on) PresetBadge.HeartIcon(tile, UIKit.TopRight, new Vector2(-8, -8), 30f, PresetBadge.Tint);
        }

        // ---------- Preset maths ----------

        static Weapon CodexFindWeapon(UpgradeManager um, string id)
        {
            foreach (var w in um.Weapons)
                if (w.Id == id) return w;
            return null;
        }

        /// <summary>Items of the preset counting for this set bonus (same rule as Synergies.Refresh, with the preset instead of the run).</summary>
        static int CodexCount(Preset p, UpgradeManager um, Synergy syn)
        {
            int n = 0;
            foreach (var w in um.Weapons)
                if (p.weapons.Contains(w.Id) && (w.Tags & syn.tag) != 0) n++;
            foreach (var ps in um.Passives)
                if (p.passives.Contains(ps.id) && (ps.tags & syn.tag) != 0) n++;
            return n;
        }

        static int CodexTier(int count) => count >= Synergies.Tier2Count ? 2 : count >= Synergies.Tier1Count ? 1 : 0;

        /// <summary>"Pyromane II · Artificier I" (best tiers first, at most <paramref name="max"/>).</summary>
        static string CodexTierSummary(Preset p, UpgradeManager um, int max)
        {
            var reached = new List<(Synergy syn, int tier)>();
            foreach (var syn in Synergies.All)
            {
                int tier = CodexTier(CodexCount(p, um, syn));
                if (tier > 0) reached.Add((syn, tier));
            }
            reached.Sort((a, b) => b.tier.CompareTo(a.tier));
            var parts = new List<string>();
            for (int i = 0; i < reached.Count && i < max; i++)
                parts.Add($"<color=#{reached[i].syn.Hex}>{reached[i].syn.name} {(reached[i].tier >= 2 ? "II" : "I")}</color>");
            string text = string.Join(" · ", parts);
            if (reached.Count > max) text += $" +{reached.Count - max}";
            return text;
        }

        static string CodexSynergyDetails(Preset p, UpgradeManager um)
        {
            var sb = new StringBuilder();
            foreach (var syn in Synergies.All)
            {
                int n = CodexCount(p, um, syn);
                if (n == 0) continue;
                int tier = CodexTier(n);
                if (sb.Length > 0) sb.Append('\n');
                if (tier == 0)
                {
                    sb.Append($"<color=#{syn.Hex}>{syn.name}</color> <color=#{MutedHex}>{n}/{Synergies.Tier1Count}</color>  <color=#{DimHex}>{syn.tier1}</color>");
                    continue;
                }
                string next = tier == 1 ? $"  <color=#{DimHex}>({n}/{Synergies.Tier2Count} : {syn.tier2})</color>" : "";
                string effect = tier >= 2 ? $"{syn.tier1}. {syn.tier2}." : syn.tier1 + ".";
                sb.Append($"<color=#{syn.Hex}><b>{syn.name} {(tier >= 2 ? "II" : "I")}</b></color>  {effect}{next}");
            }
            return sb.Length > 0 ? sb.ToString() : $"<color=#{DimHex}>Aucune : prends au moins {Synergies.Tier1Count} objets de la même famille.</color>";
        }

        static string CodexEvolutionDetails(Preset p, UpgradeManager um)
        {
            var sb = new StringBuilder();
            string evoHex = ColorUtility.ToHtmlStringRGB(UpgradeManager.EvolutionColor);
            foreach (var w in um.Weapons)
            {
                if (!p.weapons.Contains(w.Id)) continue;
                var partner = um.FindPassive(w.EvolutionPassive);
                if (sb.Length > 0) sb.Append('\n');
                if (partner == null)
                    sb.Append($"<color=#{DimHex}>{w.displayName} : pas d'évolution</color>");
                else if (p.passives.Contains(partner.id))
                    sb.Append($"<color=#{evoHex}><b>{w.EvolutionName}</b></color>  = {w.displayName} max + {partner.name}");
                else
                    sb.Append($"<color=#{DimHex}>{w.displayName} : ajoute {partner.name} pour {w.EvolutionName}</color>");
            }
            return sb.Length > 0 ? sb.ToString() : $"<color=#{DimHex}>Ajoute des armes pour voir leurs évolutions.</color>";
        }

        // ---------- Layout helpers ----------

        /// <summary>Full-width card at <paramref name="y"/> (height set later with CodexFinish). Tappable when <paramref name="onClick"/> is set.</summary>
        static RectTransform CodexCard(RectTransform content, string name, float y, Color background, Color frame, UnityAction onClick)
        {
            var card = UIKit.Rect(name, content);
            UIKit.Place(card, UIKit.Top, new Vector2(0, -y), new Vector2(CodexWidth, 120));
            if (frame.a > 0f)
            {
                var f = UIKit.Rect("Frame", card);
                UIKit.Stretch(f, -6f);
                UIKit.Image(f, frame).raycastTarget = false;
            }
            var bgRect = UIKit.Rect("Bg", card);
            UIKit.Stretch(bgRect);
            var bg = UIKit.Image(bgRect, background);
            if (onClick != null)
            {
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var colors = button.colors;
                colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
                button.colors = colors;
                button.onClick.AddListener(() => SFX.Play(SfxId.Click));
                button.onClick.AddListener(onClick);
            }
            return card;
        }

        static void CodexFinish(RectTransform card, float height) => card.sizeDelta = new Vector2(card.sizeDelta.x, height);

        /// <summary>Thin colored bar on the left edge, full height.</summary>
        static void CodexStripe(RectTransform card, Color color)
        {
            var s = UIKit.Rect("Stripe", card);
            s.anchorMin = Vector2.zero;
            s.anchorMax = new Vector2(0f, 1f);
            s.pivot = new Vector2(0f, 0.5f);
            s.offsetMin = Vector2.zero;
            s.offsetMax = new Vector2(10f, 0f);
            UIKit.Image(s, color).raycastTarget = false;
        }

        /// <summary>Wrapped text block at the cursor; moves the cursor below it (sized to its real height, so long French lines never overflow).</summary>
        static Text CodexFlow(RectTransform parent, string text, int size, Color color, float x, ref float y, float width, FontStyle style, float gap)
        {
            var t = MetaUI.Text(parent, text, size, color, TextAnchor.UpperLeft, UIKit.TopLeft, new Vector2(x, -y), new Vector2(width, size * 1.3f), style);
            float h = Mathf.Max(size * 1.2f, Mathf.Ceil(t.preferredHeight) + 4f);
            t.rectTransform.sizeDelta = new Vector2(width, h);
            y += h + gap;
            return t;
        }

        static float CodexIntro(RectTransform content, string text, float y)
        {
            CodexFlow(content, text, 28, UIKit.Muted, 20f, ref y, CodexWidth, FontStyle.Normal, 18f);
            return y;
        }

        static void CodexAnim(RectTransform r, int index, bool animate)
        {
            if (animate) UIAnim.SlideIn(r, new Vector2(0, -50), Mathf.Min(index, 6) * 0.03f, 0.3f, 0.92f);
        }

        static string CodexTags(ItemTag tags) =>
            tags == ItemTag.None ? $"<color=#{DimHex}>Utilitaire (ne compte pour aucune famille)</color>" : Synergies.TagNames(tags);

        /// <summary>The run rules that shape a build, shown at the top of the weapons tab.</summary>
        static string CodexStuffRules(UpgradeManager um) =>
            $"<color=#{MetaUI.GoldHex}><b>Ton stuff : {um.buildCap} niveaux max</b></color> par partie (chaque niveau d'arme ou de bonus compte, " +
            $"{um.maxWeapons} armes et {um.maxPassives} bonus au plus). Les <b>évolutions</b> sont gratuites. Chaque tirage propose une carte " +
            "pour monter une de tes armes, et une arme au Nv 5 voit toujours son bonus partenaire. Une fois le stuff complet, " +
            $"chaque niveau donne une onde de choc, un soin et des points. Chaque partie, <b>{um.absentWeapons} armes</b> tirées au hasard sont absentes.";
    }
}
