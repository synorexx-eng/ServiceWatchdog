using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ServiceWatchdog;

public partial class App : Application
{
    private System.Threading.Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Необработанная ошибка: {args.Exception.Message}",
                "ServiceWatchdog",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        const string mutexName = "ServiceWatchdog-SingleInstance-Mutex";
        _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show(
                "ServiceWatchdog уже запущен. Проверьте значок в области уведомлений.",
                "ServiceWatchdog",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var startMinimized = e.Args.Contains("--minimized");

        var mainWindow = new MainWindow(startMinimized);
        MainWindow = mainWindow;
        if (!mainWindow.StartHidden)
            mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Mutex освобождаем, только если им владеет этот экземпляр (у второго экземпляра ReleaseMutex бросит исключение).
        if (_ownsMutex)
            _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
