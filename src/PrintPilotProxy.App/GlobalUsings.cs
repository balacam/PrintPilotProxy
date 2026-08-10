// Global alias disambiguations — required when UseWindowsForms=true conflicts with WPF types
global using Application      = System.Windows.Application;
global using UserControl      = System.Windows.Controls.UserControl;
global using Window           = System.Windows.Window;
global using MessageBox       = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage  = System.Windows.MessageBoxImage;
global using Visibility       = System.Windows.Visibility;
global using WindowState      = System.Windows.WindowState;
// Clipboard left unaliased globally — use WpfClipboard local alias in LogsViewModel
