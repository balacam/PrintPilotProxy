# Linux Support

The service host, CLI and proxy engine run on Linux (x64 and arm64) under systemd. macOS builds and runs the same
headless binaries, but ships no service definition (see [Not covered](#not-covered)). The WPF management app is
Windows-only; on Linux the proxy is managed with the CLI and by editing `config.json`.

## What is the same

The proxy behaviour does not change: deny-by-default client ACL, restricted destination ports, optional
`PrintPilot-HMAC` auth, no TLS interception, UDP discovery on 37421, TCP listener on 3128.

## Differences from Windows

| Area | Windows | Linux |
|---|---|---|
| Config | `C:\ProgramData\PrintPilotProxy\config.json` | `/etc/printpilotproxy/config.json` |
| Data | `C:\ProgramData\PrintPilotProxy\data` | `/var/lib/printpilotproxy` |
| Logs | `...\logs\service.log` | `/var/log/printpilotproxy/service.log` |
| Service host | Windows Service (`sc`) | systemd unit `printpilotproxy.service` |
| Management channel | Named pipe `PrintPilotProxy` (ACL: SYSTEM, Administrators, authenticated users) | Unix socket `/run/printpilotproxy/ipc.sock`, mode `0660` |
| Secret protection | DPAPI, machine scope | AES-256-GCM, key in `/var/lib/printpilotproxy/secret.key` (`0600`) |
| Private files | ACL limited to SYSTEM/Administrators | mode `0600` (files) / `0700` (folders) |
| Firewall | Managed through Windows Firewall | Not managed; open the ports yourself |

`PRINTPILOTPROXY_ROOT` moves config, data and logs under one folder (for development without root), and
`PRINTPILOTPROXY_IPC_SOCKET` sets the socket path.

### Management channel and who may use it

On Windows the pipe server asks the OS who the caller is. On Linux the kernel decides before the service sees the
connection: the socket is `0660`, so only the service account, root and members of the `printpilotproxy` group can
connect, and every connected peer is treated as an administrator. Keep that group small. The
`Ipc:RequireAdministrator` setting therefore has no effect on Linux.

### Secrets

`secret.key` plays the role of the DPAPI machine key. Back it up together with `config.json`; without it the
stored upstream-proxy password cannot be decrypted and must be entered again. A `config.json` copied from Windows
carries a DPAPI value that is unreadable on Linux for the same reason.

## Install

```bash
# Self-contained build; replace linux-x64 with linux-arm64 where needed.
dotnet publish src/PrintPilotProxy.Service -c Release -r linux-x64 --self-contained -o /opt/printpilotproxy
dotnet publish src/PrintPilotProxy.Cli     -c Release -r linux-x64 --self-contained -o /opt/printpilotproxy/cli

sudo install -m 0644 deploy/linux/printpilotproxy.sysusers /usr/lib/sysusers.d/printpilotproxy.conf
sudo systemd-sysusers
sudo install -m 0644 deploy/linux/printpilotproxy.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now printpilotproxy
```

The first start writes a default `config.json`. Client access defaults to `AllowList`, so no client is accepted until
its address is listed in the configuration.

## Firewall

Open TCP 3128 (proxy) and UDP 37421 (discovery) for the client network only, with whatever your distribution uses:

```bash
sudo ufw allow from 10.0.0.0/24 to any port 3128 proto tcp
sudo ufw allow from 10.0.0.0/24 to any port 37421 proto udp
# firewalld: sudo firewall-cmd --permanent --add-rich-rule='rule family=ipv4 source address=10.0.0.0/24 port port=3128 protocol=tcp accept'
```

## Operate

```bash
systemctl status printpilotproxy
journalctl -u printpilotproxy -f
/opt/printpilotproxy/cli/PrintPilotProxy.Cli status   # run as root or as a member of the printpilotproxy group; also: stop, validate, version
```

## Not covered

- macOS service definition (launchd) and installer. Paths exist (`/Library/Application Support/PrintPilotProxy`), but
  the IPC socket falls back to a temporary folder and no plist is provided.
- Managing the Linux firewall from the application.
- A graphical management UI (the WPF app stays Windows-only; an Avalonia port is a separate decision).
- `.deb` / `.rpm` packages. The steps above are the supported install.
