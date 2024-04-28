using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Models
{
    public class SubmissionsMatchFixModel
    {
        public string? MD5 { get; set; }
        public string? SHA1 { get; set; }
        public List<MetadataMatch> MetadataMatches { get; set; } = new List<MetadataMatch>();
        public class MetadataMatch
        {
            public Communications.MetadataSources Source { get; set; }
            public string PlatformId { get; set; }
            public string GameId { get; set; }
        }
    }
}