using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using ServiceWatchdog.Models;
using ServiceWatchdog.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ServiceWatchdog;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly WatchdogEngine _engine = new();
    private readonly ObservableCollection<WatchRule> _rules = new();
    private AppSettings _settings = new();

    private NotifyIcon? _notifyIcon;
    private bool _isExiting;
    private bool _hasShownTrayNotice;

    public MainWindow() : this(false) { }

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();

        RulesGrid.ItemsSource = _rules;

        _settings = _settingsService.Load();
        foreach (var rule in _settings.Rules)
            _rules.Add(rule);

        StartWithWindowsCheckBox.IsChecked = _startupService.IsEnabled;

        _engine.LogMessage += Engine_LogMessage;
        _engine.Start(_rules.ToList());

        SetupTrayIcon();

        if (startMinimized || _settings.StartMinimized)
        {
            Hide();
        }
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Visible = true,
            Text = "ServiceWatchdog"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = menu;

        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        if (_hasShownTrayNotice) return;
        _hasShownTrayNotice = true;

        _notifyIcon?.ShowBalloonTip(
            3000,
            "ServiceWatchdog продолжает работать",
            "Приложение свёрнуто в трей и продолжает мониторинг. Чтобы открыть окно — дважды кликните по значку, для выхода используйте пункт меню \"Выход\".",
            ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private void Engine_LogMessage(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogListBox.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
            while (LogListBox.Items.Count > 200)
                LogListBox.Items.RemoveAt(LogListBox.Items.Count - 1);
        });
    }

    private void PersistRules()
    {
        _settings.Rules = _rules.ToList();
        _settingsService.Save(_settings);
        _engine.UpdateRules(_rules.ToList());
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RuleEditWindow(null) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            _rules.Add(dialog.Result);
            PersistRules();
        }
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not WatchRule selected) return;
        OpenEditDialog(selected);
    }

    private void RulesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (RulesGrid.SelectedItem is WatchRule selected)
            OpenEditDialog(selected);
    }

    private void OpenEditDialog(WatchRule selected)
    {
        var dialog = new RuleEditWindow(selected) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            var index = _rules.IndexOf(selected);
            _rules[index] = dialog.Result;
            PersistRules();
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not WatchRule selected) return;

        var result = MessageBox.Show(this, $"Удалить правило \"{selected.Name}\"?", "ServiceWatchdog",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _rules.Remove(selected);
        PersistRules();
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is WatchRule selected)
        {
            await _engine.CheckNowAsync(selected);
        }
        else
        {
            foreach (var rule in _rules.ToList())
                await _engine.CheckNowAsync(rule);
        }
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = StartWithWindowsCheckBox.IsChecked == true;
        try
        {
            _startupService.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось изменить автозапуск: {ex.Message}", "ServiceWatchdog",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            _notifyIcon?.Dispose();
            _engine.Stop();
            return;
        }

        // По умолчанию закрытие окна сворачивает в трей, а не завершает мониторинг.
        e.Cancel = true;
        HideToTray();
    }
}
