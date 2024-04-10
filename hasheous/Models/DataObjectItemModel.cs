using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Models
{
    public class DataObjectItemModel
    {
        public string Name { get; set; }
    }

    public class AttributeItem
    {
        public enum AttributeType
        {
            LongString = 0,
            ShortString = 1,
            DateTime = 2,
            ImageId = 3,
            ObjectRelationship = 10,
            EmbeddedList = 11
        }

        public enum AttributeName
        {
            Description = 0,
            Manufacturer = 1,
            Publisher = 2,
            Logo = 3,
            Platform = 4,
            Year = 5,
            Country = 6,
            Language = 7,
            ROMs = 8,
            VIMMManualId = 9
        }

        public long? Id { get; set; }
        public AttributeType attributeType { get; set; }
        public AttributeName attributeName { get; set; }
        public Classes.DataObjects.DataObjectType attributeRelationType { get; set; }
        public object Value { get; set; }
    }

    public class AttributeItemCompiled : AttributeItem
    {
        public string? Link
        {
            get
            {
                switch (attributeType)
                {
                    case AttributeType.ShortString:
                        switch (attributeName)
                        {
                            case AttributeName.VIMMManualId:
                                return "https://vimm.net/manual/" + Value.ToString();

                            default:
                                return null;
                        }

                    default:
                        return null;
                }
            }
        }
    }

    public class RelationItem
    {
        public Classes.DataObjects.DataObjectType relationType { get; set; }
        public long relationId { get; set; }
    }
}