﻿using System;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Companies
    {
        const string fieldList = "fields change_date,change_date_category,changed_company_id,checksum,country,created_at,description,developed,logo,name,parent,published,slug,start_date,start_date_category,updated_at,url,websites;";

        public Companies()
        {
        }

        public static async Task<Company?> GetCompanies(long? Id)
        {
            if ((Id == 0) || (Id == null))
            {
                return null;
            }
            else
            {
                return await _GetCompanies(SearchUsing.id, Id);
            }
        }

        public static async Task<Company> GetCompanies(string Slug)
        {
            return await _GetCompanies(SearchUsing.slug, Slug.ToLower());
        }

        private static async Task<Company> _GetCompanies(SearchUsing searchUsing, object searchValue)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Company", (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Company", (string)searchValue);
            }

            // set up where clause
            string WhereClause = "";
            string WhereClauseField = "";
            switch (searchUsing)
            {
                case SearchUsing.id:
                    WhereClause = "where id = " + searchValue;
                    WhereClauseField = "id";
                    break;
                case SearchUsing.slug:
                    WhereClause = "where slug = \"" + searchValue + "\"";
                    WhereClauseField = "slug";
                    break;
                default:
                    throw new Exception("Invalid search type");
            }

            Company returnValue = new Company();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    if (returnValue != null) { await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue); }
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
                        returnValue = await Storage.GetCacheValueAsync<Company>(returnValue, Storage.TablePrefix.IGDB, WhereClauseField, searchValue);
                    }
                    break;
                case Storage.CacheStatus.Current:
                    return await Storage.GetCacheValueAsync<Company>(returnValue, Storage.TablePrefix.IGDB, WhereClauseField, searchValue);
                    break;
                default:
                    throw new Exception("How did you get here?");
            }

            return returnValue;
        }

        private static async Task UpdateSubClasses(Company company)
        {
            if (company.Logo != null)
            {
                CompanyLogo companyLogo = await CompanyLogos.GetCompanyLogo(company.Logo.Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Company(company));
            }
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<Company> GetObjectFromServer(string WhereClause)
        {
            // get Companies metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<Company>(IGDBClient.Endpoints.Companies, fieldList, WhereClause);
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

