---
name: performance-review
description: Review PRs for latency, repeated work, hot-path efficiency, and caching opportunities.
---

Review the PR with these rules:

1. Focus on the changed hot path.
   - Review the actual code path touched by the PR rather than guessing broadly about overall app performance.
   - Look for repeated work in request handling, background jobs, and data-access loops.

2. Check for repeated expensive operations.
   - Flag N+1 queries, repeated DB reads, repeated object hydration, repeated serialization, repeated external calls, and repeated expensive transformations.
   - Look for work inside loops or per-item processing that scales poorly with object count.

3. Compare before and after performance characteristics.
   - Identify the old cost model and the new cost model.
   - Check whether the PR reduces work in the relevant path or simply moves the cost elsewhere.

4. Look for caching opportunities.
   - Suggest caching when repeated reads are expensive and the data is stable enough to reuse safely.
   - Call out cache invalidation or key design requirements when a suggestion is made.
   - Keep suggestions targeted to actual user-facing latency or repeated expensive operations.

5. Treat performance regressions as actionable review findings.
   - A PR that makes the hot path slower should be called out unless the tradeoff is explicitly justified and validated.
   - Prefer concrete reasoning and specific code paths over generic “might be slow” comments.

Repository-specific expectations:
- Prefer repo conventions in .github/copilot-instructions.md over generic review guidance.
- This codebase has user-facing API routes, metadata lookups, and background jobs; focus on repeated work in request and cache-heavy paths.
- Do not approve a PR that introduces a meaningful latency regression in a hot path without explicit justification.
