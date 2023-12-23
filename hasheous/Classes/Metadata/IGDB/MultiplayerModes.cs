using System;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class MultiplayerModes
    {
        const string fieldList = "fields campaigncoop,checksum,dropin,game,lancoop,offlinecoop,offlinecoopmax,offlinemax,onlinecoop,onlinecoopmax,onlinemax,platform,splitscreen,splitscreenonline;";

        public MultiplayerModes()
        {
        }


        public static MultiplayerMode? GetGame_MultiplayerModes(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                Task<MultiplayerMode> RetVal = _GetGame_MultiplayerModes(SearchUsing.id, Id);
                return RetVal.Result;
            }
        }

        public static MultiplayerMode GetGame_MultiplayerModes(string Slug)
        {
            Task<MultiplayerMode> RetVal = _GetGame_MultiplayerModes(SearchUsing.slug, Slug);
            return RetVal.Result;
        }

        private static async Task<MultiplayerMode> _GetGame_MultiplayerModes(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "MultiplayerMode", (long)searchValue);
            }
            else
            {
                cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "MultiplayerMode", (string)searchValue);
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

            MultiplayerMode returnValue = new MultiplayerMode();
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
                        returnValue = Storage.GetCacheValue<MultiplayerMode>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = Storage.GetCacheValue<MultiplayerMode>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
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

        private static async Task<MultiplayerMode> GetObjectFromServer(string WhereClause)
        {
            // get Game_MultiplayerModes metadata
            Communications comms = new Communications();
            var results = await comms.APIComm<MultiplayerMode>(IGDBClient.Endpoints.MultiplayerModes, fieldList, WhereClause);
            var result = results.First();

            return result;
        }
    }
}

