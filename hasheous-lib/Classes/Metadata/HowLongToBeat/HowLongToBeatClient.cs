using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace hasheous_server.Classes.Metadata.HowLongToBeat
{
    /// <summary>
    /// A single game entry returned by the HowLongToBeat search endpoint.
    /// Completion times are in seconds.
    /// </summary>
    public class HowLongToBeatGame
    {
        [JsonProperty("game_id")]
        public long GameId { get; set; }

        [JsonProperty("game_name")]
        public string GameName { get; set; } = "";

        [JsonProperty("game_alias")]
        public string? GameAlias { get; set; }

        [JsonProperty("game_type")]
        public string? GameType { get; set; }

        [JsonProperty("profile_platform")]
        public string? ProfilePlatform { get; set; }

        [JsonProperty("release_world")]
        public int? ReleaseWorld { get; set; }

        [JsonProperty("count_comp")]
        public long CountComp { get; set; }

        [JsonProperty("comp_main")]
        public long CompMain { get; set; }

        [JsonProperty("comp_plus")]
        public long CompPlus { get; set; }

        [JsonProperty("comp_100")]
        public long Comp100 { get; set; }

        [JsonProperty("comp_all")]
        public long CompAll { get; set; }
    }

    /// <summary>
    /// Minimal client for the (unofficial) HowLongToBeat search API.
    /// The site's search endpoint path is discovered from its JavaScript bundle and requires a
    /// short-lived token fetched from "{endpoint}/init". The token is bound to the caller's IP and
    /// user agent, so a single long-lived HttpClient is used for all requests.
    /// </summary>
    public static class HowLongToBeatClient
    {
        private const string BaseUrl = "https://howlongtobeat.com/";

        // fallback if the search endpoint can't be discovered from the site's scripts
        private const string DefaultSearchPath = "api/search/site";

        private const string UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

        // how long to reuse a discovered search path and auth token before refreshing them
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);

        // minimum delay between requests to avoid hammering the site
        private static readonly TimeSpan MinRequestInterval = TimeSpan.FromMilliseconds(1500);

        private static readonly HttpClient client = CreateClient();

        private static readonly SemaphoreSlim requestLock = new SemaphoreSlim(1, 1);

        private static DateTime lastRequestTime = DateTime.MinValue;

        private static SearchSession? session;

        // the discovered search endpoint changes rarely, so it is only rediscovered when a search is rejected
        private static string? discoveredSearchPath;

        private static readonly Regex ScriptSrcRegex = new Regex("<script[^>]+src=\"([^\"]*/_next/static/chunks/[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SearchFetchRegex = new Regex(@"fetch\s*\(\s*[""']/api/([a-zA-Z0-9_/]+)[^""']*[""']\s*,\s*{[^}]*method:\s*[""']POST[""'][^}]*}", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private class SearchSession
        {
            public string SearchPath { get; set; } = DefaultSearchPath;
            public string? AuthToken { get; set; }
            public string? HpKey { get; set; }
            public string? HpValue { get; set; }
            public DateTime Created { get; set; } = DateTime.UtcNow;
        }

        private static HttpClient CreateClient()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                // keep connections alive for the session lifetime so the token stays bound to the same client
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                AutomaticDecompression = DecompressionMethods.All
            };

            HttpClient httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            httpClient.DefaultRequestHeaders.Referrer = new Uri(BaseUrl);

            return httpClient;
        }

        /// <summary>
        /// Searches HowLongToBeat for games matching the supplied search term.
        /// </summary>
        /// <param name="searchTerm">The game name to search for.</param>
        /// <returns>The list of matching games; empty if nothing was found.</returns>
        /// <exception cref="hasheous_server.Classes.MetadataLib.MetadataRateLimitException">Thrown when HowLongToBeat responds with HTTP 429.</exception>
        public static async Task<List<HowLongToBeatGame>> SearchAsync(string searchTerm)
        {
            if (String.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<HowLongToBeatGame>();
            }

            await requestLock.WaitAsync();
            try
            {
                // try once with the cached session, then once more with a freshly discovered session
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    bool forceRefresh = attempt > 0;
                    SearchSession activeSession = await GetSessionAsync(forceRefresh);

                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + activeSession.SearchPath);
                    request.Headers.Add("Origin", BaseUrl.TrimEnd('/'));
                    if (!String.IsNullOrEmpty(activeSession.AuthToken))
                    {
                        request.Headers.Add("x-auth-token", activeSession.AuthToken);
                    }
                    if (!String.IsNullOrEmpty(activeSession.HpKey))
                    {
                        request.Headers.Add("x-hp-key", activeSession.HpKey);
                        request.Headers.Add("x-hp-val", activeSession.HpValue ?? "");
                    }
                    request.Content = new StringContent(BuildSearchPayload(searchTerm, activeSession), Encoding.UTF8, "application/json");

                    using HttpResponseMessage response = await SendThrottledAsync(request);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        DateTime retryAfter = DateTime.UtcNow.AddHours(1);
                        if (response.Headers.RetryAfter?.Delta != null)
                        {
                            retryAfter = DateTime.UtcNow.Add(response.Headers.RetryAfter.Delta.Value);
                        }
                        else if (response.Headers.RetryAfter?.Date != null)
                        {
                            retryAfter = response.Headers.RetryAfter.Date.Value.UtcDateTime;
                        }
                        throw new hasheous_server.Classes.MetadataLib.MetadataRateLimitException("HowLongToBeat rate limit reached.", retryAfter);
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
                    {
                        // token expired or search endpoint moved - rediscover and retry
                        Logging.Log(Logging.LogType.Debug, "HowLongToBeat", $"Search returned {(int)response.StatusCode}; refreshing search session.");
                        session = null;
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    string body = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(body);
                    return json["data"]?.ToObject<List<HowLongToBeatGame>>() ?? new List<HowLongToBeatGame>();
                }

                throw new HttpRequestException("HowLongToBeat search was rejected after refreshing the search session.");
            }
            finally
            {
                requestLock.Release();
            }
        }

        private static string BuildSearchPayload(string searchTerm, SearchSession activeSession)
        {
            JObject payload = new JObject
            {
                ["searchType"] = "games",
                ["searchTerms"] = new JArray(searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                ["searchPage"] = 1,
                ["size"] = 20,
                ["searchOptions"] = new JObject
                {
                    ["games"] = new JObject
                    {
                        ["userId"] = 0,
                        ["platform"] = "",
                        ["sortCategory"] = "popular",
                        ["rangeCategory"] = "main",
                        ["rangeTime"] = new JObject { ["min"] = 0, ["max"] = 0 },
                        ["gameplay"] = new JObject
                        {
                            ["perspective"] = "",
                            ["flow"] = "",
                            ["genre"] = "",
                            ["difficulty"] = ""
                        },
                        ["rangeYear"] = new JObject { ["min"] = "", ["max"] = "" },
                        ["modifier"] = ""
                    },
                    ["users"] = new JObject { ["sortCategory"] = "postcount" },
                    ["lists"] = new JObject { ["sortCategory"] = "follows" },
                    ["filter"] = "",
                    ["sort"] = 0,
                    ["randomizer"] = 0
                },
                ["useCache"] = true
            };

            if (!String.IsNullOrEmpty(activeSession.HpKey))
            {
                payload[activeSession.HpKey] = activeSession.HpValue;
            }

            return payload.ToString(Formatting.None);
        }

        private static async Task<SearchSession> GetSessionAsync(bool forceRefresh)
        {
            if (!forceRefresh && session != null && DateTime.UtcNow - session.Created < SessionLifetime)
            {
                return session;
            }

            if (forceRefresh || discoveredSearchPath == null)
            {
                discoveredSearchPath = await DiscoverSearchPathAsync() ?? DefaultSearchPath;
            }

            SearchSession newSession = new SearchSession
            {
                SearchPath = discoveredSearchPath
            };

            // fetch the auth token for the search endpoint
            string initUrl = $"{BaseUrl}{newSession.SearchPath}/init?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, initUrl))
            using (HttpResponseMessage response = await SendThrottledAsync(request))
            {
                if (response.IsSuccessStatusCode)
                {
                    JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    newSession.AuthToken = json["token"]?.ToString();

                    // some versions of the site also require an extra key/value pair in the headers and payload
                    foreach (JProperty property in json.Properties())
                    {
                        string name = property.Name.ToLowerInvariant();
                        if (name.Contains("key"))
                        {
                            newSession.HpKey = property.Value.ToString();
                        }
                        else if (name.Contains("val"))
                        {
                            newSession.HpValue = property.Value.ToString();
                        }
                    }
                }
                else
                {
                    Logging.Log(Logging.LogType.Warning, "HowLongToBeat", $"Unable to fetch search auth token ({(int)response.StatusCode}); continuing without one.");
                }
            }

            session = newSession;
            return newSession;
        }

        private static async Task<string?> DiscoverSearchPathAsync()
        {
            try
            {
                string homePage;
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, BaseUrl))
                using (HttpResponseMessage response = await SendThrottledAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    homePage = await response.Content.ReadAsStringAsync();
                }

                foreach (Match scriptMatch in ScriptSrcRegex.Matches(homePage))
                {
                    string scriptUrl = new Uri(new Uri(BaseUrl), scriptMatch.Groups[1].Value).ToString();
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, scriptUrl);
                    using HttpResponseMessage response = await SendThrottledAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    string script = await response.Content.ReadAsStringAsync();
                    Match fetchMatch = SearchFetchRegex.Match(script);
                    if (fetchMatch.Success)
                    {
                        return "api/" + fetchMatch.Groups[1].Value.TrimEnd('/');
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "HowLongToBeat", "Unable to discover search endpoint; using default.", ex);
            }

            return null;
        }

        private static async Task<HttpResponseMessage> SendThrottledAsync(HttpRequestMessage request)
        {
            TimeSpan sinceLast = DateTime.UtcNow - lastRequestTime;
            if (sinceLast < MinRequestInterval)
            {
                await Task.Delay(MinRequestInterval - sinceLast);
            }

            try
            {
                return await client.SendAsync(request);
            }
            finally
            {
                lastRequestTime = DateTime.UtcNow;
            }
        }
    }
}
