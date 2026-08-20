# Troubleshooting Guide

This guide helps diagnose and resolve common issues encountered when running or connecting to PrintPilotProxy.

## Service Fails to Start

**Symptom**: The PrintPilotProxy background service crashes on startup or refuses to start.

- **Possible Cause**: Port collision.
  - **Diagnosis**: Check the service logs located at `C:\ProgramData\PrintPilotProxy\logs\service.log`. Look for `System.Net.Sockets.SocketException: Only one usage of each socket address is permitted`.
  - **Resolution**: Open `config.json` and change `Listener.Port` to an available port, or stop the conflicting application.

- **Possible Cause**: Invalid configuration file.
  - **Diagnosis**: Check the service logs for JSON parsing errors or `ConfigurationValidator` failure messages.
  - **Resolution**: Ensure `C:\ProgramData\PrintPilotProxy\config.json` is valid JSON and adheres to the schema.

- **Possible Cause**: Insufficient permissions.
  - **Diagnosis**: The service cannot read/write to `C:\ProgramData\PrintPilotProxy`.
  - **Resolution**: Ensure the `LocalSystem` account (or whichever account the service runs under) has Full Control over the `C:\ProgramData\PrintPilotProxy` directory.

## Client Connection Timeout

**Symptom**: PrintPilot clients experience timeouts when attempting to connect to the external SMTP server via the proxy.

- **Possible Cause**: Windows Firewall is blocking the proxy port.
  - **Diagnosis**: Ensure `Firewall.RuleEnabled` is `true` in the configuration. Check Windows Defender Firewall inbound rules for "PrintPilotProxy".
  - **Resolution**: Toggle the Firewall switch in the Management App, or manually create an inbound TCP rule for port `3128`.

- **Possible Cause**: Client IP is not in the ACL.
  - **Diagnosis**: Open the Management App and check the "Recent Requests" log. Look for `HTTP 403` errors with the message "Access denied by ACL".
  - **Resolution**: Add the client's IP address to the `AllowedClients` list in the configuration.

- **Possible Cause**: Proxy Server lacks outbound internet access.
  - **Diagnosis**: The client connects to the proxy successfully, but the proxy cannot reach the destination. The Management App logs will show proxy errors.
  - **Resolution**: Check the perimeter firewall rules for the Proxy Server to ensure it can reach the destination SMTP ports (e.g., TCP 25, 465, 587).

## Proxy Authentication Failure

**Symptom**: Client receives `HTTP 407 Proxy Authentication Required`.

- **Possible Cause**: Clock skew between Client and Proxy.
  - **Diagnosis**: The HMAC authentication scheme relies on timestamps. If the client PC and Proxy Server clocks differ by more than 5 minutes, authentication will fail. Check the proxy logs for "Clock skew exceeded".
  - **Resolution**: Sync the clocks on both the PrintPilot Client PC and the PrintPilotProxy Server using an NTP server.

- **Possible Cause**: Invalid Client Version.
  - **Diagnosis**: An older PrintPilot client might be using an incompatible HMAC protocol version.
  - **Resolution**: Upgrade the PrintPilot client to match the proxy's expected protocol version (Version 1).

## Management App Cannot Connect to Service

**Symptom**: Opening the WPF Management App shows a "Disconnected" state or fails to load settings.

- **Possible Cause**: The background service is not running.
  - **Diagnosis**: Open `services.msc` and check the status of "PrintPilotProxy".
  - **Resolution**: Start the service.

- **Possible Cause**: IPC Named Pipe failure.
  - **Diagnosis**: The app logs (if any) show `TimeoutException` when trying to connect to the named pipe.
  - **Resolution**: Restart the "PrintPilotProxy" Windows Service. Ensure no other instances of the App are hanging in the background.
