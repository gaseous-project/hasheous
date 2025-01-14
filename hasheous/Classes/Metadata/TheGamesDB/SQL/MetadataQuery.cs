using Classes;

namespace TheGamesDB.SQL
{
    public class QueryModel
    {
        public string? query { get; set; }
        public QueryFieldName? queryField { get; set; }
        public string? fieldList { get; set; }
        public string? includeList { get; set; }
        public string? filter { get; set; }
        public int page { get; set; }
        public enum QueryFieldName
        {
            id,
            name
        }
    }

    public class MetadataQuery
    {
        public static T GetMetadata<T>(QueryModel queryModel)
        {
            // get type of T as a string
            string typeName = typeof(T).Name;

            // setup database objects
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionStringNoDatabase);
            string sql = "";
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            // select query based on type
            switch (typeName)
            {
                case "GamesByGameID":
                    // setup query

                    // extract 
                    break;

                default:
                    break;
            }
        }
    }
}