using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Survivor.EditorTools
{
    /// <summary>
    /// Soft FX pass: the procedural textures (Survivor.FXTextures, saved once as PNGs), the FX materials, and the
    /// FX prefab parts (gas cloud, fire pool, darts). Unlike the other materials, FX materials are owned by code:
    /// their settings are re-applied on EVERY build, so changing them here always takes effect.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        class FxMats
        {
            public Material flare, wave, fill, smoke, flame, splat, gasCloud, fireGround, dart;
        }

        static FxMats fxMats;

        const string UnlitShader = "Universal Render Pipeline/Unlit";
        const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>Called at the end of CreateMaterials: new FX materials, and the soft look re-applied to the old ones.</summary>
        static void CreateFXMaterials(Mats m)
        {
            var dot = CreateFXTexture("T_FX_SoftDot", false);
            var ring = FXAsset("T_FX_RingBand", FXTextures.RingBand, true);
            var puff = FXAsset("T_FX_Puff", FXTextures.Puff, false);
            var cloud = FXAsset("T_FX_Cloud", FXTextures.Cloud, true);
            const int T = (int)RenderQueue.Transparent;

            fxMats = new FxMats
            {
                flare = FXMat("M_FX_Flare", UnlitShader, FXAsset("T_FX_Flare", FXTextures.Flare, false), true, Color.white, T),
                wave = FXMat("M_FX_Wave", UnlitShader, FXAsset("T_FX_Wave", FXTextures.Wave, true), true, Color.white, T),
                fill = FXMat("M_FX_Fill", UnlitShader, FXAsset("T_FX_Fill", FXTextures.Fill, false), true, Color.white, T),
                smoke = FXMat("M_FX_Smoke", ParticleShader, puff, false, Color.white, T - 10),
                flame = FXMat("M_FX_Flame", ParticleShader, puff, true, new Color(1.25f, 1.25f, 1.25f, 1f), T),
                splat = FXMat("M_FX_Splat", ParticleShader, FXAsset("T_FX_Splat", FXTextures.Splat, false), false, Color.white, T - 50),
                gasCloud = FXMat("M_FX_GasCloud", UnlitShader, cloud, false, new Color(0.5f, 0.62f, 0.17f, 0.3f), T - 20),
                fireGround = FXMat("M_FX_FireGround", UnlitShader, cloud, true, new Color(1.3f, 0.55f, 0.16f, 0.7f), T),
                dart = Unlit("M_Dart", new Color(0.85f, 1.25f, 0.32f)),
            };

            // Existing assets, re-applied every build: thin ring without the old inner fill, soft beam for every
            // trail / bolt, soft alpha-blended blood droplets instead of opaque squares.
            FXMat("M_FX_Ring", UnlitShader, ring, true, Color.white, T);
            FXMat("M_FX_Line", ParticleShader, FXAsset("T_FX_Beam", FXTextures.Beam, false), true, Color.white, T);
            FXMat("M_BloodParticle", ParticleShader, dot, false, null, T);

            // Tracers: a bright HDR core that blooms a little (Unlit opaque).
            m.bullet.SetColor("_BaseColor", new Color(1.5f, 1.2f, 0.62f));
            EditorUtility.SetDirty(m.bullet);
            fxMats.dart.SetColor("_BaseColor", new Color(0.85f, 1.25f, 0.32f)); // toxic #A6D23C, HDR
            EditorUtility.SetDirty(fxMats.dart);
        }

        /// <summary>Generates a procedural texture once as a PNG (delete the file to regenerate it).</summary>
        static Texture2D FXAsset(string name, System.Func<Texture2D> make, bool mipmaps)
        {
            string path = $"{Root}/Textures/{name}.png";
            if (!File.Exists(path))
            {
                var tex = make();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = mipmaps;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // soft gradients band when compressed
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Loads or creates a transparent FX material and (re)applies all of its settings.</summary>
        static Material FXMat(string name, string shader, Texture2D texture, bool additive, Color? color, int queue)
        {
            var mat = LoadOrCreate(name, shader, out _);
            if (!mat.shader || mat.shader.name != shader) mat.shader = Shader.Find(shader);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            if (texture) mat.SetTexture("_BaseMap", texture);
            if (color.HasValue) mat.SetColor("_BaseColor", color.Value);
            mat.renderQueue = queue;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Hands the soft FX materials to GameFX (called from BuildScene).</summary>
        static void WireFX(GameFX fx)
        {
            if (fxMats == null) return;
            fx.flareMaterial = fxMats.flare;
            fx.waveMaterial = fxMats.wave;
            fx.fillMaterial = fxMats.fill;
            fx.smokeMaterial = fxMats.smoke;
            fx.flameMaterial = fxMats.flame;
            fx.splatMaterial = fxMats.splat;
        }

        /// <summary>Flat quad lying on the ground (front face up), 1 m wide before scaling.</summary>
        static GameObject GroundQuad(string name, Transform parent, Material mat, float scale, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>
        /// The gas aura (scaled to the radius by GasWeapon): two soft cloud layers a bit wider than the radius
        /// (their texture fades out before the edge) turning in opposite directions, and a thin edge ring on the radius.
        /// </summary>
        static GameObject GasCloudVisual(Mats m)
        {
            var root = new GameObject("Gas Cloud");
            var cloud = fxMats != null ? fxMats.gasCloud : m.gas;
            GroundQuad("Cloud A", root.transform, cloud, 1.2f, 0f);
            GroundQuad("Cloud B", root.transform, cloud, 1f, 67f);
            if (fxMats != null) GroundQuad("Edge", root.transform, fxMats.wave, 1f / FXTextures.WaveEdge, 0f);
            return root;
        }

        /// <summary>Molotov fire: two flickering flame layers on the ground (FirePool animates them and adds flames + smoke).</summary>
        static GameObject FirePoolPrefab(Mats m)
        {
            var root = new GameObject("FirePool");
            var mat = fxMats != null ? fxMats.fireGround : m.fire;
            GroundQuad("Flames A", root.transform, mat, 1.15f, 0f);
            GroundQuad("Flames B", root.transform, mat, 0.85f, 71f);
            root.AddComponent<FirePool>();
            return Save(root, "FirePool");
        }

        /// <summary>Poison dart: a toxic-green tracer (its own prefab: the revolver's bullets stay yellow).</summary>
        static GameObject DartPrefab(Mats m)
        {
            var mat = fxMats != null ? fxMats.dart : m.bullet;
            return ProjectilePrefab("Dart", new Vector3(0.1f, 0.1f, 0.42f), mat, 0.3f, m.fxLine, 0.08f, 0.12f,
                new Color(0.65f, 0.82f, 0.24f, 0.7f), new Color(1f, 1.3f, 0.4f));
        }
    }
}
