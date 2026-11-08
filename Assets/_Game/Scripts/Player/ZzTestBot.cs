#if UNITY_EDITOR
using System.Text;
using UnityEngine;

namespace Survivor
{
    /// <summary>Temporary editor-only play-test bot: circles (or camps), auto-picks upgrades, logs pacing.</summary>
    public class ZzTestBot : MonoBehaviour
    {
        public static ZzTestBot Instance;
        public float radius = 9f;
        public float speed = 4f;
        public bool camp;
        public StringBuilder log = new StringBuilder();
        public int levelUps, exceptions;
        public bool ended;
        public float firstEvo = -1f, fullBuild = -1f, stuffFull = -1f;
        public float minHpWindow = 1f;
        float angle, nextLog;
        int minFps = 999;
        float fpsAcc; int fpsFrames;

        // ---------- Batch of runs (editor tests): Queue "focus" / "smart" / "random" runs, read AllLogs ----------

        /// <summary>Runs still to play after the current one ("focus", "smart" or "random"). Each ends with Replay().</summary>
        public static readonly System.Collections.Generic.Queue<string> Batch = new();
        /// <summary>END lines of every finished batch run.</summary>
        public static readonly StringBuilder AllLogs = new();
        static bool hooked;

        /// <summary>Starts a batch from Play mode on a fresh Classic run: e.g. StartBatch("focus","random","focus").</summary>
        public static void StartBatch(params string[] kinds)
        {
            Batch.Clear();
            foreach (var k in kinds) Batch.Enqueue(k);
            if (!hooked)
            {
                hooked = true;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => AttachNext(false); // Replay starts the run itself
            }
            AttachNext(true);
        }

        static void AttachNext(bool start)
        {
            if (Batch.Count == 0) return;
            var pc = FindAnyObjectByType<PlayerController>();
            if (!pc) return;
            string kind = Batch.Dequeue();
            var bot = pc.gameObject.AddComponent<ZzTestBot>();
            bot.kind = kind;
            bot.smart = kind != "random";
            bot.focus = kind == "focus";
            var gm = GameManager.Instance;
            if (start && gm && gm.State == GameState.Menu)
            {
                GameModes.Configure(GameMode.Classic);
                gm.StartRun();
            }
        }

        public string kind = "";
        float replayAt = -1f;

        void Awake() { Instance = this; Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; PlayerController.DebugInput = Vector2.zero; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (stack.Contains("ScreenshotUtility")) return;
            exceptions++;
            if (exceptions <= 6) log.AppendLine("ERR " + msg + " @ " + (stack.Length > 260 ? stack.Substring(0, 260) : stack).Replace("\n", " | "));
        }

        string Build(out int weapons, out int passives, out int evos)
        {
            var um = UpgradeManager.Instance;
            weapons = passives = evos = 0;
            var sb = new StringBuilder();
            foreach (var w in um.Weapons) if (w.Level > 0) { weapons++; if (w.Evolved) evos++; sb.Append(w.displayName.Substring(0, 3)).Append(w.Evolved ? "*" : w.Level.ToString()).Append(' '); }
            sb.Append("| ");
            foreach (var p in um.Passives) if (p.level > 0) { passives++; sb.Append(p.id.Substring(0, Mathf.Min(4, p.id.Length))).Append(p.level).Append(' '); }
            return sb.ToString();
        }

        public bool smart = true;
        /// <summary>Human-like movement: flee the crowd, strafe around it, dodge shots, grab gems when safe.</summary>
        public bool kite = true;
        /// <summary>0..1: how well the bot reacts (1 = perfect reads every frame, 0.6 = a casual player).</summary>
        public float skill = 0.75f;
        Vector2 kiteDir;
        float kiteThink, strafeSign = 1f, strafeSwap;
        EnemyProjectile[] shots = new EnemyProjectile[0];
        readonly System.Collections.Generic.List<Vector4> hazards = new();
        float shotScan;

        Vector2 Kite()
        {
            kiteThink -= Time.deltaTime;
            if (kiteThink > 0f) return kiteDir;
            // A casual player re-reads the screen ~5-10 times a second.
            kiteThink = Mathf.Lerp(0.25f, 0.05f, skill);

            var pos = PlayerController.Instance.Position;
            Vector3 flee = Vector3.zero, centroid = Vector3.zero;
            int near = 0;
            var list = Enemy.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (!e || e.IsDead || e.IsProp) continue;
                Vector3 d = pos - e.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist > 8f || dist < 0.001f) continue;
                float w = (e.IsBoss ? 3f : e.IsElite ? 2f : 1f) / Mathf.Max(0.6f, dist * dist);
                flee += d / dist * w;
                centroid += e.transform.position;
                near++;
            }

            shotScan -= Time.deltaTime;
            if (shotScan <= 0f) { shotScan = 0.2f; shots = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None); }
            foreach (var sh in shots)
            {
                if (!sh || !sh.gameObject.activeInHierarchy) continue;
                Vector3 d = pos - sh.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist > 4f || dist < 0.001f) continue;
                flee += d / dist * (1.5f / Mathf.Max(0.5f, dist));
            }

            Vector3 dir;
            if (near > 0 && flee.sqrMagnitude > 0.0004f)
            {
                Vector3 away = flee.normalized;
                // Strafe around the crowd instead of running straight away (keeps the weapons on them), swap side now and then.
                strafeSwap -= kiteThink;
                if (strafeSwap <= 0f) { strafeSwap = Random.Range(4f, 9f); strafeSign = -strafeSign; }
                Vector3 tangent = new Vector3(-away.z, 0f, away.x) * strafeSign;
                float danger = Mathf.Clamp01(flee.magnitude * 1.5f);
                dir = (away * (0.55f + 0.45f * danger) + tangent * (1f - 0.6f * danger)).normalized;
                // A human still drifts toward nearby gems while kiting, unless it's really dangerous.
                XPGem gem = null; float gd = 81f;
                foreach (var g in XPGem.Active)
                {
                    if (!g) continue;
                    float dd = (g.transform.position - pos).sqrMagnitude;
                    if (dd < gd) { gd = dd; gem = g; }
                }
                if (gem && danger < 0.7f)
                {
                    Vector3 toGem = gem.transform.position - pos; toGem.y = 0f;
                    dir = (dir + toGem.normalized * (0.9f * (1f - danger))).normalized;
                }
            }
            else
            {
                // Safe: go grab the nearest gem, else wander slowly.
                XPGem best = null; float bd = 100f;
                foreach (var g in XPGem.Active)
                {
                    if (!g) continue;
                    float dd = (g.transform.position - pos).sqrMagnitude;
                    if (dd < bd) { bd = dd; best = g; }
                }
                dir = best ? (best.transform.position - pos) : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                angle += 0.3f;
                dir.y = 0f; dir = dir.normalized;
            }
            // Events a human reacts to: walk into the safe zone, step out of telegraphed meteors / fire, go claim a cursed zone when calm.
            var we = WorldEvents.Instance;
            if (we)
            {
                if (we.TryGetSafeZone(out var sc, out float sr))
                {
                    Vector3 toC = sc - pos; toC.y = 0f;
                    if (toC.magnitude > sr * 0.55f) dir = (toC.normalized * 2f + dir * 0.3f).normalized;
                }
                else if (near < 4 && we.TryGetCursedZone(out var zc))
                {
                    Vector3 toZ = zc - pos; toZ.y = 0f;
                    if (toZ.magnitude > 1.2f && toZ.magnitude < 25f) dir = (toZ.normalized * 1.5f + dir * 0.5f).normalized;
                }
                we.GetHazards(hazards);
                Vector3 dodge = Vector3.zero;
                foreach (var hz in hazards)
                {
                    Vector3 d = pos - new Vector3(hz.x, 0f, hz.z);
                    float dist = d.magnitude, r = hz.w + 0.8f;
                    if (dist > r) continue;
                    dodge += (dist < 0.01f ? Vector3.right : d / dist) * (1f + (r - dist));
                }
                if (dodge.sqrMagnitude > 0.0001f && Random.value < skill) dir = (dodge.normalized * 2.5f + dir).normalized;
            }

            // Imperfect reads.
            float noise = (1f - skill) * 0.9f;
            dir += new Vector3(Random.Range(-noise, noise), 0f, Random.Range(-noise, noise));
            dir.y = 0f;
            kiteDir = new Vector2(dir.x, dir.z).normalized;
            return kiteDir;
        }

        /// <summary>
        /// Card choice. smart: a decent player (evolve, level owned weapons, partners). focus (with smart): plans for the
        /// 20-level stuff = 3 weapons to Nv 5 + their partners, no 4th weapon. Neither: a random card (the "random build" test).
        /// </summary>
        public bool focus = true;

        UpgradeOption Pick(System.Collections.Generic.List<UpgradeOption> opts)
        {
            Build(out int weapons, out int passives, out _);
            UpgradeOption best = opts[0];
            float bestScore = float.MinValue;
            foreach (var o in opts)
            {
                float s = 0f;
                bool partner = o.hint.Contains("Fait évoluer");
                bool set = o.hint.Contains("Débloque");
                switch (o.kind)
                {
                    case UpgradeKind.Evolution: s = 100f; break;
                    case UpgradeKind.Weapon: s = 50f; break;
                    case UpgradeKind.NewWeapon: s = weapons < 3 ? 60f : focus ? 3f : weapons < 5 ? 25f : 5f; break;
                    case UpgradeKind.Passive:
                        bool owned = o.levelLabel != null && !o.levelLabel.ToUpperInvariant().Contains("NOUVEAU");
                        s = partner ? 55f : owned ? 30f : set ? 26f : passives < 6 ? 20f : 5f;
                        break;
                    default: s = 1f; break;
                }
                s += (int)o.rarity * 8f;
                if (s > bestScore) { bestScore = s; best = o; }
            }
            return best;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.State == GameState.LevelUp)
            {
                // The hand on screen (never re-roll: a seeded run must see the same hands as a human).
                var hand = UpgradeManager.Instance.CurrentHand;
                var opts = hand != null ? new System.Collections.Generic.List<UpgradeOption>(hand) : UpgradeManager.Instance.RollOptions(3);
                if (opts.Count > 0) { levelUps++; gm.ChooseUpgrade(smart ? Pick(opts) : opts[Random.Range(0, opts.Count)]); }
                return;
            }
            var umLog = UpgradeManager.Instance;
            if (umLog && umLog.BuildComplete && stuffFull < 0f) stuffFull = gm.RunTime;
            if (gm.State == GameState.GameOver && !ended)
            {
                ended = true;
                string b = Build(out int w, out int p, out int e);
                string end = $"END [{kind}] t={gm.RunTime:0} won={gm.Won} lvl={PlayerXP.Instance.Level} score={gm.Score} burstScore={gm.LevelBurstScore} bursts={gm.LevelBursts} kills={gm.Kills} bosses={gm.BossKills} gold={gm.Gold} picks={levelUps} firstEvo={firstEvo:0} stuffFull={stuffFull:0} evos={e} errors={exceptions} absent={UpgradeManager.Instance.AbsentNames()} :: {b}";
                log.AppendLine(end);
                AllLogs.AppendLine(end);
                if (Batch.Count > 0) replayAt = Time.unscaledTime + 1f;
            }
            if (replayAt > 0f && Time.unscaledTime >= replayAt)
            {
                replayAt = -1f;
                gm.Replay();
                return;
            }
            if (gm.State != GameState.Playing) { PlayerController.DebugInput = Vector2.zero; return; }
            if (Time.timeScale > 0.9f && Time.timeScale < speed) Time.timeScale = speed;

            var hp = PlayerHealth.Instance;
            if (hp) minHpWindow = Mathf.Min(minHpWindow, hp.Fraction);

            if (camp) PlayerController.DebugInput = Vector2.zero;
            else if (kite)
            {
                PlayerController.DebugInput = Kite();
            }
            else
            {
                angle += Time.deltaTime * 0.55f;
                var pos = PlayerController.Instance.Position;
                Vector3 target = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                Vector3 d = target - pos; d.y = 0;
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 dir = (tangent + d * 0.25f).normalized;
                PlayerController.DebugInput = new Vector2(dir.x, dir.z);
            }

            float fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            fpsAcc += fps; fpsFrames++;
            if (gm.RunTime > 3f && fps < minFps) minFps = (int)fps;
            if (Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 5f;
                string b = Build(out int w, out int p, out int e);
                if (e > 0 && firstEvo < 0) firstEvo = gm.RunTime;
                if (w >= 5 && p >= 6 && fullBuild < 0) fullBuild = gm.RunTime;
                var we = WorldEvents.Instance;
                var rd = FindAnyObjectByType<RunDirector>();
                log.AppendLine($"[{gm.RunTime:0}s] L{PlayerXP.Instance.Level} S{UpgradeManager.Instance.BuildLevels} {(rd ? rd.Phase.ToString() : "-")} hp {(hp ? hp.Fraction : 0):0.00} min {minHpWindow:0.00} alive {Enemy.Active.Count} comfort {ComfortMeter.Value:0.00} pres {(we ? we.PressureLevel : -1)} combo? fps {(fpsFrames > 0 ? fpsAcc / fpsFrames : 0):0}/{minFps} :: {b}");
                fpsAcc = 0; fpsFrames = 0; minFps = 999; minHpWindow = 1f;
            }
        }
    }
}
#endif
