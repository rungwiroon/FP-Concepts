# Multi-Entity Architecture Plan

## Goal
Scale the current trait-based architecture to support multiple entities while maintaining testability, separation of concerns, and the ability to defer infrastructure decisions.

## Current State

### Working Pattern (Single Entity)
```
TodoApp/
├── Domain/
│   └── Todo.cs, TodoValidation.cs
├── Features/Todos/
│   └── TodoService.cs
├── Infrastructure/
│   ├── Traits/
│   │   └── DatabaseIO.cs (single trait for Todo operations)
│   ├── Capabilities/
│   │   └── Database.cs
│   └── Live/
│       └── LiveDatabaseIO.cs (EF Core implementation)
└── Tests/
    └── TestInfrastructure/
        └── TestDatabaseIO.cs (Dictionary-based)
```

### Current Trait Pattern
```csharp
// Single DatabaseIO trait handles all Todo operations
public interface DatabaseIO
{
    Task<List<Todo>> GetAllTodosAsync(TodoSortOrder sortOrder, CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);
    Task<Todo> AddTodoAsync(Todo todo, CancellationToken ct);
    Task<Todo> UpdateTodoAsync(Todo todo, CancellationToken ct);
    Task<Unit> DeleteTodoAsync(Todo todo, CancellationToken ct);
}
```

## Target State

### Recommended Approach: Separate Trait Per Entity

**Why this approach?**
1. ✅ Domain-specific operations per entity (e.g., `GetUserByEmail`, `GetTodosByUser`)
2. ✅ Clear service dependencies (TodoService only depends on TodoRepositoryIO)
3. ✅ Independent testing (TestTodoRepository, TestUserRepository)
4. ✅ Scales well - adding entities doesn't affect existing code
5. ✅ Infrastructure can be deferred - start with test implementations

### Target Folder Structure

```
TodoApp/
├── Domain/
│   ├── Entities/
│   │   ├── Todo.cs
│   │   ├── User.cs
│   │   ├── Project.cs
│   │   └── Task.cs
│   └── Validation/
│       ├── TodoValidation.cs
│       ├── UserValidation.cs
│       └── ProjectValidation.cs
│
├── Application/
│   ├── Todos/
│   │   └── TodoService.cs
│   ├── Users/
│   │   └── UserService.cs
│   ├── Projects/
│   │   └── ProjectService.cs
│   └── Tasks/
│       └── TaskService.cs
│
├── Infrastructure/
│   ├── Traits/
│   │   ├── ITodoRepository.cs      # Trait interface
│   │   ├── IUserRepository.cs
│   │   ├── IProjectRepository.cs
│   │   ├── ITaskRepository.cs
│   │   ├── ILoggerIO.cs
│   │   └── ITimeIO.cs
│   │
│   ├── Capabilities/
│   │   ├── TodoRepository.cs       # Static class with Has<M,RT,T>.ask helpers
│   │   ├── UserRepository.cs
│   │   ├── ProjectRepository.cs
│   │   ├── TaskRepository.cs
│   │   ├── Logger.cs
│   │   └── Time.cs
│   │
│   ├── Live/                        # DEFER - Implement last!
│   │   ├── LiveTodoRepository.cs   # EF Core implementation
│   │   ├── LiveUserRepository.cs
│   │   ├── LiveProjectRepository.cs
│   │   └── AppDbContext.cs
│   │
│   └── AppRuntime.cs                # Combines all traits
│
└── Tests/
    └── TestInfrastructure/
        ├── TestTodoRepository.cs    # Dictionary-based
        ├── TestUserRepository.cs
        ├── TestProjectRepository.cs
        ├── TestTaskRepository.cs
        └── TestRuntime.cs
```

## Implementation Plan

### Phase 1: Domain Layer (Pure Business Logic)

**No infrastructure dependencies!**

```csharp
// Domain/Entities/User.cs
public record User
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

// Domain/Entities/Project.cs
public record Project
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int OwnerId { get; init; }  // User reference
    public DateTime CreatedAt { get; init; }
}

// Domain/Entities/Task.cs
public record ProjectTask
{
    public int Id { get; init; }
    public Guid ProjectId { get; init; }  // Project reference
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
}

// Domain/Validation/UserValidation.cs
public static class UserValidation
{
    public static Validation<Error, User> Validate(User user) =>
        // Pure validation logic (no IO)
}
```

### Phase 2: Infrastructure Traits (Contracts Only)

**Define interfaces, no implementations yet!**

```csharp
// Infrastructure/Traits/ITodoRepository.cs
public interface ITodoRepository
{
    Task<List<Todo>> GetAllTodosAsync(TodoSortOrder sortOrder, CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);
    Task<List<Todo>> GetTodosByUserAsync(int userId, CancellationToken ct);  // Custom query
    Task<Todo> AddTodoAsync(Todo todo, CancellationToken ct);
    Task<Todo> UpdateTodoAsync(Todo todo, CancellationToken ct);
    Task<Unit> DeleteTodoAsync(Todo todo, CancellationToken ct);
}

// Infrastructure/Traits/IUserRepository.cs
public interface IUserRepository
{
    Task<List<User>> GetAllUsersAsync(CancellationToken ct);
    Task<Option<User>> GetUserByIdAsync(int id, CancellationToken ct);
    Task<Option<User>> GetUserByEmailAsync(string email, CancellationToken ct);  // Custom query
    Task<User> CreateUserAsync(User user, CancellationToken ct);
    Task<User> UpdateUserAsync(User user, CancellationToken ct);
    Task<Unit> DeleteUserAsync(User user, CancellationToken ct);
}

// Infrastructure/Traits/IProjectRepository.cs
public interface IProjectRepository
{
    Task<List<Project>> GetAllProjectsAsync(CancellationToken ct);
    Task<Option<Project>> GetProjectByIdAsync(Guid id, CancellationToken ct);
    Task<List<Project>> GetProjectsByUserAsync(int userId, CancellationToken ct);  // Custom query
    Task<Project> CreateProjectAsync(Project project, CancellationToken ct);
    Task<Project> UpdateProjectAsync(Project project, CancellationToken ct);
    Task<Unit> DeleteProjectAsync(Project project, CancellationToken ct);
}

// Infrastructure/Traits/ITaskRepository.cs
public interface ITaskRepository
{
    Task<List<ProjectTask>> GetTasksByProjectAsync(Guid projectId, CancellationToken ct);
    Task<Option<ProjectTask>> GetTaskByIdAsync(int id, CancellationToken ct);
    Task<ProjectTask> CreateTaskAsync(ProjectTask task, CancellationToken ct);
    Task<ProjectTask> UpdateTaskAsync(ProjectTask task, CancellationToken ct);
    Task<Unit> DeleteTaskAsync(ProjectTask task, CancellationToken ct);
}
```

### Phase 3: Capability Modules (Has Pattern Helpers)

**Static helper classes using Has<M, RT, T>.ask**

```csharp
// Infrastructure/Capabilities/TodoRepository.cs
public static class TodoRepository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, ITodoRepository>
{
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

    public static K<M, List<Todo>> getTodosByUser(int userId) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todos in M.LiftIO(IO.liftAsync(env =>
            repo.GetTodosByUserAsync(userId, env.Token)))
        select todos;

    public static K<M, Todo>> addTodo(Todo todo) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from saved in M.LiftIO(IO.liftAsync(env =>
            repo.AddTodoAsync(todo, env.Token)))
        select saved;

    // ... more helpers
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

    public static K<M, Option<User>> getUserByEmail(string email) =>
        from repo in Has<M, RT, IUserRepository>.ask
        from user in M.LiftIO(IO.liftAsync(env =>
            repo.GetUserByEmailAsync(email, env.Token)))
        select user;

    public static K<M, User> createUser(User user) =>
        from repo in Has<M, RT, IUserRepository>.ask
        from saved in M.LiftIO(IO.liftAsync(env =>
            repo.CreateUserAsync(user, env.Token)))
        select saved;

    // ... more helpers
}

// Similar for ProjectRepository<M, RT> and TaskRepository<M, RT>
```

### Phase 4: Application Services (Business Logic)

**Services declare only the traits they need**

```csharp
// Application/Users/UserService.cs
public static class UserService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, IUserRepository>, Has<M, ILoggerIO>, Has<M, ITimeIO>
{
    public static K<M, User> CreateUser(string email, string name) =>
        from _ in Logger<M, RT>.logInfo("Creating user: {Email}", email)
        from now in Time<M, RT>.UtcNow
        from user in M.Pure(new User
        {
            Email = email,
            Name = name,
            CreatedAt = now
        })
        from validated in UserValidation.Validate(user).To<M, User>()
        from saved in UserRepository<M, RT>.createUser(validated)
        from __ in Logger<M, RT>.logInfo("Created user {UserId}", saved.Id)
        select saved;

    public static K<M, User> GetUserByEmail(string email) =>
        from _ in Logger<M, RT>.logInfo("Getting user by email: {Email}", email)
        from userOpt in UserRepository<M, RT>.getUserByEmail(email)
        from user in userOpt.To<M, User>(() => Error.New(404, $"User {email} not found"))
        select user;
}

// Application/Projects/ProjectService.cs
public static class ProjectService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, IProjectRepository>,
               Has<M, IUserRepository>,      // Needs user verification
               Has<M, ILoggerIO>,
               Has<M, ITimeIO>
{
    public static K<M, Project> CreateProject(int userId, string name) =>
        from _ in Logger<M, RT>.logInfo("Creating project for user {UserId}", userId)

        // Verify user exists
        from userOpt in UserRepository<M, RT>.getUserById(userId)
        from user in userOpt.To<M, User>(() => Error.New(404, "User not found"))

        // Create project
        from now in Time<M, RT>.UtcNow
        from project in M.Pure(new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = userId,
            CreatedAt = now
        })
        from saved in ProjectRepository<M, RT>.createProject(project)
        from __ in Logger<M, RT>.logInfo("Created project {ProjectId}", saved.Id)
        select saved;

    public static K<M, List<Project>> GetUserProjects(int userId) =>
        from _ in Logger<M, RT>.logInfo("Getting projects for user {UserId}", userId)
        from projects in ProjectRepository<M, RT>.getProjectsByUser(userId)
        from __ in Logger<M, RT>.logInfo("Found {Count} projects", projects.Count)
        select projects;
}

// Application/Todos/TodoService.cs (Enhanced)
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,
               Has<M, IUserRepository>,      // New dependency!
               Has<M, ILoggerIO>,
               Has<M, ITimeIO>
{
    public static K<M, Todo> CreateTodoForUser(int userId, string title, string? description) =>
        from _ in Logger<M, RT>.logInfo("Creating todo for user {UserId}", userId)

        // Verify user exists
        from userOpt in UserRepository<M, RT>.getUserById(userId)
        from user in userOpt.To<M, User>(() => Error.New(404, "User not found"))

        // Create todo (assuming Todo now has UserId property)
        from now in Time<M, RT>.UtcNow
        from todo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            UserId = userId,  // New field
            IsCompleted = false,
            CreatedAt = now
        })
        from validated in TodoValidation.Validate(todo).To<M, Todo>()
        from saved in TodoRepository<M, RT>.addTodo(validated)
        from __ in Logger<M, RT>.logInfo("Created todo {TodoId}", saved.Id)
        select saved;

    public static K<M, List<Todo>> GetUserTodos(int userId) =>
        from _ in Logger<M, RT>.logInfo("Getting todos for user {UserId}", userId)
        from todos in TodoRepository<M, RT>.getTodosByUser(userId)
        from __ in Logger<M, RT>.logInfo("Found {Count} todos", todos.Count)
        select todos;
}
```

### Phase 5: Test Infrastructure (BEFORE Production Infrastructure!)

**Dictionary-based implementations for fast unit testing**

```csharp
// Tests/TestInfrastructure/TestTodoRepository.cs
public class TestTodoRepository : ITodoRepository
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

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

    public Task<List<Todo>> GetTodosByUserAsync(int userId, CancellationToken ct)
    {
        var todos = _todos.Values.Where(t => t.UserId == userId).ToList();
        return Task.FromResult(todos);
    }

    public Task<Todo> AddTodoAsync(Todo todo, CancellationToken ct)
    {
        if (todo.Id == 0)
            todo = todo with { Id = _nextId++ };
        _todos[todo.Id] = todo;
        return Task.FromResult(todo);
    }

    // ... more methods

    // Test helpers
    public void Seed(params Todo[] todos) { ... }
    public void Clear() { ... }
    public int Count => _todos.Count;
}

// Tests/TestInfrastructure/TestUserRepository.cs
public class TestUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();
    private int _nextId = 1;

    public Task<Option<User>> GetUserByIdAsync(int id, CancellationToken ct)
    {
        var result = _users.TryGetValue(id, out var user) ? Some(user) : None;
        return Task.FromResult(result);
    }

    public Task<Option<User>> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        var user = _users.Values.FirstOrDefault(u => u.Email == email);
        var result = user != null ? Some(user) : None;
        return Task.FromResult(result);
    }

    public Task<User> CreateUserAsync(User user, CancellationToken ct)
    {
        if (user.Id == 0)
            user = user with { Id = _nextId++ };
        _users[user.Id] = user;
        return Task.FromResult(user);
    }

    // ... more methods

    // Test helpers
    public void Seed(params User[] users) { ... }
    public void Clear() { ... }
}

// Similar for TestProjectRepository and TestTaskRepository
```

### Phase 6: Test Runtime

**Combines all test implementations**

```csharp
// Tests/TestInfrastructure/TestRuntime.cs
public class TestRuntime :
    Has<Eff<TestRuntime>, ITodoRepository>,
    Has<Eff<TestRuntime>, IUserRepository>,
    Has<Eff<TestRuntime>, IProjectRepository>,
    Has<Eff<TestRuntime>, ITaskRepository>,
    Has<Eff<TestRuntime>, ILoggerIO>,
    Has<Eff<TestRuntime>, ITimeIO>
{
    public TestTodoRepository TodoRepository { get; }
    public TestUserRepository UserRepository { get; }
    public TestProjectRepository ProjectRepository { get; }
    public TestTaskRepository TaskRepository { get; }
    public TestLoggerIO Logger { get; }
    public TestTimeIO Time { get; }

    public TestRuntime(DateTime? currentTime = null)
    {
        TodoRepository = new TestTodoRepository();
        UserRepository = new TestUserRepository();
        ProjectRepository = new TestProjectRepository();
        TaskRepository = new TestTaskRepository();
        Logger = new TestLoggerIO();
        Time = new TestTimeIO(currentTime);
    }

    // Has implementations (property mapping)
    ITodoRepository Has<Eff<TestRuntime>, ITodoRepository>.Ask => TodoRepository;
    IUserRepository Has<Eff<TestRuntime>, IUserRepository>.Ask => UserRepository;
    IProjectRepository Has<Eff<TestRuntime>, IProjectRepository>.Ask => ProjectRepository;
    ITaskRepository Has<Eff<TestRuntime>, ITaskRepository>.Ask => TaskRepository;
    ILoggerIO Has<Eff<TestRuntime>, ILoggerIO>.Ask => Logger;
    ITimeIO Has<Eff<TestRuntime>, ITimeIO>.Ask => Time;
}
```

### Phase 7: Write Tests (Validate Business Logic)

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
    public async Task CreateProject_WithValidUser_Success()
    {
        // Arrange
        var user = new User { Email = "john@example.com", Name = "John", CreatedAt = DateTime.UtcNow };
        _runtime.UserRepository.Seed(user);

        // Act
        var result = await ProjectService<Eff<TestRuntime>, TestRuntime>
            .CreateProject(user.Id, "My Project")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var project = result.Match(
            Succ: p => p,
            Fail: err => throw new Exception($"Expected success: {err.Message}"));

        Assert.Equal("My Project", project.Name);
        Assert.Equal(user.Id, project.OwnerId);
    }

    [Test]
    public async Task CreateProject_WithInvalidUser_ReturnsError()
    {
        // Act
        var result = await ProjectService<Eff<TestRuntime>, TestRuntime>
            .CreateProject(999, "My Project")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.True(result.IsFail);
        result.Match(
            Succ: _ => Assert.Fail("Expected error"),
            Fail: err => Assert.Equal(404, err.Code));
    }
}

// Tests/TodoServiceTests.cs (Enhanced)
[Test]
public async Task CreateTodoForUser_WithValidUser_Success()
{
    // Arrange
    var user = new User { Email = "john@example.com", Name = "John", CreatedAt = DateTime.UtcNow };
    _runtime.UserRepository.Seed(user);

    // Act
    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .CreateTodoForUser(user.Id, "Buy milk", "From store")
        .RunAsync(_runtime, EnvIO.New());

    // Assert
    var todo = result.ThrowIfFail();
    Assert.Equal("Buy milk", todo.Title);
    Assert.Equal(user.Id, todo.UserId);
}

[Test]
public async Task GetUserTodos_ReturnsOnlyUserTodos()
{
    // Arrange
    var user1 = new User { Id = 1, Email = "user1@example.com", Name = "User 1" };
    var user2 = new User { Id = 2, Email = "user2@example.com", Name = "User 2" };
    _runtime.UserRepository.Seed(user1, user2);

    var todo1 = new Todo { Id = 1, Title = "User 1 Todo", UserId = 1, CreatedAt = DateTime.UtcNow };
    var todo2 = new Todo { Id = 2, Title = "User 2 Todo", UserId = 2, CreatedAt = DateTime.UtcNow };
    _runtime.TodoRepository.Seed(todo1, todo2);

    // Act
    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .GetUserTodos(1)
        .RunAsync(_runtime, EnvIO.New());

    // Assert
    var todos = result.ThrowIfFail();
    Assert.Equal(1, todos.Count);
    Assert.Equal("User 1 Todo", todos[0].Title);
}
```

### Phase 8: Production Infrastructure (LAST - When Ready!)

**EF Core implementations - can be deferred or changed later**

```csharp
// Infrastructure/Live/AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<Todo> Todos { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectTask> Tasks { get; set; }

    // EF Core configuration...
}

// Infrastructure/Live/LiveTodoRepository.cs
public class LiveTodoRepository(AppDbContext context) : ITodoRepository
{
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

    public async Task<List<Todo>> GetTodosByUserAsync(int userId, CancellationToken ct)
    {
        return await context.Todos
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    // ... more EF Core implementations
}

// Infrastructure/Live/LiveUserRepository.cs
public class LiveUserRepository(AppDbContext context) : IUserRepository
{
    public async Task<Option<User>> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        return Optional(user);
    }

    // ... more EF Core implementations
}

// Similar for LiveProjectRepository and LiveTaskRepository
```

### Phase 9: Production Runtime

```csharp
// Infrastructure/AppRuntime.cs
public class AppRuntime :
    Has<Eff<AppRuntime>, ITodoRepository>,
    Has<Eff<AppRuntime>, IUserRepository>,
    Has<Eff<AppRuntime>, IProjectRepository>,
    Has<Eff<AppRuntime>, ITaskRepository>,
    Has<Eff<AppRuntime>, ILoggerIO>,
    Has<Eff<AppRuntime>, ITimeIO>
{
    public AppRuntime(AppDbContext dbContext, ILogger logger)
    {
        TodoRepository = new LiveTodoRepository(dbContext);
        UserRepository = new LiveUserRepository(dbContext);
        ProjectRepository = new LiveProjectRepository(dbContext);
        TaskRepository = new LiveTaskRepository(dbContext);
        Logger = new LiveLoggerIO(logger);
        Time = new LiveTimeIO();
    }

    public ITodoRepository TodoRepository { get; }
    public IUserRepository UserRepository { get; }
    public IProjectRepository ProjectRepository { get; }
    public ITaskRepository TaskRepository { get; }
    public ILoggerIO Logger { get; }
    public ITimeIO Time { get; }

    // Has implementations
    ITodoRepository Has<Eff<AppRuntime>, ITodoRepository>.Ask => TodoRepository;
    IUserRepository Has<Eff<AppRuntime>, IUserRepository>.Ask => UserRepository;
    IProjectRepository Has<Eff<AppRuntime>, IProjectRepository>.Ask => ProjectRepository;
    ITaskRepository Has<Eff<AppRuntime>, ITaskRepository>.Ask => TaskRepository;
    ILoggerIO Has<Eff<AppRuntime>, ILoggerIO>.Ask => Logger;
    ITimeIO Has<Eff<AppRuntime>, ITimeIO>.Ask => Time;
}
```

## Key Benefits of This Approach

### 1. Infrastructure Last (Deferred Decisions)
```
Day 1-3: Domain + Traits + Application Services
Day 4-5: Test Infrastructure + Unit Tests ✅ Business logic validated!
Day 6+:  Production Infrastructure (when ready to choose database)
```

### 2. Clear Dependencies
Each service declares exactly what it needs:
```csharp
// TodoService only needs todos and users
where RT : Has<M, ITodoRepository>, Has<M, IUserRepository>, Has<M, ILoggerIO>

// ProjectService needs projects and users
where RT : Has<M, IProjectRepository>, Has<M, IUserRepository>, Has<M, ILoggerIO>
```

### 3. Easy Testing
```csharp
// Test with dictionary implementations - no database!
var runtime = new TestRuntime();
runtime.UserRepository.Seed(user);
var result = await UserService.CreateProject(...)
    .RunAsync(runtime, EnvIO.New());
```

### 4. Flexible Infrastructure
Can swap database technologies without changing business logic:
```csharp
// Version 1: EF Core + SQL Server
public class LiveTodoRepository(AppDbContext) : ITodoRepository

// Version 2: Dapper + PostgreSQL
public class LiveTodoRepository(IDbConnection) : ITodoRepository

// Version 3: MongoDB
public class LiveTodoRepository(IMongoDatabase) : ITodoRepository

// Business logic NEVER changes!
```

## Migration Strategy

### From Current (Single DatabaseIO) to Multi-Entity

1. **Rename current trait**
   - `DatabaseIO` → `ITodoRepository`
   - `Database<M, RT>` → `TodoRepository<M, RT>`

2. **Add new entity traits**
   - Create `IUserRepository`, `IProjectRepository`, etc.
   - Create capability modules for each

3. **Update Runtime**
   - Change from single `DatabaseIO` to multiple traits
   - Update property names

4. **Update Tests**
   - Rename `TestDatabaseIO` → `TestTodoRepository`
   - Add new test repositories

5. **Update Services**
   - Update `where` clauses to use new trait names
   - Add cross-entity operations (e.g., GetUserTodos)

6. **Update Live implementations**
   - Rename `LiveDatabaseIO` → `LiveTodoRepository`
   - Add new live repositories

## Next Steps

1. ✅ Review this plan
2. Create first additional entity (User)
3. Create IUserRepository trait
4. Create TestUserRepository
5. Write tests for UserService
6. Implement UserService
7. ✅ Validate business logic works BEFORE touching database
8. Defer LiveUserRepository until infrastructure decisions are made

## References

- Current working code: `TodoApp/Features/Todos/TodoService.cs`
- Current test implementation: `TodoApp.Tests/TestInfrastructure/TestDatabaseIO.cs`
- Current trait pattern: `TodoApp/Infrastructure/Traits/DatabaseIO.cs`
