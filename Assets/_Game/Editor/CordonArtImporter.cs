using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Survivor.EditorTools
{
    /// <summary>
    /// Import rules for everything exported by the Blender kit (Tools/Blender) into Assets/_Game/Art:
    /// - Characters/*.fbx: Generic rig, optimized hierarchy, clips renamed ("Rig|Walk" -> "Walk") and
    ///   looped when their name says so (Walk, Run, Idle, Loop...).
    /// - Props/*.fbx and the rest: static meshes, no rig, no animation.
    /// - No materials are imported: the builder assigns the shared palette material.
    /// - T_Palette.png: sRGB color, alpha kept as DATA (1 - emission mask), never as transparency.
    /// </summary>
    public class CordonArtImporter : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/_Game/Art/";
        static readonly string[] LoopWords = { "Walk", "Run", "Idle", "Loop", "Crawl", "Fly", "Spin", "Swell", "Hover", "Charge" };

        /// <summary>Bones kept as real transforms after optimization, so gameplay can attach things to them.</summary>
        static readonly string[] ExposedBones = { "Hand.R", "Hand.L", "LowerArm.L", "Head", "Chest" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.bakeAxisConversion = true;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.meshCompression = ModelImporterMeshCompression.Low;
            mi.isReadable = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;

            bool character = assetPath.Contains("/Characters/");
            if (character)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.resampleCurves = true;
                mi.skinWeights = ModelImporterSkinWeights.Standard;
                // The hero keeps his bones (the injector's vial is scaled by code, only one of him on screen);
                // enemies are optimized (hundreds of them).
                bool hero = System.IO.Path.GetFileNameWithoutExtension(assetPath).StartsWith("Passeur");
                mi.optimizeGameObjects = !hero;
                mi.extraExposedTransformPaths = hero ? new string[0] : ExposedPaths(mi);
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.optimizeGameObjects = false;
            }
        }

        /// <summary>Full hierarchy paths of the bones we want to keep (found from the previous import, if any).</summary>
        static string[] ExposedPaths(ModelImporter mi)
        {
            var paths = new List<string>();
            foreach (var p in mi.transformPaths)
            {
                int slash = p.LastIndexOf('/');
                string leaf = slash >= 0 ? p.Substring(slash + 1) : p;
                if (System.Array.IndexOf(ExposedBones, leaf) >= 0) paths.Add(p);
            }
            return paths.ToArray();
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(ArtRoot) || !assetPath.Contains("/Characters/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                c.name = n;
                bool loop = false;
                foreach (var w in LoopWords)
                    if (n.Contains(w)) loop = true;
                c.loopTime = loop;
                c.loopPose = false;
                c.lockRootRotation = true;
                c.lockRootHeightY = false;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var ti = (TextureImporter)assetImporter;
            if (System.IO.Path.GetFileName(assetPath).StartsWith("T_Ground"))
            {
                // Tiling ground: color in RGB, smoothness (puddles) in A.
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (!assetPath.EndsWith("T_Palette.png")) return;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false; // alpha is the emission mask: never dilate colors into it
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
