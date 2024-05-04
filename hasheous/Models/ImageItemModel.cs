namespace hasheous_server.Models
{
    public class ImageItem
    {
        public string Id { get; set; }
        public byte[] content { get; set; }
        public string mimeType { get; set; }
        public string extension { get; set; }
    }
}