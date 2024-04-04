using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Classes;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using IGDB;
using IGDB.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NuGet.Common;
using static hasheous_server.Classes.Metadata.IGDB.Communications;

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
            None = 100
        }

        public List<Models.DataObjectItem> GetDataObjects(DataObjectType objectType, string? search = null)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "objecttype", objectType },
            };
            if (search == null)
            {
                sql = "SELECT * FROM DataObject WHERE ObjectType = @objecttype ORDER BY `Name`;";
            }
            else
            {
                sql = "SELECT * FROM DataObject WHERE ObjectType = @objecttype AND `Name` LIKE @search ORDER BY `Name`;";
                dbDict.Add("search", "%" + search + "%");
            }
            DataTable data = db.ExecuteCMD(sql, dbDict);

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

        private void UpdateDataObjectDate(long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE DataObject SET UpdatedDate=@updateddate WHERE Id=@id;";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "updateddate", DateTime.UtcNow }
            });
        }

        private Models.DataObjectItem BuildDataObject(DataObjectType ObjectType, long id, DataRow row, bool GetChildRelations = false)
        {
            // get attributes
            List<AttributeItem> attributes = GetAttributes(id, GetChildRelations);

            // get signature items
            List<Dictionary<string, object>> signatureItems = GetSignatures(ObjectType, id);

            // get extra attributes if dataobjecttype is game
            if (ObjectType == DataObjectType.Game)
            {
                attributes.Add(GetRoms(signatureItems));
                attributes.AddRange(GetCountriesAndLanguagesForGame(signatureItems));
            }

            // get metadata matches
            List<DataObjectItem.MetadataItem> metadataItems = GetMetadataMap(ObjectType, id);

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

        public List<AttributeItem> GetAttributes(long DataObjectId, bool GetChildRelations)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject_Attributes WHERE DataObjectId = @id";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", DataObjectId }
            });
            List<AttributeItem> attributes = new List<AttributeItem>();
            foreach (DataRow dataRow in data.Rows)
            {
                AttributeItem attributeItem = BuildAttributeItem(dataRow, GetChildRelations);
                attributes.Add(attributeItem);
            }

            return attributes;
        }

        public List<Dictionary<string, object>> GetSignatures(DataObjectType ObjectType, long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{};

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
                            CASE 
                                WHEN ((Signatures_Games.`Year` IS NOT NULL OR Signatures_Games.`Year` <> '') AND (Signatures_Platforms.`Platform` IS NULL)) THEN CONCAT(Signatures_Games.`Name`, ' (', Signatures_Games.`Year`, ')')
                                WHEN ((Signatures_Games.`Year` IS NOT NULL OR Signatures_Games.`Year` <> '') AND (Signatures_Platforms.`Platform` IS NOT NULL)) THEN CONCAT(Signatures_Games.`Name`, ' (', Signatures_Games.`Year`, ')', ' - ', Signatures_Platforms.`Platform`)
                                WHEN ((Signatures_Games.`Year` IS NULL OR Signatures_Games.`Year` = '') AND (Signatures_Platforms.`Platform` IS NOT NULL)) THEN CONCAT(Signatures_Games.`Name`, ' - ', Signatures_Platforms.`Platform`)
                                ELSE Signatures_Games.`Name`
                            END AS `Game`
                        FROM 
                            DataObject_SignatureMap 
                        JOIN 
                            Signatures_Games ON DataObject_SignatureMap.`SignatureId` = Signatures_Games.`Id`
                        LEFT JOIN
                            Signatures_Platforms ON Signatures_Games.`SystemId` = Signatures_Platforms.`Id`
                        WHERE DataObject_SignatureMap.`DataObjectId` = @id AND DataObject_SignatureMap.`DataObjectTypeId` = @typeid
                        ORDER BY Signatures_Games.`Name`;";
                    break;
            }
            
            List<Dictionary<string, object>> signatureItems = db.ExecuteCMDDict(sql, new Dictionary<string, object>{
                { "id", DataObjectId },
                { "typeid", ObjectType }
            });

            return signatureItems;
        }

        public List<DataObjectItem.MetadataItem> GetMetadataMap(DataObjectType ObjectType, long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM DataObject_MetadataMap WHERE DataObjectId = @id ORDER BY SourceId";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", DataObjectId }
            });
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
                foreach (KeyValuePair<string, string> gameCountry in gameCountries)
                {
                    if (!countries.ContainsKey(gameCountry.Key))
                    {
                        countries.Add(gameCountry.Key, gameCountry.Value);
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
                AttributeItem countryAttributes = new AttributeItem{
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
                AttributeItem languageAttributes = new AttributeItem{
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

        public AttributeItem GetRoms(List<Dictionary<string, object>> GameSignatures)
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
                        `DevelopmentStatus`,
                        `Attributes`,
                        `RomType`,
                        `RomTypeMedia`,
                        `MediaLabel`,
                        `MetadataSource`
                    FROM
                        Signatures_Roms
                    WHERE
                        GameId=@gameid
                    ORDER BY `Name`;";
                DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                    { "gameid", GameSignature["SignatureId"] }
                });

                foreach (DataRow row in data.Rows)
                {
                    Signatures_Games_2.RomItem rom = signature.BuildRomItem(row);
                    roms.Add(rom);
                }
            }

            AttributeItem attribute = new AttributeItem{
                attributeName = AttributeItem.AttributeName.ROMs,
                attributeType = AttributeItem.AttributeType.EmbeddedList,
                attributeRelationType = DataObjectType.ROM,
                Value = roms
            };

            return attribute;
        }

        public Models.DataObjectItem NewDataObject(DataObjectType objectType, Models.DataObjectItemModel model)
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
                { "updateddate", DateTime.UtcNow }
            };

            db.ExecuteNonQuery(sql, dbDict);

            DataObjectMetadataSearch(objectType, id);

            return GetDataObject(objectType, id);
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
        }

        public Models.DataObjectItem EditDataObject(DataObjectType objectType, long id, Models.DataObjectItem model)
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

            DataObjectItem EditedObject = GetDataObject(objectType, id);

            // update attributes
            foreach (AttributeItem newAttribute in model.Attributes)
            {
                bool attributeFound = false;
                foreach (AttributeItem existingAttribute in EditedObject.Attributes)
                {
                    if (
                        (newAttribute.attributeType == existingAttribute.attributeType) &&
                        (newAttribute.attributeName == existingAttribute.attributeName)
                    )
                    {
                        attributeFound = true;

                        string compareValue = "";
                        string sqlField;
                        switch (existingAttribute.attributeType)
                        {
                            case AttributeItem.AttributeType.ObjectRelationship:
                                sqlField = "AttributeRelation";
                                DataObjectItem tempCompare = (DataObjectItem)existingAttribute.Value;
                                if (tempCompare != null)
                                {
                                    compareValue = tempCompare.Id.ToString();
                                }
                                break;

                            default:
                                sqlField = "AttributeValue";
                                compareValue = (string)existingAttribute.Value;
                                break;

                        }

                        if (compareValue != (string)newAttribute.Value)
                        {
                            // update existing value
                            sql = "UPDATE DataObject_Attributes SET " + sqlField + "=@value WHERE DataObjectId=@id AND AttributeId=@attrid;";
                            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                { "id", id },
                                { "attrid", existingAttribute.Id },
                                { "value", newAttribute.Value }
                            });
                        } else {
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
            }

            // update metadata map
            foreach (DataObjectItem.MetadataItem newMetadataItem in model.Metadata)
            {
                bool metadataFound = false;
                foreach (DataObjectItem.MetadataItem existingMetadataItem in EditedObject.Metadata)
                {
                    if (newMetadataItem.Source == existingMetadataItem.Source)
                    {
                        metadataFound = true;
                        if (newMetadataItem.Id != existingMetadataItem.Id)
                        {
                            // change to manually set
                            sql = "UPDATE DataObject_MetadataMap SET MatchMethod=@match, MetadataId=@metaid WHERE DataObjectId=@id AND SourceId=@source;";
                            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                { "id", id },
                                { "match", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin },
                                { "metaid", newMetadataItem.Id },
                                { "source", existingMetadataItem.Source }
                            });
                        }
                    }
                }

                if (metadataFound == false)
                {
                    sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metaid, @source, @match, @last, @next);";
                    db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                        { "id", id },
                        { "match", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin },
                        { "metaid", newMetadataItem.Id },
                        { "source", newMetadataItem.Source },
                        { "last", DateTime.UtcNow },
                        { "next", DateTime.UtcNow.AddMonths(1) }
                    });
                }
            }

            // signatures
            sql = "DELETE FROM DataObject_SignatureMap WHERE DataObjectId=@id";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "id", id }
            });
            foreach (Dictionary<string, object>? signature in model.SignatureDataObjects)
            {
                AddSignature(id, objectType, long.Parse(signature["SignatureId"].ToString()));
            }

            return GetDataObject(objectType, id);
        }

        /// <summary>
        /// Performs a metadata look up on DataObjects with no match metadata
        /// </summary>
        public MatchItem? DataObjectMetadataSearch(DataObjectType objectType, bool ForceSearch = false)
        {
            var retVal = _DataObjectMetadataSearch(objectType, null, ForceSearch);
            retVal.Wait(new TimeSpan(0, 0, 15));
            return retVal.Result;
        }

        /// <summary>
        /// Performs a metadata look up on the selected DataObject if it has no metadata match
        /// </summary>
        /// <param name="id"></param>
        public MatchItem? DataObjectMetadataSearch(DataObjectType objectType, long? id, bool ForceSearch = false)
        {
            var retVal = _DataObjectMetadataSearch(objectType, id, ForceSearch);
            retVal.Wait(new TimeSpan(0, 0, 15));
            return retVal.Result;
        }

        private async Task<MatchItem?> _DataObjectMetadataSearch(DataObjectType objectType, long? id, bool ForceSearch)
        {
            MatchItem? DataObjectSearchResults = null;

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict;

            List<DataObjectItem> DataObjectsToProcess = new List<DataObjectItem>();

            if (id != null)
            {
                DataObjectsToProcess.Add(GetDataObject(objectType, (long)id));
            }
            else
            {
                DataObjectsToProcess.AddRange(GetDataObjects(objectType));
            }

            // search for metadata
            foreach (DataObjectItem item in DataObjectsToProcess)
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
                        (
                            metadata.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch &&
                            metadata.NextSearch < DateTime.UtcNow
                        ) || ForceSearch == true
                    )
                    {
                        // searching is allowed
                        switch (metadata.Source)
                        {
                            case Metadata.IGDB.Communications.MetadataSources.IGDB:
                                switch (objectType)
                                {
                                    case DataObjectType.Company:
                                        DataObjectSearchResults = await GetDataObject<IGDB.Models.Company>(MetadataSources.IGDB, IGDBClient.Endpoints.Companies, "fields *;", "where name ~ *\"" + item.Name + "\"");        
                                        break;

                                    case DataObjectType.Platform:
                                        DataObjectSearchResults = await GetDataObject<IGDB.Models.Platform>(MetadataSources.IGDB, IGDBClient.Endpoints.Platforms, "fields *;", "where name ~ *\"" + item.Name + "\"");
                                        break;

                                    case DataObjectType.Game:
                                        long? PlatformId = null;
                                        foreach (AttributeItem attribute in item.Attributes)
                                        {
                                            if (attribute.attributeType == AttributeItem.AttributeType.ObjectRelationship)
                                            {
                                                if (attribute.attributeRelationType == DataObjectType.Platform)
                                                {
                                                    DataObjectItem platformDO = (DataObjectItem)attribute.Value;
                                                    foreach (DataObjectItem.MetadataItem provider in platformDO.Metadata)
                                                    {
                                                        if (provider.Source == MetadataSources.IGDB && (
                                                            provider.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic || 
                                                            provider.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Manual ||
                                                            provider.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin
                                                            )
                                                        )
                                                        {
                                                            IGDB.Models.Platform platform = Metadata.IGDB.Platforms.GetPlatform((string?)provider.Id, false);
                                                            PlatformId = platform.Id;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        
                                        if (PlatformId != null)
                                        {
                                            List<string> SearchCandidates = GetSearchCandidates(item.Name);

                                            bool SearchComplete = false;
                                            foreach (string SearchCandidate in SearchCandidates)
                                            {
                                                foreach (Games.SearchType searchType in Enum.GetValues(typeof(Games.SearchType)))
                                                {
                                                    IGDB.Models.Game[] games = Games.SearchForGame(SearchCandidate, (long)PlatformId, searchType);
                                                    if (games.Length == 1)
                                                    {
                                                        // exact match!
                                                        DataObjectSearchResults = new MatchItem{
                                                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                            MetadataId = games[0].Slug
                                                        };
                                                        SearchComplete = true;
                                                        break;
                                                    }
                                                    else if (games.Length > 1)
                                                    {
                                                        // too many matches - high likelihood of sequels and other variants
                                                        foreach (Game game in games) {
                                                            if (game.Name == SearchCandidate) {
                                                                // found game title matches the search candidate
                                                                DataObjectSearchResults = new MatchItem{
                                                                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                                    MetadataId = game.Slug
                                                                };
                                                                SearchComplete = true;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                                if (SearchComplete == true)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            DataObjectSearchResults = new MatchItem{
                                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                                                MetadataId = ""
                                            };
                                        }
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

            UpdateDataObjectDate((long)id);

            return DataObjectSearchResults;
        }

        private static List<string> GetSearchCandidates(string GameName)
        {
            // remove version numbers from name
            GameName = Regex.Replace(GameName, @"v(\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();
            GameName = Regex.Replace(GameName, @"Rev (\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();

            // assumption: no games have () in their titles so we'll remove them
            int idx = GameName.IndexOf('(');
            if (idx >= 0) {
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

            Logging.Log(Logging.LogType.Information, "Import Game", "Search candidates: " + String.Join(", ", SearchCandidates));

            return SearchCandidates;
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
                    switch (Source)
                    {
                        case MetadataSources.IGDB:
                            var Value = typeof(T).GetProperty("Slug").GetValue(results[0]);
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

        public AttributeItem AddAttribute(long DataObjectId, AttributeItem attribute)
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
            AttributeItem attributeItem = BuildAttributeItem(returnValue.Rows[0], true);

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
                    attributeItem.attributeRelationType = relationType;
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
        public List<Dictionary<string, object>> SignatureSearch(long DataObjectId, DataObjectType ObjectType, string SearchString)
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
                    List<AttributeItem> attributes = GetAttributes(DataObjectId, true);

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
                            
                            List<Dictionary<string, object>> platformSignatures = GetSignatures(DataObjectType.Platform, platformObject.Id);
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
    }
}