# PrintPilotProxy — Uninstall Guide

PrintPilotProxy provides a clean uninstallation procedure that safely removes application binaries, registered Windows services, shortcuts, and firewall rules.

---

## 1. Standard Uninstall (Settings / Control Panel)

1. Open **Settings** on Windows (or **Control Panel** -> **Programs and Features**).
2. Go to **Apps** -> **Installed Apps**.
3. Locate **PrintPilotProxy** in the list.
4. Click the options menu (`...`) and select **Uninstall**.
5. Confirm Administrator elevation (UAC) when prompted.

---

## 2. Command-Line Uninstall

To perform a quiet uninstallation via Command Prompt or PowerShell (elevated):

```cmd
msiexec.exe /x PrintPilotProxy-0.1.0-x64.msi /qn /l*v uninstall.log
```

Alternatively, uninstall using the Product Code:
```cmd
msiexec.exe /x {23B78834-C90D-4BF7-B0BA-7EB64719778B} /qn
```

---

## 3. What Gets Removed vs Preserved

### Removed during Uninstall:
* `C:\Program Files\PrintPilotProxy\` (All binaries, libraries, and app files)
* **PrintPilotProxy** Windows Service registration (`sc.exe delete PrintPilotProxy`)
* Start Menu shortcut (`PrintPilotProxy`)
* Managed Windows Firewall rule (`PrintPilotProxy-Inbound`)
* `C:\ProgramData\PrintPilotProxy\` directory (configuration, logs, backups)

> [!WARNING]
> Uninstalling PrintPilotProxy removes configuration files and log histories stored in `C:\ProgramData\PrintPilotProxy\`. If you wish to retain configuration, back up `C:\ProgramData\PrintPilotProxy\config.json` before uninstalling.

### Untouched Resources:
* Unrelated Windows Services
* Unrelated Firewall rules belonging to other applications
* System proxy settings or network adapter configurations
