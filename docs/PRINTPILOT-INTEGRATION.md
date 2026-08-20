# PrintPilot Integration Guide

This guide explains how to integrate a PrintPilot client with PrintPilotProxy.

## Proxy Configuration in PrintPilot

To route PrintPilot traffic through PrintPilotProxy, you need to configure the proxy settings within your PrintPilot client.

1.  **Auto-Discovery**: PrintPilot clients can automatically discover PrintPilotProxy on the local network using UDP broadcast on port `37421`.
2.  **Manual Proxy Host**: Set this to the IP address or hostname of the server running PrintPilotProxy (e.g., `192.168.10.10`).
3.  **Proxy Port**: Set this to the port PrintPilotProxy is listening on (default is `3128`).
4.  **Protocols & Tunneling**: PrintPilotProxy supports HTTP and HTTPS/TLS tunneling using the HTTP `CONNECT` method, enabling secure, end-to-end encrypted proxying between PrintPilot clients and the destination SMTP Server / Mail Relay.

## Network & Firewall Requirements

For the integration to work successfully:

*   **Client to Proxy (LAN)**: The PrintPilot client PC must be able to reach the Proxy Server on the configured proxy port (TCP `3128`) and discovery port (UDP `37421`). Ensure internal network and host firewalls permit this traffic.
*   **Proxy to SMTP / External**: The Proxy Server must have outbound network access to reach the target SMTP Server (e.g., ports 25, 465, 587 or custom).
*   **Allowed Clients**: The IP address of the PrintPilot client must be explicitly allowed in PrintPilotProxy's ACL (or set to Allow All mode).
