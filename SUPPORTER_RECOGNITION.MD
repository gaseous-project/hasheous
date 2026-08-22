# Supporter Recognition

Hasheous can recognize active supporters by linking a user account to a payment provider account and periodically syncing recent payments.

## Current provider

- OpenCollective

## What gets stored

- External supporter account links are stored in `UserSupporterLinks`.
- Supporter account links are retained even when payments expire so users do not need to relink if they resume support later.
- The `Supporter` role is granted when a linked account has a qualifying payment within the last 30 days.
- The role is removed automatically when no linked provider remains active.
- Linked supporter accounts are for supporter recognition only and are not used as sign-in methods.

## Active contribution rule

A contribution is active when the current UTC time is less than or equal to:

- `LastPaymentUtc + ActiveContributionDays`

The default `ActiveContributionDays` value is `30`.

## Configuration

The feature uses two configuration groups:

- OAuth linking
  - `opencollectiveclientid`
  - `opencollectiveclientsecret`
  - OpenCollective OAuth callback URL: `https://<your-hasheous-host>/signin-opencollective`
- Background supporter sync
  - `opencollectiveapitoken`
  - `opencollectivecollectiveslug`

### Required API token scopes for `opencollectiveapitoken`

The token is sent as a ****** to the OpenCollective GraphQL v2 API
(`https://api.opencollective.com/graphql/v2`) and is used exclusively for
read-only access to contribution/transaction data.

When creating the token on the OpenCollective platform (Settings → For
Developers → Personal Tokens), grant the following scope:

| Scope | Reason |
|---|---|
| `account` (read) | Required to look up the collective by slug and read its transaction list. The query reads `account.transactions` with fields `createdAt` and `fromAccount` (id, slug, name). |

No write scopes are needed. The token does not require access to expenses,
orders, legal or financial details beyond the public contribution records
listed above.

These values are exposed through `Config.SupporterRecognitionConfiguration`.

## User flow

1. A signed-in user opens the account page.
2. The account page lists available external providers, including OpenCollective when OAuth linking is configured.
3. The user links OpenCollective through OAuth.
4. Hasheous stores the provider account id and slug in `UserSupporterLinks`.
5. Hasheous immediately syncs the linked account and then refreshes it daily through the orchestrator queue.
6. If the linked account has a qualifying payment in the last 30 days, the user receives the `Supporter` role.

## Background synchronization

- Queue item: `SyncSupporterStatus`
- Task implementation: `hasheous-lib/Classes/ProcessQueue/Tasks/SyncSupporterStatus.cs`
- Service implementation: `hasheous-lib/Classes/Supporters/SupporterRecognitionService.cs`

The synchronization process:

1. Queries the configured provider for recent payments.
2. Matches those payments against linked Hasheous accounts.
3. Updates `UserSupporterLinks` with the latest payment and expiry timestamps.
4. Grants or removes the `Supporter` role.

## Developer integration

Use the `Supporter` role when enabling supporter-only features.

### Backend authorization

You can gate an endpoint with either:

- `[Authorize(Roles = "Supporter")]`
- `[Authorize(Policy = "Supporter")]`

The `Supporter` authorization policy is registered in `hasheous/StartupExtensions.cs`.

### Backend logic checks

Examples:

- `await _userManager.IsInRoleAsync(user, "Supporter")`
- inspect `UserSupporterLinks` when provider-specific details are needed

Use the table when a feature needs provider metadata such as:

- which provider granted supporter status
- when the latest payment was made
- when recognition expires

### Frontend checks

The account page and any other authenticated page can inspect:

- `userProfile.Roles.includes('Supporter')`

Use this for supporter-only UI affordances, badges, or navigation.

## Expandability

The implementation is provider-oriented:

- `ISupporterProvider` defines the sync contract.
- `SupporterRecognitionService` works with provider instances instead of hard-coded OpenCollective-only logic.
- `UserSupporterLinks` stores provider names and provider account identifiers generically.
- The `Supporter` role is provider-agnostic, so Patreon or other providers can grant the same entitlement later.

To add another provider in future:

1. Implement `ISupporterProvider`.
2. Register the provider in `SupporterRecognitionService`.
3. Add provider configuration fields.
4. Add account-linking support if OAuth or another link flow is required.
5. Reuse the existing `Supporter` role and `UserSupporterLinks` persistence model.
