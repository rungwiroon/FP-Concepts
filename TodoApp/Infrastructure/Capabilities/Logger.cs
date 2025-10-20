using System.Diagnostics.CodeAnalysis;
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Logger utility module with dual type parameters.
/// Follows the pattern: Logger<M, RT> where M : Monad<M> and RT : Has<M, LoggerIO>
/// This allows the logger to work with any monad M, not just Eff.
/// </summary>
public static class Logger<M, RT>
    where M : Monad<M>
    where RT : Has<M, LoggerIO>
{
    /// <summary>
    /// Log an informational message.
    /// </summary>
    public static K<M, Unit> logInfo([ConstantExpected] string message, params object[] args) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogInfo(message, args);

    /// <summary>
    /// Log a warning message.
    /// </summary>
    public static K<M, Unit> logWarning([ConstantExpected] string message, params object[] args) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogWarning(message, args);

    /// <summary>
    /// Log an error message with optional exception.
    /// </summary>
    public static K<M, Unit> logError(Exception? ex = null, [ConstantExpected] string? message = null, params object[] args) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogError(ex, message, args);
}
