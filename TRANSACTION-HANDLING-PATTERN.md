# Transaction Handling Pattern for Multi-Entity Architecture

## The Problem

When you have separate repositories for different entities, you need a way to coordinate transactions across multiple repository operations:

```csharp
// ❌ PROBLEM: No transaction coordination
public static K<M, Project> CreateProjectWithInitialTask(int userId, string projectName, string taskTitle) =>
    from user in UserRepository<M, RT>.getUserById(userId)
    from project in ProjectRepository<M, RT>.createProject(new Project { ... })
    // ⚠️ If this fails, project is already saved!
    from task in TaskRepository<M, RT>.createTask(new ProjectTask { ... })
    select project;
```

**Issues:**
- Each repository calls `SaveChangesAsync()` independently
- No atomicity across repositories
- Partial failures leave inconsistent data
- Multiple database round-trips

## The Solution: Unit of Work Pattern as a Trait

Add a `IUnitOfWork` trait that manages database transactions, following the same Has pattern as other capabilities.

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Service                       │
│  (Coordinates multiple repositories + unit of work)          │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ UserRepo     │    │ ProjectRepo  │    │ UnitOfWork   │
│ (Has)        │    │ (Has)        │    │ (Has)        │
└──────────────┘    └──────────────┘    └──────────────┘
        │                   │                   │
        └───────────────────┴───────────────────┘
                            ▼
                    ┌──────────────┐
                    │  DbContext   │
                    │ (EF Core)    │
                    └──────────────┘
```

## Implementation

### Step 1: Define IUnitOfWork Trait

```csharp
// Infrastructure/Traits/IUnitOfWork.cs
namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Trait for managing database transactions.
/// Provides transaction boundaries and SaveChanges coordination.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all pending changes to the database.
    /// Used at the end of a successful operation.
    /// </summary>
    Task<Unit> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Begins a new database transaction.
    /// All subsequent operations will be part of this transaction
    /// until CommitTransactionAsync or RollbackTransactionAsync is called.
    /// </summary>
    Task<Unit> BeginTransactionAsync(CancellationToken ct);

    /// <summary>
    /// Commits the current transaction, making all changes permanent.
    /// </summary>
    Task<Unit> CommitTransactionAsync(CancellationToken ct);

    /// <summary>
    /// Rolls back the current transaction, discarding all changes.
    /// </summary>
    Task<Unit> RollbackTransactionAsync(CancellationToken ct);
}
```

### Step 2: Update Repository Traits (Remove SaveChanges)

**Key Change:** Repositories should NOT call `SaveChangesAsync()` themselves. They only modify the context, and the Unit of Work coordinates the save.

```csharp
// Infrastructure/Traits/ITodoRepository.cs
public interface ITodoRepository
{
    Task<List<Todo>> GetAllTodosAsync(TodoSortOrder sortOrder, CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);

    // ✅ These methods only modify the DbContext, they DON'T save
    void AddTodo(Todo todo);
    void UpdateTodo(Todo todo);
    void DeleteTodo(Todo todo);
}

// Infrastructure/Traits/IUserRepository.cs
public interface IUserRepository
{
    Task<Option<User>> GetUserByIdAsync(int id, CancellationToken ct);
    Task<Option<User>> GetUserByEmailAsync(string email, CancellationToken ct);

    // ✅ Only modify context, don't save
    void CreateUser(User user);
    void UpdateUser(User user);
    void DeleteUser(User user);
}

// Similar for IProjectRepository, ITaskRepository
```

### Step 3: Capability Module for Unit of Work

```csharp
// Infrastructure/Capabilities/UnitOfWork.cs
using LanguageExt;
using LanguageExt.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Static helper methods for accessing IUnitOfWork capability using Has pattern.
/// </summary>
public static class UnitOfWork<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, IUnitOfWork>
{
    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    public static K<M, Unit> saveChanges =>
        from uow in Has<M, RT, IUnitOfWork>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            uow.SaveChangesAsync(env.Token)))
        select result;

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    public static K<M, Unit> beginTransaction =>
        from uow in Has<M, RT, IUnitOfWork>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            uow.BeginTransactionAsync(env.Token)))
        select result;

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    public static K<M, Unit> commitTransaction =>
        from uow in Has<M, RT, IUnitOfWork>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            uow.CommitTransactionAsync(env.Token)))
        select result;

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    public static K<M, Unit> rollbackTransaction =>
        from uow in Has<M, RT, IUnitOfWork>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            uow.RollbackTransactionAsync(env.Token)))
        select result;

    /// <summary>
    /// Executes an operation within a transaction.
    /// Automatically commits on success or rolls back on failure.
    /// </summary>
    public static K<M, A> inTransaction<A>(K<M, A> operation)
        where M : Fallible<M> =>
        from _ in beginTransaction
        from result in operation.Catch(error =>
            from __ in rollbackTransaction
            from ___ in M.Fail<A>(error)
            select ___)
        from __ in commitTransaction
        select result;
}
```

### Step 4: Update Repository Capability Modules

```csharp
// Infrastructure/Capabilities/TodoRepository.cs
public static class TodoRepository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, ITodoRepository>
{
    // ✅ Read operations return values directly
    public static K<M, List<Todo>> getAllTodos(TodoSortOrder sortOrder) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todos in M.LiftIO(IO.liftAsync(env =>
            repo.GetAllTodosAsync(sortOrder, env.Token)))
        select todos;

    public static K<M, Option<Todo>> getTodoById(int id) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todo in M.LiftIO(IO.liftAsync(env =>
            repo.GetTodoByIdAsync(id, env.Token)))
        select todo;

    // ✅ Write operations only modify context, return Unit
    public static K<M, Unit> addTodo(Todo todo) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.AddTodo(todo);
            return unit;
        })
        select unit;

    public static K<M, Unit> updateTodo(Todo todo) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.UpdateTodo(todo);
            return unit;
        })
        select unit;

    public static K<M, Unit> deleteTodo(Todo todo) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.DeleteTodo(todo);
            return unit;
        })
        select unit;
}

// Infrastructure/Capabilities/UserRepository.cs
public static class UserRepository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, IUserRepository>
{
    public static K<M, Option<User>> getUserById(int id) =>
        from repo in Has<M, RT, IUserRepository>.ask
        from user in M.LiftIO(IO.liftAsync(env =>
            repo.GetUserByIdAsync(id, env.Token)))
        select user;

    public static K<M, Unit> createUser(User user) =>
        from repo in Has<M, RT, IUserRepository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.CreateUser(user);
            return unit;
        })
        select unit;

    // ... similar for other operations
}
```

### Step 5: Live Implementations

#### Live Unit of Work

```csharp
// Infrastructure/Live/LiveUnitOfWork.cs
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TodoApp.Data;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of IUnitOfWork using EF Core DbContext.
/// Manages transactions and SaveChanges coordination.
/// </summary>
public class LiveUnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;

    public async Task<Unit> SaveChangesAsync(CancellationToken ct)
    {
        await context.SaveChangesAsync(ct);
        return Unit.Default;
    }

    public async Task<Unit> BeginTransactionAsync(CancellationToken ct)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Transaction already in progress");

        _currentTransaction = await context.Database.BeginTransactionAsync(ct);
        return Unit.Default;
    }

    public async Task<Unit> CommitTransactionAsync(CancellationToken ct)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No transaction in progress");

        try
        {
            await context.SaveChangesAsync(ct);
            await _currentTransaction.CommitAsync(ct);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        return Unit.Default;
    }

    public async Task<Unit> RollbackTransactionAsync(CancellationToken ct)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No transaction in progress");

        try
        {
            await _currentTransaction.RollbackAsync(ct);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        return Unit.Default;
    }
}
```

#### Updated Live Repositories

```csharp
// Infrastructure/Live/LiveTodoRepository.cs
public class LiveTodoRepository(AppDbContext context) : ITodoRepository
{
    // ✅ Read operations unchanged
    public async Task<List<Todo>> GetAllTodosAsync(TodoSortOrder sortOrder, CancellationToken ct)
    {
        var query = context.Todos.AsNoTracking();
        query = sortOrder switch
        {
            TodoSortOrder.CreatedAtDescending => query.OrderByDescending(t => t.CreatedAt),
            TodoSortOrder.CreatedAtAscending => query.OrderBy(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };
        return await query.ToListAsync(ct);
    }

    public async Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct)
    {
        var todo = await context.Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        return Optional(todo);
    }

    // ✅ Write operations only modify context - NO SaveChangesAsync!
    public void AddTodo(Todo todo)
    {
        context.Todos.Add(todo);
    }

    public void UpdateTodo(Todo todo)
    {
        context.Entry(todo).State = EntityState.Modified;
    }

    public void DeleteTodo(Todo todo)
    {
        context.Todos.Remove(todo);
    }
}

// Infrastructure/Live/LiveUserRepository.cs
public class LiveUserRepository(AppDbContext context) : IUserRepository
{
    public async Task<Option<User>> GetUserByIdAsync(int id, CancellationToken ct)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return Optional(user);
    }

    public async Task<Option<User>> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
        return Optional(user);
    }

    public void CreateUser(User user)
    {
        context.Users.Add(user);
    }

    public void UpdateUser(User user)
    {
        context.Entry(user).State = EntityState.Modified;
    }

    public void DeleteUser(User user)
    {
        context.Users.Remove(user);
    }
}
```

### Step 6: Test Implementations

#### Test Unit of Work

```csharp
// Tests/TestInfrastructure/TestUnitOfWork.cs
using LanguageExt;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test implementation of IUnitOfWork.
/// Tracks transaction state for testing purposes.
/// </summary>
public class TestUnitOfWork : IUnitOfWork
{
    public bool IsInTransaction { get; private set; }
    public int SaveChangesCallCount { get; private set; }
    public int CommitCallCount { get; private set; }
    public int RollbackCallCount { get; private set; }

    public Task<Unit> SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.FromResult(Unit.Default);
    }

    public Task<Unit> BeginTransactionAsync(CancellationToken ct)
    {
        if (IsInTransaction)
            throw new InvalidOperationException("Transaction already in progress");

        IsInTransaction = true;
        return Task.FromResult(Unit.Default);
    }

    public Task<Unit> CommitTransactionAsync(CancellationToken ct)
    {
        if (!IsInTransaction)
            throw new InvalidOperationException("No transaction in progress");

        CommitCallCount++;
        IsInTransaction = false;
        return Task.FromResult(Unit.Default);
    }

    public Task<Unit> RollbackTransactionAsync(CancellationToken ct)
    {
        if (!IsInTransaction)
            throw new InvalidOperationException("No transaction in progress");

        RollbackCallCount++;
        IsInTransaction = false;
        return Task.FromResult(Unit.Default);
    }

    public void Reset()
    {
        IsInTransaction = false;
        SaveChangesCallCount = 0;
        CommitCallCount = 0;
        RollbackCallCount = 0;
    }
}
```

#### Updated Test Repositories

```csharp
// Tests/TestInfrastructure/TestTodoRepository.cs
public class TestTodoRepository : ITodoRepository
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

    // ✅ Read operations
    public Task<List<Todo>> GetAllTodosAsync(TodoSortOrder sortOrder, CancellationToken ct)
    {
        var todos = _todos.Values;
        var sorted = sortOrder switch
        {
            TodoSortOrder.CreatedAtDescending => todos.OrderByDescending(t => t.CreatedAt).ToList(),
            TodoSortOrder.CreatedAtAscending => todos.OrderBy(t => t.CreatedAt).ToList(),
            _ => todos.ToList()
        };
        return Task.FromResult(sorted);
    }

    public Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct)
    {
        var result = _todos.TryGetValue(id, out var todo) ? Some(todo) : None;
        return Task.FromResult(result);
    }

    // ✅ Write operations - synchronous, modify in-memory dictionary
    public void AddTodo(Todo todo)
    {
        if (todo.Id == 0)
            todo = todo with { Id = _nextId++ };
        _todos[todo.Id] = todo;
    }

    public void UpdateTodo(Todo todo)
    {
        if (!_todos.ContainsKey(todo.Id))
            throw new InvalidOperationException($"Todo {todo.Id} not found");
        _todos[todo.Id] = todo;
    }

    public void DeleteTodo(Todo todo)
    {
        _todos.Remove(todo.Id);
    }

    // Test helpers
    public void Seed(params Todo[] todos)
    {
        foreach (var todo in todos)
            _todos[todo.Id] = todo;
        _nextId = _todos.Count > 0 ? _todos.Keys.Max() + 1 : 1;
    }

    public void Clear() => _todos.Clear();
    public int Count => _todos.Count;
}

// Similar pattern for TestUserRepository, TestProjectRepository, etc.
```

### Step 7: Update Runtime

```csharp
// Infrastructure/AppRuntime.cs
public class AppRuntime :
    Has<Eff<AppRuntime>, ITodoRepository>,
    Has<Eff<AppRuntime>, IUserRepository>,
    Has<Eff<AppRuntime>, IProjectRepository>,
    Has<Eff<AppRuntime>, IUnitOfWork>,        // ✅ Add UnitOfWork
    Has<Eff<AppRuntime>, ILoggerIO>,
    Has<Eff<AppRuntime>, ITimeIO>
{
    public AppRuntime(AppDbContext dbContext, ILogger logger)
    {
        TodoRepository = new LiveTodoRepository(dbContext);
        UserRepository = new LiveUserRepository(dbContext);
        ProjectRepository = new LiveProjectRepository(dbContext);
        UnitOfWork = new LiveUnitOfWork(dbContext);     // ✅ Same DbContext instance
        Logger = new LiveLoggerIO(logger);
        Time = new LiveTimeIO();
    }

    public ITodoRepository TodoRepository { get; }
    public IUserRepository UserRepository { get; }
    public IProjectRepository ProjectRepository { get; }
    public IUnitOfWork UnitOfWork { get; }
    public ILoggerIO Logger { get; }
    public ITimeIO Time { get; }

    // Has implementations
    ITodoRepository Has<Eff<AppRuntime>, ITodoRepository>.Ask => TodoRepository;
    IUserRepository Has<Eff<AppRuntime>, IUserRepository>.Ask => UserRepository;
    IProjectRepository Has<Eff<AppRuntime>, IProjectRepository>.Ask => ProjectRepository;
    IUnitOfWork Has<Eff<AppRuntime>, IUnitOfWork>.Ask => UnitOfWork;
    ILoggerIO Has<Eff<AppRuntime>, ILoggerIO>.Ask => Logger;
    ITimeIO Has<Eff<AppRuntime>, ITimeIO>.Ask => Time;
}

// Tests/TestInfrastructure/TestRuntime.cs
public class TestRuntime :
    Has<Eff<TestRuntime>, ITodoRepository>,
    Has<Eff<TestRuntime>, IUserRepository>,
    Has<Eff<TestRuntime>, IProjectRepository>,
    Has<Eff<TestRuntime>, IUnitOfWork>,        // ✅ Add UnitOfWork
    Has<Eff<TestRuntime>, ILoggerIO>,
    Has<Eff<TestRuntime>, ITimeIO>
{
    public TestRuntime(DateTime? currentTime = null)
    {
        TodoRepository = new TestTodoRepository();
        UserRepository = new TestUserRepository();
        ProjectRepository = new TestProjectRepository();
        UnitOfWork = new TestUnitOfWork();
        Logger = new TestLoggerIO();
        Time = new TestTimeIO(currentTime);
    }

    public TestTodoRepository TodoRepository { get; }
    public TestUserRepository UserRepository { get; }
    public TestProjectRepository ProjectRepository { get; }
    public TestUnitOfWork UnitOfWork { get; }
    public TestLoggerIO Logger { get; }
    public TestTimeIO Time { get; }

    // Has implementations
    ITodoRepository Has<Eff<TestRuntime>, ITodoRepository>.Ask => TodoRepository;
    IUserRepository Has<Eff<TestRuntime>, IUserRepository>.Ask => UserRepository;
    IProjectRepository Has<Eff<TestRuntime>, IProjectRepository>.Ask => ProjectRepository;
    IUnitOfWork Has<Eff<TestRuntime>, IUnitOfWork>.Ask => UnitOfWork;
    ILoggerIO Has<Eff<TestRuntime>, ILoggerIO>.Ask => Logger;
    ITimeIO Has<Eff<TestRuntime>, ITimeIO>.Ask => Time;
}
```

### Step 8: Using in Application Services

#### Pattern 1: Simple Single-Entity Operation

```csharp
// Application/Todos/TodoService.cs
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,
               Has<M, IUnitOfWork>,          // ✅ Add UnitOfWork dependency
               Has<M, ILoggerIO>,
               Has<M, ITimeIO>
{
    /// <summary>
    /// Creates a new todo.
    /// Simple operation - just needs SaveChanges at the end.
    /// </summary>
    public static K<M, Todo> CreateTodo(string title, string? description) =>
        from _ in Logger<M, RT>.logInfo("Creating todo: {Title}", title)
        from now in Time<M, RT>.UtcNow

        // Create and validate
        from todo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = now
        })
        from validated in TodoValidation.Validate(todo).To<M, Todo>()

        // Add to repository (only modifies DbContext)
        from __ in TodoRepository<M, RT>.addTodo(validated)

        // Save changes (single operation, no transaction needed)
        from ___ in UnitOfWork<M, RT>.saveChanges

        from ____ in Logger<M, RT>.logInfo("Created todo {TodoId}", validated.Id)
        select validated;

    /// <summary>
    /// Updates a todo.
    /// Simple operation - just needs SaveChanges.
    /// </summary>
    public static K<M, Todo> UpdateTodo(int id, string title, bool isCompleted) =>
        from _ in Logger<M, RT>.logInfo("Updating todo {TodoId}", id)

        // Get existing
        from todoOpt in TodoRepository<M, RT>.getTodoById(id)
        from existing in todoOpt.To<M, Todo>(() => Error.New(404, "Todo not found"))

        // Update
        from updated in M.Pure(existing with { Title = title, IsCompleted = isCompleted })
        from validated in TodoValidation.Validate(updated).To<M, Todo>()
        from __ in TodoRepository<M, RT>.updateTodo(validated)

        // Save
        from ___ in UnitOfWork<M, RT>.saveChanges

        select validated;
}
```

#### Pattern 2: Multi-Entity Operation with Transaction

```csharp
// Application/Projects/ProjectService.cs
public static class ProjectService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, IProjectRepository>,
               Has<M, IUserRepository>,
               Has<M, ITaskRepository>,
               Has<M, IUnitOfWork>,          // ✅ Need UnitOfWork for transactions
               Has<M, ILoggerIO>,
               Has<M, ITimeIO>
{
    /// <summary>
    /// Creates a project with an initial task.
    /// Uses transaction to ensure both are created atomically.
    /// </summary>
    public static K<M, Project> CreateProjectWithInitialTask(
        int userId,
        string projectName,
        string initialTaskTitle) =>

        from _ in Logger<M, RT>.logInfo(
            "Creating project with initial task for user {UserId}", userId)

        // ✅ Wrap entire operation in transaction
        from result in UnitOfWork<M, RT>.inTransaction(
            from user in verifyUserExists(userId)
            from project in createProject(userId, projectName)
            from task in createInitialTask(project.Id, initialTaskTitle)
            from __ in Logger<M, RT>.logInfo(
                "Created project {ProjectId} with task {TaskId}",
                project.Id, task.Id)
            select project
        )

        select result;

    /// <summary>
    /// Deletes a project and all its tasks.
    /// Uses transaction to ensure atomic deletion.
    /// </summary>
    public static K<M, Unit> DeleteProjectWithTasks(Guid projectId) =>
        from _ in Logger<M, RT>.logInfo("Deleting project {ProjectId} with all tasks", projectId)

        from result in UnitOfWork<M, RT>.inTransaction(
            // Get project
            from projectOpt in ProjectRepository<M, RT>.getProjectById(projectId)
            from project in projectOpt.To<M, Project>(() =>
                Error.New(404, "Project not found"))

            // Get all tasks
            from tasks in TaskRepository<M, RT>.getTasksByProject(projectId)

            // Delete all tasks
            from __ in tasks.Traverse(task =>
                TaskRepository<M, RT>.deleteTask(task))

            // Delete project
            from ___ in ProjectRepository<M, RT>.deleteProject(project)

            from ____ in Logger<M, RT>.logInfo(
                "Deleted project and {Count} tasks", tasks.Count)

            select unit
        )

        select result;

    // Private helper methods
    private static K<M, User> verifyUserExists(int userId) =>
        from userOpt in UserRepository<M, RT>.getUserById(userId)
        from user in userOpt.To<M, User>(() =>
            Error.New(404, $"User {userId} not found"))
        select user;

    private static K<M, Project> createProject(int userId, string name) =>
        from now in Time<M, RT>.UtcNow
        from project in M.Pure(new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = userId,
            CreatedAt = now
        })
        from _ in ProjectRepository<M, RT>.createProject(project)
        select project;

    private static K<M, ProjectTask> createInitialTask(Guid projectId, string title) =>
        from now in Time<M, RT>.UtcNow
        from task in M.Pure(new ProjectTask
        {
            ProjectId = projectId,
            Title = title,
            IsCompleted = false,
            CreatedAt = now
        })
        from _ in TaskRepository<M, RT>.createTask(task)
        select task;
}
```

#### Pattern 3: Conditional Transaction

```csharp
// Application/Users/UserService.cs
public static class UserService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, IUserRepository>,
               Has<M, IProjectRepository>,
               Has<M, IUnitOfWork>,
               Has<M, ILoggerIO>,
               Has<M, ITimeIO>
{
    /// <summary>
    /// Deletes a user. If deleteProjects is true, also deletes all their projects.
    /// Uses transaction only when deleting multiple entities.
    /// </summary>
    public static K<M, Unit> DeleteUser(int userId, bool deleteProjects) =>
        deleteProjects
            ? deleteUserWithProjects(userId)
            : deleteUserOnly(userId);

    // Simple case - no transaction needed
    private static K<M, Unit> deleteUserOnly(int userId) =>
        from _ in Logger<M, RT>.logInfo("Deleting user {UserId}", userId)
        from userOpt in UserRepository<M, RT>.getUserById(userId)
        from user in userOpt.To<M, User>(() => Error.New(404, "User not found"))
        from __ in UserRepository<M, RT>.deleteUser(user)
        from ___ in UnitOfWork<M, RT>.saveChanges
        select unit;

    // Complex case - transaction required
    private static K<M, Unit> deleteUserWithProjects(int userId) =>
        from _ in Logger<M, RT>.logInfo(
            "Deleting user {UserId} with all projects", userId)

        from result in UnitOfWork<M, RT>.inTransaction(
            from userOpt in UserRepository<M, RT>.getUserById(userId)
            from user in userOpt.To<M, User>(() => Error.New(404, "User not found"))

            // Get all user's projects
            from projects in ProjectRepository<M, RT>.getProjectsByUser(userId)

            // Delete all projects
            from __ in projects.Traverse(project =>
                ProjectRepository<M, RT>.deleteProject(project))

            // Delete user
            from ___ in UserRepository<M, RT>.deleteUser(user)

            from ____ in Logger<M, RT>.logInfo(
                "Deleted user and {Count} projects", projects.Count)

            select unit
        )

        select result;
}
```

## Testing with Transactions

```csharp
// Tests/ProjectServiceTests.cs
public class ProjectServiceTests
{
    private TestRuntime _runtime;

    [SetUp]
    public void Setup()
    {
        _runtime = new TestRuntime();
    }

    [Test]
    public async Task CreateProjectWithInitialTask_Success_BothCreated()
    {
        // Arrange
        var user = new User { Id = 1, Email = "john@example.com", Name = "John" };
        _runtime.UserRepository.Seed(user);

        // Act
        var result = await ProjectService<Eff<TestRuntime>, TestRuntime>
            .CreateProjectWithInitialTask(user.Id, "My Project", "Initial Task")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var project = result.ThrowIfFail();

        // ✅ Verify transaction was used
        Assert.AreEqual(1, _runtime.UnitOfWork.CommitCallCount);
        Assert.AreEqual(0, _runtime.UnitOfWork.RollbackCallCount);

        // ✅ Verify both entities were created
        Assert.AreEqual(1, _runtime.ProjectRepository.Count);
        Assert.AreEqual(1, _runtime.TaskRepository.Count);
    }

    [Test]
    public async Task CreateProjectWithInitialTask_InvalidUser_NothingCreated()
    {
        // Act
        var result = await ProjectService<Eff<TestRuntime>, TestRuntime>
            .CreateProjectWithInitialTask(999, "My Project", "Initial Task")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.True(result.IsFail);

        // ✅ Verify transaction was rolled back
        Assert.AreEqual(0, _runtime.UnitOfWork.CommitCallCount);
        Assert.AreEqual(1, _runtime.UnitOfWork.RollbackCallCount);

        // ✅ Verify nothing was created
        Assert.AreEqual(0, _runtime.ProjectRepository.Count);
        Assert.AreEqual(0, _runtime.TaskRepository.Count);
    }

    [Test]
    public async Task DeleteProjectWithTasks_Success_AllDeleted()
    {
        // Arrange
        var user = new User { Id = 1, Email = "john@example.com", Name = "John" };
        var project = new Project { Id = Guid.NewGuid(), OwnerId = 1, Name = "Project" };
        var task1 = new ProjectTask { Id = 1, ProjectId = project.Id, Title = "Task 1" };
        var task2 = new ProjectTask { Id = 2, ProjectId = project.Id, Title = "Task 2" };

        _runtime.UserRepository.Seed(user);
        _runtime.ProjectRepository.Seed(project);
        _runtime.TaskRepository.Seed(task1, task2);

        // Act
        var result = await ProjectService<Eff<TestRuntime>, TestRuntime>
            .DeleteProjectWithTasks(project.Id)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        result.ThrowIfFail();

        // ✅ Verify transaction was committed
        Assert.AreEqual(1, _runtime.UnitOfWork.CommitCallCount);

        // ✅ Verify all were deleted atomically
        Assert.AreEqual(0, _runtime.ProjectRepository.Count);
        Assert.AreEqual(0, _runtime.TaskRepository.Count);
    }
}
```

## Decision Guide: When to Use Transactions?

### ✅ Use Explicit Transaction (inTransaction)

1. **Multiple entity modifications** - Creating/updating/deleting across repositories
2. **Dependent operations** - Second operation depends on first succeeding
3. **Complex business rules** - Need all-or-nothing guarantee
4. **Critical data consistency** - Partial failure would corrupt data

**Examples:**
- Create project + initial tasks
- Delete user + all their data (projects, tasks, etc.)
- Transfer ownership (update old owner, update new owner, create audit record)
- Bulk operations across entities

### ✅ Use Simple SaveChanges

1. **Single entity operation** - Creating/updating/deleting one record
2. **Read-only operations** - No saves needed at all
3. **Independent operations** - Failure of one doesn't affect others

**Examples:**
- Create single todo
- Update user profile
- Get all projects (read-only)
- Mark task as complete

## Benefits of This Pattern

### 1. Explicit Transaction Boundaries
```csharp
// ✅ Clear what's in the transaction
from result in UnitOfWork<M, RT>.inTransaction(
    from project in createProject(...)
    from task in createTask(...)
    select project
)
```

### 2. Automatic Rollback on Failure
```csharp
// If createTask fails, createProject is automatically rolled back
// No orphaned data!
```

### 3. Testable Transaction Logic
```csharp
// Test implementation tracks all transaction calls
Assert.AreEqual(1, _runtime.UnitOfWork.CommitCallCount);
Assert.AreEqual(0, _runtime.UnitOfWork.RollbackCallCount);
```

### 4. Repository Independence
```csharp
// Repositories don't know about transactions
// They just modify the context
public void AddTodo(Todo todo)
{
    context.Todos.Add(todo);
    // NO SaveChangesAsync here!
}
```

### 5. Performance Optimization
```csharp
// Single SaveChanges for multiple operations
from _ in UserRepository<M, RT>.createUser(user)
from __ in ProjectRepository<M, RT>.createProject(project1)
from ___ in ProjectRepository<M, RT>.createProject(project2)
from ____ in UnitOfWork<M, RT>.saveChanges  // One database round-trip
```

## Migration Path

### Step 1: Add IUnitOfWork trait and implementations
### Step 2: Update all repository interfaces (remove SaveChangesAsync from writes)
### Step 3: Update all repository implementations
### Step 4: Update all runtime classes to include IUnitOfWork
### Step 5: Update all services to use UnitOfWork.saveChanges
### Step 6: Add transactions to multi-entity operations
### Step 7: Update tests to verify transaction behavior

## Summary

The Unit of Work pattern as a trait provides:

✅ **Atomic transactions** across multiple repositories
✅ **Explicit transaction boundaries** in service code
✅ **Automatic rollback** on failures
✅ **Testable** transaction logic
✅ **Performance** optimization through batched saves
✅ **Consistent** with the Has pattern architecture
✅ **Type-safe** transaction management

This pattern ensures data consistency while maintaining the clean separation of concerns and testability that the trait-based architecture provides.
