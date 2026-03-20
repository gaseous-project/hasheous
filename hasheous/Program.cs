using System.Net;
using System.Net.Mail;
using System.Reflection;
using Authentication;
using Classes;
using Classes.ProcessQueue;
using hasheous.Classes;
using Hasheous;
using hasheous_server.Classes;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using StackExchange.Redis;
using static Classes.Common;

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
// app.UseDataObjectDetailMetaInjection();
app.UseDefaultFiles();
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "plain/text"
});
app.MapControllers();
app.UseCorrelationId();
app.ConfigureCacheWarmer();
app.ConfigureHourlyMaintenance();
Logging.WriteToDiskOnly = false;
await hasheous_server.Classes.Localisation.ExtractEnglishLanguageFile();
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

    Config.database = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
    await Config.database.InitDB();
    Classes.Metadata.Utility.TableBuilder.BuildTables();
    Config.UpdateConfig();
}
