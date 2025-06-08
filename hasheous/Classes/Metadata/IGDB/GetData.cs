using System;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Metadata
    {
        const string fieldList = "fields *;";

        public Metadata()
        {
        }

        public static async Task<T?> GetMetadata<T>(long? Id) where T : new()
        {
            if ((Id == 0) || (Id == null))
            {
                return default;
            }
            else
            {
                return await _GetMetadata<T>(SearchUsing.id, Id);
            }
        }

        public static async Task<T?> GetMetadata<T>(string Slug) where T : new()
        {
            return await _GetMetadata<T>(SearchUsing.slug, Slug);
        }

        private static async Task<T?> _GetMetadata<T>(SearchUsing searchUsing, object searchValue) where T : new()
        {
            // get the type name of T
            string typeName = typeof(T).Name;

            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, typeName, (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, typeName, (string)searchValue);
            }

            // set up where clause
            string WhereClause = "";
            string searchField = "";
            switch (searchUsing)
            {
                case SearchUsing.id:
                    WhereClause = "where id = " + searchValue;
                    searchField = "id";
                    break;
                case SearchUsing.slug:
                    WhereClause = "where slug = \"" + searchValue + "\"";
                    searchField = "slug";
                    break;
                default:
                    throw new Exception("Invalid search type");
            }

            T returnValue = new T();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer<T>(WhereClause);
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
                    break;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer<T>(WhereClause);
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = await Storage.GetCacheValueAsync<T>(returnValue, Storage.TablePrefix.IGDB, searchField, searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = await Storage.GetCacheValueAsync<T>(returnValue, Storage.TablePrefix.IGDB, searchField, searchValue);
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

        private static async Task<T> GetObjectFromServer<T>(string WhereClause)
        {
            Communications comms = new Communications(Communications.MetadataSources.IGDB);

            string endpoint = GetEndpointData<T>().Endpoint;

            var results = await comms.APIComm<T>(endpoint, fieldList, WhereClause);
            var result = results.FirstOrDefault();

            return result;
        }

        public static EndpointDataItem GetEndpointData<T>()
        {
            // use reflection to get the endpoint for the type T. The endpoint is a public const and is the name of the type, and is under IGDBClient.Endpoints
            var typeName = typeof(T).Name;
            EndpointDataItem endpoint = new EndpointDataItem();

            switch (typeName)
            {
                case "AgeRatingCategory":
                    endpoint.Endpoint = IGDBClient.Endpoints.AgeRatingCategories;
                    break;

                case "AgeRatingContentDescriptionV2":
                    endpoint.Endpoint = "age_rating_content_descriptions_v2";
                    break;

                case "Collection":
                    endpoint.Endpoint = IGDBClient.Endpoints.Collections;
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "CollectionMembership":
                    endpoint.Endpoint = "collection_memberships";
                    break;

                case "CollectionMembershipType":
                    endpoint.Endpoint = "collection_membership_types";
                    break;

                case "CollectionRelation":
                    endpoint.Endpoint = "collection_relations";
                    break;

                case "CollectionRelationType":
                    endpoint.Endpoint = "collection_relation_types";
                    break;

                case "CollectionType":
                    endpoint.Endpoint = "collection_types";
                    break;

                case "Company":
                    endpoint.Endpoint = IGDBClient.Endpoints.Companies;
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "CompanyStatus":
                    endpoint.Endpoint = "company_statuses";
                    break;

                case "Event":
                    endpoint.Endpoint = "events";
                    break;

                case "EventLogo":
                    endpoint.Endpoint = "event_logos";
                    break;

                case "EventNetwork":
                    endpoint.Endpoint = "event_networks";
                    break;

                case "Franchise":
                    endpoint.Endpoint = IGDBClient.Endpoints.Franchies;
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "Game":
                    endpoint.Endpoint = IGDBClient.Endpoints.Games;
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "GameLocalization":
                    endpoint.Endpoint = "game_localizations";
                    break;

                case "GameStatus":
                    endpoint.Endpoint = "game_statuses";
                    break;

                case "Language":
                    endpoint.Endpoint = "languages";
                    break;

                case "LanguageSupport":
                    endpoint.Endpoint = "language_supports";
                    break;

                case "LanguageSupportType":
                    endpoint.Endpoint = "language_support_types";
                    break;

                case "NetworkType":
                    endpoint.Endpoint = "network_types";
                    break;

                case "Platform":
                    endpoint.Endpoint = IGDBClient.Endpoints.Platforms;
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "PlatformFamily":
                    endpoint.Endpoint = IGDBClient.Endpoints.PlatformFamilies;
                    break;

                case "PlatformVersionCompany":
                    endpoint.Endpoint = "platform_version_companies";
                    break;

                case "Region":
                    endpoint.Endpoint = "regions";
                    break;

                case "ReleaseDateStatus":
                    endpoint.Endpoint = "release_date_statuses";
                    break;

                default:
                    var endpointField = typeof(IGDBClient.Endpoints).GetField(typeName);
                    if (endpointField == null)
                    {
                        // try again with pluralized type name
                        endpointField = typeof(IGDBClient.Endpoints).GetField(typeName + "s");

                        if (endpointField == null)
                            return null;
                    }

                    endpoint.Endpoint = (string)endpointField.GetValue(null);
                    break;
            }

            return endpoint;
        }

        public class EndpointDataItem
        {
            public string Endpoint { get; set; }
            public bool SupportsSlugSearch { get; set; } = false;
        }
    }
}