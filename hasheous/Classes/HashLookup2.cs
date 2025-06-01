using System.Data;
using System.Security.Cryptography.Xml;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Classes;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using IGDB.Models;
using Newtonsoft.Json;
using NuGet.Common;
using static Classes.Common;

namespace Classes
{
    public class HashLookup
    {
        public class HashNotFoundException : Exception
        {
            public HashNotFoundException()
            {
            }

            public HashNotFoundException(string message)
                : base(message)
            {
            }

            public HashNotFoundException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Database db { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public hasheous_server.Models.HashLookupModel model { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public bool returnAllSources { get; set; } = false;

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string returnFields { get; set; } = "All";

        public enum ValidFields
        {
            All,
            Publisher,
            Platform,
            Signatures,
            Metadata,
            Attributes
        }

        public HashLookup()
        {

        }

        public HashLookup(Database db, hasheous_server.Models.HashLookupModel model, bool? returnAllSources = false, string? returnFields = "All")
        {
            this.db = db;
            this.model = model;
            this.returnAllSources = returnAllSources ?? false;
            this.returnFields = returnFields ?? "All";
        }

        public async Task PerformLookup()
        {
            // parse return fields
            List<ValidFields> validFields = new List<ValidFields>();
            if (returnFields == "All")
            {
                validFields.Add(ValidFields.All);
            }
            else
            {
                string[] fields = returnFields.Split(',');
                foreach (string field in fields)
                {
                    if (Enum.TryParse<ValidFields>(field.Trim(), true, out ValidFields validField))
                    {
                        validFields.Add(validField);
                    }
                }
            }

            SignatureManagement signature = new SignatureManagement();
            // get the raw signature
            List<Signatures_Games_2> rawSignatures = await signature.GetRawSignatures(model);

            // narrow down the options
            Signatures_Games_2 discoveredSignature = new Signatures_Games_2();
            if (rawSignatures.Count == 0)
            {
                throw new HashNotFoundException("The provided hash was not found in any signature database.");
            }
            else
            {
                if (rawSignatures.Count == 1)
                {
                    // only 1 signature found!
                    discoveredSignature = rawSignatures.ElementAt(0);
                }
                else if (rawSignatures.Count > 1)
                {
                    // more than one signature found - find one with highest score
                    foreach (Signatures_Games_2 Sig in rawSignatures)
                    {
                        if (Sig.Score > discoveredSignature.Score)
                        {
                            discoveredSignature = Sig;
                        }
                    }
                }

                // should only have one signature now
                // compile metadata
                DataObjects dataObjects = new DataObjects();

                // publisher
                DataObjectItem? publisher = null;
                if (validFields.Contains(ValidFields.Publisher) || validFields.Contains(ValidFields.All))
                {
                    publisher = await GetDataObjectFromSignatureId(db, DataObjects.DataObjectType.Company, discoveredSignature.Game.PublisherId);
                    if (publisher == null)
                    {
                        // no returned publisher! create one
                        publisher = await dataObjects.NewDataObject(DataObjects.DataObjectType.Company, new DataObjectItemModel
                        {
                            Name = discoveredSignature.Game.Publisher
                        });
                        // add signature mappinto to publisher
                        dataObjects.AddSignature(publisher.Id, DataObjects.DataObjectType.Company, discoveredSignature.Game.PublisherId);

                        // force metadata search
                        dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Company, publisher.Id, true);

                        // re-get the publisher
                        publisher = await dataObjects.GetDataObject(DataObjects.DataObjectType.Company, publisher.Id);
                    }
                }

                // platform
                DataObjectItem? platform = null;
                if (validFields.Contains(ValidFields.Platform) || validFields.Contains(ValidFields.All))
                {
                    platform = await GetDataObjectFromSignatureId(db, DataObjects.DataObjectType.Platform, discoveredSignature.Game.SystemId);
                    if (platform == null)
                    {
                        // no returned platform! create one
                        platform = await dataObjects.NewDataObject(DataObjects.DataObjectType.Platform, new DataObjectItemModel
                        {
                            Name = discoveredSignature.Game.System
                        });
                        // add signature mapping to platform
                        dataObjects.AddSignature(platform.Id, DataObjects.DataObjectType.Platform, discoveredSignature.Game.SystemId);

                        // force metadata search
                        dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Platform, platform.Id, true);

                        // re-get the platform
                        platform = await dataObjects.GetDataObject(DataObjects.DataObjectType.Platform, platform.Id);
                    }
                }

                // game
                DataObjectItem? game = await GetDataObjectFromSignatureId(db, DataObjects.DataObjectType.Game, long.Parse(discoveredSignature.Game.Id));
                if (game == null)
                {
                    // no returned game! trim up the name and check if one exists with the same name and platform

                    // remove version numbers from name
                    string gameName = discoveredSignature.Game.Name;
                    gameName = Regex.Replace(gameName, @"v(\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();
                    gameName = Regex.Replace(gameName, @"Rev (\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();

                    // assumption: no games have () in their titles so we'll remove them
                    int idx = gameName.IndexOf('(');
                    if (idx >= 0)
                    {
                        gameName = gameName.Substring(0, idx);
                    }

                    // check if the game exists - create a new one if it doesn't
                    game = await dataObjects.SearchDataObject(DataObjects.DataObjectType.Game, gameName, new List<DataObjects.DataObjectSearchCriteriaItem>
                    {
                        new DataObjects.DataObjectSearchCriteriaItem
                        {
                            Field = AttributeItem.AttributeName.Platform,
                            Value = platform.Id.ToString()
                        }
                    });
                    if (game == null)
                    {
                        game = await dataObjects.NewDataObject(DataObjects.DataObjectType.Game, new DataObjectItemModel
                        {
                            Name = gameName
                        });

                        // add platform reference
                        await dataObjects.AddAttribute(game.Id, new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Platform,
                            attributeType = AttributeItem.AttributeType.ObjectRelationship,
                            attributeRelationType = DataObjects.DataObjectType.Platform,
                            Value = platform.Id
                        });
                        // add publisher reference
                        await dataObjects.AddAttribute(game.Id, new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.Publisher,
                            attributeType = AttributeItem.AttributeType.ObjectRelationship,
                            attributeRelationType = DataObjects.DataObjectType.Company,
                            Value = publisher.Id
                        });
                        // force metadata search
                        dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Game, game.Id, true);
                    }

                    // add signature mapping to game
                    dataObjects.AddSignature(game.Id, DataObjects.DataObjectType.Game, long.Parse(discoveredSignature.Game.Id));

                    // add all raw signatures to the game
                    foreach (Signatures_Games_2 sig in rawSignatures)
                    {
                        if (sig.Game != null)
                        {
                            if (sig.Game.Id != discoveredSignature.Game.Id)
                            {
                                dataObjects.AddSignature(game.Id, DataObjects.DataObjectType.Game, long.Parse(sig.Game.Id));
                            }
                        }
                    }

                    // VIMMSLair manual search
                    foreach (AttributeItem attribute in platform.Attributes)
                    {
                        if (attribute.attributeName == AttributeItem.AttributeName.VIMMPlatformName)
                        {
                            // ensure the signature is a No-Intros one
                            Signatures_Games_2? sig = null;
                            foreach (Signatures_Games_2 s in rawSignatures)
                            {
                                if (s.Rom.SignatureSource == RomSignatureObject.Game.Rom.SignatureSourceType.NoIntros)
                                {
                                    sig = s;
                                    break;
                                }
                            }

                            if (sig != null)
                            {
                                string platformName = (string)attribute.Value;

                                string manualId = await VIMMSLair.ManualSearch.Search(platformName, Path.GetFileNameWithoutExtension(discoveredSignature.Rom.Name));
                                if (manualId != "")
                                {
                                    // add manual reference
                                    await dataObjects.AddAttribute(game.Id, new AttributeItem
                                    {
                                        attributeName = AttributeItem.AttributeName.VIMMManualId,
                                        attributeType = AttributeItem.AttributeType.ShortString,
                                        attributeRelationType = DataObjects.DataObjectType.Game,
                                        Value = manualId
                                    });
                                    break;
                                }
                            }
                        }
                    }

                    // re-get the game
                    game = await dataObjects.GetDataObject(DataObjects.DataObjectType.Game, game.Id);
                }

                // build return item
                this.Id = game.Id;
                this.Name = game.Name;
                if (platform != null)
                {
                    this.Platform = new MiniDataObjectItem
                    {
                        Name = platform.Name,
                        metadata = platform.Metadata
                    };
                }
                if (publisher != null)
                {
                    this.Publisher = new MiniDataObjectItem
                    {
                        Name = publisher.Name,
                        metadata = publisher.Metadata
                    };
                }

                if (validFields.Contains(ValidFields.Signatures) || validFields.Contains(ValidFields.All))
                {
                    if (returnAllSources == true)
                    {
                        // get all signatures
                        this.Signatures = new Dictionary<RomSignatureObject.Game.Rom.SignatureSourceType, List<SignatureLookupItem.SignatureResult>>();
                        foreach (Signatures_Games_2 sig in rawSignatures)
                        {
                            if (!this.Signatures.ContainsKey(sig.Rom.SignatureSource))
                            {
                                this.Signatures.Add(sig.Rom.SignatureSource, new List<SignatureLookupItem.SignatureResult>());
                            }
                            this.Signatures[sig.Rom.SignatureSource].Add(new SignatureLookupItem.SignatureResult(sig));
                        }
                    }
                    else
                    {
                        this.Signature = new SignatureLookupItem.SignatureResult(discoveredSignature);
                    }
                }

                if (validFields.Contains(ValidFields.Metadata) || validFields.Contains(ValidFields.All))
                {
                    this.Metadata = game.Metadata;
                }

                // attributes
                if (validFields.Contains(ValidFields.Attributes) || validFields.Contains(ValidFields.All))
                {
                    this.Attributes = new List<AttributeItemCompiled>();
                    foreach (AttributeItem attribute in game.Attributes)
                    {
                        switch (attribute.attributeName)
                        {
                            case AttributeItem.AttributeName.Publisher:
                            case AttributeItem.AttributeName.Platform:
                            case AttributeItem.AttributeName.ROMs:
                            case AttributeItem.AttributeName.Country:
                                break;

                            default:
                                AttributeItemCompiled attributeItemCompiled = new AttributeItemCompiled
                                {
                                    Id = attribute.Id,
                                    attributeName = attribute.attributeName,
                                    attributeRelationType = attribute.attributeRelationType,
                                    attributeType = attribute.attributeType,
                                    Value = attribute.Value
                                };
                                this.Attributes.Add(attributeItemCompiled);
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get DataObject from signature sigId
        /// </summary>
        /// <param name="db">The database connection to use</param>
        /// <param name="objectType">The type of the object to retrieve</param>
        /// <param name="sigId">The signature id to search for</param>
        /// <returns>Null if not found; otherwise returns a DataObjectItem of type objectType</returns>
        private async Task<DataObjectItem?> GetDataObjectFromSignatureId(Database db, DataObjects.DataObjectType objectType, long sigId)
        {
            string sql = @"
                SELECT 
                    DataObjectId 
                FROM
                    DataObject_SignatureMap
                WHERE
                    SignatureId = @sigid AND DataObjectTypeId = @typeid
            ;";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "sigid", sigId },
                { "typeid",  objectType }
            });

            if (data.Rows.Count > 0)
            {
                DataObjects dataObject = new DataObjects();
                DataObjectItem item = await dataObject.GetDataObject(objectType, (long)data.Rows[0][0]);
                return item;
            }
            else
            {
                return null;
            }
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public MiniDataObjectItem Platform { get; set; }
        public MiniDataObjectItem Publisher { get; set; }

        public SignatureLookupItem.SignatureResult? Signature { get; set; }
        public Dictionary<RomSignatureObject.Game.Rom.SignatureSourceType, List<SignatureLookupItem.SignatureResult>>? Signatures { get; set; }
        public List<DataObjectItem.MetadataItem>? Metadata { get; set; }
        public List<AttributeItemCompiled>? Attributes { get; set; }

        public class MiniDataObjectItem
        {
            public string Name { get; set; }
            public List<DataObjectItem.MetadataItem> metadata { get; set; }
        }
    }
}