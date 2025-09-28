namespace Classes.ProcessQueue
{
    /// <summary>
    /// Specifies the types of items that can be queued for processing in background services.
    /// </summary>
    public enum QueueItemType
    {
        /// <summary>
        /// Reserved for blocking all services - no actual background service is tied to this type
        /// </summary>
        All,

        /// <summary>
        /// Default type - no background service is tied to this type
        /// </summary>
        NotConfigured,

        /// <summary>
        /// Ingests signature DAT files into the database
        /// </summary>
        SignatureIngestor,

        /// <summary>
        /// Tallys all votes in the database
        /// </summary>
        TallyVotes,

        /// <summary>
        /// Searches for metadata matches for all objects in the database
        /// </summary>
        MetadataMatchSearch,

        /// <summary>
        /// Fetches missing artwork for game data objects
        /// </summary>
        GetMissingArtwork,

        /// <summary>
        /// Fetch VIMM manual metadata
        /// </summary>
        FetchVIMMMetadata,

        /// <summary>
        /// Fetch TheGamesDb metadata
        /// </summary>
        FetchTheGamesDbMetadata,

        /// <summary>
        /// Fetch RetroAchievements metadata
        /// </summary>
        FetchRetroAchievementsMetadata,

        /// <summary>
        /// Fetch IGDB metadata
        /// </summary>
        FetchIGDBMetadata,

        /// <summary>
        /// Fetch GiantBomb metadata
        /// </summary>
        FetchGiantBombMetadata,

        /// <summary>
        /// Fetch Redump metadata
        /// </summary>
        FetchRedumpMetadata,

        /// <summary>
        /// Runs daily maintenance tasks
        /// </summary>
        DailyMaintenance,

        /// <summary>
        /// Runs weekly maintenance tasks
        /// </summary>
        WeeklyMaintenance,

        /// <summary>
        /// Reserved for cache warming tasks - no actual background service is tied to this type
        /// </summary>
        CacheWarmer,

        /// <summary>
        /// Metadata map dump task
        /// </summary>
        MetadataMapDump
    }
}