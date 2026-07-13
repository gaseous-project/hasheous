# Copilot instructions for the hasheous codebase

Use this to get productive fast. Follow the existing patterns in this repo over generic .NET advice.

- Repo layout
  - `hasheous/` .NET 10 web API + static UI; public endpoints and Swagger.
  - `service-orchestrator/` .NET 10 API hosting background orchestration and scheduled jobs.
  - `hasheous-lib/` Shared library: data access, config, auth, background tasks, models, migrations (embedded SQL).
  - `hasheous-cli/` CLI utilities.
  - NOTE (resource namespace): Embedded resources (SQL migrations, support data lists) now resolve under the assembly root namespace `hasheous_lib` (e.g. `hasheous_lib.Schema.hasheous-1004.sql`, `hasheous_lib.Support.Country.txt`). Use that prefix when adding new embedded resources; prior references using `hasheous.*` were refactored.

- Architecture & data flow
  - MariaDB/MySQL is the source of truth. On startup, schema is created/migrated via embedded scripts (see `hasheous-lib/Classes/Database.cs::InitDB`, scripts named `hasheous-####.sql`).
  - Signature data is ingested from DAT/XML (TOSEC/No-Intro/etc.) by `Classes/SignatureIngestors/XML.cs` into `Signatures_*` tables.
  - Signature game import in `Classes/SignatureIngestors/XML.cs` now runs with a bounded worker pool (`MaxConcurrentImportWorkers`, currently 4) that repeatedly processes the next game via `ImportDatRecordInternal(...)` until all parsed games are consumed.
  - Signature ingestion now stores game names from `SortingName` and includes a one-time per-parser migration flag (`HasMigratedToSortingName_<ParserType>`) to migrate legacy name records while preserving alternate-name mappings.
  - Signature game records now also persist country/language variants on `Signatures_Games` (`Country`, `Language`), and migration logic updates legacy null-country rows plus links variant rows back to existing `DataObject_SignatureMap` entries.
  - Public API (versioned) handles lookups like `POST /api/v1/Lookup/ByHash` via `Classes/HashLookup` + `Classes/Database`.
  - MCP is hosted on the public `hasheous` web API at `POST /api/v1/Mcp` (controller: `hasheous/Controllers/V1.0/McpController.cs`; shared processor: `hasheous-lib/Classes/Mcp/McpRequestProcessor.cs`).
  - MCP discovery is published at `GET /.well-known/mcp.json` (controller: `hasheous/Controllers/WellKnownController.cs`) and should point clients to the hosted MCP endpoint.
  - Redis (Valkey) provides caching if enabled (`Classes/RedisConnection`), otherwise an in-memory cache is used.
  - Metadata file caching now supports optional S3-compatible object storage fallback (including MinIO) via shared helpers in `hasheous-lib/Classes/S3StorageTools.cs` and `hasheous-lib/Classes/StorageFallbackResolver.cs`.
  - Metadata proxy image and bundle routes use local-disk first, then S3 fallback, and fail open: if S3 is unavailable they fall back to existing provider fetch/build behavior without surfacing S3 errors to clients.
  - `ProxyCacheManager.DownloadAndCacheAsync(...)` now returns a tuple: `(ResolvedContentStream? ContentStream, string? LocalFilePath)`. Use `ContentStream` for responses and `LocalFilePath` when post-download cleanup is needed.
  - When serving cache reads from `ProxyCacheManager` (`ResolvedContentStream`), register the wrapper for response disposal (for example `HttpContext.Response.RegisterForDispose(resolvedStream)`) before returning `File(resolvedStream.Stream, ...)`; disposing only the inner stream can leak S3 response resources/file handles.
  - S3 uploads for newly downloaded/built files are scheduled on response completion (`HttpContext.Response.OnCompleted`) so client download latency is not blocked by object-store upload time.
  - Separation: the orchestration server exists to separate background tasks from the frontend web server. The frontend web server only services user requests; the orchestrator schedules and runs internal/background work (`QueueProcessor.QueueItems` in `service-orchestrator/Program.cs`). Note the orchestrator has no public endpoints, and should only be called by trusted web servers using the inter-host API key.

- Run & debug (local)
  - First run creates `~/.hasheous-server/config.json` (see `hasheous-lib/Classes/Config.cs`). MariaDB 11+ required; Redis optional.
  - App URLs: hasheous http 5220 / https 7157; orchestrator http 5140 / https 7023 (see each `Properties/launchSettings.json`).
  - `service-host` no longer hard-fails when `--reportingserverurl` is omitted; it logs to console-only mode and continues execution.
  - VS Code tasks: build/publish/watch defined at root; common loop is the “watch” task on `hasheous/hasheous.csproj`.
  - Docker: `docker-compose-build.yml` brings up server, orchestrator, MariaDB, and Valkey. Healthcheck: `GET /api/v1/Healthcheck`.

- Configuration
  - File: `~/.hasheous-server/config.json` (auto-updated on startup by `Config.UpdateConfig()`). Env vars (used by Docker): `dbhost`, `dbuser`, `dbpass`, `igdbclientid`, `igdbclientsecret`, `redisenabled`, `redishost`, `redisport`, `reportingserverurl`, etc.
  - S3 config (`Config.S3StorageConfiguration`): `Enabled`, `Region`, `ServiceUrl`, `AccessKey`, `SecretKey`, `SessionToken`, `ForcePathStyle`, `DefaultBucket`.
  - Tiered cache policy config (`Config.CachePolicies` / config field `Policies`) controls proxy cache retention by content type and storage tier:
    - `Media`: local tier defaults to size-only retention; S3 tier defaults to 2-year max age.
    - `Bundles`: local and S3 tiers default to 90-day max age.
    - `MinFreeDiskSpaceBytes` on local tiers triggers eviction even when size is under target if disk free space is low.
  - S3 env vars: `s3enabled`, `s3region`, `s3serviceurl`, `s3accesskey`, `s3secretkey`, `s3sessiontoken`, `s3forcepathstyle`.
  - For MinIO and similar S3-compatible endpoints, use host-only `ServiceUrl` (for example `https://s3.mrgtech.net`) and typically set `ForcePathStyle = true`.
  - S3 fallback is effectively disabled when either `Enabled` is false or bucket/key inputs are missing.
  - Temporary metadata bundle workspace is configured via `Config.LibraryConfiguration.LibraryTemporaryBundlesDirectory` (defaults to `Path.Combine(Path.GetTempPath(), "Bundles")`).
  - Development mode disables client API key requirement by setting `Config.RequireClientAPIKey = false` in `hasheous/StartupExtensions.cs` (`ConfigureDevelopmentModeAsync`) and enables developer exception page.
  - GiantBomb: set `gbapikey` env var (or update config.json) to enable GiantBomb metadata ingestion & proxy; optional `BaseURL` override (defaults to `https://www.giantbomb.com/`).
  - SteamGridDB: set `sgdbapikey` env var (or update config.json -> `SteamGridDBConfiguration.APIKey`) to enable SteamGridDB metadata matching.

- API versioning & routing
  - Framework now uses `Asp.Versioning.Mvc` (via `Asp.Versioning.Mvc.ApiExplorer` 10.x). Project-level `global using Asp.Versioning;` makes attributes like `[ApiVersion("1.0")]` and `[MapToApiVersion("1.0")]` available without per-file imports.
  - Controllers use `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]/")]`.
  - Prefer using cache profiles: `"5Minute"` or `"7Days"` configured in `hasheous/Program.cs` (see `LookupController` usage).
  - Deprecated hash-lookup route note: `HashLookupController` and `POST /api/v1/HashLookup/Lookup` are no longer active. Use `LookupController` endpoints (for example `POST /api/v1/Lookup/ByHash`) for all hash lookup behavior.
  - Data object admin task endpoints are exposed on `DataObjectsController` for moderators/admins:
    - `GET /api/v1/DataObjects/{ObjectType}/{Id}/Tasks` returns all task records for the object.
    - `GET /api/v1/DataObjects/{ObjectType}/{Id}/Tasks/{TaskId}?resetTask=true` returns a single task and optionally resets it.
  - Swagger is enabled with custom schema IDs and API key security definitions.
  - MCP endpoint route: `POST /api/v1/Mcp` (JSON-RPC over HTTP). Keep MCP internet-facing endpoints in `hasheous` (not `service-orchestrator`).
  - Discovery route: `GET /.well-known/mcp.json` for client/server discovery metadata.
  - Metadata proxy file routes (`IGDB/Image`, `TheGamesDB/Images`, `GiantBomb/a/uploads`, `GiantBomb/images`, `ScreenScraper/media{endpoint}.php`, `TheGamesDB/Games/Images`, and `TheGamesDB/Platforms/Images`) may return either physical-file or stream-backed file results depending on cache source; do not assume `PhysicalFileResult` only when consuming these actions internally. These media endpoints do NOT require the `X-Client-API-Key` header and are marked with `[NoClientApiKeyNeeded()]`.
  - Metadata bundle route valid sources are currently `IGDB`, `TheGamesDB`, and `Screenscraper`.
  - ScreenScraper proxy routes are exposed under metadata proxy:
    - `GET /api/v1/MetadataProxy/ScreenScraper/jeuInfos.php` supports `gameid` or hash lookup (`crc`, `md5`, `sha1`) and can return JSON or XML via `output`. Requires `X-Client-API-Key`.
    - `GET /api/v1/MetadataProxy/ScreenScraper/systemesListe.php` returns cached/platform metadata from ScreenScraper integration. Requires `X-Client-API-Key`.
    - `GET /api/v1/MetadataProxy/ScreenScraper/media{endpoint}.php` proxies/caches media and rejects traversal-like media IDs. Does NOT require `X-Client-API-Key`.
  - MCP lookups are intentionally public: the hosted MCP controller uses `[AllowAnonymous]` rather than API key auth.

- Auth & security
  - Identity cookies configured; roles/policies: Admin, Moderator, Member, "Verified Email". Roles are seeded on startup.
  - Role hierarchy: Admin > Moderator > Member. "Verified Email" is a status role (not hierarchical) automatically assigned/removed based on email confirmation status.
  - API keys:
  - User key header `X-API-Key` via `[Authentication.ApiKey.ApiKeyAttribute]` (`ApiKeyAuthorizationFilter` wired in `hasheous/Program.cs`) — identifies individual users and their actions.
  - Client key header `X-Client-API-Key` via `[Authentication.ClientApiKey.ClientApiKeyAttribute]` — identifies client apps (e.g., Gaseous, Romm); typically required when `Config.RequireClientAPIKey` is true.
  - To exempt specific endpoints from client API key requirement, use `[Authentication.ClientApiKey.NoClientApiKeyNeededAttribute]` on the method. This is useful for public media endpoints that should not require authentication.
  - Inter-host API key for orchestrator calls (see `InterHostApiKey*` and registration in `service-orchestrator/Program.cs`) — security mechanism allowing multiple web server frontends (load-balanced) to securely call the orchestrator.

- Data access & migrations
  - Create a `new Classes.Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString)`.
  - Prefer async methods: `ExecuteCMDAsync`/`ExecuteCMDDictAsync`; use `await` in controllers/handlers. Avoid blocking sync calls unless there’s no async alternative.
  - Add new migration scripts in `hasheous-lib/Schema` as `hasheous-####.sql` with the next number.
  - Recent migration note: `hasheous-1034.sql` updates `Signatures_Games.Country`/`Language` to `VARCHAR(100)` and adds country-aware composite indexes for ingestion and game matching.
  - Recent migration note: `hasheous-1035.sql` updates `Signatures_Sources.Url` to nullable `VARCHAR(255)` (from text) so source links are bounded and easier to render safely in the UI.
  - Recent migration note: `hasheous-1036.sql` adds composite indexes on `Signatures_Roms` for (`GameId`, hashes, `IngestorVersion`) to improve multi-hash lookup performance.
  - Embedded migration & support file manifest names now start with `hasheous_lib.Schema.` or `hasheous_lib.Support.`. After adding a file, ensure Build Action = EmbeddedResource and verify with `Assembly.GetExecutingAssembly().GetManifestResourceNames()` if debugging mismatches.

- Caching
  - Use `hasheous.Classes.RedisConnection.GenerateKey(prefix, keyObj)` and `PurgeCache(prefix)`. Redis enabled when `Config.RedisConfiguration.Enabled`.

- Background jobs
  - Add/adjust scheduled tasks in `service-orchestrator/Program.cs` under `QueueProcessor.QueueItems` (e.g., `FetchIGDBMetadata`, timings are minutes).
  - GiantBomb: when `Config.GiantBomb.APIKey` is present a `FetchGiantBombMetadata` job is queued (default 10080 minutes / 7 days) to refresh GiantBomb platform/game/image data.
  - ScreenScraper: `FetchScreenScraperMetadata` is queued every 1440 minutes (24 hours). It reads cached metadata JSON under `Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper/games` and imports records through `XML.XMLIngestor.ImportDatRecord(...)`.
  - Hourly maintenance now runs proxy cache policy maintenance via `ProxyCacheManager.RunMaintenanceAsync()` (tiered LRU/age eviction for local and S3 cache tiers).
  - Queue task refactor: obsolete blocking entries `GetMissingArtwork` and `MetadataMatchSearch` were removed from metadata fetch task `Blocks` lists. Don’t rely on them for future coordination.
  - Data object metadata guard: `DataObjects.DataObjectMetadataSearch(objectType, id?, ForceSearch)` now uses an atomic file lock under `~/.hasheous-server/Data/Metadata/Hasheous/DataObjectFlags` to prevent duplicate concurrent runs for the same `(objectType, id)` key.
  - Metadata search tasks are launched concurrently per metadata source. The per-run `jobId` must be unique, the bounded return guard is controlled by `maxWaitSeconds` (currently 4), and `finalise()` must wait until every launched task has completed before running.
  - Guard behavior details: lock acquisition uses create-new semantics (`FileMode.CreateNew`) and keeps the lock handle open for the full search duration; lock-file collisions cause immediate skip/return.
  - Stale lock policy: existing lock files are treated as valid for up to 1 hour; older lock files are deleted and lock acquisition is retried. For `id == null`, the lock key uses `all` (for example: `Game_all_MetadataSearchInProgress.flag`).

- JSON & serialization
  - System.Text.Json and Newtonsoft are both configured: enums-as-strings, nulls ignored, max depth 64, indented output (Newtonsoft).

- Examples to copy
  - Healthcheck: `hasheous-lib/Controllers/HealthcheckController.cs` → `[AllowAnonymous]`, versioned route, `Ok()`.
  - Lookup: `hasheous/Controllers/V1.0/LookupController.cs` shows async handlers, request models, cache usage, and the timeout pattern.
  - Account management: `hasheous/Controllers/V1.0/AccountController.cs` shows authenticated endpoints with `[Authorize]`, user context access via `_userManager.GetUserAsync(User)`, and role management patterns.
  - Data object task access/reset: `hasheous/Controllers/V1.0/DataObjectController.cs` shows admin/moderator task endpoints and optional task reset flow.
  - Email verification: Account controller demonstrates automatic role assignment (`UpdateVerifiedEmailRole()`) and secure user-specific operations.

- Gotchas
  - Large uploads are allowed (Kestrel/FormOptions set to max); ensure proxies forward `X-Forwarded-*` (already configured).
  - Keep XML docs for Swagger (`IncludeXmlComments` expects generated XML files from the projects); build output paths are `bin/Debug/net10.0/` (not net8.0).
  - Swagger filter custom implementations: framework now uses Swashbuckle 10.x, which brings `Microsoft.OpenApi` 2.x. Schema types are `JsonSchemaType` flags (e.g., `JsonSchemaType.String | JsonSchemaType.Null` for nullable strings), examples use `JsonNode` instead of `OpenApiObject`/`OpenApiString`, and tag/security references use `OpenApiTagReference`/`OpenApiSecuritySchemeReference` instead of embedding objects directly. See `hasheous-lib/Classes/SwaggerLookupRequestBodyFilter.cs` and `SwaggerIDocumentFilter.cs` for reference implementations.
  - Swagger auth requirements now account for both direct attributes and service filters (`[ServiceFilter(...AuthorizationFilter)]`) when determining required API key schemes. If you add security filters via service filters, ensure `AuthorizationOperationFilter` continues to recognize them.

If something is unclear or missing (e.g., additional services, tests, or new auth flows), ask and this guide can be refined.

## Quick how‑tos
- Add a new API endpoint
  - Create a controller under `hasheous/Controllers/V1.0` with `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]/")]`.
  - API version attributes (`[ApiVersion]`, `[MapToApiVersion]`) are available globally via project-level `global using Asp.Versioning;` — no per-file using directive needed.
  - Reuse cache profiles via `[ResponseCache(CacheProfileName = "5Minute")]` or `"7Days"`.
  - Protect with `[Authentication.ApiKey.ApiKeyAttribute]` or `[Authentication.ClientApiKey.ClientApiKeyAttribute]` when needed.
  - If an endpoint protected with `[Authentication.ClientApiKey.ClientApiKeyAttribute]` should not require the key (e.g., public media endpoints), add `[Authentication.ClientApiKey.NoClientApiKeyNeededAttribute]` to exempt it.
  - For authenticated endpoints, use `[Authorize]` and access user context via `_userManager.GetUserAsync(User)`.
  - Use `new Classes.Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString)` and `ExecuteCMD/ExecuteCMDDict` for DB work.
  - If caching, build keys with `hasheous.Classes.RedisConnection.GenerateKey(prefix, keyObj)`.

- Add or update MCP tools
  - Keep transport-specific protocol handling thin (`McpController` for HTTP, `hasheous-cli` for stdio).
  - Implement tool behavior in shared processor code: `hasheous-lib/Classes/Mcp/McpRequestProcessor.cs`.
  - For hosted MCP, keep the endpoint public with `[AllowAnonymous]` and route under `api/v{version:apiVersion}/Mcp`.
  - Keep the discovery manifest in `WellKnownController` aligned with actual MCP endpoint URL, auth mode, and tool metadata.
  - Update `README-MCP.MD` and `README.MD` MCP link when adding/changing methods/tools.

- Add a background job
  - Edit `service-orchestrator/Program.cs` and add a `QueueProcessor.QueueItem(QueueItemType.YourItem, <minutes>, false)` to `QueueProcessor.QueueItems`.
  - Implement the corresponding handler under `hasheous-lib/Classes/ProcessQueue/` (follow existing `QueueItemType` patterns).
  - Wire the queue item into both dispatchers: `hasheous-lib/Classes/ProcessQueue/ProcessQueue.cs` and `service-host/Program.cs` so orchestrator and one-off service-host execution can resolve the task.

- Add a DB migration
  - Create `hasheous-lib/Schema/hasheous-####.sql` using the next available number; `InitDB()` applies scripts in order and bumps `schema_version`.
  - When referencing a migration or support text file at runtime, construct resource names with `hasheous_lib.Schema.` / `hasheous_lib.Support.` prefixes.

- Use the lookup timeout pattern
  - See `LookupController.LookupPost`: `Task.WhenAny(..., Task.Delay(TimeSpan.FromSeconds(10)))` and set `Retry-After = 90` on 503 responses.
  - Interactive hash lookup metadata searches use a short `Task.Delay(TimeSpan.FromSeconds(2))` guard to keep UI-driven requests responsive.
  - That guard must not serialize provider work: launch metadata tasks concurrently, return promptly if the guard expires, and let the outstanding tasks finish before finalising the run.

- Raw-body POST endpoints and Swagger documentation
  - `LookupPost` (`POST /api/v1/Lookup/ByHash`) accepts a raw JSON body instead of a bound model parameter. The action reads `Request.Body` directly via `StreamReader` and calls `JsonDocument.Parse(...)` manually.
  - This avoids ASP.NET Core model binding requiring a named form/query field; the body can be a JSON object `{"crc":"..."}` or a JSON array `[{"crc":"..."},{"md5":"..."}]`. A quoted JSON string containing an object/array is also accepted.
  - `LookupController` validates request payloads manually and rejects explicit zero-byte hashes with `400 Bad Request` before the database lookup runs.
  - Because there is no bindable parameter, Swagger cannot infer the request body automatically. Use an `IOperationFilter` to inject the `OpenApiRequestBody` manually for such actions. The filter `LookupRequestBodyOperationFilter` in `hasheous-lib/Classes/SwaggerLookupRequestBodyFilter.cs` is the reference implementation; register it with `options.OperationFilter<LookupRequestBodyOperationFilter>()` in `StartupExtensions.cs`.
  - When adding other raw-body endpoints, follow this same pattern: read body manually in the action and add a dedicated `IOperationFilter` for Swagger documentation.

## Signature lookup behavior
- `SignatureManagement.GetRawSignatures(...)` excludes zero-size ROM signature rows by default (`Signatures_Roms.Size > 0`) before hash-condition matching.
- `LookupController` now rejects explicit zero-byte hashes early for both raw-body `POST /api/v1/Lookup/ByHash` and direct `GET /api/v1/Lookup/ByHash/{hash}` routes, returning `400 Bad Request` rather than querying the signature database.
- Keep the non-zero-size filter when extending hash lookup SQL unless a feature explicitly requires zero-byte signatures.
- `SignatureManagement.BuildGameItem(...)` currently populates both singular and plural dictionary properties for compatibility: `Country` and `Countries`, `Language` and `Languages`.
- Data object signature listing for games includes `Signatures_Games.Country`; frontend game signature labels/suggestions append country text in `wwwroot/pages/dataobjectdetail.js` and `wwwroot/pages/dataobjectedit.js`.

### Multi-hash lookup (new behavior)
- `GetRawSignatures(...)` accepts an array (`List<HashLookupModel>`) where each element can include one or more hash fields (CRC/MD5/SHA1/SHA256).
- Within each `HashLookupModel`, hash fields are evaluated as an `OR` group (any valid hash in that element can satisfy the element).
- Across the array, each element is enforced as an `AND` requirement against the same game id using per-model `EXISTS` clauses bound to `view_Signatures_Games.Id`.
- Practical effect: different array elements can match different ROM rows, but all elements must resolve under one `GameId` to be returned.
- `modelCount` is used for uniquely-named SQL parameters and subquery aliases (for example `@sha256{modelCount}`, `sr_model{modelCount}`) to avoid collisions across models.

- Correlation & logging
  - Middleware sets `CallContext` values (CorrelationId, CallingProcess, CallingUser); orchestrator also returns `x-correlation-id` header.

### Object property hashing (new utility)
- Helper: `ComputeObjectPropertyHash(object obj, string? algorithm = "SHA256", string[]? ignoredProperties = null)` (added in shared library).
- Purpose: lightweight deterministic fingerprint of an object's public instance property values (ordered alphabetically) for change detection / cache keys.
- Behavior:
  - Null object => empty string.
  - Properties read via reflection; enumerable (non-string) values flattened as `[item1,item2,...]` using `ToString()` per element.
  - Null property values serialized as `<null>`.
  - Optionally ignore specific property names (case-insensitive) to exclude volatile fields (timestamps, etc.).
  - Not a deep graph cryptographic signature; avoid for security decisions.
- Usage examples:
  - Cache key: `var key = RedisConnection.GenerateKey("DataObjectFingerprint", ComputeObjectPropertyHash(model, ignoredProperties: new[]{"UpdatedDate"}));`
  - Change detection: persist last hash and compare before writing/invalidating heavy derived data.
- Guidelines:
  - Keep ignored lists small; if many fields are excluded consider crafting a purpose-built DTO instead.
  - For large collections, ensure ordering is deterministic before hashing (sort if source order can vary).

### Strong name matching (new utility)
- Helpers: `Common.GetStrongNameMatchScore(string candidate, string? resultName)` and `Common.IsStrongNameMatch(string candidate, string? resultName)`.
- Purpose: conservative automatic name matching for metadata search results, with typo tolerance and token awareness.
- Behavior:
  - Tokenizes both sides and adds numeric shorthand variants (for example `c64` -> `64`).
  - Pulls parenthetical aliases from result names (for example `Commodore 65 (C64)` also matches alias tokens).
  - Requires every candidate token to map to a result token with exact or near-exact matching.
  - Uses Levenshtein-based token scoring and penalizes unmatched extra result tokens/large length differences.
  - Returns high score for exact matches; automatic acceptance threshold currently uses `>= 8` in SteamGridDB matching.

- Email verification system
  - "Verified Email" role is automatically assigned/removed based on `user.EmailConfirmed` status.
  - Use `UpdateVerifiedEmailRole(user)` pattern after email confirmation changes.
  - `ProfileBasicViewModel.EmailConfirmed` provides frontend email status.
  - Frontend: use `postData()` for authenticated API calls (handles CSRF tokens and cookies).

## Maintenance
- A PR guard (`.github/workflows/copilot-instructions-guard.yml`) fails when architecture/config files change without updating this file; it prints hints via `.github/scripts/copilot-instructions-help.sh`.
  - Update this file when: resource namespace conventions change (e.g., `hasheous_lib.*` migration), new cross-cutting utilities like `ComputeObjectPropertyHash` are added, queue coordination semantics are modified, MCP routing/tooling/auth changes, or major framework/dependency updates occur (e.g., .NET version bumps, Swagger/OpenAPI package upgrades).

## API key usage examples
- User API key (header `X-API-Key`):
  - curl example:
    - GET lookup: `curl -H "X-API-Key: <USER_KEY>" "https://localhost:7157/api/v1/Lookup/ByHash/md5/<md5>"`
    - POST lookup: `curl -H "X-API-Key: <USER_KEY>" -H "Content-Type: application/json" -d '{"MD5":"...","SHA1":"..."}' "https://localhost:7157/api/v1/Lookup/ByHash"`
- Client API key (header `X-Client-API-Key`): include the header on client-protected endpoints when `Config.RequireClientAPIKey` is true.
- Authenticated endpoints (Account management): use cookie-based authentication with CSRF protection via `postData()` function in frontend.

## Ingestion paths (local)
- Place DAT/XML files under `~/.hasheous-server/Data/Signatures/`:
  - TOSEC: `TOSEC/`
  - No-Intro: `NoIntro/DAT/` and `NoIntro/DB/`
  - MAME Arcade: `MAME Arcade/`
  - MAME Mess: `MAME MESS/`
  - Redump: `Redump/`

## Redump metadata
- Downloader class: `hasheous-lib/Classes/Metadata/Redump/MetadataDownload.cs`.
- Redump host migration: use `https://redump.info` as the base host (`BaseUrl`) and `https://redump.info/downloads/` for platform listing.
- HTML parsing expectation: platform rows are parsed from the first `div.downloads-table-scaler` table; links are selected by anchor label (`DAT + Serial/Version` and `Cuesheets`) instead of fixed table-column positions.
- URL handling: relative Redump links should be expanded with `BaseUrl`; avoid hardcoded `http://redump.org` URLs or appending legacy query suffixes manually.

## LaunchBox metadata
- Background task: `QueueItemType.FetchLaunchBoxMetadata` (implemented under `hasheous-lib/Classes/ProcessQueue/Tasks/FetchLaunchBoxMetadata.cs`).
- Downloader/importer entrypoint: `hasheous-lib/Classes/Metadata/LaunchBox/MetadataDownload.cs` (`DownloadManager`).
- Import helper: `hasheous-lib/Classes/Metadata/LaunchBox/XmlModelImporter.cs`.
  - Supports streaming XML import via `ImportXmlMultipleModelsAsync(...)` to avoid loading large files fully into memory.
  - Generates SQL tables from model scalar properties.
  - Supports identity handling (`Id` auto-increment when configured as identity and not included in inserts).
- Model locations: `hasheous-lib/Classes/Metadata/LaunchBox/Models/*.cs`.
- AI description enrichment:
  - Game description/tag jobs can now pull LaunchBox `Game.Overview` when metadata source is `LaunchBox`.
  - Platform description/tag jobs can now pull LaunchBox `Platform.Notes` by parsing slug text from immutable ids (`<id>-<slug>`).
  - Source payload key is `Source_LaunchBox`; include this when extending AI task source aggregation logic in `hasheous-lib/Classes/TaskManagement.cs`.

### LaunchBox ForeignKey conventions
- Attribute: `hasheous-lib/Classes/Metadata/ForeignKeyAttribute.cs`.
- Untyped FK usage (lookup table auto-created):
  - `[ForeignKey("Company")]`
  - Creates/uses a simple `{Id BIGINT AUTO_INCREMENT, Name VARCHAR(255) UNIQUE}` table.
- Typed FK usage (references an existing imported table):
  - Use the explicit constructor with lookup/id columns:
    - `[ForeignKey("Game", typeof(GameModel), "Name", "Id")]`
  - For typed FKs, lookup/id columns are required and validated against the referenced model type.
- FK-tagged source properties are expected to be `string` values (lookup keys from XML).
- FK-tagged destination columns are persisted as `BIGINT` IDs.
- Blank/whitespace FK values are normalized to `NULL` during insert.

### LaunchBox model-defined indexes
- Index definitions are declared on models via static `GetIndexes()` returning `IEnumerable<ModelIndexDefinition>`.
  - Definition type: `hasheous-lib/Classes/Metadata/ModelIndexDefinition.cs`.
  - Helpers support both single and composite indexes:
    - `ModelIndexDefinition.Single(...)`
    - `ModelIndexDefinition.Composite(...)`
- Indexes are created by `XmlModelImporter.EnsureIndexesAsync(...)` after table creation.
- Guardrails:
  - Index names and columns are sanitized.
  - Referenced columns must exist on the model/table.
  - Existing indexes are detected via `information_schema.statistics` and skipped.
- MySQL key length safety:
  - For `string` columns (stored as `LONGTEXT`), index creation uses prefix length `(191)` automatically (for example ``Name(191)``) to avoid `Specified key was too long` errors on MariaDB/MySQL.

## ScreenScraper metadata
- Background task: `QueueItemType.FetchScreenScraperMetadata`.
- Local cache source: `~/.hasheous-server/Data/Metadata/ScreenScraper/games` (resolved from `Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper`).
- Import path: cached JSON files are parsed with `gaseous_signature_parser` (ScreenScraper parser mode), language/country short codes are normalized via `Common.GetNameByCode(...)`, then records are imported using `XML.XMLIngestor.ImportDatRecord(...)`.
- Blocking behavior: this task blocks `QueueItemType.SignatureIngestor` while it runs.
- Metadata proxy parity endpoints now include:
  - `jeuInfos.php` response shaping (matching ScreenScraper style) with support for `gameid` and hash-based fallback lookup (`crc`/`md5`/`sha1`).
  - `systemesListe.php` for platform listing.
  - `media{endpoint}.php` for media passthrough with cache-first behavior via `ProxyCacheManager`.
- Cache miss sentinel handling: when ScreenScraper media download succeeds but the cached payload is tiny (`ContentLength <= 7`), treat it as a provider "not found" marker, dispose stream, delete the cached local file, and return `404`.
- Bundle support now includes ScreenScraper via `GET /api/v1/MetadataProxy/Bundles/Screenscraper/{GameID}.bundle`, with game metadata plus media files sourced from ScreenScraper payloads.
- Sensitive ScreenScraper query credentials (`devid`, `devpassword`, `ssid`, `sspassword`, `softname`) are stripped from rewritten media URLs before responses are returned.

## SteamGridDB metadata
- Provider class: `hasheous-lib/Classes/Metadata/SteamGridDB/IMetadata_SteamGridDB.cs` (`MetadataSteamGridDB : IMetadata`).
- Source enum: `MetadataSources.SteamGridDb` is now part of the metadata search source set.
- Enablement: provider is enabled only when `Config.SteamGridDBConfiguration.APIKey` is populated (from `sgdbapikey` or config file).
- Matching flow:
  - Prepends the data object name to search candidates.
  - If SteamGridDB returns exactly one game, it is accepted as an automatic match.
  - If multiple games are returned, all results are scored with `Common.GetStrongNameMatchScore(...)`; best score wins, with shorter display name as tie-breaker.
  - Automatic match is accepted only when best score is `>= 8`; otherwise falls back to no match.

## Common cache prefixes
- `HashLookup`: entity details resolved during lookups (publisher/platform/game).
- `Signature`: signature-related lookups (`SignatureManagement`).
- `InsightsReport`: insights/report queries and cache warmer.
- `DataObject`: invalidated on data object mutations.
- `ApiKeys` / `ClientApiKeys`: in-memory/Redis API key caches.
 - GiantBomb queries currently read directly from the `giantbomb` schema; cached files & images stored under `~/.hasheous-server/Data/Metadata/GiantBomb` (images in `Images/`).

## Dump files (MetadataMap.zip)
- Producer: `hasheous-lib/Classes/ProcessQueue/Tasks/Dumps.cs` (queued as `QueueItemType.MetadataMapDump` in `service-orchestrator/Program.cs`, default every 10080 minutes = 7 days).
- Output: a zip at `Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory/MetadataMap.zip`.
- Temporary working directories used while composing bundles are created under `Config.LibraryConfiguration.LibraryTemporaryBundlesDirectory/<guid>`.
- Inside the zip: `Content/<PlatformName>/<GameName> (<Id>).json` per game.
  - PlatformName comes from the game’s `Attributes` where `attributeName == Platform`; falls back to `Unknown Platform` if missing.
  - File names are sanitized and include the DataObject Id.
- Serialization: Newtonsoft.Json with
  - Indented formatting
  - Nulls ignored
  - Enums serialized as strings (StringEnumConverter)
- JSON shape (per-file): serialized `DataObjectItem` for a Game:
  - `Id` (long), `Name` (string), `ObjectType` ("Game")
  - `Attributes`: list of Attribute items: `attributeName` (e.g., `Platform`, `Year`, `Publisher`, etc.), `attributeType`, `attributeRelationType`, `Value`
    - `Value` may be a primitive or a nested `DataObjectItem` for `ObjectRelationship` attributes (e.g., Platform)
  - `Metadata`: list with items: `Id`, `ImmutableId?`, `Status` (NotMapped/Mapped/MappedWithErrors), `MatchMethod?`, `Source` (IGDB/TheGamesDb/... as string), `Link` (computed URL), `LastSearch`, `NextSearch`, `WinningVoteCount`, `TotalVoteCount`, `WinningVotePercent`
  - `SignatureDataObjects`: list of dictionaries containing signature entries (when present)
  - `CreatedDate`, `UpdatedDate`
  - `Permissions` and `UserPermissions` (if included by the API)
- Lifecycle: JSON files are written under `.../Content/...`, then zipped into `MetadataMap.zip`; the `Content` directory is deleted after zipping. The zip remains as the artifact.

## GiantBomb metadata
- Enable by supplying `gbapikey` env var (or editing config file -> `GiantBombConfiguration.APIKey`). Without a key GiantBomb tasks/endpoints remain inert.
- Local storage: `~/.hasheous-server/Data/Metadata/GiantBomb` plus `Images/` subfolder for retrieved images.
- DB schema: data persisted to `giantbomb` schema tables (platforms, games, images, relations) and queried via `GiantBomb.MetadataQuery` (reflection-based field selection & relationship expansion).
- Background job: `FetchGiantBombMetadata` (platforms → games → sub‑types/images) scheduled every 10080 minutes (7 days) once API key present. Incremental logic now also uses a global `GiantBomb_LastUpdate` tracking key to fetch only objects whose `date_last_updated` falls within `[LastUpdate, UpdateEndDate]` (UpdateEndDate fixed at `3000-01-01`). Legacy platform/game offset & per-entity tracking keys (`GiantBomb_LastPlatformFetch`, `GiantBomb_GameOffset-*`) still apply where referenced.
- Rate limiting: adaptive soft/hard hourly thresholds with backoff & retry (see `MetadataDownload.cs`).
- Proxy endpoints (require `X-Client-API-Key` when client keys enforced):
  - Singular: `GET /api/v1/MetadataProxy/GiantBomb/{datatype}/{guid}?field_list=*` (datatype: company|game|platform|image|rating|rating_board|release|user_review)
  - Collections: `GET /api/v1/MetadataProxy/GiantBomb/{datatype}s?filter=...&sort=...&limit=100&offset=0&field_list=*` (plural adds trailing `s`; note `rating_board` → `rating_boards`).
  - Companies plural convenience: `GET /api/v1/MetadataProxy/GiantBomb/companies`
  - Image passthrough (lazy fetch & local cache): `GET /api/v1/MetadataProxy/GiantBomb/a/uploads/<path>` (sanitized; refuses path traversal).
- Response formats: `json` (default), `xml`, `jsonp` via `format` query parameter.
- Adding new GiantBomb data type: extend `GiantBomb.MetadataQuery.QueryableTypes` and `GiantBombSourceMap` (table name, class type, ID column), ensure ingestion populates table, then proxy automatically supports it. Recent example: added `rating_board` mapped to `RatingBoards` table and referenced via `Rating.rating_board` (now a nested object instead of a string).
- Incremental date filtering: Platform & Game, plus user reviews (`user_reviews`) and ratings (`game_ratings`, `rating_boards`) requests append `filter=date_last_updated:{LastUpdate}|{UpdateEndDate}`. After a successful full cycle the job sets `GiantBomb_LastUpdate` (UTC "yyyy-MM-dd HH:mm:ss"). Delete or backdate that key (in Settings) to force a wider refresh.
- Image handling: image tag processing now occurs inline during platform ingestion (`ProcessImageTags`) and only refreshes if `GiantBomb_LastImageFetch-{guid}` is older than the configured `TimeToExpire` days (default 30). Deletion before insert is narrowed to the specific `(guid, original_url)` tuple instead of wholesale per-guid deletion.
- Tracking helpers: `MetadataDownload.GetTracking` / `SetTracking` are now public static — reuse for new GiantBomb incremental tracking keys rather than duplicating logic.

### GiantBomb quick curl example
`curl -H "X-Client-API-Key: <CLIENT_KEY>" "https://localhost:7157/api/v1/MetadataProxy/GiantBomb/games?filter=name:like:Mario&limit=5"`

Additional example (rating boards):
`curl -H "X-Client-API-Key: <CLIENT_KEY>" "https://localhost:7157/api/v1/MetadataProxy/GiantBomb/rating_boards?limit=10"`

## Contributing with AI (.NET 10 specifics)
- When updating Swagger operation filters or custom documentation: use `JsonSchemaType` flags for schema types (e.g., `JsonSchemaType.String`), `JsonNode.Parse("...")` for examples, and dedicated reference types (`OpenApiSecuritySchemeReference(name, hostDoc, externalResource)`, `OpenApiTagReference(name, hostDoc, externalResource)`) instead of embedding full objects.
- Ensure new Swagger helpers use `using Microsoft.OpenApi;` (not `Microsoft.OpenApi.Models` or `Microsoft.OpenApi.Any`, which no longer exist in v2.x).

## Contributing with AI
- Keep PRs small and single-purpose. Describe intent, touched areas, and any migration/caching impact.
- Follow structure:
  - Web API endpoints → `hasheous/Controllers/V1.0`, versioned route attributes, reuse cache profiles.
  - Background jobs → add `QueueProcessor.QueueItem(...)` in `service-orchestrator/Program.cs` and implement under `hasheous-lib/Classes/ProcessQueue/`.
  - Data access → use `Classes.Database` with `ExecuteCMD/ExecuteCMDDict`; avoid raw ADO elsewhere.
  - Migrations → add new `hasheous-lib/Schema/hasheous-####.sql`; don’t modify prior scripts.
- Auth and headers: apply `[Authentication.ApiKey.ApiKeyAttribute]` or `[Authentication.ClientApiKey.ClientApiKeyAttribute]` as needed; document required headers in XML comments.
- Caching: generate keys with `RedisConnection.GenerateKey(prefix, keyObj)`; call `PurgeCache(prefix)` on mutations; list affected prefixes in the PR.
- Config and secrets: never hardcode secrets; use env vars or `~/.hasheous-server/config.json` fields updated via `Config`.
- Swagger/docs: keep XML summaries up to date; include response types, cache profile notes, and examples (see `LookupController`).
- Build/run checks: ensure the solution builds and the API boots locally; prefer the VS Code `watch` task for quick verification.
- Update this guide when changing routing, auth, migrations, orchestrator queue, docker, or README. The PR guard will fail otherwise and prints hints.
- UI/static: for changes under `wwwroot/`, include before/after screenshots if relevant.
  - Localization: move all user-visible text to `wwwroot/localisation/en.json` and use `data-lang` attributes or `lang.getLang()` calls.
  - CSS: use CSS variables (`--warning-color`, `--valid-color`, `--invalid-color`) and semantic class names instead of inline styles.
  - Frontend API: use `postData()` function for authenticated requests instead of direct `fetch()` - handles CSRF tokens and authentication cookies automatically.
  - Sources page behavior: `wwwroot/pages/sources.js` should only render the homepage block when `<source>homepage` has a non-empty localization value; this supports sources such as `generic` that intentionally have no homepage URL.
  - Source color mapping: add/update source badge colors in `wwwroot/styles/datasourcecolours.css` via `--signature-source-color-<source>` and matching `.color-<source>` rules (for example, `generic`).
  - Long URL readability: keep wrapping enabled for source/homepage and table link cells (`.source-homepage`, `.tablecell[media-selector="cell_link"]`) so long links do not overflow cards or grids.
  - Data object details admin UI now includes a dedicated tasks panel (`#dataObjectTasksSection`) in `wwwroot/pages/dataobjectdetail.html` rendered by `loadTasksSection()` in `wwwroot/pages/dataobjectdetail.js`; keep role-gating aligned with backend authorization (Admin/Moderator).

## AI prompt templates
- Prompt templates for AI descriptions/tags live in `hasheous-lib/Support/AIGameDescriptionPrompt.txt`, `hasheous-lib/Support/AIGameTagPrompt.txt`, `hasheous-lib/Support/AIPlatformDescriptionPrompt.txt`, and `hasheous-lib/Support/AIPlatformTagPrompt.txt`.
- Current prompt guidance no longer gives Wikipedia priority context instructions.
- Description prompts now explicitly require plain description output with no added title headings.

## .NET 10 and dependencies
- Framework: .NET 10.0. Target framework set in `Directory.Build.props` and applied to all projects.
- API versioning: `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` 10.x (replacing old `Microsoft.AspNetCore.Mvc.Versioning` packages).
- Swagger: Swashbuckle 10.2.3 brings `Microsoft.OpenApi` 2.x. Schema types are `JsonSchemaType` enums, examples are `JsonNode`, references are dedicated types (`OpenApiTagReference`, `OpenApiSecuritySchemeReference`). See Swagger filter implementations for patterns.
- Build warnings: XML documentation warnings (`CS1572`, `CS1573`, `CS1587`, `CS1591`) are suppressed project-wide in `Directory.Build.props` to reduce noise; focus remains on nullable (`CS8600+`) and logic warnings.

## async guidance
- Prefer Task-returning actions: `public async Task<IActionResult> Action(...)`.
- Await DB calls (`ExecuteCMDAsync`/`ExecuteCMDDictAsync`) and long-running operations.
- Use `Task.WhenAny` for bounded latency where appropriate (see `LookupController`).
- Avoid `.Result`/`.Wait()` to prevent thread-pool starvation and deadlocks.

### Async refactor notes (current)
- `Classes.Database` internal execution path is async end-to-end (`_ExecuteCMD`, `_ExecuteCMDDict`, and `MySQLServerConnector.ExecCMD`) and uses `ExecuteReaderAsync()` for MySQL reads.
- Keep the synchronous wrappers (`ExecuteCMD` / `ExecuteCMDDict`) as compatibility shims only; prefer async callers and avoid introducing new sync call sites.
- `DataObjectPermission.GetObjectPermission(...)` and `DataObjectPermission.GetObjectPermissionList(...)` now return `Task<...>` and must be awaited by controller/service callers.
- `SignatureManagement.SearchSignatures(...)` now returns `Task<object[]>`; API endpoints should `await` it before returning results.
- For incremental async cleanup, update method signatures and call chains together in one change so no mixed sync/async regressions are introduced.
