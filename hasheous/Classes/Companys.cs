using System.Data;
using Classes;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using NuGet.Common;

namespace hasheous_server.Classes
{
    public class Companys
    {
        public List<Models.CompanyItem> GetCompanies()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Company ORDER BY `Name`;";
            DataTable data = db.ExecuteCMD(sql);

            List<Models.CompanyItem> companies = new List<Models.CompanyItem>();
            foreach (DataRow row in data.Rows)
            {
                Models.CompanyItem item = BuildCompany(
                    (long)row["Id"],
                    row
                );

                companies.Add(item);
            }

            return companies;
        }

        public Models.CompanyItem? GetCompany(long id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Company WHERE Id=@id;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id }
            };

            DataTable data = db.ExecuteCMD(sql, dbDict);

            if (data.Rows.Count > 0)
            {
                CompanyItem item = BuildCompany(id, data.Rows[0]);

                return item;
            }
            else
            {
                return null;
            }
        }

        private Models.CompanyItem BuildCompany(long id, DataRow row)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id }
            };

            // get signature publishers
            sql = "SELECT * FROM Company_SignatureMap WHERE CompanyId = @id";
            List<Dictionary<string, object>> signaturePublishers = db.ExecuteCMDDict(sql, dbDict);

            // get metadata matches
            sql = "SELECT * FROM Company_MetadataMap WHERE CompanyId = @id ORDER BY SourceId";
            DataTable data = db.ExecuteCMD(sql, dbDict);
            List<CompanyItemModel.MetadataItem> metadataItems = new List<CompanyItemModel.MetadataItem>();
            foreach (DataRow dataRow in data.Rows)
            {
                CompanyItemModel.MetadataItem metadataItem = new CompanyItemModel.MetadataItem{
                    Id = (string)dataRow["MetadataId"],
                    MatchMethod = (BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod)dataRow["MatchMethod"],
                    Source = (Metadata.IGDB.Communications.MetadataSources)dataRow["SourceId"],
                    LastSearch = (DateTime)dataRow["LastSearched"],
                    NextSearch = (DateTime)dataRow["NextSearch"]
                };

                metadataItems.Add(metadataItem);
            }

            CompanyItem item = new CompanyItem{
                Id = id,
                Name = (string)row["Name"],
                CreatedDate = (DateTime)row["CreatedDate"],
                UpdatedDate = (DateTime)row["UpdatedDate"],
                Metadata = metadataItems,
                SignatureCompanies = signaturePublishers
            };

            return item;
        }

        public Models.CompanyItem NewCompany(Models.CompanyItemModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO Company (`Name`, `CreatedDate`, `UpdatedDate`) VALUES (@name, @createddate, @updateddate); SELECT LAST_INSERT_ID();";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "name", model.Name },
                { "createddate", DateTime.UtcNow },
                { "updateddate", DateTime.UtcNow}
            };

            DataTable data = db.ExecuteCMD(sql, dbDict);

            // set up metadata searching
            foreach (Enum source in Enum.GetValues(typeof(Metadata.IGDB.Communications.MetadataSources)))
            {
                if ((Metadata.IGDB.Communications.MetadataSources)source != Metadata.IGDB.Communications.MetadataSources.None)
                {
                    sql = "INSERT INTO Company_MetadataMap (CompanyId, MetadataId, SourceId, MatchMethod, LastSearched, NextSearch) VALUES (@id, @metaid, @srcid, @method, @lastsearched, @nextsearch);";
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

            CompanyMetadataSearch((long)(ulong)data.Rows[0][0]);

            return GetCompany((long)(ulong)data.Rows[0][0]);
        }

        public Models.CompanyItem EditCompany(long id, Models.CompanyItemModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE Company SET `Name`=@name, `UpdatedDate`=@updateddate WHERE Id=@id";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", id },
                { "name", model.Name },
                { "createddate", DateTime.UtcNow },
                { "updateddate", DateTime.UtcNow }
            };

            db.ExecuteNonQuery(sql, dbDict);

            if (model.SignatureCompanies != null)
            {
                sql = "DELETE FROM Company_SignatureMap WHERE CompanyId=@id;";
                db.ExecuteNonQuery(sql, dbDict);
                foreach (int SignatureId in model.SignatureCompanies)
                {
                    sql = "INSERT INTO Company_SignatureMap (CompanyId, SignatureId) VALUES (@id, @signatureid);";
                    dbDict = new Dictionary<string, object>{
                        { "id", id },
                        { "signatureid", SignatureId }
                    };
                    db.ExecuteNonQuery(sql, dbDict);
                }
            }

            return GetCompany(id);
        }

        /// <summary>
        /// Performs a metadata look up on companies with no match metadata
        /// </summary>
        public void CompanyMetadataSearch()
        {
            _CompanyMetadataSearch(null);
        }

        /// <summary>
        /// Performs a metadata look up on the selected company if it has no metadata match
        /// </summary>
        /// <param name="id"></param>
        public void CompanyMetadataSearch(long? id)
        {
            _CompanyMetadataSearch(id);
        }

        private async void _CompanyMetadataSearch(long? id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict;

            List<CompanyItem> companies = new List<CompanyItem>();

            if (id != null)
            {
                companies.Add(GetCompany((long)id));
            }
            else
            {
                companies.AddRange(GetCompanies());
            }

            // search for metadata
            foreach (CompanyItem item in companies)
            {
                foreach (CompanyItemModel.MetadataItem metadata in item.Metadata)
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
                                var results = await GetIGDB("where name ~ *\"" + item.Name + "\"");
                                if (results.Length == 0)
                                {
                                    // no results - stay in no match, and set next search to next month
                                }
                                else
                                {
                                    if (results.Length == 1)
                                    {
                                        // one result - use this
                                        dbDict["method"] = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic;
                                        dbDict["metadataid"] = results[0].Slug;
                                    }
                                    else
                                    {
                                        // too many results - set to too many
                                        dbDict["method"] = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.AutomaticTooManyMatches;
                                    }
                                }
                                break;
                        }

                        sql = "UPDATE Company_MetadataMap SET MetadataId=@metadataid, MatchMethod=@method, LastSearched=@lastsearched, NextSearch=@nextsearch WHERE CompanyId=@id AND SourceId=@srcid;";
                        db.ExecuteNonQuery(sql, dbDict);
                    }
                }
            }
        }

        private static async Task<IGDB.Models.Company[]> GetIGDB(string WhereClause)
        {
            // get Companies metadata
            Communications comms = new Communications();
            var results = await comms.APIComm<IGDB.Models.Company>(IGDB.IGDBClient.Endpoints.Companies, "fields *;", WhereClause);
            
            return results;
        }
    }
}