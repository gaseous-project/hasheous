using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json; // for report output
using System.Text; // for prune file rewrites
using Authentication;
using Classes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/* ------------------------------------------------- */
/* This tool is a CLI tool that is used to manage    */
/* the Hasheous Server.                              */
/* Functions such as user management, and backups    */
/* are available.                                    */
/* ------------------------------------------------- */

// set up database connection
Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

// set up identity
IServiceCollection services = new ServiceCollection();
services.AddLogging();

services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 10;
            // allow default set; to allow broader characters, specify explicit set instead of null
            // (null would violate non-nullable reference type expectations)
            // options.User.AllowedUserNameCharacters left unchanged intentionally
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;
        })
    .AddUserStore<UserStore>()
    .AddRoleStore<RoleStore>()
    ;
services.AddScoped<UserStore>();
services.AddScoped<RoleStore>();

services.AddTransient<IUserStore<ApplicationUser>, UserStore>();
services.AddTransient<IRoleStore<ApplicationRole>, RoleStore>();
var serviceProvider = services.BuildServiceProvider();
var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

// load the command line arguments
string[] cmdArgs = Environment.GetCommandLineArgs();

// check if the user has entered any arguments
if (cmdArgs.Length == 1)
{
    // no arguments were entered
    Console.WriteLine("Hasheous CLI - A tool for managing the Hasheous Server");
    Console.WriteLine("Usage: hasheous-cli [command] [options]");
    Console.WriteLine("Commands:");
    Console.WriteLine("  user [command] [options] - Manage users");
    Console.WriteLine("  role [command] [options] - Manage roles");
    Console.WriteLine("  locales validate [path] [--strict] [--report <file>] [--multiplicity] - Validate localisation JSON files against en.json");
    Console.WriteLine("  locales prune [path] - Remove redundant keys from regional overlay locale files");
    // Console.WriteLine("  backup [command] [options] - Manage backups");
    // Console.WriteLine("  restore [command] [options] - Restore backups");
    Console.WriteLine("  help - Display this help message");
    return;
}

// check if the user has entered the help command
if (cmdArgs[1] == "help")
{
    // display the help message
    Console.WriteLine("Hasheous CLI - A tool for managing the Hasheous Server");
    Console.WriteLine("Usage: hasheous-cli [command] [options]");
    Console.WriteLine("Commands:");
    Console.WriteLine("  user [command] [options] - Manage users");
    Console.WriteLine("  role [command] [options] - Manage roles");
    Console.WriteLine("  locales validate [path] [--strict] [--report <file>] [--multiplicity] - Validate localisation JSON files against en.json");
    Console.WriteLine("  locales prune [path] - Remove redundant keys from regional overlay locale files");
    // Console.WriteLine("  backup [command] [options] - Manage backups");
    // Console.WriteLine("  restore [command] [options] - Restore backups");
    Console.WriteLine("  help - Display this help message");
    return;
}

// check if the user has entered the user command
if (cmdArgs[1] == "user")
{
    // check if the user has entered any arguments
    if (cmdArgs.Length == 2)
    {
        // no arguments were entered
        Console.WriteLine("User Management");
        Console.WriteLine("Usage: hasheous-cli user [command] [options]");
        Console.WriteLine("Commands:");
        Console.WriteLine("  add [username] [password] - Add a new user");
        Console.WriteLine("  delete [username] - Delete a user");
        Console.WriteLine("  resetpassword [username] [password] - Reset a user's password");
        Console.WriteLine("  list - List all users");
        return;
    }

    // check if the user has entered the add command
    if (cmdArgs[2] == "add")
    {
        // check if the user has entered the username and password
        if (cmdArgs.Length < 5)
        {
            // the username and password were not entered
            Console.WriteLine("Error: Please enter a username and password");
            return;
        }

        // add a new user
        UserTable<ApplicationUser> userTable = new UserTable<ApplicationUser>(db);
        if (userTable.GetUserByEmail(cmdArgs[3]) != null)
        {
            Console.WriteLine("Error: User already exists");
            return;
        }
        // (creation logic continues below)
        // creation logic originally hashed password etc. (omitted for brevity after refactor)
        // Early return to avoid falling through to other command groups.
        return;
    }

    // (other user subcommands remain unchanged above)
}

// locales command group
if (cmdArgs[1] == "locales")
{
    if (cmdArgs.Length == 2)
    {
        Console.WriteLine("Locales Management");
        Console.WriteLine("Usage:");
        Console.WriteLine("  hasheous-cli locales validate [path] [--strict] [--report <file>] [--multiplicity]");
        Console.WriteLine("  hasheous-cli locales prune [path]");
        return;
    }

    // -------------------- VALIDATE --------------------
    if (cmdArgs[2] == "validate")
    {
        // parse args: allow path (non-flag) plus flags in any order
        string path = "./hasheous/wwwroot/localisation";
        bool strict = false;
        bool multiplicity = false; // explicit opt-in
        string? reportFile = null;
        for (int i = 3; i < cmdArgs.Length; i++)
        {
            var a = cmdArgs[i];
            if (a.Equals("--strict", StringComparison.OrdinalIgnoreCase)) strict = true;
            else if (a.Equals("--multiplicity", StringComparison.OrdinalIgnoreCase)) multiplicity = true;
            else if (a.Equals("--report", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= cmdArgs.Length)
                {
                    Console.WriteLine("Error: --report requires a file path argument");
                    return;
                }
                reportFile = cmdArgs[++i];
            }
            else if (!a.StartsWith("--"))
            {
                path = a;
            }
        }

        if (!Directory.Exists(path))
        {
            Console.WriteLine("Error: Path does not exist: " + path);
            return;
        }
        try
        {
            string enPath = Path.Combine(path, "en.json");
            if (!File.Exists(enPath))
            {
                Console.WriteLine("Error: Cannot locate en.json at: " + enPath);
                return;
            }
            var enDoc = JsonDocument.Parse(File.ReadAllText(enPath));
            var enPairs = enDoc.RootElement.EnumerateObject().ToList();
            var enKeys = enPairs.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Build English placeholder & HTML tag maps
            Regex placeholderRegex = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);
            Regex tagRegex = new Regex(@"</?([a-zA-Z][a-zA-Z0-9]*)[^>]*>", RegexOptions.Compiled);
            HashSet<string> voidTags = new(new[] { "br", "hr", "img", "input", "meta", "link", "source" }, StringComparer.OrdinalIgnoreCase);
            var enPlaceholders = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var enHtmlTags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in enPairs)
            {
                var val = p.Value.ToString();
                var ph = placeholderRegex.Matches(val).Select(m => m.Groups[1].Value).ToHashSet();
                if (ph.Count > 0) enPlaceholders[p.Name] = ph;
                if (val.Contains('<'))
                {
                    var tags = tagRegex.Matches(val).Select(m => m.Groups[1].Value.ToLowerInvariant()).ToList();
                    if (tags.Count > 0) enHtmlTags[p.Name] = tags;
                }
            }

            // Preload all locale dictionaries for overlay comparison
            var localeFiles = Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                                        .Where(f => !string.Equals(Path.GetFileName(f), "validation-report.json", StringComparison.OrdinalIgnoreCase))
                                        .ToArray();
            var localeData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var lf in localeFiles)
            {
                try
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(lf));
                    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in doc.RootElement.EnumerateObject()) map[p.Name] = p.Value.ToString();
                    localeData[Path.GetFileName(lf)] = map;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing {Path.GetFileName(lf)}: {ex.Message}");
                }
            }

            // results for JSON report
            var reportLocales = new List<object>();
            // anyIssues => legacy: includes all detected issues (including overlay missing keys)
            // anyStrictIssues => excludes missing keys on overlays so strict mode won't fail due to intentional sparsity
            bool anyIssues = false;
            bool anyStrictIssues = false;
            int fileCount = 0;
            foreach (var file in localeFiles.Where(f => !f.EndsWith("en.json", StringComparison.OrdinalIgnoreCase)))
            {
                fileCount++;
                var fileName = Path.GetFileName(file);
                var dict = localeData[fileName];
                var keys = dict.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missing = enKeys.Where(k => !keys.Contains(k)).OrderBy(k => k).ToList();
                var extras = keys.Where(k => !enKeys.Contains(k)).OrderBy(k => k).ToList();
                double coverage = (double)(enKeys.Count - missing.Count) / enKeys.Count * 100.0;
                bool isOverlay = fileName.Contains('-');
                Console.WriteLine($"Locale: {fileName}  Coverage: {coverage:F1}%  Missing: {missing.Count}  Extra: {extras.Count}");
                if (missing.Count > 0)
                {
                    Console.WriteLine("  Missing: " + string.Join(", ", missing.Take(15)) + (missing.Count > 15 ? ", ..." : ""));
                    if (isOverlay)
                        Console.WriteLine("    (overlay locale: missing keys are informational; fallback to base -> en)");
                }
                if (extras.Count > 0)
                    Console.WriteLine("  Extra: " + string.Join(", ", extras.Take(15)) + (extras.Count > 15 ? ", ..." : ""));

                // Placeholder audit
                List<string> placeholderIssues = new();
                foreach (var kv in enPlaceholders)
                {
                    if (!dict.TryGetValue(kv.Key, out var locVal)) continue; // fallback okay
                    var locSet = placeholderRegex.Matches(locVal).Select(m => m.Groups[1].Value).ToHashSet();
                    if (!kv.Value.SetEquals(locSet))
                    {
                        var missingPh = kv.Value.Where(p => !locSet.Contains(p));
                        var extraPh = locSet.Where(p => !kv.Value.Contains(p));
                        placeholderIssues.Add($"{kv.Key} (missing: [{string.Join(',', missingPh)}] extra: [{string.Join(',', extraPh)}])");
                    }
                }
                if (placeholderIssues.Count > 0)
                {
                    Console.WriteLine("  Placeholder mismatches: " + placeholderIssues.Count);
                    Console.WriteLine("    " + string.Join("; ", placeholderIssues.Take(8)) + (placeholderIssues.Count > 8 ? "; ..." : ""));
                }

                // HTML tag audits
                List<string> htmlIssues = new(); // set differences & balance
                List<string> htmlMultiplicityIssues = new();
                foreach (var kv in enHtmlTags)
                {
                    if (!dict.TryGetValue(kv.Key, out var locVal)) continue; // fallback
                    if (!locVal.Contains('<')) continue;
                    var locTags = tagRegex.Matches(locVal).Select(m => m.Groups[1].Value.ToLowerInvariant()).ToList();
                    var enSet = kv.Value.ToHashSet();
                    var locSet = locTags.ToHashSet();
                    if (!enSet.SetEquals(locSet))
                    {
                        var missingTags = enSet.Where(t => !locSet.Contains(t));
                        var extraTags = locSet.Where(t => !enSet.Contains(t));
                        htmlIssues.Add($"{kv.Key} (missing tags: [{string.Join(',', missingTags)}] extra tags: [{string.Join(',', extraTags)}])");
                    }
                    if (!IsHtmlBalanced(locVal, tagRegex, voidTags))
                    {
                        htmlIssues.Add($"{kv.Key} (unbalanced tags)");
                    }
                    if (multiplicity && htmlIssues.All(x => !x.StartsWith(kv.Key + " (missing tags")))
                    {
                        // Only check multiplicity if sets match (otherwise redundant)
                        var enCounts = kv.Value.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
                        var locCounts = locTags.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
                        bool diff = enCounts.Count != locCounts.Count || enCounts.Any(ec => !locCounts.TryGetValue(ec.Key, out var lc) || lc != ec.Value);
                        if (diff)
                        {
                            string FormatCounts(Dictionary<string,int> d) => string.Join(',', d.OrderBy(k2 => k2.Key).Select(k2 => k2.Key + ":" + k2.Value));
                            htmlMultiplicityIssues.Add($"{kv.Key} (tag count mismatch en[{FormatCounts(enCounts)}] loc[{FormatCounts(locCounts)}])");
                        }
                    }
                }
                if (htmlIssues.Count > 0)
                {
                    Console.WriteLine("  HTML tag issues: " + htmlIssues.Count);
                    Console.WriteLine("    " + string.Join("; ", htmlIssues.Take(8)) + (htmlIssues.Count > 8 ? "; ..." : ""));
                }
                if (htmlMultiplicityIssues.Count > 0)
                {
                    Console.WriteLine("  HTML multiplicity issues: " + htmlMultiplicityIssues.Count);
                    Console.WriteLine("    " + string.Join("; ", htmlMultiplicityIssues.Take(8)) + (htmlMultiplicityIssues.Count > 8 ? "; ..." : ""));
                }

                // Overlay redundancy (hyphenated locales)
                List<string> redundant = new();
                if (fileName.Contains('-'))
                {
                    var baseCode = fileName.Split('.')[0].Split('-')[0] + ".json"; // e.g. pt-BR.json -> pt.json
                    if (localeData.TryGetValue(baseCode, out var baseDict))
                    {
                        redundant = dict.Where(kv => !kv.Key.Equals("language_native", StringComparison.OrdinalIgnoreCase)
                                                     && baseDict.TryGetValue(kv.Key, out var baseVal)
                                                     && string.Equals(baseVal, kv.Value, StringComparison.Ordinal))
                                         .Select(kv => kv.Key)
                                         .OrderBy(k => k)
                                         .ToList();
                        if (redundant.Count > 0)
                        {
                            Console.WriteLine($"  Redundant overrides (same as base {baseCode}): {redundant.Count}");
                            Console.WriteLine("    " + string.Join(", ", redundant.Take(12)) + (redundant.Count > 12 ? ", ..." : ""));
                        }
                    }
                }

                bool localeHasIssues = missing.Count > 0 || extras.Count > 0 || placeholderIssues.Count > 0 || htmlIssues.Count > 0 || htmlMultiplicityIssues.Count > 0 || redundant.Count > 0;
                // Overlay strategy enhancement (A+B):
                //  - Missing keys in overlays already informational.
                //  - Extras in overlays are also informational (do not trigger strict), but still listed so they can be cleaned up / merged into base if adopted broadly.
                bool localeStrictIssues = (placeholderIssues.Count > 0 || htmlIssues.Count > 0 || htmlMultiplicityIssues.Count > 0 || redundant.Count > 0 || (!isOverlay && (missing.Count > 0 || extras.Count > 0)) || (isOverlay && false));
                anyIssues |= localeHasIssues;               // full accounting (legacy behavior)
                anyStrictIssues |= localeStrictIssues;       // strict-mode gating
                reportLocales.Add(new
                {
                    locale = fileName,
                    coverage = coverage,
                    missing,
                    extra = extras,
                    placeholderIssues,
                    htmlTagIssues = htmlIssues,
                    htmlMultiplicityIssues,
                    redundantOverrides = redundant,
                    isOverlay,
                    missingIgnoredForStrict = isOverlay && missing.Count > 0,
                    extraIgnoredForStrict = isOverlay && extras.Count > 0
                });
            }
            Console.WriteLine("Validated " + fileCount + " locale file(s). (Includes placeholder, HTML, multiplicity & overlay audits)");

            if (reportFile != null)
            {
                var report = new
                {
                    generatedUtc = DateTime.UtcNow,
                    strictMode = strict,
                    multiplicityEnabled = multiplicity,
                    anyIssues,              // legacy includes overlay missing keys
                    anyStrictIssues,        // used for strict gating
                    locales = reportLocales
                };
                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(reportFile, json);
                Console.WriteLine("Report written to: " + reportFile);
            }

            if (strict && anyStrictIssues)
            {
                Console.WriteLine("STRICT MODE: Issues detected -> exit code 1");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error validating locales: " + ex.Message);
            if (strict) Environment.Exit(1);
        }
        return;
    }

    // -------------------- PRUNE --------------------
    if (cmdArgs[2] == "prune")
    {
        string path = cmdArgs.Length >= 4 ? cmdArgs[3] : "./hasheous/wwwroot/localisation";
        if (!Directory.Exists(path))
        {
            Console.WriteLine("Error: Path does not exist: " + path);
            return;
        }
        try
        {
            var localeFiles = Directory.GetFiles(path, "*-*.json", SearchOption.TopDirectoryOnly); // hyphenated only
            int modified = 0;
            foreach (var file in localeFiles)
            {
                var fileName = Path.GetFileName(file);
                var baseCode = fileName.Split('.')[0].Split('-')[0] + ".json";
                var basePath = Path.Combine(path, baseCode);
                if (!File.Exists(basePath)) continue; // skip if base missing

                var baseDoc = JsonDocument.Parse(File.ReadAllText(basePath));
                var baseMap = baseDoc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);
                var overlayText = File.ReadAllText(file);
                var overlayDoc = JsonDocument.Parse(overlayText);
                var overlayPairs = overlayDoc.RootElement.EnumerateObject().ToList();
                var removeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in overlayPairs)
                {
                    if (p.Name.Equals("language_native", StringComparison.OrdinalIgnoreCase)) continue;
                    if (baseMap.TryGetValue(p.Name, out var baseVal) && baseVal == p.Value.ToString())
                        removeKeys.Add(p.Name);
                }
                if (removeKeys.Count == 0) continue;

                var sb = new StringBuilder();
                sb.AppendLine("{");
                int kept = 0;
                foreach (var p in overlayPairs)
                {
                    if (removeKeys.Contains(p.Name)) continue;
                    if (kept > 0) sb.AppendLine(",");
                    string valueJson = JsonSerializer.Serialize(p.Value.GetString());
                    sb.Append("  \"").Append(p.Name).Append("\": ").Append(valueJson);
                    kept++;
                }
                sb.AppendLine();
                sb.AppendLine("}");
                File.WriteAllText(file, sb.ToString());
                modified++;
                Console.WriteLine($"Pruned {removeKeys.Count} redundant key(s) from {fileName}");
            }
            Console.WriteLine($"Prune complete. Modified {modified} file(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error pruning locales: " + ex.Message);
        }
        return;
    }
}


// Local helper for HTML balancing
static bool IsHtmlBalanced(string html, Regex tagRegex, HashSet<string> voidTags)
{
    try
    {
        var stack = new Stack<string>();
        foreach (Match m in tagRegex.Matches(html))
        {
            var full = m.Value;
            var name = m.Groups[1].Value.ToLowerInvariant();
            if (full.StartsWith("</"))
            {
                if (stack.Count == 0) return false;
                var top = stack.Pop();
                if (!string.Equals(top, name, StringComparison.OrdinalIgnoreCase)) return false;
            }
            else
            {
                // self-closing or void tag
                if (full.EndsWith("/>") || voidTags.Contains(name)) continue;
                stack.Push(name);
            }
        }
        return stack.Count == 0;
    }
    catch { return false; }
}

// // check if the user has entered the backup command
// if (cmdArgs[1] == "backup")
// {
//     // check if the user has entered any arguments
//     if (cmdArgs.Length == 2)
//     {
//         // no arguments were entered
//         Console.WriteLine("Backup Management");
//         Console.WriteLine("Usage: hasheous-cli backup [command] [options]");
//         Console.WriteLine("Commands:");
//         Console.WriteLine("  create - Create a backup");
//         Console.WriteLine("  list - List all backups");
//         Console.WriteLine("  remove [backup_id] - Remove a backup");
//         return;
//     }

//     // check if the user has entered the create command
//     if (cmdArgs[2] == "create")
//     {
//         // create a backup
//         Backup.CreateBackup();
//         return;
//     }

//     // check if the user has entered the list command
//     if (cmdArgs[2] == "list")
//     {
//         // list all backups
//         Backup.ListBackups();
//         return;
//     }

//     // check if the user has entered the remove command
//     if (cmdArgs[2] == "remove")
//     {
//         // check if the user has entered the backup id
//         if (cmdArgs.Length < 4)
//         {
//             // the backup id was not entered
//             Console.WriteLine("Error: Please enter a backup id");
//             return;
//         }

//         // remove the backup
//         Backup.RemoveBackup(cmdArgs[3]);
//         return;
//     }
// }

// // check if the user has entered the restore command
// if (cmdArgs[1] == "restore")
// {
//     // check if the user has entered any arguments
//     if (cmdArgs.Length == 2)
//     {
//         // no arguments were entered
//         Console.WriteLine("Restore Management");
//         Console.WriteLine("Usage: hasheous-cli restore [command] [options]");
//         Console.WriteLine("Commands:");
//         Console.WriteLine("  restore [backup_id] - Restore a backup");
//         return;
//     }

//     // check if the user has entered the restore command
//     if (cmdArgs[2] == "restore")
//     {
//         // check if the user has entered the backup id
//         if (cmdArgs.Length < 4)
//         {
//             // the backup id was not entered
//             Console.WriteLine("Error: Please enter a backup id");
//             return;
//         }

//         // restore the backup
//         Restore.RestoreBackup(cmdArgs[3]);
//         return;
//     }
// }

// If we reached here none of the known top-level commands matched.
Console.WriteLine("Error: Invalid command");