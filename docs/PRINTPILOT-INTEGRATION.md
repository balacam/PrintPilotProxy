# PrintPilot Integration Guide

This guide explains how to integrate a PrintPilot client with PrintPilotProxy.

## Proxy Configuration in PrintPilot

To route PrintPilot traffic through PrintPilotProxy, you need to configure the proxy settings within your PrintPilot client.

1.  **Proxy Host**: Set this to the IP address or hostname of the server running PrintPilotProxy (e.g., `192.168.10.10`).
2.  **Proxy Port**: Set this to the port PrintPilotProxy is listening on (default is `3128`).
3.  **Protocols**: PrintPilotProxy supports both HTTP and HTTPS proxying. HTTPS is achieved using the HTTP `CONNECT` method, ensuring secure, end-to-end encrypted communication between the PrintPilot client and the external PrintPilot Cloud servers.

## Network & Firewall Requirements

For the integration to work successfully:

*   **Client to Proxy**: The PrintPilot client PC must be able to reach the Proxy Server on the configured port (e.g., TCP 3128). Ensure any local or network firewalls between the client and proxy allow this traffic.
*   **Proxy to Internet**: The Proxy Server must have outbound internet access to reach PrintPilot Cloud services (typically TCP ports 80 and 443).
*   **Allowed Clients**: The IP address of the PrintPilot client must be explicitly added to the "Allowed Clients" ACL in PrintPilotProxy.

## Important Note Regarding Email

**PrintPilotProxy does NOT handle email content.**

PrintPilotProxy is strictly a forward HTTP/HTTPS proxy. It routes web traffic (API calls, web socket connections, etc.) between the PrintPilot client and the cloud. It does not act as an SMTP relay, POP3/IMAP proxy, or inspect email payloads. Any email-related functionalities (such as scan-to-email) require separate network configurations or mail servers depending on your specific setup.
