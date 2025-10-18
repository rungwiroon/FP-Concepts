using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// CancellationToken capability module with dual type parameters.
/// Follows the pattern: CancellationTokenCapability<M, RT> where M : Monad<M> and RT : Has<M, CancellationTokenIO>
/// Uses CORRECTED Has<M, RT, CancellationTokenIO>.ask (three params, lowercase)
/// </summary>
public static class CancellationTokenCapability<M, RT>
    where M : Monad<M>
    where RT : Has<M, CancellationTokenIO>
{
    /// <summary>
    /// Get the cancellation token from the runtime.
    /// </summary>
    public static K<M, CancellationToken> getCancellationToken() =>
        from ct in Has<M, RT, CancellationTokenIO>.ask
        from token in M.Pure(ct.GetCancellationToken())
        select token;
}
