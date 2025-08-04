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

            // migrate images from database to filesystem
            sql = "SELECT * FROM Images LIMIT 1000;";
            DataTable images = await db.ExecuteCMDAsync(sql);
            if (images.Rows.Count > 0)
            {
                Logging.Log(Logging.LogType.Information, "Maintenance", "Migrating images from database to filesystem");
                foreach (DataRow row in images.Rows)
                {
                    string imageId = row["Id"].ToString();
                    byte[] imageData = row["Content"] as byte[];
                    string extension = row["Extension"].ToString();
                    string filePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages, imageId + extension);

                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            Logging.Log(Logging.LogType.Information, "Maintenance", "Deleted existing image: " + imageId + extension);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                        await File.WriteAllBytesAsync(filePath, imageData);

                        // Update the database to remove the image content
                        sql = "DELETE FROM Images WHERE Id = @Id;";
                        Dictionary<string, object> parameters = new Dictionary<string, object>
                        {
                            { "@Id", imageId }
                        };
                        await db.ExecuteCMDAsync(sql, parameters);

                        Logging.Log(Logging.LogType.Information, "Maintenance", "Migrated image " + imageId + extension + " to filesystem.");
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "Maintenance", "Failed to migrate image " + imageId + extension + ": " + ex.Message);
                    }
                }
            }
        }

        public async Task RunWeeklyMaintenance()
        {
            // optimise database tables
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