using System.Diagnostics.CodeAnalysis;
using LanguageExt;
using Microsoft.Extensions.Logging;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of LoggerIO wrapping ILogger.
/// Returns Unit for proper functional composition.
/// </summary>
public class LiveLoggerIO(ILogger logger) : LoggerIO
{
    public Unit LogInfo([ConstantExpected]string message, params object[] args)
    {
        // CA2254: Message is dynamic by design in functional architecture
        #pragma warning disable CA2254
        logger.LogInformation(message, args);
        #pragma warning restore CA2254
        return unit;
    }

    public Unit LogWarning([ConstantExpected]string message, params object[] args)
    {
        // CA2254: Message is dynamic by design in functional architecture
#pragma warning disable CA2254
        logger.LogWarning(message, args);
        #pragma warning restore CA2254
        return unit;
    }

    public Unit LogError(Exception? ex = null, [ConstantExpected]string? message = null, params object[] args)
    {
        // CA2254: Message is dynamic by design in functional architecture
        #pragma warning disable CA2254
        logger.LogError(ex, message, args);
        #pragma warning restore CA2254
        return unit;
    }
}
