using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Database capability module with domain operations.
/// No DbContext exposure - all operations are high-level and domain-focused.
/// This enables pure unit testing without EF Core infrastructure.
/// </summary>
public static class Database<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    /// <summary>
    /// Get all todos from the database.
    /// </summary>
    public static K<M, List<Todo>> getAllTodos() =>
        from db in Has<M, RT, DatabaseIO>.ask
        from todos in M.LiftIO(IO.liftAsync(env => db.GetAllTodosAsync(env.Token)))
        select todos;

    /// <summary>
    /// Get a single todo by ID.
    /// Returns None if not found.
    /// </summary>
    public static K<M, Option<Todo>> getTodoById(int id) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from todo in M.LiftIO(IO.liftAsync(env => db.GetTodoByIdAsync(id, env.Token)))
        select todo;

    /// <summary>
    /// Add a new todo to the database.
    /// </summary>
    public static K<M, Todo> addTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from saved in M.LiftIO(IO.liftAsync(env => db.AddTodoAsync(todo, env.Token)))
        select saved;

    /// <summary>
    /// Update an existing todo in the database.
    /// </summary>
    public static K<M, Todo> updateTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from updated in M.LiftIO(IO.liftAsync(env => db.UpdateTodoAsync(todo, env.Token)))
        select updated;

    /// <summary>
    /// Delete a todo from the database.
    /// </summary>
    public static K<M, Unit> deleteTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from _ in M.LiftIO(IO.liftAsync(env => db.DeleteTodoAsync(todo, env.Token)))
        select Unit.Default;
}
