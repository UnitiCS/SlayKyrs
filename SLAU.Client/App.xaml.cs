using System.Windows;

namespace SLAU.Client;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Глобальная обработка необработанных исключений
        Current.DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"An unexpected error occurred:\n{args.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}