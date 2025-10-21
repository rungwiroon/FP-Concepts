using LanguageExt;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Pure in-memory test implementation of DatabaseIO.
/// NO EF CORE - just a dictionary! Enables true unit testing.
/// Fast, simple, and completely isolated.
/// </summary>
public class TestDatabaseIO : DatabaseIO
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

    public Task<List<Todo>> GetAllTodosAsync(CancellationToken cancellationToken)
    {
        var todos = _todos.Values.ToList();
        return Task.FromResult(todos);
    }

    public Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken cancellationToken)
    {
        var result = _todos.TryGetValue(id, out var todo)
            ? Some(todo)
            : None;

        return Task.FromResult(result);
    }

    public Task<Todo> AddTodoAsync(Todo todo, CancellationToken cancellationToken)
    {
        // Assign ID if not set (simulating database auto-increment)
        if (todo.Id == 0)
        {
            todo = todo with { Id = _nextId++ };
        }

        _todos[todo.Id] = todo;
        return Task.FromResult(todo);
    }

    public Task<Todo> UpdateTodoAsync(Todo todo, CancellationToken cancellationToken)
    {
        if (!_todos.ContainsKey(todo.Id))
        {
            throw new InvalidOperationException($"Todo with id {todo.Id} not found");
        }

        _todos[todo.Id] = todo;
        return Task.FromResult(todo);
    }

    public Task<Unit> DeleteTodoAsync(Todo todo, CancellationToken cancellationToken)
    {
        _todos.Remove(todo.Id);
        return Task.FromResult(Unit.Default);
    }

    // Helper methods for test setup

    /// <summary>
    /// Seed the database with test todos.
    /// Much simpler than EF Core seeding!
    /// </summary>
    public void Seed(params Todo[] todos)
    {
        foreach (var todo in todos)
        {
            var id = todo.Id == 0 ? _nextId++ : todo.Id;
            _todos[id] = todo with { Id = id };

            if (id >= _nextId)
            {
                _nextId = id + 1;
            }
        }
    }

    /// <summary>
    /// Clear all todos from the database.
    /// </summary>
    public void Clear()
    {
        _todos.Clear();
        _nextId = 1;
    }

    /// <summary>
    /// Get the count of todos in the database.
    /// Useful for test assertions.
    /// </summary>
    public int Count => _todos.Count;
}
