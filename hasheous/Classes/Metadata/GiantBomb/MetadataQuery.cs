using Classes;

namespace GiantBomb
{
    public class MetadataQuery
    {
        public static long PlatformLookup(string platformName)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            string sql = "SELECT `Id` FROM `giantbomb`.`Platform` WHERE LOWER(`name`) = @name;";
            var parameters = new Dictionary<string, object>
            {
                { "@name", platformName.ToLower() }
            };

            var result = db.ExecuteCMD(sql, parameters);

            if (result.Rows.Count > 0)
            {
                // Assuming the first row contains the platform ID
                return Convert.ToInt64(result.Rows[0]["Id"]);
            }
            else
            {
                return 0;
            }
        }

        public static long GameLookup(long platformId, string gameName)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            string sql = "SELECT `Id` FROM `giantbomb`.`Game` JOIN `giantbomb`.`Relation_Game_platforms` ON `giantbomb`.`Game`.`Id` = `giantbomb`.`Relation_Game_platforms`.`Game_id` WHERE LOWER(`name`) = @name AND `giantbomb`.`Relation_Game_platforms`.`platforms_id` = @platformId";
            var parameters = new Dictionary<string, object>
            {
                { "@name", gameName.ToLower() },
                { "@platformId", platformId }
            };

            var result = db.ExecuteCMD(sql, parameters);

            if (result.Rows.Count > 0)
            {
                // Assuming the first row contains the game ID
                return Convert.ToInt64(result.Rows[0]["Id"]);
            }
            else
            {
                return 0;
            }
        }
    }
}