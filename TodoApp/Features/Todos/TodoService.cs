using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using TodoApp.Domain;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Extensions;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

/// <summary>
/// TodoService using Has<M, RT, T>.ask pattern with full CRUD operations.
/// Generic over M (monad) and RT (runtime with capabilities).
/// Uses high-level domain operations - no DbContext exposure!
/// </summary>
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    /// <summary>
    /// List all todos ordered by creation date (newest first).
    /// </summary>
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.getAllTodos()
        from sorted in M.Pure(todos.OrderByDescending(t => t.CreatedAt).ToList())
        from __ in Logger<M, RT>.logInfo("Found {TodosCount} todos", sorted.Count)
        select sorted;

    /// <summary>
    /// Get a single todo by ID.
    /// Returns error if not found (404).
    /// Pattern: Option + .To<M, A>() for not-found handling
    /// </summary>
    public static K<M, Todo> Get(int id) =>
        from _ in Logger<M, RT>.logInfo("Getting todo by ID: {Id}", id)
        from todoOpt in Database<M, RT>.getTodoById(id)
        from todo in todoOpt.To<M, Todo>(() => Error.New(404, $"Todo with id {id} not found"))
        from __ in Logger<M, RT>.logInfo("Found todo: {TodoTitle}", todo.Title)
        select todo;

    /// <summary>
    /// Create a new todo with validation.
    /// Pattern:
    /// - Use M.Pure to create entity
    /// - Use .To<M, A>() extension to convert Validation to K<M, A> (purely functional!)
    /// - No exceptions needed!
    /// </summary>
    public static K<M, Todo> Create(string title, string? description) =>
        from _ in Logger<M, RT>.logInfo("Creating todo: {Title}", title)
        from now in Time<M, RT>.UtcNow
        from newTodo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = now
        })
        from validated in TodoValidation.Validate(newTodo).To<M, Todo>()
        from saved in Database<M, RT>.addTodo(validated)
        from __ in Logger<M, RT>.logInfo("Created todo with ID: {SavedId}", saved.Id)
        select saved;

    /// <summary>
    /// Update an existing todo with validation.
    /// Pattern:
    /// - Get existing (with 404 handling)
    /// - Use M.Pure to create updated entity
    /// - Use .To<M, A>() extension for functional validation
    /// - No exceptions!
    /// </summary>
    public static K<M, Todo> Update(int id, string title, string? description) =>
        from _ in Logger<M, RT>.logInfo("Updating todo {Id}", id)
        from existing in Get(id)
        from updatedTodo in M.Pure(existing with
        {
            Title = title,
            Description = description
        })
        from validated in TodoValidation.Validate(updatedTodo).To<M, Todo>()
        from saved in Database<M, RT>.updateTodo(validated)
        from __ in Logger<M, RT>.logInfo("Updated todo {Id}", id)
        select saved;

    /// <summary>
    /// Toggle completion status of a todo.
    /// Pattern: Get existing + create toggled entity + update
    /// </summary>
    public static K<M, Todo> ToggleComplete(int id) =>
        from _ in Logger<M, RT>.logInfo("Toggling completion for todo {Id}", id)
        from existing in Get(id)
        from now in Time<M, RT>.UtcNow
        from toggled in M.Pure(existing with
        {
            IsCompleted = !existing.IsCompleted,
            CompletedAt = !existing.IsCompleted ? now : null
        })
        from updated in Database<M, RT>.updateTodo(toggled)
        from __ in Logger<M, RT>.logInfo("Todo {Id} marked as {Status}", id, updated.IsCompleted ? "completed" : "incomplete")
        select updated;

    /// <summary>
    /// Delete a todo by ID.
    /// Pattern: Get existing + delete
    /// </summary>
    public static K<M, Unit> Delete(int id) =>
        from _ in Logger<M, RT>.logInfo("Deleting todo {Id}", id)
        from existing in Get(id)
        from __ in Database<M, RT>.deleteTodo(existing)
        from ___ in Logger<M, RT>.logInfo("Deleted todo {Id}", id)
        select unit;
}
