using System.Diagnostics.CodeAnalysis;
using LanguageExt;

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Logging capability trait.
/// Returns Unit instead of void for proper functional composition.
/// </summary>
public interface LoggerIO
{
    Unit LogInfo([ConstantExpected] string message, params object[] args);
    Unit LogWarning([ConstantExpected] string message, params object[] args);
    Unit LogError(Exception? ex = null, [ConstantExpected] string? message = null, params object[] args);
}
