using System.Data;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Classes;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using IGDB.Models;
using NuGet.Common;
using static Classes.Common;

namespace Classes
{
	public class HashLookup2
    {
        public HashLookup2()
        {

        }

        public HashLookup2(Database db, HashLookupModel model)
        {
            SignatureManagement signature = new SignatureManagement();
            // get the raw signature
            List<Signatures_Games_2> rawSignatures = signature.GetRawSignatures(model);

            // narrow down the options
            Signatures_Games_2 discoveredSignature = new Signatures_Games_2();
            if (rawSignatures.Count == 0)
            {
                Signature = null;
                MetadataResults = null;
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
                DataObjectItem? publisher = GetDataObjectFromSignatureId(db, DataObjects.DataObjectType.Company, discoveredSignature.Game.PublisherId);
                if (publisher == null)
                {
                    // no returned publisher! create one
                    publisher = dataObjects.NewDataObject(DataObjects.DataObjectType.Company, new DataObjectItemModel{
                        Name = discoveredSignature.Game.Publisher
                    });
                    // add signature mappinto to publisher
                    dataObjects.AddSignature(publisher.Id, DataObjects.DataObjectType.Company, discoveredSignature.Game.PublisherId);
                }

                // platform
                DataObjectItem? platform = GetDataObjectFromSignatureId(db, DataObjects.DataObjectType.Platform, discoveredSignature.Game.SystemId);
                if (platform == null)
                {
                    // no returned platform! create one
                    platform = dataObjects.NewDataObject(DataObjects.DataObjectType.Platform, new DataObjectItemModel{
                        Name = discoveredSignature.Game.System
                    });
                    // add signature mapping to platform
                    dataObjects.AddSignature(platform.Id, DataObjects.DataObjectType.Platform, discoveredSignature.Game.SystemId);
                }

                // game
                DataObjectItem? game = GetDataObjectFromSignatureId(db, DataObjects.DataObjectType.Game, long.Parse(discoveredSignature.Game.Id));
                if (game == null)
                {
                    // no returned game! create one
                    game = dataObjects.NewDataObject(DataObjects.DataObjectType.Game, new DataObjectItemModel{
                        Name = discoveredSignature.Game.Name
                    });
                    // add signature mapping to game
                    dataObjects.AddSignature(game.Id, DataObjects.DataObjectType.Game, long.Parse(discoveredSignature.Game.Id));
                    
                    // add country attribute
                    if (discoveredSignature.Game.Countries != null)
                    {
                        dataObjects.AddAttribute(game.Id, new AttributeItem{
                            attributeName = AttributeItem.AttributeName.Country,
                            attributeType = AttributeItem.AttributeType.ShortString,
                            Value = string.Join("; ", discoveredSignature.Game.Countries.Select(x => x.Value + " (" + x.Key + ")").ToArray())
                        });
                    }
                    // add language attribute
                    if (discoveredSignature.Game.Languages != null)
                    {
                        dataObjects.AddAttribute(game.Id, new AttributeItem{
                            attributeName = AttributeItem.AttributeName.Language,
                            attributeType = AttributeItem.AttributeType.ShortString,
                            Value = string.Join("; ", discoveredSignature.Game.Languages.Select(x => x.Value).ToArray())
                        });
                    }
                    // add platform reference
                    dataObjects.AddAttribute(game.Id, new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Platform,
                        attributeType = AttributeItem.AttributeType.ObjectRelationship,
                        attributeRelationType = DataObjects.DataObjectType.Platform,
                        Value = platform.Id
                    });
                    // add publisher reference
                    dataObjects.AddAttribute(game.Id, new AttributeItem{
                        attributeName = AttributeItem.AttributeName.Publisher,
                        attributeType = AttributeItem.AttributeType.ObjectRelationship,
                        attributeRelationType = DataObjects.DataObjectType.Company,
                        Value = publisher.Id
                    });

                    // force metadata search
                    dataObjects.DataObjectMetadataSearch(DataObjects.DataObjectType.Game, game.Id, true);
                }

                // build return item
                Signature = new SignatureLookupItem.SignatureResult(discoveredSignature);
                
            }
        }

        /// <summary>
        /// Get DataObject from signature sigId
        /// </summary>
        /// <param name="db">The database connection to use</param>
        /// <param name="objectType">The type of the object to retrieve</param>
        /// <param name="sigId">The signature id to search for</param>
        /// <returns>Null if not found; otherwise returns a DataObjectItem of type objectType</returns>
        private DataObjectItem? GetDataObjectFromSignatureId(Database db, DataObjects.DataObjectType objectType, long sigId)
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
                DataObjectItem item = dataObject.GetDataObject(objectType, (long)data.Rows[0][0]);
                return item;
            }
            else
            {
                return null;
            }
        }

        public SignatureLookupItem.SignatureResult? Signature { get; set; }
        public List<SignatureLookupItem.MetadataResult>? MetadataResults { get; set; }

        
    }
}