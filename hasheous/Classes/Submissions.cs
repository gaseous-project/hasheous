using System.Data;
using Classes;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using static hasheous_server.Models.DataObjectItem;

namespace hasheous_server.Classes
{
    public class Submissions
    {
        /// <summary>
        /// Handle vote submissions from users
        /// </summary>
        /// <param name="UserId"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public SubmissionsMatchFixModel AddVote(string UserId, SubmissionsMatchFixModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            HashLookup2 hashLookup = new HashLookup2(db, new Models.HashLookupModel
            {
                MD5 = model.MD5,
                SHA1 = model.SHA1
            });
            if (hashLookup != null)
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
                            IGDB.Models.Platform platform = Metadata.IGDB.Platforms.GetPlatform(metadataMatch.PlatformId, false);
                            IGDB.Models.Game game = Metadata.IGDB.Games.GetGame(metadataMatch.GameId, false, false, false);
                            if (platform != null && game != null)
                            {
                                if (game.Platforms.Ids.ToList<long>().Contains((long)platform.Id))
                                {
                                    AllowInsert = true;
                                }
                            }
                            break;
                    }

                    if (AllowInsert == true)
                    {
                        // check for an existing vote - users only get one vote per game
                        // if a user submits an existing vote, it will be updated
                        string sql = "SELECT * FROM MatchUserVotes WHERE UserId = @userId AND DataObjectId = @dataObjectId AND MetadataSourceId = @metadataSourceId";
                        DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                            { "userId", UserId },
                            { "dataObjectId", hashLookup.Id },
                            { "metadataSourceId", metadataMatch.Source }
                        });

                        if (data.Rows.Count == 0)
                        {
                            // no existing vote - insert a new record
                            sql = "INSERT INTO MatchUserVotes (DataObjectId, UserId, MetadataSourceId, MetadataPlatformId, MetadataGameId) VALUES (@dataObjectId, @userId, @metadataSourceId, @metadataPlatformId, @metadataGameId)";
                            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                { "dataObjectId", hashLookup.Id },
                                { "userId", UserId },
                                { "metadataSourceId", metadataMatch.Source },
                                { "metadataPlatformId", metadataMatch.PlatformId },
                                { "metadataGameId", metadataMatch.GameId }
                            });
                        }
                        else
                        {
                            // update existing vote if different
                            if (data.Rows[0]["MetadataPlatformId"].ToString() != metadataMatch.PlatformId || data.Rows[0]["MetadataGameId"].ToString() != metadataMatch.GameId)
                            {
                                sql = "UPDATE MatchUserVotes SET MetadataPlatformId = @metadataPlatformId, MetadataGameId = @metadataGameId WHERE UserId = @userId AND DataObjectId = @dataObjectId AND MetadataSourceId = @metadataSourceId";
                                db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                                    { "dataObjectId", hashLookup.Id },
                                    { "userId", UserId },
                                    { "metadataSourceId", metadataMatch.Source },
                                    { "metadataPlatformId", metadataMatch.PlatformId },
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
        public void TallyVotes()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // loop all game dataobjects, then fetch all votes for each game
            DataObjects dataObjects = new DataObjects();
            DataObjectsList dataObjectsList = dataObjects.GetDataObjects(DataObjects.DataObjectType.Game, 0, 0, null, false);

            foreach (DataObjectItem dataObject in dataObjectsList.Objects)
            {
                // get votes for each metadata source for this dataobject
                foreach (Communications.MetadataSources metadataSource in Enum.GetValues(typeof(Communications.MetadataSources)))
                {
                    // calculate votes
                    string sql = "SELECT DataObjectId, MetadataSourceId, MetadataPlatformId, MetadataGameId, COUNT(*) AS `Votes` FROM MatchUserVotes WHERE DataObjectId = @dataObjectId AND MetadataSourceId = @metadataSourceId GROUP BY MetadataSourceId, MetadataGameId, MetadataPlatformId ORDER BY DataObjectId, MetadataSourceId, `Votes` DESC;";
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
                        SetMetadataValue(dataObject, true, metadataSource, data.Rows[0]["MetadataPlatformId"].ToString(), data.Rows[0]["MetadataGameId"].ToString(), (uint)(long)data.Rows[0]["Votes"], (uint)totalVoteCount);
                    }
                }
            }
        }

        private void SetMetadataValue(DataObjectItem dataObject, bool Update, Communications.MetadataSources metadataSource, string MetadataPlatformId, string MetadataGameId, uint WinningVoteCount, uint TotalVoteCount)
        {
            // can we update the value?
            // rules:
            // 1. do not update metadata if the value is already correct
            // 2. do not update metadata set manually, or manually by admin
            // 3. do not update metadata set automatically unless we have a winningvotecount of at least 3 (meaning at least three people agree it's correct)

            // Rule 3. do not update metadata set automatically unless we have a winningvotecount of at least 3 (meaning at least three people agree it's correct)
            if (WinningVoteCount >= 3)
            {
                foreach (MetadataItem metadata in dataObject.Metadata)
                {
                    if (metadata.Source == metadataSource)
                    {
                        // Rule 1. do not update metadata if the value is already correct
                        if (metadata.Id == MetadataGameId)
                        {
                            // no update required
                            return;
                        }

                        // Rule 2. do not update metadata set manually, or manually by admin
                        if (metadata.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Manual || metadata.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.ManualByAdmin)
                        {
                            // no update required
                            return;
                        }

                        // if we've gotten here, then an update is allowed
                        Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                        string sql;
                        if (Update == true)
                        {
                            // TODO: rewrite this
                            sql = "UPDATE DataObject_MetadataMap SET MatchMethod=@matchmethod, MetadataId=@metadataId, WinningVoteCount=@winningvotecount, TotalVoteCount=@totalvotecount WHERE DataObjectId = @dataobjectid AND SourceId = @SourceId;";
                        }
                        else
                        {
                            // TODO: rewrite this
                            sql = "INSERT INTO DataObject_MetadataMap (DataObjectId, MetadataId, SourceId, MatchMethod, WinningVoteCount, TotalVoteCount) VALUES (@dataobjectid, @metadataId, @sourceId, @matchmethod, @winningvotecount, @totalvotecount);";
                        }
                        db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                            { "dataobjectid", dataObject.Id },
                            { "metadataId", MetadataGameId },
                            { "matchmethod", BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Voted },
                            { "sourceid", metadataSource},
                            { "winningvotecount", WinningVoteCount },
                            { "totalvotecount", TotalVoteCount }
                        });
                    }
                }
            }
            else
            {
                // no update allowed
                return;
            }
        }
    }
}