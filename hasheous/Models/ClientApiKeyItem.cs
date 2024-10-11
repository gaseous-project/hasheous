namespace hasheous_server.Models
{
    public class ClientApiKeyItem
    {
        public long ClientId { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Expires { get; set; }
        public bool Revoked { get; set; }
    }
}