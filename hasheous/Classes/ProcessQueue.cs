﻿using System;
using Classes;
using hasheous_server.Classes;
using static Classes.Common;

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
                _LastRunTime = DateTime.Parse(Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ")));
                _Interval = ExecutionInterval;
                _AllowManualStart = AllowManualStart;
                _RemoveWhenStopped = RemoveWhenStopped;
            }

            public QueueItem(QueueItemType ItemType, int ExecutionInterval, List<QueueItemType> Blocks, bool AllowManualStart = true, bool RemoveWhenStopped = false)
            {
                _ItemType = ItemType;
                _ItemState = QueueItemState.NeverStarted;
                _LastRunTime = DateTime.Parse(Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ")));
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
                    return DateTime.Parse(Config.ReadSetting("LastRun_" + _ItemType.ToString(), DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ssZ")));
                }
                set
                {
                    if (_SaveLastRunTime == true)
                    {
                        Config.SetSetting("LastRun_" + _ItemType.ToString(), value.ToString("yyyy-MM-ddThh:mm:ssZ"));
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

            public void Execute()
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
                                    submissions.TallyVotes();
                                    break;

                                case QueueItemType.GetMissingArtwork:
                                    BackgroundMetadataMatcher.BackgroundMetadataMatcher tMatcher = new BackgroundMetadataMatcher.BackgroundMetadataMatcher();
                                    tMatcher.GetGamesWithoutArtwork();
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
            /// Fetches missing artwork for game data objects
            /// </summary>
            GetMissingArtwork
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

