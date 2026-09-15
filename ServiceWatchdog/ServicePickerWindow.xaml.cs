using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ServiceWatchdog;

public partial class ServicePickerWindow : Window
{
    private class ServiceRow
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private readonly List<ServiceRow> _allServices = new();

    public string? SelectedServiceName { get; private set; }
    public string? SelectedDisplayName { get; private set; }

    public ServicePickerWindow()
    {
        InitializeComponent();
        LoadServices();
    }

    private void LoadServices()
    {
        try
        {
            var services = ServiceController.GetServices()
                .OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase);

            foreach (var s in services)
            {
                _allServices.Add(new ServiceRow
                {
                    ServiceName = s.ServiceName,
                    DisplayName = s.DisplayName,
                    Status = s.Status.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось получить список служб: {ex.Message}", "ServiceWatchdog",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        ServicesListView.ItemsSource = _allServices;
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = FilterTextBox.Text?.Trim() ?? string.Empty;
        if (filter.Length == 0)
        {
            ServicesListView.ItemsSource = _allServices;
            return;
        }

        ServicesListView.ItemsSource = _allServices.Where(s =>
            s.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
            s.ServiceName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e) => AcceptSelection();

    private void ServicesListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => AcceptSelection();

    private void AcceptSelection()
    {
        if (ServicesListView.SelectedItem is ServiceRow row)
        {
            SelectedServiceName = row.ServiceName;
            SelectedDisplayName = row.DisplayName;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
