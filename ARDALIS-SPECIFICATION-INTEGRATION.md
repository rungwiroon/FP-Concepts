# Integrating Ardalis.Specification with Functional Architecture

## Overview of Ardalis.Specification

**Ardalis.Specification** is a mature, battle-tested library for the Specification pattern in .NET. It provides:

- Rich specification features (filtering, includes, ordering, paging)
- Built-in EF Core integration via `ISpecificationEvaluator`
- Fluent query builder API
- Support for projections and transformations
- Well-tested and maintained

**Key Question:** Can this OOP library work with our functional, trait-based architecture?

**Answer:** ✅ **YES!** With adapter layers, we get the best of both worlds:
- Ardalis's rich specification infrastructure
- Our functional composition and Has pattern
- Type-safe, testable, composable queries

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Application Service (Functional)                           │
│  Uses: TodoRepository<M, RT>.find(spec)                     │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  Adapter Layer                                              │
│  Converts: Ardalis.Specification ↔ Our Trait Architecture  │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Ardalis      │    │ Our Traits   │    │ EF Core      │
│ Specs        │    │ ITodoRepo    │    │ Evaluator    │
└──────────────┘    └──────────────┘    └──────────────┘
```

## Installation

```bash
cd TodoApp
dotnet add package Ardalis.Specification
dotnet add package Ardalis.Specification.EntityFrameworkCore
```

## Comparison: Custom vs Ardalis

### Our Custom Specification

```csharp
// ✅ Pure functional, expression-based
public class CompletedTodoSpec : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => todo.IsCompleted;
    }
}

// ✅ Simple, composable
var spec = new CompletedTodoSpec()
    .And(new TodosByUserSpec(userId));

// ❌ Limited features (no includes, ordering, paging built-in)
// ❌ Manual implementation needed
```

### Ardalis.Specification

```csharp
// ✅ Rich features (includes, ordering, paging, projections)
public class CompletedTodosByUserSpec : Specification<Todo>
{
    public CompletedTodosByUserSpec(int userId)
    {
        Query
            .Where(t => t.IsCompleted && t.UserId == userId)
            .Include(t => t.User)               // ✅ EF Core includes
            .OrderByDescending(t => t.CreatedAt) // ✅ Built-in ordering
            .Skip(20).Take(10);                  // ✅ Built-in paging
    }
}

// ❌ OOP-style (inheritance, mutable Query property)
// ❌ Not directly composable with And/Or
// ❌ Requires inheritance (not pure functional)
```

## Hybrid Approach: Best of Both Worlds

Use Ardalis for infrastructure, wrap it in functional adapters.

### Step 1: Install Packages

```xml
<!-- TodoApp/TodoApp.csproj -->
<ItemGroup>
  <PackageReference Include="Ardalis.Specification" Version="7.0.0" />
  <PackageReference Include="Ardalis.Specification.EntityFrameworkCore" Version="7.0.0" />
</ItemGroup>
```

### Step 2: Create Ardalis-Based Specifications

```csharp
// Domain/Specifications/Ardalis/CompletedTodosByUserSpec.cs
using Ardalis.Specification;

namespace TodoApp.Domain.Specifications.Ardalis;

/// <summary>
/// Ardalis specification for completed todos by user.
/// Includes User navigation property and orders by creation date.
/// </summary>
public sealed class CompletedTodosByUserSpec : Specification<Todo>
{
    public CompletedTodosByUserSpec(int userId)
    {
        Query
            .Where(t => t.IsCompleted && t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);
    }
}

/// <summary>
/// Paginated version with includes.
/// </summary>
public sealed class CompletedTodosByUserPagedSpec : Specification<Todo>
{
    public CompletedTodosByUserPagedSpec(int userId, int page, int pageSize)
    {
        Query
            .Where(t => t.IsCompleted && t.UserId == userId)
            .Include(t => t.User)  // EF Core navigation property
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking();  // Performance optimization
    }
}

/// <summary>
/// Search with includes and ordering.
/// </summary>
public sealed class SearchTodosSpec : Specification<Todo>
{
    public SearchTodosSpec(
        int userId,
        string searchTerm,
        bool? isCompleted = null,
        DateTime? createdAfter = null)
    {
        Query.Where(t => t.UserId == userId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            Query.Where(t => t.Title.Contains(searchTerm) ||
                            (t.Description != null && t.Description.Contains(searchTerm)));
        }

        if (isCompleted.HasValue)
        {
            Query.Where(t => t.IsCompleted == isCompleted.Value);
        }

        if (createdAfter.HasValue)
        {
            Query.Where(t => t.CreatedAt > createdAfter.Value);
        }

        Query
            .Include(t => t.User)
            .OrderByDescending(t => t.CreatedAt)
            .AsNoTracking();
    }
}

/// <summary>
/// Projection specification - returns DTOs instead of entities.
/// </summary>
public sealed class TodoSummarySpec : Specification<Todo, TodoSummaryDto>
{
    public TodoSummarySpec(int userId)
    {
        Query
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        Query.Select(t => new TodoSummaryDto
        {
            Id = t.Id,
            Title = t.Title,
            IsCompleted = t.IsCompleted,
            CreatedAt = t.CreatedAt,
            UserName = t.User.Name  // Projected from navigation
        });
    }
}

// DTO for projection
public record TodoSummaryDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public string UserName { get; init; } = string.Empty;
}
```

### Step 3: Update Repository Interface

```csharp
// Infrastructure/Traits/ITodoRepository.cs
using Ardalis.Specification;
using TodoApp.Domain.Models;

public interface ITodoRepository
{
    // Standard CRUD
    Task<List<Todo>> GetAllTodosAsync(CancellationToken ct);
    Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct);

    // ✅ Ardalis.Specification support
    Task<List<Todo>> FindAsync(ISpecification<Todo> spec, CancellationToken ct);
    Task<Option<Todo>> FindOneAsync(ISpecification<Todo> spec, CancellationToken ct);
    Task<int> CountAsync(ISpecification<Todo> spec, CancellationToken ct);

    // ✅ Projection support
    Task<List<TResult>> FindAsync<TResult>(ISpecification<Todo, TResult> spec, CancellationToken ct);
    Task<Option<TResult>> FindOneAsync<TResult>(ISpecification<Todo, TResult> spec, CancellationToken ct);

    // ✅ Pagination (Ardalis has built-in paging in specs)
    Task<PagedResult<Todo>> FindPagedAsync(ISpecification<Todo> spec, CancellationToken ct);

    // Write operations
    void AddTodo(Todo todo);
    void UpdateTodo(Todo todo);
    void DeleteTodo(Todo todo);
}
```

### Step 4: Live Repository Implementation

```csharp
// Infrastructure/Live/LiveTodoRepository.cs
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain.Models;

public class LiveTodoRepository(AppDbContext context) : ITodoRepository
{
    // ✅ Use Ardalis's SpecificationEvaluator for EF Core
    private readonly ISpecificationEvaluator _evaluator = SpecificationEvaluator.Default;

    // Standard CRUD operations
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

    // ✅ Ardalis specification queries (entities)
    public async Task<List<Todo>> FindAsync(ISpecification<Todo> spec, CancellationToken ct)
    {
        // Ardalis evaluator applies all spec logic (where, include, orderby, skip, take)
        var query = ApplySpecification(spec);
        return await query.ToListAsync(ct);
    }

    public async Task<Option<Todo>> FindOneAsync(ISpecification<Todo> spec, CancellationToken ct)
    {
        var query = ApplySpecification(spec);
        var entity = await query.FirstOrDefaultAsync(ct);
        return Optional(entity);
    }

    public async Task<int> CountAsync(ISpecification<Todo> spec, CancellationToken ct)
    {
        // Apply only filtering, not ordering/paging
        var query = ApplySpecification(spec, evaluateCriteriaOnly: true);
        return await query.CountAsync(ct);
    }

    // ✅ Ardalis specification queries with projection
    public async Task<List<TResult>> FindAsync<TResult>(
        ISpecification<Todo, TResult> spec,
        CancellationToken ct)
    {
        var query = ApplySpecification(spec);
        return await query.ToListAsync(ct);
    }

    public async Task<Option<TResult>> FindOneAsync<TResult>(
        ISpecification<Todo, TResult> spec,
        CancellationToken ct)
    {
        var query = ApplySpecification(spec);
        var result = await query.FirstOrDefaultAsync(ct);
        return Optional(result);
    }

    // ✅ Pagination using spec's built-in Skip/Take
    public async Task<PagedResult<Todo>> FindPagedAsync(
        ISpecification<Todo> spec,
        CancellationToken ct)
    {
        // Get total count (before paging)
        var totalCount = await CountAsync(spec, ct);

        // Get paged items (with Skip/Take from spec)
        var items = await FindAsync(spec, ct);

        // Extract pagination info from spec
        var pageSize = spec.Take ?? items.Count;
        var skip = spec.Skip ?? 0;
        var pageNumber = pageSize > 0 ? (skip / pageSize) + 1 : 1;

        return new PagedResult<Todo>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
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

    // Helper: Apply specification to query
    private IQueryable<Todo> ApplySpecification(
        ISpecification<Todo> spec,
        bool evaluateCriteriaOnly = false)
    {
        return _evaluator.GetQuery(
            context.Todos.AsQueryable(),
            spec,
            evaluateCriteriaOnly);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(
        ISpecification<Todo, TResult> spec)
    {
        return _evaluator.GetQuery(
            context.Todos.AsQueryable(),
            spec);
    }
}
```

### Step 5: Test Repository Implementation

```csharp
// Tests/TestInfrastructure/TestTodoRepository.cs
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;

public class TestTodoRepository : ITodoRepository
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

    // ✅ Use in-memory evaluator for tests
    private readonly InMemorySpecificationEvaluator _evaluator = new();

    // Standard CRUD...
    public Task<List<Todo>> GetAllTodosAsync(CancellationToken ct)
    {
        return Task.FromResult(_todos.Values.ToList());
    }

    // ✅ Ardalis specification queries (in-memory)
    public Task<List<Todo>> FindAsync(ISpecification<Todo> spec, CancellationToken ct)
    {
        // Apply specification to in-memory collection
        var query = _evaluator.GetQuery(_todos.Values.AsQueryable(), spec);
        var results = query.ToList();
        return Task.FromResult(results);
    }

    public Task<Option<Todo>> FindOneAsync(ISpecification<Todo> spec, CancellationToken ct)
    {
        var query = _evaluator.GetQuery(_todos.Values.AsQueryable(), spec);
        var result = query.FirstOrDefault();
        return Task.FromResult(Optional(result));
    }

    public Task<int> CountAsync(ISpecification<Todo> spec, CancellationToken ct)
    {
        var query = _evaluator.GetQuery(
            _todos.Values.AsQueryable(),
            spec,
            evaluateCriteriaOnly: true);
        return Task.FromResult(query.Count());
    }

    // ✅ Projection support
    public Task<List<TResult>> FindAsync<TResult>(
        ISpecification<Todo, TResult> spec,
        CancellationToken ct)
    {
        var query = _evaluator.GetQuery(_todos.Values.AsQueryable(), spec);
        return Task.FromResult(query.ToList());
    }

    public Task<Option<TResult>> FindOneAsync<TResult>(
        ISpecification<Todo, TResult> spec,
        CancellationToken ct)
    {
        var query = _evaluator.GetQuery(_todos.Values.AsQueryable(), spec);
        var result = query.FirstOrDefault();
        return Task.FromResult(Optional(result));
    }

    // ✅ Pagination
    public async Task<PagedResult<Todo>> FindPagedAsync(
        ISpecification<Todo> spec,
        CancellationToken ct)
    {
        var totalCount = await CountAsync(spec, ct);
        var items = await FindAsync(spec, ct);

        var pageSize = spec.Take ?? items.Count;
        var skip = spec.Skip ?? 0;
        var pageNumber = pageSize > 0 ? (skip / pageSize) + 1 : 1;

        return new PagedResult<Todo>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    // Write operations...
    public void AddTodo(Todo todo)
    {
        if (todo.Id == 0)
            todo = todo with { Id = _nextId++ };
        _todos[todo.Id] = todo;
    }

    // Test helpers...
    public void Seed(params Todo[] todos)
    {
        foreach (var todo in todos)
            _todos[todo.Id] = todo;
        _nextId = _todos.Count > 0 ? _todos.Keys.Max() + 1 : 1;
    }

    public void Clear() => _todos.Clear();
    public int Count => _todos.Count;
}

// In-memory evaluator for tests
public class InMemorySpecificationEvaluator : ISpecificationEvaluator
{
    public static InMemorySpecificationEvaluator Default { get; } = new();

    public IQueryable<TResult> GetQuery<T, TResult>(
        IQueryable<T> query,
        ISpecification<T, TResult> specification)
    {
        return SpecificationEvaluator.Default.GetQuery(query, specification);
    }

    public IQueryable<T> GetQuery<T>(
        IQueryable<T> query,
        ISpecification<T> specification,
        bool evaluateCriteriaOnly = false)
    {
        return SpecificationEvaluator.Default.GetQuery(
            query,
            specification,
            evaluateCriteriaOnly);
    }
}
```

### Step 6: Repository Capability Module

```csharp
// Infrastructure/Capabilities/TodoRepository.cs
using Ardalis.Specification;
using TodoApp.Domain.Models;

public static class TodoRepository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, ITodoRepository>
{
    // Standard queries...
    public static K<M, List<Todo>> getAllTodos =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todos in M.LiftIO(IO.liftAsync(env =>
            repo.GetAllTodosAsync(env.Token)))
        select todos;

    // ✅ Ardalis specification queries
    public static K<M, List<Todo>> find(ISpecification<Todo> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todos in M.LiftIO(IO.liftAsync(env =>
            repo.FindAsync(spec, env.Token)))
        select todos;

    public static K<M, Option<Todo>> findOne(ISpecification<Todo> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from todo in M.LiftIO(IO.liftAsync(env =>
            repo.FindOneAsync(spec, env.Token)))
        select todo;

    public static K<M, int> count(ISpecification<Todo> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from count in M.LiftIO(IO.liftAsync(env =>
            repo.CountAsync(spec, env.Token)))
        select count;

    // ✅ Projection queries
    public static K<M, List<TResult>> findProjected<TResult>(
        ISpecification<Todo, TResult> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from results in M.LiftIO(IO.liftAsync(env =>
            repo.FindAsync(spec, env.Token)))
        select results;

    public static K<M, Option<TResult>> findOneProjected<TResult>(
        ISpecification<Todo, TResult> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            repo.FindOneAsync(spec, env.Token)))
        select result;

    // ✅ Pagination
    public static K<M, PagedResult<Todo>> findPaged(ISpecification<Todo> spec) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            repo.FindPagedAsync(spec, env.Token)))
        select result;

    // Write operations...
    public static K<M, Unit> addTodo(Todo todo) =>
        from repo in Has<M, RT, ITodoRepository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.AddTodo(todo);
            return unit;
        })
        select unit;
}
```

### Step 7: Usage in Application Services

```csharp
// Application/Todos/TodoService.cs
using Ardalis.Specification;
using TodoApp.Domain.Specifications.Ardalis;

public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,
               Has<M, IUnitOfWork>,
               Has<M, ILoggerIO>
{
    /// <summary>
    /// Gets completed todos for user (using Ardalis spec).
    /// </summary>
    public static K<M, List<Todo>> GetCompletedTodosByUser(int userId) =>
        from _ in Logger<M, RT>.logInfo(
            "Getting completed todos for user {UserId}", userId)

        // ✅ Use Ardalis specification
        from todos in TodoRepository<M, RT>.find(
            new CompletedTodosByUserSpec(userId))

        from __ in Logger<M, RT>.logInfo(
            "Found {Count} completed todos", todos.Count)

        select todos;

    /// <summary>
    /// Gets paginated, completed todos with includes.
    /// </summary>
    public static K<M, PagedResult<Todo>> GetCompletedTodosPaged(
        int userId,
        int page,
        int pageSize) =>

        from _ in Logger<M, RT>.logInfo(
            "Getting completed todos page {Page} for user {UserId}",
            page, userId)

        // ✅ Ardalis handles paging, includes, ordering in spec
        from result in TodoRepository<M, RT>.findPaged(
            new CompletedTodosByUserPagedSpec(userId, page, pageSize))

        from __ in Logger<M, RT>.logInfo(
            "Returning {Count} of {Total} todos",
            result.Items.Count, result.TotalCount)

        select result;

    /// <summary>
    /// Search todos with dynamic filters (using Ardalis spec).
    /// </summary>
    public static K<M, List<Todo>> SearchTodos(
        int userId,
        string searchTerm,
        bool? isCompleted = null,
        DateTime? createdAfter = null) =>

        from _ in Logger<M, RT>.logInfo(
            "Searching todos for user {UserId}: {SearchTerm}", userId, searchTerm)

        // ✅ Ardalis spec with multiple dynamic filters
        from todos in TodoRepository<M, RT>.find(
            new SearchTodosSpec(userId, searchTerm, isCompleted, createdAfter))

        select todos;

    /// <summary>
    /// Gets todo summaries (projection, no full entities).
    /// </summary>
    public static K<M, List<TodoSummaryDto>> GetTodoSummaries(int userId) =>
        from _ in Logger<M, RT>.logInfo(
            "Getting todo summaries for user {UserId}", userId)

        // ✅ Projection spec returns DTOs, not entities
        from summaries in TodoRepository<M, RT>.findProjected(
            new TodoSummarySpec(userId))

        from __ in Logger<M, RT>.logInfo(
            "Returning {Count} summaries", summaries.Count)

        select summaries;
}
```

## Testing with Ardalis Specifications

```csharp
// Tests/TodoServiceTests.cs
using TodoApp.Domain.Specifications.Ardalis;

public class TodoServiceTests
{
    private TestRuntime _runtime;

    [SetUp]
    public void Setup()
    {
        _runtime = new TestRuntime();
    }

    [Test]
    public async Task GetCompletedTodosByUser_WithArdalisSpec_ReturnsFiltered()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = new[]
        {
            new Todo { Id = 1, Title = "Complete", UserId = 1, IsCompleted = true },
            new Todo { Id = 2, Title = "Incomplete", UserId = 1, IsCompleted = false },
            new Todo { Id = 3, Title = "Other user", UserId = 2, IsCompleted = true }
        };
        _runtime.TodoRepository.Seed(todos);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetCompletedTodosByUser(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var filtered = result.ThrowIfFail();
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("Complete", filtered[0].Title);
    }

    [Test]
    public async Task GetCompletedTodosPaged_WithArdalisSpec_ReturnsPaged()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = Enumerable.Range(1, 25)
            .Select(i => new Todo
            {
                Id = i,
                Title = $"Todo {i}",
                UserId = 1,
                IsCompleted = true,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            })
            .ToArray();
        _runtime.TodoRepository.Seed(todos);

        // Act - Page 2, size 10
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetCompletedTodosPaged(userId: 1, page: 2, pageSize: 10)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var page = result.ThrowIfFail();
        Assert.AreEqual(10, page.Items.Count);
        Assert.AreEqual(25, page.TotalCount);
        Assert.AreEqual(2, page.PageNumber);
        Assert.AreEqual(3, page.TotalPages);
    }

    [Test]
    public async Task SearchTodos_WithDynamicFilters_Works()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@test.com", Name = "Test" };
        _runtime.UserRepository.Seed(user);

        var todos = new[]
        {
            new Todo { Id = 1, Title = "Buy milk urgent", UserId = 1, IsCompleted = false },
            new Todo { Id = 2, Title = "Buy bread", UserId = 1, IsCompleted = false },
            new Todo { Id = 3, Title = "Clean urgent", UserId = 1, IsCompleted = true }
        };
        _runtime.TodoRepository.Seed(todos);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .SearchTodos(
                userId: 1,
                searchTerm: "urgent",
                isCompleted: false)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var filtered = result.ThrowIfFail();
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("Buy milk urgent", filtered[0].Title);
    }

    [Test]
    public async Task GetTodoSummaries_Projection_ReturnsDto()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@test.com", Name = "John Doe" };
        _runtime.UserRepository.Seed(user);

        var todo = new Todo
        {
            Id = 1,
            Title = "Test Todo",
            UserId = 1,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };
        _runtime.TodoRepository.Seed(todo);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .GetTodoSummaries(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var summaries = result.ThrowIfFail();
        Assert.AreEqual(1, summaries.Count);
        Assert.AreEqual("Test Todo", summaries[0].Title);
        Assert.AreEqual("John Doe", summaries[0].UserName);  // Projected!
    }
}
```

## Is Ardalis.Specification Functional Programming Compatible?

### Analysis

**OOP Aspects (Not FP-pure):**
- ❌ Uses inheritance (`Specification<T>` base class)
- ❌ Mutable `Query` property
- ❌ Fluent builder pattern (side effects)
- ❌ Not directly composable with And/Or

**FP-Compatible Aspects:**
- ✅ Expression trees (pure, composable)
- ✅ Immutable query execution
- ✅ Deterministic evaluation
- ✅ Works with LINQ (functional at core)
- ✅ Can be wrapped in functional adapters

### Verdict: ✅ **Compatible with Adapter Pattern**

We can use Ardalis.Specification in our functional architecture by:

1. **Encapsulating specs in factories** - Pure functions that create specs
2. **Wrapping in functional capabilities** - `TodoRepository<M, RT>.find(spec)`
3. **Treating specs as values** - Pass them around immutably
4. **Using adapters** - Bridge between OOP specs and FP traits

## Pros and Cons

### Pros of Using Ardalis.Specification

✅ **Battle-tested** - Production-proven, well-maintained library
✅ **Rich features** - Includes, ordering, paging, projections built-in
✅ **EF Core integration** - Optimized query generation
✅ **Documentation** - Extensive examples and community support
✅ **Less code** - Don't implement specification infrastructure yourself
✅ **Performance** - Highly optimized evaluators
✅ **Projections** - Built-in DTO support

### Cons

❌ **OOP-style** - Inheritance, not pure FP
❌ **Dependency** - External library to maintain
❌ **Less composable** - Can't directly And/Or specs
❌ **Learning curve** - Team needs to learn Ardalis API
❌ **Less control** - Can't customize evaluation logic as easily

### Recommendation

**Use Ardalis.Specification if:**
- You need rich query features (includes, projections)
- You want proven, maintained infrastructure
- Your team is comfortable with light OOP in domain
- You're building a large app with complex queries

**Use Custom Specification if:**
- You want pure FP (expression-based, composable)
- You need full control over evaluation
- You prefer minimal dependencies
- You want And/Or/Not composition

**Hybrid Approach (Best):**
- Use Ardalis for complex queries (includes, projections)
- Use custom specs for simple, composable filters
- Both work through same repository trait interface!

## Complete Example

```csharp
// Service using Ardalis spec
public static K<M, PagedResult<Todo>> GetDashboardTodos(int userId, int page) =>
    from result in TodoRepository<M, RT>.findPaged(
        new DashboardTodosSpec(userId, page, pageSize: 20))
    select result;

// Ardalis spec with all features
public class DashboardTodosSpec : Specification<Todo>
{
    public DashboardTodosSpec(int userId, int page, int pageSize)
    {
        Query
            .Where(t => t.UserId == userId && !t.IsCompleted)
            .Include(t => t.User)
            .Include(t => t.Project)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking();
    }
}

// Generated EF Core SQL:
/*
SELECT t.*, u.*, p.*
FROM Todos t
INNER JOIN Users u ON t.UserId = u.Id
LEFT JOIN Projects p ON t.ProjectId = p.Id
WHERE t.UserId = @p0 AND t.IsCompleted = 0
ORDER BY t.CreatedAt DESC
OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY
*/
```

## Summary

✅ **Ardalis.Specification is FP-compatible** via adapter pattern
✅ **Rich features** - Includes, ordering, paging, projections
✅ **Works with trait architecture** - Wrap in capability modules
✅ **Testable** - In-memory evaluator for tests
✅ **Production-ready** - Battle-tested, maintained, optimized

The hybrid approach gives you:
- Ardalis's rich infrastructure
- Our functional composition
- Type-safe trait-based architecture
- Best of both OOP and FP worlds

Choose based on your needs - both approaches work seamlessly with the trait-based repository architecture!
