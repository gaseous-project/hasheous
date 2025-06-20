﻿using System;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;

namespace Classes
{
	public class Common
	{
		/// <summary>
		/// Returns IfNullValue if the ObjectToCheck is null
		/// </summary>
		/// <param name="ObjectToCheck">Any nullable object to check for null</param>
		/// <param name="IfNullValue">Any object to return if ObjectToCheck is null</param>
		/// <returns></returns>
		static public object ReturnValueIfNull(object? ObjectToCheck, object IfNullValue)
		{
			if (ObjectToCheck != null && ObjectToCheck != System.DBNull.Value)
			{
				return ObjectToCheck;
			}
			else
			{
				return IfNullValue;
			}
		}

		static public DateTime ConvertUnixToDateTime(double UnixTimeStamp)
		{
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
			dateTime = dateTime.AddSeconds(UnixTimeStamp).ToLocalTime();
			return dateTime;
		}

		public class hashObject
		{
			public hashObject()
			{

			}

			public hashObject(string FileName)
			{
				var xmlStream = File.OpenRead(FileName);

				var md5 = MD5.Create();
				byte[] md5HashByte = md5.ComputeHash(xmlStream);
				string md5Hash = BitConverter.ToString(md5HashByte).Replace("-", "").ToLowerInvariant();
				_md5hash = md5Hash;

				var sha1 = SHA1.Create();
				xmlStream.Position = 0;
				byte[] sha1HashByte = sha1.ComputeHash(xmlStream);
				string sha1Hash = BitConverter.ToString(sha1HashByte).Replace("-", "").ToLowerInvariant();
				_sha1hash = sha1Hash;

				xmlStream.Close();
			}

			string _md5hash = "";
			string _sha1hash = "";

			public string md5hash
			{
				get
				{
					return _md5hash.ToLower();
				}
				set
				{
					_md5hash = value;
				}
			}

			public string sha1hash
			{
				get
				{
					return _sha1hash.ToLower();
				}
				set
				{
					_sha1hash = value;
				}
			}
		}

		public static long DirSize(DirectoryInfo d)
		{
			long size = 0;
			// Add file sizes.
			FileInfo[] fis = d.GetFiles();
			foreach (FileInfo fi in fis)
			{
				size += fi.Length;
			}
			// Add subdirectory sizes.
			DirectoryInfo[] dis = d.GetDirectories();
			foreach (DirectoryInfo di in dis)
			{
				size += DirSize(di);
			}
			return size;
		}

		public static string[] SkippableFiles = {
			".DS_STORE",
			"desktop.ini"
		};

		public static int GetLookupByCode(LookupTypes LookupType, string Code)
		{
			Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
			string sql = "SELECT Id FROM " + LookupType.ToString() + " WHERE Code = @code";
			Dictionary<string, object> dbDict = new Dictionary<string, object>{
				{ "code", Code }
			};

			DataTable data = db.ExecuteCMD(sql, dbDict);
			if (data.Rows.Count == 0)
			{
				return -1;
			}
			else
			{
				return (int)data.Rows[0]["Id"];
			}
		}

		public static int GetLookupByValue(LookupTypes LookupType, string Value)
		{
			Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
			string sql = "SELECT Id FROM " + LookupType.ToString() + " WHERE Value = @value";
			Dictionary<string, object> dbDict = new Dictionary<string, object>{
				{ "value", Value }
			};

			DataTable data = db.ExecuteCMD(sql, dbDict);
			if (data.Rows.Count == 0)
			{
				return -1;
			}
			else
			{
				return (int)data.Rows[0]["Id"];
			}
		}

		public enum LookupTypes
		{
			Country,
			Language
		}

		/// <summary>
		/// Provides a way to set contextual data that flows with the call and 
		/// async context of a test or invocation.
		/// </summary>
		public static class CallContext
		{
			static ConcurrentDictionary<string, AsyncLocal<object>> state = new ConcurrentDictionary<string, AsyncLocal<object>>();

			/// <summary>
			/// Stores a given object and associates it with the specified name.
			/// </summary>
			/// <param name="name">The name with which to associate the new item in the call context.</param>
			/// <param name="data">The object to store in the call context.</param>
			public static void SetData(string name, object data) =>
				state.GetOrAdd(name, _ => new AsyncLocal<object>()).Value = data;

			/// <summary>
			/// Retrieves an object with the specified name from the <see cref="CallContext"/>.
			/// </summary>
			/// <param name="name">The name of the item in the call context.</param>
			/// <returns>The object in the call context associated with the specified name, or <see langword="null"/> if not found.</returns>
			public static object GetData(string name) =>
				state.TryGetValue(name, out AsyncLocal<object> data) ? data.Value : null;
		}
	}
}

