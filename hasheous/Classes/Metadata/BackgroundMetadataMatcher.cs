using System;
using System.IO;
using MySqlConnector;
using gaseous_signature_parser.models.RomSignatureObject;
using System.Data;
using Classes;
using IGDB;
using IGDB.Models;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using hasheous_server.Classes;
using System.Threading.Tasks;

namespace BackgroundMetadataMatcher
{
    public class BackgroundMetadataMatcher
    {
        public BackgroundMetadataMatcher()
        {

        }

        /// <summary>
        /// The method used to match the signature to the IGDB source
        /// </summary>
        public enum MatchMethod
        {
            /// <summary>
            /// No match
            /// </summary>
            NoMatch = 0,

            /// <summary>
            /// Automatic matches are subject to change - depending on IGDB
            /// </summary>
            Automatic = 1,

            /// <summary>
            /// Manual matches will never change
            /// </summary>
            Manual = 2,

            /// <summary>
            /// Too many matches to successfully match
            /// </summary>
            AutomaticTooManyMatches = 3,

            /// <summary>
            /// Manually set by an admin - will never change unless set by an admin
            /// </summary>
            ManualByAdmin = 4,

            /// <summary>
            /// Match made by vote
            /// </summary>
            Voted = 5
        }

        public void GetGamesWithoutArtwork()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = @"
                SELECT
                    *
                FROM
                    `DataObject`
                LEFT JOIN
                    `DataObject_MetadataMap` ON `DataObject_MetadataMap`.`DataObjectId` = `DataObject`.`Id`
                LEFT JOIN
                    (
                        SELECT
                            *
                        FROM
                            `DataObject_Attributes`
                        WHERE
                            `DataObject_Attributes`.`AttributeName` = 3
                    ) `Attr` ON `Attr`.`DataObjectId` = `DataObject`.`Id`
                WHERE
                    `DataObject`.`ObjectType` = 2 AND
                    (`DataObject_MetadataMap`.`MetadataId` IS NOT NULL AND `DataObject_MetadataMap`.`MetadataId` <> "") AND
                    (`Attr`.`AttributeValue` IS NULL OR `Attr`.`AttributeValue` = "");
            ";

            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>());
            foreach (DataRow row in data.Rows)
            {
                Logging.Log(Logging.LogType.Information, "Background Metadata Matcher", "Getting artwork for game " + (string)row["Name"]);
                _ = GetGameArtwork((long)row["Id"]);
            }
        }

        public async Task GetGameArtwork(long DataObjectId, bool force = false)
        {
            DataObjects dataObjects = new DataObjects();
            DataObjectItem dataObjectItem = await dataObjects.GetDataObject(DataObjects.DataObjectType.Game, DataObjectId);

            if (dataObjectItem != null)
            {
                // check for cover
                bool logoPresent = false;
                foreach (AttributeItem attribute in dataObjectItem.Attributes)
                {
                    if (
                        attribute.attributeType == AttributeItem.AttributeType.ImageId &&
                        attribute.attributeName == AttributeItem.AttributeName.Logo
                    )
                    {
                        logoPresent = true;
                        break;
                    }
                }

                // only add a logo if it isn't already present or force is true
                if (logoPresent == false || force)
                {
                    // check for metadata source
                    foreach (DataObjectItem.MetadataItem metadata in dataObjectItem.Metadata)
                    {
                        if (
                            metadata.MatchMethod == MatchMethod.Automatic ||
                            metadata.MatchMethod == MatchMethod.Manual ||
                            metadata.MatchMethod == MatchMethod.ManualByAdmin ||
                            metadata.MatchMethod == MatchMethod.Voted
                        )
                        {
                            string? imageRef = null;
                            Communications.MetadataSources? coverProvider = null;

                            if (metadata.Id.Length > 0)
                            {
                                switch (metadata.Source)
                                {
                                    case Communications.MetadataSources.IGDB:
                                        // get game metadata
                                        Game game;

                                        // check if metadata.id is a long
                                        if (long.TryParse(metadata.Id, out long metadataId))
                                        {
                                            game = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(metadataId);
                                        }
                                        else
                                        {
                                            // if not, try to get it by name
                                            game = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(metadata.Id);
                                        }
                                        if (game.Cover != null)
                                        {
                                            if (game.Cover.Id != null)
                                            {
                                                Cover cover = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Cover>((long)game.Cover.Id);
                                                if (cover != null)
                                                {
                                                    string CoverPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Game(game), "Cover.jpg");
                                                    if (!File.Exists(CoverPath))
                                                    {
                                                        // download the cover image
                                                        if (!Directory.Exists(Path.GetDirectoryName(CoverPath)))
                                                        {
                                                            Directory.CreateDirectory(Path.GetDirectoryName(CoverPath));
                                                        }

                                                        using (var client = new System.Net.Http.HttpClient())
                                                        {
                                                            Uri coverUri = new Uri("https://images.igdb.com/igdb/image/upload/t_original/" + cover.ImageId + ".jpg");

                                                            var response = await client.GetAsync(coverUri);
                                                            if (response.IsSuccessStatusCode)
                                                            {
                                                                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                                                                await File.WriteAllBytesAsync(CoverPath, imageBytes);
                                                            }
                                                            else
                                                            {
                                                                Logging.Log(Logging.LogType.Warning, "Background Metadata Matcher", "Failed to download cover image for game: " + game.Name);
                                                                return;
                                                            }
                                                        }
                                                    }

                                                    if (File.Exists(CoverPath))
                                                    {
                                                        Images images = new Images();
                                                        coverProvider = Communications.MetadataSources.IGDB;
                                                        imageRef = images.AddImage("Cover.jpg", File.ReadAllBytes(CoverPath)) + ":" + coverProvider.ToString();
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                }

                                // delete existing logo if it exists
                                foreach (AttributeItem attribute in dataObjectItem.Attributes)
                                {
                                    if (
                                        attribute.attributeType == AttributeItem.AttributeType.ImageId &&
                                        attribute.attributeName == AttributeItem.AttributeName.Logo
                                    )
                                    {
                                        dataObjects.DeleteAttribute(DataObjectId, (long)attribute.Id);
                                    }
                                }

                                if (imageRef != null)
                                {
                                    // add the new logo
                                    await dataObjects.AddAttribute(DataObjectId, new AttributeItem
                                    {
                                        attributeName = AttributeItem.AttributeName.Logo,
                                        attributeType = AttributeItem.AttributeType.ImageId,
                                        attributeRelationType = DataObjects.DataObjectType.None,
                                        Value = imageRef
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}