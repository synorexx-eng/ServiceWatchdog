using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Windows.Threading;
using ServiceWatchdog.Models;

namespace ServiceWatchdog.Services;

/// <summary>
/// Раз в секунду проверяет, каким правилам пора выполнить проверку (согласно их собственному
/// интервалу), и для просроченных правил проверяет статус службы/процесса и перезапускает при необходимости.
/// Работает на DispatcherTimer, поэтому вызовы безопасны для обновления UI-биндингов напрямую.
/// </summary>
public class WatchdogEngine
{
    private readonly DispatcherTimer _tickTimer;
    private readonly Dictionary<WatchRule, DateTime> _nextCheckAt = new();
    private readonly HashSet<WatchRule> _checksInFlight = new();

    public IReadOnlyList<WatchRule> Rules { get; private set; } = Array.Empty<WatchRule>();

    public event EventHandler<string>? LogMessage;

    public WatchdogEngine()
    {
        _tickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += OnTick;
    }

    public void Start(IReadOnlyList<WatchRule> rules)
    {
        Rules = rules;
        _nextCheckAt.Clear();
        var now = DateTime.Now;
        foreach (var rule in rules)
            _nextCheckAt[rule] = now;

        _tickTimer.Start();
    }

    public void Stop() => _tickTimer.Stop();

    public void UpdateRules(IReadOnlyList<WatchRule> rules)
    {
        Rules = rules;
        var now = DateTime.Now;
        foreach (var rule in rules)
        {
            if (!_nextCheckAt.ContainsKey(rule))
                _nextCheckAt[rule] = now;
        }
    }

    public async Task CheckNowAsync(WatchRule rule)
    {
        if (!_checksInFlight.Add(rule)) return;
        try
        {
            await CheckRuleAsync(rule);
        }
        finally
        {
            _checksInFlight.Remove(rule);
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        foreach (var rule in Rules)
        {
            if (!rule.Enabled) continue;
            if (!_nextCheckAt.TryGetValue(rule, out var due) || now < due) continue;

            _nextCheckAt[rule] = now.AddSeconds(Math.Max(1, rule.IntervalSeconds));

            if (_checksInFlight.Contains(rule)) continue;
            _checksInFlight.Add(rule);
            try
            {
                await CheckRuleAsync(rule);
            }
            finally
            {
                _checksInFlight.Remove(rule);
            }
        }
    }

    private async Task CheckRuleAsync(WatchRule rule)
    {
        try
        {
            if (rule.Type == RuleType.Service)
                await CheckServiceAsync(rule);
            else
                await CheckProcessAsync(rule);
        }
        catch (Exception ex)
        {
            rule.Status = $"Ошибка: {ex.Message}";
            Log($"[{rule.Name}] Ошибка проверки: {ex.Message}");
        }
        finally
        {
            rule.LastChecked = DateTime.Now;
        }
    }

    private async Task CheckServiceAsync(WatchRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ServiceName))
        {
            rule.Status = "Не задано имя службы";
            return;
        }

        using var sc = new ServiceController(rule.ServiceName);
        try
        {
            sc.Refresh();
            var status = sc.Status;

            if (status == ServiceControllerStatus.Running)
            {
                rule.Status = "Работает";
                return;
            }

            if (status == ServiceControllerStatus.StartPending)
            {
                rule.Status = "Запускается…";
                return;
            }

            rule.Status = $"Остановлена ({status}) — запуск…";
            Log($"[{rule.Name}] Служба \"{rule.ServiceName}\" в состоянии {status}, запускаю.");

            await Task.Run(() =>
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            });

            sc.Refresh();
            rule.Status = sc.Status == ServiceControllerStatus.Running
                ? "Работает (перезапущена)"
                : $"Не удалось запустить ({sc.Status})";
        }
        catch (InvalidOperationException)
        {
            rule.Status = "Служба не найдена";
            Log($"[{rule.Name}] Служба \"{rule.ServiceName}\" не найдена.");
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            rule.Status = "Таймаут запуска службы";
            Log($"[{rule.Name}] Таймаут ожидания запуска службы \"{rule.ServiceName}\".");
        }
    }

    private async Task CheckProcessAsync(WatchRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ProcessPath))
        {
            rule.Status = "Не задан процесс";
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(rule.ProcessPath);
        var running = Process.GetProcessesByName(processName);
        if (running.Length > 0)
        {
            rule.Status = "Работает";
            foreach (var p in running) p.Dispose();
            return;
        }

        rule.Status = "Не запущен — запуск…";
        Log($"[{rule.Name}] Процесс \"{processName}\" не найден, запускаю.");

        try
        {
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = rule.ProcessPath,
                    Arguments = rule.ProcessArguments ?? string.Empty,
                    UseShellExecute = true
                };
                var workDir = Path.GetDirectoryName(rule.ProcessPath);
                if (!string.IsNullOrEmpty(workDir))
                    psi.WorkingDirectory = workDir;

                Process.Start(psi);
            });
            rule.Status = "Запущен";
        }
        catch (Exception ex)
        {
            rule.Status = $"Не удалось запустить: {ex.Message}";
            Log($"[{rule.Name}] Не удалось запустить процесс: {ex.Message}");
        }
    }

    private void Log(string message) => LogMessage?.Invoke(this, message);
}
