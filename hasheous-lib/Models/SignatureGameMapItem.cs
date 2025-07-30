using static BackgroundMetadataMatcher.BackgroundMetadataMatcher;

namespace hasheous_server.Models
{
    public class SignatureGameMapItem
    {
        public long SignatureGameId { get; set; }
        public long IGDBGameId { get; set; }
        public MatchMethod MatchMethod { get; set; }
        public DateTime LastSearch { get; set; }
        public DateTime NextSearch { get; set; }
    }
}