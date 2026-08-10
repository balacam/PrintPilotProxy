using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace PrintPilotProxy.App.Localization;

/// <summary>
/// Central UI localization service backed by the Strings satellite resources.
/// Exposes a live indexer so WPF bindings update automatically when the
/// culture changes. Thread-safe for reads after construction.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string DefaultCultureName = "en";

    /// <summary>The 13 UI languages supported by PrintPilotProxy.</summary>
    public static readonly IReadOnlyList<string> SupportedCultureNames = new[]
    {
        "en-US", "de-DE", "fr-FR", "es-ES", "pt-BR", "it-IT", "nl-NL", "tr-TR", "pl-PL", "ro-RO", "bg-BG", "cs-CZ", "sv-SE"
    };

    /// <summary>Display names for the Language page (native language names).</summary>
    public static readonly IReadOnlyList<(string CultureName, string DisplayName)> SupportedLanguages = new[]
    {
        ("system", "System Default"),
        ("en-US", "English"),
        ("de-DE", "Deutsch"),
        ("fr-FR", "Français"),
        ("es-ES", "Español"),
        ("pt-BR", "Português"),
        ("it-IT", "Italiano"),
        ("nl-NL", "Nederlands"),
        ("tr-TR", "Türkçe"),
        ("pl-PL", "Polski"),
        ("ro-RO", "Română"),
        ("bg-BG", "Български"),
        ("cs-CZ", "Čeština"),
        ("sv-SE", "Svenska")
    };

    public static LocalizationService Instance { get; } = new();

    private readonly ResourceManager _resources;
    private CultureInfo _culture;

    private LocalizationService()
    {
        _resources = new ResourceManager(
            "PrintPilotProxy.App.Resources.Strings",
            typeof(LocalizationService).Assembly);
        _culture = CultureInfo.CurrentUICulture;
        if (!IsSupported(_culture))
            _culture = new CultureInfo(DefaultCultureName);
    }

    /// <summary>The currently selected culture.</summary>
    public CultureInfo CurrentCulture => _culture;

    /// <summary>Live look-up of a localized string.</summary>
    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        try
        {
            return _resources.GetString(key, _culture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }

    /// <summary>Localized string formatted with the current culture.</summary>
    public string GetFormat(string key, params object?[] args)
    {
        var template = GetString(key);
        try { return string.Format(_culture, template, args); }
        catch (FormatException) { return template; }
    }

    /// <summary>
    /// Applies the given culture. Null or empty selects the Windows UI culture;
    /// an unsupported or unknown name is ignored. Notifies all bindings.
    /// </summary>
    public void SetCulture(string? cultureName)
    {
        CultureInfo? target = null;
        if (string.IsNullOrWhiteSpace(cultureName) || cultureName == "system")
        {
            target = CultureInfo.CurrentUICulture;
        }
        else
        {
            try
            {
                var candidate = new CultureInfo(cultureName);
                if (IsSupported(candidate))
                    target = candidate;
            }
            catch (CultureNotFoundException) { /* fall through */ }
        }

        if (target == null || Equals(target, _culture))
            return;

        _culture = target;
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public static bool IsSupported(CultureInfo culture)
    {
        var name = culture.Name;
        foreach (var supported in SupportedCultureNames)
        {
            if (string.Equals(supported, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Raised with an empty property name whenever the culture changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}