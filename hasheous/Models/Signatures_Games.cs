using System;
using System.Text.Json.Serialization;
using gaseous_signature_parser.models.RomSignatureObject;

namespace hasheous_server.Models
{
	public class Signatures_Games : HasheousClient.Models.LookupResponseModel
	{
		public Signatures_Games()
		{
		}
    }

	public class Signatures_Games_2
	{
		public GameItem? Game { get; set; }
		public RomItem? Rom { get; set; }

		public int Score
        {
            get
            {
                int _score = 0;

                if (Game != null)
                {
                    _score = _score + Game.Score;
                }

                if (Rom != null)
                {
                    _score = _score + Rom.Score;
                }

                return _score;
            }
        }

		public class GameItem : gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game
		{
			public GameItem()
			{
			}

			public long SystemId { get; set; }

            public long PublisherId { get; set; }

            public Dictionary<string, string> Countries = new Dictionary<string, string>();
            public Dictionary<string, string> Languages = new Dictionary<string, string>();

			public int Score
            {
                get
                {
                    // calculate a score based on the availablility of data
                    int _score = 0;
                    var properties = this.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        if (prop.GetGetMethod() != null)
                        {
                            switch (prop.Name.ToLower())
                            {
                                case "id":
                                case "score":
                                    break;
                                case "name":
                                case "year":
                                case "publisher":
                                case "system":
                                    if (prop.PropertyType == typeof(string))
                                    {
                                        if (prop.GetValue(this) != null)
                                        {
                                            string propVal = prop.GetValue(this).ToString();
                                            if (propVal.Length > 0)
                                            {
                                                _score = _score + 10;
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    if (prop.PropertyType == typeof(string))
                                    {
                                        if (prop.GetValue(this) != null)
                                        {
                                            string propVal = prop.GetValue(this).ToString();
                                            if (propVal.Length > 0)
                                            {
                                                _score = _score + 1;
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                    }

                    return _score;
                }
            }
		}

		public class RomItem : gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom
		{
			public RomItem()
			{
			}

			public int Score
            {
                get
                {
                    // calculate a score based on the availablility of data
                    int _score = 0;
                    var properties = this.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        if (prop.GetGetMethod() != null)
                        {
                            switch (prop.Name.ToLower())
                            {
                                case "name":
                                case "size":
                                case "crc":
                                case "developmentstatus":
                                case "flags":
                                case "attributes":
                                case "romtypemedia":
                                case "medialabel":
                                    if (prop.PropertyType == typeof(string) || prop.PropertyType == typeof(Int64) || prop.PropertyType == typeof(List<string>))
                                    {
                                        if (prop.GetValue(this) != null)
                                        {
                                            string propVal = prop.GetValue(this).ToString();
                                            if (propVal.Length > 0)
                                            {
                                                _score = _score + 10;
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    if (prop.PropertyType == typeof(string))
                                    {
                                        if (prop.GetValue(this) != null)
                                        {
                                            string propVal = prop.GetValue(this).ToString();
                                            if (propVal.Length > 0)
                                            {
                                                _score = _score + 1;
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                    }

                    return _score;
                }
            }
		}
	}
}

