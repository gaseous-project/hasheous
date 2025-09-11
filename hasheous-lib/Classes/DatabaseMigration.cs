using System;
using System.Data;
using System.Reflection;

namespace Classes
{
    public static class DatabaseMigration
    {
        public static List<int> BackgroundUpgradeTargetSchemaVersions = new List<int>();

        public static void PreUpgradeScript(int TargetSchemaVersion, Database.databaseType? DatabaseType)
        {

        }

        public static void PostUpgradeScript(int TargetSchemaVersion, Database.databaseType? DatabaseType)
        {
            // load resources
            var assembly = Assembly.GetExecutingAssembly();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>();

            switch (TargetSchemaVersion)
            {
                case 1004:
                    // load country list
                    Logging.Log(Logging.LogType.Information, "Database Upgrade", "Adding country look up table contents");

                    string countryResourceName = "hasheous_lib.Support.Country.txt";
                    using (Stream stream = assembly.GetManifestResourceStream(countryResourceName))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        do
                        {
                            string[] line = reader.ReadLine().Split("|");

                            sql = "INSERT INTO Country (Code, Value) VALUES (@code, @value);";
                            dbDict = new Dictionary<string, object>{
                                { "code", line[0] },
                                { "value", line[1] }
                            };
                            db.ExecuteNonQuery(sql, dbDict);
                        } while (reader.EndOfStream == false);
                    }

                    // load language list
                    Logging.Log(Logging.LogType.Information, "Database Upgrade", "Adding language look up table contents");

                    string languageResourceName = "hasheous_lib.Support.Language.txt";
                    using (Stream stream = assembly.GetManifestResourceStream(languageResourceName))
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        do
                        {
                            string[] line = reader.ReadLine().Split("|");

                            sql = "INSERT INTO Language (Code, Value) VALUES (@code, @value);";
                            dbDict = new Dictionary<string, object>{
                                { "code", line[0] },
                                { "value", line[1] }
                            };
                            db.ExecuteNonQuery(sql, dbDict);
                        } while (reader.EndOfStream == false);
                    }
                    break;
            }
        }

        public static void UpgradeScriptBackgroundTasks()
        {

        }
    }
}