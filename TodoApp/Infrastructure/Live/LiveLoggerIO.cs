using Microsoft.Extensions.Logging;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of LoggerIO wrapping ILogger.
/// Simple wrapper - no IO or Eff wrapping needed!
/// </summary>
public class LiveLoggerIO(ILogger logger) : LoggerIO
{
    public void LogInfo(string message) =>
        logger.LogInformation(message);

    public void LogWarning(string message) =>
        logger.LogWarning(message);

    public void LogError(string message, Exception? ex = null) =>
        logger.LogError(ex, message);
}
