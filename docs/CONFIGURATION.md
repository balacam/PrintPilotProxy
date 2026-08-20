# Configuration Reference

PrintPilotProxy stores its configuration in a JSON file.
- **Location**: `C:\ProgramData\PrintPilotProxy\config.json`
- **Application**: Changes can be made via the WPF UI, CLI, or by editing the JSON directly. The Service validates the configuration upon loading.

The schema matches the `ProxyConfiguration` class.

## Root Object (`ProxyConfiguration`)

| Property | Type | Description |
| :--- | :--- | :--- |
| `SchemaVersion` | int | Schema version for forward compatibility (Current: 2). |
| `Listener` | object | Listener behavior. See [Listener Settings](#listener-settings). |
| `ClientAccess` | object | Network access control. See [Client Access Settings](#client-access-settings). |
| `Security` | object | Port restriction and auth. See [Security Settings](#security-settings). |
| `Logging` | object | Log retention and levels. See [Logging Settings](#logging-settings). |
| `Service` | object | Windows Service behavior. See [Service Settings](#service-settings). |
| `Firewall` | object | Windows Firewall integration. See [Firewall Settings](#firewall-settings). |
| `Language` | object | UI localization preferences. |
| `LastModified` | string (ISO 8601) | Timestamp of the last configuration change. |

## Listener Settings (`Listener`)

Defines how the proxy binds to network interfaces.

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Mode` | string | `"Auto"` | Allowed values: `"Auto"`, `"SpecificAddress"`, `"AllInterfaces"`, `"SpecificAdapter"`. |
| `ListenAddress` | string | `"0.0.0.0"` | IP to bind to. Only used if `Mode` is `SpecificAddress`. |
| `AdapterName` | string | `null` | Network adapter name. Only used if `Mode` is `SpecificAdapter`. |
| `Port` | int | `3128` | The TCP port for the HTTP CONNECT proxy. |
| `MaxConnections` | int | `1000` | Max concurrent proxy connections. |
| `ConnectionTimeoutSeconds` | int | `120` | Timeout for idle connections. |

## Client Access Settings (`ClientAccess`)

Controls which IP addresses can connect to the proxy. **Deny-by-default is recommended.**

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Mode` | string | `"AllowAll"` | Allowed values: `"AllowAll"`, `"AllowList"`. Change to `AllowList` for security. |
| `AllowedClients` | array | `[]` | List of explicit client objects. Each object contains `Name` (string), `IpAddressOrCidr` (string), and `IsEnabled` (bool). |

## Security Settings (`Security`)

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `DestinationPortRestrictionsEnabled`| bool | `false` | If true, only connections to `AllowedDestinationPorts` are permitted. |
| `AllowedDestinationPorts` | array of int | `[80, 443]` | The outbound ports the proxy is allowed to connect to. |
| `RequireAuthentication` | bool | `false` | If true, requires valid HMAC authentication headers from the client. |

## Logging Settings (`Logging`)

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `RequestLoggingEnabled` | bool | `true` | Whether to log individual proxy requests. |
| `RetentionDays` | int | `30` | How long to keep log files before deleting. |
| `MaxSizeMb` | int | `100` | Maximum size of total logs in MB. |
| `MinimumLevel` | string | `"Information"` | Minimum log severity (e.g., `"Debug"`, `"Information"`, `"Warning"`). |

## Service Settings (`Service`)

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `AutoStartProxy` | bool | `true` | Start the proxy engine automatically when the Windows Service starts. |
| `AutoRestartOnFailure`| bool | `true` | Restart the proxy engine if it crashes unexpectedly. |
| `RestartDelaySeconds` | int | `5` | Wait time before restarting the engine. |

## Firewall Settings (`Firewall`)

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `RuleEnabled` | bool | `true` | Automatically manage a Windows Firewall inbound rule for the proxy port. |
| `InterfaceScope` | string | `"Any"` | Firewall rule scope: `"Any"`, `"LAN"`, `"Wireless"`, `"RAS"`. |
