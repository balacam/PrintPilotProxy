# Build & Packaging Guide

## Building the Project
You can build the project using the .NET CLI or Visual Studio.

```bash
# Clone the repository
git clone https://github.com/PrintPilotProxy/PrintPilotProxy.git
cd PrintPilotProxy

# Restore dependencies
dotnet restore

# Build the entire solution
dotnet build -c Release
```

## Packaging
The official releases are packaged in two formats:
1. **Windows Installer (.msi)**: Handled by the `PrintPilotProxy.Installer` project (WiX Toolset).
2. **Portable ZIP Archive**: Contains the published binaries for App, Cli, and Service.

To publish the self-contained binaries:
```bash
dotnet publish src/PrintPilotProxy.App -c Release -r win-x64 --self-contained true -o publish/App
dotnet publish src/PrintPilotProxy.Service -c Release -r win-x64 --self-contained true -o publish/Service
dotnet publish src/PrintPilotProxy.Cli -c Release -r win-x64 --self-contained true -o publish/Cli
```
