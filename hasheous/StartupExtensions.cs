using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text.Json.Serialization;
using Authentication;
using Classes;
using Classes.ProcessQueue;
using hasheous.Classes;
using hasheous_server.Classes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using static Classes.Common;

namespace Hasheous;

/// <summary>
/// Provides extension methods to modularize Hasheous application service registration and middleware pipeline configuration.
/// </summary>
public static class StartupExtensions
{
    // Service registrations
    /// <summary>
    /// Registers System.Text.Json and Newtonsoft.Json options (enums as strings, null suppression, indentation) and response caching.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddHasheousJsonAndNewtonsoft(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(x =>
        {
            x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            x.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            x.JsonSerializerOptions.MaxDepth = 64;
        });
        services.AddMvc().AddNewtonsoftJson(x =>
        {
            x.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            x.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            x.SerializerSettings.MaxDepth = 64;
            x.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
        });
        services.AddResponseCaching();
        return services;
    }

    /// <summary>
    /// Adds common cache profiles used by controllers (None, Default30, 5Minute, 7Days, MaxDays).
    /// </summary>
    public static IServiceCollection AddHasheousCacheProfiles(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.CacheProfiles.Add("None", new CacheProfile { Duration = 1, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" } });
            options.CacheProfiles.Add("Default30", new CacheProfile { Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" } });
            options.CacheProfiles.Add("5Minute", new CacheProfile { Duration = 300, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" } });
            options.CacheProfiles.Add("7Days", new CacheProfile { Duration = 604800, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" } });
            options.CacheProfiles.Add("MaxDays", new CacheProfile { Duration = int.MaxValue, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "*" } });
        });
        return services;
    }
    /// <summary>
    /// Configures SMTP email sender and registers <see cref="IEmailSender"/>.
    /// </summary>
    public static IServiceCollection AddHasheousEmail(this IServiceCollection services)
    {
        var smtpClient = new SmtpClient(Config.EmailSMTPConfiguration.Host)
        {
            Port = Config.EmailSMTPConfiguration.Port,
            Credentials = new NetworkCredential(Config.EmailSMTPConfiguration.UserName, Config.EmailSMTPConfiguration.Password),
            EnableSsl = Config.EmailSMTPConfiguration.EnableSSL,
        };
        services.AddSingleton(smtpClient);
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        return services;
    }

    /// <summary>
    /// Adds API versioning and versioned API explorer configuration.
    /// </summary>
    public static IServiceCollection AddHasheousApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(config =>
        {
            config.DefaultApiVersion = new ApiVersion(1, 0);
            config.AssumeDefaultVersionWhenUnspecified = true;
            config.ReportApiVersions = true;
            config.ApiVersionReader = ApiVersionReader.Combine(new UrlSegmentApiVersionReader(), new HeaderApiVersionReader("x-api-version"), new MediaTypeApiVersionReader("x-api-version"));
        });
        services.AddVersionedApiExplorer(setup =>
        {
            setup.GroupNameFormat = "'v'VVV";
            setup.SubstituteApiVersionInUrl = true;
        });
        return services;
    }

    /// <summary>
    /// Configures large request body limits and forwarded headers (X-Forwarded-*).
    /// </summary>
    public static IServiceCollection AddHasheousUploadAndForwardingLimits(this IServiceCollection services)
    {
        services.Configure<IISServerOptions>(options => { options.MaxRequestBodySize = int.MaxValue; });
        services.Configure<KestrelServerOptions>(options => { options.Limits.MaxRequestBodySize = int.MaxValue; });
        services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.RequireHeaderSymmetry = false;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
        });
        return services;
    }

    /// <summary>
    /// Registers Swagger/OpenAPI generation with security definitions and custom schema ID handling.
    /// </summary>
    public static IServiceCollection AddHasheousSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("API Key", new OpenApiSecurityScheme
            {
                Name = ApiKey.ApiKeyHeaderName,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Description = "API Key Authentication",
                Scheme = "ApiKeyScheme"
            });
            options.AddSecurityDefinition("Client API Key", new OpenApiSecurityScheme
            {
                Name = ClientApiKey.APIKeyHeaderName,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Description = "Client API Key",
                Scheme = "ClientApiKeyScheme"
            });
            options.AddSecurityDefinition("Task Worker API Key", new OpenApiSecurityScheme
            {
                Name = TaskWorkerAPIKey.APIKeyHeaderName,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Description = "Task Worker API Key",
                Scheme = "TaskWorkerApiKeyScheme"
            });
            options.OperationFilter<AuthorizationOperationFilter>();
            options.DocumentFilter<IGDBMetadataDocumentFilter>();
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1.0",
                Title = "Hasheous API",
                Description = "An API for querying game metadata",
                TermsOfService = new Uri("https://github.com/gaseous-project/hasheous"),
                Contact = new OpenApiContact
                {
                    Name = "GitHub Repository",
                    Url = new Uri("https://github.com/gaseous-project/hasheous")
                },
                License = new OpenApiLicense
                {
                    Name = "Hasheous License",
                    Url = new Uri("https://github.com/gaseous-project/hasheous/blob/main/LICENSE")
                }
            });
            options.CustomSchemaIds(type =>
            {
                if (type.IsGenericType)
                {
                    var genericTypeName = type.GetGenericTypeDefinition().Name;
                    genericTypeName = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));
                    var genericArgs = string.Join("_", type.GetGenericArguments().Select(t => t.Name));
                    return genericTypeName + "_" + genericArgs;
                }
                return type.FullName?.Replace("+", "_") ?? type.Name;
            });
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            options.OrderActionsBy((apiDesc) => $"{apiDesc.RelativePath}_{apiDesc.HttpMethod}");
        });
        return services;
    }

    /// <summary>
    /// Registers Redis connection (if enabled in configuration) and HttpClient.
    /// </summary>
    public static IServiceCollection AddHasheousRedis(this IServiceCollection services)
    {
        if (Config.RedisConfiguration.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port));
            services.AddHttpClient();
        }
        return services;
    }

    /// <summary>
    /// Configures antiforgery token header and auto validation filter.
    /// </summary>
    public static IServiceCollection AddHasheousCsrf(this IServiceCollection services)
    {
        services.AddAntiforgery(options => { options.HeaderName = "X-XSRF-TOKEN"; });
        services.AddControllers(options => { options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); });
        return services;
    }

    /// <summary>
    /// Configures ASP.NET Identity, cookies, roles, authorization policies, and optional social authentication providers.
    /// </summary>
    public static IServiceCollection AddHasheousIdentityAndAuth(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 10;
                // Use framework default AllowedUserNameCharacters
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedPhoneNumber = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddUserStore<UserStore>()
            .AddRoleStore<RoleStore>()
            .AddDefaultTokenProviders()
            .AddDefaultUI();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Hasheous.Identity";
            options.ExpireTimeSpan = TimeSpan.FromDays(90);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.None;
        });
        services.AddScoped<UserStore>();
        services.AddScoped<RoleStore>();
        services.AddTransient<IUserStore<ApplicationUser>, UserStore>();
        services.AddTransient<IRoleStore<ApplicationRole>, RoleStore>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            options.AddPolicy("Moderator", policy => policy.RequireRole("Moderator"));
            options.AddPolicy("Member", policy => policy.RequireRole("Member"));
            options.AddPolicy("VerifiedEmail", policy => policy.RequireRole("Verified Email"));
        });
        services.AddAuthentication(o => { o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; });
        services.AddAuthentication().AddCookie();
        if (Config.SocialAuthConfiguration.GoogleAuthEnabled)
        {
            services.AddAuthentication().AddGoogle(options =>
            {
                options.ClientId = Config.SocialAuthConfiguration.GoogleClientId;
                options.ClientSecret = Config.SocialAuthConfiguration.GoogleClientSecret;
            });
        }
        if (Config.SocialAuthConfiguration.MicrosoftAuthEnabled)
        {
            services.AddAuthentication().AddMicrosoftAccount(options =>
            {
                options.ClientId = Config.SocialAuthConfiguration.MicrosoftClientId;
                options.ClientSecret = Config.SocialAuthConfiguration.MicrosoftClientSecret;
                options.SaveTokens = true;
                options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
                options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
            });
        }
        return services;
    }

    /// <summary>
    /// Registers API key and client API key authorization filters and validators.
    /// </summary>
    public static IServiceCollection AddHasheousApiKeySupport(this IServiceCollection services)
    {
        services.AddSingleton<Authentication.ApiKey.ApiKeyAuthorizationFilter>();
        services.AddSingleton<Authentication.ApiKey.IApiKeyValidator, Authentication.ApiKey.ApiKeyValidator>();
        services.AddSingleton<Authentication.ClientApiKey.ClientApiKeyAuthorizationFilter>();
        services.AddSingleton<Authentication.ClientApiKey.IClientApiKeyValidator, Authentication.ClientApiKey.ClientApiKeyValidator>
        ();
        services.AddSingleton<Authentication.TaskWorkerAPIKey.TaskWorkerAPIKeyAuthorizationFilter>();
        services.AddSingleton<Authentication.TaskWorkerAPIKey.ITaskWorkerAPIKeyValidator, Authentication.TaskWorkerAPIKey.TaskWorkerAPIKeyValidator>
        ();
        return services;
    }

    // App pipeline configuration
    /// <summary>
    /// Applies development-only middleware (HTTPS redirection, developer exception page) and sets config flags.
    /// </summary>
    public static async Task ConfigureDevelopmentModeAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            Config.RequireClientAPIKey = false;
            app.UseHttpsRedirection();
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // app.UseHsts(); (intentionally omitted as in original code)
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Adds Swagger and Swagger UI with Hasheous configuration.
    /// </summary>
    public static void UseHasheousSwaggerUI(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => { options.SwaggerEndpoint($"/swagger/v1/swagger.json", "v1.0"); });
    }

    /// <summary>
    /// Ensures system roles exist and assigns Verified Email role to users with confirmed emails.
    /// </summary>
    public static async Task SeedRolesAndVerifiedEmailAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleStore>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = new[] { "Admin", "Moderator", "Member", "Verified Email" };
        foreach (var role in roles)
        {
            if (await roleManager.FindByNameAsync(role, CancellationToken.None) == null)
            {
                ApplicationRole applicationRole = new ApplicationRole { Name = role, NormalizedName = role.ToUpper() };
                await roleManager.CreateAsync(applicationRole, CancellationToken.None);
            }
        }
        const string verifiedEmailRole = "Verified Email";
        var usersWithConfirmedEmails = userManager.Users.Where(u => u.EmailConfirmed).ToList();
        foreach (var user in usersWithConfirmedEmails)
        {
            if (!await userManager.IsInRoleAsync(user, verifiedEmailRole))
            {
                await userManager.AddToRoleAsync(user, verifiedEmailRole);
            }
        }
    }

    /// <summary>
    /// Middleware that injects dynamic OpenGraph/Twitter meta tags for dataobjectdetail pages, with Redis caching support.
    /// </summary>
    public static void UseDataObjectDetailMetaInjection(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                if (context.Request.Query.TryGetValue("page", out var pageVals)
                    && string.Equals(pageVals.FirstOrDefault(), "dataobjectdetail", StringComparison.OrdinalIgnoreCase)
                    && context.Request.Query.TryGetValue("id", out var idVals)
                    && long.TryParse(idVals.FirstOrDefault(), out var id)
                    && context.Request.Query.TryGetValue("type", out var typeVals))
                {
                    var templatePath = Path.Combine(app.Environment.WebRootPath, "index.html");
                    var html = await File.ReadAllTextAsync(templatePath);
                    string cacheKey = $"PageCache:{id}";
                    if (Config.RedisConfiguration.Enabled)
                    {
                        string? cachedData = hasheous.Classes.RedisConnection.GetDatabase(0).StringGet(cacheKey);
                        if (!string.IsNullOrEmpty(cachedData))
                        {
                            html = html.Replace("<!--OG_INJECT-->", cachedData);
                            await context.Response.WriteAsync(html);
                            return;
                        }
                    }
                    DataObjects dataObjects = new DataObjects();
                    var item = await dataObjects.GetDataObject(id);
                    if (item != null)
                    {
                        var title = WebUtility.HtmlEncode($"Hasheous - {item.Name}" ?? $"Item {id}");
                        var desc = WebUtility.HtmlEncode(item.Attributes?.FirstOrDefault(a => a.attributeName == hasheous_server.Models.AttributeItem.AttributeName.Description)?.Value.ToString() ?? "");
                        var dataobjectImage = WebUtility.HtmlDecode(item.Attributes?.FirstOrDefault(a => a.attributeName == hasheous_server.Models.AttributeItem.AttributeName.Logo)?.Value.ToString() ?? "");
                        string image = "/images/logo.svg";
                        if (!string.IsNullOrEmpty(dataobjectImage))
                        {
                            if (Directory.Exists(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages))
                            {
                                var files = Directory.GetFiles(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages, $"{dataobjectImage}.*");
                                if (files.Length > 0)
                                {
                                    var file = files[0];
                                    var ext = Path.GetExtension(file).ToLower();
                                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".avif" || ext == ".gif" || ext == ".svg" || ext == ".bmp" || ext == ".tiff" || ext == ".tif" || ext == ".ico" || ext == ".jfif" || ext == ".pjpeg" || ext == ".pjp")
                                    {
                                        image = $"{context.Request.Scheme}://{context.Request.Host}/api/v1/images/{dataobjectImage}{ext}";
                                    }
                                }
                            }
                        }
                        var canonical = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}?page=dataobjectdetail&type={typeVals}&id={id}";
                        var og = $@"
<meta property=""og:type"" content=""website"">
<meta property=""og:title"" content=""{title}"">
<meta property=""og:description"" content=""{desc}"">
<meta property=""og:url"" content=""{canonical}"">
<meta property=""og:image"" content=""{image}"">
<meta name=""twitter:card"" content=""summary_large_image"">
<meta name=""twitter:title"" content=""{title}"">
<meta name=""twitter:description"" content=""{desc}"">
<meta name=""twitter:image"" content=""{image}"">
<link rel=""canonical"" href=""{canonical}"">";
                        if (Config.RedisConfiguration.Enabled)
                        {
                            hasheous.Classes.RedisConnection.GetDatabase(0).StringSet(cacheKey, og, TimeSpan.FromHours(1));
                        }
                        html = html.Replace("<!--OG_INJECT-->", og);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        await context.Response.WriteAsync(html);
                        return;
                    }
                }
                await next();
            }
            catch (Exception exc)
            {
                Logging.Log(Logging.LogType.Warning, "MetaInjectionMiddleware", "Error injecting meta tags: " + exc.ToString());
                await next();
            }
        });
    }

    /// <summary>
    /// Middleware that sets a correlation id and calling user/process context for each request.
    /// </summary>
    public static void UseCorrelationId(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            string correlationId = Guid.NewGuid().ToString();
            CallContext.SetData("CorrelationId", correlationId);
            CallContext.SetData("CallingProcess", context.Request.Method + ": " + context.Request.Path);
            string userIdentity;
            try
            {
                var nameIdentifierClaim = context.User.Claims.FirstOrDefault(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
                userIdentity = nameIdentifierClaim != null ? nameIdentifierClaim.Value : "";
            }
            catch { userIdentity = ""; }
            CallContext.SetData("CallingUser", userIdentity);
            context.Response.Headers.Append("x-correlation-id", correlationId);
            await next();
        });
    }

    /// <summary>
    /// Initializes and executes the in-process cache warmer queue item.
    /// </summary>
    public static void ConfigureCacheWarmer(this WebApplication app)
    {
        QueueProcessor.QueueItem cacheWarmerQueueItem = new QueueProcessor.QueueItem(QueueItemType.CacheWarmer, 30, true)
        {
            Task = new Classes.ProcessQueue.CacheWarmer()
        };
        cacheWarmerQueueItem.ForceExecute();
        QueueProcessor.QueueItems.Add(cacheWarmerQueueItem);
    }

    public static void ConfigureHourlyMaintenance(this WebApplication app)
    {
        QueueProcessor.QueueItem maintenanceQueueItem = new QueueProcessor.QueueItem(QueueItemType.HourlyMaintenance_Frontend, 60, true)
        {
            Task = new Classes.ProcessQueue.HourlyMaintenance_Frontend()
        };
        maintenanceQueueItem.ForceExecute();
        QueueProcessor.QueueItems.Add(maintenanceQueueItem);
    }
}