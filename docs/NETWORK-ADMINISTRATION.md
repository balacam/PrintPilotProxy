# Network Administration Guide

This guide details the network topology, routing, and firewall requirements for deploying PrintPilotProxy.

## Example Network Topology

*Please note: The following IP addresses are for example purposes only.*

*   **PrintPilot PC**: `192.168.10.50` (The client requiring internet access)
*   **Proxy Server**: `192.168.10.10` (The server running PrintPilotProxy)
*   **Proxy Port**: `3128` (The TCP port PrintPilotProxy listens on)
*   **Allowed Client**: `192.168.10.50` (Configured in PrintPilotProxy's ACL)

## Routing and Firewall

In a restricted network, you must ensure traffic can flow between the necessary components while maintaining security.

1.  **PrintPilot PC to Proxy Server**:
    *   **Routing**: The PrintPilot PC (`192.168.10.50`) must have a valid route to the Proxy Server (`192.168.10.10`).
    *   **Firewall (Internal)**: Any internal firewalls must allow inbound TCP traffic from `192.168.10.50` to `192.168.10.10` on port `3128`.
2.  **Proxy Server to Internet**:
    *   **Routing**: The Proxy Server (`192.168.10.10`) requires a route to the public internet (usually via a default gateway).
    *   **Firewall (Edge)**: The perimeter firewall must allow outbound TCP traffic from `192.168.10.10` to the internet on required destination ports (typically `80` and `443`).
3.  **PrintPilot PC to Internet**:
    *   **Firewall (Edge)**: For maximum security, the perimeter firewall should **deny** direct outbound internet access from the PrintPilot PC (`192.168.10.50`). All traffic should be forced through the Proxy Server.

## Access Control Lists (ACL)

PrintPilotProxy uses an internal ACL to authorize clients.
*   By default, all incoming connections are denied.
*   You must explicitly add the PrintPilot PC's IP (`192.168.10.50` in our example) to the Allowed Clients list.

## DNS Resolution

*   **PrintPilot PC**: The client PC does *not* necessarily need public DNS resolution if it's configured to use the proxy. It only needs to resolve the IP of the Proxy Server (if a hostname is used instead of an IP).
*   **Proxy Server**: The Proxy Server *must* have functioning DNS resolution to look up the public IP addresses of PrintPilot Cloud services (e.g., via internal DNS servers that forward to public resolvers).

## HTTPS CONNECT and Destination Ports

PrintPilotProxy handles secure traffic using the HTTP `CONNECT` method.

*   **HTTPS CONNECT**: When the PrintPilot PC needs to establish a secure (HTTPS) connection, it sends a `CONNECT` request to the Proxy Server. The proxy then establishes a blind TCP tunnel to the destination. The traffic remains end-to-end encrypted; PrintPilotProxy does not intercept or decrypt the TLS payload.
*   **Destination Ports**: To prevent abuse (e.g., tunneling SSH or other protocols through the proxy), PrintPilotProxy restricts the destination ports it will connect to. By default, it will typically only allow connections to standard web ports like `80` (HTTP) and `443` (HTTPS). You should configure the allowed destination ports to match the exact requirements of the PrintPilot Cloud services.

## Network Auto-Discovery (V2)
PrintPilotProxy uses an Auto-discovery mechanism to find local private IPv4 addresses (RFC1918 blocks: 10.x.x.x, 172.16.x.x, 192.168.x.x) and binds exclusively to those. It migrates v1 127.0.0.1 setups to SpecificAddress to preserve legacy security.

