# Testing Guide

Unit and integration tests are located in the `tests/` directory.

## Running All Tests
To execute all test suites across the solution, use the .NET CLI targeting the solution file:

```bash
dotnet test PrintPilotProxy.sln
```

> [!IMPORTANT]
> **AI & Developer Note (MSB1011 Ambiguity)**:
> You must explicitly specify `PrintPilotProxy.sln` (e.g., `dotnet test PrintPilotProxy.sln`). Because the directory contains both `PrintPilotProxy.sln` and `PrintPilotProxy.slnx`, running bare `dotnet test` will fail with MSBuild error `MSB1011: Specify which project or solution file to use`.

## Running Specific Test Suites
You can run individual test projects independently:

```bash
# Core Domain & Model Tests (Platform-agnostic, fast)
dotnet test tests/PrintPilotProxy.Core.Tests/PrintPilotProxy.Core.Tests.csproj

# Proxy Engine & ACL Tests
dotnet test tests/PrintPilotProxy.Proxy.Tests/PrintPilotProxy.Proxy.Tests.csproj

# Infrastructure, HMAC, IPC & Network Tests
dotnet test tests/PrintPilotProxy.Infrastructure.Tests/PrintPilotProxy.Infrastructure.Tests.csproj

# WPF Management App & ViewModel Tests
dotnet test tests/PrintPilotProxy.App.Tests/PrintPilotProxy.App.Tests.csproj
```

## Test Suite Coverage
- **`PrintPilotProxy.Core.Tests`**: Validates configuration models, CIDR / IP ACL parsing, configuration validation, and security auditing rules.
- **`PrintPilotProxy.Proxy.Tests`**: Validates Unobtanium proxy engine lifecycle, connection tunneling, and client/port ACL enforcement.
- **`PrintPilotProxy.Infrastructure.Tests`**: Validates zero-touch HMAC authentication (clock skew, replay, signature mismatch), named pipe IPC security, DPAPI data protection, and UDP discovery rate limiting.
- **`PrintPilotProxy.App.Tests`**: Validates WPF MVVM view models, log parsing, and navigation routes. Note that tests involving WPF views require a Windows STA (Single-Threaded Apartment) thread.

