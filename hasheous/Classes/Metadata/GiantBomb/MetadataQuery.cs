using System.Data;
using System.Text;
using Classes;
using GiantBomb.Models;

namespace GiantBomb
{
    public class MetadataQuery
    {
        public enum QueryableTypes
        {
            game,
            games,
            platform,
            platforms,
            image,
            rating,
            release,
            user_review
        }

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

        // public static GiantBombGenericResponse SearchForMetadata<T>(string? filter, string? sort, int limit = 100, int offset = 0)
        // {
        //     GiantBombGenericResponse response = new GiantBombGenericResponse();

        //     // Parse the filter parameter
        //     // filters are comma separated lists of key value pairs separated by a colon
        //     // keys are the names of fields in the database
        //     // values are the values to filter by
        //     // Single filter: field:value
        //     // Multiple filters: field:value,field:value - the same field can be used multiple times as an OR filter
        //     // Date filters: field:start value|end value (using datetime format)
        //     List<string> filters = new List<string>();
        //     Dictionary<string, string> filterDict = new Dictionary<string, string>();

        //     if (!string.IsNullOrEmpty(filter))
        //     {
        //         string[] filterParts = filter.Split(',');
        //         foreach (string part in filterParts)
        //         {
        //             string[] keyValue = part.Split(':');
        //             if (keyValue.Length == 2)
        //             {
        //                 filters.Add($"`{keyValue[0]}` = '{keyValue[1]}'");
        //                 filterDict[keyValue[0]] = keyValue[1];
        //             }
        //             else if (keyValue.Length == 3 && keyValue[1].Contains('|'))
        //             {
        //                 // Handle date range
        //                 string[] dateRange = keyValue[1].Split('|');
        //                 if (dateRange.Length == 2)
        //                 {
        //                     filters.Add($"{keyValue[0]} BETWEEN '{dateRange[0]}' AND '{dateRange[1]}'");
        //                 }
        //             }
        //         }
        //     }

        //     // Parse the sort parameter
        //     // sort is a comma separated list of fields to sort by, with an optional direction (asc/desc)
        //     // Example: field1:asc,field2:desc
        //     List<string> sortFields = new List<string>();
        //     if (!string.IsNullOrEmpty(sort))
        //     {
        //         string[] sortParts = sort.Split(',');
        //         foreach (string part in sortParts)
        //         {
        //             string[] sortKeyValue = part.Split(':');
        //             if (sortKeyValue.Length == 2)
        //             {
        //                 string field = sortKeyValue[0];
        //                 string direction = sortKeyValue[1].ToLower() == "desc" ? "DESC" : "ASC";
        //                 sortFields.Add($"`{field}` {direction}");
        //             }
        //             else if (sortKeyValue.Length == 1)
        //             {
        //                 // Default to ascending if no direction is specified
        //                 sortFields.Add($"`{sortKeyValue[0]}` ASC");
        //             }
        //         }
        //     }

        //     // Build the SQL query
        //     StringBuilder sql = new StringBuilder("SELECT * FROM `giantbomb`.");
        //     sql.Append("`");
        //     sql.Append(typeof(T).Name);
        //     sql.Append("`");
        //     sql.Append(" WHERE ");
        //     if (filters.Count > 0)
        //     {
        //         sql.Append(string.Join(" AND ", filters));
        //     }
        //     else
        //     {
        //         sql.Append("1 = 1"); // No filters, select all
        //     }
        //     if (sortFields.Count > 0)
        //     {
        //         sql.Append(" ORDER BY ");
        //         sql.Append(string.Join(", ", sortFields));
        //     }
        //     sql.Append(" LIMIT @limit OFFSET @offset;");
        //     var parameters = new Dictionary<string, object>
        //     {
        //         { "@limit", limit },
        //         { "@offset", offset }
        //     };

        //     string sqlString = sql.ToString();

        //     Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
        //     var result = db.ExecuteCMD(sqlString, parameters);
        //     List<T> metadataList = new List<T>();

        //     foreach (DataRow row in result.Rows)
        //     {
        //         T metadata = Activator.CreateInstance<T>();
        //         foreach (var prop in typeof(T).GetProperties())
        //         {
        //             if (row.Table.Columns.Contains(prop.Name) && prop.CanWrite)
        //             {
        //                 object value = row[prop.Name];
        //                 if (value != DBNull.Value)
        //                 {
        //                     // If property is a class (not string), treat as subclass
        //                     if (value != null && prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
        //                     {
        //                         // check if the property type is a collection or array
        //                         if (prop.PropertyType.IsGenericType && prop.PropertyType.GetInterfaces().Any(i =>
        //                 i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) && prop.PropertyType != typeof(string))
        //                         {
        //                             // check if subclassess in the collection have an Id property
        //                             var subIdProp = prop.PropertyType.GetGenericArguments()[0].GetProperty("id") ?? prop.PropertyType.GetGenericArguments()[0].GetProperty("Id");
        //                             if (subIdProp == null)
        //                             {
        //                                 // deserialise the value as a JSON array into the property type
        //                                 var jsonValue = value.ToString();
        //                                 if (!string.IsNullOrEmpty(jsonValue))
        //                                 {
        //                                     var deserializedValue = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonValue, prop.PropertyType);
        //                                     prop.SetValue(metadata, deserializedValue);
        //                                 }
        //                             }
        //                             else
        //                             {
        //                                 // value is a JSON array of ids, deserialise to a list of longs, and then populate the property with a list of objects
        //                                 var jsonValue = value.ToString();
        //                                 if (!string.IsNullOrEmpty(jsonValue))
        //                                 {
        //                                     var ids = Newtonsoft.Json.JsonConvert.DeserializeObject<List<long>>(jsonValue);
        //                                     var listType = typeof(List<>).MakeGenericType(prop.PropertyType.GetGenericArguments()[0]);
        //                                     var listInstance = Activator.CreateInstance(listType);
        //                                     foreach (var id in ids)
        //                                     {
        //                                         // Assuming you have a method to get the object by ID
        //                                         var item = db.GetById(prop.PropertyType.GetGenericArguments()[0], id);
        //                                         if (item != null)
        //                                         {
        //                                             listType.GetMethod("Add")?.Invoke(listInstance, new[] { item });
        //                                         }
        //                                     }
        //                                     prop.SetValue(metadata, listInstance);
        //                                 }
        //                             }










        //                     prop.SetValue(metadata, Convert.ChangeType(value, prop.PropertyType));
        //                 }
        //             }
        //         }
        //         metadataList.Add(metadata);
        //     }

        //     // compile the response
        //     response.results = metadataList.Cast<object>().ToList();
        //     response.error = "OK";
        //     response.limit = result.Rows.Count < limit ? result.Rows.Count : limit;
        //     response.offset = offset;
        //     response.version = "1.0";
        //     response.status_code = 1;

        //     return response;
        // }
    }
}