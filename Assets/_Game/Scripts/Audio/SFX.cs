using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Survivor
{
    /// <summary>Every sound in the game. All of them are synthesized in code (see SfxSynth.cs).</summary>
    public enum SfxId
    {
        // Weapons: played by the weapon scripts.
        Gunshot, Shotgun, Zap, Throw, Slash, Saw, Ignite,
        // Combat: played automatically by Enemy / Combat.
        Hit, Kill, Gore, Explosion, Fuse, EnemyShoot, Break, Metal,
        // Pickups: played automatically by XPGem / Pickup.
        Gem, Coin, Heal, Magnet, Chest,
        // Player and UI: played automatically by GameHUD / UIKit buttons.
        Hurt, LevelUp, Evolution, Pick, Click, GameOver,
        // Events and announcements.
        Horde, BossRoar, Alert, Callout, Frenzy, Success, Fail, Thud, Squeak,
        // Run spice (World/WorldEvents, Run/*): meteors falling, relic bell, relic pickup, pact sealed.
        // Keep new ids at the END: enemy assets store sounds by number.
        Whoosh, Bell, Relic, Pact,
    }

    /// <summary>
    /// Procedural sound effects, no audio files needed: clips are synthesized at startup.
    ///
    /// HOW TO USE, from any script:
    ///     SFX.Play(SfxId.Gunshot);            // default volume and pitch
    ///     SFX.Play(SfxId.Shotgun, 0.8f);      // a bit quieter
    ///     SFX.Play(SfxId.Zap, 1f, 1.3f);      // higher pitch (evolved weapon, etc.)
    ///
    /// Call it as often as you like (every bullet, every hit): each sound has a cooldown and a voice
    /// limit, so 300 enemies never turn into noise soup. A small random pitch variation is added.
    ///
    /// Already played automatically, DON'T play them again:
    ///   Hit / Kill / Gore / EnemyShoot / Fuse   (Enemy)
    ///   Explosion                               (GameFX.Explosion: every explosion is covered)
    ///   Gem / Coin / Heal / Magnet / Chest      (pickups)
    ///   Hurt / LevelUp / Evolution / Pick / Click / GameOver / Callout (GameHUD, UIKit.Button)
    ///   Horde / BossRoar / Alert / Frenzy / Success / Fail / Thud / Squeak (RunDirector, ArenaEvents)
    ///   Whoosh / Bell / Relic / Pact            (WorldEvents, RunModifiers: meteors, relics, pacts)
    /// Weapons play their own: Gunshot, Shotgun, Zap, Throw, Slash, Saw, Ignite.
    ///
    /// The SFX object creates itself before the first scene loads and survives scene reloads.
    /// </summary>
    public class SFX : MonoBehaviour
    {
        const int VoiceCount = 24;
        const string MutedKey = "sfx_muted";

        static SFX instance;

        /// <summary>Master volume, 0..1.</summary>
        public static float Volume = 1f;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                muted = value;
                if (value && instance) instance.StopAll();
            }
        }

        static bool muted;
        static float lastGem;
        static int gemStreak;

        /// <summary>How a sound behaves in the mix.</summary>
        struct Def
        {
            public float volume, cooldown, jitter;
            public int maxVoices, priority;

            public Def(float volume, float cooldown, int maxVoices, float jitter, int priority)
            {
                this.volume = volume;
                this.cooldown = cooldown;
                this.maxVoices = maxVoices;
                this.jitter = jitter;
                this.priority = priority;
            }
        }

        AudioClip[] clips;
        Def[] defs;
        float[] lastPlay;
        AudioSource[] sources;
        int[] voiceId, voicePriority;
        float[] voiceStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (instance) return;
            var go = new GameObject("SFX");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SFX>();
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;

            int count = Enum.GetValues(typeof(SfxId)).Length;
            clips = new AudioClip[count];
            defs = new Def[count];
            lastPlay = new float[count];
            for (int i = 0; i < count; i++)
            {
                var id = (SfxId)i;
                float[] data = SfxBank.Build(id);
                var clip = AudioClip.Create("sfx_" + id, data.Length, 1, Synth.Rate, false);
                clip.SetData(data, 0);
                clips[i] = clip;
                defs[i] = DefFor(id);
                lastPlay[i] = -99f;
            }

            sources = new AudioSource[VoiceCount];
            voiceId = new int[VoiceCount];
            voicePriority = new int[VoiceCount];
            voiceStart = new float[VoiceCount];
            for (int v = 0; v < VoiceCount; v++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;
                s.dopplerLevel = 0f;
                s.priority = 64;
                sources[v] = s;
                voiceId[v] = -1;
            }
        }

        /// <summary>volume, cooldown (s), max simultaneous voices, random pitch range, priority (higher steals lower).</summary>
        static Def DefFor(SfxId id) => id switch
        {
            SfxId.Gunshot => new Def(0.45f, 0.05f, 3, 0.08f, 2),
            SfxId.Shotgun => new Def(0.6f, 0.08f, 2, 0.06f, 3),
            SfxId.Zap => new Def(0.4f, 0.06f, 3, 0.1f, 2),
            SfxId.Throw => new Def(0.4f, 0.08f, 2, 0.1f, 1),
            SfxId.Slash => new Def(0.35f, 0.07f, 2, 0.1f, 1),
            SfxId.Saw => new Def(0.3f, 0.1f, 2, 0.08f, 1),
            SfxId.Ignite => new Def(0.5f, 0.1f, 2, 0.08f, 2),
            SfxId.Hit => new Def(0.35f, 0.045f, 3, 0.15f, 0),
            SfxId.Kill => new Def(0.45f, 0.035f, 4, 0.06f, 1),
            SfxId.Gore => new Def(0.55f, 0.08f, 2, 0.1f, 2),
            SfxId.Explosion => new Def(0.75f, 0.07f, 4, 0.1f, 4),
            SfxId.Fuse => new Def(0.45f, 0.2f, 2, 0.05f, 3),
            SfxId.EnemyShoot => new Def(0.3f, 0.15f, 2, 0.1f, 1),
            SfxId.Break => new Def(0.6f, 0.06f, 2, 0.1f, 3),
            SfxId.Metal => new Def(0.4f, 0.06f, 2, 0.1f, 1),
            SfxId.Gem => new Def(0.3f, 0.03f, 3, 0f, 1),
            SfxId.Coin => new Def(0.4f, 0.05f, 2, 0.03f, 2),
            SfxId.Heal => new Def(0.6f, 0.2f, 1, 0f, 4),
            SfxId.Magnet => new Def(0.7f, 0.3f, 1, 0f, 4),
            SfxId.Chest => new Def(0.8f, 0.3f, 1, 0f, 5),
            SfxId.Hurt => new Def(0.6f, 0.15f, 1, 0.08f, 4),
            SfxId.LevelUp => new Def(0.7f, 0.3f, 1, 0f, 5),
            SfxId.Evolution => new Def(0.9f, 0.5f, 1, 0f, 6),
            SfxId.Pick => new Def(0.6f, 0.05f, 1, 0f, 5),
            SfxId.Click => new Def(0.5f, 0.04f, 2, 0f, 5),
            SfxId.GameOver => new Def(0.9f, 0.5f, 1, 0f, 6),
            SfxId.Horde => new Def(0.8f, 1f, 1, 0f, 5),
            SfxId.BossRoar => new Def(0.9f, 0.8f, 1, 0.05f, 6),
            SfxId.Alert => new Def(0.7f, 0.3f, 1, 0f, 5),
            SfxId.Callout => new Def(0.65f, 0.15f, 1, 0.04f, 4),
            SfxId.Frenzy => new Def(0.85f, 0.5f, 1, 0f, 6),
            SfxId.Success => new Def(0.85f, 0.5f, 1, 0f, 6),
            SfxId.Fail => new Def(0.7f, 0.5f, 1, 0f, 5),
            SfxId.Thud => new Def(0.7f, 0.1f, 2, 0.08f, 3),
            SfxId.Squeak => new Def(0.5f, 0.3f, 1, 0.12f, 3),
            SfxId.Whoosh => new Def(0.45f, 0.12f, 2, 0.1f, 3),
            SfxId.Bell => new Def(0.55f, 0.4f, 1, 0.02f, 3),
            SfxId.Relic => new Def(0.85f, 0.4f, 1, 0f, 6),
            SfxId.Pact => new Def(0.9f, 0.6f, 1, 0f, 6),
            _ => new Def(0.5f, 0.05f, 2, 0.05f, 1),
        };

        // ---------- Public API ----------

        /// <summary>Plays a sound. Cheap and safe to spam: cooldowns and voice limits do the filtering.</summary>
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (muted || !instance || volume <= 0f) return;
            instance.PlayVoice((int)id, volume, pitch);
        }

        /// <summary>XP gem "pling": quick consecutive pickups climb the scale (Vampire Survivors style).</summary>
        public static void PlayGem()
        {
            float now = Time.unscaledTime;
            gemStreak = now - lastGem < 0.4f ? Mathf.Min(gemStreak + 1, 18) : 0;
            lastGem = now;
            Play(SfxId.Gem, 1f, Mathf.Pow(2f, gemStreak / 12f));
        }

        // ---------- Voices ----------

        void PlayVoice(int id, float volume, float pitch)
        {
            if (id < 0 || id >= clips.Length) return;
            float now = Time.unscaledTime;
            var def = defs[id];
            if (now - lastPlay[id] < def.cooldown) return;

            int same = 0, oldestSame = -1, free = -1, steal = -1;
            float oldestSameTime = float.MaxValue, stealTime = float.MaxValue;
            for (int v = 0; v < VoiceCount; v++)
            {
                if (!sources[v].isPlaying)
                {
                    if (free < 0) free = v;
                    continue;
                }
                if (voiceId[v] == id)
                {
                    same++;
                    if (voiceStart[v] < oldestSameTime)
                    {
                        oldestSameTime = voiceStart[v];
                        oldestSame = v;
                    }
                }
                if (voicePriority[v] <= def.priority && voiceStart[v] < stealTime)
                {
                    stealTime = voiceStart[v];
                    steal = v;
                }
            }

            int pick = same >= def.maxVoices ? oldestSame : free >= 0 ? free : steal;
            if (pick < 0) return;

            lastPlay[id] = now;
            var s = sources[pick];
            s.clip = clips[id];
            s.volume = Mathf.Clamp01(def.volume * volume * Volume);
            s.pitch = Mathf.Clamp(pitch * (1f + Random.Range(-def.jitter, def.jitter)), 0.3f, 3f);
            s.Play();
            voiceId[pick] = id;
            voicePriority[pick] = def.priority;
            voiceStart[pick] = now;
        }

        void StopAll()
        {
            if (sources == null) return;
            foreach (var s in sources) if (s) s.Stop();
        }
    }
}
