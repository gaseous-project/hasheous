using System.Reflection;
using System.Text.Json.Serialization;
using Classes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;
using Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.RateLimiting;
using static Classes.Common;
using System.Net.Mail;
using System.Net;
using static Authentication.ApiKey;

Logging.WriteToDiskOnly = true;
Logging.Log(Logging.LogType.Information, "Startup", "Starting Hasheous Server " + Assembly.GetExecutingAssembly().GetName().Version);

// set up db
Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionStringNoDatabase);

// check db availability
bool dbOnline = false;
do
{
    Logging.Log(Logging.LogType.Information, "Startup", "Waiting for database...");
    if (db.TestConnection() == true)
    {
        dbOnline = true;
    }
    else
    {
        Thread.Sleep(30000);
    }
} while (dbOnline == false);

db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

db.InitDB();

// load app settings
Config.InitSettings();
// write updated settings back to the config file
Config.UpdateConfig();

// set up server
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(x =>
{
    // serialize enums as strings in api responses (e.g. Role)
    x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

    // suppress nulls
    x.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

    // set max depth
    x.JsonSerializerOptions.MaxDepth = 64;
});
builder.Services.AddMvc().AddNewtonsoftJson(x =>
{
    // serialize enums as string in api responses
    x.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());

    // suppress nulls
    x.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;

    // set max depth
    x.SerializerSettings.MaxDepth = 64;

    // indent all responses
    x.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
});
builder.Services.AddResponseCaching();
builder.Services.AddControllers(options =>
{
    options.CacheProfiles.Add("Default30",
        new CacheProfile()
        {
            Duration = 30
        });
    options.CacheProfiles.Add("5Minute",
        new CacheProfile()
        {
            Duration = 300,
            Location = ResponseCacheLocation.Any
        });
    options.CacheProfiles.Add("7Days",
        new CacheProfile()
        {
            Duration = 604800,
            Location = ResponseCacheLocation.Any
        });
    options.CacheProfiles.Add("MaxDays",
    new CacheProfile()
    {
        Duration = int.MaxValue,
        Location = ResponseCacheLocation.Any
    });
});

// email configuration
var smtpClient = new SmtpClient(Config.EmailSMTPConfiguration.Host)
{
    Port = Config.EmailSMTPConfiguration.Port,
    Credentials = new NetworkCredential(Config.EmailSMTPConfiguration.UserName, Config.EmailSMTPConfiguration.Password),
    EnableSsl = Config.EmailSMTPConfiguration.EnableSSL,
};
builder.Services.AddSingleton(smtpClient);
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey: httpContext.Request.Headers.Host.ToString(), partition =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                AutoReplenishment = true,
                Window = TimeSpan.FromSeconds(10)
            });
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try later again... ", cancellationToken: token);
    };
});

// api versioning
builder.Services.AddApiVersioning(config =>
{
    config.DefaultApiVersion = new ApiVersion(1, 0);
    config.AssumeDefaultVersionWhenUnspecified = true;
    config.ReportApiVersions = true;
    config.ApiVersionReader = ApiVersionReader.Combine(new UrlSegmentApiVersionReader(),
                                                    new HeaderApiVersionReader("x-api-version"),
                                                    new MediaTypeApiVersionReader("x-api-version"));
});
builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// set max upload size
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = int.MaxValue;
});
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    {
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

        // using System.Reflection;
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
    }
);
builder.Services.AddHostedService<TimedHostedService>();

// identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 10;
            options.User.AllowedUserNameCharacters = null;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;
        })
    .AddUserStore<UserStore>()
    .AddRoleStore<RoleStore>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    ;
builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Hasheous.Identity";
            options.ExpireTimeSpan = TimeSpan.FromDays(90);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
builder.Services.AddScoped<UserStore>();
builder.Services.AddScoped<RoleStore>();

builder.Services.AddTransient<IUserStore<ApplicationUser>, UserStore>();
builder.Services.AddTransient<IRoleStore<ApplicationRole>, RoleStore>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Moderator", policy => policy.RequireRole("Moderator"));
    options.AddPolicy("Member", policy => policy.RequireRole("Member"));
});

// setup api key
builder.Services.AddSingleton<ApiKeyAuthorizationFilter>();
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint($"/swagger/v1/swagger.json", "v1.0");
    }
);
//}

//app.UseHttpsRedirection();

app.UseResponseCaching();

// set up system roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleStore>();
    var roles = new[] { "Admin", "Moderator", "Member" };

    foreach (var role in roles)
    {
        if (await roleManager.FindByNameAsync(role, CancellationToken.None) == null)
        {
            ApplicationRole applicationRole = new ApplicationRole();
            applicationRole.Name = role;
            applicationRole.NormalizedName = role.ToUpper();
            await roleManager.CreateAsync(applicationRole, CancellationToken.None);
        }
    }

    // // set up administrator account
    // var userManager = scope.ServiceProvider.GetRequiredService<UserStore>();
    // if (await userManager.FindByNameAsync("admin@localhost", CancellationToken.None) == null)
    // {
    //     ApplicationUser adminUser = new ApplicationUser{
    //         Id = Guid.NewGuid().ToString(),
    //         Email = "admin@localhost",
    //         NormalizedEmail = "ADMIN@LOCALHOST",
    //         EmailConfirmed = true,
    //         UserName = "administrator",
    //         NormalizedUserName = "ADMINISTRATOR"
    //     };

    //     //set user password
    //     PasswordHasher<ApplicationUser> ph = new PasswordHasher<ApplicationUser>();
    //     adminUser.PasswordHash = ph.HashPassword(adminUser, "letmein");

    //     await userManager.CreateAsync(adminUser, CancellationToken.None);
    //     await userManager.AddToRoleAsync(adminUser, "Admin", CancellationToken.None);
    //     await userManager.AddToRoleAsync(adminUser, "Moderator", CancellationToken.None);
    // }
}

app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true, //allow unkown file types also to be served
    DefaultContentType = "plain/text" //content type to returned if fileType is not known.
});

app.MapControllers();

app.Use(async (context, next) =>
{
    // set the correlation id
    string correlationId = Guid.NewGuid().ToString();
    CallContext.SetData("CorrelationId", correlationId);
    CallContext.SetData("CallingProcess", context.Request.Method + ": " + context.Request.Path);

    string userIdentity;
    try
    {
        userIdentity = context.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault().Value;
    }
    catch
    {
        userIdentity = "";
    }
    CallContext.SetData("CallingUser", userIdentity);

    context.Response.Headers.Add("x-correlation-id", correlationId.ToString());
    await next();
});

// add background tasks
ProcessQueue.QueueItems.Add(
    new ProcessQueue.QueueItem(
        ProcessQueue.QueueItemType.SignatureIngestor,
        60,
        new List<ProcessQueue.QueueItemType>
        {

        }
        )
    );

ProcessQueue.QueueItems.Add(
    new ProcessQueue.QueueItem(
        ProcessQueue.QueueItemType.TallyVotes,
        1440,
        new List<ProcessQueue.QueueItemType>
        {

        }
        )
    );

Logging.WriteToDiskOnly = false;

// start the app
app.Run();
