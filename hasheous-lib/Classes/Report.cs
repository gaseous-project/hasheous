namespace hasheous_server.Classes.Report
{
    /// <summary>
    /// Provides a shared report model for the host service and related components.
    /// </summary>
    public class Report
    {
        /// <summary>
        /// Initializes a new instance of the Report class.
        /// </summary>
        /// <param name="reportingServerUrl">The URL of the reporting server. Should only contain the base address (scheme, host and port). Example: "https://example.com:443"</param>
        /// <param name="processId">The process identifier.</param>
        /// <param name="correlationId">The correlation identifier for tracking.</param>
        public Report(string reportingServerUrl, string processId, string correlationId)
        {
            this.processId = processId;
            this.correlationId = correlationId;

            this.httpClient.BaseAddress = new Uri(reportingServerUrl);
        }

        private HttpClient httpClient = new HttpClient();

        private string processId;
        private string correlationId;

        /// <summary>
        /// Shared instance of the report model used to aggregate reporting data across the host process.
        /// </summary>
        private hasheous_server.Models.ReportModel _reportModel = new hasheous_server.Models.ReportModel();

        /// <summary>
        /// Sends a progress update to the shared report model.
        /// </summary>
        /// <param name="progressItemKey">The unique key identifying the progress item.</param>
        /// <param name="count">The current progress count.</param>
        /// <param name="total">The total count to complete.</param>
        /// <param name="description">A description of the progress item.</param>
        public async System.Threading.Tasks.Task SendAsync(string progressItemKey, int? count, int? total, string description)
        {
            if (_reportModel.Progress.ContainsKey(progressItemKey))
            {
                // Update existing progress item
                var item = _reportModel.Progress[progressItemKey];
                item.count = count;
                item.total = total;
                item.description = description;
            }
            else
            {
                // Add new progress item
                _reportModel.Progress[progressItemKey] = new hasheous_server.Models.ReportModel.ProgressItem
                {
                    count = count,
                    total = total,
                    description = description
                };
            }

            // send to reporting server if configured
            if (this.httpClient.BaseAddress != null)
            {
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(_reportModel);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                try
                {
                    string url = $"/api/v1/BackgroundTasks/{this.processId.ToString()}/{this.correlationId.ToString()}/report";
                    Console.WriteLine($"Sending report to {httpClient.BaseAddress}{url}");
                    var response = await httpClient.PostAsync(url, content);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    // Log the error but do not throw
                    Console.WriteLine($"Failed to send report to server: {ex.Message}");
                }
            }
        }
    }
}