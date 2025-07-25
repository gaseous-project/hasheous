﻿using System;
using System.Data.Common;
using System.Threading.Tasks;
using Classes;
using hasheous.Classes;
using hasheous_server.Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Models;
using TheGamesDB;
using static Classes.Common;
using static hasheous_server.Models.DataObjectItem;

namespace Classes
{
    public static class ProcessQueue
    {
        public static List<QueueItem> QueueItems = new List<QueueItem>();

        public class QueueItem
        {
            public QueueItem(QueueItemType ItemType, int ExecutionInterval, bool AllowManualStart = true, bool RemoveWhenStopped = false)
            {
                _ItemType = ItemType;
                _ItemState = QueueItemState.NeverStarted;
                _LastRunTime = Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow);
                _Interval = ExecutionInterval;
                _AllowManualStart = AllowManualStart;
                _RemoveWhenStopped = RemoveWhenStopped;
            }

            public QueueItem(QueueItemType ItemType, int ExecutionInterval, List<QueueItemType> Blocks, bool AllowManualStart = true, bool RemoveWhenStopped = false)
            {
                _ItemType = ItemType;
                _ItemState = QueueItemState.NeverStarted;
                _LastRunTime = Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow);
                _Interval = ExecutionInterval;
                _AllowManualStart = AllowManualStart;
                _RemoveWhenStopped = RemoveWhenStopped;
                _Blocks = Blocks;
            }

            private QueueItemType _ItemType = QueueItemType.NotConfigured;
            private QueueItemState _ItemState = QueueItemState.NeverStarted;
            private DateTime _LastRunTime = DateTime.UtcNow;
            private DateTime _LastFinishTime
            {
                get
                {
                    return Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow);
                }
                set
                {
                    if (_SaveLastRunTime == true)
                    {
                        Config.SetSetting("LastRun_" + _ItemType.ToString(), value);
                    }
                }
            }
            private bool _SaveLastRunTime = false;
            private int _Interval = 0;
            private string _LastResult = "";
            private string? _LastError = null;
            private bool _ForceExecute = false;
            private bool _AllowManualStart = true;
            private bool _RemoveWhenStopped = false;
            private bool _IsBlocked = false;
            private string _CorrelationId = "";
            private List<QueueItemType> _Blocks = new List<QueueItemType>();

            public QueueItemType ItemType => _ItemType;
            public QueueItemState ItemState => _ItemState;
            public DateTime LastRunTime => _LastRunTime;
            private double _LastRunDuration = 0;
            public DateTime LastFinishTime => _LastFinishTime;
            public double LastRunDuration => _LastRunDuration;
            public DateTime NextRunTime
            {
                get
                {
                    return LastRunTime.AddMinutes(Interval);
                }
            }
            public int Interval => _Interval;
            public string LastResult => _LastResult;
            public string? LastError => _LastError;
            public bool Force => _ForceExecute;
            public bool AllowManualStart => _AllowManualStart;
            public bool RemoveWhenStopped => _RemoveWhenStopped;
            public string CorrelationId => _CorrelationId;
            public bool IsBlocked => _IsBlocked;
            public object? Options { get; set; } = null;
            public List<QueueItemType> Blocks => _Blocks;

            public async Task Execute()
            {
                if (_ItemState != QueueItemState.Disabled)
                {
                    if ((DateTime.UtcNow > NextRunTime || _ForceExecute == true) && _ItemState != QueueItemState.Running)
                    {
                        // we can run - do some setup before we start processing
                        _LastRunTime = DateTime.UtcNow;
                        _ItemState = QueueItemState.Running;
                        _LastResult = "";
                        _LastError = null;

                        // set the correlation id
                        Guid correlationId = Guid.NewGuid();
                        _CorrelationId = correlationId.ToString();
                        CallContext.SetData("CorrelationId", correlationId);
                        CallContext.SetData("CallingProcess", _ItemType.ToString());
                        CallContext.SetData("CallingUser", "System");

                        // log the start
                        Logging.Log(Logging.LogType.Debug, "Timered Event", "Executing " + _ItemType + " with correlation id " + _CorrelationId);

                        try
                        {
                            hasheous_server.Classes.DataObjects DataObjects = new hasheous_server.Classes.DataObjects();

                            switch (_ItemType)
                            {
                                case QueueItemType.SignatureIngestor:
                                    XML.XMLIngestor tIngest = new XML.XMLIngestor();

                                    foreach (int i in Enum.GetValues(typeof(gaseous_signature_parser.parser.SignatureParser)))
                                    {
                                        gaseous_signature_parser.parser.SignatureParser parserType = (gaseous_signature_parser.parser.SignatureParser)i;
                                        if (
                                            parserType != gaseous_signature_parser.parser.SignatureParser.Auto &&
                                            parserType != gaseous_signature_parser.parser.SignatureParser.Unknown
                                        )
                                        {

                                            string SignaturePath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, parserType.ToString());
                                            string SignatureProcessedPath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, parserType.ToString());

                                            if (!Directory.Exists(SignaturePath))
                                            {
                                                Directory.CreateDirectory(SignaturePath);
                                            }

                                            if (!Directory.Exists(SignatureProcessedPath))
                                            {
                                                Directory.CreateDirectory(SignatureProcessedPath);
                                            }

                                            tIngest.Import(SignaturePath, SignatureProcessedPath, parserType);
                                        }
                                    }
                                    break;

                                case QueueItemType.TallyVotes:
                                    Submissions submissions = new Submissions();
                                    await submissions.TallyVotes();
                                    break;

                                case QueueItemType.MetadataMatchSearch:
                                    DataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Platform);
                                    DataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Game, true);
                                    DataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Company, true);
                                    break;

                                case QueueItemType.GetMissingArtwork:
                                    BackgroundMetadataMatcher.BackgroundMetadataMatcher tMatcher = new BackgroundMetadataMatcher.BackgroundMetadataMatcher();
                                    tMatcher.GetGamesWithoutArtwork();
                                    break;

                                case QueueItemType.FetchVIMMMetadata:
                                    // get all platforms
                                    DataObjectsList Platforms = new DataObjectsList();

                                    // get VIMMSLair manual metadata for each platform
                                    Platforms = await DataObjects.GetDataObjects(DataObjects.DataObjectType.Platform);
                                    foreach (DataObjectItem Platform in Platforms.Objects)
                                    {
                                        AttributeItem VIMMPlatformName = Platform.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.VIMMPlatformName);
                                        if (VIMMPlatformName != null)
                                        {
                                            VIMMSLair.ManualDownloader tDownloader = new VIMMSLair.ManualDownloader(VIMMPlatformName.Value.ToString());
                                            await tDownloader.Download();

                                            // if we have a manual metadata file, load it into an object and process it
                                            if (tDownloader.LocalFileName != "")
                                            {
                                                // search for the game
                                                await VIMMSLair.ManualSearch.MatchManuals(tDownloader.LocalFileName, Platform);
                                            }
                                        }
                                    }
                                    break;

                                case QueueItemType.FetchTheGamesDbMetadata:
                                    // set up JSON
                                    TheGamesDB.JSON.DownloadManager tgdbDownloader = new TheGamesDB.JSON.DownloadManager();
                                    tgdbDownloader.Download();

                                    // set up SQL
                                    TheGamesDB.SQL.DownloadManager tgdbSQLDownloader = new TheGamesDB.SQL.DownloadManager();
                                    tgdbSQLDownloader.Download();
                                    break;

                                case QueueItemType.FetchRetroAchievementsMetadata:
                                    RetroAchievements.DownloadManager raDownloader = new RetroAchievements.DownloadManager();
                                    raDownloader.Download();

                                    break;

                                case QueueItemType.FetchIGDBMetadata:
                                    InternetGameDatabase.DownloadManager igdbDownloader = new InternetGameDatabase.DownloadManager();
                                    igdbDownloader.Download();
                                    break;

                                case QueueItemType.FetchGiantBombMetadata:
                                    GiantBomb.MetadataDownload gbDownloader = new GiantBomb.MetadataDownload();

                                    Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Company));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Image), "guid,original_url");
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.ImageTag));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Platform));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Game));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Dlc));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Rating));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Release));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Review));
                                    db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.UserReview));

                                    gbDownloader.DownloadPlatforms();
                                    gbDownloader.DownloadGames();
                                    // gbDownloader.DownloadSubTypes<GiantBomb.Models.GiantBombReviewResponse, GiantBomb.Models.Review>("reviews");
                                    gbDownloader.DownloadSubTypes<GiantBomb.Models.GiantBombUserReviewResponse, GiantBomb.Models.UserReview>("user_reviews");
                                    gbDownloader.DownloadImages();
                                    break;

                                case QueueItemType.AutoMapper:
                                    await AutoMapper.RomAutoMapper();
                                    break;

                                case QueueItemType.Maintenance:
                                    TheGamesDB.JSON.MetadataQuery.RunMaintenance();
                                    break;

                                case QueueItemType.DailyMaintenance:
                                    Maintenance dMaintenance = new Maintenance();
                                    await dMaintenance.RunDailyMaintenance();

                                    break;

                                case QueueItemType.WeeklyMaintenance:
                                    Maintenance wMaintenance = new Maintenance();
                                    await wMaintenance.RunWeeklyMaintenance();

                                    break;

                                case QueueItemType.CacheWarmer:
                                    if (Config.RedisConfiguration.Enabled)
                                    {
                                        // warm all app reports
                                        string cacheKey = RedisConnection.GenerateKey("InsightsReport", 0);

                                        // delete existing cache entry if it exists
                                        if (RedisConnection.GetDatabase(0).KeyExists(cacheKey))
                                        {
                                            RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
                                        }

                                        // generate the report and cache it
                                        _ = await Insights.Insights.GenerateInsightReport(0);

                                        // warm per app reports
                                        DataObjectsList dataObjectsList = await DataObjects.GetDataObjects(DataObjects.DataObjectType.App);

                                        foreach (DataObjectItem item in dataObjectsList.Objects)
                                        {
                                            cacheKey = RedisConnection.GenerateKey("InsightsReport", item.Id);

                                            // delete existing cache entry if it exists
                                            if (RedisConnection.GetDatabase(0).KeyExists(cacheKey))
                                            {
                                                RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
                                            }

                                            // generate the report and cache it
                                            _ = await Insights.Insights.GenerateInsightReport(item.Id);
                                        }
                                    }

                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Log(Logging.LogType.Warning, "Timered Event", "An error occurred", ex);
                            _LastResult = "";
                            _LastError = ex.ToString();
                        }

                        _ForceExecute = false;
                        _ItemState = QueueItemState.Stopped;
                        _LastFinishTime = DateTime.UtcNow;
                        _LastRunDuration = Math.Round((DateTime.UtcNow - _LastRunTime).TotalSeconds, 2);

                        Logging.Log(Logging.LogType.Information, "Timered Event", "Total " + _ItemType + " run time = " + _LastRunDuration);
                    }
                }
            }

            public void ForceExecute()
            {
                _ForceExecute = true;
            }

            public void BlockedState(bool BlockState)
            {
                _IsBlocked = BlockState;
            }
        }

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
            /// Loops all ROMs in the database and attempts to match them to a data object - or creates a new datao object if no match is found
            /// </summary>
            AutoMapper,

            /// <summary>
            /// Reserved for maintenance tasks - no actual background service is tied to this type
            /// </summary>
            Maintenance,

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
            CacheWarmer
        }

        public enum QueueItemState
        {
            NeverStarted,
            Running,
            Stopped,
            Disabled
        }
    }
}

