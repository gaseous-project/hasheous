using hasheous_taskrunner.Classes;

namespace hasheous_taskrunner.Classes.Communication
{
    /// <summary>
    /// Common communication utilities and helpers.
    /// </summary>
    public static class Common
    {
        static Common()
        {
            // Congigure HttpHelper
            TaskRunner.Classes.HttpHelper.BaseUri = Config.Configuration["HostAddress"];
            TaskRunner.Classes.HttpHelper.Headers = new Dictionary<string, string>
            {
                { "X-Client-Host", Config.Configuration["ClientName"] },
                { "X-Client-Version", Config.ClientVersion.ToString() }
            };
        }

        private static Dictionary<string, string> registrationInfo = new Dictionary<string, string>();

        /// <summary>
        /// Determines whether the task runner is registered with the host by checking for both
        /// the client identifier and the client API key in the registration info dictionary.
        /// </summary>
        public static bool IsRegistered()
        {
            return registrationInfo.ContainsKey("client_id") && registrationInfo.ContainsKey("client_api_key");
        }

        /// <summary>
        /// Sets registration information for the task runner host.
        /// </summary>
        /// <param name="info">A dictionary containing registration keys and values (for example "client_id" and "client_api_key").</param>
        public static void SetRegistrationInfo(Dictionary<string, string> info)
        {
            registrationInfo = info;
            Config.SetAuthValue("client_id", registrationInfo["client_id"]);
        }

        /// <summary>
        /// Performs an HTTP POST to the specified URL with the provided content and deserializes the response to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The expected response type.</typeparam>
        /// <param name="url">The request URL (relative or absolute).</param>
        /// <param name="contentValue">The object to send as the request body.</param>
        /// <returns>A task that returns the deserialized response of type <typeparamref name="T"/>.</returns>
        public static async Task<T> Post<T>(string url, object contentValue)
        {
            AddClientSecretHeader();
            return await TaskRunner.Classes.HttpHelper.Post<T>(url, contentValue);
        }

        /// <summary>
        /// Performs an HTTP PUT to the specified URL with the provided content and deserializes the response to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The expected response type.</typeparam>
        /// <param name="url">The request URL (relative or absolute).</param>
        /// <param name="contentValue">The object to send as the request body.</param>
        /// <returns>A task that returns the deserialized response of type <typeparamref name="T"/>.</returns>
        public static async Task<T> Put<T>(string url, object contentValue)
        {
            AddClientSecretHeader();
            return await TaskRunner.Classes.HttpHelper.Put<T>(url, contentValue);
        }

        /// <summary>
        /// Performs an HTTP GET to the specified URL and deserializes the response to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The expected response type.</typeparam>
        /// <param name="url">The request URL (relative or absolute).</param>
        /// <returns>A task that returns the deserialized response of type <typeparamref name="T"/>.</returns>
        public static async Task<T> Get<T>(string url)
        {
            AddClientSecretHeader();
            return await TaskRunner.Classes.HttpHelper.Get<T>(url);
        }

        /// <summary>
        /// Performs an HTTP DELETE to the specified URL.
        /// </summary>
        /// <param name="url">The request URL (relative or absolute).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task Delete(string url)
        {
            AddClientSecretHeader();
            await TaskRunner.Classes.HttpHelper.Delete(url);
        }

        private static void AddClientSecretHeader()
        {
            if (IsRegistered())
            {
                if (!TaskRunner.Classes.HttpHelper.Headers.ContainsKey("X-TaskWorker-API-Key"))
                {
                    TaskRunner.Classes.HttpHelper.Headers.Add("X-TaskWorker-API-Key", registrationInfo["client_api_key"]);
                }
            }
            else
            {
                // not registered - remove header if it exists and throw an error
                if (TaskRunner.Classes.HttpHelper.Headers.ContainsKey("X-TaskWorker-API-Key"))
                {
                    TaskRunner.Classes.HttpHelper.Headers.Remove("X-TaskWorker-API-Key");
                }
                throw new InvalidOperationException("Task runner is not registered. Cannot add client secret header.");
            }
        }
    }
}