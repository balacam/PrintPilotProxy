# Development Guide

This guide explains how to set up the development environment, build the project, and run it locally.

## Prerequisites
- **.NET 8.0 SDK** or later.
- **Visual Studio 2022** (recommended for WPF support) or JetBrains Rider.
- **Windows 10/11** (required for the WPF App and Windows Service testing).

## Solution Structure
- `PrintPilotProxy.sln`: The main solution file encompassing all projects.

## Building the Project
Please see [BUILD.md](BUILD.md) for instructions on compiling the project and creating release packages.

## Running Locally

Because PrintPilotProxy uses a Windows Service and IPC, running it locally requires starting multiple components.

### 1. Run the Service (Background)
For debugging, you can run the Service as a console application instead of installing it as a Windows Service.
```bash
cd src/PrintPilotProxy.Service
dotnet run
```
*Note: The Service requires administrative privileges to modify Windows Firewall rules and bind to certain IPs. Run your terminal or IDE as Administrator.*

### 2. Run the Management App (UI)
While the Service is running, open a new terminal to start the UI.
```bash
cd src/PrintPilotProxy.App
dotnet run
```
The App will connect to the running Service via Named Pipes.

## Running Tests
Please see [TESTING.md](TESTING.md) for details on running unit and integration tests.

## Code Style & Guidelines
- The project follows standard C# naming conventions.
- Platform-specific logic (Windows registry, named pipes, firewall) must be isolated in `PrintPilotProxy.Infrastructure`.
- Core business logic, configuration models, and security validation must reside in `PrintPilotProxy.Core` without external dependencies.
- **Dependency Injection**: Services are registered in `InfrastructureServiceExtensions.cs` and `ProxyServiceExtensions.cs`.
