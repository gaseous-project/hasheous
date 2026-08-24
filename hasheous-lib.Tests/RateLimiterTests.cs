using System.Security.Claims;
using Classes.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;

namespace hasheous_lib.Tests;

public class RateLimiterMatchTests
{
    [Fact]
    public void MatchesWildcardRolePathAndHeaderRules()
    {
        DynamicRateLimitManager manager = new(Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-tests", Guid.NewGuid().ToString("N"), "rules.json"), TimeSpan.FromMinutes(5));
        RateLimitRequestContext context = new()
        {
            Method = "POST",
            Path = "/api/v1/Lookup/ByHash",
            Origin = "https://example.com",
            UserAgent = "GaseousClient/1.0",
            RemoteIp = "127.0.0.1",
            UserRoles = new List<string> { "Supporter" },
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Test-Header"] = "allowed-client"
            }
        };

        RateLimitMatchCriteria criteria = new()
        {
            Methods = new List<string> { "POST" },
            Paths = new List<string> { "/api/v1/Lookup/*" },
            UserAgents = new List<string> { "GaseousClient/*" },
            UserRoles = new List<string> { "Supp*" },
            Headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Test-Header"] = new List<string> { "allowed-*" }
            }
        };

        Assert.True(manager.Matches(context, criteria));
    }

    [Fact]
    public void DoesNotMatchWhenRequiredHeaderIsMissing()
    {
        DynamicRateLimitManager manager = new(Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-tests", Guid.NewGuid().ToString("N"), "rules.json"), TimeSpan.FromMinutes(5));
        RateLimitRequestContext context = new()
        {
            Method = "GET",
            Path = "/api/v1/Mcp",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        RateLimitMatchCriteria criteria = new()
        {
            Headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Required"] = new List<string> { "present" }
            }
        };

        Assert.False(manager.Matches(context, criteria));
    }
}

public class RateLimiterWebRequestTests
{
    [Fact]
    public void SignedWebCookieMarksBuiltInPageRequestAsExempt()
    {
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-dp")));
        DynamicRateLimitManager manager = new(Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-tests", Guid.NewGuid().ToString("N"), "rules.json"), TimeSpan.FromMinutes(5), dataProtectionProvider);
        DefaultHttpContext context = new();
        manager.IssueWebRequestCookie(context);

        string cookieValue = context.Response.Headers["Set-Cookie"].ToString().Split(';', 2)[0].Split('=', 2)[1];
        context.Request.Headers.Cookie = $"{DynamicRateLimitManager.WebRequestCookieName}={cookieValue}";

        Assert.True(manager.IsBuiltInWebPageRequest(context));
    }

    [Fact]
    public void MissingSignedCookieDoesNotMarkBuiltInPageRequestAsExempt()
    {
        DynamicRateLimitManager manager = new(Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-tests", Guid.NewGuid().ToString("N"), "rules.json"), TimeSpan.FromMinutes(5));
        DefaultHttpContext context = new();

        Assert.False(manager.IsBuiltInWebPageRequest(context));
    }

    [Fact]
    public async Task CookieRolesAreReflectedInRequestContext()
    {
        DefaultHttpContext context = new();
        context.Request.Headers["User-Agent"] = "Mozilla/5.0";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Moderator")
        ], "Cookies"));

        DynamicRateLimitManager manager = new(Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-tests", Guid.NewGuid().ToString("N"), "rules.json"), TimeSpan.FromMinutes(5));
        RateLimitRequestContext requestContext = await manager.BuildRequestContextAsync(context, CancellationToken.None);

        Assert.Equal("user-1", requestContext.UserId);
        Assert.Contains("Admin", requestContext.UserRoles);
        Assert.Contains("Moderator", requestContext.UserRoles);
        Assert.True(requestContext.IsAuthenticated);
    }
}

public class RateLimiterRuleFileTests
{
    [Fact]
    public void ConstructorCreatesDefaultRulesFile()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), "hasheous-rate-limiter-tests", Guid.NewGuid().ToString("N"));
        string rulesPath = Path.Combine(directoryPath, "rate-limit-rules.json");

        _ = new DynamicRateLimitManager(rulesPath, TimeSpan.FromMinutes(5));

        Assert.True(File.Exists(rulesPath));
        string contents = File.ReadAllText(rulesPath);
        Assert.Contains("Example External Lookup Clients", contents);
    }
}
