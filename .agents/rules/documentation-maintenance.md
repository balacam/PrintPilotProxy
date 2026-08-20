---
name: Documentation Maintenance
description: Ensures documentation remains synchronized with code changes.
---

# Documentation Maintenance Rule

This rule ensures that the documentation for PrintPilotProxy remains accurate and synchronized with the actual source code and configuration.

When modifying code or configuration in this project, you must:

1. **Detect Changes**: Determine whether your code or configuration change affects the system's behavior, API, networking, deployment, or architecture.
2. **Identify Documents**: Identify which documentation files (e.g., `PROJECT_CONTEXT.md`, `docs/ARCHITECTURE.md`, `docs/CONFIGURATION.md`) are affected by this change.
3. **Update Selectively**: Update only the relevant documentation files. Do not rewrite large portions of the documentation unnecessarily.
4. **Verify Consistency**: Ensure the updated documentation accurately reflects the implementation.
5. **Never Invent**: Do not document hypothetical features or invent information. Source code is the source of truth.
6. **Protect Secrets**: Never expose real credentials, tokens, passwords, or secrets in the documentation. Use placeholders where necessary.
7. **Report**: In your final summary to the user, report which documentation files were updated.
8. **No Update Needed**: If the code change does not affect documentation, explicitly state: `Documentation reviewed — no update required.`
