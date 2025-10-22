# บทที่ 5: สร้าง Backend API ด้วย Capabilities

> ลงมือสร้าง RESTful API แบบเต็มรูปแบบด้วย language-ext v5

---

## เนื้อหาในบทนี้

- ภาพรวม Todo Application
- Project Setup
- Domain Layer - Todo Entity
- Infrastructure Layer - Capabilities ทั้งหมด
- Feature Layer - TodoService
- API Layer - ASP.NET Core Endpoints
- การรันและทดสอบ API
- Error Handling
- Best Practices
- แบบฝึกหัด

---

## 5.1 ภาพรวม Todo Application

เราจะสร้าง **RESTful API** สำหรับจัดการ Todo list โดยใช้ **Functional Programming** ทั้งระบบ

### Features

**CRUD Operations:**
- **GET /todos** - List all todos
- **GET /todos/{id}** - Get single todo
- **POST /todos** - Create new todo
- **PUT /todos/{id}** - Update todo
- **PATCH /todos/{id}/toggle** - Toggle completion
- **DELETE /todos/{id}** - Delete todo

### Technology Stack

**Backend:**
- ASP.NET Core 8.0 (Minimal APIs)
- language-ext v5.0.0-beta-54
- Entity Framework Core 9.0
- SQLite

### Architecture

```
TodoApp/
├── Domain/
│   └── Todo.cs                    # Domain entity
├── Data/
│   └── AppDbContext.cs            # EF Core context
├── Infrastructure/
│   ├── Traits/                    # Capability interfaces
│   │   ├── DatabaseIO.cs
│   │   ├── LoggerIO.cs
│   │   └── TimeIO.cs
│   ├── Live/                      # Production implementations
│   │   ├── LiveDatabaseIO.cs
│   │   ├── LiveLoggerIO.cs
│   │   └── LiveTimeIO.cs
│   ├── Capabilities/              # Capability modules
│   │   ├── Database.cs
│   │   ├── Logger.cs
│   │   └── Time.cs
│   ├── Extensions/                # Extension methods
│   │   ├── OptionExtensions.cs
│   │   └── ValidationExtensions.cs
│   └── AppRuntime.cs              # Runtime with capabilities
├── Features/
│   └── Todos/
│       ├── TodoService.cs         # Business logic
│       ├── TodoValidation.cs      # Validation rules
│       └── TodoDtos.cs            # DTOs
└── Program.cs                     # API endpoints & DI setup
```

---

## 5.2 Project Setup

### 5.2.1 สร้าง Project

```bash
# สร้าง ASP.NET Core Web API project
dotnet new web -n TodoApp
cd TodoApp

# ติดตั้ง packages
dotnet add package LanguageExt.Core --version 5.0.0-beta-54
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0
```

### 5.2.2 สร้าง Folder Structure

```bash
mkdir -p Domain
mkdir -p Data
mkdir -p Infrastructure/Traits
mkdir -p Infrastructure/Live
mkdir -p Infrastructure/Capabilities
mkdir -p Infrastructure/Extensions
mkdir -p Features/Todos
```

---

## 5.3 Domain Layer

### 5.3.1 Todo Entity

Domain entity ต้องเป็น **immutable** - ใช้ `record` ของ C#:

```csharp
// Domain/Todo.cs
using LanguageExt;

namespace TodoApp.Domain;

/// <summary>
/// Todo domain entity - immutable record.
/// All properties use 'init' for immutability.
/// </summary>
public record Todo
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
```

**Key Points:**
- ✅ `record` - Immutable by default
- ✅ `init` properties - Set เฉพาะตอน construction
- ✅ `with` expression - สร้าง copy พร้อมเปลี่ยนค่า
- ✅ ไม่มี setters - ป้องกัน mutation

**ตัวอย่างการใช้ `with`:**

```csharp
var todo = new Todo
{
    Id = 1,
    Title = "Learn FP",
    IsCompleted = false
};

// สร้าง copy ใหม่พร้อมเปลี่ยน IsCompleted
var completed = todo with { IsCompleted = true };

// todo เดิมไม่เปลี่ยน!
Console.WriteLine(todo.IsCompleted);       // false
Console.WriteLine(completed.IsCompleted);  // true
```

### 5.3.2 Database Context

```csharp
// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain;

namespace TodoApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.CompletedAt);
        });
    }
}
```

---

## 5.4 Infrastructure Layer

### 5.4.1 DatabaseIO Capability

**Trait Interface:**

```csharp
// Infrastructure/Traits/DatabaseIO.cs
using LanguageExt;
using TodoApp.Domain;

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// DatabaseIO capability - defines database operations.
/// Methods return Unit instead of void for better composition.
/// </summary>
public interface DatabaseIO
{
    // Query operations
    Option<Todo> GetTodoById(int id);
    List<Todo> GetAllTodos();

    // Command operations
    Todo AddTodo(Todo todo);
    Todo UpdateTodo(Todo todo);
    Unit DeleteTodo(Todo todo);
    Unit SaveChanges();
}
```

**Live Implementation:**

```csharp
// Infrastructure/Live/LiveDatabaseIO.cs
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

public class LiveDatabaseIO : DatabaseIO
{
    private readonly IServiceProvider _services;

    public LiveDatabaseIO(IServiceProvider services)
    {
        _services = services;
    }

    private AppDbContext GetContext() =>
        _services.GetRequiredService<AppDbContext>();

    public Option<Todo> GetTodoById(int id)
    {
        var ctx = GetContext();
        var todo = ctx.Todos.Find(id);
        return Optional(todo);
    }

    public List<Todo> GetAllTodos()
    {
        var ctx = GetContext();
        return ctx.Todos
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public Todo AddTodo(Todo todo)
    {
        var ctx = GetContext();
        ctx.Todos.Add(todo);
        return todo;
    }

    public Todo UpdateTodo(Todo todo)
    {
        var ctx = GetContext();
        ctx.Todos.Update(todo);
        return todo;
    }

    public Unit DeleteTodo(Todo todo)
    {
        var ctx = GetContext();
        ctx.Todos.Remove(todo);
        return Unit.Default;
    }

    public Unit SaveChanges()
    {
        var ctx = GetContext();
        ctx.SaveChanges();
        return Unit.Default;
    }
}
```

**Capability Module:**

```csharp
// Infrastructure/Capabilities/Database.cs
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

public static class Database<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    public static K<M, Option<Todo>> getTodoById(int id) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from todo in M.LiftIO(IO.lift(_ => db.GetTodoById(id)))
        select todo;

    public static K<M, List<Todo>> getAllTodos() =>
        from db in Has<M, RT, DatabaseIO>.ask
        from todos in M.LiftIO(IO.lift(_ => db.GetAllTodos()))
        select todos;

    public static K<M, Todo> addTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from added in M.LiftIO(IO.lift(_ => db.AddTodo(todo)))
        from _ in M.LiftIO(IO.lift(_ => db.SaveChanges()))
        select added;

    public static K<M, Todo> updateTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from updated in M.LiftIO(IO.lift(_ => db.UpdateTodo(todo)))
        from _ in M.LiftIO(IO.lift(_ => db.SaveChanges()))
        select updated;

    public static K<M, Unit> deleteTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from _ in M.LiftIO(IO.lift(_ => db.DeleteTodo(todo)))
        from __ in M.LiftIO(IO.lift(_ => db.SaveChanges()))
        select Unit.Default;
}
```

### 5.4.2 LoggerIO Capability

**Trait Interface:**

```csharp
// Infrastructure/Traits/LoggerIO.cs
using LanguageExt;

namespace TodoApp.Infrastructure.Traits;

public interface LoggerIO
{
    Unit LogInfo(string message);
    Unit LogWarning(string message);
    Unit LogError(string message);
}
```

**Live Implementation:**

```csharp
// Infrastructure/Live/LiveLoggerIO.cs
using LanguageExt;
using Microsoft.Extensions.Logging;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

public class LiveLoggerIO : LoggerIO
{
    private readonly ILogger _logger;

    public LiveLoggerIO(ILogger logger)
    {
        _logger = logger;
    }

    public Unit LogInfo(string message)
    {
        _logger.LogInformation(message);
        return Unit.Default;
    }

    public Unit LogWarning(string message)
    {
        _logger.LogWarning(message);
        return Unit.Default;
    }

    public Unit LogError(string message)
    {
        _logger.LogError(message);
        return Unit.Default;
    }
}
```

**Capability Module:**

```csharp
// Infrastructure/Capabilities/Logger.cs
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

public static class Logger<M, RT>
    where M : Monad<M>
    where RT : Has<M, LoggerIO>
{
    public static K<M, Unit> logInfo(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogInfo(message);

    public static K<M, Unit> logWarning(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogWarning(message);

    public static K<M, Unit> logError(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogError(message);
}
```

### 5.4.3 TimeIO Capability

**Trait Interface:**

```csharp
// Infrastructure/Traits/TimeIO.cs
namespace TodoApp.Infrastructure.Traits;

public interface TimeIO
{
    DateTime UtcNow();
}
```

**Live Implementation:**

```csharp
// Infrastructure/Live/LiveTimeIO.cs
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

public class LiveTimeIO : TimeIO
{
    public DateTime UtcNow() => DateTime.UtcNow;
}
```

**Capability Module:**

```csharp
// Infrastructure/Capabilities/Time.cs
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

public static class Time<M, RT>
    where M : Monad<M>
    where RT : Has<M, TimeIO>
{
    public static K<M, DateTime> UtcNow =>
        from time in Has<M, RT, TimeIO>.ask
        select time.UtcNow();
}
```

### 5.4.4 Extension Methods

**OptionExtensions:**

```csharp
// Infrastructure/Extensions/OptionExtensions.cs
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

public static class OptionExtensions
{
    public static K<M, A> To<M, A>(
        this Option<A> option,
        Func<Error> errorFactory)
        where M : Monad<M>, Fallible<M>
    {
        return option.Match(
            Some: value => M.Pure(value),
            None: () => M.Fail<A>(errorFactory())
        );
    }

    public static K<M, A> To<M, A>(
        this Option<A> option,
        string errorMessage)
        where M : Monad<M>, Fallible<M>
    {
        return option.To<M, A>(() => Error.New(errorMessage));
    }
}
```

**ValidationExtensions:**

```csharp
// Infrastructure/Extensions/ValidationExtensions.cs
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

public static class ValidationExtensions
{
    public static K<M, A> To<M, A>(
        this Validation<Error, A> validation)
        where M : Monad<M>, Fallible<M>
    {
        return validation.Match(
            Succ: value => M.Pure(value),
            Fail: errors => M.Fail<A>(errors.Head)
        );
    }
}
```

### 5.4.5 AppRuntime

```csharp
// Infrastructure/AppRuntime.cs
using LanguageExt;
using LanguageExt.Effects;
using LanguageExt.Traits;
using Microsoft.Extensions.Logging;
using TodoApp.Infrastructure.Live;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure;

/// <summary>
/// Application runtime that provides all capabilities.
/// Implements Has<Eff<AppRuntime>, T> for each capability.
/// </summary>
public record AppRuntime(IServiceProvider Services) :
    Has<Eff<AppRuntime>, DatabaseIO>,
    Has<Eff<AppRuntime>, LoggerIO>,
    Has<Eff<AppRuntime>, TimeIO>
{
    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt =>
            new LiveDatabaseIO(rt.Services)));

    static K<Eff<AppRuntime>, LoggerIO> Has<Eff<AppRuntime>, LoggerIO>.Ask =>
        liftEff((Func<AppRuntime, LoggerIO>)(rt =>
            new LiveLoggerIO(
                rt.Services.GetRequiredService<ILogger<AppRuntime>>())));

    static K<Eff<AppRuntime>, TimeIO> Has<Eff<AppRuntime>, TimeIO>.Ask =>
        liftEff((Func<AppRuntime, TimeIO>)(_ =>
            new LiveTimeIO()));
}
```

---

## 5.5 Feature Layer

### 5.5.1 TodoValidation

```csharp
// Features/Todos/TodoValidation.cs
using LanguageExt;
using LanguageExt.Common;
using TodoApp.Domain;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

public static class TodoValidation
{
    public static Validation<Error, Todo> Validate(Todo todo) =>
        (ValidateTitle(todo), ValidateDescription(todo))
            .Apply((_, _) => todo).As();

    private static Validation<Error, Todo> ValidateTitle(Todo todo) =>
        !string.IsNullOrWhiteSpace(todo.Title) && todo.Title.Length <= 200
            ? Success<Error, Todo>(todo)
            : Fail<Error, Todo>(Error.New("Title is required and must be less than 200 characters"));

    private static Validation<Error, Todo> ValidateDescription(Todo todo) =>
        todo.Description == null || todo.Description.Length <= 1000
            ? Success<Error, Todo>(todo)
            : Fail<Error, Todo>(Error.New("Description must be less than 1000 characters"));
}
```

**Applicative Validation - ทำไมใช้ `Apply`?**

```csharp
// ✅ Applicative - รวบรวม errors ทั้งหมด
(ValidateTitle(todo), ValidateDescription(todo))
    .Apply((_, _) => todo).As();

// ถ้า title และ description ผิดทั้งคู่
// จะได้ errors 2 ตัว: ["Title is required...", "Description must be..."]

// ❌ Monadic - หยุดที่ error แรก
from _ in ValidateTitle(todo)
from __ in ValidateDescription(todo)
select todo;

// ถ้า title ผิด จะได้แค่ error ของ title
// ไม่ได้เช็ค description เลย!
```

### 5.5.2 TodoDtos

```csharp
// Features/Todos/TodoDtos.cs
namespace TodoApp.Features.Todos;

public record CreateTodoRequest(string Title, string? Description);

public record UpdateTodoRequest(string Title, string? Description);

public record TodoResponse(
    int Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
```

### 5.5.3 TodoService

```csharp
// Features/Todos/TodoService.cs
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using TodoApp.Domain;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Extensions;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

/// <summary>
/// TodoService - all business logic using capabilities.
/// Generic over M (monad) and RT (runtime).
/// </summary>
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.getAllTodos()
        from __ in Logger<M, RT>.logInfo($"Found {todos.Count} todos")
        select todos;

    public static K<M, Todo> Get(int id) =>
        from _ in Logger<M, RT>.logInfo($"Getting todo {id}")
        from todoOpt in Database<M, RT>.getTodoById(id)
        from todo in todoOpt.To<M, Todo>(() => Error.New(404, $"Todo {id} not found"))
        from __ in Logger<M, RT>.logInfo($"Found todo: {todo.Title}")
        select todo;

    public static K<M, Todo> Create(string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from now in Time<M, RT>.UtcNow
        from newTodo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = now
        })
        from validated in TodoValidation.Validate(newTodo).To<M, Todo>()
        from saved in Database<M, RT>.addTodo(validated)
        from __ in Logger<M, RT>.logInfo($"Created todo ID: {saved.Id}")
        select saved;

    public static K<M, Todo> Update(int id, string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Updating todo {id}")
        from existing in Get(id)
        from updated in M.Pure(existing with
        {
            Title = title,
            Description = description
        })
        from validated in TodoValidation.Validate(updated).To<M, Todo>()
        from saved in Database<M, RT>.updateTodo(validated)
        from __ in Logger<M, RT>.logInfo($"Updated todo {id}")
        select saved;

    public static K<M, Todo> ToggleComplete(int id) =>
        from _ in Logger<M, RT>.logInfo($"Toggling todo {id}")
        from existing in Get(id)
        from now in Time<M, RT>.UtcNow
        from toggled in M.Pure(existing with
        {
            IsCompleted = !existing.IsCompleted,
            CompletedAt = !existing.IsCompleted ? now : null
        })
        from updated in Database<M, RT>.updateTodo(toggled)
        from __ in Logger<M, RT>.logInfo(
            $"Todo {id} marked as {(updated.IsCompleted ? "completed" : "incomplete")}")
        select updated;

    public static K<M, Unit> Delete(int id) =>
        from _ in Logger<M, RT>.logInfo($"Deleting todo {id}")
        from existing in Get(id)
        from __ in Database<M, RT>.deleteTodo(existing)
        from ___ in Logger<M, RT>.logInfo($"Deleted todo {id}")
        select unit;
}
```

---

## 5.6 API Layer

### 5.6.1 Program.cs - Setup

```csharp
// Program.cs
using LanguageExt;
using LanguageExt.Effects;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Features.Todos;
using TodoApp.Infrastructure;
using static LanguageExt.Prelude;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=todos.db"));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Helper to convert Fin<A> to IResult
static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess) =>
    result.Match(
        Succ: onSuccess,
        Fail: error =>
        {
            var code = error.Code == 404 ? 404 : 500;
            return Results.Problem(
                detail: error.Message,
                statusCode: code);
        }
    );

// Mapper
static TodoResponse MapToResponse(Todo todo) => new(
    todo.Id,
    todo.Title,
    todo.Description,
    todo.IsCompleted,
    todo.CreatedAt,
    todo.CompletedAt
);
```

### 5.6.2 API Endpoints

```csharp
// GET /todos - List all
app.MapGet("/todos", async (IServiceProvider services, CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .List()
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todos =>
        Results.Ok(todos.Select(MapToResponse)));
});

// GET /todos/{id} - Get single
app.MapGet("/todos/{id:int}", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Get(id)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// POST /todos - Create
app.MapPost("/todos", async (
    CreateTodoRequest request,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Create(request.Title, request.Description)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo =>
        Results.Created($"/todos/{todo.Id}", MapToResponse(todo)));
});

// PUT /todos/{id} - Update
app.MapPut("/todos/{id:int}", async (
    int id,
    UpdateTodoRequest request,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Update(id, request.Title, request.Description)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// PATCH /todos/{id}/toggle - Toggle completion
app.MapPatch("/todos/{id:int}/toggle", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .ToggleComplete(id)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// DELETE /todos/{id} - Delete
app.MapDelete("/todos/{id:int}", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Delete(id)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, _ => Results.NoContent());
});

app.Run();
```

---

## 5.7 การรันและทดสอบ

### 5.7.1 รัน Application

```bash
dotnet restore
dotnet build
dotnet run
```

Server จะรันที่ `http://localhost:5000`

### 5.7.2 ทดสอบด้วย curl

**Create Todo:**

```bash
curl -X POST http://localhost:5000/todos \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Learn Functional Programming",
    "description": "Study language-ext v5"
  }'
```

**Response:**
```json
{
  "id": 1,
  "title": "Learn Functional Programming",
  "description": "Study language-ext v5",
  "isCompleted": false,
  "createdAt": "2024-01-15T10:30:00Z",
  "completedAt": null
}
```

**List Todos:**

```bash
curl http://localhost:5000/todos
```

**Get Single Todo:**

```bash
curl http://localhost:5000/todos/1
```

**Update Todo:**

```bash
curl -X PUT http://localhost:5000/todos/1 \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Master Functional Programming",
    "description": "Deep dive into language-ext v5"
  }'
```

**Toggle Completion:**

```bash
curl -X PATCH http://localhost:5000/todos/1/toggle
```

**Delete Todo:**

```bash
curl -X DELETE http://localhost:5000/todos/1
```

---

## 5.8 Error Handling

### 5.8.1 Not Found (404)

```csharp
// Get non-existent todo
curl http://localhost:5000/todos/999
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "An error occurred",
  "status": 404,
  "detail": "Todo 999 not found"
}
```

### 5.8.2 Validation Error (500)

```csharp
// Create todo with invalid title
curl -X POST http://localhost:5000/todos \
  -H "Content-Type: application/json" \
  -d '{"title": "", "description": "Test"}'
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred",
  "status": 500,
  "detail": "Title is required and must be less than 200 characters"
}
```

**การจัดการ Error:**

```csharp
from todoOpt in Database<M, RT>.getTodoById(id)
from todo in todoOpt.To<M, Todo>(() =>
    Error.New(404, $"Todo {id} not found"))  // ← Error with code
```

```csharp
static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess) =>
    result.Match(
        Succ: onSuccess,
        Fail: error =>
        {
            var code = error.Code == 404 ? 404 : 500;  // ← เช็ค error code
            return Results.Problem(
                detail: error.Message,
                statusCode: code);
        }
    );
```

---

## 5.9 Best Practices

### 5.9.1 Separate Layers

**❌ ห้ามทำ:**
```csharp
// Service ใช้ DbContext โดยตรง
public static K<M, Todo> Get<M, RT>(int id)
{
    var db = _services.GetRequiredService<AppDbContext>();  // ❌ ห้าม!
    var todo = db.Todos.Find(id);
    return M.Pure(todo);
}
```

**✅ ทำแบบนี้:**
```csharp
// Service ใช้ผ่าน capability
public static K<M, Todo> Get<M, RT>(int id)
    where RT : Has<M, DatabaseIO>
{
    return
        from todoOpt in Database<M, RT>.getTodoById(id)
        from todo in todoOpt.To<M, Todo>("Not found")
        select todo;
}
```

### 5.9.2 Pure Business Logic

```csharp
// ✅ Pure function - แยกจาก effects
public static Todo ToggleTodo(Todo todo, DateTime now) =>
    todo with
    {
        IsCompleted = !todo.IsCompleted,
        CompletedAt = !todo.IsCompleted ? now : null
    };

// ✅ Effectful function - ใช้ pure function
public static K<M, Todo> ToggleComplete<M, RT>(int id)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, TimeIO>
{
    return
        from existing in Get(id)
        from now in Time<M, RT>.UtcNow
        from toggled in M.Pure(ToggleTodo(existing, now))  // ← Pure!
        from updated in Database<M, RT>.updateTodo(toggled)
        select updated;
}
```

### 5.9.3 Validation

```csharp
// ✅ Applicative validation - รวบรวม errors ทั้งหมด
(ValidateTitle(todo), ValidateDescription(todo))
    .Apply((_, _) => todo).As();

// ❌ Monadic - หยุดที่ error แรก
from _ in ValidateTitle(todo)
from __ in ValidateDescription(todo)
select todo;
```

### 5.9.4 Logging

```csharp
// ✅ Log ทุกขั้นตอนสำคัญ
public static K<M, Todo> Create<M, RT>(...)
{
    return
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from validated in Validate(...)
        from saved in Save(validated)
        from __ in Logger<M, RT>.logInfo($"Created todo ID: {saved.Id}")
        select saved;
}
```

---

## 5.10 สรุป

### สิ่งที่เรียนรู้ในบทนี้

1. **Project Structure** - Three-layer architecture
2. **Domain Layer** - Immutable entities with records
3. **Infrastructure Layer** - Traits, Live implementations, Capabilities
4. **Feature Layer** - TodoService, TodoValidation, TodoDtos
5. **API Layer** - ASP.NET Core Minimal APIs
6. **Error Handling** - Type-safe with Fin<A>
7. **Testing** - curl commands
8. **Best Practices** - Separate layers, pure functions, validation

### Key Patterns

**1. Trait → Live → Capability → Service**
```
DatabaseIO (trait)
  ↓
LiveDatabaseIO (implementation)
  ↓
Database<M, RT> (capability module)
  ↓
TodoService<M, RT> (business logic)
```

**2. Error Handling**
```
Option<Todo> → .To<M, Todo>(error) → K<M, Todo>
Validation<Error, Todo> → .To<M, Todo>() → K<M, Todo>
```

**3. LINQ Query Syntax**
```csharp
from x in GetX()
from y in GetY(x)
from z in GetZ(x, y)
select Result(x, y, z)
```

---

## 5.11 แบบฝึกหัด

### แบบฝึกหัดที่ 1: เพิ่ม Search Feature

เพิ่ม endpoint `GET /todos/search?q={query}` ที่ค้นหา todos จาก title หรือ description:

**Hints:**
1. เพิ่ม method ใน `DatabaseIO`
2. เพิ่ม method ใน `Database<M, RT>`
3. เพิ่ม method ใน `TodoService<M, RT>`
4. เพิ่ม endpoint ใน `Program.cs`

### แบบฝึกหัดที่ 2: เพิ่ม Pagination

เพิ่ม pagination สำหรับ `GET /todos?page=1&pageSize=10`:

**Hints:**
1. สร้าง `PagedResult<T>` DTO
2. แก้ไข `DatabaseIO.GetAllTodos` ให้รับ page parameters
3. Calculate total pages
4. Return PagedResult

### แบบฝึกหัดที่ 3: เพิ่ม Filtering

เพิ่ม filtering สำหรับ `GET /todos?completed=true`:

**Hints:**
1. เพิ่ม `FilterOptions` DTO
2. แก้ไข query logic ใน `DatabaseIO`
3. Support multiple filters

### แบบฝึกหัดที่ 4: เพิ่ม Metrics Capability

สร้าง `MetricsIO` capability ที่นับจำนวน operations:

**Hints:**
1. สร้าง `MetricsIO` trait
2. สร้าง `LiveMetricsIO` (in-memory counter)
3. เพิ่มใน `AppRuntime`
4. เรียกใช้ใน `TodoService`
5. สร้าง endpoint `GET /metrics` แสดง statistics

---

**บทถัดไป:** [บทที่ 6: Validation และ Error Handling](chapter-06.md)

ในบทถัดไป เราจะเจาะลึก Validation patterns และ Error handling แบบต่างๆ!
