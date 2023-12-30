using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SignaturesController : ControllerBase
    {
        /// <summary>
        /// Get the current signature counts from the database
        /// </summary>
        /// <returns>Number of sources, publishers, games, and rom signatures in the database</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public Signatures_Status Status()
        {
            return new Signatures_Status();
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public List<Signatures_Games> GetSignature(string md5 = "", string sha1 = "")
        {
            if (md5.Length > 0)
            {
                return _GetSignature("Signatures_Roms.md5 = @searchstring", md5);
            } else
            {
                return _GetSignature("Signatures_Roms.sha1 = @searchstring", sha1);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public List<Signatures_Games> GetByTosecName(string TosecName = "")
        {
            if (TosecName.Length > 0)
            {
                return _GetSignature("Signatures_Roms.name = @searchstring", TosecName);
            } else
            {
                return null;
            }
        }

        private List<Signatures_Games> _GetSignature(string sqlWhere, string searchString)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT     view_Signatures_Games.*,    Signatures_Roms.Id AS romid,    Signatures_Roms.Name AS romname,    Signatures_Roms.Size,    Signatures_Roms.CRC,    Signatures_Roms.MD5,    Signatures_Roms.SHA1,    Signatures_Roms.DevelopmentStatus,    Signatures_Roms.Attributes,    Signatures_Roms.RomType,    Signatures_Roms.RomTypeMedia,    Signatures_Roms.MediaLabel,    Signatures_Roms.MetadataSource FROM    Signatures_Roms        INNER JOIN    view_Signatures_Games ON Signatures_Roms.GameId = view_Signatures_Games.Id WHERE " + sqlWhere;
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            dbDict.Add("searchString", searchString);

            DataTable sigDb = db.ExecuteCMD(sql, dbDict);

            List<Signatures_Games> GamesList = new List<Signatures_Games>();

            foreach (DataRow sigDbRow in sigDb.Rows)
            {
                Signatures_Games.GameItem game = new Signatures_Games.GameItem
                {
                    Id = (long)sigDbRow["Id"],
                    Name = (string)sigDbRow["Name"],
                    Description = (string)sigDbRow["Description"],
                    Year = (string)sigDbRow["Year"],
                    Publisher = (string)sigDbRow["Publisher"],
                    Demo = (Signatures_Games.GameItem.DemoTypes)(int)sigDbRow["Demo"],
                    SystemId = (int)sigDbRow["PlatformId"],
                    System = (string)sigDbRow["Platform"],
                    SystemVariant = (string)sigDbRow["SystemVariant"],
                    Video = (string)sigDbRow["Video"],
                    Country = (string)sigDbRow["Country"],
                    Language = (string)sigDbRow["Language"],
                    Copyright = (string)sigDbRow["Copyright"]
                };
                Signatures_Games.RomItem rom = new Signatures_Games.RomItem{
                    Id = (long)sigDbRow["romid"],
                    Name = (string)sigDbRow["romname"],
                    Size = (long)sigDbRow["Size"],
                    Crc = (string)sigDbRow["CRC"],
                    Md5 = ((string)sigDbRow["MD5"]).ToLower(),
                    Sha1 = ((string)sigDbRow["SHA1"]).ToLower(),
                    DevelopmentStatus = (string)sigDbRow["DevelopmentStatus"],
                    Attributes = Newtonsoft.Json.JsonConvert.DeserializeObject<List<KeyValuePair<string, object>>>((string)Common.ReturnValueIfNull(sigDbRow["Attributes"], "[]")),
                    RomType = (Signatures_Games.RomItem.RomTypes)(int)sigDbRow["RomType"],
                    RomTypeMedia = (string)sigDbRow["RomTypeMedia"],
                    MediaLabel = (string)sigDbRow["MediaLabel"],
                    SignatureSource = (Signatures_Games.RomItem.SignatureSourceType)(Int32)sigDbRow["MetadataSource"]
                };

                Signatures_Games gameItem = new Signatures_Games
                {
                    Game = game,
                    Rom = rom
                };
                GamesList.Add(gameItem);
            }
            return GamesList;
        }
    }
}

