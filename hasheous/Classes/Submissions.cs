using System.Data;
using System.Threading.Tasks;
using Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Models;
// using HasheousClient.Models.Metadata.IGDB;
using static hasheous_server.Models.DataObjectItem;

namespace hasheous_server.Classes
{
    // Custom exception for duplicate archive observations
    public class DuplicateArchiveObservationException : Exception
    {
        public DuplicateArchiveObservationException(string message) : base(message) { }
    }

    public class Submissions
    {
        /// <summary>
        /// Handle vote submissions from users
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<SubmissionsMatchFixModel> AddVote(string UserId, SubmissionsMatchFixModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            long? dataObjectId = null;
            if (model.DataObjectId.HasValue)
            {
                // if DataObjectId is provided, use it
                dataObjectId = model.DataObjectId.Value;
            }
            else
            {
                // if DataObjectId is not provided, look it up based on the hashes
                HashLookup hashLookup = new HashLookup(db, new Models.HashLookupModel
                {
                    MD5 = model.MD5,
                    SHA1 = model.SHA1,
                    SHA256 = model.SHA256,
                    CRC = model.CRC
                });
                await hashLookup.PerformLookup();

                if (hashLookup != null)
                {
                    dataObjectId = hashLookup.Id;
                    if (dataObjectId == null)
                    {
                        throw new HashLookup.HashNotFoundException("The provided hashes did not match any records in the database.");
                    }
                }
            }

            if (dataObjectId != null)
            {
                // rom hash was found - store vote
                // all votes for valid hashes are stored
                foreach (SubmissionsMatchFixModel.MetadataMatch metadataMatch in model.MetadataMatches)
                {
                    // before inserting or updating, check that the metadata source has the data
                    bool AllowInsert = false;
                    switch (metadataMatch.Source)
                    {
                        case Communications.MetadataSources.IGDB:
                            IGDB.Models.Game game = await hasheous_server.Classes.Metadata.IGDB.Metadata.GetMetadata<IGDB.Models.Game>(metadataMatch.GameId);
                            if (game != null)
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.TheGamesDb:
                            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
                            {
                                query = metadataMatch.GameId,
                                queryField = TheGamesDB.SQL.QueryModel.QueryFieldName.id,
                                fieldList = "*",
                                includeList = "",
                                page = 1,
                                pageSize = 10
                            };

                            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
                            HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID? games = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID>(queryModel);
                            if (games != null && games.data != null && games.data.count > 0)
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.GiantBomb:
                        case Communications.MetadataSources.RetroAchievements:
                            // only requirement is the id is a number
                            if (int.TryParse(metadataMatch.GameId, out _))
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.EpicGameStore:
                            // url must be in the domain store.epicgames.com
                            string url = metadataMatch.GameId;
                            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Host.Contains("store.epicgames.com"))
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.Steam:
                            // url must be in the domain store.steampowered.com
                            url = metadataMatch.GameId;
                            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Host.Contains("store.steampowered.com"))
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.GOG:
                            // url must be in the domain gog.com
                            url = metadataMatch.GameId;
                            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Host.Contains("www.gog.com"))
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.SteamGridDb:
                            // id must be a long number
                            if (long.TryParse(metadataMatch.GameId, out _))
                            {
                                AllowInsert = true;
                            }
                            break;

                        case Communications.MetadataSources.Wikipedia:
                            // url must be a valid Wikipedia URL
                            url = metadataMatch.GameId;
                            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Host.Contains("wikipedia.org"))
                            {
                                AllowInsert = true;
                            }
                            break;

                        default:
                            // other sources can be added here
                            AllowInsert = true; // default to true for other sources
                            break;
                    }

                    if (AllowInsert == true)
                    {
                        // check for an existing vote - users only get one vote per game
                        // if a user submits an existing vote, it will be updated
                        string sql = "SELECT * FROM MatchUserVotes WHERE UserId = @userId AND DataObjectId = @dataObjectId AND MetadataSourceId = @metadataSourceId";
                        DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                            { "userId", UserId },
                            { "dataObjectId", dataObjectId },
                            { "metadataSourceId", metadataMatch.Source }
                        });

                        if (data.Rows.Count == 0)
                        {
                            // no existing vote - insert a new record
                            sql = "INSERT INTO MatchUserVotes (DataObjectId, UserId, MetadataSourceId, MetadataGameId) VALUES (@dataObjectId, @userId, @metadataSourceId, @metadataGameId)";
                            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                { "dataObjectId", dataObjectId },
                                { "userId", UserId },
                                { "metadataSourceId", metadataMatch.Source },
                                { "metadataGameId", metadataMatch.GameId }
                            });
                        }
                        else
                        {
                            // update existing vote if different
                            if (data.Rows[0]["MetadataGameId"].ToString() != metadataMatch.GameId)
                            {
                                sql = "UPDATE MatchUserVotes SET MetadataGameId = @metadataGameId WHERE UserId = @userId AND DataObjectId = @dataObjectId AND MetadataSourceId = @metadataSourceId";
                                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                    { "dataObjectId", dataObjectId },
                                    { "userId", UserId },
                                    { "metadataSourceId", metadataMatch.Source },
                                    { "metadataGameId", metadataMatch.GameId }
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                // hash was not found - we'll need to add a new hash record
                // TODO: add a new hash record on vote submission
            }

            return model;
        }

        /// <summary>
        /// Tally the votes and apply the results where appropriate
        /// </summary>
        public async Task TallyVotes()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // select all dataobjects that have votes
            string sql = "SELECT DISTINCT DataObjectId FROM MatchUserVotes;";
            DataTable dataObjectsWithVotes = db.ExecuteCMD(sql);

            DataObjects dataObjects = new DataObjects();

            foreach (DataRow objectsRow in dataObjectsWithVotes.Rows)
            {
                long dataObjectId = (long)objectsRow["DataObjectId"];

                // get the dataobject
                DataObjectItem? dataObject = await dataObjects.GetDataObject(dataObjectId);

                // only process dataobjects that are not null
                if (dataObject == null)
                {
                    continue;
                }

                // only process dataobjects that are of type Game, or Company
                DataObjects.DataObjectType[] validTypes = new DataObjects.DataObjectType[] {
                    DataObjects.DataObjectType.Game,
                    DataObjects.DataObjectType.Company
                };

                if (!validTypes.Contains(dataObject.ObjectType))
                {
                    continue;
                }

                // get votes for each metadata source for this dataobject
                foreach (Communications.MetadataSources metadataSource in Enum.GetValues(typeof(Communications.MetadataSources)))
                {
                    // calculate votes
                    sql = "SELECT DataObjectId, MetadataSourceId, MetadataGameId, COUNT(*) AS `Votes` FROM MatchUserVotes WHERE DataObjectId = @dataObjectId AND MetadataSourceId = @metadataSourceId GROUP BY MetadataSourceId, MetadataGameId ORDER BY DataObjectId, MetadataSourceId, `Votes` DESC;";
                    DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                        { "dataObjectId", dataObject.Id },
                        { "metadataSourceId", metadataSource }
                    });

                    if (data.Rows.Count > 0)
                    {
                        // existing record - update
                        // get total votes
                        int totalVoteCount = 0;
                        foreach (DataRow row in data.Rows)
                        {
                            totalVoteCount += (int)(long)row["Votes"];
                        }

                        // update metadata
                        await SetMetadataValue(dataObject, true, metadataSource, data.Rows[0]["MetadataGameId"].ToString(), (uint)(long)data.Rows[0]["Votes"], (uint)totalVoteCount);
                    }
                }
            }
        }

        private async Task SetMetadataValue(DataObjectItem dataObject, bool Update, Communications.MetadataSources metadataSource, string MetadataGameId, uint WinningVoteCount, uint TotalVoteCount)
        {
            // can we update the value?
            // rules:
            // 1. do not update metadata if the value is already correct
            // 2. do not update metadata set manually, or manually by admin
            // 3. do not update metadata set automatically unless we have a winningvotecount of at least 3 (meaning at least three people agree it's correct) unless the metadata source match method is set to NoMatch

            MetadataItem? metadataItem = dataObject.Metadata.FirstOrDefault(m => m.Source == metadataSource);
            if (metadataItem == null)
            {
                // no existing metadata item - insert a new one
                // this satisfies rule 3 with a NoMatch match method
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, WinningVoteCount, TotalVoteCount) VALUES (@dataobjectid, @metadataId, @sourceId, @matchmethod, @winningvotecount, @totalvotecount);";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                    { "dataobjectid", dataObject.Id },
                    { "metadataId", MetadataGameId },
                    { "sourceId", metadataSource },
                    { "matchmethod", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Voted },
                    { "winningvotecount", WinningVoteCount },
                    { "totalvotecount", TotalVoteCount }
                });
                return;
            }

            // if we've gotten here, then we have an existing metadata item
            // check the match method - this satisfies rule 2
            if (metadataItem.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Manual || metadataItem.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin)
            {
                // no update required
                return;
            }

            // if we've gotten here, then we can update the metadata item
            // check the winning vote count
            // if the winning vote count is less than 3, then we do not update the metadata item
            // unless the match method is set to NoMatch, in which case we allow the update
            if (metadataItem.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch || WinningVoteCount >= 3)
            {
                // update the metadata item
                // if Update is true, then we update the existing record
                // if Update is false, then we insert a new record
                // this allows us to keep track of the history of metadata matches
                // Rule 1. do not update metadata if the value is already correct
                if (metadataItem.Id == MetadataGameId && metadataItem.Source == metadataSource)
                {
                    // no update required - values are the same
                    // this satisfies rule 1
                    return;
                }

                // all rules satisfied, we can update the metadata item
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "UPDATE DataObject_MetadataMap SET MetadataId = @metadataId, MatchMethod = @matchmethod, WinningVoteCount = @winningvotecount, TotalVoteCount = @totalVoteCount WHERE DataObjectId = @dataobjectid AND SourceId = @sourceId;";
                db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    { "dataobjectid", dataObject.Id },
                    { "metadataId", MetadataGameId },
                    { "sourceId", metadataSource },
                    { "matchmethod", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Voted },
                    { "winningvotecount", WinningVoteCount },
                    { "totalVoteCount", TotalVoteCount }
                });

                if (metadataSource == Communications.MetadataSources.IGDB)
                {
                    // update the artwork
                    BackgroundMetadataMatcher.BackgroundMetadataMatcher backgroundMetadataMatcher = new BackgroundMetadataMatcher.BackgroundMetadataMatcher();
                    await backgroundMetadataMatcher.GetGameArtwork(dataObject.Id, true);
                }
            }
        }

        public async Task<ArchiveObservationModel> AddArchiveObservation(string UserId, ArchiveObservationModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // check if the content hash already exists
            HashLookup hashLookup = new HashLookup(db, new Models.HashLookupModel
            {
                MD5 = model.Content.MD5,
                SHA1 = model.Content.SHA1,
                SHA256 = model.Content.SHA256,
                CRC = model.Content.CRC
            });
            await hashLookup.PerformLookup();

            // if the content hash does not exist, reject the observation
            if (hashLookup.Id == null)
            {
                throw new HashLookup.HashNotFoundException("The provided content hash was not found in the signature database.");
            }

            // check if the archive hash already exists in the database against this user
            string sql = "SELECT * FROM UserArchiveObservations WHERE UserId = @userId AND ArchiveMD5 = @md5 AND ArchiveSHA1 = @sha1 AND ArchiveSHA256 = @sha256;";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>
            {
                { "userId", UserId },
                { "md5", model.Archive.MD5 },
                { "sha1", model.Archive.SHA1 },
                { "sha256", model.Archive.SHA256 }
            });

            if (data.Rows.Count > 0)
            {
                // archive observation already exists for this user
                throw new DuplicateArchiveObservationException("An archive observation with the same hashes already exists for this user.");
            }

            // insert the archive observation
            sql = "INSERT INTO UserArchiveObservations (UserId, ArchiveMD5, ArchiveSHA1, ArchiveSHA256, ArchiveCRC32, ArchiveSize, ArchiveType, ContentMD5, ContentSHA1, ContentSHA256, ContentCRC32) VALUES (@userId, @archivemd5, @archivesha1, @archivesha256, @archivecrc, @archivesize, @archivetype, @contentmd5, @contentsha1, @contentsha256, @contentcrc);";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "userId", UserId },
                { "archivemd5", model.Archive.MD5 },
                { "archivesha1", model.Archive.SHA1 },
                { "archivesha256", model.Archive.SHA256 },
                { "archivecrc", model.Archive.CRC },
                { "archivesize", model.Archive.Size },
                { "archivetype", model.Archive.Type },
                { "contentmd5", model.Content.MD5 },
                { "contentsha1", model.Content.SHA1 },
                { "contentsha256", model.Content.SHA256 },
                { "contentcrc", model.Content.CRC }
            });

            // return the model with the archive details
            return model;
        }
    }
}