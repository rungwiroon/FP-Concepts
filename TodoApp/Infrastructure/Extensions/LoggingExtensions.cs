using System.Diagnostics.CodeAnalysis;
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
    public static K<M, Unit> LogInfo<M, RT>([ConstantExpected]string message, params object[] args)
        where M : Monad<M>
        where RT : Has<M, LoggerIO> =>
        Logger<M, RT>.logInfo(message, args);

    public static K<M, Unit> LogError<M, RT>(
        Exception? ex = null, [ConstantExpected] string? message = null, params object[] args)
        where M : Monad<M>
        where RT : Has<M, LoggerIO> =>
        Logger<M, RT>.logError(ex, message, args);

    public static K<M, A> WithLogging<M, RT, A>(
        this K<M, A> operation,
        string startMessage,
        Func<A, string> successMessage)
        where M : Monad<M>
        where RT : Has<M, LoggerIO>
    {
        return
            from _ in LogInfo<M, RT>("{Message}", startMessage)
            from result in operation
            from __ in LogInfo<M, RT>("{Message}", successMessage(result))
            select result;
    }
}
