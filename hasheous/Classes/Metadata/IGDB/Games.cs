﻿using System;
using System.Data;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Games
    {
        const string fieldList = "fields age_ratings,aggregated_rating,aggregated_rating_count,alternative_names,artworks,bundles,category,checksum,collection,cover,created_at,dlcs,expanded_games,expansions,external_games,first_release_date,follows,forks,franchise,franchises,game_engines,game_localizations,game_modes,genres,hypes,involved_companies,keywords,language_supports,multiplayer_modes,name,parent_game,platforms,player_perspectives,ports,rating,rating_count,release_dates,remakes,remasters,screenshots,similar_games,slug,standalone_expansions,status,storyline,summary,tags,themes,total_rating,total_rating_count,updated_at,url,version_parent,version_title,videos,websites;";

        public Games()
        {

        }


        public async static Task<Game>? GetGame(long Id, bool getAllMetadata, bool followSubGames, bool forceRefresh)
        {
            if (Id == 0)
            {
                Game returnValue = new Game();
                if (await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Game", 0) == Storage.CacheStatus.NotPresent)
                {
                    returnValue = new Game
                    {
                        Id = 0,
                        Name = "Unknown Title",
                        Slug = "Unknown"
                    };
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);

                    return returnValue;
                }
                else
                {
                    return await Storage.GetCacheValueAsync<Game>(returnValue, Storage.TablePrefix.IGDB, "id", 0);
                }
            }
            else
            {
                return await _GetGame(SearchUsing.id, Id, getAllMetadata, followSubGames, forceRefresh);
            }
        }

        public static async Task<Game> GetGame(string Slug, bool getAllMetadata, bool followSubGames, bool forceRefresh)
        {
            return await _GetGame(SearchUsing.slug, Slug.ToLower(), getAllMetadata, followSubGames, forceRefresh);
        }

        public static Game GetGame(DataRow dataRow)
        {
            return Storage.BuildCacheObject<Game>(new Game(), dataRow);
        }

        private static async Task<Game> _GetGame(SearchUsing searchUsing, object searchValue, bool getAllMetadata = true, bool followSubGames = false, bool forceRefresh = false)
        {
            // check database first
            Storage.CacheStatus? cacheStatus = new Storage.CacheStatus();
            if (searchUsing == SearchUsing.id)
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Game", (long)searchValue);
            }
            else
            {
                cacheStatus = await Storage.GetCacheStatusAsync(Storage.TablePrefix.IGDB, "Game", (string)searchValue);
            }

            if (forceRefresh == true)
            {
                if (cacheStatus == Storage.CacheStatus.Current) { cacheStatus = Storage.CacheStatus.Expired; }
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

            Game returnValue = new Game();
            switch (cacheStatus)
            {
                case Storage.CacheStatus.NotPresent:
                    returnValue = await GetObjectFromServer(WhereClause);
                    await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue);
                    UpdateSubClasses(returnValue, getAllMetadata, followSubGames);
                    return returnValue;
                case Storage.CacheStatus.Expired:
                    try
                    {
                        returnValue = await GetObjectFromServer(WhereClause);
                        await Storage.NewCacheValueAsync(Storage.TablePrefix.IGDB, returnValue, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Metadata: " + returnValue.GetType().Name + ": An error occurred while connecting to IGDB. WhereClause: " + WhereClause + ex.ToString());
                        returnValue = await Storage.GetCacheValueAsync<Game>(returnValue, Storage.TablePrefix.IGDB, WhereClauseField, searchValue);
                    }
                    return returnValue;
                case Storage.CacheStatus.Current:
                    return await Storage.GetCacheValueAsync<Game>(returnValue, Storage.TablePrefix.IGDB, WhereClauseField, searchValue);
                default:
                    throw new Exception("How did you get here?");
            }
        }

        private static async Task UpdateSubClasses(Game Game, bool getAllMetadata, bool followSubGames)
        {
            if (Game.Cover != null)
            {
                Cover GameCover = await Covers.GetCover(Game.Cover.Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Game(Game));
            }

            if (getAllMetadata == true)
            {
                if (Game.AgeRatings != null)
                {
                    foreach (long AgeRatingId in Game.AgeRatings.Ids)
                    {
                        AgeRating GameAgeRating = await AgeRatings.GetAgeRatings(AgeRatingId);
                    }
                }

                if (Game.AlternativeNames != null)
                {
                    foreach (long AlternativeNameId in Game.AlternativeNames.Ids)
                    {
                        AlternativeName GameAlternativeName = await AlternativeNames.GetAlternativeNames(AlternativeNameId);
                    }
                }

                if (Game.Artworks != null)
                {
                    foreach (long ArtworkId in Game.Artworks.Ids)
                    {
                        Artwork GameArtwork = await Artworks.GetArtwork(ArtworkId, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Game(Game));
                    }
                }

                if (followSubGames)
                {
                    List<long> gamesToFetch = new List<long>();
                    if (Game.Bundles != null) { gamesToFetch.AddRange(Game.Bundles.Ids); }
                    if (Game.Dlcs != null) { gamesToFetch.AddRange(Game.Dlcs.Ids); }
                    if (Game.Expansions != null) { gamesToFetch.AddRange(Game.Expansions.Ids); }
                    if (Game.ParentGame != null) { gamesToFetch.Add((long)Game.ParentGame.Id); }
                    //if (Game.SimilarGames != null) { gamesToFetch.AddRange(Game.SimilarGames.Ids); }
                    if (Game.StandaloneExpansions != null) { gamesToFetch.AddRange(Game.StandaloneExpansions.Ids); }
                    if (Game.VersionParent != null) { gamesToFetch.Add((long)Game.VersionParent.Id); }

                    foreach (long gameId in gamesToFetch)
                    {
                        Game relatedGame = await GetGame(gameId, false, true, false);
                    }
                }

                if (Game.Collection != null)
                {
                    Collection GameCollection = await Collections.GetCollections(Game.Collection.Id);
                }

                if (Game.ExternalGames != null)
                {
                    foreach (long ExternalGameId in Game.ExternalGames.Ids)
                    {
                        ExternalGame GameExternalGame = await ExternalGames.GetExternalGames(ExternalGameId);
                    }
                }

                if (Game.Franchise != null)
                {
                    Franchise GameFranchise = await Franchises.GetFranchises(Game.Franchise.Id);
                }

                if (Game.Franchises != null)
                {
                    foreach (long FranchiseId in Game.Franchises.Ids)
                    {
                        Franchise GameFranchise = await Franchises.GetFranchises(FranchiseId);
                    }
                }

                if (Game.Genres != null)
                {
                    foreach (long GenreId in Game.Genres.Ids)
                    {
                        Genre GameGenre = await Genres.GetGenres(GenreId);
                    }
                }

                if (Game.InvolvedCompanies != null)
                {
                    foreach (long involvedCompanyId in Game.InvolvedCompanies.Ids)
                    {
                        InvolvedCompany involvedCompany = await InvolvedCompanies.GetInvolvedCompanies(involvedCompanyId);
                    }
                }

                if (Game.GameModes != null)
                {
                    foreach (long gameModeId in Game.GameModes.Ids)
                    {
                        GameMode gameMode = await GameModes.GetGame_Modes(gameModeId);
                    }
                }

                if (Game.MultiplayerModes != null)
                {
                    foreach (long multiplayerModeId in Game.MultiplayerModes.Ids)
                    {
                        MultiplayerMode multiplayerMode = await MultiplayerModes.GetGame_MultiplayerModes(multiplayerModeId);
                    }
                }

                if (Game.Platforms != null)
                {
                    foreach (long PlatformId in Game.Platforms.Ids)
                    {
                        Platform GamePlatform = await Platforms.GetPlatform(PlatformId);
                    }
                }

                if (Game.PlayerPerspectives != null)
                {
                    foreach (long PerspectiveId in Game.PlayerPerspectives.Ids)
                    {
                        PlayerPerspective GamePlayPerspective = await PlayerPerspectives.GetGame_PlayerPerspectives(PerspectiveId);
                    }
                }

                if (Game.Screenshots != null)
                {
                    foreach (long ScreenshotId in Game.Screenshots.Ids)
                    {
                        Screenshot GameScreenshot = await Screenshots.GetScreenshot(ScreenshotId, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB_Game(Game));
                    }
                }

                if (Game.Themes != null)
                {
                    foreach (long ThemeId in Game.Themes.Ids)
                    {
                        Theme GameTheme = await Themes.GetGame_Themes(ThemeId);
                    }
                }

                if (Game.Videos != null)
                {
                    foreach (long GameVideoId in Game.Videos.Ids)
                    {
                        GameVideo gameVideo = await GamesVideos.GetGame_Videos(GameVideoId);
                    }
                }
            }
        }

        private enum SearchUsing
        {
            id,
            slug
        }

        private static async Task<Game> GetObjectFromServer(string WhereClause)
        {
            // get Game metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<Game>(IGDBClient.Endpoints.Games, fieldList, WhereClause);
            var result = results.First();

            return result;
        }

        public static Game[] SearchForGame(string SearchString, long PlatformId, SearchType searchType)
        {
            Task<Game[]> games = _SearchForGame(SearchString, PlatformId, searchType);
            return games.Result;
        }

        private static async Task<Game[]> _SearchForGame(string SearchString, long PlatformId, SearchType searchType)
        {
            string searchBody = "";
            string searchFields = "fields id,name,slug,platforms,summary; ";
            switch (searchType)
            {
                case SearchType.search:
                    searchBody = "search \"" + SearchString + "\"; ";
                    searchBody += "where platforms = (" + PlatformId + ");";
                    break;
                case SearchType.wherefuzzy:
                    searchBody = "where platforms = (" + PlatformId + ") & name ~ *\"" + SearchString + "\"*;";
                    break;
                case SearchType.where:
                    searchBody = "where platforms = (" + PlatformId + ") & name ~ \"" + SearchString + "\";";
                    break;
            }


            // get Game metadata
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            var results = await comms.APIComm<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);

            return results;
        }

        public enum SearchType
        {
            where = 0,
            wherefuzzy = 1,
            search = 2
        }
    }
}