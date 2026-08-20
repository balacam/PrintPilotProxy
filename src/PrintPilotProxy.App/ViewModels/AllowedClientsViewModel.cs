using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;

namespace PrintPilotProxy.App.ViewModels;

public partial class AllowedClientsViewModel : ObservableObject
{
    private readonly IpcClientService _ipc;

    [ObservableProperty] private bool _modeAllowAll = true;
    [ObservableProperty] private bool _modeAllowList = false;

    public ObservableCollection<AllowedClientItem> Clients { get; } = new();

    [ObservableProperty] private AllowedClientItem? _selectedClient;

    // Add client form
    [ObservableProperty] private string _newClientName = string.Empty;
    [ObservableProperty] private string _newClientIpOrCidr = string.Empty;
    [ObservableProperty] private string _newClientDescription = string.Empty;

    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;

    private ProxyConfiguration? _currentConfig;

    public AllowedClientsViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        LocalizationService.Instance.PropertyChanged += (_, _) => _ = LoadAsync();
        _ = LoadAsync();
    }

    partial void OnModeAllowAllChanged(bool value)
    {
        if (value) ModeAllowList = false;
    }

    partial void OnModeAllowListChanged(bool value)
    {
        if (value) ModeAllowAll = false;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            _currentConfig = await _ipc.GetConfigurationAsync();
            if (_currentConfig == null)
            {
                SetStatus(LocalizationService.Instance["Cli.Msgs.LoadFailed"], isError: true);
                return;
            }

            ModeAllowAll  = _currentConfig.ClientAccess.Mode == ClientAccessMode.AllowAll;
            ModeAllowList = _currentConfig.ClientAccess.Mode == ClientAccessMode.AllowList;

            Clients.Clear();
            foreach (var client in _currentConfig.ClientAccess.AllowedClients)
            {
                Clients.Add(new AllowedClientItem(client));
            }
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Cli.Msgs.LoadError", ex.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddClient()
    {
        var name = NewClientName.Trim();
        var ipOrCidr = NewClientIpOrCidr.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus(LocalizationService.Instance["Cli.Msgs.NameRequired"], isError: true);
            return;
        }

        if (!NetworkValidator.IsValidIpOrCidr(ipOrCidr))
        {
            SetStatus(LocalizationService.Instance.GetFormat("Cli.Msgs.IpOrCidrInvalid", ipOrCidr), isError: true);
            return;
        }

        var client = new AllowedClient
        {
            Name = name,
            IpOrCidr = ipOrCidr,
            Description = NewClientDescription.Trim(),
            Enabled = true
        };

        Clients.Add(new AllowedClientItem(client));

        // Clear form
        NewClientName = string.Empty;
        NewClientIpOrCidr = string.Empty;
        NewClientDescription = string.Empty;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void RemoveClient()
    {
        if (SelectedClient == null)
        {
            SetStatus(LocalizationService.Instance["Cli.Msgs.SelectClientToRemove"], isError: true);
            return;
        }
        Clients.Remove(SelectedClient);
        SelectedClient = null;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ToggleClientEnabled()
    {
        if (SelectedClient == null) return;
        SelectedClient.IsEnabled = !SelectedClient.IsEnabled;
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (_currentConfig == null)
        {
            SetStatus(LocalizationService.Instance["Cli.Msgs.ConfigNotLoaded"], isError: true);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            _currentConfig.ClientAccess.Mode = ModeAllowAll
                ? ClientAccessMode.AllowAll
                : ClientAccessMode.AllowList;

            _currentConfig.ClientAccess.AllowedClients.Clear();
            foreach (var item in Clients)
            {
                _currentConfig.ClientAccess.AllowedClients.Add(item.ToModel());
            }

            _currentConfig.LastModified = DateTimeOffset.UtcNow;

            var (success, message) = await _ipc.UpdateConfigurationAsync(_currentConfig);
            if (success)
                SetStatus(LocalizationService.Instance["Cli.Msgs.Saved"], isError: false);
            else
                SetStatus(LocalizationService.Instance.GetFormat("Cli.Msgs.ApplyFailed", message), isError: true);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Cli.Msgs.Error", ex.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}

/// <summary>
/// UI-friendly wrapper around AllowedClient.
/// </summary>
public partial class AllowedClientItem : ObservableObject
{
    private readonly string _id;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _ipOrCidr;
    [ObservableProperty] private string _description;
    [ObservableProperty] private bool _isEnabled;
    public DateTimeOffset CreatedAt { get; }

    public AllowedClientItem(AllowedClient model)
    {
        _id = model.Id;
        _name = model.Name;
        _ipOrCidr = model.IpOrCidr;
        _description = model.Description;
        _isEnabled = model.Enabled;
        CreatedAt = model.CreatedAt;
    }

    public AllowedClient ToModel() => new()
    {
        Id = _id,
        Name = Name,
        IpOrCidr = IpOrCidr,
        Description = Description,
        Enabled = IsEnabled,
        CreatedAt = CreatedAt,
        ModifiedAt = DateTimeOffset.UtcNow
    };
}
