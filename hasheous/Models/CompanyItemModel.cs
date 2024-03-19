using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Models
{
    public class CompanyItemModel
    {
        public string Name { get; set; }
        public List<int>? SignatureCompanies { get; set; }
        public List<MetadataItem>? Metadata { get; set; }

        public class MetadataItem
        {
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
                                return "https://www.igdb.com/companies/" + Id;
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
    }
}