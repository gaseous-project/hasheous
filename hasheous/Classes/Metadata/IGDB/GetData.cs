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

        public static EndpointDataItem GetEndpointData<T>()
        {
            // use reflection to get the endpoint for the type T. The endpoint is a public const and is the name of the type, and is under IGDBClient.Endpoints
            var typeName = typeof(T).Name;
            EndpointDataItem endpoint = new EndpointDataItem();

            switch (typeName)
            {
                case "AgeRating":
                    endpoint.Endpoint = "age_ratings";
                    break;

                case "AgeRatingCategory":
                    endpoint.Endpoint = "age_rating_categories";
                    break;

                case "AgeRatingContentDescriptionV2":
                    endpoint.Endpoint = "age_rating_content_descriptions_v2";
                    break;

                case "AgeRatingOrganization":
                    endpoint.Endpoint = "age_rating_organizations";
                    break;

                case "AlternativeName":
                    endpoint.Endpoint = "alternative_names";
                    break;

                case "Artwork":
                    endpoint.Endpoint = "artworks";
                    break;

                case "Character":
                    endpoint.Endpoint = "characters";
                    break;

                case "CharacterGender":
                    endpoint.Endpoint = "character_genders";
                    break;

                case "CharacterMugshot":
                    endpoint.Endpoint = "character_mug_shots";
                    break;

                case "CharacterSpecies":
                    endpoint.Endpoint = "character_species";
                    break;

                case "Collection":
                    endpoint.Endpoint = "collections";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "CollectionMembership":
                    endpoint.Endpoint = "collection_memberships";
                    break;

                case "CollectionMembershipType":
                    endpoint.Endpoint = "collection_membership_types";
                    break;

                case "CollectionRelation":
                    endpoint.Endpoint = "collection_relations";
                    break;

                case "CollectionRelationType":
                    endpoint.Endpoint = "collection_relation_types";
                    break;

                case "CollectionType":
                    endpoint.Endpoint = "collection_types";
                    break;

                case "Company":
                    endpoint.Endpoint = "companies";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "CompanyLogo":
                    endpoint.Endpoint = "company_logos";
                    break;

                case "CompanyStatus":
                    endpoint.Endpoint = "company_statuses";
                    break;

                case "CompanyWebsite":
                    endpoint.Endpoint = "company_websites";
                    break;

                case "Cover":
                    endpoint.Endpoint = "covers";
                    break;

                case "DateFormat":
                    endpoint.Endpoint = "date_formats";
                    break;

                case "Event":
                    endpoint.Endpoint = "events";
                    break;

                case "EventLogo":
                    endpoint.Endpoint = "event_logos";
                    break;

                case "EventNetwork":
                    endpoint.Endpoint = "event_networks";
                    break;

                case "ExternalGame":
                    endpoint.Endpoint = "external_games";
                    break;

                case "ExternalGameSource":
                    endpoint.Endpoint = "external_game_sources";
                    break;

                case "Franchise":
                    endpoint.Endpoint = "franchises";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "Game":
                    endpoint.Endpoint = "games";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "GameEngine":
                    endpoint.Endpoint = "game_engines";
                    break;

                case "GameEngineLogo":
                    endpoint.Endpoint = "game_engine_logos";
                    break;

                case "GameLocalization":
                    endpoint.Endpoint = "game_localizations";
                    break;

                case "GameMode":
                    endpoint.Endpoint = "game_modes";
                    break;

                case "GameReleaseFormat":
                    endpoint.Endpoint = "game_release_formats";
                    break;

                case "GameStatus":
                    endpoint.Endpoint = "game_statuses";
                    break;

                case "GameTimeToBeat":
                    endpoint.Endpoint = "game_time_to_beats";
                    break;

                case "GameType":
                    endpoint.Endpoint = "game_types";
                    break;

                case "GameVersion":
                    endpoint.Endpoint = "game_versions";
                    break;

                case "GameVersionFeature":
                    endpoint.Endpoint = "game_version_features";
                    break;

                case "GameVersionFeatureValue":
                    endpoint.Endpoint = "game_version_feature_values";
                    break;

                case "GameVideo":
                    endpoint.Endpoint = "game_videos";
                    break;

                case "Genre":
                    endpoint.Endpoint = "genres";
                    break;

                case "Keyword":
                    endpoint.Endpoint = "keywords";
                    break;

                case "InvolvedCompany":
                    endpoint.Endpoint = "involved_companies";
                    break;

                case "Language":
                    endpoint.Endpoint = "languages";
                    break;

                case "LanguageSupport":
                    endpoint.Endpoint = "language_supports";
                    break;

                case "LanguageSupportType":
                    endpoint.Endpoint = "language_support_types";
                    break;

                case "MultiplayerMode":
                    endpoint.Endpoint = "multiplayer_modes";
                    break;

                case "NetworkType":
                    endpoint.Endpoint = "network_types";
                    break;

                case "Platform":
                    endpoint.Endpoint = "platforms";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "PlatformFamily":
                    endpoint.Endpoint = "platform_families";
                    break;

                case "PlatformLogo":
                    endpoint.Endpoint = "platform_logos";
                    break;

                case "PlatformType":
                    endpoint.Endpoint = "platform_types";
                    break;

                case "PlatformVersion":
                    endpoint.Endpoint = "platform_versions";
                    break;

                case "PlatformVersionCompany":
                    endpoint.Endpoint = "platform_version_companies";
                    break;

                case "PlatformVersionReleaseDate":
                    endpoint.Endpoint = "platform_version_release_dates";
                    break;

                case "PlatformWebsite":
                    endpoint.Endpoint = "platform_websites";
                    break;

                case "PlayerPerspective":
                    endpoint.Endpoint = "player_perspectives";
                    break;

                case "PopularityPrimitive":
                    endpoint.Endpoint = "popularity_primitives";
                    break;

                case "PopularityType":
                    endpoint.Endpoint = "popularity_types";
                    break;

                case "Region":
                    endpoint.Endpoint = "regions";
                    break;

                case "ReleaseDate":
                    endpoint.Endpoint = "release_dates";
                    break;

                case "ReleaseDateRegion":
                    endpoint.Endpoint = "release_date_regions";
                    break;

                case "ReleaseDateStatus":
                    endpoint.Endpoint = "release_date_statuses";
                    break;

                case "Screenshot":
                    endpoint.Endpoint = "screenshots";
                    break;

                case "Search":
                    endpoint.Endpoint = "search";
                    break;

                case "Theme":
                    endpoint.Endpoint = "themes";
                    break;

                case "Website":
                    endpoint.Endpoint = "websites";
                    break;

                case "WebsiteType":
                    endpoint.Endpoint = "website_types";
                    break;

                default:
                    var endpointField = typeof(IGDBClient.Endpoints).GetField(typeName);
                    if (endpointField == null)
                    {
                        // try again with pluralized type name
                        endpointField = typeof(IGDBClient.Endpoints).GetField(typeName + "s");

                        if (endpointField == null)
                            return null;
                    }

                    endpoint.Endpoint = (string)endpointField.GetValue(null);
                    break;
            }

            return endpoint;
        }

        public class EndpointDataItem
        {
            public string Endpoint { get; set; }
            public bool SupportsSlugSearch { get; set; } = false;
        }
    }
}