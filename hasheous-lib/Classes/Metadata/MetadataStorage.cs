namespace Classes.Metadata
{
    public class MetadataStorage
    {
        /// <summary>
        /// Stores an object and its subclasses in the database.
        /// The table name is the type name. Subclasses are stored in their own tables and linked by Id.
        /// Subclasses without an Id property will be assigned one by the database (auto-increment).
        /// </summary>
        public static async Task StoreObjectWithSubclasses(object obj, Database db, string dbName, long? id = null)
        {
            if (obj == null) return;

            Type objType = obj.GetType();
            string tableName = objType.Name;

            var properties = objType.GetProperties();
            var parameters = new Dictionary<string, object>();
            object? idValue = null;

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);

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
                            // serialize the collection to JSON if no Id property exists
                            string jsonValue = Newtonsoft.Json.JsonConvert.SerializeObject(value, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 30
                            });
                            parameters[prop.Name] = jsonValue;
                        }
                        else
                        {
                            // make sure the relationship table exists
                            string relationshipTableName = $"Relation_{tableName}_{prop.Name}";
                            string createTableSql = $@"CREATE TABLE IF NOT EXISTS {dbName}.{relationshipTableName} (
                                {tableName}_id BIGINT NOT NULL,
                                {prop.Name}_id BIGINT NOT NULL,
                                PRIMARY KEY ({tableName}_id, {prop.Name}_id),
                                INDEX idx_{tableName}_id ({tableName}_id),
                                INDEX idx_{prop.Name}_id ({prop.Name}_id)
                            );";
                            await db.ExecuteCMDAsync(createTableSql, new Dictionary<string, object>());

                            // remove all existing relationships for this object
                            string deleteSql = $"DELETE FROM {dbName}.{relationshipTableName} WHERE {tableName}_id = @id";
                            await db.ExecuteCMDAsync(deleteSql, new Dictionary<string, object> { { "id", id ?? (object)DBNull.Value } });
                            await db.ExecuteCMDAsync(deleteSql, new Dictionary<string, object> { { "id", id ?? (object)DBNull.Value } });

                            // store a JSON array of Ids for the collection
                            var ids = new List<object>();
                            foreach (var item in (IEnumerable<object>)value)
                            {
                                var subId = subIdProp.GetValue(item);
                                ids.Add(subId ?? DBNull.Value);

                                // insert the relationship into the relationship table
                                if (subId != null)
                                {
                                    string insertSql = $"INSERT INTO {dbName}.{relationshipTableName} ({tableName}_id, {prop.Name}_id) VALUES (@{tableName}_id, @{prop.Name}_id)";
                                    await db.ExecuteCMDAsync(insertSql, new Dictionary<string, object>
                                    {
                                        { $"{tableName}_id", id ?? (object)DBNull.Value },
                                        { $"{prop.Name}_id", subId }
                                    });
                                }
                            }

                            // store the JSON array of Ids
                            string jsonArray = Newtonsoft.Json.JsonConvert.SerializeObject(ids, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 30
                            });
                            parameters[prop.Name] = jsonArray;
                        }
                    }
                    else
                    {

                        var subIdProp = prop.PropertyType.GetProperty("id") ?? prop.PropertyType.GetProperty("Id");
                        if (subIdProp != null)
                        {
                            await StoreObjectWithSubclasses(value, db, dbName);

                            var subId = subIdProp.GetValue(value);
                            parameters[prop.Name] = subId ?? DBNull.Value;
                        }
                        else
                        {
                            // serialize the object to JSON if no Id property exists
                            string jsonValue = Newtonsoft.Json.JsonConvert.SerializeObject(value, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 30
                            });
                            parameters[prop.Name] = jsonValue;
                        }
                    }
                }
                else
                {
                    parameters[prop.Name] = value ?? DBNull.Value;
                    if (prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        idValue = value ?? DBNull.Value;
                    }
                }
            }

            // Check if object exists (by Id)
            bool exists = false;
            if (idValue != null)
            {
                string checkSql = $"SELECT `id` FROM {dbName}.{tableName} WHERE id = @id";
                var result = await db.ExecuteCMDAsync(checkSql, new Dictionary<string, object> { { "id", idValue } });
                exists = result.Rows.Count > 0;
            }

            // Build SQL
            string sql;
            if (exists)
            {
                // Update
                var setClause = string.Join(", ", parameters.Keys.Where(k => !k.Equals("id", StringComparison.OrdinalIgnoreCase)).Select(k => $"`{k}` = @{k}"));
                sql = $"UPDATE {dbName}.{tableName} SET {setClause} WHERE id = @id";

                await db.ExecuteCMDAsync(sql, parameters);
            }
            else
            {
                // Insert
                var cols = string.Join(", ", parameters.Keys.Select(k => $"`{k}`"));
                var vals = string.Join(", ", parameters.Keys.Select(k => $"@{k}"));
                sql = $"INSERT INTO {dbName}.{tableName} ({cols}) VALUES ({vals});";

                // Execute insert
                var result = await db.ExecuteCMDAsync(sql, parameters);
            }

            return;
        }
    }
}