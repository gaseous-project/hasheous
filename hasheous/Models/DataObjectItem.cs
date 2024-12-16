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
            public BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod? MatchMethod { get; set; }
            public Communications.MetadataSources Source { get; set; }
            public string Link
            {
                get
                {
                    switch (Source)
                    {
                        case Communications.MetadataSources.None:
                            return "";
                        case Communications.MetadataSources.IGDB:
                            if (Id.Length > 0)
                            {
                                switch (_ObjectType)
                                {
                                    case DataObjects.DataObjectType.Company:
                                        return "https://www.igdb.com/companies/" + Id;

                                    case DataObjects.DataObjectType.Platform:
                                        return "https://www.igdb.com/platforms/" + Id;

                                    case DataObjects.DataObjectType.Game:
                                        return "https://www.igdb.com/games/" + Id;

                                    default:
                                        return "";
                                }
                            }
                            else
                            {
                                return "";
                            }
                        case Communications.MetadataSources.TheGamesDb:
                            if (Id.Length > 0)
                            {
                                switch (_ObjectType)
                                {
                                    case DataObjects.DataObjectType.Platform:
                                        return "https://thegamesdb.net/platform.php?id=" + Id;

                                    case DataObjects.DataObjectType.Game:
                                        return "https://thegamesdb.net/game.php?id=" + Id;

                                    default:
                                        return "";
                                }
                            }
                            else
                            {
                                return "";
                            }
                        default:
                            return "";
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
        }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public List<DataObjectPermission.PermissionType>? Permissions { get; set; }
        public Dictionary<string, List<DataObjectPermission.PermissionType>>? UserPermissions { get; set; }
    }
}