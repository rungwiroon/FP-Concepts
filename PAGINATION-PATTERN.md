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
