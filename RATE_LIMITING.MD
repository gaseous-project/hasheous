# Rate Limiting

Hasheous includes a profile-driven request rate limiter for API/controller traffic.

## Overview

- Rules live in a standalone file: `~/.hasheous-server/rate-limit-rules.json`
- The server creates the file automatically on first startup if it does not exist
- The file is reloaded every 5 minutes without restarting the server
- Rules are evaluated in ascending `Order`
- The first matching enabled profile wins
- Built-in webpage requests are always exempt
- Static files are not affected because the limiter runs on MVC/API requests

## Built-in webpage exemption

Requests made by the hosted Hasheous webpage are excluded from rate limiting when any of the following are true:

- `X-Hasheous-Web-Request: 1` is present
- `Sec-Fetch-Site: same-origin` is present
- `Origin` matches the current Hasheous host
- `Referer` matches the current Hasheous host

The bundled frontend now automatically adds `X-Hasheous-Web-Request: 1` to same-origin API fetches.

## Rule file format

Example:

```json
{
  "Profiles": [
    {
      "Name": "External Lookup Clients",
      "Enabled": true,
      "Order": 100,
      "Exempt": false,
      "PartitionBy": [
        "header:X-Client-API-Key",
        "remote-ip"
      ],
      "Match": {
        "Methods": [ "POST" ],
        "Paths": [ "/api/v1/Lookup/*" ],
        "HasClientApiKey": true,
        "IsWebPage": false,
        "UserAgents": [ "Gaseous*" ],
        "Headers": {
          "X-Client-API-Key": [ "*" ]
        }
      },
      "FixedWindow": {
        "PermitLimit": 60,
        "WindowSeconds": 60,
        "QueueLimit": 0
      }
    },
    {
      "Name": "Admins",
      "Enabled": true,
      "Order": 10,
      "Exempt": true,
      "PartitionBy": [ "user-id" ],
      "Match": {
        "UserRoles": [ "Admin", "Moderator" ]
      },
      "FixedWindow": {
        "PermitLimit": 1,
        "WindowSeconds": 1,
        "QueueLimit": 0
      }
    }
  ]
}
```

## Profile fields

### `Name`
Human-readable identifier returned in 429 responses.

### `Enabled`
Turns a profile on or off without removing it.

### `Order`
Lower values run first.

### `Exempt`
If `true`, the matching request bypasses rate limiting.

### `PartitionBy`
Builds the limiter bucket key. Supported values:

- `remote-ip`
- `origin`
- `user-agent`
- `path`
- `method`
- `user-id`
- `user-roles`
- `header:<Header-Name>`

If omitted, `remote-ip` is used.

### `Match`
Conditions for profile selection. All specified conditions must match.

Supported fields:

- `Methods`
- `Paths`
- `Origins`
- `UserAgents`
- `RemoteIps`
- `UserRoles`
- `Headers`
- `AuthenticatedUser`
- `HasClientApiKey`
- `HasUserApiKey`
- `IsWebPage`

String matching supports `*` wildcards.

### `FixedWindow`

- `PermitLimit`: maximum requests allowed in the window
- `WindowSeconds`: window size in seconds
- `QueueLimit`: queued requests when a window is exhausted

## Runtime behavior

1. ASP.NET authentication runs
2. The rate limiter builds a request context from:
   - user roles
   - origin
   - user agent
   - remote IP
   - user API key presence
   - client API key presence
   - arbitrary headers
3. If the request is from the built-in webpage, it is immediately exempt
4. The first matching enabled profile is selected
5. If the profile is exempt, the request continues
6. Otherwise a fixed-window limiter is applied for that profile and partition key
7. When the request exceeds the limit, the API returns `429 Too Many Requests`

429 responses include:

- `Retry-After` header when available
- a JSON body containing:
  - `error`
  - `profile`
  - `retryAfterSeconds`

## Implementation guide

### Add or change rules

1. Edit `~/.hasheous-server/rate-limit-rules.json`
2. Add or modify profiles
3. Wait up to 5 minutes for automatic reload
4. Test the affected API flow

### Add new match inputs

1. Extend `RateLimitRequestContext` in `/home/runner/work/hasheous/hasheous/hasheous-lib/Classes/RateLimiting/RequestRateLimiting.cs`
2. Populate the value in `BuildRequestContextAsync(...)`
3. Add a property to `RateLimitMatchCriteria`
4. Update `Matches(...)`
5. Add tests in `/home/runner/work/hasheous/hasheous/hasheous-lib.Tests/RateLimiterTests.cs`
6. Update this document

### Add a new partition type

1. Extend `BuildPartitionValue(...)`
2. Document the new token in this file
3. Add a test covering the new token

### Recommended profile strategy

- Put explicit exemptions first
- Exempt Admin/Moderator traffic if desired
- Keep webpage traffic untouched
- Use API key headers or user IDs for trusted client partitions
- Use IP-based fallback partitions for anonymous/public traffic
- Keep broad catch-all rules last

## Operational notes

- Invalid or unreadable rule files do not stop the server; the last valid rules remain in use
- Rule reload resets in-memory limiter buckets so new limits apply cleanly
- If Redis is enabled elsewhere in Hasheous, rate limiting still uses in-process buckets in this implementation
- Because the limiter is applied as an MVC filter, it covers controller/API requests and not static file delivery
