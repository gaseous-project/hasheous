using System.Data;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using Classes;
using NuGet.Packaging;

namespace TheGamesDB.SQL
{
    public class MetadataQuery
    {
        public T? GetMetadata<T>(QueryModel queryModel)
        {
            // set up variables
            string typeName = typeof(T).Name;

            // setup database objects
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionStringNoDatabase);
            string sql = "";
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            // create validator
            QueryValidator queryValidator = new QueryValidator(queryModel);

            // set blank default field list
            queryValidator.validFieldList = new List<string>();

            // select query based on type
            switch (typeName)
            {
                case "GamesByGameID":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "game_title", "release_date", "hits", "region_id", "country_id"
                    };

                    queryValidator.validFieldList = new List<string>{
                        "players", "publishers", "genres", "overview", "last_updated", "rating", "platform", "coop", "youtube", "os", "processor", "ram", "hdd", "video", "sound", "alternates"
                    };

                    queryValidator.validIncludeList = new List<string>{
                        "boxart", "platform"
                    };

                    // create new instance of GameByGameID
                    HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID gamesByGameID = new HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID();
                    gamesByGameID.include = new HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID.IncludeItem();
                    if (queryValidator.includeItems.Contains("boxart"))
                    {
                        gamesByGameID.include.boxart = new HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID.IncludeItem.BoxartDataItem();
                        gamesByGameID.include.boxart.base_url = imageBaseUrlMeta();
                        gamesByGameID.include.boxart.data = new Dictionary<string, List<HasheousClient.Models.Metadata.TheGamesDb.GameImage>>();
                    }
                    if (queryValidator.includeItems.Contains("platform"))
                    {
                        gamesByGameID.include.platform = new HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID.IncludeItem.PlatformDataItem();
                        gamesByGameID.include.platform.data = new Dictionary<string, HasheousClient.Models.Metadata.TheGamesDb.PlatformSkinny>();
                    }

                    // generate the sql query
                    List<string> fieldList = new List<string>();
                    foreach (string fieldName in queryValidator.fieldItems)
                    {
                        switch (fieldName)
                        {
                            case "developers":
                            case "genres":
                            case "publishers":
                            case "alternates":
                                break;

                            default:
                                fieldList.Add(fieldName);
                                break;
                        }
                    }

                    sql = "SELECT " + string.Join(", ", fieldList) + " FROM thegamesdb.games WHERE " + queryValidator.sqlWhereClause + " ORDER BY game_title ASC";
                    if (queryModel.page > 0)
                    {
                        sql += " LIMIT " + (queryModel.page - 1) * queryModel.pageSize + ", " + queryModel.pageSize;
                    }
                    parameters = queryValidator.sqlWhereClauseValues;

                    // execute the query
                    DataTable dt = db.ExecuteCMD(sql, parameters);

                    // get the games data
                    HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID.DataItem dataItem = new HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID.DataItem();
                    dataItem.count = dt.Rows.Count;
                    dataItem.games = new List<HasheousClient.Models.Metadata.TheGamesDb.Game>();

                    foreach (DataRow row in dt.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Game game = new HasheousClient.Models.Metadata.TheGamesDb.Game();

                        // loop through each field in the fieldList, and populate the gamesByGameID properties with the same name
                        foreach (string field in fieldList)
                        {
                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the gamesByGameID object
                                PropertyInfo property = game.GetType().GetProperty(field);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(game, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(game, row[field].ToString());
                                    }
                                }

                                // fetch the boxart and platform data
                                if (queryValidator.includeItems.Count > 0)
                                {
                                    if (queryValidator.includeItems.Contains("boxart"))
                                    {
                                        // get the boxart
                                        sql = "SELECT `id`, `type`, `side`, `filename`, `resolution` FROM thegamesdb.banners WHERE games_id = @game_id";
                                        parameters = new Dictionary<string, object>
                                        {
                                            { "@game_id", game.id }
                                        };
                                        DataTable dtBoxart = db.ExecuteCMD(sql, parameters);
                                        foreach (DataRow rowBoxart in dtBoxart.Rows)
                                        {
                                            HasheousClient.Models.Metadata.TheGamesDb.GameImage gameImage = new HasheousClient.Models.Metadata.TheGamesDb.GameImage
                                            {
                                                id = int.Parse(rowBoxart["id"].ToString()),
                                                type = rowBoxart["type"].ToString(),
                                                side = rowBoxart["side"].ToString(),
                                                filename = rowBoxart["filename"].ToString(),
                                                resolution = rowBoxart["resolution"].ToString()
                                            };

                                            if (gamesByGameID.include.boxart.data.ContainsKey(game.id.ToString()) == false)
                                            {
                                                gamesByGameID.include.boxart.data.Add(game.id.ToString(), new List<HasheousClient.Models.Metadata.TheGamesDb.GameImage>());
                                            }
                                            gamesByGameID.include.boxart.data[game.id.ToString()].Add(gameImage);
                                        }
                                    }

                                    if (queryValidator.includeItems.Contains("platform"))
                                    {
                                        if (!gamesByGameID.include.platform.data.ContainsKey(game.platform.ToString()))
                                        {
                                            // get the platform
                                            sql = "SELECT `id`, `name`, `alias` FROM thegamesdb.platforms WHERE id = @platform_id";
                                            parameters = new Dictionary<string, object>
                                        {
                                            { "@platform_id", game.platform }
                                        };
                                            DataTable dtPlatform = db.ExecuteCMD(sql, parameters);
                                            if (dtPlatform.Rows.Count > 0)
                                            {
                                                HasheousClient.Models.Metadata.TheGamesDb.PlatformSkinny platformSkinny = new HasheousClient.Models.Metadata.TheGamesDb.PlatformSkinny
                                                {
                                                    id = int.Parse(dtPlatform.Rows[0]["id"].ToString()),
                                                    name = dtPlatform.Rows[0]["name"].ToString(),
                                                    alias = dtPlatform.Rows[0]["alias"].ToString()
                                                };

                                                gamesByGameID.include.platform.data.Add(game.platform.ToString(), platformSkinny);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // fetch the developers, genres, publishers, and alternates
                        if (queryValidator.fieldItems.Contains("developers"))
                        {
                            // get the developers
                            sql = "SELECT dev_id FROM thegamesdb.games_devs WHERE games_id = @game_id";
                            parameters = new Dictionary<string, object>
                            {
                                { "@game_id", game.id }
                            };
                            DataTable dtDevelopers = db.ExecuteCMD(sql, parameters);
                            game.developers = new List<int>();
                            foreach (DataRow rowDeveloper in dtDevelopers.Rows)
                            {
                                game.developers.Add(int.Parse(rowDeveloper["dev_id"].ToString()));
                            }
                        }

                        if (queryValidator.fieldItems.Contains("genres"))
                        {
                            // get the genres
                            sql = "SELECT genres_id FROM thegamesdb.games_genre WHERE games_id = @game_id";
                            parameters = new Dictionary<string, object>
                            {
                                { "@game_id", game.id }
                            };
                            DataTable dtGenres = db.ExecuteCMD(sql, parameters);
                            game.genres = new List<int>();
                            foreach (DataRow rowGenre in dtGenres.Rows)
                            {
                                game.genres.Add(int.Parse(rowGenre["genres_id"].ToString()));
                            }
                        }

                        if (queryValidator.fieldItems.Contains("publishers"))
                        {
                            // get the publishers
                            sql = "SELECT pub_id FROM thegamesdb.games_pubs WHERE games_id = @game_id";
                            parameters = new Dictionary<string, object>
                            {
                                { "@game_id", game.id }
                            };
                            DataTable dtPublishers = db.ExecuteCMD(sql, parameters);
                            game.publishers = new List<int>();
                            foreach (DataRow rowPublisher in dtPublishers.Rows)
                            {
                                game.publishers.Add(int.Parse(rowPublisher["pub_id"].ToString()));
                            }
                        }

                        if (queryValidator.fieldItems.Contains("alternates"))
                        {
                            // get the alternates
                            sql = "SELECT `name` FROM thegamesdb.games_alts WHERE games_id = @game_id";
                            parameters = new Dictionary<string, object>
                            {
                                { "@game_id", game.id }
                            };
                            DataTable dtAlternates = db.ExecuteCMD(sql, parameters);
                            game.alternates = new List<string>();
                            foreach (DataRow rowAlternate in dtAlternates.Rows)
                            {
                                game.alternates.Add(rowAlternate["name"].ToString());
                            }
                        }

                        // add the game to the games list
                        dataItem.games.Add(game);
                    }

                    // populate the gamesByGameID object
                    gamesByGameID.code = 200;
                    gamesByGameID.status = "success";
                    gamesByGameID.remaining_monthly_allowance = 9999;
                    gamesByGameID.extra_allowance = 0;
                    gamesByGameID.data = dataItem;

                    return (T)Convert.ChangeType(gamesByGameID, typeof(T));

                case "GamesImages":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "games_id", "type", "side", "filename", "resolution"
                    };

                    queryValidator.validFilterList = new List<string>{
                        "fanart", "banner", "boxart", "screenshot", "clearlogo", "titlescreen"
                    };

                    // create new instance of GamesImages
                    HasheousClient.Models.Metadata.TheGamesDb.GamesImages gamesImages = new HasheousClient.Models.Metadata.TheGamesDb.GamesImages();
                    gamesImages.data = new HasheousClient.Models.Metadata.TheGamesDb.GamesImages.DataItem();
                    gamesImages.data.images = new Dictionary<string, List<HasheousClient.Models.Metadata.TheGamesDb.GameImage>>();

                    // generate the sql query
                    sql = "SELECT " + String.Join(", ", queryValidator.baseFieldList) + " FROM thegamesdb.banners WHERE " + queryValidator.sqlWhereClause;
                    parameters = queryValidator.sqlWhereClauseValues;

                    // add the filter clause
                    if (queryModel.filter != null && queryModel.filter != "")
                    {
                        string filterWhereClause = "type IN (";
                        for (int i = 0; i < queryValidator.filterItems.Count; i++)
                        {
                            filterWhereClause += "@type" + i;
                            parameters.Add("@type" + i, queryValidator.filterItems[i]);
                            if (i < queryValidator.filterItems.Count - 1)
                            {
                                filterWhereClause += ",";
                            }
                        }
                        filterWhereClause += ")";

                        sql += " AND " + filterWhereClause;
                    }

                    // add the limit clause
                    if (queryModel.page > 0)
                    {
                        sql += " LIMIT " + (queryModel.page - 1) * queryModel.pageSize + ", " + queryModel.pageSize;
                    }

                    // execute the query
                    DataTable dtGamesImages = db.ExecuteCMD(sql, parameters);

                    // get the games data
                    gamesImages.code = 200;
                    gamesImages.status = "success";
                    gamesImages.remaining_monthly_allowance = 9999;
                    gamesImages.extra_allowance = 0;
                    gamesImages.data.count = dtGamesImages.Rows.Count;

                    foreach (DataRow row in dtGamesImages.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.GameImage gameImage = new HasheousClient.Models.Metadata.TheGamesDb.GameImage
                        {
                            id = int.Parse(row["id"].ToString()),
                            type = row["type"].ToString(),
                            side = row["side"].ToString(),
                            filename = row["filename"].ToString(),
                            resolution = row["resolution"].ToString()
                        };

                        if (gamesImages.data.images.ContainsKey(row["games_id"].ToString()) == false)
                        {
                            gamesImages.data.images.Add(row["games_id"].ToString(), new List<HasheousClient.Models.Metadata.TheGamesDb.GameImage>());
                        }
                        gamesImages.data.images[row["games_id"].ToString()].Add(gameImage);
                    }

                    return (T)Convert.ChangeType(gamesImages, typeof(T));

                case "Platforms":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "name", "alias"
                    };

                    queryValidator.validFieldList = new List<string>{
                        "icon", "console", "controller", "developer", "manufacturer", "media", "cpu", "memory", "graphics", "sound", "maxcontrollers", "display", "overview", "youtube"
                    };

                    // create new instance of Platforms
                    HasheousClient.Models.Metadata.TheGamesDb.Platforms platforms = new HasheousClient.Models.Metadata.TheGamesDb.Platforms();
                    platforms.data = new HasheousClient.Models.Metadata.TheGamesDb.Platforms.DataItem();
                    platforms.data.platforms = new Dictionary<string, HasheousClient.Models.Metadata.TheGamesDb.Platform>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.fieldItems) + " FROM thegamesdb.platforms ORDER BY `name` ASC";

                    // execute the query
                    DataTable dtPlatforms = db.ExecuteCMD(sql);

                    // get the platforms data
                    platforms.code = 200;
                    platforms.status = "success";
                    platforms.remaining_monthly_allowance = 9999;
                    platforms.extra_allowance = 0;
                    platforms.data.count = dtPlatforms.Rows.Count;

                    foreach (DataRow row in dtPlatforms.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Platform platform = new HasheousClient.Models.Metadata.TheGamesDb.Platform();

                        // loop through each field in the fieldList, and populate the platform properties with the same name
                        foreach (string field in queryValidator.fieldItems)
                        {
                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the platform object
                                PropertyInfo property = platform.GetType().GetProperty(field);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(platform, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(platform, row[field].ToString());
                                    }
                                }
                            }
                        }

                        // add the platform to the platforms list
                        platforms.data.platforms.Add(platform.id.ToString(), platform);
                    }

                    return (T)Convert.ChangeType(platforms, typeof(T));

                case "PlatformsByPlatformID":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "name", "alias"
                    };

                    queryValidator.validFieldList = new List<string>{
                        "icon", "console", "controller", "developer", "manufacturer", "media", "cpu", "memory", "graphics", "sound", "maxcontrollers", "display", "overview", "youtube"
                    };

                    // create new instance of Platforms
                    HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformID platformsByID = new HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformID();
                    platformsByID.data = new HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformID.DataItem();
                    platformsByID.data.platforms = new Dictionary<string, HasheousClient.Models.Metadata.TheGamesDb.Platform>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.fieldItems) + " FROM thegamesdb.platforms WHERE " + queryValidator.sqlWhereClause;
                    parameters = queryValidator.sqlWhereClauseValues;

                    // execute the query
                    DataTable dtplatformsByID = db.ExecuteCMD(sql, parameters);

                    // get the platforms data
                    platformsByID.code = 200;
                    platformsByID.status = "success";
                    platformsByID.remaining_monthly_allowance = 9999;
                    platformsByID.extra_allowance = 0;
                    platformsByID.data.count = dtplatformsByID.Rows.Count;

                    foreach (DataRow row in dtplatformsByID.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Platform platform = new HasheousClient.Models.Metadata.TheGamesDb.Platform();

                        // loop through each field in the fieldList, and populate the platform properties with the same name
                        foreach (string field in queryValidator.fieldItems)
                        {
                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the platform object
                                PropertyInfo property = platform.GetType().GetProperty(field);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(platform, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(platform, row[field].ToString());
                                    }
                                }
                            }
                        }

                        // add the platform to the platforms list
                        platformsByID.data.platforms.Add(platform.id.ToString(), platform);
                    }

                    return (T)Convert.ChangeType(platformsByID, typeof(T));

                case "PlatformsByPlatformName":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "name", "alias"
                    };

                    queryValidator.validFieldList = new List<string>{
                        "icon", "console", "controller", "developer", "manufacturer", "media", "cpu", "memory", "graphics", "sound", "maxcontrollers", "display", "overview", "youtube"
                    };

                    // create new instance of Platforms
                    HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformName platformsByName = new HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformName();
                    platformsByName.data = new HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformName.DataItem();
                    platformsByName.data.platforms = new List<HasheousClient.Models.Metadata.TheGamesDb.Platform>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.fieldItems) + " FROM thegamesdb.platforms WHERE " + queryValidator.sqlWhereClause;
                    parameters = queryValidator.sqlWhereClauseValues;

                    // execute the query
                    DataTable dtplatformsByName = db.ExecuteCMD(sql, parameters);

                    // get the platforms data
                    platformsByName.code = 200;
                    platformsByName.status = "success";
                    platformsByName.remaining_monthly_allowance = 9999;
                    platformsByName.extra_allowance = 0;
                    platformsByName.data.count = dtplatformsByName.Rows.Count;

                    foreach (DataRow row in dtplatformsByName.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Platform platform = new HasheousClient.Models.Metadata.TheGamesDb.Platform();

                        // loop through each field in the fieldList, and populate the platform properties with the same name
                        foreach (string field in queryValidator.fieldItems)
                        {
                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the platform object
                                PropertyInfo property = platform.GetType().GetProperty(field);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(platform, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(platform, row[field].ToString());
                                    }
                                }
                            }
                        }

                        // add the platform to the platforms list
                        platformsByName.data.platforms.Add(platform);
                    }

                    return (T)Convert.ChangeType(platformsByName, typeof(T));

                case "PlatformsImages":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "platforms_id", "type", "filename"
                    };

                    queryValidator.validFilterList = new List<string>{
                        "fanart", "banner", "boxart"
                    };

                    // create new instance of PlatformsImages
                    HasheousClient.Models.Metadata.TheGamesDb.PlatformsImages platformsImages = new HasheousClient.Models.Metadata.TheGamesDb.PlatformsImages();
                    platformsImages.data = new HasheousClient.Models.Metadata.TheGamesDb.PlatformsImages.DataItem();
                    platformsImages.data.images = new Dictionary<string, List<HasheousClient.Models.Metadata.TheGamesDb.PlatformImage>>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.baseFieldList) + " FROM thegamesdb.platforms_images WHERE " + queryValidator.sqlWhereClause;
                    parameters = queryValidator.sqlWhereClauseValues;

                    // add the filter clause
                    if (queryModel.filter != null && queryModel.filter != "")
                    {
                        string filterWhereClause = "type IN (";
                        for (int i = 0; i < queryValidator.filterItems.Count; i++)
                        {
                            filterWhereClause += "@type" + i;
                            parameters.Add("@type" + i, queryValidator.filterItems[i]);
                            if (i < queryValidator.filterItems.Count - 1)
                            {
                                filterWhereClause += ",";
                            }
                        }
                        filterWhereClause += ")";

                        sql += " AND " + filterWhereClause;
                    }

                    // add the limit clause
                    if (queryModel.page > 0)
                    {
                        sql += " LIMIT " + (queryModel.page - 1) * queryModel.pageSize + ", " + queryModel.pageSize;
                    }

                    // execute the query
                    DataTable dtPlatformsImages = db.ExecuteCMD(sql, parameters);

                    // get the platforms data
                    platformsImages.code = 200;
                    platformsImages.status = "success";
                    platformsImages.remaining_monthly_allowance = 9999;
                    platformsImages.extra_allowance = 0;
                    platformsImages.data.count = dtPlatformsImages.Rows.Count;

                    foreach (DataRow row in dtPlatformsImages.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.PlatformImage platformImage = new HasheousClient.Models.Metadata.TheGamesDb.PlatformImage
                        {
                            id = int.Parse(row["id"].ToString()),
                            type = row["type"].ToString(),
                            filename = row["filename"].ToString()
                        };

                        if (platformsImages.data.images.ContainsKey(row["platforms_id"].ToString()) == false)
                        {
                            platformsImages.data.images.Add(row["platforms_id"].ToString(), new List<HasheousClient.Models.Metadata.TheGamesDb.PlatformImage>());
                        }
                        platformsImages.data.images[row["platforms_id"].ToString()].Add(platformImage);
                    }

                    return (T)Convert.ChangeType(platformsImages, typeof(T));

                case "Genres":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "genre"
                    };

                    queryValidator.validFieldList = new List<string>
                    {

                    };

                    // create new instance of Genres
                    HasheousClient.Models.Metadata.TheGamesDb.Genres genres = new HasheousClient.Models.Metadata.TheGamesDb.Genres();
                    genres.data = new HasheousClient.Models.Metadata.TheGamesDb.Genres.DataItem();
                    genres.data.genres = new Dictionary<string, HasheousClient.Models.Metadata.TheGamesDb.Genre>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.fieldItems) + " FROM thegamesdb.genres ORDER BY `genre` ASC";

                    // execute the query
                    DataTable dtGenresList = db.ExecuteCMD(sql);

                    // get the genres data
                    genres.code = 200;
                    genres.status = "success";
                    genres.remaining_monthly_allowance = 9999;
                    genres.extra_allowance = 0;
                    genres.data.count = dtGenresList.Rows.Count;

                    foreach (DataRow row in dtGenresList.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Genre genre = new HasheousClient.Models.Metadata.TheGamesDb.Genre();

                        // loop through each field in the fieldList, and populate the genre properties with the same name
                        foreach (string field in queryValidator.fieldItems)
                        {
                            string propertyName = field;
                            if (field == "genre")
                            {
                                propertyName = "name";
                            }

                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the genre object
                                PropertyInfo property = genre.GetType().GetProperty(propertyName);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(genre, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(genre, row[field].ToString());
                                    }
                                }
                            }
                        }

                        // add the genre to the genres list
                        genres.data.genres.Add(genre.id.ToString(), genre);
                    }

                    return (T)Convert.ChangeType(genres, typeof(T));

                case "Developers":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "name"
                    };

                    queryValidator.validFieldList = new List<string>
                    {

                    };

                    // create new instance of Developers
                    HasheousClient.Models.Metadata.TheGamesDb.Developers developers = new HasheousClient.Models.Metadata.TheGamesDb.Developers();
                    developers.data = new HasheousClient.Models.Metadata.TheGamesDb.Developers.DataItem();
                    developers.data.developers = new Dictionary<string, HasheousClient.Models.Metadata.TheGamesDb.Developer>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.fieldItems) + " FROM thegamesdb.devs_list ORDER BY `name` ASC";

                    // add the limit clause
                    if (queryModel.page > 0)
                    {
                        sql += " LIMIT " + (queryModel.page - 1) * queryModel.pageSize + ", " + queryModel.pageSize;
                    }

                    // execute the query
                    DataTable dtDevelopersList = db.ExecuteCMD(sql);

                    // get the developers data
                    developers.code = 200;
                    developers.status = "success";
                    developers.remaining_monthly_allowance = 9999;
                    developers.extra_allowance = 0;
                    developers.data.count = dtDevelopersList.Rows.Count;

                    foreach (DataRow row in dtDevelopersList.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Developer developer = new HasheousClient.Models.Metadata.TheGamesDb.Developer();

                        // loop through each field in the fieldList, and populate the developer properties with the same name
                        foreach (string field in queryValidator.fieldItems)
                        {
                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the developer object
                                PropertyInfo property = developer.GetType().GetProperty(field);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(developer, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(developer, row[field].ToString());
                                    }
                                }
                            }
                        }

                        // add the developer to the developers list
                        developers.data.developers.Add(developer.id.ToString(), developer);
                    }

                    return (T)Convert.ChangeType(developers, typeof(T));

                case "Publishers":
                    queryValidator.baseFieldList = new List<string>{
                        "id", "name"
                    };

                    queryValidator.validFieldList = new List<string>
                    {

                    };

                    // create new instance of Publishers
                    HasheousClient.Models.Metadata.TheGamesDb.Publishers publishers = new HasheousClient.Models.Metadata.TheGamesDb.Publishers();
                    publishers.data = new HasheousClient.Models.Metadata.TheGamesDb.Publishers.DataItem();
                    publishers.data.publishers = new Dictionary<string, HasheousClient.Models.Metadata.TheGamesDb.Publisher>();

                    // generate the sql query
                    sql = "SELECT " + string.Join(", ", queryValidator.fieldItems) + " FROM thegamesdb.pubs_list ORDER BY `name` ASC";

                    // add the limit clause
                    if (queryModel.page > 0)
                    {
                        sql += " LIMIT " + (queryModel.page - 1) * queryModel.pageSize + ", " + queryModel.pageSize;
                    }

                    // execute the query
                    DataTable dtPublishersList = db.ExecuteCMD(sql);

                    // get the publishers data
                    publishers.code = 200;
                    publishers.status = "success";
                    publishers.remaining_monthly_allowance = 9999;
                    publishers.extra_allowance = 0;
                    publishers.data.count = dtPublishersList.Rows.Count;

                    foreach (DataRow row in dtPublishersList.Rows)
                    {
                        HasheousClient.Models.Metadata.TheGamesDb.Publisher publisher = new HasheousClient.Models.Metadata.TheGamesDb.Publisher();

                        // loop through each field in the fieldList, and populate the publisher properties with the same name
                        foreach (string field in queryValidator.fieldItems)
                        {
                            if (row[field] != DBNull.Value)
                            {
                                // look for the property in the publisher object
                                PropertyInfo property = publisher.GetType().GetProperty(field);
                                if (property != null)
                                {
                                    // get the property type
                                    Type propertyType = property.PropertyType;

                                    // if property type is int, set the property value
                                    if (propertyType == typeof(int))
                                    {
                                        property.SetValue(publisher, Convert.ToInt32(row[field]));
                                    }
                                    else
                                    {
                                        property.SetValue(publisher, row[field].ToString());
                                    }
                                }
                            }
                        }

                        // add the publisher to the publishers list
                        publishers.data.publishers.Add(publisher.id.ToString(), publisher);
                    }

                    return (T)Convert.ChangeType(publishers, typeof(T));

                default:
                    break;
            }

            return default(T);
        }

        private HasheousClient.Models.Metadata.TheGamesDb.ImageBaseUrlMeta imageBaseUrlMeta()
        {
            Uri boxartBaseUrl = new Uri("https://hasheous.org/api/v1/MetadataProxy/TheGamesDb/Images/");

            HasheousClient.Models.Metadata.TheGamesDb.ImageBaseUrlMeta baseUrls = new HasheousClient.Models.Metadata.TheGamesDb.ImageBaseUrlMeta
            {
                original = new Uri(boxartBaseUrl, imageSize.original.ToString() + "/").ToString(),
                small = new Uri(boxartBaseUrl, imageSize.small.ToString() + "/").ToString(),
                thumb = new Uri(boxartBaseUrl, imageSize.thumb.ToString() + "/").ToString(),
                cropped_center_thumb = new Uri(boxartBaseUrl, imageSize.cropped_center_thumb.ToString() + "/").ToString(),
                medium = new Uri(boxartBaseUrl, imageSize.medium.ToString() + "/").ToString(),
                large = new Uri(boxartBaseUrl, imageSize.large.ToString() + "/").ToString()
            };

            return baseUrls;
        }

        public enum imageSize
        {
            original,
            small,
            thumb,
            cropped_center_thumb,
            medium,
            large
        }
    }
}