# PrintPilotProxy

Secure forward HTTP CONNECT proxy for PrintPilot clients on restricted networks (hospitals, banks, intranets). .NET 8, Windows Service + WPF management app. Read `PROJECT_CONTEXT.md` first, then `docs/` (`ARCHITECTURE.md`, `SECURITY.md`, `COMMUNICATION.md`).

## Commands

```powershell
# Always name the solution: the repo may also contain a .slnx, which causes MSB1011 otherwise.
dotnet restore PrintPilotProxy.sln
dotnet build PrintPilotProxy.sln --configuration Release --no-restore
dotnet test PrintPilotProxy.sln --configuration Release --no-build
```

There is no CI in this repo (the workflow was removed on 2026-10-18 because GitHub Actions did not start on this account); run the commands above locally before pushing. Packaging and release: `docs/BUILD.md`, `RELEASE.md`, `src/PrintPilotProxy.Installer`.

## Layout

`src/`: `Core` (platform-neutral domain), `Proxy` (engine wrapping Unobtanium Web Proxy), `Infrastructure` (IPC, I/O, security), `Service` (Windows Service host), `App` (WPF UI), `Cli`, `Installer`. Tests are in `tests/PrintPilotProxy.*.Tests`. The App talks to the Service over Named Pipes. Config lives in `C:\ProgramData\PrintPilotProxy\config.json`.

## Rules that are easy to get wrong

- Deny-by-default: client IP must be in the ACL, destination ports are restricted, optional `PrintPilot-HMAC` auth in `Proxy-Authorization`. Never add TLS interception (no MITM); the proxy only tunnels.
- Clients find the proxy by UDP broadcast on 37421 and connect on TCP 3128. Changing either breaks deployed PrintPilot clients.
- The proxy is a transport only. Authentication, licensing and device enrollment belong to PrintPilotServer / `alacam.it`.
- `.agents/rules/documentation-maintenance.md` applies: update `docs/` or `PROJECT_CONTEXT.md` with behaviour changes. `DOCUMENTATION_AUDIT.md` records known doc drift.
- `docs/archive/` holds one-off history (e.g. the 0.1.0 release note); `CHANGELOG.md` is the current change record.
