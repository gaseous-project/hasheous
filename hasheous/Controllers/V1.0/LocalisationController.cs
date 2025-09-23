// NOTE: This file was renamed from LocalizationController.cs to LocalisationController.cs for Australian English spelling consistency.
// Implementation remains unchanged beyond prior updates (ETag support, caching, route spelling).

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Globalization;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Localisation utilities (Australian English naming)
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/localisation/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class LocalisationController : ControllerBase
    {
        private readonly string _localisationRoot;
        private static object? _cachedPayload;
        private static readonly object _cacheLock = new object();
        private static string? _cachedEtag;
        /// <summary>
        /// Initializes the localisation controller with the hosting environment (used to locate the localisation directory).
        /// </summary>
        /// <param name="env">Hosting environment providing the web root path.</param>
        public LocalisationController(IWebHostEnvironment env)
        {
            _localisationRoot = Path.Combine(env.WebRootPath, "localisation");
        }

        /// <summary>
        /// Returns grouped localisation (language + locales) metadata. ETag enabled.
        /// </summary>
        [HttpGet]
        public IActionResult GetLocales()
        {
            EnsureCache();
            if (_cachedEtag == null && _cachedPayload != null)
            {
                _cachedEtag = ComputeEtag(_cachedPayload);
            }
            var clientEtags = Request.Headers["If-None-Match"].ToString();
            if (!string.IsNullOrEmpty(clientEtags) && _cachedEtag != null)
            {
                if (clientEtags.Split(',').Select(s => s.Trim().Trim('"')).Any(t => t == _cachedEtag))
                {
                    Response.Headers["ETag"] = '"' + _cachedEtag + '"';
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }
            if (_cachedEtag != null)
            {
                Response.Headers["ETag"] = '"' + _cachedEtag + '"';
            }
            return Ok(_cachedPayload);
        }

        private static string ComputeEtag(object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var sha = System.Security.Cryptography.SHA256.Create();
                var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
                return Convert.ToHexString(hash);
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        private void EnsureCache()
        {
            if (_cachedPayload != null) return;
            lock (_cacheLock)
            {
                if (_cachedPayload != null) return;
                try
                {
                    if (!Directory.Exists(_localisationRoot))
                    {
                        _cachedPayload = new List<object>();
                        return;
                    }
                    var files = Directory.GetFiles(_localisationRoot, "*.json", SearchOption.TopDirectoryOnly);
                    var localeRegex = new Regex("^[a-z]{2,3}(-[A-Za-z0-9]+)*$", RegexOptions.Compiled);
                    var items = new List<(string languageBase, string code, string? nativeName, string type)>();
                    foreach (var file in files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        if (name.Equals("en-example", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!localeRegex.IsMatch(name)) continue;
                        string languageBase = name.Split('-')[0];
                        string type = name == languageBase ? "base" : "regional";
                        if (name.Contains("Hans") || name.Contains("Hant")) type = "script";
                        string? nativeName = null;
                        try
                        {
                            var text = System.IO.File.ReadAllText(file);
                            using var doc = JsonDocument.Parse(text);
                            if (doc.RootElement.TryGetProperty("language_native", out var langNat) && langNat.ValueKind == JsonValueKind.String)
                            {
                                nativeName = langNat.GetString();
                            }
                            if (nativeName == null)
                            {
                                // Derive and persist language_native if missing (self-healing)
                                nativeName = DeriveNativeName(name);
                                try
                                {
                                    var props = doc.RootElement.EnumerateObject().ToList();
                                    if (!props.Any(p => p.NameEquals("language_native")))
                                    {
                                        // Re-write file with language_native inserted first
                                        using var sw = new StreamWriter(file, false, System.Text.Encoding.UTF8);
                                        sw.WriteLine("{");
                                        sw.WriteLine("  \"language_native\": " + JsonSerializer.Serialize(nativeName) + (props.Count > 0 ? "," : ""));
                                        for (int i = 0; i < props.Count; i++)
                                        {
                                            var p = props[i];
                                            sw.Write("  \"" + p.Name + "\": " + p.Value.GetRawText());
                                            if (i < props.Count - 1) sw.Write(",");
                                            sw.WriteLine();
                                        }
                                        sw.WriteLine("}");
                                    }
                                }
                                catch { /* ignore write failures */ }
                            }
                        }
                        catch { }
                        items.Add((languageBase, name, nativeName, type));
                    }
                    var baseSet = new HashSet<string>(items.Where(t => t.code == t.languageBase).Select(t => t.languageBase));
                    _cachedPayload = items
                        .GroupBy(i => i.languageBase)
                        .Where(g => baseSet.Contains(g.Key)) // ensure base file exists
                        .OrderBy(g => g.Key)
                        .Select(g => {
                            var baseEntry = g.FirstOrDefault(x => x.code == x.languageBase);
                            var baseNative = baseEntry.nativeName ?? g.Key; // Fallback to key if no native name
                            return new
                            {
                                language = g.Key,
                                baseDisplay = baseNative,
                                locales = g
                                    .OrderBy(x => x.code.Length)
                                    .ThenBy(x => x.code)
                                    .Select(x => new
                                    {
                                        code = x.code,
                                        type = x.type,
                                        // Inherit base native name when locale lacks its own
                                        display = x.nativeName ?? baseNative,
                                        file = x.code + ".json"
                                    })
                                    .ToList()
                            };
                        })
                        .ToList();
                }
                catch (Exception ex)
                {
                    _cachedPayload = new { error = ex.Message };
                }
            }
        }

        private static string DeriveNativeName(string localeCode)
        {
            try
            {
                // Normalise: replace underscore if present (just in case), keep as-is for scripts
                var norm = localeCode.Replace('_','-');
                // Special handling for script-based Chinese
                if (norm.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)) return "简体中文"; // Simplified Chinese
                if (norm.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)) return "繁體中文"; // Traditional Chinese
                // Use CultureInfo if available
                try
                {
                    var culture = CultureInfo.GetCultures(CultureTypes.AllCultures)
                        .FirstOrDefault(c => c.Name.Equals(norm, StringComparison.OrdinalIgnoreCase));
                    if (culture == null && norm.Contains('-'))
                    {
                        // Try base language
                        var basePart = norm.Split('-')[0];
                        culture = CultureInfo.GetCultures(CultureTypes.AllCultures)
                            .FirstOrDefault(c => c.Name.Equals(basePart, StringComparison.OrdinalIgnoreCase));
                    }
                    if (culture != null && !string.IsNullOrWhiteSpace(culture.NativeName))
                    {
                        // Culture.NativeName may include region; keep as-is.
                        return culture.NativeName[0].ToString().ToUpper() + culture.NativeName.Substring(1);
                    }
                }
                catch { }
                // Fallback: language code uppercased first letter
                var simple = norm.Split('-')[0];
                return simple.Length > 0 ? char.ToUpper(simple[0]) + simple.Substring(1) : norm;
            }
            catch
            {
                return localeCode;
            }
        }
    }
}
