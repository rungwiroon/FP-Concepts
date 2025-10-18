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
                .ContinueWith(t => Optional(t.Result), ct))
        from todo in todoOpt.Match(
            Some: t => M.Pure(t),
            None: () => M.Fail<Todo>(Error.New(404, $"Todo with id {id} not found")))
        from __ in Logger<M, RT>.logInfo($"Found todo: {todo.Title}")
        select todo;

    /// <summary>
    /// Create a new todo with validation.
    /// Pattern:
    /// - Create entity inline within database operation (no M.Pure needed)
    /// - Use Validation.ToEff() for validation errors
    /// </summary>
    public static K<M, Todo> Create(string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from todo in Database<M, RT>.liftIO((ctx, ct) =>
        {
            var newTodo = new Todo
            {
                Title = title,
                Description = description,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            // Validate inline
            var validation = TodoValidation.Validate(newTodo);
            if (validation.IsFail)
            {
                // Match to extract error message
                var errorMessage = validation.Match(
                    Succ: _ => "",
                    Fail: error => error.Message);
                throw new InvalidOperationException(errorMessage);
            }

            ctx.Todos.Add(newTodo);
            return ctx.SaveChangesAsync(ct).ContinueWith(_ => newTodo, ct);
        })
        from __ in Logger<M, RT>.logInfo($"Created todo with ID: {todo.Id}")
        select todo;

    /// <summary>
    /// Update an existing todo with validation.
    /// Pattern: Combines Get (not-found handling) with inline update and validation
    /// </summary>
    public static K<M, Todo> Update(int id, string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Updating todo {id}")
        from existing in Get(id)
        from updated in Database<M, RT>.liftIO((ctx, ct) =>
        {
            // Create updated entity
            var updatedTodo = existing with
            {
                Title = title,
                Description = description
            };

            // Validate inline
            var validation = TodoValidation.Validate(updatedTodo);
            if (validation.IsFail)
            {
                // Match to extract error message
                var errorMessage = validation.Match(
                    Succ: _ => "",
                    Fail: error => error.Message);
                throw new InvalidOperationException(errorMessage);
            }

            // Update entity
            ctx.Entry(updatedTodo).State = EntityState.Modified;
            return ctx.SaveChangesAsync(ct).ContinueWith(_ => updatedTodo, ct);
        })
        from __ in Logger<M, RT>.logInfo($"Updated todo {id}")
        select updated;

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
            return ctx.SaveChangesAsync(ct).ContinueWith(_ => toggledTodo, ct);
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
            return ctx.SaveChangesAsync(ct).ContinueWith(_ => unit, ct);
        })
        from ___ in Logger<M, RT>.logInfo($"Deleted todo {id}")
        select unit;
}
