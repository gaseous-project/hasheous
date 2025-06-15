using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using IGDB;
using IGDB.Models;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public async Task<DataObjectsList> GetDataObjects(DataObjectType objectType, int pageNumber = 0, int pageSize = 0, string? search = null, bool GetChildRelations = false, bool GetMetadataMap = true, AttributeItem.AttributeName? filterAttribute = null, string? filterValue = null)
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
                    sql = "SELECT * FROM DataObject WHERE ObjectType = @objecttype ORDER BY `Name`;";
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
                    GetMetadataMap
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

        public async Task<Models.DataObjectItem?> GetDataObject(DataObjectType objectType, long id)
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
                DataObjectItem item = await BuildDataObject(objectType, id, data.Rows[0], true);

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
        }

        private async Task<Models.DataObjectItem> BuildDataObject(DataObjectType ObjectType, long id, DataRow row, bool GetChildRelations = false, bool GetMetadata = true)
        {
            // get attributes
            List<AttributeItem> attributes = await GetAttributes(id, GetChildRelations);

            // get signature items
            List<Dictionary<string, object>> signatureItems = await GetSignatures(ObjectType, id);

            // get extra attributes if dataobjecttype is game
            if (ObjectType == DataObjectType.Game)
            {
                if (GetChildRelations == true)
                {
                    attributes.Add(await GetRoms(signatureItems));
                    attributes.AddRange(GetCountriesAndLanguagesForGame(signatureItems));
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
                                        var company = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Company>(metadataItem.Id);
                                        if (company != null)
                                        {
                                            objectId = company.Id;
                                        }
                                        break;

                                    case DataObjects.DataObjectType.Platform:
                                        var platform = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Platform>(metadataItem.Id);
                                        if (platform != null)
                                        {
                                            objectId = platform.Id;
                                        }
                                        break;

                                    case DataObjects.DataObjectType.Game:
                                        var game = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(metadataItem.Id);
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

        public async Task<Models.DataObjectItem> NewDataObject(DataObjectType objectType, Models.DataObjectItemModel model)
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

                    DataObjectMetadataSearch(objectType, (long)(ulong)data.Rows[0][0]);
                    break;

                default:
                    break;
            }

            return await GetDataObject(objectType, (long)(ulong)data.Rows[0][0]);
        }

        public async Task<Models.DataObjectItem> EditDataObject(DataObjectType objectType, long id, Models.DataObjectItemModel model)
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
        }

        public async Task<Models.DataObjectItem> EditDataObject(DataObjectType objectType, long id, Models.DataObjectItem model)
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

            DataObjectItem EditedObject = await GetDataObject(objectType, id);

            // update attributes
            foreach (AttributeItem newAttribute in model.Attributes)
            {
                switch (newAttribute.attributeType)
                {
                    case AttributeItem.AttributeType.EmbeddedList:
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
                                                }
                                            }
                                            else
                                            {
                                                DataObjectItem newCompare = (DataObjectItem)newAttribute.Value;
                                                if (tempCompare.Name == newCompare.Name)
                                                {
                                                    isMatch = true;
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
            switch (objectType)
            {
                case DataObjectType.Company:
                case DataObjectType.Platform:
                case DataObjectType.Game:
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
                                    sql = "UPDATE DataObject_MetadataMap SET MatchMethod=@match, MetadataId=@metaid, WinningVoteCount=@winningvotecount, TotalVoteCount=@totalvotecount WHERE DataObjectId=@id AND SourceId=@source;";
                                    db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                { "id", id },
                                { "match", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin },
                                { "metaid", newMetadataItem.Id },
                                { "source", existingMetadataItem.Source },
                                { "winningvotecount", 0 },
                                { "totalvotecount", 0 }
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
                    break;

                default:
                    break;
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

            // access control
            if (objectType == DataObjectType.App)
            {
                // update access control
                sql = "DELETE FROM DataObject_ACL WHERE DataObject_ID=@id";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                    { "id", id }
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

            return await GetDataObject(objectType, id);
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
            switch (objectType)
            {
                case DataObjectType.Company:
                case DataObjectType.Platform:
                case DataObjectType.Game:
                    var retVal = _DataObjectMetadataSearch(objectType, id, ForceSearch);
                    retVal.Wait(new TimeSpan(0, 0, 15));
                    return retVal.Result;

                default:
                    return null;
            }
        }

        private async Task<MatchItem?> _DataObjectMetadataSearch(DataObjectType objectType, long? id, bool ForceSearch)
        {
            MatchItem? DataObjectSearchResults = new MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict;

            List<DataObjectItem> DataObjectsToProcess = new List<DataObjectItem>();

            if (id != null)
            {
                DataObjectsToProcess.Add(await GetDataObject(objectType, (long)id));
            }
            else
            {
                // DataObjectsToProcess.AddRange(GetDataObjects(objectType).Objects);
                sql = @"
                    SELECT DISTINCT
                        DataObject.*, DDMM.LastSearched
                    FROM
                        DataObject
                            JOIN 
                        (SELECT 
                            DataObjectId, LastSearched
                        FROM
                            DataObject_MetadataMap GROUP BY DataObjectId ORDER BY LastSearched) DDMM ON DataObject.Id = DDMM.DataObjectId
                    WHERE
                        ObjectType = @objecttype AND DDMM.LastSearched < @lastsearched
                    ORDER BY DataObject.`Name`
                    LIMIT 10000;
                ";
                dbDict = new Dictionary<string, object>{
                    { "objecttype", objectType },
                    { "lastsearched", DateTime.UtcNow.AddMonths(-1) }
                };

                DataTable data = db.ExecuteCMD(sql, dbDict);
                foreach (DataRow row in data.Rows)
                {
                    DataObjectItem item = await BuildDataObject(objectType, (long)row["Id"], row, false, true);
                    DataObjectsToProcess.Add(item);
                }

                if (DataObjectsToProcess.Count == 0)
                {
                    return null;
                }

                sql = "";
                dbDict = new Dictionary<string, object> { };
            }

            // search for metadata
            int processedObjectCount = 0;
            foreach (DataObjectItem item in DataObjectsToProcess)
            {
                processedObjectCount++;

                Logging.Log(Logging.LogType.Information, "Metadata Match", processedObjectCount + " / " + DataObjectsToProcess.Count + " - Searching for metadata for " + item.Name + " (" + item.ObjectType + ") Id: " + item.Id);

                List<string> SearchCandidates = GetSearchCandidates(item.Name);

                Logging.Log(Logging.LogType.Information, "Metadata Match", "Search candidates: " + string.Join(", ", SearchCandidates));

                foreach (Metadata.Communications.MetadataSources sourceType in Enum.GetValues(typeof(Metadata.Communications.MetadataSources)))
                {
                    if (sourceType == MetadataSources.None)
                    {
                        continue;
                    }

                    // get the metadataItem from the DataObjectItem for the sourceType
                    DataObjectItem.MetadataItem metadata = item.Metadata.Find(x => x.Source == sourceType);
                    bool insertMetadata = false;
                    if (metadata == null)
                    {
                        insertMetadata = true;

                        // create new metadata item
                        metadata = new DataObjectItem.MetadataItem(objectType)
                        {
                            Id = "",
                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                            Source = sourceType,
                            LastSearch = DateTime.UtcNow.AddMonths(-3),
                            NextSearch = DateTime.UtcNow.AddMonths(-1),
                            WinningVoteCount = 0,
                            TotalVoteCount = 0
                        };
                    }

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
                        ) || (
                            metadata.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch &&
                            ForceSearch == true
                        )
                    )
                    {
                        // searching is allowed
                        try
                        {
                            Logging.Log(Logging.LogType.Information, "Metadata Match", "Searching " + metadata.Source + ": " + item.Name + " (" + item.Id + ") Type: " + item.ObjectType);
                            switch (metadata.Source)
                            {
                                case Metadata.Communications.MetadataSources.IGDB:
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
                                                        DataObjectItem platformDO;
                                                        if (attribute.Value.GetType() == typeof(DataObjectItem))
                                                        {
                                                            platformDO = (DataObjectItem)attribute.Value;
                                                        }
                                                        else
                                                        {
                                                            RelationItem relationItem = (RelationItem)attribute.Value;
                                                            platformDO = await GetDataObject(DataObjectType.Platform, relationItem.relationId);
                                                        }

                                                        foreach (DataObjectItem.MetadataItem provider in platformDO.Metadata)
                                                        {
                                                            if (provider.Source == MetadataSources.IGDB && (
                                                                provider.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic ||
                                                                provider.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Manual ||
                                                                provider.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin
                                                                )
                                                            )
                                                            {
                                                                IGDB.Models.Platform platform = await Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Platform>((string?)provider.Id);
                                                                PlatformId = platform.Id;
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            if (PlatformId != null)
                                            {
                                                bool SearchComplete = false;
                                                foreach (string SearchCandidate in SearchCandidates)
                                                {
                                                    foreach (Games.SearchType searchType in Enum.GetValues(typeof(Games.SearchType)))
                                                    {
                                                        IGDB.Models.Game[] games = Games.SearchForGame(SearchCandidate, (long)PlatformId, searchType);
                                                        if (games != null)
                                                        {
                                                            if (games.Length == 1)
                                                            {
                                                                // exact match!
                                                                DataObjectSearchResults = new MatchItem
                                                                {
                                                                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                                    MetadataId = games[0].Slug
                                                                };
                                                                SearchComplete = true;
                                                                break;
                                                            }
                                                            else if (games.Length > 1)
                                                            {
                                                                // too many matches - high likelihood of sequels and other variants
                                                                foreach (Game game in games)
                                                                {
                                                                    if (game.Name == SearchCandidate)
                                                                    {
                                                                        // found game title matches the search candidate
                                                                        DataObjectSearchResults = new MatchItem
                                                                        {
                                                                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                                            MetadataId = game.Slug
                                                                        };
                                                                        SearchComplete = true;
                                                                        break;
                                                                    }
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
                                                DataObjectSearchResults = new MatchItem
                                                {
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

                                case Metadata.Communications.MetadataSources.TheGamesDb:
                                    TheGamesDB.JSON.TheGamesDBDatabase tgdbMetadata = TheGamesDB.JSON.MetadataQuery.metadata;
                                    switch (objectType)
                                    {
                                        case DataObjectType.Platform:
                                            foreach (KeyValuePair<string, TheGamesDB.JSON.TheGamesDBDatabase.IncludeItem.PlatformItem.DataItem> metadataPlatform in tgdbMetadata.include.platform.data)
                                            {
                                                if (
                                                    (metadataPlatform.Value.name == item.Name) ||
                                                    (metadataPlatform.Value.alias == item.Name))
                                                {
                                                    // we have a match, add the tgdb platform id to the data object
                                                    dbDict["metadataid"] = metadataPlatform.Key;
                                                    dbDict["method"] = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                                                    break;
                                                }
                                            }
                                            break;

                                        case DataObjectType.Game:
                                            // get the game platform if set
                                            long tgdbPlaformId = 0;
                                            AttributeItem tgdbPlatformAttribute = item.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.Platform && x.attributeType == AttributeItem.AttributeType.ObjectRelationship);

                                            if (tgdbPlatformAttribute != null)
                                            {
                                                // get the associated platform dataobject
                                                DataObjectItem tgdbPlatformDO;
                                                if (tgdbPlatformAttribute.Value.GetType() == typeof(DataObjectItem))
                                                {
                                                    tgdbPlatformDO = (DataObjectItem)tgdbPlatformAttribute.Value;
                                                }
                                                else
                                                {
                                                    RelationItem relationItem = (RelationItem)tgdbPlatformAttribute.Value;
                                                    tgdbPlatformDO = await GetDataObject(DataObjectType.Platform, relationItem.relationId);
                                                }

                                                // check if tgdbPlatformDO has a configured metadata value for TheGamesDB
                                                DataObjectItem.MetadataItem tgdbPlatformMetadata = tgdbPlatformDO.Metadata.Find(x => x.Source == MetadataSources.TheGamesDb);
                                                if (tgdbPlatformMetadata != null && tgdbPlatformMetadata.Id != "")
                                                {
                                                    // get the platform id
                                                    tgdbPlaformId = long.Parse(tgdbPlatformMetadata.Id);

                                                    // search for games
                                                    bool SearchComplete = false;
                                                    foreach (string SearchCandidate in SearchCandidates)
                                                    {
                                                        foreach (TheGamesDB.JSON.TheGamesDBDatabase.DataItem.GameItem metadataGame in tgdbMetadata.data.games)
                                                        {
                                                            if (metadataGame.platform == tgdbPlaformId)
                                                            {
                                                                if (
                                                                    metadataGame.game_title == SearchCandidate ||
                                                                    (
                                                                        metadataGame.alternates != null &&
                                                                        metadataGame.alternates.Contains(SearchCandidate)
                                                                    )
                                                                )
                                                                {
                                                                    // we have a match, add the tgdb game id to the data object
                                                                    dbDict["metadataid"] = metadataGame.id;
                                                                    dbDict["method"] = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                                                                    SearchComplete = true;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                        if (SearchComplete == true)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            break;
                                    }
                                    break;

                                case MetadataSources.RetroAchievements:
                                    switch (objectType)
                                    {
                                        case DataObjectType.Platform:
                                            // load platforms JSON
                                            string platformsJsonPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_RetroAchievements, "platforms.json");
                                            if (File.Exists(platformsJsonPath))
                                            {
                                                string platformsJson = File.ReadAllText(platformsJsonPath);
                                                List<RetroAchievements.Models.PlatformModel> platforms = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RetroAchievements.Models.PlatformModel>>(platformsJson);

                                                // search for platform
                                                foreach (RetroAchievements.Models.PlatformModel platform in platforms)
                                                {
                                                    if (platform.Name.ToLower().Trim() == item.Name.ToLower().Trim())
                                                    {
                                                        dbDict["metadataid"] = platform.ID;
                                                        dbDict["method"] = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                                                        break;
                                                    }
                                                }
                                            }
                                            break;

                                        case DataObjectType.Game:
                                            // get the game platform if set
                                            long raPlaformId = 0;
                                            AttributeItem raPlatformAttribute = item.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.Platform && x.attributeType == AttributeItem.AttributeType.ObjectRelationship);

                                            if (raPlatformAttribute != null)
                                            {
                                                // get the associated platform dataobject
                                                DataObjectItem raPlatformDO;
                                                if (raPlatformAttribute.Value.GetType() == typeof(DataObjectItem))
                                                {
                                                    raPlatformDO = (DataObjectItem)raPlatformAttribute.Value;
                                                }
                                                else
                                                {
                                                    RelationItem relationItem = (RelationItem)raPlatformAttribute.Value;
                                                    raPlatformDO = await GetDataObject(DataObjectType.Platform, relationItem.relationId);
                                                }

                                                // check if raPlatformDO has a configured metadata value for RetroAchievements
                                                DataObjectItem.MetadataItem raPlatformMetadata = raPlatformDO.Metadata.Find(x => x.Source == MetadataSources.RetroAchievements);
                                                if (raPlatformMetadata != null && raPlatformMetadata.Id != "")
                                                {
                                                    // get the platform id
                                                    raPlaformId = long.Parse(raPlatformMetadata.Id);

                                                    // load games json for the specified raPlatformId
                                                    List<RetroAchievements.Models.GameModel> raGames = new List<RetroAchievements.Models.GameModel>();
                                                    string gamesJsonPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_RetroAchievements, raPlaformId.ToString());
                                                    string[] gamesJsonFiles = Directory.GetFiles(gamesJsonPath, "*.json");
                                                    foreach (string gamesJsonFile in gamesJsonFiles)
                                                    {
                                                        string gamesJson = File.ReadAllText(gamesJsonFile);
                                                        List<RetroAchievements.Models.GameModel> games = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RetroAchievements.Models.GameModel>>(gamesJson);
                                                        raGames.AddRange(games);
                                                    }

                                                    // search for games
                                                    bool SearchComplete = false;
                                                    foreach (string SearchCandidate in SearchCandidates)
                                                    {
                                                        foreach (RetroAchievements.Models.GameModel metadataGame in raGames)
                                                        {
                                                            if (metadataGame.ConsoleID == raPlaformId)
                                                            {
                                                                // strip leading tags from game name. e.g. "~hack~ ~demo~" or "~hack~"
                                                                string gameName = metadataGame.Title;
                                                                string category = "";
                                                                if (gameName.StartsWith("~"))
                                                                {
                                                                    string pattern = @"~(.*?)~\s";
                                                                    MatchCollection matches = Regex.Matches(gameName, pattern);
                                                                    foreach (Match match in matches)
                                                                    {
                                                                        if (category.Length > 1)
                                                                        {
                                                                            category += ",";
                                                                        }
                                                                        category += match.Groups[1].Value.Trim();
                                                                    }

                                                                    // set gameName to everything after the last "~ "
                                                                    gameName = gameName.Substring(gameName.LastIndexOf("~ ") + 2);
                                                                }
                                                                if (gameName == SearchCandidate)
                                                                {
                                                                    // we have a match, add the tgdb game id to the data object
                                                                    dbDict["metadataid"] = metadataGame.ID;
                                                                    dbDict["method"] = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                                                                    SearchComplete = true;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                        if (SearchComplete == true)
                                                        {
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            break;
                                    }
                                    break;
                            }

                            Logging.Log(Logging.LogType.Information, "Metadata Match", processedObjectCount + " / " + DataObjectsToProcess.Count + " - " + item.ObjectType + " " + item.Name + " " + metadata.MatchMethod + " to " + metadata.Source + " metadata: " + metadata.Id);

                            if (insertMetadata == true)
                            {
                                sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metadataid, @srcid, @method, @lastsearched, @nextsearch);";
                            }
                            else
                            {
                                sql = "UPDATE DataObject_MetadataMap SET MetadataId=@metadataid, MatchMethod=@method, LastSearched=@lastsearched, NextSearch=@nextsearch WHERE DataObjectId=@id AND SourceId=@srcid;";
                            }
                            db.ExecuteNonQuery(sql, dbDict);
                        }
                        catch (Exception ex)
                        {
                            Logging.Log(Logging.LogType.Warning, "Metadata Match", processedObjectCount + " / " + DataObjectsToProcess.Count + " - Error processing metadata search", ex);
                        }
                    }
                }

                // get metadata cover if new object is a game
                if (objectType == DataObjectType.Game)
                {
                    BackgroundMetadataMatcher.BackgroundMetadataMatcher metadataMatcher = new BackgroundMetadataMatcher.BackgroundMetadataMatcher();
                    await metadataMatcher.GetGameArtwork((long)item.Id);
                }

                // update date
                UpdateDataObjectDate((long)item.Id);
            }

            return DataObjectSearchResults;
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

            // remove duplicates
            SearchCandidates = SearchCandidates.Distinct().ToList();

            // remove any empty candidates
            SearchCandidates.RemoveAll(x => string.IsNullOrWhiteSpace(x));

            Logging.Log(Logging.LogType.Information, "Import Game", "Search candidates: " + String.Join(", ", SearchCandidates));

            return SearchCandidates;
        }

        private async Task<MatchItem> GetDataObject<T>(MetadataSources Source, string Endpoint, string Fields, string Query)
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

            // apply changes if commit = true
            if (commit == true)
            {
                EditDataObject(targetObject.ObjectType, targetObject.Id, targetObject);
                DataObjectMetadataSearch(targetObject.ObjectType, targetObject.Id, false);
                UpdateDataObjectDate(targetObject.Id);
                DeleteDataObject(sourceObject.ObjectType, sourceObject.Id);
            }

            return targetObject;
        }
    }
}