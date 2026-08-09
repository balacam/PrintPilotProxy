using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PrintPilotProxy.App.ViewModels;

namespace PrintPilotProxy.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Current.Services.GetRequiredService<MainViewModel>();
    }
}
