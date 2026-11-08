using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Survivor.EditorTools
{
    /// <summary>
    /// CORDON art pass: swaps the greybox primitives for the Blender models (Assets/_Game/Art, built by the
    /// scripts in Tools/Blender). Every model shares ONE palette material (shader Cordon/Character).
    /// A prefab falls back to its greybox look when its model file doesn't exist, so the build never breaks.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const string ArtRoot = "Assets/_Game/Art";

        class ArtMats
        {
            public Material body, flash, elite, crystal, hurt, gem1, gem2, gem3;
        }

        static ArtMats artMats;
        /// <summary>Controllers built during this Build (a model shared by two prefabs gets one controller).</summary>
        static readonly Dictionary<string, RuntimeAnimatorController> builtControllers = new();

        // ---------- Materials ----------

        static ArtMats CreateArtMaterials()
        {
            builtControllers.Clear();
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/T_Palette.png");
            if (!palette) return null;
            if (!AssetDatabase.IsValidFolder(ArtRoot + "/Materials")) AssetDatabase.CreateFolder(ArtRoot, "Materials");
            var a = new ArtMats
            {
                body = ArtMat("M_Cordon", palette, null),
                flash = ArtMat("M_CordonFlash", palette, m =>
                {
                    m.SetFloat("_Flash", 0.85f);
                    m.SetColor("_FlashColor", new Color(1f, 0.95f, 0.9f));
                }),
                // Alphas: a discreet amber aura on the rim (Univers 9.9).
                elite = ArtMat("M_CordonElite", palette, m =>
                {
                    m.SetColor("_BaseColor", new Color(1.05f, 0.92f, 0.8f));
                    m.SetColor("_Glow", new Color(1.6f, 0.62f, 0.12f));
                    m.SetFloat("_GlowRim", 0.85f);
                    m.SetFloat("_EmissionStrength", 2.6f);
                }),
                // Cristallisé: the amber has invaded the whole body, translucent-looking and glowing softly.
                crystal = ArtMat("M_CordonCrystal", palette, m =>
                {
                    m.SetColor("_BaseColor", new Color(1.25f, 0.78f, 0.38f));
                    m.SetColor("_Glow", new Color(0.55f, 0.24f, 0.04f));
                    m.SetFloat("_GlowRim", 0.35f);
                    m.SetColor("_RimColor", new Color(1f, 0.62f, 0.2f));
                    m.SetFloat("_RimPower", 2f);
                    m.SetFloat("_Smoothness", 0.85f);
                    m.SetFloat("_EmissionStrength", 3f);
                }),
                hurt = ArtMat("M_CordonHurt", palette, m =>
                {
                    m.SetFloat("_Flash", 0.6f);
                    m.SetColor("_FlashColor", new Color(0.95f, 0.12f, 0.1f));
                }),
                // Éclats (XP) tiers: dark amber, amber, bright amber (Univers 9.4: amber never brighter than #E58E2B).
                gem1 = ArtMat("M_Eclat1", palette, m => m.SetFloat("_EmissionStrength", 1.6f)),
                gem2 = ArtMat("M_Eclat2", palette, m =>
                {
                    m.SetFloat("_EmissionStrength", 2.4f);
                    m.SetColor("_Glow", new Color(0.35f, 0.14f, 0.02f));
                }),
                gem3 = ArtMat("M_Eclat3", palette, m =>
                {
                    m.SetFloat("_EmissionStrength", 3.4f);
                    m.SetColor("_Glow", new Color(0.9f, 0.4f, 0.06f));
                    m.SetFloat("_GlowRim", 0.3f);
                }),
            };
            return a;
        }

        /// <summary>Loads or creates a Cordon/Character material. <paramref name="setup"/> only runs on creation,
        /// so Inspector tweaks survive a rebuild (same rule as the greybox materials).</summary>
        static Material ArtMat(string name, Texture2D palette, System.Action<Material> setup)
        {
            string path = $"{ArtRoot}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
            {
                mat = new Material(Shader.Find("Cordon/Character")) { enableInstancing = true };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", palette);
            if (created) setup?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Controllers are rebuilt in place during the build; the editor keeps stale in-memory copies of
        /// the prefabs that point to them until they are re-imported.</summary>
        static void RefreshArtAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { ArtRoot + "/Animators" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "/Prefabs" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Blood and stains are tinted per enemy now (amber ichor, red for humans): white base materials.</summary>
        static void PrepareTintedGore(Mats m)
        {
            m.bloodParticle.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m.bloodParticle);
            // Stains are see-through puddles now ("une flaque d'ambre collante", Univers 9.9), tinted per enemy.
            var s = m.splat;
            s.SetFloat("_Surface", 1f);
            s.SetFloat("_Blend", 0f);
            s.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            s.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            s.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            s.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            s.SetFloat("_ZWrite", 0f);
            s.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            s.renderQueue = (int)RenderQueue.Transparent - 50;
            s.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(s);
        }

        // ---------- Models ----------

        static GameObject Model(string file)
        {
            string path = $"{ArtRoot}/Characters/{file}.fbx";
            if (!File.Exists(path)) path = $"{ArtRoot}/Props/{file}.fbx";
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
        }

        static string ModelPath(string file) => $"{ArtRoot}/Characters/{file}.fbx";

        /// <summary>Clips of a character FBX by name ("Walk", "Attack"...), preview clips skipped.</summary>
        static Dictionary<string, AnimationClip> Clips(string file)
        {
            var d = new Dictionary<string, AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelPath(file)))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    d[c.name] = c;
            return d;
        }

        /// <summary>
        /// One controller per model: Move (Walk or Run, speed driven by the "Speed" parameter), Idle when the
        /// "Moving" bool is off, one-shots on triggers (Attack, Special, Windup, Hit) and a Charge loop
        /// on the "Charging" bool. Every parameter exists on every controller (no missing-parameter warnings).
        /// </summary>
        static RuntimeAnimatorController BuildController(string file)
        {
            if (builtControllers.TryGetValue(file, out var done) && done) return done;
            var clips = Clips(file);
            if (clips.Count == 0) return null;
            if (!AssetDatabase.IsValidFolder(ArtRoot + "/Animators")) AssetDatabase.CreateFolder(ArtRoot, "Animators");
            string path = $"{ArtRoot}/Animators/{file}.controller";
            // Rebuilt IN PLACE (same asset, same GUID): deleting and recreating it broke the prefabs' references.
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!ctrl) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            else
            {
                while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
                var old = ctrl.layers[0].stateMachine;
                foreach (var t in old.anyStateTransitions.ToArray()) old.RemoveAnyStateTransition(t);
                foreach (var st in old.states.ToArray()) old.RemoveState(st.state);
            }
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Moving", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter("Charging", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Attack", "Special", "Windup", "Hit" })
                ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

            // The Passeur's level-up tap on the injector plays on the Special trigger.
            if (!clips.ContainsKey("Special") && clips.TryGetValue("Inject", out var inject)) clips["Special"] = inject;
            // The hero flinches and injects with his upper body only: his legs keep running.
            var upperMask = UpperBodyMask(file);
            while (ctrl.layers.Length > 1) ctrl.RemoveLayer(ctrl.layers.Length - 1);
            var sm = ctrl.layers[0].stateMachine;
            clips.TryGetValue("Run", out var loco);
            if (!loco) clips.TryGetValue("Walk", out loco);
            if (!loco) loco = clips.Values.First();
            var move = sm.AddState("Move", new Vector3(300, 0, 0));
            move.motion = loco;
            move.speedParameterActive = true;
            move.speedParameter = "Speed";
            sm.defaultState = move;

            if (clips.TryGetValue("Idle", out var idleClip))
            {
                var idle = sm.AddState("Idle", new Vector3(300, 120, 0));
                idle.motion = idleClip;
                var toIdle = move.AddTransition(idle);
                toIdle.hasExitTime = false;
                toIdle.duration = 0.18f;
                toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
                var toMove = idle.AddTransition(move);
                toMove.hasExitTime = false;
                toMove.duration = 0.15f;
                toMove.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            }

            int row = 0;
            AnimatorStateMachine upper = null;
            AnimatorState upperIdle = null;
            if (upperMask)
            {
                ctrl.AddLayer(new AnimatorControllerLayer
                {
                    name = "Upper", defaultWeight = 1f, avatarMask = upperMask, blendingMode = AnimatorLayerBlendingMode.Override,
                    stateMachine = new AnimatorStateMachine { name = "Upper", hideFlags = HideFlags.HideInHierarchy },
                });
                upper = ctrl.layers[1].stateMachine;
                AssetDatabase.AddObjectToAsset(upper, ctrl);
                upperIdle = upper.AddState("Empty", new Vector3(300, 0, 0));
                upper.defaultState = upperIdle;
            }
            foreach (var trigger in new[] { "Attack", "Special", "Windup", "Hit" })
            {
                if (!clips.TryGetValue(trigger, out var clip)) continue;
                if (upper != null && (trigger == "Hit" || trigger == "Special"))
                {
                    var us = upper.AddState(trigger, new Vector3(600, row++ * 70, 0));
                    us.motion = clip;
                    var ue = upper.AddAnyStateTransition(us);
                    ue.hasExitTime = false;
                    ue.duration = 0.05f;
                    ue.canTransitionToSelf = false;
                    ue.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    var ub = us.AddTransition(upperIdle);
                    ub.hasExitTime = true;
                    ub.exitTime = 0.9f;
                    ub.duration = 0.12f;
                    continue;
                }
                var st = sm.AddState(trigger, new Vector3(600, row++ * 70, 0));
                st.motion = clip;
                var enter = sm.AddAnyStateTransition(st);
                enter.hasExitTime = false;
                enter.duration = 0.06f;
                enter.canTransitionToSelf = trigger != "Hit";
                enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
                var back = st.AddTransition(move);
                back.hasExitTime = true;
                back.exitTime = 0.92f;
                back.duration = 0.1f;
            }

            if (clips.TryGetValue("Death", out var deathClip))
            {
                var st = sm.AddState("Death", new Vector3(900, 0, 0));
                st.motion = deathClip;
                var enter = sm.AddAnyStateTransition(st);
                enter.hasExitTime = false;
                enter.duration = 0.1f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, "Dead");
                var back = st.AddTransition(move);
                back.hasExitTime = false;
                back.duration = 0.3f;
                back.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }

            if (clips.TryGetValue("Charge", out var chargeClip))
            {
                var st = sm.AddState("Charge", new Vector3(600, row * 70, 0));
                st.motion = chargeClip;
                var enter = sm.AddAnyStateTransition(st);
                enter.hasExitTime = false;
                enter.duration = 0.08f;
                enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, "Charging");
                var back = st.AddTransition(move);
                back.hasExitTime = false;
                back.duration = 0.15f;
                back.AddCondition(AnimatorConditionMode.IfNot, 0, "Charging");
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            builtControllers[file] = ctrl;
            return ctrl;
        }

        /// <summary>For the hero only: an avatar mask of everything from the Spine up (Generic rig paths).</summary>
        static AvatarMask UpperBodyMask(string file)
        {
            if (!file.StartsWith("Passeur")) return null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(file));
            if (!model) return null;
            string path = $"{ArtRoot}/Animators/{file}_Upper.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (!mask)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            mask.transformCount = 0;
            mask.AddTransformPath(model.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string p = mask.GetTransformPath(i);
                mask.SetTransformActive(i, p.Contains("/Spine"));
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>Instantiates a model (linked to its FBX) under <paramref name="parent"/>, palette material on
        /// every renderer, controller attached. Returns the instance, or null when the model doesn't exist.</summary>
        static GameObject PlaceModel(string file, Transform parent, Material mat, float scale, out Animator animator, out Renderer[] renderers)
        {
            animator = null;
            renderers = null;
            var asset = Model(file);
            if (!asset) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            inst.name = "Model";
            inst.transform.localPosition = Vector3.zero;
            // The Blender kit models face -Y; through the FBX axis conversion they arrive facing -Z: turn them
            // around so they face the root's forward (+Z), where gameplay points them.
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            inst.transform.localScale = Vector3.one * scale;
            renderers = inst.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (r is SkinnedMeshRenderer s)
                {
                    s.quality = SkinQuality.Bone2;
                    s.updateWhenOffscreen = false;
                    s.skinnedMotionVectors = false;
                }
            }
            animator = inst.GetComponent<Animator>();
            if (animator)
            {
                animator.runtimeAnimatorController = BuildController(file);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            return inst;
        }

        /// <summary>Ground speed (m/s) of each model's locomotion clip at playback speed 1, as reported by its
        /// Blender script (Tools/Blender/assets/*.py): the Animator plays it at (real speed / this).</summary>
        static readonly Dictionary<string, float> NaturalSpeed = new()
        {
            { "Ambre", 2.1f }, { "Fievreux", 4.4f }, { "Masse", 1.7f }, { "Cracheur", 2.0f }, { "Kyste", 1.9f },
            { "Colonie", 2.0f }, { "Rejeton", 3.4f }, { "Rat", 0.96f }, { "Pilleur", 4.38f }, { "Boucher", 2.02f },
        };

        static float Speed(string file) => NaturalSpeed.TryGetValue(file, out var v) ? v : 2f;

        /// <summary>Every enemy that has a model replaces its greybox prefab (same name, same collider, same data).</summary>
        static void ArtEnemies(Mats m, Prefabs p)
        {
            if (artMats == null) return;
            p.runner = ArtEnemy("Enemy_Runner", "Fievreux", m, 0.64f, 0.32f, 1.6f, Speed("Fievreux"), Gore.Ichor) ?? p.runner;
            p.brute = ArtEnemy("Enemy_Brute", "Masse", m, 2.56f, 0.64f, 3.2f, Speed("Masse"), Gore.Ichor) ?? p.brute;
            p.spitter = ArtEnemy("Enemy_Spitter", "Cracheur", m, 0.81f, 0.36f, 1.8f, Speed("Cracheur"), Gore.Ichor) ?? p.spitter;
            p.bloater = ArtEnemy("Enemy_Bloater", "Kyste", m, 2f, 0.6f, 2f, Speed("Kyste"), Gore.Ichor) ?? p.bloater;
            p.splitter = ArtEnemy("Enemy_Splitter", "Colonie", m, 1.32f, 0.48f, 2.3f, Speed("Colonie"), Gore.Ichor) ?? p.splitter;
            p.splitterSmall = ArtEnemy("Enemy_SplitterSmall", "Rejeton", m, 0.36f, 0.25f, 1.2f, Speed("Rejeton"), Gore.Ichor) ?? p.splitterSmall;
            p.rat = ArtEnemy("Enemy_Rat", "Rat", m, 0.25f, 0.22f, 0.5f, Speed("Rat"), Gore.Ichor) ?? p.rat;
            p.thief = ArtEnemy("Enemy_Thief", "Pilleur", m, 1f, 0.36f, 1.8f, Speed("Pilleur"), Gore.Human) ?? p.thief;
            p.boss = ArtEnemy("Enemy_Boss", "Boucher", m, 6.25f, 1f, 5f, Speed("Boucher"), Gore.Ichor) ?? p.boss;
        }

        /// <summary>Ground speed (m/s) of the Passeur's Run clip at speed 1 (reported by Tools/Blender/assets/passeur.py).</summary>
        const float PasseurRunSpeed = 5.2f;

        /// <summary>The player's look: the Passeur with every skin as a hidden outfit (PlayerSkin shows the equipped
        /// one), the run cycle synced to his speed, and the injector's vial filling with the éclats.</summary>
        static Renderer ArtPlayer(GameObject player, Transform visual)
        {
            if (artMats == null || !Model("Passeur")) return null;
            var inst = PlaceModel("Passeur", visual, artMats.body, 1f, out var animator, out var renderers);
            if (!inst) return null;
            var outfits = renderers.OfType<SkinnedMeshRenderer>().ToArray();
            if (outfits.Length == 0) return null;
            var main = outfits.FirstOrDefault(r => r.name == "Passeur") ?? outfits[0];
            foreach (var r in outfits)
            {
                r.gameObject.SetActive(r == main);
                r.quality = SkinQuality.Auto;
            }
            if (animator) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ca = player.AddComponent<CharacterAnimator>();
            ca.animator = animator;
            ca.variants = new Renderer[0];
            ca.naturalSpeed = PasseurRunSpeed;
            ca.minSpeed = 0.5f;
            ca.maxSpeed = 1.6f;
            ca.idleBelow = 0.4f;
            var vial = FindDeep(inst.transform, "Vial");
            if (vial)
            {
                var iv = player.AddComponent<InjectorVial>();
                iv.vial = vial;
                iv.anim = ca;
            }
            return main;
        }

        /// <summary>Look of an enemy family: blood, stains, chunks.</summary>
        struct Gore
        {
            public Color bloodA, bloodB, splat, gib, inner;

            // la Sève: amber-black ichor, crust chunks (Univers 9.9)
            public static Gore Ichor => new()
            {
                bloodA = new Color(0.13f, 0.06f, 0.02f), bloodB = new Color(0.55f, 0.27f, 0.06f),
                splat = new Color(0.3f, 0.13f, 0.035f, 0.72f), gib = new Color(0.52f, 0.28f, 0.09f), inner = new Color(0.12f, 0.05f, 0.02f),
            };

            public static Gore Crystal => new()
            {
                bloodA = new Color(0.6f, 0.32f, 0.06f), bloodB = new Color(0.95f, 0.6f, 0.2f),
                splat = new Color(0.5f, 0.26f, 0.05f, 0.6f), gib = new Color(0.95f, 0.6f, 0.22f), inner = new Color(0.5f, 0.24f, 0.05f),
            };

            // a human: a little red, cloth
            public static Gore Human => new()
            {
                bloodA = new Color(0.26f, 0f, 0f), bloodB = new Color(0.5f, 0.03f, 0.02f),
                splat = new Color(0.3f, 0.02f, 0.02f, 0.8f), gib = new Color(0.22f, 0.3f, 0.45f), inner = new Color(0.35f, 0.04f, 0.03f),
            };
        }

        /// <summary>
        /// An enemy prefab built on a model: physics root, "Visual" pivot (squashed on hits) holding the model,
        /// <see cref="Enemy"/> wired to the model's renderers, <see cref="CharacterAnimator"/> driving its clips.
        /// Returns null when the model is missing (the caller keeps the greybox).
        /// </summary>
        static GameObject ArtEnemy(string prefabName, string file, Mats m, float mass, float radius, float height,
            float naturalSpeed, Gore gore, Material body = null, float scale = 1f)
        {
            if (artMats == null || !Model(file)) return null;
            var root = EnemyRoot(prefabName, mass, radius, height, false, out var visual);
            var inst = PlaceModel(file, visual, body ? body : artMats.body, scale, out var animator, out var renderers);
            if (!inst)
            {
                Object.DestroyImmediate(root);
                return null;
            }
            var enemy = root.AddComponent<Enemy>();
            enemy.bodyRenderer = renderers[0];
            enemy.flashMaterial = artMats.flash;
            enemy.eliteMaterial = artMats.elite;
            enemy.visual = visual;
            enemy.bloodA = gore.bloodA;
            enemy.bloodB = gore.bloodB;
            enemy.splatColor = gore.splat;
            enemy.gibColor = gore.gib;
            enemy.gibInnerColor = gore.inner;

            var ca = root.AddComponent<CharacterAnimator>();
            ca.animator = animator;
            ca.variants = renderers.Where(r => r is SkinnedMeshRenderer).ToArray();
            ca.naturalSpeed = naturalSpeed * scale;
            enemy.anim = ca;
            return Save(root, prefabName);
        }
    }
}
