using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Extensions;

/// <summary>
/// Metrics extensions using the Has<M, RT, T>.ask pattern.
/// Generic over any monad M and runtime RT that provides LoggerIO.
/// </summary>
public static class MetricsExtensions
{
    public static K<M, A> WithMetrics<M, RT, A>(
        this K<M, A> operation,
        string operationName)
        where M : Monad<M>, MonadIO<M>
        where RT : Has<M, LoggerIO>
    {
        return
            from startTime in M.Pure(DateTime.UtcNow)
            from result in operation
            from endTime in M.Pure(DateTime.UtcNow)
            from _ in LoggingExtensions.LogInfo<M, RT>($"Metrics - {operationName}: {(endTime - startTime).TotalMilliseconds}ms")
            select result;
    }
}
