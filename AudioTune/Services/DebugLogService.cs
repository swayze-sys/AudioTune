using System.Collections.ObjectModel;
using AudioTune.Models;

namespace AudioTune.Services;

public sealed class DebugLogService
{
    public ObservableCollection<LogEntry> Entries { get; } = new();
    public event Action<LogEntry>? EntryAdded;

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry(DateTime.Now, message, level);
        App.Current?.Dispatcher.Invoke(() =>
        {
            Entries.Add(entry);
            while (Entries.Count > 500) Entries.RemoveAt(0);
            EntryAdded?.Invoke(entry);
        });
    }

    public void Clear() => App.Current?.Dispatcher.Invoke(Entries.Clear);
}
