﻿using System;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Franchises
    {
        const string fieldList = "fields checksum,created_at,games,name,slug,updated_at,url;";

        public Franchises()
        {
        }


        public static async Task<Franchise?> GetFranchises(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                return await _GetFranchises(SearchUsing.id, Id);
            }
        }

        public static async Task<Franchise> GetFranchises(string Slug)
        {
            return await _GetFranchises(SearchUsing.slug, Slug);
        }

        private static async Task<Franchise> _GetFranchises(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Franchise", (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Franchise", (string)searchValue);
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

            Franchise returnValue = new Franchise();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
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
                        returnValue = await Storage.GetCacheValueAsync<Franchise>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    returnValue = await Storage.GetCacheValueAsync<Franchise>(returnValue, Storage.TablePrefix.IGDB, "id", (long)searchValue);
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

        private static async Task<Franchise> GetObjectFromServer(string WhereClause)
        {
            // get FranchiseContentDescriptions metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<Franchise>(IGDBClient.Endpoints.Franchies, fieldList, WhereClause);
            var result = results.First();

            return result;
        }
    }
}

