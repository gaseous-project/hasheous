using System.Reflection;
using Classes;
using Classes.ProcessQueue;
using static Classes.Common;
using hasheous_server.Classes;
using hasheous.Classes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Authentication;
using System.Net.Mail;
using System.Net;
using StackExchange.Redis;
using Hasheous;

Logging.WriteToDiskOnly = true;
Logging.Log(Logging.LogType.Information, "Startup", "Starting Hasheous Server " + Assembly.GetExecutingAssembly().GetName().Version);

// database initialization
await WaitForAndInitializeDatabaseAsync();

// set up server
var builder = WebApplication.CreateBuilder(args);

// service registrations (modularized)
builder.Services
    .AddHasheousJsonAndNewtonsoft()
    .AddHasheousCacheProfiles()
    .AddHasheousEmail()
    .AddHasheousApiVersioning()
    .AddHasheousUploadAndForwardingLimits()
    .AddHasheousSwagger()
    .AddHasheousRedis()
    .AddHasheousCsrf()
    .AddHasheousIdentityAndAuth()
    .AddHasheousApiKeySupport()
    .AddHostedService<TimedHostedService>();

var app = builder.Build();

await app.ConfigureDevelopmentModeAsync();
app.UseHasheousSwaggerUI();
app.UseResponseCaching();
app.UseForwardedHeaders();
await app.SeedRolesAndVerifiedEmailAsync();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseDataObjectDetailMetaInjection();
app.UseDefaultFiles();
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "plain/text"
});
app.MapControllers();
app.UseCorrelationId();
app.ConfigureCacheWarmer();
Logging.WriteToDiskOnly = false;
app.Run();

// local helper to keep database startup logic isolated
static async Task WaitForAndInitializeDatabaseAsync()
{
    Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionStringNoDatabase);
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
    await db.InitDB();
    Classes.Metadata.Utility.TableBuilder.BuildTables();
    Config.UpdateConfig();
}
