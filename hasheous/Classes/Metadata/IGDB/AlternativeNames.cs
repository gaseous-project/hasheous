using System;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
	public class AlternativeNames
    {
        const string fieldList = "fields checksum,comment,game,name;";

        public AlternativeNames()
        {
        }

        public static AlternativeName? GetAlternativeNames(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                Task<AlternativeName> RetVal = _GetAlternativeNames(SearchUsing.id, Id);
                return RetVal.Result;
            }
        }

        public static AlternativeName GetAlternativeNames(string Slug)
        {
            Task<AlternativeName> RetVal = _GetAlternativeNames(SearchUsing.slug, Slug);
            return RetVal.Result;
        }

        private static async Task<AlternativeName> _GetAlternativeNames(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "AlternativeName", (long)searchValue);
            }
            else
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "AlternativeName", (string)searchValue);
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

            AlternativeName returnValue = new AlternativeName();
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
                        returnValue = Storage.GetCacheValue<AlternativeName>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = Storage.GetCacheValue<AlternativeName>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
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

        private static async Task<AlternativeName> GetObjectFromServer(string WhereClause)
        {
            // get AlternativeNames metadata
            Communications comms = new Communications();
            var results = await comms.APIComm<AlternativeName>(IGDBClient.Endpoints.AlternativeNames, fieldList, WhereClause);
            var result = results.First();

            return result;
        }
	}
}

