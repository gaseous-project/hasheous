using System;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace gaseous_server.Classes.Metadata.IGDB
{
    public class Platforms
	{
        const string fieldList = "fields abbreviation,alternative_name,category,checksum,created_at,generation,name,platform_family,platform_logo,slug,summary,updated_at,url,versions,websites;";

        public Platforms()
		{

		}

        private static IGDBClient igdb = new IGDBClient(
                    // Found in Twitch Developer portal for your app
                    Config.IGDB.ClientId,
                    Config.IGDB.Secret
                );

        public static Platform? GetPlatform(long Id, bool forceRefresh = false)
		{
            if (Id == 0)
            {
                Platform returnValue = new Platform();
                if (Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "Platform", 0) == Storage.CacheStatus.NotPresent)
                {
                    returnValue = new Platform
                    {
                        Id = 0,
                        Name = "Unknown Platform",
                        Slug = "Unknown"
                    };
                    Storage.NewCacheValue(Storage.TablePrefix.IGDB, returnValue);

                    return returnValue;
                }
                else
                {
                    return Storage.GetCacheValue<Platform>(returnValue, Storage.TablePrefix.IGDB, "id", 0);
                }
            }
            else
            {
                Task<Platform> RetVal = _GetPlatform(SearchUsing.id, Id, forceRefresh);
                return RetVal.Result;
            }
        }

		public static Platform GetPlatform(string Slug, bool forceRefresh = false)
		{
			Task<Platform> RetVal = _GetPlatform(SearchUsing.slug, Slug, forceRefresh);
			return RetVal.Result;
		}

		private static async Task<Platform> _GetPlatform(SearchUsing searchUsing, object searchValue, bool forceRefresh)
		{
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "Platform", (long)searchValue);
            }
            else
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "Platform", (string)searchValue);
            }

            if (forceRefresh == true)
            {
                if (cacheStatus == Storage.CacheStatus.Current) { cacheStatus = Storage.CacheStatus.Expired; }
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

            Platform returnValue = new Platform();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    Storage.NewCacheValue(Storage.TablePrefix.IGDB, returnValue);
                    UpdateSubClasses(returnValue);
                    return returnValue;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer(WhereClause);
                        Storage.NewCacheValue(Storage.TablePrefix.IGDB, returnValue, true);
                        UpdateSubClasses(returnValue);
                        return returnValue;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        return Storage.GetCacheValue<Platform>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                case Storage.CacheStatus.Current:
                    return Storage.GetCacheValue<Platform>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                default:
                    throw new Exception("How did you get here?");
            }
        }

        private static void UpdateSubClasses(Platform platform)
        {
            if (platform.Versions != null)
            {
                foreach (long PlatformVersionId in platform.Versions.Ids)
                {
                    PlatformVersion platformVersion = PlatformVersions.GetPlatformVersion(PlatformVersionId, platform);
                }
            }

            if (platform.PlatformLogo != null)
            {
                PlatformLogo platformLogo = PlatformLogos.GetPlatformLogo(platform.PlatformLogo.Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Platform(platform));
            }
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<Platform> GetObjectFromServer(string WhereClause)
        {
            // get platform metadata
            var results = await igdb.QueryAsync<Platform>(IGDBClient.Endpoints.Platforms, query: fieldList + " " + WhereClause + ";");
            var result = results.First();

            return result;
        }
    }
}

