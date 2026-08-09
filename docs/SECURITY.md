# Security Model

PrintPilotProxy is designed to act as a secure gateway for internal clients.

## Threat Model
The proxy is NOT meant to be exposed to the public internet. It must run within an internal network.

## ACL & CIDR
You must explicitly whitelist client IP addresses or CIDR ranges. Deny-by-default is enforced.

## HTTPS INTERCEPTION
HTTPS interception (MITM) is **disabled**. The proxy uses HTTP CONNECT tunneling, meaning the encrypted traffic flows through unchanged. This preserves end-to-end encryption.
