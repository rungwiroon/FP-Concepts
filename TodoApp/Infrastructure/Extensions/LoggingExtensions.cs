using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Extensions;

/// <summary>
/// Logging extensions using the Has<M, RT, T>.ask pattern.
/// Generic over any monad M and runtime RT that provides LoggerIO.
/// </summary>
public static class LoggingExtensions
{
    public static K<M, Unit> LogInfo<M, RT>(string message)
        where M : Monad<M>
        where RT : Has<M, LoggerIO> =>
        Logger<M, RT>.logInfo(message);

    public static K<M, Unit> LogError<M, RT>(string message)
        where M : Monad<M>
        where RT : Has<M, LoggerIO> =>
        Logger<M, RT>.logError(message);

    public static K<M, A> WithLogging<M, RT, A>(
        this K<M, A> operation,
        string startMessage,
        Func<A, string> successMessage)
        where M : Monad<M>
        where RT : Has<M, LoggerIO>
    {
        return
            from _ in LogInfo<M, RT>(startMessage)
            from result in operation
            from __ in LogInfo<M, RT>(successMessage(result))
            select result;
    }
}
