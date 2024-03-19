using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Models
{
    public class DataObjectItemModel
    {
        public string Name { get; set; }
        public List<int>? SignatureDataObjects { get; set; }
    }
}