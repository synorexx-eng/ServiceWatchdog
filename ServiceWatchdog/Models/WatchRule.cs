using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServiceWatchdog.Models;

public class WatchRule : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private RuleType _type = RuleType.Service;
    private string _serviceName = string.Empty;
    private string _processPath = string.Empty;
    private string _processArguments = string.Empty;
    private int _intervalSeconds = 30;
    private bool _enabled = true;
    private string _status = "—";
    private DateTime? _lastChecked;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public RuleType Type
    {
        get => _type;
        set { SetField(ref _type, value); RaiseTargetDisplayChanged(); }
    }

    /// <summary>Имя службы (как в services.msc, поле "Service Name").</summary>
    public string ServiceName
    {
        get => _serviceName;
        set { SetField(ref _serviceName, value); RaiseTargetDisplayChanged(); }
    }

    /// <summary>Путь к исполняемому файлу процесса (или просто имя, если он в PATH).</summary>
    public string ProcessPath
    {
        get => _processPath;
        set { SetField(ref _processPath, value); RaiseTargetDisplayChanged(); }
    }

    /// <summary>Необязательные аргументы командной строки для запуска процесса.</summary>
    public string ProcessArguments
    {
        get => _processArguments;
        set { SetField(ref _processArguments, value); RaiseTargetDisplayChanged(); }
    }

    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set => SetField(ref _intervalSeconds, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string TargetDisplay => Type == RuleType.Service
        ? ServiceName
        : string.IsNullOrWhiteSpace(ProcessArguments) ? ProcessPath : $"{ProcessPath} {ProcessArguments}";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime? LastChecked
    {
        get => _lastChecked;
        set => SetField(ref _lastChecked, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RaiseTargetDisplayChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetDisplay)));
}
