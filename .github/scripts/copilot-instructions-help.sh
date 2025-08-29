#!/usr/bin/env bash
# Prints guidance for updating .github/copilot-instructions.md when architecture/config files change.
# Usage: echo "<changed files>" | bash .github/scripts/copilot-instructions-help.sh <INSTRUCTIONS_PATH>

set -euo pipefail

INSTRUCTIONS_PATH=${1:-.github/copilot-instructions.md}

readarray -t CHANGED < <(cat - | sed '/^\s*$/d')

echo "\n—— Suggested updates for ${INSTRUCTIONS_PATH} ——"
echo "Review the sections below based on changed files:"

print_hint() {
  local title="$1"; shift
  echo "\n- ${title}"
  while (("$#")); do
    echo "  • $1"; shift
  done
}

FILES_JOINED=$(printf '%s\n' "${CHANGED[@]}")

# Program and controllers: routing/versioning/auth/cache profiles
if echo "$FILES_JOINED" | grep -Eq '^hasheous/(Program\.cs|Controllers/)'; then
  print_hint "API surface (hasheous)" \
    "Confirm routing pattern [ApiController]/[ApiVersion]/Route." \
    "Note cache profiles (5Minute/7Days) and example in LookupController." \
    "Mention auth attributes used ([ApiKey], [ClientApiKey]) if applicable." \
    "Swagger security definitions and xml comments inclusion."
fi

# Orchestrator queue
if echo "$FILES_JOINED" | grep -Eq '^service-orchestrator/Program\.cs'; then
  print_hint "Background jobs (service-orchestrator)" \
    "List QueueProcessor.QueueItems changes with intervals (minutes)." \
    "Call out new QueueItemType entries or timing adjustments."
fi

# Config and DB/migrations
if echo "$FILES_JOINED" | grep -Eq '^hasheous-lib/Classes/(Config\.cs|Database\.cs)'; then
  print_hint "Configuration & migrations" \
    "Update config path/shape or env var overrides if changed." \
    "Document InitDB behavior and any new migration script expectations."
fi

if echo "$FILES_JOINED" | grep -Eq '^hasheous-lib/Schema/'; then
  print_hint "Database schema" \
    "Note new hasheous-####.sql scripts and their purpose." \
    "Mention schema_version bump semantics if relevant."
fi

# Caching
if echo "$FILES_JOINED" | grep -Eq '^hasheous-lib/Classes/Redis\.cs|^hasheous-lib/Classes/LookupCache'; then
  print_hint "Caching" \
    "Reflect Redis enablement flags and key generation via RedisConnection.GenerateKey." \
    "Add invalidation guidance (PurgeCache)."
fi

# Signature ingestion
if echo "$FILES_JOINED" | grep -Eq '^hasheous-lib/Classes/SignatureIngestors/'; then
  print_hint "Signature ingestion" \
    "Summarize new/updated ingestors and target tables (Signatures_*)." \
    "Note file locations under ~/.hasheous-server/Data/Signatures/."
fi

# Launch settings and ports
if echo "$FILES_JOINED" | grep -Eq '^(hasheous|service-orchestrator)/Properties/launchSettings\.json'; then
  print_hint "Run URLs & profiles" \
    "Revise local URLs/ports from launchSettings.json." \
    "Call out dev-mode behaviors (client API key disabled)."
fi

# Docker and compose
if echo "$FILES_JOINED" | grep -Eq '^hasheous/docker/|^service-orchestrator/docker/|^docker-compose-build\.yml$'; then
  print_hint "Containerization" \
    "Explain image build, exposed ports, healthcheck endpoint (/api/v1/Healthcheck)." \
    "Document required env vars: dbhost/dbuser/dbpass, igdbclientid/secret, redisenabled/redishost/redisport."
fi

# README changes
if echo "$FILES_JOINED" | grep -Eiq '^README(\.md|\.MD)$'; then
  print_hint "README alignment" \
    "Align instructions with updated features/requirements from README." \
    "Sync example commands and versions if they changed."
fi

echo "\nTip: Keep instructions concise (20–50 lines), reference concrete files, avoid generic advice."
