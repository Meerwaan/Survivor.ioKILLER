using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor
{
    /// <summary>
    /// Runtime visuals for the run events, built from primitives and the GameFX materials (no prefab needed):
    /// ground circles, light pillars, relic pickups and pact altars.
    /// </summary>
    public static class RunProps
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock block;

        public static Renderer Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            if (parent) go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>Tints a renderer without making a new material.</summary>
        public static void Tint(Renderer r, Color color)
        {
            if (!r) return;
            block ??= new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            r.SetPropertyBlock(block);
        }

        public static Material RingMaterial => GameFX.Instance ? GameFX.Instance.ringMaterial : null;
        public static Material GlowMaterial => GameFX.Instance ? GameFX.Instance.glowMaterial : null;
    }

    /// <summary>Flat circle lying on the ground (ring texture), stays until hidden.</summary>
    public class GroundRing
    {
        readonly Transform t;
        readonly Renderer r;

        public GroundRing(string name, Transform parent)
        {
            r = RunProps.Primitive(PrimitiveType.Quad, name, parent, RunProps.RingMaterial);
            t = r.transform;
            t.rotation = Quaternion.Euler(90f, 0f, 0f);
            t.gameObject.SetActive(false);
        }

        public bool Visible => t && t.gameObject.activeSelf;

        public void Show(Vector3 position, float radius, Color color, float height = 0.05f)
        {
            if (!t) return;
            t.position = new Vector3(position.x, height, position.z);
            SetRadius(radius);
            SetColor(color);
            t.gameObject.SetActive(true);
        }

        public void SetRadius(float radius)
        {
            if (t) t.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        }

        public void SetColor(Color color) => RunProps.Tint(r, color);

        public void Hide()
        {
            if (t) t.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// A relic lying on the ground: a spinning golden gem in a beam of light. Walk on it to take it.
    /// Recycled (a handful at most exist at once).
    /// </summary>
    public class RelicPickup : MonoBehaviour, IHudTarget
    {
        static readonly List<RelicPickup> pool = new(4);
        static readonly Color Gold = new(2.6f, 2f, 0.7f);

        Transform gem;
        Renderer gemRenderer, beam;
        GroundRing ring;
        float age, nextPulse;

        public static RelicPickup Spawn(Vector3 position)
        {
            RelicPickup pickup = null;
            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (!pool[i])
                {
                    pool.RemoveAt(i);
                    continue;
                }
                if (!pool[i].gameObject.activeSelf)
                {
                    pickup = pool[i];
                    break;
                }
            }
            if (!pickup)
            {
                var go = new GameObject("Relic Pickup");
                pickup = go.AddComponent<RelicPickup>();
                pickup.Build();
                pool.Add(pickup);
            }
            position.y = 0f;
            pickup.transform.position = position;
            pickup.age = 0f;
            pickup.nextPulse = 0f;
            pickup.gameObject.SetActive(true);
            pickup.ring.Show(position, 1.1f, Gold);
            RunHUD.Register(pickup);
            if (GameFX.Instance)
            {
                GameFX.Instance.Ring(position, 3f, Gold, 0.5f);
                GameFX.Instance.Sparks(position + Vector3.up, Gold, 25, 1.5f);
            }
            SFX.Play(SfxId.Chest, 0.6f, 1.4f);
            return pickup;
        }

        void Build()
        {
            gemRenderer = RunProps.Primitive(PrimitiveType.Cube, "Gem", transform, RunProps.GlowMaterial);
            gem = gemRenderer.transform;
            gem.localScale = Vector3.one * 0.55f;
            RunProps.Tint(gemRenderer, Gold);

            beam = RunProps.Primitive(PrimitiveType.Cylinder, "Beam", transform, RunProps.GlowMaterial);
            beam.transform.localPosition = new Vector3(0f, 2f, 0f);
            beam.transform.localScale = new Vector3(0.35f, 2f, 0.35f);
            RunProps.Tint(beam, new Color(0.6f, 0.45f, 0.15f, 0.5f));

            ring = new GroundRing("Relic Ring", transform);
        }

        void OnDisable() => RunHUD.Unregister(this);

        void OnDestroy()
        {
            RunHUD.Unregister(this);
            pool.Remove(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            gem.localPosition = new Vector3(0f, 1.1f + Mathf.Sin(age * 3f) * 0.15f, 0f);
            gem.localRotation = Quaternion.Euler(45f, age * 120f, 45f);

            if (age >= nextPulse && GameFX.Instance)
            {
                nextPulse = age + 0.9f;
                GameFX.Instance.Ring(transform.position, 1.6f, Gold * 0.8f, 0.6f);
            }

            var player = PlayerController.Instance;
            if (!player || dt <= 0f) return;
            Vector3 d = player.Position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1.3f * 1.3f) return;

            Vector3 p = transform.position;
            ring.Hide();
            gameObject.SetActive(false);
            // Several relics can lie on the ground: once the last relic is taken, the others pay out gold (like DropRelic).
            bool granted = RunModifiers.Instance && RunModifiers.Instance.GrantRandomRelic(p) != null;
            if (!granted && Loot.Instance) Loot.Instance.DropCoins(p + Vector3.up * 0.5f, 10, 3);
        }

        // HUD: label above it, and an arrow on the screen edge when it's off-screen.
        public bool HudActive => gameObject.activeSelf;
        public Vector3 HudPosition => transform.position + Vector3.up * 2.4f;
        public string HudLabel => "ÉCHANTILLON";
        public Color HudColor => RunModifiers.RelicColor;
        public bool HudArrow => true;
        public float HudScale => 1f;
    }

    /// <summary>One of the two pact altars: a pillar of red light with the deal written above it.</summary>
    public class PactAltar : MonoBehaviour, IHudTarget
    {
        public Perk Pact { get; private set; }
        public float TouchRadius => 1.3f;
        /// <summary>Can't be sealed during its first 1.5 s: a player running forward must not take a curse before reading it.</summary>
        public bool Armed => age >= 1.5f;

        Renderer pillar, core;
        GroundRing ring, inner;
        string label;
        float age;
        Color color;

        public static PactAltar Create(Perk pact, Vector3 position, Color color)
        {
            var go = new GameObject("Pact Altar");
            var altar = go.AddComponent<PactAltar>();
            position.y = 0f;
            go.transform.position = position;
            altar.Pact = pact;
            altar.color = color;
            altar.label =
                $"<size=40><b>{pact.name.ToUpperInvariant()}</b></size>\n" +
                $"<color={RunModifiers.BoonColor}>{pact.boon}</color>\n" +
                $"<color={RunModifiers.BaneColor}>{pact.bane}</color>";
            altar.Build();
            RunHUD.Register(altar);
            return altar;
        }

        void Build()
        {
            pillar = RunProps.Primitive(PrimitiveType.Cylinder, "Pillar", transform, RunProps.GlowMaterial);
            pillar.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            pillar.transform.localScale = new Vector3(0.9f, 1.8f, 0.9f);
            RunProps.Tint(pillar, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.6f));

            core = RunProps.Primitive(PrimitiveType.Sphere, "Core", transform, RunProps.GlowMaterial);
            core.transform.localScale = Vector3.one * 0.7f;
            RunProps.Tint(core, color * 2.5f);

            ring = new GroundRing("Altar Ring", transform);
            ring.Show(transform.position, TouchRadius, color * 2f);
            inner = new GroundRing("Altar Ring Inner", transform);
            inner.Show(transform.position, TouchRadius * 0.5f, color * 1.5f, 0.055f);
        }

        void Update()
        {
            age += Time.deltaTime;
            core.transform.localPosition = new Vector3(0f, 1.4f + Mathf.Sin(age * 2.5f) * 0.2f, 0f);
            float pulse = 1f + Mathf.Sin(age * 5f) * 0.12f;
            inner.SetRadius(TouchRadius * 0.5f * pulse);
            if (GameFX.Instance && Random.value < 0.25f) GameFX.Instance.Embers(transform.position, 0.6f, 1, color * 2f);
        }

        /// <summary>Altars crumble with a puff of embers.</summary>
        public void Crumble(bool chosen)
        {
            var fx = GameFX.Instance;
            if (fx && !chosen)
            {
                fx.Embers(transform.position, 0.8f, 10, color);
                fx.Ring(transform.position, 2f, color, 0.4f);
            }
            RunHUD.Unregister(this);
            Destroy(gameObject);
        }

        void OnDestroy() => RunHUD.Unregister(this);

        public bool HudActive => this && gameObject.activeSelf;
        public Vector3 HudPosition => transform.position + Vector3.up * 4.2f;
        public string HudLabel => label;
        public Color HudColor => Color.white;
        public bool HudArrow => true;
        public float HudScale => 0.9f;
    }
}
