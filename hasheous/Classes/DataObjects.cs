using System.Data;
using System.Reflection;
using Classes;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using IGDB;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NuGet.Common;
using static hasheous_server.Classes.Metadata.IGDB.Communications;

namespace hasheous_server.Classes
{
    public class DataObjects
    {
        public enum DataObjectType
        {
            Company = 0,
            Platform = 1,
            Game = 2
        }

        public List<Models.DataObjectItem> GetDataObjects(DataObjectType objectType)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject WHERE ObjectType = @objecttype ORDER BY `Name`;";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>
            {
                { "objecttype", objectType }
            }
            );

            List<Models.DataObjectItem> DataObjects = new List<Models.DataObjectItem>();
            foreach (DataRow row in data.Rows)
            {
                Models.DataObjectItem item = BuildDataObject(
                    objectType,
                    (long)row["Id"],
                    row,
                    false
                );

                DataObjects.Add(item);
            }

            return DataObjects;
        }

        public Models.DataObjectItem? GetDataObject(DataObjectType objectType, long id)
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
                DataObjectItem item = BuildDataObject(objectType, id, data.Rows[0], true);

                return item;
            }
            else
            {
                return null;
            }
        }

        private Models.DataObjectItem BuildDataObject(DataObjectType ObjectType, long id, DataRow row, bool GetChildRelations = false)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id }
            };
            DataTable data;
            
            // get attributes
            sql = "SELECT * FROM DataObject_Attributes WHERE DataObjectId = @id";
            data = db.ExecuteCMD(sql, dbDict);
            List<AttributeItem> attributes = new List<AttributeItem>();
            foreach (DataRow dataRow in data.Rows)
            {
                AttributeItem attributeItem = BuildAttributeItem(dataRow, GetChildRelations);
                attributes.Add(attributeItem);
            }

            // get signature items
            switch (ObjectType)
            {
                case DataObjectType.Company:
                    sql = "SELECT DataObject_SignatureMap.SignatureId, Signatures_Publishers.Publisher FROM DataObject_SignatureMap JOIN Signatures_Publishers ON DataObject_SignatureMap.SignatureId = Signatures_Publishers.Id WHERE DataObject_SignatureMap.DataObjectId = @id ORDER BY Signatures_Publishers.Publisher;";
                    break;

                case DataObjectType.Platform:
                    sql = "SELECT DataObject_SignatureMap.SignatureId, Signatures_Platforms.Platform FROM DataObject_SignatureMap JOIN Signatures_Platforms ON DataObject_SignatureMap.SignatureId = Signatures_Platforms.Id WHERE DataObject_SignatureMap.DataObjectId = @id ORDER BY Signatures_Platforms.Platform;";
                    break;

                case DataObjectType.Game:
                    sql = @"SELECT 
                            DataObject_SignatureMap.SignatureId,
                            CASE 
                                WHEN (Signatures_Games.Year IS NULL OR Signatures_Games.Year = '') THEN Signatures_Games.Name
                                ELSE CONCAT(Signatures_Games.Name, ' (', Signatures_Games.Year, ')'
                            END AS `Name`
                        FROM 
                            DataObject_SignatureMap 
                        JOIN 
                            Signatures_Games ON DataObject_SignatureMap.SignatureId = Signatures_Games.Id 
                        WHERE DataObject_SignatureMap.DataObjectId = @id 
                        ORDER BY Signatures_Games.Name;";
                    break;
            }
            List<Dictionary<string, object>> signatureItems = db.ExecuteCMDDict(sql, dbDict);

            // get metadata matches
            sql = "SELECT * FROM DataObject_MetadataMap WHERE DataObjectId = @id ORDER BY SourceId";
            data = db.ExecuteCMD(sql, dbDict);
            List<DataObjectItem.MetadataItem> metadataItems = new List<DataObjectItem.MetadataItem>();
            foreach (DataRow dataRow in data.Rows)
            {
                DataObjectItem.MetadataItem metadataItem = new DataObjectItem.MetadataItem(ObjectType){
                    Id = (string)dataRow["MetadataId"],
                    MatchMethod = (BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod)dataRow["MatchMethod"],
                    Source = (Metadata.IGDB.Communications.MetadataSources)dataRow["SourceId"],
                    LastSearch = (DateTime)dataRow["LastSearched"],
                    NextSearch = (DateTime)dataRow["NextSearch"]
                };

                metadataItems.Add(metadataItem);
            }

            DataObjectItem item = new DataObjectItem{
                Id = id,
                Name = (string)row["Name"],
                CreatedDate = (DateTime)row["CreatedDate"],
                UpdatedDate = (DateTime)row["UpdatedDate"],
                Metadata = metadataItems,
                SignatureDataObjects = signatureItems,
                Attributes = attributes
            };

            return item;
        }

        public Models.DataObjectItem NewDataObject(DataObjectType objectType, Models.DataObjectItemModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO DataObject (`Name`, `ObjectType`, `CreatedDate`, `UpdatedDate`) VALUES (@name, @objecttype, @createddate, @updateddate); SELECT LAST_INSERT_ID();";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "name", model.Name },
                { "objecttype", objectType },
                { "createddate", DateTime.UtcNow },
                { "updateddate", DateTime.UtcNow}
            };

            DataTable data = db.ExecuteCMD(sql, dbDict);

            // set up metadata searching
            foreach (Enum source in Enum.GetValues(typeof(Metadata.IGDB.Communications.MetadataSources)))
            {
                if ((Metadata.IGDB.Communications.MetadataSources)source != Metadata.IGDB.Communications.MetadataSources.None)
                {
                    sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metaid, @srcid, @method, @lastsearched, @nextsearch);";
                    dbDict = new Dictionary<string, object>{
                        { "id", (long)(ulong)data.Rows[0][0] },
                        { "metaid", "" },
                        { "srcid", (Metadata.IGDB.Communications.MetadataSources)source },
                        { "method", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch },
                        { "lastsearched", DateTime.UtcNow.AddMonths(-3) },
                        { "nextsearch", DateTime.UtcNow.AddMonths(-1) }
                    };
                    db.ExecuteNonQuery(sql, dbDict);
                }
            }

            DataObjectMetadataSearch(objectType, (long)(ulong)data.Rows[0][0]);

            return GetDataObject(objectType, (long)(ulong)data.Rows[0][0]);
        }

        public Models.DataObjectItem EditDataObject(DataObjectType objectType, long id, Models.DataObjectItemModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE DataObject SET `Name`=@name, `UpdatedDate`=@updateddate WHERE ObjectType=@objecttype AND Id=@id";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id },
                { "name", model.Name },
                { "objecttype", objectType },
                { "createddate", DateTime.UtcNow },
                { "updateddate", DateTime.UtcNow }
            };

            db.ExecuteNonQuery(sql, dbDict);

            return GetDataObject(objectType, id);
        }

        /// <summary>
        /// Performs a metadata look up on DataObjects with no match metadata
        /// </summary>
        public void DataObjectMetadataSearch(DataObjectType objectType)
        {
            _DataObjectMetadataSearch(objectType, null);
        }

        /// <summary>
        /// Performs a metadata look up on the selected DataObject if it has no metadata match
        /// </summary>
        /// <param name="id"></param>
        public void DataObjectMetadataSearch(DataObjectType objectType, long? id)
        {
            _DataObjectMetadataSearch(objectType, id);
        }

        private async void _DataObjectMetadataSearch(DataObjectType objectType, long? id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict;

            List<DataObjectItem> DataObjects = new List<DataObjectItem>();

            if (id != null)
            {
                DataObjects.Add(GetDataObject(objectType, (long)id));
            }
            else
            {
                DataObjects.AddRange(GetDataObjects(objectType));
            }

            // search for metadata
            foreach (DataObjectItem item in DataObjects)
            {
                foreach (DataObjectItem.MetadataItem metadata in item.Metadata)
                {
                    dbDict = new Dictionary<string, object>{
                        { "id", item.Id },
                        { "metadataid", metadata.Id },
                        { "srcid", (int)metadata.Source },
                        { "method", metadata.MatchMethod },
                        { "lastsearched", DateTime.UtcNow },
                        { "nextsearch", DateTime.UtcNow.AddMonths(1) }
                    };

                    if (
                        metadata.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch &&
                        metadata.NextSearch < DateTime.UtcNow
                    )
                    {
                        // searching is allowed
                        switch (metadata.Source)
                        {
                            case Metadata.IGDB.Communications.MetadataSources.IGDB:
                                MatchItem DataObjectSearchResults;
                                switch (objectType)
                                {
                                    case DataObjectType.Company:
                                        DataObjectSearchResults = await GetDataObject<IGDB.Models.Company>(MetadataSources.IGDB, IGDBClient.Endpoints.Companies, "fields *;", "where name ~ *\"" + item.Name + "\"");        
                                        break;

                                    case DataObjectType.Platform:
                                        DataObjectSearchResults = await GetDataObject<IGDB.Models.Platform>(MetadataSources.IGDB, IGDBClient.Endpoints.Platforms, "fields *;", "where name ~ *\"" + item.Name + "\"");        
                                        break;

                                    case DataObjectType.Game:
                                        DataObjectSearchResults = await GetDataObject<IGDB.Models.Game>(MetadataSources.IGDB, IGDBClient.Endpoints.Games, "fields *;", "where name ~ *\"" + item.Name + "\"");        
                                        break;

                                    default:
                                        DataObjectSearchResults = new()
                                        {
                                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                                            MetadataId = ""
                                        };
                                        break;
                                }

                                dbDict["method"] = DataObjectSearchResults.MatchMethod;
                                dbDict["metadataid"] = DataObjectSearchResults.MetadataId;

                                break;
                        }

                        sql = "UPDATE DataObject_MetadataMap SET MetadataId=@metadataid, MatchMethod=@method, LastSearched=@lastsearched, NextSearch=@nextsearch WHERE DataObjectId=@id AND SourceId=@srcid;";
                        db.ExecuteNonQuery(sql, dbDict);
                    }
                }
            }
        }

        private async Task<MatchItem> GetDataObject<T>(MetadataSources Source, string Endpoint, string Fields, string Query)
        {
            Communications communications = new Communications(Source);
            var results = await communications.APIComm<T>(Endpoint, Fields, Query);

            MatchItem matchItem = new MatchItem();

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
                    var Value = typeof(T).GetProperty("Slug").GetValue(results[0]);
                    matchItem.MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                    matchItem.MetadataId = Value.ToString();
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

        private class MatchItem
        {
            public BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod MatchMethod { get; set; } = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch;
            public string MetadataId { get; set; } = "";
        }

        public AttributeItem AddAttribute(long DataObjectId, AttributeItem attribute)
        {
            object? attributeValue = null;
            long? attributeRelation = null;

            switch (attribute.attributeType)
            {
                case AttributeItem.AttributeType.ObjectRelationship:
                    attributeRelation = (long?)attribute.Value;
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
            AttributeItem attributeItem = BuildAttributeItem(returnValue.Rows[0], true);

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
        }

        private AttributeItem BuildAttributeItem(DataRow row, bool GetChildRelations = false)
        {
            AttributeItem attributeItem = new AttributeItem(){
                Id = (long)row["AttributeId"],
                attributeType = (AttributeItem.AttributeType)row["AttributeType"],
                attributeName = (AttributeItem.AttributeName)row["AttributeName"]
            };
            switch (attributeItem.attributeType)
            {
                case AttributeItem.AttributeType.ObjectRelationship:
                    DataObjectType relationType = (DataObjectType)row["AttributeRelationType"];
                    if (GetChildRelations == true)
                    {   
                        attributeItem.Value = GetDataObject(relationType, (long)row["AttributeRelation"]);
                    }
                    else
                    {
                        RelationItem relationItem = new RelationItem(){
                            relationType = relationType,
                            relationId = (long)row["AttributeRelation"]
                        };
                        attributeItem.Value = relationItem;
                    }
                    break;
                default:
                    attributeItem.Value = (string)row["AttributeValue"];
                    break;
            }

            return attributeItem;
        }

        public void AddSignature(long DataObjectId, long SignatureId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO DataObject_SignatureMap (DataObjectId, SignatureId) VALUES (@id, @sigid);";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "sigid", SignatureId }
            });
        }

        public void DeleteSignature(long DataObjectId, long SignatureId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "DELETE FROM DataObject_SignatureMap WHERE DataObjectId=@id AND SignatureId=@sigid);";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "sigid", SignatureId }
            });
        }
    }
}