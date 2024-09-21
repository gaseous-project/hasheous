using System.Data;
using hasheous_server.Models;

namespace Classes
{
    public class AutoMapper
    {
        /// <summary>
        /// Loops all ROMs in the database and maps them to the correct data object
        /// </summary>
        public static void RomAutoMapper()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Signatures_Roms;";
            DataTable dt = db.ExecuteCMD(sql);

            foreach (DataRow row in dt.Rows)
            {
                Logging.Log(Logging.LogType.Information, "AutoMapper", "Mapping ROM: " + row["Name"].ToString());

                // build lookup model
                HashLookupModel hashLookupModel = new HashLookupModel();
                if (row["MD5"] != System.DBNull.Value)
                {
                    hashLookupModel.MD5 = row["MD5"].ToString();
                }
                if (row["SHA1"] != System.DBNull.Value)
                {
                    hashLookupModel.SHA1 = row["SHA1"].ToString();
                }

                // search
                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), hashLookupModel);
            }
        }
    }
}