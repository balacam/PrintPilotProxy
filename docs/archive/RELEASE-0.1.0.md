# Release Notes - v0.1.0

We are excited to announce the initial release (v0.1.0) of PrintPilotProxy!

## What's Included

*   **Forward HTTP/HTTPS Proxy Engine**: Core functionality leveraging Unobtanium for secure tunneling via the HTTP `CONNECT` method.
*   **Windows Service Integration**: Runs seamlessly in the background on Windows environments.
*   **WPF Management Interface**: A graphical user interface for easy configuration of network interfaces, ports, and allowed clients.
*   **Granular Access Control**: IP and CIDR-based client access lists (ACL).
*   **Destination Port Filtering**: Restrict outbound connections to specific ports (e.g., 80, 443).

## Supported Platforms

*   **Windows**: Windows is currently the primary and fully supported platform for this release. Both the Core Proxy service and the Management UI are available.
*   **Linux**: Support for Linux Core/Proxy is planned for future releases.

## Known Limitations

*   **Linux UI**: A graphical management UI for Linux is not implemented and is not currently planned; future Linux support will focus on headless operation (systemd).
*   **Docker**: Docker packaging and containerized deployment options are not provided in this release.

## Security Model

*   **Default Deny**: All incoming proxy requests are denied by default unless the client's IP is explicitly allowed in the ACL.
*   **No SSL Interception**: PrintPilotProxy establishes blind TCP tunnels for HTTPS traffic. It does not intercept, decrypt, or inspect the TLS payload.
*   **Restricted Destinations**: Connections are limited to configured destination ports to prevent protocol smuggling or abuse.

## Installation

1.  Download the v0.1.0 release archive for Windows.
2.  Extract the contents to your desired installation directory.
3.  Run the provided installer or launch the WPF Management application to configure the service.
4.  Apply your interface, port, and client ACL settings, then start the proxy service.
