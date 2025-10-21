using LanguageExt;
using LanguageExt.Common;
using TodoApp.Domain;
using TodoApp.Features.Todos;
using TodoApp.Tests.TestInfrastructure;
using Xunit;
using static LanguageExt.Prelude;

namespace TodoApp.Tests;

/// <summary>
/// Unit tests for TodoService using the trait-based capability pattern.
/// Demonstrates how easy it is to test with TestRuntime providing test implementations.
/// </summary>
public class TodoServiceTests : IDisposable
{
    private readonly TestRuntime _runtime;

    public TodoServiceTests()
    {
        _runtime = new TestRuntime(
            databaseName: Guid.NewGuid().ToString(),
            currentTime: new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        );
    }

    public void Dispose()
    {
        _runtime?.Dispose();
    }

    [Fact]
    public async Task List_ShouldReturnAllTodos_OrderedByCreatedAtDescending()
    {
        // Arrange
        var todo1 = new Todo
        {
            Id = 1,
            Title = "First Todo",
            Description = "Description 1",
            CreatedAt = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            IsCompleted = false
        };
        var todo2 = new Todo
        {
            Id = 2,
            Title = "Second Todo",
            Description = "Description 2",
            CreatedAt = new DateTime(2025, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            IsCompleted = false
        };

        _runtime.Database.Context.Todos.AddRange(todo1, todo2);
        await _runtime.Database.Context.SaveChangesAsync();

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>.List()
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var todos = result.Match(
            Succ: t => t,
            Fail: err => throw new Exception($"Expected success but got error: {err.Message}")
        );
        Assert.Equal(2, todos.Count);
        Assert.Equal("Second Todo", todos[0].Title); // Newest first
        Assert.Equal("First Todo", todos[1].Title);

        // Verify logging
        Assert.True(_runtime.Logger.HasInfo("Listing all todos"));
        Assert.True(_runtime.Logger.HasInfo("Found"));
    }

    [Fact]
    public async Task Get_ShouldReturnTodo_WhenExists()
    {
        // Arrange
        var todo = new Todo
        {
            Id = 1,
            Title = "Test Todo",
            Description = "Test Description",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        _runtime.Database.Context.Todos.Add(todo);
        await _runtime.Database.Context.SaveChangesAsync();

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>.Get(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var retrievedTodo = result.Match(
            Succ: t => t,
            Fail: err => throw new Exception($"Expected success but got error: {err.Message}")
        );
        Assert.Equal("Test Todo", retrievedTodo.Title);
        Assert.Equal("Test Description", retrievedTodo.Description);

        // Verify logging
        Assert.True(_runtime.Logger.HasInfo("Getting todo by ID"));
        Assert.True(_runtime.Logger.HasInfo("Found todo"));
    }

    [Fact]
    public async Task Get_ShouldReturnError_WhenTodoNotFound()
    {
        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>.Get(999)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var error = result.Match(
            Succ: _ => throw new Exception("Expected failure but got success"),
            Fail: err => err
        );
        Assert.Equal(404, error.Code);
        Assert.Contains("not found", error.Message.ToLower());
    }

    [Fact]
    public async Task Create_ShouldCreateTodo_WithValidInput()
    {
        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("Buy groceries", "Milk, eggs, bread")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var todo = result.Match(Succ: t => t, Fail: err => throw new Exception($"Expected success but got error: {err.Message}"));
        Assert.Equal("Buy groceries", todo.Title);
        Assert.Equal("Milk, eggs, bread", todo.Description);
        Assert.False(todo.IsCompleted);
        Assert.Equal(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), todo.CreatedAt);

        // Verify it was saved to database
        var savedTodo = await _runtime.Database.Context.Todos.FindAsync(todo.Id);
        Assert.NotNull(savedTodo);
        Assert.Equal("Buy groceries", savedTodo!.Title);

        // Verify logging
        Assert.True(_runtime.Logger.HasInfo("Creating todo"));
        Assert.True(_runtime.Logger.HasInfo("Created todo with ID"));
    }

    [Fact]
    public async Task Create_ShouldReturnError_WhenTitleIsEmpty()
    {
        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("", "Description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var error = result.Match(Succ: _ => throw new Exception("Expected failure but got success"), Fail: err => err);
        Assert.Contains("title", error.Message.ToLower());
    }

    [Fact]
    public async Task Create_ShouldReturnError_WhenTitleIsTooLong()
    {
        // Arrange
        var longTitle = new string('a', 201); // Max is 200

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create(longTitle, "Description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var error = result.Match(Succ: _ => throw new Exception("Expected failure but got success"), Fail: err => err);
        Assert.Contains("200", error.Message);
    }

    [Fact]
    public async Task Update_ShouldUpdateTodo_WhenExists()
    {
        // Arrange
        var todo = new Todo
        {
            Id = 1,
            Title = "Original Title",
            Description = "Original Description",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        _runtime.Database.Context.Todos.Add(todo);
        await _runtime.Database.Context.SaveChangesAsync();
        _runtime.Database.Context.ChangeTracker.Clear(); // Clear tracking to avoid conflicts

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Update(1, "Updated Title", "Updated Description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var updated = result.Match(Succ: t => t, Fail: err => throw new Exception($"Expected success but got error: {err.Message}"));
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Description", updated.Description);
        Assert.False(updated.IsCompleted); // Should remain unchanged

        // Verify logging
        Assert.True(_runtime.Logger.HasInfo("Updating todo"));
        Assert.True(_runtime.Logger.HasInfo("Updated todo"));
    }

    [Fact]
    public async Task Update_ShouldReturnError_WhenTodoNotFound()
    {
        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Update(999, "Title", "Description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var error = result.Match(Succ: _ => throw new Exception("Expected failure but got success"), Fail: err => err);
        Assert.Equal(404, error.Code);
    }

    [Fact]
    public async Task ToggleComplete_ShouldToggleCompletionStatus()
    {
        // Arrange
        var todo = new Todo
        {
            Id = 1,
            Title = "Test Todo",
            Description = "Description",
            CreatedAt = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            IsCompleted = false
        };

        _runtime.Database.Context.Todos.Add(todo);
        await _runtime.Database.Context.SaveChangesAsync();
        _runtime.Database.Context.ChangeTracker.Clear(); // Clear tracking to avoid conflicts

        // Act - Toggle to completed
        var result1 = await TodoService<Eff<TestRuntime>, TestRuntime>.ToggleComplete(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert - Should be completed
        var toggled1 = result1.Match(
            Succ: t => t,
            Fail: err => throw new Exception($"Expected success but got error: {err.Message}")
        );
        Assert.True(toggled1.IsCompleted);
        Assert.NotNull(toggled1.CompletedAt);
        Assert.Equal(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc), toggled1.CompletedAt);

        // Clear tracking before second toggle
        _runtime.Database.Context.ChangeTracker.Clear();

        // Act - Toggle back to incomplete
        var result2 = await TodoService<Eff<TestRuntime>, TestRuntime>.ToggleComplete(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert - Should be incomplete
        var toggled2 = result2.Match(
            Succ: t => t,
            Fail: err => throw new Exception($"Expected success but got error: {err.Message}")
        );
        Assert.False(toggled2.IsCompleted);
        Assert.Null(toggled2.CompletedAt);

        // Verify logging
        Assert.True(_runtime.Logger.HasInfo("Toggling"));
    }

    [Fact]
    public async Task Delete_ShouldDeleteTodo_WhenExists()
    {
        // Arrange
        var todo = new Todo
        {
            Id = 1,
            Title = "To Delete",
            Description = "Description",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        _runtime.Database.Context.Todos.Add(todo);
        await _runtime.Database.Context.SaveChangesAsync();
        _runtime.Database.Context.ChangeTracker.Clear(); // Clear tracking to avoid conflicts

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>.Delete(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        result.Match(
            Succ: _ => _,
            Fail: err => throw new Exception($"Expected success but got error: {err.Message}")
        );

        // Verify it was deleted from database - need to detach and refetch
        _runtime.Database.Context.ChangeTracker.Clear();
        var deletedTodo = await _runtime.Database.Context.Todos.FindAsync(1);
        Assert.Null(deletedTodo);

        // Verify logging
        Assert.True(_runtime.Logger.HasInfo("Deleting todo"));
        Assert.True(_runtime.Logger.HasInfo("Deleted todo"));
    }

    [Fact]
    public async Task Delete_ShouldReturnError_WhenTodoNotFound()
    {
        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>.Delete(999)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var error = result.Match(Succ: _ => throw new Exception("Expected failure but got success"), Fail: err => err);
        Assert.Equal(404, error.Code);
    }

    [Fact]
    public async Task TimeIO_ShouldUseTestTime()
    {
        // Arrange
        var specificTime = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        _runtime.Time.SetTime(specificTime);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("Time Test", "Test time tracking")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var todo = result.Match(Succ: t => t, Fail: err => throw new Exception($"Expected success but got error: {err.Message}"));
        Assert.Equal(specificTime, todo.CreatedAt);
    }

    [Fact]
    public async Task LoggerIO_ShouldCaptureAllLogMessages()
    {
        // Arrange
        _runtime.Logger.Clear();

        // Act
        await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("Logger Test", "Test logging")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.True(_runtime.Logger.Logs.Count >= 2); // At least "Creating" and "Created" logs
        Assert.Contains(_runtime.Logger.Logs, l => l.Message.Contains("Creating todo"));
        Assert.Contains(_runtime.Logger.Logs, l => l.Message.Contains("Created todo with ID"));
    }

    [Fact]
    public async Task DatabaseIO_ShouldIsolateTestData()
    {
        // Arrange - Create a todo in this test's isolated database
        await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("Isolated Todo", "Should not affect other tests")
            .RunAsync(_runtime, EnvIO.New());

        // Act - List todos
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>.List()
            .RunAsync(_runtime, EnvIO.New());

        // Assert - Should only see todos created in this test
        var todos = result.Match(Succ: t => t, Fail: err => throw new Exception($"Expected success but got error: {err.Message}"));
        Assert.Single(todos);
        Assert.Equal("Isolated Todo", todos[0].Title);
    }
}
