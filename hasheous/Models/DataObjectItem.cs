using hasheous_server.Classes;
using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Models
{
    public class DataObjectItem : DataObjectItemModel
    {
        public long Id { get; set; }
        public List<Dictionary<string, object>>? SignatureDataObjects { get; set; }
        public List<MetadataItem>? Metadata { get; set; }

        public class MetadataItem
        {
            public MetadataItem(DataObjects.DataObjectType ObjectType)
            {
                _ObjectType = ObjectType;
            }

            private DataObjects.DataObjectType _ObjectType;

            public string Id { get; set; }
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
                        default:
                            return "";
                    }
                }
            }
            public DateTime LastSearch { get; set; }
            public DateTime NextSearch { get; set; }
        }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}