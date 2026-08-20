# Communication & Protocols

This document details the internal and external communication protocols used by PrintPilotProxy.

## 1. Proxy Tunneling (Client -> Proxy -> External)
The core proxy functionality is built on the standard HTTP `CONNECT` mechanism.

- **Port**: Default TCP `3128` (configurable).
- **Flow**:
  1. PrintPilot client opens a TCP connection to the proxy.
  2. Client sends an HTTP request: `CONNECT smtp.example.com:443 HTTP/1.1`.
  3. (Optional) Client includes `Proxy-Authorization: PrintPilot-HMAC ...`.
  4. Proxy validates ACL and Authentication.
  5. Proxy establishes a TCP connection to `smtp.example.com:443`.
  6. Proxy replies to client: `HTTP/1.1 200 Connection Established`.
  7. Client and External server communicate securely via TLS over the tunnel. Proxy acts as a blind TCP relay.

## 2. LAN Auto-Discovery (Client -> Proxy)
PrintPilot clients can automatically discover the proxy on the local network without manual IP configuration.

- **Protocol**: UDP Broadcast.
- **Port**: `37421`.
- **Flow**:
  1. Client broadcasts a JSON payload requesting discovery (e.g., `{"Action": "discover", "Service": "PrintPilotProxy"}`).
  2. The `PrintPilotProxy.Service` listens on UDP `37421`.
  3. The Service validates the payload.
  4. The Service responds directly to the client's UDP port with its current active TCP proxy listener endpoint (IP and Port).

## 3. Inter-Process Communication (App <-> Service)
Because the background Windows Service runs under a different security context (e.g., `LocalSystem`) than the user-facing Management App, they communicate via Inter-Process Communication (IPC).

- **Transport**: Named Pipes (`NamedPipeIpcClient`).
- **Pipe Name**: Internally managed by the Infrastructure layer.
- **Payload Format**: JSON serialized `IpcMessage` objects.
- **Message Types**:
  - `GetStatus` / `StatusResponse`: Retrieves metrics (connections, uptime).
  - `GetConfiguration` / `ConfigurationResponse`: Reads the current `ProxyConfiguration`.
  - `UpdateConfiguration`: Sends a new `ProxyConfiguration` to the service, which validates it, saves it to `config.json`, and dynamically restarts the proxy engine if necessary.
  - `StartProxy` / `StopProxy` / `RestartProxy`: Engine lifecycle commands.
  - `GetRecentRequests`: Retrieves a buffer of the last 1000 HTTP connections for the UI log viewer.
  - `GetSecurityAudit`: Invokes the core security auditor.

## 4. PrintPilot Client Integration

To route PrintPilot traffic through PrintPilotProxy, configure the proxy settings within your PrintPilot client:

1. **Auto-Discovery**: PrintPilot clients can automatically discover PrintPilotProxy on the local network using UDP broadcast on port `37421`.
2. **Manual Proxy Host**: Set this to the IP address or hostname of the server running PrintPilotProxy (e.g., `192.168.10.10`).
3. **Proxy Port**: Set this to the port PrintPilotProxy is listening on (default is `3128`).
4. **Protocols & Tunneling**: PrintPilotProxy supports HTTP and HTTPS/TLS tunneling using the HTTP `CONNECT` method, enabling secure, end-to-end encrypted proxying between PrintPilot clients and the destination SMTP Server / Mail Relay.
5. **Authentication**: If `RequireAuthentication` is enabled on the proxy, the PrintPilot client must be configured to automatically append a valid zero-touch `Proxy-Authorization: PrintPilot-HMAC ...` header to the `CONNECT` request. This is built into compatible versions of PrintPilot.

### Integration Network Requirements
- **Client to Proxy (LAN)**: The PrintPilot client PC must be able to reach the Proxy Server on the configured proxy port (TCP `3128`) and discovery port (UDP `37421`).
- **Proxy to SMTP / External**: The Proxy Server must have outbound network access to reach the target SMTP Server (e.g., ports 25, 465, 587 or custom).
- **Allowed Clients**: The IP address of the PrintPilot client must be explicitly allowed in PrintPilotProxy's ACL (or set to Allow All mode).
