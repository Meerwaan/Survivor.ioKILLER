using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Procedural FX textures: white RGB, the shape lives in the alpha. The prototype builder saves them once
    /// as PNG assets (Assets/_Game/Textures/T_FX_*.png); GameFX only generates them at runtime as a fallback
    /// when a material is missing (scene not rebuilt yet). All deterministic.
    /// </summary>
    public static class FXTextures
    {
        /// <summary>Builds a texture from an alpha function of (u, v), both in [-1, 1].</summary>
        public static Texture2D Make(string name, int width, int height, System.Func<float, float, float> alpha, bool mipmaps = false)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width * 2f - 1f, v = (y + 0.5f) / height * 2f - 1f;
                px[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255f + 0.5f));
            }
            tex.SetPixels32(px);
            tex.Apply(mipmaps, false);
            return tex;
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x, y) * amp;
                norm += amp;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float Len(float u, float v) => Mathf.Sqrt(u * u + v * v);

        /// <summary>Shockwave: a thin bright band near the edge and a faint gradient inward. No inner fill.</summary>
        public static Texture2D RingBand() => Make("T_FX_RingBand", 256, 256, (u, v) =>
        {
            float d = Len(u, v);
            if (d >= 1f) return 0f;
            float band = Mathf.Exp(-Sq((d - RingEdge) / 0.03f));
            float inner = d < RingEdge ? Sq(Mathf.Clamp01((d - 0.45f) / (RingEdge - 0.45f))) * 0.2f : 0f;
            return Mathf.Max(band, inner);
        }, true);

        /// <summary>Where the band of <see cref="RingBand"/> peaks (fraction of the quad's half size).</summary>
        public const float RingEdge = 0.93f;
        /// <summary>Where the band of <see cref="Wave"/> peaks.</summary>
        public const float WaveEdge = 0.955f;

        /// <summary>Big shockwaves (> 4 m): even thinner band, almost no inner gradient.</summary>
        public static Texture2D Wave() => Make("T_FX_Wave", 256, 256, (u, v) =>
        {
            float d = Len(u, v);
            if (d >= 1f) return 0f;
            float band = Mathf.Exp(-Sq((d - WaveEdge) / 0.017f));
            float inner = d < WaveEdge ? Sq(Mathf.Clamp01((d - 0.6f) / (WaveEdge - 0.6f))) * 0.1f : 0f;
            return Mathf.Max(band, inner);
        }, true);

        /// <summary>Telegraph fill: an even soft disc, a little denser at the rim.</summary>
        public static Texture2D Fill() => Make("T_FX_Fill", 128, 128, (u, v) =>
        {
            float d = Len(u, v);
            if (d >= 1f) return 0f;
            return (0.55f + 0.45f * Mathf.Pow(d, 6f)) * Smooth(1f, 0.94f, d);
        });

        /// <summary>Explosion core / muzzle flash: hot core, soft halo and a faint four-point star.</summary>
        public static Texture2D Flare() => Make("T_FX_Flare", 128, 128, (u, v) =>
        {
            float d = Len(u, v);
            if (d >= 1f) return 0f;
            float core = Mathf.Exp(-d * d / 0.04f);
            float halo = Sq(1f - d) * 0.6f;
            float rays = (Mathf.Exp(-v * v / 0.0012f) + Mathf.Exp(-u * u / 0.0012f)) * Mathf.Pow(1f - d, 1.6f) * 0.4f;
            return core + halo + rays;
        });

        /// <summary>Soft lumpy puff: smoke, dust, gas and flames.</summary>
        public static Texture2D Puff() => Make("T_FX_Puff", 128, 128, (u, v) =>
        {
            float n = Fbm(u * 1.8f + 3.1f, v * 1.8f + 7.3f, 4);
            float d = Len(u, v) + (n - 0.5f) * 0.5f;
            return Smooth(1f, 0.2f, d) * (0.5f + 0.5f * n);
        });

        /// <summary>Big blotchy cloud fading to the edge: the gas aura and burning ground.</summary>
        public static Texture2D Cloud() => Make("T_FX_Cloud", 256, 256, (u, v) =>
        {
            float d = Len(u, v);
            if (d >= 1f) return 0f;
            float n = Fbm(u * 2.6f + 11.3f, v * 2.6f + 4.7f, 4);
            float mask = Smooth(1f, 0.5f, d);
            return mask * (0.18f + 0.82f * Smooth(0.32f, 0.78f, n));
        });

        /// <summary>Line / trail profile across the width (V): soft edges, hot center.</summary>
        public static Texture2D Beam() => Make("T_FX_Beam", 8, 64, (u, v) =>
            Mathf.Exp(-Sq(v / 0.45f)) * 0.7f + Mathf.Exp(-Sq(v / 0.14f)) * 0.5f);

        /// <summary>Ground stain: an irregular blob with a few droplets around it.</summary>
        public static Texture2D Splat() => Make("T_FX_Splat", 128, 128, (u, v) =>
        {
            float d = Len(u, v);
            float angle = Mathf.Atan2(v, u);
            float edge = 0.58f + 0.2f * (Mathf.PerlinNoise(Mathf.Cos(angle) * 1.6f + 5f, Mathf.Sin(angle) * 1.6f + 5f) - 0.5f) * 2f;
            float blob = Smooth(edge + 0.05f, edge - 0.05f, d);
            float drops = 0f;
            for (int i = 0; i < Drops.Length; i += 3)
            {
                float du = u - Drops[i], dv = v - Drops[i + 1];
                drops = Mathf.Max(drops, Smooth(Drops[i + 2], Drops[i + 2] * 0.6f, Len(du, dv)));
            }
            float body = Mathf.Max(blob, drops);
            float grain = 0.75f + 0.25f * Fbm(u * 4f + 2f, v * 4f + 8f, 3);
            return body * grain;
        });

        // x, y, radius of the droplets around the stain
        static readonly float[] Drops =
        {
            0.78f, 0.12f, 0.07f, -0.7f, 0.42f, 0.06f, 0.2f, -0.82f, 0.05f, -0.48f, -0.66f, 0.08f,
            0.55f, 0.6f, 0.05f, 0.86f, -0.4f, 0.04f, -0.88f, -0.1f, 0.045f,
        };

        static float Sq(float x) => x * x;
    }
}
