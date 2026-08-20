---
name: Documentation Audit
description: Performs a deep audit of the repository to ensure documentation is consistent with the codebase.
---

# Documentation Audit Skill

This skill defines a reusable workflow for auditing the PrintPilotProxy documentation to ensure it remains synchronized with the actual implementation.

## Workflow

When invoked to perform a documentation audit, execute the following steps:

1. **Repository Analysis**: 
   - Analyze the current source code, configuration files, and network behavior.
   - Pay attention to authentication mechanisms, API definitions, IPC messages, and architectural dependencies.

2. **Documentation Review**:
   - Locate all `.md` files in the repository.
   - For each document, verify its technical accuracy against the source code.

3. **Consistency Checks**:
   - **Code vs Docs**: Check for outdated ports, file paths, default values, and class names.
   - **Architecture**: Verify that the documented architecture matches the current project structure.
   - **Security**: Ensure security features (ACLs, HMAC, TLS) are accurately described.
   - **Configuration**: Verify that `docs/CONFIGURATION.md` matches the actual configuration schema.
   - **Terminology**: Ensure terms match `docs/GLOSSARY.md`.
   - **Links**: Identify broken internal references or links.

4. **Actionable Updates**:
   - Correct inaccurate or outdated information.
   - Create missing documentation for new features.
   - Remove or merge duplicate documentation.
   - Ensure individual documents remain focused and avoid unnecessary proliferation.

5. **Final Reporting**:
   - Update `DOCUMENTATION_AUDIT.md` with the findings.
   - Summarize the state of the documentation, any discrepancies found, and the corrective actions taken.
