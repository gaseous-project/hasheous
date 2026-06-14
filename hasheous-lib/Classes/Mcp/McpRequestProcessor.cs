using System.Text.Json;
using System.Text.Json.Nodes;
using hasheous_server.Models;

namespace Classes.Mcp;

public static class McpRequestProcessor
{
    public sealed record McpToolDescriptor(string Name, string Description);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static readonly IReadOnlyList<McpToolDescriptor> ToolDescriptors = new List<McpToolDescriptor>
    {
        new("hasheous_search_games", "Search signature games by name and optional platform. Returns game records and optional ROM/hash data."),
        new("hasheous_get_game_details", "Get detailed game and ROM/hash information for a specific signature game id."),
        new("hasheous_lookup_hashes", "Find matching games by one or more hashes (crc, md5, sha1, sha256).")
    };

    public static async Task<JsonObject?> ProcessRequestAsync(Database db, JsonObject request)
    {
        JsonNode? id = request["id"];
        string? method = request["method"]?.GetValue<string>();
        JsonObject? parameters = request["params"] as JsonObject;

        if (string.IsNullOrWhiteSpace(method))
        {
            return BuildErrorResponse(id, -32600, "Invalid Request");
        }

        try
        {
            if (method == "notifications/initialized")
            {
                return null;
            }

            if (method == "initialize")
            {
                JsonObject initializeResult = new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject
                        {
                            ["listChanged"] = false
                        }
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "hasheous-mcp",
                        ["version"] = "0.2.0"
                    }
                };

                return BuildSuccessResponse(id, initializeResult);
            }

            if (method == "ping")
            {
                return BuildSuccessResponse(id, new JsonObject());
            }

            if (method == "tools/list")
            {
                JsonObject toolList = new JsonObject
                {
                    ["tools"] = new JsonArray(
                        BuildSearchGamesTool(),
                        BuildGetGameDetailsTool(),
                        BuildLookupHashesTool()
                    )
                };

                return BuildSuccessResponse(id, toolList);
            }

            if (method == "tools/call")
            {
                JsonObject toolCallResult = await HandleToolsCallAsync(db, parameters);
                return BuildSuccessResponse(id, toolCallResult);
            }

            return BuildErrorResponse(id, -32601, $"Method not found: {method}");
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(id, -32000, ex.Message);
        }
    }

    public static JsonObject BuildErrorResponse(JsonNode? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private static async Task<JsonObject> HandleToolsCallAsync(Database db, JsonObject? parameters)
    {
        string? toolName = parameters?["name"]?.GetValue<string>();
        JsonObject arguments = parameters?["arguments"] as JsonObject ?? new JsonObject();

        if (string.IsNullOrWhiteSpace(toolName))
        {
            return BuildToolError("Tool name is required.");
        }

        return toolName switch
        {
            "hasheous_search_games" => await SearchGamesAsync(db, arguments),
            "hasheous_get_game_details" => await GetGameDetailsAsync(db, arguments),
            "hasheous_lookup_hashes" => await LookupHashesAsync(arguments),
            _ => BuildToolError($"Unknown tool: {toolName}")
        };
    }

    private static async Task<JsonObject> SearchGamesAsync(Database db, JsonObject arguments)
    {
        string? name = arguments["name"]?.GetValue<string>()?.Trim();
        string? platform = arguments["platform"]?.GetValue<string>()?.Trim();
        bool includeRoms = arguments["includeRoms"]?.GetValue<bool>() ?? true;
        int limit = Math.Clamp(arguments["limit"]?.GetValue<int>() ?? 10, 1, 25);

        if (string.IsNullOrWhiteSpace(name))
        {
            return BuildToolError("The 'name' argument is required.");
        }

        Dictionary<string, object> dbDict = new Dictionary<string, object>
        {
            { "name", name }
        };

        string sql = @"
SELECT
    Id,
    Name,
    Description,
    Year,
    PublisherId,
    Publisher,
    PlatformId,
    Platform,
    Country,
    Language,
    Category
FROM view_Signatures_Games
WHERE Name LIKE CONCAT('%', @name, '%')";

        if (!string.IsNullOrWhiteSpace(platform))
        {
            sql += " AND Platform LIKE CONCAT('%', @platform, '%')";
            dbDict.Add("platform", platform);
        }

        sql += " ORDER BY CASE WHEN Name = @name THEN 0 WHEN Name LIKE CONCAT(@name, '%') THEN 1 ELSE 2 END, Name ASC";
        sql += $" LIMIT {limit}";

        List<Dictionary<string, object>> gameRows = await db.ExecuteCMDDictAsync(sql, dbDict);
        JsonArray games = BuildGamesArray(gameRows);

        if (includeRoms && gameRows.Count > 0)
        {
            await AttachRomsToGamesAsync(db, games, gameRows, 10);
        }

        JsonObject payload = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["name"] = name,
                ["platform"] = platform,
                ["limit"] = limit,
                ["includeRoms"] = includeRoms
            },
            ["count"] = games.Count,
            ["games"] = games
        };

        return BuildToolSuccess(payload);
    }

    private static async Task<JsonObject> GetGameDetailsAsync(Database db, JsonObject arguments)
    {
        long? gameId = arguments["gameId"]?.GetValue<long>();
        if (gameId == null || gameId <= 0)
        {
            return BuildToolError("The 'gameId' argument is required and must be greater than 0.");
        }

        List<Dictionary<string, object>> gameRows = await db.ExecuteCMDDictAsync(@"
SELECT
    Id,
    Name,
    Description,
    Year,
    PublisherId,
    Publisher,
    PlatformId,
    Platform,
    Country,
    Language,
    Category
FROM view_Signatures_Games
WHERE Id = @gameId
LIMIT 1", new Dictionary<string, object> { { "gameId", gameId.Value } });

        if (gameRows.Count == 0)
        {
            return BuildToolError($"No game found for gameId {gameId.Value}.");
        }

        JsonArray games = BuildGamesArray(gameRows);
        await AttachRomsToGamesAsync(db, games, gameRows, 100);

        JsonObject payload = new JsonObject
        {
            ["gameId"] = gameId.Value,
            ["game"] = games[0]
        };

        return BuildToolSuccess(payload);
    }

    private static async Task<JsonObject> LookupHashesAsync(JsonObject arguments)
    {
        string? md5 = NormalizeHash(arguments["md5"]?.GetValue<string>(), 32);
        string? sha1 = NormalizeHash(arguments["sha1"]?.GetValue<string>(), 40);
        string? sha256 = NormalizeHash(arguments["sha256"]?.GetValue<string>(), 64);
        string? crc = NormalizeHash(arguments["crc"]?.GetValue<string>(), 8);
        int limit = Math.Clamp(arguments["limit"]?.GetValue<int>() ?? 10, 1, 25);

        if (md5 == null && sha1 == null && sha256 == null && crc == null)
        {
            return BuildToolError("At least one hash (crc, md5, sha1, sha256) is required.");
        }

        SignatureManagement signatureManagement = new SignatureManagement();
        List<HashLookupModel> models = new List<HashLookupModel>
        {
            new HashLookupModel
            {
                MD5 = md5,
                SHA1 = sha1,
                SHA256 = sha256,
                CRC = crc
            }
        };

        List<Signatures_Games_2> matches;
        try
        {
            matches = await signatureManagement.GetRawSignatures(models);
        }
        catch (Exception ex)
        {
            return BuildToolError(ex.Message);
        }

        JsonArray games = new JsonArray();
        Dictionary<string, JsonObject> gameMap = new Dictionary<string, JsonObject>();

        foreach (Signatures_Games_2 match in matches)
        {
            if (match.Game == null || match.Rom == null || string.IsNullOrWhiteSpace(match.Game.Id))
            {
                continue;
            }

            if (!gameMap.TryGetValue(match.Game.Id, out JsonObject? gameNode))
            {
                if (gameMap.Count >= limit)
                {
                    continue;
                }

                gameNode = new JsonObject
                {
                    ["id"] = ParseLong(match.Game.Id),
                    ["name"] = match.Game.Name,
                    ["year"] = match.Game.Year,
                    ["publisher"] = match.Game.Publisher,
                    ["platform"] = match.Game.System,
                    ["countries"] = JsonSerializer.SerializeToNode(match.Game.Country, JsonOptions),
                    ["languages"] = JsonSerializer.SerializeToNode(match.Game.Language, JsonOptions),
                    ["score"] = match.Score,
                    ["matchedRoms"] = new JsonArray()
                };

                gameMap.Add(match.Game.Id, gameNode);
                games.Add(gameNode);
            }

            JsonArray matchedRoms = gameNode["matchedRoms"] as JsonArray ?? new JsonArray();
            if (matchedRoms.Count < 10)
            {
                matchedRoms.Add(new JsonObject
                {
                    ["id"] = ParseLong(match.Rom.Id),
                    ["name"] = match.Rom.Name,
                    ["size"] = match.Rom.Size,
                    ["crc"] = match.Rom.Crc,
                    ["md5"] = match.Rom.Md5,
                    ["sha1"] = match.Rom.Sha1,
                    ["sha256"] = match.Rom.Sha256
                });
            }

            gameNode["matchedRoms"] = matchedRoms;
        }

        JsonObject payload = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["crc"] = crc,
                ["md5"] = md5,
                ["sha1"] = sha1,
                ["sha256"] = sha256,
                ["limit"] = limit
            },
            ["count"] = games.Count,
            ["games"] = games
        };

        return BuildToolSuccess(payload);
    }

    private static JsonArray BuildGamesArray(List<Dictionary<string, object>> gameRows)
    {
        JsonArray games = new JsonArray();
        foreach (Dictionary<string, object> row in gameRows)
        {
            games.Add(new JsonObject
            {
                ["id"] = ReadLong(row, "Id"),
                ["name"] = ReadString(row, "Name"),
                ["description"] = ReadString(row, "Description"),
                ["year"] = ReadString(row, "Year"),
                ["publisher"] = new JsonObject
                {
                    ["id"] = ReadLong(row, "PublisherId"),
                    ["name"] = ReadString(row, "Publisher")
                },
                ["platform"] = new JsonObject
                {
                    ["id"] = ReadLong(row, "PlatformId"),
                    ["name"] = ReadString(row, "Platform")
                },
                ["country"] = ReadString(row, "Country"),
                ["language"] = ReadString(row, "Language"),
                ["category"] = ReadString(row, "Category")
            });
        }

        return games;
    }

    private static async Task AttachRomsToGamesAsync(Database db, JsonArray games, List<Dictionary<string, object>> gameRows, int maxPerGame)
    {
        List<long> gameIds = gameRows
            .Select(row => ReadLong(row, "Id"))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (gameIds.Count == 0)
        {
            return;
        }

        Dictionary<string, object> dbDict = new Dictionary<string, object>();
        List<string> gameParameters = new List<string>();

        for (int i = 0; i < gameIds.Count; i++)
        {
            string parameterName = $"gameId{i}";
            gameParameters.Add($"@{parameterName}");
            dbDict.Add(parameterName, gameIds[i]);
        }

        string sql = $@"
SELECT
    Id,
    GameId,
    Name,
    Size,
    CRC,
    MD5,
    SHA1,
    SHA256,
    DevelopmentStatus,
    RomType,
    RomTypeMedia,
    MediaLabel,
    MetadataSource
FROM Signatures_Roms
WHERE Size > 0
  AND GameId IN ({string.Join(", ", gameParameters)})
ORDER BY GameId ASC, Name ASC
LIMIT {Math.Max(100, gameIds.Count * maxPerGame)}";

        List<Dictionary<string, object>> romRows = await db.ExecuteCMDDictAsync(sql, dbDict);

        Dictionary<long, JsonArray> romsByGame = new Dictionary<long, JsonArray>();
        foreach (Dictionary<string, object> romRow in romRows)
        {
            long? gameId = ReadLong(romRow, "GameId");
            if (!gameId.HasValue)
            {
                continue;
            }

            if (!romsByGame.TryGetValue(gameId.Value, out JsonArray? gameRoms))
            {
                gameRoms = new JsonArray();
                romsByGame.Add(gameId.Value, gameRoms);
            }

            if (gameRoms.Count >= maxPerGame)
            {
                continue;
            }

            gameRoms.Add(new JsonObject
            {
                ["id"] = ReadLong(romRow, "Id"),
                ["name"] = ReadString(romRow, "Name"),
                ["size"] = ReadLong(romRow, "Size"),
                ["crc"] = ReadString(romRow, "CRC"),
                ["md5"] = ReadString(romRow, "MD5"),
                ["sha1"] = ReadString(romRow, "SHA1"),
                ["sha256"] = ReadString(romRow, "SHA256"),
                ["developmentStatus"] = ReadString(romRow, "DevelopmentStatus"),
                ["romType"] = ReadString(romRow, "RomType"),
                ["romTypeMedia"] = ReadString(romRow, "RomTypeMedia"),
                ["mediaLabel"] = ReadString(romRow, "MediaLabel"),
                ["metadataSource"] = ReadString(romRow, "MetadataSource")
            });
        }

        foreach (JsonNode? gameNode in games)
        {
            if (gameNode is not JsonObject gameObject)
            {
                continue;
            }

            long? gameId = gameObject["id"]?.GetValue<long>();
            if (!gameId.HasValue || !romsByGame.TryGetValue(gameId.Value, out JsonArray? gameRoms))
            {
                gameObject["roms"] = new JsonArray();
                continue;
            }

            gameObject["roms"] = gameRoms;
        }
    }

    private static JsonObject BuildSearchGamesTool()
    {
        McpToolDescriptor descriptor = ToolDescriptors[0];
        return new JsonObject
        {
            ["name"] = descriptor.Name,
            ["description"] = descriptor.Description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["name"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Game title to search for (partial name supported)."
                    },
                    ["platform"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional platform filter, for example 'Sega Mega Drive'."
                    },
                    ["limit"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum number of matching games to return (1-25)."
                    },
                    ["includeRoms"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Include ROM/hash entries for each returned game."
                    }
                },
                ["required"] = new JsonArray("name")
            }
        };
    }

    private static JsonObject BuildGetGameDetailsTool()
    {
        McpToolDescriptor descriptor = ToolDescriptors[1];
        return new JsonObject
        {
            ["name"] = descriptor.Name,
            ["description"] = descriptor.Description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["gameId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "The signature game id."
                    }
                },
                ["required"] = new JsonArray("gameId")
            }
        };
    }

    private static JsonObject BuildLookupHashesTool()
    {
        McpToolDescriptor descriptor = ToolDescriptors[2];
        return new JsonObject
        {
            ["name"] = descriptor.Name,
            ["description"] = descriptor.Description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["crc"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "CRC32 hash (8 hex chars)."
                    },
                    ["md5"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "MD5 hash (32 hex chars)."
                    },
                    ["sha1"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "SHA1 hash (40 hex chars)."
                    },
                    ["sha256"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "SHA256 hash (64 hex chars)."
                    },
                    ["limit"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum number of matching games to return (1-25)."
                    }
                }
            }
        };
    }

    private static JsonObject BuildSuccessResponse(JsonNode? id, JsonObject result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result
        };
    }

    private static JsonObject BuildToolSuccess(JsonObject payload)
    {
        return new JsonObject
        {
            ["content"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = payload.ToJsonString(PrettyJsonOptions)
                }),
            ["structuredContent"] = payload
        };
    }

    private static JsonObject BuildToolError(string message)
    {
        JsonObject payload = new JsonObject
        {
            ["error"] = message
        };

        return new JsonObject
        {
            ["isError"] = true,
            ["content"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = payload.ToJsonString(PrettyJsonOptions)
                }),
            ["structuredContent"] = payload
        };
    }

    private static string? NormalizeHash(string? value, int expectedLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != expectedLength)
        {
            throw new ArgumentException($"Hash value '{value}' is not {expectedLength} characters long.");
        }

        return normalized;
    }

    private static string? ReadString(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out object? value) || value == null)
        {
            return null;
        }

        string? parsed = value.ToString();
        return string.IsNullOrWhiteSpace(parsed) ? null : parsed;
    }

    private static long? ReadLong(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out object? value) || value == null)
        {
            return null;
        }

        if (value is long typedLong)
        {
            return typedLong;
        }

        return long.TryParse(value.ToString(), out long parsed) ? parsed : null;
    }

    private static long? ParseLong(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, out long parsed) ? parsed : null;
    }
}
