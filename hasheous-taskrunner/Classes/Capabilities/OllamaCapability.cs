using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace hasheous_taskrunner.Classes.Capabilities
{
    /// <summary>
    /// Capability to interact with an Ollama instance: connectivity test, model pull, and prompt execution.
    /// </summary>
    public class OllamaCapability : ICapability
    {
        /// <inheritdoc/>
        public int CapabilityId => 20;

        /// <inheritdoc/>
        public bool IsInternalCapability => true;

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

                    string ollamaUrl = "http://host.docker.internal:11434";
                    string model = string.Empty;
                    string prompt = string.Empty;

                    if (value != null)
                    {
                        if (value.ContainsKey("ollama_url"))
                        {
                            try
                            {
                                ollamaUrl = Convert.ToString(value["ollama_url"]) ?? "http://host.docker.internal:11434";
                            }
                            catch
                            {
                                ollamaUrl = "http://host.docker.internal:11434";
                            }
                        }

                        if (value.ContainsKey("model"))
                        {
                            try
                            {
                                model = Convert.ToString(value["model"]) ?? string.Empty;
                            }
                            catch
                            {
                                model = string.Empty;
                            }
                        }

                        if (value.ContainsKey("prompt"))
                        {
                            try
                            {
                                prompt = Convert.ToString(value["prompt"]) ?? string.Empty;
                            }
                            catch
                            {
                                prompt = string.Empty;
                            }
                        }
                    }

                    configDict["ollama_url"] = ollamaUrl;
                    configDict["model"] = model;
                    configDict["prompt"] = prompt;
                    _configuration = configDict;
                }
            }
        }

        private Dictionary<string, object>? _configuration;

        /// <inheritdoc/>
        public async Task<bool> TestAsync()
        {
            string baseUrl = Configuration?["ollama_url"] as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return false;
            }

            try
            {
                using var http = CreateHttpClient(baseUrl);
                var resp = await http.GetAsync("/api/version");
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"OllamaCapability: version check failed with status {(int)resp.StatusCode}.");
                    return false;
                }

                var content = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"OllamaCapability: Connected to Ollama. Version response: {content}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OllamaCapability: Connectivity test error: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>?> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var result = new Dictionary<string, object>();

            string baseUrl = Configuration?["ollama_url"] as string ?? string.Empty;
            string model = Configuration?["model"] as string ?? string.Empty;
            string prompt = Configuration?["prompt"] as string ?? string.Empty;

            // allow overriding via parameters
            if (parameters != null)
            {
                try
                {
                    if (parameters.ContainsKey("ollama_url"))
                    {
                        baseUrl = Convert.ToString(parameters["ollama_url"]) ?? baseUrl;
                    }
                    if (parameters.ContainsKey("model"))
                    {
                        model = Convert.ToString(parameters["model"]) ?? model;
                    }
                    if (parameters.ContainsKey("prompt"))
                    {
                        prompt = Convert.ToString(parameters["prompt"]) ?? prompt;
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                result["result"] = false;
                result["error"] = "Missing Ollama URL (ollama_url).";
                return result;
            }
            if (string.IsNullOrWhiteSpace(model))
            {
                result["result"] = false;
                result["error"] = "Missing model name (model).";
                return result;
            }
            if (string.IsNullOrWhiteSpace(prompt))
            {
                result["result"] = false;
                result["error"] = "Missing prompt text (prompt).";
                return result;
            }

            try
            {
                using var http = CreateHttpClient(baseUrl);

                // 1) Connectivity sanity check
                var versionResp = await http.GetAsync("/api/version");
                if (!versionResp.IsSuccessStatusCode)
                {
                    result["result"] = false;
                    result["error"] = $"Failed to connect to Ollama: {(int)versionResp.StatusCode}";
                    return result;
                }

                // 2) Pull model (stream: false for simpler handling)
                var pullBody = new
                {
                    name = model,
                    stream = false
                };
                var pullJson = JsonSerializer.Serialize(pullBody);
                var pullResp = await http.PostAsync("/api/pull", new StringContent(pullJson, Encoding.UTF8, "application/json"));
                if (!pullResp.IsSuccessStatusCode)
                {
                    var pullErr = await pullResp.Content.ReadAsStringAsync();
                    result["result"] = false;
                    result["error"] = $"Model pull failed: {(int)pullResp.StatusCode} {pullErr}";
                    return result;
                }

                // 3) Generate response
                var genBody = new
                {
                    model = model,
                    prompt = prompt,
                    stream = false
                };
                var genJson = JsonSerializer.Serialize(genBody);
                var genResp = await http.PostAsync("/api/generate", new StringContent(genJson, Encoding.UTF8, "application/json"));
                if (!genResp.IsSuccessStatusCode)
                {
                    var genErr = await genResp.Content.ReadAsStringAsync();
                    result["result"] = false;
                    result["error"] = $"Generate failed: {(int)genResp.StatusCode} {genErr}";
                    return result;
                }

                var genContent = await genResp.Content.ReadAsStringAsync();

                // Expected shape when stream=false: { "response": "...", ... }
                string? responseText = null;
                try
                {
                    using var doc = JsonDocument.Parse(genContent);
                    if (doc.RootElement.TryGetProperty("response", out var respProp) && respProp.ValueKind == JsonValueKind.String)
                    {
                        responseText = respProp.GetString();
                    }
                }
                catch
                {
                    // fall back to raw content
                    responseText = genContent;
                }

                result["result"] = true;
                result["response"] = responseText ?? string.Empty;
                return result;
            }
            catch (Exception ex)
            {
                result["result"] = false;
                result["error"] = ex.Message;
                return result;
            }
        }

        private static HttpClient CreateHttpClient(string baseUrl)
        {
            var http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
            return http;
        }
    }
}

