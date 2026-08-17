---
name: code-review
description: Review PRs for behavior preservation, performance, caching, security, and database safety.
---

Review the PR with these rules:

1. Confirm whether the change is behavior-preserving.
   - Default assumption: a PR should not change existing behavior unless the PR description explicitly says it does.
   - If behavior changes, the PR description must state the intent and scope clearly.
   - Check that the implementation matches the described behavior change.
   - Flag accidental contract, output, or API changes.

2. Compare before and after behavior.
   - Explain the old behavior and the new behavior in concrete terms.
   - Check edge cases, defaults, null/empty handling, serializer output, return values, and query semantics.
   - Identify differences in request/response shape or database result semantics.
   - Prefer precise statements like “this preserves behavior because …” or “this changes X and should be explicitly called out.”

3. Review the changed hot path for performance.
   - Focus on the actual code path changed by the PR, not broad speculation.
   - Call out repeated DB reads, repeated object hydration, N+1 queries, repeated serialization, or extra work in loops.
   - Prefer concrete, code-level performance concerns over generic comments.
   - If the PR is a performance optimization, check whether it materially reduces work in the relevant user-facing path.

4. Identify cache opportunities for user latency.
   - Suggest caching only when the data is stable enough to cache and likely to affect response times.
   - Look for repeated reads of the same lookup data, expensive transforms, or hot metadata queries.
   - Note cache invalidation requirements and cache-key sensitivity for any suggested optimization.
   - Treat caching as an improvement suggestion, not a requirement for every PR.

5. Treat security issues as blockers.
   - Flag auth bypass, authorization gaps, unsafe deserialization, secret exposure, SSRF, injection, unsafe file access, path traversal, or privilege escalation issues.
   - Do not treat security concerns as minor review feedback; they are show stoppers.
   - Call out the risk and the exact impacted surface area.

6. Treat database issues as blockers.
   - Flag missing or unsafe migrations, broken schema changes, unindexed hot queries, N+1 issues in request paths, transaction problems, and logic that alters result semantics.
   - Check for migration safety, data correctness, cache invalidation impact, and query-plan risk.
   - Database regressions should be treated as blocking unless the PR explicitly documents the tradeoff and it has been validated.

7. Review the final impact.
   - State whether the change is behavior-preserving, safe, and performance-conscious.
   - If the PR changes behavior, say so directly and verify the change matches the PR description.
   - If there is a security or database risk, call it out as a blocker.
   - Prefer concise, actionable review comments grounded in the repo’s architecture and the specific patch under review.

Repository-specific expectations:
- Prefer repo conventions in .github/copilot-instructions.md over generic review guidance.
- Treat data access and migrations as high-risk areas in this codebase.
- For user-facing performance changes, look for opportunity to reduce repeated work and repeated expensive reads.
- Do not approve a PR that introduces a security or database regression without explicit documentation and validation.
