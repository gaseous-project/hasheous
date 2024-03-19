using System.Data;
using Classes;

namespace hasheous_server.Classes
{
    public class Platforms
    {
        public List<Models.PlatformItem> GetPlatforms()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Platform LEFT JOIN Company ON Platform.CompanyId = Company.Id ORDER BY Company.`Name`, Platform.`Name`";
            DataTable data = db.ExecuteCMD(sql);

            List<Models.PlatformItem> platforms = new List<Models.PlatformItem>();
            foreach (DataRow row in data.Rows)
            {
                Models.PlatformItem platform = new Models.PlatformItem{
                    Id = (long)row["Platform.Id"],
                    Name = (string)row["Platform.Name"],
                    Company = new hasheous_server.Models.CompanyItem
                    {
                        Id = (long)row["Company.Id"],
                        Name = (string)row["Company.Name"],
                        CreatedDate = (DateTime)row["Company.CreatedDate"],
                        UpdatedDate = (DateTime)row["Company.UpdatedDate"]
                    },
                    RetroPieName = (string)row["Platform.RetroPieNam"],
                    IGDBPlatformId = (long)row["Platform.IGDBPlatformId"],
                    CreatedDate = (DateTime)row["Platform.CreatedDate"],
                    UpdatedDate = (DateTime)row["Platform.UpdatedDate"]
                };

                platforms.Add(platform);
            }

            return platforms;
        }
    }
}