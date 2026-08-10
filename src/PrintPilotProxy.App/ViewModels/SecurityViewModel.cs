using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintPilotProxy.App.Localization;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.ViewModels;

public partial class SecurityViewModel : ObservableObject
{
    private readonly IpcClientService _ipc;

    public ObservableCollection<SecurityCheckItem> Checks { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _overallStatus = string.Empty;
    [ObservableProperty] private string _overallStatusBrushKey = "TextBrush";

    public SecurityViewModel(IpcClientService ipc)
    {
        _ipc = ipc;
        _ = RunAuditAsync();
    }

    [RelayCommand]
    private async Task RunAuditAsync()
    {
        try
        {
            IsBusy = true;
            Checks.Clear();
            OverallStatus = LocalizationService.Instance["Sec.Description"];
            OverallStatusBrushKey = "WarningBrush";

            var audit = await _ipc.GetSecurityAuditAsync();
            if (audit == null)
            {
                OverallStatus = LocalizationService.Instance["Sec.Msgs.AuditFailed"];
                OverallStatusBrushKey = "ErrorBrush";
                return;
            }

            foreach (var check in audit.Checks)
            {
                var pascalId = check.Id.Length >= 4
                    ? char.ToUpper(check.Id[0]) + check.Id[1..3].ToLower() + check.Id[3..]
                    : check.Id;

                var nameKey = $"Sec.Check.{pascalId}.Name";
                var localizedName = LocalizationService.Instance[nameKey];
                if (localizedName == nameKey) localizedName = check.Name;

                var descKey = $"Sec.Check.{pascalId}.Desc";
                var localizedDesc = LocalizationService.Instance[descKey];
                if (localizedDesc == descKey) localizedDesc = check.Description;

                Checks.Add(new SecurityCheckItem
                {
                    Id = check.Id,
                    Name = localizedName,
                    Description = localizedDesc,
                    Passed = check.Passed,
                    Level = check.Level,
                    LevelLabel = check.Level switch
                    {
                        SecurityLevel.Secure  => LocalizationService.Instance["Sec.Level.Secure"],
                        SecurityLevel.Info    => LocalizationService.Instance["Sec.Level.Info"],
                        SecurityLevel.Warning => LocalizationService.Instance["Sec.Level.Warning"],
                        SecurityLevel.Critical => LocalizationService.Instance["Sec.Level.Critical"],
                        _ => check.Level.ToString()
                    },
                    LevelBrushKey = check.Level switch
                    {
                        SecurityLevel.Secure  => "SuccessBrush",
                        SecurityLevel.Info    => "InfoBrush",
                        SecurityLevel.Warning => "WarningBrush",
                        SecurityLevel.Critical => "ErrorBrush",
                        _ => "TextBrush"
                    },
                    Message = check.Message,
                    Remediation = check.Remediation ?? string.Empty
                });
            }

            OverallStatus = audit.OverallLevel switch
            {
                SecurityLevel.Secure  => LocalizationService.Instance.GetFormat("Sec.Overall", LocalizationService.Instance["Sec.Level.Secure"]),
                SecurityLevel.Info    => LocalizationService.Instance.GetFormat("Sec.Overall", LocalizationService.Instance["Sec.Level.Info"]),
                SecurityLevel.Warning => LocalizationService.Instance.GetFormat("Sec.Overall", LocalizationService.Instance["Sec.Level.Warning"]),
                SecurityLevel.Critical => LocalizationService.Instance.GetFormat("Sec.Overall", LocalizationService.Instance["Sec.Level.Critical"]),
                _ => string.Empty
            };
            OverallStatusBrushKey = audit.OverallLevel switch
            {
                SecurityLevel.Secure  => "SuccessBrush",
                SecurityLevel.Info    => "InfoBrush",
                SecurityLevel.Warning => "WarningBrush",
                SecurityLevel.Critical => "ErrorBrush",
                _ => "TextBrush"
            };
        }
        catch (Exception)
        {
            OverallStatus = LocalizationService.Instance.GetFormat("Sec.Msgs.AuditFailed");
            OverallStatusBrushKey = "ErrorBrush";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class SecurityCheckItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public SecurityLevel Level { get; set; }
    public string LevelLabel { get; set; } = string.Empty;
    public string LevelBrushKey { get; set; } = "TextBrush";
    public string Message { get; set; } = string.Empty;
    public string Remediation { get; set; } = string.Empty;
}
