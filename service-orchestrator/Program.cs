using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text.Json.Serialization;
using Authentication;
using Classes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;
using hasheous_server.Controllers.v1_0;
using static Authentication.InterHostApiKey;
using static Classes.Common;

Logging.WriteToDiskOnly = true;
Logging.Log(Logging.LogType.Information, "Startup", "Starting Hasheous Orchestration Server " + Assembly.GetExecutingAssembly().GetName().Version);

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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// configure JSON output.
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

// email configuration
var smtpClient = new SmtpClient(Config.EmailSMTPConfiguration.Host)
{
    Port = Config.EmailSMTPConfiguration.Port,
    Credentials = new NetworkCredential(Config.EmailSMTPConfiguration.UserName, Config.EmailSMTPConfiguration.Password),
    EnableSsl = Config.EmailSMTPConfiguration.EnableSSL,
};
builder.Services.AddSingleton(smtpClient);
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

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

// configure swagger
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Inter Host API Key", new OpenApiSecurityScheme
        {
            Name = InterHostApiKey.ApiKeyHeaderName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Description = "Inter HostAPI Key Authentication",
            Scheme = "ApiKeyScheme"
        });

        options.OperationFilter<AuthorizationOperationFilter>();

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

        // using System.Reflection;
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

        // sort the endpoints
        options.OrderActionsBy((apiDesc) => $"{apiDesc.RelativePath}_{apiDesc.HttpMethod}");
    }
);

// set up the background task timer
builder.Services.AddHostedService<TimedHostedService>();

// set up api key authentication
builder.Services.AddSingleton<IInterHostApiKeyValidator, InterHostApiKeyValidator>();
builder.Services.AddSingleton<InterHostApiKeyAuthorizationFilter>();

// build the app
var app = builder.Build();

// enable swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint($"/swagger/v1/swagger.json", "v1.0");
    }
);

// configure middleware
app.UseRouting();
app.MapControllers();

// configure logging
app.Use(async (context, next) =>
{
    // set the correlation id
    string correlationId = Guid.NewGuid().ToString();
    CallContext.SetData("CorrelationId", correlationId);
    CallContext.SetData("CallingProcess", context.Request.Method + ": " + context.Request.Path);

    string userIdentity;
    try
    {
        var nameIdentifierClaim = context.User.Claims.Where(x => x.Type == System.Security.Claims.ClaimTypes.NameIdentifier).FirstOrDefault();
        userIdentity = nameIdentifierClaim != null ? nameIdentifierClaim.Value : "";
    }
    catch
    {
        userIdentity = "";
    }
    CallContext.SetData("CallingUser", userIdentity);

    context.Response.Headers.Append("x-correlation-id", correlationId.ToString());
    await next();
});

// configure background services
Classes.ProcessQueue.QueueProcessor.QueueItems = new List<Classes.ProcessQueue.QueueProcessor.QueueItem>
{
    // signature ingestor
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.SignatureIngestor, 60, false),

    // tally votes
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.TallyVotes, 1440, false),

    // metadata rematch
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.MetadataMatchSearch, 120, false),

    // maintenance services
    // daily
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.DailyMaintenance, 1440, false),
    // weekly
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.WeeklyMaintenance, 10080, false),

    // default metadata fetchers
    // fetch VIMM metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchVIMMMetadata, 10080, false),
    // fetch TheGamesDB metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchTheGamesDbMetadata, 10080, false),
    // fetch Redump metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchRedumpMetadata, 10080, false),
    // fetch MAMERedump metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchMAMERedumpMetadata, 10080, false),
    // fetch PureDOSDAT metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchPureDOSDATMetadata, 10080, false),
    // fetch TOSEC metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchTOSECMetadata, 10080, false),
    // fetch WHDLoad metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchWHDLoadMetadata, 10080, false),
    // fetch FBNEO metadata
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchFBNEOMetadata, 10080, false),

    // dump all data objects
    new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.MetadataMapDump, 10080, false)
};

// non-default metadata fetchers
// IGDB
if (Config.IGDB.UseDumps == true)
{
    Classes.ProcessQueue.QueueProcessor.QueueItems.Add(new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchIGDBMetadata, 10080, false));
}
// RetroAchievements
if (Config.RetroAchievements.APIKey != "")
{
    Classes.ProcessQueue.QueueProcessor.QueueItems.Add(new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchRetroAchievementsMetadata, 10080, false));
}
// GiantBomb
if (Config.GiantBomb.APIKey != "")
{
    Classes.ProcessQueue.QueueProcessor.QueueItems.Add(new Classes.ProcessQueue.QueueProcessor.QueueItem(Classes.ProcessQueue.QueueItemType.FetchGiantBombMetadata, 10080, false));
}

// start the app
Logging.WriteToDiskOnly = false;
app.Run();