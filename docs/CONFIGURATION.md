# Configuration Reference

The configuration is saved in `config.json` typically located in the `ProgramData` folder.

## Settings
- `ListenAddress`: The IP to listen on (default: `127.0.0.1`, use `0.0.0.0` for all interfaces).
- `Port`: The port to listen on (default: `8080`).
- `AllowedClients`: Array of objects with `Name`, `IpAddressOrCidr`, and `IsEnabled`.
- `Security`:
  - `RestrictDestinationPorts`: Boolean to enforce destination ports (default: `true`).
  - `AllowedDestinationPorts`: Array of allowed destination ports (default: `[80, 443]`).
- `Logging`:
  - `LogRetentionDays`: Number of days to keep logs.
