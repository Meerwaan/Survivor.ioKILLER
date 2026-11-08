using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Survivor.EditorTools
{
    /// <summary>
    /// Menu "Survivor > Build Prototype Scene": (re)creates the greybox game in one click.
    /// Materials are only created if missing, so Inspector tweaks survive a rebuild.
    /// Prefabs, the Game scene and enemy balance numbers are (re)applied every time.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string Root = "Assets/_Game";
        const string ScenePath = Root + "/Scenes/Game.unity";

        class Mats
        {
            public Material ground, player, gun, zombie, runner, brute, spitter, boss, eyes, flash, hurt;
            public Material bullet, enemyBullet, gemBlue, gemGreen, gemRed, blade, gas, bottle, fire;
            public Material meat, magnet, chest, splat, bloodParticle;
            public Material elite, coin, grenade, saw;
            public Material fxRing, fxGlow, fxParticle, fxLine, gib;
            public Material rat, bloater, pustule, splitter, thief, sack, golden, barrel, crate, crateDark;
        }

        class Prefabs
        {
            public GameObject zombie, runner, brute, spitter, boss;
            public GameObject bullet, pellet, enemyBullet, blade, bottle, fire;
            public GameObject gem, health, magnet, chest, splat, coin;
            public GameObject grenade, saw;
            public GameObject rat, bloater, splitter, splitterSmall, thief, golden, barrel, crate, rewardChest;
            public GameObject spit, shard; // CORDON: the Cracheur's sap glob, the Souche's crust shards
        }

        /// <summary>Every enemy type (data assets), handed to the scene builder.</summary>
        class Roster
        {
            public EnemyData zombie, runner, brute, spitter, boss;
            public EnemyData rat, bloater, splitter, splitterSmall, thief, golden, barrel, crate;
        }

        [MenuItem("Survivor/Build Prototype Scene")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(); // picks up models just exported by the Blender scripts
            var m = CreateMaterials();
            artMats = CreateArtMaterials();
            if (artMats != null) PrepareTintedGore(m);
            var p = CreatePrefabs(m);

            var zombie = EnemyAsset("Zombie", p.zombie, d =>
            {
                Stats(d, hp: 9, speed: 2.3f, damage: 6, xp: 1, score: 10, radius: 0.4f, blood: 18);
                d.displayName = "Ambré";
                Approach(d, flank: 0.25f, lead: 0f, swing: 0.15f);
            });
            var runner = EnemyAsset("Runner", p.runner, d =>
            {
                Stats(d, hp: 7, speed: 4.4f, damage: 5, xp: 1, score: 12, radius: 0.32f, blood: 14);
                d.displayName = "Fiévreux";
                Approach(d, flank: 0.7f, lead: 0.5f, swing: 0.12f);
            });
            var brute = EnemyAsset("Brute", p.brute, d =>
            {
                Stats(d, hp: 70, speed: 1.7f, damage: 15, xp: 6, score: 40, radius: 0.65f, blood: 40);
                d.displayName = "Masse";
                d.knockbackResist = 0.8f;
                // A telegraphed ground slam (circle in front, 0.55 s), then rooted: see Enemy.StartSwing.
                Approach(d, flank: 0f, lead: 0f, swing: 0.55f);
                d.slamRadius = 1.6f;
            });
            var spitter = EnemyAsset("Spitter", p.spitter, d =>
            {
                Stats(d, hp: 16, speed: 2.0f, damage: 6, xp: 2, score: 20, radius: 0.36f, blood: 20);
                d.displayName = "Cracheur";
                d.behaviour = EnemyBehaviour.Ranged;
                d.projectileDamage = 9f;
                d.projectileSpeed = 7f;
                d.fireInterval = 3f;
                d.preferredRange = 5f; // close enough to stay on a narrow portrait screen
                Approach(d, flank: 0.3f, lead: 0f, swing: 0.15f);
            });
            var boss = EnemyAsset("Boss", p.boss, d =>
            {
                Stats(d, hp: 900, speed: 2.0f, damage: 22, xp: 60, score: 500, radius: 1f, blood: 120);
                d.displayName = "Boucher";
                d.behaviour = EnemyBehaviour.Boss;
                d.attackInterval = 1f;
                d.knockbackResist = 1f;
                d.projectileDamage = 12f;
                d.projectileSpeed = 6.5f;
                d.fireInterval = 2.8f;
                d.ringCount = 16;
                Approach(d, flank: 0f, lead: 0f, swing: 0.2f);
            });
            // Shooters always point at the current projectile prefab.
            spitter.projectilePrefab = p.spit ? p.spit : p.enemyBullet;
            boss.projectilePrefab = p.shard ? p.shard : p.enemyBullet;
            EditorUtility.SetDirty(spitter);
            EditorUtility.SetDirty(boss);

            var r = new Roster { zombie = zombie, runner = runner, brute = brute, spitter = spitter, boss = boss };
            CreateNewEnemies(p, r);

            var postFx = CreatePostProfile();
            BuildScene(m, p, postFx, r);

            ConfigureOrientation();
            PlayerSettings.runInBackground = true; // keeps Play mode running while the editor is unfocused
            AssetDatabase.SaveAssets();
            RefreshArtAssets();
            Debug.Log("[Survivor] Prototype scene built: " + ScenePath);

            SetPortraitGameView();
        }

        /// <summary>Portrait first, but the phone may rotate to landscape (camera and UI adapt).</summary>
        static void ConfigureOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        const string PortraitLabel = "Survivor Portrait";

        /// <summary>
        /// Switches the editor's Game view to 1080x1920 so Play mode looks like a phone held upright.
        /// Uses internal editor APIs, so it fails gracefully if Unity changes them.
        /// </summary>
        [MenuItem("Survivor/Game View Portrait (1080x1920)")]
        public static void SetPortraitGameView()
        {
            const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance", Any).GetValue(null);
                var groupType = sizesType.GetProperty("currentGroupType", Any).GetValue(sizes);
                var group = sizesType.GetMethod("GetGroup", Any).Invoke(sizes, new[] { groupType });
                var groupClass = group.GetType();

                int FindIndex()
                {
                    var texts = (string[])groupClass.GetMethod("GetDisplayTexts", Any).Invoke(group, null);
                    return Array.FindIndex(texts, t => t.StartsWith(PortraitLabel));
                }

                int index = FindIndex();
                if (index < 0)
                {
                    var sizeClass = asm.GetType("UnityEditor.GameViewSize");
                    var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeClass.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { Enum.ToObject(sizeKind, 1), 1080, 1920, PortraitLabel }); // 1 = fixed resolution
                    groupClass.GetMethod("AddCustomSize", Any).Invoke(group, new[] { size });
                    index = FindIndex();
                }

                var gameViewType = asm.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType, false, null, false);
                var prop = gameViewType.GetProperty("selectedSizeIndex", Any);
                if (prop != null) prop.SetValue(window, index);
                else gameViewType.GetMethod("SizeSelectionCallback", Any).Invoke(window, new object[] { index, null });
                window.Repaint();
                Debug.Log("[Survivor] Game view set to portrait 1080x1920.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Survivor] Couldn't switch the Game view to portrait automatically (" + e.Message +
                                 "). Pick a 1080x1920 size in the Game view's resolution menu instead.");
            }
        }

        /// <summary>Kamikazes, rats, splitters, the thief, golden zombies and the arena props.</summary>
        static void CreateNewEnemies(Prefabs p, Roster r)
        {
            r.rat = EnemyAsset("Rat", p.rat, d =>
            {
                Stats(d, hp: 1, speed: 5f, damage: 2, xp: 0.35f, score: 3, radius: 0.25f, blood: 6);
                d.displayName = "Nuée";
                d.attackInterval = 0.6f;
                d.canBeElite = false;
                Approach(d, flank: 0.5f, lead: 0.3f, swing: 0.05f);
            });
            r.bloater = EnemyAsset("Bloater", p.bloater, d =>
            {
                Stats(d, hp: 18, speed: 1.9f, damage: 22, xp: 2, score: 25, radius: 0.6f, blood: 40);
                d.displayName = "Kyste";
                d.behaviour = EnemyBehaviour.Kamikaze;
                d.fuseRange = 2.2f;
                d.fuseTime = 0.75f;
                d.deathExplosionRadius = 3.2f;
                d.deathExplosionDamage = 45f;
                d.explosionColor = new Color(0.9f, 0.56f, 0.17f); // amber #E58E2B: "explosion d'ambre" (Univers 9.9)
                d.knockbackResist = 0.3f;
                d.deathSound = SfxId.Gore;
                Approach(d, flank: 0.3f, lead: 0f, swing: 0.15f);
            });
            r.splitterSmall = EnemyAsset("SplitterSmall", p.splitterSmall, d =>
            {
                Stats(d, hp: 6, speed: 3.4f, damage: 4, xp: 0.5f, score: 5, radius: 0.28f, blood: 10);
                d.displayName = "Rejeton";
                d.canBeElite = false;
                Approach(d, flank: 1f, lead: 0f, swing: 0.08f);
            });
            r.splitter = EnemyAsset("Splitter", p.splitter, d =>
            {
                Stats(d, hp: 30, speed: 2f, damage: 9, xp: 2, score: 25, radius: 0.48f, blood: 30);
                d.displayName = "Colonie";
                d.splitInto = r.splitterSmall;
                d.splitCount = 3;
                d.deathSound = SfxId.Gore;
                Approach(d, flank: 0.35f, lead: 0f, swing: 0.15f);
            });
            r.thief = EnemyAsset("Thief", p.thief, d =>
            {
                Stats(d, hp: 60, speed: 4.3f, damage: 0, xp: 4, score: 100, radius: 0.4f, blood: 25);
                d.displayName = "Pilleur";
                d.behaviour = EnemyBehaviour.Thief;
                d.escapeTime = 12f;
                d.loot = LootTable.Thief;
                d.canBeElite = false;
                d.knockbackResist = 0.6f;
            });
            r.golden = EnemyAsset("Golden", p.golden, d =>
            {
                // Slow and evasive: the player hunts it (Bestiaire 3.1), it doesn't come for him (was 3.6 m/s, chasing).
                Stats(d, hp: 12, speed: 2.2f, damage: 5, xp: 4, score: 30, radius: 0.4f, blood: 18);
                d.displayName = "Cristallisé";
                d.loot = LootTable.Golden;
                d.canBeElite = false;
                d.evasive = true;
                Approach(d, flank: 0f, lead: 0f, swing: 0.15f);
            });
            r.barrel = EnemyAsset("Barrel", p.barrel, d =>
            {
                Stats(d, hp: 1, speed: 0f, damage: 0, xp: 0, score: 0, radius: 0.45f, blood: 0);
                d.displayName = "Baril";
                d.behaviour = EnemyBehaviour.Prop;
                d.knockbackResist = 1f;
                d.deathExplosionRadius = 3.8f;
                d.deathExplosionDamage = 60f;
                d.explosionColor = new Color(1f, 0.5f, 0.12f);
                d.loot = LootTable.None;
                d.bleeds = false;
                d.canBeElite = false;
                d.hitSound = SfxId.Metal;
                d.deathSound = SfxId.Metal;
            });
            r.crate = EnemyAsset("Crate", p.crate, d =>
            {
                Stats(d, hp: 25, speed: 0f, damage: 0, xp: 1, score: 0, radius: 0.5f, blood: 0);
                d.displayName = "Caisse REGEN";
                d.behaviour = EnemyBehaviour.Prop;
                d.knockbackResist = 1f;
                d.loot = LootTable.Crate;
                d.bleeds = false;
                d.canBeElite = false;
                d.hitSound = SfxId.Break;
                d.deathSound = SfxId.Break;
            });
        }

        static void Stats(EnemyData d, float hp, float speed, float damage, float xp, int score, float radius, int blood)
        {
            d.maxHealth = hp;
            d.moveSpeed = speed;
            d.damage = damage;
            d.xp = xp;
            d.score = score;
            d.hitRadius = radius;
            d.bloodAmount = blood;
            // Approach / melee defaults (EnemyData tooltips); each enemy overrides what it needs with Approach().
            d.flank = 0.3f;
            d.lead = 0f;
            d.swingTime = 0.15f;
            d.slamRadius = 0f;
            d.evasive = false;
        }

        /// <summary>How it closes in (arc width, intercept) and how long its swing winds up before the hit lands.</summary>
        static void Approach(EnemyData d, float flank, float lead, float swing)
        {
            d.flank = flank;
            d.lead = lead;
            d.swingTime = swing;
        }

        // ---------- Folders ----------

        static void EnsureFolders()
        {
            foreach (var sub in new[] { "Materials", "Prefabs", "Data", "Scenes", "Textures", "Settings" })
            {
                if (!AssetDatabase.IsValidFolder($"{Root}/{sub}"))
                    AssetDatabase.CreateFolder(Root, sub);
            }
        }

        // ---------- Materials ----------

        static Mats CreateMaterials()
        {
            var m = new Mats
            {
                ground = Lit("M_Ground", new Color(0.9f, 0.86f, 0.8f), 0.1f),
                player = Lit("M_Player", new Color(0.72f, 0.76f, 0.84f), 0.4f),
                gun = Lit("M_PlayerGun", new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.8f),
                zombie = Lit("M_Zombie", new Color(0.42f, 0.5f, 0.35f), 0.15f),
                runner = Lit("M_Runner", new Color(0.55f, 0.17f, 0.14f), 0.25f),
                brute = Lit("M_Brute", new Color(0.33f, 0.24f, 0.28f), 0.2f),
                spitter = Lit("M_Spitter", new Color(0.62f, 0.6f, 0.25f), 0.3f),
                boss = Lit("M_Boss", new Color(0.25f, 0.05f, 0.05f), 0.35f),
                eyes = Unlit("M_Eyes", new Color(1f, 0.35f, 0.1f)),
                flash = Lit("M_Flash", Color.white, 0f, 0f, new Color(0.9f, 0.9f, 0.9f)),
                hurt = Lit("M_Hurt", new Color(1f, 0.2f, 0.2f), 0f, 0f, new Color(0.8f, 0.05f, 0.05f)),
                bullet = Unlit("M_Bullet", new Color(1f, 0.85f, 0.4f)),
                enemyBullet = Unlit("M_EnemyBullet", new Color(0.6f, 1f, 0.2f)),
                gemBlue = Lit("M_Gem", new Color(0.2f, 0.8f, 1f), 0.8f, 0f, new Color(0.1f, 0.7f, 1.5f)),
                gemGreen = Lit("M_GemGreen", new Color(0.3f, 1f, 0.3f), 0.8f, 0f, new Color(0.2f, 1.2f, 0.2f)),
                gemRed = Lit("M_GemRed", new Color(1f, 0.25f, 0.25f), 0.8f, 0f, new Color(1.5f, 0.15f, 0.1f)),
                blade = Lit("M_Blade", new Color(0.72f, 0.72f, 0.74f), 0.75f, 0.9f),
                gas = TransparentUnlit("M_Gas", new Color(0.4f, 0.95f, 0.2f, 0.22f)),
                bottle = Lit("M_Bottle", new Color(0.25f, 0.45f, 0.2f), 0.9f),
                fire = TransparentUnlit("M_Fire", new Color(1f, 0.45f, 0.05f, 0.45f)),
                meat = Lit("M_Meat", new Color(0.75f, 0.2f, 0.25f), 0.5f),
                magnet = Lit("M_Magnet", new Color(0.3f, 0.5f, 0.9f), 0.6f, 0.5f, new Color(0.1f, 0.3f, 0.9f)),
                chest = Lit("M_Chest", new Color(0.85f, 0.6f, 0.15f), 0.6f, 0.6f, new Color(0.6f, 0.4f, 0.05f)),
                splat = Unlit("M_BloodSplat", new Color(0.28f, 0.01f, 0.01f)),
                bloodParticle = Particle("M_BloodParticle", new Color(0.65f, 0.03f, 0.02f)),
                elite = Lit("M_Elite", new Color(0.9f, 0.7f, 0.2f), 0.6f, 0.4f, new Color(1.2f, 0.7f, 0.1f)),
                coin = Lit("M_Coin", new Color(1f, 0.8f, 0.2f), 0.8f, 1f, new Color(0.8f, 0.55f, 0.05f)),
                grenade = Lit("M_Grenade", new Color(0.3f, 0.35f, 0.18f), 0.4f, 0.5f),
                saw = Lit("M_Saw", new Color(0.85f, 0.85f, 0.88f), 0.5f, 0.3f, new Color(0.18f, 0.18f, 0.2f)),
                fxRing = Additive("M_FX_Ring", "Universal Render Pipeline/Unlit", CreateFXTexture("T_FX_Ring", true)),
                fxGlow = Additive("M_FX_Glow", "Universal Render Pipeline/Unlit", null),
                fxParticle = Additive("M_FX_Particle", "Universal Render Pipeline/Particles/Unlit", CreateFXTexture("T_FX_SoftDot", false)),
                fxLine = Additive("M_FX_Line", "Universal Render Pipeline/Particles/Unlit", null),
                gib = LoadOrCreate("M_Gib", "Universal Render Pipeline/Particles/Lit", out _),
                rat = Lit("M_Rat", new Color(0.32f, 0.27f, 0.25f), 0.2f),
                bloater = Lit("M_Bloater", new Color(0.55f, 0.6f, 0.25f), 0.65f, 0f, new Color(0.08f, 0.1f, 0f)),
                pustule = Lit("M_Pustule", new Color(0.9f, 0.95f, 0.3f), 0.8f, 0f, new Color(0.8f, 1f, 0.1f)),
                splitter = Lit("M_Splitter", new Color(0.45f, 0.2f, 0.45f), 0.4f),
                thief = Lit("M_Thief", new Color(0.18f, 0.16f, 0.2f), 0.2f),
                sack = Lit("M_LootSack", new Color(0.7f, 0.5f, 0.15f), 0.5f, 0.3f, new Color(1.2f, 0.8f, 0.1f)),
                golden = Lit("M_Golden", new Color(1f, 0.78f, 0.25f), 0.8f, 0.8f, new Color(0.9f, 0.6f, 0.08f)),
                barrel = Lit("M_Barrel", new Color(0.75f, 0.12f, 0.08f), 0.5f, 0.4f),
                crate = Lit("M_Crate", new Color(0.55f, 0.38f, 0.2f), 0.1f),
                crateDark = Lit("M_CrateDark", new Color(0.32f, 0.2f, 0.1f), 0.1f),
            };

            m.ground.SetTexture("_BaseMap", CreateGroundTexture());
            m.ground.SetTextureScale("_BaseMap", new Vector2(100f, 100f));
            EditorUtility.SetDirty(m.ground);
            CreateFXMaterials(m); // soft FX look (PrototypeBuilder.FX.cs), re-applied every build
            return m;
        }

        static Material LoadOrCreate(string name, string shader, out bool created)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            created = mat == null;
            if (created)
            {
                mat = new Material(Shader.Find(shader)) { enableInstancing = true };
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        static Material Lit(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            var mat = LoadOrCreate(name, "Universal Render Pipeline/Lit", out bool created);
            if (!created) return mat;

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Unlit(string name, Color color)
        {
            var mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit", out bool created);
            if (!created) return mat;
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material TransparentUnlit(string name, Color color)
        {
            var mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit", out bool created);
            if (!created) return mat;

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Glowing see-through material (adds light). Colors above 1 bloom.</summary>
        static Material Additive(string name, string shader, Texture2D texture)
        {
            var mat = LoadOrCreate(name, shader, out bool created);
            if (!created) return mat;

            mat.SetColor("_BaseColor", Color.white);
            if (texture) mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Soft white dot or soft ring on transparent, generated once as a PNG.</summary>
        static Texture2D CreateFXTexture(string name, bool ring)
        {
            string path = $"{Root}/Textures/{name}.png";
            if (!File.Exists(path))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.16f) + Mathf.Clamp01(1f - d) * 0.12f
                        : Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    if (d > 1f) a = 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Particle(string name, Color color)
        {
            var mat = LoadOrCreate(name, "Universal Render Pipeline/Particles/Unlit", out bool created);
            if (!created) return mat;
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Dirty concrete slabs, generated once as a PNG.</summary>
        static Texture2D CreateGroundTexture()
        {
            string path = $"{Root}/Textures/T_Ground.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                var rng = new System.Random(7);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.5f
                              + Mathf.PerlinNoise(x * 0.15f + 50f, y * 0.15f + 50f) * 0.25f;
                    float v = 0.36f + n * 0.2f + (float)rng.NextDouble() * 0.04f;
                    if (Mathf.PerlinNoise(x * 0.02f + 100f, y * 0.02f + 100f) > 0.62f) v *= 0.75f; // stains
                    if (x < 3 || y < 3) v *= 0.55f; // slab seams
                    tex.SetPixel(x, y, new Color(v * 1.05f, v * 0.96f, v * 0.86f));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Prefabs ----------

        static Prefabs CreatePrefabs(Mats m)
        {
            var p = new Prefabs();

            // Pickups
            var gem = new GameObject("XPGem");
            var crystal = Part(PrimitiveType.Cube, "Crystal", gem.transform, Vector3.zero, Vector3.one * 0.6f, m.gemBlue, Quaternion.Euler(45f, 0f, 45f), false);
            var xp = gem.AddComponent<XPGem>();
            xp.crystal = crystal.GetComponent<Renderer>();
            xp.tierMaterials = new[] { m.gemBlue, m.gemGreen, m.gemRed };
            p.gem = Save(gem, "XPGem");

            p.health = PickupPrefab("Pickup_Meat", PickupType.Health, PrimitiveType.Cube, new Vector3(0.55f, 0.35f, 0.4f), m.meat);
            p.magnet = PickupPrefab("Pickup_Magnet", PickupType.Magnet, PrimitiveType.Cylinder, new Vector3(0.6f, 0.12f, 0.6f), m.magnet);
            p.chest = PickupPrefab("Pickup_Chest", PickupType.Chest, PrimitiveType.Cube, new Vector3(1.1f, 0.75f, 0.75f), m.chest);

            var coin = new GameObject("Pickup_Coin");
            Part(PrimitiveType.Cylinder, "Mesh", coin.transform, Vector3.zero, new Vector3(0.45f, 0.04f, 0.45f), m.coin, Quaternion.Euler(90f, 0f, 0f), false);
            coin.AddComponent<Pickup>().type = PickupType.Coin;
            p.coin = Save(coin, "Pickup_Coin");

            // Enemies
            // CORDON models when they exist (Assets/_Game/Art), greybox otherwise.
            p.zombie = ArtEnemy("Enemy_Zombie", "Ambre", m, 1f, 0.4f, 2f, 2.1f, Gore.Ichor) ?? EnemyPrefab("Enemy_Zombie", m.zombie, m, 1f);
            p.runner = EnemyPrefab("Enemy_Runner", m.runner, m, 0.8f);
            p.brute = EnemyPrefab("Enemy_Brute", m.brute, m, 1.6f);
            p.spitter = EnemyPrefab("Enemy_Spitter", m.spitter, m, 0.9f);
            p.boss = EnemyPrefab("Enemy_Boss", m.boss, m, 2.5f);

            var enemyBullet = new GameObject("EnemyBullet");
            Part(PrimitiveType.Sphere, "Mesh", enemyBullet.transform, Vector3.zero, Vector3.one * 0.4f, m.enemyBullet, Quaternion.identity, false);
            enemyBullet.AddComponent<EnemyProjectile>();
            p.enemyBullet = Save(enemyBullet, "EnemyBullet");

            // Player weapons
            // Tracers: slim bright HDR core + soft beam trail.
            p.bullet = ProjectilePrefab("Bullet", new Vector3(0.12f, 0.12f, 0.6f), m.bullet, 0.3f, m.fxLine, 0.09f, 0.2f);
            p.pellet = ProjectilePrefab("Pellet", new Vector3(0.1f, 0.1f, 0.34f), m.bullet, 0.25f, m.fxLine, 0.06f, 0.13f);

            var blade = new GameObject("Machete");
            Part(PrimitiveType.Cube, "Blade", blade.transform, new Vector3(0f, 0f, 0.1f), new Vector3(0.12f, 0.06f, 1.1f), m.blade, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Handle", blade.transform, new Vector3(0f, 0f, -0.6f), new Vector3(0.14f, 0.1f, 0.35f), m.gun, Quaternion.identity, true);
            AddTrail(blade, m.fxLine, 0.14f, 0.45f, new Color(0.9f, 0.9f, 1f, 0.55f));
            p.blade = Save(blade, "Machete");

            var grenade = new GameObject("Grenade");
            Part(PrimitiveType.Sphere, "Body", grenade.transform, Vector3.zero, new Vector3(0.32f, 0.38f, 0.32f), m.grenade, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Pin", grenade.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.1f, 0.1f), m.gun, Quaternion.identity, false);
            var throwArc = grenade.AddComponent<MolotovBottle>();
            throwArc.arcHeight = 3.5f;
            throwArc.spinSpeed = 720f;
            AddTrail(grenade, m.fxLine, 0.2f, 0.12f, new Color(1f, 0.7f, 0.3f, 0.5f));
            p.grenade = Save(grenade, "Grenade");

            var saw = new GameObject("SawBlade");
            Part(PrimitiveType.Cylinder, "Disc", saw.transform, Vector3.zero, new Vector3(1f, 0.02f, 1f), m.saw, Quaternion.identity, true);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f;
                var tooth = Part(PrimitiveType.Cube, "Tooth", saw.transform, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.5f),
                    new Vector3(0.12f, 0.03f, 0.16f), m.saw, Quaternion.Euler(0f, a + 30f, 0f), false);
                tooth.name = "Tooth " + i;
            }
            Part(PrimitiveType.Cylinder, "Hub", saw.transform, Vector3.zero, new Vector3(0.22f, 0.04f, 0.22f), m.gun, Quaternion.identity, false);
            saw.AddComponent<SawBlade>();
            AddTrail(saw, m.fxLine, 0.12f, 1f, new Color(1f, 0.85f, 0.75f, 0.35f));
            p.saw = Save(saw, "SawBlade");

            var bottle = new GameObject("MolotovBottle");
            Part(PrimitiveType.Cylinder, "Glass", bottle.transform, Vector3.zero, new Vector3(0.18f, 0.2f, 0.18f), m.bottle, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Rag", bottle.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.12f, 0.08f), m.fire, Quaternion.identity, false);
            bottle.AddComponent<MolotovBottle>();
            p.bottle = Save(bottle, "MolotovBottle");

            p.fire = FirePoolPrefab(m);

            // Gore
            p.splat = Save(FlatDisc("BloodSplat", m.splat), "BloodSplat");

            CreateNewEnemyPrefabs(m, p);
            ArtEnemies(m, p);
            ArtOverlays(p);
            return p;
        }

        // ---------- New enemies and props ----------

        static void CreateNewEnemyPrefabs(Mats m, Prefabs p)
        {
            // Rat: tiny, low, fast.
            var rat = EnemyRoot("Enemy_Rat", 0.25f, 0.22f, 0.5f, false, out var v);
            var ratBody = Part(PrimitiveType.Cube, "Body", v, new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.24f, 0.6f), m.rat, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Head", v, new Vector3(0f, 0.25f, 0.38f), new Vector3(0.2f, 0.18f, 0.22f), m.rat, Quaternion.identity, false);
            Part(PrimitiveType.Cube, "Eyes", v, new Vector3(0f, 0.3f, 0.5f), new Vector3(0.16f, 0.04f, 0.03f), m.eyes, Quaternion.identity, false);
            Part(PrimitiveType.Cube, "Tail", v, new Vector3(0f, 0.2f, -0.55f), new Vector3(0.05f, 0.05f, 0.5f), m.meat, Quaternion.Euler(-10f, 0f, 0f), false);
            p.rat = FinishEnemy(rat, ratBody, v, m);

            // Gonflé: a big swollen sack of pus that blows up.
            var bloater = EnemyRoot("Enemy_Bloater", 2f, 0.6f, 2f, false, out v);
            var bloaterBody = Part(PrimitiveType.Sphere, "Body", v, new Vector3(0f, 1f, 0f), new Vector3(1.35f, 1.25f, 1.35f), m.bloater, Quaternion.identity, true);
            Part(PrimitiveType.Sphere, "Pustule", v, new Vector3(0.45f, 1.3f, 0.35f), Vector3.one * 0.35f, m.pustule, Quaternion.identity, false);
            Part(PrimitiveType.Sphere, "Pustule", v, new Vector3(-0.5f, 0.9f, 0.25f), Vector3.one * 0.28f, m.pustule, Quaternion.identity, false);
            Part(PrimitiveType.Sphere, "Pustule", v, new Vector3(0.1f, 1.6f, -0.3f), Vector3.one * 0.3f, m.pustule, Quaternion.identity, false);
            Part(PrimitiveType.Cube, "Eyes", v, new Vector3(0f, 1.35f, 0.62f), new Vector3(0.4f, 0.07f, 0.08f), m.eyes, Quaternion.identity, false);
            p.bloater = FinishEnemy(bloater, bloaterBody, v, m);

            // Scindeur and its spawn: lumpy, splits in three.
            p.splitter = SplitterPrefab("Enemy_Splitter", 1.15f, m);
            p.splitterSmall = SplitterPrefab("Enemy_SplitterSmall", 0.6f, m);

            // Pilleur: hunched, carrying a glowing sack of gold.
            var thief = EnemyRoot("Enemy_Thief", 1f, 0.36f, 1.8f, false, out v);
            var thiefBody = Part(PrimitiveType.Capsule, "Body", v, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), m.thief, Quaternion.Euler(15f, 0f, 0f), true);
            Part(PrimitiveType.Sphere, "Sack", v, new Vector3(0f, 1.25f, -0.45f), new Vector3(0.8f, 0.75f, 0.8f), m.sack, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Eyes", v, new Vector3(0f, 1.45f, 0.32f), new Vector3(0.4f, 0.07f, 0.08f), m.eyes, Quaternion.identity, false);
            p.thief = FinishEnemy(thief, thiefBody, v, m);

            p.golden = ArtEnemy("Enemy_Golden", "Ambre", m, 1f, 0.4f, 2f, 2.1f, Gore.Crystal, artMats?.crystal) ?? EnemyPrefab("Enemy_Golden", m.golden, m, 1f);

            // Explosive barrel: red, banded, glowing lid.
            var barrel = EnemyRoot("Prop_Barrel", 30f, 0.45f, 1.1f, true, out v);
            var barrelBody = Part(PrimitiveType.Cylinder, "Body", v, new Vector3(0f, 0.55f, 0f), new Vector3(0.85f, 0.55f, 0.85f), m.barrel, Quaternion.identity, true);
            Part(PrimitiveType.Cylinder, "Band", v, new Vector3(0f, 0.3f, 0f), new Vector3(0.87f, 0.035f, 0.87f), m.gun, Quaternion.identity, false);
            Part(PrimitiveType.Cylinder, "Band", v, new Vector3(0f, 0.8f, 0f), new Vector3(0.87f, 0.035f, 0.87f), m.gun, Quaternion.identity, false);
            Part(PrimitiveType.Cylinder, "Lid", v, new Vector3(0f, 1.11f, 0f), new Vector3(0.55f, 0.015f, 0.55f), m.eyes, Quaternion.identity, false);
            p.barrel = FinishEnemy(barrel, barrelBody, v, m);

            // Supply crate: wooden box with a red cross on top (readable from the camera).
            var crate = EnemyRoot("Prop_Crate", 30f, 0.5f, 1f, true, out v);
            var crateBody = Part(PrimitiveType.Cube, "Body", v, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), m.crate, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Plank", v, new Vector3(0f, 0.45f, 0.46f), new Vector3(1.1f, 0.14f, 0.04f), m.crateDark, Quaternion.Euler(0f, 0f, 45f), false);
            Part(PrimitiveType.Cube, "Plank", v, new Vector3(0f, 0.45f, -0.46f), new Vector3(1.1f, 0.14f, 0.04f), m.crateDark, Quaternion.Euler(0f, 0f, -45f), false);
            Part(PrimitiveType.Cube, "Plank", v, new Vector3(0.46f, 0.45f, 0f), new Vector3(0.04f, 0.14f, 1.1f), m.crateDark, Quaternion.Euler(45f, 0f, 0f), false);
            Part(PrimitiveType.Cube, "Plank", v, new Vector3(-0.46f, 0.45f, 0f), new Vector3(0.04f, 0.14f, 1.1f), m.crateDark, Quaternion.Euler(-45f, 0f, 0f), false);
            Part(PrimitiveType.Cube, "Cross", v, new Vector3(0f, 0.91f, 0f), new Vector3(0.5f, 0.02f, 0.15f), m.meat, Quaternion.identity, false);
            Part(PrimitiveType.Cube, "Cross", v, new Vector3(0f, 0.91f, 0f), new Vector3(0.15f, 0.02f, 0.5f), m.meat, Quaternion.identity, false);
            p.crate = FinishEnemy(crate, crateBody, v, m);

            // Small reward chest (thief, challenges): 1 free upgrade.
            var reward = new GameObject("Pickup_RewardChest");
            Part(PrimitiveType.Cube, "Mesh", reward.transform, Vector3.zero, new Vector3(0.8f, 0.55f, 0.55f), m.chest, Quaternion.identity, true);
            Part(PrimitiveType.Cube, "Lid", reward.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.84f, 0.1f, 0.59f), m.gun, Quaternion.identity, false);
            var pickup = reward.AddComponent<Pickup>();
            pickup.type = PickupType.Chest;
            pickup.chestUpgrades = 1;
            pickup.chestGold = 25;
            pickup.chestHealFraction = 0.15f;
            p.rewardChest = Save(reward, "Pickup_RewardChest");
        }

        static GameObject SplitterPrefab(string name, float scale, Mats m)
        {
            var root = EnemyRoot(name, scale * scale, 0.42f * scale, 2f * scale, false, out var v);
            var body = Part(PrimitiveType.Capsule, "Body", v, new Vector3(0f, scale, 0f), new Vector3(0.85f, 1f, 0.85f) * scale, m.splitter, Quaternion.Euler(8f, 0f, 0f), true);
            Part(PrimitiveType.Sphere, "Lump", v, new Vector3(0.3f, 1.4f, -0.1f) * scale, Vector3.one * (0.45f * scale), m.splitter, Quaternion.identity, false);
            Part(PrimitiveType.Sphere, "Lump", v, new Vector3(-0.3f, 1.2f, -0.15f) * scale, Vector3.one * (0.4f * scale), m.splitter, Quaternion.identity, false);
            Part(PrimitiveType.Cube, "Eyes", v, new Vector3(0f, 1.55f, 0.32f) * scale, new Vector3(0.45f, 0.08f, 0.1f) * scale, m.eyes, Quaternion.identity, false);
            return FinishEnemy(root, body, v, m);
        }

        /// <summary>Physics root (rigidbody + capsule) and an empty "Visual" child for the meshes.</summary>
        static GameObject EnemyRoot(string name, float mass, float radius, float height, bool immovable, out Transform visual)
        {
            var root = new GameObject(name);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = mass;
            rb.constraints = immovable
                ? RigidbodyConstraints.FreezeAll
                : RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            rb.interpolation = immovable ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;

            var col = root.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = Mathf.Max(height, radius * 2f);
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            return root;
        }

        static GameObject FinishEnemy(GameObject root, GameObject body, Transform visual, Mats m)
        {
            var enemy = root.AddComponent<Enemy>();
            enemy.bodyRenderer = body.GetComponent<Renderer>();
            enemy.flashMaterial = m.flash;
            enemy.eliteMaterial = m.elite;
            enemy.visual = visual;
            return Save(root, root.name);
        }

        static GameObject EnemyPrefab(string name, Material body, Mats m, float scale)
        {
            var root = new GameObject(name);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = scale * scale;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, scale, 0f);
            col.radius = 0.4f * scale;
            col.height = 2f * scale;

            // Meshes live under "Visual" (pivot at the feet) so hits can squash them without touching physics.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var bodyGo = Part(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, scale, 0f), new Vector3(0.8f, 1f, 0.8f) * scale, body, Quaternion.Euler(8f, 0f, 0f), true);
            Part(PrimitiveType.Cube, "Eyes", visual, new Vector3(0f, 1.55f * scale, 0.32f * scale), new Vector3(0.45f, 0.08f, 0.1f) * scale, m.eyes, Quaternion.identity, false);

            var enemy = root.AddComponent<Enemy>();
            enemy.bodyRenderer = bodyGo.GetComponent<Renderer>();
            enemy.flashMaterial = m.flash;
            enemy.eliteMaterial = m.elite;
            enemy.visual = visual;

            return Save(root, name);
        }

        static GameObject ProjectilePrefab(string name, Vector3 scale, Material mat, float radius, Material trailMat, float trailTime, float trailWidth,
            Color? trailColor = null, Color? sparkColor = null)
        {
            var go = new GameObject(name);
            Part(PrimitiveType.Sphere, "Mesh", go.transform, Vector3.zero, scale, mat, Quaternion.identity, false);
            var projectile = go.AddComponent<Projectile>();
            projectile.radius = radius;
            if (sparkColor.HasValue) projectile.sparkColor = sparkColor.Value;
            AddTrail(go, trailMat, trailTime, trailWidth, trailColor ?? new Color(1f, 0.78f, 0.38f, 0.75f));
            return Save(go, name);
        }

        /// <summary>Fading streak behind fast things (bullets, blades, saws).</summary>
        static void AddTrail(GameObject go, Material mat, float time, float width, Color color)
        {
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = mat;
            t.time = time;
            t.widthMultiplier = width;
            t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            t.startColor = color;
            t.endColor = new Color(color.r, color.g, color.b, 0f);
            t.minVertexDistance = 0.1f;
            t.numCapVertices = 2;
            t.shadowCastingMode = ShadowCastingMode.Off;
            t.receiveShadows = false;
        }

        static GameObject PickupPrefab(string name, PickupType type, PrimitiveType shape, Vector3 scale, Material mat)
        {
            var go = new GameObject(name);
            Part(shape, "Mesh", go.transform, Vector3.zero, scale, mat, Quaternion.identity, true);
            go.AddComponent<Pickup>().type = type;
            return Save(go, name);
        }

        /// <summary>Flat cylinder lying on the ground (scaled at runtime).</summary>
        static GameObject FlatDisc(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot, bool castShadows)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static GameObject Save(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Data ----------

        /// <summary>Loads (or creates) an enemy type and applies <paramref name="configure"/> to it.</summary>
        static EnemyData EnemyAsset(string name, GameObject prefab, Action<EnemyData> configure)
        {
            string path = $"{Root}/Data/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.displayName = name;
                AssetDatabase.CreateAsset(data, path);
            }
            configure(data); // balance lives in code for now, so it is re-applied on every build
            data.prefab = prefab; // always relink in case the prefab was rebuilt
            EditorUtility.SetDirty(data);
            return data;
        }

        static VolumeProfile CreatePostProfile()
        {
            string path = $"{Root}/Settings/PostFX_Game.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.5f);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.7f);

            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>();
            color.contrast.Override(15f);
            color.saturation.Override(-20f);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---------- Scene ----------

        static void BuildScene(Mats m, Prefabs p, VolumeProfile postFx, Roster r)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Atmosphere: dim, dusty, foggy edges.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.27f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.07f, 0.07f, 0.08f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 48f;

            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            CordonAtmosphere(sun); // the bible's cold overcast light, blue-grey fog (when the art exists)

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            // 480 m: the player is clamped at ±190 m and the camera sees ~25 m ahead, so the ground never runs out.
            ground.transform.localScale = new Vector3(48f, 1f, 48f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = CordonGround() ?? m.ground;

            var player = BuildPlayer(m, p);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = player.transform;
            camGo.transform.SetPositionAndRotation(player.transform.position + follow.offset, Quaternion.LookRotation(-follow.offset));

            var volume = new GameObject("Post FX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CordonPostProfile() ?? postFx;

            var game = new GameObject("Game");
            game.AddComponent<GameManager>();
            game.AddComponent<Pool>();
            game.AddComponent<Combat>();

            var fx = game.AddComponent<GameFX>();
            fx.bloodParticleMaterial = m.bloodParticle;
            fx.bloodSplatPrefab = p.splat;
            fx.gibMaterial = m.gib;
            fx.glowParticleMaterial = m.fxParticle;
            fx.ringMaterial = m.fxRing;
            fx.glowMaterial = m.fxGlow;
            fx.lineMaterial = m.fxLine;
            WireFX(fx);

            // Stream mix: a new type shows up every half minute or so, so the first minutes keep surprising,
            // and the mix gets nastier over time (zombies fade, runners / brutes / spitters grow). See Docs/Pacing.md.
            var director = game.AddComponent<RunDirector>();
            director.entries.Add(new SpawnEntry { enemy = r.zombie, startTime = 0f, weight = 1f, weightPerMinute = -0.05f, minWeight = 0.45f });
            director.entries.Add(new SpawnEntry { enemy = r.rat, startTime = 20f, weight = 0.25f, groupSize = 4 });
            director.entries.Add(new SpawnEntry { enemy = r.runner, startTime = 45f, weight = 0.5f, weightPerMinute = 0.05f, maxWeight = 0.9f });
            director.entries.Add(new SpawnEntry { enemy = r.bloater, startTime = 90f, weight = 0.1f, weightPerMinute = 0.01f, maxWeight = 0.2f });
            director.entries.Add(new SpawnEntry { enemy = r.splitter, startTime = 100f, weight = 0.2f });
            director.entries.Add(new SpawnEntry { enemy = r.brute, startTime = 130f, weight = 0.15f, weightPerMinute = 0.03f, maxWeight = 0.45f });
            director.entries.Add(new SpawnEntry { enemy = r.spitter, startTime = 150f, weight = 0.25f, weightPerMinute = 0.02f, maxWeight = 0.45f });
            director.hordeEnemy = r.zombie;
            director.hordeFastEnemy = r.runner;
            director.hordeKamikaze = r.bloater;
            director.hordeHeavyEnemy = r.brute;
            director.assaultRangedEnemy = r.spitter;
            director.ratEnemy = r.rat;
            director.goldenEnemy = r.golden;
            director.thiefEnemy = r.thief;
            director.boss = r.boss;

            var events = game.AddComponent<ArenaEvents>();
            events.barrel = r.barrel;
            events.crate = r.crate;

            var loot = game.AddComponent<Loot>();
            loot.gemPrefab = p.gem;
            loot.healthPrefab = p.health;
            loot.magnetPrefab = p.magnet;
            loot.chestPrefab = p.chest;
            loot.coinPrefab = p.coin;
            loot.rewardChestPrefab = p.rewardChest;

            BuildDecor(game);

            game.AddComponent<DamageNumbers>();
            game.AddComponent<GameHUD>();
            game.AddComponent<MenuScreen>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static GameObject BuildPlayer(Mats m, Prefabs p)
        {
            var player = new GameObject("Player") { tag = "Player" };

            var rb = player.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 4f;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = player.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.radius = 0.4f;
            col.height = 2f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            // The Passeur model when it exists (CORDON art), the greybox pill otherwise.
            Renderer bodyRenderer = ArtPlayer(player, visual);
            if (!bodyRenderer)
            {
                bodyRenderer = Part(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), m.player, Quaternion.identity, true).GetComponent<Renderer>();
                Part(PrimitiveType.Cube, "Gun", visual, new Vector3(0.38f, 1.1f, 0.45f), new Vector3(0.14f, 0.16f, 0.6f), m.gun, Quaternion.identity, true);
            }

            var gas = GasCloudVisual(m);
            gas.transform.SetParent(player.transform, false);
            gas.SetActive(false); // shown once the weapon is picked

            player.AddComponent<PlayerStats>();
            var controller = player.AddComponent<PlayerController>();
            controller.visual = visual;
            controller.proceduralBob = !player.GetComponent<CharacterAnimator>();
            var health = player.AddComponent<PlayerHealth>();
            health.bodyRenderer = bodyRenderer;
            health.hurtMaterial = player.GetComponent<CharacterAnimator>() && artMats != null ? artMats.hurt : m.hurt;
            player.AddComponent<PlayerXP>();

            var upgrades = player.AddComponent<UpgradeManager>();
            upgrades.startingWeapon = AddWeapons(player, gas.transform, m, p);

            return player;
        }
    }
}
