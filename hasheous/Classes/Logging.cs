using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using Classes;
using static Classes.Common;

namespace Classes
{
    /// <summary>
    /// Provides logging functionality for events, errors, and diagnostics.
    /// </summary>
    public class Logging
    {
        /// <summary>
        /// Gets or sets a value indicating whether logs should be written only to disk.
        /// </summary>
        public static bool WriteToDiskOnly
        {
            get
            {
                if (Config.LoggingConfiguration.OnlyLogToDisk == true)
                {
                    return true;
                }
                else
                {
                    return _WriteToDiskOnly;
                }
            }
            set
            {
                if (Config.LoggingConfiguration.OnlyLogToDisk == true)
                {
                    // do nothing, this value overrides the setting
                }
                else
                {
                    _WriteToDiskOnly = value;
                }
            }
        }
        private static bool _WriteToDiskOnly = false;

        /// <summary>
        /// Logs an event with the specified type, process, message, and optional exception, and writes to disk or database as configured.
        /// </summary>
        /// <param name="EventType">The type of the log event.</param>
        /// <param name="ServerProcess">The name of the server process generating the log.</param>
        /// <param name="Message">The log message.</param>
        /// <param name="ExceptionValue">The exception associated with the log entry, if any.</param>
        /// <param name="LogToDiskOnly">If true, logs only to disk; otherwise, logs to disk and database as configured.</param>
        static public void Log(LogType EventType, string ServerProcess, string Message, Exception? ExceptionValue = null, bool LogToDiskOnly = false)
        {
            LogItem logItem = new LogItem
            {
                EventTime = DateTime.UtcNow,
                EventType = EventType,
                Process = ServerProcess,
                Message = Message,
                ExceptionValue = Common.ReturnValueIfNull(ExceptionValue, "").ToString()
            };

            bool AllowWrite = false;
            if (EventType == LogType.Debug)
            {
                if (Config.LoggingConfiguration.DebugLogging == true)
                {
                    AllowWrite = true;
                }
            }
            else
            {
                AllowWrite = true;
            }

            if (AllowWrite == true)
            {
                // console output
                string TraceOutput = logItem.EventTime.ToString("yyyyMMdd HHmmss") + ": " + logItem.EventType.ToString() + ": " + logItem.Process + ": " + logItem.Message;
                if (logItem.ExceptionValue != null)
                {
                    TraceOutput += Environment.NewLine + logItem.ExceptionValue.ToString();
                }
                switch (logItem.EventType)
                {
                    case LogType.Information:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;

                    case LogType.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;

                    case LogType.Critical:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;

                    case LogType.Debug:
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        break;

                }
                Console.WriteLine(TraceOutput);
                Console.ResetColor();

                if (WriteToDiskOnly == true)
                {
                    LogToDiskOnly = true;
                }

                if (LogToDiskOnly == false)
                {
                    if (Config.LoggingConfiguration.AlwaysLogToDisk == true)
                    {
                        LogToDisk(logItem, TraceOutput, null);
                    }

                    string correlationId;
                    try
                    {
                        if (CallContext.GetData("CorrelationId").ToString() == null)
                        {
                            correlationId = "";
                        }
                        else
                        {
                            correlationId = CallContext.GetData("CorrelationId").ToString();
                        }
                    }
                    catch
                    {
                        correlationId = "";
                    }

                    string callingProcess;
                    try
                    {
                        if (CallContext.GetData("CallingProcess").ToString() == null)
                        {
                            callingProcess = "";
                        }
                        else
                        {
                            callingProcess = CallContext.GetData("CallingProcess").ToString();
                        }
                    }
                    catch
                    {
                        callingProcess = "";
                    }

                    string callingUser;
                    try
                    {
                        if (CallContext.GetData("CallingUser").ToString() == null)
                        {
                            callingUser = "";
                        }
                        else
                        {
                            callingUser = CallContext.GetData("CallingUser").ToString();
                        }
                    }
                    catch
                    {
                        callingUser = "";
                    }

                    Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                    string sql = "INSERT INTO ServerLogs (EventTime, EventType, Process, Message, Exception, CorrelationId, CallingProcess, CallingUser) VALUES (@EventTime, @EventType, @Process, @Message, @Exception, @correlationid, @callingprocess, @callinguser);";
                    Dictionary<string, object> dbDict = new Dictionary<string, object>();
                    dbDict.Add("EventRententionDate", DateTime.UtcNow.AddDays(Config.LoggingConfiguration.LogRetention * -1));
                    dbDict.Add("EventTime", logItem.EventTime);
                    dbDict.Add("EventType", logItem.EventType);
                    dbDict.Add("Process", logItem.Process);
                    dbDict.Add("Message", logItem.Message);
                    dbDict.Add("Exception", Common.ReturnValueIfNull(logItem.ExceptionValue, "").ToString());
                    dbDict.Add("correlationid", correlationId);
                    dbDict.Add("callingprocess", callingProcess);
                    dbDict.Add("callinguser", callingUser);

                    Task.Run(async () =>
                    {
                        try
                        {
                            await db.ExecuteCMDAsync(sql, dbDict);
                        }
                        catch (Exception ex)
                        {
                            LogToDisk(logItem, TraceOutput, ex);
                        }
                    });
                }
                else
                {
                    LogToDisk(logItem, TraceOutput, null);
                }
            }
        }

        static void LogToDisk(LogItem logItem, string TraceOutput, Exception? exception)
        {
            if (exception != null)
            {
                // dump the error
                File.AppendAllText(Config.LogFilePath, logItem.EventTime.ToString("yyyyMMdd HHmmss") + ": " + logItem.EventType.ToString() + ": " + logItem.Process + ": " + logItem.Message + Environment.NewLine + exception.ToString());


                // something went wrong writing to the db
                File.AppendAllText(Config.LogFilePath, logItem.EventTime.ToString("yyyyMMdd HHmmss") + ": The following event was unable to be written to the log database:");
            }

            File.AppendAllText(Config.LogFilePath, TraceOutput);
        }

        /// <summary>
        /// Retrieves a list of log entries based on the specified search criteria.
        /// </summary>
        /// <param name="model">The search criteria for retrieving logs.</param>
        /// <returns>A list of <see cref="LogItem"/> objects matching the criteria.</returns>
        static public List<LogItem> GetLogs(LogsViewModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "StartIndex", model.StartIndex },
                { "PageNumber", (model.PageNumber - 1) * model.PageSize },
                { "PageSize", model.PageSize }
            };
            string sql = "";

            List<string> whereClauses = new List<string>();

            // handle status criteria
            if (model.Status != null)
            {
                if (model.Status.Count > 0)
                {
                    List<string> statusWhere = new List<string>();
                    for (int i = 0; i < model.Status.Count; i++)
                    {
                        string valueName = "@eventtype" + i;
                        statusWhere.Add(valueName);
                        dbDict.Add(valueName, (int)model.Status[i]);
                    }

                    whereClauses.Add("EventType IN (" + string.Join(",", statusWhere) + ")");
                }
            }

            // handle start date criteria
            if (model.StartDateTime != null)
            {
                dbDict.Add("startdate", model.StartDateTime);
                whereClauses.Add("EventTime >= @startdate");
            }

            // handle end date criteria
            if (model.EndDateTime != null)
            {
                dbDict.Add("enddate", model.EndDateTime);
                whereClauses.Add("EventTime <= @enddate");
            }

            // handle search text criteria
            if (model.SearchText != null)
            {
                if (model.SearchText.Length > 0)
                {
                    dbDict.Add("messageSearch", model.SearchText);
                    whereClauses.Add("MATCH(Message) AGAINST (@messageSearch)");
                }
            }

            if (model.CorrelationId != null)
            {
                if (model.CorrelationId.Length > 0)
                {
                    dbDict.Add("correlationId", model.CorrelationId);
                    whereClauses.Add("CorrelationId = @correlationId");
                }
            }

            if (model.CallingProcess != null)
            {
                if (model.CallingProcess.Length > 0)
                {
                    dbDict.Add("callingProcess", model.CallingProcess);
                    whereClauses.Add("CallingProcess = @callingProcess");
                }
            }

            if (model.CallingUser != null)
            {
                if (model.CallingUser.Length > 0)
                {
                    dbDict.Add("callingUser", model.CallingUser);
                    whereClauses.Add("CallingUser = @callingUser");
                }
            }

            // compile WHERE clause
            string whereClause = "";
            if (whereClauses.Count > 0)
            {
                whereClause = "(" + String.Join(" AND ", whereClauses) + ")";
            }

            // execute query
            if (model.StartIndex == null)
            {
                if (whereClause.Length > 0)
                {
                    whereClause = "WHERE " + whereClause;
                }

                sql = "SELECT ServerLogs.Id, ServerLogs.EventTime, ServerLogs.EventType, ServerLogs.`Process`, ServerLogs.Message, ServerLogs.Exception, ServerLogs.CorrelationId, ServerLogs.CallingProcess, Users.Email FROM ServerLogs LEFT JOIN Users ON ServerLogs.CallingUser = Users.Id " + whereClause + " ORDER BY ServerLogs.Id DESC LIMIT @PageSize OFFSET @PageNumber;";
            }
            else
            {
                if (whereClause.Length > 0)
                {
                    whereClause = "AND " + whereClause;
                }

                sql = "SELECT ServerLogs.Id, ServerLogs.EventTime, ServerLogs.EventType, ServerLogs.`Process`, ServerLogs.Message, ServerLogs.Exception, ServerLogs.CorrelationId, ServerLogs.CallingProcess, Users.Email FROM ServerLogs LEFT JOIN Users ON ServerLogs.CallingUser = Users.Id  WHERE ServerLogs.Id < @StartIndex " + whereClause + " ORDER BY ServerLogs.Id DESC LIMIT @PageSize OFFSET @PageNumber;";
            }
            DataTable dataTable = db.ExecuteCMD(sql, dbDict);

            List<LogItem> logs = new List<LogItem>();
            foreach (DataRow row in dataTable.Rows)
            {
                LogItem log = new LogItem
                {
                    Id = (long)row["Id"],
                    EventTime = DateTime.Parse(((DateTime)row["EventTime"]).ToString("yyyy-MM-ddThh:mm:ss") + 'Z'),
                    EventType = (LogType)row["EventType"],
                    Process = (string)row["Process"],
                    Message = (string)row["Message"],
                    ExceptionValue = (string)row["Exception"],
                    CorrelationId = (string)Common.ReturnValueIfNull(row["CorrelationId"], ""),
                    CallingProcess = (string)Common.ReturnValueIfNull(row["CallingProcess"], ""),
                    CallingUser = (string)Common.ReturnValueIfNull(row["Email"], "")
                };

                logs.Add(log);
            }

            return logs;
        }

        /// <summary>
        /// Asynchronously deletes old log entries from the database based on the configured log retention period.
        /// </summary>
        static public async Task PurgeLogsAsync()
        {
            // delete old logs
            Logging.Log(Logging.LogType.Information, "Maintenance", "Removing logs older than " + Config.LoggingConfiguration.LogRetention + " days");
            long deletedCount = 1;
            long deletedEventCount = 0;
            long maxLoops = 10000;
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "DELETE FROM ServerLogs WHERE EventTime < @EventRetentionDate LIMIT 1000; SELECT ROW_COUNT() AS Count;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "EventRetentionDate", DateTime.UtcNow.AddDays(Config.LoggingConfiguration.LogRetention * -1) }
            };
            while (deletedCount > 0)
            {
                DataTable deletedCountTable = await db.ExecuteCMDAsync(sql, dbDict);
                deletedCount = (long)deletedCountTable.Rows[0][0];
                deletedEventCount += deletedCount;

                Logging.Log(Logging.LogType.Information, "Maintenance", "Deleted " + deletedCount + " log entries");

                // check if we've hit the limit
                maxLoops -= 1;
                if (maxLoops <= 0)
                {
                    Logging.Log(Logging.LogType.Warning, "Maintenance", "Hit the maximum number of loops for deleting logs. Stopping.");
                    break;
                }
            }
            Logging.Log(Logging.LogType.Information, "Maintenance", "Deleted " + deletedEventCount + " log entries");

            // time to delete any old log files from disk
            string[] files = Directory.GetFiles(Config.LogPath);

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                if (fi.LastWriteTime < DateTime.Now.AddDays(Config.LoggingConfiguration.LogRetention * -1))
                {
                    fi.Delete();
                }
            }
        }

        /// <summary>
        /// Specifies the type of log entry.
        /// </summary>
        public enum LogType
        {
            /// <summary>
            /// Represents an informational log entry.
            /// </summary>
            Information = 0,
            /// <summary>
            /// Represents a debug log entry.
            /// </summary>
            Debug = 1,
            /// <summary>
            /// Represents a warning log entry.
            /// </summary>
            Warning = 2,
            /// <summary>
            /// Represents a critical log entry.
            /// </summary>
            Critical = 3
        }

        /// <summary>
        /// Represents a single log entry.
        /// </summary>
        public class LogItem
        {
            /// <summary>
            /// Gets or sets the unique identifier for the log entry.
            /// </summary>
            public long Id { get; set; }

            /// <summary>
            /// Gets or sets the date and time when the log event occurred (in UTC).
            /// </summary>
            public DateTime EventTime { get; set; }

            /// <summary>
            /// Gets or sets the type of the log event.
            /// </summary>
            public LogType? EventType { get; set; }

            /// <summary>
            /// Gets or sets the process or component that generated the log entry.
            /// </summary>
            public string Process { get; set; } = "";

            /// <summary>
            /// Gets or sets the correlation identifier for related log entries.
            /// </summary>
            public string CorrelationId { get; set; } = "";

            /// <summary>
            /// Gets or sets the background process or API endpoint that generated the log entry.
            /// </summary>
            public string? CallingProcess { get; set; } = "";

            /// <summary>
            /// Gets or sets the user that generated the log entry.
            /// </summary>
            public string? CallingUser { get; set; } = "";

            private string _Message = "";

            /// <summary>
            /// Gets or sets the message associated with the log entry.
            /// </summary>
            public string Message
            {
                get
                {
                    return _Message;
                }
                set
                {
                    _Message = value;
                }
            }
            /// <summary>
            /// Gets or sets the exception details associated with the log entry, if any.
            /// </summary>
            public string? ExceptionValue { get; set; }
        }

        /// <summary>
        /// Describes the log search criteria
        /// </summary>
        public class LogsViewModel
        {
            /// <summary>
            /// The log Id to start on when using paging - required when using paging
            /// </summary>
            public long? StartIndex { get; set; }

            /// <summary>
            /// The page of the logs to load
            /// </summary>
            /// 
            [Required()]
            public int PageNumber { get; set; } = 1;

            /// <summary>
            /// The size of the page to load
            /// </summary>
            /// 
            [Required()]
            public int PageSize { get; set; } = 100;

            /// <summary>
            /// An array of log status to filter on
            /// </summary>
            public List<LogType> Status { get; set; } = new List<LogType>();

            /// <summary>
            /// The start date and time of the returned logs - all dates are in UTC
            /// </summary>
            public DateTime? StartDateTime { get; set; }

            /// <summary>
            /// The end date and time of the returned logs - all dates are in UTC
            /// </summary>
            public DateTime? EndDateTime { get; set; }

            /// <summary>
            /// Text to search the logs for
            /// </summary>
            public string? SearchText { get; set; }

            /// <summary>
            /// Correlation id of the events to search for - use to find related events
            /// </summary>
            public string? CorrelationId { get; set; }

            /// <summary>
            /// The background process or API endpoint that generated the log entry
            /// </summary>
            public string? CallingProcess { get; set; }

            /// <summary>
            /// The user that generated the log entry
            /// </summary>
            public string? CallingUser { get; set; }
        }
    }
}

