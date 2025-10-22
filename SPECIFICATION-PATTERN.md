# Specification Pattern for Repository Queries

## The Problem

Repository methods can become bloated with many query variations:

```csharp
// ❌ Repository explosion - too many specific query methods
public interface ITodoRepository
{
    Task<List<Todo>> GetAllTodosAsync(CancellationToken ct);
    Task<List<Todo>> GetCompletedTodosAsync(CancellationToken ct);
    Task<List<Todo>> GetIncompleteTodosAsync(CancellationToken ct);
    Task<List<Todo>> GetTodosByUserAsync(int userId, CancellationToken ct);
    Task<List<Todo>> GetCompletedTodosByUserAsync(int userId, CancellationToken ct);
    Task<List<Todo>> GetIncompleteTodosByUserAsync(int userId, CancellationToken ct);
    Task<List<Todo>> GetTodosCreatedAfterAsync(DateTime date, CancellationToken ct);
    Task<List<Todo>> GetCompletedTodosCreatedAfterAsync(DateTime date, CancellationToken ct);
    // ... combinatorial explosion! 😱
}
```

**Problems:**
- N × M combinations of filters = repository explosion
- Query logic scattered across many methods
- Hard to compose queries dynamically
- Duplicate code between similar queries
- Testing requires mocking all methods

## The Solution: Specification Pattern

Encapsulate query logic in **composable, reusable specifications**:

```csharp
// ✅ Clean repository - one generic query method
public interface ITodoRepository
{
    Task<List<Todo>> GetAllTodosAsync(CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);
    Task<List<Todo>> FindAsync(Specification<Todo> spec, CancellationToken ct);  // ✅ Generic!

    void AddTodo(Todo todo);
    void UpdateTodo(Todo todo);
    void DeleteTodo(Todo todo);
}

// ✅ Composable specifications
var completedByUser = new CompletedTodoSpec()
    .And(new TodosByUserSpec(userId));

var recentAndIncomplete = new CreatedAfterSpec(DateTime.Now.AddDays(-7))
    .And(new IncompleteTodoSpec());
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Application Service                                        │
│  (Composes specifications for business queries)            │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  Specification<T>                                           │
│  - ToExpression() : Expression<Func<T, bool>>              │
│  - And(), Or(), Not() : Specification<T>                   │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ CompletedSpec│    │ ByUserSpec   │    │ CreatedAfter │
│              │    │              │    │ Spec         │
└──────────────┘    └──────────────┘    └──────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  Repository (Find method)                                   │
│  - Test: .Where(spec.ToExpression().Compile())             │
│  - Live: context.Todos.Where(spec.ToExpression())          │
└─────────────────────────────────────────────────────────────┘
```

## Implementation

### Step 1: Base Specification Class

```csharp
// Domain/Specifications/Specification.cs
using System.Linq.Expressions;

namespace TodoApp.Domain.Specifications;

/// <summary>
/// Base specification pattern for encapsulating query logic.
/// Specifications can be composed using And, Or, Not operations.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public abstract class Specification<T>
{
    /// <summary>
    /// Converts the specification to a LINQ expression.
    /// This works with both LINQ to Objects (tests) and EF Core (production).
    /// </summary>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// Checks if an entity satisfies the specification.
    /// Useful for in-memory validation.
    /// </summary>
    public bool IsSatisfiedBy(T entity)
    {
        var predicate = ToExpression().Compile();
        return predicate(entity);
    }

    /// <summary>
    /// Combines this specification with another using AND logic.
    /// </summary>
    public Specification<T> And(Specification<T> other)
    {
        return new AndSpecification<T>(this, other);
    }

    /// <summary>
    /// Combines this specification with another using OR logic.
    /// </summary>
    public Specification<T> Or(Specification<T> other)
    {
        return new OrSpecification<T>(this, other);
    }

    /// <summary>
    /// Negates this specification.
    /// </summary>
    public Specification<T> Not()
    {
        return new NotSpecification<T>(this);
    }

    /// <summary>
    /// Implicit conversion to Expression for convenient usage.
    /// </summary>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> spec)
    {
        return spec.ToExpression();
    }
}

/// <summary>
/// Combines two specifications with AND logic.
/// </summary>
internal class AndSpecification<T>(Specification<T> left, Specification<T> right) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = left.ToExpression();
        var rightExpr = right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.AndAlso(
            Expression.Invoke(leftExpr, parameter),
            Expression.Invoke(rightExpr, parameter)
        );

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// Combines two specifications with OR logic.
/// </summary>
internal class OrSpecification<T>(Specification<T> left, Specification<T> right) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = left.ToExpression();
        var rightExpr = right.ToExpression();

        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.OrElse(
            Expression.Invoke(leftExpr, parameter),
            Expression.Invoke(rightExpr, parameter)
        );

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// Negates a specification.
/// </summary>
internal class NotSpecification<T>(Specification<T> spec) : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        var expr = spec.ToExpression();
        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.Not(Expression.Invoke(expr, parameter));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
```

### Step 2: Domain-Specific Specifications

```csharp
// Domain/Specifications/TodoSpecifications.cs
using System.Linq.Expressions;

namespace TodoApp.Domain.Specifications;

/// <summary>
/// Specification for completed todos.
/// </summary>
public class CompletedTodoSpec : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => todo.IsCompleted;
    }
}

/// <summary>
/// Specification for incomplete todos.
/// </summary>
public class IncompleteTodoSpec : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => !todo.IsCompleted;
    }
}

/// <summary>
/// Specification for todos belonging to a specific user.
/// </summary>
public class TodosByUserSpec(int userId) : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => todo.UserId == userId;
    }
}

/// <summary>
/// Specification for todos created after a specific date.
/// </summary>
public class TodosCreatedAfterSpec(DateTime afterDate) : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => todo.CreatedAt > afterDate;
    }
}

/// <summary>
/// Specification for todos created within a date range.
/// </summary>
public class TodosInDateRangeSpec(DateTime startDate, DateTime endDate) : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => todo.CreatedAt >= startDate && todo.CreatedAt <= endDate;
    }
}

/// <summary>
/// Specification for todos with titles containing a search term.
/// </summary>
public class TodosTitleContainsSpec(string searchTerm) : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        var lowerSearchTerm = searchTerm.ToLower();
        return todo => todo.Title.ToLower().Contains(lowerSearchTerm);
    }
}

/// <summary>
/// Specification for high-priority todos (example with complex logic).
/// </summary>
public class HighPriorityTodoSpec : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        // Example: High priority = incomplete AND created more than 7 days ago
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        return todo => !todo.IsCompleted && todo.CreatedAt < sevenDaysAgo;
    }
}
```

### Step 3: Update Repository Interface

```csharp
// Infrastructure/Traits/ITodoRepository.cs
using TodoApp.Domain.Specifications;

public interface ITodoRepository
{
    // Standard CRUD operations
    Task<List<Todo>> GetAllTodosAsync(CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);

    // ✅ Generic specification-based query
    Task<List<Todo>> FindAsync(Specification<Todo> spec, CancellationToken ct);

    // ✅ Optional: Count matching specification
    Task<int> CountAsync(Specification<Todo> spec, CancellationToken ct);

    // Write operations
    void AddTodo(Todo todo);
    void UpdateTodo(Todo todo);
    void DeleteTodo(Todo todo);
}
```

### Step 4: Test Repository Implementation

```csharp
// Tests/TestInfrastructure/TestTodoRepository.cs
using TodoApp.Domain.Specifications;

public class TestTodoRepository : ITodoRepository
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

    public Task<List<Todo>> GetAllTodosAsync(CancellationToken ct)
    {
        return Task.FromResult(_todos.Values.ToList());
    }

    public Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct)
    {
        var result = _todos.TryGetValue(id, out var todo) ? Some(todo) : None;
        return Task.FromResult(result);
    }

    // ✅ Specification-based query (LINQ to Objects)
    public Task<List<Todo>> FindAsync(Specification<Todo> spec, CancellationToken ct)
    {
        // Convert expression to compiled delegate for in-memory filtering
        var predicate = spec.ToExpression().Compile();
        var results = _todos.Values.Where(predicate).ToList();
        return Task.FromResult(results);
    }

    // ✅ Count matching specification
    public Task<int> CountAsync(Specification<Todo> spec, CancellationToken ct)
    {
        var predicate = spec.ToExpression().Compile();
        var count = _todos.Values.Count(predicate);
        return Task.FromResult(count);
    }

    // Write operations
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
```

### Step 5: Live Repository Implementation

```csharp
// Infrastructure/Live/LiveTodoRepository.cs
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain.Specifications;

public class LiveTodoRepository(AppDbContext context) : ITodoRepository
{
    public async Task<List<Todo>> GetAllTodosAsync(CancellationToken ct)
    {
        return await context.Todos
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct)
    {
        var todo = await context.Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        return Optional(todo);
    }

    // ✅ Specification-based query (EF Core - translates to SQL!)
    public async Task<List<Todo>> FindAsync(Specification<Todo> spec, CancellationToken ct)
    {
        // EF Core translates the expression tree to SQL
        return await context.Todos
            .AsNoTracking()
            .Where(spec.ToExpression())  // Expression<Func<Todo, bool>>
            .ToListAsync(ct);
    }

    // ✅ Count matching specification
    public async Task<int> CountAsync(Specification<Todo> spec, CancellationToken ct)
    {
        return await context.Todos
            .Where(spec.ToExpression())
            .CountAsync(ct);
    }

    // Write operations
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
```

### Step 6: Repository Capability Module

```csharp
// Infrastructure/Capabilities/TodoRepository.cs
using TodoApp.Domain.Specifications;

public static class TodoRepository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, ITodoRepository>
{
    // Standard queries
    public static K<M, List<Todo>> getAllTodos =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todos in M.LiftIO(IO.liftAsync(env =>
            repo.GetAllTodosAsync(env.Token)))
        select todos;

    public static K<M, Option<Todo>> getTodoById(int id) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todo in M.LiftIO(IO.liftAsync(env =>
            repo.GetTodoByIdAsync(id, env.Token)))
        select todo;

    // ✅ Specification-based query
    public static K<M, List<Todo>> find(Specification<Todo> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todos in M.LiftIO(IO.liftAsync(env =>
            repo.FindAsync(spec, env.Token)))
        select todos;

    // ✅ Count matching specification
    public static K<M, int> count(Specification<Todo> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from count in M.LiftIO(IO.liftAsync(env =>
            repo.CountAsync(spec, env.Token)))
        select count;

    // Write operations
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
```

### Step 7: Usage in Application Services

```csharp
// Application/Todos/TodoService.cs
using TodoApp.Domain.Specifications;

public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,
               Has<M, IUnitOfWork>,
               Has<M, ILoggerIO>
{
    /// <summary>
    /// Gets completed todos for a specific user.
    /// Demonstrates simple specification composition.
    /// </summary>
    public static K<M, List<Todo>> GetCompletedTodosByUser(int userId) =>
        from _ in Logger<M, RT>.logInfo("Getting completed todos for user {UserId}", userId)

        // ✅ Compose specifications
        from todos in TodoRepository<M, RT>.find(
            new CompletedTodoSpec()
                .And(new TodosByUserSpec(userId))
        )

        from __ in Logger<M, RT>.logInfo("Found {Count} completed todos", todos.Count)
        select todos;

    /// <summary>
    /// Gets recent incomplete todos (last 7 days).
    /// </summary>
    public static K<M, List<Todo>> GetRecentIncompleteTodos() =>
        from sevenDaysAgo in M.Pure(DateTime.UtcNow.AddDays(-7))

        from todos in TodoRepository<M, RT>.find(
            new IncompleteTodoSpec()
                .And(new TodosCreatedAfterSpec(sevenDaysAgo))
        )

        select todos;

    /// <summary>
    /// Searches todos by title for a specific user.
    /// </summary>
    public static K<M, List<Todo>> SearchUserTodos(int userId, string searchTerm) =>
        from todos in TodoRepository<M, RT>.find(
            new TodosByUserSpec(userId)
                .And(new TodosTitleContainsSpec(searchTerm))
        )
        select todos;

    /// <summary>
    /// Gets high-priority todos (incomplete, older than 7 days).
    /// </summary>
    public static K<M, List<Todo>> GetHighPriorityTodos(int userId) =>
        from todos in TodoRepository<M, RT>.find(
            new HighPriorityTodoSpec()
                .And(new TodosByUserSpec(userId))
        )
        select todos;

    /// <summary>
    /// Gets count of incomplete todos for dashboard.
    /// </summary>
    public static K<M, int> GetIncompleteTodoCount(int userId) =>
        from count in TodoRepository<M, RT>.count(
            new IncompleteTodoSpec()
                .And(new TodosByUserSpec(userId))
        )
        select count;

    /// <summary>
    /// Complex query: Gets todos from last month that are either:
    /// - Completed, OR
    /// - Incomplete with "urgent" in the title
    /// </summary>
    public static K<M, List<Todo>> GetLastMonthTodosWithUrgent(int userId) =>
        from lastMonth in M.Pure(DateTime.UtcNow.AddMonths(-1))

        // ✅ Complex composition with And/Or/Not
        from spec in M.Pure(
            new TodosByUserSpec(userId)
                .And(new TodosCreatedAfterSpec(lastMonth))
                .And(
                    new CompletedTodoSpec()
                        .Or(new TodosTitleContainsSpec("urgent"))
                )
        )

        from todos in TodoRepository<M, RT>.find(spec)
        select todos;
}
```

## Testing with Specifications

```csharp
// Tests/TodoServiceTests.cs
using TodoApp.Domain.Specifications;

public class TodoServiceTests
{
    private TestRuntime _runtime;

    [SetUp]
    public void Setup()
    {
        _runtime = new TestRuntime();
    }

    [Test]
    public async Task GetCompletedTodosByUser_ReturnsOnlyCompletedForUser()
    {
        // Arrange
        var user1 = new User { Id = 1, Email = "user1@test.com", Name = "User 1" };
        var user2 = new User { Id = 2, Email = "user2@test.com", Name = "User 2" };

        var todo1 = new Todo { Id = 1, Title = "User 1 Complete", UserId = 1, IsCompleted = true };
        var todo2 = new Todo { Id = 2, Title = "User 1 Incomplete", UserId = 1, IsCompleted = false };
        var todo3 = new Todo { Id = 3, Title = "User 2 Complete", UserId = 2, IsCompleted = true };

        _runtime.UserRepository.Seed(user1, user2);
        _runtime.TodoRepository.Seed(todo1, todo2, todo3);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetCompletedTodosByUser(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var todos = result.ThrowIfFail();
        Assert.AreEqual(1, todos.Count);
        Assert.AreEqual("User 1 Complete", todos[0].Title);
    }

    [Test]
    public async Task SearchUserTodos_FiltersCorrectly()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        var todo1 = new Todo { Id = 1, Title = "Buy milk", UserId = 1 };
        var todo2 = new Todo { Id = 2, Title = "Buy bread", UserId = 1 };
        var todo3 = new Todo { Id = 3, Title = "Clean house", UserId = 1 };

        _runtime.UserRepository.Seed(user);
        _runtime.TodoRepository.Seed(todo1, todo2, todo3);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .SearchUserTodos(1, "buy")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var todos = result.ThrowIfFail();
        Assert.AreEqual(2, todos.Count);
        Assert.True(todos.All(t => t.Title.Contains("Buy", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void Specification_IsSatisfiedBy_WorksCorrectly()
    {
        // Arrange
        var completedTodo = new Todo { Id = 1, Title = "Test", IsCompleted = true };
        var incompleteTodo = new Todo { Id = 2, Title = "Test", IsCompleted = false };

        var spec = new CompletedTodoSpec();

        // Act & Assert
        Assert.True(spec.IsSatisfiedBy(completedTodo));
        Assert.False(spec.IsSatisfiedBy(incompleteTodo));
    }

    [Test]
    public void Specification_Composition_WorksCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var todo = new Todo
        {
            Id = 1,
            Title = "Test",
            UserId = 5,
            IsCompleted = false,
            CreatedAt = now.AddDays(-10)
        };

        // ✅ Compose complex specification
        var spec = new TodosByUserSpec(5)
            .And(new IncompleteTodoSpec())
            .And(new TodosCreatedAfterSpec(now.AddDays(-14)));

        // Act & Assert
        Assert.True(spec.IsSatisfiedBy(todo));
    }
}
```

## Extending Source Generator for Specifications

Update the source generator to automatically include `Find` and `Count` methods:

```csharp
// In RepositoryGenerator.cs - GenerateTraitInterface method
private static void GenerateTraitInterface(SourceProductionContext context, EntityInfo entity)
{
    var source = $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using TodoApp.Domain.Specifications;
using {{entity.Namespace}};

namespace TodoApp.Infrastructure.Traits;

public interface I{{entity.EntityName}}Repository
{
    // Read operations
    Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct);
    Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct);

    // ✅ Specification-based queries (auto-generated)
    Task<List<{{entity.EntityName}}>> FindAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct);
    Task<int> CountAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct);

{{GenerateCustomQuerySignatures(entity)}}

    // Write operations
    void Add{{entity.EntityName}}({{entity.EntityName}} entity);
    void Update{{entity.EntityName}}({{entity.EntityName}} entity);
    void Delete{{entity.EntityName}}({{entity.EntityName}} entity);
}
""";

    context.AddSource($"I{entity.EntityName}Repository.g.cs", source);
}

// Similar updates for GenerateCapabilityModule, GenerateTestRepository, GenerateLiveRepository
```

## Benefits of Specification Pattern

### 1. Eliminates Repository Explosion

**Before:**
```csharp
// 10 combinations = 10 methods
GetCompletedTodos()
GetIncompleteTodos()
GetCompletedTodosByUser(userId)
GetIncompleteTodosByUser(userId)
GetRecentCompletedTodos(date)
// ... 5 more methods
```

**After:**
```csharp
// 1 method + composable specs
Find(new CompletedTodoSpec())
Find(new IncompleteTodoSpec())
Find(new CompletedTodoSpec().And(new ByUserSpec(userId)))
Find(new IncompleteTodoSpec().And(new ByUserSpec(userId)))
Find(new CompletedTodoSpec().And(new CreatedAfterSpec(date)))
// ... infinite combinations!
```

### 2. Reusable Query Logic

```csharp
// ✅ Define once, use everywhere
var highPrioritySpec = new HighPriorityTodoSpec();

// Use in different contexts
var userHighPriority = highPrioritySpec.And(new ByUserSpec(userId));
var projectHighPriority = highPrioritySpec.And(new ByProjectSpec(projectId));
var count = await repo.CountAsync(highPrioritySpec);
```

### 3. Composable Business Logic

```csharp
// ✅ Build complex queries from simple pieces
var urgentUserTodos = new TodosByUserSpec(userId)
    .And(
        new HighPriorityTodoSpec()
            .Or(new TodosTitleContainsSpec("urgent"))
    )
    .And(new IncompleteTodoSpec().Not());  // Completed or no title match
```

### 4. Testable in Isolation

```csharp
[Test]
public void HighPrioritySpec_IdentifiesOldIncompleteTodos()
{
    var spec = new HighPriorityTodoSpec();

    var oldIncomplete = new Todo { IsCompleted = false, CreatedAt = DateTime.UtcNow.AddDays(-10) };
    var recent = new Todo { IsCompleted = false, CreatedAt = DateTime.UtcNow.AddDays(-1) };
    var completed = new Todo { IsCompleted = true, CreatedAt = DateTime.UtcNow.AddDays(-10) };

    Assert.True(spec.IsSatisfiedBy(oldIncomplete));
    Assert.False(spec.IsSatisfiedBy(recent));
    Assert.False(spec.IsSatisfiedBy(completed));
}
```

### 5. Works with Both Test and Production

```csharp
// ✅ Same specification works in both!

// Test (LINQ to Objects)
var predicate = spec.ToExpression().Compile();
var results = _dictionary.Values.Where(predicate).ToList();

// Production (EF Core - translates to SQL)
var results = await context.Todos
    .Where(spec.ToExpression())  // Becomes SQL WHERE clause!
    .ToListAsync();
```

### 6. Dynamic Query Building

```csharp
public static K<M, List<Todo>> SearchTodos(
    int userId,
    bool? isCompleted,
    DateTime? createdAfter,
    string? searchTerm) =>
    from spec in buildSearchSpec(userId, isCompleted, createdAfter, searchTerm)
    from todos in TodoRepository<M, RT>.find(spec)
    select todos;

private static Specification<Todo> buildSearchSpec(
    int userId,
    bool? isCompleted,
    DateTime? createdAfter,
    string? searchTerm)
{
    Specification<Todo> spec = new TodosByUserSpec(userId);

    if (isCompleted.HasValue)
        spec = isCompleted.Value
            ? spec.And(new CompletedTodoSpec())
            : spec.And(new IncompleteTodoSpec());

    if (createdAfter.HasValue)
        spec = spec.And(new TodosCreatedAfterSpec(createdAfter.Value));

    if (!string.IsNullOrEmpty(searchTerm))
        spec = spec.And(new TodosTitleContainsSpec(searchTerm));

    return spec;
}
```

## Advanced: Specifications for Other Entities

```csharp
// Domain/Specifications/UserSpecifications.cs
public class ActiveUserSpec : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.IsActive && !user.IsDeleted;
    }
}

public class UserEmailContainsSpec(string domain) : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
    {
        return user => user.Email.Contains(domain);
    }
}

// Domain/Specifications/ProjectSpecifications.cs
public class ActiveProjectSpec : Specification<Project>
{
    public override Expression<Func<Project, bool>> ToExpression()
    {
        return project => !project.IsArchived;
    }
}

public class ProjectsByOwnerSpec(int ownerId) : Specification<Project>
{
    public override Expression<Func<Project, bool>> ToExpression()
    {
        return project => project.OwnerId == ownerId;
    }
}

// Use across entities
var activeUserProjects = new ProjectsByOwnerSpec(userId)
    .And(new ActiveProjectSpec());
```

## Summary

The Specification pattern provides:

✅ **Eliminates repository explosion** - One `Find` method instead of dozens of specific queries
✅ **Reusable query logic** - Define specifications once, compose infinitely
✅ **Composable** - And, Or, Not operations for complex queries
✅ **Testable** - Specifications can be unit tested in isolation
✅ **Type-safe** - Compile-time checking via Expression<Func<T, bool>>
✅ **Works everywhere** - Same code for test (LINQ to Objects) and production (EF Core → SQL)
✅ **Dynamic queries** - Build specifications conditionally at runtime
✅ **Maintainable** - Query logic lives in domain, not scattered in repository

**Combined with Source Generators:**
- Repositories auto-generate `Find` and `Count` methods
- Focus on writing specifications (business logic)
- Infrastructure is automatically generated

This is the perfect pattern for complex domain queries in a functional architecture!
