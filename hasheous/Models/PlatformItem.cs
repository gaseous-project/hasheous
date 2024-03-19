namespace hasheous_server.Models
{
    public class PlatformItem
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public CompanyItem Company { get; set; }
        public string RetroPieName { get; set; }
        public long IGDBPlatformId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}