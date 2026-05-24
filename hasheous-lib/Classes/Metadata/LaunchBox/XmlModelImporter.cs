using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Classes;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox
{
    /// <summary>
    /// Describes a single node type to import as part of an <see cref="XmlModelImporter.ImportXmlMultipleModelsAsync"/> call.
    /// </summary>
    public class XmlImportDescriptor
    {
        /// <summary>The CLR model type to deserialize each matching XML element into.</summary>
        public required Type ModelType { get; init; }

        /// <summary>Target SQL table name. Must be a valid SQL identifier.</summary>
        public required string TableName { get; init; }

        /// <summary>XML element name to match at depth 1 under the document root.</summary>
        public required string NodeName { get; init; }

        /// <summary>When true (default), the table is truncated before inserting.</summary>
        public bool ClearTableBeforeInsert { get; init; } = true;

        /// <summary>Name of the auto-increment identity column. Set null to disable. Default is "Id".</summary>
        public string? IdentityColumnName { get; init; } = "Id";

        /// <summary>When true, the identity property value from the model is included in INSERT statements. Default false.</summary>
        public bool IncludeIdentityValues { get; init; } = false;

        /// <summary>Creates a typed descriptor using the generic type parameter.</summary>
        public static XmlImportDescriptor For<T>(
            string tableName,
            string nodeName,
            bool clearTableBeforeInsert = true,
            string? identityColumnName = "Id",
            bool includeIdentityValues = false) where T : class
            => new XmlImportDescriptor
            {
                ModelType = typeof(T),
                TableName = tableName,
                NodeName = nodeName,
                ClearTableBeforeInsert = clearTableBeforeInsert,
                IdentityColumnName = identityColumnName,
                IncludeIdentityValues = includeIdentityValues
            };
    }

    /// <summary>
    /// Imports repeated XML nodes into a model type, creates a SQL table from model properties, and inserts all rows.
    /// </summary>
    public static class XmlModelImporter
    {
        /// <summary>
        /// Deserializes each matching XML node into <typeparamref name="T"/>, creates the target table if needed,
        /// and inserts all records into the target database.
        /// </summary>
        public static async Task<int> ImportXmlNodesAsync<T>(
            Database db,
            string databaseName,
            string tableName,
            string xmlFilePath,
            string nodeName,
            bool clearTableBeforeInsert = true,
            string? identityColumnName = "Id",
            bool includeIdentityValues = false) where T : class
        {
            if (!File.Exists(xmlFilePath))
            {
                throw new FileNotFoundException($"XML file not found: {xmlFilePath}");
            }

            string safeDatabaseName = SanitizeIdentifier(databaseName, nameof(databaseName));
            string safeTableName = SanitizeIdentifier(tableName, nameof(tableName));
            string? safeIdentityColumnName = string.IsNullOrWhiteSpace(identityColumnName)
                ? null
                : SanitizeIdentifier(identityColumnName, nameof(identityColumnName));

            List<PropertyInfo> scalarProperties = GetScalarProperties(typeof(T));
            if (scalarProperties.Count == 0)
            {
                throw new InvalidOperationException($"Type '{typeof(T).Name}' has no scalar properties to persist.");
            }

            await EnsureTableAsync(db, safeDatabaseName, safeTableName, scalarProperties, safeIdentityColumnName, includeIdentityValues);

            if (clearTableBeforeInsert)
            {
                await db.ExecuteCMDAsync($"TRUNCATE TABLE `{safeDatabaseName}`.`{safeTableName}`;");
            }

            List<T> nodes = DeserializeNodes<T>(xmlFilePath, nodeName);
            if (nodes.Count == 0)
            {
                return 0;
            }

            List<PropertyInfo> insertProperties = scalarProperties;
            if (!string.IsNullOrWhiteSpace(safeIdentityColumnName) && !includeIdentityValues)
            {
                insertProperties = scalarProperties
                    .Where(x => !x.Name.Equals(safeIdentityColumnName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (insertProperties.Count == 0)
            {
                throw new InvalidOperationException("No insertable properties were found after applying identity column rules.");
            }

            string insertSql = BuildInsertSql(safeDatabaseName, safeTableName, insertProperties);
            foreach (T node in nodes)
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                foreach (PropertyInfo property in insertProperties)
                {
                    object? value = property.GetValue(node);
                    parameters[property.Name] = value ?? DBNull.Value;
                }

                await db.ExecuteCMDAsync(insertSql, parameters);
            }

            return nodes.Count;
        }

        private static List<T> DeserializeNodes<T>(string xmlFilePath, string nodeName) where T : class
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(nodeName));
            List<T> records = new List<T>();

            XmlReader reader = XmlReader.Create(xmlFilePath);
            try
            {
                while (!reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1 && reader.LocalName == nodeName)
                    {
                        string outerXml = reader.ReadOuterXml();
                        using StringReader sr = new StringReader(outerXml);
                        if (serializer.Deserialize(sr) is T item)
                            records.Add(item);
                    }
                    else
                    {
                        reader.Read();
                    }
                }
            }
            finally
            {
                reader.Close();
            }

            return records;
        }

        /// <summary>
        /// Imports multiple model types from a single XML file in one streaming pass.
        /// Each node type is deserialized and inserted immediately, keeping memory usage constant
        /// regardless of file size. Ideal for large XML files containing several node types.
        /// </summary>
        /// <returns>Dictionary mapping each node name to the number of rows imported.</returns>
        public static async Task<Dictionary<string, int>> ImportXmlMultipleModelsAsync(
            Database db,
            string databaseName,
            string xmlFilePath,
            IEnumerable<XmlImportDescriptor> descriptors)
        {
            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException($"XML file not found: {xmlFilePath}");

            string safeDatabaseName = SanitizeIdentifier(databaseName, nameof(databaseName));

            // Build per-node-name context: ensure table, optionally truncate, compile insert SQL.
            Dictionary<string, DescriptorContext> contextMap = new Dictionary<string, DescriptorContext>();
            foreach (XmlImportDescriptor descriptor in descriptors)
            {
                string safeTableName = SanitizeIdentifier(descriptor.TableName, "TableName");
                string? safeIdentityColumn = string.IsNullOrWhiteSpace(descriptor.IdentityColumnName)
                    ? null
                    : SanitizeIdentifier(descriptor.IdentityColumnName, "IdentityColumnName");

                List<PropertyInfo> scalarProps = GetScalarProperties(descriptor.ModelType);
                if (scalarProps.Count == 0)
                    throw new InvalidOperationException($"Type '{descriptor.ModelType.Name}' has no scalar properties to persist.");

                // Detect and create FK tables.
                List<PropertyInfo> fkProperties = descriptor.ModelType.GetProperties()
                    .Where(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null)
                    .ToList();

                List<ForeignKeyPropertyContext> fkPropertyContexts = new List<ForeignKeyPropertyContext>();
                HashSet<string> ensuredForeignKeyTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyInfo fkProp in fkProperties)
                {
                    Type actualPropertyType = Nullable.GetUnderlyingType(fkProp.PropertyType) ?? fkProp.PropertyType;
                    if (actualPropertyType != typeof(string))
                    {
                        throw new InvalidOperationException(
                            $"Property '{descriptor.ModelType.Name}.{fkProp.Name}' uses [ForeignKey] but is '{fkProp.PropertyType.Name}'. " +
                            "Only string properties are supported for automatic lookup-table foreign keys.");
                    }

                    ForeignKeyAttribute fkAttr = fkProp.GetCustomAttribute<ForeignKeyAttribute>()!;
                    if (fkAttr.ReferencedModelType != null)
                    {
                        if (string.IsNullOrWhiteSpace(fkAttr.ReferencedLookupColumn)
                            || string.IsNullOrWhiteSpace(fkAttr.ReferencedIdColumn))
                        {
                            throw new InvalidOperationException(
                                $"Property '{descriptor.ModelType.Name}.{fkProp.Name}' uses typed [ForeignKey] and must explicitly provide " +
                                "ReferencedLookupColumn and ReferencedIdColumn.");
                        }

                        List<PropertyInfo> referenceProps = GetScalarProperties(fkAttr.ReferencedModelType);
                        bool hasReferencedIdColumn = referenceProps.Any(x => x.Name.Equals(fkAttr.ReferencedIdColumn!, StringComparison.OrdinalIgnoreCase));
                        bool hasReferencedLookupColumn = referenceProps.Any(x => x.Name.Equals(fkAttr.ReferencedLookupColumn!, StringComparison.OrdinalIgnoreCase));
                        if (!hasReferencedIdColumn || !hasReferencedLookupColumn)
                        {
                            throw new InvalidOperationException(
                                $"Referenced model type '{fkAttr.ReferencedModelType.Name}' for property '{descriptor.ModelType.Name}.{fkProp.Name}' " +
                                $"must contain scalar '{fkAttr.ReferencedIdColumn}' and '{fkAttr.ReferencedLookupColumn}' properties.");
                        }
                    }

                    fkPropertyContexts.Add(new ForeignKeyPropertyContext
                    {
                        Property = fkProp,
                        Attribute = fkAttr
                    });

                    if (ensuredForeignKeyTables.Add(fkAttr.ReferencedTableName))
                    {
                        // Create FK table if needed.
                        await EnsureForeignKeyTableAsync(db, safeDatabaseName, fkAttr.ReferencedTableName, fkAttr.ReferencedModelType);
                    }
                }

                await EnsureTableAsync(db, safeDatabaseName, safeTableName, scalarProps, safeIdentityColumn, descriptor.IncludeIdentityValues);
                await EnsureIndexesAsync(db, safeDatabaseName, safeTableName, descriptor.ModelType, scalarProps);

                if (descriptor.ClearTableBeforeInsert)
                    await db.ExecuteCMDAsync($"TRUNCATE TABLE `{safeDatabaseName}`.`{safeTableName}`;");

                List<PropertyInfo> insertProps = scalarProps;
                if (!string.IsNullOrWhiteSpace(safeIdentityColumn) && !descriptor.IncludeIdentityValues)
                    insertProps = scalarProps
                        .Where(x => !x.Name.Equals(safeIdentityColumn, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (insertProps.Count == 0)
                    throw new InvalidOperationException($"No insertable properties found for type '{descriptor.ModelType.Name}'.");

                contextMap[descriptor.NodeName] = new DescriptorContext
                {
                    Serializer = new XmlSerializer(descriptor.ModelType, new XmlRootAttribute(descriptor.NodeName)),
                    InsertProperties = insertProps,
                    InsertSql = BuildInsertSql(safeDatabaseName, safeTableName, insertProps),
                    ForeignKeyPropertiesByName = fkPropertyContexts.ToDictionary(x => x.Property.Name, x => x, StringComparer.OrdinalIgnoreCase),
                    ForeignKeyValueCache = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase),
                    DatabaseName = safeDatabaseName,
                    Count = 0
                };
            }

            // Single streaming pass through the file.
            XmlReader xmlReader = XmlReader.Create(xmlFilePath);
            try
            {
                while (!xmlReader.EOF)
                {
                    if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Depth == 1)
                    {
                        string nodeName = xmlReader.LocalName;
                        if (contextMap.TryGetValue(nodeName, out DescriptorContext? ctx))
                        {
                            string outerXml = xmlReader.ReadOuterXml();
                            using StringReader sr = new StringReader(outerXml);
                            object? obj = ctx.Serializer.Deserialize(sr);
                            if (obj != null)
                            {
                                // Build insert parameters
                                Dictionary<string, object> parameters = new Dictionary<string, object>();
                                foreach (PropertyInfo prop in ctx.InsertProperties)
                                {
                                    object? value = prop.GetValue(obj);

                                    // If this property is a foreign key, resolve the referenced row ID.
                                    if (ctx.ForeignKeyPropertiesByName.TryGetValue(prop.Name, out ForeignKeyPropertyContext? fkContext))
                                    {
                                        if (value is string strValue)
                                        {
                                            string normalizedValue = strValue.Trim();
                                            if (string.IsNullOrWhiteSpace(normalizedValue))
                                            {
                                                parameters[prop.Name] = DBNull.Value;
                                            }
                                            else
                                            {
                                                long? fkId = await ResolveForeignKeyValueAsync(db, ctx.DatabaseName, fkContext, normalizedValue, ctx.ForeignKeyValueCache);
                                                parameters[prop.Name] = fkId.HasValue ? fkId.Value : DBNull.Value;
                                            }
                                        }
                                        else
                                        {
                                            parameters[prop.Name] = DBNull.Value;
                                        }
                                    }
                                    else
                                    {
                                        parameters[prop.Name] = value ?? DBNull.Value;
                                    }
                                }

                                await db.ExecuteCMDAsync(ctx.InsertSql, parameters);
                                ctx.Count++;
                            }
                        }
                        else
                        {
                            xmlReader.Skip();
                        }
                    }
                    else
                    {
                        xmlReader.Read();
                    }
                }
            }
            finally
            {
                xmlReader.Close();
            }

            return contextMap.ToDictionary(x => x.Key, x => x.Value.Count);
        }

        private sealed class DescriptorContext
        {
            public required XmlSerializer Serializer { get; init; }
            public required List<PropertyInfo> InsertProperties { get; init; }
            public required string InsertSql { get; init; }
            public required Dictionary<string, ForeignKeyPropertyContext> ForeignKeyPropertiesByName { get; init; }
            public required Dictionary<string, Dictionary<string, long>> ForeignKeyValueCache { get; init; }
            public required string DatabaseName { get; init; }
            public int Count { get; set; }
        }

        private sealed class ForeignKeyPropertyContext
        {
            public required PropertyInfo Property { get; init; }
            public required ForeignKeyAttribute Attribute { get; init; }
        }

        /// <summary>
        /// Resolves a foreign-key string value to a numeric ID, with per-table in-memory caching.
        /// For typed references (ReferencedModelType != null), values are looked up from the existing table by Name.
        /// For untyped references, values are inserted into a simple Id+Name table on demand.
        /// </summary>
        private static async Task<long?> ResolveForeignKeyValueAsync(
            Database db,
            string databaseName,
            ForeignKeyPropertyContext fkContext,
            string value,
            Dictionary<string, Dictionary<string, long>> cache)
        {
            string fkTableName = fkContext.Attribute.ReferencedTableName;
            if (!cache.TryGetValue(fkTableName, out Dictionary<string, long>? tableCache))
            {
                tableCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                cache[fkTableName] = tableCache;
            }

            if (tableCache.TryGetValue(value, out long cachedId))
            {
                return cachedId;
            }

            long? resolvedId;
            if (fkContext.Attribute.ReferencedModelType == null)
            {
                resolvedId = await GetOrInsertSimpleForeignKeyValueAsync(db, databaseName, fkTableName, value);
            }
            else
            {
                resolvedId = await GetExistingForeignKeyValueAsync(
                    db,
                    databaseName,
                    fkTableName,
                    fkContext.Attribute.ReferencedLookupColumn!,
                    fkContext.Attribute.ReferencedIdColumn!,
                    value);
            }

            if (resolvedId.HasValue)
            {
                tableCache[value] = resolvedId.Value;
            }

            return resolvedId;
        }

        /// <summary>
        /// Looks up an existing ID from a typed referenced table using configured lookup/id columns.
        /// </summary>
        private static async Task<long?> GetExistingForeignKeyValueAsync(
            Database db,
            string databaseName,
            string fkTableName,
            string lookupColumn,
            string idColumn,
            string lookupValue)
        {
            string safeFkTableName = SanitizeIdentifier(fkTableName, "fkTableName");
            string safeLookupColumn = SanitizeIdentifier(lookupColumn, nameof(lookupColumn));
            string safeIdColumn = SanitizeIdentifier(idColumn, nameof(idColumn));
            string selectSql = $"SELECT `{safeIdColumn}` FROM `{databaseName}`.`{safeFkTableName}` WHERE `{safeLookupColumn}` = @lookupValue LIMIT 1;";

            List<Dictionary<string, object>> result = await db.ExecuteCMDDictAsync(selectSql, new Dictionary<string, object> { { "lookupValue", lookupValue } });
            if (result != null && result.Count > 0 && result[0].TryGetValue(safeIdColumn, out object? idObj) && idObj != null)
            {
                if (long.TryParse(idObj.ToString(), out long id))
                    return id;
            }

            return null;
        }

        /// <summary>
        /// Inserts a name into a simple FK table (Id, Name) and returns the ID.
        /// Uses INSERT ... ON DUPLICATE KEY UPDATE to handle existing values efficiently.
        /// </summary>
        private static async Task<long> GetOrInsertSimpleForeignKeyValueAsync(
            Database db,
            string databaseName,
            string fkTableName,
            string name)
        {
            string safeFkTableName = SanitizeIdentifier(fkTableName, "fkTableName");

            // INSERT ... ON DUPLICATE KEY UPDATE pattern to get ID
            string insertSql = $@"
                INSERT INTO `{databaseName}`.`{safeFkTableName}` (Name) 
                VALUES (@name)
                ON DUPLICATE KEY UPDATE Id=LAST_INSERT_ID(Id);
                SELECT LAST_INSERT_ID();";

            List<Dictionary<string, object>> result = await db.ExecuteCMDDictAsync(insertSql, new Dictionary<string, object> { { "name", name } });

            if (result != null && result.Count > 0)
            {
                Dictionary<string, object> firstRow = result[0];
                if (firstRow.TryGetValue("LAST_INSERT_ID()", out object? idObj) && idObj != null && long.TryParse(idObj.ToString(), out long id))
                {
                    return id;
                }
            }

            // Fallback: select existing ID
            string selectSql = $"SELECT Id FROM `{databaseName}`.`{safeFkTableName}` WHERE Name = @name LIMIT 1;";
            result = await db.ExecuteCMDDictAsync(selectSql, new Dictionary<string, object> { { "name", name } });

            if (result != null && result.Count > 0 && result[0].TryGetValue("Id", out object? idObj2) && idObj2 != null)
            {
                if (long.TryParse(idObj2.ToString(), out long id))
                    return id;
            }

            throw new InvalidOperationException($"Failed to insert or retrieve FK value '{name}' from {fkTableName}.");
        }

        /// <summary>
        /// Creates a simple foreign key table with Id (auto-increment) and Name (VARCHAR) columns if it doesn't exist.
        /// </summary>
        private static async Task EnsureForeignKeyTableAsync(
            Database db,
            string databaseName,
            string fkTableName,
            Type? fkModelType)
        {
            string safeFkTableName = SanitizeIdentifier(fkTableName, "fkTableName");

            if (fkModelType == null)
            {
                // Simple table: Id + Name
                string createSimpleSql = $@"
                    CREATE TABLE IF NOT EXISTS `{databaseName}`.`{safeFkTableName}` (
                        `Id` BIGINT NOT NULL AUTO_INCREMENT,
                        `Name` VARCHAR(255) NOT NULL UNIQUE,
                        PRIMARY KEY (`Id`)
                    );";

                await db.ExecuteCMDAsync(createSimpleSql);
            }
            else
            {
                // Use the model type's properties to define the FK table
                List<PropertyInfo> fkScalarProps = GetScalarProperties(fkModelType);
                if (fkScalarProps.Count == 0)
                {
                    throw new InvalidOperationException($"FK model type '{fkModelType.Name}' has no scalar properties.");
                }

                await EnsureTableAsync(db, databaseName, safeFkTableName, fkScalarProps, "Id", includeIdentityValues: false);
            }
        }

        private static async Task EnsureTableAsync(
            Database db,
            string databaseName,
            string tableName,
            List<PropertyInfo> properties,
            string? identityColumnName,
            bool includeIdentityValues)
        {
            string? idPropertyName = properties
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(identityColumnName) && x.Name.Equals(identityColumnName, StringComparison.OrdinalIgnoreCase))
                ?.Name;

            string columnSql = string.Join(", ", properties.Select(property =>
            {
                string safeColumnName = SanitizeIdentifier(property.Name, "property");

                if (!string.IsNullOrWhiteSpace(idPropertyName)
                    && safeColumnName.Equals(idPropertyName, StringComparison.OrdinalIgnoreCase)
                    && !includeIdentityValues)
                {
                    return $"`{safeColumnName}` BIGINT NOT NULL AUTO_INCREMENT";
                }

                string sqlType = property.GetCustomAttribute<ForeignKeyAttribute>() != null
                    ? "BIGINT"
                    : MapClrTypeToSqlType(property.PropertyType);
                bool isNullable = IsNullable(property.PropertyType);
                string nullability = isNullable ? "NULL" : "NOT NULL";
                return $"`{safeColumnName}` {sqlType} {nullability}";
            }));

            string primaryKeySql = string.Empty;
            if (!string.IsNullOrWhiteSpace(idPropertyName))
            {
                string safeIdColumnName = SanitizeIdentifier(idPropertyName, "Id property");
                primaryKeySql = $", PRIMARY KEY (`{safeIdColumnName}`)";
            }

            string createTableSql = $@"CREATE TABLE IF NOT EXISTS `{databaseName}`.`{tableName}` (
                {columnSql}
                {primaryKeySql}
            );";

            await db.ExecuteCMDAsync(createTableSql);
        }

        private static async Task EnsureIndexesAsync(
            Database db,
            string databaseName,
            string tableName,
            Type modelType,
            List<PropertyInfo> properties)
        {
            List<ModelIndexDefinition> indexes = GetModelIndexes(modelType);
            if (indexes.Count == 0)
            {
                return;
            }

            HashSet<string> columnNames = properties
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, PropertyInfo> propertyByName = properties
                .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

            HashSet<string> seenIndexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModelIndexDefinition index in indexes)
            {
                if (string.IsNullOrWhiteSpace(index.Name))
                    throw new InvalidOperationException($"Model '{modelType.Name}' declared an index with an empty name.");

                string safeIndexName = SanitizeIdentifier(index.Name, "index.Name");
                if (!seenIndexNames.Add(safeIndexName))
                    throw new InvalidOperationException($"Model '{modelType.Name}' has duplicate index name '{safeIndexName}'.");

                if (index.Columns == null || index.Columns.Length == 0)
                    throw new InvalidOperationException($"Index '{safeIndexName}' on model '{modelType.Name}' must contain at least one column.");

                string[] safeColumns = index.Columns
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => SanitizeIdentifier(x, "index.Columns"))
                    .ToArray();

                if (safeColumns.Length == 0)
                    throw new InvalidOperationException($"Index '{safeIndexName}' on model '{modelType.Name}' must contain at least one valid column.");

                foreach (string col in safeColumns)
                {
                    if (!columnNames.Contains(col))
                        throw new InvalidOperationException($"Index '{safeIndexName}' references unknown column '{col}' on model '{modelType.Name}'.");
                }

                string[] indexColumnSqlParts = safeColumns.Select(col =>
                {
                    PropertyInfo prop = propertyByName[col];
                    bool isForeignKeyColumn = prop.GetCustomAttribute<ForeignKeyAttribute>() != null;
                    Type actualType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    // String properties map to LONGTEXT in this importer. MySQL requires a prefix length for LONGTEXT indexes.
                    if (!isForeignKeyColumn && actualType == typeof(string))
                    {
                        return $"`{col}`(191)";
                    }

                    return $"`{col}`";
                }).ToArray();

                string indexExistsSql = @"SELECT 1
                    FROM information_schema.statistics
                    WHERE table_schema = @schema
                      AND table_name = @table
                      AND index_name = @indexName
                    LIMIT 1;";

                List<Dictionary<string, object>> existingIndexRows = await db.ExecuteCMDDictAsync(indexExistsSql, new Dictionary<string, object>
                {
                    { "schema", databaseName },
                    { "table", tableName },
                    { "indexName", safeIndexName }
                });

                if (existingIndexRows.Count > 0)
                    continue;

                string uniqueKeyword = index.Unique ? "UNIQUE " : string.Empty;
                string columnSql = string.Join(", ", indexColumnSqlParts);
                string createIndexSql = $"CREATE {uniqueKeyword}INDEX `{safeIndexName}` ON `{databaseName}`.`{tableName}` ({columnSql});";
                await db.ExecuteCMDAsync(createIndexSql);
            }
        }

        private static List<ModelIndexDefinition> GetModelIndexes(Type modelType)
        {
            MethodInfo? method = modelType.GetMethod("GetIndexes", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return new List<ModelIndexDefinition>();

            object? result = method.Invoke(null, null);
            if (result == null)
                return new List<ModelIndexDefinition>();

            if (result is IEnumerable<ModelIndexDefinition> typed)
                return typed.ToList();

            throw new InvalidOperationException($"Model '{modelType.Name}' has GetIndexes() but it does not return IEnumerable<ModelIndexDefinition>.");
        }

        private static string BuildInsertSql(string databaseName, string tableName, List<PropertyInfo> properties)
        {
            string columns = string.Join(", ", properties.Select(x => $"`{SanitizeIdentifier(x.Name, "property")}`"));
            string values = string.Join(", ", properties.Select(x => $"@{x.Name}"));

            return $"INSERT INTO `{databaseName}`.`{tableName}` ({columns}) VALUES ({values});";
        }

        private static List<PropertyInfo> GetScalarProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && IsScalarType(x.PropertyType))
                .ToList();
        }

        private static bool IsScalarType(Type type)
        {
            Type actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType.IsEnum)
            {
                return true;
            }

            return actualType == typeof(string)
                || actualType == typeof(bool)
                || actualType == typeof(byte)
                || actualType == typeof(sbyte)
                || actualType == typeof(short)
                || actualType == typeof(ushort)
                || actualType == typeof(int)
                || actualType == typeof(uint)
                || actualType == typeof(long)
                || actualType == typeof(ulong)
                || actualType == typeof(float)
                || actualType == typeof(double)
                || actualType == typeof(decimal)
                || actualType == typeof(DateTime)
                || actualType == typeof(Guid);
        }

        private static bool IsNullable(Type type)
        {
            if (!type.IsValueType)
            {
                return true;
            }

            return Nullable.GetUnderlyingType(type) != null;
        }

        private static string MapClrTypeToSqlType(Type type)
        {
            Type actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (actualType.IsEnum)
            {
                return "VARCHAR(255)";
            }

            if (actualType == typeof(string))
            {
                return "LONGTEXT";
            }

            if (actualType == typeof(bool))
            {
                return "TINYINT(1)";
            }

            if (actualType == typeof(byte) || actualType == typeof(sbyte))
            {
                return "TINYINT";
            }

            if (actualType == typeof(short) || actualType == typeof(ushort))
            {
                return "SMALLINT";
            }

            if (actualType == typeof(int) || actualType == typeof(uint))
            {
                return "INT";
            }

            if (actualType == typeof(long) || actualType == typeof(ulong))
            {
                return "BIGINT";
            }

            if (actualType == typeof(float))
            {
                return "FLOAT";
            }

            if (actualType == typeof(double))
            {
                return "DOUBLE";
            }

            if (actualType == typeof(decimal))
            {
                return "DECIMAL(38, 18)";
            }

            if (actualType == typeof(DateTime))
            {
                return "DATETIME";
            }

            if (actualType == typeof(Guid))
            {
                return "CHAR(36)";
            }

            return "LONGTEXT";
        }

        private static string SanitizeIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be empty.", parameterName);
            }

            if (!Regex.IsMatch(identifier, "^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                throw new ArgumentException($"Identifier '{identifier}' is not a valid SQL identifier.", parameterName);
            }

            return identifier;
        }
    }
}