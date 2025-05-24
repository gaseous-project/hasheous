using System;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Screenshots
    {
        const string fieldList = "fields alpha_channel,animated,checksum,game,height,image_id,url,width;";

        public Screenshots()
        {
        }


        public static async Task<Screenshot?> GetScreenshot(long? Id, string LogoPath)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                return await _GetScreenshot(SearchUsing.id, Id, LogoPath);
            }
        }

        public static async Task<Screenshot> GetScreenshot(string Slug, string LogoPath)
        {
            return await _GetScreenshot(SearchUsing.slug, Slug, LogoPath);
        }

        private static async Task<Screenshot> _GetScreenshot(SearchUsing searchUsing, object searchValue, string LogoPath)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Screenshot", (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Screenshot", (string)searchValue);
            }

            // set up where clause
            string WhereClause = "";
            switch (searchUsing)
            {
                case SearchUsing.id:
                    WhereClause = "where id = " + searchValue;
                    break;
                case SearchUsing.slug:
                    WhereClause = "where slug = " + searchValue;
                    break;
                default:
                    throw new Exception("Invalid search type");
            }

            Screenshot returnValue = new Screenshot();
            bool forceImageDownload = false;
            LogoPath = Path.Combine(LogoPath, "Screenshots");
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause, LogoPath);
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
                    forceImageDownload = true;
                    break;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer(WhereClause, LogoPath);
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue, true);
                        forceImageDownload = true;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = await Storage.GetCacheValueAsync<Screenshot>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = await Storage.GetCacheValueAsync<Screenshot>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    break;
                default:
                    throw new Exception("How did you get here?");
            }

            // if ((!File.Exists(Path.Combine(LogoPath, "Screenshot.jpg"))) || forceImageDownload == true)
            // {
            //     //GetImageFromServer(returnValue.Url, LogoPath, LogoSize.t_thumb, returnValue.ImageId);
            //     //GetImageFromServer(returnValue.Url, LogoPath, LogoSize.t_logo_med, returnValue.ImageId);
            //     GetImageFromServer(returnValue.Url, LogoPath, LogoSize.t_original, returnValue.ImageId);
            // }

            return returnValue;
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<Screenshot> GetObjectFromServer(string WhereClause, string LogoPath)
        {
            // get Screenshot metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<Screenshot>(IGDBClient.Endpoints.Screenshots, fieldList, WhereClause);
            var result = results.First();

            //GetImageFromServer(result.Url, LogoPath, LogoSize.t_thumb, result.ImageId);
            //GetImageFromServer(result.Url, LogoPath, LogoSize.t_logo_med, result.ImageId);
            //GetImageFromServer(result.Url, LogoPath, LogoSize.t_original, result.ImageId);

            return result;
        }

        // private static void GetImageFromServer(string Url, string LogoPath, LogoSize logoSize, string ImageId)
        // {
        //     using (var client = new HttpClient())
        //     {
        //         string fileName = "Artwork.jpg";
        //         string extension = "jpg";
        //         switch (logoSize)
        //         {
        //             case LogoSize.t_thumb:
        //                 fileName = "_Thumb";
        //                 extension = "jpg";
        //                 break;
        //             case LogoSize.t_logo_med:
        //                 fileName = "_Medium";
        //                 extension = "png";
        //                 break;
        //             case LogoSize.t_original:
        //                 fileName = "";
        //                 extension = "png";
        //                 break;
        //             default:
        //                 fileName = "Artwork";
        //                 extension = "jpg";
        //                 break;
        //         }
        //         fileName = ImageId + fileName;
        //         string imageUrl = Url.Replace(LogoSize.t_thumb.ToString(), logoSize.ToString()).Replace("jpg", extension);

        //         using (var s = client.GetStreamAsync("https:" + imageUrl))
        //         {
        //             if (!Directory.Exists(LogoPath)) { Directory.CreateDirectory(LogoPath); }
        //             using (var fs = new FileStream(Path.Combine(LogoPath, fileName + "." + extension), FileMode.OpenOrCreate))
        //             {
        //                 s.Result.CopyTo(fs);
        //             }
        //         }
        //     }
        // }

        private enum LogoSize
        {
            t_thumb,
            t_logo_med,
            t_original
        }
    }
}

