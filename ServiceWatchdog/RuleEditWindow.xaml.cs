using System.ServiceProcess;
using System.Windows;
using Microsoft.Win32;
using ServiceWatchdog.Models;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace ServiceWatchdog;

public partial class RuleEditWindow : Window
{
    private readonly WatchRule _workingCopy;
    public WatchRule? Result { get; private set; }

    public RuleEditWindow(WatchRule? existing)
    {
        InitializeComponent();

        _workingCopy = existing is null
            ? new WatchRule { Name = "Новое правило" }
            : CloneRule(existing);

        NameTextBox.Text = _workingCopy.Name;
        ServiceNameTextBox.Text = _workingCopy.ServiceName;
        ProcessPathTextBox.Text = _workingCopy.ProcessPath;
        ProcessArgsTextBox.Text = _workingCopy.ProcessArguments;
        IntervalTextBox.Text = _workingCopy.IntervalSeconds.ToString();
        EnabledCheckBox.IsChecked = _workingCopy.Enabled;

        if (_workingCopy.Type == RuleType.Service)
            ServiceRadioButton.IsChecked = true;
        else
            ProcessRadioButton.IsChecked = true;

        UpdateFieldVisibility();
    }

    private static WatchRule CloneRule(WatchRule source) => new()
    {
        Name = source.Name,
        Type = source.Type,
        ServiceName = source.ServiceName,
        ProcessPath = source.ProcessPath,
        ProcessArguments = source.ProcessArguments,
        IntervalSeconds = source.IntervalSeconds,
        Enabled = source.Enabled
    };

    private void TypeRadioButton_Checked(object sender, RoutedEventArgs e) => UpdateFieldVisibility();

    private void UpdateFieldVisibility()
    {
        // ServiceRadioButton may not be initialized yet during InitializeComponent's early callbacks.
        if (ServiceNameLabel is null) return;

        var isService = ServiceRadioButton.IsChecked == true;

        ServiceNameLabel.Visibility = isService ? Visibility.Visible : Visibility.Collapsed;
        ServiceNameTextBox.Visibility = isService ? Visibility.Visible : Visibility.Collapsed;
        PickServiceButton.Visibility = isService ? Visibility.Visible : Visibility.Collapsed;

        ProcessPathLabel.Visibility = isService ? Visibility.Collapsed : Visibility.Visible;
        ProcessPathTextBox.Visibility = isService ? Visibility.Collapsed : Visibility.Visible;
        BrowseButton.Visibility = isService ? Visibility.Collapsed : Visibility.Visible;
        ProcessArgsLabel.Visibility = isService ? Visibility.Collapsed : Visibility.Visible;
        ProcessArgsTextBox.Visibility = isService ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PickServiceButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ServicePickerWindow { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedServiceName is not null)
        {
            ServiceNameTextBox.Text = picker.SelectedServiceName;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || NameTextBox.Text == "Новое правило")
                NameTextBox.Text = picker.SelectedDisplayName ?? picker.SelectedServiceName;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*",
            Title = "Выберите исполняемый файл"
        };
        if (dialog.ShowDialog(this) == true)
        {
            ProcessPathTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || NameTextBox.Text == "Новое правило")
                NameTextBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Укажите имя правила.", "ServiceWatchdog", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isService = ServiceRadioButton.IsChecked == true;
        if (isService && string.IsNullOrWhiteSpace(ServiceNameTextBox.Text))
        {
            MessageBox.Show(this, "Укажите имя службы.", "ServiceWatchdog", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!isService && string.IsNullOrWhiteSpace(ProcessPathTextBox.Text))
        {
            MessageBox.Show(this, "Укажите путь к исполняемому файлу.", "ServiceWatchdog", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(IntervalTextBox.Text, out var interval) || interval < 1)
        {
            MessageBox.Show(this, "Интервал должен быть целым числом секунд (минимум 1).", "ServiceWatchdog", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _workingCopy.Name = name;
        _workingCopy.Type = isService ? RuleType.Service : RuleType.Process;
        _workingCopy.ServiceName = ServiceNameTextBox.Text.Trim();
        _workingCopy.ProcessPath = ProcessPathTextBox.Text.Trim();
        _workingCopy.ProcessArguments = ProcessArgsTextBox.Text.Trim();
        _workingCopy.IntervalSeconds = interval;
        _workingCopy.Enabled = EnabledCheckBox.IsChecked == true;

        Result = _workingCopy;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
