using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.ViewModels;

public partial class FirewallViewModel : ObservableObject
{
    private readonly IpcClientService _ipc;

    [ObservableProperty] private string _ruleStatus = "Unknown";
    [ObservableProperty] private bool _ruleExists = false;
    [ObservableProperty] private string _ruleName = "N/A";
    [ObservableProperty] private string _rulePort = "N/A";
    [ObservableProperty] private string _ruleProtocol = "TCP";
    [ObservableProperty] private string _ruleDirection = "Inbound";
    [ObservableProperty] private string _ruleScope = "N/A";

    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError = false;

    public FirewallViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;

            var config = await _ipc.GetConfigurationAsync();
            if (config != null)
            {
                RuleName = $"PrintPilotProxy - TCP {config.Listener.Port}";
                RulePort = config.Listener.Port.ToString();
            }

            var status = await _ipc.GetFirewallStatusAsync();
            if (status != null)
            {
                RuleExists = status.RuleExists;
                RuleStatus = status.RuleExists ? LocalizationService.Instance["Fw.RuleExists"] : LocalizationService.Instance["Fw.RuleNotFound"];

                if (status.CurrentRule != null)
                {
                    RuleProtocol = status.CurrentRule.Protocol;
                    RuleDirection = status.CurrentRule.Direction;
                RuleScope = status.CurrentRule.LocalAddresses.Count > 0
                    ? string.Join(", ", status.CurrentRule.LocalAddresses)
                    : LocalizationService.Instance["Common.All"];
                }
            }
            else
            {
                RuleStatus = LocalizationService.Instance["Fw.ServiceUnavailable"];
            }
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Fw.Msgs.LoadError", ex.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyRuleAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            var (success, message) = await _ipc.ApplyFirewallRuleAsync();
            SetStatus(success ? LocalizationService.Instance["Fw.Msgs.Applied"] : LocalizationService.Instance.GetFormat("Fw.Msgs.Failed", message), !success);
            if (success) await LoadAsync();
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Fw.Msgs.Error", ex.Message), isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveRuleAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            var (success, message) = await _ipc.RemoveFirewallRuleAsync();
            SetStatus(success ? LocalizationService.Instance["Fw.Msgs.Removed"] : LocalizationService.Instance.GetFormat("Fw.Msgs.Failed", message), !success);
            if (success) await LoadAsync();
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationService.Instance.GetFormat("Fw.Msgs.Error", ex.Message), isError: true);
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
