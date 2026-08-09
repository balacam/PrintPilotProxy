# Security Policy

## Supported Versions
| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |

## Reporting a Vulnerability

Please do not report security vulnerabilities through public GitHub issues.

Instead, please send an email to security@printpilot.com. We will acknowledge receipt of your vulnerability report and strive to send you regular updates about our progress.

## Security Architecture

PrintPilotProxy is designed with strict security controls to ensure safe operation within restricted network environments. 

**CRITICAL WARNING: DO NOT expose PrintPilotProxy directly to the public Internet.** It is intended for internal network use only, to proxy traffic from internal clients out to the internet.

*   **Default Deny**: All inbound proxy requests are denied by default. 
*   **Client ACL**: Access is strictly controlled via an Access Control List. You must explicitly authorize clients by their IP address or CIDR block.
*   **Destination Ports**: Outbound connections are restricted to specific, configured destination ports (e.g., 80, 443) to prevent the proxy from being used to access unauthorized services.
*   **HTTPS CONNECT & No SSL Interception**: Secure HTTPS traffic is handled using the HTTP `CONNECT` method, which establishes a blind TCP tunnel. PrintPilotProxy does not intercept, decrypt, or inspect the TLS payload, ensuring end-to-end encryption.
*   **Management IPC**: Communication between the background Windows Service and the WPF Management Interface occurs over secure local Inter-Process Communication (IPC).
*   **Windows Firewall**: Ensure that the Windows Firewall on the Proxy Server is configured to only allow inbound connections on the specific port PrintPilotProxy is bound to, and ideally restrict those inbound connections to only the IPs of your allowed PrintPilot clients.
