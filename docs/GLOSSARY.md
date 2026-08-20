# Glossary

This document defines important terminology specific to the PrintPilotProxy project. 
Using consistent terminology is essential for both human readability and AI / NotebookLM integration.

## PrintPilotProxy Components

*   **PrintPilotProxy Server**: The machine (physical or virtual) that hosts the proxy service.
*   **Proxy Engine**: The core component responsible for handling network traffic, validating ACLs, and tunneling data (implemented using Unobtanium).
*   **Management App**: The WPF-based graphical user interface (`PrintPilotProxy.App`) used to configure the proxy and view its status.
*   **Service Host**: The background Windows Service (`PrintPilotProxy.Service`) that keeps the Proxy Engine running without requiring a user to be logged in.

## Network & Security Terms

*   **Access Control List (ACL)**: The list of explicitly authorized client IP addresses or CIDR blocks allowed to connect to the proxy.
*   **Auto-Discovery**: The UDP-based mechanism (port 37421) by which PrintPilot clients automatically find the PrintPilotProxy on a local network.
*   **HTTP CONNECT**: The standard HTTP method used to establish a blind, two-way TCP tunnel through the proxy to a destination server. This allows secure TLS traffic to pass through unmodified.
*   **MITM (Man-In-The-Middle)**: A technique where a proxy decrypts and inspects TLS traffic. PrintPilotProxy explicitly **does not** perform MITM; it provides end-to-end encryption.
*   **PrintPilot-HMAC**: The zero-touch cryptographic authentication scheme used to verify that connections come from genuine PrintPilot software, preventing abuse by other software on the same allowed IP address.
*   **Inter-Process Communication (IPC)**: The mechanism (Named Pipes) used for communication between the Management App (running in user space) and the Service Host (running in system space).
