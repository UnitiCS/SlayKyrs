using System.Windows;

namespace SLAU.Client
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Глобальная обработка необработанных исключений
            Current.DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Произошла непредвиденная ошибка: {args.Exception.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}