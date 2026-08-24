using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Classes.RateLimiting;

/// <summary>
/// Describes the standalone rate-limiter rules file.
/// </summary>
public class RateLimitRuleSet
{
    public List<RateLimitProfile> Profiles { get; set; } = new();
}

/// <summary>
/// Describes a single rate-limit or exemption profile.
/// </summary>
public class RateLimitProfile
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
    public bool Exempt { get; set; }
    public List<string> PartitionBy { get; set; } = new() { "remote-ip" };
    public RateLimitMatchCriteria Match { get; set; } = new();
    public FixedWindowRateLimitSettings FixedWindow { get; set; } = new();
}

/// <summary>
/// Match conditions that determine whether a profile applies to a request.
/// </summary>
public class RateLimitMatchCriteria
{
    public List<string> Methods { get; set; } = new();
    public List<string> Paths { get; set; } = new();
    public List<string> Origins { get; set; } = new();
    public List<string> UserAgents { get; set; } = new();
    public List<string> RemoteIps { get; set; } = new();
    public List<string> UserRoles { get; set; } = new();
    public Dictionary<string, List<string>> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool? AuthenticatedUser { get; set; }
    public bool? HasClientApiKey { get; set; }
    public bool? HasUserApiKey { get; set; }
    public bool? IsWebPage { get; set; }
}

/// <summary>
/// Fixed-window limiter settings for a profile.
/// </summary>
public class FixedWindowRateLimitSettings
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 0;
}

/// <summary>
/// Normalized request properties used during rule evaluation.
/// </summary>
public class RateLimitRequestContext
{
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string RemoteIp { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public List<string> UserRoles { get; init; } = new();
    public bool IsAuthenticated { get; init; }
    public bool HasClientApiKey { get; init; }
    public bool HasUserApiKey { get; init; }
    public bool IsWebPageRequest { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Result of checking a request against the current rate-limit profiles.
/// </summary>
public class RateLimitDecision
{
    public bool Allowed { get; init; }
    public bool Exempt { get; init; }
    public string? ProfileName { get; init; }
    public TimeSpan? RetryAfter { get; init; }
}

/// <summary>
/// Provides rule loading, request matching, and limiter acquisition.
/// </summary>
public class DynamicRateLimitManager : BackgroundService
{
    public const string WebRequestHeaderName = "X-Hasheous-Web-Request";
    public const string WebRequestCookieName = "Hasheous.WebSession";
    private const string WebRequestCookiePurpose = "Hasheous.RateLimiting.WebRequestCookie";
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly TimeSpan _reloadInterval;
    private readonly string _rulesFilePath;
    private Common.hashObject _hashObject = new();
    private readonly object _reloadLock = new();
    private readonly IDataProtector? _webRequestProtector;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private ConcurrentDictionary<string, Regex> _patternCache = new(StringComparer.Ordinal);
    private ConcurrentDictionary<string, FixedWindowRateLimiter> _limiters = new(StringComparer.Ordinal);
    private RateLimitRuleSet _rules = new();
    private long _rulesVersion = 1;

    public DynamicRateLimitManager()
        : this(Config.RateLimitRulesFilePath, TimeSpan.FromMinutes(5), null)
    {
    }

    public DynamicRateLimitManager(IDataProtectionProvider dataProtectionProvider)
        : this(Config.RateLimitRulesFilePath, TimeSpan.FromMinutes(5), dataProtectionProvider, null)
    {
    }

    public DynamicRateLimitManager(IDataProtectionProvider dataProtectionProvider, IServiceScopeFactory serviceScopeFactory)
        : this(Config.RateLimitRulesFilePath, TimeSpan.FromMinutes(5), dataProtectionProvider, serviceScopeFactory)
    {
    }

    public DynamicRateLimitManager(string rulesFilePath, TimeSpan reloadInterval, IDataProtectionProvider? dataProtectionProvider = null, IServiceScopeFactory? serviceScopeFactory = null)
    {
        _rulesFilePath = rulesFilePath;
        _reloadInterval = reloadInterval <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : reloadInterval;
        _webRequestProtector = dataProtectionProvider?.CreateProtector(WebRequestCookiePurpose);
        _serviceScopeFactory = serviceScopeFactory;
        EnsureRulesFileExists();
        ReloadRules();
    }

    public RateLimitRuleSet GetCurrentRules() => _rules;

    public async Task<RateLimitDecision> AcquireLeaseAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        RateLimitRequestContext requestContext = await BuildRequestContextAsync(httpContext, cancellationToken);
        if (requestContext.IsWebPageRequest)
        {
            return new RateLimitDecision
            {
                Allowed = true,
                Exempt = true,
                ProfileName = "BuiltInWebPage"
            };
        }

        RateLimitProfile? profile = FindMatchingProfile(requestContext, _rules);
        if (profile == null)
        {
            return new RateLimitDecision
            {
                Allowed = true
            };
        }

        if (profile.Exempt)
        {
            return new RateLimitDecision
            {
                Allowed = true,
                Exempt = true,
                ProfileName = profile.Name
            };
        }

        string limiterKey = BuildLimiterKey(profile, requestContext);
        return await AcquireFromLimiterAsync(limiterKey, profile, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_reloadInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }

                ReloadRules();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "RateLimiter", "Failed to reload rate-limit rules.", ex);
            }
        }
    }

    public void ReloadRules()
    {
        lock (_reloadLock)
        {
            try
            {
                EnsureRulesFileExists();
                string rawRules = File.ReadAllText(_rulesFilePath);
                Common.hashObject hashObject = new(_rulesFilePath);
                RateLimitRuleSet? parsedRules = JsonSerializer.Deserialize<RateLimitRuleSet>(rawRules, SerializerOptions);
                _rules = Sanitize(parsedRules ?? new RateLimitRuleSet());
                _patternCache = new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);

                ConcurrentDictionary<string, FixedWindowRateLimiter> oldLimiters = _limiters;
                var newLimiters = new ConcurrentDictionary<string, FixedWindowRateLimiter>(StringComparer.Ordinal);

                // check for if newLimiters is different from oldLimiters
                if (hashObject.sha1hash != _hashObject.sha1hash)
                {
                    _hashObject = hashObject;
                    _limiters = newLimiters;
                    Interlocked.Increment(ref _rulesVersion);

                    foreach (FixedWindowRateLimiter limiter in oldLimiters.Values)
                    {
                        limiter.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "RateLimiter", $"Unable to read rate-limit rules file '{_rulesFilePath}'. Keeping the previous rules.", ex);
            }
        }
    }

    public async Task<RateLimitRequestContext> BuildRequestContextAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        string origin = httpContext.Request.Headers.Origin.FirstOrDefault() ?? string.Empty;
        string userId = httpContext.User?.Claims.FirstOrDefault(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        List<string> roles = httpContext.User?.Claims
            .Where(x => x.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
        if (string.IsNullOrWhiteSpace(userId) && httpContext.Request.Headers.TryGetValue(ApiKey.ApiKeyHeaderName, out var apiKeyHeader))
        {
            using IServiceScope? scope = _serviceScopeFactory?.CreateScope();
            ApiKey? apiKeyService = scope?.ServiceProvider.GetService<ApiKey>();
            UserStore? userStore = scope?.ServiceProvider.GetService<UserStore>();

            if (apiKeyService != null && userStore != null)
            {
                ApplicationUser? apiKeyUser = await apiKeyService.GetUserFromApiKey(apiKeyHeader.FirstOrDefault() ?? string.Empty);
                if (apiKeyUser != null)
                {
                    userId = apiKeyUser.Id;
                    roles = ((await userStore.GetRolesAsync(apiKeyUser, cancellationToken)) ?? Array.Empty<string>())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
        }

        return new RateLimitRequestContext
        {
            Method = httpContext.Request.Method ?? string.Empty,
            Path = httpContext.Request.Path.Value ?? string.Empty,
            Origin = origin,
            UserAgent = httpContext.Request.Headers["User-Agent"].ToString(),
            RemoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserId = userId,
            UserRoles = roles,
            IsAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true || !string.IsNullOrWhiteSpace(userId),
            HasClientApiKey = httpContext.Request.Headers.ContainsKey(ClientApiKey.APIKeyHeaderName),
            HasUserApiKey = httpContext.Request.Headers.ContainsKey(ApiKey.ApiKeyHeaderName),
            IsWebPageRequest = IsBuiltInWebPageRequest(httpContext),
            Headers = httpContext.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase)
        };
    }

    public RateLimitProfile? FindMatchingProfile(RateLimitRequestContext requestContext, RateLimitRuleSet rules)
    {
        return rules.Profiles
            .Where(x => x.Enabled)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => Matches(requestContext, x.Match));
    }

    public bool Matches(RateLimitRequestContext requestContext, RateLimitMatchCriteria criteria)
    {
        if (!MatchesCollection(criteria.Methods, requestContext.Method))
        {
            return false;
        }

        if (!MatchesCollection(criteria.Paths, requestContext.Path))
        {
            return false;
        }

        if (!MatchesCollection(criteria.Origins, requestContext.Origin))
        {
            return false;
        }

        if (!MatchesCollection(criteria.UserAgents, requestContext.UserAgent))
        {
            return false;
        }

        if (!MatchesCollection(criteria.RemoteIps, requestContext.RemoteIp))
        {
            return false;
        }

        if (criteria.AuthenticatedUser.HasValue && criteria.AuthenticatedUser.Value != requestContext.IsAuthenticated)
        {
            return false;
        }

        if (criteria.HasClientApiKey.HasValue && criteria.HasClientApiKey.Value != requestContext.HasClientApiKey)
        {
            return false;
        }

        if (criteria.HasUserApiKey.HasValue && criteria.HasUserApiKey.Value != requestContext.HasUserApiKey)
        {
            return false;
        }

        if (criteria.IsWebPage.HasValue && criteria.IsWebPage.Value != requestContext.IsWebPageRequest)
        {
            return false;
        }

        if (criteria.UserRoles.Count > 0 && !requestContext.UserRoles.Any(role => criteria.UserRoles.Any(pattern => MatchesPattern(role, pattern))))
        {
            return false;
        }

        foreach (KeyValuePair<string, List<string>> headerMatcher in criteria.Headers)
        {
            string headerValue = requestContext.Headers.TryGetValue(headerMatcher.Key, out string? value) ? value : string.Empty;
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return false;
            }

            if (!headerMatcher.Value.Any(pattern => MatchesPattern(headerValue, pattern)))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsBuiltInWebPageRequest(HttpContext httpContext)
    {
        string protectedCookie = httpContext.Request.Cookies[WebRequestCookieName] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(protectedCookie) || _webRequestProtector == null)
        {
            return false;
        }

        try
        {
            string payload = _webRequestProtector.Unprotect(protectedCookie);
            if (DateTime.TryParse(payload, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime issuedUtc))
            {
                return issuedUtc >= DateTime.UtcNow.AddDays(-7);
            }
        }
        catch
        {
        }

        return false;
    }

    public bool MatchesPattern(string? input, string pattern)
    {
        string safeInput = input ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        Regex regex = _patternCache.GetOrAdd(pattern.Trim(), static cachedPattern =>
        {
            string regexPattern = "^" + Regex.Escape(cachedPattern).Replace("\\*", ".*") + "$";
            return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        });
        return regex.IsMatch(safeInput);
    }

    private bool MatchesCollection(List<string> patterns, string value)
    {
        if (patterns.Count == 0)
        {
            return true;
        }

        return patterns.Any(pattern => MatchesPattern(value, pattern));
    }

    public void IssueWebRequestCookie(HttpContext httpContext)
    {
        if (_webRequestProtector == null)
        {
            return;
        }

        string protectedValue = _webRequestProtector.Protect(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        httpContext.Response.Cookies.Append(WebRequestCookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private void EnsureRulesFileExists()
    {
        string? directoryPath = Path.GetDirectoryName(_rulesFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (!File.Exists(_rulesFilePath))
        {
            File.WriteAllText(_rulesFilePath, JsonSerializer.Serialize(CreateDefaultRuleSet(), SerializerOptions));
        }
    }

    private static RateLimitRuleSet CreateDefaultRuleSet()
    {
        return new RateLimitRuleSet
        {
            Profiles = new List<RateLimitProfile>
            {
                new RateLimitProfile
                {
                    Name = "Example External Lookup Clients",
                    Enabled = false,
                    Order = 100,
                    Match = new RateLimitMatchCriteria
                    {
                        Methods = new List<string> { "POST" },
                        Paths = new List<string> { "/api/v1/Lookup/*" },
                        HasClientApiKey = true,
                        IsWebPage = false
                    },
                    PartitionBy = new List<string> { "header:X-Client-API-Key", "remote-ip" },
                    FixedWindow = new FixedWindowRateLimitSettings
                    {
                        PermitLimit = 60,
                        WindowSeconds = 60,
                        QueueLimit = 0
                    }
                }
            }
        };
    }

    private static RateLimitRuleSet Sanitize(RateLimitRuleSet rules)
    {
        rules.Profiles ??= new List<RateLimitProfile>();

        foreach (RateLimitProfile profile in rules.Profiles)
        {
            profile.Match ??= new RateLimitMatchCriteria();
            profile.FixedWindow ??= new FixedWindowRateLimitSettings();
            profile.PartitionBy ??= new List<string>();

            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? $"Profile-{Guid.NewGuid():N}" : profile.Name.Trim();
            profile.PartitionBy = profile.PartitionBy.Count == 0 ? new List<string> { "remote-ip" } : profile.PartitionBy;
            profile.FixedWindow.PermitLimit = Math.Max(1, profile.FixedWindow.PermitLimit);
            profile.FixedWindow.WindowSeconds = Math.Max(1, profile.FixedWindow.WindowSeconds);
            profile.FixedWindow.QueueLimit = Math.Max(0, profile.FixedWindow.QueueLimit);
            profile.Match.Methods ??= new List<string>();
            profile.Match.Paths ??= new List<string>();
            profile.Match.Origins ??= new List<string>();
            profile.Match.UserAgents ??= new List<string>();
            profile.Match.RemoteIps ??= new List<string>();
            profile.Match.UserRoles ??= new List<string>();
            profile.Match.Headers ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        return rules;
    }

    private string BuildLimiterKey(RateLimitProfile profile, RateLimitRequestContext requestContext)
    {
        string partitionKey = string.Join("|", profile.PartitionBy.Select(token => BuildPartitionValue(token, requestContext)));
        long rulesVersion = Interlocked.Read(ref _rulesVersion);
        return $"{rulesVersion}:{profile.Name}:{partitionKey}";
    }

    private static string BuildPartitionValue(string token, RateLimitRequestContext requestContext)
    {
        string normalizedToken = token.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return "global";
        }

        string lowerToken = normalizedToken.ToLowerInvariant();
        return lowerToken switch
        {
            "remote-ip" => requestContext.RemoteIp,
            "origin" => requestContext.Origin,
            "user-agent" => requestContext.UserAgent,
            "path" => requestContext.Path,
            "method" => requestContext.Method,
            "user-id" => requestContext.UserId,
            "user-roles" => string.Join(",", requestContext.UserRoles),
            _ when lowerToken.StartsWith("header:", StringComparison.Ordinal) => requestContext.Headers.TryGetValue(normalizedToken["header:".Length..], out string? headerValue) ? headerValue : string.Empty,
            _ => lowerToken
        };
    }

    private static FixedWindowRateLimiter CreateLimiter(RateLimitProfile profile)
    {
        return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = profile.FixedWindow.PermitLimit,
            Window = TimeSpan.FromSeconds(profile.FixedWindow.WindowSeconds),
            QueueLimit = profile.FixedWindow.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    private static TimeSpan? TryGetRetryAfter(RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            return retryAfter;
        }

        return null;
    }

    private async Task<RateLimitDecision> AcquireFromLimiterAsync(string limiterKey, RateLimitProfile profile, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            FixedWindowRateLimiter limiter = _limiters.GetOrAdd(limiterKey, _ => CreateLimiter(profile));
            try
            {
                using RateLimitLease lease = await limiter.AcquireAsync(1, cancellationToken);
                if (lease.IsAcquired)
                {
                    return new RateLimitDecision
                    {
                        Allowed = true,
                        ProfileName = profile.Name
                    };
                }

                return new RateLimitDecision
                {
                    Allowed = false,
                    ProfileName = profile.Name,
                    RetryAfter = TryGetRetryAfter(lease)
                };
            }
            catch (ObjectDisposedException) when (attempt == 0)
            {
            }
        }

        return new RateLimitDecision
        {
            Allowed = false,
            ProfileName = profile.Name
        };
    }
}

/// <summary>
/// MVC resource filter that enforces the current dynamic rate-limit profiles.
/// </summary>
public class DynamicRateLimitFilter : IAsyncResourceFilter
{
    private readonly DynamicRateLimitManager _dynamicRateLimitManager;

    public DynamicRateLimitFilter(DynamicRateLimitManager dynamicRateLimitManager)
    {
        _dynamicRateLimitManager = dynamicRateLimitManager;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        RateLimitDecision decision = await _dynamicRateLimitManager.AcquireLeaseAsync(context.HttpContext, context.HttpContext.RequestAborted);
        if (decision.Allowed)
        {
            await next();
            return;
        }

        if (decision.RetryAfter.HasValue)
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.Value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        context.Result = new JsonResult(new
        {
            error = "Rate limit exceeded.",
            profile = decision.ProfileName,
            retryAfterSeconds = decision.RetryAfter.HasValue ? Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.Value.TotalSeconds)) : (int?)null
        })
        {
            StatusCode = StatusCodes.Status429TooManyRequests
        };
    }
}
