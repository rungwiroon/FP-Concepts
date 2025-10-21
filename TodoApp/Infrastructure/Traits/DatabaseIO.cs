using LanguageExt;
using TodoApp.Domain;

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Sort order options for todo queries.
/// </summary>
public enum TodoSortOrder
{
    CreatedAtDescending,
    CreatedAtAscending,
    TitleAscending,
    TitleDescending
}

/// <summary>
/// Database capability trait with domain operations.
/// No DbContext exposure - enables pure unit testing!
/// Operations are high-level and domain-focused.
/// Supports query composition for performance (e.g., sorting at database level).
/// </summary>
public interface DatabaseIO
{
    /// <summary>
    /// Get all todos from the database with optional sorting.
    /// Sorting is performed at the database level for optimal performance.
    /// </summary>
    Task<List<Todo>> GetAllTodosAsync(TodoSortOrder sortOrder, CancellationToken cancellationToken);

    /// <summary>
    /// Get a single todo by ID.
    /// Returns None if not found.
    /// </summary>
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Add a new todo to the database.
    /// Returns the saved todo with generated ID.
    /// </summary>
    Task<Todo> AddTodoAsync(Todo todo, CancellationToken cancellationToken);

    /// <summary>
    /// Update an existing todo in the database.
    /// Returns the updated todo.
    /// </summary>
    Task<Todo> UpdateTodoAsync(Todo todo, CancellationToken cancellationToken);

    /// <summary>
    /// Delete a todo from the database.
    /// </summary>
    Task<Unit> DeleteTodoAsync(Todo todo, CancellationToken cancellationToken);
}
