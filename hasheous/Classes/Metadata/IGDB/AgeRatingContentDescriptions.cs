using System;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
	public class AgeRatingContentDescriptions
    {
        const string fieldList = "fields category,checksum,description;";

        public AgeRatingContentDescriptions()
        {
        }

        public static AgeRatingContentDescription? GetAgeRatingContentDescriptions(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                Task<AgeRatingContentDescription> RetVal = _GetAgeRatingContentDescriptions(SearchUsing.id, Id);
                return RetVal.Result;
            }
        }

        public static AgeRatingContentDescription GetAgeRatingContentDescriptions(string Slug)
        {
            Task<AgeRatingContentDescription> RetVal = _GetAgeRatingContentDescriptions(SearchUsing.slug, Slug);
            return RetVal.Result;
        }

        private static async Task<AgeRatingContentDescription> _GetAgeRatingContentDescriptions(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "AgeRatingContentDescription", (long)searchValue);
            }
            else
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "AgeRatingContentDescription", (string)searchValue);
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

            AgeRatingContentDescription returnValue = new AgeRatingContentDescription();
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
                        returnValue = Storage.GetCacheValue<AgeRatingContentDescription>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = Storage.GetCacheValue<AgeRatingContentDescription>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
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

        private static async Task<AgeRatingContentDescription> GetObjectFromServer(string WhereClause)
        {
            // get AgeRatingContentDescriptionContentDescriptions metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<AgeRatingContentDescription>(IGDBClient.Endpoints.AgeRatingContentDescriptions, fieldList, WhereClause);
            var result = results.First();

            return result;
        }
	}
}

