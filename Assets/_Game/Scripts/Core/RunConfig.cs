namespace Survivor
{
    /// <summary>Which kind of run is being played. Everything but Campaign is ranked (same rules for everyone).</summary>
    public enum GameMode
    {
        /// <summary>Campaign stage: fixed duration, stars, revive ad allowed. Not ranked.</summary>
        Campaign,
        /// <summary>Endless, pure equal rules. Board "classic".</summary>
        Classic,
        /// <summary>Daily seeded run: same seed / start / threats for everyone today. One ranked attempt per day.</summary>
        Daily,
        /// <summary>Weekly elemental challenge: same element and threats for everyone this week.</summary>
        Weekly,
        /// <summary>New Game+: endless with the threats ("Menaces") the player turned on, each adds a score multiplier.</summary>
        Menaces,
    }

    /// <summary>
    /// How the next run is set up. The menu writes it before StartRun();
    /// RunDirector reads the multipliers, GameManager reads the win condition and the score multiplier.
    /// Default values = the classic endless run.
    /// Static so it survives the scene reload done by "Rejouer".
    /// </summary>
    public static class RunConfig
    {
        public static GameMode Mode = GameMode.Classic;

        /// <summary>Campaign stage index (0-based), or -1 outside the campaign.</summary>
        public static int Stage = -1;
        public static bool IsCampaign => Mode == GameMode.Campaign && Stage >= 0;
        /// <summary>Ranked runs: no revive, no help that others can't get. Score goes to <see cref="BoardId"/>.</summary>
        public static bool Ranked => Mode != GameMode.Campaign;

        public static float EnemyHealthMult = 1f;
        public static float EnemyDamageMult = 1f;
        public static float SpawnRateMult = 1f;
        /// <summary>Seconds to survive to win the stage. 0 = endless.</summary>
        public static float Duration = 0f;
        /// <summary>Stage / mode title for the HUD, e.g. "1-2  Le centre commercial". Empty in classic mode.</summary>
        public static string Label = "";

        /// <summary>Seed for the card draws (daily / weekly). 0 = not seeded (normal randomness).</summary>
        public static int Seed = 0;
        /// <summary>Final score = base score x this (threats). 1 in classic.</summary>
        public static float ScoreMult = 1f;
        /// <summary>Leaderboard the score goes to ("classic", "daily_20261106", ...). Empty = not submitted.</summary>
        public static string BoardId = "classic";
        /// <summary>Weapon id forced at the start (daily / weekly). Empty = the player's chosen start weapon.</summary>
        public static string ForcedStartWeapon = "";
        /// <summary>False for a practice attempt (e.g. the daily run after the ranked attempt): nothing is submitted.</summary>
        public static bool SubmitScore = true;
        /// <summary>Active threats of the run, one bit per entry of <see cref="Threats.All"/> (Modes/Threats.cs). 0 = none.</summary>
        public static int ThreatMask = 0;
        /// <summary>Weekly elemental challenge: cards with this family come up twice as often (same for everyone). None elsewhere.</summary>
        public static ItemTag BoostedTag = ItemTag.None;

        public static void SetEndless()
        {
            Mode = GameMode.Classic;
            Stage = -1;
            EnemyHealthMult = EnemyDamageMult = SpawnRateMult = 1f;
            Duration = 0f;
            Label = "";
            Seed = 0;
            ScoreMult = 1f;
            BoardId = "classic";
            ForcedStartWeapon = "";
            SubmitScore = true;
            ThreatMask = 0;
            BoostedTag = ItemTag.None;
        }
    }
}
