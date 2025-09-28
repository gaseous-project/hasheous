using System;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
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
        /// <summary>
        /// Gets the list of queue items to be processed.
        /// </summary>
        public static List<QueueItem> QueueItems = new List<QueueItem>();

        /// <summary>
        /// Represents an item in the process queue with its execution logic and state.
        /// </summary>
        public class QueueItem
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="QueueItem"/> class for serialization purposes.
            /// </summary>
            public QueueItem()
            {
                // Default constructor for serialization purposes
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="QueueItem"/> class.
            /// </summary>
            /// <param name="ItemType">The type of the queue item.</param>
            /// <param name="ExecutionInterval">The interval in minutes between executions.</param>
            /// <param name="InProcess"> 
            /// Whether the item will run in this process or not.
            /// If false, it will be run in a separate process.
            ///  </param>
            /// <param name="AllowManualStart">Whether manual start is allowed.</param>
            /// <param name="RemoveWhenStopped">Whether to remove the item when stopped.</param>
            public QueueItem(QueueItemType ItemType, int ExecutionInterval, bool InProcess, bool AllowManualStart = true, bool RemoveWhenStopped = false)
            {
                _ItemType = ItemType;
                _ItemState = QueueItemState.NeverStarted;
                _LastRunTime = Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow);
                _Interval = ExecutionInterval;
                _InProcess = InProcess;
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

                    case QueueItemType.FetchRedumpMetadata:
                        Task = new FetchRedumpMetadata();
                        break;

                    case QueueItemType.FetchTOSECMetadata:
                        Task = new FetchTOSECMetadata();
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

                    case QueueItemType.MetadataMapDump:
                        Task = new Dumps();
                        break;
                }
            }

            /// <summary>
            /// Gets the unique identifier for the process associated with this queue item.
            /// </summary>
            public readonly Guid ProcessId = Guid.NewGuid();

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
            private bool _InProcess = false;
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
            public bool Enabled
            {
                get
                {
                    return Config.ReadSetting<bool>("Enabled_" + _ItemType.ToString(), true);
                }
                set
                {
                    Config.SetSetting("Enabled_" + _ItemType.ToString(), value);
                }
            }
            public object? Options { get; set; } = null;

            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
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
                            // if we have a task, execute it
                            if (Task != null && _InProcess == true)
                            {
                                await Task.ExecuteAsync();
                            }
                            else
                            {
                                // if we don't have a task, execute the service-host with item type
                                string[] args = new string[] { "service-host.dll", "--service", _ItemType.ToString(), "--reportingserver", Config.ServiceCommunication.ReportingServerUrl, "--correlationid", _CorrelationId };
                                var process = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "dotnet",
                                        WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                        Arguments = string.Join(" ", args),
                                        UseShellExecute = false,
                                        RedirectStandardOutput = false,
                                        RedirectStandardError = false,
                                        CreateNoWindow = true
                                    }
                                };
                                Logging.Log(Logging.LogType.Information, "Timered Event", "Executing service-host with arguments: " + string.Join(" ", args));

                                // start the process
                                process.Start();

                                await process.WaitForExitAsync();

                                if (process.ExitCode != 0)
                                {
                                    throw new Exception("Service-host exited with code " + process.ExitCode);
                                }
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

        public class SimpleQueueItem
        {
            public Guid ProcessId { get; set; }
            public QueueItemType ItemType { get; set; }
            public QueueItemState ItemState { get; set; }
            public DateTime LastRunTime { get; set; }
            public DateTime LastFinishTime { get; set; }
            public double LastRunDuration { get; set; }
            public DateTime NextRunTime { get; set; }
            public int Interval { get; set; }
            public string LastResult { get; set; }
            public bool Force { get; set; }
            public bool AllowManualStart { get; set; }
            public bool RemoveWhenStopped { get; set; }
            public string CorrelationId { get; set; }
            public bool IsBlocked { get; set; }
            public bool Enabled { get; set; }
            public List<QueueItemType> Blocks { get; set; }
        }
    }
}

