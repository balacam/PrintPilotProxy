// ProxySettingsViewModel is an alias for NetworkSettingsViewModel.
// Navigation to "ProxySettings" resolves to ProxySettingsPage which hosts
// the NetworkSettingsViewModel via its DataContext.
// This file intentionally kept for assembly type resolution compatibility.
namespace PrintPilotProxy.App.ViewModels;

/// <summary>
/// Backward-compatibility alias view model for ProxySettings.
/// Inherits NetworkSettingsViewModel so navigation to ProxySettings works identically.
/// </summary>
public partial class ProxySettingsViewModel : NetworkSettingsViewModel
{
    public ProxySettingsViewModel(Services.IpcClientService ipc) : base(ipc)
    {
    }
}
