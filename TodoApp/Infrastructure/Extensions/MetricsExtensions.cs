using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Extensions;

/// <summary>
/// Metrics extensions using the Has<M, RT, T>.ask pattern.
/// Generic over any monad M and runtime RT that provides LoggerIO and TimeIO.
/// </summary>
public static class MetricsExtensions
{
    public static K<M, A> WithMetrics<M, RT, A>(
        this K<M, A> operation,
        string operationName)
        where M : Monad<M>
        where RT : Has<M, LoggerIO>, Has<M, TimeIO>
    {
        return
            from startTime in Time<M, RT>.UtcNow
            from result in operation
            from endTime in Time<M, RT>.UtcNow
            from _ in LoggingExtensions.LogInfo<M, RT>($"Metrics - {operationName}: {(endTime - startTime).TotalMilliseconds}ms")
            select result;
    }
}
