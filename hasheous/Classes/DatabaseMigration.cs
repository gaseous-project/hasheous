using System;
using System.Data;

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
            
        }

        public static void UpgradeScriptBackgroundTasks()
        {
            
        }
    }
}