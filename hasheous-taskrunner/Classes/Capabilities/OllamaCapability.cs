using System.Linq;
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

                    string ollamaUrl = "";
                    string model = string.Empty;
                    string prompt = string.Empty;

                    if (value != null)
                    {
                        if (value.ContainsKey("ollama_url"))
                        {
                            try
                            {
                                ollamaUrl = Convert.ToString(value["ollama_url"]) ?? "";
                                // ensure ollamaUrl is a valid URL
                                var uri = new Uri(ollamaUrl);
                                ollamaUrl = uri.ToString();
                            }
                            catch
                            {
                                ollamaUrl = "";
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

                // 3) RAG flow if embeddings provided; else direct generate
                string responseText;

                List<string> embeddingTexts = new List<string>();
                if (parameters != null && parameters.ContainsKey("embeddings"))
                {
                    try
                    {
                        if (parameters["embeddings"] is IEnumerable<object> objList)
                        {
                            foreach (var o in objList)
                            {
                                var s = Convert.ToString(o);
                                if (!string.IsNullOrEmpty(s)) embeddingTexts.Add(s);
                            }
                        }
                        else if (parameters["embeddings"] is IEnumerable<string> strList)
                        {
                            embeddingTexts.AddRange(strList.Where(s => !string.IsNullOrEmpty(s)));
                        }
                        else if (parameters["embeddings"] is string singleStr && !string.IsNullOrWhiteSpace(singleStr))
                        {
                            embeddingTexts.Add(singleStr);
                        }
                    }
                    catch { }
                }

                if (embeddingTexts.Count > 0)
                {
                    // Use a dedicated embedding model for better retrieval
                    string embedModel = "nomic-embed-text";

                    // Ensure embedding model is available
                    var pullEmbedBody = new { name = embedModel, stream = false };
                    var pullEmbedJson = JsonSerializer.Serialize(pullEmbedBody);
                    var pullEmbedResp = await http.PostAsync("/api/pull", new StringContent(pullEmbedJson, Encoding.UTF8, "application/json"));
                    if (!pullEmbedResp.IsSuccessStatusCode)
                    {
                        // If pull fails, continue and hope model already exists
                        Console.WriteLine($"OllamaCapability: Embed model pull failed: {(int)pullEmbedResp.StatusCode}");
                    }

                    // Compute embeddings
                    var docEmbeddings = new List<double[]>();
                    foreach (var text in embeddingTexts)
                    {
                        var emb = await GetEmbeddingAsync(http, embedModel, text);
                        if (emb != null) docEmbeddings.Add(emb);
                        else docEmbeddings.Add(Array.Empty<double>());
                    }

                    var queryEmbedding = await GetEmbeddingAsync(http, embedModel, prompt) ?? Array.Empty<double>();

                    // Rank by cosine similarity
                    var ranked = new List<(int idx, double score)>();
                    for (int i = 0; i < docEmbeddings.Count; i++)
                    {
                        double score = CosineSimilarity(queryEmbedding, docEmbeddings[i]);
                        ranked.Add((i, score));
                    }
                    var topK = ranked
                        .OrderByDescending(r => r.score)
                        .Take(5)
                        .Select(r => embeddingTexts[r.idx])
                        .ToList();

                    string ragPrompt = BuildRagPrompt(prompt, topK);

                    var genBody = new
                    {
                        model = model,
                        prompt = ragPrompt,
                        stream = false,
                        options = new
                        {
                            num_predict = 256,
                            temperature = 0.2
                        }
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
                    responseText = string.Empty;
                    try
                    {
                        using var doc = JsonDocument.Parse(genContent);
                        if (doc.RootElement.TryGetProperty("response", out var respProp))
                        {
                            responseText = respProp.GetString() ?? string.Empty;
                        }
                    }
                    catch
                    {
                        responseText = genContent;
                    }
                }
                else
                {
                    // Direct generation without chunking
                    var genBody = new
                    {
                        model = model,
                        prompt = prompt,
                        stream = false,
                        options = new
                        {
                            num_predict = 256,
                            temperature = 0.2
                        }
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
                    responseText = string.Empty;
                    try
                    {
                        using var doc = JsonDocument.Parse(genContent);
                        if (doc.RootElement.TryGetProperty("response", out var respProp))
                        {
                            responseText = respProp.GetString() ?? string.Empty;
                        }
                    }
                    catch
                    {
                        responseText = genContent;
                    }
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

        private async Task<double[]?> GetEmbeddingAsync(HttpClient http, string embedModel, string text)
        {
            var body = new { model = embedModel, prompt = text };
            var json = JsonSerializer.Serialize(body);
            var resp = await http.PostAsync("/api/embeddings", new StringContent(json, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }
            var content = await resp.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("embedding", out var embProp))
                {
                    var list = new List<double>();
                    foreach (var v in embProp.EnumerateArray())
                    {
                        list.Add(v.GetDouble());
                    }
                    return list.ToArray();
                }
            }
            catch { }
            return null;
        }

        private static double CosineSimilarity(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0.0;
            double dot = 0.0, na = 0.0, nb = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            double denom = Math.Sqrt(na) * Math.Sqrt(nb);
            if (denom == 0.0) return 0.0;
            return dot / denom;
        }

        private static string BuildRagPrompt(string taskPrompt, List<string> contexts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a precise summarizer. Use ONLY the provided context.");
            sb.AppendLine("If the context is insufficient, say 'Insufficient context'.");
            sb.AppendLine("Produce a concise summary, no chit-chat, under 8 sentences.");
            sb.AppendLine();
            sb.AppendLine("Context:");
            for (int i = 0; i < contexts.Count; i++)
            {
                sb.AppendLine($"--- Passage {i + 1} ---");
                sb.AppendLine(contexts[i]);
            }
            sb.AppendLine();
            sb.AppendLine("Task:");
            sb.AppendLine(taskPrompt);
            return sb.ToString();
        }
    }
}

