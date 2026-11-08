using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Survivor.EditorTools
{
    /// <summary>
    /// CORDON art pass, the small things: éclats (XP), pickups, thrown weapons, enemy projectiles and the
    /// barrels / REGEN crates. The greybox prefabs are built first, then dressed here with the Blender meshes
    /// (Assets/_Game/Art/Props/*.fbx) when they exist: gameplay components and sizes stay the same.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        static Material glowMat;

        static Mesh PropMesh(string fbx, string obj)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/Props/{fbx}.fbx");
            if (!model) return null;
            var t = FindDeep(model.transform, obj);
            var mf = t ? t.GetComponent<MeshFilter>() : null;
            return mf ? mf.sharedMesh : null;
        }

        static GameObject MeshChild(Transform parent, string name, Mesh mesh, Material mat, bool shadows, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // kit meshes arrive facing -Z (see PlaceModel)
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        static bool NameMatches(string name, string pattern) =>
            pattern.EndsWith("*") ? name.StartsWith(pattern.TrimEnd('*')) : name == pattern;

        /// <summary>Opens a prefab, removes the greybox meshes, lets <paramref name="dress"/> add the model, saves.</summary>
        static void Dress(GameObject prefab, System.Action<GameObject> dress, params string[] removeChildren)
        {
            if (!prefab) return;
            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            var doomed = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root.transform && removeChildren.Any(pat => NameMatches(t.name, pat)))
                .ToArray();
            foreach (var t in doomed)
                if (t) Object.DestroyImmediate(t.gameObject);
            dress(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void ArtOverlays(Prefabs p)
        {
            if (artMats == null) return;
            var palette = (Texture2D)artMats.body.GetTexture("_BaseMap");
            glowMat = ArtMat("M_CordonGlow", palette, m =>
            {
                m.SetFloat("_EmissionStrength", 3.2f);
                m.SetFloat("_RimPower", 2.5f);
            });

            // Éclats: amber resin, three sizes and three glows (Univers 6.1).
            var e1 = PropMesh("Pickups", "Eclat_1");
            var e2 = PropMesh("Pickups", "Eclat_2");
            var e3 = PropMesh("Pickups", "Eclat_3");
            if (e1 && e2 && e3)
                Dress(p.gem, root =>
                {
                    var xp = root.GetComponent<XPGem>();
                    var c = root.transform.Find("Crystal");
                    c.localRotation = Quaternion.identity;
                    c.localScale = Vector3.one * 1.3f; // real size, a bit oversized to read on a phone (root stays at 1)
                    c.GetComponent<MeshFilter>().sharedMesh = e1;
                    xp.tierMeshes = new[] { e1, e2, e3 };
                    xp.tierMaterials = new[] { artMats.gem1, artMats.gem2, artMats.gem3 };
                    c.GetComponent<Renderer>().sharedMaterial = artMats.gem1;
                });

            DressPickup(p.health, "Ration");
            DressPickup(p.magnet, "Collecteur");
            DressPickup(p.chest, "CaisseLargage");
            DressPickup(p.coin, "Or");
            DressPickup(p.rewardChest, "Cache");

            // Thrown weapons keep their trails and scripts.
            DressMesh(p.blade, "Weapons", "Machete", artMats.body, "Blade", "Handle");
            DressMesh(p.bottle, "Weapons", "MolotovBottle", glowMat, "Glass", "Rag");
            DressMesh(p.grenade, "Weapons", "Grenade", artMats.body, "Body", "Pin");
            DressMesh(p.saw, "Weapons", "SawBlade", artMats.body, "Disc", "Tooth*", "Hub");

            // Enemy shots: the Cracheur's sap glob, the Souche's crust shards (one prefab each now).
            p.spit = ShotPrefab("EnemySpit", PropMesh("Weapons", "SpitGlob"), 0.25f);
            p.shard = ShotPrefab("BossShard", PropMesh("Weapons", "CrustShard"), 0.25f);

            // Barrels (fuel or REGEN reagent, one per spawn) and REGEN supply crates.
            var fuel = PropMesh("Props", "Baril_Fuel");
            var regen = PropMesh("Props", "Baril_Regen");
            if (fuel && regen)
                Dress(p.barrel, root =>
                {
                    var visual = root.transform.Find("Visual");
                    var a = MeshChild(visual, "Baril_Fuel", fuel, artMats.body, true).GetComponent<Renderer>();
                    var b = MeshChild(visual, "Baril_Regen", regen, artMats.body, true).GetComponent<Renderer>();
                    var enemy = root.GetComponent<Enemy>();
                    enemy.bodyRenderer = a;
                    enemy.flashMaterial = artMats.flash;
                    enemy.gibColor = new Color(0.45f, 0.2f, 0.14f);
                    var ca = root.GetComponent<CharacterAnimator>();
                    if (!ca) ca = root.AddComponent<CharacterAnimator>();
                    ca.variants = new[] { a, b };
                    enemy.anim = ca;
                }, "Body", "Band", "Lid");
            var crate = PropMesh("Props", "CaisseRegen");
            if (crate)
                Dress(p.crate, root =>
                {
                    var visual = root.transform.Find("Visual");
                    var r = MeshChild(visual, "CaisseRegen", crate, artMats.body, true).GetComponent<Renderer>();
                    var enemy = root.GetComponent<Enemy>();
                    enemy.bodyRenderer = r;
                    enemy.flashMaterial = artMats.flash;
                    enemy.gibColor = new Color(0.85f, 0.88f, 0.9f);
                }, "Body", "Plank", "Cross");
        }

        static void DressPickup(GameObject prefab, string obj)
        {
            var mesh = PropMesh("Pickups", obj);
            if (!mesh) return;
            Dress(prefab, root => MeshChild(root.transform, "Mesh", mesh, glowMat, true), "Mesh", "Lid");
        }

        static void DressMesh(GameObject prefab, string fbx, string obj, Material mat, params string[] remove)
        {
            var mesh = PropMesh(fbx, obj);
            if (!mesh) return;
            Dress(prefab, root => MeshChild(root.transform, "Mesh", mesh, mat, true), remove);
        }

        static GameObject ShotPrefab(string name, Mesh mesh, float radius)
        {
            if (!mesh) return null;
            var go = new GameObject(name);
            MeshChild(go.transform, "Mesh", mesh, glowMat, false);
            go.AddComponent<EnemyProjectile>().radius = radius;
            return Save(go, name);
        }
    }
}
