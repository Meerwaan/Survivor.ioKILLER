using System;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Tiny synthesizer used by <see cref="SFX"/>: oscillators, noise, one-pole filters and envelopes
    /// written straight into float buffers. Phases are in cycles (0..1), times in seconds.
    /// </summary>
    static class Synth
    {
        public const int Rate = 44100;
        public const int Sine = 0, Triangle = 1, Square = 2, Saw = 3;

        static uint seed = 2463534242u;

        /// <summary>White noise in -1..1 (xorshift: fast and deterministic, leaves UnityEngine.Random alone).</summary>
        public static float Noise()
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed / 4294967295f * 2f - 1f;
        }

        public static float[] New(float seconds) => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * Rate))];
        public static float T(int i) => i / (float)Rate;

        /// <summary>One-pole low-pass coefficient for a cutoff in Hz: y += k * (x - y).</summary>
        public static float LP(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(10f, hz) / Rate);

        public static float Decay(float t, float tau) => Mathf.Exp(-t / tau);
        /// <summary>Linear attack, then exponential decay.</summary>
        public static float AD(float t, float attack, float tau) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / tau);

        public static float Wave(int shape, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            switch (shape)
            {
                case Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Square: return p < 0.5f ? 1f : -1f;
                case Saw: return 2f * p - 1f;
                default: return Mathf.Sin(p * 2f * Mathf.PI);
            }
        }

        public static float SinC(float cycles) => Mathf.Sin(cycles * 2f * Mathf.PI);
        public static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        public static float Tanh(float x) => (float)Math.Tanh(x);

        /// <summary>Adds a plucked note: quick attack, exponential decay, short fade at the end.</summary>
        public static void Tone(float[] b, float start, float length, float freq, int shape, float amp, float tau, float vibrato = 0f)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int n = Mathf.RoundToInt(length * Rate);
            float phase = 0f;
            for (int k = 0; k < n; k++)
            {
                int idx = s0 + k;
                if (idx >= b.Length) break;
                float t = k / (float)Rate;
                float f = freq * (1f + vibrato * SinC(5.5f * t));
                phase += f / Rate;
                if (phase > 1f) phase -= 1f;
                float tail = Mathf.Clamp01((length - t) / 0.01f);
                b[idx] += amp * AD(t, 0.004f, tau) * tail * Wave(shape, phase);
            }
        }

        public static void LowPass(float[] b, float hz)
        {
            float k = LP(hz), y = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                y += k * (b[i] - y);
                b[i] = y;
            }
        }

        /// <summary>Normalizes to <paramref name="peak"/> and adds tiny fades so clips never click.</summary>
        public static void Finish(float[] b, float peak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++) max = Mathf.Max(max, Mathf.Abs(b[i]));
            float scale = max > 0.0001f ? peak / max : 1f;
            int fadeIn = Mathf.Min(b.Length, 22), fadeOut = Mathf.Min(b.Length, 264);
            for (int i = 0; i < b.Length; i++)
            {
                float g = scale;
                if (i < fadeIn) g *= i / (float)fadeIn;
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < fadeOut) g *= fromEnd / (float)fadeOut;
                b[i] *= g;
            }
        }
    }

    /// <summary>The recipe for every <see cref="SfxId"/>. Runs once at startup (a few milliseconds).</summary>
    static class SfxBank
    {
        public static float[] Build(SfxId id)
        {
            switch (id)
            {
                case SfxId.Gunshot: return Gunshot();
                case SfxId.Shotgun: return Shotgun();
                case SfxId.Zap: return Zap();
                case SfxId.Throw: return Throw();
                case SfxId.Slash: return Slash();
                case SfxId.Saw: return SawBuzz();
                case SfxId.Ignite: return Ignite();
                case SfxId.Hit: return Hit();
                case SfxId.Kill: return Kill();
                case SfxId.Gore: return Gore();
                case SfxId.Explosion: return Explosion();
                case SfxId.Fuse: return Fuse();
                case SfxId.EnemyShoot: return EnemyShoot();
                case SfxId.Break: return Break();
                case SfxId.Metal: return Metal();
                case SfxId.Gem: return Gem();
                case SfxId.Coin: return Coin();
                case SfxId.Heal: return Heal();
                case SfxId.Magnet: return Magnet();
                case SfxId.Chest: return Chest();
                case SfxId.Hurt: return Hurt();
                case SfxId.LevelUp: return LevelUp();
                case SfxId.Evolution: return Evolution();
                case SfxId.Pick: return Pick();
                case SfxId.Click: return Click();
                case SfxId.GameOver: return GameOver();
                case SfxId.Horde: return Horde();
                case SfxId.BossRoar: return BossRoar();
                case SfxId.Alert: return Alert();
                case SfxId.Callout: return Callout();
                case SfxId.Frenzy: return Frenzy();
                case SfxId.Success: return Success();
                case SfxId.Fail: return Fail();
                case SfxId.Thud: return Thud();
                case SfxId.Squeak: return Squeak();
                case SfxId.Whoosh: return Whoosh();
                case SfxId.Bell: return Bell();
                case SfxId.Relic: return Relic();
                case SfxId.Pact: return Pact();
                default: return Click();
            }
        }

        // ---------- Weapons ----------

        static float[] Gunshot()
        {
            var b = Synth.New(0.28f);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                lp += Synth.LP(Mathf.Lerp(9000f, 1200f, t / 0.28f)) * (Synth.Noise() - lp);
                float crack = lp * Synth.AD(t, 0.001f, 0.03f);
                phase += (55f + 170f * Synth.Decay(t, 0.03f)) / Synth.Rate;
                float thump = Synth.SinC(phase) * Synth.AD(t, 0.002f, 0.07f);
                b[i] = crack + thump * 0.8f;
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Shotgun()
        {
            var b = Synth.New(0.5f);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                lp += Synth.LP(Mathf.Lerp(6000f, 700f, t / 0.5f)) * (Synth.Noise() - lp);
                float blast = lp * Synth.AD(t, 0.001f, 0.07f);
                phase += (45f + 130f * Synth.Decay(t, 0.05f)) / Synth.Rate;
                float thump = Synth.SinC(phase) * Synth.AD(t, 0.002f, 0.12f);
                // Pump: two small mechanical clicks at the end.
                float click = 0f;
                if (t > 0.34f) click += Synth.Noise() * Synth.Decay(t - 0.34f, 0.004f) * 0.35f;
                if (t > 0.41f) click += Synth.Noise() * Synth.Decay(t - 0.41f, 0.004f) * 0.3f;
                b[i] = Synth.Tanh((blast * 1.3f + thump) * 1.5f) + click;
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Zap()
        {
            var b = Synth.New(0.22f);
            float f = 1000f, phase = 0f, lo = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                if (i % 90 == 0) f = 500f + (Synth.Noise() * 0.5f + 0.5f) * 1400f;
                phase += f / Synth.Rate;
                float n = Synth.Noise();
                lo += Synth.LP(3000f) * (n - lo);
                b[i] = (Synth.Wave(Synth.Square, phase) * 0.5f + (n - lo) * 0.6f) * Synth.AD(t, 0.002f, 0.07f);
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Throw()
        {
            const float len = 0.32f;
            var b = Synth.New(len);
            float lo = 0f, hi = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float cut = Mathf.Lerp(500f, 2600f, t / len);
                float n = Synth.Noise();
                lo += Synth.LP(cut * 0.5f) * (n - lo);
                hi += Synth.LP(cut * 2f) * (n - hi);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len));
                b[i] = (hi - lo) * env * env;
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Slash()
        {
            const float len = 0.18f;
            var b = Synth.New(len);
            float lo = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float n = Synth.Noise();
                lo += Synth.LP(Mathf.Lerp(2500f, 7000f, t / len) * 0.4f) * (n - lo);
                phase += 3400f / Synth.Rate;
                float shing = Synth.SinC(phase) * Synth.Decay(t, 0.05f) * 0.25f;
                b[i] = (n - lo) * Synth.AD(t, 0.015f, 0.04f) * 0.8f + shing;
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] SawBuzz()
        {
            var b = Synth.New(0.26f);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += 130f / Synth.Rate;
                float am = 0.6f + 0.4f * Synth.SinC(30f * t);
                lp += Synth.LP(2500f) * (Synth.Noise() - lp);
                float env = Synth.AD(t, 0.01f, 0.12f);
                b[i] = Synth.Tanh((Synth.Wave(Synth.Saw, phase) * am * 0.7f + lp * 0.5f) * env * 2f);
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Ignite()
        {
            var b = Synth.New(0.6f);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                lp += Synth.LP(300f + 2000f * Synth.AD(t, 0.08f, 0.25f)) * (Synth.Noise() - lp);
                phase += (80f + 60f * Synth.Decay(t, 0.1f)) / Synth.Rate;
                float sub = Synth.SinC(phase) * Synth.AD(t, 0.03f, 0.12f) * 0.5f;
                b[i] = lp * Synth.AD(t, 0.06f, 0.22f) * 1.2f + sub;
            }
            Synth.Finish(b);
            return b;
        }

        // ---------- Combat ----------

        static float[] Hit()
        {
            var b = Synth.New(0.1f);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                lp += Synth.LP(1800f) * (Synth.Noise() - lp);
                phase += (90f + 220f * Synth.Decay(t, 0.02f)) / Synth.Rate;
                b[i] = lp * Synth.AD(t, 0.001f, 0.022f) * 0.9f + Synth.SinC(phase) * Synth.AD(t, 0.001f, 0.035f) * 0.8f;
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Kill()
        {
            var b = Synth.New(0.16f);
            float lp = 0f, phase = 0f, low = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += (160f + 600f * Synth.Decay(t, 0.018f)) / Synth.Rate;
                low += 85f / Synth.Rate;
                lp += Synth.LP(2600f) * (Synth.Noise() - lp);
                b[i] = Synth.SinC(phase) * Synth.AD(t, 0.001f, 0.045f) * 0.7f
                       + lp * Synth.AD(t, 0.001f, 0.03f) * 0.8f
                       + Synth.SinC(low) * Synth.AD(t, 0.002f, 0.06f) * 0.6f;
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Gore()
        {
            var b = Synth.New(0.45f);
            float lp = 0f, phase = 0f, chunk = 1f, chunkTarget = 1f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                lp += Synth.LP(900f + 600f * Synth.Decay(t, 0.1f)) * (Synth.Noise() - lp);
                // Lumpy, wet texture.
                if (i % 400 == 0) chunkTarget = 0.4f + 0.6f * Mathf.Abs(Synth.Noise());
                chunk += (chunkTarget - chunk) * 0.01f;
                phase += (55f + 80f * Synth.Decay(t, 0.05f)) / Synth.Rate;
                b[i] = lp * Synth.AD(t, 0.003f, 0.13f) * chunk * 1.6f + Synth.SinC(phase) * Synth.AD(t, 0.002f, 0.09f) * 0.8f;
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Explosion()
        {
            var b = Synth.New(1.2f);
            float lp = 0f, lp2 = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float cut = 150f + 3000f * Synth.Decay(t, 0.12f);
                lp += Synth.LP(cut) * (Synth.Noise() - lp);
                lp2 += Synth.LP(cut * 0.5f) * (lp - lp2);
                float rumble = lp2 * Synth.AD(t, 0.004f, 0.35f) * 2.5f;
                phase += (28f + 55f * Synth.Decay(t, 0.15f)) / Synth.Rate;
                float sub = Synth.SinC(phase) * Synth.AD(t, 0.004f, 0.28f);
                float crackle = Synth.Noise() > 0.993f ? Synth.Noise() * Synth.Decay(t, 0.3f) * 0.5f : 0f;
                b[i] = Synth.Tanh((rumble + sub * 1.1f + crackle) * 1.4f);
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Fuse()
        {
            const float len = 0.75f;
            var b = Synth.New(len);
            float phase = 0f, gatePhase = 0f, gate = 0f, lo = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float k = t / len;
                phase += (700f + 900f * k) / Synth.Rate;
                gatePhase += (6f + 22f * k) / Synth.Rate;
                float target = gatePhase - Mathf.Floor(gatePhase) < 0.45f ? 1f : 0f;
                gate += (target - gate) * 0.02f;
                float n = Synth.Noise();
                lo += Synth.LP(4000f) * (n - lo);
                b[i] = (Synth.Wave(Synth.Square, phase) * 0.35f * gate + (n - lo) * 0.12f) * (0.6f + 0.4f * k);
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] EnemyShoot()
        {
            var b = Synth.New(0.16f);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += (150f + 300f * Synth.Decay(t, 0.03f)) / Synth.Rate;
                lp += Synth.LP(1200f) * (Synth.Noise() - lp);
                b[i] = Synth.SinC(phase) * Synth.AD(t, 0.003f, 0.05f) + lp * Synth.AD(t, 0.001f, 0.025f) * 0.7f;
            }
            Synth.Finish(b);
            return b;
        }

        static readonly float[] BreakTimes = { 0f, 0.035f, 0.08f, 0.14f, 0.2f };
        static readonly float[] BreakAmps = { 1f, 0.7f, 0.8f, 0.5f, 0.3f };

        static float[] Break()
        {
            var b = Synth.New(0.32f);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float env = 0f;
                for (int k = 0; k < BreakTimes.Length; k++)
                    if (t >= BreakTimes[k]) env += BreakAmps[k] * Synth.Decay(t - BreakTimes[k], 0.018f);
                lp += Synth.LP(2800f) * (Synth.Noise() - lp);
                phase += (110f + 80f * Synth.Decay(t, 0.03f)) / Synth.Rate;
                b[i] = lp * env * 1.3f + Synth.SinC(phase) * Synth.AD(t, 0.001f, 0.05f) * 0.7f;
            }
            Synth.Finish(b);
            return b;
        }

        static readonly float[] MetalFreqs = { 587f, 1213f, 1979f, 2731f };
        static readonly float[] MetalAmps = { 1f, 0.6f, 0.45f, 0.3f };
        static readonly float[] MetalTaus = { 0.12f, 0.08f, 0.06f, 0.04f };

        static float[] Metal()
        {
            var b = Synth.New(0.35f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float s = Synth.Noise() * Synth.Decay(t, 0.004f) * 0.6f;
                for (int k = 0; k < MetalFreqs.Length; k++)
                    s += Synth.SinC(MetalFreqs[k] * t) * MetalAmps[k] * Synth.Decay(t, MetalTaus[k]);
                b[i] = s;
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        // ---------- Pickups ----------

        static float[] Gem()
        {
            var b = Synth.New(0.14f);
            float phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += 1568f / Synth.Rate;
                if (phase > 1f) phase -= 1f;
                b[i] = (Synth.SinC(phase) + 0.3f * Synth.SinC(phase * 2f)) * Synth.AD(t, 0.002f, 0.045f);
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Coin()
        {
            var b = Synth.New(0.3f);
            float phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += (t < 0.07f ? 988f : 1319f) / Synth.Rate;
                float wave = Synth.Wave(Synth.Square, phase) * 0.35f + Synth.Wave(Synth.Sine, phase) * 0.65f;
                float env = (t < 0.07f ? 1f : Synth.Decay(t - 0.07f, 0.09f)) * Mathf.Clamp01(t / 0.002f);
                b[i] = wave * env;
            }
            Synth.Finish(b, 0.75f);
            return b;
        }

        static float[] Heal()
        {
            const float len = 0.45f;
            var b = Synth.New(len);
            float phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += Mathf.Lerp(420f, 980f, t / len) / Synth.Rate;
                if (phase > 1f) phase -= 1f;
                float trem = 0.75f + 0.25f * Synth.SinC(18f * t);
                float env = Mathf.Sin(Mathf.PI * t / len);
                b[i] = (Synth.SinC(phase) + 0.25f * Synth.Wave(Synth.Triangle, phase * 2f)) * env * trem;
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Magnet()
        {
            const float len = 0.65f;
            var b = Synth.New(len);
            float phase = 0f, lo = 0f, hi = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float k = t / len;
                phase += 150f * Mathf.Pow(10f, k) / Synth.Rate;
                float n = Synth.Noise();
                float cut = 300f + 3000f * k;
                lo += Synth.LP(cut * 0.5f) * (n - lo);
                hi += Synth.LP(cut * 2f) * (n - hi);
                float env = Mathf.Sin(Mathf.PI * k);
                b[i] = Synth.SinC(phase) * 0.5f * env + (hi - lo) * env * 0.6f;
            }
            Synth.Finish(b, 0.85f);
            return b;
        }

        static float[] Chest()
        {
            var b = Synth.New(1.0f);
            int[] notes = { 84, 88, 91, 96 };
            for (int k = 0; k < notes.Length; k++)
            {
                Synth.Tone(b, k * 0.08f, 0.6f, Synth.Note(notes[k]), Synth.Triangle, 0.5f, 0.25f);
                Synth.Tone(b, k * 0.08f, 0.6f, Synth.Note(notes[k]), Synth.Square, 0.12f, 0.2f);
            }
            int[] sparkle = { 0, 7, 4, 12, 7, 16, 12, 19, 16, 24 };
            for (int k = 0; k < sparkle.Length; k++)
                Synth.Tone(b, 0.3f + k * 0.055f, 0.15f, Synth.Note(96 + sparkle[k]), Synth.Sine, 0.18f, 0.04f);
            Synth.Finish(b, 0.85f);
            return b;
        }

        // ---------- Player / UI ----------

        static float[] Hurt()
        {
            const float len = 0.28f;
            var b = Synth.New(len);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += Mathf.Lerp(240f, 110f, t / len) / Synth.Rate;
                lp += Synth.LP(1400f) * (Synth.Wave(Synth.Saw, phase) + Synth.Noise() * 0.3f - lp);
                b[i] = Synth.Tanh(lp * Synth.AD(t, 0.005f, 0.08f) * 2.5f);
            }
            Synth.Finish(b);
            return b;
        }

        static float[] LevelUp()
        {
            var b = Synth.New(0.85f);
            int[] notes = { 72, 76, 79, 84 };
            for (int k = 0; k < notes.Length; k++)
            {
                bool last = k == notes.Length - 1;
                float len = last ? 0.6f : 0.2f, tau = last ? 0.3f : 0.12f;
                Synth.Tone(b, k * 0.08f, len, Synth.Note(notes[k]), Synth.Square, 0.2f, tau);
                Synth.Tone(b, k * 0.08f, len, Synth.Note(notes[k]), Synth.Triangle, 0.4f, tau, last ? 0.006f : 0f);
            }
            Synth.Finish(b, 0.85f);
            return b;
        }

        static float[] Evolution()
        {
            var b = Synth.New(1.6f);
            int[] arp = { 72, 79, 84, 88, 91, 96 };
            for (int k = 0; k < arp.Length; k++)
                Synth.Tone(b, k * 0.06f, 0.4f, Synth.Note(arp[k]), Synth.Triangle, 0.35f, 0.15f);
            int[] chord = { 72, 76, 79, 84 };
            foreach (int n in chord)
            {
                Synth.Tone(b, 0.4f, 1.15f, Synth.Note(n), Synth.Triangle, 0.25f, 0.6f, 0.006f);
                Synth.Tone(b, 0.4f, 1.15f, Synth.Note(n), Synth.Square, 0.06f, 0.5f, 0.006f);
            }
            float lo = 0f;
            for (int i = Mathf.RoundToInt(0.4f * Synth.Rate); i < b.Length; i++)
            {
                float t = Synth.T(i) - 0.4f;
                float n = Synth.Noise();
                lo += Synth.LP(5000f) * (n - lo);
                b[i] += (n - lo) * Synth.AD(t, 0.05f, 0.4f) * 0.12f;
            }
            Synth.Finish(b, 0.9f);
            return b;
        }

        static float[] Pick()
        {
            var b = Synth.New(0.16f);
            Synth.Tone(b, 0f, 0.07f, Synth.Note(79), Synth.Triangle, 0.5f, 0.04f);
            Synth.Tone(b, 0.05f, 0.11f, Synth.Note(86), Synth.Triangle, 0.5f, 0.05f);
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Click()
        {
            var b = Synth.New(0.05f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                b[i] = Synth.SinC(1500f * t) * Synth.AD(t, 0.0005f, 0.008f) + Synth.Noise() * Synth.Decay(t, 0.002f) * 0.3f;
            }
            Synth.Finish(b, 0.7f);
            return b;
        }

        static float[] GameOver()
        {
            var b = Synth.New(1.6f);
            Synth.Tone(b, 0f, 0.3f, Synth.Note(67), Synth.Saw, 0.4f, 0.15f);
            Synth.Tone(b, 0.25f, 0.3f, Synth.Note(64), Synth.Saw, 0.4f, 0.15f);
            Synth.Tone(b, 0.5f, 1.1f, Synth.Note(60), Synth.Saw, 0.45f, 0.5f, 0.01f);
            Synth.Tone(b, 0.5f, 1.1f, Synth.Note(48), Synth.Saw, 0.35f, 0.6f);
            Synth.LowPass(b, 1200f);
            Synth.Finish(b, 0.9f);
            return b;
        }

        // ---------- Events ----------

        static float[] Horde()
        {
            var b = Synth.New(1.5f);
            float p1 = 0f, p2 = 0f, lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float env = Mathf.Clamp01(t / 0.2f) * (t < 1f ? 1f : Mathf.Max(0f, 1f - (t - 1f) / 0.5f));
                float vib = 1f + 0.012f * Synth.SinC(5f * t);
                p1 += 98f * vib / Synth.Rate;
                p2 += 147f * vib / Synth.Rate;
                float x = Synth.Wave(Synth.Saw, p1) + Synth.Wave(Synth.Saw, p2) * 0.7f;
                lp += Synth.LP(900f + 500f * env) * (x - lp);
                float drum = Synth.SinC(55f * t) * Synth.Decay(t, 0.12f);
                if (t >= 0.5f) drum += Synth.SinC(55f * (t - 0.5f)) * Synth.Decay(t - 0.5f, 0.12f);
                b[i] = Synth.Tanh(lp * env * 1.2f + drum * 0.9f);
            }
            Synth.Finish(b, 0.9f);
            return b;
        }

        static float[] BossRoar()
        {
            var b = Synth.New(1.7f);
            float p1 = 0f, p2 = 0f, lp = 0f, growl = 0f, wob = 0f, wobTarget = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float env = Mathf.Clamp01(t / 0.25f) * (t < 1.1f ? 1f : Mathf.Max(0f, 1f - (t - 1.1f) / 0.6f));
                if (i % 300 == 0) wobTarget = Synth.Noise();
                wob += (wobTarget - wob) * 0.003f;
                float f = (70f + 25f * Synth.SinC(0.9f * t)) * (1f + 0.08f * wob) * (t > 1.1f ? 1f - (t - 1.1f) * 0.3f : 1f);
                p1 += f / Synth.Rate;
                p2 += f * 1.5f / Synth.Rate;
                growl += Synth.LP(600f) * (Synth.Noise() - growl);
                float x = (Synth.Wave(Synth.Saw, p1) + Synth.Wave(Synth.Saw, p2) * 0.4f) * 0.8f + growl * 1.5f;
                lp += Synth.LP(1500f) * (x - lp);
                b[i] = Synth.Tanh(lp * env * 3f);
            }
            Synth.Finish(b, 0.95f);
            return b;
        }

        static float[] Alert()
        {
            var b = Synth.New(0.6f);
            Synth.Tone(b, 0f, 0.25f, Synth.Note(81), Synth.Triangle, 0.5f, 0.1f);
            Synth.Tone(b, 0f, 0.25f, Synth.Note(93), Synth.Sine, 0.2f, 0.08f);
            Synth.Tone(b, 0.18f, 0.4f, Synth.Note(88), Synth.Triangle, 0.5f, 0.15f);
            Synth.Tone(b, 0.18f, 0.4f, Synth.Note(100), Synth.Sine, 0.2f, 0.1f);
            Synth.Finish(b, 0.85f);
            return b;
        }

        static readonly int[] CalloutChord = { 45, 52, 57, 64 };

        static float[] Callout()
        {
            var b = Synth.New(0.45f);
            var phases = new float[CalloutChord.Length];
            float lp = 0f, kick = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float x = 0f;
                for (int k = 0; k < phases.Length; k++)
                {
                    phases[k] += Synth.Note(CalloutChord[k]) / Synth.Rate;
                    x += Synth.Wave(Synth.Saw, phases[k]) * 0.4f;
                }
                lp += Synth.LP(600f + 5000f * Synth.Decay(t, 0.06f)) * (x - lp);
                kick += (40f + 100f * Synth.Decay(t, 0.03f)) / Synth.Rate;
                float snare = Synth.Noise() * Synth.AD(t, 0.001f, 0.04f) * 0.4f;
                b[i] = Synth.Tanh(lp * Synth.AD(t, 0.002f, 0.14f) * 1.5f + Synth.SinC(kick) * Synth.AD(t, 0.001f, 0.1f) + snare);
            }
            Synth.Finish(b, 0.9f);
            return b;
        }

        static float[] Frenzy()
        {
            var b = Synth.New(1.0f);
            float phase = 0f, lp = 0f, lo = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float k = Mathf.Clamp01(t / 0.6f);
                phase += 110f * Mathf.Pow(8f, k) / Synth.Rate;
                lp += Synth.LP(400f + 6000f * k) * (Synth.Wave(Synth.Saw, phase) - lp);
                float env = t < 0.6f ? 0.4f + 0.6f * k : Synth.Decay(t - 0.6f, 0.15f);
                float n = Synth.Noise();
                lo += Synth.LP(3000f) * (n - lo);
                float riser = t < 0.6f ? (n - lo) * 0.15f * k : 0f;
                b[i] = lp * env * 0.6f + riser;
            }
            Synth.Tone(b, 0.6f, 0.4f, Synth.Note(69), Synth.Triangle, 0.3f, 0.25f);
            Synth.Tone(b, 0.6f, 0.4f, Synth.Note(76), Synth.Triangle, 0.3f, 0.25f);
            Synth.Tone(b, 0.6f, 0.4f, Synth.Note(81), Synth.Square, 0.15f, 0.25f);
            Synth.Finish(b, 0.9f);
            return b;
        }

        static float[] Success()
        {
            var b = Synth.New(0.95f);
            int[] notes = { 79, 84, 88, 91 };
            for (int k = 0; k < notes.Length; k++)
            {
                bool last = k == notes.Length - 1;
                float len = last ? 0.6f : 0.1f, tau = last ? 0.3f : 0.08f;
                Synth.Tone(b, k * 0.09f, len, Synth.Note(notes[k]), Synth.Square, 0.2f, tau);
                Synth.Tone(b, k * 0.09f, len, Synth.Note(notes[k]), Synth.Triangle, 0.35f, tau, last ? 0.006f : 0f);
            }
            Synth.Finish(b, 0.85f);
            return b;
        }

        static float[] Fail()
        {
            var b = Synth.New(0.8f);
            float phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float f, env;
                if (t < 0.3f)
                {
                    f = Synth.Note(64) * (1f - 0.06f * t / 0.3f);
                    env = Synth.AD(t, 0.01f, 0.2f) * Mathf.Clamp01((0.3f - t) / 0.02f);
                }
                else if (t >= 0.32f)
                {
                    float u = t - 0.32f;
                    f = Synth.Note(60) * (1f - 0.1f * u / 0.48f);
                    env = Synth.AD(u, 0.01f, 0.3f);
                }
                else
                {
                    f = Synth.Note(60);
                    env = 0f;
                }
                phase += f / Synth.Rate;
                b[i] = Synth.Wave(Synth.Saw, phase) * env;
            }
            Synth.LowPass(b, 1500f);
            Synth.Finish(b, 0.8f);
            return b;
        }

        static float[] Thud()
        {
            var b = Synth.New(0.35f);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                phase += (38f + 70f * Synth.Decay(t, 0.04f)) / Synth.Rate;
                lp += Synth.LP(500f) * (Synth.Noise() - lp);
                b[i] = Synth.SinC(phase) * Synth.AD(t, 0.002f, 0.11f) + lp * Synth.AD(t, 0.002f, 0.06f) * 1.5f;
            }
            Synth.Finish(b);
            return b;
        }

        static float[] Squeak()
        {
            var b = Synth.New(0.36f);
            float phase = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                int chirp = Mathf.Min(2, Mathf.FloorToInt(t / 0.11f));
                float tc = t - chirp * 0.11f;
                float f = 2400f + chirp * 300f + 900f * Synth.SinC(40f * tc) + 600f * tc / 0.06f;
                phase += f / Synth.Rate;
                if (phase > 1f) phase -= 1f;
                b[i] = tc < 0.07f ? Synth.SinC(phase) * Synth.AD(tc, 0.003f, 0.025f) : 0f;
            }
            Synth.Finish(b, 0.75f);
            return b;
        }

        /// <summary>Something heavy falling from the sky: filtered noise sweeping down.</summary>
        static float[] Whoosh()
        {
            var b = Synth.New(0.55f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float k = t / 0.55f;
                float n = Synth.Noise();
                lp += Synth.LP(3500f * (1f - k) + 300f) * (n - lp);
                lp2 += Synth.LP(200f) * (lp - lp2);
                float env = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((0.55f - t) / 0.08f);
                b[i] = (lp - lp2) * env;
            }
            Synth.Finish(b, 0.8f);
            return b;
        }

        /// <summary>Deep church bell: inharmonic partials with long decays.</summary>
        static float[] Bell()
        {
            var b = Synth.New(1.4f);
            float f = Synth.Note(45);
            float[] ratios = { 0.5f, 1f, 1.19f, 1.56f, 2f, 2.74f, 3.76f };
            float[] amps = { 0.5f, 0.6f, 0.35f, 0.3f, 0.25f, 0.15f, 0.08f };
            float[] taus = { 0.9f, 0.7f, 0.45f, 0.35f, 0.3f, 0.2f, 0.12f };
            for (int p = 0; p < ratios.Length; p++)
                Synth.Tone(b, 0f, 1.4f, f * ratios[p], Synth.Sine, amps[p], taus[p]);
            for (int i = 0; i < 400 && i < b.Length; i++) b[i] += Synth.Noise() * 0.3f * (1f - i / 400f);
            Synth.Finish(b, 0.85f);
            return b;
        }

        /// <summary>Relic found: a sparkling arpeggio over a warm pad.</summary>
        static float[] Relic()
        {
            var b = Synth.New(1.3f);
            int[] notes = { 76, 81, 85, 88, 93 };
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * 0.07f;
                Synth.Tone(b, start, 1.2f - start, Synth.Note(notes[k]), Synth.Sine, 0.3f, 0.35f, 0.004f);
                Synth.Tone(b, start, 0.3f, Synth.Note(notes[k] + 12), Synth.Triangle, 0.08f, 0.06f);
            }
            Synth.Tone(b, 0f, 1.3f, Synth.Note(57), Synth.Triangle, 0.3f, 0.6f);
            Synth.Tone(b, 0f, 1.3f, Synth.Note(64), Synth.Triangle, 0.2f, 0.6f);
            Synth.Finish(b, 0.85f);
            return b;
        }

        /// <summary>Pact sealed: dark detuned chord with a low boom.</summary>
        static float[] Pact()
        {
            var b = Synth.New(1.5f);
            int[] chord = { 38, 45, 49, 50 };
            var phases = new float[chord.Length * 2];
            float lp = 0f, boom = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = Synth.T(i);
                float x = 0f;
                for (int k = 0; k < chord.Length; k++)
                {
                    float f = Synth.Note(chord[k]);
                    phases[k * 2] += f * 1.004f / Synth.Rate;
                    phases[k * 2 + 1] += f * 0.996f / Synth.Rate;
                    x += (Synth.Wave(Synth.Saw, phases[k * 2]) + Synth.Wave(Synth.Saw, phases[k * 2 + 1])) * 0.2f;
                }
                lp += Synth.LP(300f + 1800f * Synth.Decay(t, 0.3f)) * (x - lp);
                boom += (35f + 60f * Synth.Decay(t, 0.08f)) / Synth.Rate;
                float env = Synth.AD(t, 0.02f, 0.5f);
                b[i] = Synth.Tanh(lp * env * 1.4f + Synth.SinC(boom) * Synth.AD(t, 0.002f, 0.25f) * 0.9f);
            }
            Synth.Finish(b, 0.9f);
            return b;
        }
    }
}
