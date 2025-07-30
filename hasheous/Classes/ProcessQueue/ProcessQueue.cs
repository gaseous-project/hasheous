using System;
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

namespace Classes.ProcessQueue
{
    public static class QueueProcessor
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

                _SaveLastRunTime = true;

                switch (ItemType)
                {
                    case QueueItemType.SignatureIngestor:
                        Task = new SignatureIngestor();
                        break;

                    case QueueItemType.TallyVotes:
                        Task = new TallyVotes();
                        break;

                    case QueueItemType.MetadataMatchSearch:
                        Task = new MetadataMatchSearch();
                        break;

                    case QueueItemType.GetMissingArtwork:
                        Task = new GetMissingArtwork();
                        break;

                    case QueueItemType.FetchVIMMMetadata:
                        Task = new FetchVIMMMetadata();
                        break;

                    case QueueItemType.FetchTheGamesDbMetadata:
                        Task = new FetchTheGamesDbMetadata();
                        break;

                    case QueueItemType.FetchRetroAchievementsMetadata:
                        Task = new FetchRetroAchievementsMetadata();
                        break;

                    case QueueItemType.FetchIGDBMetadata:
                        Task = new FetchIGDBMetadata();
                        break;

                    case QueueItemType.FetchGiantBombMetadata:
                        Task = new FetchGiantBombMetadata();
                        break;

                    case QueueItemType.DailyMaintenance:
                        Task = new DailyMaintenance();
                        break;

                    case QueueItemType.WeeklyMaintenance:
                        Task = new WeeklyMaintenance();
                        break;

                    case QueueItemType.CacheWarmer:
                        Task = new CacheWarmer();
                        break;
                }
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

            public IQueueTask? Task { get; set; } = null;
            public List<QueueItemType> Blocks
            {
                get
                {
                    if (Task != null)
                    {
                        return Task.Blocks;
                    }
                    else
                    {
                        return new List<QueueItemType>();
                    }
                }
            }

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

                            // if we have a task, execute it
                            if (Task != null)
                            {
                                await Task.ExecuteAsync();
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

        public enum QueueItemState
        {
            NeverStarted,
            Running,
            Stopped,
            Disabled
        }
    }
}

