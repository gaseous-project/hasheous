using Classes;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Metadata provider implementation for the RetroAchievements source.
    /// </summary>
    public class MetadataRetroAchievements : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.RetroAchievements;

        /// <inheritdoc/>
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(hasheous_server.Models.DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            switch (item.ObjectType)
            {
                case DataObjects.DataObjectType.Platform:
                    string platformJsonPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_RetroAchievements, "platforms.json");
                    if (File.Exists(platformJsonPath))
                    {
                        string platformsJson = File.ReadAllText(platformJsonPath);
                        List<RetroAchievements.Models.PlatformModel>? platforms = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RetroAchievements.Models.PlatformModel>>(platformsJson);

                        if (platforms != null)
                        {
                            foreach (RetroAchievements.Models.PlatformModel platform in platforms)
                            {
                                if (searchCandidates.Any(candidate => string.Equals(platform.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                                {
                                    DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                    {
                                        MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                        MetadataId = platform.ID.ToString()
                                    };
                                }
                            }
                        }
                    }

                    break;

                case DataObjects.DataObjectType.Game:
                    // needs to have a platformId option provided to search properly
                    if (options == null || !options.ContainsKey("platformId"))
                    {
                        throw new ArgumentException("Platform ID must be provided in options for TheGamesDB game search.");
                    }
                    // check that options["platformId"] is a long
                    if (options["platformId"] == null || options["platformId"].GetType() != typeof(long))
                    {
                        throw new ArgumentException("Platform ID must be of type long for TheGamesDB game search.");
                    }
                    long platformId = (long)options["platformId"];

                    string gamesJsonPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_RetroAchievements, platformId.ToString());
                    if (Directory.Exists(gamesJsonPath))
                    {
                        List<RetroAchievements.Models.GameModel> games = new List<RetroAchievements.Models.GameModel>();
                        foreach (string gamesJsonFile in Directory.GetFiles(gamesJsonPath, "*.json"))
                        {
                            string gamesJson = File.ReadAllText(gamesJsonFile);
                            List<RetroAchievements.Models.GameModel>? platformGames = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RetroAchievements.Models.GameModel>>(gamesJson);
                            if (platformGames != null)
                            {
                                games.AddRange(platformGames);
                            }
                        }

                        foreach (RetroAchievements.Models.GameModel game in games)
                        {
                            if (searchCandidates.Any(candidate => string.Equals(game.Title, candidate, StringComparison.OrdinalIgnoreCase)))
                            {
                                DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                {
                                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                    MetadataId = game.ID.ToString()
                                };
                            }
                        }
                    }

                    break;
            }

            return DataObjectSearchResults;
        }
    }
}