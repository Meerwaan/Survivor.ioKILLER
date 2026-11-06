using UnityEngine;

namespace Survivor.EditorTools
{
    /// <summary>Weapon setup on the player, kept apart so weapon work doesn't touch the rest of the builder.</summary>
    public static partial class PrototypeBuilder
    {
        /// <summary>Adds every weapon component to the player. Returns the default starting weapon.</summary>
        static Weapon AddWeapons(GameObject player, Transform gasVisual, Mats m, Prefabs p)
        {
            var revolver = player.AddComponent<RevolverWeapon>();
            revolver.displayName = "Revolver";
            revolver.color = new Color(1f, 0.8f, 0.4f);
            revolver.bulletPrefab = p.bullet;

            var shotgun = player.AddComponent<ShotgunWeapon>();
            shotgun.displayName = "Fusil à pompe";
            shotgun.color = new Color(1f, 0.55f, 0.3f);
            shotgun.pelletPrefab = p.pellet;

            var machete = player.AddComponent<MacheteWeapon>();
            machete.displayName = "Machettes";
            machete.color = new Color(0.8f, 0.85f, 0.95f);
            machete.bladePrefab = p.blade;

            var molotov = player.AddComponent<MolotovWeapon>();
            molotov.displayName = "Cocktail Molotov";
            molotov.color = new Color(1f, 0.45f, 0.1f);
            molotov.bottlePrefab = p.bottle;
            molotov.firePrefab = p.fire;

            // Fair play: every weapon is available from the start (unlockCost stays 0).
            var gasWeapon = player.AddComponent<GasWeapon>();
            gasWeapon.displayName = "Gaz toxique";
            gasWeapon.color = new Color(0.5f, 0.95f, 0.3f);
            gasWeapon.visual = gasVisual;
            gasWeapon.unlockCost = 0;

            // Poison + Balle bridge (2nd poison carrier). Reuses the bullet prefab.
            var darts = player.AddComponent<DartWeapon>();
            darts.displayName = "Fléchettes";
            darts.color = new Color(0.55f, 1f, 0.6f);
            darts.bulletPrefab = p.bullet;
            darts.unlockCost = 0;

            var flamer = player.AddComponent<FlamethrowerWeapon>();
            flamer.displayName = "Lance-flammes";
            flamer.color = new Color(1f, 0.4f, 0.15f);
            flamer.puffPrefab = FlamePuffPrefab(m);
            flamer.unlockCost = 0;

            var tesla = player.AddComponent<TeslaWeapon>();
            tesla.displayName = "Batterie Tesla";
            tesla.color = new Color(0.45f, 0.75f, 1f);
            tesla.unlockCost = 0;

            var grenades = player.AddComponent<GrenadeWeapon>();
            grenades.displayName = "Grenades";
            grenades.color = new Color(0.78f, 0.72f, 0.45f);
            grenades.grenadePrefab = p.grenade;
            grenades.unlockCost = 0;

            var saws = player.AddComponent<SawWeapon>();
            saws.displayName = "Scie circulaire";
            saws.color = new Color(1f, 0.35f, 0.4f);
            saws.sawPrefab = p.saw;
            saws.unlockCost = 0;

            return revolver;
        }

        /// <summary>Flamethrower blob: see-through orange shell around a glowing core.</summary>
        static GameObject FlamePuffPrefab(Mats m)
        {
            var core = Unlit("M_FlameCore", new Color(2.4f, 1.1f, 0.25f));
            var go = new GameObject("FlamePuff");
            Part(PrimitiveType.Sphere, "Shell", go.transform, Vector3.zero, Vector3.one, m.fire, Quaternion.identity, false);
            Part(PrimitiveType.Sphere, "Core", go.transform, Vector3.zero, Vector3.one * 0.5f, core, Quaternion.identity, false);
            go.AddComponent<FlamePuff>();
            return Save(go, "FlamePuff");
        }
    }
}
