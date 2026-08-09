# Windows Operations

## Windows Service
PrintPilotProxy runs natively as a Windows Service (`Microsoft.Extensions.Hosting.WindowsServices`). It handles auto-restarts and headless execution.

## Windows Firewall
The UI can automatically configure Windows Firewall to allow incoming traffic on the configured proxy port.

## Troubleshooting
Logs are located in `C:\ProgramData\PrintPilotProxy\Logs\`.
Check the Event Viewer if the service fails to start.
