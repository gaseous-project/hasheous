using Authentication;
using Classes.ProcessQueue;

namespace Classes
{
    public class BackgroundTasks
    {
        private static HttpClient _httpClient = new HttpClient();

        public static async Task<object> ManageQueueItem(Guid ProcessId, bool? ForceRun = null, bool? Enabled = null, bool IsRemote = true)
        {
            // Validate ProcessId
            if (ProcessId == Guid.Empty)
            {
                throw new ArgumentException("Invalid ProcessId.");
            }

            // Fail early if ForceRun and Enabled are both null
            if (ForceRun == null && Enabled == null)
            {
                throw new ArgumentException("At least one of ForceRun or Enabled must be specified.");
            }

            // loop all keys in GetServerData, and get the queue item for the provided ProcessId
            // if the ProcessId is not found, return NotFound
            // if the ProcessId is local, handle it directly
            // if the ProcessId is remote, send a request to the reporting server to manage the queue item
            Dictionary<string, object> queueItems = await GetServerData(IsRemote);
            foreach (var key in queueItems.Keys)
            {
                switch (key)
                {
                    case "Local":
                        // Handle local queue items
                        // Cast to List<QueueProcessor.QueueItem> to find the item
                        List<QueueProcessor.QueueItem> localQueueItems = (List<QueueProcessor.QueueItem>)queueItems[key];
                        // Find the queue item with the matching ProcessId
                        QueueProcessor.QueueItem? queueItem = localQueueItems.FirstOrDefault(qi => qi.ProcessId == ProcessId);
                        if (queueItem != null)
                        {
                            // Handle local queue item management
                            if (ForceRun.HasValue)
                            {
                                if (ForceRun.Value == true)
                                {
                                    // Force run the queue item
                                    queueItem.ForceExecute();
                                }
                            }

                            if (Enabled.HasValue)
                            {
                                // Enable or disable the queue item
                                queueItem.Enabled = Enabled.Value;
                            }

                            // Return the updated queue item
                            return new QueueProcessor.SimpleQueueItem
                            {
                                ProcessId = queueItem.ProcessId,
                                ItemType = queueItem.ItemType,
                                ItemState = queueItem.ItemState,
                                LastRunTime = queueItem.LastRunTime,
                                LastFinishTime = queueItem.LastFinishTime,
                                LastRunDuration = queueItem.LastRunDuration, // Use the updated property
                                NextRunTime = queueItem.NextRunTime,
                                Interval = queueItem.Interval,
                                LastResult = queueItem.LastResult,
                                LastReport = queueItem.LastReport
                            };
                        }
                        break;
                    case "Remote":
                        // Handle remote queue items
                        // Cast to List<QueueProcessor.SimpleQueueItem> to find the item
                        List<QueueProcessor.SimpleQueueItem> remoteQueueItems = (List<QueueProcessor.SimpleQueueItem>)queueItems[key];
                        // Find the queue item with the matching ProcessId
                        QueueProcessor.SimpleQueueItem? rQueueItem = remoteQueueItems.FirstOrDefault(qi => qi.ProcessId == ProcessId);
                        if (remoteQueueItems != null)
                        {
                            // Handle remote queue item management
                            string arguments = "";
                            if (ForceRun.HasValue)
                            {
                                arguments += $"ForceRun={ForceRun.Value}&";
                            }
                            if (Enabled.HasValue)
                            {
                                arguments += $"Enabled={Enabled.Value}&";
                            }
                            if (arguments.EndsWith("&"))
                            {
                                arguments = arguments.TrimEnd('&');
                            }

                            if (!string.IsNullOrEmpty(arguments))
                            {
                                try
                                {
                                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Config.ServiceCommunication.ReportingServerUrl + $"/api/v1.0/BackgroundTasks/{ProcessId}?{arguments}");
                                    request.Headers.Add(InterHostApiKey.ApiKeyHeaderName, Config.ServiceCommunication.APIKey);

                                    HttpResponseMessage response = await _httpClient.SendAsync(request);

                                    if (response.IsSuccessStatusCode)
                                    {
                                        string content = await response.Content.ReadAsStringAsync();
                                        var remoteQueueItem = Newtonsoft.Json.JsonConvert.DeserializeObject<QueueProcessor.SimpleQueueItem>(content, new Newtonsoft.Json.JsonSerializerSettings
                                        {
                                            // Add any necessary settings here
                                        });
                                        return remoteQueueItem;
                                    }
                                    else
                                    {
                                        Logging.Log(Logging.LogType.Warning, "Background Tasks", $"Error managing remote queue item: {response.StatusCode} - {response.ReasonPhrase}");
                                        throw new InvalidOperationException($"Error managing remote queue item: {response.StatusCode} - {response.ReasonPhrase}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logging.Log(Logging.LogType.Warning, "Background Tasks", $"Exception managing remote queue item: {ex.Message}", ex);
                                    throw new Exception($"Exception managing remote queue item: {ex.Message}", ex);
                                }
                            }
                            else
                            {
                                throw new ArgumentException("No valid parameters provided for remote queue item management.");
                            }
                        }
                        break;
                    default:
                        throw new KeyNotFoundException($"Unknown queue type: {key}");
                }
            }

            // If we reach here, the ProcessId was not found in any queue
            throw new KeyNotFoundException($"Queue item with ProcessId {ProcessId} not found.");
        }

        public static async Task<Dictionary<string, object>> GetServerData(bool getRemote = true)
        {
            Dictionary<string, object> queue = new Dictionary<string, object>();

            // add local queue items
            queue.Add("Local", QueueProcessor.QueueItems);

            // add remote queue items - connect to reporting server url with api key
            if (getRemote)
            {
                if (Config.ServiceCommunication.ReportingServerUrl != null && Config.ServiceCommunication.APIKey != null)
                {
                    try
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Config.ServiceCommunication.ReportingServerUrl + "/api/v1.0/BackgroundTasks");
                        request.Headers.Add(InterHostApiKey.ApiKeyHeaderName, Config.ServiceCommunication.APIKey);
                        HttpResponseMessage response = await _httpClient.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            string content = await response.Content.ReadAsStringAsync();

                            var remoteQueue = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<QueueProcessor.SimpleQueueItem>>>(content, new Newtonsoft.Json.JsonSerializerSettings
                            {

                            }) ?? new Dictionary<string, List<QueueProcessor.SimpleQueueItem>>();

                            // Add remote queue items to the dictionary
                            queue.Add("Remote", remoteQueue["Local"]);
                        }
                        else
                        {
                            // Log or handle the error response as needed
                            Logging.Log(Logging.LogType.Warning, "Background Tasks", $"Error fetching remote queue: {response.StatusCode} - {response.ReasonPhrase}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions such as network errors
                        Logging.Log(Logging.LogType.Warning, "Background Tasks", $"Exception fetching remote queue: {ex.Message}", ex);
                    }
                }
            }

            return queue;
        }
    }
}