---
name: security-review
description: Review PRs for authentication, authorization, input validation, data exposure, and security regressions.
---

Review the PR with these rules:

1. Check all auth and authorization paths.
   - Verify that access control remains correct and no endpoint or action becomes more permissive.
   - Review role checks, API key enforcement, authentication filters, and authorization bypasses.
   - Ensure privileged operations remain protected.

2. Look for validation and injection issues.
   - Review raw request parsing, SQL, file paths, URL handling, and untrusted input flow.
   - Flag injection risks, unsafe deserialization, path traversal, SSRF, and unsafe file operations.
   - Check for missing validation on externally supplied values.

3. Review sensitive data handling.
   - Flag secret leakage, debug output, tokens, cookies, user data exposure, or logging of sensitive fields.
   - Check that the PR does not accidentally expose internal state through responses or logs.

4. Review trust boundaries.
   - Confirm that data from external providers, user input, and file content is handled with appropriate validation and sanitization.
   - Call out unsafe assumptions about upstream data sources.

5. Treat security issues as show stoppers.
   - Any auth bypass, validation gap, injection risk, or data exposure issue should be called out as a blocker.
   - The review should explicitly state the risk and the affected surface area.

Repository-specific expectations:
- Prefer repo conventions in .github/copilot-instructions.md over generic review guidance.
- This codebase uses API keys, cookies, middleware, and public metadata endpoints; treat auth and data exposure as critical review areas.
- Do not approve a PR that introduces a security regression without explicit mitigation and validation.
