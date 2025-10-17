using LanguageExt;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

public static class MetricsExtensions
{
    public static K<Db, A> WithMetrics<A>(
        this K<Db, A> operation,
        string operationName)
    {
        return
            from startTime in Db.LiftIO(() => Task.FromResult(DateTime.UtcNow))
            from result in operation
            from endTime in Db.LiftIO(() => Task.FromResult(DateTime.UtcNow))
            from _ in LoggingExtensions.LogInfo($"Metrics - {operationName}: {(endTime - startTime).TotalMilliseconds}ms")
            select result;
    }
}
