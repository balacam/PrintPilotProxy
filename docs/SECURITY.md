# Security Model

PrintPilotProxy is designed to act as a secure, restrictive gateway for internal PrintPilot clients. It operates on a **deny-by-default** and **least-privilege** architecture.

## Reporting a Vulnerability
Please do not report security vulnerabilities through public GitHub issues. Instead, please send an email to security@printpilot.com. We will acknowledge receipt of your vulnerability report and strive to send you regular updates about our progress.

## Threat Model
**CRITICAL WARNING: DO NOT expose PrintPilotProxy directly to the public Internet.** It is intended for internal network use only, to proxy traffic from internal clients out to the internet.
Its primary purpose is to allow restricted internal clients to reach external servers (like Office365 or SendGrid) securely.

## 1. Network ACL (Access Control List)
By default, the proxy operates in `AllowAll` mode for ease of setup. For production environments, this MUST be changed to `AllowList`.
- **IP / CIDR Validation**: The proxy verifies the incoming socket's IP address against the configured `AllowedClients` list.
- **Action**: Any connection from an IP not on the allow list is immediately dropped (HTTP 403 Forbidden).

## 2. Destination Port Restrictions
Forward proxies can be abused to connect to internal services or send spam if unrestricted.
- **Enabled via Config**: `Security.DestinationPortRestrictionsEnabled = true`
- **Behavior**: The proxy inspects the requested URI in the HTTP `CONNECT` request. If the destination port is not explicitly in `AllowedDestinationPorts` (default 80, 443), the connection is dropped (HTTP 403 Forbidden).

## 3. Cryptographic HMAC Authentication (Zero-Touch)
To prevent unauthorized software on *allowed* IP addresses from abusing the proxy, PrintPilotProxy supports a zero-touch cryptographic authentication scheme (`PrintPilot-HMAC`).
- **Enabled via Config**: `Security.RequireAuthentication = true`
- **Protocol**: Clients must include a `Proxy-Authorization` or `X-PrintPilot-Auth` header.
- **Mechanism**: The header contains a protocol version, a Unix timestamp, a random nonce, and a SHA-256 HMAC signature.
- **Key Generation**: Both the Client and Proxy derive a shared secret using obfuscated byte fragments combined with a salt. No manual key exchange is required.
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
