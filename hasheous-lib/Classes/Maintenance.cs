using System.Data;

namespace Classes
{
    public class Maintenance
    {
        public async Task RunDailyMaintenance()
        {
            await Logging.PurgeLogsAsync();

            // delete insights older than 30 days
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "DELETE FROM Insights_API_Requests WHERE event_datetime < NOW() - INTERVAL 31 DAY;";
            await db.ExecuteCMDAsync(sql);
        }

        public async Task RunWeeklyMaintenance()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();

            Logging.Log(Logging.LogType.Information, "Maintenance", "Optimising database tables");
            sql = "SHOW FULL TABLES WHERE Table_Type = 'BASE TABLE';";
            DataTable tables = await db.ExecuteCMDAsync(sql);

            int StatusCounter = 1;
            foreach (DataRow row in tables.Rows)
            {
                sql = "OPTIMIZE TABLE " + row[0].ToString();
                DataTable response = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>(), 240);
                foreach (DataRow responseRow in response.Rows)
                {
                    string retVal = "";
                    for (int i = 0; i < responseRow.ItemArray.Length; i++)
                    {
                        retVal += responseRow.ItemArray[i] + "; ";
                    }
                    Logging.Log(Logging.LogType.Information, "Maintenance", "(" + StatusCounter + "/" + tables.Rows.Count + "): Optimise table " + row[0].ToString() + ": " + retVal);
                }

                StatusCounter += 1;
            }
        }
    }
}