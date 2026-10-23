using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using PrintPilotProxy.App.ViewModels;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.Views
{
    public partial class NetworkSettingsPage : UserControl
    {
        private bool _syncingPassword;

        public NetworkSettingsPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private NetworkSettingsViewModel? ViewModel => DataContext as NetworkSettingsViewModel;

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
                oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newViewModel)
                newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncPasswordBoxFromViewModel();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NetworkSettingsViewModel.UpstreamPassword))
                SyncPasswordBoxFromViewModel();
        }

        // The stored password never reaches the UI: the service sends a placeholder, which is shown as an empty box.
        // Leaving the box empty keeps the stored password.
        private void SyncPasswordBoxFromViewModel()
        {
            var value = ViewModel?.UpstreamPassword;
            var shown = value == ConfigurationSecrets.UnchangedSentinel ? string.Empty : value ?? string.Empty;
            if (UpstreamPasswordBox.Password == shown)
                return;

            _syncingPassword = true;
            try
            {
                UpstreamPasswordBox.Password = shown;
            }
            finally
            {
                _syncingPassword = false;
            }
        }

        private void OnUpstreamPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingPassword || ViewModel is not { } viewModel)
                return;

            viewModel.UpstreamPassword = UpstreamPasswordBox.Password;
        }
    }
}
