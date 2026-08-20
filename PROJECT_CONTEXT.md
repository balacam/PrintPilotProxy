# PrintPilotProxy - Project Context

## Project Identity
**PrintPilotProxy** is a secure, forward HTTP/HTTPS proxy service designed specifically to enable **PrintPilot clients** to communicate safely over restricted networks.

## Project Purpose & Scope
In highly secured environments (such as hospitals, financial institutions, or corporate intranets), print clients are often isolated behind strict firewalls and cannot connect directly to external mail/SMTP servers. 
PrintPilotProxy solves this problem by acting as a controlled intermediary. It securely tunnels traffic through the perimeter firewall while providing granular access control (IP ACLs), destination port restrictions, cryptographic authentication (HMAC), and detailed traffic monitoring.

## PrintPilot ↔ PrintPilotProxy Relationship
- **Why it exists**: To act as a secure bridge for isolated PrintPilot clients that lack direct outbound internet access.
- **Auto-Discovery**: PrintPilot clients automatically locate the proxy on the LAN via UDP broadcasts on port 37421. The proxy responds with its IP, port, and authentication requirements.
- **Communication Flow**: PrintPilot sends standard HTTP `CONNECT` requests to the proxy (TCP 3128). The proxy establishes a blind TCP tunnel to the external server (e.g., SMTP). The client and external server communicate securely via TLS over this tunnel.
- **Authentication**: If enabled, PrintPilot sends a `PrintPilot-HMAC` zero-touch cryptographic signature in the `Proxy-Authorization` header.
- **Security Boundaries**: The proxy only validates the IP ACL and the HMAC signature. It does not perform SSL/TLS decryption (no MITM). It simply forwards the encrypted traffic.

## Architecture Summary
The project is built on .NET 8 and consists of cleanly separated layers:
- **Service**: A headless Windows Service (`PrintPilotProxy.Service`) that hosts the proxy engine.
- **Proxy**: The proxy engine implementation (`PrintPilotProxy.Proxy`) wrapping Unobtanium Web Proxy.
- **Core**: Platform-agnostic domain models, interfaces, and business logic (`PrintPilotProxy.Core`).
- **Infrastructure**: Platform-specific implementations for I/O, IPC, and security (`PrintPilotProxy.Infrastructure`).
- **App**: A WPF-based graphical management interface (`PrintPilotProxy.App`).
- **Cli**: A command-line tool for headless configuration (`PrintPilotProxy.Cli`).

## Runtime & Communication Model
1. **Client to Proxy (Tunneling)**: Clients connect to the proxy via TCP (default port 3128) and issue HTTP CONNECT requests.
2. **Auto-Discovery**: Clients use UDP broadcasts on port 37421.
3. **IPC (Inter-Process Communication)**: The Management App (`PrintPilotProxy.App`) communicates with the background Windows Service (`PrintPilotProxy.Service`) using Named Pipes to read status and update configurations without requiring manual file edits.

## Security Model Overview
The proxy operates on a "deny-by-default" principle:
- **Network ACL**: Client IP addresses must be explicitly allowed.
- **HMAC Authentication**: Optional zero-touch HMAC-based authentication prevents unauthorized use of the proxy even from allowed IPs.
- **Destination Restrictions**: Outbound connections are restricted to specific ports (e.g., 80, 443).
- **No Interception**: The proxy does NOT perform SSL/TLS decryption.

## Important Implementation Notes
- Configuration is stored locally at `C:\ProgramData\PrintPilotProxy\config.json`.
- The root certificate for the underlying Unobtanium engine is stored in `C:\ProgramData\PrintPilotProxy\rootCert.pfx` (used for engine initialization, though MITM is disabled).
- While the Core is .NET 8 Standard and platform-independent, the current hosting environment is tightly coupled to Windows (Windows Service & WPF). Linux support (systemd) is planned.

## Documentation Index

- `docs/ARCHITECTURE.md` - System overview, component breakdown, engine details.
- `docs/COMMUNICATION.md` - Protocols (HTTP CONNECT, UDP Discovery, IPC, PrintPilot integration).
- `docs/SECURITY.md` - Security model, threat model, authentication details.
- `docs/CONFIGURATION.md` - Schema and properties of `config.json`.
- `docs/DEVELOPMENT.md` - Setup, coding guidelines.
- `docs/BUILD.md` - Building and packaging instructions.
- `docs/DEPLOYMENT.md` - Installation, upgrade, and uninstall procedures.
- `docs/ADMINISTRATION.md` - Network topology, firewall rules, service operations, and logging.
- `docs/TROUBLESHOOTING.md` - Diagnosing and resolving common issues.
- `docs/TESTING.md` - Running automated tests.
- `docs/GLOSSARY.md` - Project terminology.
