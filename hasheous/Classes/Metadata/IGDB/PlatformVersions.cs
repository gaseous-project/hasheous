using System;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class PlatformVersions
    {
        const string fieldList = "fields checksum,companies,connectivity,cpu,graphics,main_manufacturer,media,memory,name,online,os,output,platform_logo,platform_version_release_dates,resolutions,slug,sound,storage,summary,url;";

        public PlatformVersions()
        {
        }


        public static async Task<PlatformVersion?> GetPlatformVersion(long Id)//, Platform ParentPlatform)
        {
            if (Id == 0)
            {
                return null;
            }
            else
            {
                return await _GetPlatformVersion(SearchUsing.id, Id);//, ParentPlatform);
            }
        }

        public static async Task<PlatformVersion> GetPlatformVersion(string Slug)//, Platform ParentPlatform)
        {
            return await _GetPlatformVersion(SearchUsing.slug, Slug);//, ParentPlatform);
        }

        private static async Task<PlatformVersion> _GetPlatformVersion(SearchUsing searchUsing, object searchValue)//, Platform ParentPlatform)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "PlatformVersion", (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "PlatformVersion", (string)searchValue);
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

            PlatformVersion returnValue = new PlatformVersion();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    if (returnValue != null)
                    {
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
                        // UpdateSubClasses(ParentPlatform, returnValue);
                    }
                    return returnValue;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer(WhereClause);
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue, true);
                        // UpdateSubClasses(ParentPlatform, returnValue);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = await Storage.GetCacheValueAsync<PlatformVersion>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    return returnValue;
                case Storage.CacheStatus.Current:
                    return await Storage.GetCacheValueAsync<PlatformVersion>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                default:
                    throw new Exception("How did you get here?");
            }
        }

        // private static void UpdateSubClasses(Platform ParentPlatform, PlatformVersion platformVersion)
        // {
        //     if (platformVersion.PlatformLogo != null)
        //     {
        //         PlatformLogo platformLogo = PlatformLogos.GetPlatformLogo(platformVersion.PlatformLogo.Id, Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Platform(ParentPlatform), "Versions", platformVersion.Slug));
        //     }
        // }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<PlatformVersion?> GetObjectFromServer(string WhereClause)
        {
            // get PlatformVersion metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<PlatformVersion>(IGDBClient.Endpoints.PlatformVersions, fieldList, WhereClause);
            if (results.Length > 0)
            {
                var result = results.First();

                return result;
            }
            else
            {
                return null;
            }
        }
    }
}

