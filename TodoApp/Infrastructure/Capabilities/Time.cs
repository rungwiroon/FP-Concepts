using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Time capability module using Has<M, RT, T>.ask pattern (3 params, lowercase)
/// Provides pure functional access to time operations
/// </summary>
public static class Time<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, TimeIO>
{
    /// <summary>
    /// Get current UTC time from the runtime
    /// </summary>
    public static K<M, DateTime> getUtcNow =>
        from timeIO in Has<M, RT, TimeIO>.ask
        from now in M.Pure(timeIO.GetUtcNow())
        select now;
}
