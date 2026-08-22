using Classes;
using Classes.ProcessQueue;
using hasheous_server.Classes;
using hasheous_server.Classes.Report;
using HasheousServerHost.Classes.CLI;
using static Classes.Common;

// start command line parser
string[] cmdArgs = Environment.GetCommandLineArgs();

// Parse the command line arguments
if (cmdArgs.Length == 1 || cmdArgs.Contains("--help"))
{
    // No arguments provided, display usage
    Help.DisplayHelp();
    return;
}

// Check for version argument
if (cmdArgs.Contains("--version"))
{
    Console.WriteLine("Hasheous Server Host Version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
    return;
}

// process other command line arguments
string serviceName = null;
string subServiceName = null;
string reportingServerUrl = null;
string processId = Guid.Empty.ToString();
string correlationId = null;

for (int i = 0; i < cmdArgs.Length; i++)
{
    if (cmdArgs[i] == "--service" && i + 1 < cmdArgs.Length)
    {
        string serviceNameArg = cmdArgs[i + 1];
        // Check if the service name contains a colon, indicating a sub-service
        var argParts = serviceNameArg.Split(':');
        if (argParts.Length > 2)
        {
            Console.WriteLine($"Error: Invalid service name '{serviceNameArg}'. Too many colons.");
            Help.DisplayHelp();
            return;
        }
        else if (argParts.Length == 2)
        {
            serviceName = argParts[0];
            subServiceName = argParts[1];
        }
        else
        {
            serviceName = serviceNameArg;
        }
    }
    else if (cmdArgs[i] == "--reportingserver" && i + 1 < cmdArgs.Length)
    {
        reportingServerUrl = cmdArgs[i + 1];
    }
    else if (cmdArgs[i] == "--processid" && i + 1 < cmdArgs.Length)
    {
        processId = cmdArgs[i + 1];
    }
    else if (cmdArgs[i] == "--correlationid" && i + 1 < cmdArgs.Length)
    {
        correlationId = cmdArgs[i + 1];
    }
}

// If no service name is provided, display help
if (string.IsNullOrEmpty(serviceName))
{
    Console.WriteLine("Error: No service name provided.");
    Help.DisplayHelp();
    return;
}

// verify the service name can be parsed as Classes.ProcessQueue.QueueItemType, and is not "All" or "NotConfigured"
if (!Enum.TryParse(serviceName, out QueueItemType taskType) || taskType == QueueItemType.All || taskType == QueueItemType.NotConfigured)
{
    Console.WriteLine($"Error: Invalid service name '{serviceName}'.");
    Help.DisplayHelp();
    return;
}

// If a sub-service name is provided, verify it can be parsed as a DATImport.IDATFileImport implementation
object? serviceObject = null;
if (!string.IsNullOrEmpty(subServiceName))
{
    // Check if the sub-service name corresponds to a registered DATImport.IDATFileImport implementation
    var matchingIngestor = DATImport.SignatureIngestor.DATImporters.FirstOrDefault(i => (i.GetType().FullName ?? i.GetType().Name).ToString().Equals(subServiceName, StringComparison.OrdinalIgnoreCase) && i.IsEnabled);
    if (matchingIngestor == null)
    {
        Console.WriteLine($"Error: Invalid sub-service name '{subServiceName}'. No matching registered DATImport.IDATFileImport implementation found.");
        Help.DisplayHelp();
        return;
    }
    if (matchingIngestor is DATImport.IDATFileImport datIngestor)
    {
        serviceObject = datIngestor;
    }
    else
    {
        Console.WriteLine($"Error: The matching registered DATImport.IDATFileImport implementation for '{subServiceName}' is not of type DATImport.IDATFileImport.");
        Help.DisplayHelp();
        return;
    }
}

// If no reporting server URL is provided, abort
if (string.IsNullOrEmpty(reportingServerUrl))
{
    Console.WriteLine("Error: No reporting server URL provided. Reporting to console only.");
    // Help.DisplayHelp();
    // return;
}

// If a correlation ID is provided, set it in the CallContext
if (string.IsNullOrEmpty(correlationId))
{
    // If no correlation ID is provided, generate a new one
    correlationId = Guid.NewGuid().ToString();
}
CallContext.SetData("ProcessId", processId);
CallContext.SetData("CorrelationId", correlationId);
CallContext.SetData("CallingProcess", taskType.ToString());
CallContext.SetData("CallingUser", "System");

// Initialize the configuration
Config.LogName = serviceName;

// setup the reporting instance
Report report = new Report(reportingServerUrl, processId, correlationId);
Logging.report = report;

// Start the specified service
Logging.Log(Logging.LogType.Information, serviceName, $"Starting service with reporting server '{reportingServerUrl}'...");
Logging.SendReport(Config.LogName, null, null, "Service starting");

// Initialize the service with the provided configuration
IQueueTask? Task;

switch (taskType)
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

    case QueueItemType.FetchIGDBMetadata:
        Task = new FetchIGDBMetadata();
        break;

    case QueueItemType.FetchGiantBombMetadata:
        Task = new FetchGiantBombMetadata();
        break;

    case QueueItemType.FetchLaunchBoxMetadata:
        Task = new FetchLaunchBoxMetadata();
        break;

    case QueueItemType.SyncSupporterStatus:
        Task = new SyncSupporterStatus();
        break;

    case QueueItemType.HourlyMaintenance:
        Task = new HourlyMaintenance();
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

    case QueueItemType.TaskResultParser:
        Task = new TaskResultParser();
        break;

    default:
        Console.WriteLine($"Error: Unsupported service type '{serviceName}'.");
        return;
}

// start the task
try
{
    Config.database = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
    await Task.ExecuteAsync(serviceObject);
}
catch (Exception ex)
{
    Logging.Log(Logging.LogType.Critical, serviceName, $"An error occurred while executing service.", ex);
    // terminate the application with a non-zero exit code
    Environment.Exit(1);
}

// Log the successful completion of the service
Logging.Log(Logging.LogType.Information, serviceName, "Service completed successfully.");
Environment.Exit(0); // exit with success code