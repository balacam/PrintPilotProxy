# PrintPilotProxy v0.1.0 Release Notes

**PrintPilotProxy v0.1.0** is an enterprise-ready forward proxy application for Windows designed for secure local network print/proxy control.

---

## Key Highlights & Features

### 1. Robust Forward Proxy Engine
* Powered by `Unobtanium.Web.Proxy`.
* **Blind Tunneling (HTTPS CONNECT)**: Strict passthrough without TLS decryption or certificate generation.
* Machine-independent listening modes: `Auto` (automatic adapter discovery), `SpecificAdapter`, `SpecificAddress`, and `AllInterfaces`.
* IP / CIDR ACL controls and destination port filtering rules.

### 2. Multi-Language Support (13 Languages)
* Full support for 13 UI languages:
  1. English (`en-US`)
  2. Deutsch (`de-DE`)
  3. Français (`fr-FR`)
  4. Español (`es-ES`)
  5. Português (`pt-BR`)
  6. Italiano (`it-IT`)
  7. Nederlands (`nl-NL`)
  8. Türkçe (`tr-TR`)
  9. Polski (`pl-PL`)
  10. Română (`ro-RO`)
  11. Български (`bg-BG`)
  12. Čeština (`cs-CZ`)
  13. Svenska (`sv-SE`)
* System default culture auto-detection with runtime language switching.

### 3. IPC Security Architecture
* Windows Named Pipe management channel (`PrintPilotProxy`) with kernel-enforced ACLs allowing access exclusively to `LocalSystem` and `BuiltinAdministrators`.
* Strongly-typed JSON IPC messaging format with payload size validation.

### 4. WPF Administration GUI
* Modern WPF interface featuring Dashboard, Proxy Settings, Allowed Clients, Security, Firewall, Service Control, Logs, Diagnostics, and Language pages.

### 5. Automated Windows Service & WiX Installer
* Production-ready WiX Toolset v5 x64 Windows Installer (`PrintPilotProxy-0.1.0-x64.msi`).
* Automatic service installation and startup (`PrintPilotProxy` service).
* Portable zip package (`PrintPilotProxy-0.1.0-win-x64.zip`) for advanced environments.

---

## Release Artifacts & SHA-256 Checksums

| File | Description | SHA-256 Checksum |
| :--- | :--- | :--- |
| `PrintPilotProxy-0.1.0-x64.msi` | Windows Installer Package (x64) | `dd5c7917092815ae1f5a9cfd8ebb1cadb5a10311307892b84750ca5ebda3c1fe` |
| `PrintPilotProxy-0.1.0-win-x64.zip` | Portable Zip Archive (x64) | `47ddb49aa33e5f3e83645a9df77de814dc24b1d2bc613ddd38e20c684ebd05ab` |
| `SHA256SUMS.txt` | Cryptographic Checksums | - |
