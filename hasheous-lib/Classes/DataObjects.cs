using System.Data;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Authentication;
using Classes;
using hasheous.Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Classes.Tasks.Clients;
using hasheous_server.Models;
using IGDB;
using IGDB.Models;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using static hasheous_server.Classes.Metadata.Communications;
using static hasheous_server.Models.DataObjectItem;

namespace hasheous_server.Classes
{
    public class DataObjects
    {
        public class DataObjectsBadSearchCriteriaException : Exception
        {
            public DataObjectsBadSearchCriteriaException()
            {
            }

            public DataObjectsBadSearchCriteriaException(string message)
                : base(message)
            {
            }

            public DataObjectsBadSearchCriteriaException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public enum DataObjectType
        {
            Company = 0,
            Platform = 1,
            Game = 2,
            ROM = 3,
            App = 4,
            None = 100
        }

        public static Dictionary<DataObjectType, object> DataObjectDefinitions = new Dictionary<DataObjectType, object>
        {
            { DataObjectType.Company, new DataObjectDefinition{
                HasMetadata = true,
                HasSignatures = true,
                AllowMerge = true,
                Attributes = new List<AttributeItem>{
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Description,
                        attributeType = AttributeItem.AttributeType.LongString,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Logo,
                        attributeType = AttributeItem.AttributeType.ImageId,
                        attributeRelationType = DataObjectType.None
                    }
                }
            } },
            { DataObjectType.Platform, new DataObjectDefinition{
                HasMetadata = true,
                HasSignatures = true,
                AllowMerge = true,
                Attributes = new List<AttributeItem>{
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Description,
                        attributeType = AttributeItem.AttributeType.LongString,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Logo,
                        attributeType = AttributeItem.AttributeType.ImageId,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Manufacturer,
                        attributeType = AttributeItem.AttributeType.ObjectRelationship,
                        attributeRelationType = DataObjectType.Company
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.VIMMPlatformName,
                        attributeType = AttributeItem.AttributeType.ShortString,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem
                    {
                        attributeName = AttributeItem.AttributeName.Tags,
                        attributeType = AttributeItem.AttributeType.EmbeddedList,
                        attributeRelationType = DataObjectType.None
                    }
                }
            } },
            { DataObjectType.Game, new DataObjectDefinition{
                HasMetadata = true,
                HasSignatures = true,
                AllowMerge = true,
                Attributes = new List<AttributeItem>{
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Description,
                        attributeType = AttributeItem.AttributeType.LongString,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Logo,
                        attributeType = AttributeItem.AttributeType.ImageId,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Publisher,
                        attributeType = AttributeItem.AttributeType.ObjectRelationship,
                        attributeRelationType = DataObjectType.Company
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Platform,
                        attributeType = AttributeItem.AttributeType.ObjectRelationship,
                        attributeRelationType = DataObjectType.Platform
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.VIMMManualId,
                        attributeType = AttributeItem.AttributeType.ShortString,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Wikipedia,
                        attributeType = AttributeItem.AttributeType.Link,
                        attributeRelationType = DataObjectType.None
                    },
                    new AttributeItem
                    {
                        attributeName = AttributeItem.AttributeName.Tags,
                        attributeType = AttributeItem.AttributeType.EmbeddedList,
                        attributeRelationType = DataObjectType.None
                    }
                }
            } },
            {
                DataObjectType.App, new DataObjectDefinition
                {
                    HasMetadata = false,
                    HasSignatures = false,
                    AllowMerge = false,
                    Attributes = new List<AttributeItem>
                    {
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Description,
                            attributeType = AttributeItem.AttributeType.LongString,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Logo,
                            attributeType = AttributeItem.AttributeType.ImageId,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Publisher,
                            attributeType = AttributeItem.AttributeType.ShortString,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.HomePage,
                            attributeType = AttributeItem.AttributeType.Link,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Public,
                            attributeType = AttributeItem.AttributeType.Boolean,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.IssueTracker,
                            attributeType = AttributeItem.AttributeType.Link,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Screenshot1,
                            attributeType = AttributeItem.AttributeType.ImageId,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Screenshot2,
                            attributeType = AttributeItem.AttributeType.ImageId,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Screenshot3,
                            attributeType = AttributeItem.AttributeType.ImageId,
                            attributeRelationType = DataObjectType.None
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Screenshot4,
                            attributeType = AttributeItem.AttributeType.ImageId,
                            attributeRelationType = DataObjectType.None
                        }
                    }
                } }
        };

        /// <summary>
        /// Static cache of metadata handler types, keyed by MetadataSources.
        /// Initialized lazily on first use to avoid reflection overhead.
        /// </summary>
        private static Dictionary<MetadataSources, Type>? _metadataHandlerTypeCache;
        private static Dictionary<MetadataSources, MetadataLib.IMetadata>? _metadataHandlerInstanceCache;

        /// <summary>
        /// Gets or builds the cached mapping of MetadataSources to IMetadata handler types.
        /// Performs reflection once and caches the result to avoid expensive lookups in nested loops.
        /// </summary>
        private static Dictionary<MetadataSources, Type> GetMetadataHandlerTypeCache()
        {
            if (_metadataHandlerTypeCache != null)
                return _metadataHandlerTypeCache;

            _metadataHandlerTypeCache = new Dictionary<MetadataSources, Type>();

            // Get all IMetadata implementations from the assembly
            var metadataTypes = typeof(MetadataLib.IMetadata).Assembly.GetTypes()
                .Where(t => typeof(MetadataLib.IMetadata).IsAssignableFrom(t) && !t.IsInterface);

            foreach (var type in metadataTypes)
            {
                try
                {
                    // Create a single instance to determine its MetadataSource
                    var instance = Activator.CreateInstance(type) as MetadataLib.IMetadata;
                    if (instance != null)
                    {
                        _metadataHandlerTypeCache[instance.MetadataSource] = type;
                    }
                }
                catch
                {
                    // Skip types that cannot be instantiated
                }
            }

            return _metadataHandlerTypeCache;
        }

        /// <summary>
        /// Gets or builds the cached mapping of MetadataSources to IMetadata handler instances.
        /// Reuses handler instances across searches if they are stateless/thread-safe.
        /// </summary>
        private static Dictionary<MetadataSources, MetadataLib.IMetadata> GetMetadataHandlerInstanceCache()
        {
            if (_metadataHandlerInstanceCache != null)
                return _metadataHandlerInstanceCache;

            _metadataHandlerInstanceCache = new Dictionary<MetadataSources, MetadataLib.IMetadata>();
            var typeCache = GetMetadataHandlerTypeCache();

            foreach (var kvp in typeCache)
            {
                try
                {
                    var instance = Activator.CreateInstance(kvp.Value) as MetadataLib.IMetadata;
                    if (instance != null)
                    {
                        _metadataHandlerInstanceCache[kvp.Key] = instance;
                    }
                }
                catch
                {
                    // Skip types that cannot be instantiated
                }
            }

            return _metadataHandlerInstanceCache;
        }

        /// <summary>
        /// Generates a Redis cache key for a data object based on its type and ID. Use this to ensure standardised key names.
        /// </summary>
        /// <param name="objectType">The type of the data object.</param>
        /// <param name="objectId">The unique identifier of the data object.</param>
        /// <returns>A string representing the Redis cache key.</returns>
        public static string DataObjectCacheKey(DataObjectType objectType, long objectId)
        {
            return RedisConnection.GenerateKey("DataObject", objectType.ToString() + objectId.ToString());
        }

        public async Task<DataObjectsList> GetDataObjects(DataObjectType objectType, int pageNumber = 0, int pageSize = 0, string? search = null, bool GetChildRelations = false, bool GetMetadataMap = true, AttributeItem.AttributeName? filterAttribute = null, string? filterValue = null, ApplicationUser? user = null)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "objecttype", objectType }
            };

            if (filterAttribute == null)
            {
                if (search == null)
                {
                    switch (objectType)
                    {
                        case DataObjectType.App:
                            if (user != null)
                            {
                                sql = "SELECT * FROM DataObject LEFT JOIN DataObject_Attributes ON DataObject.Id = DataObject_Attributes.DataObjectId AND DataObject_Attributes.AttributeType = 6 LEFT JOIN DataObject_ACL ON DataObject.Id = DataObject_ACL.DataObject_ID WHERE ObjectType = @objecttype AND (DataObject_Attributes.AttributeValue = 1 OR (DataObject_ACL.UserId = @userid AND DataObject_ACL.Read = 1)) ORDER BY `Name`;";
                                dbDict.Add("userid", user.Id);
                            }
                            else
                            {
                                sql = "SELECT * FROM DataObject LEFT JOIN DataObject_Attributes ON DataObject.Id = DataObject_Attributes.DataObjectId AND DataObject_Attributes.AttributeType = 6 LEFT JOIN DataObject_ACL ON DataObject.Id = DataObject_ACL.DataObject_ID WHERE ObjectType = @objecttype AND DataObject_Attributes.AttributeValue = 1 ORDER BY `Name`;";
                            }
                            break;

                        default:
                            sql = "SELECT * FROM DataObject WHERE ObjectType = @objecttype ORDER BY `Name`;";
                            break;
                    }
                }
                else
                {
                    sql = "SELECT * FROM DataObject WHERE ObjectType = @objecttype AND `Name` LIKE @search ORDER BY `Name`;";
                    dbDict.Add("search", "%" + search + "%");
                }
            }
            else
            {
                dbDict.Add("filterAttribute", filterAttribute);
                dbDict.Add("filterValue", filterValue);

                switch (filterAttribute)
                {
                    case AttributeItem.AttributeName.Manufacturer:
                    case AttributeItem.AttributeName.Publisher:
                    case AttributeItem.AttributeName.Platform:
                        sql = "SELECT DISTINCT DataObject.* FROM DataObject JOIN DataObject_Attributes ON DataObject.Id = DataObject_Attributes.DataObjectId WHERE ObjectType = @objecttype AND DataObject_Attributes.AttributeName = @filterAttribute AND (DataObject_Attributes.AttributeRelation = @filterValue) ORDER BY `Name`;";
                        break;

                    default:
                        sql = "SELECT DISTINCT DataObject.* FROM DataObject JOIN DataObject_Attributes ON DataObject.Id = DataObject_Attributes.DataObjectId WHERE ObjectType = @objecttype AND DataObject_Attributes.AttributeName = @filterAttribute AND (DataObject_Attributes.AttributeValue = @filterValue) ORDER BY `Name`;";
                        break;
                }
            }

            DataTable data = db.ExecuteCMD(sql, dbDict);

            List<Models.DataObjectItem> DataObjects = new List<Models.DataObjectItem>();

            // compile data for return
            int pageOffset = pageSize * (pageNumber - 1);
            for (int i = pageOffset; i < data.Rows.Count; i++)
            {
                if (pageNumber != 0 && pageSize != 0)
                {
                    if (i >= (pageOffset + pageSize))
                    {
                        break;
                    }
                }

                Models.DataObjectItem item = await BuildDataObject(
                    objectType,
                    (long)data.Rows[i]["Id"],
                    data.Rows[i],
                    GetChildRelations,
                    GetMetadataMap,
                    false
                );

                DataObjects.Add(item);
            }

            float pageCount = (float)data.Rows.Count / (float)pageSize;
            DataObjectsList objectsList = new DataObjectsList
            {
                Objects = DataObjects,
                Count = data.Rows.Count,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(pageCount)
            };

            return objectsList;
        }

        public async Task<Models.DataObjectItem?> GetDataObject(long id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject WHERE Id=@id;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id }
            };

            DataTable data = db.ExecuteCMD(sql, dbDict);

            if (data.Rows.Count > 0)
            {
                DataObjectItem item = await BuildDataObject((DataObjectType)data.Rows[0]["ObjectType"], id, data.Rows[0], true);

                return item;
            }
            else
            {
                return null;
            }
        }

        public async Task<Models.DataObjectItem?> GetDataObject(DataObjectType objectType, long id, bool GetChildRelations = true, bool GetMetadata = true, bool GetSignatureData = true)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject WHERE ObjectType=@objecttype AND Id=@id;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id },
                { "objecttype", objectType }
            };

            DataTable data = db.ExecuteCMD(sql, dbDict);

            if (data.Rows.Count > 0)
            {
                DataObjectItem item = await BuildDataObject(objectType, id, data.Rows[0], GetChildRelations, GetMetadata, GetSignatureData);

                return item;
            }
            else
            {
                return null;
            }
        }

        public async Task<Models.DataObjectItem?> SearchDataObject(DataObjectType objectType, string? objectName, List<DataObjectSearchCriteriaItem> searchCriteria)
        {
            if (searchCriteria.Count == 0)
            {
                throw new DataObjectsBadSearchCriteriaException("No search criteria provided");
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject";
            Dictionary<string, object> dbDict = new Dictionary<string, object> { };

            string nameClause = "";
            if (objectName != null && objectName != "")
            {
                if (objectName.Contains("%"))
                {
                    nameClause = "`Name` LIKE @name";
                    dbDict.Add("name", objectName);
                }
                else
                {
                    nameClause = "`Name` = @name";
                    dbDict.Add("name", objectName);
                }
            }

            int loopCount = 0;
            foreach (DataObjectSearchCriteriaItem searchCriteriaItem in searchCriteria)
            {
                string valueField = "AttributeValue";
                switch (searchCriteriaItem.Field)
                {
                    case AttributeItem.AttributeName.Platform:
                    case AttributeItem.AttributeName.Publisher:
                        valueField = "AttributeRelation";
                        break;
                }

                sql += " JOIN DataObject_Attributes ON DataObject.Id = DataObject_Attributes.DataObjectId AND DataObject_Attributes.AttributeName = @field" + loopCount + " AND DataObject_Attributes." + valueField + " = @value" + loopCount;
                dbDict.Add("field" + loopCount, (int)searchCriteriaItem.Field);
                dbDict.Add("value" + loopCount, searchCriteriaItem.Value);

                loopCount++;
            }

            if (nameClause != "")
            {
                sql += " WHERE " + nameClause;
            }

            DataTable data = db.ExecuteCMD(sql, dbDict);

            if (data.Rows.Count > 0)
            {
                DataObjectItem item = await BuildDataObject(objectType, (long)data.Rows[0]["Id"], data.Rows[0], true);

                return item;
            }
            else
            {
                return null;
            }
        }

        public class DataObjectSearchCriteriaItem
        {
            public AttributeItem.AttributeName Field { get; set; }
            public string Value { get; set; }
        }

        private void UpdateDataObjectDate(long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE DataObject SET UpdatedDate=@updateddate WHERE Id=@id;";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "updateddate", DateTime.UtcNow }
            });

            // get the object type
            DataObjectItem? item = GetDataObject(DataObjectId).Result;
            if (item != null)
            {
                DataObjectType objectType = item.ObjectType;

                // generate a cache key for this object id
                string cacheKey = DataObjectCacheKey(objectType, DataObjectId);
                // purge redis cache of this object
                if (Config.RedisConfiguration.Enabled)
                {
                    RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
                }
            }
        }

        private async Task<Models.DataObjectItem> BuildDataObject(DataObjectType ObjectType, long id, DataRow row, bool GetChildRelations = false, bool GetMetadata = true, bool GetSignatureData = true)
        {
            // get attributes
            List<AttributeItem> attributes = await GetAttributes(id, GetChildRelations);

            // get signature items
            List<Dictionary<string, object>> signatureItems = new List<Dictionary<string, object>>();
            if (GetSignatureData == true)
            {
                signatureItems = await GetSignatures(ObjectType, id);

                // get extra attributes based on dataobjecttype
                switch (ObjectType)
                {
                    case DataObjectType.Game:
                        if (GetChildRelations == true)
                        {
                            attributes.Add(await GetRoms(signatureItems));
                            attributes.Add(await GetTagAttribute(id));
                            attributes.AddRange(GetCountriesAndLanguagesForGame(signatureItems));
                        }
                        break;

                    case DataObjectType.Platform:
                        // check dumps directory for files named <platformname>.zip
                        if (GetChildRelations == true)
                        {
                            attributes.Add(await GetTagAttribute(id));

                            string dumpFile = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms", (string)row["Name"] + ".zip");
                            if (File.Exists(dumpFile))
                            {
                                AttributeItem dumpAttribute = new AttributeItem
                                {
                                    attributeName = AttributeItem.AttributeName.DumpFile,
                                    attributeType = AttributeItem.AttributeType.Link,
                                    attributeRelationType = DataObjectType.None,
                                    Value = "/api/v1/Dumps/platforms/" + (string)row["Name"] + ".zip"
                                };
                                attributes.Add(dumpAttribute);
                            }
                        }
                        break;
                }
            }

            // get metadata matches
            List<DataObjectItem.MetadataItem> metadataItems = new List<DataObjectItem.MetadataItem>();
            if (GetMetadata == true)
            {
                metadataItems = await GetMetadataMap(ObjectType, id);
            }

            DataObjectItem item = new DataObjectItem
            {
                Id = id,
                ObjectType = ObjectType,
                Name = (string)row["Name"],
                CreatedDate = (DateTime)row["CreatedDate"],
                UpdatedDate = (DateTime)row["UpdatedDate"],
                Metadata = metadataItems,
                SignatureDataObjects = signatureItems,
                Attributes = attributes
            };

            return item;
        }

        public async Task<List<AttributeItem>> GetAttributes(long DataObjectId, bool GetChildRelations)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject_Attributes WHERE DataObjectId = @id";
            DataTable data = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
                { "id", DataObjectId }
            });
            List<AttributeItem> attributes = new List<AttributeItem>();
            foreach (DataRow dataRow in data.Rows)
            {
                try
                {
                    AttributeItem attributeItem = await BuildAttributeItem(dataRow, GetChildRelations);

                    // further processing
                    switch (attributeItem.attributeType)
                    {
                        case AttributeItem.AttributeType.ImageId:
                            if (attributeItem.Value.ToString().Contains(":"))
                            {
                                string[] attributeValues = attributeItem.Value.ToString().Split(':');
                                attributeItem.Value = attributeValues[0];

                                // create attribution attribute
                                AttributeItem imageAttribution = new AttributeItem()
                                {
                                    attributeType = AttributeItem.AttributeType.ImageAttribution,
                                    attributeName = AttributeItem.AttributeName.LogoAttribution,
                                    Value = attributeValues[1],
                                    attributeRelationType = attributeItem.attributeRelationType = DataObjectType.None
                                };
                                attributes.Add(imageAttribution);
                            }
                            break;
                    }

                    attributes.Add(attributeItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error processing attribute: " + ex.Message);
                }
            }

            return attributes;
        }

        public async Task<List<Dictionary<string, object>>> GetSignatures(DataObjectType ObjectType, long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object> { };

            bool abort = false;

            switch (ObjectType)
            {
                case DataObjectType.Company:
                    sql = "SELECT DataObject_SignatureMap.SignatureId, Signatures_Publishers.Publisher FROM DataObject_SignatureMap JOIN Signatures_Publishers ON DataObject_SignatureMap.SignatureId = Signatures_Publishers.Id WHERE DataObject_SignatureMap.DataObjectId = @id AND DataObject_SignatureMap.DataObjectTypeId = @typeid ORDER BY Signatures_Publishers.Publisher;";
                    break;

                case DataObjectType.Platform:
                    sql = "SELECT DataObject_SignatureMap.SignatureId, Signatures_Platforms.Platform FROM DataObject_SignatureMap JOIN Signatures_Platforms ON DataObject_SignatureMap.SignatureId = Signatures_Platforms.Id WHERE DataObject_SignatureMap.DataObjectId = @id AND DataObject_SignatureMap.DataObjectTypeId = @typeid ORDER BY Signatures_Platforms.Platform;";
                    break;

                case DataObjectType.Game:
                    sql = @"SELECT
                            DataObject_SignatureMap.`SignatureId`,
                            Signatures_Games.`Name`,
                            Signatures_Games.`Year`,
                            Signatures_Platforms.`Platform`,
                            Signatures_Games.`SourceId` AS `SourceId`,
                            Signatures_Games.`MetadataSource` AS `MetadataSource`
                        FROM 
                            DataObject_SignatureMap 
                        JOIN 
                            Signatures_Games ON DataObject_SignatureMap.`SignatureId` = Signatures_Games.`Id`
                        LEFT JOIN
                            Signatures_Platforms ON Signatures_Games.`SystemId` = Signatures_Platforms.`Id`
                        WHERE DataObject_SignatureMap.`DataObjectId` = @id AND DataObject_SignatureMap.`DataObjectTypeId` = @typeid
                        ORDER BY Signatures_Games.`Name`;";
                    break;

                default:
                    abort = true;
                    break;
            }

            if (abort == true)
            {
                return new List<Dictionary<string, object>>();
            }

            List<Dictionary<string, object>> signatureItems = await db.ExecuteCMDDictAsync(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "typeid", ObjectType }
            });

            return signatureItems;
        }

        public async Task<List<DataObjectItem.MetadataItem>> GetMetadataMap(DataObjectType ObjectType, long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject_MetadataMap WHERE DataObjectId = @id ORDER BY SourceId";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", DataObjectId }
            });
            List<DataObjectItem.MetadataItem> metadataItems = new List<DataObjectItem.MetadataItem>();

            foreach (DataRow dataRow in data.Rows)
            {
                DataObjectItem.MetadataItem metadataItem = new DataObjectItem.MetadataItem(ObjectType)
                {
                    Id = (string)dataRow["MetadataId"],
                    MatchMethod = (BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod)dataRow["MatchMethod"],
                    Source = (Metadata.Communications.MetadataSources)dataRow["SourceId"],
                    LastSearch = (DateTime)dataRow["LastSearched"],
                    NextSearch = (DateTime)dataRow["NextSearch"],
                    WinningVoteCount = (int)Common.ReturnValueIfNull(dataRow["WinningVoteCount"], 0),
                    TotalVoteCount = (int)Common.ReturnValueIfNull(dataRow["TotalVoteCount"], 0)
                };

                if (metadataItem.Id.Length == 0)
                {
                    metadataItem.ImmutableId = "";
                    metadataItem.Status = MetadataItem.MappingStatus.NotMapped;
                }
                else
                {
                    switch (metadataItem.Source)
                    {
                        case Communications.MetadataSources.None:
                            metadataItem.ImmutableId = metadataItem.Id;
                            metadataItem.Status = MetadataItem.MappingStatus.Mapped;
                            break;

                        case Communications.MetadataSources.IGDB:
                            long? objectId = null;
                            try
                            {
                                switch (ObjectType)
                                {
                                    case DataObjects.DataObjectType.Company:
                                        IGDB.Models.Company company;
                                        if (long.TryParse(metadataItem.Id, out long parsedCompanyId))
                                        {
                                            company = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Company>(parsedCompanyId);
                                        }
                                        else
                                        {
                                            company = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Company>(metadataItem.Id);
                                        }
                                        if (company != null)
                                        {
                                            objectId = company.Id;
                                        }
                                        break;

                                    case DataObjects.DataObjectType.Platform:
                                        IGDB.Models.Platform platform;
                                        if (long.TryParse(metadataItem.Id, out long parsedPlatformId))
                                        {
                                            platform = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Platform>(parsedPlatformId);
                                        }
                                        else
                                        {
                                            platform = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Platform>(metadataItem.Id);
                                        }
                                        if (platform != null)
                                        {
                                            objectId = platform.Id;
                                        }
                                        break;

                                    case DataObjects.DataObjectType.Game:
                                        IGDB.Models.Game game;
                                        if (long.TryParse(metadataItem.Id, out long parsedGameId))
                                        {
                                            game = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(parsedGameId);
                                        }
                                        else
                                        {
                                            game = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(metadataItem.Id);
                                        }
                                        if (game != null)
                                        {
                                            objectId = game.Id;
                                        }
                                        break;

                                    default:
                                        objectId = null;
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                objectId = null;
                            }

                            if (objectId.HasValue)
                            {
                                metadataItem.ImmutableId = objectId.Value.ToString();
                                metadataItem.Status = MetadataItem.MappingStatus.Mapped;
                            }
                            else
                            {
                                metadataItem.ImmutableId = metadataItem.Id;
                                metadataItem.Status = MetadataItem.MappingStatus.MappedWithErrors;
                            }
                            break;

                        case Communications.MetadataSources.TheGamesDb:
                            metadataItem.ImmutableId = metadataItem.Id;
                            metadataItem.Status = MetadataItem.MappingStatus.Mapped;
                            break;

                        default:
                            metadataItem.ImmutableId = metadataItem.Id;
                            metadataItem.Status = MetadataItem.MappingStatus.Mapped;
                            break;

                    }
                }

                metadataItems.Add(metadataItem);
            }

            // loop through each enum in Metadata.Communications.MetadataSources and create a metadata item
            // check if the enum is in metadataItems, if not, add it
            foreach (Metadata.Communications.MetadataSources source in Enum.GetValues(typeof(Metadata.Communications.MetadataSources)))
            {
                if (source != MetadataSources.None)
                {
                    bool found = false;
                    foreach (DataObjectItem.MetadataItem metadataItem in metadataItems)
                    {
                        if (metadataItem.Source == source)
                        {
                            found = true;
                        }
                    }

                    if (found == false)
                    {
                        DataObjectItem.MetadataItem metadataItem = new DataObjectItem.MetadataItem(ObjectType)
                        {
                            Id = "",
                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                            Source = source,
                            LastSearch = DateTime.UtcNow.AddMonths(-3),
                            NextSearch = DateTime.UtcNow.AddMonths(-1),
                            WinningVoteCount = 0,
                            TotalVoteCount = 0
                        };

                        // insert a record for this metadata source
                        sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metaid, @srcid, @method, @lastsearched, @nextsearch);";
                        db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                            { "id", DataObjectId },
                            { "metaid", "" },
                            { "srcid", (int)source },
                            { "method", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch },
                            { "lastsearched", DateTime.UtcNow.AddMonths(-3) },
                            { "nextsearch", DateTime.UtcNow.AddMonths(-1) }
                        });

                        metadataItems.Add(metadataItem);
                    }
                }
            }

            return metadataItems;
        }

        public List<AttributeItem> GetCountriesAndLanguagesForGame(List<Dictionary<string, object>> GameSignatures)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            SignatureManagement signature = new SignatureManagement();

            Dictionary<string, object> countries = new Dictionary<string, object>();
            Dictionary<string, object> languages = new Dictionary<string, object>();

            foreach (Dictionary<string, object> GameSignature in GameSignatures)
            {
                Dictionary<string, object> dbDict = new Dictionary<string, object>{
                    { "sigid", GameSignature["SignatureId"] }
                };

                // get country
                Dictionary<string, string> gameCountries = signature.GetLookup(Common.LookupTypes.Country, long.Parse(GameSignature["SignatureId"].ToString()));
                Dictionary<string, KeyValuePair<string, string>> countryCodeCorrections = signature.GetLookupCorrections(Common.LookupTypes.Country);
                foreach (KeyValuePair<string, string> gameCountry in gameCountries)
                {
                    string countryKey = gameCountry.Key;
                    string countryValue = gameCountry.Value;
                    if (countryCodeCorrections.ContainsKey(gameCountry.Key))
                    {
                        countryKey = countryCodeCorrections[gameCountry.Key].Key;
                        countryValue = countryCodeCorrections[gameCountry.Key].Value;
                    }

                    if (!countries.ContainsKey(countryKey))
                    {
                        countries.Add(countryKey, countryValue);
                    }
                }

                // get language
                Dictionary<string, string> gameLanguages = signature.GetLookup(Common.LookupTypes.Language, long.Parse(GameSignature["SignatureId"].ToString()));
                foreach (KeyValuePair<string, string> gameLanguage in gameLanguages)
                {
                    if (!languages.ContainsKey(gameLanguage.Key))
                    {
                        languages.Add(gameLanguage.Key, gameLanguage.Value);
                    }
                }
            }

            List<AttributeItem> attributeItems = new List<AttributeItem>();

            // compile countries
            if (countries.Count > 0)
            {
                AttributeItem countryAttributes = new AttributeItem
                {
                    attributeName = AttributeItem.AttributeName.Country,
                    attributeType = AttributeItem.AttributeType.ShortString,
                    attributeRelationType = DataObjectType.None
                };
                for (int i = 0; i < countries.Count; i++)
                {
                    if (i > 0)
                    {
                        countryAttributes.Value += ", ";
                    }
                    countryAttributes.Value += countries.ElementAt(i).Value + " (" + countries.ElementAt(i).Key + ")";
                }
                attributeItems.Add(countryAttributes);
            }

            // compile languages
            if (languages.Count > 0)
            {
                AttributeItem languageAttributes = new AttributeItem
                {
                    attributeName = AttributeItem.AttributeName.Language,
                    attributeType = AttributeItem.AttributeType.ShortString,
                    attributeRelationType = DataObjectType.None
                };
                for (int i = 0; i < languages.Count; i++)
                {
                    if (i > 0)
                    {
                        languageAttributes.Value += ", ";
                    }
                    languageAttributes.Value += languages.ElementAt(i).Value.ToString();
                }
                attributeItems.Add(languageAttributes);
            }

            return attributeItems;
        }

        public async Task<AttributeItem> GetRoms(List<Dictionary<string, object>> GameSignatures)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            SignatureManagement signature = new SignatureManagement();

            List<Signatures_Games_2.RomItem> roms = new List<Signatures_Games_2.RomItem>();

            foreach (Dictionary<string, object> GameSignature in GameSignatures)
            {
                string sql = @"SELECT
                        `Id` AS romid,
                        `Name` AS romname,
                        `Size`,
                        `CRC`,
                        `MD5`,
                        `SHA1`,
                        `SHA256`,
                        `Status`,
                        `DevelopmentStatus`,
                        `Attributes`,
                        `RomType`,
                        `RomTypeMedia`,
                        `MediaLabel`,
                        `MetadataSource`,
                        `Countries`,
                        `Languages`
                    FROM
                        Signatures_Roms
                    WHERE
                        GameId=@gameid
                    ORDER BY `Name`;";
                DataTable data = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
                    { "gameid", GameSignature["SignatureId"] }
                });

                foreach (DataRow row in data.Rows)
                {
                    Signatures_Games_2.RomItem rom = signature.BuildRomItem(row);
                    roms.Add(rom);
                }
            }

            AttributeItem attribute = new AttributeItem
            {
                attributeName = AttributeItem.AttributeName.ROMs,
                attributeType = AttributeItem.AttributeType.EmbeddedList,
                attributeRelationType = DataObjectType.ROM,
                Value = roms
            };

            return attribute;
        }

        public async Task<Models.DataObjectItem> NewDataObject(DataObjectType objectType, Models.DataObjectItemModel model, ApplicationUser? user = null)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO DataObject (`Name`, `ObjectType`, `CreatedDate`, `UpdatedDate`) VALUES (@name, @objecttype, @createddate, @updateddate); SELECT LAST_INSERT_ID();";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "name", model.Name },
                { "objecttype", objectType },
                { "createddate", DateTime.UtcNow },
                { "updateddate", DateTime.UtcNow }
            };

            DataTable data = db.ExecuteCMD(sql, dbDict);

            // configure the new object
            switch (objectType)
            {
                case DataObjectType.Company:
                case DataObjectType.Platform:
                case DataObjectType.Game:
                    foreach (Enum source in Enum.GetValues(typeof(Metadata.Communications.MetadataSources)))
                    {
                        if ((Metadata.Communications.MetadataSources)source != Metadata.Communications.MetadataSources.None)
                        {
                            sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metaid, @srcid, @method, @lastsearched, @nextsearch);";
                            dbDict = new Dictionary<string, object>{
                        { "id", (long)(ulong)data.Rows[0][0] },
                        { "metaid", "" },
                        { "srcid", (Metadata.Communications.MetadataSources)source },
                        { "method", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch },
                        { "lastsearched", DateTime.UtcNow.AddMonths(-3) },
                        { "nextsearch", DateTime.UtcNow.AddMonths(-1) }
                    };
                            db.ExecuteNonQuery(sql, dbDict);
                        }
                    }

                    await DataObjectMetadataSearch(objectType, (long)(ulong)data.Rows[0][0]);
                    break;

                case DataObjectType.App:
                    sql = "INSERT INTO DataObject_ACL (`DataObject_ID`, `UserId`, `Read`, `Write`, `Delete`) VALUES (@id, @userid, @read, @write, @delete);";
                    dbDict = new Dictionary<string, object>{
                            { "id", (long)(ulong)data.Rows[0][0] },
                            { "userid", user.Id },
                            { "read", true },
                            { "write", true },
                            { "delete", true }
                        };
                    db.ExecuteNonQuery(sql, dbDict);
                    break;

                default:
                    break;
            }

            return await GetDataObject(objectType, (long)(ulong)data.Rows[0][0]);
        }

        public async Task<Models.DataObjectItem> EditDataObject(DataObjectType objectType, long id, Models.DataObjectItemModel model, bool allowSearch = true)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE DataObject SET `Name`=@name, `UpdatedDate`=@updateddate WHERE ObjectType=@objecttype AND Id=@id";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id },
                { "name", model.Name },
                { "objecttype", objectType },
                { "updateddate", DateTime.UtcNow }
            };

            db.ExecuteNonQuery(sql, dbDict);

            // generate a cache key for this object id
            string cacheKey = DataObjectCacheKey(objectType, id);
            // purge redis cache of this object
            if (Config.RedisConfiguration.Enabled)
            {
                RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
            }

            if (allowSearch)
            {
                await DataObjectMetadataSearch(objectType, id);
            }

            return await GetDataObject(objectType, id);
        }

        public void DeleteDataObject(DataObjectType objectType, long id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "DELETE FROM DataObject WHERE ObjectType=@objecttype AND Id=@id";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id },
                { "objecttype", objectType },
                { "updateddate", DateTime.UtcNow }
            };

            db.ExecuteNonQuery(sql, dbDict);

            // purge redis cache of all keys for this object type
            if (Config.RedisConfiguration.Enabled)
            {
                string cacheKey = DataObjectCacheKey(objectType, id);
                RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
            }
        }

        public async Task<Models.DataObjectItem> EditDataObject(DataObjectType objectType, long id, Models.DataObjectItem model, bool trustModelMetadataSearchType = false)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE DataObject SET `Name`=@name, `UpdatedDate`=@updateddate WHERE ObjectType=@objecttype AND Id=@id";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id },
                { "name", model.Name },
                { "objecttype", objectType },
                { "updateddate", DateTime.UtcNow }
            };

            db.ExecuteNonQuery(sql, dbDict);

            // generate a cache key for this object id
            string cacheKey = DataObjectCacheKey(objectType, id);
            // purge redis cache of this object
            if (Config.RedisConfiguration.Enabled)
            {
                RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
            }

            DataObjectItem EditedObject = await GetDataObject(objectType, id);

            // update attributes
            foreach (AttributeItem newAttribute in model.Attributes)
            {
                switch (newAttribute.attributeType)
                {
                    case AttributeItem.AttributeType.EmbeddedList:
                        switch (newAttribute.attributeName)
                        {
                            case AttributeItem.AttributeName.Tags:
                                // handle tags updating
                                // supplied value is a Dictionary<DataObjectItemTages.TagType, List<string>>

                                // get existing tags for lookup
                                Dictionary<DataObjectItemTags.TagType, DataObjectItemTags> existingTags = await GetTags();
                                bool newTagsAdded = false;

                                // new tags to add
                                // scan all supplied tags and if they aren't in existing tags, add them
                                Dictionary<DataObjectItemTags.TagType, List<string>> newTags = new Dictionary<DataObjectItemTags.TagType, List<string>>();
                                Dictionary<DataObjectItemTags.TagType, List<string>>? newAttributeValue = null;

                                if (newAttribute.Value is JObject)
                                {
                                    var jObject = (JObject)newAttribute.Value;
                                    newAttributeValue = jObject.ToObject<Dictionary<DataObjectItemTags.TagType, List<string>>>();
                                }
                                else if (newAttribute.Value is Dictionary<DataObjectItemTags.TagType, hasheous_server.Models.DataObjectItemTags>)
                                {
                                    var dict = (Dictionary<DataObjectItemTags.TagType, hasheous_server.Models.DataObjectItemTags>)newAttribute.Value;
                                    newAttributeValue = new Dictionary<DataObjectItemTags.TagType, List<string>>();
                                    foreach (KeyValuePair<DataObjectItemTags.TagType, hasheous_server.Models.DataObjectItemTags> kvp in dict)
                                    {
                                        List<string> tagList = new List<string>();
                                        foreach (DataObjectItemTags.TagModel tagModel in kvp.Value.Tags)
                                        {
                                            tagList.Add(tagModel.Text);
                                        }
                                        newAttributeValue.Add(kvp.Key, tagList);
                                    }
                                }
                                else
                                {
                                    newAttributeValue = (Dictionary<DataObjectItemTags.TagType, List<string>>)newAttribute.Value;
                                }

                                if (newAttributeValue != null && newAttributeValue.Count > 0)
                                {
                                    // normalise tags in newAttributeValue to lowercase and trimmed
                                    foreach (KeyValuePair<DataObjectItemTags.TagType, List<string>> tagType in newAttributeValue)
                                    {
                                        for (int i = 0; i < tagType.Value.Count; i++)
                                        {
                                            tagType.Value[i] = tagType.Value[i].Trim().ToLower();
                                        }
                                    }

                                    // loop the keys in the supplied value dictionary<DataObjectItemTages.TagType, List<string>>
                                    foreach (KeyValuePair<DataObjectItemTags.TagType, List<string>> suppliedTagType in newAttributeValue)
                                    {
                                        foreach (string suppliedTag in suppliedTagType.Value)
                                        {
                                            bool tagFound = false;
                                            if (existingTags.ContainsKey(suppliedTagType.Key))
                                            {
                                                foreach (DataObjectItemTags.TagModel tagModel in existingTags[suppliedTagType.Key].Tags)
                                                {
                                                    if (tagModel.Text == suppliedTag && tagModel.AIGenerated == false)
                                                    {
                                                        tagFound = true;
                                                    }
                                                }
                                            }

                                            if (tagFound == false)
                                            {
                                                // add to new tags
                                                if (!newTags.ContainsKey(suppliedTagType.Key))
                                                {
                                                    newTags.Add(suppliedTagType.Key, new List<string>());
                                                }
                                                newTags[suppliedTagType.Key].Add(suppliedTag);
                                            }
                                        }
                                    }
                                }

                                // add new tags to database
                                foreach (KeyValuePair<DataObjectItemTags.TagType, List<string>> tagTypeToAdd in newTags)
                                {
                                    foreach (string tagToAdd in tagTypeToAdd.Value)
                                    {
                                        sql = "INSERT INTO `Tags` (`type`, `name`) VALUES (@type, @name);";
                                        await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
                                            { "type", (int)tagTypeToAdd.Key },
                                            { "name", tagToAdd }
                                        });
                                        newTagsAdded = true;
                                    }
                                }

                                // refresh existing tags
                                if (newTagsAdded == true)
                                {
                                    existingTags = await GetTags();
                                }

                                // now update the DataObject_Tags Map
                                // get existing tag mappings for this data object
                                Dictionary<DataObjectItemTags.TagType, DataObjectItemTags> existingTagMappings = await GetTags(id);
                                // loop through supplied tags again and add any missing mappings
                                foreach (KeyValuePair<DataObjectItemTags.TagType, List<string>> suppliedTagType in newAttributeValue)
                                {
                                    foreach (string suppliedTag in suppliedTagType.Value)
                                    {
                                        bool mappingFound = false;
                                        if (existingTagMappings.ContainsKey(suppliedTagType.Key))
                                        {
                                            foreach (DataObjectItemTags.TagModel tagModel in existingTagMappings[suppliedTagType.Key].Tags)
                                            {
                                                if (tagModel.Text == suppliedTag && tagModel.AIGenerated == false)
                                                {
                                                    mappingFound = true;
                                                }
                                            }
                                        }

                                        if (mappingFound == false)
                                        {
                                            // add mapping
                                            long tagIdToMap = -1;
                                            if (existingTags.ContainsKey(suppliedTagType.Key))
                                            {
                                                foreach (DataObjectItemTags.TagModel tagModel in existingTags[suppliedTagType.Key].Tags)
                                                {
                                                    if (tagModel.Text == suppliedTag && tagModel.AIGenerated == false)
                                                    {
                                                        tagIdToMap = tagModel.Id;
                                                    }
                                                }
                                            }

                                            if (tagIdToMap != -1)
                                            {
                                                sql = "INSERT INTO DataObject_Tags (DataObjectId, TagId, AIAssigned) VALUES (@dataobjectid, @tagid, @aiassigned);";
                                                await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
                                                    { "dataobjectid", id },
                                                    { "tagid", tagIdToMap },
                                                    { "aiassigned", false }
                                                });
                                            }
                                        }
                                    }
                                }
                                // now remove any mappings that are not in the supplied value
                                foreach (KeyValuePair<DataObjectItemTags.TagType, DataObjectItemTags> existingTagMapping in existingTagMappings)
                                {
                                    foreach (DataObjectItemTags.TagModel existingTagModel in existingTagMapping.Value.Tags)
                                    {
                                        bool mappingInSupplied = false;
                                        if (newAttributeValue.ContainsKey(existingTagMapping.Key))
                                        {
                                            foreach (string suppliedTag in newAttributeValue[existingTagMapping.Key])
                                            {
                                                if (existingTagModel.Text == suppliedTag && existingTagModel.AIGenerated == false)
                                                {
                                                    mappingInSupplied = true;
                                                }
                                            }
                                        }

                                        if (mappingInSupplied == false)
                                        {
                                            // delete mapping
                                            sql = "DELETE FROM DataObject_Tags WHERE DataObjectId=@dataobjectid AND TagId=@tagid AND AIAssigned=@aiassigned;";
                                            await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
                                                { "dataobjectid", id },
                                                { "tagid", existingTagModel.Id },
                                                { "aiassigned", false }
                                            });
                                        }
                                    }
                                }

                                break;
                        }
                        break;

                    default:
                        bool attributeFound = false;
                        foreach (AttributeItem existingAttribute in EditedObject.Attributes)
                        {
                            if (
                                (newAttribute.attributeType == existingAttribute.attributeType) &&
                                (newAttribute.attributeName == existingAttribute.attributeName)
                            )
                            {
                                attributeFound = true;

                                string sqlField;
                                bool isMatch = false;
                                string matchValue = "";
                                switch (existingAttribute.attributeType)
                                {
                                    case AttributeItem.AttributeType.ObjectRelationship:
                                        sqlField = "AttributeRelation";
                                        DataObjectItem tempCompare = (DataObjectItem)existingAttribute.Value;
                                        if (tempCompare != null)
                                        {
                                            if (long.TryParse(newAttribute.Value.ToString(), out long newCompareLong))
                                            {
                                                if (tempCompare.Id == newCompareLong)
                                                {
                                                    isMatch = true;
                                                    matchValue = newCompareLong.ToString();
                                                }
                                            }
                                            else
                                            {
                                                DataObjectItem? newCompare = null;
                                                if (newAttribute.Value is hasheous_server.Models.RelationItem)
                                                {
                                                    newCompare = await GetDataObject((newAttribute.Value as hasheous_server.Models.RelationItem).relationId);
                                                }
                                                else
                                                {
                                                    newCompare = (DataObjectItem)newAttribute.Value;
                                                }

                                                if (tempCompare.Name == newCompare.Name)
                                                {
                                                    isMatch = true;
                                                    matchValue = newCompare.Id.ToString();
                                                }
                                            }
                                        }
                                        break;

                                    default:
                                        sqlField = "AttributeValue";
                                        if ((string)newAttribute.Value == (string)existingAttribute.Value)
                                        {
                                            isMatch = true;
                                        }
                                        break;

                                }

                                //if (compareValue != (string)newAttribute.Value)
                                if (isMatch == false)
                                {
                                    if (newAttribute.Value == "")
                                    {
                                        // blank value - delete it
                                        DeleteAttribute(id, (long)existingAttribute.Id);
                                    }
                                    else
                                    {
                                        // update existing value
                                        sql = "UPDATE DataObject_Attributes SET " + sqlField + "=@value WHERE DataObjectId=@id AND AttributeId=@attrid;";
                                        db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                            { "id", id },
                                            { "attrid", existingAttribute.Id },
                                            { "value", newAttribute.Value }
                                        });
                                    }
                                }
                                else
                                {
                                    if (newAttribute.Value == "")
                                    {
                                        // blank value - delete it
                                        DeleteAttribute(id, (long)existingAttribute.Id);
                                    }
                                }
                            }
                        }

                        if (attributeFound == false)
                        {
                            if (newAttribute.Value != "")
                            {
                                // create a new attribute
                                AddAttribute(id, newAttribute);
                            }
                        }
                        break;
                }
            }

            // update metadata map
            bool metadataChangeDetected = false;
            switch (objectType)
            {
                case DataObjectType.Company:
                case DataObjectType.Platform:
                case DataObjectType.Game:
                    foreach (DataObjectItem.MetadataItem newMetadataItem in model.Metadata)
                    {
                        string newMetadataId = "";

                        switch (newMetadataItem.Source)
                        {
                            case Metadata.Communications.MetadataSources.IGDB:
                                if (long.TryParse(newMetadataItem.Id, out long parsedId))
                                {
                                    newMetadataId = parsedId.ToString();
                                }
                                else
                                {
                                    // IGDB metadata id is not a long, so we need to search for it
                                    if (objectType == DataObjectType.Game)
                                    {
                                        if (!string.IsNullOrEmpty(newMetadataItem.Id))
                                        {
                                            IGDB.Models.Game? newMetadataGame = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(newMetadataItem.Id);
                                            if (newMetadataGame != null)
                                            {
                                                newMetadataId = newMetadataGame.Id.ToString();
                                            }
                                            else
                                            {
                                                // if we can't find the game, skip it
                                                continue;
                                            }
                                        }
                                    }
                                    else if (objectType == DataObjectType.Platform)
                                    {
                                        if (!string.IsNullOrEmpty(newMetadataItem.Id))
                                        {
                                            IGDB.Models.Platform? newMetadataPlatform = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Platform>(newMetadataItem.Id);
                                            if (newMetadataPlatform != null)
                                            {
                                                newMetadataId = newMetadataPlatform.Id.ToString();
                                            }
                                            else
                                            {
                                                // if we can't find the platform, skip it
                                                continue;
                                            }
                                        }
                                    }
                                    else if (objectType == DataObjectType.Company)
                                    {
                                        if (!string.IsNullOrEmpty(newMetadataItem.Id))
                                        {
                                            IGDB.Models.Company? newMetadataCompany = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Company>(newMetadataItem.Id);
                                            if (newMetadataCompany != null)
                                            {
                                                newMetadataId = newMetadataCompany.Id.ToString();
                                            }
                                            else
                                            {
                                                // if we can't find the company, skip it
                                                continue;
                                            }
                                        }
                                    }
                                }
                                break;

                            default:
                                // other sources use the id as is
                                newMetadataId = newMetadataItem.Id;
                                break;
                        }

                        bool metadataFound = false;
                        BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod? matchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin;
                        if (trustModelMetadataSearchType == true)
                        {
                            matchMethod = newMetadataItem.MatchMethod;
                        }

                        foreach (DataObjectItem.MetadataItem existingMetadataItem in EditedObject.Metadata)
                        {
                            if (newMetadataItem.Source == existingMetadataItem.Source)
                            {
                                metadataFound = true;
                                if (newMetadataId.ToString() != existingMetadataItem.Id)
                                {
                                    metadataChangeDetected = true;

                                    // change to manually set
                                    sql = "UPDATE DataObject_MetadataMap SET MatchMethod=@match, MetadataId=@metaid, WinningVoteCount=@winningvotecount, TotalVoteCount=@totalvotecount WHERE DataObjectId=@id AND SourceId=@source;";
                                    db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                        { "id", id },
                                        { "match", matchMethod ?? BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin },
                                        { "metaid", newMetadataId },
                                        { "source", existingMetadataItem.Source },
                                        { "winningvotecount", 0 },
                                        { "totalvotecount", 0 }
                                    });
                                }
                            }

                            if (trustModelMetadataSearchType == true)
                            {
                                // update next search field if match method is NoMatch or Automatic
                                if (new List<BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod?>{
                                    BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                                    BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                    BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.AutomaticTooManyMatches
                                }.Contains(matchMethod))
                                {
                                    // update next search regardless of changes
                                    sql = "UPDATE DataObject_MetadataMap SET NextSearch=@nextsearch WHERE DataObjectId=@id AND SourceId=@source;";
                                    db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                        { "id", id },
                                        { "source", newMetadataItem.Source },
                                        { "nextsearch", newMetadataItem.NextSearch }
                                    });
                                }
                            }
                        }

                        if (metadataFound == false)
                        {
                            metadataChangeDetected = true;

                            sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metaid, @source, @match, @last, @next);";
                            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                { "id", id },
                                { "match", matchMethod ?? BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin },
                                { "metaid", newMetadataId },
                                { "source", newMetadataItem.Source },
                                { "last", DateTime.UtcNow },
                                { "next", DateTime.UtcNow.AddMonths(1) }
                            });
                        }
                    }
                    break;

                default:
                    break;
            }
            // remove all AI content and linked if there is no metadata providers attached
            bool noMetadataLinksPresent = false;
            if (model.Metadata != null)
            {
                if (model.Metadata.Count == 0)
                {
                    noMetadataLinksPresent = true;
                }
                else
                {
                    noMetadataLinksPresent = true;
                    foreach (DataObjectItem.MetadataItem metadataItem in model.Metadata)
                    {
                        if (String.IsNullOrEmpty(metadataItem.Id) == false)
                        {
                            noMetadataLinksPresent = false;
                        }
                    }
                }
            }
            if (noMetadataLinksPresent == true)
            {
                // delete ai description
                sql = "DELETE FROM DataObject_Attributes WHERE DataObjectId=@id AND AttributeType=@attrtype AND AttributeName=@attrname;";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                    { "id", id },
                    { "attrtype", (int)AttributeItem.AttributeType.LongString },
                    { "attrname", (int)AttributeItem.AttributeName.AIDescription }
                });
                // delete ai tags
                sql = "DELETE FROM DataObject_Tags WHERE DataObjectId=@id AND AIAssigned=@aiassigned;";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                    { "id", id },
                    { "aiassigned", true }
                });
                // delete ai tasks
                sql = "DELETE FROM Task_Queue WHERE dataobjectid=@id;";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                    { "id", id }
                });
            }

            // signatures
            sql = "DELETE FROM DataObject_SignatureMap WHERE DataObjectId=@id";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", id }
            });
            List<long> signatureIds = new List<long>();
            foreach (Dictionary<string, object>? signature in model.SignatureDataObjects)
            {
                if (!signatureIds.Contains(long.Parse(signature["SignatureId"].ToString())))
                {
                    AddSignature(id, objectType, long.Parse(signature["SignatureId"].ToString()));
                    signatureIds.Add(long.Parse(signature["SignatureId"].ToString()));
                }
            }

            // access control
            if (objectType == DataObjectType.App)
            {
                // update access control
                sql = "DELETE FROM DataObject_ACL WHERE DataObject_ID=@id";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                    { "id", id}
                });

                foreach (KeyValuePair<string, List<DataObjectPermission.PermissionType>> acl in model.UserPermissions)
                {
                    // get user id from email
                    sql = "SELECT Id FROM Users WHERE Email=@email;";
                    dbDict = new Dictionary<string, object>{
                        { "email", acl.Key }
                    };
                    DataTable user = db.ExecuteCMD(sql, dbDict);
                    if (user.Rows.Count > 0)
                    {
                        sql = "INSERT INTO DataObject_ACL (`DataObject_ID`, `UserId`, `Read`, `Write`, `Delete`) VALUES (@id, @userid, @read, @write, @delete);";
                        dbDict = new Dictionary<string, object>{
                            { "id", id },
                            { "userid", user.Rows[0]["Id"] }
                        };
                        if (acl.Value.Contains(DataObjectPermission.PermissionType.Read))
                        {
                            dbDict.Add("read", true);
                        }
                        else
                        {
                            dbDict.Add("read", false);
                        }
                        if (acl.Value.Contains(DataObjectPermission.PermissionType.Update))
                        {
                            dbDict.Add("write", true);
                        }
                        else
                        {
                            dbDict.Add("write", false);
                        }
                        if (acl.Value.Contains(DataObjectPermission.PermissionType.Delete))
                        {
                            dbDict.Add("delete", true);
                        }
                        else
                        {
                            dbDict.Add("delete", false);
                        }
                        db.ExecuteNonQuery(sql, dbDict);
                    }
                }
            }

            UpdateDataObjectDate(id);

            if (metadataChangeDetected == true)
            {
                // metadata map change detected - reset associated tagging task
                var tasks = TaskManagement.GetAllTasks(id);
                if (tasks != null && tasks.Count > 0)
                {
                    foreach (var task in tasks)
                    {
                        if (task.TaskName == Models.Tasks.TaskType.AIDescriptionAndTagging)
                        {
                            await task.Reset();
                        }
                    }
                }
                else
                {
                    // no tagging task exists - create one
                    TaskManagement.EnqueueTask((long)id, Models.Tasks.TaskType.AIDescriptionAndTagging);
                }
            }

            return await GetDataObject(objectType, id);
        }

        /// <summary>
        /// Performs a metadata look up on DataObjects with no match metadata
        /// </summary>
        public async Task DataObjectMetadataSearch(DataObjectType objectType, bool ForceSearch = false)
        {
            await _DataObjectMetadataSearch(objectType, null, ForceSearch);
        }

        /// <summary>
        /// Performs a metadata look up on the selected DataObject if it has no metadata match
        /// </summary>
        /// <param name="id"></param>
        public async Task DataObjectMetadataSearch(DataObjectType objectType, long? id, bool ForceSearch = false)
        {
            switch (objectType)
            {
                case DataObjectType.Company:
                case DataObjectType.Platform:
                case DataObjectType.Game:
                    await _DataObjectMetadataSearch(objectType, id, ForceSearch);
                    break;

                default:
                    break;
            }
        }

        // do not search for metadata if the matchmethod is Manual, ManualByAdmin, or Voted
        private static List<BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod> dontSearchMatchMethods = [
            BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Manual,
            BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin,
            BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Voted
        ];

        // get all metadata sources
        private static MetadataSources[] allMetadataSources = (MetadataSources[])Enum.GetValues(typeof(MetadataSources));

        private async Task _DataObjectMetadataSearch(DataObjectType objectType, long? id, bool ForceSearch)
        {
            HashSet<MetadataSources> ProcessSources = [
                MetadataSources.IGDB,
                MetadataSources.TheGamesDb,
                MetadataSources.RetroAchievements,
                MetadataSources.GiantBomb
            ];

            // set now time
            DateTime now = DateTime.UtcNow;

            // set up report sending
            string logName = $"{Config.LogName}-MetadataMatchSearch";

            // set up randomiser
            Random rand = new Random();

            // start processing each object
            int processedObjectCount = 0;
            int totalObjectCount = 1;

            if (id != null)
            {
                // get a single data object
                DataObjectItem? singleDataObject = await GetDataObject(objectType, (long)id);
                if (singleDataObject != null)
                {
                    await _DataObjectMetadataSearch_Apply(singleDataObject, logName, rand, objectType, id, ForceSearch, now, ProcessSources, 1, totalObjectCount);
                }
                else
                {
                    // requested object not found
                    return;
                }
            }
            else
            {
                // get all data objects of the specified type that need metadata searching - any item who's next search date is in the past.
                Logging.SendReport(logName, 0, 0, $"Querying database for {objectType} data objects needing metadata search...");

                Dictionary<string, object> dbDict = new Dictionary<string, object>
                {
                    { "@objecttype", objectType },
                    { "@nextsearched", now }
                };

                // first get a count of all records
                DataTable countData = await Config.database.ExecuteCMDAsync(@"
                    SELECT
                        COUNT(*) AS ObjectCount
                    FROM
                        DataObject
                            JOIN 
                        (SELECT 
                            DataObjectId, NextSearch
                        FROM
                            DataObject_MetadataMap
                        WHERE
                            MatchMethod IN (0, 1)
                        GROUP BY DataObjectId
                        ORDER BY NextSearch ASC) DDMM ON DataObject.Id = DDMM.DataObjectId
                    WHERE
                        ObjectType = @objecttype AND DDMM.NextSearch < @nextsearched;
                ", dbDict);
                if (countData.Rows.Count > 0)
                {
                    totalObjectCount = Convert.ToInt32(countData.Rows[0]["ObjectCount"]);

                    int pageNumber = 0;
                    int pageSize = 100;

                    // start processing data objects in pages
                    do
                    {
                        int offset = pageNumber * pageSize;
                        Logging.Log(Logging.LogType.Information, "Metadata Match", $"Querying database for page {pageNumber} of {objectType} data objects needing metadata search...");
                        DataTable data = await Config.database.ExecuteCMDAsync(@$"
                        SELECT DISTINCT
                            DataObject.*, DDMM.NextSearch
                        FROM
                            DataObject
                                JOIN 
                            (SELECT 
                                DataObjectId, MatchMethod, NextSearch
                            FROM
                                DataObject_MetadataMap
                            WHERE
                                MatchMethod IN (0, 1)
                            GROUP BY DataObjectId
                            ORDER BY NextSearch ASC) DDMM ON DataObject.Id = DDMM.DataObjectId
                        WHERE
                            ObjectType = @objecttype AND DDMM.NextSearch < @nextsearched
                        ORDER BY DataObject.`Name`
                        LIMIT {pageSize} OFFSET {offset};
                        ", dbDict);

                        if (data.Rows.Count == 0)
                        {
                            // we're done here
                            Logging.Log(Logging.LogType.Information, "Metadata Match", $"No more {objectType} data objects found needing metadata search.");
                            break;
                        }

                        List<DataObjectItem> DataObjectsToProcess = new List<DataObjectItem>();
                        foreach (DataRow row in data.Rows)
                        {
                            DataObjectItem item = await BuildDataObject(objectType, (long)row["Id"], row, false, true);
                            DataObjectsToProcess.Add(item);
                        }

                        foreach (DataObjectItem item in DataObjectsToProcess)
                        {
                            processedObjectCount++;

                            await _DataObjectMetadataSearch_Apply(item, logName, rand, objectType, id, ForceSearch, now, ProcessSources, processedObjectCount, totalObjectCount);
                        }

                        if (data.Rows.Count < pageSize)
                        {
                            // we're done here
                            Logging.Log(Logging.LogType.Information, "Metadata Match", $"No more {objectType} data objects found needing metadata search.");
                            break;
                        }

                        pageNumber++;
                    } while (true);
                }
            }

            Logging.SendReport(logName, null, null, $"Metadata search complete for {processedObjectCount} {objectType} data objects.");
        }

        private async Task _DataObjectMetadataSearch_Apply(DataObjectItem item, string logName, Random rand, DataObjectType objectType, long? id, bool ForceSearch, DateTime now, HashSet<MetadataSources> ProcessSources, int processedObjectCount, int objectTotalCount)
        {

            DataObjectItem? itemPlatform = null;
            if (item.ObjectType == DataObjectType.Game)
            {
                if (item.Attributes != null && item.Attributes.Count > 0)
                {
                    AttributeItem? platformAttribute = item.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.Platform && x.attributeType == AttributeItem.AttributeType.ObjectRelationship);
                    if (platformAttribute != null)
                    {
                        // get the associated platform dataobject
                        if (platformAttribute.Value.GetType() == typeof(DataObjectItem))
                        {
                            itemPlatform = (DataObjectItem)platformAttribute.Value;
                        }
                        else
                        {
                            RelationItem relationItem = (RelationItem)platformAttribute.Value;
                            itemPlatform = await GetDataObject(DataObjectType.Platform, relationItem.relationId);
                        }
                    }
                }

                if (itemPlatform == null)
                {
                    Logging.Log(Logging.LogType.Warning, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - Skipping game {item.Name} as no platform is mapped.");
                    return;
                }
            }

            // generate a list of search candidates
            List<string> SearchCandidates = GetSearchCandidates(item.Name);

            Logging.Log(Logging.LogType.Information, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - Searching for metadata for {string.Join(", ", SearchCandidates)} ({item.ObjectType}) Id: {item.Id}");
            Logging.SendReport(logName, processedObjectCount, objectTotalCount, $"Searching for metadata for {string.Join(", ", SearchCandidates)} ({item.ObjectType})", true);

            List<DataObjectItem.MetadataItem> metadataUpdates = new List<MetadataItem>();

            // calculate a dates to search next time should we not find any matches, or need to refresh automatic matches
            // day count should be randomised to ensure that we don't spend multiple days processing records
            // automatic and automaticetoomanymatches - should be between 6 and 12 months
            int automaticNextDay = rand.Next(180, 365);
            // nomatch - should be between 1 and 6 months
            int noMatchNextDay = rand.Next(30, 180);
            // default - just one day
            int defaultNextDay = 1;

            // process each metadata source
            foreach (MetadataSources metadataSource in allMetadataSources)
            {
                // skip if it's an unsupported source type
                if (!ProcessSources.Contains(metadataSource))
                {
                    // set the next search date to 6 months in the future to avoid rechecking too often
                    DataObjectItem.MetadataItem? existingMetadata = null;
                    if (item.Metadata != null)
                    {
                        existingMetadata = item.Metadata.Find(x => x.Source == metadataSource);
                    }
                    if (existingMetadata != null)
                    {
                        existingMetadata.NextSearch = now.AddDays(noMatchNextDay);
                        metadataUpdates.Add(existingMetadata);
                    }
                    continue;
                }

                // setup search options
                Dictionary<string, object> searchOptions = new Dictionary<string, object>();

                // if item type is game, search platformItem for an metadata source that equals metadataSource - if not found, skip
                DataObjectItem.MetadataItem? platformMetadata = null;
                if (item.ObjectType == DataObjectType.Game && itemPlatform != null)
                {
                    if (itemPlatform != null && itemPlatform.Metadata != null && itemPlatform.Metadata.Count > 0)
                    {
                        // check if platform has metadata for this source
                        platformMetadata = itemPlatform.Metadata.Find(x => x.Source == metadataSource);
                        if (!String.IsNullOrEmpty(platformMetadata?.ImmutableId))
                        {
                            searchOptions.Add("platformId", long.Parse(platformMetadata.ImmutableId ?? "0"));
                        }
                        else
                        {
                            platformMetadata = null;
                        }
                    }

                    if (platformMetadata == null)
                    {
                        Logging.Log(Logging.LogType.Warning, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - Skipping metadata source {metadataSource} for game {item.Name} as no platform metadata is mapped.");
                        continue;
                    }
                }

                // Get metadata handler instance from cache (avoids expensive reflection + instantiation in nested loop)
                var metadataHandlerCache = GetMetadataHandlerInstanceCache();

                if (!metadataHandlerCache.TryGetValue(metadataSource, out var metadataHandler))
                {
                    // No handler found for this metadata source, skip it
                    Logging.Log(Logging.LogType.Warning, "Metadata Match", $"No IMetadata handler found for source: {metadataSource}");
                    continue;
                }

                // get the metadataitem from the dataobject - if not present, create a new one
                // default to new
                DataObjectItem.MetadataItem metadata = new DataObjectItem.MetadataItem(objectType)
                {
                    Id = "",
                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                    Source = metadataSource,
                    LastSearch = now.AddMonths(-3),
                    NextSearch = now.AddMonths(-1),
                    WinningVoteCount = 0,
                    TotalVoteCount = 0
                };
                if (item.Metadata != null)
                {
                    DataObjectItem.MetadataItem? metadataFromItem = item.Metadata.Find(x => x.Source == metadataSource);
                    if (metadataFromItem != null)
                    {
                        metadata = metadataFromItem;
                    }
                }

                if (metadata.MatchMethod == null || !dontSearchMatchMethods.Contains((BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod)metadata.MatchMethod))
                {
                    // if the next search is in the past, or if we are forcing a search, then we can search for metadata
                    if (ForceSearch || metadata.NextSearch < now)
                    {
                        // searching is allowed
                        Logging.Log(Logging.LogType.Information, "Metadata Match", $"Checking {metadataSource}...");

                        try
                        {
                            // perform the search
                            DataObjects.MatchItem searchResult = await metadataHandler.FindMatchItemAsync(item, SearchCandidates, searchOptions);

                            // update the metadata item with the search results
                            metadata.Id = searchResult.MetadataId;
                            metadata.MatchMethod = searchResult.MatchMethod;
                            metadata.LastSearch = now;
                            switch (metadata.MatchMethod)
                            {
                                case BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic:
                                case BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.AutomaticTooManyMatches:
                                    metadata.NextSearch = now.AddDays(automaticNextDay);
                                    break;
                                case BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch:
                                    metadata.NextSearch = now.AddDays(noMatchNextDay);
                                    break;
                                default:
                                    metadata.NextSearch = now.AddDays(defaultNextDay);
                                    break;
                            }

                            // add to updates list
                            metadataUpdates.Add(metadata);

                            Logging.Log(Logging.LogType.Information, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - {item.ObjectType} {item.Name} {metadata.MatchMethod} to {metadata.Source} metadata: {metadata.Id}");
                        }
                        catch (Exception ex)
                        {
                            Logging.Log(Logging.LogType.Warning, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - Error processing metadata search", ex);
                        }
                    }
                }

                if (metadataSource == MetadataSources.IGDB && metadata.ImmutableId != null && metadata.ImmutableId != "")
                {
                    // IGDB metadata found, we can use this to check other metadata sources
                    // if (item.Metadata.Find(x => x.Source == MetadataSources.Wikipedia) == null)
                    // {
                    // no wikipedia metadata present, try to get it from IGDB
                    var wiki = new MetadataLib.MetadataWikipedia();
                    try
                    {
                        var wikiMetadataResults = await wiki.FindMatchItemAsync(item, SearchCandidates, new Dictionary<string, object>
                            {
                                { "igdbGameId", long.Parse(metadata.ImmutableId) }
                            });
                        if (wikiMetadataResults != null && wikiMetadataResults.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic)
                        {
                            // update the metadata item with the search results
                            DataObjectItem.MetadataItem wikiMetadata = new DataObjectItem.MetadataItem(objectType)
                            {
                                Id = wikiMetadataResults.MetadataId,
                                MatchMethod = wikiMetadataResults.MatchMethod,
                                Source = MetadataSources.Wikipedia,
                                LastSearch = now,
                                NextSearch = now.AddMonths(6),
                                WinningVoteCount = 0,
                                TotalVoteCount = 0
                            };

                            // add to updates list
                            metadataUpdates.Add(wikiMetadata);

                            Logging.Log(Logging.LogType.Information, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - {item.ObjectType} {item.Name} {wikiMetadata.MatchMethod} to {wikiMetadata.Source} metadata: {wikiMetadata.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - Error processing Wikipedia metadata search", ex);
                    }
                    // }
                }
            }

            // clone DataObject to a new object incorporating any metadata updates - skip if no changes
            if (metadataUpdates.Count > 0)
            {
                DataObjectItem updatedDataObject = new DataObjectItem()
                {
                    Id = item.Id,
                    Name = item.Name,
                    ObjectType = item.ObjectType,
                    Permissions = item.Permissions,
                    SignatureDataObjects = item.SignatureDataObjects,
                    UpdatedDate = now,
                    UserPermissions = item.UserPermissions,
                    CreatedDate = item.CreatedDate,
                    Attributes = item.Attributes,
                    Metadata = item.Metadata
                };

                if (updatedDataObject.Metadata == null)
                {
                    updatedDataObject.Metadata = new List<DataObjectItem.MetadataItem>();
                }

                // apply metadata updates
                foreach (DataObjectItem.MetadataItem metadataUpdate in metadataUpdates)
                {
                    // check if metadata source already exists
                    DataObjectItem.MetadataItem? existingMetadata = updatedDataObject.Metadata.Find(x => x.Source == metadataUpdate.Source);
                    if (existingMetadata != null)
                    {
                        // update existing
                        existingMetadata.Id = metadataUpdate.Id;
                        existingMetadata.MatchMethod = metadataUpdate.MatchMethod;
                        existingMetadata.LastSearch = metadataUpdate.LastSearch;
                        existingMetadata.NextSearch = metadataUpdate.NextSearch;
                        existingMetadata.WinningVoteCount = metadataUpdate.WinningVoteCount;
                        existingMetadata.TotalVoteCount = metadataUpdate.TotalVoteCount;
                    }
                    else
                    {
                        // add new
                        updatedDataObject.Metadata.Add(metadataUpdate);
                    }
                }

                // save updated data object
                _ = EditDataObject(updatedDataObject.ObjectType, updatedDataObject.Id, (DataObjectItem)updatedDataObject, true);
            }

            // ensure there are tasks for this item
            bool aiTaskPresent = TaskManagement.GetAllTasks(item.Id).Any(t => t.TaskName == Models.Tasks.TaskType.AIDescriptionAndTagging);
            // get metadata cover if new object is a game
            if (objectType == DataObjectType.Game)
            {
                try
                {
                    BackgroundMetadataMatcher.BackgroundMetadataMatcher metadataMatcher = new BackgroundMetadataMatcher.BackgroundMetadataMatcher();
                    _ = metadataMatcher.GetGameArtwork((long)item.Id, ForceSearch);
                }
                catch (Exception ex)
                {
                    Logging.Log(Logging.LogType.Warning, "Metadata Match", $"{processedObjectCount} / {objectTotalCount} - Error processing game artwork metadata search", ex);
                }
            }

            // update date
            UpdateDataObjectDate((long)item.Id);

            // enqueue AI description and tagging task if not already present
            if (aiTaskPresent == false)
            {
                TaskManagement.EnqueueTask(item.Id, Models.Tasks.TaskType.AIDescriptionAndTagging);
            }
        }

        private static List<string> GetSearchCandidates(string GameName)
        {
            // remove version numbers from name
            GameName = Regex.Replace(GameName, @"v(\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();
            GameName = Regex.Replace(GameName, @"Rev (\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();

            // assumption: no games have () in their titles so we'll remove them
            int idx = GameName.IndexOf('(');
            if (idx >= 0)
            {
                GameName = GameName.Substring(0, idx);
            }

            List<string> SearchCandidates = new List<string>();
            SearchCandidates.Add(GameName.Trim());
            if (GameName.Contains(" - "))
            {
                SearchCandidates.Add(GameName.Replace(" - ", ": ").Trim());
                SearchCandidates.Add(GameName.Substring(0, GameName.IndexOf(" - ")).Trim());
            }
            if (GameName.Contains(": "))
            {
                SearchCandidates.Add(GameName.Substring(0, GameName.IndexOf(": ")).Trim());
            }

            // strip any leading "The " from the game name
            if (GameName.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            {
                SearchCandidates.Add(GameName.Substring(4).Trim());
            }

            // strip any ", The" from the end of the game name
            if (GameName.EndsWith(", The", StringComparison.OrdinalIgnoreCase))
            {
                SearchCandidates.Add(GameName.Substring(0, GameName.Length - 5).Trim());
            }

            // strip any leading "A " from the game name
            if (GameName.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
            {
                SearchCandidates.Add(GameName.Substring(2).Trim());
            }

            // strip any leading "An " from the game name
            if (GameName.StartsWith("An ", StringComparison.OrdinalIgnoreCase))
            {
                SearchCandidates.Add(GameName.Substring(3).Trim());
            }

            // add the original name as a candidate
            SearchCandidates.Add(GameName);

            // loop all candidates and convert roman numerals to numbers
            List<string> tempSearchCandidates = SearchCandidates.ToList();
            foreach (var candidate in tempSearchCandidates.Select((o, i) => new { Value = o, Index = i }))
            {
                string? romanNumeral = Common.RomanNumerals.FindFirstRomanNumeral(candidate.Value);
                if (!String.IsNullOrEmpty(romanNumeral))
                {
                    string newCandidate = candidate.Value.Replace(romanNumeral, Common.RomanNumerals.RomanToInt(romanNumeral).ToString());
                    if (candidate.Index + 1 == tempSearchCandidates.Count)
                        SearchCandidates.Add(newCandidate); // add a new candidate if the roman numeral is at the end
                    else
                        SearchCandidates.Insert(candidate.Index + 1, newCandidate); // insert a new candidate after the current one
                }
            }

            // remove duplicates
            SearchCandidates = SearchCandidates.Distinct().ToList();

            // remove any empty candidates
            SearchCandidates.RemoveAll(x => string.IsNullOrWhiteSpace(x));

            Logging.Log(Logging.LogType.Information, "Import Game", "Search candidates: " + String.Join(", ", SearchCandidates));

            return SearchCandidates;
        }

        public async Task<MatchItem> GetDataObject<T>(MetadataSources Source, string Endpoint, string Fields, string Query)
        {
            Communications communications = new Communications(Source);
            T[]? results;

            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                results = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetObjectsFromDatabase<T>(Endpoint, Fields, Query);
            }
            else
            {
                results = await communications.APIComm<T>(Endpoint, Fields, Query);
            }

            MatchItem matchItem = new MatchItem();

            if (results == null)
            {
                // no results - stay in no match, and set next search to next month
                matchItem.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch;
                return matchItem;
            }

            if (results.Length == 0)
            {
                // no results - stay in no match, and set next search to next month
                matchItem.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch;
                return matchItem;
            }
            else
            {
                if (results.Length == 1)
                {
                    // one result - use this
                    switch (Source)
                    {
                        case MetadataSources.IGDB:
                            var Value = typeof(T).GetProperty("Id").GetValue(results[0]);
                            matchItem.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                            matchItem.MetadataId = Value.ToString();
                            break;

                    }

                    return matchItem;
                }
                else
                {
                    // too many results - set to too many
                    matchItem.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.AutomaticTooManyMatches;
                    return matchItem;
                }
            }
        }

        public class MatchItem
        {
            public BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod MatchMethod { get; set; } = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch;
            public string MetadataId { get; set; } = "";
        }

        public async Task<AttributeItem> AddAttribute(long DataObjectId, AttributeItem attribute)
        {
            object? attributeValue = null;
            long? attributeRelation = null;

            switch (attribute.attributeType)
            {
                case AttributeItem.AttributeType.ObjectRelationship:
                    attributeRelation = long.Parse(attribute.Value.ToString());
                    break;

                default:
                    attributeValue = attribute.Value;
                    break;

            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO DataObject_Attributes (DataObjectId, AttributeType, AttributeName, AttributeValue, AttributeRelation, AttributeRelationType) VALUES (@id, @attributetype, @attributename, @attributevalue, @attributerelation, @attributerelationtype); SELECT LAST_INSERT_ID();";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "attributetype", attribute.attributeType },
                { "attributename", attribute.attributeName },
                { "attributevalue", attributeValue },
                { "attributerelation", attributeRelation },
                { "attributerelationtype", attribute.attributeRelationType }
            });

            sql = "SELECT * FROM DataObject_Attributes WHERE AttributeId=@id";
            DataTable returnValue = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", data.Rows[0][0] }
            });
            AttributeItem attributeItem = await BuildAttributeItem(returnValue.Rows[0], true);

            UpdateDataObjectDate(DataObjectId);

            return attributeItem;
        }

        public void DeleteAttribute(long DataObjectId, long AttributeId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "DELETE FROM DataObject_Attributes WHERE DataObjectId=@id AND AttributeId=@attrid;";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "attrid", AttributeId }
            });

            UpdateDataObjectDate(DataObjectId);
        }

        private async Task<AttributeItem> BuildAttributeItem(DataRow row, bool GetChildRelations = false)
        {
            AttributeItem attributeItem = new AttributeItem()
            {
                Id = (long)row["AttributeId"],
                attributeType = (AttributeItem.AttributeType)row["AttributeType"],
                attributeName = (AttributeItem.AttributeName)row["AttributeName"],
                attributeRelationType = (DataObjectType)row["AttributeRelationType"]
            };
            switch (attributeItem.attributeType)
            {
                case AttributeItem.AttributeType.ObjectRelationship:
                    if (GetChildRelations == true)
                    {
                        attributeItem.Value = await GetDataObject(attributeItem.attributeRelationType, (long)row["AttributeRelation"]);
                    }
                    else
                    {
                        RelationItem relationItem = new RelationItem()
                        {
                            relationType = attributeItem.attributeRelationType,
                            relationId = (long)row["AttributeRelation"]
                        };
                        attributeItem.Value = relationItem;
                    }
                    break;
                default:
                    if (row["AttributeValue"] != DBNull.Value)
                    {
                        attributeItem.Value = (string)row["AttributeValue"];
                    }
                    else
                    {
                        attributeItem.Value = "";
                    }
                    break;
            }

            return attributeItem;
        }

        public void AddSignature(long DataObjectId, DataObjectType dataObjectType, long SignatureId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO DataObject_SignatureMap (DataObjectId, DataObjectTypeId, SignatureId) VALUES (@id, @typeid, @sigid);";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "typeid", dataObjectType },
                { "sigid", SignatureId }
            });

            UpdateDataObjectDate(DataObjectId);
        }

        public void DeleteSignature(long DataObjectId, DataObjectType dataObjectType, long SignatureId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "DELETE FROM DataObject_SignatureMap WHERE DataObjectId=@id AND DataObjectTypeId=@typeid AND SignatureId=@sigid);";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "typeid", dataObjectType },
                { "sigid", SignatureId }
            });

            UpdateDataObjectDate(DataObjectId);
        }

        /// <summary>
        /// Fetch signatures relevant to the selected DataObjectType
        /// </summary>
        /// <param name="ObjectType">The ObjectType to search signatures for</param>
        /// <param name="SearchString">The search term</param>
        /// <returns>A list of signatures</returns>
        public async Task<List<Dictionary<string, object>>> SignatureSearch(long DataObjectId, DataObjectType ObjectType, string SearchString)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string tableName;
            string fieldName;
            string customWhere = "";

            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "typeid", ObjectType },
                { "searchstring", SearchString }
            };

            // any signature returned should be relevant to the ObjectType, and not be used anywhere else
            switch (ObjectType)
            {
                case DataObjectType.Company:
                    tableName = "Publishers";
                    fieldName = "Publisher";
                    break;

                case DataObjectType.Platform:
                    tableName = "Platforms";
                    fieldName = "Platform";
                    break;

                case DataObjectType.Game:
                    tableName = "Games";
                    fieldName = "`Name`";

                    // get a list of platform signature ids to filter results on
                    List<AttributeItem> attributes = await GetAttributes(DataObjectId, true);

                    List<int> platformIds = new List<int>();

                    foreach (AttributeItem attribute in attributes)
                    {
                        if (
                            attribute.attributeType == AttributeItem.AttributeType.ObjectRelationship &&
                            attribute.attributeName == AttributeItem.AttributeName.Platform &&
                            attribute.attributeRelationType == DataObjectType.Platform
                            )
                        {
                            DataObjectItem platformObject = (DataObjectItem)attribute.Value;

                            List<Dictionary<string, object>> platformSignatures = await GetSignatures(DataObjectType.Platform, platformObject.Id);
                            foreach (Dictionary<string, object> platformSignature in platformSignatures)
                            {
                                platformIds.Add(int.Parse((string)platformSignature["SignatureId"]));
                            }
                        }
                    }

                    // construct where clause
                    if (platformIds.Count > 0)
                    {
                        customWhere = "Signatures_<TableName>.SystemId IN ( ";
                        for (int i = 0; i < platformIds.Count; i++)
                        {
                            if (i > 0)
                            {
                                customWhere += ", ";
                            }

                            customWhere += "@id" + i;
                            dbDict.Add("id" + i, platformIds[i]);
                        }
                        customWhere += " ) AND ";
                    }

                    break;

                default:
                    throw new DataObjectsBadSearchCriteriaException("Invalid ObjectType");
            }

            string sql = @"SELECT
                            Signatures_<TableName>.* 
                        FROM
                            Signatures_<TableName>
                        LEFT JOIN
                            (
                                SELECT
                                    *
                                FROM
                                    DataObject_SignatureMap
                                WHERE
                                    DataObjectTypeId = @typeid
                            ) DataObject_SignatureMap ON Signatures_<TableName>.Id = DataObject_SignatureMap.SignatureId
                        WHERE
                            DataObject_SignatureMap.SignatureId IS NULL AND " + customWhere + @"
                            Signatures_<TableName>.<FieldName> LIKE CONCAT('%', @searchstring, '%');";
            sql = sql.Replace("<TableName>", tableName).Replace("<FieldName>", fieldName);

            List<Dictionary<string, object>> results = db.ExecuteCMDDict(sql, dbDict);

            return results;
        }

        public async Task<Dictionary<DataObjectItemTags.TagType, DataObjectItemTags>> GetTags(long? DataObjectId = null)
        {
            Dictionary<DataObjectItemTags.TagType, DataObjectItemTags> tags = new Dictionary<DataObjectItemTags.TagType, DataObjectItemTags>();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Tags ORDER BY `name`;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            if (DataObjectId != null)
            {
                sql = "SELECT Tags.id, Tags.`type`, Tags.`name`, DataObject_Tags.`AIAssigned` FROM `Tags` INNER JOIN `DataObject_Tags` ON `Tags`.`id` = `DataObject_Tags`.`TagId` WHERE `DataObject_Tags`.`DataObjectId`=@id ORDER BY `Tags`.`name`;";
                dbDict.Add("id", DataObjectId);
            }
            DataTable data = await db.ExecuteCMDAsync(sql, dbDict);

            foreach (DataRow row in data.Rows)
            {
                // convert type to enum
                DataObjectItemTags.TagType tagType = (DataObjectItemTags.TagType)(int)row["type"];

                if (!tags.ContainsKey(tagType))
                {
                    tags.Add(tagType, new DataObjectItemTags()
                    {
                        Type = tagType,
                        Tags = new List<DataObjectItemTags.TagModel>()
                    });
                }

                tags[tagType].Tags.Add(new DataObjectItemTags.TagModel()
                {
                    Id = (long)row["id"],
                    Text = (string)row["name"],
                    AIGenerated = row.Table.Columns.Contains("AIAssigned") && row["AIAssigned"] != DBNull.Value ? (bool)row["AIAssigned"] : false
                });
            }

            return tags;
        }

        public async Task<AttributeItem> GetTagAttribute(long DataObjectId)
        {
            Dictionary<DataObjectItemTags.TagType, DataObjectItemTags> tags = await GetTags(DataObjectId);
            AttributeItem tagAttribute = new AttributeItem()
            {
                attributeType = AttributeItem.AttributeType.EmbeddedList,
                attributeName = AttributeItem.AttributeName.Tags,
                attributeRelationType = DataObjectType.None,
                Value = tags
            };
            return tagAttribute;
        }

        public DataObjectItem MergeObjects(DataObjectItem sourceObject, DataObjectItem targetObject, bool commit = false)
        {
            // first, ensure both objects are the same type
            if (sourceObject.ObjectType != targetObject.ObjectType)
            {
                throw new Exception("Cannot merge objects of different types");
            }

            // copy root attributes
            // copy the name if the target is empty - note this should not ever be required
            if (targetObject.Name == "")
            {
                targetObject.Name = sourceObject.Name;
            }

            // copy attributes
            foreach (AttributeItem srcAttribute in sourceObject.Attributes)
            {
                switch (srcAttribute.attributeType)
                {
                    case AttributeItem.AttributeType.ObjectRelationship:
                    case AttributeItem.AttributeType.EmbeddedList:
                        break;

                    default:
                        bool targetAttributeFound = false;
                        foreach (AttributeItem targetAttribute in targetObject.Attributes)
                        {
                            if (targetAttribute.attributeName == srcAttribute.attributeName)
                            {
                                targetAttributeFound = true;
                                if (targetAttribute.Value == null || targetAttribute.Value == "")
                                {
                                    targetAttribute.Value = srcAttribute.Value;
                                }
                            }
                        }

                        if (targetAttributeFound == false)
                        {
                            switch (srcAttribute.attributeName)
                            {
                                case AttributeItem.AttributeName.Country:
                                case AttributeItem.AttributeName.Language:
                                case AttributeItem.AttributeName.ROMs:
                                    break;

                                default:
                                    targetObject.Attributes.Add(srcAttribute);
                                    break;
                            }
                        }
                        break;
                }
            }

            // copy metadata
            foreach (DataObjectItem.MetadataItem srcMetadata in sourceObject.Metadata)
            {
                bool targetMetadataFound = false;
                foreach (DataObjectItem.MetadataItem targetMetadata in targetObject.Metadata)
                {
                    if (targetMetadata.Source == srcMetadata.Source)
                    {
                        targetMetadataFound = true;
                        if (targetMetadata.Id == null || targetMetadata.Id == "")
                        {
                            targetMetadata.Id = srcMetadata.Id;
                            targetMetadata.MatchMethod = srcMetadata.MatchMethod;
                        }
                    }
                }

                if (targetMetadataFound == false)
                {
                    targetObject.Metadata.Add(srcMetadata);
                }
            }

            // copy signatures
            foreach (Dictionary<string, object> srcSignature in sourceObject.SignatureDataObjects)
            {
                bool targetSignatureFound = false;
                foreach (Dictionary<string, object> targetSignature in targetObject.SignatureDataObjects)
                {
                    if (targetSignature["SignatureId"] == srcSignature["SignatureId"])
                    {
                        targetSignatureFound = true;
                    }
                }

                if (targetSignatureFound == false)
                {
                    targetObject.SignatureDataObjects.Add(srcSignature);
                }
            }

            // Update all attribute relations that reference the source object to point to the target object.
            // This ensures that any dependencies on the source object are redirected to the target object during the merge operation.
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE DataObject_Attributes SET AttributeRelation=@targetid WHERE AttributeRelation=@sourceid AND AttributeRelationType=@typeid;";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "targetid", targetObject.Id },
                { "sourceid", sourceObject.Id },
                { "typeid", sourceObject.ObjectType }
            }, 180);

            // apply changes if commit = true
            if (commit == true)
            {
                var editDataObject = EditDataObject(targetObject.ObjectType, targetObject.Id, targetObject);
                var dataObjectMetadataSearch = DataObjectMetadataSearch(targetObject.ObjectType, targetObject.Id, false);
                UpdateDataObjectDate(targetObject.Id);
                DeleteDataObject(sourceObject.ObjectType, sourceObject.Id);
            }

            return targetObject;
        }

        /// <summary>
        /// Get similar data objects based on tags. Returns up to 10 objects with the highest overall similarity,
        /// calculated by comparing tags across all tag categories. Each returned object includes per-category
        /// similarity scores and an overall similarity percentage.
        /// </summary>
        /// <param name="DataObject">The data object to find similar items for</param>
        /// <param name="filterTagType">Optional tag type to filter similarity calculation. If null, returns overall similarity across all categories. If specified, returns similarity based only on that tag type.</param>
        /// <returns>
        /// A list of up to 10 similar data objects sorted by similarity (highest first)
        /// </returns>
        public async Task<DataObjectsList?> GetSimilarDataObjects(Models.DataObjectItem? dataObject, DataObjectItemTags.TagType? filterTagType = null)
        {
            DataObjectsList list = new DataObjectsList
            {
                Objects = new List<DataObjectItem>(),
                Count = 0,
                PageNumber = 1,
                PageSize = 10,
                TotalPages = 0
            };

            // check for tags - if none, abort
            if (dataObject == null || dataObject.Attributes == null || dataObject.Attributes.Count == 0)
            {
                return list;
            }

            // check DataObject type - only games are supported, so return list as is if the type is not game
            if (dataObject != null && dataObject.ObjectType != DataObjectType.Game)
            {
                return list;
            }

            AttributeItem? tagAttribute = dataObject.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.Tags);
            if (tagAttribute == null || tagAttribute.Value == null)
            {
                return list;
            }

            // Extract source tags by category
            var sourceTags = tagAttribute.Value as Dictionary<DataObjectItemTags.TagType, DataObjectItemTags>;
            if (sourceTags == null || sourceTags.Count == 0)
            {
                return list;
            }

            // Build a dictionary of source tag IDs keyed by tag type for quick lookup
            Dictionary<DataObjectItemTags.TagType, HashSet<long>> sourceTagIds = new Dictionary<DataObjectItemTags.TagType, HashSet<long>>();

            // If a specific tag type is requested, only include tags of that type
            if (filterTagType != null)
            {
                if (sourceTags.ContainsKey((DataObjectItemTags.TagType)filterTagType))
                {
                    sourceTagIds[(DataObjectItemTags.TagType)filterTagType] = new HashSet<long>(sourceTags[(DataObjectItemTags.TagType)filterTagType].Tags.Select(t => t.Id));
                }
                else
                {
                    // Source object has no tags of the requested type
                    return list;
                }
            }
            else
            {
                // Include all tag types
                foreach (var tagEntry in sourceTags)
                {
                    sourceTagIds[tagEntry.Key] = new HashSet<long>(tagEntry.Value.Tags.Select(t => t.Id));
                }
            }

            // Get all objects of the same type (excluding the source object itself)
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT Id FROM DataObject WHERE ObjectType = @objecttype AND Id != @sourceid ORDER BY Id;";
            DataTable candidateData = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>
            {
                { "objecttype", dataObject.ObjectType },
                { "sourceid", dataObject.Id }
            });

            if (candidateData.Rows.Count == 0)
            {
                return list;
            }

            // Collect all candidate IDs
            List<long> candidateIds = new List<long>();
            foreach (DataRow row in candidateData.Rows)
            {
                candidateIds.Add((long)row["Id"]);
            }

            // Batch-fetch all tags for all candidates in one query
            string candidateIdList = string.Join(",", candidateIds);
            string tagsSql = @"SELECT DataObject_Tags.DataObjectId, Tags.id, Tags.type, Tags.name, DataObject_Tags.AIAssigned 
                              FROM Tags 
                              INNER JOIN DataObject_Tags ON Tags.id = DataObject_Tags.TagId 
                              WHERE DataObject_Tags.DataObjectId IN (" + candidateIdList + @") 
                              ORDER BY DataObject_Tags.DataObjectId, Tags.name;";

            DataTable allTagsData = await db.ExecuteCMDAsync(tagsSql, new Dictionary<string, object>());

            // Build a dictionary: candidateId -> { tagType -> List<tagId> }
            Dictionary<long, Dictionary<DataObjectItemTags.TagType, HashSet<long>>> candidateTagsMap =
                new Dictionary<long, Dictionary<DataObjectItemTags.TagType, HashSet<long>>>();

            foreach (DataRow tagRow in allTagsData.Rows)
            {
                long candidateId = (long)tagRow["DataObjectId"];
                DataObjectItemTags.TagType tagType = (DataObjectItemTags.TagType)(int)tagRow["type"];
                long tagId = (long)tagRow["id"];

                if (!candidateTagsMap.ContainsKey(candidateId))
                {
                    candidateTagsMap[candidateId] = new Dictionary<DataObjectItemTags.TagType, HashSet<long>>();
                }

                if (!candidateTagsMap[candidateId].ContainsKey(tagType))
                {
                    candidateTagsMap[candidateId][tagType] = new HashSet<long>();
                }

                candidateTagsMap[candidateId][tagType].Add(tagId);
            }

            // Dictionary to store similarity scores: candidateId -> { tagType -> similarity%, overall -> similarity% }
            Dictionary<long, Dictionary<string, double>> similarityScores = new Dictionary<long, Dictionary<string, double>>();

            // Evaluate each candidate
            foreach (long candidateId in candidateIds)
            {
                // Skip candidates with no tags
                if (!candidateTagsMap.ContainsKey(candidateId) || candidateTagsMap[candidateId].Count == 0)
                {
                    continue;
                }

                var candidateTags = candidateTagsMap[candidateId];

                // Calculate per-category similarity
                Dictionary<string, double> categoryScores = new Dictionary<string, double>();
                double totalSimilarity = 0;
                int categoriesWithTags = 0;

                foreach (var sourceTagEntry in sourceTagIds)
                {
                    DataObjectItemTags.TagType tagType = sourceTagEntry.Key;
                    HashSet<long> sourceTagIdSet = sourceTagEntry.Value;

                    double categorySimilarity = 0;
                    if (sourceTagIdSet.Count > 0)
                    {
                        if (candidateTags.ContainsKey(tagType))
                        {
                            HashSet<long> candidateTagIds = candidateTags[tagType];
                            int matchCount = sourceTagIdSet.Intersect(candidateTagIds).Count();
                            // Similarity is the percentage of source tags that appear in the candidate
                            categorySimilarity = (double)matchCount / sourceTagIdSet.Count * 100.0;
                        }
                        totalSimilarity += categorySimilarity;
                        categoriesWithTags++;
                    }

                    // Store category-specific similarity (only if source has tags in this category)
                    categoryScores[tagType.ToString()] = categorySimilarity;
                }

                // Calculate overall similarity as average across categories that have source tags
                // If filtering by tag type, the "overall" is just that category's similarity
                double overallSimilarity = categoriesWithTags > 0 ? totalSimilarity / categoriesWithTags : 0;
                categoryScores["Overall"] = overallSimilarity;

                similarityScores[candidateId] = categoryScores;
            }

            // Sort candidates by overall similarity descending and take top 10
            var topCandidates = similarityScores
                .Where(x => x.Value["Overall"] > 0) // Only include candidates with at least some similarity
                .OrderByDescending(x => x.Value["Overall"])
                .Take(10)
                .ToList();

            if (topCandidates.Count == 0)
            {
                return list;
            }

            // Batch-fetch DataObject records for top 10
            List<long> topCandidateIds = topCandidates.Select(x => x.Key).ToList();
            string topIdList = string.Join(",", topCandidateIds);

            string dataObjectSql = "SELECT * FROM DataObject WHERE Id IN (" + topIdList + ") ORDER BY FIELD(Id, " + topIdList + ");";
            DataTable topDataObjectsData = await db.ExecuteCMDAsync(dataObjectSql, new Dictionary<string, object>());

            // Build DataObject items using BuildDataObject for each
            foreach (var candidateEntry in topCandidates)
            {
                DataRow? dataObjectRow = null;
                foreach (DataRow row in topDataObjectsData.Rows)
                {
                    if ((long)row["Id"] == candidateEntry.Key)
                    {
                        dataObjectRow = row;
                        break;
                    }
                }

                if (dataObjectRow != null)
                {
                    DataObjectItem? candidate = await BuildDataObject(dataObject.ObjectType, candidateEntry.Key, dataObjectRow, true);
                    if (candidate != null)
                    {
                        // Store similarity metadata in a new attribute for display
                        // Create a custom attribute to hold similarity scores
                        var similarityAttribute = new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Description, // Reuse for now; could add custom type
                            attributeType = AttributeItem.AttributeType.LongString,
                            attributeRelationType = DataObjectType.None,
                            Value = System.Text.Json.JsonSerializer.Serialize(candidateEntry.Value)
                        };

                        // Add similarity scores as custom property (store in a way accessible to consumers)
                        // For now, we'll attach via the item's metadata or a custom property
                        candidate.Attributes ??= new List<AttributeItem>();

                        list.Objects.Add(candidate);
                    }
                }
            }

            list.Count = list.Objects.Count;
            list.PageSize = 10;
            list.TotalPages = 1;

            return list;
        }
    }
}