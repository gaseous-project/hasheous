namespace hasheous_taskrunner.Classes.Capabilities
{
    /// <summary>
    /// Provides internet-related capability checks and actions (e.g., connectivity test and HTTP requests).
    /// Obviously internet is required for this capability to work, but this capability allows for testing and utilizing internet features, and ensuring connectivity in environments where internet access may be restricted.
    /// </summary>
    public class InternetCapability : ICapability
    {
        /// <inheritdoc/>
        public int CapabilityId => 0;

        /// <inheritdoc/>
        public bool IsInternalCapability => false;

        /// <inheritdoc/>
        public Dictionary<string, object>? Configuration
        {
            get
            {
                return _configuration;
            }
            set
            {
                if (_configuration == null || _configuration.Count == 0)
                {
                    Dictionary<string, object> configDict = value ?? new Dictionary<string, object>();
                    List<string> addresses = new List<string>();
                    int pingAttempts = 4;

                    if (value != null)
                    {
                        if (value.ContainsKey("test_addresses"))
                        {
                            try
                            {
                                addresses = System.Text.Json.JsonSerializer.Deserialize<List<string>>(value["test_addresses"].ToString() ?? "[]") ?? new List<string>();
                            }
                            catch
                            {
                                addresses = new List<string>();
                            }
                        }

                        if (value.ContainsKey("ping_attempts"))
                        {
                            try
                            {
                                pingAttempts = Convert.ToInt32(value["ping_attempts"]);
                            }
                            catch
                            {
                                pingAttempts = 4;
                            }
                        }
                    }

                    configDict["test_addresses"] = addresses;
                    configDict["ping_attempts"] = pingAttempts;
                    _configuration = configDict;
                }
            }
        }

        private Dictionary<string, object>? _configuration;

        /// <inheritdoc/>
        public async Task<bool> TestAsync()
        {
            List<string> addresses = Configuration?["test_addresses"] as List<string> ?? new List<string>();
            int pingAttempts = Configuration?["ping_attempts"] as int? ?? 4;

            if (addresses.Count == 0)
            {
                // no addresses specified - no capability
                return false;
            }

            try
            {
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    foreach (var address in addresses)
                    {
                        for (int i = 0; i < pingAttempts; i++)
                        {
                            try
                            {
                                var reply = await ping.SendPingAsync(address);
                                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                                {
                                    return true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ping attempt to {address} failed: {ex.Message}");
                            }
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>?> ExecuteAsync(Dictionary<string, object> parameters)
        {
            // Example implementation: perform an internet-related task
            if (parameters.ContainsKey("url"))
            {
                string url = parameters["url"].ToString() ?? "";
                try
                {
                    using (var client = new HttpClient())
                    {
                        var response = await client.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            return new Dictionary<string, object>
                            {
                                { "status", "success" },
                                { "content", content }
                            };
                        }
                        else
                        {
                            return new Dictionary<string, object>
                            {
                                { "status", "error" },
                                { "message", $"Failed to fetch URL. Status code: {response.StatusCode}" }
                            };
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}