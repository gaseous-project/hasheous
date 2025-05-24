using System;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class InvolvedCompanies
    {
        const string fieldList = "fields *;";

        public InvolvedCompanies()
        {
        }


        public static async Task<InvolvedCompany?> GetInvolvedCompanies(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                return await _GetInvolvedCompanies(SearchUsing.id, Id);
            }
        }

        public static async Task<InvolvedCompany> GetInvolvedCompanies(string Slug)
        {
            return await _GetInvolvedCompanies(SearchUsing.slug, Slug);
        }

        private static async Task<InvolvedCompany> _GetInvolvedCompanies(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "InvolvedCompany", (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "InvolvedCompany", (string)searchValue);
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

            InvolvedCompany returnValue = new InvolvedCompany();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
                    await UpdateSubClasses(returnValue);
                    break;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer(WhereClause);
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = await Storage.GetCacheValueAsync<InvolvedCompany>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = await Storage.GetCacheValueAsync<InvolvedCompany>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    break;
                default:
                    throw new Exception("How did you get here?");
            }

            return returnValue;
        }

        private static async Task UpdateSubClasses(InvolvedCompany involvedCompany)
        {
            if (involvedCompany.Company != null)
            {
                Company company = await Companies.GetCompanies(involvedCompany.Company.Id);
            }
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<InvolvedCompany> GetObjectFromServer(string WhereClause)
        {
            // get InvolvedCompanies metadata
            try
            {
                Communications comms = new Communications(Communications.MetadataSources.IGDB);
                var results = await comms.APIComm<InvolvedCompany>(IGDBClient.Endpoints.InvolvedCompanies, fieldList, WhereClause);
                var result = results.First();

                return result;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "Involved Companies", "Failure when requesting involved companies.");
                Logging.Log(Logging.LogType.Critical, "Involved Companies", "Field list: " + fieldList);
                Logging.Log(Logging.LogType.Critical, "Involved Companies", "Where clause: " + WhereClause);
                Logging.Log(Logging.LogType.Critical, "Involved Companies", "Error", ex);
                throw;
            }
        }
    }
}

