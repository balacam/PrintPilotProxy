# PrintPilotProxy — Upgrade & Update Guide

PrintPilotProxy supports seamless in-place major and minor upgrades without deleting persistent application settings or log histories.

---

## 1. Upgrading via Windows Installer (`.msi`)

When upgrading to a new build or minor release:

1. Download the new `PrintPilotProxy-<version>-x64.msi` installer.
2. Launch the installer. Windows Installer automatically detects the existing installation.
3. Accept the UAC prompt to proceed with the upgrade.
4. The setup wizard will perform an in-place upgrade:
   * Stops the active `PrintPilotProxy` Windows Service safely.
   * Updates application binaries in `C:\Program Files\PrintPilotProxy\`.
   * Restarts the updated `PrintPilotProxy` Windows Service automatically.

---

## 2. Configuration & Data Preservation Policy

During an upgrade, **PrintPilotProxy strictly preserves all data** in `C:\ProgramData\PrintPilotProxy\`:

* **`config.json`**: Machine proxy settings, IP/CIDR ACL rules, destination port restrictions, listener mode selection, and startup preferences remain unchanged.
* **`logs\`**: Log histories are preserved.
* **`backups\`**: Configuration backups are retained.
* **Language Preferences**: UI language settings selected in the WPF application persist across updates.

> [!NOTE]
> Major upgrades do **NOT** execute data directory deletion logic (`RemoveFolder`). Persistent configurations remain intact across version transitions.

---

## 3. Configuration Backup & Migration Safety

Before applying major schema migrations, PrintPilotProxy creates an automatic configuration snapshot in `C:\ProgramData\PrintPilotProxy\backups\config_backup_<timestamp>.json`.
If a configuration migration failure occurs, the service automatically falls back to the last valid snapshot.
