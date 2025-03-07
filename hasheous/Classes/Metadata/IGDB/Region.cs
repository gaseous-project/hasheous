using System;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Regions
    {
        const string fieldList = "fields category,checksum,created_at,identifier,name,updated_at;";

        public Regions()
        {
        }


        public static Region? GetGame_Regions(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                Task<Region> RetVal = _GetGame_Regions(SearchUsing.id, Id);
                return RetVal.Result;
            }
        }

        public static Region GetGame_Regions(string Slug)
        {
            Task<Region> RetVal = _GetGame_Regions(SearchUsing.slug, Slug);
            return RetVal.Result;
        }

        private static async Task<Region> _GetGame_Regions(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "Region", (long)searchValue);
            }
            else
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "Region", (string)searchValue);
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

            Region returnValue = new Region();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    Storage.NewCacheValue(Storage.TablePrefix.IGDB, returnValue);
                    break;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer(WhereClause);
                        Storage.NewCacheValue(Storage.TablePrefix.IGDB, returnValue, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = Storage.GetCacheValue<Region>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = Storage.GetCacheValue<Region>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    break;
                default:
                    throw new Exception("How did you get here?");
            }

            return returnValue;
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<Region> GetObjectFromServer(string WhereClause)
        {
            // get Game_Modes metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<Region>("regions", fieldList, WhereClause);
            var result = results.First();

            return result;
        }
    }
}

