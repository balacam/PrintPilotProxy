using System;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PrintPilotProxy.App.Services;
using PrintPilotProxy.App.ViewModels;
using PrintPilotProxy.App.Views;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using Xunit;

namespace PrintPilotProxy.App.Tests;

public class MainViewModelNavigationTests
{
    private static IServiceProvider CreateTestServiceProvider(IpcClientService ipcService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        PrintPilotProxy.Infrastructure.InfrastructureServiceExtensions.AddInfrastructureServices(services);
        services.AddSingleton(ipcService);
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<NetworkSettingsViewModel>();
        services.AddTransient<ProxySettingsViewModel>();
        services.AddTransient<AllowedClientsViewModel>();
        services.AddTransient<FirewallViewModel>();
        services.AddTransient<ServiceViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<LanguageViewModel>();
        services.AddTransient<SecurityViewModel>();
        return services.BuildServiceProvider();
    }

    private static void EnsureWpfApplication()
    {
        if (Application.Current == null)
        {
            try
            {
                var scheme = PackUriHelper.UriSchemePack;
            }
            catch { }

            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var dict = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/PrintPilotProxy.App;component/Styles/AppStyles.xaml", UriKind.Absolute)
                };
                app.Resources.MergedDictionaries.Add(dict);
            }
            catch { }
        }
        else
        {
            try
            {
                if (Application.Current.Resources.MergedDictionaries.Count == 0)
                {
                    var dict = new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/PrintPilotProxy.App;component/Styles/AppStyles.xaml", UriKind.Absolute)
                    };
                    Application.Current.Resources.MergedDictionaries.Add(dict);
                }
            }
            catch { }
        }
    }

    private static Mock<IIpcClient> CreateMockIpc(bool isConnected = true)
    {
        var mock = new Mock<IIpcClient>();
        mock.Setup(c => c.IsConnected).Returns(isConnected);
        mock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(isConnected);
        if (isConnected)
        {
            mock.Setup(c => c.SendAsync(It.IsAny<IpcMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IpcMessage { Type = IpcMessageTypes.StatusResponse });
        }
        else
        {
            mock.Setup(c => c.SendAsync(It.IsAny<IpcMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("IPC Service Unavailable"));
        }
        return mock;
    }

    [Theory]
    [InlineData("Settings", typeof(NetworkSettingsPage), typeof(NetworkSettingsViewModel))]
    [InlineData("ProxySettings", typeof(NetworkSettingsPage), typeof(NetworkSettingsViewModel))]
    [InlineData("NetworkSettings", typeof(NetworkSettingsPage), typeof(NetworkSettingsViewModel))]
    [InlineData("Dashboard", typeof(DashboardPage), typeof(DashboardViewModel))]
    [InlineData("AllowedClients", typeof(AllowedClientsPage), typeof(AllowedClientsViewModel))]
    [InlineData("Firewall", typeof(FirewallPage), typeof(FirewallViewModel))]
    [InlineData("Service", typeof(ServicePage), typeof(ServiceViewModel))]
    [InlineData("Logs", typeof(LogsPage), typeof(LogsViewModel))]
    [InlineData("Diagnostics", typeof(DiagnosticsPage), typeof(DiagnosticsViewModel))]
    [InlineData("Security", typeof(SecurityPage), typeof(SecurityViewModel))]
    [InlineData("Language", typeof(LanguagePage), typeof(LanguageViewModel))]
    public void NavigateCommand_ExecutesAndInstantiatesViewAndViewModel(
        string pageAlias, Type expectedViewType, Type expectedVmType)
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApplication();

                var mockIpcClient = CreateMockIpc();
                var ipcService = new IpcClientService(mockIpcClient.Object);
                var sp = CreateTestServiceProvider(ipcService);
                var mainVm = sp.GetRequiredService<MainViewModel>();

                mainVm.NavigateCommand.Execute(pageAlias);

                mainVm.CurrentPage.Should().NotBeNull();
                mainVm.CurrentPage!.GetType().Should().Be(expectedViewType);

                if (mainVm.CurrentPage is FrameworkElement fe && fe.DataContext != null)
                {
                    fe.DataContext.GetType().Should().Be(expectedVmType);
                }
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadException != null)
        {
            throw threadException;
        }
    }

    [Fact]
    [Trait("Category", "STA")]
    public void DashboardPage_InstantiatesWithoutXamlParseException()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApplication();
                var page = new DashboardPage();
                page.Should().NotBeNull();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadException != null)
        {
            throw threadException;
        }
    }

    [Fact]
    [Trait("Category", "STA")]
    public void ServicePage_InstantiatesWithoutXamlParseException()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApplication();
                var page = new ServicePage();
                page.Should().NotBeNull();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadException != null)
        {
            throw threadException;
        }
    }

    [Fact]
    [Trait("Category", "STA")]
    public void NetworkSettings_InstantiatesEvenWhenIpcFails()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApplication();

                var mockIpcClient = CreateMockIpc(isConnected: false);
                var ipcService = new IpcClientService(mockIpcClient.Object);
                var sp = CreateTestServiceProvider(ipcService);
                var mainVm = sp.GetRequiredService<MainViewModel>();

                mainVm.NavigateCommand.Execute("Settings");

                mainVm.CurrentPage.Should().NotBeNull();
                mainVm.CurrentPage!.GetType().Should().Be(typeof(NetworkSettingsPage));
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadException != null)
        {
            throw threadException;
        }
    }

    [Fact]
    public void RealIpcClientService_ResolvesFromDiWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        PrintPilotProxy.Infrastructure.InfrastructureServiceExtensions.AddInfrastructureServices(services);
        services.AddSingleton<IpcClientService>();
        services.AddSingleton<MainViewModel>();
        var sp = services.BuildServiceProvider();

        var ipcService = sp.GetRequiredService<IpcClientService>();
        ipcService.Should().NotBeNull();

        var mainVm = sp.GetRequiredService<MainViewModel>();
        mainVm.Should().NotBeNull();
    }
}
