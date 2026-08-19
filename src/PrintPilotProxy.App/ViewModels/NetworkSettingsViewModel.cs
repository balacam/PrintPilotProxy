using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;

namespace PrintPilotProxy.App.ViewModels;

/// <summary>
/// View model for the Network Settings page.
/// Allows selection of listener mode (Auto / Specific Adapter / Specific IP),
/// proxy port, max connections, and connection timeout.
/// </summary>
public partial class NetworkSettingsViewModel : ObservableObject
{
    private readonly IpcClientService _ipc;

    // ─── Listener Mode ───────────────────────────────────────────────────────

    [ObservableProperty] private bool _modeAuto = true;
    [ObservableProperty] private bool _modeSpecificAdapter = false;
    [ObservableProperty] private bool _modeSpecificIp = false;

    // ─── Adapter selection ───────────────────────────────────────────────────

    public ObservableCollection<AdapterItem> DetectedAdapters { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdapterSelectionEnabled))]
    private AdapterItem? _selectedAdapter;

    public bool IsAdapterSelectionEnabled => ModeSpecificAdapter && DetectedAdapters.Count > 0;

    // ─── Specific IP ─────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSpecificIpEnabled))]
    private string _specificIpAddress = string.Empty;

    public bool IsSpecificIpEnabled => ModeSpecificIp;

    // ─── Port & Limits ───────────────────────────────────────────────────────

    [ObservableProperty] private string _proxyPort = "3128";
    [ObservableProperty] private string _maxConnections = "1000";
    [ObservableProperty] private string _connectionTimeoutSeconds = "120";

    // ─── UI state ────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;
    [ObservableProperty] private bool _isDirty = false;

    // ─── Security & Access ───────────────────────────────────────────────────

    [ObservableProperty] private bool _allowAllDestinationPorts = true;
    [ObservableProperty] private string _allowedDestinationPortsText = "80, 443";

    [ObservableProperty] private bool _clientAccessAllowAll = true;
    [ObservableProperty] private bool _clientAccessAllowList = false;

    private ProxyConfiguration? _originalConfig;

    public NetworkSettingsViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    // ─── Mode radio helpers ──────────────────────────────────────────────────

    partial void OnModeAutoChanged(bool value)
    {
        if (value) { ModeSpecificAdapter = false; ModeSpecificIp = false; }
        OnPropertyChanged(nameof(IsAdapterSelectionEnabled));
        OnPropertyChanged(nameof(IsSpecificIpEnabled));
        IsDirty = true;
    }

    partial void OnModeSpecificAdapterChanged(bool value)
    {
        if (value) { ModeAuto = false; ModeSpecificIp = false; }
        OnPropertyChanged(nameof(IsAdapterSelectionEnabled));
        OnPropertyChanged(nameof(IsSpecificIpEnabled));
        IsDirty = true;
    }

    partial void OnModeSpecificIpChanged(bool value)
    {
        if (value) { ModeAuto = false; ModeSpecificAdapter = false; }
        OnPropertyChanged(nameof(IsAdapterSelectionEnabled));
        OnPropertyChanged(nameof(IsSpecificIpEnabled));
        IsDirty = true;
    }

    partial void OnProxyPortChanged(string value) => IsDirty = true;
    partial void OnMaxConnectionsChanged(string value) => IsDirty = true;
    partial void OnConnectionTimeoutSecondsChanged(string value) => IsDirty = true;
    partial void OnSpecificIpAddressChanged(string value) => IsDirty = true;
    partial void OnSelectedAdapterChanged(AdapterItem? value) => IsDirty = true;
    partial void OnAllowAllDestinationPortsChanged(bool value) => IsDirty = true;
    partial void OnAllowedDestinationPortsTextChanged(string value) => IsDirty = true;
    partial void OnClientAccessAllowAllChanged(bool value)
    {
        if (value) ClientAccessAllowList = false;
        IsDirty = true;
    }
    partial void OnClientAccessAllowListChanged(bool value)
    {
        if (value) ClientAccessAllowAll = false;
        IsDirty = true;
    }

    // ─── Load ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            // Load adapters from service
            var interfaces = await _ipc.GetNetworkInterfacesAsync();
            DetectedAdapters.Clear();
            foreach (var ni in interfaces)
            {
                foreach (var addrStr in ni.Addresses)
                {
                    DetectedAdapters.Add(new AdapterItem
                    {
                        DisplayName = $"{ni.Name}  –  {addrStr}  ({(ni.IsPrivate ? LocalizationService.Instance["Common.Private"] : LocalizationService.Instance["Common.Public"])})",
                        InterfaceName = ni.Name,
                        IpAddress = addrStr
                    });
                }
            }

            // Load current config
            _originalConfig = await _ipc.GetConfigurationAsync();
            if (_originalConfig == null)
            {
                SetStatus(LocalizationService.Instance["Net.Msgs.LoadFailed"], isError: true);
                return;
            }

            ApplyConfigToUi(_originalConfig);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Net.Msgs.LoadError", ex.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyConfigToUi(ProxyConfiguration config)
    {
        var isAuto = config.Listener.Mode == ListenerMode.Auto || config.Listener.Mode == ListenerMode.AllInterfaces;
        var isAdapter = config.Listener.Mode == ListenerMode.SpecificAdapter
                        || (config.Listener.Mode == ListenerMode.SpecificAddress && DetectedAdapters.Any(a => a.IpAddress == config.Listener.ListenAddress));
        var isSpecificIp = config.Listener.Mode == ListenerMode.SpecificAddress && !isAdapter;

        if (!isAuto && !isAdapter && !isSpecificIp)
        {
            isAuto = true;
        }

        ModeAuto            = isAuto;
        ModeSpecificAdapter = isAdapter;
        ModeSpecificIp      = isSpecificIp;

        if (ModeSpecificAdapter || ModeSpecificIp)
        {
            var match = DetectedAdapters.FirstOrDefault(a => a.IpAddress == config.Listener.ListenAddress);
            if (match != null)
                SelectedAdapter = match;
        }

        SpecificIpAddress = config.Listener.ListenAddress ?? string.Empty;
        ProxyPort = config.Listener.Port.ToString();
        MaxConnections = config.Listener.MaxConnections.ToString();
        ConnectionTimeoutSeconds = config.Listener.ConnectionTimeoutSeconds.ToString();

        AllowAllDestinationPorts = !config.Security.DestinationPortRestrictionsEnabled;
        AllowedDestinationPortsText = string.Join(", ", config.Security.AllowedDestinationPorts);

        ClientAccessAllowAll = config.ClientAccess.Mode == ClientAccessMode.AllowAll;
        ClientAccessAllowList = config.ClientAccess.Mode == ClientAccessMode.AllowList;
    }

    // ─── Validate ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Reset()
    {
        if (_originalConfig != null)
        {
            ApplyConfigToUi(_originalConfig);
            IsDirty = false;
            SetStatus(LocalizationService.Instance["Net.Msgs.ResetDone"], isError: false);
        }
    }

    // ─── Apply ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ApplyAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            // ── Validate port ──
            if (!int.TryParse(ProxyPort, out var port) || !NetworkValidator.IsValidPort(port))
            {
                SetStatus(LocalizationService.Instance.GetFormat("Net.Msgs.PortInvalid", ProxyPort), isError: true);
                return;
            }

            if (!int.TryParse(MaxConnections, out var maxConn) || maxConn < 1 || maxConn > 100000)
            {
                SetStatus(LocalizationService.Instance["Net.Msgs.MaxConnectionsInvalid"], isError: true);
                return;
            }

            if (!int.TryParse(ConnectionTimeoutSeconds, out var timeout) || timeout < 1 || timeout > 3600)
            {
                SetStatus(LocalizationService.Instance["Net.Msgs.TimeoutInvalid"], isError: true);
                return;
            }

            // ── Resolve listen address ──
            ListenerMode mode;
            string? listenAddress = null;

            if (ModeAuto)
            {
                mode = ListenerMode.Auto;
            }
            else if (ModeSpecificAdapter)
            {
                if (SelectedAdapter == null)
                {
                    SetStatus(LocalizationService.Instance["Net.Msgs.SelectAdapter"], isError: true);
                    return;
                }
                mode = ListenerMode.SpecificAddress;
                listenAddress = SelectedAdapter.IpAddress;
            }
            else // ModeSpecificIp
            {
                var trimmed = SpecificIpAddress.Trim();
                if (!NetworkValidator.IsValidListenAddress(trimmed))
                {
                    SetStatus(LocalizationService.Instance.GetFormat("Net.Msgs.IpInvalid", trimmed), isError: true);
                    return;
                }

                // Verify the IP actually exists on this machine
                if (!IsIpAssignedLocally(trimmed, out var allLocalIps))
                {
                    SetStatus(
                        LocalizationService.Instance.GetFormat("Net.Msgs.IpNotLocal", trimmed, string.Join(", ", allLocalIps)),
                        isError: true);
                    return;
                }

                mode = ListenerMode.SpecificAddress;
                listenAddress = trimmed;
            }

            // ── Build updated config ──
            var current = await _ipc.GetConfigurationAsync();
            if (current == null)
            {
                SetStatus(LocalizationService.Instance["Net.Msgs.ReadConfigFailed"], isError: true);
                return;
            }

            current.Listener.Mode = mode;
            current.Listener.ListenAddress = listenAddress;
            current.Listener.Port = port;
            current.Listener.MaxConnections = maxConn;
            current.Listener.ConnectionTimeoutSeconds = timeout;

            // ── Update Security & Access ──
            current.Security.DestinationPortRestrictionsEnabled = !AllowAllDestinationPorts;
            var portStrings = AllowedDestinationPortsText.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var ports = new List<int>();
            foreach (var ps in portStrings)
            {
                if (int.TryParse(ps, out var p) && p > 0 && p <= 65535)
                {
                    ports.Add(p);
                }
            }
            if (ports.Any())
            {
                current.Security.AllowedDestinationPorts = ports;
            }

            current.ClientAccess.Mode = ClientAccessAllowAll ? ClientAccessMode.AllowAll : ClientAccessMode.AllowList;

            current.LastModified = DateTimeOffset.UtcNow;

            // ── Send to service ──
            var (success, message) = await _ipc.UpdateConfigurationAsync(current);
            if (success)
            {
                _originalConfig = current;
                IsDirty = false;
                await _ipc.RestartProxyAsync();
                SetStatus(LocalizationService.Instance["Net.Msgs.Applied"], isError: false);
            }
            else
            {
                SetStatus(LocalizationService.Instance.GetFormat("Net.Msgs.ApplyFailed", message), isError: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Net.Msgs.UnexpectedError", ex.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static bool IsIpAssignedLocally(string ip, out List<string> allLocalIps)
    {
        allLocalIps = new List<string>();
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            allLocalIps = host.AddressList
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                         || a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                .Select(a => a.ToString())
                .ToList();
            allLocalIps.Add("127.0.0.1");
            allLocalIps.Add("0.0.0.0");

            // Also enumerate adapters directly
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    allLocalIps.Add(ua.Address.ToString());
                }
            }

            return allLocalIps.Contains(ip);
        }
        catch
        {
            // If we can't check, allow it (service will validate)
            return true;
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}

public class AdapterItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string InterfaceName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public override string ToString() => DisplayName;
}
