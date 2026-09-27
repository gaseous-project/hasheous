using Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.HowLongToBeat;
using hasheous_server.Models;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// HowLongToBeat metadata provider that implements <see cref="IMetadata"/> to locate and return
    /// metadata matches from the HowLongToBeat source for game data objects.
    /// HowLongToBeat has no platform identifiers, so games are matched by name, with the Hasheous
    /// platform name (supplied in the "platformName" option) used to break ties.
    /// </summary>
    public class MetadataHowLongToBeat : IMetadata
    {
        /// <summary>
        /// Minimum name match score required to accept a result as an automatic match.
        /// </summary>
        public const int MinimumMatchScore = 8;

        /// <inheritdoc />
        public Communications.MetadataSources MetadataSource => Communications.MetadataSources.HowLongToBeat;

        /// <inheritdoc />
        public bool Enabled
        {
            get
            {
                return Config.HowLongToBeatConfiguration.Enabled;
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

            // HowLongToBeat only has game records
            if (Enabled == false || item.ObjectType != DataObjects.DataObjectType.Game)
            {
                return DataObjectSearchResults;
            }

            string? platformName = null;
            if (options != null && options.TryGetValue("platformName", out object? platformNameValue))
            {
                platformName = platformNameValue?.ToString();
            }

            // insert the item name into the searchCandidates list as the first item to search for
            searchCandidates.Insert(0, item.Name);

            foreach (string searchCandidate in searchCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                List<HowLongToBeatGame> games = await HowLongToBeatClient.SearchAsync(searchCandidate);

                HowLongToBeatGame? bestGame = SelectBestMatch(searchCandidate, platformName, games);
                if (bestGame != null)
                {
                    DataObjectSearchResults.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                    DataObjectSearchResults.MetadataId = bestGame.GameId.ToString();
                    return DataObjectSearchResults;
                }
            }

            // if we get here, we didn't find a match
            return DataObjectSearchResults;
        }

        /// <summary>
        /// Picks the best HowLongToBeat result for a search candidate.
        /// Results are ranked by name match score (including aliases), then by whether the result is
        /// available on the supplied platform, then by whether it is a base game (not a hack/mod/DLC),
        /// then by the number of completions recorded.
        /// </summary>
        /// <param name="searchCandidate">The name that was searched for.</param>
        /// <param name="platformName">The Hasheous platform name for the game, if known.</param>
        /// <param name="games">The search results returned by HowLongToBeat.</param>
        /// <returns>The best matching game, or null if no result scores at least <see cref="MinimumMatchScore"/>.</returns>
        public static HowLongToBeatGame? SelectBestMatch(string searchCandidate, string? platformName, List<HowLongToBeatGame>? games)
        {
            if (games == null || games.Count == 0)
            {
                return null;
            }

            return games
                .Select(game => new
                {
                    Game = game,
                    Score = GetNameScore(searchCandidate, game),
                    PlatformMatch = IsPlatformMatch(platformName, game.ProfilePlatform),
                    IsBaseGame = String.Equals(game.GameType, "game", StringComparison.OrdinalIgnoreCase)
                })
                .Where(x => x.Score >= MinimumMatchScore)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.PlatformMatch)
                .ThenByDescending(x => x.IsBaseGame)
                .ThenByDescending(x => x.Game.CountComp)
                .Select(x => x.Game)
                .FirstOrDefault();
        }

        private static int GetNameScore(string searchCandidate, HowLongToBeatGame game)
        {
            int score = Common.GetStrongNameMatchScore(searchCandidate, game.GameName);

            if (!String.IsNullOrWhiteSpace(game.GameAlias))
            {
                foreach (string alias in game.GameAlias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    score = Math.Max(score, Common.GetStrongNameMatchScore(searchCandidate, alias));
                }
            }

            return score;
        }

        /// <summary>
        /// Determines whether any of the HowLongToBeat platforms (a comma separated list, e.g.
        /// "Nintendo Switch, PC") corresponds to the Hasheous platform name. A platform is considered
        /// a match when all the words of one name appear in the other, so "Super Nintendo" matches
        /// "Super Nintendo Entertainment System".
        /// </summary>
        /// <param name="platformName">The Hasheous platform name.</param>
        /// <param name="profilePlatforms">The HowLongToBeat platform list.</param>
        /// <returns>True if a platform matches; otherwise false.</returns>
        public static bool IsPlatformMatch(string? platformName, string? profilePlatforms)
        {
            if (String.IsNullOrWhiteSpace(platformName) || String.IsNullOrWhiteSpace(profilePlatforms))
            {
                return false;
            }

            HashSet<string> platformTokens = Tokenize(platformName);
            if (platformTokens.Count == 0)
            {
                return false;
            }

            foreach (string profilePlatform in profilePlatforms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                HashSet<string> profileTokens = Tokenize(profilePlatform);
                if (profileTokens.Count == 0)
                {
                    continue;
                }

                if (profileTokens.IsSubsetOf(platformTokens) || platformTokens.IsSubsetOf(profileTokens))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> Tokenize(string value)
        {
            return new HashSet<string>(
                value.Split(new[] { ' ', '-', '/', '(', ')', ':', '.', '_' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
