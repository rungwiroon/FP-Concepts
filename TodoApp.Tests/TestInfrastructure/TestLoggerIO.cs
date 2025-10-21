using System.Diagnostics.CodeAnalysis;
using LanguageExt;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test implementation of LoggerIO that captures log messages for assertions.
/// </summary>
public class TestLoggerIO : LoggerIO
{
    private readonly List<LogEntry> _logs = new();

    public IReadOnlyList<LogEntry> Logs => _logs;

    public Unit LogInfo([ConstantExpected] string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Info, message, args, null));
        return Unit.Default;
    }

    public Unit LogWarning([ConstantExpected] string message, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Warning, message, args, null));
        return Unit.Default;
    }

    public Unit LogError(Exception? ex = null, [ConstantExpected] string? message = null, params object[] args)
    {
        _logs.Add(new LogEntry(LogLevel.Error, message ?? string.Empty, args, ex));
        return Unit.Default;
    }

    public void Clear() => _logs.Clear();

    public bool HasInfo(string message) =>
        _logs.Any(l => l.Level == LogLevel.Info && l.Message.Contains(message));

    public bool HasWarning(string message) =>
        _logs.Any(l => l.Level == LogLevel.Warning && l.Message.Contains(message));

    public bool HasError(string message) =>
        _logs.Any(l => l.Level == LogLevel.Error && l.Message.Contains(message));

    public record LogEntry(LogLevel Level, string Message, object[] Args, Exception? Exception);

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
