using Classes;
using hasheous_server.Classes;

namespace TOSEC
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string IndexUrl { get; } = "https://www.tosecdev.org/downloads/category/22-datfiles";

        public async Task Download()
        {
            try
            {
                // setup temp download directories
                string tempDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryTempDirectory, "TOSEC");
                if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
                Directory.CreateDirectory(tempDir);

                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_TOSEC);
                if (Directory.Exists(extractDir)) { Directory.Delete(extractDir, true); }
                Directory.CreateDirectory(extractDir);

                // index url leads to a page with links to all datfiles
                var response = await client.GetAsync(IndexUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                // Parse the content to find datfile links
                // The first div with the class "pd-subcategory" contains the link to the download page
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(content);
                var mainDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'pd-subcategory')]");
                // a single a tag inside the div
                var linkNode = mainDiv.SelectSingleNode(".//a");
                if (linkNode == null)
                {
                    throw new Exception("Could not find TOSEC datfile link on index page.");
                }
                string datfilePageUrl = linkNode.GetAttributeValue("href", "").Trim();
                if (!datfilePageUrl.StartsWith("http"))
                {
                    datfilePageUrl = "https://www.tosecdev.org" + datfilePageUrl;
                }

                // now fetch the datfile page
                response = await client.GetAsync(datfilePageUrl);
                response.EnsureSuccessStatusCode();
                content = await response.Content.ReadAsStringAsync();
                doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(content);
                // look for a div with class "pd-float"
                var downloadDiv = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'pd-float')]");
                // inside the div is a single a tag with the download link
                linkNode = downloadDiv.SelectSingleNode(".//a");
                if (linkNode == null)
                {
                    throw new Exception($"Could not find TOSEC datfile download link on datfile page: {datfilePageUrl}");
                }
                string datfileDownloadUrl = linkNode.GetAttributeValue("href", "").Trim();
                if (!datfileDownloadUrl.StartsWith("http"))
                {
                    datfileDownloadUrl = "https://www.tosecdev.org" + datfileDownloadUrl;
                }

                // download the datfile zip
                Logging.Log(Logging.LogType.Information, "TOSEC", $"Downloading TOSEC datfile from {datfileDownloadUrl}");
                string tempZipPath = System.IO.Path.Combine(tempDir, "tosec_datfiles.zip");
                await DownloadTools.DownloadFile(new Uri(datfileDownloadUrl), tempZipPath);
                // secure extraction (Zip Slip protected)
                Classes.PathSecurity.ExtractZipSafely(tempZipPath, extractDir, renameOnCollision: true, onSkippedEntry: (e) =>
                {
                    Logging.Log(Logging.LogType.Warning, "TOSEC", $"Skipped potentially unsafe entry: {e}");
                });
                // delete the zip
                File.Delete(tempZipPath);

                // cleanup signature processed directory
                string tosecProcessedDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, "TOSEC");
                if (Directory.Exists(tosecProcessedDir)) { Directory.Delete(tosecProcessedDir, true); }

                // move extracted files to processing directory
                string tosecProcessingDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "TOSEC");
                if (Directory.Exists(tosecProcessingDir)) { Directory.Delete(tosecProcessingDir, true); }
                Directory.CreateDirectory(tosecProcessingDir);
                foreach (var file in Directory.GetFiles(Path.Combine(extractDir, "TOSEC"), "*.dat", SearchOption.TopDirectoryOnly))
                {
                    var destFile = Path.Combine(tosecProcessingDir, Path.GetFileName(file));
                    File.Move(file, destFile);
                }

                // cleanup temp directory
                if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }

                Logging.Log(Logging.LogType.Information, "TOSEC", "TOSEC metadata download and extraction completed.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "TOSEC", $"Error during TOSEC metadata download: {ex.Message}");
            }
        }
    }
}