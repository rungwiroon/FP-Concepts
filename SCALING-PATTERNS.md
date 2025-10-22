# Scaling Patterns for Multi-Entity Systems

## Introduction

As applications grow from simple single-entity CRUD operations to complex multi-entity systems, developers face a common challenge: how to scale the codebase while maintaining testability, separation of concerns, and the flexibility to defer infrastructure decisions.

### The Challenge

Starting with a simple Todo application is straightforward. But what happens when you need to add Users, Projects, Tasks, and other entities? How do you:
- Avoid repository explosion (dozens of specific query methods)?
- Handle complex queries efficiently?
- Ensure data consistency across multiple entity operations?
- Maintain testability without requiring a full database?

Traditional approaches often lead to:
- **Repository explosion** - Dozens of specific query methods for every entity
- **Tight coupling** - Business logic tightly bound to database implementation  
- **Poor testability** - Requiring full database setup for unit tests
- **Premature decisions** - Forced to choose database technology before understanding requirements
- **Data consistency issues** - No clear transaction boundaries across repositories

### The Solution: Three Core Patterns

This document presents three patterns that work together to solve these problems:

1. **Specification Pattern** - Composable, reusable query logic
2. **Pagination Pattern** - Efficient handling of large datasets  
3. **Transaction Pattern** - Coordinated multi-entity operations

All built on a foundation of:
- **Trait-based dependencies** - Explicit dependencies through interfaces
- **Infrastructure deferral** - Test implementations first, production later
- **Functional composition** - Pure business logic with effect management

## Table of Contents

1. [Specification Pattern](#specification-pattern) - Eliminates repository explosion
2. [Pagination Pattern](#pagination-pattern) - Handles large datasets efficiently
3. [Transaction Pattern](#transaction-pattern) - Ensures data consistency
4. [Implementation Guide](#implementation-guide) - How to scale from single to multi-entity
5. [Complete Example](#complete-example) - All patterns working together

---

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

---

# Pagination Pattern for Repository Queries

## The Problem

Without pagination, queries return all matching records:

```csharp
// ❌ Returns ALL matching todos (could be thousands!)
var todos = await repo.FindAsync(new TodosByUserSpec(userId), ct);

// Problems:
// - Memory issues with large datasets
// - Slow network transfer
// - Poor UX (overwhelming UI)
// - Database load (scanning large result sets)
```

## The Solution: Paginated Queries with Specifications

Combine specifications with pagination for efficient, composable queries:

```csharp
// ✅ Returns page 1 (20 items) of matching todos
var result = await repo.FindPagedAsync(
    spec: new TodosByUserSpec(userId),
    pageNumber: 1,
    pageSize: 20,
    ct
);

// Result contains:
// - Items (List<Todo>)
// - TotalCount
// - PageNumber, PageSize
// - TotalPages, HasNextPage, HasPreviousPage
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Application Service                                        │
│  (Request page 2, 20 items, sorted by date)                │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Specification│    │ PageRequest  │    │ SortOrder    │
│ (Filtering)  │    │ (Pagination) │    │ (Sorting)    │
└──────────────┘    └──────────────┘    └──────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  Repository.FindPagedAsync                                  │
│  1. Apply specification (WHERE)                             │
│  2. Count total matches                                     │
│  3. Apply sorting (ORDER BY)                                │
│  4. Apply pagination (SKIP/TAKE)                            │
│  5. Return PagedResult<T>                                   │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
   Dictionary           EF Core              Result
  (Test Impl)         (Live Impl)        PagedResult<T>
```

## Implementation

### Step 1: PagedResult Model

```csharp
// Domain/Models/PagedResult.cs
namespace TodoApp.Domain.Models;

/// <summary>
/// Represents a paginated result set with metadata.
/// </summary>
/// <typeparam name="T">The type of items in the page</typeparam>
public record PagedResult<T>
{
    /// <summary>
    /// The items in the current page.
    /// </summary>
    public required List<T> Items { get; init; }

    /// <summary>
    /// Total number of items matching the query (across all pages).
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public required int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Index of first item on this page (1-based).
    /// </summary>
    public int FirstItemIndex => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;

    /// <summary>
    /// Index of last item on this page (1-based).
    /// </summary>
    public int LastItemIndex => Math.Min(PageNumber * PageSize, TotalCount);

    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    public static PagedResult<T> Empty(int pageNumber, int pageSize) => new()
    {
        Items = [],
        TotalCount = 0,
        PageNumber = pageNumber,
        PageSize = pageSize
    };

    /// <summary>
    /// Maps items to a different type while preserving pagination metadata.
    /// </summary>
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> mapper) => new()
    {
        Items = Items.Select(mapper).ToList(),
        TotalCount = TotalCount,
        PageNumber = PageNumber,
        PageSize = PageSize
    };
}
```

### Step 2: Sort Order

```csharp
// Domain/Models/SortOrder.cs
namespace TodoApp.Domain.Models;

/// <summary>
/// Represents sorting configuration for queries.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public record SortOrder<T>
{
    /// <summary>
    /// The property to sort by.
    /// </summary>
    public required Expression<Func<T, object>> OrderBy { get; init; }

    /// <summary>
    /// Sort direction (true = ascending, false = descending).
    /// </summary>
    public required bool Ascending { get; init; }

    /// <summary>
    /// Creates an ascending sort order.
    /// </summary>
    public static SortOrder<T> Asc(Expression<Func<T, object>> orderBy) => new()
    {
        OrderBy = orderBy,
        Ascending = true
    };

    /// <summary>
    /// Creates a descending sort order.
    /// </summary>
    public static SortOrder<T> Desc(Expression<Func<T, object>> orderBy) => new()
    {
        OrderBy = orderBy,
        Ascending = false
    };
}

// Domain/Models/TodoSortField.cs (Domain-specific)
namespace TodoApp.Domain.Models;

/// <summary>
/// Predefined sort orders for Todo entities.
/// </summary>
public static class TodoSortField
{
    public static SortOrder<Todo> CreatedAtAsc => SortOrder<Todo>.Asc(t => t.CreatedAt);
    public static SortOrder<Todo> CreatedAtDesc => SortOrder<Todo>.Desc(t => t.CreatedAt);
    public static SortOrder<Todo> TitleAsc => SortOrder<Todo>.Asc(t => t.Title);
    public static SortOrder<Todo> TitleDesc => SortOrder<Todo>.Desc(t => t.Title);
    public static SortOrder<Todo> CompletedFirstThenByDate => SortOrder<Todo>.Desc(t => t.IsCompleted);
}
```

### Step 3: Update Repository Interface

```csharp
// Infrastructure/Traits/ITodoRepository.cs
using TodoApp.Domain.Models;
using TodoApp.Domain.Specifications;

public interface ITodoRepository
{
    // Standard CRUD operations
    Task<List<Todo>> GetAllTodosAsync(CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);

    // Specification-based queries
    Task<List<Todo>> FindAsync(Specification<Todo> spec, CancellationToken ct);
    Task<int> CountAsync(Specification<Todo> spec, CancellationToken ct);

    // ✅ Paginated queries
    Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        SortOrder<Todo>? sortOrder,
        CancellationToken ct);

    // ✅ Simplified overload with default sorting
    Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct);

    // Write operations
    void AddTodo(Todo todo);
    void UpdateTodo(Todo todo);
    void DeleteTodo(Todo todo);
}
```

### Step 4: Test Repository Implementation

```csharp
// Tests/TestInfrastructure/TestTodoRepository.cs
using TodoApp.Domain.Models;
using TodoApp.Domain.Specifications;

public class TestTodoRepository : ITodoRepository
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

    // ... existing methods ...

    // ✅ Paginated query (LINQ to Objects)
    public Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        SortOrder<Todo>? sortOrder,
        CancellationToken ct)
    {
        // Validate pagination parameters
        if (pageNumber < 1)
            throw new ArgumentException("Page number must be >= 1", nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentException("Page size must be >= 1", nameof(pageSize));

        // 1. Apply specification filter
        var predicate = spec.ToExpression().Compile();
        var filtered = _todos.Values.Where(predicate);

        // 2. Get total count (before pagination)
        var totalCount = filtered.Count();

        // 3. Apply sorting
        IEnumerable<Todo> sorted = sortOrder switch
        {
            { Ascending: true } => ApplyOrderBy(filtered, sortOrder.OrderBy, ascending: true),
            { Ascending: false } => ApplyOrderBy(filtered, sortOrder.OrderBy, ascending: false),
            null => filtered.OrderByDescending(t => t.CreatedAt) // Default sort
        };

        // 4. Apply pagination
        var items = sorted
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 5. Build result
        var result = new PagedResult<Todo>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Task.FromResult(result);
    }

    public Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct)
    {
        return FindPagedAsync(spec, pageNumber, pageSize, sortOrder: null, ct);
    }

    // Helper method to apply sorting with expression
    private static IEnumerable<Todo> ApplyOrderBy(
        IEnumerable<Todo> source,
        Expression<Func<Todo, object>> orderBy,
        bool ascending)
    {
        var compiled = orderBy.Compile();
        return ascending
            ? source.OrderBy(compiled)
            : source.OrderByDescending(compiled);
    }

    // ... existing write operations and helpers ...
}
```

### Step 5: Live Repository Implementation

```csharp
// Infrastructure/Live/LiveTodoRepository.cs
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain.Models;
using TodoApp.Domain.Specifications;

public class LiveTodoRepository(AppDbContext context) : ITodoRepository
{
    // ... existing methods ...

    // ✅ Paginated query (EF Core - translates to SQL with OFFSET/FETCH)
    public async Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        SortOrder<Todo>? sortOrder,
        CancellationToken ct)
    {
        // Validate pagination parameters
        if (pageNumber < 1)
            throw new ArgumentException("Page number must be >= 1", nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentException("Page size must be >= 1", nameof(pageSize));

        // 1. Build base query with specification filter
        var query = context.Todos
            .AsNoTracking()
            .Where(spec.ToExpression());

        // 2. Get total count (single COUNT query)
        var totalCount = await query.CountAsync(ct);

        // 3. Apply sorting
        IQueryable<Todo> sorted = sortOrder switch
        {
            { Ascending: true } => ApplyOrderBy(query, sortOrder.OrderBy, ascending: true),
            { Ascending: false } => ApplyOrderBy(query, sortOrder.OrderBy, ascending: false),
            null => query.OrderByDescending(t => t.CreatedAt) // Default sort
        };

        // 4. Apply pagination (OFFSET/FETCH in SQL)
        var items = await sorted
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // 5. Build result
        return new PagedResult<Todo>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct)
    {
        return FindPagedAsync(spec, pageNumber, pageSize, sortOrder: null, ct);
    }

    // Helper method to apply sorting (preserves IQueryable for SQL translation)
    private static IQueryable<Todo> ApplyOrderBy(
        IQueryable<Todo> query,
        Expression<Func<Todo, object>> orderBy,
        bool ascending)
    {
        return ascending
            ? query.OrderBy(orderBy)
            : query.OrderByDescending(orderBy);
    }

    // ... existing write operations ...
}
```

### Step 6: Repository Capability Module

```csharp
// Infrastructure/Capabilities/TodoRepository.cs
using TodoApp.Domain.Models;
using TodoApp.Domain.Specifications;

public static class TodoRepository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, ITodoRepository>
{
    // ... existing methods ...

    // ✅ Paginated query with sorting
    public static K<M, PagedResult<Todo>> findPaged(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        SortOrder<Todo>? sortOrder = null) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            repo.FindPagedAsync(spec, pageNumber, pageSize, sortOrder, env.Token)))
        select result;

    // ✅ Overload with default sorting
    public static K<M, PagedResult<Todo>> findPaged(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize) =>
        findPaged(spec, pageNumber, pageSize, sortOrder: null);
}
```

### Step 7: Usage in Application Services

```csharp
// Application/Todos/TodoService.cs
using TodoApp.Domain.Models;
using TodoApp.Domain.Specifications;

public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,
               Has<M, IUnitOfWork>,
               Has<M, ILoggerIO>
{
    /// <summary>
    /// Gets a paginated list of user's todos with optional filters.
    /// </summary>
    public static K<M, PagedResult<Todo>> GetUserTodosPaged(
        int userId,
        int pageNumber,
        int pageSize,
        bool? isCompleted = null,
        string? searchTerm = null) =>

        from _ in Logger<M, RT>.logInfo(
            "Getting todos page {Page} for user {UserId}", pageNumber, userId)

        // Build specification based on filters
        from spec in buildTodoSpec(userId, isCompleted, searchTerm)

        // Get paginated results sorted by created date (newest first)
        from result in TodoRepository<M, RT>.findPaged(
            spec,
            pageNumber,
            pageSize,
            TodoSortField.CreatedAtDesc)

        from __ in Logger<M, RT>.logInfo(
            "Returning {Count} of {Total} todos (page {Page}/{TotalPages})",
            result.Items.Count,
            result.TotalCount,
            result.PageNumber,
            result.TotalPages)

        select result;

    /// <summary>
    /// Gets high-priority todos, paginated and sorted.
    /// </summary>
    public static K<M, PagedResult<Todo>> GetHighPriorityTodosPaged(
        int userId,
        int pageNumber,
        int pageSize) =>

        from spec in M.Pure(
            new HighPriorityTodoSpec()
                .And(new TodosByUserSpec(userId))
        )

        from result in TodoRepository<M, RT>.findPaged(
            spec,
            pageNumber,
            pageSize,
            TodoSortField.CreatedAtAsc)  // Oldest first (most urgent)

        select result;

    /// <summary>
    /// Search todos with pagination and custom sorting.
    /// </summary>
    public static K<M, PagedResult<Todo>> SearchTodosPaged(
        int userId,
        string searchTerm,
        int pageNumber,
        int pageSize,
        bool sortByTitleInsteadOfDate = false) =>

        from spec in M.Pure(
            new TodosByUserSpec(userId)
                .And(new TodosTitleContainsSpec(searchTerm))
        )

        from sortOrder in M.Pure(sortByTitleInsteadOfDate
            ? TodoSortField.TitleAsc
            : TodoSortField.CreatedAtDesc)

        from result in TodoRepository<M, RT>.findPaged(
            spec,
            pageNumber,
            pageSize,
            sortOrder)

        select result;

    // Helper to build specification from filters
    private static K<M, Specification<Todo>> buildTodoSpec(
        int userId,
        bool? isCompleted,
        string? searchTerm)
    {
        Specification<Todo> spec = new TodosByUserSpec(userId);

        if (isCompleted.HasValue)
        {
            spec = isCompleted.Value
                ? spec.And(new CompletedTodoSpec())
                : spec.And(new IncompleteTodoSpec());
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            spec = spec.And(new TodosTitleContainsSpec(searchTerm));
        }

        return M.Pure(spec);
    }
}
```

### Step 8: API/Controller Usage

```csharp
// API/Controllers/TodosController.cs
using Microsoft.AspNetCore.Mvc;
using TodoApp.Domain.Models;

[ApiController]
[Route("api/users/{userId}/todos")]
public class TodosController(AppRuntime runtime) : ControllerBase
{
    /// <summary>
    /// GET /api/users/5/todos?page=1&pageSize=20&completed=false&search=urgent
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TodoDto>>> GetTodos(
        int userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? completed = null,
        [FromQuery] string? search = null)
    {
        var result = await TodoService<Eff<AppRuntime>, AppRuntime>
            .GetUserTodosPaged(userId, page, pageSize, completed, search)
            .RunAsync(runtime, EnvIO.New());

        return result.Match(
            Succ: pagedTodos => Ok(pagedTodos.Map(TodoDto.FromDomain)),
            Fail: error => error.Code switch
            {
                404 => NotFound(error.Message),
                _ => StatusCode(500, error.Message)
            }
        );
    }

    /// <summary>
    /// Response includes pagination metadata in headers (optional pattern)
    /// </summary>
    [HttpGet("with-headers")]
    public async Task<ActionResult<List<TodoDto>>> GetTodosWithHeaders(
        int userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await TodoService<Eff<AppRuntime>, AppRuntime>
            .GetUserTodosPaged(userId, page, pageSize)
            .RunAsync(runtime, EnvIO.New());

        return result.Match(
            Succ: pagedTodos =>
            {
                // Add pagination metadata to response headers
                Response.Headers.Add("X-Total-Count", pagedTodos.TotalCount.ToString());
                Response.Headers.Add("X-Total-Pages", pagedTodos.TotalPages.ToString());
                Response.Headers.Add("X-Current-Page", pagedTodos.PageNumber.ToString());
                Response.Headers.Add("X-Page-Size", pagedTodos.PageSize.ToString());
                Response.Headers.Add("X-Has-Next", pagedTodos.HasNextPage.ToString());
                Response.Headers.Add("X-Has-Previous", pagedTodos.HasPreviousPage.ToString());

                return Ok(pagedTodos.Items.Select(TodoDto.FromDomain));
            },
            Fail: error => StatusCode(500, error.Message)
        );
    }
}
```

## Testing Pagination

```csharp
// Tests/TodoServiceTests.cs
using TodoApp.Domain.Models;
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
    public async Task GetUserTodosPaged_ReturnsCorrectPage()
    {
        // Arrange - Create 25 todos for user 1
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = Enumerable.Range(1, 25)
            .Select(i => new Todo
            {
                Id = i,
                Title = $"Todo {i}",
                UserId = 1,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            })
            .ToArray();
        _runtime.TodoRepository.Seed(todos);

        // Act - Get page 2 (items 11-20)
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetUserTodosPaged(userId: 1, pageNumber: 2, pageSize: 10)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var page = result.ThrowIfFail();

        Assert.AreEqual(10, page.Items.Count);       // Page size
        Assert.AreEqual(25, page.TotalCount);        // Total items
        Assert.AreEqual(2, page.PageNumber);         // Current page
        Assert.AreEqual(3, page.TotalPages);         // Total pages (25/10 = 3)
        Assert.True(page.HasPreviousPage);           // Can go back
        Assert.True(page.HasNextPage);               // Can go forward
        Assert.AreEqual(11, page.FirstItemIndex);    // First item index
        Assert.AreEqual(20, page.LastItemIndex);     // Last item index
    }

    [Test]
    public async Task GetUserTodosPaged_LastPage_CorrectCount()
    {
        // Arrange - 25 todos, page 3 should have only 5 items
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = Enumerable.Range(1, 25)
            .Select(i => new Todo
            {
                Id = i,
                Title = $"Todo {i}",
                UserId = 1,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            })
            .ToArray();
        _runtime.TodoRepository.Seed(todos);

        // Act - Get page 3 (last page, should have 5 items)
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetUserTodosPaged(userId: 1, pageNumber: 3, pageSize: 10)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var page = result.ThrowIfFail();

        Assert.AreEqual(5, page.Items.Count);        // Only 5 items on last page
        Assert.AreEqual(25, page.TotalCount);
        Assert.AreEqual(3, page.PageNumber);
        Assert.AreEqual(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);              // No next page
        Assert.AreEqual(21, page.FirstItemIndex);
        Assert.AreEqual(25, page.LastItemIndex);
    }

    [Test]
    public async Task GetUserTodosPaged_WithFilters_PaginatesFilteredResults()
    {
        // Arrange - 20 completed, 10 incomplete
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = Enumerable.Range(1, 30)
            .Select(i => new Todo
            {
                Id = i,
                Title = $"Todo {i}",
                UserId = 1,
                IsCompleted = i <= 20,  // First 20 are completed
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            })
            .ToArray();
        _runtime.TodoRepository.Seed(todos);

        // Act - Get page 1 of incomplete todos (should be 10 total)
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetUserTodosPaged(
                userId: 1,
                pageNumber: 1,
                pageSize: 5,
                isCompleted: false)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var page = result.ThrowIfFail();

        Assert.AreEqual(5, page.Items.Count);        // Page size
        Assert.AreEqual(10, page.TotalCount);        // Total incomplete todos
        Assert.AreEqual(2, page.TotalPages);         // 10/5 = 2 pages
        Assert.True(page.Items.All(t => !t.IsCompleted));
    }

    [Test]
    public async Task GetUserTodosPaged_EmptyResults_ReturnsEmptyPage()
    {
        // Arrange - User with no todos
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetUserTodosPaged(userId: 1, pageNumber: 1, pageSize: 10)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var page = result.ThrowIfFail();

        Assert.AreEqual(0, page.Items.Count);
        Assert.AreEqual(0, page.TotalCount);
        Assert.AreEqual(1, page.PageNumber);
        Assert.AreEqual(0, page.TotalPages);
        Assert.False(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Test]
    public async Task SearchTodosPaged_SortsByTitle()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = new[]
        {
            new Todo { Id = 1, Title = "Zebra task", UserId = 1, CreatedAt = DateTime.UtcNow },
            new Todo { Id = 2, Title = "Apple task", UserId = 1, CreatedAt = DateTime.UtcNow },
            new Todo { Id = 3, Title = "Banana task", UserId = 1, CreatedAt = DateTime.UtcNow }
        };
        _runtime.TodoRepository.Seed(todos);

        // Act - Search with title sorting
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .SearchTodosPaged(
                userId: 1,
                searchTerm: "task",
                pageNumber: 1,
                pageSize: 10,
                sortByTitleInsteadOfDate: true)
            .RunAsync(_runtime, EnvIO.New());

        // Assert - Should be alphabetically sorted
        var page = result.ThrowIfFail();

        Assert.AreEqual(3, page.Items.Count);
        Assert.AreEqual("Apple task", page.Items[0].Title);
        Assert.AreEqual("Banana task", page.Items[1].Title);
        Assert.AreEqual("Zebra task", page.Items[2].Title);
    }
}
```

## Extending Source Generator for Pagination

Update the source generator to automatically include pagination methods:

```csharp
// In RepositoryGenerator.cs - GenerateTraitInterface
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
using TodoApp.Domain.Models;
using TodoApp.Domain.Specifications;
using {{entity.Namespace}};

namespace TodoApp.Infrastructure.Traits;

public interface I{{entity.EntityName}}Repository
{
    // Read operations
    Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct);
    Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct);

    // Specification-based queries
    Task<List<{{entity.EntityName}}>> FindAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct);
    Task<int> CountAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct);

    // ✅ Pagination (auto-generated)
    Task<PagedResult<{{entity.EntityName}}>> FindPagedAsync(
        Specification<{{entity.EntityName}}> spec,
        int pageNumber,
        int pageSize,
        SortOrder<{{entity.EntityName}}>? sortOrder,
        CancellationToken ct);

    Task<PagedResult<{{entity.EntityName}}>> FindPagedAsync(
        Specification<{{entity.EntityName}}> spec,
        int pageNumber,
        int pageSize,
        CancellationToken ct);

    // Write operations
    void Add{{entity.EntityName}}({{entity.EntityName}} entity);
    void Update{{entity.EntityName}}({{entity.EntityName}} entity);
    void Delete{{entity.EntityName}}({{entity.EntityName}} entity);
}
""";

    context.AddSource($"I{entity.EntityName}Repository.g.cs", source);
}

// Similarly update GenerateCapabilityModule, GenerateTestRepository, GenerateLiveRepository
```

## Frontend Integration

### React/TypeScript Example

```typescript
// types/pagination.ts
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// hooks/usePaginatedTodos.ts
import { useState, useEffect } from 'react';

export function usePaginatedTodos(userId: number) {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [data, setData] = useState<PagedResult<Todo> | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const fetchTodos = async () => {
      setLoading(true);
      try {
        const response = await fetch(
          `/api/users/${userId}/todos?page=${page}&pageSize=${pageSize}`
        );
        const result: PagedResult<Todo> = await response.json();
        setData(result);
      } finally {
        setLoading(false);
      }
    };

    fetchTodos();
  }, [userId, page, pageSize]);

  return {
    todos: data?.items ?? [],
    totalCount: data?.totalCount ?? 0,
    currentPage: page,
    totalPages: data?.totalPages ?? 0,
    hasNext: data?.hasNextPage ?? false,
    hasPrevious: data?.hasPreviousPage ?? false,
    goToPage: setPage,
    nextPage: () => setPage(p => p + 1),
    previousPage: () => setPage(p => Math.max(1, p - 1)),
    loading
  };
}

// components/TodoList.tsx
export function TodoList({ userId }: { userId: number }) {
  const {
    todos,
    totalCount,
    currentPage,
    totalPages,
    hasNext,
    hasPrevious,
    nextPage,
    previousPage,
    loading
  } = usePaginatedTodos(userId);

  return (
    <div>
      <h2>Todos ({totalCount} total)</h2>

      {loading ? (
        <Spinner />
      ) : (
        <ul>
          {todos.map(todo => (
            <li key={todo.id}>{todo.title}</li>
          ))}
        </ul>
      )}

      <div className="pagination">
        <button onClick={previousPage} disabled={!hasPrevious}>
          Previous
        </button>
        <span>Page {currentPage} of {totalPages}</span>
        <button onClick={nextPage} disabled={!hasNext}>
          Next
        </button>
      </div>
    </div>
  );
}
```

## Advanced: Cursor-Based Pagination

For high-performance scenarios, consider cursor-based pagination:

```csharp
// Domain/Models/CursorPagedResult.cs
public record CursorPagedResult<T>
{
    public required List<T> Items { get; init; }
    public required string? NextCursor { get; init; }
    public required string? PreviousCursor { get; init; }
    public required bool HasMore { get; init; }
}

// Repository method
Task<CursorPagedResult<Todo>> FindPagedByCursorAsync(
    Specification<Todo> spec,
    string? cursor,
    int pageSize,
    CancellationToken ct);
```

## Performance Considerations

### EF Core Query Analysis

```csharp
// The generated SQL for pagination looks like:
/*
SELECT COUNT(*) FROM Todos WHERE UserId = @p0;  -- Total count

SELECT t.Id, t.Title, t.Description, t.IsCompleted, t.CreatedAt, t.UserId
FROM Todos t
WHERE t.UserId = @p0
ORDER BY t.CreatedAt DESC
OFFSET @p1 ROWS      -- (pageNumber - 1) * pageSize
FETCH NEXT @p2 ROWS  -- pageSize
ONLY;
*/
```

### Optimization Tips

1. **Add database indexes** on frequently sorted/filtered columns:
   ```sql
   CREATE INDEX IX_Todos_UserId_CreatedAt ON Todos(UserId, CreatedAt DESC);
   ```

2. **Avoid COUNT(*) for large tables** - Use approximate counts:
   ```csharp
   // Option: Cache total counts for common queries
   // Option: Return "10,000+" instead of exact count
   ```

3. **Use AsNoTracking()** for read-only queries (already in implementation)

4. **Consider cursor-based pagination** for real-time data or very large datasets

## Benefits

✅ **Efficient** - Loads only requested page, not all data
✅ **Scalable** - Works with millions of records
✅ **Composable** - Combines with specifications seamlessly
✅ **Testable** - Works in both test (dictionary) and production (SQL)
✅ **Type-safe** - Strongly typed pagination metadata
✅ **Flexible** - Supports custom sorting per query
✅ **Auto-generated** - Source generator includes pagination in all repositories
✅ **User-friendly** - Rich metadata (HasNextPage, TotalPages, etc.)

## Summary

Pagination pattern provides:

✅ **PagedResult<T>** - Rich pagination metadata
✅ **Specification integration** - Filter + paginate in one call
✅ **Flexible sorting** - Per-query sort configuration
✅ **Works everywhere** - Test (LINQ) and production (EF Core → SQL)
✅ **Auto-generated** - Source generator includes all pagination methods
✅ **Performance** - Efficient queries with OFFSET/FETCH
✅ **Frontend-ready** - Easy integration with React/Vue/Angular

**Complete example:**
```csharp
// Service
var result = await TodoRepository<M, RT>.findPaged(
    spec: new CompletedTodoSpec().And(new TodosByUserSpec(userId)),
    pageNumber: 2,
    pageSize: 20,
    sortOrder: TodoSortField.CreatedAtDesc
);

// Result
result.Items;           // 20 todos (or fewer on last page)
result.TotalCount;      // Total matching todos
result.PageNumber;      // 2
result.TotalPages;      // e.g., 5
result.HasNextPage;     // true
result.HasPreviousPage; // true
```

Perfect for building modern, scalable applications with the trait-based architecture!

---

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

---

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
