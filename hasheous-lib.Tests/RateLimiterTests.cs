using System.Security.Claims;
using Classes.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace hasheous_lib.Tests;

public class RateLimiterMatchTests
{
    [Fact]
    public void MatchesWildcardRolePathAndHeaderRules()
    {
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

        Assert.True(DynamicRateLimitManager.Matches(context, criteria));
    }

    [Fact]
    public void DoesNotMatchWhenRequiredHeaderIsMissing()
    {
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

        Assert.False(DynamicRateLimitManager.Matches(context, criteria));
    }
}

public class RateLimiterWebRequestTests
{
    [Fact]
    public void CustomWebHeaderMarksBuiltInPageRequestAsExempt()
    {
        DefaultHttpContext context = new();
        context.Request.Headers[DynamicRateLimitManager.WebRequestHeaderName] = "1";

        Assert.True(DynamicRateLimitManager.IsBuiltInWebPageRequest(context));
    }

    [Fact]
    public void SameOriginRefererMarksBuiltInPageRequestAsExempt()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("hasheous.example");
        context.Request.Headers.Referer = "https://hasheous.example/index.html?page=search";

        Assert.True(DynamicRateLimitManager.IsBuiltInWebPageRequest(context));
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

        RateLimitRequestContext requestContext = await DynamicRateLimitManager.BuildRequestContextAsync(context, CancellationToken.None);

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
