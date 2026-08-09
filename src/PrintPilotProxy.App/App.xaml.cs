using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PrintPilotProxy.App.ViewModels;
using PrintPilotProxy.App.Views;

namespace PrintPilotProxy.App;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
        this.InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Infrastructure
        PrintPilotProxy.Infrastructure.InfrastructureServiceExtensions.AddInfrastructureServices(services);

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        // services.AddTransient<ProxySettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
