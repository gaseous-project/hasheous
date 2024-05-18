using System.Security.Cryptography.X509Certificates;
using gaseous_signature_parser.models.RomSignatureObject;

namespace hasheous_server.Models
{
    public class SourceItem
    {
        public SourceItem()
        {
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Email { get; set; }
        public string Homepage { get; set; }
        public string Url { get; set; }
        public RomSignatureObject.Game.Rom.SignatureSourceType SourceType { get; set; }
        public string SourceMD5 { get; set; }
        public string SourceSHA1 { get; set; }
    }
}