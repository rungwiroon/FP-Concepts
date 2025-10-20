using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Data;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Database utility module with dual type parameters.
/// Follows the pattern: Database<M, RT> where M : Monad<M> and RT : Has<M, DatabaseIO>
/// This allows the database operations to work with any monad M, not just Eff.
/// </summary>
public static class Database<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    /// <summary>
    /// Get the database context from the runtime.
    /// </summary>
    public static K<M, AppDbContext> getContext() =>
        from db in Has<M, RT, DatabaseIO>.ask
        from ctx in M.LiftIO(IO.lift(env => db.GetContext()))
        select ctx;

    /// <summary>
    /// Save changes to the database.
    /// </summary>
    public static K<M, Unit> saveChanges() =>
        from db in Has<M, RT, DatabaseIO>.ask
        from _ in M.LiftIO(IO.liftAsync((env) => db.SaveChangesAsync(env.Token)))
        select Unit.Default;

    /// <summary>
    /// Lift an async database operation into the monad.
    /// This is a helper for running arbitrary async operations with the context.
    /// </summary>
    public static K<M, A> liftIO<A>(Func<AppDbContext, CancellationToken, Task<A>> operation) =>
        from ctx in getContext()
        from result in M.LiftIO(IO.liftAsync((env) => operation(ctx, env.Token)))
        select result;
}
