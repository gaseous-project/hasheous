using System;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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

		public class RomanNumerals
		{
			/// <summary>
			/// Converts an integer to its Roman numeral representation.
			/// </summary>
			/// <param name="number">The integer to convert (1-3999).</param>
			/// <returns>A string containing the Roman numeral.</returns>
			public static string IntToRoman(int number)
			{
				if (number < 1 || number > 3999)
					throw new ArgumentOutOfRangeException(nameof(number), "Value must be in the range 1-3999.");

				var numerals = new[]
				{
				new { Value = 1000, Numeral = "M" },
				new { Value = 900, Numeral = "CM" },
				new { Value = 500, Numeral = "D" },
				new { Value = 400, Numeral = "CD" },
				new { Value = 100, Numeral = "C" },
				new { Value = 90, Numeral = "XC" },
				new { Value = 50, Numeral = "L" },
				new { Value = 40, Numeral = "XL" },
				new { Value = 10, Numeral = "X" },
				new { Value = 9, Numeral = "IX" },
				new { Value = 5, Numeral = "V" },
				new { Value = 4, Numeral = "IV" },
				new { Value = 1, Numeral = "I" }
			};

				var result = string.Empty;
				foreach (var item in numerals)
				{
					while (number >= item.Value)
					{
						result += item.Numeral;
						number -= item.Value;
					}
				}
				return result;
			}

			/// <summary>
			/// Finds the first Roman numeral in a string.
			/// </summary>
			/// <param name="input">The input string to search.</param>
			/// <returns>The first Roman numeral found, or null if none found.</returns>
			public static string? FindFirstRomanNumeral(string input)
			{
				if (string.IsNullOrEmpty(input))
					return null;

				// Regex for Roman numerals (1-3999, case-insensitive)
				var matches = Regex.Matches(input, @"\bM{0,3}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})\b", RegexOptions.IgnoreCase);
				foreach (Match match in matches)
				{
					if (match.Success && !string.IsNullOrEmpty(match.Value))
						return match.Value.ToUpper();
				}

				return null;
			}

			/// <summary>
			/// Converts a Roman numeral string to its integer representation.
			/// </summary>
			/// <param name="roman">The Roman numeral string to convert.</param>
			/// <returns>The integer representation of the Roman numeral.</returns>
			public static int RomanToInt(string roman)
			{
				if (string.IsNullOrEmpty(roman))
					throw new ArgumentException("Input cannot be null or empty.", nameof(roman));

				var romanMap = new Dictionary<char, int>
			{
				{ 'I', 1 },
				{ 'V', 5 },
				{ 'X', 10 },
				{ 'L', 50 },
				{ 'C', 100 },
				{ 'D', 500 },
				{ 'M', 1000 }
			};

				int total = 0;
				int prevValue = 0;

				foreach (char c in roman.ToUpper())
				{
					if (!romanMap.ContainsKey(c))
						throw new ArgumentException($"Invalid Roman numeral character: {c}", nameof(roman));

					int currentValue = romanMap[c];

					// If the current value is greater than the previous value, subtract twice the previous value
					// (to account for the addition in the previous iteration).
					if (currentValue > prevValue)
					{
						total += currentValue - 2 * prevValue;
					}
					else
					{
						total += currentValue;
					}

					prevValue = currentValue;
				}

				return total;
			}
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
		/// Returns the file name (with extension) if a file exists in the directory matching the given name (ignoring extension), or null if not found.
		/// </summary>
		public static string? GetFileNameWithExtension(string directoryPath, string fileNameWithoutExtension)
		{
			if (!Directory.Exists(directoryPath))
				return null;

			var files = Directory.GetFiles(directoryPath);
			var match = files.FirstOrDefault(f =>
				Path.GetFileNameWithoutExtension(f)
					.Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));

			return match != null ? Path.GetFileName(match) : null;
		}

		/// <summary>
		/// Generates a deterministic hash (default SHA256) from the public instance properties of an object.
		/// - Orders properties alphabetically by name.
		/// - For enumerable (non-string) properties, flattens items in order.
		/// - Null values are represented as &lt;null&gt;.
		/// Can optionally ignore specific properties by name (case-insensitive).
		/// The intent is a lightweight fingerprint for change detection, not a cryptographic signature of deep graphs.
		/// </summary>
		/// <param name="obj">Object to hash (returns empty string if null).</param>
		/// <param name="algorithm">Hash algorithm name (SHA256, SHA1, MD5, etc.). Defaults to SHA256.</param>
		/// <param name="ignoredProperties">Array of public property names to ignore (case-insensitive). Optional.</param>
		/// <returns>Lowercase hex hash string.</returns>
		public static string ComputeObjectPropertyHash(object? obj, string? algorithm = "SHA256", string[]? ignoredProperties = null)
		{
			if (obj == null) return string.Empty;

			var type = obj.GetType();

			// Cache property infos per type for performance
			var props = _hashPropsCache.GetOrAdd(type, t =>
				t.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
				 .Where(p => p.CanRead)
				 .OrderBy(p => p.Name, StringComparer.Ordinal)
				 .ToArray());

			HashSet<string>? ignoreSet = null;
			if (ignoredProperties != null && ignoredProperties.Length > 0)
			{
				ignoreSet = new HashSet<string>(ignoredProperties.Where(s => !string.IsNullOrWhiteSpace(s)),
					StringComparer.OrdinalIgnoreCase);
			}

			var sb = new System.Text.StringBuilder(256);

			foreach (var p in props)
			{
				if (ignoreSet != null && ignoreSet.Contains(p.Name))
					continue;

				object? value;
				try
				{
					value = p.GetValue(obj);
				}
				catch
				{
					// If a getter throws, skip it to keep hash generation resilient.
					continue;
				}

				sb.Append(p.Name);
				sb.Append('=');

				if (value == null)
				{
					sb.Append("<null>");
				}
				else if (value is string s)
				{
					sb.Append(s);
				}
				else if (value is System.Collections.IEnumerable enumerable && value is not System.Collections.IDictionary)
				{
					var first = true;
					sb.Append('[');
					foreach (var item in enumerable)
					{
						if (!first) sb.Append(',');
						first = false;
						sb.Append(item?.ToString() ?? "<null>");
					}
					sb.Append(']');
				}
				else
				{
					sb.Append(value.ToString());
				}

				sb.Append(';');
			}

			var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
			var algoName = string.IsNullOrWhiteSpace(algorithm) ? "SHA256" : algorithm.Trim();

			using var hashAlgo = System.Security.Cryptography.HashAlgorithm.Create(algoName)
				?? System.Security.Cryptography.SHA256.Create();

			var hash = hashAlgo.ComputeHash(bytes);
			var hex = new char[hash.Length * 2];
			int i = 0;
			foreach (var b in hash)
			{
				hex[i++] = GetHexNibble(b >> 4);
				hex[i++] = GetHexNibble(b & 0xF);
			}
			return new string(hex);
		}

		static char GetHexNibble(int val) => (char)(val < 10 ? ('0' + val) : ('a' + (val - 10)));

		static readonly ConcurrentDictionary<Type, System.Reflection.PropertyInfo[]> _hashPropsCache =
			new ConcurrentDictionary<Type, System.Reflection.PropertyInfo[]>();

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

