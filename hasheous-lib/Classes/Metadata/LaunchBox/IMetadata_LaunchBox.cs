using System.Data;
using Classes;
using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// IGDB metadata provider that implements <see cref="IMetadata"/> to locate and return
    /// metadata matches from the IGDB source for data objects (companies, platforms, games).
    /// </summary>
    public class MetadataLaunchBox : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.LaunchBox;

        /// <inheritdoc/>
        public bool Enabled
        {
            get
            {
                // check if the database "launchbox" exists in our configured database server - if it doesn't then this metadata source is not enabled
                Database db = Config.database;
                DataTable result = db.ExecuteCMD("SHOW DATABASES LIKE 'launchbox'");
                return result.Rows.Count > 0;
            }
        }

        /// <inheritdoc/>
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(hasheous_server.Models.DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            DataObjects dataObjects = new DataObjects();

            hasheous_server.Classes.DataObjects.MatchItem DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            switch (item.ObjectType)
            {
                case DataObjects.DataObjectType.Game:
                    // needs to have a platformId option provided to search properly
                    if (options == null || !options.ContainsKey("platformIdString"))
                    {
                        throw new ArgumentException("Platform ID must be provided in options for LaunchBox game search.");
                    }
                    // platformId is expected to be a string in the form of "25-nintendo-64" - need to remove the leading numeric ID and hyphen to get the platform name
                    string platformIdStr = options["platformIdString"].ToString();
                    string platformName = platformIdStr.Substring(platformIdStr.IndexOf('-') + 1);
                    platformName = platformName.Replace('-', ' '); // also replace any remaining hyphens with spaces to match LaunchBox formatting
                    platformName = platformName.Trim();

                    // get our id for the platform so we can FK match against it in the File entries
                    Database db = Config.database;
                    string platformQuery = $"SELECT `Id` FROM `launchbox`.`Platform` WHERE `Name` = @Name";
                    DataTable platformResult = await db.ExecuteCMDAsync(platformQuery, new Dictionary<string, object> { { "@Name", platformName } });
                    long platformId = -1;
                    if (platformResult.Rows.Count > 0)
                    {
                        platformId = Convert.ToInt64(platformResult.Rows[0]["Id"]);
                    }
                    if (platformId == -1)
                    {
                        // platform not found in our database - cannot proceed with search
                        break;
                    }

                    // search for games matching each candidate name until we find a match or exhaust the list
                    foreach (string candidate in searchCandidates)
                    {
                        DataObjectSearchResults = await SearchGamesAsync(platformId, candidate);
                        if (DataObjectSearchResults != null)
                        {
                            return DataObjectSearchResults;
                        }
                    }

                    break;
                default:
                    DataObjectSearchResults = new()
                    {
                        MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                        MetadataId = ""
                    };
                    break;
            }

            return DataObjectSearchResults;
        }

        private async Task<DataObjects.MatchItem?> SearchGamesAsync(long platformId, string candidate)
        {
            DataTable games = await Config.database.ExecuteCMDAsync("SELECT `DatabaseID`, `Name` FROM `launchbox`.`Game` WHERE `Platform` = @platformId AND `Name` LIKE @candidate", new Dictionary<string, object>
            {
                { "@platformId", platformId },
                { "@candidate", $"%{candidate}%" }
            });

            // no data returned
            if (games == null || games.Rows == null || games.Rows.Count == 0)
            {
                return null;
            }

            // search results
            if (games.Rows.Count == 1)
            {
                // exact match found
                var game = games.Rows[0];
                return new DataObjects.MatchItem
                {
                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                    MetadataId = game["DatabaseID"]?.ToString() ?? ""
                };
            }
            else if (games.Rows.Count > 1)
            {
                // multiple matches found - try and narrow it down a bit more
                foreach (DataRow game in games.Rows)
                {
                    // check for exact name match
                    if (string.Equals(game["Name"]?.ToString() ?? "", candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return new DataObjects.MatchItem
                        {
                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                            MetadataId = game["DatabaseID"]?.ToString() ?? ""
                        };
                    }
                }
            }

            return null;
        }
    }
}