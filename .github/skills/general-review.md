---
name: general-review
description: Review PRs for behavior preservation, contract changes, and overall correctness.
---

Review the PR with these rules:

1. Confirm whether the change is behavior-preserving.
   - Default rule: a PR should not change existing behavior unless the PR description explicitly states that it does.
   - If behavior changes, confirm the PR description clearly identifies the intent and scope.
   - Flag any accidental change in API contracts, return values, defaults, edge-case handling, or output shapes.

2. Compare before and after behavior.
   - Describe the old behavior and the new behavior in concrete terms.
   - Check edge cases, null/empty handling, default values, serialization output, request/response semantics, and database result semantics.
   - If the PR changes behavior, state that explicitly and verify it is intentional.

3. Check for correctness and regression risk.
   - Review the changed path for logic errors, partial updates, state mismatches, and hidden assumptions.
   - Look for conditions where the fix works in the happy path but changes semantics in edge cases.
   - Ensure the implementation matches the stated goal of the PR.

4. Review the final impact.
   - State whether the change is behavior-preserving or intentionally behavior-changing.
   - If the PR introduces a regression, call it out as a blocker.
   - Prefer concise, actionable review comments grounded in the actual code under review.

Repository-specific expectations:
- Prefer repo conventions in .github/copilot-instructions.md over generic review guidance.
- Treat data access, migrations, auth, and user-facing behavior as high-risk areas in this codebase.
- Do not approve a change that silently alters behavior without clear documentation and validation.
