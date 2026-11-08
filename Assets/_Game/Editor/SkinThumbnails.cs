using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Survivor.EditorTools
{
    /// <summary>
    /// Photographs every outfit of the Passeur model (Art/Characters/Passeur.fbx, one skinned mesh per skin) on a
    /// transparent background, into Resources/SkinThumbs/&lt;skin id&gt;.png. SkinPreview shows these in the shop
    /// (la Plaque) instead of the flat placeholder shapes. Re-run after the Passeur or a skin changes.
    /// </summary>
    public static class SkinThumbnails
    {
        const string Fbx = "Assets/_Game/Art/Characters/Passeur.fbx";
        const string Palette = "Assets/_Game/Art/Materials/M_Cordon.mat";
        const string OutDir = "Assets/_Game/Resources/SkinThumbs";
        const int W = 384, H = 480;

        [MenuItem("Survivor/Bake Skin Thumbnails")]
        public static void Bake()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Palette);
            if (!model || !mat)
            {
                Debug.LogWarning("SkinThumbnails: Passeur.fbx or M_Cordon.mat missing");
                return;
            }
            Directory.CreateDirectory(OutDir);

            var origin = new Vector3(0f, -600f, 0f);
            var go = Object.Instantiate(model, origin, Quaternion.identity);
            go.hideFlags = HideFlags.HideAndDontSave;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (go.TryGetComponent<Animator>(out var animator)) animator.enabled = false;

            // A calm idle pose (the bind pose is a T-pose).
            var clips = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            var idle = clips.FirstOrDefault(c => c.name.EndsWith("Idle")) ?? clips.FirstOrDefault();
            if (idle) idle.SampleAnimation(go, idle.length * 0.35f);
            go.transform.position = origin; // root motion in the sample would move it

            var outfits = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // Three-quarter front view, slightly from above, like a character select.
            var camGo = new GameObject("Thumb Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 40f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            Vector3 target = origin + new Vector3(0f, 0.98f, 0f);
            // The model faces -Z: the camera stands on that side, a little above and to the left.
            Vector3 dir = Quaternion.Euler(-12f, 205f, 0f) * Vector3.forward;
            cam.transform.position = target + dir * 5.5f;
            cam.transform.LookAt(target);

            var key = new GameObject("Thumb Key") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.intensity = 1.9f;
            key.transform.rotation = Quaternion.Euler(35f, 20f, 0f);
            var rim = new GameObject("Thumb Rim") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.55f, 0.72f, 1f);
            rim.intensity = 1.3f;
            rim.transform.rotation = Quaternion.Euler(15f, 160f, 0f);

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            int count = 0;
            try
            {
                foreach (var skin in Cosmetics.Skins)
                {
                    string want = PlayerSkin.OutfitName(skin.id);
                    var shown = outfits.FirstOrDefault(o => o.name == want);
                    if (!shown) continue;
                    foreach (var o in outfits) o.gameObject.SetActive(o == shown);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    string id = string.IsNullOrEmpty(skin.id) ? "skin_default" : skin.id;
                    File.WriteAllBytes($"{OutDir}/{id}.png", tex.EncodeToPNG());
                    count++;
                }
            }
            finally
            {
                RenderSettings.fog = fog;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(key.gameObject);
                Object.DestroyImmediate(rim.gameObject);
                Object.DestroyImmediate(go);
            }

            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(OutDir, "*.png"))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 512;
                imp.SaveAndReimport();
            }
            Debug.Log($"SkinThumbnails: {count} outfits photographed into {OutDir}");
        }
    }
}
