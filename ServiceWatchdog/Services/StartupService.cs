using System.IO;

namespace ServiceWatchdog.Services;

/// <summary>Управляет ярлыком в папке автозагрузки текущего пользователя.</summary>
public class StartupService
{
    private static readonly string StartupFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static readonly string ShortcutPath =
        Path.Combine(StartupFolder, "ServiceWatchdog.lnk");

    public bool IsEnabled => File.Exists(ShortcutPath);

    public void SetEnabled(bool enabled)
    {
        if (enabled)
            CreateShortcut();
        else if (File.Exists(ShortcutPath))
            File.Delete(ShortcutPath);
    }

    private static void CreateShortcut()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

        // IWshRuntimeLibrary недоступен без COM-референса, поэтому создаём .lnk через WScript.Shell по позднему связыванию.
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell недоступен.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            try
            {
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.WindowStyle = 7; // minimized
                shortcut.Description = "ServiceWatchdog";
                shortcut.IconLocation = exePath;
                shortcut.Save();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}
