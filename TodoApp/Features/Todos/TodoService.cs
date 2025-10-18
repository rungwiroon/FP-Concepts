using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

/// <summary>
/// TodoService using Has<M, RT, T>.ask pattern with full CRUD operations.
/// Generic over M (monad) and RT (runtime with capabilities).
/// All operations migrated from Db<A> pattern using the research document patterns:
/// - Optional().ToEff() for not-found handling
/// - Validation<Error, A>.ToEff() for validation
/// - Inline entity creation within database operations
/// </summary>
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    /// <summary>
    /// List all todos ordered by creation date (newest first).
    /// </summary>
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(ct))
        from __ in Logger<M, RT>.logInfo($"Found {todos.Count} todos")
        select todos;

    /// <summary>
    /// Get a single todo by ID.
    /// Returns error if not found (404).
    /// Pattern: Optional() + match for not-found handling
    /// Uses AsNoTracking() to avoid EF Core tracking conflicts
    /// </summary>
    public static K<M, Todo> Get(int id) =>
        from _ in Logger<M, RT>.logInfo($"Getting todo by ID: {id}")
        from todoOpt in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                .Map(t => Optional(t.Result)))
        from todo in todoOpt.Match(
            Some: t => M.Pure(t),
            None: () => M.Fail<Todo>(Error.New(404, $"Todo with id {id} not found")))
        from __ in Logger<M, RT>.logInfo($"Found todo: {todo.Title}")
        select todo;

    /// <summary>
    /// Create a new todo with validation.
    /// Pattern:
    /// - Use M.Pure to create entity
    /// - Use Validation.Match to return M.Pure or M.Fail (purely functional!)
    /// - No exceptions needed!
    /// </summary>
    public static K<M, Todo> Create(string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from newTodo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        })
        from validated in TodoValidation.Validate(newTodo).Match(
            Succ: _ => M.Pure(newTodo),
            Fail: err => M.Fail<Todo>(err))
        from saved in Database<M, RT>.liftIO((ctx, ct) =>
        {
            ctx.Todos.Add(validated);
            return ctx.SaveChangesAsync(ct).Map(_ => validated);
        })
        from __ in Logger<M, RT>.logInfo($"Created todo with ID: {saved.Id}")
        select saved;

    /// <summary>
    /// Update an existing todo with validation.
    /// Pattern:
    /// - Get existing (with 404 handling)
    /// - Use M.Pure to create updated entity
    /// - Use Validation.Match for functional validation
    /// - No exceptions!
    /// </summary>
    public static K<M, Todo> Update(int id, string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Updating todo {id}")
        from existing in Get(id)
        from updatedTodo in M.Pure(existing with
        {
            Title = title,
            Description = description
        })
        from validated in TodoValidation.Validate(updatedTodo).Match(
            Succ: _ => M.Pure(updatedTodo),
            Fail: err => M.Fail<Todo>(err))
        from saved in Database<M, RT>.liftIO((ctx, ct) =>
        {
            ctx.Entry(validated).State = EntityState.Modified;
            return ctx.SaveChangesAsync(ct).Map(_ => validated);
        })
        from __ in Logger<M, RT>.logInfo($"Updated todo {id}")
        select saved;

    /// <summary>
    /// Toggle completion status of a todo.
    /// Pattern: Get existing + inline entity update
    /// </summary>
    public static K<M, Todo> ToggleComplete(int id) =>
        from _ in Logger<M, RT>.logInfo($"Toggling completion for todo {id}")
        from existing in Get(id)
        from updated in Database<M, RT>.liftIO((ctx, ct) =>
        {
            // Create toggled entity
            var toggledTodo = existing with
            {
                IsCompleted = !existing.IsCompleted,
                CompletedAt = !existing.IsCompleted ? DateTime.UtcNow : null
            };

            // Update entity
            ctx.Entry(toggledTodo).State = EntityState.Modified;
            return ctx.SaveChangesAsync(ct).Map(_ => toggledTodo);
        })
        from __ in Logger<M, RT>.logInfo($"Todo {id} marked as {(updated.IsCompleted ? "completed" : "incomplete")}")
        select updated;

    /// <summary>
    /// Delete a todo by ID.
    /// Pattern: Get existing + inline removal
    /// </summary>
    public static K<M, Unit> Delete(int id) =>
        from _ in Logger<M, RT>.logInfo($"Deleting todo {id}")
        from existing in Get(id)
        from __ in Database<M, RT>.liftIO((ctx, ct) =>
        {
            ctx.Todos.Remove(existing);
            return ctx.SaveChangesAsync(ct).Map(_ => unit);
        })
        from ___ in Logger<M, RT>.logInfo($"Deleted todo {id}")
        select unit;
}
