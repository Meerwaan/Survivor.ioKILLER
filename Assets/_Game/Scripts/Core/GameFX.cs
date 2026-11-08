using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor
{
    /// <summary>
    /// All the cheap juice in one place: blood, crust chunks that bounce on the floor, sparks, embers, smoke,
    /// flames, shockwave rings, camera-facing flares, lightning, danger telegraphs, ground stains that fade out,
    /// and the soft glow under every éclat. Everything is pooled (ring buffers and particle systems with caps),
    /// so it costs nothing to call often. HDR colors are capped (~1.5) so bloom glows instead of blowing out.
    /// </summary>
    public class GameFX : MonoBehaviour
    {
        public static GameFX Instance { get; private set; }

        // ---------- Palette (Univers 9.4): one color, one meaning ----------
        /// <summary>#C26A1C sap, éclats, wounds. Never blinks.</summary>
        public static readonly Color Amber = new(0.761f, 0.416f, 0.11f);
        /// <summary>#E58E2B level-up wave, Ambré telegraphs, elites, bosses.</summary>
        public static readonly Color AmberBright = new(0.898f, 0.557f, 0.169f);
        /// <summary>#8C4A12 crust of the infected.</summary>
        public static readonly Color AmberDark = new(0.549f, 0.29f, 0.071f);
        /// <summary>#8A6A3A dried crust (matte, never glows).</summary>
        public static readonly Color DriedCrust = new(0.541f, 0.416f, 0.227f);
        /// <summary>#221006 black veins, the ichor.</summary>
        public static readonly Color Veins = new(0.133f, 0.063f, 0.024f);
        /// <summary>#FFC43D to #FF6A2A fire, always with smoke.</summary>
        public static readonly Color FireYellow = new(1f, 0.769f, 0.239f), FireOrange = new(1f, 0.416f, 0.165f);
        /// <summary>#3A3A3A the smoke that goes with fire.</summary>
        public static readonly Color SmokeGrey = new(0.227f, 0.227f, 0.227f);
        /// <summary>#2CB1C4 REGEN (never a weapon effect).</summary>
        public static readonly Color RegenCyan = new(0.173f, 0.694f, 0.769f);
        /// <summary>#9ADCFF Tesla, overload.</summary>
        public static readonly Color Electric = new(0.604f, 0.863f, 1f);
        /// <summary>#A6D23C gas, poison.</summary>
        public static readonly Color Toxic = new(0.651f, 0.824f, 0.235f);
        /// <summary>#7E4FC7 the cult only.</summary>
        public static readonly Color CultViolet = new(0.494f, 0.31f, 0.78f);
        /// <summary>#B8322A danger, human blood (small amounts).</summary>
        public static readonly Color Danger = new(0.722f, 0.196f, 0.165f);
        /// <summary>#5FD08A heal, safe air.</summary>
        public static readonly Color Heal = new(0.373f, 0.816f, 0.541f);
        /// <summary>#F2F2EE the Cordon's artillery aim circles.</summary>
        public static readonly Color CordonWhite = new(0.949f, 0.949f, 0.933f);

        public Material bloodParticleMaterial;
        public GameObject bloodSplatPrefab;
        [Tooltip("Ground stains alive at once (they fade out after a few seconds).")]
        public int maxSplats = 150;

        [Header("Juice materials (created by the prototype builder)")]
        [Tooltip("Particles/Lit, white: crust chunks of enemies.")]
        public Material gibMaterial;
        [Tooltip("Particles/Unlit additive with a soft dot: sparks, embers, éclat glow.")]
        public Material glowParticleMaterial;
        [Tooltip("Unlit additive with a thin ring texture: shockwaves, telegraph outlines, event circles.")]
        public Material ringMaterial;
        [Tooltip("Unlit additive, plain: event props (beams, relic gems).")]
        public Material glowMaterial;
        [Tooltip("Particles/Unlit additive with a soft beam profile: lightning, trails.")]
        public Material lineMaterial;

        [Header("Soft FX (created by the prototype builder; generated at runtime if missing)")]
        [Tooltip("Unlit additive, soft flare with a hot core: explosion cores and muzzle flashes (camera-facing).")]
        public Material flareMaterial;
        [Tooltip("Unlit additive, very thin band: big shockwaves (over 4 m).")]
        public Material waveMaterial;
        [Tooltip("Unlit additive, soft even disc: the fill of danger telegraphs.")]
        public Material fillMaterial;
        [Tooltip("Particles/Unlit alpha blended, soft puff: smoke, dust, gas.")]
        public Material smokeMaterial;
        [Tooltip("Particles/Unlit additive, soft puff: flames.")]
        public Material flameMaterial;
        [Tooltip("Particles/Unlit alpha blended, irregular stain: ground splats.")]
        public Material splatMaterial;

        const int RingCount = 48, FlashCount = 32, BoltCount = 24, BoltPoints = 9, WarnCount = 32, HaloMax = 300;
        /// <summary>HDR caps: bloom threshold is ~1 with a +0.55 exposure, anything brighter turns into a white disc.</summary>
        const float RingHdr = 1.5f, WaveHdr = 1.25f, FlashHdr = 1.5f, WarnHdr = 1.35f;
        /// <summary>Rings wider than this are drawn as a thin, dimmer wave (the screen is only ~11 m wide).</summary>
        const float WaveRadius = 4f;
        const float MaxFlashRadius = 2.4f;

        ParticleSystem blood, gibs, sparks, embers, smoke, flames, splatSystem, halos;
        Transform[] splats;
        int nextSplat;
        ParticleSystem.Particle[] haloBuffer;
        readonly Vector3[] boltScratch = new Vector3[BoltPoints];
        Transform cam;

        class Shape
        {
            public Transform t;
            public Renderer r;
            public float age, duration, size, alpha = 1f;
            public Color color;
            public bool live;
        }

        class Bolt
        {
            public LineRenderer line, core;
            public float age, duration, width;
            public Color color;
            public bool live;
        }

        /// <summary>Danger telegraph: a thin fixed outline and a soft disc that fills it up until impact.</summary>
        class Warn
        {
            public Shape outer, inner;
            public float age, duration, radius;
            public Color color;
            public bool live;
        }

        Shape[] rings, flashes;
        Bolt[] bolts;
        Warn[] warns;
        int nextRing, nextFlash, nextBolt, nextWarn;
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        /// <summary>True when flames are drawn as particles (the flamethrower then skips its old puff prefab).</summary>
        public bool HasFlames => flames;

        void Awake()
        {
            Instance = this;
            block = new MaterialPropertyBlock();
            EnsureMaterials();

            blood = CreateSystem("Blood", bloodParticleMaterial, 2000, 2.5f, new Color(0.4f, 0f, 0f), new Color(0.8f, 0.05f, 0.02f));
            var bm = blood.main;
            bm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6.5f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            var bSize = blood.sizeOverLifetime;
            bSize.enabled = true;
            bSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
            var br = blood.GetComponent<ParticleSystemRenderer>();
            br.renderMode = ParticleSystemRenderMode.Stretch; // droplets read as streaks, not squares
            br.velocityScale = 0.05f;
            br.lengthScale = 1.4f;

            gibs = CreateGibs();

            sparks = CreateSystem("Sparks", glowParticleMaterial, 1500, 1.2f, Color.white, Color.white);
            var sm = sparks.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            var sizeOverLife = sparks.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.04f;
            sr.lengthScale = 1f;

            embers = CreateSystem("Embers", glowParticleMaterial, 1500, -0.35f, Color.white, Color.white);
            var em = embers.main;
            em.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            em.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            var emSize = embers.sizeOverLifetime;
            emSize.enabled = true;
            emSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            if (smokeMaterial) smoke = CreateSmoke();
            if (flameMaterial) flames = CreateFlames();
            if (splatMaterial) splatSystem = CreateSplats();
            if (glowParticleMaterial) halos = CreateHalos();

            rings = new Shape[RingCount];
            for (int i = 0; i < RingCount; i++) rings[i] = CreateShape("Ring", ringMaterial, Quaternion.Euler(90f, 0f, 0f));
            flashes = new Shape[FlashCount];
            for (int i = 0; i < FlashCount; i++) flashes[i] = CreateShape("Flash", flareMaterial ? flareMaterial : glowMaterial, Quaternion.identity);
            bolts = new Bolt[BoltCount];
            for (int i = 0; i < BoltCount; i++) bolts[i] = CreateBolt();
            warns = new Warn[WarnCount];
            for (int i = 0; i < WarnCount; i++)
                warns[i] = new Warn
                {
                    outer = CreateShape("Warn", ringMaterial, Quaternion.Euler(90f, 0f, 0f)),
                    inner = CreateShape("Warn Fill", fillMaterial ? fillMaterial : ringMaterial, Quaternion.Euler(90f, 0f, 0f)),
                };
        }

        // ---------- Materials ----------

        /// <summary>The builder makes these as assets; if the scene wasn't rebuilt yet, make runtime copies so nothing is missing.</summary>
        void EnsureMaterials()
        {
            const int T = (int)RenderQueue.Transparent;
            if (!flareMaterial) flareMaterial = RuntimeMat(glowMaterial, FXTextures.Flare(), true, T);
            if (!waveMaterial) waveMaterial = RuntimeMat(ringMaterial, FXTextures.Wave(), true, T);
            if (!fillMaterial) fillMaterial = RuntimeMat(ringMaterial, FXTextures.Fill(), true, T);
            if (!smokeMaterial || !flameMaterial)
            {
                var puff = FXTextures.Puff();
                if (!smokeMaterial) smokeMaterial = RuntimeMat(glowParticleMaterial, puff, false, T - 10);
                if (!flameMaterial) flameMaterial = RuntimeMat(glowParticleMaterial, puff, true, T, new Color(1.25f, 1.25f, 1.25f));
            }
            if (!splatMaterial) splatMaterial = RuntimeMat(glowParticleMaterial, FXTextures.Splat(), false, T - 50);
            // Old opaque blood material (no texture): use the soft dot, alpha blended.
            if (bloodParticleMaterial && bloodParticleMaterial.HasProperty(BaseMap) && !bloodParticleMaterial.GetTexture(BaseMap))
                bloodParticleMaterial = RuntimeMat(glowParticleMaterial, null, false, T) ?? bloodParticleMaterial;
        }

        static Material RuntimeMat(Material template, Texture texture, bool additive, int queue, Color? color = null)
        {
            if (!template) return null;
            var m = new Material(template) { name = template.name + " (runtime)" };
            if (texture) m.SetTexture(BaseMap, texture);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetColor(BaseColor, color ?? Color.white);
            m.renderQueue = queue;
            return m;
        }

        // ---------- Construction ----------

        ParticleSystem CreateSystem(string name, Material material, int max, float gravity, Color a, Color b)
        {
            var go = new GameObject(name + " Particles");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return ps;
        }

        static ParticleSystem.MinMaxGradient AlphaFade(params Vector2[] keys)
        {
            var alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) alphas[i] = new GradientAlphaKey(keys[i].y, keys[i].x);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxCurve Curve(params Vector2[] keys)
        {
            var frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++) frames[i] = new Keyframe(keys[i].x, keys[i].y);
            var c = new AnimationCurve(frames);
            for (int i = 0; i < frames.Length; i++) c.SmoothTangents(i, 0f);
            return new ParticleSystem.MinMaxCurve(1f, c);
        }

        ParticleSystem CreateGibs()
        {
            var ps = CreateSystem("Gibs", gibMaterial ? gibMaterial : bloodParticleMaterial, 700, 3f, Color.white, Color.white);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
            // Flakes of crust: wide, thin.
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            // Chunks bounce once or twice on the floor, then shrink away.
            var floor = new GameObject("Gib Floor").transform;
            floor.SetParent(transform, false);
            floor.SetPositionAndRotation(new Vector3(0f, 0.06f, 0f), Quaternion.identity);
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            col.SetPlane(0, floor);
            col.bounce = 0.3f;
            col.dampen = 0.45f;
            col.lifetimeLoss = 0f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.alignment = ParticleSystemRenderSpace.Local;
            r.SetMeshes(FXMeshes.Chunks());
            r.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
            return ps;
        }

        ParticleSystem CreateSmoke()
        {
            var ps = CreateSystem("Smoke", smokeMaterial, 260, -0.03f, SmokeGrey, SmokeGrey);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = Curve(new Vector2(0f, 0.5f), new Vector2(0.35f, 0.95f), new Vector2(1f, 1.3f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = AlphaFade(new Vector2(0f, 0f), new Vector2(0.12f, 1f), new Vector2(0.5f, 0.65f), new Vector2(1f, 0f));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            // Puffs burst out, then hang in the air.
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 20f;
            drag.dampen = 0f;
            drag.drag = 2f;
            return ps;
        }

        ParticleSystem CreateFlames()
        {
            var ps = CreateSystem("Flames", flameMaterial, 700, -0.2f, Color.white, Color.white);
            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = Curve(new Vector2(0f, 0.35f), new Vector2(0.35f, 1f), new Vector2(1f, 0.7f));
            // White-hot, #FFC43D, #FF6A2A, then a dark red tail.
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(FireYellow, 0.25f),
                    new GradientColorKey(FireOrange, 0.6f), new GradientColorKey(new Color(0.4f, 0.1f, 0.04f), 1f),
                },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.08f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            return ps;
        }

        ParticleSystem CreateSplats()
        {
            // Full = new stains are skipped until old ones fade (3 s), so keep some headroom for big fights.
            var ps = CreateSystem("Splats", splatMaterial, Mathf.Max(200, maxSplats), 0f, Color.white, Color.white);
            var main = ps.main;
            main.startLifetime = 3f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = Curve(new Vector2(0f, 0.7f), new Vector2(0.08f, 1f), new Vector2(1f, 1.05f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = AlphaFade(new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(1f, 0f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sortMode = ParticleSystemSortMode.None; // oldest first: new stains on top
            return ps;
        }

        /// <summary>One soft additive glow per éclat on the ground, all in one particle system (one draw call).</summary>
        ParticleSystem CreateHalos()
        {
            var ps = CreateSystem("Eclat Glow", glowParticleMaterial, HaloMax, 0f, Amber, Amber);
            var main = ps.main;
            main.startLifetime = 1000f;
            main.startSpeed = 0f;
            haloBuffer = new ParticleSystem.Particle[HaloMax];
            return ps;
        }

        Shape CreateShape(string name, Material material, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.rotation = rotation;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new Shape { t = go.transform, r = r };
        }

        LineRenderer CreateLine(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.positionCount = BoltPoints;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>A bolt is a wide colored glow with a thin white-hot core on top.</summary>
        Bolt CreateBolt()
        {
            var glow = CreateLine("Bolt", transform);
            var core = CreateLine("Core", glow.transform);
            glow.gameObject.SetActive(false);
            return new Bolt { line = glow, core = core };
        }

        // ---------- Particles ----------

        static readonly Color HumanBloodA = new(0.26f, 0f, 0f), HumanBloodB = new(0.52f, 0.02f, 0.01f);
        static readonly Color DefaultInner = new(0.45f, 0.03f, 0.03f);
        static readonly Color IchorB = new(0.45f, 0.22f, 0.05f);

        /// <summary>Human blood: dark red, almost no green or blue.</summary>
        static bool IsHumanRed(Color c) => c.r > 0.15f && c.g < 0.07f && c.b < 0.07f;

        /// <summary>
        /// Human gore (the Pilleur's look: cloth-blue chunks with a red inside). Humans never lose chunks
        /// (Univers 9.10), so <see cref="Gibs(Vector3,int,Color,Color)"/> turns it into a small red puff.
        /// </summary>
        static bool IsHumanGore(Color crust, Color inner) => IsHumanRed(inner) && crust.b > crust.r + 0.12f;

        /// <summary>Red blood (humans). Enemies pass their own colors (Ambrés: amber-black ichor).</summary>
        public void Blood(Vector3 position, int count) => Blood(position, count, HumanBloodA, HumanBloodB);

        /// <summary>Droplets mixed between two colors. Human red is capped to a small puff.</summary>
        public void Blood(Vector3 position, int count, Color a, Color b)
        {
            if (count <= 0 || !blood) return;
            if (IsHumanRed(a) && IsHumanRed(b)) count = Mathf.Min(count, 10);
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true, startColor = a };
            int half = count / 2;
            blood.Emit(p, count - half);
            p.startColor = b;
            blood.Emit(p, half);
        }

        /// <summary>Amber-black ichor of the Ambrés (#221006 + amber), for effects that don't know the enemy.</summary>
        public void Ichor(Vector3 position, int count) => Blood(position, count, Veins, IchorB);

        /// <summary>Chunks in the enemy's color (crust), mixed with dark red.</summary>
        public void Gibs(Vector3 position, int count, Color color) => Gibs(position, count, color, DefaultInner);

        /// <summary>
        /// Crust chunks: about half in <paramref name="color"/>, a quarter in a darker shade, a third in
        /// <paramref name="inner"/>. Human colors (see <see cref="IsHumanGore"/>) never gib.
        /// </summary>
        public void Gibs(Vector3 position, int count, Color color, Color inner) =>
            Gibs(position, count, color, inner, IsHumanGore(color, inner));

        /// <summary>Same, with an explicit flag: <paramref name="human"/> = no chunks, just a small red puff.</summary>
        public void Gibs(Vector3 position, int count, Color color, Color inner, bool human)
        {
            if (human)
            {
                HumanPuff(position);
                return;
            }
            if (count <= 0 || !gibs) return;
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            int meat = count / 3, shade = count / 4;
            p.startColor = color;
            gibs.Emit(p, count - meat - shade);
            Color dark = color * 0.7f;
            dark.a = 1f;
            p.startColor = dark;
            gibs.Emit(p, shade);
            p.startColor = inner;
            gibs.Emit(p, meat);
        }

        static readonly Color HumanPuffColor = new(0.42f, 0.05f, 0.04f, 0.4f);

        /// <summary>A human hit hard or killed: a small red puff, no chunks (Univers 9.9 / 9.10).</summary>
        public void HumanPuff(Vector3 position) => Smoke(position, 0.15f, 2, 0.55f, HumanPuffColor, 0.3f);

        /// <summary>Fast streaks, e.g. bullet impacts. Use colors above 1 for glow.</summary>
        public void Sparks(Vector3 position, Color color, int count, float speedMult = 1f)
        {
            if (count <= 0 || !sparks) return;
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true, startColor = Clamp(color) };
            if (Mathf.Abs(speedMult - 1f) > 0.01f)
            {
                for (int i = 0; i < count; i++)
                {
                    p.velocity = Random.onUnitSphere * Random.Range(4f, 12f) * speedMult;
                    sparks.Emit(p, 1);
                }
                return;
            }
            sparks.Emit(p, count);
        }

        /// <summary>Slow glowing blobs floating up (fire, burning enemies, auras).</summary>
        public void Embers(Vector3 position, float radius, int count, Color color)
        {
            if (count <= 0 || !embers) return;
            var p = new ParticleSystem.EmitParams { startColor = Clamp(color) };
            for (int i = 0; i < count; i++)
            {
                Vector2 c = Random.insideUnitCircle * radius;
                p.position = position + new Vector3(c.x, Random.Range(0f, 0.3f), c.y);
                p.velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 1.8f), Random.Range(-0.3f, 0.3f));
                embers.Emit(p, 1);
            }
        }

        static readonly Color DefaultSmoke = new(0.227f, 0.227f, 0.227f, 0.6f);

        /// <summary>Dark smoke puffs (#3A3A3A) rising from a disc of <paramref name="radius"/>.</summary>
        public void Smoke(Vector3 position, float radius, int count, float size = 1f) => Smoke(position, radius, count, size, DefaultSmoke, 0.7f);

        /// <summary>
        /// Soft alpha-blended puffs: smoke, dust, gas. Alpha of <paramref name="color"/> is the density,
        /// <paramref name="rise"/> how fast they climb (m/s).
        /// </summary>
        public void Smoke(Vector3 position, float radius, int count, float size, Color color, float rise = 0.7f)
        {
            if (count <= 0 || !smoke) return;
            var p = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                Vector2 c = Random.insideUnitCircle;
                p.position = position + new Vector3(c.x * radius, Random.Range(0.15f, 0.6f), c.y * radius);
                p.velocity = new Vector3(c.x * (0.6f + radius), rise * Random.Range(0.6f, 1.3f), c.y * (0.6f + radius));
                p.startSize = size * Random.Range(0.75f, 1.3f);
                smoke.Emit(p, 1);
            }
        }

        static readonly Color32 FlameStart = new(255, 255, 255, 255);

        /// <summary>One tongue of fire (soft additive puff, white-hot to orange to dark), e.g. the flamethrower jet.</summary>
        public void Flame(Vector3 position, Vector3 velocity, float life, float size)
        {
            if (!flames) return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = Mathf.Max(0.05f, life),
                startSize = size,
                startColor = FlameStart,
                rotation = Random.Range(0f, 360f),
            };
            flames.Emit(p, 1);
        }

        static Color32 Clamp(Color c)
        {
            float m = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            return new Color(c.r / m, c.g / m, c.b / m, 1f);
        }

        static Color CapHdr(Color c, float max)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m <= max) return c;
            float k = max / m;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        // ---------- Shapes ----------

        /// <summary>
        /// Expanding shockwave on the ground: a thin band with a soft inward gradient. Anything over 4 m is
        /// drawn as an even thinner, dimmer wave so celebrations never cover the screen.
        /// </summary>
        public void Ring(Vector3 position, float radius, Color color, float duration = 0.35f)
        {
            if (radius <= 0f) return;
            var s = rings[nextRing];
            nextRing = (nextRing + 1) % rings.Length;
            bool wave = radius > WaveRadius && waveMaterial;
            s.r.sharedMaterial = wave ? waveMaterial : ringMaterial;
            s.alpha = wave ? Mathf.Lerp(0.7f, 0.4f, Mathf.InverseLerp(WaveRadius, 9f, radius)) : 1f;
            Begin(s, new Vector3(position.x, 0.06f + nextRing * 0.001f, position.z), radius, CapHdr(color, wave ? WaveHdr : RingHdr), duration);
        }

        /// <summary>Camera-facing flare that pops and fades (explosion core, muzzle flash). Keep it short.</summary>
        public void Flash(Vector3 position, float radius, Color color, float duration = 0.18f)
        {
            if (radius <= 0f) return;
            var s = flashes[nextFlash];
            nextFlash = (nextFlash + 1) % flashes.Length;
            radius = Mathf.Min(radius, MaxFlashRadius);
            Color c = CapHdr(color, FlashHdr);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            c = Color.Lerp(c, new Color(m, m, m, c.a), 0.25f); // a hotter, whiter core
            var view = Cam();
            if (view)
            {
                // Pulled toward the camera so the ground never cuts the flare; random roll so the star varies.
                position += (view.position - position).normalized * Mathf.Min(radius, 2f);
                s.t.rotation = view.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
            s.alpha = 1f;
            Begin(s, position, radius, c, Mathf.Clamp(duration, 0.04f, 0.16f));
            UpdateFlash(s);
        }

        Transform Cam()
        {
            if (!cam)
            {
                var c = Camera.main;
                if (c) cam = c.transform;
            }
            return cam;
        }

        static void Begin(Shape s, Vector3 position, float size, Color color, float duration)
        {
            s.t.position = position;
            s.size = size;
            s.color = color;
            s.duration = Mathf.Max(0.04f, duration);
            s.age = 0f;
            s.live = true;
            s.t.gameObject.SetActive(true);
        }

        static readonly Color ExplosionSmoke = new(0.227f, 0.227f, 0.227f, 0.62f);

        /// <summary>Flare + shockwave + sparks + embers + dark smoke, and the boom sound (bigger = louder and deeper).</summary>
        public void Explosion(Vector3 position, float radius, Color color)
        {
            SFX.Play(SfxId.Explosion, Mathf.Clamp(0.45f + radius * 0.12f, 0.45f, 1f), Mathf.Clamp(1.3f - radius * 0.08f, 0.75f, 1.25f));
            Vector3 ground = new(position.x, 0f, position.z);
            Flash(ground + Vector3.up * 0.6f, Mathf.Clamp(radius * 0.5f, 0.35f, MaxFlashRadius), color * 1.6f, 0.11f);
            Ring(ground, radius, color * 1.5f, 0.32f);
            Sparks(ground + Vector3.up * 0.5f, color * 2f, Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 5, 24), 0.55f + radius * 0.15f);
            Embers(ground, radius * 0.5f, Mathf.Clamp(Mathf.RoundToInt(radius * 2f), 2, 8), color * 1.5f);
            if (smoke && smoke.particleCount < 200)
            {
                // Smoke leans slightly toward the blast's hue (toxic, electric...), mostly #3A3A3A.
                Color tint = Clamp(color);
                Color c = Color.Lerp(ExplosionSmoke, tint * 0.4f, 0.15f);
                c.a = ExplosionSmoke.a;
                Smoke(ground, radius * 0.45f, Mathf.Clamp(Mathf.RoundToInt(radius * 1.6f), 2, 8), 0.55f + radius * 0.3f, c, 0.7f);
            }
        }

        static readonly Color DustColor = new(0.5f, 0.49f, 0.46f, 0.55f);
        static readonly Color GritColor = new(1.2f, 1.15f, 1.05f);

        /// <summary>The Cordon's shell landing: white pop, grey dust, a soot mark. No lava (Univers 9.9).</summary>
        public void DustBurst(Vector3 position, float radius)
        {
            SFX.Play(SfxId.Explosion, Mathf.Clamp(0.45f + radius * 0.12f, 0.45f, 1f), Mathf.Clamp(1.2f - radius * 0.08f, 0.7f, 1.15f));
            Vector3 ground = new(position.x, 0f, position.z);
            Flash(ground + Vector3.up * 0.6f, Mathf.Clamp(radius * 0.4f, 0.3f, 1.6f), CordonWhite * 1.4f, 0.09f);
            Ring(ground, radius, CordonWhite * 1.1f, 0.4f);
            Sparks(ground + Vector3.up * 0.3f, GritColor, Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 4, 16), 0.5f);
            Smoke(ground, radius * 0.55f, Mathf.Clamp(Mathf.RoundToInt(radius * 2.2f), 4, 10), 0.8f + radius * 0.35f, DustColor, 0.5f);
            Scorch(ground, radius * 1.3f);
        }

        /// <summary>
        /// Telegraph for something about to hit the ground (shells, elite blasts...): a thin outline of the danger
        /// radius, and a soft disc that fills it exactly when the hit lands (Univers 9.3). No blinking.
        /// </summary>
        public void Telegraph(Vector3 position, float radius, Color color, float duration)
        {
            var w = warns[nextWarn];
            nextWarn = (nextWarn + 1) % warns.Length;
            float y = 0.07f + nextWarn * 0.001f;
            w.outer.t.position = new Vector3(position.x, y, position.z);
            w.inner.t.position = new Vector3(position.x, y + 0.0005f, position.z);
            // The ring texture's band sits a little inside the quad: scale it so the band is on the real radius.
            float d = radius * 2f / FXTextures.RingEdge;
            w.outer.t.localScale = new Vector3(d, d, 1f);
            w.inner.t.localScale = Vector3.zero;
            w.radius = radius;
            w.color = CapHdr(color, WarnHdr);
            w.duration = Mathf.Max(0.1f, duration);
            w.age = 0f;
            w.live = true;
            w.outer.t.gameObject.SetActive(true);
            w.inner.t.gameObject.SetActive(true);
            UpdateWarn(w);
        }

        void UpdateWarn(Warn w)
        {
            float k = Mathf.Clamp01(w.age / w.duration);
            var c = w.color;
            c.a = 0.5f + 0.5f * k;
            block.SetColor(BaseColor, c);
            w.outer.r.SetPropertyBlock(block);
            float d = w.radius * 2f * k;
            w.inner.t.localScale = new Vector3(d, d, 1f);
            c = w.color * 0.55f;
            c.a = 0.18f + 0.32f * k;
            block.SetColor(BaseColor, c);
            w.inner.r.SetPropertyBlock(block);
        }

        // ---------- Lightning ----------

        public void Lightning(Vector3 from, Vector3 to, Color color, float width = 0.18f, float duration = 0.18f)
        {
            var b = bolts[nextBolt];
            nextBolt = (nextBolt + 1) % bolts.Length;

            Vector3 d = to - from;
            Vector3 side = Vector3.Cross(d.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            side.Normalize();
            float jitter = Mathf.Clamp(d.magnitude * 0.12f, 0.15f, 0.9f);

            for (int i = 0; i < BoltPoints; i++)
            {
                float k = i / (BoltPoints - 1f);
                Vector3 p = from + d * k;
                if (i > 0 && i < BoltPoints - 1)
                    p += side * Random.Range(-jitter, jitter) + Vector3.up * Random.Range(-jitter, jitter) * 0.5f;
                boltScratch[i] = p;
            }
            b.line.SetPositions(boltScratch);
            b.core.SetPositions(boltScratch);

            b.color = color;
            b.width = width;
            b.duration = duration;
            b.age = 0f;
            b.live = true;
            b.line.gameObject.SetActive(true);
            Apply(b, 0f);
        }

        static void Apply(Bolt b, float k)
        {
            float a = 1f - k * k;
            var c = Clamp(b.color);
            Color glow = new Color32(c.r, c.g, c.b, (byte)(a * 150f));
            b.line.startColor = glow;
            b.line.endColor = glow;
            b.line.widthMultiplier = b.width * 2.2f * (1f + (1f - k) * 0.4f);
            Color hot = Color.Lerp(Color.white, (Color)c, 0.2f);
            hot.a = a;
            b.core.startColor = hot;
            b.core.endColor = hot;
            b.core.widthMultiplier = b.width * 0.5f;
        }

        // ---------- Animation ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < rings.Length; i++)
            {
                var s = rings[i];
                if (!s.live) continue;
                if (!Advance(s, dt)) continue;
                float k = s.age / s.duration;
                float grow = 1f - (1f - k) * (1f - k) * (1f - k); // ease out
                float d = s.size * 2f * (0.2f + 0.8f * grow);
                s.t.localScale = new Vector3(d, d, 1f);
                SetColor(s, (1f - k) * s.alpha);
            }

            for (int i = 0; i < flashes.Length; i++)
            {
                var s = flashes[i];
                if (!s.live) continue;
                if (!Advance(s, dt)) continue;
                UpdateFlash(s);
            }

            for (int i = 0; i < warns.Length; i++)
            {
                var w = warns[i];
                if (!w.live) continue;
                w.age += dt;
                if (w.age >= w.duration)
                {
                    w.live = false;
                    w.outer.t.gameObject.SetActive(false);
                    w.inner.t.gameObject.SetActive(false);
                    continue;
                }
                UpdateWarn(w);
            }

            for (int i = 0; i < bolts.Length; i++)
            {
                var b = bolts[i];
                if (!b.live) continue;
                b.age += dt;
                if (b.age >= b.duration)
                {
                    b.live = false;
                    b.line.gameObject.SetActive(false);
                    continue;
                }
                Apply(b, b.age / b.duration);
            }
        }

        void UpdateFlash(Shape s)
        {
            float k = s.age / s.duration;
            // The flare texture's bright part is about half the quad: the quad is a bit bigger than the radius.
            float grow = k < 0.2f ? Mathf.Lerp(0.55f, 1.1f, k / 0.2f) : Mathf.Lerp(1.1f, 0.85f, (k - 0.2f) / 0.8f);
            float d = s.size * 2.3f * grow;
            s.t.localScale = new Vector3(d, d, 1f);
            SetColor(s, (1f - k) * (1f - k));
        }

        static readonly float[] HaloSize = { 1f, 1.3f, 1.75f };
        // #C26A1C, a bit brighter, #E58E2B; steady (amber never blinks)
        static readonly Color32[] HaloColor = { new(194, 106, 28, 90), new(210, 122, 34, 105), new(229, 142, 43, 122) };

        /// <summary>The éclats' glow follows them (written after every gem moved this frame).</summary>
        void LateUpdate()
        {
            if (!halos) return;
            var gems = XPGem.Active;
            int n = Mathf.Min(gems.Count, haloBuffer.Length);
            for (int i = 0; i < n; i++)
            {
                var g = gems[i];
                int tier = Mathf.Clamp(g.Tier, 0, HaloSize.Length - 1);
                ref var p = ref haloBuffer[i];
                p.position = g.transform.position;
                p.startSize = HaloSize[tier];
                p.startColor = HaloColor[tier];
                p.rotation = 0f;
                p.velocity = Vector3.zero;
                p.startLifetime = 1000f;
                p.remainingLifetime = 1000f;
            }
            halos.SetParticles(haloBuffer, n);
        }

        static bool Advance(Shape s, float dt)
        {
            s.age += dt;
            if (s.age < s.duration) return true;
            s.live = false;
            s.t.gameObject.SetActive(false);
            return false;
        }

        void SetColor(Shape s, float alpha)
        {
            var c = s.color;
            c.a = Mathf.Clamp01(alpha);
            block.SetColor(BaseColor, c);
            s.r.SetPropertyBlock(block);
        }

        // ---------- Ground stains ----------

        static readonly Color DefaultSplat = new(0.3f, 0.02f, 0.02f, 0.8f);
        static readonly Color Soot = new(0.035f, 0.032f, 0.03f, 0.55f);

        public void Splat(Vector3 position, float size) => Splat(position, size, DefaultSplat);

        /// <summary>A stain on the ground in <paramref name="color"/> (amber ichor, human blood...). Fades out in 3 s (humans: 2 s).</summary>
        public void Splat(Vector3 position, float size, Color color) => Splat(position, size, color, IsHumanRed(color) ? 2f : 3f);

        /// <summary>A stain that fades out after <paramref name="lifetime"/> seconds.</summary>
        public void Splat(Vector3 position, float size, Color color, float lifetime)
        {
            if (size <= 0f) return;
            if (splatSystem)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(position.x, 0.012f, position.z),
                    startSize3D = new Vector3(size * Random.Range(0.75f, 1.25f), size * Random.Range(0.75f, 1.25f), 1f),
                    rotation = Random.Range(0f, 360f),
                    startColor = color,
                    startLifetime = Mathf.Max(0.3f, lifetime),
                };
                splatSystem.Emit(p, 1);
                return;
            }
            LegacySplat(position, size, color);
        }

        /// <summary>Black soot mark (grenades, shells): fire never leaves blood.</summary>
        public void Scorch(Vector3 position, float size) => Splat(position, size, Soot, 4f);

        /// <summary>Old path (no splat material): pooled flat discs that stay until reused.</summary>
        void LegacySplat(Vector3 position, float size, Color color)
        {
            if (!bloodSplatPrefab || maxSplats <= 0) return;
            splats ??= new Transform[maxSplats];
            var t = splats[nextSplat];
            if (!t)
            {
                t = Instantiate(bloodSplatPrefab, transform).transform;
                splats[nextSplat] = t;
            }

            // Tiny height offset per slot so overlapping stains don't flicker.
            float y = 0.01f + nextSplat * 0.0001f;
            t.SetPositionAndRotation(new Vector3(position.x, y, position.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            t.localScale = new Vector3(size * Random.Range(0.7f, 1.3f), 0.005f, size * Random.Range(0.7f, 1.3f));
            if (t.TryGetComponent<Renderer>(out var r))
            {
                block.SetColor(BaseColor, color);
                r.SetPropertyBlock(block);
            }

            nextSplat = (nextSplat + 1) % maxSplats;
        }
    }
}
