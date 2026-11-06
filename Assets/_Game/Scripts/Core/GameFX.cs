using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor
{
    /// <summary>
    /// All the cheap juice in one place: blood, gibs that bounce on the floor, sparks, embers,
    /// shockwave rings, glowing flashes, lightning bolts, danger telegraphs, and blood stains on the ground.
    /// Everything is pooled (fixed-size ring buffers), so it costs nothing to call often.
    /// Colors above 1 glow thanks to bloom.
    /// </summary>
    public class GameFX : MonoBehaviour
    {
        public static GameFX Instance { get; private set; }

        public Material bloodParticleMaterial;
        public GameObject bloodSplatPrefab;
        [Tooltip("Oldest stains get reused once this many are on the ground.")]
        public int maxSplats = 150;

        [Header("Juice materials (created by the prototype builder)")]
        [Tooltip("Particles/Lit, white: chunks of enemies.")]
        public Material gibMaterial;
        [Tooltip("Particles/Unlit additive with a soft dot: sparks and embers.")]
        public Material glowParticleMaterial;
        [Tooltip("Unlit additive with a ring texture: shockwaves.")]
        public Material ringMaterial;
        [Tooltip("Unlit additive, plain: explosion cores.")]
        public Material glowMaterial;
        [Tooltip("Particles/Unlit additive, plain: lightning.")]
        public Material lineMaterial;

        const int RingCount = 48, FlashCount = 24, BoltCount = 24, BoltPoints = 9, WarnCount = 32;

        ParticleSystem blood, gibs, sparks, embers;
        Transform[] splats;
        int nextSplat;

        class Shape
        {
            public Transform t;
            public Renderer r;
            public float age, duration, size;
            public Color color;
            public bool live;
        }

        class Bolt
        {
            public LineRenderer line;
            public float age, duration, width;
            public Color color;
            public bool live;
        }

        /// <summary>Danger telegraph: a fixed outer circle and an inner one that fills it up until impact.</summary>
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

        void Awake()
        {
            Instance = this;
            splats = new Transform[maxSplats];
            block = new MaterialPropertyBlock();

            blood = CreateSystem("Blood", bloodParticleMaterial, 3000, 2.5f, new Color(0.4f, 0f, 0f), new Color(0.8f, 0.05f, 0.02f));
            var bm = blood.main;
            bm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);

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

            rings = new Shape[RingCount];
            for (int i = 0; i < RingCount; i++) rings[i] = CreateShape("Ring", PrimitiveType.Quad, ringMaterial, Quaternion.Euler(90f, 0f, 0f));
            flashes = new Shape[FlashCount];
            for (int i = 0; i < FlashCount; i++) flashes[i] = CreateShape("Flash", PrimitiveType.Sphere, glowMaterial, Quaternion.identity);
            bolts = new Bolt[BoltCount];
            for (int i = 0; i < BoltCount; i++) bolts[i] = CreateBolt();
            warns = new Warn[WarnCount];
            for (int i = 0; i < WarnCount; i++)
                warns[i] = new Warn
                {
                    outer = CreateShape("Warn", PrimitiveType.Quad, ringMaterial, Quaternion.Euler(90f, 0f, 0f)),
                    inner = CreateShape("Warn Fill", PrimitiveType.Quad, ringMaterial, Quaternion.Euler(90f, 0f, 0f)),
                };
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

        ParticleSystem CreateGibs()
        {
            var ps = CreateSystem("Gibs", gibMaterial ? gibMaterial : bloodParticleMaterial, 800, 3f, Color.white, Color.white);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            // Chunks bounce once or twice on the floor, then shrink away.
            var floor = new GameObject("Gib Floor").transform;
            floor.SetParent(transform, false);
            floor.SetPositionAndRotation(new Vector3(0f, 0.08f, 0f), Quaternion.identity);
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            col.SetPlane(0, floor);
            col.bounce = 0.35f;
            col.dampen = 0.4f;
            col.lifetimeLoss = 0f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.alignment = ParticleSystemRenderSpace.Local;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            return ps;
        }

        Shape CreateShape(string name, PrimitiveType type, Material material, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.rotation = rotation;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Shape { t = go.transform, r = r };
        }

        Bolt CreateBolt()
        {
            var go = new GameObject("Bolt");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.positionCount = BoltPoints;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false);
            return new Bolt { line = line };
        }

        // ---------- Particles ----------

        public void Blood(Vector3 position, int count)
        {
            if (count <= 0) return;
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            blood.Emit(p, count);
        }

        /// <summary>Chunks of meat in the enemy's color, mixed with dark red.</summary>
        public void Gibs(Vector3 position, int count, Color color)
        {
            if (count <= 0 || !gibs) return;
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            int meat = count / 3;
            p.startColor = color;
            gibs.Emit(p, count - meat);
            p.startColor = new Color(0.45f, 0.03f, 0.03f);
            gibs.Emit(p, meat);
        }

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

        /// <summary>Slow glowing blobs floating up (fire, gas, burning enemies).</summary>
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

        static Color32 Clamp(Color c)
        {
            float m = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            return new Color(c.r / m, c.g / m, c.b / m, 1f);
        }

        // ---------- Shapes ----------

        /// <summary>Expanding shockwave on the ground.</summary>
        public void Ring(Vector3 position, float radius, Color color, float duration = 0.35f)
        {
            var s = rings[nextRing];
            nextRing = (nextRing + 1) % rings.Length;
            Begin(s, new Vector3(position.x, 0.06f + nextRing * 0.001f, position.z), radius, color, duration);
        }

        /// <summary>Glowing ball that pops and fades (explosion core, muzzle flash).</summary>
        public void Flash(Vector3 position, float radius, Color color, float duration = 0.18f)
        {
            var s = flashes[nextFlash];
            nextFlash = (nextFlash + 1) % flashes.Length;
            Begin(s, position, radius, color, duration);
        }

        static void Begin(Shape s, Vector3 position, float size, Color color, float duration)
        {
            s.t.position = position;
            s.size = size;
            s.color = color;
            s.duration = Mathf.Max(0.05f, duration);
            s.age = 0f;
            s.live = true;
            s.t.gameObject.SetActive(true);
        }

        /// <summary>Flash + shockwave + sparks + embers, and the boom sound (bigger = louder and deeper).</summary>
        public void Explosion(Vector3 position, float radius, Color color)
        {
            SFX.Play(SfxId.Explosion, Mathf.Clamp(0.45f + radius * 0.12f, 0.45f, 1f), Mathf.Clamp(1.3f - radius * 0.08f, 0.75f, 1.25f));
            Vector3 ground = new(position.x, 0f, position.z);
            Flash(ground + Vector3.up * 0.5f, radius * 0.9f, color * 2.2f, 0.22f);
            Ring(ground, radius * 1.15f, color * 2f, 0.35f);
            Sparks(ground + Vector3.up * 0.5f, color * 2f, Mathf.Clamp(Mathf.RoundToInt(radius * 8f), 6, 40), 0.6f + radius * 0.2f);
            Embers(ground, radius * 0.6f, Mathf.Clamp(Mathf.RoundToInt(radius * 3f), 3, 14), color * 1.5f);
        }

        /// <summary>
        /// Telegraph for something about to hit the ground (meteors, elite blasts...): a pulsing circle of the
        /// danger radius, and an inner circle that grows to fill it exactly when the hit lands.
        /// </summary>
        public void Telegraph(Vector3 position, float radius, Color color, float duration)
        {
            var w = warns[nextWarn];
            nextWarn = (nextWarn + 1) % warns.Length;
            float y = 0.07f + nextWarn * 0.001f;
            w.outer.t.position = new Vector3(position.x, y, position.z);
            w.inner.t.position = new Vector3(position.x, y + 0.0005f, position.z);
            w.outer.t.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            w.inner.t.localScale = Vector3.zero;
            w.radius = radius;
            w.color = color;
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
            // Blinks faster as the hit gets closer.
            float blink = 0.6f + 0.4f * Mathf.Sin(w.age * Mathf.Lerp(8f, 30f, k));
            var c = w.color;
            c.a = Mathf.Clamp01(0.35f + 0.65f * blink);
            block.SetColor(BaseColor, c);
            w.outer.r.SetPropertyBlock(block);
            float d = w.radius * 2f * k;
            w.inner.t.localScale = new Vector3(d, d, 1f);
            c = w.color * 0.7f;
            c.a = 0.4f + 0.5f * k;
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
                b.line.SetPosition(i, p);
            }

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
            Color col = new Color32(c.r, c.g, c.b, (byte)(a * 255f));
            b.line.startColor = col;
            b.line.endColor = col;
            b.line.widthMultiplier = b.width * (1f + (1f - k) * 0.5f);
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
                float d = s.size * 2f * (0.15f + 0.85f * grow);
                s.t.localScale = new Vector3(d, d, 1f);
                SetColor(s, 1f - k);
            }

            for (int i = 0; i < flashes.Length; i++)
            {
                var s = flashes[i];
                if (!s.live) continue;
                if (!Advance(s, dt)) continue;
                float k = s.age / s.duration;
                float d = s.size * 2f * (k < 0.25f ? Mathf.Lerp(0.4f, 1.1f, k / 0.25f) : Mathf.Lerp(1.1f, 0.8f, (k - 0.25f) / 0.75f));
                s.t.localScale = Vector3.one * d;
                SetColor(s, 1f - k);
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

        public void Splat(Vector3 position, float size)
        {
            if (!bloodSplatPrefab) return;

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

            nextSplat = (nextSplat + 1) % maxSplats;
        }
    }
}
