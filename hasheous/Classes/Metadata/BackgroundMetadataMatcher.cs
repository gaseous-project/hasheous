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
                GetGameArtwork((long)row["Id"]);
            }
        }

        public void GetGameArtwork(long DataObjectId)
        {
            DataObjects dataObjects = new DataObjects();
            DataObjectItem dataObjectItem = dataObjects.GetDataObject(DataObjects.DataObjectType.Game, DataObjectId);

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

                // only add a logo if it isn't already present
                if (logoPresent == false)
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
                                        Game game = hasheous_server.Classes.Metadata.IGDB.Games.GetGame(metadata.Id, false, false, false);
                                        if (game.Cover != null)
                                        {
                                            if (game.Cover.Id != null)
                                            {
                                                Cover cover = Covers.GetCover((long)game.Cover.Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Game(game));
                                                if (cover != null)
                                                {
                                                    string CoverPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Game(game), "Cover.png");
                                                    if (File.Exists(CoverPath))
                                                    {
                                                        Images images = new Images();
                                                        coverProvider = Communications.MetadataSources.IGDB;
                                                        imageRef = images.AddImage("Cover.png", File.ReadAllBytes(CoverPath)) + ":" + coverProvider.ToString();
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                }

                                if (imageRef != null)
                                {
                                    dataObjects.AddAttribute(DataObjectId, new AttributeItem
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