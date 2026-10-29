# Security Model

PrintPilotProxy is designed to act as a secure, restrictive gateway for internal PrintPilot clients. It operates on a **deny-by-default** and **least-privilege** architecture.

## Reporting a Vulnerability
Please do not report security vulnerabilities through public GitHub issues. Instead, please send an email to security@printpilot.com. We will acknowledge receipt of your vulnerability report and strive to send you regular updates about our progress.

## Threat Model
**CRITICAL WARNING: DO NOT expose PrintPilotProxy directly to the public Internet.** It is intended for internal network use only, to proxy traffic from internal clients out to the internet.
Its primary purpose is to allow restricted internal clients to reach external servers (like Office365 or SendGrid) securely.

## 1. Network ACL (Access Control List)
A new installation starts in `AllowList` mode with an empty list, so no client is accepted until it is added. A configuration file that already exists keeps its mode; if it is `AllowAll`, change it to `AllowList` for production. The service logs a warning at start when the proxy listens on a publicly routable address, or accepts everyone without authentication on a non-loopback address.
- **IP / CIDR Validation**: The proxy verifies the incoming socket's IP address against the configured `AllowedClients` list.
- **Action**: Any connection from an IP not on the allow list is immediately dropped (HTTP 403 Forbidden).

### Protecting the configuration and the management channel
- **Secret handling**: `config.json` and its backups are readable only by SYSTEM and Administrators (inheritance is removed). The management API never returns the stored upstream password; it shows `(unchanged)`, and sending that value back keeps the stored password.
- **Who may change settings**: by default the logged-in console user and administrators may start, stop and reconfigure the proxy through the service pipe. Set `Ipc:RequireAdministrator` to `true` in the service's `appsettings.json` to require an administrator; the service logs a warning at start while it is `false`.
- **Concurrency**: commands that change state are executed one at a time, and a repeated command (same correlation id) returns the earlier answer instead of running twice.
- **Logs**: `Logging.RetentionDays`, `Logging.MaxSizeMb`, `Logging.MinimumLevel` and `Logging.RequestLoggingEnabled` are honoured by the service.

## 2. Destination Port Restrictions
Forward proxies can be abused to connect to internal services or send spam if unrestricted.
- **Enabled via Config**: `Security.DestinationPortRestrictionsEnabled = true`
- **Behavior**: The proxy inspects the requested URI in the HTTP `CONNECT` request. If the destination port is not explicitly in `AllowedDestinationPorts` (default 80, 443, 587, 465), the connection is dropped (HTTP 403 Forbidden).

## 3. Cryptographic HMAC Authentication (Zero-Touch)
PrintPilotProxy supports an optional protocol HMAC (`PrintPilot-HMAC`) and replay cache. The shared secret is derived from distributed code, so software that reproduces the derivation can generate valid headers. This is not per-device enrollment or proof that an allowed-IP request came from the genuine PrintPilot executable.
- **Enabled via Config**: `Security.RequireAuthentication = true`
- **Protocol**: Clients must include a `Proxy-Authorization` or `X-PrintPilot-Auth` header.
- **Mechanism**: The header contains a protocol version, a Unix timestamp, a random nonce, and a SHA-256 HMAC signature.
- **Key Generation**: Both the Client and Proxy derive a shared secret using obfuscated byte fragments combined with a salt. No manual key exchange is required.
- **Boundary**: Obfuscation is not secret provisioning. Retain the IP allow list and network firewall; use separately provisioned credentials if unique client authentication is required. Destination policy currently checks ports, not a domain or destination-IP allow list.
- **Replay Protection**: The proxy enforces a strict clock skew validation (maximum 5 minutes) based on the timestamp. Requests with expired timestamps are rejected.

### Authentication Header Example
```text
Proxy-Authorization: PrintPilot-HMAC v=1,ts=1690000000,nonce=abc123def456,sig=a1b2c3d4...
```

## 4. End-to-End Encryption (No MITM)
PrintPilotProxy utilizes the HTTP `CONNECT` method to establish blind TCP tunnels.
- **No Decryption**: The proxy does **NOT** decrypt TLS/SSL traffic. HTTPS interception (Man-in-the-Middle) is explicitly disabled in the underlying Unobtanium engine.
- **Certificate**: While the engine generates a local root certificate (`C:\ProgramData\PrintPilotProxy\rootCert.pfx`), it is not used to decrypt client traffic. It exists solely to satisfy the Unobtanium library's initialization requirements.

## 5. Security Audits
The `PrintPilotProxy.Core` library includes an automated `SecurityAuditor`. This runs continuous checks against the current configuration and issues warnings in the UI if it detects:
- `AllowAll` mode is enabled.
- The proxy is bound to `0.0.0.0` (All Interfaces) instead of a specific adapter.
- Destination port restrictions are disabled.
- Excessively broad CIDR ranges (e.g., `/8`) are allowed.
