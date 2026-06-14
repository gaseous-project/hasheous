# Hasheous MCP Server

This document describes the Model Context Protocol (MCP) support in Hasheous.

## Hosted MCP endpoint

- **URL:** `POST /api/v1/Mcp`
- **Protocol:** JSON-RPC 2.0 request/response payloads
- **API versioning route:** `api/v{version}/Mcp` (current: `v1`)
- **Authentication:** none; the endpoint is intentionally public for free database lookups

## Discovery manifest

- **URL:** `GET /.well-known/mcp.json`
- **Purpose:** publishes a machine-readable MCP manifest so MCP-aware clients can discover the Hasheous server more easily
- **Authentication:** none

## Supported JSON-RPC methods

- `initialize`
- `ping`
- `tools/list`
- `tools/call`
- `notifications/initialized` (accepted as notification/no response)

## MCP tools

### `hasheous_search_games`
Searches signature games by name and optional platform.

**Arguments**
- `name` (required, string)
- `platform` (optional, string)
- `limit` (optional, integer, 1-25; default 10)
- `includeRoms` (optional, boolean; default `true`)

### `hasheous_get_game_details`
Returns detailed game data (including ROMs/hashes) for one signature game id.

**Arguments**
- `gameId` (required, integer)

### `hasheous_lookup_hashes`
Finds matching games from one or more hash values.

**Arguments**
- `crc` (optional, string, 8 hex chars)
- `md5` (optional, string, 32 hex chars)
- `sha1` (optional, string, 40 hex chars)
- `sha256` (optional, string, 64 hex chars)
- `limit` (optional, integer, 1-25; default 10)

At least one hash must be supplied.

## Example requests

### Initialize
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {}
}
```

### List tools
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list",
  "params": {}
}
```

### Call search tool
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "hasheous_search_games",
    "arguments": {
      "name": "Altered Beast",
      "platform": "Sega Mega Drive",
      "limit": 5,
      "includeRoms": true
    }
  }
}
```

## Response shape

Successful JSON-RPC responses return:

- `result.content` (MCP text content array)
- `result.structuredContent` (machine-friendly object)

Tool-level validation errors return a successful JSON-RPC envelope with:

- `result.isError = true`
- `result.structuredContent.error`

Protocol-level errors return JSON-RPC `error` objects.

## Discovery manifest shape

The discovery manifest advertises:

- server name/description/version
- MCP protocol version
- hosted endpoint URL
- manifest URL
- docs URL
- transport type
- auth mode
- supported HTTP methods
- tool names/descriptions
- OpenAPI document URL

## CLI stdio MCP mode

Hasheous also supports stdio MCP mode via CLI:

```bash
dotnet run --project hasheous-cli -- mcp
```

The CLI mode uses the same MCP request/tool processor as the hosted endpoint.
