# Functional Todo API

A Todo web application built with ASP.NET Core and SQLite demonstrating functional programming patterns using **language-ext v5** with the **Has<M, RT, T>.ask trait-based capability pattern**.

## Overview

This application showcases a modern functional programming approach in C# using traits and capabilities instead of custom monads. The implementation uses language-ext v5's Has<M, RT, T>.ask pattern for dependency injection and capability management.

## Features

- Full CRUD operations for todos (Create, Read, Update, Delete)
- Toggle completion status
- Applicative validation with error accumulation
- Structured logging with constant message templates
- Type-safe error handling with `Fin<A>`
- Trait-based capabilities (DatabaseIO, LoggerIO, TimeIO)
- SQLite database with Entity Framework Core
- Async/await with CancellationToken support

## Architecture

This application follows modern functional programming patterns with **language-ext v5**:

### Key Patterns

1. **`Has<M, RT, T>.ask` Pattern**: Trait-based dependency injection
   - Replaces custom monads with generic capabilities
   - Type-safe access to dependencies via traits
   - Composable and testable

2. **Traits as Capabilities**:
   - `DatabaseIO` - Database context and operations
   - `LoggerIO` - Structured logging
   - `TimeIO` - Pure time operations
   - `CancellationTokenIO` - Cancellation support

3. **Generic Service Layer**:
   - Generic over `M` (monad) and `RT` (runtime)
   - Constraints ensure required capabilities
   - Pure functions with composable effects

4. **`Eff<RT>` Monad**: Production runtime
   - Combines IO, Reader, and Error effects
   - Runtime environment provides all capabilities
   - Async execution with `RunAsync()`

## Technology Stack

- **ASP.NET Core 8.0** - Web framework
- **language-ext v5.0.0-beta-54** - Functional programming library
- **Entity Framework Core 9.0** - ORM
- **SQLite** - Database
- **Minimal APIs** - Endpoint routing

## Project Structure

```
TodoApp/
├── Domain/
│   └── Todo.cs                          # Todo entity record
├── Data/
│   └── AppDbContext.cs                  # EF Core DbContext
├── Infrastructure/
│   ├── AppRuntime.cs                    # Runtime environment
│   ├── ApiResultHelpers.cs              # HTTP result mapping
│   ├── Traits/                          # Capability trait interfaces
│   │   ├── DatabaseIO.cs                # Database operations trait
│   │   ├── LoggerIO.cs                  # Logging trait
│   │   ├── TimeIO.cs                    # Time operations trait
│   │   └── CancellationTokenIO.cs       # Cancellation trait
│   ├── Live/                            # Production implementations
│   │   ├── LiveDatabaseIO.cs            # EF Core implementation
│   │   ├── LiveLoggerIO.cs              # ILogger implementation
│   │   └── LiveTimeIO.cs                # DateTime implementation
│   ├── Capabilities/                    # Capability modules
│   │   ├── Database.cs                  # Database operations
│   │   ├── Logger.cs                    # Logging operations
│   │   └── Time.cs                      # Time operations
│   └── Extensions/
│       ├── LoggingExtensions.cs         # Logging helpers
│       ├── MetricsExtensions.cs         # Metrics/timing helpers
│       ├── OptionExtensions.cs          # Option<A>.To<M, A>()
│       └── ValidationExtensions.cs      # Validation<E, A>.To<M, A>()
├── Features/
│   └── Todos/
│       ├── TodoService.cs               # Generic service layer
│       ├── TodoValidation.cs            # Validation logic
│       └── TodoDtos.cs                  # Request/Response DTOs
└── Program.cs                           # API endpoints
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Any IDE (Visual Studio, VS Code, Rider)

### Installation

1. Restore packages:
```bash
cd TodoApp
dotnet restore
```

2. Run the application:
```bash
dotnet run
```

The API will be available at `http://localhost:5000` (or port shown in console).

### Swagger UI

When running in development mode, open your browser to:
```
http://localhost:5000/swagger
```

## API Endpoints

### List all todos
```http
GET /todos
```

### Get a todo by ID
```http
GET /todos/{id}
```

### Create a new todo
```http
POST /todos
Content-Type: application/json

{
  "title": "Buy groceries",
  "description": "Milk, eggs, bread"
}
```

### Update a todo
```http
PUT /todos/{id}
Content-Type: application/json

{
  "title": "Buy groceries",
  "description": "Milk, eggs, bread, butter"
}
```

### Toggle completion status
```http
PATCH /todos/{id}/toggle
```

### Delete a todo
```http
DELETE /todos/{id}
```

## Example Usage

### Create a todo
```bash
curl -X POST http://localhost:5000/todos \
  -H "Content-Type: application/json" \
  -d '{"title":"Learn functional programming","description":"Study language-ext v5"}'
```

### List all todos
```bash
curl http://localhost:5000/todos
```

### Get a specific todo
```bash
curl http://localhost:5000/todos/1
```

### Update a todo
```bash
curl -X PUT http://localhost:5000/todos/1 \
  -H "Content-Type: application/json" \
  -d '{"title":"Learn FP","description":"Study language-ext and trait-based patterns"}'
```

### Toggle completion
```bash
curl -X PATCH http://localhost:5000/todos/1/toggle
```

### Delete a todo
```bash
curl -X DELETE http://localhost:5000/todos/1
```

## Key Implementation Details

### The Has<M, RT, T>.ask Pattern

The Has<M, RT, T>.ask pattern provides trait-based dependency injection:

```csharp
// Define trait interface
public interface DatabaseIO
{
    AppDbContext GetContext();
    Task<Unit> SaveChangesAsync(CancellationToken cancellationToken);
}

// Implement Has<M, RT, T>.ask for the trait
static K<M, DatabaseIO> Has<M, RT, DatabaseIO>.Ask =>
    liftEff((Func<RT, DatabaseIO>)(rt => new LiveDatabaseIO(rt.Services)));

// Use in service layer
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(ct))
        from __ in Logger<M, RT>.logInfo("Found {TodosCount} todos", todos.Count)
        select todos;
}
```

### Generic Service Layer

All business logic is generic over `M` and `RT`:

```csharp
public static K<M, Todo> Get(int id) =>
    from _ in Logger<M, RT>.logInfo("Getting todo by ID: {Id}", id)
    from todo in Database<M, RT>.liftIO((ctx, ct) =>
        ctx.Todos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            .Map(Optional))
        .Bind(opt => opt.To<M, Todo>(() => Error.New(404, $"Todo with id {id} not found")))
    from __ in Logger<M, RT>.logInfo("Found todo: {TodoTitle}", todo.Title)
    select todo;
```

### AppRuntime - Production Runtime

The runtime provides all capabilities:

```csharp
public class AppRuntime :
    Has<Eff<AppRuntime>, DatabaseIO>,
    Has<Eff<AppRuntime>, LoggerIO>,
    Has<Eff<AppRuntime>, TimeIO>,
    Has<Eff<AppRuntime>, CancellationTokenIO>
{
    public IServiceProvider Services { get; }

    // Has<M, RT, T>.ask implementations use liftEff
}
```

### Endpoint Execution

Endpoints use `RunAsync` with `EnvIO` for cancellation:

```csharp
app.MapGet("/todos", async (IServiceProvider services, CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>.List()
        .RunAsync(runtime, EnvIO.New(token: ct));
    return ToResult(result, todos => Results.Ok(todos.Select(MapToResponse)));
});
```

### Structured Logging

All logging uses constant message templates with parameters:

```csharp
// Trait interface with [ConstantExpected]
public interface LoggerIO
{
    Unit LogInfo([ConstantExpected] string message, params object[] args);
    Unit LogWarning([ConstantExpected] string message, params object[] args);
    Unit LogError(Exception? ex, [ConstantExpected] string? message, params object[] args);
}

// Usage in service layer
Logger<M, RT>.logInfo("Getting todo by ID: {Id}", id)
Logger<M, RT>.logInfo("Found {TodosCount} todos", todos.Count)
```

### Validation with Error Accumulation

Applicative validation accumulates all errors:

```csharp
public static Validation<Error, CreateTodoRequest> Validate(CreateTodoRequest request) =>
    (ValidateTitle(request.Title), ValidateDescription(request.Description))
        .Apply((title, desc) => request with { Title = title, Description = desc })
        .As();

// Convert Validation to M
from validReq in TodoValidation.Validate(request)
    .To<M, CreateTodoRequest>()
```

### Extension Methods for Monadic Conversion

```csharp
// Option<A> to M
public static K<M, A> To<M, A>(this Option<A> option, Func<Error> onNone)
    where M : Monad<M>, Fallible<M> =>
    option.Match(
        Some: M.Pure,
        None: () => M.Fail<A>(onNone()));

// Validation<Error, A> to M
public static K<M, A> To<M, A>(this Validation<Error, A> validation)
    where M : Monad<M>, Fallible<M> =>
    validation.Match(
        Succ: M.Pure,
        Fail: errors => M.Fail<A>(errors.Head));
```

## Functional Concepts Demonstrated

### 1. Trait-Based Capabilities
Instead of custom monads, we use trait interfaces that define capabilities. The runtime implements these traits to provide functionality.

### 2. Has<M, RT, T>.ask Pattern
The three-parameter lowercase pattern for accessing capabilities:
- `M` - The monad type
- `RT` - The runtime type
- `T` - The trait/capability type

### 3. Generic Programming
The service layer is generic over `M` and `RT`, allowing different runtime implementations (production, testing, etc.).

### 4. LINQ Query Syntax
C# query syntax provides clean, readable monadic composition:
```csharp
from x in operation1
from y in operation2
from z in operation3
select result
```

### 5. Pure Functions
All business logic is pure - side effects are pushed to:
- Trait implementations (LiveDatabaseIO, LiveLoggerIO)
- Endpoint handlers (Program.cs)

### 6. Type Safety
The compiler ensures:
- All required capabilities are provided via constraints
- Effects are properly handled
- Errors are managed through `Fin<A>`
- Cancellation is propagated correctly

### 7. Composability
Operations compose naturally:
```csharp
from todo in Get(id)
from updated in Update(id, request)
from _ in Logger.logInfo("Updated todo {Id}", id)
select updated
```

### 8. Effect Extensions
Cross-cutting concerns through extensions:
```csharp
operation
    .WithLogging("Operation", result => $"Result: {result}")
    .WithMetrics("OperationName")
```

## Benefits of This Approach

1. **Composability** - Operations compose like LEGO blocks
2. **Testability** - Easy to provide test implementations of traits
3. **Maintainability** - Clear separation of concerns via traits
4. **Type Safety** - Compiler enforces all constraints
5. **Flexibility** - Can swap runtime implementations
6. **No Custom Monads** - Uses language-ext's built-in Eff<RT>
7. **Performance** - Efficient async/await with IO

## Testing

The trait-based design makes testing easy:

```csharp
// Test implementation
public class TestDatabaseIO : DatabaseIO
{
    private readonly AppDbContext testContext;

    public AppDbContext GetContext() => testContext;
    public Task<Unit> SaveChangesAsync(CancellationToken ct) => unit;
}

// Test runtime
public class TestRuntime : Has<Eff<TestRuntime>, DatabaseIO>
{
    static K<Eff<TestRuntime>, DatabaseIO> Has<Eff<TestRuntime>, DatabaseIO>.Ask =>
        liftEff(_ => new TestDatabaseIO());
}

// Use in tests
var result = await TodoService<Eff<TestRuntime>, TestRuntime>.Get(1)
    .RunAsync(new TestRuntime(), EnvIO.New());
```

## Next Steps

To enhance this application, you could:

1. **Add More Capabilities**
   - CachingIO - In-memory or distributed caching
   - MetricsIO - Application metrics and telemetry
   - EmailIO - Email notifications
   - HttpClientIO - External API calls

2. **Add Effect Extensions**
   - Transaction support
   - Retry logic with exponential backoff
   - Circuit breaker pattern
   - Rate limiting

3. **Add Features**
   - User authentication and authorization
   - Todo categories/tags
   - Due dates and reminders
   - Priority levels
   - Search and filtering

4. **Add Testing**
   - Unit tests with test runtime
   - Integration tests with test database
   - Property-based tests with FsCheck
   - API tests with WebApplicationFactory

5. **Add Documentation**
   - Swagger/OpenAPI annotations
   - XML documentation comments
   - Architecture decision records

## Database

The application uses SQLite with a file named `todos.db` created in the application directory. The database is automatically created and migrated on first run.

## Resources

- [language-ext Documentation](https://github.com/louthy/language-ext)
- [functional-backend-guide.md](../functional-backend-guide.md) - Comprehensive guide to this architecture
- [Functional Programming in C#](https://github.com/louthy/language-ext/wiki)

## Conclusion

This Todo API demonstrates how to build real-world ASP.NET Core applications using modern functional programming patterns with language-ext v5. The trait-based Has<M, RT, T>.ask pattern provides:

- **Simpler architecture** - No custom monads needed
- **Better composability** - Generic over M and RT
- **Easier testing** - Swap trait implementations
- **Type safety** - Compiler-enforced correctness
- **Production ready** - Async/await with proper error handling

The result is code that is more maintainable, testable, and easier to reason about than traditional imperative approaches!

## License

MIT
