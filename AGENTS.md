# PrintPilotProxy assistant entry point

Read `PROJECT_CONTEXT.md`, `docs/AGENT_CONTEXT.md`, `docs/SECURITY.md` and `CLAUDE.md`. Target `PrintPilotProxy.sln` explicitly in restore/build/test commands. Make behavior documentation changes in this repository; do not copy desktop/Server guides without comparing.

This product is an optional LAN transport (TCP 3128, discovery UDP 37421), not an alacam.it backend or an OldOS enrollment server. Never add TLS interception. A new configuration starts with an empty allow list. An existing configuration, management console privileges and the shared protocol HMAC have different security boundaries; see the agent context.
