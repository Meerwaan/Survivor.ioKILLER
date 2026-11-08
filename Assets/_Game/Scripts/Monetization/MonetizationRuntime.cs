using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Monetization's own entry point (no scene edit): a persistent object created at startup that, for every
    /// loaded scene, dresses the player with the equipped skin and counts run starts (ad frequency caps).
    /// Every run is its own scene load ("Rejouer" / "Menu" reload the scene), so "first Playing state in this
    /// scene" = one run started.
    /// </summary>
    public class MonetizationRuntime : MonoBehaviour
    {
        public static MonetizationRuntime Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance) return;
            var go = new GameObject("Monetization");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<MonetizationRuntime>();
        }

        GameManager trackedGame;
        PlayerController dressedPlayer;
        bool runCounted;

        /// <summary>Per-run flags (reset when a new run starts).</summary>
        public static bool ReviveUsedThisRun { get; set; }
        public static bool DoubleGoldUsedThisRun { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ReviveUsedThisRun = false;
            DoubleGoldUsedThisRun = false;
        }

        void Start()
        {
            Cosmetics.ValidateEquipped();
        }

        void Update()
        {
            // New scene: dress the new player.
            var player = PlayerController.Instance;
            if (player && player != dressedPlayer)
            {
                dressedPlayer = player;
                PlayerSkin.Attach(player.gameObject);
            }

            var gm = GameManager.Instance;
            if (gm != trackedGame)
            {
                trackedGame = gm;
                runCounted = false;
            }
            if (!gm || runCounted) return;
            if (gm.State == GameState.Playing)
            {
                runCounted = true;
                ReviveUsedThisRun = false;
                DoubleGoldUsedThisRun = false;
                Monetization.OnRunStarted();
            }
        }
    }
}
