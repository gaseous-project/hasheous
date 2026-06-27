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

		public class Numbers
		{
			private static readonly Dictionary<int, string> NumberWords = new Dictionary<int, string>
			{
				{ 0, "Zero" },
				{ 1, "One" },
				{ 2, "Two" },
				{ 3, "Three" },
				{ 4, "Four" },
				{ 5, "Five" },
				{ 6, "Six" },
				{ 7, "Seven" },
				{ 8, "Eight" },
				{ 9, "Nine" },
				{ 10, "Ten" },
				{ 11, "Eleven" },
				{ 12, "Twelve" },
				{ 13, "Thirteen" },
				{ 14, "Fourteen" },
				{ 15, "Fifteen" },
				{ 16, "Sixteen" },
				{ 17, "Seventeen" },
				{ 18, "Eighteen" },
				{ 19, "Nineteen" },
				{ 20, "Twenty" },
				{ 30, "Thirty" },
				{ 40, "Forty" },
				{ 50, "Fifty" },
				{ 60, "Sixty" },
				{ 70, "Seventy" },
				{ 80, "Eighty" },
				{ 90, "Ninety" },
				{ 100, "Hundred" },
				{ 1000, "Thousand" },
				{ 1000000, "Million" },
				{ 1000000000, "Billion" }
			};

			private static readonly Dictionary<string, int> WordsToNumber = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			{
				{ "Zero", 0 },
				{ "One", 1 },
				{ "Two", 2 },
				{ "Three", 3 },
				{ "Four", 4 },
				{ "Five", 5 },
				{ "Six", 6 },
				{ "Seven", 7 },
				{ "Eight", 8 },
				{ "Nine", 9 },
				{ "Ten", 10 },
				{ "Eleven", 11 },
				{ "Twelve", 12 },
				{ "Thirteen", 13 },
				{ "Fourteen", 14 },
				{ "Fifteen", 15 },
				{ "Sixteen", 16 },
				{ "Seventeen", 17 },
				{ "Eighteen", 18 },
				{ "Nineteen", 19 },
				{ "Twenty", 20 },
				{ "Thirty", 30 },
				{ "Forty", 40 },
				{ "Fifty", 50 },
				{ "Sixty", 60 },
				{ "Seventy", 70 },
				{ "Eighty", 80 },
				{ "Ninety", 90 },
				{ "Hundred", 100 },
				{ "Thousand", 1000 },
				{ "Million", 1000000 },
				{ "Billion", 1000000000 }
			};

			/// <summary>
			/// Converts a number to its English word representation.
			/// </summary>
			/// <param name="number">The number to convert (0 to 999,999,999).</param>
			/// <returns>The English word representation of the number.</returns>
			public static string NumberToWords(int number)
			{
				if (number < 0 || number > 999999999)
					throw new ArgumentOutOfRangeException(nameof(number), "Value must be in the range 0-999,999,999.");

				if (number == 0)
					return "Zero";

				if (NumberWords.TryGetValue(number, out var word))
					return word;

				List<string> parts = new List<string>();

				// Billions
				int billions = number / 1000000000;
				if (billions > 0)
				{
					parts.Add(NumberToWords(billions) + " Billion");
					number %= 1000000000;
				}

				// Millions
				int millions = number / 1000000;
				if (millions > 0)
				{
					parts.Add(NumberToWords(millions) + " Million");
					number %= 1000000;
				}

				// Thousands
				int thousands = number / 1000;
				if (thousands > 0)
				{
					parts.Add(NumberToWords(thousands) + " Thousand");
					number %= 1000;
				}

				// Hundreds
				int hundreds = number / 100;
				if (hundreds > 0)
				{
					parts.Add(NumberWords[hundreds] + " Hundred");
					number %= 100;
				}

				// Ones and Tens
				if (number > 0)
				{
					if (number < 20)
					{
						parts.Add(NumberWords[number]);
					}
					else
					{
						int tens = number / 10;
						int ones = number % 10;
						string tensWord = NumberWords[tens * 10];
						if (ones > 0)
						{
							parts.Add(tensWord + " " + NumberWords[ones]);
						}
						else
						{
							parts.Add(tensWord);
						}
					}
				}

				return string.Join(" ", parts);
			}

			/// <summary>
			/// Converts English number words to an integer.
			/// Handles written forms like "Twenty One", "One Hundred Thirty Four", etc.
			/// </summary>
			/// <param name="words">The English words representing a number.</param>
			/// <returns>The integer representation, or null if conversion fails.</returns>
			public static int? WordsToNumbers(string words)
			{
				if (string.IsNullOrWhiteSpace(words))
					return null;

				// Normalize spacing and remove extra whitespace
				words = Regex.Replace(words.Trim(), @"\s+", " ");
				string[] tokens = words.Split(' ', StringSplitOptions.RemoveEmptyEntries);

				int result = 0;
				int current = 0;

				foreach (string token in tokens)
				{
					if (!WordsToNumber.TryGetValue(token, out int value))
						return null; // Invalid token

					if (value >= 1000)
					{
						current += result;
						result = current * value;
						current = 0;
					}
					else if (value == 100)
					{
						current *= value;
					}
					else
					{
						current += value;
					}
				}

				result += current;
				return result >= 0 ? result : null;
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

		private sealed class LookupCacheData
		{
			public LookupCacheData(Dictionary<string, int> idByCode, Dictionary<string, int> idByValue, Dictionary<string, string> valueByCode)
			{
				IdByCode = idByCode;
				IdByValue = idByValue;
				ValueByCode = valueByCode;
			}

			public Dictionary<string, int> IdByCode { get; }
			public Dictionary<string, int> IdByValue { get; }
			public Dictionary<string, string> ValueByCode { get; }
		}

		private static readonly ConcurrentDictionary<LookupTypes, LookupCacheData> _lookupCache =
			new ConcurrentDictionary<LookupTypes, LookupCacheData>();
		private static readonly object _lookupCacheSync = new object();

		private static LookupCacheData GetOrLoadLookupCache(LookupTypes lookupType)
		{
			if (_lookupCache.TryGetValue(lookupType, out LookupCacheData? cached))
			{
				return cached;
			}

			lock (_lookupCacheSync)
			{
				if (_lookupCache.TryGetValue(lookupType, out cached))
				{
					return cached;
				}

				Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
				string sql = "SELECT `Id`, `Code`, `Value` FROM " + lookupType.ToString();
				DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>());

				Dictionary<string, int> idByCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, int> idByValue = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, string> valueByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				foreach (DataRow row in data.Rows)
				{
					int id = Convert.ToInt32(row["Id"]);
					string code = Convert.ToString(ReturnValueIfNull(row["Code"], "")) ?? "";
					string value = Convert.ToString(ReturnValueIfNull(row["Value"], "")) ?? "";

					if (!idByCode.ContainsKey(code))
					{
						idByCode[code] = id;
					}

					if (!idByValue.ContainsKey(value))
					{
						idByValue[value] = id;
					}

					if (!valueByCode.ContainsKey(code))
					{
						valueByCode[code] = value;
					}
				}

				cached = new LookupCacheData(idByCode, idByValue, valueByCode);
				_lookupCache[lookupType] = cached;
				return cached;
			}
		}

		public static void InvalidateLookupCache(LookupTypes lookupType)
		{
			_lookupCache.TryRemove(lookupType, out _);
		}

		public static void InvalidateLookupCache()
		{
			_lookupCache.Clear();
		}

		public static int GetLookupByCode(LookupTypes LookupType, string Code)
		{
			if (string.IsNullOrWhiteSpace(Code))
			{
				return -1;
			}

			LookupCacheData cache = GetOrLoadLookupCache(LookupType);
			return cache.IdByCode.TryGetValue(Code, out int id) ? id : -1;
		}

		public static int GetLookupByValue(LookupTypes LookupType, string Value)
		{
			if (string.IsNullOrWhiteSpace(Value))
			{
				return -1;
			}

			LookupCacheData cache = GetOrLoadLookupCache(LookupType);
			return cache.IdByValue.TryGetValue(Value, out int id) ? id : -1;
		}

		public static string GetNameByCode(LookupTypes LookupType, string Code)
		{
			if (string.IsNullOrWhiteSpace(Code))
			{
				return "";
			}

			LookupCacheData cache = GetOrLoadLookupCache(LookupType);
			return cache.ValueByCode.TryGetValue(Code, out string? value) ? value : "";
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

		public static bool IsStrongNameMatch(string candidate, string? resultName)
		{
			return GetStrongNameMatchScore(candidate, resultName) >= 8;
		}

		public static int GetStrongNameMatchScore(string candidate, string? resultName)
		{
			if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(resultName))
			{
				return int.MinValue;
			}

			if (string.Equals(candidate, resultName, StringComparison.OrdinalIgnoreCase))
			{
				return 100;
			}

			HashSet<string> candidateTokens = TokenizeWithNumericVariants(candidate);
			HashSet<string> resultTokens = TokenizeWithNumericVariants(resultName);

			// Pull in parenthetical aliases, e.g. "Commodore 65 (C64)" should expose "C64" and "64".
			foreach (Match aliasMatch in Regex.Matches(resultName, @"\(([^)]{1,40})\)"))
			{
				foreach (string token in TokenizeWithNumericVariants(aliasMatch.Groups[1].Value))
				{
					resultTokens.Add(token);
				}
			}

			int score = 0;
			HashSet<string> matchedResultTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Every candidate token must be represented in the result with exact or near-exact matching.
			foreach (string candidateToken in candidateTokens)
			{
				int bestTokenScore = int.MinValue;
				string? bestResultToken = null;

				foreach (string resultToken in resultTokens)
				{
					int tokenScore = TokenMatchScore(candidateToken, resultToken);
					if (tokenScore > bestTokenScore)
					{
						bestTokenScore = tokenScore;
						bestResultToken = resultToken;
					}
				}

				if (bestTokenScore <= 0 || bestResultToken == null)
				{
					return int.MinValue;
				}

				score += bestTokenScore;
				matchedResultTokens.Add(bestResultToken);
			}

			int unmatchedResultTokenPenalty = resultTokens
				.Where(token => !matchedResultTokens.Contains(token))
				.Count(token => token.Length >= 3);

			score -= unmatchedResultTokenPenalty * 3;

			string normalizedCandidate = Regex.Replace(candidate.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
			string normalizedResult = Regex.Replace(resultName.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
			score -= Math.Abs(normalizedResult.Length - normalizedCandidate.Length) / 2;

			return score;
		}

		private static HashSet<string> TokenizeWithNumericVariants(string input)
		{
			HashSet<string> tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string rawPart in Regex.Split(input, @"[^A-Za-z0-9]+"))
			{
				if (string.IsNullOrWhiteSpace(rawPart))
				{
					continue;
				}

				string token = rawPart.Trim().ToLowerInvariant();
				tokens.Add(token);

				// Add a numeric variant for shorthand tokens like "c64" -> "64".
				string numericOnly = new string(token.Where(char.IsDigit).ToArray());
				if (!string.IsNullOrEmpty(numericOnly))
				{
					tokens.Add(numericOnly);
				}
			}

			return tokens;
		}

		private static bool TokensEquivalent(string a, string b)
		{
			return TokenMatchScore(a, b) > 0;
		}

		private static int TokenMatchScore(string a, string b)
		{
			if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
			{
				return 10;
			}

			int maxLength = Math.Max(a.Length, b.Length);
			int distance = LevenshteinDistance(a, b);

			// Keep typo tolerance conservative to avoid incorrect automatic matches.
			if (maxLength >= 8 && distance <= 2)
			{
				return 6;
			}

			if (maxLength >= 4 && distance <= 1)
			{
				return 7;
			}

			return 0;
		}

		private static int LevenshteinDistance(string left, string right)
		{
			int[,] d = new int[left.Length + 1, right.Length + 1];

			for (int i = 0; i <= left.Length; i++)
			{
				d[i, 0] = i;
			}

			for (int j = 0; j <= right.Length; j++)
			{
				d[0, j] = j;
			}

			for (int i = 1; i <= left.Length; i++)
			{
				for (int j = 1; j <= right.Length; j++)
				{
					int cost = left[i - 1] == right[j - 1] ? 0 : 1;
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost
					);
				}
			}

			return d[left.Length, right.Length];
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

