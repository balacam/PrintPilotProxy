using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;

namespace PrintPilotProxy.App.ViewModels;

public partial class LanguageViewModel : ObservableObject
{
    private readonly IpcClientService _ipc;

    public ObservableCollection<LanguageOption> Languages { get; } = new();

    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _statusIsError;

    public LanguageViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        LoadLanguages();
        _ = LoadCurrentAsync();
    }

    private void LoadLanguages()
    {
        Languages.Clear();
        foreach (var (cultureName, displayName) in LocalizationService.SupportedLanguages)
        {
            Languages.Add(new LanguageOption
            {
                CultureName = cultureName,
                DisplayName = displayName
            });
        }
    }

    private async Task LoadCurrentAsync()
    {
        try
        {
            var config = await _ipc.GetConfigurationAsync();
            if (config != null)
            {
                var current = config.Language?.CultureName;
                SelectedLanguage = Languages.FirstOrDefault(l =>
                    string.Equals(l.CultureName, current, StringComparison.OrdinalIgnoreCase))
                    ?? Languages.FirstOrDefault(l => l.CultureName == "system");
            }
            else
            {
                SelectedLanguage = Languages.FirstOrDefault(l => l.CultureName == "system");
            }
        }
        catch
        {
            StatusMessage = LocalizationService.Instance["Lang.LoadFailed"];
            StatusIsError = true;
            SelectedLanguage = Languages.FirstOrDefault(l => l.CultureName == "system");
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        try
        {
            StatusMessage = string.Empty;
            var config = await _ipc.GetConfigurationAsync();
            if (config == null)
            {
                StatusMessage = LocalizationService.Instance["Lang.LoadFailed"];
                StatusIsError = true;
                return;
            }

            config.Language ??= new Core.Models.LanguageSettings();
            config.Language.CultureName = SelectedLanguage?.CultureName ?? "system";

            var (ok, msg) = await _ipc.UpdateConfigurationAsync(config);
            if (ok)
            {
                LocalizationService.Instance.SetCulture(config.Language.CultureName);
                StatusMessage = LocalizationService.Instance["Lang.Applied"];
                StatusIsError = false;
            }
            else
            {
                StatusMessage = LocalizationService.Instance.GetFormat("Lang.ApplyFailed", msg);
                StatusIsError = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationService.Instance.GetFormat("Lang.ApplyFailed", ex.Message);
            StatusIsError = true;
        }
    }
}

public class LanguageOption
{
    public string CultureName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
