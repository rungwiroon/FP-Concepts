using LanguageExt;

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Logging capability trait.
/// Returns Unit instead of void for proper functional composition.
/// </summary>
public interface LoggerIO
{
    Unit LogInfo(string message);
    Unit LogWarning(string message);
    Unit LogError(string message, Exception? ex = null);
}
