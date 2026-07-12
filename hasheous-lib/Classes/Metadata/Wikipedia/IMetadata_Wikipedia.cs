using System.Data;
using System.Globalization;
using Classes;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Provides metadata lookup functionality using Wikipedia as a source.
    /// Implements IMetadata to locate and map external metadata for data objects.
    /// </summary>
    public class MetadataWikipedia : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.Wikipedia;

        /// <inheritdoc/>
        public bool Enabled
        {
            get
            {
                return true;
            }
        }

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
                case DataObjects.DataObjectType.Game:
                    // currently wikipedia metadata lookup is only available from IGDB game metadata
                    long? igdbGameId = null;
                    string sql = "SELECT `MetadataId` FROM `DataObject_MetadataMap` WHERE `DataObjectId` = @DataObjectId AND `SourceId` = @MetadataSource";
                    DataTable? dt = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object>
                    {
                        { "@DataObjectId", item.Id },
                        { "@MetadataSource", hasheous_server.Classes.Metadata.Communications.MetadataSources.IGDB }
                    });
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        if (TryGetLongFromObject(dt.Rows[0]["MetadataId"], out long parsedMetadataId))
                        {
                            igdbGameId = parsedMetadataId;
                        }
                    }

                    // if no valid IGDB metadata id is found in the database, check options for a supplied id
                    if (igdbGameId == null)
                    {
                        if (options == null || !options.TryGetValue("igdbGameId", out object? optionValue) || !TryGetLongFromObject(optionValue, out long parsedOptionId))
                        {
                            throw new ArgumentException("IGDB Game ID must be provided in options for Wikipedia game search.");
                        }
                        igdbGameId = parsedOptionId;
                    }

                    if (igdbGameId == null)
                    {
                        throw new ArgumentException("IGDB Game ID must be provided for Wikipedia game search.");
                    }

                    HasheousClient.Models.Metadata.IGDB.Game? igdbGame = await Metadata.IGDB.Metadata.GetMetadata<HasheousClient.Models.Metadata.IGDB.Game>(igdbGameId);
                    if (igdbGame != null)
                    {
                        // check the websites array for a wikipedia link
                        if (igdbGame.Websites != null && igdbGame.Websites.Count > 0)
                        {
                            foreach (var website in igdbGame.Websites)
                            {
                                HasheousClient.Models.Metadata.IGDB.Website? webGame = await Metadata.IGDB.Metadata.GetMetadata<HasheousClient.Models.Metadata.IGDB.Website>(website);
                                if (webGame != null)
                                {
                                    if (webGame.Type == 3)
                                    {
                                        DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                        {
                                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                            MetadataId = webGame.Url
                                        };
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }

            return DataObjectSearchResults;
        }

        private static bool TryGetLongFromObject(object? value, out long result)
        {
            result = 0;

            if (value == null || value == DBNull.Value)
            {
                return false;
            }

            switch (value)
            {
                case long l:
                    result = l;
                    return true;
                case int i:
                    result = i;
                    return true;
                case short s:
                    result = s;
                    return true;
                case byte b:
                    result = b;
                    return true;
                case sbyte sb:
                    result = sb;
                    return true;
                case ushort us:
                    result = us;
                    return true;
                case uint ui:
                    result = ui;
                    return true;
                case ulong ul when ul <= long.MaxValue:
                    result = (long)ul;
                    return true;
                case decimal dec when dec >= long.MinValue && dec <= long.MaxValue && dec == decimal.Truncate(dec):
                    result = (long)dec;
                    return true;
                case string str:
                    return long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            }

            string? stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(stringValue) && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }
    }
}