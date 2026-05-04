using Classes;
using craftersmine.SteamGridDBNet;
using hasheous_server.Classes.Metadata;
using hasheous_server.Models;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// SteamGridDB metadata provider that implements <see cref="IMetadata"/> to locate and return
    /// metadata matches from the SteamGridDB source for data objects.
    /// </summary>
    public class MetadataSteamGridDB : IMetadata
    {
        /// <inheritdoc />
        public Communications.MetadataSources MetadataSource => Communications.MetadataSources.SteamGridDb;

        /// <inheritdoc />
        public bool Enabled
        {
            get
            {
                return !String.IsNullOrEmpty(Config.SteamGridDBConfiguration.APIKey);
            }
        }

        /// <inheritdoc />
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            // escape early if we don't have an API key
            if (Enabled == false)
            {
                return DataObjectSearchResults;
            }

            // initialize the SteamGridDB client
            SteamGridDb sgdb = new SteamGridDb(Config.SteamGridDBConfiguration.APIKey);

            // insert the item name into the searchCandidates list as the first item to search for
            searchCandidates.Insert(0, item.Name);

            // loop through the search candidates and attempt to find a match on SteamGridDB
            foreach (string searchCandidate in searchCandidates)
            {
                SteamGridDbGame[]? games = await sgdb.SearchForGamesAsync(searchCandidate);

                if (games != null && games.Length > 0)
                {
                    // if we get only one result back, we'll consider it a match and return it
                    if (games.Length == 1)
                    {
                        DataObjectSearchResults.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                        DataObjectSearchResults.MetadataId = games[0].Id.ToString();
                        return DataObjectSearchResults;
                    }
                    else if (games.Length > 1)
                    {
                        // Evaluate all results and pick the strongest name match instead of first-hit wins.
                        SteamGridDbGame? bestGame = null;
                        int bestScore = int.MinValue;

                        foreach (SteamGridDbGame game in games)
                        {
                            int score = Common.GetStrongNameMatchScore(searchCandidate, game.Name);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestGame = game;
                            }
                            else if (score == bestScore && bestGame != null)
                            {
                                int currentLength = game.Name?.Length ?? int.MaxValue;
                                int bestLength = bestGame.Name?.Length ?? int.MaxValue;
                                if (currentLength < bestLength)
                                {
                                    bestGame = game;
                                }
                            }
                        }

                        if (bestGame != null && bestScore >= 8)
                        {
                            DataObjectSearchResults.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                            DataObjectSearchResults.MetadataId = bestGame.Id.ToString();
                            return DataObjectSearchResults;
                        }
                    }
                }
            }

            // if we get here, we didn't find a match
            return DataObjectSearchResults;
        }
    }
}