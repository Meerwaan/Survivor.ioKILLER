using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Draws the game's pictograms in code (weapons, bonuses, relics, camp buildings...).
    /// Each glyph is a signed distance function (negative = inside) built from a few shapes,
    /// rendered once into a texture with an outline and a soft shadow, then cached.
    /// Picked by display name (UIKit.Icon only knows names), so new items fall back to a letter.
    /// </summary>
    public static class IconPainter
    {
        const int Size = 128;
        static readonly Dictionary<string, Sprite> cache = new();

        delegate float Sdf(Vector2 p);

        /// <summary>A white pictogram for this item name, or null if none matches (caller shows a letter).</summary>
        public static Sprite For(string name)
        {
            string key = GlyphKey(name);
            if (key == null) return null;
            // "&& s": Unity may destroy the texture (editor play mode restarts) while the static cache survives.
            if (cache.TryGetValue(key, out var s) && s) return s;
            var glyph = Glyph(key);
            if (glyph == null) return null;
            s = Render(glyph);
            cache[key] = s;
            return s;
        }

        // ---------- name -> glyph ----------

        static string GlyphKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string n = Fold(name);
            // Order matters: the most specific words first.
            foreach (var (word, key) in Keywords)
                if (n.Contains(word)) return key;
            return null;
        }

        static readonly (string, string)[] Keywords =
        {
            // Weapons and their evolutions
            ("revolver", "revolver"), ("gatling", "revolver"), ("fusil", "shotgun"), ("chevrotine", "shotgun"),
            ("machette", "machete"), ("molotov", "molotov"), ("gaz", "gas"), ("nuage", "gas"),
            ("flechette", "dart"), ("lance-flamme", "flame"), ("tesla", "bolt"), ("grenade", "grenade"),
            ("scie", "saw"), ("aiguille", "dart"), ("dragon", "flame"), ("peste", "gas"), ("sous-munition", "grenade"),
            ("hachoir", "machete"), ("napalm", "molotov"), ("canon a viande", "shotgun"),
            // Bonuses
            ("rage", "claws"), ("adrenaline", "syringe"), ("gros calibre", "area"), ("sang froid", "heartplus"),
            ("bidon", "jerrycan"), ("chargeur", "bullets"), ("cadavre", "skullburst"), ("oeil", "crosshair"),
            ("huile", "drop"), ("ricochet", "ricochet"), ("incendiaire", "bulletfire"), ("sangsue", "fangdrop"),
            ("poudre", "bomb"), ("cuivre", "coil"), ("batterie", "battery"), ("acide de", "battery"),
            ("dentelee", "serrated"), ("chauffee", "hotblade"), ("fiole d'acide", "flask"), ("ration", "medkit"),
            ("gilet", "shield"), ("dos au mur", "brokenheart"), ("bottes de course", "boot"), ("collecteur", "magnet"),
            ("injecteur", "crystal"), ("patte de lapin", "clover"),
            // Families (synergies)
            ("pyromane", "flame"), ("artificier", "bomb"), ("electricien", "bolt"), ("boucher", "machete"),
            ("pistolero", "bullets"), ("alchimiste", "flask"), ("increvable", "heartplus"),
            // Relics and pacts
            ("cryogenique", "snow"), ("bottes de plomb", "boot"), ("croc", "fangdrop"), ("cloche", "bell"),
            ("marque", "crosshair"), ("fer a cheval", "horseshoe"), ("coeur de pierre", "heartplus"),
            ("sablier", "hourglass"), ("meche", "bomb"), ("boussole", "compass"), ("barbelee", "shield"),
            ("dose de secours", "syringe"), ("pacte du sang", "drop"), ("pacte de verre", "crystal"),
            ("pacte du chaos", "skullburst"), ("pacte de l'avare", "coin"), ("pacte de fer", "shield"),
            ("pacte du sacrifice", "brokenheart"), ("pacte du chasseur", "crosshair"), ("tempete", "bolt"),
            ("pacte", "skullburst"),
            // Camp and meta
            ("potager", "sprout"), ("entrepot", "crate"), ("radio", "radio"), ("feu de camp", "flame"),
            ("cuisine", "pot"), ("dortoir", "bed"), ("douche", "drop"), ("dispensaire", "medkit"),
            ("palissade", "wall"), ("caisse", "crate"), ("coffre", "crate"), ("bourse", "coin"), ("trousse", "medkit"),
            // Menu tabs
            ("jouer", "crosshair"), ("relais", "flame"), ("carnet", "book"), ("contrats", "radio"), ("plaque", "dogtag"),
        };

        static string Fold(string s)
        {
            s = s.ToLowerInvariant();
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'à': case 'â': case 'ä': sb.Append('a'); break;
                    case 'î': case 'ï': sb.Append('i'); break;
                    case 'ô': case 'ö': sb.Append('o'); break;
                    case 'ù': case 'û': case 'ü': sb.Append('u'); break;
                    case 'ç': sb.Append('c'); break;
                    case 'œ': sb.Append("oe"); break;
                    case '’': sb.Append('\''); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ---------- rendering ----------

        static Sprite Render(Sdf f)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Icon" };
            var px = new Color32[Size * Size];
            float pix = 2f / Size;          // one pixel in glyph units (the glyph space is -1..1)
            const float outline = 0.055f;   // dark contour, keeps icons readable on any tile color
            var shadowOffset = new Vector2(0.03f, -0.055f);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                float d = f(p);
                float fill = Mathf.Clamp01(0.5f - d / pix);
                float edge = Mathf.Clamp01(0.5f - (d - outline) / pix);
                float shadow = Mathf.Clamp01(0.5f - (f(p - shadowOffset) - outline) / (pix * 3f)) * 0.45f;

                // Fill: warm off-white with a soft top light, so it reads as a stamped badge, not a flat cutout.
                float light = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(p.y * 0.5f + 0.6f));
                var fillCol = new Color(0.97f * light, 0.95f * light, 0.9f * light, 1f);
                var edgeCol = new Color(0.05f, 0.055f, 0.065f, 1f);

                // Composite: shadow < outline < fill
                Color c = new(0f, 0f, 0f, shadow);
                c = Over(c, edgeCol, edge);
                c = Over(c, fillCol, fill);
                px[y * Size + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Color Over(Color under, Color top, float a)
        {
            float outA = a + under.a * (1f - a);
            if (outA <= 0.0001f) return new Color(0, 0, 0, 0);
            Color c = (top * a + under * under.a * (1f - a)) / outA;
            c.a = outA;
            return c;
        }

        // ---------- shape helpers (Inigo Quilez style) ----------

        static float Circle(Vector2 p, float cx, float cy, float r) => (p - new Vector2(cx, cy)).magnitude - r;

        static float Ring(Vector2 p, float cx, float cy, float r, float w) => Mathf.Abs((p - new Vector2(cx, cy)).magnitude - r) - w;

        static Vector2 Rot(Vector2 p, float deg)
        {
            float a = -deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        /// <summary>Rounded box centered on (cx, cy), half size (hx, hy), rotated by deg.</summary>
        static float Box(Vector2 p, float cx, float cy, float hx, float hy, float deg = 0f, float round = 0f)
        {
            var q = Rot(p - new Vector2(cx, cy), deg);
            var d = new Vector2(Mathf.Abs(q.x) - hx + round, Mathf.Abs(q.y) - hy + round);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        static float Seg(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            var a = new Vector2(ax, ay);
            var pa = p - a;
            var ba = new Vector2(bx, by) - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        /// <summary>Any simple polygon (points in order).</summary>
        static float Poly(Vector2 p, params float[] xy)
        {
            int n = xy.Length / 2;
            var v0 = new Vector2(xy[0], xy[1]);
            float d = Vector2.Dot(p - v0, p - v0);
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                var vi = new Vector2(xy[i * 2], xy[i * 2 + 1]);
                var vj = new Vector2(xy[j * 2], xy[j * 2 + 1]);
                var e = vj - vi;
                var w = p - vi;
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= vi.y, c2 = p.y < vj.y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1f;
            }
            return s * Mathf.Sqrt(d);
        }

        static float U(params float[] d)
        {
            float m = d[0];
            for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]);
            return m;
        }

        static float Cut(float a, float b) => Mathf.Max(a, -b);

        // ---------- glyphs ----------

        static Sdf Glyph(string key)
        {
            switch (key)
            {
                case "revolver":
                    return p => U(
                        Box(p, 0.12f, 0.22f, 0.62f, 0.13f, 0f, 0.04f),          // frame + barrel
                        Box(p, -0.12f, 0.17f, 0.2f, 0.2f, 0f, 0.08f),           // cylinder
                        Box(p, -0.42f, -0.22f, 0.15f, 0.36f, -22f, 0.08f),      // grip
                        Box(p, -0.48f, 0.42f, 0.08f, 0.08f, 30f, 0.02f),        // hammer
                        Box(p, 0.68f, 0.38f, 0.04f, 0.06f),                     // sight
                        Cut(Ring(p, -0.08f, -0.05f, 0.17f, 0.045f), Box(p, -0.08f, 0.2f, 0.3f, 0.25f))); // trigger guard
                case "shotgun":
                    return p =>
                    {
                        var q = Rot(p, 28f);
                        return U(
                            Box(q, 0.2f, 0.06f, 0.78f, 0.07f, 0f, 0.03f),
                            Box(q, 0.2f, -0.09f, 0.62f, 0.06f, 0f, 0.03f),
                            Box(q, 0.18f, -0.17f, 0.2f, 0.09f, 0f, 0.05f),      // pump
                            Poly(q, -0.6f, 0.1f, -0.95f, -0.05f, -0.95f, -0.32f, -0.55f, -0.12f));
                    };
                case "machete":
                    return p =>
                    {
                        float a = MacheteAt(Rot(p, 35f));
                        float b = MacheteAt(Rot(new Vector2(-p.x, p.y), 35f));
                        return U(a, b);
                    };
                case "molotov":
                    return p => U(
                        Cut(Box(p, -0.1f, -0.38f, 0.32f, 0.5f, 0f, 0.15f), Box(p, -0.1f, -0.42f, 0.33f, 0.09f)),
                        Box(p, -0.1f, 0.2f, 0.11f, 0.2f, 0f, 0.04f),
                        Box(p, -0.1f, 0.38f, 0.14f, 0.05f, 0f, 0.02f),
                        FlameAt(p, 0.18f, 0.55f, 0.42f));
                case "gas":
                    return p => Cut(U(
                            Circle(p, -0.3f, -0.1f, 0.38f), Circle(p, 0.18f, 0.05f, 0.45f), Circle(p, 0.45f, -0.3f, 0.3f),
                            Box(p, 0.05f, -0.42f, 0.55f, 0.2f, 0f, 0.2f)),
                        U(Ring(p, -0.22f, -0.12f, 0.13f, 0.035f), Ring(p, 0.2f, 0.08f, 0.18f, 0.035f), Ring(p, 0.42f, -0.32f, 0.09f, 0.03f)));
                case "dart":
                    return p =>
                    {
                        var q = Rot(p, 45f);
                        return U(Seg(q, -0.55f, 0f, 0.5f, 0f, 0.06f),
                            Poly(q, 0.85f, 0f, 0.45f, 0.13f, 0.45f, -0.13f),
                            Poly(q, -0.4f, 0f, -0.8f, 0.3f, -0.85f, 0.05f),
                            Poly(q, -0.4f, 0f, -0.8f, -0.3f, -0.85f, -0.05f));
                    };
                case "flame":
                    return p => Cut(FlameAt(p, 0f, -0.1f, 1f), FlameAt(p, 0.05f, -0.42f, 0.42f) - 0.0f);
                case "bolt":
                    return p => Poly(p, 0.15f, 0.92f, -0.5f, -0.05f, -0.05f, -0.05f, -0.2f, -0.92f, 0.5f, 0.15f, 0.05f, 0.15f);
                case "grenade":
                    return p => U(
                        Cut(Box(p, 0f, -0.2f, 0.5f, 0.6f, 0f, 0.45f), U(Box(p, 0f, -0.02f, 0.6f, 0.03f), Box(p, 0f, -0.38f, 0.6f, 0.03f))),
                        Box(p, 0f, 0.43f, 0.18f, 0.12f, 0f, 0.03f),
                        Box(p, 0.25f, 0.22f, 0.07f, 0.32f, -20f, 0.03f),
                        Ring(p, -0.3f, 0.62f, 0.14f, 0.045f));
                case "saw":
                    return p =>
                    {
                        float r = p.magnitude;
                        float ang = Mathf.Atan2(p.y, p.x);
                        float teeth = 0.72f + 0.14f * Saw(ang * 12f / (2f * Mathf.PI));
                        float disc = r - teeth;
                        return Cut(Cut(disc, Circle(p, 0f, 0f, 0.16f)), Ring(p, 0f, 0f, 0.4f, 0.03f));
                    };
                case "claws":
                    return p => U(Slash(p, -0.38f), Slash(p, 0f), Slash(p, 0.38f));
                case "syringe":
                    return p =>
                    {
                        var q = Rot(p, -45f);
                        return Cut(U(Box(q, 0f, 0f, 0.5f, 0.16f, 0f, 0.03f),
                                Box(q, -0.62f, 0f, 0.12f, 0.06f), Box(q, -0.78f, 0f, 0.04f, 0.22f),
                                Seg(q, 0.5f, 0f, 0.95f, 0f, 0.025f), Box(q, 0.55f, 0f, 0.05f, 0.1f)),
                            Box(q, 0.12f, 0.0f, 0.3f, 0.07f));
                    };
                case "area":
                    return p => U(Ring(p, 0f, 0f, 0.42f, 0.07f), Circle(p, 0f, 0f, 0.14f),
                        Arrow(p, 0f), Arrow(p, 90f), Arrow(p, 180f), Arrow(p, 270f));
                case "heartplus":
                    return p => Cut(Heart(p, 0f, -0.05f, 1f), U(Box(p, 0f, 0.0f, 0.3f, 0.08f), Box(p, 0f, 0.0f, 0.08f, 0.3f)));
                case "brokenheart":
                    return p => Cut(Heart(p, 0f, -0.05f, 1f), Poly(p, 0.0f, 0.55f, 0.1f, 0.25f, -0.08f, 0.05f, 0.08f, -0.2f, -0.02f, -0.62f, -0.1f, -0.2f, 0.0f, 0.05f, -0.18f, 0.25f, -0.1f, 0.55f));
                case "jerrycan":
                    return p => Cut(U(Box(p, 0f, -0.15f, 0.48f, 0.65f, 0f, 0.1f), Poly(p, 0.25f, 0.5f, 0.55f, 0.85f, 0.7f, 0.75f, 0.45f, 0.4f)),
                        U(Box(p, -0.12f, 0.32f, 0.2f, 0.08f, 0f, 0.04f), Seg(p, -0.3f, -0.6f, 0.3f, 0.2f, 0.035f), Seg(p, 0.3f, -0.6f, -0.3f, 0.2f, 0.035f)));
                case "bullets":
                    return p => U(Bullet(p, -0.42f), Bullet(p, 0f), Bullet(p, 0.42f));
                case "bulletfire":
                    return p => U(Bullet(p, -0.25f), FlameAt(p, 0.38f, -0.15f, 0.62f));
                case "skullburst":
                    return p =>
                    {
                        float skull = Skull(p, 0f, -0.02f, 0.6f);
                        return U(Cut(Burst(p, 0.98f, 0.72f, 10), skull - 0.08f), skull);
                    };
                case "crosshair":
                    return p => U(Ring(p, 0f, 0f, 0.55f, 0.065f), Circle(p, 0f, 0f, 0.1f),
                        Box(p, 0f, 0.72f, 0.06f, 0.2f), Box(p, 0f, -0.72f, 0.06f, 0.2f), Box(p, 0.72f, 0f, 0.2f, 0.06f), Box(p, -0.72f, 0f, 0.2f, 0.06f));
                case "drop":
                    return p => Drop(p, 0f, -0.2f, 1f);
                case "fangdrop":
                    return p => U(Drop(p, 0.2f, -0.25f, 0.75f), Poly(p, -0.75f, 0.85f, -0.25f, 0.85f, -0.5f, -0.05f));
                case "ricochet":
                    return p => U(Seg(p, -0.8f, 0.6f, -0.2f, -0.45f, 0.08f), Seg(p, -0.2f, -0.45f, 0.4f, 0.35f, 0.08f),
                        Poly(p, 0.85f, 0.75f, 0.25f, 0.62f, 0.62f, 0.22f), Box(p, 0f, -0.72f, 0.85f, 0.05f));
                case "bomb":
                    return p => U(Circle(p, -0.1f, -0.18f, 0.55f), Box(p, 0.25f, 0.33f, 0.14f, 0.12f, -45f, 0.02f),
                        Seg(p, 0.33f, 0.43f, 0.55f, 0.68f, 0.04f), Burst(p - new Vector2(0.62f, 0.75f), 0.22f, 0.1f, 8));
                case "coil":
                    return p =>
                    {
                        float d = 9f;
                        for (int i = 0; i < 5; i++) d = Mathf.Min(d, Ring(new Vector2(p.x * 2.2f, p.y), -0.95f + i * 0.47f * 1f, 0f, 0.42f, 0.09f) / 2.2f);
                        return U(d, Seg(p, -0.95f, -0.2f, -0.6f, -0.2f, 0.05f), Seg(p, 0.6f, 0.2f, 0.95f, 0.2f, 0.05f));
                    };
                case "battery":
                    return p => Cut(U(Box(p, 0f, -0.08f, 0.42f, 0.72f, 0f, 0.08f), Box(p, 0f, 0.72f, 0.18f, 0.08f, 0f, 0.02f)),
                        Poly(p, 0.08f, 0.45f, -0.2f, -0.05f, 0.0f, -0.05f, -0.1f, -0.55f, 0.2f, 0.0f, 0.0f, 0.0f));
                case "serrated":
                    return p =>
                    {
                        var q = Rot(p, 40f);
                        float blade = Box(q, 0.15f, 0.05f, 0.62f, 0.2f, 0f, 0.05f);
                        float teeth = Mathf.Abs(Saw((q.x + 2f) * 6f)) * 0.12f;
                        blade = Mathf.Max(blade, -(q.y + 0.15f - teeth));
                        return U(blade, Box(q, -0.68f, 0.05f, 0.22f, 0.1f, 0f, 0.05f), Box(q, -0.45f, 0.05f, 0.03f, 0.22f));
                    };
                case "hotblade":
                    return p => U(MacheteAt(Rot(p - new Vector2(-0.15f, -0.1f), 35f) * 1.15f) / 1.15f, FlameAt(p, 0.5f, 0.35f, 0.5f));
                case "flask":
                    return p => Cut(U(Poly(p, -0.14f, 0.45f, 0.14f, 0.45f, 0.62f, -0.62f, 0.55f, -0.78f, -0.55f, -0.78f, -0.62f, -0.62f),
                            Box(p, 0f, 0.6f, 0.2f, 0.18f, 0f, 0.03f)),
                        U(Circle(p, -0.12f, -0.45f, 0.08f), Circle(p, 0.18f, -0.3f, 0.06f)));
                case "medkit":
                    return p => Cut(U(Box(p, 0f, -0.1f, 0.78f, 0.55f, 0f, 0.12f), Ring(p, 0f, 0.5f, 0.22f, 0.06f)),
                        U(Box(p, 0f, -0.1f, 0.38f, 0.11f), Box(p, 0f, -0.1f, 0.11f, 0.38f)));
                case "shield":
                    return p =>
                    {
                        float outer = Poly(p, 0f, 0.85f, 0.7f, 0.6f, 0.62f, -0.15f, 0f, -0.88f, -0.62f, -0.15f, -0.7f, 0.6f);
                        float inner = Poly(p * 1.45f, 0f, 0.85f, 0.7f, 0.6f, 0.62f, -0.15f, 0f, -0.88f, -0.62f, -0.15f, -0.7f, 0.6f) / 1.45f;
                        return Cut(outer, Mathf.Abs(inner) - 0.035f);
                    };
                case "boot":
                    return p => U(Box(p, -0.2f, 0.2f, 0.3f, 0.55f, 0f, 0.08f), Box(p, 0.15f, -0.48f, 0.62f, 0.25f, 0f, 0.12f),
                        Seg(p, -0.85f, 0.35f, -0.6f, 0.35f, 0.05f), Seg(p, -0.95f, 0.05f, -0.6f, 0.05f, 0.05f), Seg(p, -0.85f, -0.25f, -0.6f, -0.25f, 0.05f));
                case "magnet":
                    return p => U(Cut(Ring(p, 0f, 0.05f, 0.45f, 0.17f), Box(p, 0f, -0.5f, 1f, 0.55f)),
                        Box(p, -0.45f, -0.3f, 0.17f, 0.35f), Box(p, 0.45f, -0.3f, 0.17f, 0.35f));
                case "crystal":
                    return p => U(Poly(p, 0f, 0.95f, 0.3f, 0.2f, 0.18f, -0.75f, -0.18f, -0.75f, -0.3f, 0.2f),
                        Poly(p, -0.45f, 0.45f, -0.25f, -0.2f, -0.38f, -0.72f, -0.62f, -0.55f, -0.65f, 0.0f),
                        Poly(p, 0.5f, 0.3f, 0.66f, -0.1f, 0.6f, -0.68f, 0.38f, -0.72f, 0.33f, -0.15f));
                case "clover":
                    return p => U(Heart(Rot(p - new Vector2(0f, 0.36f), 0f), 0f, 0f, 0.42f), Heart(Rot(p - new Vector2(0f, -0.28f), 180f), 0f, 0f, 0.42f),
                        Heart(Rot(p - new Vector2(-0.32f, 0.04f), 90f), 0f, 0f, 0.42f), Heart(Rot(p - new Vector2(0.32f, 0.04f), -90f), 0f, 0f, 0.42f),
                        Seg(p, 0.05f, -0.1f, 0.35f, -0.9f, 0.055f));
                case "snow":
                    return p => U(Spoke(p, 0f), Spoke(p, 60f), Spoke(p, 120f), Circle(p, 0f, 0f, 0.15f));
                case "bell":
                    return p => U(Poly(p, -0.15f, 0.62f, 0.15f, 0.62f, 0.4f, 0.3f, 0.48f, -0.35f, 0.65f, -0.5f, -0.65f, -0.5f, -0.48f, -0.35f, -0.4f, 0.3f),
                        Circle(p, 0f, -0.65f, 0.15f), Ring(p, 0f, 0.72f, 0.12f, 0.04f));
                case "horseshoe":
                    return p => Cut(U(Cut(Ring(p, 0f, 0.1f, 0.5f, 0.17f), Box(p, 0f, -0.55f, 0.32f, 0.4f)),
                        Box(p, -0.5f, -0.55f, 0.2f, 0.1f, 0f, 0.03f), Box(p, 0.5f, -0.55f, 0.2f, 0.1f, 0f, 0.03f)),
                        U(Circle(p, -0.5f, 0.1f, 0.05f), Circle(p, 0.5f, 0.1f, 0.05f), Circle(p, 0f, 0.6f, 0.05f), Circle(p, -0.36f, 0.45f, 0.05f), Circle(p, 0.36f, 0.45f, 0.05f)));
                case "hourglass":
                    return p => U(Box(p, 0f, 0.8f, 0.55f, 0.08f, 0f, 0.03f), Box(p, 0f, -0.8f, 0.55f, 0.08f, 0f, 0.03f),
                        Poly(p, -0.42f, 0.7f, 0.42f, 0.7f, 0.08f, 0f, 0.42f, -0.7f, -0.42f, -0.7f, -0.08f, 0f));
                case "compass":
                    return p => Cut(U(Ring(p, 0f, 0f, 0.7f, 0.08f), Poly(p, 0f, 0.55f, 0.16f, 0f, 0f, -0.55f, -0.16f, 0f)),
                        Poly(p, 0f, -0.48f, 0.1f, -0.02f, -0.1f, -0.02f));
                case "coin":
                    return p => Cut(Circle(p, 0f, 0f, 0.7f), U(Ring(p, 0f, 0f, 0.52f, 0.045f), Box(p, 0f, 0f, 0.07f, 0.3f)));
                case "sprout":
                    return p => U(Seg(p, 0f, -0.75f, 0f, 0.2f, 0.06f), Leaf(p, -0.32f, 0.3f, 35f), Leaf(p, 0.32f, 0.45f, -35f),
                        Box(p, 0f, -0.78f, 0.6f, 0.1f, 0f, 0.05f));
                case "crate":
                    return p => U(Cut(Box(p, 0f, 0f, 0.72f, 0.62f, 0f, 0.06f), Box(p, 0f, 0f, 0.5f, 0.4f)),
                        Seg(p, -0.48f, -0.38f, 0.48f, 0.38f, 0.09f), Box(p, 0f, 0f, 0.5f, 0.03f));
                case "radio":
                    return p => Cut(U(Box(p, 0f, -0.25f, 0.75f, 0.45f, 0f, 0.1f), Seg(p, 0.35f, 0.2f, 0.65f, 0.9f, 0.04f)),
                        U(Circle(p, -0.3f, -0.25f, 0.22f), Box(p, 0.38f, -0.15f, 0.22f, 0.05f), Box(p, 0.38f, -0.35f, 0.22f, 0.05f)));
                case "pot":
                    return p => U(Box(p, 0f, -0.25f, 0.6f, 0.42f, 0f, 0.12f), Box(p, 0f, 0.22f, 0.72f, 0.07f, 0f, 0.03f),
                        Box(p, 0f, 0.36f, 0.12f, 0.08f, 0f, 0.03f), Box(p, -0.78f, -0.05f, 0.14f, 0.05f), Box(p, 0.78f, -0.05f, 0.14f, 0.05f),
                        Seg(p, -0.25f, 0.5f, -0.2f, 0.85f, 0.035f), Seg(p, 0.15f, 0.5f, 0.2f, 0.85f, 0.035f));
                case "bed":
                    return p => U(Box(p, 0.1f, -0.15f, 0.8f, 0.14f, 0f, 0.04f), Box(p, -0.62f, 0.12f, 0.2f, 0.14f, 0f, 0.08f),
                        Box(p, 0.25f, 0.05f, 0.55f, 0.08f, 0f, 0.05f), Box(p, -0.8f, -0.1f, 0.05f, 0.5f), Box(p, 0.85f, -0.4f, 0.05f, 0.2f), Box(p, -0.8f, -0.4f, 0.05f, 0.2f));
                case "book":
                    return p => Cut(U(Box(p, -0.38f, 0f, 0.36f, 0.62f, 6f, 0.05f), Box(p, 0.38f, 0f, 0.36f, 0.62f, -6f, 0.05f)),
                        U(Box(p, 0f, 0f, 0.035f, 0.7f), Box(p, -0.4f, 0.25f, 0.2f, 0.03f, 6f), Box(p, -0.4f, 0.05f, 0.2f, 0.03f, 6f),
                            Box(p, 0.4f, 0.25f, 0.2f, 0.03f, -6f), Box(p, 0.4f, 0.05f, 0.2f, 0.03f, -6f)));
                case "dogtag":
                    return p => U(Cut(Box(p, 0.08f, -0.12f, 0.42f, 0.62f, -12f, 0.22f),
                            U(Circle(p, 0.2f, 0.36f, 0.08f), Box(p, 0.05f, -0.15f, 0.22f, 0.035f, -12f), Box(p, 0.02f, -0.32f, 0.18f, 0.035f, -12f))),
                        Cut(Ring(p, -0.12f, 0.55f, 0.32f, 0.03f), Box(p, 0.2f, 0.2f, 0.25f, 0.3f)));
                case "wall":
                    return p => Cut(Box(p, 0f, -0.05f, 0.85f, 0.65f, 0f, 0.03f),
                        U(Box(p, 0f, 0.2f, 1f, 0.03f), Box(p, 0f, -0.25f, 1f, 0.03f),
                            Box(p, 0f, 0.42f, 0.03f, 0.2f), Box(p, -0.45f, -0.03f, 0.03f, 0.2f), Box(p, 0.45f, -0.03f, 0.03f, 0.2f), Box(p, 0f, -0.48f, 0.03f, 0.2f)));
            }
            return null;
        }

        // ---------- glyph parts ----------

        static float MacheteAt(Vector2 q) => U(
            Poly(q, -0.25f, 0.08f, 0.55f, 0.16f, 0.88f, 0.05f, 0.82f, -0.12f, -0.25f, -0.1f),
            Box(q, -0.55f, -0.01f, 0.3f, 0.09f, 0f, 0.05f), Box(q, -0.26f, -0.01f, 0.03f, 0.15f));

        static float FlameAt(Vector2 p, float cx, float cy, float s)
        {
            var q = (p - new Vector2(cx, cy)) / s;
            float body = Circle(q, 0f, -0.3f, 0.45f);
            float tip = Poly(q, 0.05f, 0.95f, 0.42f, -0.2f, -0.42f, -0.2f);
            float side = Poly(q, -0.35f, 0.5f, -0.05f, -0.1f, -0.45f, -0.15f);
            return U(body, tip, side) * s;
        }

        static float Heart(Vector2 p, float cx, float cy, float s)
        {
            var q = (p - new Vector2(cx, cy)) / s;
            float a = Circle(q, -0.3f, 0.2f, 0.36f), b = Circle(q, 0.3f, 0.2f, 0.36f);
            float c = Poly(q, -0.64f, 0.12f, 0.64f, 0.12f, 0f, -0.75f);
            return U(a, b, c) * s;
        }

        static float Drop(Vector2 p, float cx, float cy, float s)
        {
            var q = (p - new Vector2(cx, cy)) / s;
            return U(Circle(q, 0f, -0.15f, 0.5f), Poly(q, 0f, 1.05f, 0.47f, -0.05f, -0.47f, -0.05f)) * s;
        }

        static float Bullet(Vector2 p, float x) => U(Box(p, x, -0.2f, 0.13f, 0.45f, 0f, 0.02f), Circle(p, x, 0.25f, 0.13f),
            Poly(p, x - 0.13f, 0.25f, x + 0.13f, 0.25f, x, 0.62f), Box(p, x, -0.7f, 0.16f, 0.05f));

        static float Slash(Vector2 p, float off)
        {
            var q = Rot(p - new Vector2(off, 0f), -20f);
            return Poly(q, 0f, 0.9f, 0.09f, 0f, 0f, -0.9f, -0.05f, 0f);
        }

        static float Arrow(Vector2 p, float deg)
        {
            var q = Rot(p, deg);
            return Poly(q, 0.95f, 0f, 0.7f, 0.2f, 0.7f, -0.2f);
        }

        static float Skull(Vector2 p, float cx, float cy, float s)
        {
            var q = (p - new Vector2(cx, cy)) / s;
            float head = U(Circle(q, 0f, 0.15f, 0.62f), Box(q, 0f, -0.45f, 0.35f, 0.3f, 0f, 0.1f));
            float holes = U(Circle(q, -0.25f, 0.05f, 0.17f), Circle(q, 0.25f, 0.05f, 0.17f), Poly(q, 0f, -0.15f, 0.08f, -0.32f, -0.08f, -0.32f),
                Box(q, -0.12f, -0.62f, 0.035f, 0.12f), Box(q, 0.12f, -0.62f, 0.035f, 0.12f));
            return Cut(head, holes) * s;
        }

        static float Burst(Vector2 p, float rOut, float rIn, int spikes)
        {
            float ang = Mathf.Atan2(p.y, p.x);
            float t = Mathf.Abs(Saw(ang * spikes / (2f * Mathf.PI)));
            float r = Mathf.Lerp(rOut, rIn, t);
            return p.magnitude - r;
        }

        static float Spoke(Vector2 p, float deg)
        {
            var q = Rot(p, deg);
            return U(Box(q, 0f, 0f, 0.85f, 0.06f), Seg(q, 0.55f, 0f, 0.75f, 0.2f, 0.05f), Seg(q, 0.55f, 0f, 0.75f, -0.2f, 0.05f),
                Seg(q, -0.55f, 0f, -0.75f, 0.2f, 0.05f), Seg(q, -0.55f, 0f, -0.75f, -0.2f, 0.05f));
        }

        static float Leaf(Vector2 p, float cx, float cy, float deg)
        {
            var q = Rot(p - new Vector2(cx, cy), deg);
            return Mathf.Max(Circle(q, 0f, 0.2f, 0.32f), Circle(q, 0f, -0.2f, 0.32f));
        }

        /// <summary>Triangle wave in -1..1 (period 1).</summary>
        static float Saw(float x)
        {
            float f = x - Mathf.Floor(x);
            return 1f - 4f * Mathf.Abs(f - 0.5f);
        }
    }
}
