using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;

namespace hasheous_server.Classes
{
    public class Sources
    {
        public List<hasheous_server.Models.SourceItem> GetSources(RomSignatureObject.Game.Rom.SignatureSourceType sourceType)
        {
            string SourceTypeLabel = "";
            switch (sourceType)
            {
                case RomSignatureObject.Game.Rom.SignatureSourceType.TOSEC:
                case RomSignatureObject.Game.Rom.SignatureSourceType.MAMEArcade:
                case RomSignatureObject.Game.Rom.SignatureSourceType.MAMEMess:
                case RomSignatureObject.Game.Rom.SignatureSourceType.Redump:
                case RomSignatureObject.Game.Rom.SignatureSourceType.RetroAchievements:
                case RomSignatureObject.Game.Rom.SignatureSourceType.FBNeo:
                    SourceTypeLabel = sourceType.ToString();
                    break;

                case RomSignatureObject.Game.Rom.SignatureSourceType.NoIntros:
                    SourceTypeLabel = "No-Intro";
                    break;

                default:
                    throw new Exception("Invalid source type");
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Signatures_Sources WHERE SourceType = @sourcetype ORDER BY `Name` ASC, Version DESC;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "sourcetype", SourceTypeLabel }
            };
            DataTable table = db.ExecuteCMD(sql, dbDict);

            List<hasheous_server.Models.SourceItem> sourceItems = new List<hasheous_server.Models.SourceItem>();

            foreach (DataRow row in table.Rows)
            {
                sourceItems.Add(BuildSourceItem(row));
            }

            return sourceItems;
        }

        public Dictionary<RomSignatureObject.Game.Rom.SignatureSourceType, int> GetSourceStatistics()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT MetadataSource, COUNT(Id) AS RomCount FROM Signatures_Roms GROUP BY MetadataSource;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            DataTable table = db.ExecuteCMD(sql, dbDict);

            Dictionary<RomSignatureObject.Game.Rom.SignatureSourceType, int> romCount = new Dictionary<RomSignatureObject.Game.Rom.SignatureSourceType, int>();

            foreach (DataRow row in table.Rows)
            {
                romCount.Add((RomSignatureObject.Game.Rom.SignatureSourceType)row["MetadataSource"], (int)(long)row["RomCount"]);
            }

            return romCount;
        }

        public hasheous_server.Models.SourceItem BuildSourceItem(DataRow row)
        {
            hasheous_server.Models.SourceItem sourceItem = new hasheous_server.Models.SourceItem
            {
                Id = (int)row["Id"],
                Name = (string)row["Name"],
                Description = (string)row["Description"],
                Category = (string)row["Category"],
                Version = (string)row["Version"],
                Author = (string)row["Author"],
                Email = (string)row["Email"],
                Homepage = (string)row["Homepage"],
                Url = (string)row["Url"],
                SourceMD5 = (string)row["SourceMD5"],
                SourceSHA1 = (string)row["SourceSHA1"]
            };

            switch ((string)row["SourceType"])
            {
                case "No-Intro":
                    sourceItem.SourceType = RomSignatureObject.Game.Rom.SignatureSourceType.NoIntros;
                    break;
                default:
                    sourceItem.SourceType = (RomSignatureObject.Game.Rom.SignatureSourceType)Enum.Parse(typeof(RomSignatureObject.Game.Rom.SignatureSourceType), row["SourceType"].ToString());
                    break;
            }

            return sourceItem;
        }
    }
}