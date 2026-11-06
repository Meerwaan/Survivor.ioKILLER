using System;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// What an ad network must provide. Plug a real SDK (Unity LevelPlay, AdMob...) by writing one class that
    /// implements this and assigning it in <see cref="Ads.CreateProvider"/>: nothing else in the game changes.
    /// Callbacks must always be invoked exactly once (also on failure), on the main thread.
    /// </summary>
    public interface IAdProvider
    {
        /// <summary>True when the provider is a stand-in (UI shows "(test)" markers and the test toggles).</summary>
        bool IsTest { get; }
        bool IsRewardedReady(string placement);
        /// <summary>onDone(true) only when the ad was watched to the end (reward granted).</summary>
        void ShowRewarded(string placement, Action<bool> onDone);
        bool IsInterstitialReady(string placement);
        void ShowInterstitial(string placement, Action onDone);
    }

    /// <summary>Placement names, as they would be configured in the ad network dashboard.</summary>
    public static class AdPlacement
    {
        public const string Revive = "revive";
        public const string DoubleGold = "double_gold";
        public const string CampBoost = "camp_boost";
        public const string CampExpress = "camp_express";
        public const string PreRun = "pre_run";
    }

    /// <summary>
    /// Entry point for every ad in the game. Rules (owner's, non-negotiable):
    /// nothing watched gives run power; rewarded ads are always a choice; the forced ad is announced BEFORE a run
    /// and never after the day's first game: so at most ONE per day, before the first run of the day,
    /// never for a brand-new player (first runs ever), and never for players who bought "Sans pub".
    /// Works while Time.timeScale == 0 (all overlays run on unscaled time) and never touches timeScale.
    /// </summary>
    public static class Ads
    {
        /// <summary>Minimum run starts between two forced ads.</summary>
        public const int InterstitialEveryRuns = 3;
        /// <summary>Kept for the Profile test tools (the forced ad now only comes before the day's first run).</summary>
        public const int FreeRunsPerDay = 2;
        /// <summary>A new player never sees a forced ad during their first runs ever (onboarding).</summary>
        public const int NewPlayerFreeRuns = 3;

        /// <summary>Test tool (Profile): the next run start gets the forced ad whatever the rules say. Session only.</summary>
        public static bool ForceNextInterstitial;

        const string TestInterstitialKey = "ads_test_interstitial";
        const string TestUnavailableKey = "ads_test_unavailable";

        static IAdProvider provider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            provider = null;
            Busy = false;
            ForceNextInterstitial = false;
        }

        /// <summary>The ad network. Swap the mock for a real SDK adapter here (single place).</summary>
        public static IAdProvider Provider
        {
            get => provider ??= CreateProvider();
            set => provider = value;
        }

        static IAdProvider CreateProvider()
        {
            // Real SDK later, e.g.: return new LevelPlayAdProvider(appKey);
            return new MockAdProvider();
        }

        public static bool IsTest => Provider.IsTest;

        /// <summary>Editor play-test bot running: no forced ad nor revive offer (it can't tap, and it would skew its measures).</summary>
        public static bool BotActive
        {
            get
            {
#if UNITY_EDITOR
                return ZzTestBot.Instance != null;
#else
                return false;
#endif
            }
        }

        /// <summary>An ad (or its announcement) is on screen: ignore other requests.</summary>
        public static bool Busy { get; private set; }

        /// <summary>"Sans pub" bought (or included in a pack): no forced ads ever. Rewarded ads stay available (optional).</summary>
        public static bool NoAds => Store.Owns(Store.NoAdsId);

        // ---------- Test toggles (Profile page, only while the provider is a mock) ----------

        /// <summary>Forced test ads before runs (on by default so the real flow can be tried).</summary>
        public static bool TestInterstitials
        {
            get => PlayerPrefs.GetInt(TestInterstitialKey, 1) == 1;
            set { PlayerPrefs.SetInt(TestInterstitialKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Simulates "no ad to show" (no fill) to test the paths where offers are hidden.</summary>
        public static bool TestUnavailable
        {
            get => PlayerPrefs.GetInt(TestUnavailableKey, 0) == 1;
            set { PlayerPrefs.SetInt(TestUnavailableKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---------- Rewarded ----------

        public static bool IsRewardedReady(string placement)
        {
            if (Busy) return false;
            if (IsTest && TestUnavailable) return false;
            return Provider.IsRewardedReady(placement);
        }

        /// <summary>Shows a rewarded ad. onDone(true) = watched to the end, grant the reward.</summary>
        public static void ShowRewarded(string placement, Action<bool> onDone)
        {
            if (!IsRewardedReady(placement))
            {
                onDone?.Invoke(false);
                return;
            }
            Busy = true;
            Provider.ShowRewarded(placement, ok =>
            {
                Busy = false;
                if (ok)
                {
                    Monetization.Data.rewardedWatched++;
                    MetaSave.Save();
                }
                onDone?.Invoke(ok);
            });
        }

        // ---------- Forced (interstitial), only before a run ----------

        /// <summary>Why no forced ad would be shown before the next run (null = one would).</summary>
        public static string InterstitialBlocker()
        {
            if (NoAds) return "Sans pub";
            if (BotActive) return "bot de test";
            if (IsTest && !TestInterstitials) return "pubs de test désactivées";
            if (!ForceNextInterstitial)
            {
                if (Monetization.RunsToday > 0) return "seulement avant la 1re partie du jour";
                if (MetaSave.Data.life.runs < NewPlayerFreeRuns) return "nouveau joueur";
            }
            if (IsTest && TestUnavailable) return "aucune pub disponible";
            if (!Provider.IsInterstitialReady(AdPlacement.PreRun)) return "aucune pub disponible";
            return null;
        }

        /// <summary>
        /// Call this instead of starting the run directly: either starts at once, or announces
        /// "Petite pub avant la partie… 3", shows the forced ad, then starts. <paramref name="start"/> is always
        /// called exactly once (even if the ad fails), unless a request is already on screen (double tap).
        /// </summary>
        public static void BeforeRun(Action start)
        {
            if (start == null || Busy) return;
            if (InterstitialBlocker() != null)
            {
                start();
                return;
            }

            Busy = true;
            PreRunAnnounce.Show(skipped =>
            {
                if (skipped)
                {
                    // "Sans pub" bought from the announcement: no ad at all.
                    Busy = false;
                    start();
                    return;
                }
                ForceNextInterstitial = false;
                var d = Monetization.Data;
                d.runsSinceInterstitial = 0;
                d.interstitialsShown++;
                MetaSave.Save();
                Provider.ShowInterstitial(AdPlacement.PreRun, () =>
                {
                    Busy = false;
                    start();
                });
            });
        }
    }
}
