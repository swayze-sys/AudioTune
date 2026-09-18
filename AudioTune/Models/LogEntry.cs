namespace AudioTune.Models;

public sealed record LogEntry(DateTime Time, string Message, LogLevel Level = LogLevel.Info);
public enum LogLevel { Trace, Info, Success, Warning, Error }
