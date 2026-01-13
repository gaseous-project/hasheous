using System.Diagnostics;
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
                Console.WriteLine($"OllamaCapability: Pulling model '{model}'...");
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

                // Check if this is a tag generation request
                bool isTagGeneration = false;
                if (parameters != null && parameters.ContainsKey("isTagGeneration"))
                {
                    bool.TryParse(Convert.ToString(parameters["isTagGeneration"]), out isTagGeneration);
                }

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

                    // embeddings are usually markdown - break into chunks along markdown boundaries
                    const int maxChunkSize = 4000;

                    var chunkedEmbeddings = new List<string>();
                    foreach (var text in embeddingTexts)
                    {
                        // Split into markdown-aware chunks
                        var chunks = ChunkMarkdown(text, maxChunkSize);
                        foreach (var chunk in chunks)
                        {
                            chunkedEmbeddings.Add(chunk);
                        }
                    }
                    embeddingTexts = chunkedEmbeddings;
                }

                var stopWatch = new Stopwatch();
                stopWatch.Start();
                if (embeddingTexts.Count > 0)
                {
                    // Use a dedicated embedding model for better retrieval
                    string embedModel = "nomic-embed-text";

                    // Ensure embedding model is available
                    Console.WriteLine($"OllamaCapability: Pulling embedding model '{embedModel}'...");
                    var pullEmbedBody = new { name = embedModel, stream = false };
                    var pullEmbedJson = JsonSerializer.Serialize(pullEmbedBody);
                    var pullEmbedResp = await http.PostAsync("/api/pull", new StringContent(pullEmbedJson, Encoding.UTF8, "application/json"));
                    if (!pullEmbedResp.IsSuccessStatusCode)
                    {
                        // If pull fails, continue and hope model already exists
                        Console.WriteLine($"OllamaCapability: Embed model pull failed: {(int)pullEmbedResp.StatusCode}");
                    }

                    // Compute embeddings
                    Console.WriteLine("OllamaCapability: Computing embeddings for RAG...");
                    var docEmbeddings = new List<double[]>();
                    int docIndex = 1;
                    foreach (var text in embeddingTexts)
                    {
                        var emb = await GetEmbeddingAsync(http, embedModel, text);
                        if (emb != null) docEmbeddings.Add(emb);
                        else docEmbeddings.Add(Array.Empty<double>());
                        docIndex++;
                    }

                    var queryEmbedding = await GetEmbeddingAsync(http, embedModel, prompt) ?? Array.Empty<double>();

                    // Rank by cosine similarity
                    var ranked = new List<(int idx, double score)>();
                    for (int i = 0; i < docEmbeddings.Count; i++)
                    {
                        double score = CosineSimilarity(queryEmbedding, docEmbeddings[i]);
                        ranked.Add((i, score));
                    }
                    // Adjust parameters based on task type
                    int topK = isTagGeneration ? 10 : 5;
                    int numPredict = isTagGeneration ? 512 : 256;
                    double temperature = isTagGeneration ? 0.5 : 0.2;

                    var topKResults = ranked
                        .OrderByDescending(r => r.score)
                        .Take(topK)
                        .Select(r => embeddingTexts[r.idx])
                        .ToList();

                    string ragPrompt = isTagGeneration
                        ? BuildRagPromptForTags(prompt, topKResults)
                        : BuildRagPromptForSummary(prompt, topKResults);

                    Console.WriteLine("OllamaCapability: Generating response with RAG prompt...");
                    var genBody = new
                    {
                        model = model,
                        prompt = ragPrompt,
                        stream = false,
                        think = false,
                        options = new
                        {
                            num_predict = numPredict,
                            temperature = temperature
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
                    Console.WriteLine("OllamaCapability: Generating response without RAG...");
                    // Direct generation without chunking
                    var genBody = new
                    {
                        model = model,
                        prompt = prompt,
                        stream = false,
                        think = false,
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
                stopWatch.Stop();

                var elapsedTime = stopWatch.ElapsedMilliseconds;
                var elapsedUnits = "ms";
                if (elapsedTime >= 1000)
                {
                    elapsedTime /= 1000;
                    elapsedUnits = "s";
                    if (elapsedTime >= 60)
                    {
                        elapsedTime /= 60;
                        elapsedUnits = "min";
                    }
                }
                Console.WriteLine($"OllamaCapability: Generation completed in {elapsedTime} {elapsedUnits}.");

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
                Timeout = TimeSpan.FromMinutes(10)
            };
            return http;
        }

        private async Task<double[]?> GetEmbeddingAsync(HttpClient http, string embedModel, string text)
        {
            // Chunk text if too long (8000 chars ≈ 2000 tokens for most models)
            const int maxChunkSize = 8000;
            if (text.Length <= maxChunkSize)
            {
                return await GetSingleEmbeddingAsync(http, embedModel, text);
            }

            // Split into chunks and average embeddings
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += maxChunkSize)
            {
                chunks.Add(text.Substring(i, Math.Min(maxChunkSize, text.Length - i)));
            }

            var embeddings = new List<double[]>();
            foreach (var chunk in chunks)
            {
                var emb = await GetSingleEmbeddingAsync(http, embedModel, chunk);
                if (emb != null) embeddings.Add(emb);
            }

            if (embeddings.Count == 0) return null;

            // Average all chunk embeddings
            int dim = embeddings[0].Length;
            var avgEmb = new double[dim];
            foreach (var emb in embeddings)
            {
                for (int i = 0; i < dim; i++)
                {
                    avgEmb[i] += emb[i];
                }
            }
            for (int i = 0; i < dim; i++)
            {
                avgEmb[i] /= embeddings.Count;
            }

            return avgEmb;
        }

        private async Task<double[]?> GetSingleEmbeddingAsync(HttpClient http, string embedModel, string text)
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

        private static string BuildRagPromptForSummary(string taskPrompt, List<string> contexts)
        {
            var sb = new StringBuilder();
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

        private static string BuildRagPromptForTags(string taskPrompt, List<string> contexts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Reference Material:");
            for (int i = 0; i < contexts.Count; i++)
            {
                sb.AppendLine($"--- Passage {i + 1} ---");
                sb.AppendLine(contexts[i]);
            }
            sb.AppendLine();
            sb.AppendLine("Instructions:");
            sb.AppendLine(taskPrompt);
            return sb.ToString();
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

        private static List<string> ChunkMarkdown(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            var lines = text.Split('\n');
            var sections = new List<List<string>>();
            var currentSection = new List<string>();

            // First pass: split by headers
            foreach (var line in lines)
            {
                bool isHeader = line.TrimStart().StartsWith("# ") ||
                                line.TrimStart().StartsWith("## ") ||
                                line.TrimStart().StartsWith("### ") ||
                                line.TrimStart().StartsWith("#### ") ||
                                line.TrimStart().StartsWith("##### ") ||
                                line.TrimStart().StartsWith("###### ");

                if (isHeader && currentSection.Count > 0)
                {
                    sections.Add(currentSection);
                    currentSection = new List<string> { line };
                }
                else
                {
                    currentSection.Add(line);
                }
            }
            if (currentSection.Count > 0)
            {
                sections.Add(currentSection);
            }

            // Second pass: process each section
            foreach (var section in sections)
            {
                ProcessSection(section, maxChunkSize, chunks);
            }

            return chunks.Count > 0 ? chunks : new List<string> { text };
        }

        private static void ProcessSection(List<string> sectionLines, int maxChunkSize, List<string> chunks)
        {
            string sectionText = string.Join("\n", sectionLines);

            // If section fits, add as-is
            if (sectionText.Length <= maxChunkSize)
            {
                chunks.Add(sectionText.TrimEnd());
                return;
            }

            // Section is too large: extract code blocks and tables first
            var extracted = ExtractBlockElements(sectionText);
            string remaining = extracted.remaining;

            // Add extracted code blocks and tables as their own chunks
            foreach (var block in extracted.blocks)
            {
                chunks.Add(block.TrimEnd());
            }

            // Now chunk remaining content by paragraphs
            ChunkByParagraphs(remaining, maxChunkSize, chunks);
        }

        private static void ChunkByParagraphs(string text, int maxChunkSize, List<string> chunks)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var lines = text.Split('\n');
            var currentChunk = new StringBuilder();

            foreach (var line in lines)
            {
                // Paragraph boundary is a blank line
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentChunk.Length > 0)
                    {
                        // Check if adding empty line would exceed size
                        string tentative = currentChunk.ToString() + "\n";
                        if (tentative.Length <= maxChunkSize)
                        {
                            currentChunk.AppendLine();
                        }
                        else
                        {
                            // Save current chunk and start fresh
                            chunks.Add(currentChunk.ToString().TrimEnd());
                            currentChunk.Clear();
                        }
                    }
                }
                else
                {
                    string tentative = currentChunk.Length == 0 ? line : currentChunk.ToString() + "\n" + line;
                    if (tentative.Length <= maxChunkSize)
                    {
                        if (currentChunk.Length > 0)
                            currentChunk.AppendLine(line);
                        else
                            currentChunk.Append(line);
                    }
                    else
                    {
                        // Current chunk is full
                        if (currentChunk.Length > 0)
                        {
                            chunks.Add(currentChunk.ToString().TrimEnd());
                        }
                        currentChunk.Clear();

                        // If single line exceeds maxChunkSize, split it by sentence
                        if (line.Length > maxChunkSize)
                        {
                            ChunkBySentences(line, maxChunkSize, chunks);
                        }
                        else
                        {
                            currentChunk.Append(line);
                        }
                    }
                }
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().TrimEnd());
            }
        }

        private static void ChunkBySentences(string text, int maxChunkSize, List<string> chunks)
        {
            // Split by sentence boundaries: . ! ? followed by space
            var sentences = System.Text.RegularExpressions.Regex.Split(text, @"(?<=[.!?])\s+")
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            var currentChunk = new StringBuilder();
            foreach (var sentence in sentences)
            {
                string tentative = currentChunk.Length == 0 ? sentence : currentChunk.ToString() + " " + sentence;
                if (tentative.Length <= maxChunkSize)
                {
                    if (currentChunk.Length > 0)
                        currentChunk.Append(" ");
                    currentChunk.Append(sentence);
                }
                else
                {
                    if (currentChunk.Length > 0)
                        chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();

                    if (sentence.Length > maxChunkSize)
                    {
                        // Sentence itself is too long, force split
                        chunks.Add(sentence.Substring(0, maxChunkSize));
                        if (sentence.Length > maxChunkSize)
                            chunks.Add(sentence.Substring(maxChunkSize));
                    }
                    else
                    {
                        currentChunk.Append(sentence);
                    }
                }
            }
            if (currentChunk.Length > 0)
                chunks.Add(currentChunk.ToString());
        }

        private class BlockExtraction
        {
            public List<string> blocks { get; set; } = new List<string>();
            public string remaining { get; set; } = string.Empty;
        }

        private static BlockExtraction ExtractBlockElements(string text)
        {
            var result = new BlockExtraction();
            var lines = text.Split('\n');
            var remaining = new StringBuilder();
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                // Detect code block (``` or indented code)
                if (trimmed.StartsWith("```"))
                {
                    var codeBlock = new StringBuilder(line);
                    i++;
                    while (i < lines.Length)
                    {
                        codeBlock.AppendLine(lines[i]);
                        if (lines[i].TrimStart().StartsWith("```"))
                        {
                            i++;
                            break;
                        }
                        i++;
                    }
                    result.blocks.Add(codeBlock.ToString().TrimEnd());
                    continue;
                }

                // Detect table (line with | delimiters)
                if (trimmed.Contains("|") && (i + 1 < lines.Length && lines[i + 1].TrimStart().Contains("|")))
                {
                    var table = new StringBuilder(line);
                    i++;
                    // Add table header separator and rows
                    while (i < lines.Length && lines[i].TrimStart().Contains("|"))
                    {
                        table.AppendLine(lines[i]);
                        i++;
                    }
                    result.blocks.Add(table.ToString().TrimEnd());
                    continue;
                }

                // Not a block element, add to remaining
                remaining.AppendLine(line);
                i++;
            }

            result.remaining = remaining.ToString();
            return result;
        }
    }
}

