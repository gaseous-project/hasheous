using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using IGDB.Models;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using MySqlConnector;

namespace Classes
{
	public class Database
	{
		public Database()
		{

		}

		public Database(databaseType Type, string ConnectionString)
		{
			_ConnectorType = Type;
			_ConnectionString = ConnectionString;
		}

		public enum databaseType
		{
			MySql
		}

		string _ConnectionString = "";

		public string ConnectionString
		{
			get
			{
				return _ConnectionString;
			}
			set
			{
				_ConnectionString = value;
			}
		}

		databaseType? _ConnectorType = null;

		public databaseType? ConnectorType
		{
			get
			{
				return _ConnectorType;
			}
			set
			{
				_ConnectorType = value;
			}
		}

		public async Task InitDB()
		{
			// load resources
			var assembly = Assembly.GetExecutingAssembly();

			switch (_ConnectorType)
			{
				case databaseType.MySql:
					// check if the database exists first - first run must have permissions to create a database
					string sql = "CREATE DATABASE IF NOT EXISTS `" + Config.DatabaseConfiguration.DatabaseName + "`;";
					Dictionary<string, object> dbDict = new Dictionary<string, object>();
					Logging.Log(Logging.LogType.Information, "Database", "Creating database if it doesn't exist");
					await ExecuteCMDAsync(sql, dbDict, 30, "server=" + Config.DatabaseConfiguration.HostName + ";port=" + Config.DatabaseConfiguration.Port + ";userid=" + Config.DatabaseConfiguration.UserName + ";password=" + Config.DatabaseConfiguration.Password);

					// check if schema version table is in place - if not, create the schema version table
					sql = "SELECT TABLE_SCHEMA, TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = '" + Config.DatabaseConfiguration.DatabaseName + "' AND TABLE_NAME = 'schema_version';";
					DataTable SchemaVersionPresent = await ExecuteCMDAsync(sql, dbDict);
					if (SchemaVersionPresent.Rows.Count == 0)
					{
						// no schema table present - create it
						Logging.Log(Logging.LogType.Information, "Database", "Schema version table doesn't exist. Creating it.");
						sql = "CREATE TABLE `schema_version` (`schema_version` INT NOT NULL, PRIMARY KEY (`schema_version`)); INSERT INTO `schema_version` (`schema_version`) VALUES (0);";
						await ExecuteCMDAsync(sql, dbDict);
					}

					for (int i = 1000; i < 10000; i++)
					{
						string resourceName = "hasheous_lib.Schema.hasheous-" + i + ".sql";
						string dbScript = "";

						string[] resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
						if (resources.Contains(resourceName))
						{
							Logging.Log(Logging.LogType.Information, "Database", "Found schema update script for version " + i);

							using (Stream stream = assembly.GetManifestResourceStream(resourceName))
							using (StreamReader reader = new StreamReader(stream))
							{
								dbScript = reader.ReadToEnd();

								Logging.Log(Logging.LogType.Information, "Database", "Read schema update script for version " + i);

								// apply script
								sql = "SELECT schema_version FROM schema_version;";
								dbDict = new Dictionary<string, object>();
								DataTable SchemaVersion = await ExecuteCMDAsync(sql, dbDict);
								if (SchemaVersion.Rows.Count == 0)
								{
									// something is broken here... where's the table?
									Logging.Log(Logging.LogType.Critical, "Database", "Schema table missing! This shouldn't happen!");
									throw new Exception("schema_version table is missing!");
								}
								else
								{
									int SchemaVer = (int)SchemaVersion.Rows[0][0];
									Logging.Log(Logging.LogType.Information, "Database", "Schema version is " + SchemaVer);
									if (SchemaVer < i)
									{
										// run pre-upgrade code
										DatabaseMigration.PreUpgradeScript(i, _ConnectorType);

										// apply schema!
										Logging.Log(Logging.LogType.Information, "Database", "Updating schema to version " + i);
										await ExecuteCMDAsync(dbScript, dbDict);

										sql = "UPDATE schema_version SET schema_version=@schemaver";
										dbDict = new Dictionary<string, object>();
										dbDict.Add("schemaver", i);
										await ExecuteCMDAsync(sql, dbDict);

										// run post-upgrade code
										DatabaseMigration.PostUpgradeScript(i, _ConnectorType);
									}
									else
									{
										Logging.Log(Logging.LogType.Information, "Database", "Schema version is up to date. No update needed. " + SchemaVer + " >= " + i);
									}
								}
							}
						}
					}

					Logging.Log(Logging.LogType.Information, "Database", "Database setup complete");
					break;
			}
		}

		#region Synchronous Database Access
		public DataTable ExecuteCMD(string Command)
		{
			Dictionary<string, object> dbDict = new Dictionary<string, object>();
			return _ExecuteCMD(Command, dbDict, 30, "");
		}

		public DataTable ExecuteCMD(string Command, Dictionary<string, object> Parameters)
		{
			return _ExecuteCMD(Command, Parameters, 30, "");
		}

		public DataTable ExecuteCMD(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			return _ExecuteCMD(Command, Parameters, Timeout, ConnectionString);
		}

		public List<Dictionary<string, object>> ExecuteCMDDict(string Command)
		{
			Dictionary<string, object> dbDict = new Dictionary<string, object>();
			return _ExecuteCMDDict(Command, dbDict, 30, "");
		}

		public List<Dictionary<string, object>> ExecuteCMDDict(string Command, Dictionary<string, object> Parameters)
		{
			return _ExecuteCMDDict(Command, Parameters, 30, "");
		}

		public List<Dictionary<string, object>> ExecuteCMDDict(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			return _ExecuteCMDDict(Command, Parameters, Timeout, ConnectionString);
		}
		#endregion Synchronous Database Access

		#region Asynchronous Database Access
		public async Task<DataTable> ExecuteCMDAsync(string Command)
		{
			Dictionary<string, object> dbDict = new Dictionary<string, object>();
			return _ExecuteCMD(Command, dbDict, 30, "");
		}

		public async Task<DataTable> ExecuteCMDAsync(string Command, Dictionary<string, object> Parameters)
		{
			return _ExecuteCMD(Command, Parameters, 30, "");
		}

		public async Task<DataTable> ExecuteCMDAsync(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			return _ExecuteCMD(Command, Parameters, Timeout, ConnectionString);
		}

		public async Task<List<Dictionary<string, object>>> ExecuteCMDDictAsync(string Command)
		{
			Dictionary<string, object> dbDict = new Dictionary<string, object>();
			return _ExecuteCMDDict(Command, dbDict, 30, "");
		}

		public async Task<List<Dictionary<string, object>>> ExecuteCMDDictAsync(string Command, Dictionary<string, object> Parameters)
		{
			return _ExecuteCMDDict(Command, Parameters, 30, "");
		}

		public async Task<List<Dictionary<string, object>>> ExecuteCMDDictAsync(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			return _ExecuteCMDDict(Command, Parameters, Timeout, ConnectionString);
		}
		#endregion Asynchronous Database Access

		private List<Dictionary<string, object>> _ExecuteCMDDict(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			DataTable dataTable = _ExecuteCMD(Command, Parameters, Timeout, ConnectionString);

			// convert datatable to dictionary
			List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>();

			foreach (DataRow dataRow in dataTable.Rows)
			{
				Dictionary<string, object?> row = new Dictionary<string, object?>();
				for (int i = 0; i < dataRow.Table.Columns.Count; i++)
				{
					string columnName = dataRow.Table.Columns[i].ColumnName;
					if (dataRow[i] == System.DBNull.Value)
					{
						row.Add(columnName, null);
					}
					else
					{
						row.Add(columnName, dataRow[i].ToString());
					}
				}
				rows.Add(row);
			}

			return rows;
		}

		private DataTable _ExecuteCMD(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			if (ConnectionString == "") { ConnectionString = _ConnectionString; }
			switch (_ConnectorType)
			{
				case databaseType.MySql:
					MySQLServerConnector conn = new MySQLServerConnector(ConnectionString);
					return (DataTable)conn.ExecCMD(Command, Parameters, Timeout);
				default:
					return new DataTable();
			}
		}

		public int ExecuteNonQuery(string Command)
		{
			Dictionary<string, object> dbDict = new Dictionary<string, object>();
			return _ExecuteNonQuery(Command, dbDict, 30, "");
		}

		public int ExecuteNonQuery(string Command, Dictionary<string, object> Parameters)
		{
			return _ExecuteNonQuery(Command, Parameters, 30, "");
		}

		public int ExecuteNonQuery(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			return _ExecuteNonQuery(Command, Parameters, Timeout, ConnectionString);
		}

		private int _ExecuteNonQuery(string Command, Dictionary<string, object> Parameters, int Timeout = 30, string ConnectionString = "")
		{
			if (ConnectionString == "") { ConnectionString = _ConnectionString; }
			switch (_ConnectorType)
			{
				case databaseType.MySql:
					MySQLServerConnector conn = new MySQLServerConnector(ConnectionString);
					return (int)conn.ExecNonQuery(Command, Parameters, Timeout);
				default:
					return 0;
			}
		}

		public void ExecuteTransactionCMD(List<SQLTransactionItem> CommandList, int Timeout = 60)
		{
			object conn;
			switch (_ConnectorType)
			{
				case databaseType.MySql:
					{
						var commands = new List<Dictionary<string, object>>();
						foreach (SQLTransactionItem CommandItem in CommandList)
						{
							var nCmd = new Dictionary<string, object>();
							nCmd.Add("sql", CommandItem.SQLCommand);
							nCmd.Add("values", CommandItem.Parameters);
							commands.Add(nCmd);
						}

						conn = new MySQLServerConnector(_ConnectionString);
						((MySQLServerConnector)conn).TransactionExecCMD(commands, Timeout);
						break;
					}
			}
		}

		public async Task<int> GetDatabaseSchemaVersion()
		{
			switch (_ConnectorType)
			{
				case databaseType.MySql:
					string sql = "SELECT schema_version FROM schema_version;";
					DataTable SchemaVersion = await ExecuteCMDAsync(sql);
					if (SchemaVersion.Rows.Count == 0)
					{
						return 0;
					}
					else
					{
						return (int)SchemaVersion.Rows[0][0];
					}

				default:
					return 0;

			}
		}

		public bool TestConnection()
		{
			switch (_ConnectorType)
			{
				case databaseType.MySql:
					MySQLServerConnector conn = new MySQLServerConnector(_ConnectionString);
					return conn.TestConnection();
				default:
					return false;
			}
		}

		public class SQLTransactionItem
		{
			public SQLTransactionItem()
			{

			}

			public SQLTransactionItem(string SQLCommand, Dictionary<string, object> Parameters)
			{
				this.SQLCommand = SQLCommand;
				this.Parameters = Parameters;
			}

			public string? SQLCommand;
			public Dictionary<string, object>? Parameters = new Dictionary<string, object>();
		}

		private partial class MySQLServerConnector
		{
			private string DBConn = "";

			public MySQLServerConnector(string ConnectionString)
			{
				DBConn = ConnectionString;
			}

			public DataTable ExecCMD(string SQL, Dictionary<string, object> Parameters, int Timeout)
			{
				DataTable RetTable = new DataTable();

				Logging.Log(Logging.LogType.Debug, "Database", "Connecting to database", null, true);
				MySqlConnection conn = new MySqlConnection(DBConn);
				conn.Open();

				MySqlCommand cmd = new MySqlCommand
				{
					Connection = conn,
					CommandText = SQL,
					CommandTimeout = Timeout
				};

				foreach (string Parameter in Parameters.Keys)
				{
					cmd.Parameters.AddWithValue(Parameter, Parameters[Parameter]);
				}

				try
				{
					Logging.Log(Logging.LogType.Debug, "Database", "Executing sql: '" + SQL + "'", null, true);
					if (Parameters.Count > 0)
					{
						string dictValues = string.Join(";", Parameters.Select(x => string.Join("=", x.Key, x.Value)));
						Logging.Log(Logging.LogType.Debug, "Database", "Parameters: " + dictValues, null, true);
					}
					RetTable.Load(cmd.ExecuteReader());
				}
				catch (Exception ex)
				{
					Logging.Log(Logging.LogType.Critical, "Database", "Error while executing '" + SQL + "'", ex);
				}

				Logging.Log(Logging.LogType.Debug, "Database", "Closing database connection", null, true);
				conn.Close();

				return RetTable;
			}

			public int ExecNonQuery(string SQL, Dictionary<string, object> Parameters, int Timeout)
			{
				int result = 0;

				Logging.Log(Logging.LogType.Debug, "Database", "Connecting to database", null, true);
				MySqlConnection conn = new MySqlConnection(DBConn);
				conn.Open();

				MySqlCommand cmd = new MySqlCommand
				{
					Connection = conn,
					CommandText = SQL,
					CommandTimeout = Timeout
				};

				foreach (string Parameter in Parameters.Keys)
				{
					cmd.Parameters.AddWithValue(Parameter, Parameters[Parameter]);
				}

				try
				{
					Logging.Log(Logging.LogType.Debug, "Database", "Executing sql: '" + SQL + "'", null, true);
					if (Parameters.Count > 0)
					{
						string dictValues = string.Join(";", Parameters.Select(x => string.Join("=", x.Key, x.Value)));
						Logging.Log(Logging.LogType.Debug, "Database", "Parameters: " + dictValues, null, true);
					}
					result = cmd.ExecuteNonQuery();
				}
				catch (Exception ex)
				{
					Logging.Log(Logging.LogType.Critical, "Database", "Error while executing '" + SQL + "'", ex);
					Trace.WriteLine("Error executing " + SQL);
					Trace.WriteLine("Full exception: " + ex.ToString());
				}

				Logging.Log(Logging.LogType.Debug, "Database", "Closing database connection", null, true);
				conn.Close();

				return result;
			}

			public void TransactionExecCMD(List<Dictionary<string, object>> Parameters, int Timeout)
			{
				var conn = new MySqlConnection(DBConn);
				conn.Open();
				var command = conn.CreateCommand();
				MySqlTransaction transaction;
				transaction = conn.BeginTransaction();
				command.Connection = conn;
				command.Transaction = transaction;
				foreach (Dictionary<string, object> Parameter in Parameters)
				{
					var cmd = buildcommand(conn, Parameter["sql"].ToString(), (Dictionary<string, object>)Parameter["values"], Timeout);
					cmd.Transaction = transaction;
					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
				conn.Close();
			}

			private MySqlCommand buildcommand(MySqlConnection Conn, string SQL, Dictionary<string, object> Parameters, int Timeout)
			{
				var cmd = new MySqlCommand();
				cmd.Connection = Conn;
				cmd.CommandText = SQL;
				cmd.CommandTimeout = Timeout;
				{
					var withBlock = cmd.Parameters;
					if (Parameters is object)
					{
						if (Parameters.Count > 0)
						{
							foreach (string param in Parameters.Keys)
								withBlock.AddWithValue(param, Parameters[param]);
						}
					}
				}

				return cmd;
			}

			public bool TestConnection()
			{
				MySqlConnection conn = new MySqlConnection(DBConn);
				try
				{
					conn.Open();
					conn.Close();
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		public void BuildTableFromType(string databaseName, string prefix, Type type, string overrideId = "", string customColumnIndexes = "")
		{
			// Get the table name from the class name
			string tableName = type.Name;
			if (!string.IsNullOrEmpty(prefix))
			{
				tableName = prefix + "_" + tableName;
			}

			// Ensure the table name is valid for MySQL
			tableName = tableName.Replace(" ", "_").Replace("-", "_").Replace(".", "_");

			// create the database if it does not exist
			string createDatabaseQuery = $"CREATE DATABASE IF NOT EXISTS `{databaseName}`";
			Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
			db.ExecuteNonQuery(createDatabaseQuery);

			// Get the properties of the class
			PropertyInfo[] properties = type.GetProperties();

			// Create the table with the basic structure if it does not exist
			string createTableQuery = $"CREATE TABLE IF NOT EXISTS `{databaseName}`.`{tableName}` (`Id` BIGINT PRIMARY KEY, `dateAdded` DATETIME DEFAULT CURRENT_TIMESTAMP, `lastUpdated` DATETIME DEFAULT CURRENT_TIMESTAMP )";
			if (!string.IsNullOrEmpty(overrideId))
			{
				// If an override ID is provided, use it as the primary key - don't create it now as the field might not exist yet
				createTableQuery = $"CREATE TABLE IF NOT EXISTS `{databaseName}`.`{tableName}` (`dateAdded` DATETIME DEFAULT CURRENT_TIMESTAMP, `lastUpdated` DATETIME DEFAULT CURRENT_TIMESTAMP )";
			}
			db.ExecuteNonQuery(createTableQuery);

			// Loop through each property to add it as a column in the table
			foreach (PropertyInfo property in properties)
			{
				// Get the property name and type
				string columnName = property.Name;
				string columnType = "VARCHAR(255)"; // Default type, can be changed based on property type

				// Convert the property type name to a string
				string propertyTypeName = property.PropertyType.Name;
				if (propertyTypeName == "Nullable`1")
				{
					// If the property is nullable, get the underlying type
					propertyTypeName = property.PropertyType.GetGenericArguments()[0].Name;
				}

				// if property is a class, check if that class a property named "Id". If it does, this column will be a foreign key. If it does not, this column will be a longtext.
				if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
				{
					PropertyInfo? idProperty = property.PropertyType
						.GetProperties()
						.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
					if (idProperty != null)
					{
						// This is a foreign key reference
						columnType = "BIGINT"; // Assuming Id is of type long
					}
					else
					{
						// This is a longtext column
						columnType = "LONGTEXT";
					}
				}
				else
				{

					// Determine the SQL type based on the property type
					switch (propertyTypeName)
					{
						case "String":
							if (columnName.ToLower() == "description" || columnName.ToLower() == "notes" || columnName.ToLower() == "comments" || columnName.ToLower() == "details" || columnName.ToLower() == "summary" || columnName.ToLower() == "content" || columnName.ToLower() == "text" || columnName.ToLower() == "body" || columnName.ToLower() == "message" || columnName.ToLower() == "info" || columnName.ToLower() == "data" || columnName.ToLower() == "deck" || columnName.ToLower() == "aliases")
							{
								columnType = "LONGTEXT"; // Use TEXT for longer strings
							}
							else
							{
								columnType = "VARCHAR(255)";
							}
							break;
						case "Int32":
							columnType = "INT";
							break;
						case "Int64":
							columnType = "BIGINT";
							break;
						case "Boolean":
							columnType = "BOOLEAN";
							break;
						case "DateTime":
						case "DateTimeOffset":
							columnType = "DATETIME";
							break;
						case "Double":
							columnType = "DOUBLE";
							break;
						case "Float":
						case "Single":
							columnType = "FLOAT";
							break;
						case "IdentityOrValue`1":
							columnType = "BIGINT";
							break;
						case "IdentitiesOrValues`1":
							columnType = "LONGTEXT";
							break;
					}
				}

				// check if there is a column with the name of the property
				string checkColumnQuery = $"SHOW COLUMNS FROM `{databaseName}`.`{tableName}` LIKE '{columnName}'";
				var result = db.ExecuteCMD(checkColumnQuery);
				if (result.Rows.Count > 0)
				{
					// Column already exists, check if the type matches
					string existingType = result.Rows[0]["Type"].ToString();
					if (existingType.ToLower().Split("(")[0] != columnType.ToLower().Split("(")[0] && existingType != "text" && existingType != "longtext")
					{
						// If the type does not match, we cannot change the column type in MySQL without dropping it first
						Console.WriteLine($"Column '{columnName}' in table '{tableName}' already exists with type '{existingType}', but expected type is '{columnType}'.");
						string alterColumnQuery = $"ALTER TABLE `{databaseName}`.`{tableName}` MODIFY COLUMN `{columnName}` {columnType}";
						Console.WriteLine($"Executing query: {alterColumnQuery}");
						try
						{
							db.ExecuteNonQuery(alterColumnQuery);
						}
						catch (Exception ex)
						{
							Console.WriteLine($"Error altering column '{columnName}' in table '{tableName}': {ex.Message}");
						}
						continue; // Skip this column as we cannot change its type
					}
					continue; // Skip this column as it already exists
				}

				// Add the column to the table if it does not already exist
				string addColumnQuery = $"ALTER TABLE `{databaseName}`.`{tableName}` ADD COLUMN IF NOT EXISTS `{columnName}` {columnType}";
				Console.WriteLine($"Executing query: {addColumnQuery}");
				db.ExecuteNonQuery(addColumnQuery);
			}

			if (!string.IsNullOrEmpty(overrideId))
			{
				// If an override ID is provided, add it as the primary key
				// check if the primary key already exists - if the columns are not the same, we need to drop the primary key first
				string checkPrimaryKeyQuery = $"SHOW KEYS FROM `{databaseName}`.`{tableName}` WHERE Key_name = 'PRIMARY'";
				var primaryKeyResult = db.ExecuteCMD(checkPrimaryKeyQuery);
				string[] overrideIdFields = overrideId.Split(',').Select(f => f.Trim()).ToArray();
				if (primaryKeyResult.Rows.Count > 0)
				{
					// Primary key already exists, check if the fields match
					string existingPrimaryKey = "";
					foreach (DataRow row in primaryKeyResult.Rows)
					{
						if (existingPrimaryKey != "")
						{
							existingPrimaryKey += ",";
						}
						existingPrimaryKey += row["Column_name"].ToString();
					}
					string[] existingPrimaryKeyFields = existingPrimaryKey.Split(',').Select(f => f.Trim()).ToArray();
					if (!overrideIdFields.SequenceEqual(existingPrimaryKeyFields))
					{
						// If the primary key fields do not match, we need to drop the primary key first
						string dropPrimaryKeyQuery = $"ALTER TABLE `{databaseName}`.`{tableName}` DROP PRIMARY KEY";
						Console.WriteLine($"Executing query: {dropPrimaryKeyQuery}");
						db.ExecuteNonQuery(dropPrimaryKeyQuery);
					}
				}

				// Add the override ID as the primary key
				string addPrimaryKeyQuery = $"ALTER TABLE `{databaseName}`.`{tableName}` ADD PRIMARY KEY ({overrideId})";
				Console.WriteLine($"Executing query: {addPrimaryKeyQuery}");
				db.ExecuteNonQuery(addPrimaryKeyQuery);
			}

			// Add custom indexes if provided
			if (!string.IsNullOrEmpty(customColumnIndexes))
			{
				string[] indexes = customColumnIndexes.Split(',');
				foreach (string index in indexes)
				{
					string trimmedIndex = index.Trim();
					if (!string.IsNullOrEmpty(trimmedIndex))
					{
						string checkIndexQuery = $"SHOW INDEX FROM `{databaseName}`.`{tableName}` WHERE Key_name = 'idx_{trimmedIndex}'";
						var indexResult = db.ExecuteCMD(checkIndexQuery);
						if (indexResult.Rows.Count == 0)
						{
							string createIndexQuery = $"CREATE INDEX `idx_{trimmedIndex}` ON `{databaseName}`.`{tableName}` (`{trimmedIndex}`)";
							Console.WriteLine($"Executing query: {createIndexQuery}");
							db.ExecuteNonQuery(createIndexQuery);
						}
					}
				}
			}
		}
	}
}

