using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Survivor
{
    /// <summary>
    /// Applies the equipped skin to the player at runtime (added by MonetizationRuntime, no scene edit):
    /// body color via an instanced material, plus accessory primitives parented to the player's visual
    /// (hats, crown, halo, horns...) and an optional glowing aura disc on the ground. Purely cosmetic.
    /// Coexists with PlayerHealth's hurt flash: the flash swaps the body material, then we put ours back.
    /// </summary>
    public class PlayerSkin : MonoBehaviour
    {
        static readonly List<PlayerSkin> Active = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        Renderer body;
        Material original, skinMat;
        PlayerHealth health;
        /// <summary>CORDON model: every skin is a modeled outfit (a mesh variant on the Passeur's rig).</summary>
        SkinnedMeshRenderer[] outfits;
        Transform visual, partsRoot, auraRoot;
        readonly List<Material> materials = new();

        public static void Attach(GameObject player)
        {
            if (player && !player.GetComponent<PlayerSkin>()) player.AddComponent<PlayerSkin>();
        }

        /// <summary>Re-applies the equipped skin (called when the player equips something in the menu).</summary>
        public static void RefreshAll()
        {
            foreach (var s in Active)
                if (s) s.Apply(Cosmetics.EquippedSkin);
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start()
        {
            health = GetComponent<PlayerHealth>();
            body = health ? health.bodyRenderer : null;
            if (!body)
            {
                var t = transform.Find("Visual/Body");
                if (t) body = t.GetComponent<Renderer>();
            }
            if (body) original = body.sharedMaterial;
            var controller = GetComponent<PlayerController>();
            visual = controller && controller.visual ? controller.visual : transform;
            var model = visual.Find("Model");
            if (model)
            {
                outfits = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (outfits.Length < 2) outfits = null;
            }
            Apply(Cosmetics.EquippedSkin);
        }

        /// <summary>Mesh variant name of a skin on the Passeur model (Tools/Blender/assets/passeur.py).</summary>
        public static string OutfitName(string skinId) => skinId switch
        {
            "skin_camo" => "Passeur_Camo",
            "skin_sheriff" => "Passeur_Sherif",
            "skin_punk" => "Passeur_Punk",
            "skin_medic" => "Passeur_Medic",
            "skin_king" => "Passeur_King",
            "skin_viking" => "Passeur_Viking",
            "skin_hazmat" => "Passeur_Hazmat",
            "skin_angel" => "Passeur_Angel",
            "skin_demon" => "Passeur_Demon",
            "skin_neon" => "Passeur_Neon",
            "skin_founder" => "Passeur_Founder",
            _ => "Passeur",
        };

        /// <summary>Shows the outfit of <paramref name="s"/> and hides the others; the hurt flash follows it.</summary>
        void ApplyOutfit(SkinDef s)
        {
            string want = OutfitName(s != null ? s.id : Cosmetics.DefaultSkinId);
            SkinnedMeshRenderer shown = null;
            foreach (var o in outfits)
                if (o && o.name == want) shown = o;
            if (!shown) shown = System.Array.Find(outfits, o => o && o.name == "Passeur") ?? outfits[0];
            foreach (var o in outfits)
            {
                if (!o) continue;
                if (original) o.sharedMaterial = original; // never keep a hurt flash on a hidden outfit
                o.gameObject.SetActive(o == shown);
            }
            if (health) health.bodyRenderer = shown;
            body = shown;
        }

        void LateUpdate()
        {
            if (outfits != null) return;
            // PlayerHealth restores its cached (original) material after a hurt flash: put the skin back.
            if (skinMat && body && body.sharedMaterial == original) body.sharedMaterial = skinMat;
        }

        void OnDestroy() => Clear();

        void Clear()
        {
            if (partsRoot) Destroy(partsRoot.gameObject);
            if (auraRoot) Destroy(auraRoot.gameObject);
            partsRoot = auraRoot = null;
            if (body && original && body.sharedMaterial == skinMat) body.sharedMaterial = original;
            skinMat = null;
            foreach (var m in materials)
                if (m) Destroy(m);
            materials.Clear();
        }

        public void Apply(SkinDef s)
        {
            if (outfits != null)
            {
                ApplyOutfit(s);
                return;
            }
            Clear();
            if (s == null || !original) return;
            if (s.id == Cosmetics.DefaultSkinId) return; // the builder's look, untouched

            skinMat = Mat(s.body, s.smoothness, s.metallic, s.bodyEmission);
            if (body.sharedMaterial == original) body.sharedMaterial = skinMat;

            partsRoot = new GameObject("Skin " + s.id).transform;
            partsRoot.SetParent(visual, false);
            BuildAccessory(s);

            if (s.aura.a > 0f)
            {
                auraRoot = new GameObject("Skin Aura").transform;
                auraRoot.SetParent(transform, false);
                var aura = Part(PrimitiveType.Cylinder, auraRoot, new Vector3(0f, 0.03f, 0f), new Vector3(1.9f, 0.004f, 1.9f),
                    Mat(s.aura * 0.4f, 0.2f, 0f, s.aura * 1.6f), false);
                aura.name = "Aura";
            }
        }

        // ---------- Accessories (Visual local space: body capsule from y 0 to 2, radius 0.4, facing +z) ----------

        void BuildAccessory(SkinDef s)
        {
            var root = partsRoot;
            Material a = Mat(s.accent, 0.4f, 0f, Color.black);
            switch (s.accessory)
            {
                case SkinAccessory.Cap:
                    Part(PrimitiveType.Sphere, root, new Vector3(0f, 1.86f, 0f), new Vector3(0.84f, 0.5f, 0.84f), a);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.84f, 0.42f), new Vector3(0.6f, 0.06f, 0.42f), a);
                    break;

                case SkinAccessory.CowboyHat:
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.9f, 0f), new Vector3(1.35f, 0.025f, 1.35f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 2.08f, 0f), new Vector3(0.6f, 0.17f, 0.6f), a);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.97f, 0f), new Vector3(0.62f, 0.035f, 0.62f), Mat(s.accent2, 0.3f, 0f, Color.black));
                    break;

                case SkinAccessory.Mohawk:
                {
                    var glow = Mat(s.accent, 0.5f, 0f, s.accent * 0.35f);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 2.08f, 0.24f), new Vector3(0.17f, 0.34f, 0.24f), glow);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 2.16f, 0f), new Vector3(0.17f, 0.5f, 0.26f), glow);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 2.08f, -0.24f), new Vector3(0.17f, 0.38f, 0.24f), glow);
                    break;
                }

                case SkinAccessory.Medic:
                {
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.7f, 0f), new Vector3(0.86f, 0.06f, 0.86f), a);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.15f, -0.46f), new Vector3(0.6f, 0.7f, 0.28f), Mat(s.accent2, 0.5f, 0f, Color.black));
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.2f, -0.605f), new Vector3(0.36f, 0.11f, 0.02f), a);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.2f, -0.605f), new Vector3(0.11f, 0.36f, 0.02f), a);
                    // Red cross on top of the pack too: readable from the top-down camera.
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.505f, -0.46f), new Vector3(0.3f, 0.02f, 0.09f), a);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.505f, -0.46f), new Vector3(0.09f, 0.02f, 0.24f), a);
                    break;
                }

                case SkinAccessory.Crown:
                {
                    var gold = Mat(s.accent, 0.8f, 0.9f, s.accent * 0.25f);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 2f, 0f), new Vector3(0.66f, 0.09f, 0.66f), gold);
                    for (int i = 0; i < 6; i++)
                    {
                        float ang = i * 60f * Mathf.Deg2Rad;
                        var p = new Vector3(Mathf.Sin(ang) * 0.27f, 2.15f, Mathf.Cos(ang) * 0.27f);
                        Part(PrimitiveType.Cube, root, p, new Vector3(0.1f, 0.2f, 0.1f), gold, true, Quaternion.Euler(0f, i * 60f + 45f, 0f));
                    }
                    Part(PrimitiveType.Sphere, root, new Vector3(0f, 2.02f, 0.33f), Vector3.one * 0.11f, Mat(s.accent2, 0.9f, 0f, s.accent2 * 0.8f));
                    break;
                }

                case SkinAccessory.Viking:
                {
                    Part(PrimitiveType.Sphere, root, new Vector3(0f, 1.86f, 0f), new Vector3(0.88f, 0.62f, 0.88f), Mat(s.accent, 0.6f, 0.7f, Color.black));
                    var horn = Mat(s.accent2, 0.4f, 0f, Color.black);
                    Part(PrimitiveType.Capsule, root, new Vector3(0.42f, 2.02f, 0f), new Vector3(0.13f, 0.22f, 0.13f), horn, true, Quaternion.Euler(0f, 0f, -45f));
                    Part(PrimitiveType.Capsule, root, new Vector3(-0.42f, 2.02f, 0f), new Vector3(0.13f, 0.22f, 0.13f), horn, true, Quaternion.Euler(0f, 0f, 45f));
                    break;
                }

                case SkinAccessory.Hazmat:
                {
                    Part(PrimitiveType.Sphere, root, new Vector3(0f, 1.74f, 0f), new Vector3(0.86f, 0.7f, 0.86f), skinMat);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.62f, 0.33f), new Vector3(0.56f, 0.24f, 0.14f), Mat(s.accent, 0.95f, 0.2f, Color.black));
                    var metal = Mat(s.accent2, 0.6f, 0.6f, Color.black);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1.2f, -0.44f), new Vector3(0.32f, 0.36f, 0.32f), metal);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0.15f, 1.42f, 0.38f), new Vector3(0.13f, 0.05f, 0.13f), metal, true, Quaternion.Euler(90f, 0f, 0f));
                    Part(PrimitiveType.Cylinder, root, new Vector3(-0.15f, 1.42f, 0.38f), new Vector3(0.13f, 0.05f, 0.13f), metal, true, Quaternion.Euler(90f, 0f, 0f));
                    break;
                }

                case SkinAccessory.Halo:
                {
                    var glow = Mat(s.accent, 0.5f, 0f, s.accent * 2.2f);
                    for (int i = 0; i < 14; i++)
                    {
                        float ang = i * (360f / 14f) * Mathf.Deg2Rad;
                        Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Sin(ang) * 0.4f, 2.45f, Mathf.Cos(ang) * 0.4f), Vector3.one * 0.13f, glow, false);
                    }
                    break;
                }

                case SkinAccessory.DemonHorns:
                {
                    var horn = Mat(s.accent, 0.6f, 0f, Color.black);
                    Part(PrimitiveType.Capsule, root, new Vector3(0.2f, 2.02f, 0.06f), new Vector3(0.12f, 0.22f, 0.12f), horn, true, Quaternion.Euler(-15f, 0f, -25f));
                    Part(PrimitiveType.Capsule, root, new Vector3(-0.2f, 2.02f, 0.06f), new Vector3(0.12f, 0.22f, 0.12f), horn, true, Quaternion.Euler(-15f, 0f, 25f));
                    break;
                }

                case SkinAccessory.Neon:
                {
                    var glow = Mat(s.accent, 0.8f, 0f, s.accent * 2f);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0f, 2.2f, 0f), new Vector3(0.035f, 0.22f, 0.035f), Mat(new Color(0.1f, 0.1f, 0.12f), 0.5f, 0.5f, Color.black));
                    Part(PrimitiveType.Sphere, root, new Vector3(0f, 2.45f, 0f), Vector3.one * 0.18f, glow, false);
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.62f, 0.34f), new Vector3(0.58f, 0.08f, 0.1f), glow, false);
                    break;
                }

                case SkinAccessory.Cape:
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.1f, -0.44f), new Vector3(0.74f, 1.25f, 0.05f), a, true, Quaternion.Euler(8f, 0f, 0f));
                    Part(PrimitiveType.Cube, root, new Vector3(0f, 1.7f, -0.3f), new Vector3(0.78f, 0.08f, 0.3f), a);
                    break;
            }
        }

        GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows = true, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            // No collider: the player's rigidbody would turn it into part of its physics shape.
            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Instanced copy of the player's own (URP Lit) material, so it's always a shader the build includes.</summary>
        Material Mat(Color color, float smoothness, float metallic, Color emission)
        {
            var m = new Material(original) { name = "Skin " + color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0.001f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            materials.Add(m);
            return m;
        }
    }
}
