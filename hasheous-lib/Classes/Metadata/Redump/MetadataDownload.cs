using System.IO.Compression;
using Classes;
using hasheous_server.Classes;

namespace Redump
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string PlatformsUrl { get; } = "http://redump.org/downloads/";

        public async Task Download()
        {
            try
            {
                // setup temp download directories
                string tempDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryTempDirectory, "Redump");
                if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
                Directory.CreateDirectory(tempDir);

                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Redump);
                if (Directory.Exists(extractDir)) { Directory.Delete(extractDir, true); }
                Directory.CreateDirectory(extractDir);

                // platforms url leads to a page with links to all platform dumps
                var response = await client.GetAsync(PlatformsUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                // Parse the content to find platform links
                // get the table inside the div with id "main"
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(content);
                var mainDiv = doc.DocumentNode.SelectSingleNode("//div[@id='main']");
                var table = mainDiv.SelectSingleNode(".//table");
                var rows = table.SelectNodes(".//tr");
                foreach (var row in rows.Skip(1)) // Skip header row
                {
                    string platformName = "";
                    string cueSheetLink = "";
                    string platformLink = "";

                    var cols = row.SelectNodes(".//td");
                    if (cols.Count < 2) continue;

                    // the first column has the platform name
                    platformName = cols[0].InnerText.Trim();
                    // the second column has the link to the cuesheets - ok if it is empty
                    var col2linkNode = cols[1].SelectSingleNode(".//a");
                    if (col2linkNode != null)
                    {
                        cueSheetLink = col2linkNode.GetAttributeValue("href", "").Trim();
                        if (!cueSheetLink.StartsWith("http"))
                        {
                            cueSheetLink = "http://redump.org" + cueSheetLink;
                        }
                    }
                    // the third column has the link to the datfile - value is required
                    var col3linkNode = cols[2].SelectSingleNode(".//a");
                    if (col3linkNode == null)
                    {
                        Logging.Log(Logging.LogType.Warning, "Redump", $"No datfile link found for platform {platformName}, skipping.");
                        continue;
                    }
                    else
                    {
                        platformLink = col3linkNode.GetAttributeValue("href", "").Trim();
                        if (!platformLink.StartsWith("http"))
                        {
                            platformLink = "http://redump.org" + platformLink + "serial,version";
                        }
                    }

                    // Download the datfile
                    Logging.Log(Logging.LogType.Information, "Redump", $"Downloading datfile for platform {platformName} from {platformLink}");
                    string downloadPath = System.IO.Path.Combine(tempDir, $"{platformName}.zip");
                    await DownloadTools.DownloadFile(new Uri(platformLink), downloadPath);
                    // Extract the datfile
                    Logging.Log(Logging.LogType.Information, "Redump", $"Extracting datfile for platform {platformName} to {extractDir}");
                    // get the name of the first entry in the zip file
                    string datFileName = "";
                    if (File.Exists(downloadPath) == false)
                    {
                        Logging.Log(Logging.LogType.Warning, "Redump", $"Datfile zip for platform {platformName} not found at {downloadPath}, skipping extraction.");
                        continue;
                    }

                    // Read the zip entries to get the first entry name to populate cuesheet directory name
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(downloadPath))
                    {
                        // Safely get first entry name (may be absent)
                        string tempDatFileName = archive.Entries.FirstOrDefault()?.FullName ?? string.Empty;
                        string safeDatFileName = Path.GetFileName(tempDatFileName);
                        if (string.IsNullOrEmpty(safeDatFileName) || PathSecurity.IsZipSlipUnsafe(Path.Combine(extractDir), safeDatFileName))
                        {
                            Logging.Log(Logging.LogType.Warning, "Redump", $"First entry in datfile zip for platform {platformName} appears unsafe, skipping extraction.");
                            continue;
                        }
                        datFileName = Path.GetFileNameWithoutExtension(safeDatFileName);
                    }
                    // Secure extraction (Zip Slip protected)
                    Classes.PathSecurity.ExtractZipSafely(downloadPath, extractDir, renameOnCollision: true, onSkippedEntry: (e) =>
                    {
                        Logging.Log(Logging.LogType.Warning, "Redump", $"Skipped potentially unsafe dat zip entry: {e}");
                    });

                    // If cueSheetLink is not empty, download the cuesheet
                    if (!string.IsNullOrEmpty(cueSheetLink))
                    {
                        Logging.Log(Logging.LogType.Information, "Redump", $"Downloading cuesheet for platform {platformName} from {cueSheetLink}");

                        string cuePlatformName = datFileName;
                        const string marker = " - Datfile (";
                        Logging.Log(Logging.LogType.Information, "Redump", $"Initial cuePlatformName: {cuePlatformName}");
                        int markerIndex = cuePlatformName.IndexOf(marker, StringComparison.Ordinal);
                        if (markerIndex >= 0)
                        {
                            cuePlatformName = cuePlatformName[..markerIndex].TrimEnd();
                        }
                        string cueDownloadPath = System.IO.Path.Combine(tempDir, $"{cuePlatformName}_cuesheets.zip");
                        await DownloadTools.DownloadFile(new Uri(cueSheetLink), cueDownloadPath);
                        // Extract the cuesheet
                        string cueExtractDir = System.IO.Path.Combine(extractDir, "cuesheets", cuePlatformName);
                        if (!Directory.Exists(cueExtractDir)) { Directory.CreateDirectory(cueExtractDir); }
                        Logging.Log(Logging.LogType.Information, "Redump", $"Extracting cuesheet for platform {cuePlatformName} to {cueExtractDir}");
                        // loop through all zip entries and extract all files - check for presence of existing files and rename if necessary
                        if (File.Exists(cueDownloadPath) == false)
                        {
                            Logging.Log(Logging.LogType.Warning, "Redump", $"Cuesheet zip file for platform {cuePlatformName} not found at {cueDownloadPath}, skipping extraction.");
                        }
                        else
                        {
                            Classes.PathSecurity.ExtractZipSafely(cueDownloadPath, cueExtractDir, renameOnCollision: true, onSkippedEntry: (e) =>
                            {
                                Logging.Log(Logging.LogType.Warning, "Redump", $"Skipped potentially unsafe cuesheet zip entry: {e}");
                            });
                        }
                    }
                }

                // cleanup signature processed directory
                string redumpProcessedDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, "Redump");
                if (Directory.Exists(redumpProcessedDir)) { Directory.Delete(redumpProcessedDir, true); }

                // move extracted files to processing directory
                string redumpProcessingDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "Redump");
                if (Directory.Exists(redumpProcessingDir)) { Directory.Delete(redumpProcessingDir, true); }
                Directory.CreateDirectory(redumpProcessingDir);
                foreach (var file in Directory.GetFiles(extractDir, "*.dat", SearchOption.TopDirectoryOnly))
                {
                    var destFile = Path.Combine(redumpProcessingDir, Path.GetFileName(file));
                    File.Move(file, destFile);
                }

                // cleanup temp directory
                if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }

                Logging.Log(Logging.LogType.Information, "Redump", "Redump metadata download and extraction completed.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "Redump", $"Error during Redump metadata download: {ex.Message}");
            }
        }
    }
}