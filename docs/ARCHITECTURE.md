# Architecture

PrintPilotProxy is designed with a clean architecture and separation of concerns.

## Components
1. **Core**: Contains models, validation, and domain logic. Abstracted from the proxy engine and UI.
2. **Proxy**: Implements the proxy engine abstraction over Unobtanium Web Proxy. Handles ACL and HTTP tunneling.
3. **Service**: The Windows Service that hosts the proxy engine.
4. **UI**: WPF management application using MVVM (CommunityToolkit.Mvvm).
5. **CLI**: Command-line interface for headless management.

## Cross-Platform Considerations
While currently focused on Windows (WPF + Windows Services), the core is built on .NET 8 standard libraries, allowing future support for Linux (systemd).
