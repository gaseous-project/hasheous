using hasheous_server.Classes;
using hasheous_server.Classes.Metadata;

/// <summary>
/// Represents a data object item with its properties and metadata.
/// </summary>
namespace hasheous_server.Models
{
    /// <summary>
    /// Represents a list of data objects with pagination information.
    /// </summary>
    public class DataObjectsList
    {
        /// <summary>
        /// Gets or sets the list of data objects.
        /// </summary>
        public List<DataObjectItem> Objects { get; set; } = new List<DataObjectItem>();
        /// <summary>
        /// Gets or sets the total count of data objects in the list.
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// Gets or sets the current page number of the data objects list.
        /// </summary>
        public int PageNumber { get; set; }
        /// <summary>
        /// Gets or sets the page size (number of items per page) of the data objects list.
        /// </summary>
        public int PageSize { get; set; }
        /// <summary>
        /// Gets or sets the total number of pages available for the data objects list.
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Represents a data object item with its properties, metadata, and attributes.
    /// </summary>
    public class DataObjectItem : DataObjectItemModel
    {
        /// <summary>
        /// Gets or sets the unique identifier of the data object item.
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// Gets or sets the type of the data object item.
        /// </summary>
        public DataObjects.DataObjectType ObjectType { get; set; }
        /// <summary>
        /// Gets or sets the list of signature data objects associated with the data object item.
        /// </summary>
        public List<Dictionary<string, object>>? SignatureDataObjects { get; set; }
        /// <summary>
        /// Gets or sets the list of metadata items associated with the data object item.
        /// </summary>
        public List<MetadataItem>? Metadata { get; set; }
        /// <summary>
        /// Gets or sets the list of attribute items associated with the data object item.
        /// </summary>
        public List<AttributeItem>? Attributes { get; set; }

        /// <summary>
        /// Represents a metadata item associated with a data object item, including its properties and link generation logic.
        /// </summary>
        public class MetadataItem
        {
            /// <summary>
            /// Initializes a new instance of the MetadataItem class with the specified object type.
            /// </summary>
            /// <param name="ObjectType">The type of the data object item.</param>
            public MetadataItem(DataObjects.DataObjectType ObjectType)
            {
                _ObjectType = ObjectType;
            }

            /// <summary>
            /// Gets the type of the data object item associated with this metadata item.
            /// </summary>
            private DataObjects.DataObjectType _ObjectType;

            /// <summary>
            /// Gets the type of the data object item associated with this metadata item.
            /// </summary>
            public DataObjects.DataObjectType ObjectType => _ObjectType;

            /// <summary>
            /// Gets or sets the unique identifier of the metadata item.
            /// </summary>
            public string Id { get; set; }
            /// <summary>
            /// Gets or sets the immutable identifier of the metadata item, which is used to uniquely identify the item across different sources and systems.
            /// </summary>
            public string? ImmutableId { get; set; }
            /// <summary>
            /// Gets or sets the status of the metadata item, indicating whether it is mapped, not mapped, or mapped with errors.
            /// </summary>
            public MappingStatus Status { get; set; }
            /// <summary>
            /// Gets or sets the mapping status of the metadata item, indicating whether it is mapped, not mapped, or mapped with errors.
            /// </summary>
            public enum MappingStatus
            {
                /// <summary>
                /// Indicates that the metadata item is not mapped to any data object.
                /// </summary>
                NotMapped,
                /// <summary>
                /// Indicates that the metadata item is successfully mapped to a data object.
                /// </summary>
                Mapped,
                /// <summary>
                /// Indicates that the metadata item is mapped to a data object but has encountered errors during the mapping process.
                /// </summary>
                MappedWithErrors
            }
            /// <summary>
            /// Gets or sets the match method used to determine the mapping of the metadata item, which can be based on various criteria such as exact match, fuzzy match, or custom matching algorithms.
            /// </summary>
            public BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod? MatchMethod { get; set; }
            /// <summary>
            /// Gets or sets the source of the metadata item, indicating the external system or service from which the metadata was obtained.
            /// </summary>
            public Communications.MetadataSources Source { get; set; }
            /// <summary>
            /// Gets the link to the metadata item based on its source and object type. If the link cannot be generated, it returns an empty string.
            /// </summary>
            public string Link
            {
                get
                {
                    Uri? link = LinkBuilder(Source, _ObjectType, Id).Result;
                    if (link == null)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        return LinkBuilder(Source, _ObjectType, Id).Result.ToString();
                    }
                }
            }
            /// <summary>
            /// Gets or sets the date and time when the metadata item was last searched for updates or changes.
            /// </summary>
            public DateTime LastSearch { get; set; }
            /// <summary>
            /// Gets or sets the date and time when the metadata item is scheduled to be searched for updates or changes next.
            /// </summary>
            public DateTime NextSearch { get; set; }
            /// <summary>
            /// Gets or sets the number of votes received by the metadata item that support its mapping to a data object.
            /// </summary>
            public int WinningVoteCount { get; set; }
            /// <summary>
            /// Gets or sets the total number of votes received by the metadata item, including both supporting and opposing votes.
            /// </summary>
            public int TotalVoteCount { get; set; }
            /// <summary>
            /// Gets the percentage of winning votes for the metadata item, calculated as (WinningVoteCount / TotalVoteCount) * 100. If there are no votes, it returns 0.
            /// </summary>
            public uint WinningVotePercent
            {
                get
                {
                    if (WinningVoteCount == 0 || TotalVoteCount == 0)
                    {
                        return 0;
                    }
                    else
                    {
                        return (uint)Math.Round((decimal)((WinningVoteCount / TotalVoteCount) * 100), 0);
                    }
                }
            }

            /// <summary>
            /// Builds a link to the metadata item based on its source, object type, and identifier. If the identifier is null or empty, it returns null. If the identifier is a valid URL, it returns the URL. If the source is IGDB and the identifier is an integer or long, it retrieves the corresponding IGDB object and uses its slug to build the link. Otherwise, it uses predefined link templates for different sources and object types to construct the link.
            /// </summary>
            /// <param name="source">
            /// The source of the metadata item, indicating the external system or service from which the metadata was obtained.
            /// </param>
            /// <param name="objectType">
            /// The type of the data object to which the metadata item is related.
            /// </param>
            /// <param name="id">
            /// The identifier of the metadata item.
            /// </param>
            /// <returns>
            /// A URI linking to the metadata item, or null if the link cannot be constructed.
            /// </returns>
            private static async Task<Uri?> LinkBuilder(Communications.MetadataSources source, DataObjects.DataObjectType objectType, string id)
            {
                // if id is null or empty, return an empty string
                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }

                // if id is a valid URL, return it
                if (Uri.TryCreate(id, UriKind.Absolute, out Uri? uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    return uriResult;
                }

                // check if the source is IGDB and the id is an integer or long. If it is, get the IGDB object and use the slug
                if (source == Communications.MetadataSources.IGDB && long.TryParse(id, out long igdbId))
                {
                    switch (objectType)
                    {
                        case DataObjects.DataObjectType.Company:
                            IGDB.Models.Company company = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Company>(igdbId);
                            if (company != null)
                            {
                                id = company.Slug;
                            }
                            break;
                        case DataObjects.DataObjectType.Platform:
                            IGDB.Models.Platform platform = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Platform>(igdbId);
                            if (platform != null)
                            {
                                id = platform.Slug;
                            }
                            break;
                        case DataObjects.DataObjectType.Game:
                            IGDB.Models.Game game = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(igdbId);
                            if (game != null)
                            {
                                id = game.Slug;
                            }
                            break;
                        default:
                            return null;
                    }
                }

                // otherwise, build the link based on the source and object type
                if (_LinkTemplates.TryGetValue(source, out List<LinkTemplateItem>? templates))
                {
                    var template = templates.FirstOrDefault(t => t.ObjectType == objectType);
                    if (template != null)
                    {
                        return new Uri(string.Format(template.Template, id));
                    }
                }

                return null;
            }

            /// <summary>
            /// Gets the predefined link templates for different metadata sources and object types. Each entry in the dictionary maps a metadata source to a list of link template items, which specify the object type and the corresponding URL template for constructing links to metadata items.
            /// </summary>
            private static Dictionary<Communications.MetadataSources, List<LinkTemplateItem>>? _LinkTemplates = new Dictionary<Communications.MetadataSources, List<LinkTemplateItem>>
            {
                {
                    Communications.MetadataSources.IGDB,
                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.IGDB,
                            ObjectType = DataObjects.DataObjectType.Company,
                            Template = "https://www.igdb.com/companies/{0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.IGDB,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://www.igdb.com/platforms/{0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.IGDB,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://www.igdb.com/games/{0}"
                        }
                    }
                },
                {
                    Communications.MetadataSources.TheGamesDb,
                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.TheGamesDb,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://thegamesdb.net/platform.php?id={0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.TheGamesDb,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://thegamesdb.net/game.php?id={0}"
                        }
                    }
                },
                {
                    Communications.MetadataSources.RetroAchievements,
                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.RetroAchievements,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://retroachievements.org/system/{0}/games"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.RetroAchievements,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://retroachievements.org/game/{0}"
                        }
                    }
                },
                {
                    Communications.MetadataSources.GiantBomb,
                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.GiantBomb,
                            ObjectType = DataObjects.DataObjectType.Company,
                            Template = "https://www.giantbomb.com/companies/3010-{0}/"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.GiantBomb,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://www.giantbomb.com/platforms/3045-{0}/"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.GiantBomb,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://www.giantbomb.com/games/3030-{0}/"
                        }
                    }
                },
                {
                    Communications.MetadataSources.SteamGridDb,
                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.SteamGridDb,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://www.steamgriddb.com/game/{0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.SteamGridDb,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://www.steamgriddb.com/game/{0}"
                        }
                    }
                },
                {
                    Communications.MetadataSources.ScreenScraper,
                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.ScreenScraper,
                            ObjectType = DataObjects.DataObjectType.Company,
                            Template = "https://www.screenscraper.fr/companieinfos.php?companyid={0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.ScreenScraper,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://www.screenscraper.fr/systemeinfos.php?plateforme={0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.ScreenScraper,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://www.screenscraper.fr/gameinfos.php?gameid={0}"
                        }
                    }
                },
                {
                    Communications.MetadataSources.LaunchBox,

                    new List<LinkTemplateItem>
                    {
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.LaunchBox,
                            ObjectType = DataObjects.DataObjectType.Platform,
                            Template = "https://gamesdb.launchbox-app.com/platforms/games/{0}"
                        },
                        new LinkTemplateItem
                        {
                            Source = Communications.MetadataSources.LaunchBox,
                            ObjectType = DataObjects.DataObjectType.Game,
                            Template = "https://gamesdb.launchbox-app.com/games/dbid/{0}"
                        }
                    }
                }
            };

            /// <summary>
            /// Represents a link template item that specifies the metadata source, object type, and URL template for constructing links to metadata items.
            /// </summary>
            private class LinkTemplateItem
            {
                public Communications.MetadataSources Source { get; set; }
                public DataObjects.DataObjectType ObjectType { get; set; }
                public string Template { get; set; }
            }
        }
        /// <summary>
        /// Gets or sets the date and time when the data object item was created. This property is used to track the creation timestamp of the data object item in the system.
        /// </summary>
        public DateTime CreatedDate { get; set; }
        /// <summary>
        /// Gets or sets the date and time when the data object item was last updated. This property is used to track the last modification timestamp of the data object item in the system.
        /// </summary>
        public DateTime UpdatedDate { get; set; }
        /// <summary>
        /// Gets or sets the list of permissions associated with the data object item. Each permission specifies the type of access granted to users or roles for the data object item.
        /// </summary>
        public List<DataObjectPermission.PermissionType>? Permissions { get; set; }
        /// <summary>
        /// Gets or sets the user-specific permissions for the data object item. This property is a dictionary that maps user identifiers to a list of permission types, allowing for fine-grained control over access to the data object item based on individual users.
        /// </summary>
        public Dictionary<string, List<DataObjectPermission.PermissionType>>? UserPermissions { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the data object item is blocked. A blocked data object item will not be returned to the user in API responses.
        /// </summary>
        public bool IsBlocked { get; set; }
    }
}