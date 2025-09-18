using System.Data;
using System.Text;
using Classes;
using GiantBomb.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GiantBomb
{
    public class MetadataQuery
    {
        public enum QueryableTypes
        {
            company,
            game,
            platform,
            image,
            rating,
            rating_board,
            release,
            user_review
        }

        private class GiantBombSourceMapItem
        {
            public QueryableTypes Type { get; set; }
            public string TableName { get; set; }
            public Type? ClassType { get; set; }
        }

        private static readonly Dictionary<QueryableTypes, GiantBombSourceMapItem> GiantBombSourceMap = new()
        {
            { QueryableTypes.company, new GiantBombSourceMapItem {
                Type = QueryableTypes.company,
                TableName = "Company",
                ClassType = typeof(Company) } },
            { QueryableTypes.game, new GiantBombSourceMapItem {
                Type = QueryableTypes.game,
                TableName = "Game",
                ClassType = typeof(Game) } },
            { QueryableTypes.platform, new GiantBombSourceMapItem {
                Type = QueryableTypes.platform,
                TableName = "Platform",
                ClassType = typeof(Platform) } },
            { QueryableTypes.image, new GiantBombSourceMapItem {
                Type = QueryableTypes.image,
                TableName = "Image",
                ClassType = typeof(Image) } },
            { QueryableTypes.rating, new GiantBombSourceMapItem {
                Type = QueryableTypes.rating,
                TableName = "Rating",
                ClassType = typeof(Rating) } },
            { QueryableTypes.rating_board, new GiantBombSourceMapItem {
                Type = QueryableTypes.rating_board,
                TableName = "RatingBoards",
                ClassType = typeof(RatingBoards) } },
            { QueryableTypes.release, new GiantBombSourceMapItem {
                Type = QueryableTypes.release,
                TableName = "Release",
                ClassType = typeof(Release) } },
            { QueryableTypes.user_review, new GiantBombSourceMapItem {
                Type = QueryableTypes.user_review,
                TableName = "UserReview",
                ClassType = typeof(UserReview) } }
        };

        public enum GiantBombReturnTypes
        {
            xml,
            json,
            jsonp
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

        /// <summary>
        /// Get metadata by GUID
        /// </summary>
        /// <param name="dataType">
        /// The type of data to retrieve (e.g., game, platform, image, rating, release, user_review).
        /// </param>
        /// <param name="guid">
        /// The unique identifier (GUID) of the specific item to retrieve.
        /// </param>
        /// <param name="fieldList">
        /// A comma-separated list of fields to include in the response. Use "*" to include all fields.
        /// </param>
        /// <returns>
        /// A GiantBombGenericResponse object containing the requested metadata.
        /// </returns>
        public static GiantBombGenericResponse GetMetadataByGuid(QueryableTypes dataType, string guid, string fieldList)
        {
            GiantBombSourceMapItem? sourceMapItem = GiantBombSourceMap.GetValueOrDefault(dataType);

            if (sourceMapItem == null || sourceMapItem.ClassType == null)
            {
                throw new ArgumentException("Invalid data type specified.");
            }

            if (fieldList == "*")
            {
                // Get all properties of the class type
                fieldList = string.Join(",", sourceMapItem.ClassType.GetProperties().Select(p => p.Name));
            }
            else
            {
                // Validate the provided fields against the class properties
                var validFields = new HashSet<string>(sourceMapItem.ClassType.GetProperties().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                var requestedFields = fieldList.Split(',').Select(f => f.Trim()).ToList();

                foreach (var field in requestedFields)
                {
                    if (!validFields.Contains(field))
                    {
                        throw new ArgumentException($"Invalid field '{field}' specified for type '{dataType}'.");
                    }
                }

                fieldList = string.Join(",", requestedFields);
            }

            // Build the SQL query
            StringBuilder sql = new StringBuilder("SELECT ");
            sql.Append(fieldList);
            sql.Append(" FROM `giantbomb`.`");
            sql.Append(sourceMapItem.TableName);
            sql.Append("`");
            sql.Append(" WHERE `guid` = @guid;");
            var parameters = new Dictionary<string, object>
            {
                { "@guid", guid }
            };
            string sqlString = sql.ToString();
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            var result = db.ExecuteCMD(sqlString, parameters);
            GiantBombGenericResponse response = new GiantBombGenericResponse();

            if (result.Rows.Count > 0)
            {
                // Invoke generic BuildResponseFromDataTable<T>() with runtime type sourceMapItem.ClassType
                var mq = new MetadataQuery();
                var buildMethod = typeof(MetadataQuery)
                    .GetMethod("BuildResponseFromDataTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (buildMethod == null)
                    throw new InvalidOperationException("BuildResponseFromDataTable method not found.");

                var genericBuild = buildMethod.MakeGenericMethod(sourceMapItem.ClassType);
                response = (GiantBombGenericResponse)genericBuild.Invoke(mq, new object[] { result, 0 });
            }
            else
            {
                response.error = "Item not found";
                response.limit = 0;
                response.offset = 0;
                response.version = "1.0";
                response.status_code = 404;
                response.results = new List<object>();
            }

            return response;
        }

        public static GiantBombGenericResponse SearchForMetadata(QueryableTypes dataType, string? filter, string fieldList, string? sort, int limit = 100, int offset = 0)
        {
            GiantBombSourceMapItem? sourceMapItem = GiantBombSourceMap.GetValueOrDefault(dataType);

            if (sourceMapItem == null || sourceMapItem.ClassType == null)
            {
                throw new ArgumentException("Invalid data type specified.");
            }

            // validate the fieldList
            var validFields = new HashSet<string>(sourceMapItem.ClassType.GetProperties().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            if (fieldList == "*")
            {
                // Get all properties of the class type
                fieldList = string.Join(",", sourceMapItem.ClassType.GetProperties().Select(p => p.Name));
            }
            else
            {
                // Validate the provided fields against the class properties
                var requestedFields = fieldList.Split(',').Select(f => f.Trim()).ToList();

                foreach (var field in requestedFields)
                {
                    if (!validFields.Contains(field))
                    {
                        throw new ArgumentException($"Invalid field '{field}' specified for type '{dataType}'.");
                    }
                }

                fieldList = string.Join(",", requestedFields);
            }

            // validate the filter
            // format should be:
            // Single filter: &filter=field:value
            // Multiple filters: &filter=field:value,field:value
            // Date filters: &filter=field:start value|end value (using datetime format)
            // Field names should be validated against the class properties
            if (!string.IsNullOrEmpty(filter))
            {
                var filterParts = filter.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in filterParts)
                {
                    var fieldValue = part.Split(':', 2);
                    if (fieldValue.Length != 2)
                    {
                        throw new ArgumentException($"Invalid filter format '{part}'. Expected format is 'field:value'.");
                    }
                    var field = fieldValue[0].Trim();
                    if (field == "image_tag")
                    {
                        field = "image_tags"; // special case for image_tags field
                    }
                    if (!validFields.Contains(field))
                    {
                        throw new ArgumentException($"Invalid filter field '{field}' specified for type '{dataType}'.");
                    }
                }
            }

            // validate the sort
            // format should be:
            // Single sort: &sort=field:asc|desc
            if (!string.IsNullOrEmpty(sort))
            {
                var sortParts = sort.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in sortParts)
                {
                    var fieldOrder = part.Split(':', 2);
                    var field = fieldOrder[0].Trim();
                    if (!validFields.Contains(field))
                    {
                        throw new ArgumentException($"Invalid sort field '{field}' specified for type '{dataType}'.");
                    }
                    if (fieldOrder.Length == 2)
                    {
                        var order = fieldOrder[1].Trim().ToLower();
                        if (order != "asc" && order != "desc")
                        {
                            throw new ArgumentException($"Invalid sort order '{order}' specified for field '{field}'. Expected 'asc' or 'desc'.");
                        }
                    }
                }
            }

            // Build the SQL query
            StringBuilder sql = new StringBuilder("SELECT ");
            sql.Append(fieldList);
            sql.Append(" FROM `giantbomb`.`");
            sql.Append(sourceMapItem.TableName);
            sql.Append("`");

            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(filter))
            {
                sql.Append(" WHERE ");
                // Note: Parameters for filter should be added to 'parameters' dictionary as needed.
                var filterItems = filter.Split(',', StringSplitOptions.RemoveEmptyEntries);
                bool first = true;
                foreach (var item in filterItems)
                {
                    if (!first)
                    {
                        sql.Append(" AND ");
                    }
                    first = false;
                    var fieldValue = item.Split(':', 2);
                    var field = fieldValue[0].Trim();
                    if (field == "image_tag")
                    {
                        field = "image_tags"; // special case for image_tags field
                    }
                    var value = fieldValue[1].Trim();
                    // Remove any HTTP URL encoding from the filter value
                    if (!string.IsNullOrEmpty(value))
                    {
                        value = System.Net.WebUtility.UrlDecode(value);
                    }

                    if (value.Contains('|'))
                    {
                        // Date range filter
                        var dateParts = value.Split('|', 2);
                        if (dateParts.Length == 2)
                        {
                            var startDate = dateParts[0].Trim();
                            var endDate = dateParts[1].Trim();
                            sql.Append($"(`{field}` BETWEEN @start_{field} AND @end_{field})");
                            parameters.Add($"@start_{field}", startDate);
                            parameters.Add($"@end_{field}", endDate);
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid date range filter format '{value}' for field '{field}'. Expected format is 'start|end'.");
                        }
                    }
                    else
                    {
                        // Single value filter
                        if (field == "image_tags")
                        {
                            // search is a LIKE match
                            sql.Append($"`{field}` LIKE @filter_{field}");
                            parameters.Add($"@filter_{field}", $"%{value}%");
                        }
                        else
                        {
                            // exact match
                            sql.Append($"`{field}` = @filter_{field}");
                            parameters.Add($"@filter_{field}", value);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(sort))
            {
                sql.Append(" ORDER BY ");
                sql.Append(sort);
            }

            sql.Append(" LIMIT @limit OFFSET @offset;");
            parameters.Add("@limit", limit);
            parameters.Add("@offset", offset);

            string sqlString = sql.ToString();
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            var result = db.ExecuteCMD(sqlString, parameters);

            // Invoke generic BuildResponseFromDataTable<T>() with runtime type sourceMapItem.ClassType
            var mq = new MetadataQuery();
            var buildMethod = typeof(MetadataQuery)
                .GetMethod("BuildResponseFromDataTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (buildMethod == null)
                throw new InvalidOperationException("BuildResponseFromDataTable method not found.");
            var genericBuild = buildMethod.MakeGenericMethod(sourceMapItem.ClassType);
            var response = (GiantBombGenericResponse)genericBuild.Invoke(mq, new object[] { result, offset });
            return response;
        }

        private GiantBombGenericResponse BuildResponseFromDataTable<T>(DataTable table, int offset = 0) where T : new()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            GiantBombGenericResponse response = new GiantBombGenericResponse();
            List<T> metadataList = new List<T>();

            foreach (DataRow row in table.Rows)
            {
                T metadata = new T();

                // Populate the properties of T from the DataRow
                foreach (var prop in typeof(T).GetProperties())
                {
                    if (row.Table.Columns.Contains(prop.Name) && prop.CanWrite)
                    {
                        // property name matches a column in the datatable
                        object value = row[prop.Name];
                        if (value != DBNull.Value)
                        {
                            // If property is a class (not string), treat as subclass
                            if (value != null && prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                            {
                                // check if the property type is a collection or array
                                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) && prop.PropertyType != typeof(string))
                                {
                                    // check if subclassess in the collection have an Id property
                                    var subIdProp = prop.PropertyType.GetGenericArguments()[0].GetProperty("id") ?? prop.PropertyType.GetGenericArguments()[0].GetProperty("Id");
                                    if (subIdProp == null)
                                    {
                                        // deserialise the value as a JSON array into the property type
                                        var jsonValue = value.ToString();
                                        if (!string.IsNullOrEmpty(jsonValue))
                                        {
                                            var deserializedValue = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonValue, prop.PropertyType);
                                            prop.SetValue(metadata, deserializedValue);
                                        }
                                    }
                                    else
                                    {
                                        // value is a JSON array of ids, deserialise to a list of longs, and then populate the property with a list of objects
                                        var jsonValue = value.ToString();
                                        if (!string.IsNullOrEmpty(jsonValue))
                                        {
                                            var ids = Newtonsoft.Json.JsonConvert.DeserializeObject<List<long>>(jsonValue);
                                            var listType = typeof(List<>).MakeGenericType(prop.PropertyType.GetGenericArguments()[0]);
                                            var listInstance = Activator.CreateInstance(listType);
                                            foreach (var id in ids)
                                            {
                                                // lookup table name based on listType in GiantBombSourceMap
                                                var elementType = listType.GetGenericArguments()[0];
                                                var sourceMapItem = GiantBombSourceMap.Values.FirstOrDefault(m => m.ClassType == elementType);
                                                if (sourceMapItem != null)
                                                {
                                                    string sql = $"SELECT `id`, `api_detail_url`, `name` FROM `giantbomb`.`{sourceMapItem.TableName}` WHERE `id` = @id;";
                                                    var parameters = new Dictionary<string, object>
                                                    { { "@id", id } };
                                                    var itemTable = db.ExecuteCMD(sql, parameters);
                                                    if (itemTable.Rows.Count > 0)
                                                    {
                                                        // create an instance of the element type and populate it
                                                        var item = Activator.CreateInstance(elementType);
                                                        foreach (var itemProp in elementType.GetProperties())
                                                        {
                                                            if (itemTable.Columns.Contains(itemProp.Name) && itemProp.CanWrite)
                                                            {
                                                                var itemValue = itemTable.Rows[0][itemProp.Name];
                                                                if (itemValue != DBNull.Value)
                                                                {
                                                                    itemProp.SetValue(item, Convert.ChangeType(itemValue, itemProp.PropertyType));
                                                                }
                                                            }
                                                        }
                                                        if (item != null)
                                                        {
                                                            listType.GetMethod("Add")?.Invoke(listInstance, new[] { item });
                                                        }
                                                    }
                                                }
                                            }
                                            prop.SetValue(metadata, listInstance);
                                        }
                                    }
                                }
                                else
                                {
                                    // Single subclass, database column type is long (the id of the subclass)
                                    // check if the image class is "Image" - nullable or otherwise
                                    if (prop.PropertyType == typeof(Image))
                                    {
                                        // Image class, handle specially
                                        var jsonValue = value.ToString();
                                        if (!string.IsNullOrEmpty(jsonValue))
                                        {
                                            var deserializedValue = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonValue, prop.PropertyType);
                                            prop.SetValue(metadata, deserializedValue);
                                            continue; // skip the id lookup below
                                        }
                                    }
                                    else
                                    {
                                        // value is the id of the subclass, lookup the subclass by id
                                        var id = Convert.ToInt64(value);
                                        var sourceMapItem = GiantBombSourceMap.Values.FirstOrDefault(m => m.ClassType == prop.PropertyType);
                                        if (sourceMapItem != null)
                                        {
                                            string sql = $"SELECT `id`, `api_detail_url`, `name` FROM `giantbomb`.`{sourceMapItem.TableName}` WHERE `id` = @id;";
                                            var parameters = new Dictionary<string, object>
                                        { { "@id", id } };
                                            var itemTable = db.ExecuteCMD(sql, parameters);
                                            if (itemTable.Rows.Count > 0)
                                            {
                                                // create an instance of the property type and populate it
                                                var item = Activator.CreateInstance(prop.PropertyType);
                                                foreach (var itemProp in prop.PropertyType.GetProperties())
                                                {
                                                    if (itemTable.Columns.Contains(itemProp.Name) && itemProp.CanWrite)
                                                    {
                                                        var itemValue = itemTable.Rows[0][itemProp.Name];
                                                        if (itemValue != DBNull.Value)
                                                        {
                                                            itemProp.SetValue(item, Convert.ChangeType(itemValue, itemProp.PropertyType));
                                                        }
                                                    }
                                                }
                                                prop.SetValue(metadata, item);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Simple property, set the value directly
                                {
                                    var targetType = prop.PropertyType;

                                    // Handle Nullable<T>
                                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                                    {
                                        targetType = Nullable.GetUnderlyingType(targetType)!;
                                    }

                                    try
                                    {
                                        if (targetType.IsEnum)
                                        {
                                            var enumValue = value is string s
                                                ? Enum.Parse(targetType, s, true)
                                                : Enum.ToObject(targetType, value);
                                            prop.SetValue(metadata, enumValue);
                                        }
                                        else if (targetType.IsInstanceOfType(value))
                                        {
                                            prop.SetValue(metadata, value);
                                        }
                                        else
                                        {
                                            prop.SetValue(metadata, Convert.ChangeType(value, targetType));
                                        }
                                    }
                                    catch
                                    {
                                        // Swallow conversion issues silently (keep existing behavior style)
                                    }
                                }
                            }
                        }
                    }
                }
                metadataList.Add(metadata);
            }
            // compile the response
            response.results = metadataList.Cast<object>().ToList();
            response.error = "OK";
            response.limit = table.Rows.Count;
            response.offset = offset;
            response.version = "1.0";
            response.status_code = 1;
            return response;
        }
    }
}