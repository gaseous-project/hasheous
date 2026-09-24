---
name: database-review
description: Review PRs for migration safety, query correctness, and database regression risk.
---

Review the PR with these rules:

1. Check database semantics and correctness.
   - Review schema changes, table/index changes, query changes, update logic, and transaction boundaries.
   - Confirm that the PR does not silently change result semantics or data shape.
   - Look for missing filters, changed join semantics, or altered default values.

2. Review migration safety.
   - Treat schema or data migration edits as high risk.
   - Check for missing migration scripts, unsafe backfills, or destructive statements without guardrails.
   - Verify that migration ordering is correct and the change remains compatible with existing data.

3. Check for performance regressions.
   - Look for N+1 queries, repeated scans, unbounded result sets, and expensive loops in database access.
   - Check whether the PR adds work on hot request paths or large tables.
   - Flag queries that would scale poorly with production data volumes.

4. Review indexing and query plans.
   - Check whether new filters or joins need an index.
   - Look for cases where a query may become slow without an appropriate supporting index.
   - Call out work that is logically correct but operationally risky at scale.

5. Treat database issues as blockers.
   - Unsafe schema changes, migration risk, or query regressions should be treated as blockers.
   - Any change that risks data correctness or production instability should be called out directly.

Repository-specific expectations:
- Prefer repo conventions in .github/copilot-instructions.md over generic review guidance.
- This codebase relies on MariaDB/MySQL and embedded schema migrations; treat those as critical review areas.
- Do not approve a PR that introduces a database correctness or migration risk without explicit validation.
