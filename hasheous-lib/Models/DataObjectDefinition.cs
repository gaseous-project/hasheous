namespace hasheous_server.Models
{
    public class DataObjectDefinition
    {
        public List<AttributeItem> Attributes { get; set; }
        public bool HasMetadata { get; set; }
        public bool HasSignatures { get; set; }
        public bool AllowMerge { get; set; }
    }
}