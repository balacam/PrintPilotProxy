# Current Proxy context for assistants

## What runs

The Windows service owns the engine and OS actions. WPF App/CLI call it through the local named pipe. TCP 3128 accepts CONNECT tunnels and plain HTTP requests; UDP 37421 advertises the Proxy. TLS interception is disabled. HTTPS confidentiality is supplied by the client-to-destination TLS session; forwarding plain HTTP/SMTP does not add encryption.

Proxy is optional for PrintPilot/Server outbound Internet access. It does not own account login, licences, pairing or mailbox consent. Current OldOS clients contact Server directly (`Proxy = null`) and do not configure this proxy themselves. Server and Proxy share discovery port 37421 but use distinct service messages; do not merge their protocols.

## Defaults are context dependent

- `JsonConfigurationManager.LoadAsync` creates an empty `AllowList` when the configuration file is absent. This is closed by default.
- `ClientAccessSettings` in the model defaults to `AllowAll`, and missing JSON sections are normalized from model defaults. Existing/partial configuration is not the same as fresh installation. Never infer a safe allow list merely from a successful parse.
- HMAC is optional and uses a secret derived from distributed code. It supplies replay checks, not a unique enrolled client identity. Do not claim only the genuine executable can generate headers.
- `Ipc:RequireAdministrator` defaults to false: the active console user can mutate proxy/firewall configuration through the privileged service. Set it true for an administrator-only management policy. This is a documented policy choice, not a missing pipe ACL.
- Named-pipe messages are capped at 1,048,576 characters, with 32 concurrent connections; this is a character limit, not an exact 1 MiB wire-byte limit. Mutation correlation IDs support idempotency.
- `Listener.MaxConnections` only emits a warning. It is not a hard resource ceiling. Destination restrictions cover ports; there is no destination host/IP allow list in `EvaluateAccess`.

## UI and verification

`NetworkSettingsPage` is the live network configuration page; `ProxySettingsPage` is a compatibility stub. Do not implement new controls in the stub. Preserve read-only secret placeholders in exported/IPC configuration. Local tests do not verify a real firewall policy, external SMTP session, enterprise upstream proxy, installation or sustained connection load.
