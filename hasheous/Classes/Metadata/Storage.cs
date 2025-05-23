﻿using System;
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
                            switch (objectTypeName)
                            {
                                //case "boolean":
                                //    Boolean storedBool = Convert.ToBoolean((int)dataRow[property.Name]);
                                //    property.SetValue(EndpointType, storedBool);
                                //    break;
                                case "datetimeoffset":
                                    DateTimeOffset? storedDate = (DateTime?)dataRow[property.Name];
                                    property.SetValue(EndpointType, storedDate);
                                    break;
                                //case "nullable":
                                //	Console.WriteLine("Nullable: " + property.PropertyType.UnderlyingSystemType);
                                //	break;
                                case "identityorvalue":
                                    subObjectTypeName = property.PropertyType.UnderlyingSystemType.ToString().Split("`1")[1].Replace("[IGDB.Models.", "").Replace("]", "").ToLower();

                                    switch (subObjectTypeName)
                                    {
                                        case "collection":
                                            objectToStore = new IdentityOrValue<Collection>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "company":
                                            objectToStore = new IdentityOrValue<Company>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "cover":
                                            objectToStore = new IdentityOrValue<Cover>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "franchise":
                                            objectToStore = new IdentityOrValue<Franchise>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "game":
                                            objectToStore = new IdentityOrValue<Game>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "platformfamily":
                                            objectToStore = new IdentityOrValue<PlatformFamily>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "platformlogo":
                                            objectToStore = new IdentityOrValue<PlatformLogo>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "platformversioncompany":
                                            objectToStore = new IdentityOrValue<PlatformVersionCompany>(id: (long)dataRow[property.Name]);
                                            break;
                                        case "region":
                                            objectToStore = new IdentityOrValue<Region>(id: (long)dataRow[property.Name]);
                                            break;
                                    }

                                    if (objectToStore != null)
                                    {
                                        property.SetValue(EndpointType, objectToStore);
                                    }

                                    break;
                                case "identitiesorvalues":
                                    subObjectTypeName = property.PropertyType.UnderlyingSystemType.ToString().Split("`1")[1].Replace("[IGDB.Models.", "").Replace("]", "").ToLower();

                                    long[] fromJsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>((string)dataRow[property.Name]);

                                    switch (subObjectTypeName)
                                    {
                                        case "agerating":
                                            objectToStore = new IdentitiesOrValues<AgeRating>(ids: fromJsonObject);
                                            break;
                                        case "alternativename":
                                            objectToStore = new IdentitiesOrValues<AlternativeName>(ids: fromJsonObject);
                                            break;
                                        case "artwork":
                                            objectToStore = new IdentitiesOrValues<Artwork>(ids: fromJsonObject);
                                            break;
                                        case "ageratingcontentdescription":
                                            objectToStore = new IdentitiesOrValues<AgeRatingContentDescription>(ids: fromJsonObject);
                                            break;
                                        case "game":
                                            objectToStore = new IdentitiesOrValues<Game>(ids: fromJsonObject);
                                            break;
                                        case "externalgame":
                                            objectToStore = new IdentitiesOrValues<ExternalGame>(ids: fromJsonObject);
                                            break;
                                        case "franchise":
                                            objectToStore = new IdentitiesOrValues<Franchise>(ids: fromJsonObject);
                                            break;
                                        case "gameengine":
                                            objectToStore = new IdentitiesOrValues<GameEngine>(ids: fromJsonObject);
                                            break;
                                        case "gamemode":
                                            objectToStore = new IdentitiesOrValues<GameMode>(ids: fromJsonObject);
                                            break;
                                        case "gamelocalization":
                                            objectToStore = new IdentitiesOrValues<GameLocalization>(ids: fromJsonObject);
                                            break;
                                        case "gamevideo":
                                            objectToStore = new IdentitiesOrValues<GameVideo>(ids: fromJsonObject);
                                            break;
                                        case "genre":
                                            objectToStore = new IdentitiesOrValues<Genre>(ids: fromJsonObject);
                                            break;
                                        case "involvedcompany":
                                            objectToStore = new IdentitiesOrValues<InvolvedCompany>(ids: fromJsonObject);
                                            break;
                                        case "multiplayermode":
                                            objectToStore = new IdentitiesOrValues<MultiplayerMode>(ids: fromJsonObject);
                                            break;
                                        case "platform":
                                            objectToStore = new IdentitiesOrValues<Platform>(ids: fromJsonObject);
                                            break;
                                        case "platformversion":
                                            objectToStore = new IdentitiesOrValues<PlatformVersion>(ids: fromJsonObject);
                                            break;
                                        case "platformwebsite":
                                            objectToStore = new IdentitiesOrValues<PlatformWebsite>(ids: fromJsonObject);
                                            break;
                                        case "platformversioncompany":
                                            objectToStore = new IdentitiesOrValues<PlatformVersionCompany>(ids: fromJsonObject);
                                            break;
                                        case "platformversionreleasedate":
                                            objectToStore = new IdentitiesOrValues<PlatformVersionReleaseDate>(ids: fromJsonObject);
                                            break;
                                        case "playerperspective":
                                            objectToStore = new IdentitiesOrValues<PlayerPerspective>(ids: fromJsonObject);
                                            break;
                                        case "region":
                                            objectToStore = new IdentitiesOrValues<Region>(ids: fromJsonObject);
                                            break;
                                        case "releasedate":
                                            objectToStore = new IdentitiesOrValues<ReleaseDate>(ids: fromJsonObject);
                                            break;
                                        case "screenshot":
                                            objectToStore = new IdentitiesOrValues<Screenshot>(ids: fromJsonObject);
                                            break;
                                        case "theme":
                                            objectToStore = new IdentitiesOrValues<Theme>(ids: fromJsonObject);
                                            break;
                                        case "website":
                                            objectToStore = new IdentitiesOrValues<Website>(ids: fromJsonObject);
                                            break;
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
                                case "[igdb.models.category":
                                    property.SetValue(EndpointType, (Category)dataRow[property.Name]);
                                    break;
                                case "[igdb.models.gamestatus":
                                    property.SetValue(EndpointType, (GameStatus)dataRow[property.Name]);
                                    break;
                                case "[igdb.models.ageratingcategory":
                                    property.SetValue(EndpointType, (AgeRatingCategory)dataRow[property.Name]);
                                    break;
                                case "[igdb.models.ageratingcontentdescriptioncategory":
                                    property.SetValue(EndpointType, (AgeRatingContentDescriptionCategory)dataRow[property.Name]);
                                    break;
                                case "[igdb.models.ageratingtitle":
                                    property.SetValue(EndpointType, (AgeRatingTitle)dataRow[property.Name]);
                                    break;
                                case "[igdb.models.externalcategory":
                                    property.SetValue(EndpointType, (ExternalCategory)dataRow[property.Name]);
                                    break;
                                case "[igdb.models.startdatecategory":
                                    property.SetValue(EndpointType, (StartDateCategory)dataRow[property.Name]);
                                    break;
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

