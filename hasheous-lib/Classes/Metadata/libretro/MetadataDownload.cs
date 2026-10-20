using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Classes;
using DATImport;
using gaseous_signature_parser;
using gaseous_signature_parser.classes;
using gaseous_signature_parser.classes.bracketparsers;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Models;

namespace libretro
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public DATImport.SignatureIngestor.SignatureType SourceType => DATImport.SignatureIngestor.SignatureType.libretro;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string GitUrl { get; } = "https://github.com/libretro/libretro-database.git";

        private static string GitBranch { get; } = "master";

        private static string GitDirectory { get; } = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_libretro, "libretro-database");

        private static string SourceName { get; } = "libretro";

        private static string metadataExportPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_libretro, "exported_metadata");

        /// <inheritdoc/>
        public async Task StageFiles()
        {
            try
            {
                // clone the repository
                try
                {
                    bool cloneSuccess = await DownloadTools.CloneOrRefreshRepoAsync(GitUrl, GitBranch, GitDirectory);
                    if (!cloneSuccess)
                    {
                        Logging.Log(Logging.LogType.Warning, SourceName, $"{SourceName} repository is already up to date; no changes detected.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh {SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

                // copy the dat and metadat directories to the signature ingest directory - restrict copying to the specific files needed
                string[] datsToCopy = new[]
                {
                    "Amstrad - CPC",
                    "Arduboy Inc - Arduboy",
                    "Atomiswave",
                    "CHIP-8",
                    "Commodore - Amiga",
                    "Commodore - CD32",
                    "DOS",
                    "Enterprise - 128",
                    "Handheld Electronic Game",
                    "HBMAME",
                    "NEC - PC-98",
                    "Nintendo - GameCube",
                    "Nintendo - Nintendo Entertainment System",
                    "Nintendo - Super Nintendo Entertainment System",
                    "Nintendo - Wii U",
                    "Nintendo - Wii",
                    "PICO-8",
                    "ScummVM",
                    "SNK - Neo Geo",
                    "Sega - Saturn",
                    "Sinclair - ZX 81",
                    "Sinclair - ZX Spectrum",
                    "Sony - PlayStation 3",
                    "Sony - PlayStation Minis",
                    "TIC-80",
                    "Uzebox",
                    "Videotron - TV-Computer",
                    "Vircon32"
                };

                string srcDatDirectory = Path.Combine(GitDirectory, "dat");
                string srcMetadataDirectory = Path.Combine(GitDirectory, "metadat");

                string destDatDirectory = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "libretro", "dat");
                string destMetadataDirectory = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "libretro", "metadat");

                string[] metadataDirectories = Directory.GetDirectories(srcMetadataDirectory, "*", SearchOption.TopDirectoryOnly);

                // clean destination directories before copying
                if (Directory.Exists(destDatDirectory))
                {
                    Directory.Delete(destDatDirectory, true);
                }
                if (Directory.Exists(destMetadataDirectory))
                {
                    Directory.Delete(destMetadataDirectory, true);
                }

                // create destination directories if they don't exist
                if (!Directory.Exists(destDatDirectory))
                {
                    Directory.CreateDirectory(destDatDirectory);
                }
                if (!Directory.Exists(destMetadataDirectory))
                {
                    Directory.CreateDirectory(destMetadataDirectory);
                }

                // start copying files
                foreach (string fileName in datsToCopy)
                {
                    string srcFilePath = Path.Combine(srcDatDirectory, fileName + ".dat");
                    string destFilePath = Path.Combine(destDatDirectory, fileName + ".dat");
                    if (File.Exists(srcFilePath))
                    {
                        File.Copy(srcFilePath, destFilePath);

                        foreach (string metadataDirectory in metadataDirectories)
                        {
                            string srcMetadataFilePath = Path.Combine(metadataDirectory, fileName + ".dat");
                            if (File.Exists(srcMetadataFilePath))
                            {
                                string destMetadataSubDirectory = Path.Combine(destMetadataDirectory, Path.GetFileName(metadataDirectory));
                                if (!Directory.Exists(destMetadataSubDirectory))
                                {
                                    Directory.CreateDirectory(destMetadataSubDirectory);
                                }
                                string destMetadataFilePath = Path.Combine(destMetadataSubDirectory, fileName + ".dat");
                                File.Copy(srcMetadataFilePath, destMetadataFilePath);
                            }
                        }
                    }
                }

                Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata processing completed successfully.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, SourceName, $"Error downloading {SourceName} metadata: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public Task ProcessFiles()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> ValidateFiles()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public async Task PostImport(HasheousRomSignatureObject ImportedObject)
        {
            // export the ImportedObject metadata to the appropriate location
            if (!Directory.Exists(metadataExportPath))
            {
                Directory.CreateDirectory(metadataExportPath);
            }

            string metadataExportFilePath = Path.Combine(metadataExportPath, $"{ImportedObject.Name}.json");

            if (File.Exists(metadataExportFilePath))
            {
                File.Delete(metadataExportFilePath);
            }

            await File.WriteAllTextAsync(metadataExportFilePath, Newtonsoft.Json.JsonConvert.SerializeObject(ImportedObject));
        }

        /// <inheritdoc/>
        public async Task<DataObjectItem> ApplyDATMetadata(DataObjectItem TargetObject)
        {
            Console.WriteLine($"Applying DAT metadata to TargetObject: {TargetObject.Name}");

            // libretro only works on game DataObjectItems
            if (TargetObject.ObjectType != DataObjects.DataObjectType.Game)
            {
                {
                    return TargetObject;
                }
            }

            // remove all tags associated with this item before applying new metadata
            string removeTagsSql = "DELETE FROM DataObject_Tags WHERE DataObjectId=@dataobjectid AND MetadataSource=@metadatasource;";
            await Config.database.ExecuteCMDAsync(removeTagsSql, new Dictionary<string, object>
            {
                { "dataobjectid", TargetObject.Id },
                { "metadatasource", Communications.MetadataSources.Libretro }
            });

            // remove all libretro tags from the tags attribute of the item
            if (TargetObject.Attributes != null)
            {
                var tagsAttribute = TargetObject.Attributes.FirstOrDefault(a => a.attributeName == AttributeItem.AttributeName.Tags);
                if (tagsAttribute != null && tagsAttribute.Value != null)
                {
                    Dictionary<hasheous_server.Models.DataObjectItemTags.TagType, DataObjectItemTags> tags = (Dictionary<hasheous_server.Models.DataObjectItemTags.TagType, DataObjectItemTags>)tagsAttribute.Value;
                    foreach (var tagKey in tags.Keys.ToList())
                    {
                        foreach (var tag in tags[tagKey].Tags.ToList())
                        {
                            if (tag.MetadataSource == Communications.MetadataSources.Libretro)
                            {
                                tags[tagKey].Tags.Remove(tag);
                            }
                        }
                    }
                }
            }

            // get all libretro signatures for the platform DataObject (if available)
            foreach (var signature in TargetObject.SignatureDataObjects)
            {
                // check for a libretro file
                if (!signature.ContainsKey("MetadataSource"))
                {
                    continue;
                }

                int signatureTypeId = Convert.ToInt32(signature["MetadataSource"]);

                if (signatureTypeId != (int)SourceType)
                {
                    continue;
                }

                string libretroFile = Path.Combine(metadataExportPath, signature["Platform"] + ".json");
                if (!File.Exists(libretroFile))
                {
                    continue;
                }

                string libretroJson = await File.ReadAllTextAsync(libretroFile);
                HasheousRomSignatureObject? ImportedObject = Newtonsoft.Json.JsonConvert.DeserializeObject<HasheousRomSignatureObject>(libretroJson);
                if (ImportedObject == null)
                {
                    continue;
                }

                // find the game with hasheous id
                var game = ImportedObject.Games.FirstOrDefault(g => g.HasheousGameId == Convert.ToInt64(signature["SignatureId"]));
                if (game == null)
                {
                    continue;
                }

                // check if this signature has a tag associated with it
                if (!game.flags.ContainsKey("genre"))
                {
                    continue;
                }

                string? genre = game.flags["genre"].ToString();
                if (string.IsNullOrWhiteSpace(genre))
                {
                    continue;
                }

                // normalise the tag
                genre = genre.Trim().ToLower();

                // get the existing tag id
                long tagId = -1;
                string tagSql = "SELECT `id` FROM Tags WHERE `type`=@type AND `name`=@name;";
                DataTable tagDt = await Config.database.ExecuteCMDAsync(tagSql, new Dictionary<string, object>
                {
                    { "type", hasheous_server.Models.DataObjectItemTags.TagType.GameGenre },
                    { "name", genre }
                });
                if (tagDt.Rows.Count == 0)
                {
                    // if the tag does not exist, create it and get the new ID
                    string insertTagSql = "INSERT INTO Tags (`type`, `name`) VALUES (@type, @name); SELECT LAST_INSERT_ID();";
                    DataTable insertTagDt = await Config.database.ExecuteCMDAsync(insertTagSql, new Dictionary<string, object>
                    {
                        { "type", hasheous_server.Models.DataObjectItemTags.TagType.GameGenre },
                        { "name", genre }
                    });
                    if (insertTagDt.Rows.Count > 0)
                    {
                        tagId = Convert.ToInt64(insertTagDt.Rows[0][0]);
                    }
                }
                else
                {
                    tagId = Convert.ToInt64(tagDt.Rows[0]["id"]);
                }

                if (tagId == -1)
                {
                    continue;
                }

                string sql = "SELECT * FROM DataObject_SignatureMap WHERE DataObjectTypeId=@datatypeid AND SignatureId=@gameid;";
                DataTable dt = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object>
                {
                    { "datatypeid", DataObjects.DataObjectType.Game },
                    { "gameid", game.HasheousGameId }
                });

                foreach (DataRow row in dt.Rows)
                {
                    // Update the metadata for each data object associated with this game
                    long DataObjectId = (long)row["DataObjectId"];

                    string insertMappingSql = "INSERT INTO DataObject_Tags (DataObjectId, TagId, AIAssigned, MetadataSource, MetadataSourceID) VALUES (@dataobjectid, @tagid, @aiassigned, @metadatasource, @metadatasourceid);";
                    await Config.database.ExecuteCMDAsync(insertMappingSql, new Dictionary<string, object>
                    {
                        { "dataobjectid", DataObjectId },
                        { "tagid", tagId },
                        { "aiassigned", false },
                        { "metadatasource", Communications.MetadataSources.Libretro },
                        { "metadatasourceid", game.HasheousGameId }
                    });
                }
            }

            return TargetObject;
        }
    }
}