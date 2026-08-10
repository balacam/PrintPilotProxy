using System.Windows;
using PrintPilotProxy.App.ViewModels;

namespace PrintPilotProxy.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = mainViewModel;
    }
}
