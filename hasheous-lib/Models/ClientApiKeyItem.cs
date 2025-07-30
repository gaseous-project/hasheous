namespace hasheous_server.Models
{
    public class ClientApiKeyItem
    {
        public long? KeyId { get; set; }
        public long? ClientAppId { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Expires { get; set; }
        public bool Expired
        {
            get
            {
                if (Expires == null)
                {
                    return false;
                }
                else
                {
                    return Expires < DateTime.UtcNow;
                }
            }
        }
        public bool Revoked { get; set; }
    }
}