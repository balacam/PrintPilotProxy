# PrintPilotProxy — Installation Guide

PrintPilotProxy provides two release options for 64-bit Windows systems (Windows 10 / 11 and Windows Server 2016+):

1. **Windows Installer (Recommended)**: `PrintPilotProxy-0.1.0-x64.msi`
2. **Portable ZIP Archive**: `PrintPilotProxy-0.1.0-win-x64.zip`

---

## 1. Standard Windows Installer (`PrintPilotProxy-0.1.0-x64.msi`)

### Prerequisites
* 64-bit Windows (x64)
* Administrator privileges (for service installation and firewall rule creation)

### Step-by-Step Installation
1. Download `PrintPilotProxy-0.1.0-x64.msi` from the official release page.
2. Double-click `PrintPilotProxy-0.1.0-x64.msi` to launch the Windows Installer setup wizard.
3. Accept the User Account Control (UAC) prompt when requested.
4. Follow the setup wizard to accept the license agreement and select the destination directory.
5. The installer will automatically:
   * Install application binaries to `C:\Program Files\PrintPilotProxy\`
   * Create application data directories in `C:\ProgramData\PrintPilotProxy\`
   * Register and start the `PrintPilotProxy` Windows Service automatically (`Automatic` startup)
   * Create a Start Menu shortcut named **PrintPilotProxy** targeting the WPF Administration UI.

### Quiet / Unattended Installation (Sysadmins)
To install silently across multiple domain computers:
```cmd
msiexec.exe /i PrintPilotProxy-0.1.0-x64.msi /qn /l*v install.log
```

---

## 2. Portable Distribution (`PrintPilotProxy-0.1.0-win-x64.zip`)

For advanced users or portable testing environments:

1. Extract `PrintPilotProxy-0.1.0-win-x64.zip` to a directory of your choice.
2. The package includes self-contained executables for:
   * **WPF App**: `bin\App\PrintPilotProxy.App.exe`
   * **Service**: `bin\Service\PrintPilotProxy.Service.exe`
   * **CLI**: `bin\Cli\PrintPilotProxy.Cli.exe`

### Running the Windows Service manually from Portable Package
To register the service from the portable folder using an elevated Command Prompt or PowerShell:
```cmd
sc.exe create PrintPilotProxy binPath= "C:\path\to\bin\Service\PrintPilotProxy.Service.exe" start= auto
sc.exe start PrintPilotProxy
```

---

## 3. Directory Layout & Storage

| Path | Purpose |
| :--- | :--- |
| `C:\Program Files\PrintPilotProxy\` | Executables, native libraries, and application manifests. |
| `C:\ProgramData\PrintPilotProxy\` | Persistent configuration (`config.json`), ACL settings, and runtime state. |
| `C:\ProgramData\PrintPilotProxy\logs\` | Application and proxy log files (`PrintPilotProxy-*.log`). |
| `C:\ProgramData\PrintPilotProxy\backups\` | Configuration backup snapshots created during updates or configuration resets. |

---

## 4. SHA256 Verification

To verify the integrity of downloaded release packages, compare file hashes against `SHA256SUMS.txt`:

```powershell
Get-FileHash PrintPilotProxy-0.1.0-x64.msi -Algorithm SHA256
```
