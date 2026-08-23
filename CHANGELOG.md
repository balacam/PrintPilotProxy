# Changelog

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
