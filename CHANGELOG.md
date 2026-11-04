# Changelog

## [Unreleased]
### Fixed
- `SpecificAdapter` listener mode now binds every usable address of the adapter. It used to bind only the first one, which Windows often lists as IPv6, leaving IPv4 clients unable to connect.
- The upstream proxy password is entered in a masked password box instead of a plain text box, and a failure to encrypt it is reported instead of silently keeping the old password.
- The About page, the page headings and the Internet Connection section are translated in all 13 UI languages; internet test results are shown as translated sentences instead of raw codes.
- The About page describes what the product does: an HTTP CONNECT tunnel for PrintPilot clients, not a print stream relay.
### Changed
- Version 1.0.3 targets .NET 10 LTS (`net10.0` / `net10.0-windows`, self-contained) instead of .NET 8, whose support ends on 2026-11-10. Microsoft.Extensions packages are 10.0.14 and Serilog.Extensions.Hosting is 10.0.0; the redundant `System.IO.Pipes.AccessControl` package is dropped because .NET 10 ships it.
- Client access defaults to `AllowList` in the model and the engine (PX-03). A `config.json` whose `clientAccess` section or `mode` is missing now stays closed instead of accepting every client; files that state `AllowAll` keep it.
- "Max Connections" is labelled "Connection warning threshold" in all 13 languages, with a hint that connections above it are logged, not refused (PX-04). Behaviour is unchanged.
- Internal refactoring of the proxy engine and service worker: the access checks shared by CONNECT and plain HTTP requests, endpoint setup and IPC responses each live in one place. Behaviour is unchanged.

## [0.9.3] - 2026-08-22
### Fixed
- Fixed specific network adapter address resolution type mismatch bug (`string.Equals(IPAddress)`).
- Prevented secondary cleanup exception in `DisposeServer` from masking the primary proxy start failure message.
- Ensured `AdapterName` is correctly assigned on network configuration apply.

## [0.8.6] - 2026-08-21
### Added
- Added date display (yyyy-MM-dd HH:mm:ss) alongside time in Client Traffic logs and Dashboard activity tables.
- Widened Time column for full timestamp readability.

- Initial release of PrintPilotProxy
- Forward HTTP/HTTPS proxy with CONNECT tunneling
- Client IP/CIDR access control list
- Destination port restrictions
- WPF management interface
- Windows Service support
- Windows Firewall integration
- Configuration management with backup/restore
- Security audit
- Diagnostics
- Health monitoring
- Structured logging with Serilog
