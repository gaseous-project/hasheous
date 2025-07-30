using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Metadata
    {
        const string fieldList = "fields *;";

        public Metadata()
        {
        }

        public static async Task<T?> GetMetadata<T>(long? Id) where T : new()
        {
            if ((Id == 0) || (Id == null))
            {
                return default;
            }
            else
            {
                return await _GetMetadata<T>(SearchUsing.id, Id);
            }
        }

        public static async Task<T?> GetMetadata<T>(string Slug) where T : new()
        {
            // process slug to remove any leading or trailing whitespace
            Slug = Slug.Trim();

            if (string.IsNullOrEmpty(Slug))
            {
                return default;
            }

            // check if the type supports slug search
            EndpointDataItem endpointData = GetEndpointData<T>();
            if (!endpointData.SupportsSlugSearch)
            {
                throw new Exception("The type " + typeof(T).Name + " does not support slug search.");
            }

            // check the slug for invalid characters
            if (Slug.Contains("\"") || Slug.Contains("'") || Slug.Contains(";"))
            {
                throw new Exception("The slug contains invalid characters.");
            }

            // get metadata using slug
            Slug = Slug.ToLowerInvariant();

            return await _GetMetadata<T>(SearchUsing.slug, Slug);
        }

        private static async Task<T?> _GetMetadata<T>(SearchUsing searchUsing, object searchValue) where T : new()
        {
            // get the type name of T
            string typeName = typeof(T).Name;

            // set up where clause
            string WhereClause = "";
            string searchField = "";
            switch (searchUsing)
            {
                case SearchUsing.id:
                    WhereClause = "where id = " + searchValue;
                    searchField = "id";
                    break;
                case SearchUsing.slug:
                    WhereClause = "where slug = \"" + searchValue + "\"";
                    searchField = "slug";
                    break;
                default:
                    throw new Exception("Invalid search type");
            }

            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                string endpoint = GetEndpointData<T>().Endpoint;

                return await GetObjectFromDatabase<T>(endpoint, fieldList, WhereClause);
            }

            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, typeName, (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, typeName, (string)searchValue);
            }

            T returnValue = new T();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer<T>(WhereClause);
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
                    break;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer<T>(WhereClause);
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = await Storage.GetCacheValueAsync<T>(returnValue, Storage.TablePrefix.IGDB, searchField, searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = await Storage.GetCacheValueAsync<T>(returnValue, Storage.TablePrefix.IGDB, searchField, searchValue);
                    break;
                default:
                    throw new Exception("How did you get here?");
            }

            return returnValue;
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<T> GetObjectFromServer<T>(string WhereClause)
        {
            Communications comms = new Communications(Communications.MetadataSources.IGDB);

            string endpoint = GetEndpointData<T>().Endpoint;

            var results = await comms.APIComm<T>(endpoint, fieldList, WhereClause);
            var result = results.FirstOrDefault();

            return result;
        }

        public static async Task<T> GetObjectFromDatabase<T>(string Endpoint, string Fields, string Query)
        {
            SQLQuery? sqlQuery = GenerateSQLQuery(Endpoint, Fields, Query, true);
            if (sqlQuery == null)
            {
                throw new Exception("Invalid query: " + Query);
            }

            // set the SQL query and dictionary for the database
            string sql = sqlQuery.sql;
            Dictionary<string, object> dbDict = sqlQuery.dbDict;

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            DataTable data = await db.ExecuteCMDAsync(sql, dbDict);

            if (data.Rows.Count == 0)
            {
                // no results found
                Logging.Log(Logging.LogType.Information, "API Connection", "No results found for IGDB dump API query: " + Query);
                return default(T);
            }

            // convert the DataTable to an array of T
            T[]? results = ConvertDataTableToObjectArray<T>(data);

            return results.FirstOrDefault();
        }

        public static async Task<T[]> GetObjectsFromDatabase<T>(string Endpoint, string Fields, string Query)
        {
            SQLQuery? sqlQuery = GenerateSQLQuery(Endpoint, Fields, Query, false);
            if (sqlQuery == null)
            {
                throw new Exception("Invalid query: " + Query);
            }

            // set the SQL query and dictionary for the database
            string sql = sqlQuery.sql;
            Dictionary<string, object> dbDict = sqlQuery.dbDict;

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            DataTable data = await db.ExecuteCMDAsync(sql, dbDict);

            if (data.Rows.Count == 0)
            {
                // no results found
                Logging.Log(Logging.LogType.Information, "API Connection", "No results found for IGDB dump API query: " + Query);
                return default(T[]);
            }

            // convert the DataTable to an array of T
            T[]? results = ConvertDataTableToObjectArray<T>(data);

            return results;
        }

        private static SQLQuery? GenerateSQLQuery(string Endpoint, string Fields, string Query, bool limit = false)
        {
            SQLQuery sqlQuery = new SQLQuery();

            // split the query into separate conditions - "and" = "&", "or" = "|"
            // this will allow us to handle multiple conditions in the query
            // we will split by "&" and "|" to get the individual conditions preserving the "and" and "or" operators
            List<SQLCondition> conditions = new List<SQLCondition>();

            // split by unquoted ";"
            string[] queryParts = Query.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            // will hold any user defined limit clause
            string limitClause = string.Empty;

            foreach (var part in queryParts)
            {
                // trim the part to remove leading and trailing whitespace
                string trimmedPart = part.Trim();

                // check for keywords like "where" or "search"
                if (trimmedPart.StartsWith("where", StringComparison.OrdinalIgnoreCase))
                {
                    // "where" clause
                    // remove "where" from the beginning of the query if it exists
                    if (trimmedPart.StartsWith("where", StringComparison.OrdinalIgnoreCase))
                    {
                        trimmedPart = trimmedPart.Substring(5).Trim();
                    }

                    // parse the query into conditions
                    bool insideQuotes = false;
                    string currentCondition = string.Empty;
                    foreach (var character in trimmedPart)
                    {
                        if (character == '"' || character == '\'')
                        {
                            insideQuotes = !insideQuotes; // toggle the insideQuotes flag
                        }

                        if (!insideQuotes && (character == '&' || character == '|'))
                        {
                            // we have reached a condition separator, so we need to split the query
                            if (!string.IsNullOrEmpty(currentCondition))
                            {
                                // parse the condition into a SQLCondition object
                                SQLCondition sqlCondition = new SQLCondition(currentCondition);

                                // determine the type of condition based on the separator
                                if (character == '&')
                                {
                                    sqlCondition.Type = SQLCondition.ConditionType.And;
                                }
                                else if (character == '|')
                                {
                                    sqlCondition.Type = SQLCondition.ConditionType.Or;
                                }

                                // add the condition to the list
                                conditions.Add(sqlCondition);
                            }

                            currentCondition = string.Empty; // reset the current condition
                        }
                        else
                        {
                            // add the character to the current condition
                            currentCondition += character;
                        }
                    }

                    // if there is a remaining condition, add it to the list
                    if (!string.IsNullOrEmpty(currentCondition))
                    {
                        SQLCondition sqlCondition = new SQLCondition(currentCondition);
                        conditions.Add(sqlCondition);
                    }
                }
                else if (trimmedPart.StartsWith("search", StringComparison.OrdinalIgnoreCase))
                {
                    // "search" clause
                    // remove "search" from the beginning of the query if it exists
                    if (trimmedPart.StartsWith("search", StringComparison.OrdinalIgnoreCase))
                    {
                        trimmedPart = part.Substring(6).Trim();
                    }

                    // search queries are case insensitive full text searches of the name field
                    // check if the name field is present in the fields list
                    if (!Fields.Contains("name", StringComparison.OrdinalIgnoreCase) && !Fields.Contains("*"))
                    {
                        throw new Exception("Search queries require the 'name' field to be present in the fields list.");
                    }

                    // we should only have the value of the search left, so we can use it directly
                    string searchValue = trimmedPart.Trim();

                    // create a new SQLCondition for the search query
                    SQLCondition searchCondition = new SQLCondition(searchValue, true);
                    searchCondition.IsSearch = true; // set the IsSearch flag to true
                    conditions.Add(searchCondition);
                }
                else if (trimmedPart.StartsWith("limit", StringComparison.OrdinalIgnoreCase))
                {
                    // "limit" clause
                    // remove "limit" from the beginning of the query if it exists
                    if (trimmedPart.StartsWith("limit", StringComparison.OrdinalIgnoreCase))
                    {
                        trimmedPart = trimmedPart.Substring(5).Trim();
                    }

                    // check if the limit clause is valid
                    if (int.TryParse(trimmedPart, out int limitValue) && limitValue > 0)
                    {
                        limitClause = " LIMIT " + limitValue; // set the limit clause
                    }
                    else
                    {
                        throw new Exception("Invalid limit value: " + trimmedPart);
                    }
                }
            }

            // now we have a list of conditions, we can build the SQL query
            StringBuilder sqlBuilder = new StringBuilder();
            sqlBuilder.Append("SELECT ");
            sqlBuilder.Append(Fields.Replace("fields ", string.Empty).Replace(";", string.Empty)); // remove "fields " from the beginning of the fields string
            sqlBuilder.Append(" FROM ");
            sqlBuilder.Append("`igdb`."); // prefix for IGDB tables
            sqlBuilder.Append($"`{Endpoint}`");

            if (conditions.Count > 0)
            {
                sqlBuilder.Append(" WHERE ");
                for (int i = 0; i < conditions.Count; i++)
                {
                    SQLCondition condition = conditions[i];

                    // if the condition is a search query, we need to handle it differently
                    if (condition.IsSearch)
                    {
                        // for search queries, we use the MATCH operator on the name field
                        sqlBuilder.Append("MATCH(name) AGAINST(@searchValue IN NATURAL LANGUAGE MODE)");
                        sqlQuery.dbDict.Add("searchValue", condition.Value); // add the search value to the dictionary
                    }
                    else
                    {
                        // for normal conditions, we need to add the column name, operator, and value
                        // add the condition to the SQL query
                        if (condition.Operator != "IN")
                        {
                            sqlBuilder.Append(condition.ColumnName);
                            sqlBuilder.Append(" ");
                            sqlBuilder.Append(condition.Operator);
                        }

                        if (condition.Operator == "LIKE" || condition.Operator == "NOT LIKE")
                        {
                            sqlBuilder.Append(" CONCAT("); // add quotes for LIKE and NOT LIKE operators
                                                           // if the operator is LIKE or NOT LIKE, we need to add wildcards for the value
                            if (condition.StartsWithWildcard)
                            {
                                sqlBuilder.Append("'%',");
                            }
                            else
                            {
                                sqlBuilder.Append("");
                            }
                            sqlBuilder.Append("@");
                            sqlBuilder.Append(condition.ColumnName.Replace(".", "_")); // replace dots with underscores for parameter names
                            if (condition.EndsWithWildcard)
                            {
                                sqlBuilder.Append(",'%'"); // add the trailing wildcard
                            }
                            else
                            {
                                sqlBuilder.Append(""); // just the value without wildcards
                            }
                            sqlBuilder.Append(")");
                        }
                        else if (condition.Operator == "IN")
                        {
                            // if the operator is IN, this will be a json array, so we need to handle it differently
                            // loop for all values in the array
                            if (condition.Value is string[] arrayValues)
                            {
                                // remove the brackets and split by comma
                                sqlBuilder.Append(" (");
                                for (int j = 0; j < arrayValues.Length; j++)
                                {
                                    string arrayValue = arrayValues[j].Trim();
                                    sqlBuilder.Append("JSON_CONTAINS(");
                                    sqlBuilder.Append($"`{condition.ColumnName.Replace(".", "_")}`"); // replace dots with underscores for parameter names
                                    sqlBuilder.Append(", @");
                                    sqlBuilder.Append(condition.ColumnName.Replace(".", "_") + j);
                                    sqlBuilder.Append(", '$')"); // use JSON_CONTAINS to check if the value is in the array
                                    sqlQuery.dbDict.Add(condition.ColumnName.Replace(".", "_") + j, arrayValue); // add the value to the dictionary with a unique key
                                    if (j < arrayValues.Length - 1)
                                    {
                                        sqlBuilder.Append(" OR ");
                                    }
                                }
                                sqlBuilder.Append(")"); // close the IN clause
                            }
                        }
                        else
                        {
                            // for other operators, we just add the value as a parameter
                            sqlBuilder.Append(" @");
                            sqlBuilder.Append(condition.ColumnName.Replace(".", "_")); // replace dots with underscores for parameter names
                        }
                    }

                    // add the value to the dictionary
                    sqlQuery.dbDict.Add(condition.ColumnName.Replace(".", "_"), condition.Value);

                    // if this is not the last condition, add the type of condition
                    if (i < conditions.Count - 1)
                    {
                        if (condition.Type == SQLCondition.ConditionType.And)
                        {
                            sqlBuilder.Append(" AND ");
                        }
                        else if (condition.Type == SQLCondition.ConditionType.Or)
                        {
                            sqlBuilder.Append(" OR ");
                        }
                    }
                }
            }

            // if limit is true, add a limit clause to the query
            if (!string.IsNullOrEmpty(limitClause))
            {
                sqlBuilder.Append(limitClause);
            }
            else
            {
                if (limit)
                {
                    sqlBuilder.Append(" LIMIT 1");
                }
            }

            // set the SQL query
            sqlQuery.sql = sqlBuilder.ToString();

            // if the SQL query is empty, return null
            if (string.IsNullOrEmpty(sqlQuery.sql))
            {
                return null;
            }

            // return the SQL query object
            return sqlQuery;
        }

        private class SQLQuery
        {
            public string sql { get; set; }
            public Dictionary<string, object> dbDict { get; set; } = new Dictionary<string, object>();
        }

        private class SQLCondition
        {
            public SQLCondition()
            {
                Type = ConditionType.And; // default to And
            }

            public SQLCondition(string condition, bool IsSearch = false)
            {
                _condition = condition;

                if (IsSearch)
                {
                    // this is a full text search query, so we will not parse it like a normal condition
                    Value = _condition.Trim().Trim('\'', '"'); // remove quotes and whitespace
                    ColumnName = "name"; // full text search queries are always on the name field
                    Operator = "MATCH"; // use MATCH operator for full text search
                    IsSearch = true; // set the IsSearch flag to true
                    Type = ConditionType.And; // default to And for search queries
                }
                else
                {
                    // this is a normal condition, so we will parse it

                    // parse the condition to get the column name, operator, and value
                    // split on spaces, but handle quoted values
                    string[] parts = _condition.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 3)
                    {
                        throw new Exception("Invalid condition format: " + _condition);
                    }

                    // the first part is the column name
                    ColumnName = parts[0];

                    // the second part is the operator
                    Operator = parts[1];
                    // = is for case sensitive equality
                    // ~ is for case insensitive equality
                    // MariaDB doesn't support ~ as an operator, so we will convert it to =
                    if (Operator == "~")
                    {
                        Operator = "=";
                    }
                    // if the operator is not one of the supported operators, throw an exception
                    string[] supportedOperators = { "=", ">", "<", ">=", "<=", "!=", "LIKE", "NOT LIKE" };
                    if (!supportedOperators.Contains(Operator))
                    {
                        throw new Exception("Unsupported operator: " + Operator);
                    }

                    // the rest is the value, which can be a single value or a quoted string
                    Value = string.Join(" ", parts.Skip(2)).Trim();

                    // if value begins with an * and/or ends with an *, we need to remove the * and/or add wildcards for LIKE queries
                    if (Value is string valueStr)
                    {
                        valueStr = valueStr.Trim(';', ' '); // remove semicolons, and whitespace

                        if (valueStr.StartsWith("*"))
                        {
                            StartsWithWildcard = true;
                            Operator = "LIKE"; // change operator to LIKE for wildcard support
                            valueStr = valueStr.Substring(1); // remove the leading *
                        }
                        if (valueStr.EndsWith("*"))
                        {
                            EndsWithWildcard = true;
                            Operator = "LIKE"; // change operator to LIKE for wildcard support
                            valueStr = valueStr.Substring(0, valueStr.Length - 1); // remove the trailing *
                        }

                        Value = valueStr; // set the value to the trimmed string
                    }
                    // if the value is a string, we need to remove the quotes
                    if (Value is string valueString)
                    {
                        if (valueString.StartsWith("\"") && valueString.EndsWith("\""))
                        {
                            valueString = valueString.Substring(1, valueString.Length - 2); // remove the quotes
                        }
                        else if (valueString.StartsWith("'") && valueString.EndsWith("'"))
                        {
                            valueString = valueString.Substring(1, valueString.Length - 2); // remove the quotes
                        }

                        // set the value back to the string
                        Value = valueString;
                    }

                    // if the value is a number, we need to convert it to a number
                    if (Value is string valueStrNum && double.TryParse(valueStrNum, out double numericValue))
                    {
                        Value = numericValue; // set the value to the numeric value
                    }

                    // if the value is a boolean, we need to convert it to a boolean
                    if (Value is string valueStrBool && bool.TryParse(valueStrBool, out bool boolValue))
                    {
                        Value = boolValue; // set the value to the boolean value
                    }

                    // if the value is an array, we need to convert it to an array
                    if (Value is string valueStrArray && (valueStrArray.StartsWith("[") && valueStrArray.EndsWith("]") || valueStrArray.StartsWith("(") && valueStrArray.EndsWith(")")))
                    {
                        // remove the brackets and split by comma
                        string[] arrayValues = valueStrArray.Substring(1, valueStrArray.Length - 2).Split(',');
                        Value = arrayValues.Select(v => v.Trim()).ToArray(); // set the value to the array of strings
                        Operator = "IN"; // change operator to IN for array support
                    }

                    // if the value is a long, we need to convert it to a long
                    if (Value is string valueStrLong && long.TryParse(valueStrLong, out long longValue))
                    {
                        Value = longValue; // set the value to the long value
                    }

                    // if the value is an int, we need to convert it to an int
                    if (Value is string valueStrInt && int.TryParse(valueStrInt, out int intValue))
                    {
                        Value = intValue; // set the value to the int value
                    }

                    // if the value is a DateTime, we need to convert it to a DateTime
                    if (Value is string valueStrDate && DateTime.TryParse(valueStrDate, out DateTime dateValue))
                    {
                        Value = dateValue; // set the value to the DateTime value
                    }

                    // if the value is a DateTimeOffset, we need to convert it to a DateTimeOffset
                    if (Value is string valueStrDateOffset && DateTimeOffset.TryParse(valueStrDateOffset, out DateTimeOffset dateOffsetValue))
                    {
                        Value = dateOffsetValue; // set the value to the DateTimeOffset value
                    }
                }
            }

            private string _condition = string.Empty;

            public string ColumnName { get; set; }
            public string Operator { get; set; }
            public object Value { get; set; }
            public enum ConditionType
            {
                And,
                Or
            }
            public ConditionType? Type { get; set; }
            public bool StartsWithWildcard { get; set; } = false;
            public bool EndsWithWildcard { get; set; } = false;
            public bool IsSearch { get; set; } = false;
        }

        private static T[]? ConvertDataTableToObjectArray<T>(DataTable dataTable)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return null;
            }

            // get the name of T
            string typeName = typeof(T).Name;

            // if T is not in the namespace of HasheousClient.Models.Metadata.IGDB, we need to load a type with same name from the HasheousClient.Models.Metadata.IGDB namespace to use as a lookup for field names
            Type? compareType = typeof(T);
            if (!compareType.Namespace.StartsWith("HasheousClient.Models.Metadata.IGDB"))
            {
                // try to find the type in the HasheousClient.Models.Metadata.IGDB namespace
                var hasheousAssembly = typeof(HasheousClient.Models.Metadata.IGDB.Game).Assembly;
                compareType = hasheousAssembly.GetType("HasheousClient.Models.Metadata.IGDB." + typeName);
                if (compareType == null)
                {
                    // nothing to compare against, so we will use the type T directly
                    compareType = typeof(T);
                }
            }

            // convert the DataTable to an array of T - each row is an instance of T, and each column is a property of T
            T[] results = new T[dataTable.Rows.Count];
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                // create a new instance of T
                T item = Activator.CreateInstance<T>();

                // get properties of T
                foreach (var property in typeof(T).GetProperties())
                {
                    // get the name of the property
                    string fieldName = property.Name;

                    // if the property is in the compareType, get the field name from the JSONPropertyName attribute if it exists
                    if (compareType.GetProperty(fieldName) != null)
                    {
                        var jsonPropertyAttribute = compareType.GetProperty(fieldName)?.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), false).FirstOrDefault() as Newtonsoft.Json.JsonPropertyAttribute;
                        if (jsonPropertyAttribute != null)
                        {
                            fieldName = jsonPropertyAttribute.PropertyName;
                        }
                    }

                    // check if the DataTable has a column with this name
                    if (dataTable.Columns.Contains(fieldName))
                    {
                        // get the value from the DataTable
                        object? value = dataTable.Rows[i][fieldName];

                        // if the value is not null or DBNull, set the property value
                        if (value != DBNull.Value && value != null)
                        {
                            // if the property type is IdentityOrValue, convert the value to the appropriate type
                            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IdentityOrValue<>))
                            {
                                long deserializedId = value is long ? (long)value : Convert.ToInt64(value);

                                // create the appropriate IdentityOrValue type, and set the value of IdentityOrValue.Id from deserializedId
                                // the id can only be set as part of the type constructor, so we need to create a new instance of the type with the id
                                Type genericType = property.PropertyType.GetGenericArguments()[0];
                                var identityOrValueInstance = Activator.CreateInstance(property.PropertyType, deserializedId);
                                if (identityOrValueInstance != null)
                                {
                                    property.SetValue(item, identityOrValueInstance);
                                }
                            }
                            else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IdentitiesOrValues<>))
                            {
                                long[] deserializedIds = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(value.ToString() ?? "[]");

                                // create the appropriate IdentitiesOrValues type, and set the value of IdentitiesOrValues.Ids from deserializedIds
                                // the ids can only be set as part of the type constructor, so we need to create a new instance of the type with the ids
                                Type genericType = property.PropertyType.GetGenericArguments()[0];
                                var identitiesOrValuesInstance = Activator.CreateInstance(property.PropertyType, deserializedIds);
                                if (identitiesOrValuesInstance != null)
                                {
                                    property.SetValue(item, identitiesOrValuesInstance);
                                }
                            }
                            else if (property.PropertyType == typeof(DateTimeOffset?) || property.PropertyType == typeof(DateTimeOffset))
                            {
                                // database stores DateTimeOffset as DateTime, so we need to convert it
                                if (value is DateTime dateTimeValue)
                                {
                                    // create a new DateTimeOffset with the same value
                                    DateTimeOffset dateTimeOffsetValue = new DateTimeOffset(dateTimeValue);
                                    property.SetValue(item, (DateTimeOffset?)dateTimeOffsetValue);
                                }
                                else if (value is DateTimeOffset dateTimeOffset)
                                {
                                    property.SetValue(item, (DateTimeOffset?)dateTimeOffset);
                                }
                                else if (value == null || value == DBNull.Value)
                                {
                                    property.SetValue(item, null);
                                }
                            }
                            else if (property.PropertyType == typeof(Int32[]))
                            {
                                // if the property is an array of integers, we need to convert the value to an array of integers
                                if (value is string strValue)
                                {
                                    // try to deserialize the string as an array of integers
                                    int[] intArray = Newtonsoft.Json.JsonConvert.DeserializeObject<int[]>(strValue);
                                    property.SetValue(item, intArray);
                                }
                                else if (value is int[] intArray)
                                {
                                    property.SetValue(item, intArray);
                                }
                            }
                            else if (property.PropertyType == typeof(List<Int32>))
                            {
                                // if the property is a list of integers, we need to convert the value to a list of integers
                                if (value is string strValue)
                                {
                                    // try to deserialize the string as a list of integers
                                    List<int> intList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<int>>(strValue);
                                    property.SetValue(item, intList);
                                }
                                else if (value is List<int> intList)
                                {
                                    property.SetValue(item, intList);
                                }
                            }
                            else if (property.PropertyType == typeof(Int64[]))
                            {
                                // if the property is an array of long integers, we need to convert the value to an array of long integers
                                if (value is string strValue)
                                {
                                    // try to deserialize the string as an array of long integers
                                    long[] longArray = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(strValue);
                                    property.SetValue(item, longArray);
                                }
                                else if (value is long[] longArray)
                                {
                                    property.SetValue(item, longArray);
                                }
                            }
                            else if (property.PropertyType == typeof(List<Int64>))
                            {
                                // if the property is a list of long integers, we need to convert the value to a list of long integers
                                if (value is string strValue)
                                {
                                    // try to deserialize the string as a list of long integers
                                    List<long> longList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<long>>(strValue);
                                    property.SetValue(item, longList);
                                }
                                else if (value is List<long> longList)
                                {
                                    property.SetValue(item, longList);
                                }
                            }
                            else if (property.PropertyType == typeof(double[]))
                            {
                                // if the property is an array of doubles, we need to convert the value to an array of doubles
                                if (value is string strValue)
                                {
                                    // try to deserialize the string as an array of doubles
                                    double[] doubleArray = Newtonsoft.Json.JsonConvert.DeserializeObject<double[]>(strValue);
                                    property.SetValue(item, doubleArray);
                                }
                                else if (value is double[] doubleArray)
                                {
                                    property.SetValue(item, doubleArray);
                                }
                            }
                            else if (property.PropertyType == typeof(List<double>))
                            {
                                // if the property is a list of doubles, we need to convert the value to a list of doubles
                                if (value is string strValue)
                                {
                                    // try to deserialize the string as a list of doubles
                                    List<double> doubleList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<double>>(strValue);
                                    property.SetValue(item, doubleList);
                                }
                                else if (value is List<double> doubleList)
                                {
                                    property.SetValue(item, doubleList);
                                }
                            }
                            else
                            {
                                // set the property value directly
                                property.SetValue(item, value);
                            }
                        }
                    }
                }
                // set the item in the results array
                results[i] = item;
            }

            return results;
        }

        public static readonly Dictionary<string, EndpointDataItem> Endpoints = new Dictionary<string, EndpointDataItem>
        {
            { "AgeRating", new EndpointDataItem {
                Endpoint = "age_ratings",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Organization", new EndpointDataItem.FieldNameItem { TargetType = "AgeRatingOrganization" }},
                    { "RatingCategory", new EndpointDataItem.FieldNameItem { TargetType = "AgeRatingCategory" }},
                    { "RatingContentDescriptions", new EndpointDataItem.FieldNameItem { TargetType = "AgeRatingContentDescriptionV2" }}
                }
            } },
            { "AgeRatingCategory", new EndpointDataItem {
                Endpoint = "age_rating_categories",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Organization", new EndpointDataItem.FieldNameItem { TargetType = "AgeRatingOrganization" } }
                }
            } },
            { "AgeRatingContentDescriptionV2", new EndpointDataItem {
                Endpoint = "age_rating_content_descriptions_v2",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Organization", new EndpointDataItem.FieldNameItem { TargetType = "AgeRatingOrganization" } }
                }
            } },
            { "AgeRatingOrganization", new EndpointDataItem { Endpoint = "age_rating_organizations" } },
            { "AlternativeName", new EndpointDataItem {
                Endpoint = "alternative_names",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "Artwork", new EndpointDataItem {
                Endpoint = "artworks",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "Character", new EndpointDataItem {
                Endpoint = "characters",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "CharacterGender", new EndpointDataItem.FieldNameItem { TargetType = "CharacterGender" } },
                    { "CharacterSpecies", new EndpointDataItem.FieldNameItem { TargetType = "CharacterSpecies" } },
                    { "Games", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Mugshot", new EndpointDataItem.FieldNameItem { TargetType = "CharacterMugshot" } }
                }
            } },
            { "CharacterGender", new EndpointDataItem { Endpoint = "character_genders" } },
            { "CharacterMugshot", new EndpointDataItem { Endpoint = "character_mug_shots" } },
            { "CharacterSpecies", new EndpointDataItem { Endpoint = "character_species" } },
            { "Collection", new EndpointDataItem {
                Endpoint = "collections",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "AsChildRelations", new EndpointDataItem.FieldNameItem { TargetType = "CollectionRelation" } },
                    { "AsParentRelations", new EndpointDataItem.FieldNameItem { TargetType = "CollectionRelation" } },
                    { "Games", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Type", new EndpointDataItem.FieldNameItem { TargetType = "CollectionType" } }
                },
                SupportsSlugSearch = true
            } },
            { "CollectionMembership", new EndpointDataItem {
                Endpoint = "collection_memberships",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Collection", new EndpointDataItem.FieldNameItem { TargetType = "Collection" } },
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Type", new EndpointDataItem.FieldNameItem { TargetType = "CollectionMembershipType" } }
                }
            } },
            { "CollectionMembershipType", new EndpointDataItem {
                Endpoint = "collection_membership_types",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "AllowedCollectionType", new EndpointDataItem.FieldNameItem { TargetType = "CollectionType" } },
                }
            } },
            { "CollectionRelation", new EndpointDataItem {
                Endpoint = "collection_relations",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "ParentCollection", new EndpointDataItem.FieldNameItem { TargetType = "Collection" } },
                    { "ChildCollection", new EndpointDataItem.FieldNameItem { TargetType = "Collection" } },
                    { "Type", new EndpointDataItem.FieldNameItem { TargetType = "CollectionRelationType" } }
                }
            } },
            { "CollectionRelationType", new EndpointDataItem {
                Endpoint = "collection_relation_types",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "AllowedChildType", new EndpointDataItem.FieldNameItem { TargetType = "CollectionType" } },
                    { "AllowedParentType", new EndpointDataItem.FieldNameItem { TargetType = "CollectionType" } }
                }
            } },
            { "CollectionType", new EndpointDataItem { Endpoint = "collection_types" } },
            { "Company", new EndpointDataItem {
                Endpoint = "companies",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "ChangeDateFormats", new EndpointDataItem.FieldNameItem { TargetType = "DateFormat" } },
                    { "ChangedCompanyId", new EndpointDataItem.FieldNameItem { TargetType = "Company" } },
                    { "Developed", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Logo", new EndpointDataItem.FieldNameItem { TargetType = "CompanyLogo" } },
                    { "Parent", new EndpointDataItem.FieldNameItem { TargetType = "Company" } },
                    { "Published", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "StartDateFormat", new EndpointDataItem.FieldNameItem { TargetType = "DateFormat" } },
                    { "Status", new EndpointDataItem.FieldNameItem { TargetType = "CompanyStatus" } },
                    { "Websites", new EndpointDataItem.FieldNameItem { TargetType = "CompanyWebsite" } }
                },
                SupportsSlugSearch = true
            } },
            { "CompanyLogo", new EndpointDataItem { Endpoint = "company_logos" } },
            { "CompanyStatus", new EndpointDataItem { Endpoint = "company_statuses" } },
            { "CompanyWebsite", new EndpointDataItem {
                Endpoint = "company_websites",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Type", new EndpointDataItem.FieldNameItem { TargetType = "WebsiteType" } },
                }
            } },
            { "Cover", new EndpointDataItem {
                Endpoint = "covers",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "GameLocalization", new EndpointDataItem.FieldNameItem { TargetType = "GameLocalization" } }
                }
            } },
            { "DateFormat", new EndpointDataItem { Endpoint = "date_formats" } },
            { "Event", new EndpointDataItem {
                Endpoint = "events",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "EventLogo", new EndpointDataItem.FieldNameItem { TargetType = "EventLogo" } },
                    { "EventNetwork", new EndpointDataItem.FieldNameItem { TargetType = "EventNetwork" } },
                    { "Games", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "EventLogo", new EndpointDataItem { Endpoint = "event_logos" } },
            { "EventNetwork", new EndpointDataItem {
                Endpoint = "event_networks",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Event", new EndpointDataItem.FieldNameItem { TargetType = "Event" } },
                    { "NetworkType", new EndpointDataItem.FieldNameItem { TargetType = "NetworkType" } }
                }
            } },
            { "ExternalGame", new EndpointDataItem {
                Endpoint = "external_games",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "ExternalGameSource", new EndpointDataItem.FieldNameItem { TargetType = "ExternalGameSource" } },
                    { "GameReleaseFormat", new EndpointDataItem.FieldNameItem { TargetType = "GameReleaseFormat" } },
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Platform", new EndpointDataItem.FieldNameItem { TargetType = "Platform" } }
                }
            } },
            { "ExternalGameSource", new EndpointDataItem { Endpoint = "external_game_sources" } },
            { "Franchise", new EndpointDataItem {
                Endpoint = "franchises",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Games", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                },
                SupportsSlugSearch = true
            } },
            { "Game", new EndpointDataItem {
                Endpoint = "games",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "AgeRatings", new EndpointDataItem.FieldNameItem { TargetType = "AgeRating" } },
                    { "AlternativeNames", new EndpointDataItem.FieldNameItem { TargetType = "AlternativeName" } },
                    { "Artworks", new EndpointDataItem.FieldNameItem { TargetType = "Artwork" } },
                    { "Bundles", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Collections", new EndpointDataItem.FieldNameItem { TargetType = "Collection" } },
                    { "Cover", new EndpointDataItem.FieldNameItem { TargetType = "Cover" } },
                    { "Dlcs", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "ExpandedGames", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Expansions", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "ExternalGames", new EndpointDataItem.FieldNameItem { TargetType = "ExternalGame" } },
                    { "Franchise", new EndpointDataItem.FieldNameItem { TargetType = "Franchise" } },
                    { "Franchises", new EndpointDataItem.FieldNameItem { TargetType = "Franchise" } },
                    { "GameEngines", new EndpointDataItem.FieldNameItem { TargetType = "GameEngine" } },
                    { "GameLocalizations", new EndpointDataItem.FieldNameItem { TargetType = "GameLocalization" } },
                    { "GameModes", new EndpointDataItem.FieldNameItem { TargetType = "GameMode" } },
                    { "GameStatus", new EndpointDataItem.FieldNameItem { TargetType = "GameStatus" } },
                    { "GameType", new EndpointDataItem.FieldNameItem { TargetType = "GameType" } },
                    { "Genres", new EndpointDataItem.FieldNameItem { TargetType = "Genre" } },
                    { "Keywords", new EndpointDataItem.FieldNameItem { TargetType = "Keyword" } },
                    { "InvolvedCompanies", new EndpointDataItem.FieldNameItem { TargetType = "InvolvedCompany" } },
                    { "LanguageSupports", new EndpointDataItem.FieldNameItem { TargetType = "LanguageSupport" } },
                    { "MultiplayerModes", new EndpointDataItem.FieldNameItem { TargetType = "MultiplayerMode" } },
                    { "ParentGame", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Platforms", new EndpointDataItem.FieldNameItem { TargetType = "Platform" } },
                    { "PlayerPerspectives", new EndpointDataItem.FieldNameItem { TargetType = "PlayerPerspective" } },
                    { "Ports", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "ReleaseDates", new EndpointDataItem.FieldNameItem { TargetType = "ReleaseDate" } },
                    { "Remakes", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Remasters", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Screenshots", new EndpointDataItem.FieldNameItem { TargetType = "Screenshot" } },
                    { "SimilarGames", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Themes", new EndpointDataItem.FieldNameItem { TargetType = "Theme" } },
                    { "VersionParent", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Videos", new EndpointDataItem.FieldNameItem { TargetType = "GameVideo" } },
                    { "Websites", new EndpointDataItem.FieldNameItem { TargetType = "Website" } }
                },
                SupportsSlugSearch = true
                }},
            { "GameEngine", new EndpointDataItem {
                Endpoint = "game_engines",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Companies", new EndpointDataItem.FieldNameItem { TargetType = "Company" } },
                    { "Logo", new EndpointDataItem.FieldNameItem { TargetType = "GameEngineLogo" } },
                    { "Platforms", new EndpointDataItem.FieldNameItem { TargetType = "Platform" } }
                }
            } },
            { "GameEngineLogo", new EndpointDataItem {Endpoint = "game_engine_logos" } },
            { "GameLocalization", new EndpointDataItem {
                Endpoint = "game_localizations",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Cover", new EndpointDataItem.FieldNameItem { TargetType = "Cover" } },
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Region", new EndpointDataItem.FieldNameItem { TargetType = "Region" } }
                }
            } },
            { "GameMode", new EndpointDataItem { Endpoint = "game_modes" } },
            { "GameReleaseFormat", new EndpointDataItem { Endpoint = "game_release_formats" } },
            { "GameStatus", new EndpointDataItem { Endpoint = "game_statuses" } },
            { "GameTimeToBeat", new EndpointDataItem {
                Endpoint = "game_time_to_beats",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "GameId", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "GameType", new EndpointDataItem { Endpoint = "game_types" } },
            { "GameVersion", new EndpointDataItem {
                Endpoint = "game_versions",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    {"Features", new EndpointDataItem.FieldNameItem { TargetType = "GameVersionFeature" }},
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Games", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "GameVersionFeature", new EndpointDataItem {
                Endpoint = "game_version_features",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Values", new EndpointDataItem.FieldNameItem { TargetType = "GameVersionFeatureValue" } }
                }
            } },
            { "GameVersionFeatureValue", new EndpointDataItem {
                Endpoint = "game_version_feature_values",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "GameFeature", new EndpointDataItem.FieldNameItem { TargetType = "GameVersionFeature" } }
                }
            } },
            { "GameVideo", new EndpointDataItem {
                Endpoint = "game_videos",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "Genre", new EndpointDataItem { Endpoint = "genres" } },
            { "Keyword", new EndpointDataItem { Endpoint = "keywords" } },
            { "InvolvedCompany", new EndpointDataItem {
                Endpoint = "involved_companies",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Company", new EndpointDataItem.FieldNameItem { TargetType = "Company" } },
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "Language", new EndpointDataItem { Endpoint = "languages" } },
            { "LanguageSupport", new EndpointDataItem {
                Endpoint = "language_supports",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Language", new EndpointDataItem.FieldNameItem { TargetType = "Language" } },
                    { "LanguageSupportType", new EndpointDataItem.FieldNameItem { TargetType = "LanguageSupportType" } }
                }
            } },
            { "LanguageSupportType", new EndpointDataItem { Endpoint = "language_support_types" } },
            { "MultiplayerMode", new EndpointDataItem {
                Endpoint = "multiplayer_modes",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Platform", new EndpointDataItem.FieldNameItem { TargetType = "Platform" } }
                }
            } },
            { "NetworkType", new EndpointDataItem {
                Endpoint = "network_types",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "EventNetwork", new EndpointDataItem.FieldNameItem { TargetType = "EventNetwork" } }
                }
            } },
            { "Platform", new EndpointDataItem {
                Endpoint = "platforms",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "PlatformFamily", new EndpointDataItem.FieldNameItem { TargetType = "PlatformFamily" } },
                    { "PlatformLogo", new EndpointDataItem.FieldNameItem { TargetType = "PlatformLogo" } },
                    { "PlatformType", new EndpointDataItem.FieldNameItem { TargetType = "PlatformType" } },
                    { "Versions", new EndpointDataItem.FieldNameItem { TargetType = "PlatformVersion" } },
                    { "Websites", new EndpointDataItem.FieldNameItem { TargetType = "PlatformWebsite" } }
                },
                SupportsSlugSearch = true
            } },
            { "PlatformFamily", new EndpointDataItem { Endpoint = "platform_families" } },
            { "PlatformLogo", new EndpointDataItem { Endpoint = "platform_logos" } },
            { "PlatformType", new EndpointDataItem { Endpoint = "platform_types" } },
            { "PlatformVersion", new EndpointDataItem {
                Endpoint = "platform_versions",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Companies", new EndpointDataItem.FieldNameItem { TargetType = "PlatformVersionCompany" } },
                    { "Manufacturer", new EndpointDataItem.FieldNameItem { TargetType = "Company" } },
                    { "PlatformLogo", new EndpointDataItem.FieldNameItem { TargetType = "PlatformLogo" } },
                    { "PlatformVersionReleaseDates", new EndpointDataItem.FieldNameItem { TargetType = "PlatformVersionReleaseDate" } }
                }
            } },
            { "PlatformVersionCompany", new EndpointDataItem {
                Endpoint = "platform_version_companies",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Company", new EndpointDataItem.FieldNameItem { TargetType = "Company" } },
                    { "Manufacturer", new EndpointDataItem.FieldNameItem { TargetType = "Company" } }
                }
            } },
            { "PlatformVersionReleaseDate", new EndpointDataItem {
                Endpoint = "platform_version_release_dates",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "DateFormat", new EndpointDataItem.FieldNameItem { TargetType = "DateFormat" } },
                    { "PlatformVersion", new EndpointDataItem.FieldNameItem { TargetType = "PlatformVersion" } },
                    { "ReleaseRegion", new EndpointDataItem.FieldNameItem { TargetType = "Region" } }
                }
            } },
            { "PlatformWebsite", new EndpointDataItem { Endpoint = "platform_websites" } },
            { "PlayerPerspective", new EndpointDataItem { Endpoint = "player_perspectives" } },
            { "PopularityPrimitive", new EndpointDataItem {
                Endpoint = "popularity_primitives",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "GameId", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "ExternalPopularitySource", new EndpointDataItem.FieldNameItem { TargetType = "ExternalGameSource" } },
                    { "PopularityType", new EndpointDataItem.FieldNameItem { TargetType = "PopularityType" } }
                }
            } },
            { "PopularityType", new EndpointDataItem {
                Endpoint = "popularity_types",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "ExternalPopularitySource", new EndpointDataItem.FieldNameItem { TargetType = "ExternalGameSource" } }
                }
            } },
            { "Region", new EndpointDataItem { Endpoint = "regions" } },
            { "ReleaseDate", new EndpointDataItem {
                Endpoint = "release_dates",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "DateFormat", new EndpointDataItem.FieldNameItem { TargetType = "DateFormat" } },
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } },
                    { "Platform", new EndpointDataItem.FieldNameItem { TargetType = "Platform" } },
                    { "ReleaseRegion", new EndpointDataItem.FieldNameItem { TargetType = "ReleaseDateRegion" } },
                    { "Status", new EndpointDataItem.FieldNameItem { TargetType = "ReleaseDateStatus" } }
                }
            } },
            { "ReleaseDateRegion", new EndpointDataItem { Endpoint = "release_date_regions" } },
            { "ReleaseDateStatus", new EndpointDataItem { Endpoint = "release_date_statuses" } },
            { "Screenshot", new EndpointDataItem {
                Endpoint = "screenshots",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "Search", new EndpointDataItem { Endpoint = "search" } },
            { "Theme", new EndpointDataItem { Endpoint = "themes" } },
            { "Website", new EndpointDataItem {
                Endpoint = "websites",
                FieldNames = new Dictionary<string, EndpointDataItem.FieldNameItem>
                {
                    { "Game", new EndpointDataItem.FieldNameItem { TargetType = "Game" } }
                }
            } },
            { "WebsiteType", new EndpointDataItem { Endpoint = "website_types" } }
        };

        public static EndpointDataItem GetEndpointData<T>()
        {
            // use reflection to get the endpoint for the type T. The endpoint is a public const and is the name of the type, and is under IGDBClient.Endpoints
            var typeName = typeof(T).Name;
            EndpointDataItem endpoint = new EndpointDataItem();

            if (Endpoints.TryGetValue(typeName, out var endpointData))
            {
                endpoint = endpointData;
            }
            else
            {
                var endpointField = typeof(IGDBClient.Endpoints).GetField(typeName);
                if (endpointField == null)
                {
                    // try again with pluralized type name
                    endpointField = typeof(IGDBClient.Endpoints).GetField(typeName + "s");

                    if (endpointField == null)
                        return null;
                }

                endpoint.Endpoint = (string)endpointField.GetValue(null);
            }

            return endpoint;
        }

        public static string GetEndpointFromSourceTypeAndFieldName(string SourceType, string fieldName)
        {
            // get the endpoint data for the source type
            EndpointDataItem? endpointData = Endpoints.GetValueOrDefault(SourceType);

            // if the endpoint data is null, return null
            if (endpointData == null)
            {
                return string.Empty;
            }

            // if the field name is not in the endpoint data, return the endpoint
            if (!endpointData.FieldNames.ContainsKey(fieldName))
            {
                return string.Empty;
            }

            // if the field name is in the endpoint data, return the target type of the field name
            return endpointData.FieldNames[fieldName].TargetType;
        }

        public class EndpointDataItem
        {
            public string Endpoint { get; set; }
            public Dictionary<string, FieldNameItem> FieldNames { get; set; } = new Dictionary<string, FieldNameItem>();
            public bool SupportsSlugSearch { get; set; } = false;

            public class FieldNameItem
            {
                public string TargetType { get; set; } = string.Empty; // the type that this field name is for
            }
        }
    }
}