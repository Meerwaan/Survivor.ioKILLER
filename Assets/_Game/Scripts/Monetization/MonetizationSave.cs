using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Everything monetization needs to remember (ads frequency caps, camp boosts, purchases, cosmetics).
    /// Stored inside the meta save (<see cref="MetaData.shop"/>): new fields must get a default value so old saves load.
    /// Fair play: nothing in here ever changes how strong a run is.
    /// </summary>
    [Serializable]
    public class MonetizationSave
    {
        // ----- Store (mock IAP for now) -----
        /// <summary>Product ids bought with real money (see Store.Products).</summary>
        public List<string> purchases = new();

        // ----- Cosmetics -----
        /// <summary>Cosmetic ids bought with gold ("skin_camo", "title_pilleur"...).</summary>
        public List<string> cosmetics = new();
        /// <summary>Unlocked cosmetics the player has already seen in the shop (for the "NOUVEAU" badge).</summary>
        public List<string> seen = new();

        // ----- Run counters (frequency caps) -----
        /// <summary>Local date (yyyyMMdd) of <see cref="runsToday"/>.</summary>
        public string runDay = "";
        /// <summary>Runs started today (no forced ad around the first game of the day).</summary>
        public int runsToday;
        /// <summary>Runs started since the last forced (interstitial) ad.</summary>
        public int runsSinceInterstitial;
        /// <summary>Runs started since the last "Seconde chance" offer. Starts high so the first campaign death gets one.</summary>
        public int runsSinceReviveOffer = 99;

        // ----- Camp boosts (rewarded ads) -----
        /// <summary>Unix time (UTC seconds) until which the camp produces x2. 0 = no boost.</summary>
        public long campBoostUntil;
        /// <summary>Local date (yyyyMMdd) of <see cref="expressCount"/>.</summary>
        public string expressDay = "";
        /// <summary>"Livraison express" ads watched today.</summary>
        public int expressCount;

        // ----- Stats -----
        public int rewardedWatched;
        public int interstitialsShown;

        public void Sanitize()
        {
            purchases ??= new List<string>();
            cosmetics ??= new List<string>();
            seen ??= new List<string>();
            runDay ??= "";
            expressDay ??= "";
        }
    }

    /// <summary>Access to <see cref="MonetizationSave"/> plus the run counters used by the ad frequency caps.</summary>
    public static class Monetization
    {
        /// <summary>Local calendar day, same convention as the daily missions.</summary>
        public static string Today => DateTime.Now.ToString("yyyyMMdd");

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static MonetizationSave Data
        {
            get
            {
                var meta = MetaSave.Data;
                meta.shop ??= new MonetizationSave();
                meta.shop.Sanitize();
                return meta.shop;
            }
        }

        /// <summary>Runs started today (rolls over at local midnight).</summary>
        public static int RunsToday
        {
            get
            {
                var d = Data;
                return d.runDay == Today ? d.runsToday : 0;
            }
        }

        /// <summary>Called once per run, when it actually starts (see MonetizationRuntime).</summary>
        public static void OnRunStarted()
        {
            var d = Data;
            string today = Today;
            if (d.runDay != today)
            {
                d.runDay = today;
                d.runsToday = 0;
            }
            d.runsToday++;
            d.runsSinceInterstitial = Mathf.Min(d.runsSinceInterstitial + 1, 1000);
            d.runsSinceReviveOffer = Mathf.Min(d.runsSinceReviveOffer + 1, 1000);
            MetaSave.Save();
        }
    }
}
