namespace ServiceWatchdog.Models;

public class AppSettings
{
    public List<WatchRule> Rules { get; set; } = new();

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }
}
