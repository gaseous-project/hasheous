using System.Threading.Tasks;
using GiantBomb.Models;
using hasheous_server.Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Models
{
    public class DataObjectsList
    {
        public List<DataObjectItem> Objects { get; set; }
        public int Count { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class DataObjectItem : DataObjectItemModel
    {
        public long Id { get; set; }
        public DataObjects.DataObjectType ObjectType { get; set; }
        public List<Dictionary<string, object>>? SignatureDataObjects { get; set; }
        public List<MetadataItem>? Metadata { get; set; }
        public List<AttributeItem>? Attributes { get; set; }

        public class MetadataItem
        {
            public MetadataItem(DataObjects.DataObjectType ObjectType)
            {
                _ObjectType = ObjectType;
            }

            private DataObjects.DataObjectType _ObjectType;

            public string Id { get; set; }
            public string? ImmutableId { get; set; }
            public MappingStatus Status { get; set; }
            public enum MappingStatus
            {
                NotMapped,
                Mapped,
                MappedWithErrors
            }
            public BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod? MatchMethod { get; set; }
            public Communications.MetadataSources Source { get; set; }
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
            public DateTime LastSearch { get; set; }
            public DateTime NextSearch { get; set; }
            public int WinningVoteCount { get; set; }
            public int TotalVoteCount { get; set; }
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
                }
            };

            private class LinkTemplateItem
            {
                public Communications.MetadataSources Source { get; set; }
                public DataObjects.DataObjectType ObjectType { get; set; }
                public string Template { get; set; }
            }
        }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public List<DataObjectPermission.PermissionType>? Permissions { get; set; }
        public Dictionary<string, List<DataObjectPermission.PermissionType>>? UserPermissions { get; set; }
    }
}