using System;
using System.Windows.Markup;
using WpfBinding = System.Windows.Data.Binding;
using WpfBindingMode = System.Windows.Data.BindingMode;

namespace PrintPilotProxy.App.Localization;

/// <summary>
/// Markup extension for live-localizable XAML strings.
/// Example: <c>Text="{loc:Loc Key=Nav.Dashboard}"</c> or <c>{loc:Loc Nav.Dashboard}</c>.
/// The returned binding re-renders whenever the selected culture changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        var service = LocalizationService.Instance;
        var binding = new WpfBinding($"[{Key}]")
        {
            Source = service,
            Mode = WpfBindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}