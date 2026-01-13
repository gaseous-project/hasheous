using System.Net;
using hasheous_taskrunner.Classes;

namespace hasheous_taskrunner.Classes.Communication
{
    /// <summary>
    /// Provides registration utilities for the task runner communication subsystem.
    /// Add initialization and registration helpers here as needed.
    /// </summary>
    public static class Registration
    {
        private static DateTime lastRegistrationTime = DateTime.MinValue;
        private static readonly TimeSpan registrationInterval = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Initializes registration-related resources; implement registration logic here.
        /// </summary>
        /// <param name="parameters">The parameters required for registration.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task Initialize(Dictionary<string, object> parameters)
        {
            // attempt to register - keep trying until successful
            string registrationUrl = $"{Config.BaseUriPath}/clients?clientName={WebUtility.UrlEncode(Config.Configuration["ClientName"])}&clientVersion={WebUtility.UrlEncode(Config.ClientVersion.ToString())}";
            Console.WriteLine("Registering task worker with host...");
            Console.WriteLine("Registration URL: " + registrationUrl);
            if (Config.GetAuthValue("client_id") != null)
            {
                Console.WriteLine("Client is already registered with ID: " + Config.GetAuthValue("client_id"));
                parameters.Add("client_id", Config.GetAuthValue("client_id"));
            }
            TaskRunner.Classes.HttpHelper.BaseUri = Config.Configuration["HostAddress"];
            TaskRunner.Classes.HttpHelper.Headers.Add("X-API-Key", Config.Configuration["APIKey"]);

            // start registration loop
            int retryCount = 0;
            while (true)
            {
                if (Common.IsRegistered())
                {
                    break;
                }

                try
                {
                    retryCount++;
                    Dictionary<string, string>? registrationInfo = await TaskRunner.Classes.HttpHelper.Post<Dictionary<string, string>>(registrationUrl, parameters);
                    if (registrationInfo == null)
                    {
                        throw new InvalidOperationException("Registration response was null.");
                    }

                    // set registration info
                    Console.WriteLine("Registration completed, setting registration info...");
                    Console.WriteLine("Client ID: " + registrationInfo["client_id"]);
                    Common.SetRegistrationInfo(registrationInfo);

                    // remmove API key header after registration
                    TaskRunner.Classes.HttpHelper.Headers.Remove("X-API-Key");

                    // checking registration requirements
                    if (registrationInfo.ContainsKey("required_capabilities"))
                    {
                        string requiredCapabilitiesJson = registrationInfo["required_capabilities"];
                        var requiredCapabilities = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(requiredCapabilitiesJson);
                        if (requiredCapabilities != null)
                        {
                            var capabilityResults = await Capabilities.Capabilities.CheckCapabilitiesAsync(requiredCapabilities);
                            Config.RegistrationParameters["capabilities"] = capabilityResults;
                            await UpdateRegistrationInfo();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Registration failed: {ex.Message}");
                    if (retryCount >= 10)
                    {
                        Console.WriteLine("Maximum retry attempts reached. Aborting.");
                        Environment.Exit(1);
                    }
                    Console.WriteLine($"Retrying in 5 seconds... (Attempt {retryCount})");
                    System.Threading.Thread.Sleep(5000);
                }
            }
        }

        /// <summary>
        /// Updates the registration information on the host for the currently registered client.
        /// </summary>
        /// <returns>A task that represents the asynchronous update operation.</returns>
        public async static Task UpdateRegistrationInfo()
        {
            if (Common.IsRegistered())
            {
                string updateUrl = $"{Config.BaseUriPath}/clients/{Config.GetAuthValue("client_id")}";
                Console.WriteLine("Updating task worker registration info...");
                try
                {
                    await Common.Put<string?>(updateUrl, Config.RegistrationParameters);
                    Console.WriteLine("Registration info update successful.");
                    lastRegistrationTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to update registration info: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Unregisters the client from the host and cleans up registration-related resources.
        /// </summary>
        public async static Task Unregister()
        {
            if (Common.IsRegistered())
            {
                string unregisterUrl = $"{Config.BaseUriPath}/clients/{Config.GetAuthValue("client_id")}";
                Console.WriteLine("Unregistering task worker from host...");
                try
                {
                    await Common.Delete(unregisterUrl);
                    Console.WriteLine("Unregistration successful.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to unregister: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Re-registers the task worker if the registration interval has elapsed.
        /// </summary>
        public async static Task ReRegisterIfDue()
        {
            if (DateTime.UtcNow - lastRegistrationTime >= registrationInterval)
            {
                Console.WriteLine("Re-registering task worker with host...");
                await Initialize(Config.RegistrationParameters);
                lastRegistrationTime = DateTime.UtcNow;
            }
        }
    }
}