using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survivor
{
    /// <summary>A theorycraft build: a few weapons and bonuses the player likes together.</summary>
    [Serializable]
    public class Preset
    {
        public string name = "";
        /// <summary>Weapon ids (Weapon.Id, the type name).</summary>
        public List<string> weapons = new();
        /// <summary>Passive ids (Passive.id).</summary>
        public List<string> passives = new();

        public bool Has(string id) => weapons.Contains(id) || passives.Contains(id);
    }

    /// <summary>
    /// Saved build presets (Codex tab). A preset never picks anything for the player and never changes
    /// the level-up draws: the cards of the active preset only get a small "PRESET" reminder badge.
    /// Saved as JSON in its own PlayerPrefs key, independent from the rest of the meta save.
    /// </summary>
    public static class Presets
    {
        public const int MaxPresets = 8;
        public const int NameLength = 18;
        const string Key = "presets_v1";

        [Serializable]
        class SaveData
        {
            public int active = -1;
            public List<Preset> presets = new();
        }

        static SaveData data;

        public static event Action Changed;

        static SaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static IReadOnlyList<Preset> All => Data.presets;
        public static int Count => Data.presets.Count;
        /// <summary>Index of the active preset, -1 = none.</summary>
        public static int ActiveIndex => Data.active;
        public static Preset Active => Data.active >= 0 && Data.active < Data.presets.Count ? Data.presets[Data.active] : null;

        /// <summary>Same slot limits as in a run.</summary>
        public static int MaxWeapons => UpgradeManager.Instance ? UpgradeManager.Instance.maxWeapons : 5;
        public static int MaxPassives => UpgradeManager.Instance ? UpgradeManager.Instance.maxPassives : 6;

        /// <summary>True if the weapon or passive id is part of the active preset (level-up card badge).</summary>
        public static bool InActive(string id)
        {
            var p = Active;
            return p != null && !string.IsNullOrEmpty(id) && p.Has(id);
        }

        public static int IndexOf(Preset p) => p == null ? -1 : Data.presets.IndexOf(p);

        /// <summary>New empty preset named "Stuff N" (null when the list is full).</summary>
        public static Preset Create()
        {
            if (Data.presets.Count >= MaxPresets) return null;
            var p = new Preset { name = FreeName() };
            Data.presets.Add(p);
            // The first preset ever made is active right away: that's what people expect.
            if (Data.presets.Count == 1) Data.active = 0;
            Save();
            return p;
        }

        public static void Delete(int index)
        {
            if (index < 0 || index >= Data.presets.Count) return;
            Data.presets.RemoveAt(index);
            if (Data.active == index) Data.active = -1;
            else if (Data.active > index) Data.active--;
            Save();
        }

        /// <summary>-1 = no active preset.</summary>
        public static void SetActive(int index)
        {
            if (index < -1 || index >= Data.presets.Count) index = -1;
            if (Data.active == index) return;
            Data.active = index;
            Save();
        }

        public static void Rename(Preset p, string name)
        {
            if (p == null) return;
            name = Clean(name);
            if (name.Length == 0 || name == p.name) return;
            p.name = name;
            Save();
        }

        /// <summary>Adds or removes an item. Returns false when adding but the slots are full.</summary>
        public static bool Toggle(Preset p, string id, bool weapon)
        {
            if (p == null || string.IsNullOrEmpty(id)) return false;
            var list = weapon ? p.weapons : p.passives;
            if (list.Remove(id))
            {
                Save();
                return true;
            }
            if (list.Count >= (weapon ? MaxWeapons : MaxPassives)) return false;
            list.Add(id);
            Save();
            return true;
        }

        /// <summary>Drops ids that no longer exist (renamed or removed items), so they can't eat a slot forever.</summary>
        public static void RemoveUnknown(Predicate<string> isWeapon, Predicate<string> isPassive)
        {
            int removed = 0;
            foreach (var p in Data.presets)
            {
                removed += p.weapons.RemoveAll(id => !isWeapon(id));
                removed += p.passives.RemoveAll(id => !isPassive(id));
            }
            if (removed > 0) Save();
        }

        // ---------- Save ----------

        static string FreeName()
        {
            for (int n = 1; ; n++)
            {
                string name = "Stuff " + n;
                if (!Data.presets.Exists(p => p.name == name)) return name;
            }
        }

        /// <summary>No rich-text tags (names are shown in rich-text labels), trimmed, limited length.</summary>
        static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = name.Replace("<", "").Replace(">", "").Replace("\n", " ").Trim();
            return name.Length > NameLength ? name.Substring(0, NameLength).Trim() : name;
        }

        static void Load()
        {
            data = null;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<SaveData>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Presets: corrupted save, starting fresh ({e.Message})");
                    data = null;
                }
            }
            data ??= new SaveData();
            Sanitize(data);
        }

        static void Sanitize(SaveData d)
        {
            d.presets ??= new List<Preset>();
            d.presets.RemoveAll(p => p == null);
            if (d.presets.Count > MaxPresets) d.presets.RemoveRange(MaxPresets, d.presets.Count - MaxPresets);
            foreach (var p in d.presets)
            {
                p.weapons = Distinct(p.weapons);
                p.passives = Distinct(p.passives);
                p.name = Clean(p.name);
            }
            // Names after the cleanup so FreeName sees the others.
            data = d;
            foreach (var p in d.presets)
                if (p.name.Length == 0) p.name = FreeName();
            if (d.active < -1 || d.active >= d.presets.Count) d.active = -1;
        }

        static List<string> Distinct(List<string> list)
        {
            var result = new List<string>();
            if (list == null) return result;
            foreach (var id in list)
                if (!string.IsNullOrEmpty(id) && !result.Contains(id)) result.Add(id);
            return result;
        }

        static void Save()
        {
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Presets: could not save ({e.Message})");
            }
            Changed?.Invoke();
        }
    }

    /// <summary>The pink heart "PRESET" tag, shared by the Codex and the level-up cards.</summary>
    public static class PresetBadge
    {
        public static readonly Color Tint = new(1f, 0.36f, 0.6f);
        public const string Hex = "FF5C99";

        static Sprite heart;

        /// <summary>Heart shape, generated once (the legacy font may not have "♥").</summary>
        public static Sprite Heart => heart ? heart : heart = MakeHeart();

        /// <summary>Pill with a heart and "PRESET", anchored by its bottom-left corner.</summary>
        public static RectTransform Create(Transform parent, Vector2 anchor, Vector2 position, float height)
        {
            float width = height * 3.6f;
            var pill = UIKit.Rect("Preset Badge", parent);
            UIKit.Place(pill, anchor, position, new Vector2(width, height));
            // Chamfered chip (same family as the other tags), with a darker lip so it reads on light cards.
            var lip = UIKit.Image(pill, new Color(Tint.r * 0.5f, Tint.g * 0.5f, Tint.b * 0.5f), UIKit.ChipSprite);
            lip.type = Image.Type.Sliced;
            lip.raycastTarget = false;
            var face = UIKit.Rect("Face", pill);
            UIKit.Stretch(face, 2f);
            face.offsetMin = new Vector2(2f, 4f);
            var f = UIKit.Image(face, Tint, UIKit.ChipSprite);
            f.type = Image.Type.Sliced;
            f.raycastTarget = false;

            var icon = UIKit.Rect("Heart", pill);
            UIKit.Place(icon, UIKit.Left, new Vector2(height * 0.18f, 0f), new Vector2(height * 0.72f, height * 0.72f));
            UIKit.Image(icon, Color.white, Heart).raycastTarget = false;

            var label = UIKit.Label(pill, "PRESET", Mathf.RoundToInt(height * 0.58f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            var r = label.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(height * 0.9f, 0f);
            r.offsetMax = new Vector2(-height * 0.12f, 0f);
            label.GetComponent<Shadow>().effectDistance = new Vector2(2, -2);
            return pill;
        }

        public static RectTransform HeartIcon(Transform parent, Vector2 anchor, Vector2 position, float size, Color color)
        {
            var r = UIKit.Rect("Heart", parent);
            UIKit.Place(r, anchor, position, new Vector2(size, size));
            UIKit.Image(r, color, Heart).raycastTarget = false;
            return r;
        }

        static Sprite MakeHeart()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 2x2 supersampling of the classic heart curve (x² + y² - 1)³ - x²y³ <= 0.
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    float u = ((x + 0.25f + sx * 0.5f) / size * 2f - 1f) * 1.25f;
                    float v = ((y + 0.25f + sy * 0.5f) / size * 2f - 1f) * 1.25f + 0.1f;
                    float a = u * u + v * v - 1f;
                    if (a * a * a - u * u * v * v * v <= 0f) hits++;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
