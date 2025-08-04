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

        public async Task<string> AddImage(string fileName, byte[] bytes)
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

            // save the image to disk
            string filePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages, hash + Path.GetExtension(fileName));
            if (!Directory.Exists(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages))
            {
                Directory.CreateDirectory(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages);
            }
            await File.WriteAllBytesAsync(filePath, bytes);

            // return the hash
            return hash;
        }

        public async Task<ImageItem?> GetImage(string sha1hash)
        {
            // check if the image exists on disk first before querying the database
            var diskImage = Common.GetFileNameWithExtension(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages, sha1hash);
            if (diskImage != null)
            {
                string filePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages, diskImage);
                if (File.Exists(filePath))
                {
                    return new ImageItem
                    {
                        Id = sha1hash,
                        content = await File.ReadAllBytesAsync(filePath),
                        mimeType = supportedImages[Path.GetExtension(diskImage)],
                        extension = Path.GetExtension(diskImage)
                    };
                }
            }

            // if not found on disk, query the database
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT Content, Extension FROM Images WHERE Id=@id";
            DataTable data = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
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