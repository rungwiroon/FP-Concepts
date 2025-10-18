namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Logging capability trait.
/// Simple interface with plain methods - no IO or Eff wrappers needed!
/// </summary>
public interface LoggerIO
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}
