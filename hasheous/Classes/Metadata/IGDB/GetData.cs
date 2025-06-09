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
                case "AgeRating":
                    endpoint.Endpoint = "age_ratings";
                    break;

                case "AgeRatingCategory":
                    endpoint.Endpoint = "age_rating_categories";
                    break;

                case "AgeRatingContentDescriptionV2":
                    endpoint.Endpoint = "age_rating_content_descriptions_v2";
                    break;

                case "AgeRatingOrganization":
                    endpoint.Endpoint = "age_rating_organizations";
                    break;

                case "AlternativeName":
                    endpoint.Endpoint = "alternative_names";
                    break;

                case "Artwork":
                    endpoint.Endpoint = "artworks";
                    break;

                case "Character":
                    endpoint.Endpoint = "characters";
                    break;

                case "CharacterGender":
                    endpoint.Endpoint = "character_genders";
                    break;

                case "CharacterMugshot":
                    endpoint.Endpoint = "character_mug_shots";
                    break;

                case "CharacterSpecies":
                    endpoint.Endpoint = "character_species";
                    break;

                case "Collection":
                    endpoint.Endpoint = "collections";
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
                    endpoint.Endpoint = "companies";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "CompanyLogo":
                    endpoint.Endpoint = "company_logos";
                    break;

                case "CompanyStatus":
                    endpoint.Endpoint = "company_statuses";
                    break;

                case "CompanyWebsite":
                    endpoint.Endpoint = "company_websites";
                    break;

                case "Cover":
                    endpoint.Endpoint = "covers";
                    break;

                case "DateFormat":
                    endpoint.Endpoint = "date_formats";
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

                case "ExternalGame":
                    endpoint.Endpoint = "external_games";
                    break;

                case "ExternalGameSource":
                    endpoint.Endpoint = "external_game_sources";
                    break;

                case "Franchise":
                    endpoint.Endpoint = "franchises";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "Game":
                    endpoint.Endpoint = "games";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "GameEngine":
                    endpoint.Endpoint = "game_engines";
                    break;

                case "GameEngineLogo":
                    endpoint.Endpoint = "game_engine_logos";
                    break;

                case "GameLocalization":
                    endpoint.Endpoint = "game_localizations";
                    break;

                case "GameMode":
                    endpoint.Endpoint = "game_modes";
                    break;

                case "GameReleaseFormat":
                    endpoint.Endpoint = "game_release_formats";
                    break;

                case "GameStatus":
                    endpoint.Endpoint = "game_statuses";
                    break;

                case "GameTimeToBeat":
                    endpoint.Endpoint = "game_time_to_beats";
                    break;

                case "GameType":
                    endpoint.Endpoint = "game_types";
                    break;

                case "GameVersion":
                    endpoint.Endpoint = "game_versions";
                    break;

                case "GameVersionFeature":
                    endpoint.Endpoint = "game_version_features";
                    break;

                case "GameVersionFeatureValue":
                    endpoint.Endpoint = "game_version_feature_values";
                    break;

                case "GameVideo":
                    endpoint.Endpoint = "game_videos";
                    break;

                case "Genre":
                    endpoint.Endpoint = "genres";
                    break;

                case "Keyword":
                    endpoint.Endpoint = "keywords";
                    break;

                case "InvolvedCompany":
                    endpoint.Endpoint = "involved_companies";
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

                case "MultiplayerMode":
                    endpoint.Endpoint = "multiplayer_modes";
                    break;

                case "NetworkType":
                    endpoint.Endpoint = "network_types";
                    break;

                case "Platform":
                    endpoint.Endpoint = "platforms";
                    endpoint.SupportsSlugSearch = true;
                    break;

                case "PlatformFamily":
                    endpoint.Endpoint = "platform_families";
                    break;

                case "PlatformLogo":
                    endpoint.Endpoint = "platform_logos";
                    break;

                case "PlatformType":
                    endpoint.Endpoint = "platform_types";
                    break;

                case "PlatformVersion":
                    endpoint.Endpoint = "platform_versions";
                    break;

                case "PlatformVersionCompany":
                    endpoint.Endpoint = "platform_version_companies";
                    break;

                case "PlatformVersionReleaseDate":
                    endpoint.Endpoint = "platform_version_release_dates";
                    break;

                case "PlatformWebsite":
                    endpoint.Endpoint = "platform_websites";
                    break;

                case "PlayerPerspective":
                    endpoint.Endpoint = "player_perspectives";
                    break;

                case "PopularityPrimitive":
                    endpoint.Endpoint = "popularity_primitives";
                    break;

                case "PopularityType":
                    endpoint.Endpoint = "popularity_types";
                    break;

                case "Region":
                    endpoint.Endpoint = "regions";
                    break;

                case "ReleaseDate":
                    endpoint.Endpoint = "release_dates";
                    break;

                case "ReleaseDateRegion":
                    endpoint.Endpoint = "release_date_regions";
                    break;

                case "ReleaseDateStatus":
                    endpoint.Endpoint = "release_date_statuses";
                    break;

                case "Screenshot":
                    endpoint.Endpoint = "screenshots";
                    break;

                case "Search":
                    endpoint.Endpoint = "search";
                    break;

                case "Theme":
                    endpoint.Endpoint = "themes";
                    break;

                case "Website":
                    endpoint.Endpoint = "websites";
                    break;

                case "WebsiteType":
                    endpoint.Endpoint = "website_types";
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