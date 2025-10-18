using LanguageExt;
using LanguageExt.Traits;
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Features.Todos;

/// <summary>
/// Generic over M (monad) and RT (runtime with capabilities).
/// Demonstrates the working trait-based approach with Database and Logger capabilities.
/// </summary>
public static class TodoServiceSimple<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    /// <summary>
    /// List all todos - FULLY WORKING with Has<M, RT, T>.ask pattern
    /// </summary>
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(ct))
        from __ in Logger<M, RT>.logInfo($"Found {todos.Count} todos")
        select todos;
}
