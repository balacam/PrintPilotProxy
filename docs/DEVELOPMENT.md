# Developer Guide

## Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or JetBrains Rider

## Building and Running
1. Clone the repository.
2. Run `dotnet restore`.
3. Run `dotnet build`.
4. Start the UI: `dotnet run --project src/PrintPilotProxy.UI`
5. Run tests: `dotnet test`

## Architecture Overview
The project is split into Core, Proxy, Service, UI, and CLI. Please see `ARCHITECTURE.md` for details.
