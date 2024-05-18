using System.Data;
using Classes;
using hasheous_server.Models;

namespace hasheous_server.Classes
{
    public class Images
    {
        static readonly Dictionary<string, string> supportedImages = new Dictionary<string, string>{
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".bmp", "image/bmp" },
            { ".svg", "image/svg+xml" }
        };

        public string AddImage(string fileName, byte[] bytes)
        {
            // check if it's a supported file type
            if (!supportedImages.ContainsKey(Path.GetExtension(fileName).ToLower()))
            {
                throw new Exception("File type not supported");
            }

            // check hash isn't already in the db and return the hash if it is
            string hash;
            using (var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider())
            {
                hash = string.Concat(sha1.ComputeHash(bytes).Select(x => x.ToString("X2")));
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT `Id` FROM Images WHERE Id = @id";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", hash }
            });
            if (data.Rows.Count > 0)
            {
                return hash;
            }

            // add the image to the database
            sql = "INSERT INTO Images (Id, Content, Extension) VALUES (@id, @content, @ext);";
            db.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "id", hash },
                { "content", bytes },
                { "ext", Path.GetExtension(fileName) }
            });

            return hash;
        }

        public ImageItem? GetImage(string sha1hash)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT Content, Extension FROM Images WHERE Id=@id";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", sha1hash }
            });

            if (data.Rows.Count == 0)
            {
                return null;
            }
            else
            {
                ImageItem image = new ImageItem
                {
                    Id = sha1hash,
                    content = data.Rows[0]["Content"] as byte[],
                    mimeType = supportedImages[data.Rows[0]["Extension"] as string],
                    extension = data.Rows[0]["Extension"] as string
                };
                return image;
            }
        }
    }
}