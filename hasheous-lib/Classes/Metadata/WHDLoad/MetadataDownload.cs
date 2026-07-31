using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using Classes;
using DATImport;
using hasheous_server.Classes;

namespace WHDLoad
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public gaseous_signature_parser.parser.SignatureParser SourceType => gaseous_signature_parser.parser.SignatureParser.WHDLoad;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string GitUrl { get; } = "https://github.com/BlitterStudio/amiberry.git";

        private static string GitBranch { get; } = "master";

        private static string SourceName { get; } = "WHDLoad";

        public async Task StageFiles()
        {
            try
            {
                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_WHDLoad);

                // clone the repository
                try
                {
                    bool cloneSuccess = await DownloadTools.CloneOrRefreshRepoAsync(GitUrl, GitBranch, extractDir);
                    if (!cloneSuccess)
                    {
                        Logging.Log(Logging.LogType.Warning, SourceName, $"{SourceName} repository is already up to date; no changes detected.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh {SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

                // copy the signature files to the processing directory
                string datFile = Path.Combine(extractDir, "whdboot", "game-data", "whdload_db.json");
                if (File.Exists(datFile))
                {
                    string destDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "WHDLoad");
                    if (Directory.Exists(destDir)) { Directory.Delete(destDir, true); }
                    Directory.CreateDirectory(destDir);
                    string destFile = Path.Combine(destDir, "whdload_db.json");
                    File.Copy(datFile, destFile);

                    Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata file copied to processing directory: {destFile}");
                }
                else
                {
                    throw new Exception($"{SourceName} metadata file not found in cloned repository: {datFile}");
                }

                Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata processing completed successfully.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, SourceName, $"Error downloading {SourceName} metadata: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task ProcessFiles()
        {
            // convert the json to XML for importing
            string jsonSource = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "WHDLoad", "whdload_db.json");
            using FileStream fs = File.OpenRead(jsonSource);
            using JsonDocument doc = await JsonDocument.ParseAsync(fs);

            JsonElement root = doc.RootElement;

            // create the XML document
            string xmlOutput = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "WHDLoad", "whdload_db.dat");
            using FileStream xmlFs = File.Create(xmlOutput);
            XmlWriterSettings settings = new XmlWriterSettings { Indent = true, NewLineOnAttributes = false, Encoding = Encoding.UTF8 };

            using (XmlWriter writer = XmlWriter.Create(xmlFs, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("whdbooter");
                writer.WriteAttributeString("timestamp", root.TryGetProperty("upstream_timestamp", out JsonElement timestamp) ? timestamp.GetString() : DateTime.UtcNow.ToString("o"));

                var gamesElement = root.TryGetProperty("games", out JsonElement games) ? games : default;

                var convertElement = (JsonElement el, string elementName) =>
                {
                    if (el.TryGetProperty(elementName, out JsonElement element))
                    {
                        switch (element.ValueKind)
                        {
                            case JsonValueKind.String:
                                writer.WriteElementString(elementName, element.GetString());
                                break;
                            case JsonValueKind.Number:
                                writer.WriteElementString(elementName, element.GetRawText());
                                break;
                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                writer.WriteElementString(elementName, element.GetBoolean().ToString().ToLower());
                                break;
                            default:
                                // Handle other types if necessary
                                break;
                        }
                    }
                };

                foreach (JsonElement game in gamesElement.EnumerateArray())
                {
                    // start game element
                    writer.WriteStartElement("game");
                    writer.WriteAttributeString("filename", game.GetProperty("filename").GetString());
                    writer.WriteAttributeString("sha1", game.GetProperty("sha1").GetString());

                    convertElement(game, "name");
                    convertElement(game, "subpath");
                    convertElement(game, "variant_uuid");
                    convertElement(game, "slave_count");
                    convertElement(game, "slave_default");
                    convertElement(game, "slave_libraries");

                    var slavesElement = game.TryGetProperty("slaves", out JsonElement slaves) ? slaves : default;
                    int slaveCounter = 0;
                    foreach (JsonElement slave in slavesElement.EnumerateArray())
                    {
                        slaveCounter += 1;
                        writer.WriteStartElement("slave");
                        writer.WriteAttributeString("number", slaveCounter.ToString());
                        convertElement(slave, "filename");
                        convertElement(slave, "datapath");
                        convertElement(slave, "custom");
                        writer.WriteEndElement();
                    }

                    var hardwareElement = game.TryGetProperty("hardware", out JsonElement hardware) ? hardware : default;
                    writer.WriteStartElement("hardware");
                    // hardware works differently, it is a single string elemenet with each json property on a new line. The key name is in all caps, and the value is separated by an equals sign. For example:
                    // CPU=68020
                    string hardwareString = "";
                    foreach (JsonProperty hwProp in hardware.EnumerateObject())
                    {
                        string hardwareValue = "";
                        switch (hwProp.Value.ValueKind)
                        {
                            case JsonValueKind.String:
                                hardwareValue = hwProp.Value.GetString();
                                break;
                            case JsonValueKind.Number:
                                hardwareValue = hwProp.Value.GetRawText();
                                break;
                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                hardwareValue = hwProp.Value.GetBoolean().ToString().ToLower();
                                break;
                            default:
                                // Handle other types if necessary
                                break;
                        }

                        hardwareString += $"{hwProp.Name.ToUpper()}={hardwareValue}\n";
                    }
                    writer.WriteString($"\n{hardwareString}");
                    writer.WriteEndElement(); // hardware

                    writer.WriteEndElement(); // Game
                }

                writer.WriteEndElement(); // whdbooter
                writer.WriteEndDocument();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFiles()
        {
            // Implement validation logic if needed
            return true; // No validation needed for WHDLoad metadata
        }
    }
}