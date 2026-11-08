using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Survivor.EditorTools
{
    /// <summary>
    /// CORDON art pass, the world: the bible's light (Univers 9.5: cold low light, blue-grey fog, desaturated
    /// grade, high-threshold bloom, light grain), the Ostrov ground, and the decor scattered by WorldDresser
    /// from the Blender decor kit + its manifest (Tools/Blender/assets/decor_manifest.json).
    /// Everything falls back to the greybox when the art files are missing.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string DecorFbx = ArtRoot + "/Props/Decor_Ostrov.fbx";
        const string DecorManifest = "Tools/Blender/assets/decor_manifest.json";
        const string GroundTex = ArtRoot + "/Textures/T_Ground_Ostrov.png";

        static readonly Color FogCordon = new(0.36f, 0.4f, 0.46f); // overcast haze: it melts the top of the screen

        /// <summary>Overcast, cold, wet: a low bluish sun and a blue-grey haze (no warm light except fire and amber).</summary>
        static void CordonAtmosphere(Light sun)
        {
            if (artMats == null) return;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.4f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogCordon;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 58f;
            sun.color = new Color(0.8f, 0.87f, 0.96f);
            sun.intensity = 2.1f;
            sun.shadowStrength = 0.78f;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        }

        static Material CordonGround()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTex);
            if (!tex) return null;
            var mat = LoadOrCreate("M_GroundOstrov", "Universal Render Pipeline/Lit", out bool created);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(50f, 50f)); // 400 m plane, one tile = 8 m
            if (created)
            {
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.8f);
                mat.SetFloat("_SmoothnessTextureChannel", 1f); // smoothness from the albedo alpha (puddles)
                mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                mat.SetFloat("_EnvironmentReflections", 1f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile CordonPostProfile()
        {
            if (artMats == null) return null;
            string path = Root + "/Settings/PostFX_Cordon.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.55f);
            color.contrast.Override(14f);
            color.saturation.Override(-18f);
            var wb = profile.Add<WhiteBalance>();
            wb.temperature.Override(-6f);
            var smh = profile.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.93f, 0.98f, 1.08f, 0f));   // bluish blacks
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.96f, 0f)); // slightly warm highs
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.62f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.03f, 0.05f));
            var grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.8f);
            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---------- Decor ----------

        [System.Serializable]
        class ManifestEntry
        {
            public string name;
            public float[] size;
            public bool blocks;
            public float[] collider;
            public float[] collider_center;
            public string kind;
            public float weight = 1f;
            public string cluster;
        }

        [System.Serializable]
        class Manifest
        {
            public ManifestEntry[] items;
            public ManifestEntry[] objects; // the decor script writes { "about": ..., "objects": [...] }
        }

        static readonly string[] SnapWords = { "Wall", "Barrier", "Sandbag", "Fence", "Shelter", "Kiosk", "Container", "Planter", "Sign" };

        /// <summary>Makes one prefab per decor model (mesh + palette material + box collider when it blocks)
        /// and a WorldDresser that scatters them around the player.</summary>
        static void BuildDecor(GameObject game)
        {
            BuildBackdrop();
            if (artMats == null || !File.Exists(DecorFbx) || !File.Exists(DecorManifest)) return;
            string json = File.ReadAllText(DecorManifest).Trim();
            if (json.StartsWith("[")) json = "{\"items\":" + json + "}";
            var manifest = JsonUtility.FromJson<Manifest>(json);
            if (manifest != null && manifest.items == null) manifest.items = manifest.objects;
            if (manifest?.items == null) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DecorFbx);
            string dir = Root + "/Prefabs/Decor";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root + "/Prefabs", "Decor");
            var wet = ArtMat("M_CordonWet", (Texture2D)artMats.body.GetTexture("_BaseMap"), mat =>
            {
                mat.SetFloat("_Smoothness", 0.92f);
                mat.SetColor("_RimColor", new Color(0.25f, 0.3f, 0.36f));
            });

            var items = new List<DecorItem>();
            foreach (var e in manifest.items)
            {
                if (string.IsNullOrEmpty(e.name) || e.kind == "backdrop") continue;
                var src = FindDeep(model.transform, e.name);
                var mf = src ? src.GetComponent<MeshFilter>() : null;
                if (!mf || !mf.sharedMesh) continue;

                var go = new GameObject("Decor_" + e.name);
                go.isStatic = false;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                bool decal = e.kind == "decal";
                r.sharedMaterial = e.name.Contains("Puddle") ? wet : artMats.body;
                r.shadowCastingMode = decal ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (e.blocks && e.collider != null && e.collider.Length == 3)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(e.collider[0], Mathf.Max(1f, e.collider[1]), e.collider[2]);
                    box.center = e.collider_center != null && e.collider_center.Length == 3
                        ? new Vector3(e.collider_center[0], Mathf.Max(e.collider_center[1], box.size.y * 0.5f), e.collider_center[2])
                        : new Vector3(0f, box.size.y * 0.5f, 0f);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/Decor_{e.name}.prefab");
                Object.DestroyImmediate(go);

                float sx = e.size != null && e.size.Length == 3 ? e.size[0] : 1f;
                float sz = e.size != null && e.size.Length == 3 ? e.size[2] : 1f;
                bool snap = false;
                foreach (var w in SnapWords)
                    if (e.name.Contains(w)) snap = true;
                items.Add(new DecorItem
                {
                    prefab = prefab,
                    kind = string.IsNullOrEmpty(e.kind) ? "small" : (e.kind == "obstacle" && !e.blocks ? "small" : e.kind),
                    weight = e.weight > 0f ? e.weight : 1f,
                    radius = Mathf.Max(sx, sz) * 0.5f,
                    cluster = string.IsNullOrEmpty(e.cluster) ? "street" : e.cluster,
                    freeYaw = !snap,
                });
            }
            if (items.Count == 0) return;
            var dresser = new GameObject("Decor").AddComponent<WorldDresser>();
            dresser.transform.SetParent(game.transform, false);
            dresser.items = items.ToArray();
        }

        /// <summary>The Six Towers (and the far skyline), kept north of the player deep in the fog: the menu
        /// camera sees them behind the Passeur.</summary>
        static void BuildBackdrop()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Props/Backdrop.fbx");
            if (artMats == null || !model) return;
            var root = new GameObject("Backdrop");
            root.AddComponent<Backdrop>();
            // Far away and dull, so the haze swallows them: "on devine les Six Tours" (Univers 9.5).
            var far = ArtMat("M_CordonBackdrop", (Texture2D)artMats.body.GetTexture("_BaseMap"), mat =>
            {
                mat.SetColor("_BaseColor", new Color(0.4f, 0.43f, 0.48f));
                mat.SetColor("_RimColor", Color.black);
                mat.SetFloat("_Smoothness", 0f);
                mat.SetFloat("_EmissionStrength", 0.5f);
                mat.SetFloat("_Ambient", 0.7f);
            });
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = new GameObject(mf.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = mf.transform.localPosition;
                go.transform.localRotation = mf.transform.localRotation;
                go.transform.localScale = mf.transform.localScale;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = far;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f) return f;
            }
            return null;
        }
    }
}
