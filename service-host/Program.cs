using Classes;
using Classes.ProcessQueue;
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
string reportingServerUrl = null;
string correlationId = null;

for (int i = 0; i < cmdArgs.Length; i++)
{
    if (cmdArgs[i] == "--service" && i + 1 < cmdArgs.Length)
    {
        serviceName = cmdArgs[i + 1];
    }
    else if (cmdArgs[i] == "--reportingserver" && i + 1 < cmdArgs.Length)
    {
        reportingServerUrl = cmdArgs[i + 1];
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

// If no reporting server URL is provided, abort
if (string.IsNullOrEmpty(reportingServerUrl))
{
    Console.WriteLine("Error: No reporting server URL provided.");
    Help.DisplayHelp();
    return;
}

// If a correlation ID is provided, set it in the CallContext
if (string.IsNullOrEmpty(correlationId))
{
    // If no correlation ID is provided, generate a new one
    correlationId = Guid.NewGuid().ToString();
}
CallContext.SetData("CorrelationId", correlationId);
CallContext.SetData("CallingProcess", taskType.ToString());
CallContext.SetData("CallingUser", "System");

// Initialize the configuration
Config.LogName = serviceName;

// Start the specified service
Logging.Log(Logging.LogType.Information, serviceName, $"Starting service with reporting server '{reportingServerUrl}'...");

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

    default:
        Console.WriteLine($"Error: Unsupported service type '{serviceName}'.");
        return;
}

// start the task
try
{
    await Task.ExecuteAsync();
}
catch (Exception ex)
{
    Logging.Log(Logging.LogType.Critical, serviceName, $"Failed to start service: {ex.Message}");
    // terminate the application with a non-zero exit code
    Environment.Exit(1);
}

// Log the successful completion of the service
Logging.Log(Logging.LogType.Information, serviceName, "Service completed successfully.");
Environment.Exit(0); // exit with success code