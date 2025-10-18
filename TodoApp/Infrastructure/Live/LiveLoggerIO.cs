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
    public Unit LogInfo(string message)
    {
        logger.LogInformation(message);
        return unit;
    }

    public Unit LogWarning(string message)
    {
        logger.LogWarning(message);
        return unit;
    }

    public Unit LogError(string message, Exception? ex = null)
    {
        logger.LogError(ex, message);
        return unit;
    }
}
