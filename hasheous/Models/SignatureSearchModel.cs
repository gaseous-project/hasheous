namespace hasheous_server.Models
{
    public class SignatureSearchModel
    {
        public enum SignatureSearchTypes
        {
            Publisher,
            Platform,
            Game,
            Rom
        }

        public SignatureSearchTypes SearchType { get; set; }
        public int[]? Ids { get; set; }
        public string? Name { get; set; }
    }
}