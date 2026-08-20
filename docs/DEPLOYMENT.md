# Deployment Guide

This document details the installation, upgrade, and uninstallation procedures for PrintPilotProxy.

## 1. Installation

PrintPilotProxy provides two release options for 64-bit Windows systems (Windows 10 / 11 and Windows Server 2016+):

### 1.1 Standard Windows Installer (`.msi`)

**Prerequisites:**
* 64-bit Windows (x64)
* Administrator privileges

**Step-by-Step Installation:**
1. Download `PrintPilotProxy-<version>-x64.msi` from the official release page.
2. Double-click the MSI to launch the Windows Installer setup wizard.
3. Accept the User Account Control (UAC) prompt when requested.
4. Follow the setup wizard to accept the license agreement.
5. The installer will automatically:
   * Install application binaries to `C:\Program Files\PrintPilotProxy\`
   * Create application data directories in `C:\ProgramData\PrintPilotProxy\`
   * Register and start the `PrintPilotProxy` Windows Service automatically
   * Create a Start Menu shortcut named **PrintPilotProxy** targeting the WPF Administration UI.

**Quiet / Unattended Installation (Sysadmins):**
```cmd
msiexec.exe /i PrintPilotProxy-<version>-x64.msi /qn /l*v install.log
```

### 1.2 Portable Distribution (`.zip`)

For advanced users or portable testing environments:

1. Extract `PrintPilotProxy-<version>-win-x64.zip` to a directory of your choice.
2. The package includes self-contained executables for the App, Service, and CLI.

**Running the Windows Service manually from Portable Package:**
```cmd
sc.exe create PrintPilotProxy binPath= "C:\path\to\bin\Service\PrintPilotProxy.Service.exe" start= auto
sc.exe start PrintPilotProxy
```

### 1.3 Directory Layout & Storage

| Path | Purpose |
| :--- | :--- |
| `C:\Program Files\PrintPilotProxy\` | Executables, native libraries, and application manifests. |
| `C:\ProgramData\PrintPilotProxy\` | Persistent configuration (`config.json`), ACL settings, and runtime state. |
| `C:\ProgramData\PrintPilotProxy\logs\` | Application and proxy log files (`PrintPilotProxy-*.log`). |
| `C:\ProgramData\PrintPilotProxy\backups\` | Configuration backup snapshots created during updates or configuration resets. |

### 1.4 SHA256 Verification

To verify the integrity of downloaded release packages, compare file hashes against `SHA256SUMS.txt`:
```powershell
Get-FileHash PrintPilotProxy-<version>-x64.msi -Algorithm SHA256
```

---

## 2. Upgrade Guide

PrintPilotProxy supports seamless in-place major and minor upgrades without deleting persistent application settings or log histories.

### 2.1 Upgrading via Windows Installer (`.msi`)

1. Download the new `PrintPilotProxy-<version>-x64.msi` installer.
2. Launch the installer. Windows Installer automatically detects the existing installation.
3. Accept the UAC prompt to proceed with the upgrade.
4. The setup wizard will perform an in-place upgrade:
   * Stops the active `PrintPilotProxy` Windows Service safely.
   * Updates application binaries in `C:\Program Files\PrintPilotProxy\`.
   * Restarts the updated `PrintPilotProxy` Windows Service automatically.

### 2.2 Configuration & Data Preservation Policy

During an upgrade, **PrintPilotProxy strictly preserves all data** in `C:\ProgramData\PrintPilotProxy\`:
* **`config.json`**: Machine proxy settings, IP/CIDR ACL rules, destination port restrictions, listener mode selection, and startup preferences remain unchanged.
* **`logs\`**: Log histories are preserved.
* **`backups\`**: Configuration backups are retained.
* **Language Preferences**: UI language settings selected in the WPF application persist across updates.

> [!NOTE]
> Major upgrades do **NOT** execute data directory deletion logic (`RemoveFolder`). Persistent configurations remain intact across version transitions.

### 2.3 Configuration Backup & Migration Safety

Before applying major schema migrations, PrintPilotProxy creates an automatic configuration snapshot in `C:\ProgramData\PrintPilotProxy\backups\config_backup_<timestamp>.json`.
If a configuration migration failure occurs, the service automatically falls back to the last valid snapshot.

---

## 3. Uninstall Guide

PrintPilotProxy provides a clean uninstallation procedure that safely removes application binaries, registered Windows services, shortcuts, and firewall rules.

### 3.1 Standard Uninstall (Settings / Control Panel)

1. Open **Settings** on Windows (or **Control Panel** -> **Programs and Features**).
2. Go to **Apps** -> **Installed Apps**.
3. Locate **PrintPilotProxy** in the list.
4. Click the options menu (`...`) and select **Uninstall**.
5. Confirm Administrator elevation (UAC) when prompted.

### 3.2 Command-Line Uninstall

To perform a quiet uninstallation via Command Prompt or PowerShell (elevated):
```cmd
msiexec.exe /x PrintPilotProxy-<version>-x64.msi /qn /l*v uninstall.log
```
Alternatively, uninstall using the Product Code:
```cmd
msiexec.exe /x {23B78834-C90D-4BF7-B0BA-7EB64719778B} /qn
```

### 3.3 What Gets Removed vs Preserved

**Removed during Uninstall:**
* `C:\Program Files\PrintPilotProxy\` (All binaries, libraries, and app files)
* **PrintPilotProxy** Windows Service registration (`sc.exe delete PrintPilotProxy`)
* Start Menu shortcut (`PrintPilotProxy`)
* Managed Windows Firewall rule (`PrintPilotProxy-Inbound`)
* `C:\ProgramData\PrintPilotProxy\` directory (configuration, logs, backups)

> [!WARNING]
> Uninstalling PrintPilotProxy removes configuration files and log histories stored in `C:\ProgramData\PrintPilotProxy\`. If you wish to retain configuration, back up `C:\ProgramData\PrintPilotProxy\config.json` before uninstalling.

**Untouched Resources:**
* Unrelated Windows Services
* Unrelated Firewall rules belonging to other applications
* System proxy settings or network adapter configurations
