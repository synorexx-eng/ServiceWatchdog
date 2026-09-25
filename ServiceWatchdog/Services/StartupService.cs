using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace ServiceWatchdog.Services;

/// <summary>
/// Управляет автозапуском через Планировщик заданий (задача "При входе в систему", наивысшие права).
/// Ярлык в папке автозагрузки не подходит: Windows не запускает оттуда приложения с requireAdministrator.
/// </summary>
public class StartupService
{
    private const string TaskName = "ServiceWatchdog";

    private static readonly string LegacyShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ServiceWatchdog.lnk");

    public bool IsEnabled => RunSchtasks("/Query", "/TN", TaskName) == 0;

    public void SetEnabled(bool enabled)
    {
        // Убираем ярлык, созданный старыми версиями.
        if (File.Exists(LegacyShortcutPath))
            File.Delete(LegacyShortcutPath);

        if (enabled)
            CreateTask();
        else if (IsEnabled && RunSchtasks("/Delete", "/TN", TaskName, "/F") != 0)
            throw new InvalidOperationException("Не удалось удалить задачу автозапуска.");
    }

    private static void CreateTask()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");
        var userId = WindowsIdentity.GetCurrent().Name;

        // XML нужен, чтобы снять лимит времени выполнения (по умолчанию 72 часа — задачу бы убило).
        var xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>ServiceWatchdog</Description></RegistrationInfo>
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled><UserId>{SecurityElement.Escape(userId)}</UserId></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SecurityElement.Escape(userId)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(exePath)}</Command>
                  <Arguments>--minimized</Arguments>
                  <WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exePath))}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;

        var xmlPath = Path.Combine(Path.GetTempPath(), $"ServiceWatchdog-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, xml, Encoding.Unicode);
            if (RunSchtasks("/Create", "/TN", TaskName, "/XML", xmlPath, "/F") != 0)
                throw new InvalidOperationException("Не удалось создать задачу автозапуска в Планировщике заданий.");
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    private static int RunSchtasks(params string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }
}
