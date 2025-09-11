# Copilot instructions for the hasheous codebase

Use this to get productive fast. Follow the existing patterns in this repo over generic .NET advice.

- Repo layout
  - `hasheous/` ASP.NET Core 8 web API + static UI; public endpoints and Swagger.
  - `service-orchestrator/` ASP.NET Core API hosting background orchestration and scheduled jobs.
  - `hasheous-lib/` Shared library: data access, config, auth, background tasks, models, migrations (embedded SQL).
  - `hasheous-cli/` CLI utilities.
  - NOTE (resource namespace): Embedded resources (SQL migrations, support data lists) now resolve under the assembly root namespace `hasheous_lib` (e.g. `hasheous_lib.Schema.hasheous-1004.sql`, `hasheous_lib.Support.Country.txt`). Use that prefix when adding new embedded resources; prior references using `hasheous.*` were refactored.

- Architecture & data flow
  - MariaDB/MySQL is the source of truth. On startup, schema is created/migrated via embedded scripts (see `hasheous-lib/Classes/Database.cs::InitDB`, scripts named `hasheous-####.sql`).
  - Signature data is ingested from DAT/XML (TOSEC/No-Intro/etc.) by `Classes/SignatureIngestors/XML.cs` into `Signatures_*` tables.
  - Public API (versioned) handles lookups like `POST /api/v1/Lookup/ByHash` via `Classes/HashLookup` + `Classes/Database`.
  - Redis (Valkey) provides caching if enabled (`Classes/RedisConnection`), otherwise an in-memory cache is used.
  - Separation: the orchestration server exists to separate background tasks from the frontend web server. The frontend web server only services user requests; the orchestrator schedules and runs internal/background work (`QueueProcessor.QueueItems` in `service-orchestrator/Program.cs`). Note the orchestrator has no public endpoints, and should only be called by trusted web servers using the inter-host API key.

- Run & debug (local)
  - First run creates `~/.hasheous-server/config.json` (see `hasheous-lib/Classes/Config.cs`). MariaDB 11+ required; Redis optional.
  - App URLs: hasheous http 5220 / https 7157; orchestrator http 5140 / https 7023 (see each `Properties/launchSettings.json`).
  - VS Code tasks: build/publish/watch defined at root; common loop is the “watch” task on `hasheous/hasheous.csproj`.
  - Docker: `docker-compose-build.yml` brings up server, orchestrator, MariaDB, and Valkey. Healthcheck: `GET /api/v1/Healthcheck`.

- Configuration
  - File: `~/.hasheous-server/config.json` (auto-updated on startup by `Config.UpdateConfig()`). Env vars (used by Docker): `dbhost`, `dbuser`, `dbpass`, `igdbclientid`, `igdbclientsecret`, `redisenabled`, `redishost`, `redisport`, `reportingserverurl`, etc.
  - Development mode disables client API key requirement (`Config.RequireClientAPIKey = false`) and enables developer exception page.
  - GiantBomb: set `gbapikey` env var (or update config.json) to enable GiantBomb metadata ingestion & proxy; optional `BaseURL` override (defaults to `https://www.giantbomb.com/`).

- API versioning & routing
  - Controllers use `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]/")]`.
  - Prefer using cache profiles: `"5Minute"` or `"7Days"` configured in `hasheous/Program.cs` (see `LookupController` usage).
  - Swagger is enabled with custom schema IDs and API key security definitions.

- Auth & security
  - Identity cookies configured; roles/policies: Admin, Moderator, Member, "Verified Email". Roles are seeded on startup.
  - Role hierarchy: Admin > Moderator > Member. "Verified Email" is a status role (not hierarchical) automatically assigned/removed based on email confirmation status.
  - API keys:
  - User key header `X-API-Key` via `[Authentication.ApiKey.ApiKeyAttribute]` (`ApiKeyAuthorizationFilter` wired in `hasheous/Program.cs`) — identifies individual users and their actions.
  - Client key header `X-Client-API-Key` via `[Authentication.ClientApiKey.ClientApiKeyAttribute]` — identifies client apps (e.g., Gaseous, Romm); typically required when `Config.RequireClientAPIKey` is true. Required to access the metadata proxy endpoints.
  - Inter-host API key for orchestrator calls (see `InterHostApiKey*` and registration in `service-orchestrator/Program.cs`) — security mechanism allowing multiple web server frontends (load-balanced) to securely call the orchestrator.

- Data access & migrations
  - Create a `new Classes.Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString)`.
  - Prefer async methods: `ExecuteCMDAsync`/`ExecuteCMDDictAsync`; use `await` in controllers/handlers. Avoid blocking sync calls unless there’s no async alternative.
  - Add new migration scripts in `hasheous-lib/Schema` as `hasheous-####.sql` with the next number.
  - Embedded migration & support file manifest names now start with `hasheous_lib.Schema.` or `hasheous_lib.Support.`. After adding a file, ensure Build Action = EmbeddedResource and verify with `Assembly.GetExecutingAssembly().GetManifestResourceNames()` if debugging mismatches.

- Caching
  - Use `hasheous.Classes.RedisConnection.GenerateKey(prefix, keyObj)` and `PurgeCache(prefix)`. Redis enabled when `Config.RedisConfiguration.Enabled`.

- Background jobs
  - Add/adjust scheduled tasks in `service-orchestrator/Program.cs` under `QueueProcessor.QueueItems` (e.g., `FetchIGDBMetadata`, timings are minutes).
  - GiantBomb: when `Config.GiantBomb.APIKey` is present a `FetchGiantBombMetadata` job is queued (default 10080 minutes / 7 days) to refresh GiantBomb platform/game/image data.
  - Queue task refactor: obsolete blocking entries `GetMissingArtwork` and `MetadataMatchSearch` were removed from metadata fetch task `Blocks` lists. Don’t rely on them for future coordination.

- JSON & serialization
  - System.Text.Json and Newtonsoft are both configured: enums-as-strings, nulls ignored, max depth 64, indented output (Newtonsoft).

- Examples to copy
  - Healthcheck: `hasheous-lib/Controllers/HealthcheckController.cs` → `[AllowAnonymous]`, versioned route, `Ok()`.
  - Lookup: `hasheous/Controllers/V1.0/LookupController.cs` shows async handlers, request models, cache usage, and the timeout pattern.
  - Account management: `hasheous/Controllers/V1.0/AccountController.cs` shows authenticated endpoints with `[Authorize]`, user context access via `_userManager.GetUserAsync(User)`, and role management patterns.
  - Email verification: Account controller demonstrates automatic role assignment (`UpdateVerifiedEmailRole()`) and secure user-specific operations.

- Gotchas
  - Large uploads are allowed (Kestrel/FormOptions set to max); ensure proxies forward `X-Forwarded-*` (already configured).
  - Keep XML docs for Swagger (`IncludeXmlComments` expects generated XML files from the projects).

If something is unclear or missing (e.g., additional services, tests, or new auth flows), ask and this guide can be refined.

## Quick how‑tos
- Add a new API endpoint
  - Create a controller under `hasheous/Controllers/V1.0` with `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]/")]`.
  - Reuse cache profiles via `[ResponseCache(CacheProfileName = "5Minute")]` or `"7Days"`.
  - Protect with `[Authentication.ApiKey.ApiKeyAttribute]` or `[Authentication.ClientApiKey.ClientApiKeyAttribute]` when needed.
  - For authenticated endpoints, use `[Authorize]` and access user context via `_userManager.GetUserAsync(User)`.
  - Use `new Classes.Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString)` and `ExecuteCMD/ExecuteCMDDict` for DB work.
  - If caching, build keys with `hasheous.Classes.RedisConnection.GenerateKey(prefix, keyObj)`.

- Add a background job
  - Edit `service-orchestrator/Program.cs` and add a `QueueProcessor.QueueItem(QueueItemType.YourItem, <minutes>, false)` to `QueueProcessor.QueueItems`.
  - Implement the corresponding handler under `hasheous-lib/Classes/ProcessQueue/` (follow existing `QueueItemType` patterns).

- Add a DB migration
  - Create `hasheous-lib/Schema/hasheous-####.sql` using the next available number; `InitDB()` applies scripts in order and bumps `schema_version`.
  - When referencing a migration or support text file at runtime, construct resource names with `hasheous_lib.Schema.` / `hasheous_lib.Support.` prefixes.

- Use the lookup timeout pattern
  - See `LookupController.LookupPost`: `Task.WhenAny(..., Task.Delay(TimeSpan.FromSeconds(10)))` and set `Retry-After = 90` on 503 responses.

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

- Email verification system
  - "Verified Email" role is automatically assigned/removed based on `user.EmailConfirmed` status.
  - Use `UpdateVerifiedEmailRole(user)` pattern after email confirmation changes.
  - `ProfileBasicViewModel.EmailConfirmed` provides frontend email status.
  - Frontend: use `postData()` for authenticated API calls (handles CSRF tokens and cookies).

## Maintenance
- A PR guard (`.github/workflows/copilot-instructions-guard.yml`) fails when architecture/config files change without updating this file; it prints hints via `.github/scripts/copilot-instructions-help.sh`.
  - Update this file when: resource namespace conventions change (e.g., `hasheous_lib.*` migration), new cross-cutting utilities like `ComputeObjectPropertyHash` are added, or queue coordination semantics are modified.

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

## async guidance
- Prefer Task-returning actions: `public async Task<IActionResult> Action(...)`.
- Await DB calls (`ExecuteCMDAsync`/`ExecuteCMDDictAsync`) and long-running operations.
- Use `Task.WhenAny` for bounded latency where appropriate (see `LookupController`).
- Avoid `.Result`/`.Wait()` to prevent thread-pool starvation and deadlocks.
