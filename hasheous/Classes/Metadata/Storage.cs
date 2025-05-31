using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using IGDB;
using IGDB.Models;

namespace Classes.Metadata
{
    public class Storage
    {
        public enum CacheStatus
        {
            NotPresent,
            Current,
            Expired
        }

        public enum TablePrefix
        {
            IGDB
        }

        private static string GetTableName(TablePrefix prefix, string Endpoint)
        {
            return prefix.ToString() + "_" + Endpoint;
        }

        public static async Task<CacheStatus> GetCacheStatusAsync(TablePrefix prefix, string Endpoint, string Slug)
        {
            return await _GetCacheStatus(prefix, Endpoint, "slug", Slug);
        }

        public static async Task<CacheStatus> GetCacheStatusAsync(TablePrefix prefix, string Endpoint, long Id)
        {
            return await _GetCacheStatus(prefix, Endpoint, "id", Id);
        }

        public static CacheStatus GetCacheStatus(DataRow Row)
        {
            if (Row.Table.Columns.Contains("lastUpdated"))
            {
                DateTime CacheExpiryTime = DateTime.UtcNow.AddHours(-168);
                if ((DateTime)Row["lastUpdated"] < CacheExpiryTime)
                {
                    return CacheStatus.Expired;
                }
                else
                {
                    return CacheStatus.Current;
                }
            }
            else
            {
                throw new Exception("No lastUpdated column!");
            }
        }

        private static async Task<CacheStatus> _GetCacheStatus(TablePrefix prefix, string Endpoint, string SearchField, object SearchValue)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            string sql = "SELECT lastUpdated FROM " + GetTableName(prefix, Endpoint) + " WHERE " + SearchField + " = @" + SearchField;

            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            dbDict.Add("Endpoint", Endpoint);
            dbDict.Add(SearchField, SearchValue);

            DataTable dt = await db.ExecuteCMDAsync(sql, dbDict);
            if (dt.Rows.Count == 0)
            {
                // no data stored for this item, or lastUpdated
                return CacheStatus.NotPresent;
            }
            else
            {
                DateTime CacheExpiryTime = DateTime.UtcNow.AddHours(-168);
                if ((DateTime)dt.Rows[0]["lastUpdated"] < CacheExpiryTime)
                {
                    return CacheStatus.Expired;
                }
                else
                {
                    return CacheStatus.Current;
                }
            }
        }

        public static async Task NewCacheValueAsync(TablePrefix prefix, object ObjectToCache, bool UpdateRecord = false)
        {
            // get the object type name
            if (ObjectToCache != null)
            {
                string ObjectTypeName = ObjectToCache.GetType().Name;

                // build dictionary
                string objectJson = Newtonsoft.Json.JsonConvert.SerializeObject(ObjectToCache);
                Dictionary<string, object?> objectDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object?>>(objectJson);
                objectDict.Add("dateAdded", DateTime.UtcNow);
                objectDict.Add("lastUpdated", DateTime.UtcNow);

                // generate sql
                string fieldList = "";
                string valueList = "";
                string updateFieldValueList = "";
                foreach (KeyValuePair<string, object?> key in objectDict)
                {
                    if (fieldList.Length > 0)
                    {
                        fieldList = fieldList + ", ";
                        valueList = valueList + ", ";
                    }
                    fieldList = fieldList + key.Key;
                    valueList = valueList + "@" + key.Key;
                    if ((key.Key != "id") && (key.Key != "dateAdded"))
                    {
                        if (updateFieldValueList.Length > 0)
                        {
                            updateFieldValueList = updateFieldValueList + ", ";
                        }
                        updateFieldValueList += key.Key + " = @" + key.Key;
                    }

                    // check property type
                    Type objectType = ObjectToCache.GetType();
                    if (objectType != null)
                    {
                        PropertyInfo objectProperty = objectType.GetProperty(key.Key);
                        if (objectProperty != null)
                        {
                            string compareName = objectProperty.PropertyType.Name.ToLower().Split("`")[0];
                            var objectValue = objectProperty.GetValue(ObjectToCache);
                            if (objectValue != null)
                            {
                                string newObjectValue;
                                Dictionary<string, object> newDict;
                                switch (compareName)
                                {
                                    case "identityorvalue":
                                        newObjectValue = Newtonsoft.Json.JsonConvert.SerializeObject(objectValue);
                                        newDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(newObjectValue);
                                        objectDict[key.Key] = newDict["Id"];
                                        break;
                                    case "identitiesorvalues":
                                        newObjectValue = Newtonsoft.Json.JsonConvert.SerializeObject(objectValue);
                                        newDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(newObjectValue);
                                        newObjectValue = Newtonsoft.Json.JsonConvert.SerializeObject(newDict["Ids"]);
                                        objectDict[key.Key] = newObjectValue;
                                        break;
                                    case "int32[]":
                                    case "int64[]":
                                        newObjectValue = Newtonsoft.Json.JsonConvert.SerializeObject(objectValue);
                                        objectDict[key.Key] = newObjectValue;
                                        break;
                                }
                            }
                        }
                    }
                }

                string sql = "";
                if (UpdateRecord == false)
                {
                    sql = "INSERT INTO " + GetTableName(prefix, ObjectTypeName) + " (" + fieldList + ") VALUES (" + valueList + ")";
                }
                else
                {
                    sql = "UPDATE " + GetTableName(prefix, ObjectTypeName) + " SET " + updateFieldValueList + " WHERE Id = @Id";
                }

                // execute sql
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                await db.ExecuteCMDAsync(sql, objectDict);
            }
        }

        public static async Task<T> GetCacheValueAsync<T>(T EndpointType, TablePrefix prefix, string SearchField, object SearchValue)
        {
            string Endpoint = EndpointType.GetType().Name;

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            string sql = "SELECT * FROM " + GetTableName(prefix, Endpoint) + " WHERE " + SearchField + " = @" + SearchField;

            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            dbDict.Add("Endpoint", Endpoint);
            dbDict.Add(SearchField, SearchValue);

            DataTable dt = await db.ExecuteCMDAsync(sql, dbDict);
            if (dt.Rows.Count == 0)
            {
                // no data stored for this item
                throw new Exception("No record found that matches endpoint " + Endpoint + " with search value " + SearchValue);
            }
            else
            {
                DataRow dataRow = dt.Rows[0];
                return BuildCacheObject<T>(EndpointType, dataRow);
            }
        }

        public static T BuildCacheObject<T>(T EndpointType, DataRow dataRow)
        {
            foreach (PropertyInfo property in EndpointType.GetType().GetProperties())
            {
                if (dataRow.Table.Columns.Contains(property.Name))
                {
                    if (dataRow[property.Name] != DBNull.Value)
                    {
                        string objectTypeName = property.PropertyType.Name.ToLower().Split("`")[0];
                        string subObjectTypeName = "";
                        object? objectToStore = null;
                        if (objectTypeName == "nullable")
                        {
                            objectTypeName = property.PropertyType.UnderlyingSystemType.ToString().Split("`1")[1].Replace("[System.", "").Replace("]", "").ToLower();
                        }
                        try
                        {
                            // Use the assembly of a known IGDB.Models type to resolve the type
                            var igdbAssembly = typeof(IGDB.Models.Game).Assembly;
                            Type? genericArgType;

                            switch (objectTypeName)
                            {
                                case "datetimeoffset":
                                    DateTimeOffset? storedDate = (DateTime?)dataRow[property.Name];
                                    property.SetValue(EndpointType, storedDate);
                                    break;
                                case "identityorvalue":
                                    subObjectTypeName = property.PropertyType.UnderlyingSystemType.ToString().Split("`1")[1].Replace("[IGDB.Models.", "").Replace("]", "");

                                    // Use reflection to create IdentityOrValue<T> dynamically
                                    genericArgType = igdbAssembly.GetType("IGDB.Models." + subObjectTypeName, false, true);
                                    if (genericArgType == null)
                                    {
                                        throw new Exception("Could not find type IGDB.Models." + subObjectTypeName);
                                    }

                                    Type identityOrValueType = typeof(IdentityOrValue<>).MakeGenericType(genericArgType);
                                    objectToStore = Activator.CreateInstance(identityOrValueType, new object[] { (object)dataRow[property.Name] });
                                    property.SetValue(EndpointType, objectToStore);

                                    break;
                                case "identitiesorvalues":
                                    subObjectTypeName = property.PropertyType.UnderlyingSystemType.ToString().Split("`1")[1].Replace("[IGDB.Models.", "").Replace("]", "");

                                    long[] fromJsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>((string)dataRow[property.Name]);

                                    genericArgType = igdbAssembly.GetType("IGDB.Models." + subObjectTypeName, false, true);
                                    if (genericArgType == null)
                                    {
                                        throw new Exception("Could not find type IGDB.Models." + subObjectTypeName);
                                    }

                                    // Create a generic type for IdentitiesOrValues<T>
                                    Type identitiesOrValuesType = typeof(IdentitiesOrValues<>).MakeGenericType(genericArgType);

                                    // Create an instance of IdentitiesOrValues<T> with the deserialized IDs
                                    objectToStore = Activator.CreateInstance(identitiesOrValuesType, new object[] { fromJsonObject });

                                    if (objectToStore == null)
                                    {
                                        throw new Exception("Could not create instance of IdentitiesOrValues<" + subObjectTypeName + ">");
                                    }

                                    if (objectToStore != null)
                                    {
                                        property.SetValue(EndpointType, objectToStore);
                                    }

                                    break;
                                case "int32[]":
                                    Int32[] fromJsonObject_int32Array = Newtonsoft.Json.JsonConvert.DeserializeObject<Int32[]>((string)dataRow[property.Name]);
                                    if (fromJsonObject_int32Array != null)
                                    {
                                        property.SetValue(EndpointType, fromJsonObject_int32Array);
                                    }
                                    break;
                                case "int64[]":
                                    Int64[] fromJsonObject_int64Array = Newtonsoft.Json.JsonConvert.DeserializeObject<Int64[]>((string)dataRow[property.Name]);
                                    if (fromJsonObject_int64Array != null)
                                    {
                                        property.SetValue(EndpointType, fromJsonObject_int64Array);
                                    }
                                    break;
                                // case "[igdb.models.category":
                                //     property.SetValue(EndpointType, (Category)dataRow[property.Name]);
                                //     break;
                                // case "[igdb.models.gamestatus":
                                //     property.SetValue(EndpointType, (GameStatus)dataRow[property.Name]);
                                //     break;
                                // case "[igdb.models.ageratingcategory":
                                //     property.SetValue(EndpointType, (AgeRatingCategory)dataRow[property.Name]);
                                //     break;
                                // case "[igdb.models.ageratingcontentdescriptioncategory":
                                //     property.SetValue(EndpointType, (AgeRatingContentDescriptionCategory)dataRow[property.Name]);
                                //     break;
                                // case "[igdb.models.ageratingtitle":
                                //     property.SetValue(EndpointType, (AgeRatingTitle)dataRow[property.Name]);
                                //     break;
                                // case "[igdb.models.externalcategory":
                                //     property.SetValue(EndpointType, (ExternalCategory)dataRow[property.Name]);
                                //     break;
                                // case "[igdb.models.startdatecategory":
                                //     property.SetValue(EndpointType, (StartDateCategory)dataRow[property.Name]);
                                //     break;
                                default:
                                    property.SetValue(EndpointType, dataRow[property.Name]);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error occurred in column " + property.Name);
                            Console.WriteLine(ex.ToString());
                        }
                    }
                }
            }

            return EndpointType;
        }
    }
}

