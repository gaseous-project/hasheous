using System;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class PlayerPerspectives
    {
        const string fieldList = "fields checksum,created_at,name,slug,updated_at,url;";

        public PlayerPerspectives()
        {
        }


        public static PlayerPerspective? GetGame_PlayerPerspectives(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                Task<PlayerPerspective> RetVal = _GetGame_PlayerPerspectives(SearchUsing.id, Id);
                return RetVal.Result;
            }
        }

        public static PlayerPerspective GetGame_PlayerPerspectives(string Slug)
        {
            Task<PlayerPerspective> RetVal = _GetGame_PlayerPerspectives(SearchUsing.slug, Slug);
            return RetVal.Result;
        }

        private static async Task<PlayerPerspective> _GetGame_PlayerPerspectives(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "PlayerPerspective", (long)searchValue);
            }
            else
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "PlayerPerspective", (string)searchValue);
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

            PlayerPerspective returnValue = new PlayerPerspective();
            bool forceImageDownload = false;
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    Storage.NewCacheValue(Storage.TablePrefix.IGDB, returnValue);
                    forceImageDownload = true;
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
                        returnValue = Storage.GetCacheValue<PlayerPerspective>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = Storage.GetCacheValue<PlayerPerspective>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
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

        private static async Task<PlayerPerspective> GetObjectFromServer(string WhereClause)
        {
            // get Game_PlayerPerspectives metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<PlayerPerspective>(IGDBClient.Endpoints.PlayerPerspectives, fieldList, WhereClause);
            var result = results.First();

            return result;
        }
    }
}

