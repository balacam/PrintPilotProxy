# Linux Support (PLANNED)

**Note: Linux support is currently PLANNED and not yet implemented.**

## Future Architecture
When implemented, PrintPilotProxy will run on modern Linux distributions using the .NET 8 runtime. 
It will run as a `systemd` service for background execution.

## Configuration
Config paths will likely reside in `/etc/printpilotproxy/`.

## Firewall
Support for integrating with `iptables`, `nftables`, or `firewalld` will be provided for easy configuration.
